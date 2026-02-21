using System.Collections;
using System.Collections.Generic;
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
        private bool isApplied;
        private string appliedTarget;
        private bool loggedUseReady;
        private bool loggedTyperReady;

        public void Initialize(Dictionary<string, string> translations, GameObject hostObject)
        {
            this.translations = translations;
            this.hostObject = hostObject;
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
            PlayMakerFSM bootFsm = MLCUtils.FindFsmIncludingInactiveByPathAndName("COMPUTER/SYSTEM/POS/BootSequence", "Use");
            bool anyChanged = false;
            bool hasAnyTarget = false;

            if (IsFsmReady(bootFsm))
            {
                if (!loggedUseReady)
                {
                    CoreConsole.Print("[FsmTextHook] Use FSM is initialized and ready.");
                    loggedUseReady = true;
                }

                anyChanged |= ApplyBuildStringActionStringPartsTranslation(bootFsm, "State 1", 0);
                anyChanged |= ApplyBuildStringActionStringPartsTranslation(bootFsm, "State 3", 0);
                anyChanged |= ApplyBuildStringActionStringPartsTranslation(bootFsm, "State 4", 0);
                anyChanged |= ApplyBuildStringActionStringPartsTranslation(bootFsm, "State 5", 0);
                anyChanged |= ApplyAllStateSetStringValueTranslation(bootFsm);

                hasAnyTarget =
                    HasState(bootFsm, "State 1") ||
                    HasState(bootFsm, "State 3") ||
                    HasState(bootFsm, "State 4") ||
                    HasState(bootFsm, "State 5");
            }

            PlayMakerFSM typerFsm = MLCUtils.FindFsmIncludingInactiveByPathAndName("COMPUTER/SYSTEM/POS/Command", "Typer");
            if (IsFsmReady(typerFsm))
            {
                if (!loggedTyperReady)
                {
                    CoreConsole.Print("[FsmTextHook] Typer FSM is initialized and ready.");
                    loggedTyperReady = true;
                }

                // Player input / BuildStringFast action[1] = [old, path, command]
                // Skip command slot (index 2) to avoid fighting live user input.
                anyChanged |= ApplyBuildStringActionStringPartsTranslation(typerFsm, "Player input", 0, 2);
                anyChanged |= ApplyBuildStringActionStringPartsTranslation(typerFsm, "Player input", 1, 2);
                anyChanged |= ApplyAllStateSetStringValueTranslation(typerFsm);
                anyChanged |= ApplyAllStateSetFsmStringTranslation(typerFsm);
                hasAnyTarget |= HasState(typerFsm, "Player input");
            }

            if (anyChanged)
            {
                appliedTarget = "GAME POS";
            }

            return anyChanged || hasAnyTarget;
        }

        private bool IsFsmReady(PlayMakerFSM fsm)
        {
            return fsm != null && fsm.Fsm != null && fsm.Fsm.Initialized && fsm.FsmStates != null;
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

        private bool ApplyBuildStringActionStringPartsTranslation(PlayMakerFSM fsm, string stateName, int actionIndex, params int[] skipPartIndexes)
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
            for (int i = 0; i < parts.Length; i++)
            {
                if (ShouldSkipIndex(i, skipPartIndexes))
                    continue;

                changed |= TranslateStringPart(parts[i]);
            }

            return changed;
        }

        private bool ApplyAllStateSetStringValueTranslation(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null)
                    continue;

                for (int j = 0; j < state.Actions.Length; j++)
                {
                    HutongGames.PlayMaker.Actions.SetStringValue action = state.Actions[j] as HutongGames.PlayMaker.Actions.SetStringValue;
                    if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                        continue;

                    changed |= TranslateSetStringValue(action);
                }
            }

            return changed;
        }

        private bool ApplyAllStateSetFsmStringTranslation(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.FsmStates == null)
                return false;

            bool changed = false;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state == null || state.Actions == null)
                    continue;

                for (int j = 0; j < state.Actions.Length; j++)
                {
                    object action = state.Actions[j];
                    if (action == null || action.GetType().Name != "SetFsmString")
                        continue;

                    changed |= TranslateActionFsmStringField(action, "setValue");
                }
            }

            return changed;
        }

        private bool TranslateSetStringValue(HutongGames.PlayMaker.Actions.SetStringValue action)
        {
            if (action == null || action.stringValue == null || string.IsNullOrEmpty(action.stringValue.Value))
                return false;

            string original = action.stringValue.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                action.stringValue.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    action.stringValue.Value = lineTranslated;
                    return true;
                }
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
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                fsmString.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    fsmString.Value = lineTranslated;
                    return true;
                }
            }

            return false;
        }

        private bool TranslateStringPart(HutongGames.PlayMaker.FsmString part)
        {
            if (part == null || string.IsNullOrEmpty(part.Value))
                return false;

            string original = part.Value;
            string translated = GetTranslation(original, original);
            if (translated != original)
            {
                part.Value = translated;
                return true;
            }

            if (original.IndexOf('\n') >= 0)
            {
                string lineTranslated = TranslateTextByLines(original);
                if (lineTranslated != original)
                {
                    part.Value = lineTranslated;
                    return true;
                }
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

            return fallback;
        }

        private void Cleanup()
        {
            if (hostObject != null)
                Object.Destroy(hostObject);
        }
    }
}
