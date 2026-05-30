using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Applies hardcoded PlayMaker FSM source translations through loaded translation data.
    /// The registration table itself lives in <c>FsmTextHook.BuiltInTargets.cs</c>.
    /// </summary>
    public partial class FsmTextHook : ITranslationSurface
    {
        public string Name { get { return "FsmTextHook"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.OncePerScene; } }
        public bool IsComplete { get { return false; } }

        public int InitialPass()
        {
            UpdateForCurrentScene();
            return 0;
        }

        public bool ApplyForScene(string sceneName)
        {
            return UpdateForScene(sceneName);
        }

        public int MonitorTick(float deltaTime)
        {
            return 0;
        }

        public void Reset()
        {
            ResetRuntimeState();
        }

        public void ClearTranslations()
        {
            // Translation data is owned by the shared TranslationDictionary.
            // Reset() will clear runtime caches; pattern data lives elsewhere.
        }

        private readonly List<FsmTarget> targets = new List<FsmTarget>();
        private TranslationDictionary translations;
        private readonly Dictionary<string, string> translationCache = new Dictionary<string, string>();
        private readonly Dictionary<string, List<PlayMakerFSM>> indexedFsmCache = new Dictionary<string, List<PlayMakerFSM>>();
        private readonly Dictionary<string, List<PlayMakerFSM>> fsmListCache = new Dictionary<string, List<PlayMakerFSM>>();
        private readonly Dictionary<string, List<PlayMakerArrayListProxy>> arrayListProxyCache = new Dictionary<string, List<PlayMakerArrayListProxy>>();
        private readonly Dictionary<string, List<TextMesh>> textMeshCache = new Dictionary<string, List<TextMesh>>();
        private readonly HashSet<string> resolvedTargets = new HashSet<string>();
        private readonly HashSet<string> warnedTargets = new HashSet<string>();
        private readonly HashSet<int> initializedFsmIds = new HashSet<int>();

        private sealed class FsmRule
        {
            public readonly string Source;
            public readonly string Translation;

            public FsmRule(string source, string translation)
            {
                Source = source;
                Translation = translation;
            }
        }

        private sealed class FsmTarget
        {
            public readonly string Key;
            public readonly string ObjectPath;
            public readonly string FsmName;
            public readonly string StateName;
            public readonly int ActionIndex;
            public readonly bool WholeFsm;
            public readonly List<FsmRule> Rules = new List<FsmRule>();

            public FsmTarget(string objectPath, string fsmName, string stateName, int actionIndex)
            {
                ObjectPath = objectPath;
                FsmName = fsmName;
                StateName = stateName;
                ActionIndex = actionIndex;
                WholeFsm = string.IsNullOrEmpty(stateName) && actionIndex < 0;
                Key = BuildKey(objectPath, fsmName, stateName, actionIndex);
            }
        }

        public void Initialize(TranslationContext ctx)
        {
            this.translations = ctx.Translations;
            BuildTargets();
            ResetRuntimeState();
        }

        public void ResetRuntimeState()
        {
            ClearRuntimeCaches();
        }

        private bool UpdateForCurrentScene()
        {
            return UpdateForScene(Application.loadedLevelName);
        }

        private bool UpdateForScene(string currentScene)
        {
            if (targets.Count == 0)
                return false;

            bool isMainMenu = currentScene == "MainMenu";
            bool isGame = currentScene == "GAME";
            bool isIntro = currentScene == "Intro";
            if (!isMainMenu && !isGame && !isIntro)
                return false;

            bool changed = ApplySceneTargets(currentScene);
            if (changed)
                CoreConsole.Print("[FsmTextHook] Aplicou traduções FSM fixas em " + currentScene);

            return changed;
        }

        private void BuildTargets()
        {
            targets.Clear();
            translationCache.Clear();

            Dictionary<string, FsmTarget> byKey = new Dictionary<string, FsmTarget>();
            AddBuiltInTargets(byKey);

            targets.AddRange(byKey.Values);
            for (int i = 0; i < targets.Count; i++)
            {
                SortRules(targets[i].Rules);
            }
        }

        private void AddTargetRule(Dictionary<string, FsmTarget> byKey, string objectPath, string fsmName, string stateName, int actionIndex, string source)
        {
            if (byKey == null || string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(source))
                return;

            string translation;
            if (translations == null || !translations.TryGetExact(source, out translation))
                return;

            string key = BuildKey(objectPath, fsmName, stateName, actionIndex);
            FsmTarget target;
            if (!byKey.TryGetValue(key, out target))
            {
                target = new FsmTarget(objectPath, fsmName, stateName, actionIndex);
                byKey[key] = target;
            }

            target.Rules.Add(new FsmRule(source, translation));
        }

        private static string BuildKey(string objectPath, string fsmName, string stateName, int actionIndex)
        {
            return (objectPath ?? string.Empty)
                + "|"
                + (fsmName ?? string.Empty)
                + "|"
                + (stateName ?? string.Empty)
                + "|"
                + actionIndex.ToString();
        }

        private static void SortRules(List<FsmRule> rules)
        {
            rules.Sort(delegate (FsmRule a, FsmRule b)
            {
                int len = b.Source.Length.CompareTo(a.Source.Length);
                if (len != 0)
                    return len;

                return string.Compare(a.Source, b.Source, System.StringComparison.Ordinal);
            });
        }

        private bool ApplySceneTargets(string currentScene)
        {
            bool changed = false;
            for (int i = 0; i < targets.Count; i++)
            {
                FsmTarget target = targets[i];
                if (target == null || resolvedTargets.Contains(target.Key) || !IsTargetForScene(target, currentScene))
                    continue;

                bool resolved;
                changed |= TryApplyTarget(target, out resolved);
                if (resolved)
                    MarkTargetResolved(target);
            }

            return changed;
        }

        private bool TryApplyTarget(FsmTarget target, out bool resolved)
        {
            resolved = false;
            if (target == null)
                return false;

            try
            {
                return ApplyTarget(target, out resolved);
            }
            catch (System.Exception ex)
            {
                if (!warnedTargets.Contains(target.Key))
                {
                    warnedTargets.Add(target.Key);
                    CoreConsole.Warning("[FsmTextHook] Alvo FSM falhou uma vez '" + target.Key + "': " + ex.Message);
                }

                return false;
            }
        }

        private bool ApplyTarget(FsmTarget target, out bool resolved)
        {
            resolved = false;
            if (target.WholeFsm)
            {
                bool handled;
                bool indexedResolved;
                bool indexedLineChanged = TryTranslateIndexedLineTarget(target, out handled, out indexedResolved);
                if (handled)
                {
                    resolved = indexedResolved;
                    return indexedLineChanged;
                }

                List<PlayMakerFSM> fsms = GetFsmsForWholeTarget(target);
                bool changed = false;
                changed |= TranslateArrayListProxiesForTarget(target);
                for (int i = 0; i < fsms.Count; i++)
                {
                    changed |= TranslateWholeFsm(fsms[i], target);
                }

                changed |= TranslateTextMeshesForTarget(target);

                resolved = fsms.Count > 0 || HasCachedArrayListProxies(target) || HasCachedTextMeshes(target);
                return changed;
            }

            List<PlayMakerFSM> indexedFsms = GetFsmsForIndexedTarget(target);
            bool indexedChanged = false;
            for (int i = 0; i < indexedFsms.Count; i++)
            {
                PlayMakerFSM fsm = indexedFsms[i];
                if (!IsFsmReady(fsm) || fsm.FsmStates == null)
                    continue;

                HutongGames.PlayMaker.FsmState state = FindState(fsm, target.StateName);
                if (state == null || state.Actions == null || target.ActionIndex < 0 || target.ActionIndex >= state.Actions.Length)
                    continue;

                resolved = true;
                indexedChanged |= TranslateAction(state.Actions[target.ActionIndex], target);
            }

            return indexedChanged;
        }

        private bool TranslateWholeFsm(PlayMakerFSM fsm, FsmTarget target)
        {
            if (!IsFsmReady(fsm))
                return false;

            bool changed = false;
            if (fsm.FsmVariables != null && fsm.FsmVariables.StringVariables != null)
            {
                HutongGames.PlayMaker.FsmString[] variables = fsm.FsmVariables.StringVariables;
                for (int i = 0; i < variables.Length; i++)
                {
                    changed |= TranslateFsmString(variables[i], target);
                }
            }

            if (fsm.FsmStates != null)
            {
                for (int i = 0; i < fsm.FsmStates.Length; i++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                    if (state == null || state.Actions == null)
                        continue;

                    for (int j = 0; j < state.Actions.Length; j++)
                    {
                        changed |= TranslateAction(state.Actions[j], target);
                    }
                }
            }

            TextMesh textMesh = fsm.GetComponent<TextMesh>();
            if (textMesh != null && TranslateString(textMesh.text, target, out string translatedTextMesh))
            {
                textMesh.text = translatedTextMesh;
                changed = true;
            }

            return changed;
        }

        private bool TranslateTextMeshesForTarget(FsmTarget target)
        {
            List<TextMesh> textMeshes = GetTextMeshesForTarget(target);
            bool changed = false;
            for (int i = 0; i < textMeshes.Count; i++)
            {
                TextMesh textMesh = textMeshes[i];
                if (textMesh == null || string.IsNullOrEmpty(textMesh.text))
                    continue;

                if (!TranslateString(textMesh.text, target, out string translatedText))
                    continue;

                textMesh.text = translatedText;
                changed = true;
            }

            return changed;
        }

        private bool TranslateAction(object action, FsmTarget target)
        {
            if (action == null)
                return false;

            bool changed = false;
            changed |= TranslateBuildString(action, target);
            changed |= TranslateSetStringValueAction(action, target);
            changed |= TranslateSetPropertyStringParameter(action, target);
            changed |= TranslateArrayListGetProxy(action, target);
            changed |= TranslateActionDirectStringFields(action, target);
            changed |= TranslateActionFsmStringFields(action, target);
            return changed;
        }

        private bool TryTranslateIndexedLineTarget(FsmTarget target, out bool handled, out bool resolved)
        {
            handled = false;
            resolved = false;
            if (target == null || !target.WholeFsm || !TextMatchesExact(GetLastPathSegment(target.ObjectPath), "Line"))
                return false;

            handled = true;
            List<PlayMakerFSM> fsms = GetFsmsForWholeTarget(target);
            if (fsms.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < fsms.Count; i++)
            {
                changed |= SyncIndexedLineFromArrayList(fsms[i], target);
            }

            resolved = true;
            return changed;
        }

        private bool SyncIndexedLineFromArrayList(PlayMakerFSM fsm, FsmTarget target)
        {
            if (!IsFsmReady(fsm) || fsm.FsmStates == null)
                return false;

            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 1");
            if (state == null || state.Actions == null || state.Actions.Length == 0)
                return false;

            object action = state.Actions[0];
            if (action == null || action.GetType().Name != "ArrayListGet")
                return false;

            int index = GetArrayListGetIndex(action);
            PlayMakerArrayListProxy proxy = GetArrayListGetProxy(action);
            if (proxy == null || proxy._arrayList == null)
                return false;

            if (index < 0 || index >= proxy._arrayList.Count || proxy._arrayList[index] == null)
                return SetIndexedLineValue(fsm, string.Empty);

            string sourceLine = proxy._arrayList[index].ToString();
            if (string.IsNullOrEmpty(sourceLine))
                return SetIndexedLineValue(fsm, string.Empty);

            string displayLine;
            if (!TranslateString(sourceLine, target, out displayLine))
                displayLine = sourceLine;

            bool changed = false;
            if (proxy._arrayList[index] as string != displayLine)
            {
                proxy._arrayList[index] = displayLine;
                changed = true;
            }

            changed |= SetIndexedLineValue(fsm, displayLine);
            return changed;
        }

        private bool SetIndexedLineValue(PlayMakerFSM fsm, string value)
        {
            if (fsm == null || fsm.gameObject == null)
                return false;

            string safeValue = value ?? string.Empty;
            bool changed = false;
            HutongGames.PlayMaker.FsmString text = fsm.FsmVariables != null ? fsm.FsmVariables.GetFsmString("Text") : null;
            changed |= FsmUtils.SetFsmStringValue(text, safeValue);

            HutongGames.PlayMaker.FsmState state = FindState(fsm, "State 1");
            if (state != null && state.Actions != null)
            {
                if (state.Actions.Length > 0)
                    changed |= FsmUtils.SetNestedStringValue(state.Actions[0], safeValue, "result", "namedVar");

                if (state.Actions.Length > 1)
                    changed |= FsmUtils.SetNestedStringValue(state.Actions[1], safeValue, "targetProperty", "StringParameter");
            }

            TextMesh textMesh = fsm.GetComponent<TextMesh>();
            if (textMesh != null && textMesh.text != safeValue)
            {
                textMesh.text = safeValue;
                changed = true;
            }

            return changed;
        }

        private bool TranslateBuildString(object action, FsmTarget target)
        {
            if (!FsmUtils.IsBuildStringAction(action))
                return false;

            FieldInfo stringPartsField = FsmUtils.GetStringPartsField(action);
            if (stringPartsField == null)
                return false;

            bool changed = false;
            object stringPartsValue = stringPartsField != null ? stringPartsField.GetValue(action) : null;

            HutongGames.PlayMaker.FsmString[] parts = stringPartsValue as HutongGames.PlayMaker.FsmString[];
            if (parts != null)
            {
                changed |= TranslateBuildStringPatternPieces(parts);

                for (int i = 0; i < parts.Length; i++)
                {
                    changed |= TranslateFsmString(parts[i], target);
                }

                return changed;
            }

            string[] literalParts = stringPartsValue as string[];
            if (literalParts == null)
                return false;

            changed |= TranslateBuildStringPatternPieces(literalParts);

            for (int i = 0; i < literalParts.Length; i++)
            {
                if (!TranslateString(literalParts[i], target, out string translated))
                    continue;

                literalParts[i] = translated;
                changed = true;
            }

            if (changed)
                stringPartsField.SetValue(action, literalParts);

            return changed;
        }

        private bool TranslateBuildStringPatternPieces(HutongGames.PlayMaker.FsmString[] parts)
        {
            if (parts == null || translations == null)
                return false;

            bool changed = false;
            bool[] translatedParts = new bool[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (translatedParts[i] || parts[i] == null || string.IsNullOrEmpty(parts[i].Value))
                    continue;

                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (translatedParts[j] || parts[j] == null || string.IsNullOrEmpty(parts[j].Value))
                        continue;

                    string translationLeft;
                    string translationRight;
                    if (!translations.TryGetPatternPieceTranslation(parts[i].Value, parts[j].Value, out translationLeft, out translationRight))
                        continue;

                    string newLeft = PreserveOuterWhitespace(parts[i].Value, translationLeft);
                    string newRight = PreserveOuterWhitespace(parts[j].Value, translationRight);

                    if (parts[i].Value != newLeft)
                    {
                        parts[i].Value = newLeft;
                        changed = true;
                    }

                    if (parts[j].Value != newRight)
                    {
                        parts[j].Value = newRight;
                        changed = true;
                    }

                    translatedParts[i] = true;
                    translatedParts[j] = true;
                    break;
                }
            }

            return changed;
        }

        private bool TranslateBuildStringPatternPieces(string[] parts)
        {
            if (parts == null || translations == null)
                return false;

            bool changed = false;
            bool[] translatedParts = new bool[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (translatedParts[i] || string.IsNullOrEmpty(parts[i]))
                    continue;

                for (int j = i + 1; j < parts.Length; j++)
                {
                    if (translatedParts[j] || string.IsNullOrEmpty(parts[j]))
                        continue;

                    string translationLeft;
                    string translationRight;
                    if (!translations.TryGetPatternPieceTranslation(parts[i], parts[j], out translationLeft, out translationRight))
                        continue;

                    string newLeft = PreserveOuterWhitespace(parts[i], translationLeft);
                    string newRight = PreserveOuterWhitespace(parts[j], translationRight);

                    if (parts[i] != newLeft)
                    {
                        parts[i] = newLeft;
                        changed = true;
                    }

                    if (parts[j] != newRight)
                    {
                        parts[j] = newRight;
                        changed = true;
                    }

                    translatedParts[i] = true;
                    translatedParts[j] = true;
                    break;
                }
            }

            return changed;
        }

        private static string PreserveOuterWhitespace(string original, string replacement)
        {
            if (string.IsNullOrEmpty(original) || replacement == null)
                return replacement;

            int leading = 0;
            while (leading < original.Length && char.IsWhiteSpace(original[leading]))
                leading++;

            int trailing = original.Length - 1;
            while (trailing >= leading && char.IsWhiteSpace(original[trailing]))
                trailing--;

            string prefix = leading > 0 ? original.Substring(0, leading) : string.Empty;
            string suffix = trailing < original.Length - 1 ? original.Substring(trailing + 1) : string.Empty;
            return prefix + replacement + suffix;
        }

        private bool TranslateArrayListGetProxy(object action, FsmTarget target)
        {
            if (action == null || action.GetType().Name != "ArrayListGet")
                return false;

            PlayMakerArrayListProxy proxy = GetArrayListGetProxy(action);
            return TranslateArrayListProxy(proxy, target);
        }

        private bool TranslateSetPropertyStringParameter(object action, FsmTarget target)
        {
            if (action == null || action.GetType().Name != "SetProperty")
                return false;

            FieldInfo targetPropertyField = FsmUtils.GetField(action.GetType(), "targetProperty");
            if (targetPropertyField == null)
                return false;

            object targetProperty = targetPropertyField.GetValue(action);
            if (targetProperty == null)
                return false;

            FieldInfo stringParameterField = FsmUtils.GetField(targetProperty.GetType(), "StringParameter");
            if (stringParameterField == null)
                return false;

            string value = stringParameterField.GetValue(targetProperty) as string;
            if (!TranslateString(value, target, out string translated))
                return false;

            stringParameterField.SetValue(targetProperty, translated);
            return true;
        }

        private bool TranslateSetStringValueAction(object action, FsmTarget target)
        {
            if (action == null)
                return false;

            HutongGames.PlayMaker.Actions.SetStringValue typedAction = action as HutongGames.PlayMaker.Actions.SetStringValue;
            if (typedAction != null)
            {
                string current = typedAction.stringValue != null ? typedAction.stringValue.Value : null;
                if (!TranslateString(current, target, out string translated))
                    return false;

                typedAction.stringValue = translated;
                return true;
            }

            if (action.GetType().Name != "SetStringValue")
                return false;

            FieldInfo stringValueField = FsmUtils.GetField(action.GetType(), "stringValue");
            if (stringValueField == null)
                return false;

            HutongGames.PlayMaker.FsmString currentFsmString = stringValueField.GetValue(action) as HutongGames.PlayMaker.FsmString;
            string currentValue = currentFsmString != null ? currentFsmString.Value : null;
            if (!TranslateString(currentValue, target, out string reflectedTranslated))
                return false;

            stringValueField.SetValue(action, (HutongGames.PlayMaker.FsmString)reflectedTranslated);
            return true;
        }

        private bool TranslateActionDirectStringFields(object action, FsmTarget target)
        {
            if (action == null)
                return false;

            bool changed = false;
            FieldInfo[] fields = FsmUtils.GetFields(action.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.IsStatic || field.FieldType != typeof(string))
                    continue;

                if (!IsTranslatableDirectStringField(field.Name))
                    continue;

                string value;
                try
                {
                    value = field.GetValue(action) as string;
                }
                catch
                {
                    continue;
                }

                if (!TranslateString(value, target, out string translated))
                    continue;

                field.SetValue(action, translated);
                changed = true;
            }

            return changed;
        }

        private bool TranslateActionFsmStringFields(object action, FsmTarget target)
        {
            if (action == null)
                return false;

            bool changed = false;
            FieldInfo[] fields = FsmUtils.GetFields(action.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.IsStatic || ShouldSkipActionField(field))
                    continue;

                object value;
                try
                {
                    value = field.GetValue(action);
                }
                catch
                {
                    continue;
                }

                HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    changed |= TranslateFsmString(fsmString, target);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
                if (fsmStrings != null)
                {
                    for (int j = 0; j < fsmStrings.Length; j++)
                    {
                        changed |= TranslateFsmString(fsmStrings[j], target);
                    }

                    continue;
                }

                if (ShouldScanNestedFsmValue(field, value))
                    changed |= TranslateNestedFsmStringFields(value, target);
            }

            return changed;
        }

        private bool TranslateNestedFsmStringFields(object instance, FsmTarget target)
        {
            if (instance == null)
                return false;

            bool changed = false;
            FieldInfo[] fields = FsmUtils.GetFields(instance.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.IsStatic || ShouldSkipActionField(field))
                    continue;

                object value;
                try
                {
                    value = field.GetValue(instance);
                }
                catch
                {
                    continue;
                }

                HutongGames.PlayMaker.FsmString fsmString = value as HutongGames.PlayMaker.FsmString;
                if (fsmString != null)
                {
                    changed |= TranslateFsmString(fsmString, target);
                    continue;
                }

                HutongGames.PlayMaker.FsmString[] fsmStrings = value as HutongGames.PlayMaker.FsmString[];
                if (fsmStrings == null)
                    continue;

                for (int j = 0; j < fsmStrings.Length; j++)
                {
                    changed |= TranslateFsmString(fsmStrings[j], target);
                }
            }

            return changed;
        }

        private bool TranslateArrayListProxiesForTarget(FsmTarget target)
        {
            List<PlayMakerArrayListProxy> proxies = GetArrayListProxiesForTarget(target);
            if (proxies.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < proxies.Count; i++)
            {
                PlayMakerArrayListProxy proxy = proxies[i];
                changed |= TranslateArrayListProxy(proxy, target);
            }

            return changed;
        }

        private List<PlayMakerArrayListProxy> GetArrayListProxiesForTarget(FsmTarget target)
        {
            List<PlayMakerArrayListProxy> cached;
            if (arrayListProxyCache.TryGetValue(target.Key, out cached) && AreArrayListProxiesValid(cached))
                return cached;

            List<PlayMakerArrayListProxy> result = new List<PlayMakerArrayListProxy>();
            string expectedReference = GetLastPathSegment(target.ObjectPath);
            GameObject currentObject = LocalizationUtils.FindGameObjectIncludingInactive(target.ObjectPath) ?? FindNearestGameObjectForPath(target.ObjectPath);
            Transform current = currentObject != null ? currentObject.transform : null;
            int depth = 0;
            while (current != null && depth < 6)
            {
                AddMatchingArrayListProxies(current.gameObject, target, expectedReference, result);
                if (result.Count > 0)
                    break;

                current = current.parent;
                depth++;
            }

            if (result.Count > 0)
                arrayListProxyCache[target.Key] = result;

            return result;
        }

        private void AddMatchingArrayListProxies(GameObject root, FsmTarget target, string expectedReference, List<PlayMakerArrayListProxy> result)
        {
            if (root == null)
                return;

            PlayMakerArrayListProxy[] proxies = root.GetComponentsInChildren<PlayMakerArrayListProxy>(true);
            if (proxies == null || proxies.Length == 0)
                return;

            for (int i = 0; i < proxies.Length; i++)
            {
                PlayMakerArrayListProxy proxy = proxies[i];
                if (proxy == null || proxy.gameObject == null || result.Contains(proxy))
                    continue;

                string proxyPath = LocalizationUtils.GetGameObjectPath(proxy.gameObject);
                if (IsObjectPathMatch(proxyPath, target.ObjectPath) || TextMatchesExact(proxy.referenceName, expectedReference))
                    result.Add(proxy);
            }
        }

        private static bool AreArrayListProxiesValid(List<PlayMakerArrayListProxy> proxies)
        {
            if (proxies == null || proxies.Count == 0)
                return false;

            for (int i = 0; i < proxies.Count; i++)
            {
                if (proxies[i] == null || proxies[i].gameObject == null)
                    return false;
            }

            return true;
        }

        private static bool AreFsmsValid(List<PlayMakerFSM> fsms)
        {
            if (fsms == null || fsms.Count == 0)
                return false;

            for (int i = 0; i < fsms.Count; i++)
            {
                if (fsms[i] == null || fsms[i].gameObject == null)
                    return false;
            }

            return true;
        }

        private static bool AreTextMeshesValid(List<TextMesh> textMeshes)
        {
            if (textMeshes == null || textMeshes.Count == 0)
                return false;

            for (int i = 0; i < textMeshes.Count; i++)
            {
                if (textMeshes[i] == null || textMeshes[i].gameObject == null)
                    return false;
            }

            return true;
        }

        private bool TranslateArrayListProxy(PlayMakerArrayListProxy proxy, FsmTarget target)
        {
            if (proxy == null)
                return false;

            bool changed = false;
            changed |= TranslateArrayList(proxy._arrayList, target);
            changed |= TranslateStringList(proxy.preFillStringList, target);
            return changed;
        }

        private bool TranslateArrayList(ArrayList list, FsmTarget target)
        {
            if (list == null || list.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                string value = list[i] as string;
                if (string.IsNullOrEmpty(value))
                    continue;

                if (!TranslateString(value, target, out string translated))
                    continue;

                list[i] = translated;
                changed = true;
            }

            return changed;
        }

        private bool TranslateStringList(List<string> list, FsmTarget target)
        {
            if (list == null || list.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                string value = list[i];
                if (string.IsNullOrEmpty(value))
                    continue;

                if (!TranslateString(value, target, out string translated))
                    continue;

                list[i] = translated;
                changed = true;
            }

            return changed;
        }

        private bool HasCachedArrayListProxies(FsmTarget target)
        {
            List<PlayMakerArrayListProxy> proxies;
            return target != null
                && arrayListProxyCache.TryGetValue(target.Key, out proxies)
                && AreArrayListProxiesValid(proxies);
        }

        private List<TextMesh> GetTextMeshesForTarget(FsmTarget target)
        {
            List<TextMesh> cached;
            if (textMeshCache.TryGetValue(target.Key, out cached) && AreTextMeshesValid(cached))
                return cached;

            List<TextMesh> result = new List<TextMesh>();
            GameObject root = LocalizationUtils.FindGameObjectIncludingInactive(target.ObjectPath) ?? FindNearestGameObjectForPath(target.ObjectPath);
            if (root != null)
            {
                TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);
                for (int i = 0; i < textMeshes.Length; i++)
                {
                    TextMesh textMesh = textMeshes[i];
                    if (textMesh == null || textMesh.gameObject == null)
                        continue;

                    if (!IsObjectPathMatch(LocalizationUtils.GetGameObjectPath(textMesh.gameObject), target.ObjectPath))
                        continue;

                    result.Add(textMesh);
                }
            }

            if (result.Count > 0)
                textMeshCache[target.Key] = result;

            return result;
        }

        private bool HasCachedTextMeshes(FsmTarget target)
        {
            List<TextMesh> cached;
            return target != null
                && textMeshCache.TryGetValue(target.Key, out cached)
                && AreTextMeshesValid(cached);
        }

        private void MarkTargetResolved(FsmTarget target)
        {
            if (target == null || resolvedTargets.Contains(target.Key))
                return;

            resolvedTargets.Add(target.Key);
        }

        private static bool IsTargetForScene(FsmTarget target, string currentScene)
        {
            if (target == null)
                return false;

            bool mainMenuTarget = target.ObjectPath.StartsWith("Radio/", System.StringComparison.Ordinal);
            bool introTarget = IsSceneRootTarget(target, "Intro");
            if (currentScene == "MainMenu")
                return mainMenuTarget;

            if (currentScene == "GAME")
                return !mainMenuTarget && !introTarget;

            if (currentScene == "Intro")
                return introTarget;

            return false;
        }

        private static bool IsSceneRootTarget(FsmTarget target, string sceneRoot)
        {
            if (target == null || string.IsNullOrEmpty(target.ObjectPath) || string.IsNullOrEmpty(sceneRoot))
                return false;

            return TextMatchesExact(target.ObjectPath, sceneRoot)
                || target.ObjectPath.StartsWith(sceneRoot + "/", System.StringComparison.Ordinal);
        }

        private bool TranslateFsmString(HutongGames.PlayMaker.FsmString fsmString, FsmTarget target)
        {
            if (fsmString == null)
                return false;

            string value = fsmString.Value;
            if (!TranslateString(value, target, out string translated))
                return false;

            fsmString.Value = translated;
            return true;
        }

        private bool TranslateString(string value, FsmTarget target, out string translated)
        {
            translated = value;
            if (string.IsNullOrEmpty(value) || target == null || target.Rules.Count == 0)
                return false;

            string cacheKey = target.Key + "\n" + value;
            if (translationCache.TryGetValue(cacheKey, out translated))
                return translated != value;

            string result = ApplyRules(value, target.Rules);
            translationCache[cacheKey] = result;
            translated = result;
            return result != value;
        }

        private static string ApplyRules(string value, List<FsmRule> rules)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            StringBuilder builder = null;
            int index = 0;
            while (index < value.Length)
            {
                FsmRule match = FindRuleAt(value, index, rules);
                if (match == null)
                {
                    if (builder != null)
                        builder.Append(value[index]);

                    index++;
                    continue;
                }

                if (builder == null)
                    builder = new StringBuilder(value.Length + 16).Append(value, 0, index);

                AppendTranslation(builder, match.Translation);
                index += match.Source.Length;
            }

            return builder == null ? value : builder.ToString();
        }

        private static FsmRule FindRuleAt(string value, int index, List<FsmRule> rules)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                FsmRule rule = rules[i];
                if (!MatchesAt(value, index, rule.Source))
                    continue;

                if (!HasTranslationBoundary(value, index, rule.Source))
                    continue;

                if (IsAlreadyTranslatedOccurrence(value, index, rule))
                    continue;

                return rule;
            }

            return null;
        }

        private static bool IsAlreadyTranslatedOccurrence(string value, int sourceIndex, FsmRule rule)
        {
            if (string.IsNullOrEmpty(value) || rule == null || string.IsNullOrEmpty(rule.Source) || string.IsNullOrEmpty(rule.Translation))
                return false;

            int translationSourceIndex = 0;
            while ((translationSourceIndex = rule.Translation.IndexOf(rule.Source, translationSourceIndex, System.StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int translationStart = sourceIndex - translationSourceIndex;
                if (translationStart >= 0
                    && translationStart + rule.Translation.Length <= value.Length
                    && string.Compare(value, translationStart, rule.Translation, 0, rule.Translation.Length, System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }

                translationSourceIndex += rule.Source.Length;
            }

            return false;
        }

        private static bool MatchesAt(string value, int index, string source)
        {
            if (string.IsNullOrEmpty(source) || index + source.Length > value.Length)
                return false;

            return string.Compare(value, index, source, 0, source.Length, System.StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool HasTranslationBoundary(string value, int index, string source)
        {
            int length = string.IsNullOrEmpty(source) ? 0 : source.Length;
            char before = index > 0 ? value[index - 1] : '\0';
            char after = index + length < value.Length ? value[index + length] : '\0';

            if (index > 0 && IsWordChar(before) && IsWordChar(value[index]) && !IsUnitSuffixAfterNumber(before, source))
                return false;

            if (index + length < value.Length && IsWordChar(value[index + length - 1]) && IsWordChar(after))
                return false;

            return true;
        }

        private static bool IsUnitSuffixAfterNumber(char before, string source)
        {
            return char.IsDigit(before)
                && !string.IsNullOrEmpty(source)
                && source.StartsWith("km/h", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWordChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static void AppendTranslation(StringBuilder builder, string translation)
        {
            if (builder.Length > 0 && !string.IsNullOrEmpty(translation) && translation[0] == 'ª' && char.IsWhiteSpace(builder[builder.Length - 1]))
                builder.Length--;

            builder.Append(translation);
        }

        private List<PlayMakerFSM> GetFsmsForIndexedTarget(FsmTarget target)
        {
            List<PlayMakerFSM> cached;
            if (indexedFsmCache.TryGetValue(target.Key, out cached) && AreFsmsValid(cached))
                return cached;

            List<PlayMakerFSM> result = new List<PlayMakerFSM>();
            List<GameObject> objects = LocalizationUtils.FindAllGameObjectsIncludingInactive(target.ObjectPath);
            for (int i = 0; i < objects.Count; i++)
            {
                AddMatchingFsms(objects[i].GetComponents<PlayMakerFSM>(), target, result);
            }

            if (result.Count == 0)
            {
                PlayMakerFSM fallback = FindMatchingFsmByPrefix(target);
                if (fallback != null)
                    result.Add(fallback);
            }

            if (result.Count > 0)
                indexedFsmCache[target.Key] = result;

            return result;
        }

        private List<PlayMakerFSM> GetFsmsForWholeTarget(FsmTarget target)
        {
            List<PlayMakerFSM> cached;
            if (fsmListCache.TryGetValue(target.Key, out cached) && cached != null && cached.Count > 0)
                return cached;

            List<PlayMakerFSM> result = new List<PlayMakerFSM>();
            GameObject exactObject = LocalizationUtils.FindGameObjectIncludingInactive(target.ObjectPath);
            if (exactObject != null)
            {
                AddMatchingFsms(exactObject.GetComponents<PlayMakerFSM>(), target, result);
            }

            if (result.Count == 0)
            {
                GameObject root = FindNearestGameObjectForPath(target.ObjectPath);
                if (root != null)
                {
                    PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
                    for (int i = 0; i < fsms.Length; i++)
                    {
                        PlayMakerFSM fsm = fsms[i];
                        if (fsm == null || !IsObjectPathMatch(LocalizationUtils.GetGameObjectPath(fsm.gameObject), target.ObjectPath))
                            continue;

                        AddMatchingFsm(fsm, target, result);
                    }
                }
            }

            if (result.Count > 0)
                fsmListCache[target.Key] = result;

            return result;
        }

        private PlayMakerFSM FindMatchingFsmByPrefix(FsmTarget target)
        {
            GameObject root = FindNearestGameObjectForPath(target.ObjectPath);
            if (root == null)
                return null;

            PlayMakerFSM[] fsms = root.GetComponentsInChildren<PlayMakerFSM>(true);
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null || !IsObjectPathMatch(LocalizationUtils.GetGameObjectPath(fsm.gameObject), target.ObjectPath))
                    continue;

                if (FsmMatches(fsm, target.FsmName, target.StateName))
                    return fsm;
            }

            return null;
        }

        private static void AddMatchingFsms(PlayMakerFSM[] fsms, FsmTarget target, List<PlayMakerFSM> result)
        {
            if (fsms == null)
                return;

            for (int i = 0; i < fsms.Length; i++)
            {
                AddMatchingFsm(fsms[i], target, result);
            }
        }

        private static void AddMatchingFsm(PlayMakerFSM fsm, FsmTarget target, List<PlayMakerFSM> result)
        {
            if (fsm == null)
                return;

            if (!string.IsNullOrEmpty(target.FsmName) && FsmUtils.GetFsmName(fsm) != target.FsmName)
                return;

            if (!string.IsNullOrEmpty(target.StateName) && FindState(fsm, target.StateName) == null)
                return;

            if (!result.Contains(fsm))
                result.Add(fsm);
        }

        private static PlayMakerFSM FindMatchingFsmOnObject(GameObject gameObject, string fsmName, string stateName)
        {
            if (gameObject == null)
                return null;

            PlayMakerFSM[] fsms = gameObject.GetComponents<PlayMakerFSM>();
            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (FsmMatches(fsm, fsmName, stateName))
                    return fsm;
            }

            return null;
        }

        private static bool FsmMatches(PlayMakerFSM fsm, string fsmName, string stateName)
        {
            if (fsm == null)
                return false;

            if (!string.IsNullOrEmpty(fsmName) && FsmUtils.GetFsmName(fsm) != fsmName)
                return false;

            if (!string.IsNullOrEmpty(stateName) && FindState(fsm, stateName) == null)
                return false;

            return true;
        }

        private static HutongGames.PlayMaker.FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || fsm.FsmStates == null || string.IsNullOrEmpty(stateName))
                return null;

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                HutongGames.PlayMaker.FsmState state = fsm.FsmStates[i];
                if (state != null && state.Name == stateName)
                    return state;
            }

            return null;
        }

        private GameObject FindNearestGameObjectForPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('/');
            for (int length = parts.Length; length >= 1; length--)
            {
                string candidate = string.Join("/", parts, 0, length);
                GameObject found = LocalizationUtils.FindGameObjectIncludingInactive(candidate);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool IsObjectPathMatch(string actualPath, string rulePath)
        {
            if (actualPath == rulePath)
                return true;

            if (string.IsNullOrEmpty(actualPath) || string.IsNullOrEmpty(rulePath) || !actualPath.StartsWith(rulePath, System.StringComparison.Ordinal))
                return false;

            if (actualPath.Length == rulePath.Length)
                return true;

            char next = actualPath[rulePath.Length];
            return next == '/' || char.IsDigit(next);
        }

        private static int GetArrayListGetIndex(object action)
        {
            if (action == null)
                return -1;

            FieldInfo atIndexField = FsmUtils.GetField(action.GetType(), "atIndex");
            if (atIndexField == null)
                return -1;

            HutongGames.PlayMaker.FsmInt atIndex = atIndexField.GetValue(action) as HutongGames.PlayMaker.FsmInt;
            return atIndex == null ? -1 : atIndex.Value;
        }

        private static PlayMakerArrayListProxy GetArrayListGetProxy(object action)
        {
            if (action == null)
                return null;

            FieldInfo proxyField = FsmUtils.GetField(action.GetType(), "proxy");
            return proxyField == null ? null : proxyField.GetValue(action) as PlayMakerArrayListProxy;
        }

        private bool IsFsmReady(PlayMakerFSM fsm)
        {
            if (fsm == null || fsm.Fsm == null)
                return false;

            if (!fsm.Fsm.Initialized)
            {
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
                        // Some FSMs are not safe to initialize early; their serialized actions can still be translated below.
                    }
                }
            }

            return fsm.FsmStates != null;
        }

        private static bool ShouldSkipActionField(FieldInfo field)
        {
            if (field == null)
                return true;

            string name = field.Name;
            return name == "fsm"
                || name == "state"
                || name == "owner"
                || name == "fsmName"
                || name == "variableName"
                || name == "stringVariable"
                || name == "stringParts"
                || name == "result"
                || name == "storeResult"
                || name == "output"
                || name == "outputString"
                || name == "eventTarget"
                || name == "gameObject"
                || name == "target"
                || name == "targetObject";
        }

        private static bool ShouldScanNestedFsmValue(FieldInfo field, object value)
        {
            if (value == null)
                return false;

            if (ShouldSkipActionField(field))
                return false;

            System.Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsArray)
                return false;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            string ns = type.Namespace ?? string.Empty;
            if (!ns.StartsWith("HutongGames.PlayMaker", System.StringComparison.Ordinal))
                return false;

            string typeName = type.Name;
            return typeName == "FsmVar"
                || typeName == "FsmProperty"
                || typeName == "NamedVariable";
        }

        private static string GetLastPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            int slashIndex = path.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex + 1 < path.Length
                ? path.Substring(slashIndex + 1)
                : path;
        }

        private static bool TextMatchesExact(string value, string expected)
        {
            return string.Equals(value, expected, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTranslatableDirectStringField(string fieldName)
        {
            return fieldName == "stringValue" || fieldName == "text" || fieldName == "m_stringGenerated";
        }

        private void ClearRuntimeCaches()
        {
            translationCache.Clear();
            indexedFsmCache.Clear();
            fsmListCache.Clear();
            arrayListProxyCache.Clear();
            textMeshCache.Clear();
            resolvedTargets.Clear();
            warnedTargets.Clear();
            initializedFsmIds.Clear();
        }
    }
}
