using System;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Hooks mod-owned text generation points that bypass the normal FSM/TextMesh
    /// translation pipeline. MagazineAPI product names are localized before they
    /// are stored, and hooks the Ruscko brochure where its confirmation and service
    /// lines are written.
    /// </summary>
    public sealed class ModTextTranslator : ITranslationSurface
    {
        private const string MagazineApiAssemblyName = "MagazineAPI";
        private const string MagazineOrderFormTypeName = "MagazineAPI.OrderFormManager";
        private const string RusckoControllerTypeName = "RusckoValnikPickup.RusckoBrochureController";
        private const string PoolGameTypeName = "MiniPool.MiniPoolMod";
        private static readonly string HarmonyId = "MWC_Localization_Core_BR.ModTextTranslator";
        private static TranslationDictionary activeTranslations;
        private static FieldInfo displayNameField;
        private static FieldInfo slotTextMeshesField;
        private static FieldInfo rusckoConfirmationTextField;
        private static FieldInfo rusckoReceiptLinesField;
        private static Type rusckoControllerType;
        private static object rusckoControllerInstance;
        private static HarmonyInstance harmony;
        private static bool magazineApiResolved;
        private static bool rusckoResolved;
        private static bool poolGameResolved;
        private static bool magazineApiHookFailureLogged;
        private static bool rusckoHookFailureLogged;

        public string Name { get { return "ModTextTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Fast; } }
        // Harmony hooks keep translating future writes after this polling surface retires.
        public bool IsComplete { get { return magazineApiResolved && rusckoResolved && poolGameResolved; } }

        public void Initialize(TranslationContext ctx)
        {
            activeTranslations = ctx != null ? ctx.Translations : null;
        }

        public int InitialPass()
        {
            return TryInstallMagazineApiHooks() + TryInstallRusckoHooks() + TryInstallPoolGameHooks();
        }

        public int MonitorTick(float deltaTime)
        {
            return TryInstallMagazineApiHooks() + TryInstallRusckoHooks() + TryInstallPoolGameHooks()
                + TranslateCurrentRusckoText(rusckoControllerInstance);
        }

        public void Reset()
        {
            // Harmony patches survive scene changes and keep translating future writes.
            rusckoControllerInstance = null;
        }

        public void ClearTranslations()
        {
            activeTranslations = null;
            rusckoControllerInstance = null;
        }

        private static int TryInstallMagazineApiHooks()
        {
            if (magazineApiResolved)
                return 0;

            Assembly apiAssembly = FindLoadedAssembly(MagazineApiAssemblyName);
            if (apiAssembly == null)
                return ResolveMissingMod(
                    ref magazineApiResolved,
                    "MagazineAPI não está carregada; sem traduções de produtos da revista nesta sessão.");

            try
            {
                Type orderFormType = apiAssembly.GetType(MagazineOrderFormTypeName, false);
                if (orderFormType == null)
                    return LogHookFailure("OrderFormManager não encontrado na MagazineAPI.");

                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo toggleProduct = orderFormType.GetMethod("ToggleProduct", instanceFlags);
                MethodInfo refreshSlotDisplay = orderFormType.GetMethod("RefreshSlotDisplay", instanceFlags);
                if (toggleProduct == null || refreshSlotDisplay == null)
                    return LogHookFailure("Métodos de atualização do pedido não encontrados na MagazineAPI.");

                MethodInfo beforeToggle = typeof(ModTextTranslator).GetMethod(
                    "BeforeMagazineApiToggleProduct", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo afterRefresh = typeof(ModTextTranslator).GetMethod(
                    "AfterMagazineApiRefreshSlotDisplay", BindingFlags.Static | BindingFlags.NonPublic);
                if (beforeToggle == null || afterRefresh == null)
                    return LogHookFailure("Hooks de tradução do MagazineAPI não encontrados.");

                EnsureHarmony();
                harmony.Patch(toggleProduct, prefix: new HarmonyMethod(beforeToggle));
                harmony.Patch(refreshSlotDisplay, postfix: new HarmonyMethod(afterRefresh));
                magazineApiResolved = true;
                CoreConsole.Print("[ModTextTranslator] Hooks instalados em MagazineAPI.OrderFormManager");
                return 1;
            }
            catch (Exception ex)
            {
                return LogHookFailure("Falha ao instalar hooks da MagazineAPI: " + ex.Message);
            }
        }

        private static int TryInstallRusckoHooks()
        {
            if (rusckoResolved)
                return 0;

            Type controllerType = FindLoadedType(RusckoControllerTypeName);
            if (controllerType == null)
                return ResolveMissingMod(
                    ref rusckoResolved,
                    "RusckoValnikPickup não está carregado; sem traduções do folheto nesta sessão.");

            try
            {
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo showMessage = controllerType.GetMethod("ShowMessage", instanceFlags);
                MethodInfo updatePageVisibility = controllerType.GetMethod("UpdatePageVisibility", instanceFlags);
                MethodInfo updateInvoice = controllerType.GetMethod("UpdateInvoice", instanceFlags);
                MethodInfo beforeShowMessage = typeof(ModTextTranslator).GetMethod(
                    "BeforeRusckoShowMessage", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo afterUpdatePageVisibility = typeof(ModTextTranslator).GetMethod(
                    "AfterRusckoUpdatePageVisibility", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo afterUpdateInvoice = typeof(ModTextTranslator).GetMethod(
                    "AfterRusckoUpdateInvoice", BindingFlags.Static | BindingFlags.NonPublic);

                rusckoConfirmationTextField = controllerType.GetField("_confirmationText", instanceFlags);
                rusckoReceiptLinesField = controllerType.GetField("_receiptLines", instanceFlags);
                rusckoControllerType = controllerType;

                if (showMessage == null || updatePageVisibility == null || updateInvoice == null
                    || beforeShowMessage == null || afterUpdatePageVisibility == null || afterUpdateInvoice == null
                    || rusckoConfirmationTextField == null || rusckoReceiptLinesField == null)
                {
                    return LogRusckoHookFailure("Os campos ou métodos do folheto do Ruscko não foram encontrados.");
                }

                EnsureHarmony();
                harmony.Patch(showMessage, prefix: new HarmonyMethod(beforeShowMessage));
                harmony.Patch(updatePageVisibility, postfix: new HarmonyMethod(afterUpdatePageVisibility));
                harmony.Patch(updateInvoice, postfix: new HarmonyMethod(afterUpdateInvoice));
                rusckoResolved = true;
                TranslateCurrentRusckoText(rusckoControllerInstance);
                CoreConsole.Print("[ModTextTranslator] Hooks instalados na confirmação e nos serviços do folheto do Ruscko");
                return 1;
            }
            catch (Exception ex)
            {
                return LogRusckoHookFailure("Falha ao instalar hooks do folheto do Ruscko: " + ex.Message);
            }
        }

        private static int TryInstallPoolGameHooks()
        {
            if (poolGameResolved)
                return 0;

            Type poolGameType = FindLoadedType(PoolGameTypeName);
            if (poolGameType == null)
                return ResolveMissingMod(
                    ref poolGameResolved,
                    "PoolGame não está carregado; sem tradução da mesa de sinuca nesta sessão.");

            try
            {
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo shadowLabel = null;
                MethodInfo colorLabel = null;
                MethodInfo say = null;
                MethodInfo[] methods = poolGameType.GetMethods(instanceFlags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name == "ShadowLabel" && method.GetParameters().Length == 3)
                        shadowLabel = method;
                    else if (method.Name == "ColorLabel" && method.GetParameters().Length == 4)
                        colorLabel = method;
                    else if (method.Name == "Say" && method.GetParameters().Length == 2)
                        say = method;
                }

                MethodInfo translateLabel = typeof(ModTextTranslator).GetMethod(
                    "BeforePoolGameLabel", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo translateMessage = typeof(ModTextTranslator).GetMethod(
                    "BeforePoolGameSay", BindingFlags.Static | BindingFlags.NonPublic);
                if (shadowLabel == null || colorLabel == null || say == null
                    || translateLabel == null || translateMessage == null)
                    return LogPoolGameHookFailure("Métodos de texto da interface do PoolGame não encontrados.");

                EnsureHarmony();
                HarmonyMethod prefix = new HarmonyMethod(translateLabel);
                harmony.Patch(shadowLabel, prefix: prefix);
                harmony.Patch(colorLabel, prefix: new HarmonyMethod(translateLabel));
                harmony.Patch(say, prefix: new HarmonyMethod(translateMessage));
                poolGameResolved = true;
                CoreConsole.Print("[ModTextTranslator] Hooks instalados na interface do PoolGame");
                return 1;
            }
            catch (Exception ex)
            {
                return LogPoolGameHookFailure("Falha ao instalar hooks do PoolGame: " + ex.Message);
            }
        }

        private static Type FindLoadedType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                try
                {
                    Type type = assembly.GetType(typeName, false);
                    if (type != null)
                        return type;
                }
                catch (Exception)
                {
                    // Ignore assemblies being loaded or unloaded during a scene change.
                }
            }

            return null;
        }

        private static HarmonyInstance EnsureHarmony()
        {
            if (harmony == null)
                harmony = HarmonyInstance.Create(HarmonyId);
            return harmony;
        }

        private static int LogHookFailure(string message)
        {
            magazineApiResolved = true;
            if (!magazineApiHookFailureLogged)
            {
                magazineApiHookFailureLogged = true;
                CoreConsole.Warning("[ModTextTranslator] " + message);
            }
            return 0;
        }

        private static int LogRusckoHookFailure(string message)
        {
            rusckoResolved = true;
            if (!rusckoHookFailureLogged)
            {
                rusckoHookFailureLogged = true;
                CoreConsole.Warning("[ModTextTranslator] " + message);
            }
            return 0;
        }

        private static int LogPoolGameHookFailure(string message)
        {
            poolGameResolved = true;
            CoreConsole.Warning("[ModTextTranslator] " + message);
            return 0;
        }

        private static int ResolveMissingMod(ref bool isResolved, string message)
        {
            // Mods are loaded before the GAME scene is marked ready. Avoid resolving
            // absence during MainMenu, where MSCLoader may not have loaded them yet.
            if (Application.loadedLevelName != "GAME")
                return 0;

            isResolved = true;
            CoreConsole.Print("[ModTextTranslator] " + message);
            return 0;
        }

        private static void BeforeMagazineApiToggleProduct(object __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (__0 == null || translations == null)
                return;

            try
            {
                if (displayNameField == null || displayNameField.DeclaringType != __0.GetType())
                {
                    displayNameField = __0.GetType().GetField(
                        "DisplayName",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (displayNameField == null || displayNameField.FieldType != typeof(string))
                    return;

                string source = displayNameField.GetValue(__0) as string;
                string translated;
                if (!string.IsNullOrEmpty(source)
                    && translations.TryGetExact(source, "MagazineAPI/OrderForm", out translated)
                    && !string.IsNullOrEmpty(translated)
                    && translated != source)
                {
                    displayNameField.SetValue(__0, translated);
                }
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir produto da revista: " + ex.Message);
            }
        }

        private static void BeforeRusckoShowMessage(object __instance, ref string __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (string.IsNullOrEmpty(__0) || translations == null)
                return;

            rusckoControllerInstance = __instance;
            string translated;
            if (translations.TryTranslate(__0, "RusckoBrochureController/RusckoBrochure_UI/Confimation", out translated)
                && !string.IsNullOrEmpty(translated))
            {
                __0 = translated;
            }
        }

        private static void BeforePoolGameLabel(ref string __1)
        {
            TranslationDictionary translations = activeTranslations;
            if (string.IsNullOrEmpty(__1) || translations == null)
                return;

            __1 = TranslatePoolGameText(__1, translations);
        }

        private static void BeforePoolGameSay(ref string __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (string.IsNullOrEmpty(__0) || translations == null)
                return;

            __0 = TranslatePoolGameText(__0, translations);
        }

        private static string TranslatePoolGameText(string source, TranslationDictionary translations)
        {
            string translated;
            if (translations.TryTranslate(source, "PoolGame/MiniPoolMod", out translated)
                && !string.IsNullOrEmpty(translated))
            {
                return translated;
            }

            // Bet result toasts combine a rule message and a money result with a
            // vertical bar. Translate each side so dynamic rule text still works.
            int separator = source.IndexOf('|');
            if (separator < 0)
                return source;

            string left = source.Substring(0, separator);
            string right = source.Substring(separator + 1);
            string leftTrimmed = left.Trim();
            string rightTrimmed = right.Trim();
            string leftTranslated;
            string rightTranslated;
            bool leftChanged = translations.TryTranslate(leftTrimmed, "PoolGame/MiniPoolMod", out leftTranslated)
                && !string.IsNullOrEmpty(leftTranslated) && leftTranslated != leftTrimmed;
            bool rightChanged = translations.TryTranslate(rightTrimmed, "PoolGame/MiniPoolMod", out rightTranslated)
                && !string.IsNullOrEmpty(rightTranslated) && rightTranslated != rightTrimmed;
            if (!leftChanged && !rightChanged)
                return source;

            return (leftChanged ? RestoreOuterWhitespace(left, leftTranslated) : left)
                + "|"
                + (rightChanged ? RestoreOuterWhitespace(right, rightTranslated) : right);
        }

        private static string RestoreOuterWhitespace(string original, string translated)
        {
            int start = 0;
            while (start < original.Length && char.IsWhiteSpace(original[start]))
                start++;

            int end = original.Length;
            while (end > start && char.IsWhiteSpace(original[end - 1]))
                end--;

            return original.Substring(0, start) + translated + original.Substring(end);
        }

        private static void AfterRusckoUpdatePageVisibility(object __instance)
        {
            TranslationDictionary translations = activeTranslations;
            if (__instance == null || translations == null || rusckoConfirmationTextField == null)
                return;

            try
            {
                rusckoControllerInstance = __instance;
                TextMesh confirmationText = rusckoConfirmationTextField.GetValue(__instance) as TextMesh;
                TranslateRusckoTextMesh(confirmationText, translations);
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir confirmação do folheto do Ruscko: " + ex.Message);
            }
        }

        private static void AfterRusckoUpdateInvoice(object __instance)
        {
            TranslationDictionary translations = activeTranslations;
            if (__instance == null || translations == null || rusckoReceiptLinesField == null)
                return;

            try
            {
                rusckoControllerInstance = __instance;
                Array lines = rusckoReceiptLinesField.GetValue(__instance) as Array;
                if (lines == null)
                    return;

                for (int i = 0; i < lines.Length; i++)
                    TranslateRusckoTextMesh(lines.GetValue(i) as TextMesh, translations);
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir os serviços do folheto do Ruscko: " + ex.Message);
            }
        }

        private static int TranslateCurrentRusckoText(object controller)
        {
            if (rusckoControllerType == null || controller == null || activeTranslations == null)
                return 0;

            try
            {
                int translated = 0;

                if (rusckoConfirmationTextField != null)
                {
                    if (TranslateRusckoTextMesh(
                        rusckoConfirmationTextField.GetValue(controller) as TextMesh,
                        activeTranslations))
                    {
                        translated++;
                    }
                }

                if (rusckoReceiptLinesField != null)
                {
                    Array lines = rusckoReceiptLinesField.GetValue(controller) as Array;
                    if (lines != null)
                    {
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (TranslateRusckoTextMesh(lines.GetValue(i) as TextMesh, activeTranslations))
                                translated++;
                        }
                    }
                }

                return translated;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao atualizar textos existentes do folheto do Ruscko: " + ex.Message);
                return 0;
            }
        }

        private static bool TranslateRusckoTextMesh(TextMesh textMesh, TranslationDictionary translations)
        {
            if (textMesh == null || translations == null || string.IsNullOrEmpty(textMesh.text))
                return false;

            string path = LocalizationUtils.GetGameObjectPath(textMesh.gameObject);
            string translated;
            if (!translations.TryTranslate(textMesh.text, path, out translated)
                || string.IsNullOrEmpty(translated)
                || translated == textMesh.text)
            {
                return false;
            }

            textMesh.text = translated;
            return true;
        }

        private static void AfterMagazineApiRefreshSlotDisplay(object __instance)
        {
            TranslationDictionary translations = activeTranslations;
            if (__instance == null || translations == null)
                return;

            try
            {
                if (slotTextMeshesField == null || slotTextMeshesField.DeclaringType != __instance.GetType())
                {
                    slotTextMeshesField = __instance.GetType().GetField(
                        "slotTextMeshes",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                Array slots = slotTextMeshesField != null ? slotTextMeshesField.GetValue(__instance) as Array : null;
                if (slots == null)
                    return;

                for (int i = 0; i < slots.Length; i++)
                {
                    TextMesh textMesh = slots.GetValue(i) as TextMesh;
                    if (textMesh == null || textMesh.gameObject == null || string.IsNullOrEmpty(textMesh.text))
                        continue;

                    string path = LocalizationUtils.GetGameObjectPath(textMesh.gameObject);
                    string translated;
                    if (translations.TryTranslate(textMesh.text, path, out translated)
                        && !string.IsNullOrEmpty(translated)
                        && translated != textMesh.text)
                    {
                        textMesh.text = translated;
                    }
                }
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao atualizar linhas da revista: " + ex.Message);
            }
        }

        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                try
                {
                    if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                        return assembly;
                }
                catch (Exception)
                {
                    // Ignore assemblies being unloaded during scene transitions.
                }
            }

            return null;
        }
    }
}
