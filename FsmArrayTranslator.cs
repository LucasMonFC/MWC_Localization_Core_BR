using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// MSC teletext database translator. Uses the safe in-game strategy:
    /// temporarily activate Systems/Teletext so the database ArrayLists populate,
    /// translate those lists, then restore the previous active state.
    /// </summary>
    public class FsmArrayTranslator : ITranslationSurface
    {
        public string Name { get { return "FsmArrayTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Slow; } }
        public bool IsComplete { get { return completed; } }

        private const string TeletextRootPath = "Systems/Teletext";
        private const string DatabasePath = "Systems/Teletext/VKTekstiTV/Database";

        private TranslationDictionary sharedTranslations;
        private string teletextFilePath;

        private readonly Dictionary<string, Dictionary<string, string>> categoryTranslations =
            new Dictionary<string, Dictionary<string, string>>();
        private readonly Dictionary<string, List<string>> indexBasedTranslations =
            new Dictionary<string, List<string>>();

        private readonly HashSet<string> translatedArrays = new HashSet<string>();
        private int lastLoadedTranslationCount;
        private bool completed;
        private bool activatedTeletextRoot;

        public void Initialize(TranslationContext ctx)
        {
            sharedTranslations = ctx.Translations;
            teletextFilePath = Path.Combine(ctx.AssetsFolder, "translate_teletext.txt");
            LoadTeletextTranslations(teletextFilePath);
            sharedTranslations.LoadPatternsFromFile(teletextFilePath);
        }

        public int InitialPass()
        {
            return ProcessTeletextDatabase();
        }

        public int MonitorTick(float deltaTime)
        {
            return ProcessTeletextDatabase();
        }

        public void ClearTranslations()
        {
            categoryTranslations.Clear();
            indexBasedTranslations.Clear();
            lastLoadedTranslationCount = 0;
            Reset();
        }

        public void Reset()
        {
            translatedArrays.Clear();
            completed = false;
            activatedTeletextRoot = false;
        }

        public void LoadTeletextTranslations(string filePath)
        {
            TranslationFileParser.ParseCategoryBasedFile(
                filePath,
                out Dictionary<string, Dictionary<string, string>> loadedCategoryTranslations,
                out Dictionary<string, List<string>> loadedIndexBasedTranslations);

            categoryTranslations.Clear();
            foreach (KeyValuePair<string, Dictionary<string, string>> category in loadedCategoryTranslations)
                categoryTranslations[category.Key] = category.Value;

            indexBasedTranslations.Clear();
            foreach (KeyValuePair<string, List<string>> category in loadedIndexBasedTranslations)
                indexBasedTranslations[category.Key] = category.Value;

            lastLoadedTranslationCount = 0;
            foreach (Dictionary<string, string> category in categoryTranslations.Values)
                lastLoadedTranslationCount += category.Count;
        }

        public int GetTranslationCount()
        {
            return lastLoadedTranslationCount;
        }

        private int ProcessTeletextDatabase()
        {
            if (completed)
                return 0;

            GameObject teletextRoot = LocalizationUtils.FindGameObjectIncludingInactive(TeletextRootPath);
            if (teletextRoot == null)
                return 0;

            if (!teletextRoot.activeSelf)
            {
                teletextRoot.SetActive(true);
                activatedTeletextRoot = true;
            }

            try
            {
                GameObject database = LocalizationUtils.FindGameObjectIncludingInactive(DatabasePath);
                if (database == null)
                    return 0;

                PlayMakerArrayListProxy[] proxies = database.GetComponents<PlayMakerArrayListProxy>();
                if (proxies == null || proxies.Length == 0)
                    return 0;

                int totalTranslated = 0;
                bool allKnownArraysComplete = true;

                for (int i = 0; i < proxies.Length; i++)
                {
                    PlayMakerArrayListProxy proxy = proxies[i];
                    if (proxy == null || string.IsNullOrEmpty(proxy.referenceName))
                        continue;

                    string categoryName = proxy.referenceName;
                    string arrayKey = i.ToString() + ":" + categoryName;
                    if (translatedArrays.Contains(arrayKey))
                        continue;

                    int translated = TranslateProxy(proxy, categoryName, out int fallbackCount);
                    bool isPopulated = proxy._arrayList != null && proxy._arrayList.Count > 0;
                    bool hasTranslations = HasTranslationsFor(categoryName);

                    if (translated > 0 || isPopulated || !hasTranslations)
                    {
                        if (translated > 0)
                        {
                            if (fallbackCount > 0)
                                CoreConsole.Print($"[FsmArrayTranslator] '{categoryName}': Used index fallback for {fallbackCount} items");
                            CoreConsole.Print($"[FsmArrayTranslator] Translated '{categoryName}' with {translated} items");
                            totalTranslated += translated;
                        }

                        translatedArrays.Add(arrayKey);
                    }

                    if (!translatedArrays.Contains(arrayKey))
                        allKnownArraysComplete = false;
                }

                if (allKnownArraysComplete)
                {
                    completed = true;
                    RestoreTeletextRootIfNeeded(teletextRoot);
                }

                return totalTranslated;
            }
            catch (System.Exception ex)
            {
                CoreConsole.Error($"[FsmArrayTranslator] Error processing teletext database: {ex.Message}");
                return 0;
            }
        }

        private void RestoreTeletextRootIfNeeded(GameObject teletextRoot)
        {
            if (!activatedTeletextRoot || teletextRoot == null)
                return;

            teletextRoot.SetActive(false);
            activatedTeletextRoot = false;
        }

        private bool HasTranslationsFor(string categoryName)
        {
            Dictionary<string, string> categoryDict;
            if (categoryTranslations.TryGetValue(categoryName, out categoryDict) && categoryDict != null && categoryDict.Count > 0)
                return true;

            List<string> indexList;
            return indexBasedTranslations.TryGetValue(categoryName, out indexList) && indexList != null && indexList.Count > 0;
        }

        private int TranslateProxy(PlayMakerArrayListProxy proxy, string categoryName, out int fallbackCount)
        {
            fallbackCount = 0;
            if (proxy == null || proxy._arrayList == null)
                return 0;

            int translated = TranslateArrayListInPlace(
                proxy._arrayList,
                categoryName,
                categoryTranslations,
                indexBasedTranslations,
                sharedTranslations,
                out fallbackCount);

            if (translated > 0)
                SyncPreFill(proxy, categoryName);

            return translated;
        }

        private void SyncPreFill(PlayMakerArrayListProxy proxy, string categoryName)
        {
            if (proxy == null || proxy.preFillStringList == null || proxy.preFillStringList.Count == 0)
                return;

            int ignoredFallbackCount;
            ArrayList preFillAsArray = new ArrayList();
            for (int i = 0; i < proxy.preFillStringList.Count; i++)
                preFillAsArray.Add(proxy.preFillStringList[i]);

            TranslateArrayListInPlace(
                preFillAsArray,
                categoryName,
                categoryTranslations,
                indexBasedTranslations,
                sharedTranslations,
                out ignoredFallbackCount);

            for (int i = 0; i < proxy.preFillStringList.Count && i < preFillAsArray.Count; i++)
                proxy.preFillStringList[i] = preFillAsArray[i] as string;
        }

        internal static int TranslateArrayListInPlace(
            ArrayList list,
            string categoryName,
            Dictionary<string, Dictionary<string, string>> categoryDict,
            Dictionary<string, List<string>> indexDict,
            TranslationDictionary fallback,
            out int fallbackCount)
        {
            fallbackCount = 0;
            if (list == null)
                return 0;

            Dictionary<string, string> categoryTranslations = null;
            bool hasCategoryTranslations = categoryDict != null
                && categoryDict.TryGetValue(categoryName, out categoryTranslations)
                && categoryTranslations != null
                && categoryTranslations.Count > 0;

            List<string> indexTranslations = null;
            bool hasIndexFallback = indexDict != null
                && indexDict.TryGetValue(categoryName, out indexTranslations)
                && indexTranslations != null
                && indexTranslations.Count > 0;

            int translatedCount = 0;
            int nonEmptySourceIndex = -1;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    continue;

                string original = list[i].ToString();
                string lookupKey = TranslationFileParser.NormalizeMultiLineKey(TranslationFileParser.UnescapeString(original));
                if (string.IsNullOrEmpty(lookupKey))
                    continue;

                nonEmptySourceIndex++;

                string translation = null;
                bool hasTranslation = hasCategoryTranslations && categoryTranslations.TryGetValue(lookupKey, out translation);

                if (!hasTranslation && fallback != null)
                    hasTranslation = fallback.TryGetExact(original, out translation);

                if (!hasTranslation && hasIndexFallback && nonEmptySourceIndex < indexTranslations.Count)
                {
                    translation = indexTranslations[nonEmptySourceIndex];
                    hasTranslation = !string.IsNullOrEmpty(translation);
                    if (hasTranslation)
                        fallbackCount++;
                }

                if (!hasTranslation || translation == original)
                    continue;

                list[i] = translation;
                translatedCount++;
            }

            return translatedCount;
        }
    }
}
