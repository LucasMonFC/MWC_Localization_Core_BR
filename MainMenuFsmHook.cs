using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Applies main menu translations that are owned by PlayMaker FSM variables/actions.
    /// This is needed for labels where TextMesh text is regenerated directly from FSM values.
    /// </summary>
    public class MainMenuFsmHook : MonoBehaviour
    {
        private Dictionary<string, string> translations;
        private GameObject hostObject;
        private bool isApplied;

        public void Initialize(Dictionary<string, string> translations, GameObject hostObject)
        {
            this.translations = translations;
            this.hostObject = hostObject;
            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            float startTime = Time.realtimeSinceStartup;
            const float timeoutSeconds = 20f;

            while (!isApplied && Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (TryApplyTranslations())
                {
                    isApplied = true;
                    CoreConsole.Print("[MainMenuFsmHook] Main menu FSM text translations applied");
                    Cleanup();
                    yield break;
                }

                yield return null;
            }

            if (!isApplied)
            {
                CoreConsole.Warning("[MainMenuFsmHook] Main menu FSM hook timed out before targets became ready");
                Cleanup();
            }
        }

        private bool TryApplyTranslations()
        {
            if (translations == null)
                return false;

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
