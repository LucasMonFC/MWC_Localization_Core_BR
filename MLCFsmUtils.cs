using System.Collections.Generic;
using System.Reflection;

namespace MWC_Localization_Core
{
    /// <summary>
    /// PlayMaker FSM reflection helpers shared by translation hooks.
    /// Caches FieldInfo lookups to avoid repeated reflection costs during scene scans.
    /// </summary>
    internal static class MLCFsmUtils
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
