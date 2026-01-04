# My Winter Car Localization Framework - AI Instructions

## Project Context
**Game:** My Winter Car (Unity 5, PlayMaker FSM)  
**Framework:** BepInEx 5.x plugin system  
**Architecture:** Multi-language localization (originally Korean, now extensible)  
**Current Phase:** Phase 1 - Generic Localization Framework (v0.3.0+)

## Core Architecture

### Plugin Design (v0.3.0+)

The plugin is now **language-agnostic** and loads all language-specific settings from external files:

**Core Workflow:**
1. Load `config.txt` - Language metadata, Unicode ranges, font mappings, position adjustments
2. Load `translate.txt` - Main translation dictionary (KEY = Translation)
3. Load `translate_magazine.txt` - Magazine-specific translations
4. Scan TextMesh components matching GameObject paths
5. Apply translations and custom fonts from config
6. Adjust text positions using configurable rules
7. Monitor dynamic UI elements for changes

**Key Files:**
- `Plugin.cs` - Main BepInEx plugin (`LocalizationPlugin` class)
- `LocalizationConfig.cs` - Loads and manages config.txt settings
- `TextMeshTranslator.cs` - Handles translation, fonts, position adjustments
- `TeletextHandler.cs` - Teletext data source translation (runtime array replacement)
- `PositionAdjustment.cs` - Configurable position adjustment system
- `MagazineTextHandler.cs` - Complex text handling (comma-separated, price lines)
- `StringHelper.cs` - String normalization utilities

**Configuration Files (in `l10n_assets/`):**
- `config.txt` - Language-specific settings (currently Korean)
- `translate.txt` - Main translations
- `translate_msc.txt` - My Summer Car compatibility
- `translate_magazine.txt` - Magazine translations
- `translate_teletext.txt` - Teletext/TV data source translations (category-based)
- `fonts.unity3d` - Custom font assets (optional)

## Teletext & Data Source Translation

### Teletext System Architecture

**Challenge:** Teletext content is stored in PlayMaker ArrayLists, not TextMesh components. Standard TextMesh translation doesn't work.

**Solution:** Direct data source manipulation - replace ArrayList contents before game displays them.

### Lazy-Loading Problem

Most teletext arrays are **lazy-loaded** (empty until navigated to):
- `day` - Pre-populated (weekdays)
- `kotimaa`, `ulkomaat`, `talous`, `urheilu`, `ruoka`, `ajatus`, `kulttuuri` - Lazy-loaded (news, recipes, quotes)

**Why Pre-fill Fails:**
```csharp
// ❌ WRONG - Game overwrites preFillStringList
proxy.preFillStringList.AddRange(translations);  // Game ignores this!
```

**Why Runtime Replacement Works:**
```csharp
// ✓ CORRECT - Replace ArrayList AFTER game populates it
if (proxy._arrayList.Count > 0) {  // Game just loaded content
    ArrayList newArrayList = new ArrayList();
    // Fill with translations by index...
    proxy._arrayList = newArrayList;  // Replace entire ArrayList
}
```

### MSC's Approach (Reference: ExtraMod.cs)

**My Summer Car's teletext translation** (lines 2314-2421):
1. **Monitor in FixedUpdate** - Check every frame for array population
2. **Detect population** - `proxy._arrayList.Count > 0`
3. **Create new ArrayList** - Build replacement with translations
4. **Replace entire ArrayList** - `proxy._arrayList = newArrayList`
5. **Track translated arrays** - Prevent re-translation

**Key Insight:** Game reads from `_arrayList` at runtime, not `preFillStringList`.

### Index-Based Translation

Since arrays are lazy-loaded, we can't match by content. Use **index position** instead:

```ini
# translate_teletext.txt format
[kotimaa]
MAKELIN TOIMINNANJOHTAJA EROTETTIIN... = 마켈린 상무이사 해고...
TAKSIUUDISTUS SUUNNITTEILLA... = 택시 개혁안 계획 중...

# Index 0 = first translation, Index 1 = second translation, etc.
# Order MUST match dump order from game
```

**Translation Process:**
```csharp
// Load translations in order
indexBasedTranslations["kotimaa"] = [translation0, translation1, ...]

// When array populates, replace by index
for (int i = 0; i < originalArray.Count; i++) {
    if (i < translations.Count) {
        newArray.Add(translations[i]);  // Replace item at index i
    }
}
```

### TeletextHandler Implementation

**Monitoring Loop (LateUpdate):**
```csharp
int translated = teletextHandler.MonitorAndTranslateArrays();
if (translated > 0) {
    ApplyTeletextFonts();  // Apply Korean font to display
}
```

**Array Replacement:**
```csharp
// TeletextHandler.TranslateArrayListProxy()
ArrayList newArrayList = new ArrayList();
for (int i = 0; i < proxy._arrayList.Count; i++) {
    newArrayList.Add(translations[i]);  // Index-based
}
proxy._arrayList = newArrayList;  // CRITICAL: Replace entire ArrayList
```

**Font Application (Separate Step):**
```csharp
// After translating data, apply fonts to display
GameObject teletextRoot = GameObject.Find("Systems/TV/Teletext/VKTekstiTV/PAGES");
TextMesh[] displays = teletextRoot.GetComponentsInChildren<TextMesh>();
foreach (var tm in displays) {
    translator.ApplyFontOnly(tm, path);  // Font + material only
}
```

### translate_teletext.txt Format

**Category-Based Structure:**
```ini
[day]
MAANANTAI = 월요일
TIISTAI = 화요일
KESKIVIIKKO = 수요일

[kotimaa]
News headline text
With possible multiple lines
=
Translated headline
여러 줄로 된 번역

Single line = Single line translation

[ulkomaat]
...
```

**Multi-line Support:**
```ini
# Multi-line format:
Original text
Line 2
Line 3
=
Translated text
번역 2행
번역 3행

# Newline escape (within single line):
Text with\nnewline = 번역\n줄바꿈
```

**Category Mapping:**
- `day` → `Systems/TV/Teletext/VKTekstiTV/Database[0]`
- `kotimaa` → `Database[1]` (domestic news)
- `ulkomaat` → `Database[2]` (foreign news)
- `talous` → `Database[3]` (economy)
- `urheilu` → `Database[4]` (sports)
- `ruoka` → `Database[5]` (recipes)
- `ajatus` → `Database[6]` (quotes)
- `kulttuuri` → `Database[7]` (culture)

### Performance Considerations

**✅ Efficient:**
- Check every frame (fast - just array count check)
- Translate once when array populates
- Track translated arrays to prevent re-translation
- Skip arrays with no translations

**❌ Avoid:**
- Throttling monitoring (arrays populate for <1 frame window)
- Pre-filling (game overwrites it)
- Key-based matching (arrays are empty until populated)

### Debugging Teletext Issues

**Problem: Text not translating**
- Check logs for `[Monitor]` messages - confirms detection
- Verify translation count matches dump count
- Check index order in translate_teletext.txt

**Problem: Missing Korean font**
- Ensure `ApplyTeletextFonts()` called after translation
- Check `Systems/TV/Teletext/VKTekstiTV/PAGES` path exists
- Verify font bundle loaded correctly

**Problem: Partial translation**
- Some arrays lazy-load - normal behavior
- Monitor logs show translation as you navigate pages
- Check `translatedArrays` HashSet tracking

## Configuration System

### config.txt Structure

```ini
# Language metadata
LANGUAGE_NAME = Korean
LANGUAGE_CODE = ko-KR

# Unicode ranges for character detection (optional - leave empty for Latin languages)
UNICODE_RANGES = AC00-D7AF,1100-11FF,3130-318F

# Font mappings (original game font → custom font asset)
[FONTS]
FugazOne-Regular = NanumSquareRoundEB
Heebo-Black = PaperlogyExtraBold
# ...

# Position adjustments (configurable text positioning)
[POSITION_ADJUSTMENTS]
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0
Contains(PERAPORTTI/ATMs/) & EndsWith(/Text) = 0,0.25,0
```

### Unicode Ranges

**For Non-Latin Languages:**
```ini
# Korean example:
UNICODE_RANGES = AC00-D7AF,1100-11FF,3130-318F

# Japanese example:
UNICODE_RANGES = 3040-309F,30A0-30FF,4E00-9FFF

# Chinese example:
UNICODE_RANGES = 4E00-9FFF,3400-4DBF
```

**For Latin Languages (Spanish, French, German, etc.):**
```ini
# Leave empty or commented - no character detection needed
# UNICODE_RANGES = 
```

### Position Adjustments

Conditions support path matching without code changes:

```ini
# Syntax: Conditions = X,Y,Z offset
Contains(path) & EndsWith(suffix) = 0,-0.05,0

# Condition types:
# - Contains(text) - path contains substring
# - EndsWith(text) - path ends with substring  
# - StartsWith(text) - path starts with substring
# - Equals(text) - path exactly matches
# - !Contains(text) - negation (does NOT contain)

# Examples:
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0
Contains(Screen) & !Contains(/Row) & EndsWith(/Text) = 0,0.25,0
```

## Key Normalization

```csharp
// StringHelper.FormatUpperKey() removes spaces/newlines, converts uppercase
"BEER 149 MK" → "BEER149MK"
"Price Total" → "PRICETOTAL"
"my text" → "MYTEXT"
```

Used consistently for all translation keys to handle game text variations.

## Text Detection System

### Character Detection (Optional)

If `UNICODE_RANGES` is configured:
```csharp
config.ContainsLocalizedCharacters(text)
// Returns true if text contains any characters in the configured ranges
// Used to skip already-translated text
```

If `UNICODE_RANGES` is empty:
```csharp
// No detection - always attempt translation
// Correct for Latin languages
```

### Font Application

```csharp
// TextMeshTranslator.ApplyCustomFont()
string originalFontName = textMesh.font.name;
Font customFont = config.GetCustomFontMapping(originalFontName);

if (customFont != null)
{
    textMesh.font = customFont;
    renderer.material.mainTexture = customFont.material.mainTexture;
    config.GetPositionOffset(path); // Apply configured position
}
```

### Position Adjustments

```csharp
// TextMeshTranslator.AdjustTextPosition()
Vector3 offset = config.GetPositionOffset(path);

if (offset != Vector3.zero)
{
    textMesh.transform.localPosition += offset;
}
else
{
    // Fallback to hardcoded adjustments (backwards compatible)
}
```

## Dynamic UI Monitoring System

### Critical Elements Requiring Continuous Monitoring

**Priority Elements (checked every frame):**
- `GUI/Indicators/Interaction` - Item interaction prompts
- `GUI/Indicators/Partname` - Item/part names  
- `GUI/Indicators/Subtitles` - NPC subtitles

**Regular Elements (throttled checking):**
- `GUI/HUD/*` - Player stats (hunger, thirst, stress, etc.)
- `Sheets/YellowPagesMagazine/*/Lines/YellowLine` - Magazine text

### Monitoring Architecture

**Translation State Tracking:**
- Cache TextMesh references, don't re-search scene every frame
- Only re-translate when text content actually changes
- Skip already-localized text (unless magazine - game regenerates content)

**Priority vs Regular Monitoring:**
- Priority paths: Checked every frame (instant response)
- Regular paths: Throttled scanning (performance)
- Magazine text: Persistent monitoring (content regenerates)

## Complex Text Handling

### Magazine Random Text
```csharp
// Format: "word1, word2, word3"
// Translation: Each word translated individually
"bucket, hydraulic, oil" → "양동이, 유압, 오일"
```

### Magazine Price Lines
```csharp
// Format: "h.149,- puh.123456"
// Translation: Price + phone label + number
"h.149,- puh.123456" → "149 MK, 전화 - 123456"
```

### Handled by MagazineTextHandler
- Detects magazine paths: `Sheets/YellowPagesMagazine/*/Lines/YellowLine`
- Splits comma-separated words
- Translates each word individually
- Reconstructs with locale formatting

## Performance Optimization

**✅ Best Practices:**
- Cache config offsets lookup (O(n) first load, O(1) per lookup)
- Track translation state to skip re-translation
- Throttle non-priority element scanning
- Only apply font/position when needed

**❌ Anti-patterns:**
- Scanning all TextMesh objects every frame
- Re-translating already-localized text repeatedly
- Unnecessary Unicode range checks for Latin languages
- Not tracking which elements have been adjusted

## Development Workflow

### For Plugin Modifications

1. **Update core logic** - Plugin.cs, TextMeshTranslator.cs, etc.
2. **Consider backwards compatibility** - Old configs should still work
3. **Test with multiple languages** - Not just Korean
4. **Update copilot-instructions.md** - Document any architectural changes

### For Language Pack Creation

1. **Create config.txt** - Language metadata, fonts, position adjustments
2. **Create translate.txt** - Main translations (KEY = Translation format)
3. **Create fonts.unity3d** - Custom fonts (if needed)
4. **Test with F9 reload** - Live reload without restarting game
5. **Share configuration** - Community contributions welcome

## Testing & Debugging

### F9 Key (Live Reload)
- Reloads all config files instantly
- No game restart needed
- Perfect for iterative adjustment

### BepInEx Console (F12)
- Check for config parsing errors
- Verify translation count
- Monitor font loading status
- See position adjustment logs

### Common Issues & Solutions

**Issue: Font not applying**
- Verify font exists in fonts.unity3d
- Check config mapping: `OriginalFont = AssetFontName`
- Font asset name must match exactly

**Issue: Position adjustment not working**
- Verify path conditions match actual GameObject path
- Check offset format: `X,Y,Z` with commas
- Use F9 to reload and test quickly

**Issue: Translation not appearing**
- Verify key normalization: uppercase, no spaces
- Check Unicode ranges if applicable
- Look for parsing errors in BepInEx console

**Issue: Performance problems**
- Check for continuous re-translation loops
- Verify Unicode range detection working (if configured)
- Monitor element count in priority vs regular lists

## Architecture Simplification Guidelines

When system becomes complex:

1. **Responsibility separation** - Each class has clear purpose
   - `LocalizationConfig` - Config loading only
   - `PositionAdjustment` - Position logic only
   - `TextMeshTranslator` - Translation orchestration
   - `MagazineTextHandler` - Complex text handling

2. **Configuration over code** - Move logic to config.txt
   - Position adjustments (not hardcoded)
   - Font mappings (not dictionary)
   - Character detection (not hardcoded ranges)

3. **Clear data flow** - Easy to trace execution
   - Config loads → Translator uses config → Adjustments applied
   - Not circular dependencies

4. **Modular components** - Easy to test independently
   - Each class testable in isolation
   - Clear interface contracts

## File Structure

```
MWC_Localization_Core/
├── Plugin.cs                      # Main plugin entry point
├── LocalizationConfig.cs          # Config loading system
├── TextMeshTranslator.cs          # Translation & font application
├── TeletextHandler.cs             # Teletext data source translation
├── PositionAdjustment.cs          # Position adjustment system
├── MagazineTextHandler.cs         # Magazine-specific handling
├── StringHelper.cs                # String utilities
├── l10n_assets/
│   ├── config.txt                 # Language settings
│   ├── config_template_latin.txt  # Latin language template
│   ├── translate.txt              # Main translations
│   ├── translate_msc.txt          # MSC compatibility
│   ├── translate_magazine.txt     # Magazine translations
│   ├── translate_teletext.txt     # Teletext translations
│   └── fonts.unity3d              # Custom fonts (optional)
├── LANGUAGE_PACK_GUIDE.md         # For language pack creators
├── POSITION_ADJUSTMENTS_GUIDE.md  # Position adjustment details
├── CONFIG_REFERENCE.md            # Quick config reference
└── README.md                       # Project overview
```

## Quick Reference

### Version History
- **v0.2.0** - Korean-specific initial implementation
- **v0.3.0** - Generic multi-language framework with configurable adjustments
- **v0.3.1** - Teletext/data source translation system (runtime array replacement)

### Key Classes
- `LocalizationPlugin` - Main BepInEx plugin
- `LocalizationConfig` - Configuration management
- `TextMeshTranslator` - Core translation logic
- `TeletextHandler` - Teletext data source manipulation
- `PositionAdjustment` - Position rule matching
- `MagazineTextHandler` - Complex text handling

### Config Sections
- **Top level** - LANGUAGE_NAME, LANGUAGE_CODE, UNICODE_RANGES
- **[FONTS]** - Font mappings
- **[POSITION_ADJUSTMENTS]** - Position rules

### Critical Methods
- `LocalizationConfig.GetPositionOffset(path)` - Find offset for path
- `TextMeshTranslator.TranslateAndApplyFont(textMesh, path)` - Main translation
- `TeletextHandler.MonitorAndTranslateArrays()` - Runtime teletext monitoring
- `TeletextHandler.TranslateArrayListProxy()` - ArrayList replacement
- `PositionAdjustment.Matches(path)` - Test if rule applies

---

**Last Updated:** January 4, 2026  
**Status:** Phase 1 Complete - Generic framework with teletext support (v0.3.1+)  
**Next:** Community language packs, Phase 2 advanced features
