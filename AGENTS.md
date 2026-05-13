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
| `Mod_OnMenuLoad` | each MainMenu enter | Load config + fonts + main translation files, build the surface list, run initial passes for MainMenu |
| `Mod_PostLoad` | once after GAME loads | Translate scene, run initial passes for GAME, spawn the `MWC_LateUpdateHandler` GameObject |
| `Mod_Update` | every frame | F8 hotkey, scene-change detection (cache prune + LateUpdateHandler teardown), initial pass after hot reload — **not** continuous monitoring |

**Important:** `LateUpdate` and `FixedUpdate` declared on a `Mod` subclass do **not** auto-run. Continuous per-frame work is delegated to [LateUpdateHandler.cs](LateUpdateHandler.cs), a `MonoBehaviour` hosted on a created GameObject `MWC_LateUpdateHandler`. This MonoBehaviour is destroyed and recreated on every scene change.

### Why LateUpdate matters

The game rebuilds TextMesh content, FSM string values, and ArrayList contents during its own `Update()`. Patching during MSCLoader's `Update` callback frequently gets overwritten the same frame. `LateUpdateHandler.LateUpdate` runs after all `Update` calls, so monitor/translate work there is stable. The `Mod_Update` callback is reserved for input and scene-transition bookkeeping.

### Surface abstraction

Every place the mod patches text is modeled as an `ITranslationSurface` ([TranslationSurface.cs](TranslationSurface.cs)). The main class holds a single `List<ITranslationSurface>` and dispatches the lifecycle uniformly:

```csharp
ctx = new TranslationContext(translations, customFonts, config, translator, magazine, assetsFolder);
foreach (s in surfaces) s.Initialize(ctx);
foreach (s in surfaces) s.InitialPass();          // on scene load + F8 reload
// LateUpdateHandler iterates surfaces and calls s.MonitorTick(dt) at each surface's Cadence
foreach (s in surfaces) s.Reset();                // scene change
foreach (s in surfaces) s.ClearTranslations();    // F8: before reloading from disk
```

Each surface declares a `SurfaceCadence` so the scheduler knows how often to tick it:

| Cadence | Interval | Used by |
|---|---|---|
| `PerFrame` | every LateUpdate | `GuiTextMonitor` — HUD primary→shadow mirroring needs to keep up with per-frame text changes |
| `Fast` | `FSM_SOURCE_POLL_INTERVAL` (0.2s) | `FsmTextHook` — small dynamic FSM sources the game rebuilds per screen open |
| `Slow` | `ARRAY_MONITOR_INTERVAL` (2s), staggered | `MagazineTextHandler`, `TeletextHandler`, `ArrayListProxyHandler`, `HashTableProxyHandler` |
| `OncePerScene` | never (only InitialPass) | reserved; no current surfaces |

`LateUpdateHandler.Initialize` offsets the first tick time of consecutive `Slow` surfaces by `ARRAY_MONITOR_STEP_INTERVAL` (0.5s) so they don't all fire on the same frame.

### Translation pipeline

1. **Config** ([LocalizationConfig.cs](LocalizationConfig.cs)) — parses `config.txt`: language metadata, `[FONTS]` mapping (original font name → custom font asset name), and `[POSITION_ADJUSTMENTS]` rules into [TextAdjustment.cs](TextAdjustment.cs) instances. Also hosts `LocalizationConstants` (polling intervals) and `LocalizationConfig.ForcedFontPathPrefixes` / `IsForcedFontPath` (see "Forced-font path prefixes" below).
2. **Translation files** — loaded in this order, later files override earlier:
   - `translate_msc.txt` (optional My Summer Car base) — main class
   - `translate.txt` (main) — main class
   - `translate_mod.txt` (optional mod content) — main class
   - `translate_magazine.txt` → `MagazineTextHandler.Initialize` (price/phone line formatting, abbreviated keywords)
   - `translate_teletext.txt` → `TeletextHandler.Initialize` (category-section INI format; some categories are **index-ordered**, not key-matched). Teletext also feeds FSM patterns into the shared dictionary.
   - All key=value files are normalized by `LocalizationUtils.FormatUpperKey` (uppercase, strip whitespace) at insertion into `TranslationDictionary`. `\=` escapes `=`; `\n` becomes a newline in values.
3. **Pattern translations** — Entries with `{0}`/`{1}` placeholders are detected during pattern-load scans and become `TranslationPattern`s stored inside `TranslationDictionary`. Modes: `FsmPattern` (literal replacement), `FsmPatternWithTranslation` (extracted params translated through the dictionary), `CustomHandler` (code-only delegate).

### Where translations get applied

Each row below is one `ITranslationSurface` implementation. The "Component" column doubles as the file pointer.

| Surface | What it patches | Cadence |
|---|---|---|
| [GuiTextMonitor.cs](GuiTextMonitor.cs) | HUD primary→shadow paired meshes (Interaction, PartName, Subtitles) and HUD value meshes (Day/Money/Thirst/etc.) | PerFrame |
| [MagazineTextHandler.cs](MagazineTextHandler.cs) | Yellow Pages magazine FSM string sources + price/phone line formatting | Slow |
| [TeletextHandler.cs](TeletextHandler.cs) | Teletext/TV `PlayMakerArrayListProxy._arrayList` content with category-based + index-based lookup | Slow |
| [ArrayListProxyHandler.cs](ArrayListProxyHandler.cs) | `PlayMakerArrayListProxy._arrayList` for hardcoded paths (HUD days, magazine keyword pools, tire pics). Also applies fonts to TextMeshes under known parent paths. | Slow |
| [HashTableProxyHandler.cs](HashTableProxyHandler.cs) | `PlayMakerHashTableProxy` (`KeywordsFI`/`KeywordsEN`): live hashtable + snapshot + `preFillStringList` via reflection | Slow |
| [FsmTextHook.cs](FsmTextHook.cs) + [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) | FSM action fields, `FsmString` variables, `BuildString` parts, `SetProperty` `StringParameter`. Each target is `(objectPath, fsmName, stateName, actionIndex)` or `WholeFsm`. | Fast |

`TextMeshTranslator` ([TextMeshTranslator.cs](TextMeshTranslator.cs)) is a **service**, not a surface. Surfaces and the main-class scene scan call it to translate one TextMesh + apply the mapped font + adjustment. Its caches are reset alongside the surfaces on scene change.

`Mod_OnMenuLoad` / `Mod_PostLoad` run a one-shot pass over every `TextMesh` (`LocalizationUtils.GetAllTextMeshesIncludingInactive`) plus the surface `InitialPass` for hardcoded array/hashtable/FSM targets.

### Hot reload (F8)

`ReloadTranslations()` in [MWC_Localization_Core.cs](MWC_Localization_Core.cs) is the canonical "reset everything" path. The full sequence:

1. `translations.Clear()` + `translations.ResetPatterns()` + `foreach surface.ClearTranslations()`
2. `LocalizationUtils.ClearCaches()` + `translator.ClearRuntimeCaches()` + `config.ClearTextAdjustmentCaches()` + `foreach surface.Reset()`
3. Reload config + fonts + main translation files
4. `foreach surface.Initialize(ctx)` — each surface re-loads its own translation file
5. Reset scene flags, reapply fonts to all TextMeshes, run `InitialPass` for the current scene
6. Restart the `LateUpdateHandler` scheduler

When adding new state to a surface, ensure both `Reset()` (runtime caches) and `ClearTranslations()` (owned translation data, if any) cover it.

### Caching invariants

- `LocalizationUtils` caches `GameObject` paths (up to 10k entries), `GameObject.Find` lookups, and a Resources-based FSM index for inactive objects.
- `TextMeshTranslator` caches per-instance applied font and font-bundle texture to skip redundant assignment each frame.
- `TranslationDictionary` keeps a bounded (128-entry) raw-source → translation recent-lookups cache absorbing the per-frame repeated lookups that HUD monitors generate. Cleared on `Clear()` / `ResetPatterns()` / `AddAll()` and bulk-cleared when it fills (not strict LRU).
- `FsmTextHook` caches resolved `PlayMakerFSM`s, ArrayList proxies, TextMeshes per target plus a per-target translation cache keyed on `(targetKey, sourceString)`.
- **Scene change** uses `LocalizationUtils.PruneCaches()`, which drops only entries pointing at destroyed Unity objects (the FSM index is fully rebuilt). Stable HUD paths survive the scene transition cold-start free.
- **F8 reload** uses `LocalizationUtils.ClearCaches()` for a full wipe.
- If you add a new long-lived cache, wire it into the corresponding surface's `Reset()` or, for global utilities, into `ClearCaches`/`PruneCaches` in `LocalizationUtils`.
- Exception: `fontBundle` is intentionally `static` because MSCLoader can reconstruct the `Mod` instance during a session — reloading the AssetBundle leaks Unity assets.

### Drift-tracked paths

The game rewrites the transform of some sheets after we adjust them (e.g. `Sheets/Magazine/Products`). `TextAdjustment.DriftTrackedPathPatterns` is a small **code-side** whitelist of substring matches; meshes whose path matches get re-pinned in `LateUpdate` via `LocalizationConfig.RefreshDriftTrackedAdjustments`. Adding a path is a code change, not a config change.

### Forced-font path prefixes

`LocalizationConfig.ForcedFontPathPrefixes` lists path roots that get the custom font applied even when the text isn't in the translation dictionary (teletext display, computer POS, unemployment letter, rally/service sheets, TV graphics). Check via `LocalizationConfig.IsForcedFontPath(path)`. New "show foreign font correctly even when text stays original" cases go here.

### Excluded paths

`TextMeshTranslator.ExcludedPath` is the explicit "do not touch" list — stereo bass LCD, VIN plate, custom-color picker buttons, FPS counter. Those have either dynamic numeric content or critical formatting.

## Conventions

- Translation lookups go through `TranslationDictionary` ([TranslationDictionary.cs](TranslationDictionary.cs)): `TryGetExact(source, out value)` for direct lookup (with LRU + already-normalized fast path) and `TryMatchPattern(source, path)` for pattern fallback. Don't reach into the underlying `Dictionary<string, string>` directly — normalization + caching happens inside.
- Adding a new translation surface = new class implementing `ITranslationSurface`, then append to the `surfaces` list in `Mod_OnMenuLoad`. No edits to `LateUpdateHandler` or `ReloadTranslations` needed.
- New PlayMaker FSM hooks go in [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) via `AddTargetRule(...)`. Each rule lists a single source string; rules with the same `(objectPath, fsmName, stateName, actionIndex)` are grouped automatically and sorted longest-source-first to handle overlapping matches.
- FSM reflection: use the `FsmUtils` helpers in [LocalizationUtils.cs](LocalizationUtils.cs) (`GetFields`, `GetField`, `SetFsmStringValue`, `SetNestedStringValue`). The FieldInfo cache there matters — uncached reflection during scene scans is visibly expensive.
- Console: use `CoreConsole.Print/Warning/Error` ([CoreConsole.cs](CoreConsole.cs)), not `ModConsole` directly, so the in-game debug toggles work.

## Output layout

The shipping mod is `MWC_Localization_Core.dll` plus the contents of [dist/Assets/MWC_Localization_Core/](dist/Assets/MWC_Localization_Core/) (the Korean reference language pack), which the game expects under `Mods/Assets/MWC_Localization_Core/` at runtime. `ModLoader.GetModAssetsFolder(this)` resolves to that path.
