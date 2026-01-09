# My Winter Car Localization Framework - Migration Guide

## Project Overview
**Game:** My Winter Car (Unity 5, PlayMaker FSM)  
**Framework:** MSCLoader 1.4  
**Architecture:** Multi-language localization (generic framework for community language packs)  
**Status:** v1.0.0 - MSCLoader Migration (fixing critical bugs)

## Migration Context
Transitioning from BepInEx (generic Unity plugin loader) to MSCLoader (game-specific mod framework) for better compatibility with My Winter Car modding ecosystem.
## MSCLoader Lifecycle

**Execution Order:**
```
Main Menu Load → ModSettings() → OnMenuLoad() → [User plays]
New Game/Continue → PreLoad() → OnLoad() → PostLoad() → [Game Loop: Update(), FixedUpdate()]
Save/Quit → OnSave() → [Back to Main Menu]
```

**Critical Setup Functions:**
- `ModSetup()` - Register callbacks ONLY (no logic!)
- `OnMenuLoad` - Main menu initialization (once per menu load)
- `PostLoad` - Game scene fully loaded (all mods ready)
- `Update` - Every frame (register via `SetupFunction(Setup.Update, Mod_Update)`)
- **⚠️ LateUpdate/FixedUpdate** - MonoBehaviour methods DO NOT auto-run in Mod class!

**Lifecycle Members**
OnNewGame - Called once when new game (not continue old save) is started
OnMenuLoad - Setup function that is executed once in MainMenu
PreLoad - Phase 1 of mod loading (executed once after GAME scene is loaded)
OnLoad - Phase 2 of mod loading (executed once GAME scene is fully loaded)
PostLoad - Phase 3 of mod loading (executed once after all mods finished with Phase 2)
OnSave - Executed once after game is being saved.
OnGUI - Works same way as unity OnGUI
Update - Works same way as unity Update
FixedUpdate - Works same way as unity FixedUpdate
OnModEnabled - Called once when mod has been enabled in settings
OnModDisabled - Called once when mod has been disabled in settings
ModSettingsLoaded - Called after saved settings have been loaded from file.
ModSettings - All settings and Keybinds should be created here.

## BepInEx → MSCLoader Migration

### Critical Mapping

| BepInEx | MSCLoader | Notes |
|---------|-----------|-------|
| `BaseUnityPlugin` | `Mod` | Base class |
| `Awake()` | `ModSetup()` | Early initialization - register Setup functions ONLY |
| `Start()` | `OnMenuLoad()` or `OnLoad()` | Scene-dependent initialization |
| `Update()` | `Update()` via `SetupFunction(Setup.Update, Mod_Update)` | Every frame |
| `LateUpdate()` | ❌ NOT SUPPORTED - Merge into Mod_Update | **CRITICAL: MonoBehaviour methods don't auto-run in Mod class!** |
| `BepInEx.Logging.ManualLogSource` | `ModConsole.Print()` | Logging |
| `Paths.PluginPath` | `ModLoader.GetModAssetsFolder(this)` | Asset folder |
| Manual F8/F9 keys | `Keybind` class | Built-in keybind system |
| Manual scene detection | `OnMenuLoad()` / `OnLoad()` events | Event-driven |
| `[BepInPlugin(GUID, Name, Version)]` | Override `ID`, `Name`, `Version` properties | Metadata |

### Scene Loading Strategy

**Old (BepInEx - Manual Polling):**
```csharp
void Update() {
    string currentScene = Application.loadedLevelName;
    if (currentScene == "MainMenu" && !mainMenuTranslated) {
        TranslateMainMenu();
        mainMenuTranslated = true;
    }
}
```

**New (MSCLoader - Event-Driven):**
```csharp
public override void ModSetup() {
    SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
    SetupFunction(Setup.PostLoad, Mod_PostLoad);
    SetupFunction(Setup.Update, Mod_Update);
}

void Mod_OnMenuLoad() {
    // Called once when main menu loads
    TranslateMainMenu();
}

void Mod_PostLoad() {
    // Called after game fully loaded
    TranslateGameScene();
}

void Mod_Update() {
    // Every frame - monitoring only
    if (reloadKey.GetKeybindDown()) ReloadTranslations();
    MonitorDynamicUI();
}
```

### Migration Progress

**Phase 1: Core Infrastructure** ✅ Complete
- Class structure: `LocalizationPlugin` → `MWC_Localization_Core : Mod`
- Logging: `ManualLogSource` → `ModConsole.Print()`
- File paths: `Paths.PluginPath` → `ModLoader.GetModAssetsFolder(this)`
- All handler classes migrated to use ModConsole

**Phase 2: Scene Translation** ✅ Event-driven refactoring complete
- Main menu: OnMenuLoad callback (removes Update() polling)
- Game scene: PostLoad callback (waits for full initialization)
- ❌ **BUG**: Update() still translates BEFORE PostLoad runs
- ❌ **BUG**: LateUpdate() does NOT execute (MonoBehaviour methods don't auto-run)

**Phase 3: Critical Refactoring** ✅ Complete
1. ✅ Removed `LateUpdate()` method entirely
2. ✅ Moved critical UI monitoring to `Mod_Update()` with throttling
3. ✅ Removed manual scene detection from `Mod_Update()` (SplashScreen, MainMenu, GAME blocks)
4. ✅ Consolidated all monitoring logic into `Mod_Update()`
5. ✅ Added text change detection and diagnostic logging for critical UI

### Known Challenges & Solutions

**Challenge 1: LateUpdate() Does NOT Work under MSCLoader**
- **Root Cause:** MSCLoader's Mod class is NOT a MonoBehaviour - it only registers callbacks via SetupFunction()
- **Evidence:** Logs show "0/100 translated in PostLoad" because LateUpdate never runs
- **Impact:** Critical UI elements (interaction prompts, subtitles) not translated, array monitoring broken
- **Solution:** Move all LateUpdate logic into Mod_Update() with proper throttling using LocalizationConstants

**Challenge 2: MSCLoader Instance Recreation on Scene Changes**
- **Root Cause:** MSCLoader creates NEW Mod instances when returning to MainMenu (Instance #1, #2, #3, etc.)
- **Impact:** Non-static AssetBundle fields reset to null, causing duplicate bundle loading errors:
  ```
  The AssetBundle '<unknown>' can't be loaded because another AssetBundle with the same files are already loaded
  ```
- **Evidence:** 
  - MainMenu → Game → MainMenu triggers new instance creation
  - Instance-level `AssetBundle fontBundle` resets to null
  - LoadAssets.LoadBundle() fails because Unity still has the first bundle loaded
- **Solution:** Make AssetBundle fields **static** to persist across instance recreation:
  ```csharp
  private static AssetBundle fontBundle;  // Static persists across instances
  ```
- **Best Practice:** Use static for any Unity assets that should persist between scene loads (bundles, prefabs loaded from bundles, etc.)

### Migration Checklist

**Core Files:**
- [x] All core files migrated to MSCLoader
- [x] Logging replaced with ModConsole
- [x] Asset paths updated

**Critical Refactoring:**
- [x] Remove `LateUpdate()` method entirely
- [x] Move critical UI monitoring to Mod_Update()
- [x] Remove manual scene detection from Mod_Update()
- [x] Consolidate monitoring logic with throttling
- [x] Add text change detection and diagnostic logging

**Testing Status:**
- [x] Mod loads without errors
- [x] Main menu translates (12/14 objects)
- [x] Arrays work (210 items)
- [x] Critical UI monitoring with change detection
- [ ] **PENDING**: In-game testing to verify critical UI translation

## Core Architecture Overview

**Key Components:**
- `MWC_Localization_Core` - Main MSCLoader mod (entry point)
- `LocalizationConfig` - Loads config.txt (language metadata, fonts, position adjustments)
- `TextMeshTranslator` - Core translation logic + font application
- `TeletextHandler` - Teletext array translation (runtime ArrayList replacement)
- `ArrayListProxyHandler` - PlayMaker ArrayList translation
- `MagazineTextHandler` - Complex text handling (comma-separated, price lines)
- `SceneTranslationManager` - Scene state tracking
- `UnifiedTextMeshMonitor` - Dynamic UI monitoring

**Translation Workflow:**
1. Load `config.txt` → Language metadata, Unicode ranges, font mappings, position adjustments
2. Load `translate.txt` → Main translation dictionary (KEY = Translation)
3. Load `translate_magazine.txt` / `translate_teletext.txt` → Specialized translations
4. Scan TextMesh components → Match GameObject paths
5. Apply translations + custom fonts + position adjustments
6. Monitor dynamic UI for changes

**Configuration Files:**
- `config.txt` - Language settings (Korean example)
- `translate.txt` - Main translations
- `translate_msc.txt` - My Summer Car compatibility
- `translate_magazine.txt` - Magazine translations
- `translate_teletext.txt` - Teletext translations (category-based, index-ordered)
- `fonts.unity3d` - Custom font bundle (optional)

## Teletext System (Critical for MSCLoader)

**Challenge:** Teletext stored in PlayMaker ArrayLists, not TextMesh → Can't use standard translation

**Solution:** Direct data source manipulation - replace ArrayList contents at runtime

**Lazy-Loading Issue:**
- Most arrays empty until navigated to (`kotimaa`, `ulkomaat`, `talous`, etc.)
- Pre-filling doesn't work (game overwrites `preFillStringList`)
- Must replace `_arrayList` AFTER game populates it

**Translation Strategy:**
```csharp
// Monitor in Mod_Update() (not LateUpdate - it doesn't run!)
if (proxy._arrayList.Count > 0) {  // Array populated
    ArrayList newArrayList = new ArrayList();
    for (int i = 0; i < proxy._arrayList.Count; i++) {
        newArrayList.Add(translations[i]);  // Index-based replacement
    }
    proxy._arrayList = newArrayList;  // Replace entire ArrayList
}
```

**translate_teletext.txt Format:**
```ini
[day]
MAANANTAI = 월요일
TIISTAI = 화요일

[kotimaa]
Original news headline = Translated headline
Second headline = 번역된 헤드라인
```
Order MUST match game's dump order (index-based)

**Font Application (Separate Step):**
After translating data, apply fonts to display TextMesh components:
```csharp
GameObject root = GameObject.Find("Systems/TV/Teletext/VKTekstiTV/PAGES");
TextMesh[] displays = root.GetComponentsInChildren<TextMesh>();
foreach (var tm in displays) translator.ApplyFontOnly(tm, path);
```

## Configuration System

**config.txt Structure:**
```ini
LANGUAGE_NAME = Korean
LANGUAGE_CODE = ko-KR

[FONTS]
FugazOne-Regular = NanumSquareRoundEB
Heebo-Black = PaperlogyExtraBold

[POSITION_ADJUSTMENTS]
Contains(GUI/HUD/) & EndsWith(/HUDLabel) = 0,-0.05,0
```

**Position Adjustment Conditions:**
- `Contains(text)` - Path contains substring
- `EndsWith(text)` - Path ends with substring
- `StartsWith(text)` - Path starts with substring
- `Equals(text)` - Exact match
- `!Contains(text)` - Negation
- Combine with `&` for AND logic

**Key Normalization:**
```csharp
// StringHelper.FormatUpperKey() - uppercase, no spaces/newlines
"BEER 149 MK" → "BEER149MK"
```

## Version History
- **v0.2.0** - Korean-specific initial implementation
- **v0.3.0** - Generic multi-language framework
- **v0.3.1** - Teletext/array translation system
- **v1.0.0** - MSCLoader migration (ongoing - fixing critical bugs)

---

**Last Updated:** January 9, 2026  
**Status:** MSCLoader Migration In Progress - Critical bugs being fixed  
**Current Issues:** LateUpdate not running, duplicate scene translation, game scene under-translated  
**Next Steps:** Refactor to MSCLoader lifecycle properly, consolidate monitoring in Mod_Update
