# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

## Project

MSCLoader mod for **My Winter Car** (Unity 5.0.0f4 + PlayMaker FSM). Acts as a generic localization framework — community language packs ship the `.txt` files and `fonts.unity3d` under `Mods/Assets/MWC_Localization_Core/`, this DLL applies them.

End-user docs (translation file formats, position adjustment syntax, F8 reload workflow) live in [README.md](README.md). This file covers the runtime architecture and the moving parts that aren't obvious from the file tree.

## Build

```
dotnet build -c Release
```

- Target framework: **.NET 3.5 / Unity Full v3.5** with `LangVersion 13` (newer C# syntax compiles down — but no BCL APIs added after .NET 3.5).
- Assembly references in [MWC_Localization_Core.csproj](MWC_Localization_Core.csproj) are **hard-coded to `D:\SteamLibrary\steamapps\common\My Winter Car\mywintercar_Data\Managed\*.dll`**. Update the `<HintPath>` entries if the game lives elsewhere; the build fails otherwise.
- The `PostBuildEvent` block copies the resulting DLL/PDB into the game's `Mods` folder (and optionally `MSCMODSFOLDER` / `MWCMODSFOLDER` if those MSBuild properties are set). It also invokes `debug.bat` in that folder when present — that's MSCLoader's auto-debug hook. Releases skip the PDB and the debug.bat call.
- No tests, no lint. CI is GitHub Actions only for releases — local verification = build + load into the game.

## Runtime architecture

### MSCLoader lifecycle wiring

[MWC_Localization_Core.cs](MWC_Localization_Core.cs) registers callbacks in `ModSetup()` — that method must stay logic-free. Real work happens in:

| Callback | When | Responsibility |
|---|---|---|
| `Mod_Settings` | once, settings UI build | F8 reload keybind, debug-log toggles |
| `Mod_OnMenuLoad` | each MainMenu enter | Load config + fonts + all `translate_*.txt`, build handlers, translate MainMenu |
| `Mod_PostLoad` | once after GAME loads | Translate scene, init data-source handlers, spawn `MWC_LateUpdateHandler` GameObject |
| `Mod_Update` | every frame | F8 hotkey, scene-change detection (cache clear), initial pass after hot reload — **not** continuous monitoring |

**Important:** `LateUpdate` and `FixedUpdate` declared on a `Mod` subclass do **not** auto-run. Continuous per-frame work is delegated to [LateUpdateHandler.cs](LateUpdateHandler.cs), a `MonoBehaviour` hosted on a created GameObject `MWC_LateUpdateHandler`. This MonoBehaviour is destroyed and recreated on every scene change.

### Why LateUpdate matters

The game rebuilds TextMesh content, FSM string values, and ArrayList contents during its own `Update()`. Patching during MSCLoader's `Update` callback frequently gets overwritten the same frame. `LateUpdateHandler.LateUpdate` runs after all `Update` calls, so monitor/translate work there is stable. The `Mod_Update` callback is reserved for input and scene-transition bookkeeping.

### Translation pipeline

1. **Config** ([LocalizationConfig.cs](LocalizationConfig.cs)) — parses `config.txt`: language metadata, `[FONTS]` mapping (original font name → custom font asset name), and `[POSITION_ADJUSTMENTS]` rules into [TextAdjustment.cs](TextAdjustment.cs) instances.
2. **Translation files** — loaded in this order, later files override earlier:
   - `translate_msc.txt` (optional My Summer Car base)
   - `translate.txt` (main)
   - `translate_mod.txt` (optional mod content)
   - `translate_magazine.txt` → `MagazineTextHandler` (price/phone line formatting, abbreviated keywords)
   - `translate_teletext.txt` → `TeletextHandler` (category-section INI format; some categories are **index-ordered**, not key-matched)
   - All key=value files are normalized by `MLCUtils.FormatUpperKey` (uppercase, strip whitespace). `\=` escapes `=`; `\n` becomes a newline in values.
3. **Pattern translations** — Each translation file is also re-scanned by [PatternMatcher.cs](PatternMatcher.cs). Entries with `{0}`/`{1}` placeholders become `TranslationPattern`s ([TranslationMode.cs](TranslationMode.cs)): `FsmPattern` (literal replacement), `FsmPatternWithTranslation` (extracted params translated through the dictionary), `CustomHandler` (code-only).

### Where translations get applied

Three different paths exist because the game stores text in three different ways:

| Surface | Component | Mechanism |
|---|---|---|
| Static GameObject `TextMesh.text` | `TextMeshTranslator` ([TextMeshTranslator.cs](TextMeshTranslator.cs)) | Dictionary lookup → pattern fallback → multi-line lookup. Applies custom font + `TextAdjustment` after replacing text. |
| `PlayMakerArrayListProxy._arrayList` (teletext, HUD day names, magazine keyword pools, ATM/repair line buffers) | `TeletextHandler`, `ArrayListProxyHandler` ([ArrayListProxyHandler.cs](ArrayListProxyHandler.cs)) | Mutates ArrayList entries in place, on a throttled schedule because most arrays lazy-load when the player opens a screen. |
| `PlayMakerHashTableProxy` (`KeywordsFI`/`KeywordsEN` magazine keyword tables) | `HashTableProxyHandler` | Updates live hashtable + snapshot + `preFillStringList` via reflection. |
| FSM action fields, `FsmString` variables, `BuildString` parts, `SetProperty` `StringParameter` | `FsmTextHook` ([FsmTextHook.cs](FsmTextHook.cs)) + hardcoded targets in [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) | Per-rule reflection walk. Each target is `(objectPath, fsmName, stateName, actionIndex)` or `WholeFsm` (all of `stateName=""`, `actionIndex=-1`). |
| HUD primary→shadow paired meshes (Interaction, PartName, Subtitles) | `UnifiedTextMeshMonitor` ([UnifiedTextMeshMonitor.cs](UnifiedTextMeshMonitor.cs)) | Translate the primary mesh, copy resulting text to shadows. |

`Mod_OnMenuLoad` / `Mod_PostLoad` run a one-shot pass over every `TextMesh` (`MLCUtils.GetAllTextMeshesIncludingInactive`) and over every hardcoded array/hashtable/FSM source. `LateUpdateHandler` then drives a throttled loop on the four heavy sources (`ARRAY_MONITOR_STEP_INTERVAL` = `ARRAY_MONITOR_INTERVAL / 4` from [LocalizationConstants.cs](LocalizationConstants.cs)) and a faster FSM-source poll for the small dynamic ones.

### Hot reload (F8)

`ReloadTranslations()` in [MWC_Localization_Core.cs](MWC_Localization_Core.cs) is the canonical "reset everything" path. When adding new state to a handler, mirror it there: clear caches, reload from disk, re-initialize, and trigger a re-translate pass. Existing handlers expose `ClearTranslations()` / `Reset()` / `ClearRuntimeCaches()` for this.

### Caching invariants

- `MLCUtils` caches `GameObject` paths (up to 10k entries), `GameObject.Find` lookups, and a Resources-based FSM index for inactive objects.
- `TextMeshTranslator` caches per-instance applied font and font-bundle texture to skip redundant assignment each frame.
- `FsmTextHook` caches resolved `PlayMakerFSM`s, ArrayList proxies, TextMeshes per target plus a per-target translation cache keyed on `(targetKey, sourceString)`.
- **Every cache must be cleared on scene change.** `Mod_Update` triggers `MLCUtils.ClearCaches()` plus per-handler clear methods on `sceneManager.UpdateScene` returning `true`. If you add a new long-lived cache, wire it into this clear path.
- Exception: `fontBundle` is intentionally `static` because MSCLoader can reconstruct the `Mod` instance during a session — reloading the AssetBundle leaks Unity assets.

### Drift-tracked paths

The game rewrites the transform of some sheets after we adjust them (e.g. `Sheets/Magazine/Products`). `TextAdjustment.DriftTrackedPathPatterns` is a small **code-side** whitelist of substring matches; meshes whose path matches get re-pinned in `LateUpdate` via `LocalizationConfig.RefreshDriftTrackedAdjustments`. Adding a path is a code change, not a config change.

### Forced-font path prefixes

`ForcedFontPathPrefixes` in `MWC_Localization_Core.cs` lists path roots that get the custom font applied even when the text isn't in the translation dictionary (teletext display, computer POS, unemployment letter, rally/service sheets, TV graphics). New "show foreign font correctly even when text stays original" cases go here.

### Excluded paths

`TextMeshTranslator.ExcludedPath` is the explicit "do not touch" list — stereo bass LCD, VIN plate, custom-color picker buttons, FPS counter. Those have either dynamic numeric content or critical formatting.

## Conventions

- Translation keys (and `FsmTextHook` source strings) flow through `MLCUtils.FormatUpperKey`. When comparing user text against translations, normalize both sides.
- New translation files reload through `ReloadTranslations()` — don't add a separate load path.
- New PlayMaker FSM hooks go in [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) via `AddTargetRule(...)`. Each rule lists a single source string; rules with the same `(objectPath, fsmName, stateName, actionIndex)` are grouped automatically and sorted longest-source-first to handle overlapping matches.
- FSM reflection: use [MLCFsmUtils.cs](MLCFsmUtils.cs) helpers (`GetFields`, `GetField`, `SetFsmStringValue`, `SetNestedStringValue`). The FieldInfo cache there matters — uncached reflection during scene scans is visibly expensive.
- Console: use `CoreConsole.Print/Warning/Error` ([CoreConsole.cs](CoreConsole.cs)), not `ModConsole` directly, so the in-game debug toggles work.

## Output layout

The shipping mod is `MWC_Localization_Core.dll` plus the contents of [dist/Assets/MWC_Localization_Core/](dist/Assets/MWC_Localization_Core/) (the Korean reference language pack), which the game expects under `Mods/Assets/MWC_Localization_Core/` at runtime. `ModLoader.GetModAssetsFolder(this)` resolves to that path.
