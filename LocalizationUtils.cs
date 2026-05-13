using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace MWC_Localization_Core
{
    /// <summary>
    /// String normalization, GameObject path/find caching, and inactive-FSM lookup helpers
    /// used throughout the localization pipeline.
    /// </summary>
    public static class LocalizationUtils
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

            EnsureFsmIndexFresh();

            PlayMakerFSM cachedFsm;
            if (inactiveFsmPathNameCache.TryGetValue(cacheKey, out cachedFsm)
                && cachedFsm != null
                && cachedFsm.gameObject != null)
            {
                return cachedFsm;
            }

            return null;
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

            EnsureFsmIndexFresh();

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

        private static void EnsureFsmIndexFresh()
        {
            float now = Time.realtimeSinceStartup;
            if (fsmIndexBuilt && now - lastFsmIndexBuildTime < LocalizationConstants.FSM_INDEX_REFRESH_INTERVAL)
                return;

            RebuildFsmIndex(now);
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

    /// <summary>
    /// PlayMaker FSM reflection helpers shared by translation hooks.
    /// Caches FieldInfo lookups to avoid repeated reflection costs during scene scans.
    /// </summary>
    internal static class FsmUtils
    {
        private static readonly Dictionary<System.Type, FieldInfo[]> fieldCache = new Dictionary<System.Type, FieldInfo[]>();
        private static readonly Dictionary<System.Type, FieldInfo> stringPartsFieldCache = new Dictionary<System.Type, FieldInfo>();

        public static string GetFsmName(PlayMakerFSM fsm)
        {
            if (fsm == null)
                return string.Empty;

            if (fsm.Fsm != null && !string.IsNullOrEmpty(fsm.Fsm.Name))
                return fsm.Fsm.Name;

            return fsm.FsmName ?? string.Empty;
        }

        public static FieldInfo GetField(System.Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static FieldInfo[] GetFields(System.Type type)
        {
            FieldInfo[] fields;
            if (!fieldCache.TryGetValue(type, out fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fieldCache[type] = fields;
            }

            return fields;
        }

        public static bool SetFsmStringValue(HutongGames.PlayMaker.FsmString target, string value)
        {
            if (target == null)
                return false;

            string safeValue = value ?? string.Empty;
            if (target.Value == safeValue)
                return false;

            target.Value = safeValue;
            return true;
        }

        public static bool SetNestedStringValue(object root, string value, params string[] fieldPath)
        {
            if (root == null || fieldPath == null || fieldPath.Length == 0)
                return false;

            object current = root;
            FieldInfo field = null;
            for (int i = 0; i < fieldPath.Length; i++)
            {
                field = GetField(current.GetType(), fieldPath[i]);
                if (field == null)
                    return false;

                if (i == fieldPath.Length - 1)
                    break;

                current = field.GetValue(current);
                if (current == null)
                    return false;
            }

            object existing = field.GetValue(current);
            HutongGames.PlayMaker.FsmString fsmString = existing as HutongGames.PlayMaker.FsmString;
            if (fsmString != null)
                return SetFsmStringValue(fsmString, value);

            if (field.FieldType == typeof(string))
            {
                string safeValue = value ?? string.Empty;
                if ((string)existing == safeValue)
                    return false;

                field.SetValue(current, safeValue);
                return true;
            }

            return false;
        }

        public static bool IsBuildStringAction(object action)
        {
            if (action == null)
                return false;

            string typeName = action.GetType().Name;
            return typeName == "BuildString" || typeName == "BuildStringFast" || typeName == "StringAddNewLine";
        }

        public static FieldInfo GetStringPartsField(object action)
        {
            if (action == null)
                return null;

            System.Type type = action.GetType();
            FieldInfo field;
            if (!stringPartsFieldCache.TryGetValue(type, out field))
            {
                field = GetField(type, "stringParts");
                stringPartsFieldCache[type] = field;
            }

            return field;
        }
    }
}
