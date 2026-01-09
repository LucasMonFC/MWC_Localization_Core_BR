using MSCLoader;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Manages scene transitions and translation flags
    /// Consolidates hasTranslatedSplashScreen, hasTranslatedMainMenu, hasTranslatedGameScene logic
    /// </summary>
    public class SceneTranslationManager
    {
        
        // Scene translation states
        private bool hasTranslatedSplashScreen = false;
        private bool hasTranslatedMainMenu = false;
        private bool hasTranslatedGameScene = false;
        
        // Current scene tracking
        private string currentScene = "";
        private string previousScene = "";

        public SceneTranslationManager()
        {
        }

        /// <summary>
        /// Check if a scene needs translation
        /// </summary>
        public bool ShouldTranslateScene(string sceneName)
        {
            switch (sceneName)
            {
                case "SplashScreen":
                    return !hasTranslatedSplashScreen;
                    
                case "MainMenu":
                    return !hasTranslatedMainMenu;
                    
                case "GAME":
                    return !hasTranslatedGameScene;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Mark a scene as translated
        /// </summary>
        public void MarkSceneTranslated(string sceneName)
        {
            switch (sceneName)
            {
                case "SplashScreen":
                    hasTranslatedSplashScreen = true;
                    CoreConsole.Print("Splash Screen marked as translated");
                    break;
                    
                case "MainMenu":
                    hasTranslatedMainMenu = true;
                    CoreConsole.Print("Main Menu marked as translated");
                    break;
                    
                case "GAME":
                    hasTranslatedGameScene = true;
                    CoreConsole.Print("Game scene marked as translated");
                    break;
            }
        }

        /// <summary>
        /// Update scene tracking and handle scene changes
        /// Returns true if scene changed
        /// </summary>
        public bool UpdateScene(string newScene)
        {
            if (currentScene != newScene)
            {
                previousScene = currentScene;
                currentScene = newScene;
                
                CoreConsole.Print($"Scene changed: {previousScene} -> {currentScene}");
                HandleSceneChange(previousScene, currentScene);
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Handle scene-specific cleanup/reset logic
        /// </summary>
        private void HandleSceneChange(string from, string to)
        {
            if (to == "MainMenu")
            {
                // Reset game scene when returning to menu
                hasTranslatedGameScene = false;
                CoreConsole.Print("Scene change: Cleared game scene translation flag");
            }
            else if (to == "GAME")
            {
                // Reset main menu when entering game
                hasTranslatedMainMenu = false;
                CoreConsole.Print("Scene change: Cleared main menu translation flag");
            }
        }

        /// <summary>
        /// Reset all translation flags (for F8 reload)
        /// </summary>
        public void ResetAll()
        {
            hasTranslatedSplashScreen = false;
            hasTranslatedMainMenu = false;
            hasTranslatedGameScene = false;
            CoreConsole.Print("All scene translation flags reset");
        }

        /// <summary>
        /// Get current scene name
        /// </summary>
        public string GetCurrentScene()
        {
            return currentScene;
        }

        /// <summary>
        /// Get previous scene name
        /// </summary>
        public string GetPreviousScene()
        {
            return previousScene;
        }

        /// <summary>
        /// Check if currently in main menu
        /// </summary>
        public bool IsInMainMenu()
        {
            return currentScene == "MainMenu";
        }

        /// <summary>
        /// Check if currently in game
        /// </summary>
        public bool IsInGame()
        {
            return currentScene == "GAME";
        }

        /// <summary>
        /// Check if scene has been translated
        /// </summary>
        public bool HasSceneBeenTranslated(string sceneName)
        {
            switch (sceneName)
            {
                case "SplashScreen":
                    return hasTranslatedSplashScreen;
                    
                case "MainMenu":
                    return hasTranslatedMainMenu;
                    
                case "GAME":
                    return hasTranslatedGameScene;
                    
                default:
                    return false;
            }
        }
    }
}
