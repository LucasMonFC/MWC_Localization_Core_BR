# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

## Project

MSCLoader mod for **My Summer Car** (Unity 5.x + PlayMaker FSM). It acts as a localization framework for the MSC port: language packs ship `.txt` files and optional `fonts.unity3d` under the MSCLoader asset folder for the mod ID. The current generic mod ID is `MSC_Localization_Core`, so the runtime asset path is `Mods/Assets/MSC_Localization_Core/`.

End-user docs for translation file formats, position adjustment syntax, and the F8 reload workflow live in [README.md](README.md). This file covers runtime architecture and maintenance notes.

## Build

```bash
dotnet build -c Release
```

- Target framework: **.NET 3.5 / Unity Full v3.5** with `LangVersion 13`.
- Assembly references in [MSC_Localization_Core.csproj](MSC_Localization_Core.csproj) are hard-coded to the local My Summer Car managed assembly path. Update `<HintPath>` entries if the game lives elsewhere.
- The `PostBuildEvent` copies the resulting DLL/PDB into the game's `Mods` folder, and optionally `MSCMODSFOLDER` if that MSBuild property is set. It also invokes `debug.bat` in that folder when present. Release builds skip the PDB and debug hook.
- No tests, no lint. Local verification is build plus loading the mod in-game.

## Runtime Architecture

### MSCLoader lifecycle wiring

[MSC_Localization_Core.cs](MSC_Localization_Core.cs) registers callbacks in `ModSetup()`; that method should stay logic-free. Real work happens in:

| Callback | When | Responsibility |
|---|---|---|
| `Mod_Settings` | once, settings UI build | F8 reload keybind, debug-log toggles |
| `Mod_OnMenuLoad` | each MainMenu enter | Load config + fonts + main translation files, build surfaces, run initial passes for MainMenu |
| `Mod_PostLoad` | once after GAME loads | Translate scene, run initial passes for GAME, spawn the `MSC_LateUpdateHandler` GameObject |
| `Mod_Update` | every frame | F8 hotkey, scene-change detection, cache pruning, LateUpdateHandler teardown |

`LateUpdate` and `FixedUpdate` declared on a `Mod` subclass do not auto-run. Continuous per-frame work is delegated to [LateUpdateHandler.cs](LateUpdateHandler.cs), a `MonoBehaviour` hosted on a created GameObject `MSC_LateUpdateHandler`.

### Why LateUpdate Matters

The game rebuilds TextMesh content, FSM string values, and ArrayList contents during its own `Update()`. Patching during MSCLoader's `Update` callback can be overwritten the same frame. `LateUpdateHandler.LateUpdate` runs after all `Update` calls, so monitor/translate work there is stable.

### Surface Abstraction

Every place the mod patches text is modeled as an `ITranslationSurface` ([TranslationSurface.cs](TranslationSurface.cs)). The main class holds a single `List<ITranslationSurface>` and dispatches lifecycle calls uniformly:

```csharp
ctx = new TranslationContext(translations, customFonts, config, translator, assetsFolder);
foreach (s in surfaces) s.Initialize(ctx);
foreach (s in surfaces) s.InitialPass();
foreach (s in surfaces) s.Reset();
foreach (s in surfaces) s.ClearTranslations();
```

Current surfaces:

| Surface | What it patches | Cadence |
|---|---|---|
| [FsmGuiTranslator.cs](FsmGuiTranslator.cs) | Built-in GUI indicator TextMeshes by injecting translation actions after PlayMaker `SetProperty` writes; also keeps `Partname` layout aligned with multiline subtitles | Slow until injected |
| [FsmArrayTranslator.cs](FsmArrayTranslator.cs) | `Systems/Teletext/VKTekstiTV/Database` ArrayList content through category/index lookup, temporarily activating teletext when needed | Slow until complete |
| [ArrayListProxyHandler.cs](ArrayListProxyHandler.cs) | Hardcoded ArrayList paths such as HUD day names, plus known parent font paths | Slow |
| [FsmTextHook.cs](FsmTextHook.cs) + [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) | FSM action fields, `FsmString` variables, `BuildString` parts, `SetStringValue`, `SetProperty` string parameters, runtime teletext/weather/rally text, and rally result sheet sources | OncePerScene |
| [SubtitleTimingHandler.cs](SubtitleTimingHandler.cs) | Source FSM/ArrayList translation for selected long subtitles and audio-clip-based timing for primary/shadow subtitle meshes | OncePerScene |

`LateUpdateHandler.Initialize` offsets consecutive `Slow` surfaces by `ARRAY_MONITOR_STEP_INTERVAL` so they do not all fire on the same frame.

## Translation Pipeline

1. **Config** ([LocalizationConfig.cs](LocalizationConfig.cs)) parses `config.txt`: language metadata, `[FONTS]` mapping, and `[POSITION_ADJUSTMENTS]`. Forced-font path prefixes and polling constants live in code.
2. **Main translation files** load in this order, with later files overriding earlier files:
   - `translate.txt`
   - `translate_mod.txt`
3. **Teletext** loads `translate_teletext.txt` in [FsmArrayTranslator.cs](FsmArrayTranslator.cs). It translates MSC teletext database arrays by temporarily activating `Systems/Teletext` when needed, preserving category lookup and index fallback. TV chat sections are intentionally not supported for MSC.
4. **FSM/runtime translations** use the same shared dictionary. `FsmTextHook` handles direct FSM action/string assignments and runtime teletext strings such as page 188 weather and page 250 rally day labels.
5. **Pattern translations** are detected from entries with `{0}`/`{1}` placeholders and stored in `TranslationDictionary`; single-slot patterns also derive static piece entries for FSMs that build strings in parts.

All key/value files are normalized by `LocalizationUtils.FormatUpperKey` at insertion into `TranslationDictionary`. Use `\=` for literal equals signs and `\n` for newlines in values.

## Hot Reload

`ReloadTranslations()` in [MSC_Localization_Core.cs](MSC_Localization_Core.cs) is the canonical reset path:

1. Clear dictionaries, patterns, and surface-owned translations.
2. Clear/prune runtime caches and reset surfaces.
3. Reload config, fonts, `translate.txt`, and `translate_mod.txt`.
4. Reinitialize all surfaces; teletext reloads its own file.
5. Reapply fonts/translations to TextMeshes and run `InitialPass`.
6. Restart the `LateUpdateHandler` scheduler.

When adding new state to a surface, wire it into `Reset()` for runtime caches and `ClearTranslations()` for owned translation data.

## Caching Invariants

- `LocalizationUtils` caches GameObject paths, `GameObject.Find` lookups, and a Resources-based FSM index.
- `TextMeshTranslator` caches per-instance applied font and AssetBundle texture.
- `TranslationDictionary` keeps a bounded recent raw-source lookup cache.
- `FsmGuiTranslator` caches injected GUI FSM targets and the shared `Partname` layout helper.
- `FsmTextHook` caches resolved FSMs, ArrayList proxies, TextMeshes, and per-target translation results.
- Scene changes use `LocalizationUtils.PruneCaches()`; F8 reload uses `LocalizationUtils.ClearCaches()`.
- `fontBundle` is intentionally static because MSCLoader can reconstruct the `Mod` instance during a session.

## Porting Notes

- `translate_msc.txt` and `translate_magazine.txt` are not loaded in the MSC port.
- Magazine, MWC VIN plate, TV chat, ATM, Fleetari payment, and PostSystem keyword hash-table logic have been removed.
- Rally result sheets remain supported through FSM hooks and forced-font paths.
- Long subtitle timing rules belong in `SubtitleTimingHandler.cs`; it applies those long translations at the source FSM/ArrayList and uses discovered `AudioClip.length` values for matching source FSM waits and subtitle UI timing. Do not add manual duration fallbacks.
- New PlayMaker FSM hooks belong in [FsmTextHook.BuiltInTargets.cs](FsmTextHook.BuiltInTargets.cs) via `AddTargetRule(...)`.
- Console output should go through `CoreConsole.Print/Warning/Error`, not `ModConsole` directly.

## Output Layout

The shipping mod is `MSC_Localization_Core.dll` plus the contents of [dist/Assets/MSC_Localization_Core/](dist/Assets/MSC_Localization_Core/). At runtime the mod expects those assets under `Mods/Assets/MSC_Localization_Core/`.
