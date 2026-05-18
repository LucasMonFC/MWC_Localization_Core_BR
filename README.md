# MSC Localization Core

MSCLoader localization framework for **My Summer Car**. 

The bundled language pack ships the `.txt` files and optional `fonts.unity3d` under `Mods/Assets/MSC_Localization_Core_BR/`, matching the current mod ID; this DLL loads them and applies translations to TextMesh, PlayMaker FSM strings, teletext arrays, HUD text, rally sheets, computer screens, and selected ArrayList-backed game data.

## Quick Start

### For Language Pack Creators

1. Copy the template files from `dist/`.
2. Edit `dist/Assets/MSC_Localization_Core_BR/config.txt` with your language settings.
3. Update translation files:
   - `translate.txt` - main game/UI/FSM text, including the former MSC-specific entries.
   - `translate_teletext.txt` - teletext database text.
   - `translate_mod.txt` - optional mod text.
4. Optionally create custom fonts in `fonts.unity3d`.
5. Test in-game with F8 reload.

### For Developers

```bash
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ".\MSC_Localization_Core.sln" /p:Configuration=Release
```

The project references My Summer Car managed assemblies from the path configured in `MSC_Localization_Core.csproj`. Update the `<HintPath>` values if your game is installed somewhere else.

## Features

**Automatic Translation** - Scans TextMesh components and replaces text.
**FSM GUI Translation** - Injects lightweight PlayMaker actions for vanilla GUI indicators instead of polling those TextMeshes every frame.
**PlayMaker FSM Hooks** - Patches known FSM action fields and variables used by menus, sheets, rally UI, computer screens, and teletext.
**Teletext Translation** - Translates `VKTekstiTV` database pages through `translate_teletext.txt`, plus runtime weather and rally-day strings on teletext pages that the game rebuilds while the TV is open.
**Long Subtitle Timing** - Applies selected long subtitles at their source FSM/ArrayList and uses the matching audio clip length for subtitle/UI timing.
**Configurable Fonts** - Maps original game fonts to localized custom fonts.
**Non-ASCII Support** - Custom fonts and forced-font paths keep accented text and other non-ASCII glyphs readable on surfaces that the game rebuilds dynamically.
**PNG Texture Replacements** - Replaces loaded material textures with language-pack PNG files by matching the original Unity texture name.
**Position Adjustments** - Fine-tunes text placement per language.
**Live Reload** - Press F8 to reload config, translations, and runtime caches without restarting the game.

## File Structure

```text
dist/
|-- Assets/
|   `-- MSC_Localization_Core_BR/
|       |-- config.txt
|       |-- translate.txt
|       |-- translate_teletext.txt
|       |-- translate_mod.txt
|       |-- fonts.unity3d
|       `-- textures/
`-- MSC_Localization_Core.dll
```

At runtime, MSCLoader resolves the assets folder from the mod ID. With the current mod ID, place the assets under:

```text
Mods/Assets/MSC_Localization_Core_BR/
```

## Configuration

### Basic Settings

```ini
LANGUAGE_NAME = Brazilian Portuguese
LANGUAGE_CODE = pt-BR
```

| Setting | Purpose | Example |
|---------|---------|---------|
| `LANGUAGE_NAME` | Display name | `Brazilian Portuguese` |
| `LANGUAGE_CODE` | ISO language code | `pt-BR` |

### Font Mappings

If you use custom fonts, map original game font names to AssetBundle font names:

```ini
[FONTS]
FugazOne-Regular = MyLocalizedFont
Heebo-Black = MyLocalizedFont-Bold
```

Font assets must exist in `fonts.unity3d` with names matching the right side values.

### PNG Texture Replacements

Place replacement PNGs under `Mods/Assets/MSC_Localization_Core_BR/textures/`. The file name must match the original Unity texture object name, without extension:

```text
Mods/Assets/MSC_Localization_Core_BR/textures/my_original_texture.png
```

The loader scans material texture slots such as `_MainTex`, `_BumpMap`, `_EmissionMap`, and related Unity shader properties. It also handles `ScreenOverlay` textures such as camera helmet overlays. It runs on GAME scene load and again on F8 reload. The menu-only `drivers_lincence.png` replacement is applied during MainMenu load instead.

## Translation Files

### translate.txt

Main translation file for UI, TextMesh scans, FSM hook source strings, HUD text, computer text, rally sheets, and dynamic strings handled by the shared translation dictionary.

```ini
# Comments use # or //
# Keys are normalized internally: uppercase and whitespace-stripped

MONDAY = SEGUNDA-FEIRA
CONNECTION CLOSED = CONEXAO ENCERRADA
WELCOME TO YOUR NEW LIFE = BEM-VINDO A SUA NOVA VIDA

# Multiline support
RALLY RESULTS = RESULTADOS DO RALLY
```

Use `\=` when a source or translation contains a literal equals sign, and `\n` for new lines.
Pattern entries with placeholders such as `{0}` and `{1}` are also supported for game-generated text that contains dynamic numbers or names. Single-slot `{0}` entries also provide their static pieces to FSM text hooks when the game builds a sentence in parts.
```ini
Overspeeding. {0}km/h at {1}km/h vehicle limit. = Excesso de velocidade. {0} km/h em zona de {1} km/h.
```
`translate_msc.txt` is no longer loaded in the MSC port; keep those entries in `translate.txt`. `translate_magazine.txt` is also not loaded because MSC does not have the MWC magazine surface.

### translate_teletext.txt
Category-based translations for the teletext database. Some categories are index-ordered, so keep section order and entry order aligned with the source data. This separate file is used because the game rebuilds some teletext content while the TV is open.

**Order and category names matter.** Keep the `[category]` headers and entry order aligned with the source pages unless you are intentionally updating the teletext database mapping. TV chat sections are intentionally not supported in the MSC port.

```ini
[day]
MONDAY = SEGUNDA-FEIRA
TUESDAY = TERCA-FEIRA

[kotimaa]
Original headline
=
Manchete traduzida
```

For multiline teletext entries, keep the `=` separator on its own line. The translated block can use more lines than the original, but should still be written to fit the TV page width and the normal teletext body area.

Current MSC teletext sections:

| Section     | Content       |
|-------------|---------------|
| `day`       | Day names     |
| `kotimaa`   | Domestic news |
| `ulkomaat`  | Foreign news  |
| `talous`    | Economy news  |
| `urheilu`   | Sports news   |
| `ruoka`     | Recipes       |
| `ajatus`    | Quotes        |
| `kulttuuri` | Culture       |

Some short dynamic strings on teletext pages, such as page 188 weather conditions and page 250 rally day labels, are handled through `FsmTextHook` rules while still using translations from the shared dictionary.

### translate_mod.txt

Optional extra translations for modded content. It uses the same `key = value` format as `translate.txt`.

## Removed MWC Surfaces

The MSC port does not load or maintain the old MWC-only magazine, VIN plate, ATM/payment, Fleetari payment breakdown, TV chat, or PostSystem keyword hash-table translation paths.

## Text Adjustments

Use `[POSITION_ADJUSTMENTS]` in `config.txt` to fine-tune position, font size, line spacing, and width scale for matched TextMesh paths. Rules are matched against full GameObject paths.

### Configuration

```ini
[POSITION_ADJUSTMENTS]
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0
Contains(Sheets/RallyResults) = 0,0,0,0.08,0.9,1.1
```

### Condition Syntax

Combine multiple conditions with `&`. Prefix a condition with `!` to negate it.

| Condition                | Matches When                                                       |
|--------------------------|--------------------------------------------------------------------|
| `Contains(path)`         | Path contains text                                                 |
| `EndsWith(path)`         | Path ends with text                                                |
| `StartsWith(path)`       | Path starts with text                                              |
| `Equals(path)`           | Path exactly matches                                               |
| `GameObjectEquals(path)` | Applies the offset to the matched GameObject instead of a TextMesh |
| `!Contains(path)`        | Path does not contain text                                         |

Use the MSCLoader console/debug options or an object inspection tool to find GameObject paths while tuning rules.

### Examples

```ini
# Position adjustment only: shift HUD labels down
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0

# Make text wider: scale width to 1.2x
Contains(YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS) = 0,0,0,,,1.2

# Full adjustment: position + size + line spacing + width
Contains(Sheets/RallyResults) = 0,0,0,0.08,0.9,1.1

# Skip font size and line spacing, only changing width
Contains(Systems/Teletext/) & EndsWith(/Text) = 0,0,0,,,0.95

# Move a whole GameObject once
GameObjectEquals(Sheets/RallyResults) = 0,0.05,0

# Combine multiple conditions with negation
Contains(GUI/Indicators/) & !Contains(/Shadow) = 0,0,0,0.12
```

### Parameter Format

Format:

```text
Conditions = X,Y,Z[,FontSize,LineSpacing,WidthScale]
```

| Parameter     | Type     | Purpose                  | Example Values       |
|---------------|----------|--------------------------|----------------------|
| `X`           | Required | Horizontal offset        | `0`, `0.5`, `-0.3`   |
| `Y`           | Required | Vertical offset          | `0`, `0.25`, `-0.05` |
| `Z`           | Required | Depth offset             | `0`                  |
| `FontSize`    | Optional | `TextMesh.characterSize` | `0.1`, `0.15`, `0.2` |
| `LineSpacing` | Optional | `TextMesh.lineSpacing`   | `1.0`, `1.2`, `0.8`  |
| `WidthScale`  | Optional | `transform.localScale.x` | `1.0`, `1.2`, `0.8`  |

Leave optional parameters empty to skip them, for example `0,0,0,,1.2`.

Tips:

- Use `WidthScale > 1.0` to make text wider.
- Use `WidthScale < 1.0` to make text narrower.
- Combine `FontSize` and `WidthScale` when a translated string fits vertically but not horizontally.
- `GameObjectEquals` only uses the `X,Y,Z` offset; font size, line spacing, and width scale are TextMesh-specific.

## Creating Custom Fonts

For languages requiring accented glyphs, Cyrillic, Japanese, Korean, Chinese, or other custom character coverage:

1. Prepare TrueType (`.ttf`) or OpenType (`.otf`) fonts.
2. Create Unity font assets with a Unity 5.x editor compatible with the game.
3. Build an AssetBundle named `fonts.unity3d`.
4. Make sure each font asset name matches the right side of the `[FONTS]` mapping in `config.txt`.
5. Place `fonts.unity3d` alongside the translation files in `Mods/Assets/MSC_Localization_Core_BR/`.

The bundle build target should match the game platform, normally Windows Standalone.

## Testing & Development

### Live Reload

Press **F8** in-game to reload configuration, translations, fonts, surface caches, and runtime hooks:

- Edit `config.txt`, `translate.txt`, `translate_teletext.txt`, or `translate_mod.txt`.
- Return to the game and press F8.
- Check the console/output log for load counts, missing files, and warnings.

Code changes still need a rebuild, copying the DLL, and restarting/reloading the mod. F8 is for data/config iteration.

### Debug Workflow

1. Enable debug and warning messages in the mod settings.
2. Launch the game and reproduce the text surface you are tuning.
3. Check MSCLoader console output or `output_log.txt` for configuration errors and translation status.
4. Use the object path shown by debug tooling, or inspect the scene with a developer/object toolkit, when writing `[POSITION_ADJUSTMENTS]`.
5. Edit files and press **F8** to test changes without restarting.

### Common Issues

**Text not translating?**

- Make sure the source text exists in the correct file.
- Use `translate.txt` for normal TextMesh/FSM/HUD/computer/sheet text.
- Use `translate_teletext.txt` only for teletext database categories.
- Check that escaped equals signs use `\=` and multiline values use `\n`.
- For dynamic text with numbers or names, prefer a `{0}`/`{1}` pattern entry in `translate.txt`.
- For dynamic FSM text, add or update an `AddTargetRule(...)` entry in `FsmTextHook.BuiltInTargets.cs`.

**Wrong font or missing accents?**

- Verify the original font name on the left side of `[FONTS]`.
- Verify the AssetBundle font asset name on the right side of `[FONTS]`.
- Make sure `fonts.unity3d` exists in the mod asset folder.
- Check the console for `Loaded font` messages.

**Text position off?**

- Find the full GameObject path.
- Add or refine a `[POSITION_ADJUSTMENTS]` rule.
- Use F8 reload to test small changes quickly.

**Teletext still shows original short words?**

- Database articles belong in `translate_teletext.txt`.
- Runtime strings rebuilt by FSMs, such as weather conditions and rally day labels, need entries in `translate.txt` plus matching FSM target rules.

## Build

```bash
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" ".\MSC_Localization_Core.sln" /p:Configuration=Release
```

Local verification is build plus loading the mod in My Summer Car. There are no automated tests in this repository.
