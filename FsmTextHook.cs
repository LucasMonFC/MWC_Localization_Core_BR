using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Applies FSM translations that are owned by PlayMaker variables/actions.
    /// This is needed for labels where TextMesh text is regenerated directly from FSM values.
    /// </summary>
    public class FsmTextHook : MonoBehaviour
    {
        private Dictionary<string, string> translations;
        private GameObject hostObject;
        private PatternMatcher patternMatcher;
        private bool isApplied;
        private string appliedTarget;
        private HashSet<string> loggedReadyTargets = new HashSet<string>();
        private List<PlayMakerFSM> cachedEnnusteDataFsms = new List<PlayMakerFSM>();
        private float lastEnnusteDataFsmScanTime = -10f;
        // POS/TextMesh batching and caches to avoid per-frame hitches
        private float lastPosUpdateTime = -10f;
        private float posUpdateInterval = 0.25f; // seconds
        private List<TextMesh> cachedTextMeshes = new List<TextMesh>();
        private float lastTextMeshCacheTime = -10f;
        private float textMeshCacheInterval = 5f; // seconds
        private int textMeshBatchSize = 64; // items processed per invocation
        private int textMeshBatchIndex = 0;

        private class TranslationCacheEntry { public string Translated; public float Time; }
        private Dictionary<string, TranslationCacheEntry> translationCache = new Dictionary<string, TranslationCacheEntry>();
        private float translationCacheTtl = 2f; // seconds
        private static readonly string[] PercentTokens = new string[] { "copying...", "formatting...", "sending..." };

        private enum FsmStrategyType
        {
            PosUse,
            PosTyper,
            TeletextBuildStringPattern,
            TeletextWeatherUpdaterTokens,
            UnemployPaperButtonVariables
        }

        private bool TryTranslateValue_PosBufferAware(string original, out string translated)
        {
            translated = original;
            if (string.IsNullOrEmpty(original)) return false;
            if (LooksLikeUserTypedCommand(original)) return false;

            if (original.IndexOf('\n') >= 0 && PosContainsPrompt(original))
            {
                string translatedBuf = TranslatePosTerminalBuffer(original);
                if (translatedBuf != original) { translated = translatedBuf; return true; }
                return false;
            }

            string t = GetTranslation(original, original);
            if (t != original) { translated = t; return true; }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original) { translated = lineTranslated; return true; }
            }

            return false;
        }

        private sealed class FsmStrategyTarget
        {
            public string ObjectPath;
            public string FsmName;
            public FsmStrategyType Strategy;
            public string AppliedLabel;
            public string ReadyLogKey;
            public string ReadyLogMessage;
            public string StateName;
            public int ActionIndex;

            public FsmStrategyTarget(
                string objectPath,
                string fsmName,
                FsmStrategyType strategy,
                string appliedLabel,
                string readyLogKey,
                string readyLogMessage,
                string stateName,
                int actionIndex)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                Strategy = strategy;
                AppliedLabel = appliedLabel;
                ReadyLogKey = readyLogKey;
                ReadyLogMessage = readyLogMessage;
                StateName = stateName;
                ActionIndex = actionIndex;
            }
        }

        private static readonly string[] PosUseStateNames = new string[] { "State 1", "State 3", "State 4", "State 5" };

        private static readonly FsmStrategyTarget[] GamePosTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/POS/BootSequence",
                "Use",
                FsmStrategyType.PosUse,
                "GAME POS",
                "POS_USE_READY",
                "[FsmTextHook] Use FSM is initialized and ready.",
                null,
                -1),
            new FsmStrategyTarget(
                "COMPUTER/SYSTEM/POS/Command",
                "Typer",
                FsmStrategyType.PosTyper,
                "GAME POS",
                "POS_TYPER_READY",
                "[FsmTextHook] Typer FSM is initialized and ready.",
                null,
                -1)
        };

        private static readonly FsmStrategyTarget[] GameTeletextTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/240/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/241/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                2),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/302/Texts/Data 1/Bottomline 1",
                "Data",
                FsmStrategyType.TeletextBuildStringPattern,
                "GAME Teletext Bottomline",
                "TTX_DATA_READY",
                "[FsmTextHook] Teletext Data FSM bottomline targets are ready.",
                "State 1",
                3)
        };

        private static readonly string TeletextEnnusteUpdaterPrefix = "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste/";

        private static readonly FsmStrategyTarget[] GameTeletextWeatherTargets = new FsmStrategyTarget[]
        {
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Nyt",
                "Logic",
                FsmStrategyType.TeletextWeatherUpdaterTokens,
                "GAME Teletext Weather",
                "TTX_WX_READY",
                "[FsmTextHook] Teletext weather updater FSM targets are ready.",
                null,
                -1),
            new FsmStrategyTarget(
                "Systems/TV/Teletext/VKTekstiTV/PAGES/188/Texts/Updater/Ennuste",
                "Logic",
                FsmStrategyType.TeletextWeatherUpdaterTokens,
                "GAME Teletext Weather",
                "TTX_WX_READY",
                "[FsmTextHook] Teletext weather updater FSM targets are ready.",
                null,
                -1)
        };

        private static readonly FsmStrategyTarget[] GameUnemployPaperTargets = BuildUnemployPaperTargets();
        private static FsmStrategyTarget[] BuildUnemployPaperTargets()
        {
            List<FsmStrategyTarget> targets = new List<FsmStrategyTarget>();
            string[] groups = new string[] { "2A", "2B", "2C", "2D" };

            for (int g = 0; g < groups.Length; g++)
            {
                string group = groups[g];
                for (int i = 1; i <= 7; i++)
                {
                    string path = "Sheets/UnemployPaper/" + group + "/" + i.ToString();
                    targets.Add(new FsmStrategyTarget(
                        path,
                        "Button",
                        FsmStrategyType.UnemployPaperButtonVariables,
                        "GAME UnemployPaper",
                        "UNEMPLOY_READY",
                        "[FsmTextHook] UnemployPaper Button FSM targets are ready.",
                        null,
                        -1));
                }
            }

            return targets.ToArray();
        }

        public void Initialize(Dictionary<string, string> translations, GameObject hostObject, string[] patternFiles)
        {
            this.translations = translations;
            this.hostObject = hostObject;
            this.patternMatcher = new PatternMatcher(translations);

            if (patternFiles != null)
            {
                for (int i = 0; i < patternFiles.Length; i++)
                {
                    string filePath = patternFiles[i];
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        patternMatcher.LoadPatternsFromFile(filePath);
                    }
                }
            }

            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            while (!isApplied)
            {
                string currentScene = Application.loadedLevelName;

                if (TryApplyTranslations(currentScene))
                {
                    isApplied = true;
                    string targetLabel = string.IsNullOrEmpty(appliedTarget) ? "Unknown" : appliedTarget;
                    CoreConsole.Print("[FsmTextHook] FSM text translations applied (" + targetLabel + ")");
                    Cleanup();
                    yield break;
                }

                // Poll at a small interval so delayed/hidden FSMs can be picked up after user interaction.
                yield return new WaitForSeconds(0.25f);
            }
        }

        private bool TryApplyTranslations(string currentScene)
        {
            if (translations == null)
                return false;

            if (currentScene == "MainMenu" && TryApplyMainMenuTranslations())
            {
                appliedTarget = "MainMenu";
                return true;
            }

            if (currentScene == "GAME")
            {
                // Keep this hook alive in GAME to refresh dynamic POS FSM string buffers.
                TryApplyGamePosFsmTranslations();
                TryApplyGameTeletextBottomlineFsmTranslations();
                TryApplyGameTeletextWeatherUpdaterFsmTranslations();
                TryApplyGameUnemployPaperFsmTranslations();
                // Also do a small batched sweep for copying/percent TextMeshes
                UpdateAllCopyingTextMeshes();
            }

            return false;
        }

        private bool TryApplyMainMenuTranslations()
        {
            GameObject folkObj = GameObject.Find("Radio/Folk");
            GameObject cdObj = GameObject.Find("Radio/CD");

            if (folkObj == null || cdObj == null)
                return false;

            PlayMakerFSM folkFsm = FindFsmWithState(folkObj, "Off");
            PlayMakerFSM cdFsm = FindFsmWithState(cdObj, "State 1");

            if (folkFsm == null || cdFsm == null)
                return false;

            if (folkFsm.Fsm == null || cdFsm.Fsm == null)
                return false;

            if (!folkFsm.Fsm.Initialized || !cdFsm.Fsm.Initialized)
                return false;

            string notImported = GetTranslation("NOT IMPORTED", "NOT IMPORTED");
            string radioImported = GetTranslation("RADIO IMPORTED", "RADIO IMPORTED");
            string cdsImported = GetTranslation("CD'S IMPORTED", "CD'S IMPORTED");

            SetFsmStringVariable(folkFsm, "Path", notImported);
            SetFsmStringVariable(cdFsm, "Path", notImported);

            ApplyStateSetStringValue(folkFsm, "Off", radioImported);
            ApplyStateSetStringValue(cdFsm, "State 1", cdsImported);

            return true;
        }

        private bool TryApplyGamePosFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;
            ApplyStrategyTargets(GamePosTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME POS";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameTeletextBottomlineFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;
            ApplyStrategyTargets(GameTeletextTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Bottomline";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameTeletextWeatherUpdaterFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameTeletextWeatherTargets, ref anyChanged, ref hasAnyTarget);

            List<PlayMakerFSM> ennusteDataFsms = GetEnnusteDataFsms();
            for (int i = 0; i < ennusteDataFsms.Count; i++)
            {
                PlayMakerFSM fsm = ennusteDataFsms[i];
                if (!IsFsmReady(fsm))
                    continue;

                LogReadyOnce("TTX_WX_READY", "[FsmTextHook] Teletext weather updater FSM targets are ready.");
                ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
            }

            if (anyChanged)
            {
                appliedTarget = "GAME Teletext Weather";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool TryApplyGameUnemployPaperFsmTranslations()
        {
            bool anyChanged = false;
            bool hasAnyTarget = false;

            ApplyStrategyTargets(GameUnemployPaperTargets, ref anyChanged, ref hasAnyTarget);

            if (anyChanged)
            {
                appliedTarget = "GAME UnemployPaper";
            }

            return anyChanged || hasAnyTarget;
        }

        private void ApplyStrategyTargets(FsmStrategyTarget[] targets, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (targets == null || targets.Length == 0)
                return;

            for (int i = 0; i < targets.Length; i++)
            {
                FsmStrategyTarget target = targets[i];
                if (target == null)
                    continue;

                PlayMakerFSM fsm = MLCUtils.FindFsmIncludingInactiveByPathAndName(target.ObjectPath, target.FsmName);
                if (!IsFsmReady(fsm))
                    continue;

                LogReadyOnce(target.ReadyLogKey, target.ReadyLogMessage);
                ApplyStrategyForTarget(target, fsm, ref anyChanged, ref hasAnyTarget);
            }
        }

        private void ApplyStrategyForTarget(FsmStrategyTarget target, PlayMakerFSM fsm, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (target == null || fsm == null)
                return;

            // Apply translations for POS Command operation progress states (copying/formatting/sending)
            if (TryApplyPosCommandProgressStateTranslations(fsm, ref anyChanged))
            {
                appliedTarget = "GAME POS Command Progress";
            }

            switch (target.Strategy)
            {
                case FsmStrategyType.PosUse:
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 1", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 3", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 4", 0, false);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "State 5", 0, false);
                    anyChanged |= ApplyAllStateSetStringValueTranslation(fsm);
                    hasAnyTarget |= HasAnyState(fsm, PosUseStateNames);
                    break;

                case FsmStrategyType.PosTyper:
                    // Player input / BuildStringFast action[1] = [old, path, command]
                    // Skip command slot (index 2) to avoid fighting live user input.
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Player input", 0, false, 2);
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, "Player input", 1, false, 2);
                    anyChanged |= ApplyAllStateSetStringValueTranslation(fsm);
                    anyChanged |= ApplyAllStateSetFsmStringTranslation(fsm);
                    hasAnyTarget |= HasState(fsm, "Player input");
                    break;

                case FsmStrategyType.TeletextBuildStringPattern:
                    anyChanged |= ApplyBuildStringActionStringPartsTranslation(fsm, target.StateName, target.ActionIndex, true);
                    hasAnyTarget |= HasState(fsm, target.StateName);
                    break;

                case FsmStrategyType.TeletextWeatherUpdaterTokens:
                    ApplyWeatherUpdaterTokenTranslations(fsm, ref anyChanged, ref hasAnyTarget);
                    break;

                case FsmStrategyType.UnemployPaperButtonVariables:
                    anyChanged |= ApplyUnemployPaperButtonVariableTranslations(fsm);
                    hasAnyTarget = true;
                    break;
            }

            if (anyChanged && !string.IsNullOrEmpty(target.AppliedLabel))
            {
                appliedTarget = target.AppliedLabel;
            }
        }

        private void LogReadyOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (loggedReadyTargets.Contains(key))
                return;

            loggedReadyTargets.Add(key);
            CoreConsole.Print(message);
        }

        private bool TryApplyPosCommandProgressStateTranslations(PlayMakerFSM fsm, ref bool anyChanged)
        {
            if (fsm == null || fsm.gameObject == null)
                return false;

            string objectPath = MLCUtils.GetGameObjectPath(fsm.gameObject);
            if (string.IsNullOrEmpty(objectPath) || !objectPath.Equals("COMPUTER/SYSTEM/POS/Command", System.StringComparison.OrdinalIgnoreCase))
                return false;

            bool changed = false;

            // Translate progress indicators in directory listing states (Dir list A & C)
            // These states show file operation progress: copying..., formatting..., sending...
            changed |= ApplyBuildStringActionStringPartsTranslation(fsm, "Dir list A", 2, true);
            changed |= ApplyBuildStringActionStringPartsTranslation(fsm, "Dir list A", 5, true);
            changed |= ApplyBuildStringActionStringPartsTranslation(fsm, "Dir list C", 2, true);
            changed |= ApplyBuildStringActionStringPartsTranslation(fsm, "Dir list C", 5, true);

            if (changed)
            {
                anyChanged = true;
                LogReadyOnce("POS_PROGRESS_READY", "[FsmTextHook] POS Command progress state translations are ready.");
            }

            return changed;
        }

        private bool IsFsmReady(PlayMakerFSM fsm)
        {
            return fsm != null && fsm.Fsm != null && fsm.Fsm.Initialized && fsm.FsmStates != null;
        }

        private void ApplyWeatherUpdaterTokenTranslations(PlayMakerFSM fsm, ref bool anyChanged, ref bool hasAnyTarget)
        {
            if (fsm == null)
                return;

            anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "State 4", 0, 1);
            anyChanged |= ApplyStateSetStringValueActionIndexesTranslation(fsm, "State 6", 0, 1);

            hasAnyTarget |= HasState(fsm, "State 4") || HasState(fsm, "State 6");
        }

        private bool ApplyUnemployPaperButtonVariableTranslations(PlayMakerFSM fsm)
        {
            if (fsm == null)
                return false;

            bool changed = false;

            changed |= TranslateFsmStringVariableExact(fsm, "jobNo");
            changed |= TranslateFsmStringVariableExact(fsm, "JobNo");
            changed |= TranslateFsmStringVariableExact(fsm, "jobYes");
            changed |= TranslateFsmStringVariableExact(fsm, "JobYes");

            return changed;
        }

        private bool TranslateFsmStringVariableExact(PlayMakerFSM fsm, string variableName)
        {
            if (fsm == null || fsm.FsmVariables == null || string.IsNullOrEmpty(variableName))
                return false;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            if (target == null || string.IsNullOrEmpty(target.Value))
                return false;

            string original = target.Value;
            string translated = GetTranslation(original, original);
            if (translated == original)
                return false;

            target.Value = translated;
            return true;
        }

        private List<PlayMakerFSM> GetEnnusteDataFsms()
        {
            bool shouldRescan = cachedEnnusteDataFsms.Count == 0 || (Time.realtimeSinceStartup - lastEnnusteDataFsmScanTime) >= 2f;
            if (!shouldRescan)
                return cachedEnnusteDataFsms;

            lastEnnusteDataFsmScanTime = Time.realtimeSinceStartup;
            cachedEnnusteDataFsms.Clear();

            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null)
                return cachedEnnusteDataFsms;

            for (int i = 0; i < allFsms.Length; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null)
                    continue;

                if (fsm.FsmName != "Data")
                    continue;

                string path = MLCUtils.GetGameObjectPath(fsm.gameObject);
                if (path.StartsWith(TeletextEnnusteUpdaterPrefix))
                {
                    cachedEnnusteDataFsms.Add(fsm);
                }
            }

            return cachedEnnusteDataFsms;
        }

        private bool HasState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                    return true;
            }

            return false;
        }

        private bool HasAnyState(PlayMakerFSM fsm, string[] stateNames)
        {
            if (fsm == null || stateNames == null || stateNames.Length == 0)
                return false;

            for (int i = 0; i < stateNames.Length; i++)
            {
                if (HasState(fsm, stateNames[i]))
                    return true;
            }

            return false;
        }

        private PlayMakerFSM FindFsmWithState(GameObject obj, string stateName)
        {
            if (obj == null)
                return null;

            PlayMakerFSM[] fsms = obj.GetComponents<PlayMakerFSM>();
            if (fsms == null)
                return null;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || fsm.FsmStates == null)
                    continue;

                for (int j = 0; j < fsm.FsmStates.Length; j++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[j];
                    if (state != null && state.Name == stateName)
                        return fsm;
                }
            }

            return null;
        }

        private void SetFsmStringVariable(PlayMakerFSM fsm, string variableName, string value)
        {
            if (fsm == null || fsm.FsmVariables == null)
                return;

            HutongGames.PlayMaker.FsmString target = fsm.FsmVariables.GetFsmString(variableName);
            if (target != null)
                target.Value = value;
        }

        private void ApplyStateSetStringValue(PlayMakerFSM fsm, string stateName, string value)
        {
            if (fsm == null || fsm.FsmStates == null)
                return;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null)
                return;

            for (int i = 0; i < targetState.Actions.Length; i++)
            {
                HutongGames.PlayMaker.Actions.SetStringValue action = targetState.Actions[i] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (action != null)
                    action.stringValue = value;
            }
        }

        private bool ApplyStateSetStringValueActionIndexesTranslation(PlayMakerFSM fsm, string stateName, params int[] actionIndexes)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return false;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null || actionIndexes == null || actionIndexes.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < actionIndexes.Length; i++)
            {
                int actionIndex = actionIndexes[i];
                if (actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                    continue;

                HutongGames.PlayMaker.Actions.SetStringValue action = targetState.Actions[actionIndex] as HutongGames.PlayMaker.Actions.SetStringValue;
                if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                    continue;

                changed |= TranslateSetStringValue(action);
            }

            return changed;
        }

        private bool ApplyBuildStringActionStringPartsTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, bool allowPatternSplit, params int[] skipPartIndexes)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState targetState = null;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                if (fsm.FsmStates[i] != null && fsm.FsmStates[i].Name == stateName)
                {
                    targetState = fsm.FsmStates[i];
                    break;
                }
            }

            if (targetState == null || targetState.Actions == null || actionIndex < 0 || actionIndex >= targetState.Actions.Length)
                return false;

            object action = targetState.Actions[actionIndex];
            if (action == null || action.GetType().Name != "BuildStringFast")
                return false;

            FieldInfo stringPartsField = action.GetType().GetField("stringParts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (stringPartsField == null)
                return false;

            HutongGames.PlayMaker.FsmString[] parts = stringPartsField.GetValue(action) as HutongGames.PlayMaker.FsmString[];
            if (parts == null || parts.Length == 0)
                return false;

            bool changed = false;
            if (allowPatternSplit)
            {
                changed = ApplyBuildStringFastPatternTranslation(fsm, stateName, actionIndex, parts);
            }

            // If this is a player-input-like state, avoid translating parts that look like user-typed commands.
            bool skipUserCommands = ShouldSkipPosTyperState(stateName);

            for (int i = 0; i < parts.Length; i++)
            {
                if (ShouldSkipIndex(i, skipPartIndexes))
                    continue;

                HutongGames.PlayMaker.FsmString part = parts[i];
                if (skipUserCommands && part != null && !string.IsNullOrEmpty(part.Value))
                {
                    // Don't translate short, single-token commands typed by the player.
                    if (LooksLikeUserTypedCommand(part.Value) && part.Value.IndexOf('\n') < 0)
                        continue;
                }

                changed |= TranslateStringPart(parts[i]);
            }

            return changed;
        }

        private bool ApplyBuildStringFastPatternTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, HutongGames.PlayMaker.FsmString[] parts)
        {
            if (patternMatcher == null || fsm == null || parts == null || parts.Length < 3)
                return false;

            HutongGames.PlayMaker.FsmString part0 = parts[0];
            HutongGames.PlayMaker.FsmString part1 = parts[1];
            HutongGames.PlayMaker.FsmString part2 = parts[2];

            if (part0 == null || part1 == null || part2 == null)
                return false;

            string middleValue = part1.Value ?? string.Empty;
            if (string.IsNullOrEmpty(middleValue))
                return false;

            string combinedText = BuildCombinedText(parts);
            if (string.IsNullOrEmpty(combinedText))
                return false;

            string path = MLCUtils.GetGameObjectPath(fsm.gameObject) + "|" + fsm.FsmName + "|" + stateName + "|" + actionIndex.ToString();
            string translatedCombined = patternMatcher.TryTranslateWithPattern(combinedText, path);
            if (string.IsNullOrEmpty(translatedCombined) || translatedCombined == combinedText)
                return false;

            int middleIndex = translatedCombined.IndexOf(middleValue, System.StringComparison.Ordinal);
            if (middleIndex < 0)
                return false;

            string newPrefix = translatedCombined.Substring(0, middleIndex);
            string newSuffix = translatedCombined.Substring(middleIndex + middleValue.Length);
            bool changed = false;

            if (part0.Value != newPrefix)
            {
                part0.Value = newPrefix;
                changed = true;
            }

            if (part2.Value != newSuffix)
            {
                part2.Value = newSuffix;
                changed = true;
            }

            return changed;
        }

        private string BuildCombinedText(HutongGames.PlayMaker.FsmString[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && !string.IsNullOrEmpty(parts[i].Value))
                {
                    sb.Append(parts[i].Value);
                }
            }

            return sb.ToString();
        }

        // Check for POS prompt markers in buffers
        private bool PosContainsPrompt(string s, bool asLine = false)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (asLine)
            {
                string l = s.TrimStart(' ', '\t');
                return l.StartsWith("C:\\>") || l.StartsWith("TELEBBS:\\>");
            }

            return s.Contains("C:\\>") || s.Contains("TELEBBS:\\>");
        }

        private string TranslatePosTerminalBuffer(string buffer)
        {
            if (string.IsNullOrEmpty(buffer)) return buffer;
            string[] lines = buffer.Split('\n');
            bool any = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                if (PosContainsPrompt(line, true)) continue;
                bool hasCR = line.Length > 0 && line[line.Length - 1] == '\r';
                string core = hasCR ? line.Substring(0, line.Length - 1) : line;
                if (string.IsNullOrEmpty(core)) continue;
                string translated = GetTranslation(core, core);
                if (translated == core) translated = TranslateTextByLines(core);
                if (translated != core) { lines[i] = hasCR ? (translated + "\r") : translated; any = true; }
            }
            if (!any) return buffer;
            return string.Join("\n", lines);
        }

        private static bool ContainsPercentToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            string lower = s.ToLowerInvariant();
            for (int i = 0; i < PercentTokens.Length; i++) if (lower.IndexOf(PercentTokens[i]) >= 0) return true;
            return false;
        }

        // Batched scanning of TextMesh objects to translate copying/formatting/sending lines.
        private void UpdateAllCopyingTextMeshes()
        {
            float now = Time.realtimeSinceStartup;

            // Throttle overall frequency
            if ((now - lastPosUpdateTime) < posUpdateInterval)
                return;
            lastPosUpdateTime = now;

            // Refresh cache of all TextMeshes occasionally
            if (cachedTextMeshes == null) cachedTextMeshes = new List<TextMesh>();
            if (cachedTextMeshes.Count == 0 || (now - lastTextMeshCacheTime) >= textMeshCacheInterval)
            {
                cachedTextMeshes.Clear();
                TextMesh[] all = Resources.FindObjectsOfTypeAll<TextMesh>();
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++) if (all[i] != null) cachedTextMeshes.Add(all[i]);
                }
                lastTextMeshCacheTime = now;
                textMeshBatchIndex = 0;
            }

            int total = cachedTextMeshes.Count;
            if (total == 0) return;

            int toProcess = textMeshBatchSize;
            int processed = 0;

            while (processed < toProcess && total > 0)
            {
                TextMesh tm = cachedTextMeshes[textMeshBatchIndex % total];
                textMeshBatchIndex = (textMeshBatchIndex + 1) % total;
                processed++;

                if (tm == null) continue;
                string cur = tm.text ?? string.Empty;
                if (cur.Length == 0) continue;

                // Only process copying/formatting/sending progress indicators
                if (!ContainsPercentToken(cur)) continue;

                // Check cache
                TranslationCacheEntry cacheEntry;
                if (translationCache.TryGetValue(cur, out cacheEntry))
                {
                    if ((now - cacheEntry.Time) < translationCacheTtl)
                    {
                        if (!string.IsNullOrEmpty(cacheEntry.Translated) && cacheEntry.Translated != cur)
                        {
                            try { tm.text = cacheEntry.Translated.Replace("\\n", "\n"); } catch { }
                        }
                        continue;
                    }
                    else
                    {
                        translationCache.Remove(cur);
                    }
                }

                string translated = GetTranslation(cur, cur);
                if (translated == cur && cur.IndexOf('\n') >= 0)
                {
                    translated = TranslateTextByLines(cur);
                }

                if (!string.IsNullOrEmpty(translated) && translated != cur)
                {
                    try { tm.text = translated.Replace("\\n", "\n"); } catch { }
                    translationCache[cur] = new TranslationCacheEntry() { Translated = translated, Time = now };
                }
                else
                {
                    // store negative result to avoid reprocessing for TTL
                    translationCache[cur] = new TranslationCacheEntry() { Translated = cur, Time = now };
                }
            }
        }

        // POS / typer safety helpers
        private static bool ShouldSkipPosTyperState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return false;
            string n = stateName.ToLowerInvariant();
            string[] tokens = new string[] { "player input", "input", "type", "typing", "command" };
            for (int i = 0; i < tokens.Length; i++) if (n.Contains(tokens[i])) return true;
            return false;
        }

        private static bool LooksLikeUserTypedCommand(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string v = value.Trim();
            if (v.Length < 1 || v.Length > 24) return false;
            if (v.IndexOfAny(new char[] { ' ', '\t', '\n', '\r' }) >= 0) return false;
            if (v.Contains(":") || v.Contains("...") || v.Contains(".")) return false;
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '/' || c == '#'))
                    return false;
            }
            return true;
        }

        private bool IterateStatesActions(PlayMakerFSM fsm, System.Func<object, bool, bool> process)
        {
            if (fsm == null || fsm.FsmStates == null) return false;
            bool changed = false;
            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null) continue;
                bool skipState = ShouldSkipPosTyperState(state.Name);
                for (int j = 0; j < state.Actions.Length; j++)
                {
                    object action = state.Actions[j];
                    if (action == null) continue;
                    try { changed |= process(action, skipState); } catch { }
                }
            }
            return changed;
        }

        private bool ApplyAllStateSetStringValueTranslation(PlayMakerFSM fsm)
        {
            // Use IterateStatesActions to find SetStringValue actions and translate their values.
            return IterateStatesActions(fsm, (action, skipState) =>
            {
                var a = action as HutongGames.PlayMaker.Actions.SetStringValue;
                if (a == null || a.stringValue == null || string.IsNullOrEmpty(a.stringValue.Value)) return false;
                // If this is a player-input-like state, don't translate values that look like typed commands.
                if (skipState && LooksLikeUserTypedCommand(a.stringValue.Value) && a.stringValue.Value.IndexOf('\n') < 0) return false;
                return TranslateSetStringValue(a);
            });
        }

        private bool ApplyAllStateSetFsmStringTranslation(PlayMakerFSM fsm)
        {
            // Use IterateStatesActions to find SetFsmString actions and translate their 'setValue' field.
            return IterateStatesActions(fsm, (action, skipState) =>
            {
                if (action == null || action.GetType().Name != "SetFsmString") return false;
                // If this is a player-input-like state, don't translate values that look like typed commands.
                FieldInfo field = action.GetType().GetField("setValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return false;
                HutongGames.PlayMaker.FsmString fsmString = field.GetValue(action) as HutongGames.PlayMaker.FsmString;
                if (fsmString == null || string.IsNullOrEmpty(fsmString.Value)) return false;
                if (skipState && LooksLikeUserTypedCommand(fsmString.Value) && fsmString.Value.IndexOf('\n') < 0) return false;
                return TranslateActionFsmStringField(action, "setValue");
            });
        }

        private bool TranslateSetStringValue(HutongGames.PlayMaker.Actions.SetStringValue action)
        {
            if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                return false;
            string original = action.stringValue.Value;
            string result;
            if (TryTranslateValue_PosBufferAware(original, out result))
            {
                action.stringValue.Value = result;
                return true;
            }

            return false;
        }

        private bool TranslateActionFsmStringField(object action, string fieldName)
        {
            if (action == null || string.IsNullOrEmpty(fieldName))
                return false;

            FieldInfo field = action.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return false;

            HutongGames.PlayMaker.FsmString fsmString = field.GetValue(action) as HutongGames.PlayMaker.FsmString;
            if (fsmString == null || string.IsNullOrEmpty(fsmString.Value))
                return false;

            string original = fsmString.Value;
            string result;
            if (TryTranslateValue_PosBufferAware(original, out result))
            {
                fsmString.Value = result;
                return true;
            }

            return false;
        }

        private bool TranslateStringPart(HutongGames.PlayMaker.FsmString part)
        {
            if (part == null || string.IsNullOrEmpty(part.Value))
                return false;
            string original = part.Value;
            string result;
            if (TryTranslateValue_PosBufferAware(original, out result))
            {
                part.Value = result;
                return true;
            }

            return false;
        }

        private bool ShouldSkipIndex(int index, int[] skipPartIndexes)
        {
            if (skipPartIndexes == null || skipPartIndexes.Length == 0)
                return false;

            for (int i = 0; i < skipPartIndexes.Length; i++)
            {
                if (skipPartIndexes[i] == index)
                    return true;
            }

            return false;
        }

        private string TranslateTextByLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] lines = text.Split('\n');
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string originalLine = lines[i];
                if (string.IsNullOrEmpty(originalLine))
                    continue;

                string lineNoCr = originalLine.Replace("\r", string.Empty);
                string translatedLine = GetTranslation(lineNoCr, lineNoCr);
                if (translatedLine != lineNoCr)
                {
                    lines[i] = translatedLine;
                    changed = true;
                }
            }

            if (!changed)
                return text;

            return string.Join("\n", lines);
        }


        private string GetTranslation(string key, string fallback)
        {
            if (translations == null)
                return fallback;

            string normalizedKey = MLCUtils.FormatUpperKey(key);
            string value;
            if (translations.TryGetValue(normalizedKey, out value))
                return value;
            // Fallback: try pattern-based translations (supports placeholders like {0})
            if (patternMatcher != null)
            {
                try
                {
                    string patternResult = patternMatcher.TryTranslateWithPattern(key, string.Empty);
                    if (!string.IsNullOrEmpty(patternResult))
                        return patternResult;
                }
                catch { }
            }

            return fallback;
        }

        private void Cleanup()
        {
            if (hostObject != null)
                Object.Destroy(hostObject);
        }
    }
}
