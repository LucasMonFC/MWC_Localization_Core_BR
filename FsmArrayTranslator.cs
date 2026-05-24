using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Translates MSC teletext ArrayLists populated by SplitTextToArrayList.
    ///
    /// This follows the upstream main strategy: inject a translation action right after
    /// SplitTextToArrayList so the populated list is translated before readers consume it.
    /// The MSC port tracks only the teletext database path; TV chat targets are MWC-only.
    /// </summary>
    public class FsmArrayTranslator : ITranslationSurface
    {
        public string Name { get { return "FsmArrayTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return processedPaths.Count >= targetPaths.Count; } }

        private TranslationDictionary sharedTranslations;
        private string teletextFilePath;

        // Stable reference used by injected actions. F8 reload clears/refills it in place.
        private readonly Dictionary<string, string> teletextTranslations = new Dictionary<string, string>();

        private readonly HashSet<string> targetPaths = new HashSet<string>
        {
            "Systems/Teletext/VKTekstiTV/Database",
        };

        private readonly HashSet<string> processedPaths = new HashSet<string>();
        private readonly HashSet<int> initializedFsmIds = new HashSet<int>();

        public void Initialize(TranslationContext ctx)
        {
            sharedTranslations = ctx.Translations;
            teletextFilePath = Path.Combine(ctx.AssetsFolder, "translate_teletext.txt");
            LoadTeletextTranslations(teletextFilePath);
            sharedTranslations.LoadPatternsFromFile(teletextFilePath);
        }

        public int InitialPass()
        {
            return ProcessAllPaths();
        }

        public int MonitorTick(float deltaTime)
        {
            return ProcessAllPaths();
        }

        public void ClearTranslations()
        {
            teletextTranslations.Clear();
            Reset();
        }

        public void Reset()
        {
            processedPaths.Clear();
            initializedFsmIds.Clear();
        }

        public void LoadTeletextTranslations(string filePath)
        {
            Dictionary<string, Dictionary<string, string>> loadedCategoryTranslations;
            Dictionary<string, List<string>> ignoredIndexTranslations;
            TranslationFileParser.ParseCategoryBasedFile(
                filePath,
                out loadedCategoryTranslations,
                out ignoredIndexTranslations);

            teletextTranslations.Clear();
            foreach (Dictionary<string, string> category in loadedCategoryTranslations.Values)
            {
                foreach (KeyValuePair<string, string> pair in category)
                    teletextTranslations[pair.Key] = pair.Value;
            }
        }

        public int GetTranslationCount()
        {
            return teletextTranslations.Count;
        }

        private int ProcessAllPaths()
        {
            try
            {
                int totalTranslated = 0;
                foreach (string path in targetPaths)
                {
                    if (processedPaths.Contains(path))
                        continue;

                    GameObject go = LocalizationUtils.FindGameObjectIncludingInactive(path);
                    if (go == null)
                        continue;

                    int injected;
                    if (!TryInjectTranslationActions(go, out injected))
                        continue;

                    if (injected > 0)
                        CoreConsole.Print($"[FsmArrayTranslator] Injetou {injected} ação(ões) de tradução do teletexto em {path}");

                    totalTranslated += TranslatePopulatedProxies(go);
                    processedPaths.Add(path);
                }

                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[FsmArrayTranslator] Erro ao processar caminhos do teletexto: {ex.Message}");
                return 0;
            }
        }

        private bool TryInjectTranslationActions(GameObject go, out int totalInjected)
        {
            totalInjected = 0;
            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            for (int fi = 0; fi < fsms.Length; fi++)
            {
                PlayMakerFSM fsm = fsms[fi];
                if (!EnsureFsmInitialized(fsm) || fsm.FsmStates == null)
                    return false;

                for (int si = 0; si < fsm.FsmStates.Length; si++)
                {
                    HutongGames.PlayMaker.FsmState state = fsm.FsmStates[si];
                    if (state == null || state.Actions == null)
                        continue;

                    totalInjected += SpliceTranslationActionsInto(state, go);
                }
            }

            return true;
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
                    // Some FSMs are not safe to initialize early; retry once the game initializes them.
                }
            }

            return fsm.FsmStates != null;
        }

        private int SpliceTranslationActionsInto(HutongGames.PlayMaker.FsmState state, GameObject go)
        {
            HutongGames.PlayMaker.FsmStateAction[] oldActions = state.Actions;
            for (int i = 0; i < oldActions.Length; i++)
            {
                if (oldActions[i] is TranslateArrayListAction)
                    return 0;
            }

            List<HutongGames.PlayMaker.FsmStateAction> newActions = null;
            int injected = 0;

            for (int ai = 0; ai < oldActions.Length; ai++)
            {
                HutongGames.PlayMaker.FsmStateAction action = oldActions[ai];
                if (newActions != null)
                    newActions.Add(action);

                if (action == null || action.GetType().Name != "SplitTextToArrayList")
                    continue;

                TranslateArrayListAction injection = BuildInjectionAction(action, go);
                if (injection == null)
                    continue;

                if (newActions == null)
                {
                    newActions = new List<HutongGames.PlayMaker.FsmStateAction>(oldActions.Length + 2);
                    for (int j = 0; j <= ai; j++)
                        newActions.Add(oldActions[j]);
                }

                newActions.Add(injection);
                injected++;
            }

            if (newActions != null)
                state.Actions = newActions.ToArray();

            return injected;
        }

        private TranslateArrayListAction BuildInjectionAction(
            HutongGames.PlayMaker.FsmStateAction splitAction,
            GameObject go)
        {
            FieldInfo referenceField = FsmUtils.GetField(splitAction.GetType(), "reference");
            if (referenceField == null)
                return null;

            HutongGames.PlayMaker.FsmString referenceFsm = referenceField.GetValue(splitAction) as HutongGames.PlayMaker.FsmString;
            string referenceName = referenceFsm != null ? referenceFsm.Value : null;
            if (string.IsNullOrEmpty(referenceName))
                return null;

            PlayMakerArrayListProxy target = null;
            PlayMakerArrayListProxy[] proxies = go.GetComponents<PlayMakerArrayListProxy>();
            for (int i = 0; i < proxies.Length; i++)
            {
                if (proxies[i] != null && proxies[i].referenceName == referenceName)
                {
                    target = proxies[i];
                    break;
                }
            }

            if (target == null)
                return null;

            return new TranslateArrayListAction
            {
                target = target,
                teletextTranslations = teletextTranslations,
                sharedTranslations = sharedTranslations,
                label = referenceName,
            };
        }

        private int TranslatePopulatedProxies(GameObject go)
        {
            PlayMakerArrayListProxy[] proxies = go.GetComponents<PlayMakerArrayListProxy>();
            int totalTranslated = 0;

            for (int i = 0; i < proxies.Length; i++)
            {
                PlayMakerArrayListProxy proxy = proxies[i];
                if (proxy == null || string.IsNullOrEmpty(proxy.referenceName))
                    continue;

                int translated = TranslateArrayListInPlace(proxy._arrayList, teletextTranslations, sharedTranslations);
                if (translated > 0)
                {
                    CoreConsole.Print($"[FsmArrayTranslator] Traduziu ao vivo '{proxy.referenceName}' com {translated} itens");
                    totalTranslated += translated;
                }
            }

            return totalTranslated;
        }

        internal static int TranslateArrayListInPlace(
            ArrayList list,
            Dictionary<string, string> teletextDict,
            TranslationDictionary fallback)
        {
            if (list == null)
                return 0;

            int translatedCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    continue;

                string original = list[i].ToString();
                if (string.IsNullOrEmpty(original))
                    continue;

                string translation = null;
                string teletextKey = TranslationFileParser.NormalizeMultiLineKey(
                    TranslationFileParser.UnescapeString(original));

                if (!string.IsNullOrEmpty(teletextKey)
                    && teletextDict != null
                    && teletextDict.TryGetValue(teletextKey, out translation)
                    && translation == original)
                {
                    translation = null;
                }

                if (translation == null && fallback != null)
                {
                    fallback.TryGetExact(original, out translation);
                    if (translation == original)
                        translation = null;
                }

                if (string.IsNullOrEmpty(translation))
                    continue;

                list[i] = translation;
                translatedCount++;
            }

            return translatedCount;
        }

        public sealed class TranslateArrayListAction : HutongGames.PlayMaker.FsmStateAction
        {
            public PlayMakerArrayListProxy target;
            public Dictionary<string, string> teletextTranslations;
            public TranslationDictionary sharedTranslations;
            public string label;

            public override void OnEnter()
            {
                try
                {
                    if (target != null)
                    {
                        int translated = TranslateArrayListInPlace(target._arrayList, teletextTranslations, sharedTranslations);
                        if (translated > 0)
                            CoreConsole.Print($"[FsmArrayTranslator] '{label}' após split: traduziu {translated} entradas");
                    }
                }
                catch (System.Exception ex)
                {
                    CoreConsole.Error($"[FsmArrayTranslator] Erro no OnEnter ({label}): {ex.Message}");
                }

                Finish();
            }
        }
    }
}
