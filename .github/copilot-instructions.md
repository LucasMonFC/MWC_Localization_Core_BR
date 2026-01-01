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
- `PositionAdjustment.cs` - Configurable position adjustment system
- `MagazineTextHandler.cs` - Complex text handling (comma-separated, price lines)
- `StringHelper.cs` - String normalization utilities

**Configuration Files (in `l10n_assets/`):**
- `config.txt` - Language-specific settings (currently Korean)
- `translate.txt` - Main translations
- `translate_msc.txt` - My Summer Car compatibility
- `translate_magazine.txt` - Magazine translations
- `fonts.unity3d` - Custom font assets (optional)

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
├── PositionAdjustment.cs          # Position adjustment system
├── MagazineTextHandler.cs         # Magazine-specific handling
├── StringHelper.cs                # String utilities
├── l10n_assets/
│   ├── config.txt                 # Language settings
│   ├── config_template_latin.txt  # Latin language template
│   ├── translate.txt              # Main translations
│   ├── translate_msc.txt          # MSC compatibility
│   ├── translate_magazine.txt     # Magazine translations
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

### Key Classes
- `LocalizationPlugin` - Main BepInEx plugin
- `LocalizationConfig` - Configuration management
- `TextMeshTranslator` - Core translation logic
- `PositionAdjustment` - Position rule matching
- `MagazineTextHandler` - Complex text handling

### Config Sections
- **Top level** - LANGUAGE_NAME, LANGUAGE_CODE, UNICODE_RANGES
- **[FONTS]** - Font mappings
- **[POSITION_ADJUSTMENTS]** - Position rules

### Critical Methods
- `LocalizationConfig.GetPositionOffset(path)` - Find offset for path
- `TextMeshTranslator.TranslateAndApplyFont(textMesh, path)` - Main translation
- `PositionAdjustment.Matches(path)` - Test if rule applies

---

**Last Updated:** January 2, 2026  
**Status:** Phase 1 Complete - Generic framework ready for v0.3.0  
**Next:** Community language packs, Phase 2 advanced features
