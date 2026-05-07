using UnityEngine;
using System.Collections.Generic;

namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization utilities for localization
    /// </summary>
    public static class MLCUtils
    {
        /// <summary>
        /// Format string for use as translation key (uppercase, no spaces/newlines)
        /// </summary>
        public static string FormatUpperKey(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;

            // Single-pass: skip whitespace chars and uppercase in one allocation
            char[] buffer = new char[original.Length];
            int len = 0;
            for (int i = 0; i < original.Length; i++)
            {
                char c = original[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    continue;
                buffer[len++] = char.ToUpperInvariant(c);
            }

            if (len == 0)
                return string.Empty;

            return new string(buffer, 0, len);
        }

        // Cache for GameObject paths to improve performance
        private static Dictionary<GameObject, string> pathCache = new Dictionary<GameObject, string>();
        // Cache for expensive GameObject.Find(path) lookups
        private static Dictionary<string, GameObject> gameObjectFindCache = new Dictionary<string, GameObject>();
        // Scene-local FSM index for inactive lookup helpers (resolved via Resources.FindObjectsOfTypeAll)
        private static Dictionary<string, PlayMakerFSM> inactiveFsmPathNameCache = new Dictionary<string, PlayMakerFSM>();
        private static bool fsmIndexBuilt = false;
        private static float lastFsmIndexBuildTime = -1000f;
        private const float FSM_INDEX_REFRESH_INTERVAL = 1f;

        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            // Check cache first
            if (pathCache.TryGetValue(obj, out string cachedPath))
                return cachedPath;

            // Build path using List + Reverse
            List<string> pathParts = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            // Reverse and join
            pathParts.Reverse();
            string path = string.Join("/", pathParts.ToArray());

            // Cache the path (limit cache size to prevent memory bloat)
            if (pathCache.Count < 10000)
            {
                pathCache[obj] = path;
            }

            return path;
        }

        /// <summary>
        /// Cached wrapper around GameObject.Find(path).
        /// Returns null when not found, and invalidates stale cached references.
        /// </summary>
        public static GameObject FindGameObjectCached(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (gameObjectFindCache.TryGetValue(path, out GameObject cachedObj))
            {
                if (cachedObj != null)
                    return cachedObj;

                gameObjectFindCache.Remove(path);
            }

            GameObject found = GameObject.Find(path);
            if (found != null)
            {
                gameObjectFindCache[path] = found;
            }

            return found;
        }

        /// <summary>
        /// Find PlayMakerFSM by object path + FSM name, including inactive objects.
        /// Uses a cache and falls back to Resources.FindObjectsOfTypeAll when needed.
        /// </summary>
        public static PlayMakerFSM FindFsmIncludingInactiveByPathAndName(string objectPath, string fsmName)
        {
            if (string.IsNullOrEmpty(objectPath) || string.IsNullOrEmpty(fsmName))
                return null;

            string cacheKey = objectPath + "|" + fsmName;

            PlayMakerFSM cachedFsm;
            if (TryGetCachedFsm(cacheKey, out cachedFsm))
            {
                return cachedFsm;
            }

            // Full inactive FSM scans are expensive. Build the index once, then only
            // refresh on cache misses after a cooldown so optional/missing targets do
            // not create a steady frametime spike.
            if (!fsmIndexBuilt || IsFsmIndexStale())
            {
                RebuildFsmIndex(Time.realtimeSinceStartup);
                if (TryGetCachedFsm(cacheKey, out cachedFsm))
                {
                    return cachedFsm;
                }
            }

            return null;
        }

        private static bool TryGetCachedFsm(string cacheKey, out PlayMakerFSM cachedFsm)
        {
            if (inactiveFsmPathNameCache.TryGetValue(cacheKey, out cachedFsm))
            {
                if (cachedFsm != null && cachedFsm.gameObject != null)
                    return true;

                inactiveFsmPathNameCache.Remove(cacheKey);
            }

            cachedFsm = null;
            return false;
        }

        /// <summary>
        /// Find PlayMakerFSMs by path prefix + FSM name, including inactive objects.
        /// Results are written into the provided list to avoid per-call allocations.
        /// </summary>
        public static void FindFsmsIncludingInactiveByPathPrefixAndName(string pathPrefix, string fsmName, List<PlayMakerFSM> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(fsmName))
                return;

            EnsureFsmIndexBuilt();
            AddMatchingFsmsByPrefix(pathPrefix, fsmName, results);

            if (IsFsmIndexStale())
            {
                RebuildFsmIndex(Time.realtimeSinceStartup);
                AddMatchingFsmsByPrefix(pathPrefix, fsmName, results);
            }
        }

        private static bool IsFsmIndexStale()
        {
            return Time.realtimeSinceStartup - lastFsmIndexBuildTime >= FSM_INDEX_REFRESH_INTERVAL;
        }

        private static void EnsureFsmIndexBuilt()
        {
            if (!fsmIndexBuilt)
                RebuildFsmIndex(Time.realtimeSinceStartup);
        }

        private static void AddMatchingFsmsByPrefix(string pathPrefix, string fsmName, List<PlayMakerFSM> results)
        {
            foreach (PlayMakerFSM fsm in inactiveFsmPathNameCache.Values)
            {
                if (fsm == null || fsm.gameObject == null)
                    continue;

                if (fsm.FsmName != fsmName)
                    continue;

                string path = GetGameObjectPath(fsm.gameObject);
                if (path.StartsWith(pathPrefix))
                {
                    results.Add(fsm);
                }
            }
        }

        private static void RebuildFsmIndex(float timestamp)
        {
            inactiveFsmPathNameCache.Clear();
            lastFsmIndexBuildTime = timestamp;
            fsmIndexBuilt = true;

            PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if (allFsms == null)
                return;

            for (int i = 0; i < allFsms.Length; i++)
            {
                PlayMakerFSM fsm = allFsms[i];
                if (fsm == null || fsm.gameObject == null || string.IsNullOrEmpty(fsm.FsmName))
                    continue;

                string path = GetGameObjectPath(fsm.gameObject);
                string key = path + "|" + fsm.FsmName;
                if (!inactiveFsmPathNameCache.ContainsKey(key))
                {
                    inactiveFsmPathNameCache[key] = fsm;
                }
            }
        }

        /// <summary>
        /// Shared accessor for all TextMeshes including inactive ones.
        /// </summary>
        public static TextMesh[] GetAllTextMeshesIncludingInactive()
        {
            return Resources.FindObjectsOfTypeAll<TextMesh>();
        }

        /// <summary>
        /// Clear all runtime caches.
        /// Call this on scene changes and reloads.
        /// </summary>
        public static void ClearCaches()
        {
            pathCache.Clear();
            gameObjectFindCache.Clear();
            inactiveFsmPathNameCache.Clear();
            fsmIndexBuilt = false;
            lastFsmIndexBuildTime = -1000f;
        }
    }
}
