using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates built-in GUI TextMeshes by injecting a small action after the
    /// PlayMaker SetProperty action that writes the TextMesh text.
    /// </summary>
    public class FsmGuiTranslator : ITranslationSurface
    {
        public string Name { get { return "FsmGuiTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return processedTargets.Count >= targets.Length; } }

        private TextMeshTranslator translator;
        private readonly HashSet<string> processedTargets = new HashSet<string>();
        private readonly HashSet<int> initializedFsmIds = new HashSet<int>();
        private readonly PartnameLayoutHelper layoutHelper = new PartnameLayoutHelper();

        private sealed class IndicatorTarget
        {
            public readonly string ObjectPath;
            public readonly string FsmName;
            public readonly string StateName;
            public readonly string Key;

            public IndicatorTarget(string objectPath, string fsmName, string stateName)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                Key = objectPath + "|" + fsmName + "|" + stateName;
            }
        }

        private readonly IndicatorTarget[] targets = new IndicatorTarget[]
        {
            new IndicatorTarget("GUI/Indicators/Partname", "Partname", "On"),
            new IndicatorTarget("GUI/Indicators/Interaction", "SetText", "State 1"),
            new IndicatorTarget("GUI/Indicators/RallyCountdown", "SetText", "State 1"),
            new IndicatorTarget("GUI/Indicators/Gear", "SetText", "State 1"),
            new IndicatorTarget("GUI/Indicators/Subtitles", "SetText", "State 3"),
            new IndicatorTarget("GUI/HUD/Thrist/Pivot", "Scale", "State 2"),
            new IndicatorTarget("GUI/HUD/Thrist/Pivot", "Scale", "State 3"),
        };

        private readonly HashSet<string> layoutContributorKeys = new HashSet<string>
        {
            "GUI/Indicators/Partname|Partname|On",
            "GUI/Indicators/Interaction|SetText|State 1",
            "GUI/Indicators/Subtitles|SetText|State 3",
        };

        public void Initialize(TranslationContext ctx)
        {
            translator = ctx.Translator;
        }

        public int InitialPass()
        {
            return ProcessAllTargets();
        }

        public int MonitorTick(float deltaTime)
        {
            return ProcessAllTargets();
        }

        public void Reset()
        {
            processedTargets.Clear();
            initializedFsmIds.Clear();
            layoutHelper.Reset();
        }

        public void ClearTranslations()
        {
            Reset();
        }

        private int ProcessAllTargets()
        {
            if (Application.loadedLevelName != "GAME")
                return 0;

            int totalInjected = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                IndicatorTarget target = targets[i];
                if (processedTargets.Contains(target.Key))
                    continue;

                try
                {
                    GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(target.ObjectPath);
                    if (go == null)
                        continue;

                    PlayMakerFSM fsm = FindFsmByName(go, target.FsmName);
                    if (fsm == null || !EnsureFsmInitialized(fsm) || fsm.FsmStates == null)
                        continue;

                    HutongGames.PlayMaker.FsmState state = FindState(fsm, target.StateName);
                    if (state == null || state.Actions == null)
                        continue;

                    bool appendLayoutAction = layoutContributorKeys.Contains(target.Key);
                    int injected = SpliceTranslationActionsInto(state, appendLayoutAction);
                    if (injected > 0)
                        CoreConsole.Print("[FsmGuiTranslator] Injected " + injected + " action(s) into " + target.Key);

                    processedTargets.Add(target.Key);
                    totalInjected += injected;
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Warning("[FsmGuiTranslator] Failed processing " + target.Key + ": " + ex.Message);
                }
            }

            return totalInjected;
        }

        private static PlayMakerFSM FindFsmByName(GameObject go, string fsmName)
        {
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                if (fsms[i] != null && FsmUtils.GetFsmName(fsms[i]) == fsmName)
                    return fsms[i];
            }

            return null;
        }

        private bool EnsureFsmInitialized(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null)
                return false;
            if (fsm.Fsm.Initialized)
                return true;

            int id = fsm.GetInstanceID();
            if (!initializedFsmIds.Contains(id))
            {
                initializedFsmIds.Add(id);
                try
                {
                    fsm.Fsm.InitData();
                }
                catch
                {
                }
            }

            return fsm.FsmStates != null;
        }

        private static HutongGames.PlayMaker.FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            HutongGames.PlayMaker.FsmState[] states = fsm.FsmStates;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].Name == stateName)
                    return states[i];
            }

            return null;
        }

        private int SpliceTranslationActionsInto(HutongGames.PlayMaker.FsmState state, bool appendLayoutAction)
        {
            HutongGames.PlayMaker.FsmStateAction[] oldActions = state.Actions;
            List<HutongGames.PlayMaker.FsmStateAction> existingInjectedActions = null;
            for (int i = 0; i < oldActions.Length; i++)
            {
                TranslateTextMeshAction existingTranslate = oldActions[i] as TranslateTextMeshAction;
                if (existingTranslate != null)
                {
                    existingTranslate.translator = translator;
                    if (existingInjectedActions == null)
                        existingInjectedActions = new List<HutongGames.PlayMaker.FsmStateAction>();
                    existingInjectedActions.Add(existingTranslate);
                    continue;
                }

                RefreshPartnameLayoutAction existingLayout = oldActions[i] as RefreshPartnameLayoutAction;
                if (existingLayout != null)
                {
                    existingLayout.helper = layoutHelper;
                    if (existingInjectedActions == null)
                        existingInjectedActions = new List<HutongGames.PlayMaker.FsmStateAction>();
                    existingInjectedActions.Add(existingLayout);
                }
            }

            if (existingInjectedActions != null)
            {
                ActivateInjectedActionsIfStateActive(state, existingInjectedActions);
                return 0;
            }

            List<HutongGames.PlayMaker.FsmStateAction> newActions = null;
            List<HutongGames.PlayMaker.FsmStateAction> injectedActions = null;
            int injected = 0;

            for (int i = 0; i < oldActions.Length; i++)
            {
                HutongGames.PlayMaker.FsmStateAction action = oldActions[i];
                if (newActions != null)
                    newActions.Add(action);

                if (action == null || action.GetType().Name != "SetProperty")
                    continue;

                TranslateTextMeshAction injection = BuildInjectionAction(action);
                if (injection == null)
                    continue;

                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 3);
                    for (int j = 0; j <= i; j++)
                        newActions.Add(oldActions[j]);
                }

                newActions.Add(injection);
                if (injectedActions == null)
                    injectedActions = new List<HutongGames.PlayMaker.FsmStateAction>();
                injectedActions.Add(injection);
                injected++;
            }

            if (appendLayoutAction)
            {
                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 1);
                    for (int j = 0; j < oldActions.Length; j++)
                        newActions.Add(oldActions[j]);
                }

                RefreshPartnameLayoutAction injection = new RefreshPartnameLayoutAction { helper = layoutHelper };
                newActions.Add(injection);
                if (injectedActions == null)
                    injectedActions = new List<HutongGames.PlayMaker.FsmStateAction>();
                injectedActions.Add(injection);
                injected++;
            }

            if (newActions != null)
            {
                state.Actions = newActions.ToArray();
                ActivateInjectedActionsIfStateActive(state, injectedActions);
            }

            return injected;
        }

        private static void ActivateInjectedActionsIfStateActive(
            HutongGames.PlayMaker.FsmState state,
            List<HutongGames.PlayMaker.FsmStateAction> injectedActions)
        {
            if (state == null || injectedActions == null || !state.Active)
                return;

            for (int i = 0; i < injectedActions.Count; i++)
            {
                HutongGames.PlayMaker.FsmStateAction action = injectedActions[i];
                if (action == null || state.ActiveActions.Contains(action))
                    continue;

                state.ActiveActions.Add(action);
                action.Active = true;
                action.Finished = false;
                action.Init(state);
                action.Entered = true;
                action.OnEnter();
            }
        }

        private TranslateTextMeshAction BuildInjectionAction(HutongGames.PlayMaker.FsmStateAction setPropertyAction)
        {
            FieldInfo targetPropertyField = FsmUtils.GetField(setPropertyAction.GetType(), "targetProperty");
            if (targetPropertyField == null)
                return null;

            object targetProperty = targetPropertyField.GetValue(setPropertyAction);
            if (targetProperty == null)
                return null;

            FieldInfo propertyNameField = FsmUtils.GetField(targetProperty.GetType(), "PropertyName");
            if (propertyNameField == null)
                return null;

            string propertyName = propertyNameField.GetValue(targetProperty) as string;
            if (string.IsNullOrEmpty(propertyName)
                || !string.Equals(propertyName, "text", System.StringComparison.OrdinalIgnoreCase))
                return null;

            TextMesh textMesh = ResolveTargetTextMesh(targetProperty);
            if (textMesh == null || textMesh.gameObject == null)
                return null;

            string path = LocalizationUtils.GetGameObjectPath(textMesh.gameObject);
            return new TranslateTextMeshAction
            {
                target = textMesh,
                path = path,
                translator = translator,
            };
        }

        private static TextMesh ResolveTargetTextMesh(object targetProperty)
        {
            FieldInfo targetObjectField = FsmUtils.GetField(targetProperty.GetType(), "TargetObject");
            if (targetObjectField == null)
                return null;

            HutongGames.PlayMaker.FsmObject fsmObject = targetObjectField.GetValue(targetProperty) as HutongGames.PlayMaker.FsmObject;
            if (fsmObject == null)
                return null;

            UnityEngine.Object unityObject = fsmObject.Value;
            if (unityObject == null)
                return null;

            TextMesh asTextMesh = unityObject as TextMesh;
            if (asTextMesh != null)
                return asTextMesh;

            GameObject asGameObject = unityObject as GameObject;
            return asGameObject != null ? asGameObject.GetComponent<TextMesh>() : null;
        }

        private static bool TryGetSetPropertyTextTarget(HutongGames.PlayMaker.FsmStateAction setPropertyAction, out string propertyName, out string targetPath)
        {
            propertyName = string.Empty;
            targetPath = "?";

            if (setPropertyAction == null)
                return false;

            FieldInfo targetPropertyField = FsmUtils.GetField(setPropertyAction.GetType(), "targetProperty");
            if (targetPropertyField == null)
                return false;

            object targetProperty = targetPropertyField.GetValue(setPropertyAction);
            if (targetProperty == null)
                return false;

            FieldInfo propertyNameField = FsmUtils.GetField(targetProperty.GetType(), "PropertyName");
            if (propertyNameField == null)
                return false;

            propertyName = propertyNameField.GetValue(targetProperty) as string;
            if (string.IsNullOrEmpty(propertyName) || !string.Equals(propertyName, "text", System.StringComparison.OrdinalIgnoreCase))
                return false;

            TextMesh textMesh = ResolveTargetTextMesh(targetProperty);
            if (textMesh != null && textMesh.gameObject != null)
                targetPath = LocalizationUtils.GetGameObjectPath(textMesh.gameObject);

            return true;
        }

        public sealed class TranslateTextMeshAction : HutongGames.PlayMaker.FsmStateAction
        {
            public TextMesh target;
            public string path;
            public TextMeshTranslator translator;

            public override void OnEnter()
            {
                TranslateNow();
            }

            public override void OnUpdate()
            {
                TranslateNow();
            }

            private void TranslateNow()
            {
                try
                {
                    if (target == null || target.gameObject == null || translator == null)
                        return;

                    if (string.IsNullOrEmpty(target.text))
                        return;

                    translator.TranslateAndApplyFont(target, path);
                    translator.ApplyCustomFont(target, path);
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Warning("[FsmGuiTranslator] Translate action failed for " + path + ": " + ex.Message);
                }
            }
        }

        public sealed class PartnameLayoutHelper
        {
            private const string PartnamePath = "GUI/Indicators/Partname";
            private const string PartnameShadowPath = "GUI/Indicators/Partname/Shadow";
            private const string InteractionPath = "GUI/Indicators/Interaction";
            private const string SubtitlesPath = "GUI/Indicators/Subtitles";
            private const float TwoLineOffset = 0.74f;
            private const float PerExtraLineStep = 0.50f;
            private const float MaxOffset = 2.24f;

            private TextMesh partnamePrimary;
            private TextMesh partnameShadow;
            private TextMesh interaction;
            private TextMesh subtitles;
            private Vector3 partnameBaseLocalPosition;
            private Vector3 shadowBaseLocalPosition;
            private bool hasBasePosition;
            private bool hasShadowBasePosition;

            public void Refresh()
            {
                if (partnamePrimary == null || partnamePrimary.gameObject == null)
                    partnamePrimary = ResolveTextMesh(PartnamePath);
                if (partnamePrimary == null)
                    return;

                if (partnameShadow == null || partnameShadow.gameObject == null)
                    partnameShadow = ResolveTextMesh(PartnameShadowPath);
                if (interaction == null || interaction.gameObject == null)
                    interaction = ResolveTextMesh(InteractionPath);
                if (subtitles == null || subtitles.gameObject == null)
                    subtitles = ResolveTextMesh(SubtitlesPath);

                if (!hasBasePosition)
                {
                    partnameBaseLocalPosition = partnamePrimary.transform.localPosition;
                    hasBasePosition = true;
                }

                if (!hasShadowBasePosition && partnameShadow != null && partnameShadow.gameObject != null)
                {
                    shadowBaseLocalPosition = partnameShadow.transform.localPosition;
                    hasShadowBasePosition = true;
                }

                int maxLines = CountLines(partnamePrimary);
                int interactionLines = CountLines(interaction);
                if (interactionLines > maxLines)
                    maxLines = interactionLines;
                int subtitleLines = CountLines(subtitles);
                if (subtitleLines > maxLines)
                    maxLines = subtitleLines;

                float offset = 0f;
                if (maxLines > 1)
                {
                    offset = TwoLineOffset + (maxLines - 2) * PerExtraLineStep;
                    if (offset > MaxOffset)
                        offset = MaxOffset;
                }

                Vector3 yOffset = new Vector3(0f, offset, 0f);
                Vector3 primaryTarget = partnameBaseLocalPosition + yOffset;
                if (partnamePrimary.transform.localPosition != primaryTarget)
                    partnamePrimary.transform.localPosition = primaryTarget;

                if (partnameShadow == null || partnameShadow.gameObject == null || !hasShadowBasePosition)
                    return;

                if (partnameShadow.transform.parent == partnamePrimary.transform)
                {
                    if (partnameShadow.transform.localPosition != shadowBaseLocalPosition)
                        partnameShadow.transform.localPosition = shadowBaseLocalPosition;
                }
                else
                {
                    Vector3 shadowTarget = shadowBaseLocalPosition + yOffset;
                    if (partnameShadow.transform.localPosition != shadowTarget)
                        partnameShadow.transform.localPosition = shadowTarget;
                }
            }

            public void Reset()
            {
                partnamePrimary = null;
                partnameShadow = null;
                interaction = null;
                subtitles = null;
                hasBasePosition = false;
                hasShadowBasePosition = false;
            }

            private static TextMesh ResolveTextMesh(string path)
            {
                GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
                return go != null ? go.GetComponent<TextMesh>() : null;
            }

            private static int CountLines(TextMesh textMesh)
            {
                if (textMesh == null || textMesh.gameObject == null || !textMesh.gameObject.activeInHierarchy)
                    return 1;

                string text = textMesh.text;
                if (string.IsNullOrEmpty(text))
                    return 1;

                int lines = 1;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n')
                        lines++;
                }

                return lines;
            }
        }

        public sealed class RefreshPartnameLayoutAction : HutongGames.PlayMaker.FsmStateAction
        {
            public PartnameLayoutHelper helper;

            public override void OnEnter()
            {
                RefreshSafely();
            }

            public override void OnUpdate()
            {
                RefreshSafely();
            }

            public override void OnExit()
            {
                RefreshSafely();
            }

            private void RefreshSafely()
            {
                try
                {
                    if (helper != null)
                        helper.Refresh();
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Warning("[FsmGuiTranslator] Layout refresh failed: " + ex.Message);
                }
            }
        }
    }
}
