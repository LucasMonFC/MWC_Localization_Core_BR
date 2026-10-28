using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace MWC_Localization_Core
{
    /// <summary>
    /// Hooks mod-owned text generation points that bypass the normal FSM/TextMesh
    /// translation pipeline. MagazineAPI product names are localized before they
    /// are stored, the Ruscko brochure is translated at its write points, and
    /// computer panels from Rivett, Bills Online and Classifieds are translated
    /// at their I386 text output points.
    /// </summary>
    public sealed class ModTextTranslator : ITranslationSurface
    {
        private const string MagazineApiAssemblyName = "MagazineAPI";
        private const string MagazineOrderFormTypeName = "MagazineAPI.OrderFormManager";
        private const string RusckoControllerTypeName = "RusckoValnikPickup.RusckoBrochureController";
        private const string PoolGameTypeName = "MiniPool.MiniPoolMod";
        private const string RivettTerminalTypeName = "RivettPartsService.TerminalCatalog";
        private const string RivettComputerApiTypeName = "RivettPartsService.ComputerApi";
        private const string BillsOnlineTypeName = "BillsOnline.Bills";
        private const string ClassifiedsOnlineTypeName = "ClassifiedsOnlineCatalogue.Cat";
        private const string I386TypeName = "I386API.I386";
        private static readonly string HarmonyId = "MWC_Localization_Core_BR.ModTextTranslator";
        private static TranslationDictionary activeTranslations;
        private static FieldInfo displayNameField;
        private static FieldInfo slotTextMeshesField;
        private static FieldInfo rusckoConfirmationTextField;
        private static FieldInfo rusckoReceiptLinesField;
        private static FieldInfo rivettMessageField;
        private static FieldInfo rivettPhoneSearchField;
        private static FieldInfo rivettPhoneProductsField;
        private static FieldInfo rivettTerminalScreenField;
        private static int i386OutputContext;
        private static PropertyInfo rivettProductLabelProperty;
        private static PropertyInfo rivettProductGroupProperty;
        private static MethodInfo rivettGroupNameMethod;
        private static readonly List<string> rivettPhonePartSourceNames = new List<string>();
        private static readonly HashSet<string> rivettPhonePartSourceNameSet = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> rivettPhoneTranslatedSearchNames = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, bool> rivettPhoneSearchGroupCache = new Dictionary<string, bool>();
        private static string rivettPhoneSearchCacheQuery;
        private static Type rusckoControllerType;
        private static object rusckoControllerInstance;
        private static HarmonyInstance harmony;
        private static bool magazineApiResolved;
        private static bool rusckoResolved;
        private static bool poolGameResolved;
        private static bool rivettResolved;
        private static bool billsOnlineResolved;
        private static bool classifiedsOnlineResolved;
        private static bool i386OutputHooksInstalled;
        private static bool rivettTerminalCatalogBuilding;
        private static bool magazineApiHookFailureLogged;
        private static bool rusckoHookFailureLogged;
        private static bool rivettPhoneWindowDrawing;

        public string Name { get { return "ModTextTranslator"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.Fast; } }
        // Harmony hooks keep translating future writes after this polling surface retires.
        public bool IsComplete { get { return magazineApiResolved && rusckoResolved && poolGameResolved && rivettResolved
            && billsOnlineResolved && classifiedsOnlineResolved; } }

        public void Initialize(TranslationContext ctx)
        {
            activeTranslations = ctx != null ? ctx.Translations : null;
            RebuildRivettPhoneSearchIndex();
        }

        public int InitialPass()
        {
            return TryInstallMagazineApiHooks() + TryInstallRusckoHooks() + TryInstallPoolGameHooks()
                + TryInstallRivettHooks() + TryInstallBillsOnlineHooks() + TryInstallClassifiedsOnlineHooks();
        }

        public int MonitorTick(float deltaTime)
        {
            return TryInstallMagazineApiHooks() + TryInstallRusckoHooks() + TryInstallPoolGameHooks()
                + TryInstallRivettHooks()
                + TryInstallBillsOnlineHooks() + TryInstallClassifiedsOnlineHooks()
                + TranslateCurrentRusckoText(rusckoControllerInstance);
        }

        public void Reset()
        {
            // Harmony patches survive scene changes and keep translating future writes.
            rusckoControllerInstance = null;
            rivettPhoneWindowDrawing = false;
            rivettTerminalCatalogBuilding = false;
            i386OutputContext = 0;
            ClearRivettPhoneSearchCache();
        }

        public void ClearTranslations()
        {
            activeTranslations = null;
            rusckoControllerInstance = null;
            rivettPhoneTranslatedSearchNames.Clear();
            ClearRivettPhoneSearchCache();
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

        private static int TryInstallRivettHooks()
        {
            if (rivettResolved)
                return 0;

            Type terminalType = FindLoadedType(RivettTerminalTypeName);
            if (terminalType == null)
                return ResolveMissingMod(
                    ref rivettResolved,
                    "Rivett Parts Service não está carregado; sem tradução do catálogo do computador nesta sessão.");

            try
            {
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo addRow = terminalType.GetMethod("Add", instanceFlags);
                MethodInfo textEntry = terminalType.GetMethod("TextEntry", instanceFlags);
                MethodInfo render = terminalType.GetMethod("Render", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo renderColored = terminalType.GetMethod("RenderColored", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo key = terminalType.GetMethod("Key", instanceFlags);
                MethodInfo translateRow = typeof(ModTextTranslator).GetMethod(
                    "BeforeRivettTerminalText", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo translateRender = typeof(ModTextTranslator).GetMethod(
                    "AfterRivettTerminalRender", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo translateMessage = typeof(ModTextTranslator).GetMethod(
                    "AfterRivettTerminalKey", BindingFlags.Static | BindingFlags.NonPublic);

                rivettMessageField = terminalType.GetField("message", instanceFlags);
                if (addRow == null || textEntry == null || render == null || renderColored == null || key == null
                    || rivettMessageField == null || translateRow == null || translateRender == null || translateMessage == null)
                    return LogRivettHookFailure("Métodos de texto do catálogo do computador não encontrados.");

                EnsureHarmony();
                HarmonyMethod beforeText = new HarmonyMethod(translateRow);
                harmony.Patch(addRow, prefix: beforeText);
                harmony.Patch(textEntry, prefix: new HarmonyMethod(translateRow));
                harmony.Patch(render, postfix: new HarmonyMethod(translateRender));
                harmony.Patch(renderColored, postfix: new HarmonyMethod(translateRender));
                harmony.Patch(key, postfix: new HarmonyMethod(translateMessage));

                InstallRivettPhoneWindowHooks(terminalType.Assembly);
                InstallRivettComputerSearchHooks(terminalType.Assembly);

                Type computerApiType = FindLoadedType(RivettComputerApiTypeName);
                MethodInfo notice = computerApiType != null ? computerApiType.GetMethod("Notice", instanceFlags) : null;
                if (notice != null)
                    harmony.Patch(notice, prefix: new HarmonyMethod(translateRow));

                rivettResolved = true;
                CoreConsole.Print("[ModTextTranslator] Hooks instalados no terminal e no painel do Rivett Parts Service");
                return 1;
            }
            catch (Exception ex)
            {
                return LogRivettHookFailure("Falha ao instalar hook do catálogo do Rivett Parts Service: " + ex.Message);
            }
        }

        private static int TryInstallBillsOnlineHooks()
        {
            if (billsOnlineResolved)
                return 0;

            Type billsType = FindLoadedType(BillsOnlineTypeName);
            if (billsType == null)
                return ResolveMissingMod(ref billsOnlineResolved,
                    "BillsOnline não está carregado; sem tradução do painel de contas do computador nesta sessão.");

            try
            {
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo update = billsType.GetMethod("command_update", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo begin = typeof(ModTextTranslator).GetMethod("BeforeBillsOnlineUpdate", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo end = typeof(ModTextTranslator).GetMethod("AfterBillsOnlineUpdate", BindingFlags.Static | BindingFlags.NonPublic);
                if (update == null || begin == null || end == null)
                    return LogOnlineModHookFailure(ref billsOnlineResolved, "Métodos de exibição do BillsOnline não encontrados.");

                EnsureI386OutputHooks();
                EnsureHarmony().Patch(update, prefix: new HarmonyMethod(begin), postfix: new HarmonyMethod(end));
                billsOnlineResolved = true;
                CoreConsole.Print("[ModTextTranslator] Hook instalado para BillsOnline");
                return 1;
            }
            catch (Exception ex)
            {
                return LogOnlineModHookFailure(ref billsOnlineResolved, "Falha ao instalar hook do BillsOnline: " + ex.Message);
            }
        }

        private static int TryInstallClassifiedsOnlineHooks()
        {
            if (classifiedsOnlineResolved)
                return 0;

            Type catalogueType = FindLoadedType(ClassifiedsOnlineTypeName);
            if (catalogueType == null)
                return ResolveMissingMod(ref classifiedsOnlineResolved,
                    "ClassifiedsOnlineCatalogue não está carregado; sem tradução do catálogo de classificados nesta sessão.");

            try
            {
                const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo update = catalogueType.GetMethod("update", instanceFlags, null, Type.EmptyTypes, null);
                MethodInfo begin = typeof(ModTextTranslator).GetMethod("BeforeClassifiedsOnlineUpdate", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo end = typeof(ModTextTranslator).GetMethod("AfterClassifiedsOnlineUpdate", BindingFlags.Static | BindingFlags.NonPublic);
                if (update == null || begin == null || end == null)
                    return LogOnlineModHookFailure(ref classifiedsOnlineResolved, "Métodos de exibição do ClassifiedsOnlineCatalogue não encontrados.");

                EnsureI386OutputHooks();
                EnsureHarmony().Patch(update, prefix: new HarmonyMethod(begin), postfix: new HarmonyMethod(end));
                classifiedsOnlineResolved = true;
                CoreConsole.Print("[ModTextTranslator] Hook instalado para ClassifiedsOnlineCatalogue");
                return 1;
            }
            catch (Exception ex)
            {
                return LogOnlineModHookFailure(ref classifiedsOnlineResolved,
                    "Falha ao instalar hook do ClassifiedsOnlineCatalogue: " + ex.Message);
            }
        }

        private static void EnsureI386OutputHooks()
        {
            if (i386OutputHooksInstalled)
                return;

            Type i386Type = FindLoadedType(I386TypeName);
            if (i386Type == null)
                throw new MissingMemberException("I386API.I386 não está carregado.");

            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo write = i386Type.GetMethod("POS_Write", staticFlags, null, new Type[] { typeof(string) }, null);
            MethodInfo writeNewLine = i386Type.GetMethod("POS_WriteNewLine", staticFlags, null, new Type[] { typeof(string) }, null);
            MethodInfo translate = typeof(ModTextTranslator).GetMethod("BeforeOnlineComputerOutput", BindingFlags.Static | BindingFlags.NonPublic);
            if (write == null || writeNewLine == null || translate == null)
                throw new MissingMemberException("Métodos de saída de texto do I386 não encontrados.");

            EnsureHarmony().Patch(write, prefix: new HarmonyMethod(translate));
            EnsureHarmony().Patch(writeNewLine, prefix: new HarmonyMethod(translate));
            i386OutputHooksInstalled = true;
        }

        private static int LogOnlineModHookFailure(ref bool resolved, string message)
        {
            resolved = true;
            CoreConsole.Warning("[ModTextTranslator] " + message);
            return 0;
        }

        private static void BeforeBillsOnlineUpdate()
        {
            i386OutputContext = 1;
        }

        private static void AfterBillsOnlineUpdate()
        {
            i386OutputContext = 0;
        }

        private static void BeforeClassifiedsOnlineUpdate()
        {
            i386OutputContext = 2;
        }

        private static void AfterClassifiedsOnlineUpdate()
        {
            i386OutputContext = 0;
        }

        private static void BeforeOnlineComputerOutput(ref string __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (i386OutputContext == 0 || string.IsNullOrEmpty(__0) || translations == null)
                return;

            string[] lines = __0.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;

                int leading = 0;
                while (leading < lines[i].Length && char.IsWhiteSpace(lines[i][leading]))
                    leading++;
                int trailing = lines[i].Length;
                while (trailing > leading && char.IsWhiteSpace(lines[i][trailing - 1]))
                    trailing--;
                string body = lines[i].Substring(leading, trailing - leading);
                string translated = TranslateOnlineComputerLine(body, translations, i386OutputContext);
                if (translated != body)
                {
                    string prefix = lines[i].Substring(0, leading);
                    if (i386OutputContext == 2)
                        prefix = RecenterClassifiedsLine(prefix, body, translated);
                    lines[i] = prefix + translated + lines[i].Substring(trailing);
                }
            }

            __0 = string.Join("\n", lines);
        }

        private static string TranslateOnlineComputerLine(string source, TranslationDictionary translations, int context)
        {
            if (context == 1)
            {
                string dueDate = TranslateBillsDueDate(source, translations);
                if (dueDate != source)
                    return dueDate;
            }
            else if (context == 2)
            {
                const string partPrefix = "++ ";
                if (source.StartsWith(partPrefix, StringComparison.Ordinal))
                {
                    string partName = source.Substring(partPrefix.Length);
                    string translatedPart;
                    if (translations.TryTranslate(partName, null, out translatedPart) && !string.IsNullOrEmpty(translatedPart))
                        return partPrefix + CapitalizePartName(translatedPart);
                }

            }

            string translated;
            if (!translations.TryTranslate(source, null, out translated) || string.IsNullOrEmpty(translated))
                return source;

            return context == 1 ? PreserveBillsTabColumns(source, translated) : translated;
        }

        private static string PreserveBillsTabColumns(string source, string translated)
        {
            int sourceSearch = 0;
            int translatedSearch = 0;
            while (true)
            {
                int sourceTab = source.IndexOf('\t', sourceSearch);
                int translatedTab = translated.IndexOf('\t', translatedSearch);
                if (sourceTab < 0 || translatedTab < 0)
                    return translated;

                if (translatedTab < sourceTab)
                {
                    int padding = sourceTab - translatedTab;
                    translated = translated.Insert(translatedTab, new string(' ', padding));
                    translatedTab += padding;
                }

                sourceSearch = sourceTab + 1;
                translatedSearch = translatedTab + 1;
            }
        }

        private static string RecenterClassifiedsLine(string prefix, string originalBody, string translatedBody)
        {
            // ClassifiedsOnlineCatalogue centers its POS text with a fixed run of
            // spaces after the leading tabs. Compensate for changed Portuguese
            // string lengths so those lines remain visually centered.
            int lastTab = prefix.LastIndexOf('\t');
            if (lastTab < 0)
                return prefix;

            int paddingStart = lastTab + 1;
            int paddingLength = prefix.Length - paddingStart;
            if (paddingLength == 0)
                return prefix;

            for (int i = paddingStart; i < prefix.Length; i++)
            {
                if (prefix[i] != ' ')
                    return prefix;
            }

            int adjustment = (originalBody.Length - translatedBody.Length) / 2;
            int newPaddingLength = Math.Max(0, paddingLength + adjustment);
            if (newPaddingLength == paddingLength)
                return prefix;

            return prefix.Substring(0, paddingStart) + new string(' ', newPaddingLength);
        }

        private static string CapitalizePartName(string value)
        {
            string lowercase = value.ToLowerInvariant();
            for (int i = 0; i < lowercase.Length; i++)
            {
                if (!char.IsLetter(lowercase[i]))
                    continue;

                return lowercase.Substring(0, i) + char.ToUpperInvariant(lowercase[i]) + lowercase.Substring(i + 1);
            }

            return lowercase;
        }

        private static string TranslateBillsDueDate(string source, TranslationDictionary translations)
        {
            int at = source.IndexOf(" at ", StringComparison.Ordinal);
            if (at <= 0)
                return source;

            string due = source.Substring(0, at);
            string time = source.Substring(at + 4);
            string translatedDue;
            if (due.StartsWith("Next week on ", StringComparison.Ordinal))
            {
                string nextWeekPrefix;
                string weekday;
                if (!TryGetBillsDateText(translations, "Bills Date Next Week Prefix", out nextWeekPrefix)
                    || !TryTranslateBillsWeekday(due.Substring(13), translations, out weekday))
                    return source;

                translatedDue = nextWeekPrefix + " " + weekday;
            }
            else if (due.StartsWith("In ", StringComparison.Ordinal) && due.IndexOf(" week(s) on ", StringComparison.Ordinal) >= 0)
            {
                int on = due.IndexOf(" week(s) on ", StringComparison.Ordinal);
                string weeks = due.Substring(3, on - 3);
                string inPrefix;
                string unit;
                string weekday;
                string unitKey = weeks == "1" ? "Bills Date Week Singular" : "Bills Date Week Plural";
                if (!TryGetBillsDateText(translations, "Bills Date In Prefix", out inPrefix)
                    || !TryGetBillsDateText(translations, unitKey, out unit)
                    || !TryTranslateBillsWeekday(due.Substring(on + 12), translations, out weekday))
                    return source;

                translatedDue = inPrefix + " " + weeks + " " + unit + ", " + weekday;
            }
            else if (IsBillsWeekday(due))
            {
                if (!TryTranslateBillsWeekday(due, translations, out translatedDue))
                    return source;
            }
            else
                return source;

            string timeConnector;
            if (!TryGetBillsDateText(translations, "Bills Date Time Connector", out timeConnector))
                return source;

            return translatedDue + " " + timeConnector + " " + time;
        }

        private static bool TryTranslateBillsWeekday(string source, TranslationDictionary translations, out string result)
        {
            result = null;
            string translated;
            string weekdayPrefix;
            if (!translations.TryTranslate(source, null, out translated) || string.IsNullOrEmpty(translated)
                || !TryGetBillsDateText(translations, "Bills Date Weekday Prefix", out weekdayPrefix))
                return false;

            result = weekdayPrefix + " " + translated.ToLowerInvariant();
            return true;
        }

        private static bool TryGetBillsDateText(TranslationDictionary translations, string key, out string value)
        {
            value = null;
            return translations != null
                && translations.TryGetExact(key, null, out value)
                && !string.IsNullOrEmpty(value);
        }

        private static bool IsBillsWeekday(string value)
        {
            return value == "Monday" || value == "Tuesday" || value == "Wednesday"
                || value == "Thursday" || value == "Friday" || value == "Saturday" || value == "Sunday";
        }

        private static void InstallRivettPhoneWindowHooks(Assembly assembly)
        {
            Type windowType = assembly.GetType("RivettPartsService.CatalogWindow", false);
            if (windowType == null)
                throw new MissingMemberException("CatalogWindow não encontrado no Rivett Parts Service.");

            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo draw = windowType.GetMethod("Draw", instanceFlags, null, Type.EmptyTypes, null);
            MethodInfo contents = windowType.GetMethod("Contents", instanceFlags, null, new Type[] { typeof(int) }, null);
            Type productType = assembly.GetType("RivettPartsService.Product", false);
            MethodInfo matches = productType != null
                ? windowType.GetMethod("Matches", instanceFlags, null, new Type[] { productType }, null)
                : null;
            MethodInfo beginDraw = typeof(ModTextTranslator).GetMethod(
                "BeforeRivettPhoneWindowDraw", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo endDraw = typeof(ModTextTranslator).GetMethod(
                "AfterRivettPhoneWindowDraw", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo guiText = typeof(ModTextTranslator).GetMethod(
                "BeforeRivettPhoneWindowText", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo guiControlText = typeof(ModTextTranslator).GetMethod(
                "BeforeRivettPhoneGuiControlText", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo modalTitle = typeof(ModTextTranslator).GetMethod(
                "BeforeRivettPhoneWindowTitle", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo localizedMatches = typeof(ModTextTranslator).GetMethod(
                "AfterRivettPhoneMatches", BindingFlags.Static | BindingFlags.NonPublic);
            if (draw == null || contents == null || matches == null || beginDraw == null || endDraw == null || guiText == null
                || guiControlText == null || modalTitle == null)
                throw new MissingMemberException("Métodos de desenho do painel por telefone não encontrados.");

            EnsureHarmony();
            CaptureRivettPhonePartNames(assembly);
            harmony.Patch(draw, prefix: new HarmonyMethod(beginDraw), postfix: new HarmonyMethod(endDraw));
            // Unity may invoke the ModalWindow callback after Draw has returned.
            // Keep the text hooks scoped to the callback that actually emits the panel.
            harmony.Patch(contents, prefix: new HarmonyMethod(beginDraw), postfix: new HarmonyMethod(endDraw));
            if (localizedMatches == null)
                throw new MissingMemberException("Hook de busca traduzida do painel por telefone não encontrado.");
            harmony.Patch(matches, postfix: new HarmonyMethod(localizedMatches));

            int guiTextCandidates = 0;
            int patchedGuiTextMethods = 0;
            MethodInfo[] layoutMethods = typeof(GUILayout).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < layoutMethods.Length; i++)
            {
                MethodInfo method = layoutMethods[i];
                if (method.Name != "Label" && method.Name != "Button")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                    continue;

                guiTextCandidates++;
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(guiText));
                    patchedGuiTextMethods++;
                }
                catch (Exception)
                {
                    // Some Unity overloads can be native-backed on certain game builds.
                    // Keep the managed overloads that remain patchable.
                }
            }

            int modalTitleCandidates = 0;
            int patchedModalTitles = 0;
            MethodInfo[] guiMethods = typeof(GUI).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < guiMethods.Length; i++)
            {
                MethodInfo method = guiMethods[i];
                if (method.Name != "ModalWindow")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length <= 3 || parameters[3].ParameterType != typeof(string))
                    continue;

                modalTitleCandidates++;
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(modalTitle));
                    patchedModalTitles++;
                }
                catch (Exception)
                {
                    // The catalog text hooks still work if a native title overload cannot be patched.
                }
            }

            if (patchedGuiTextMethods == 0)
                throw new MissingMemberException("Métodos Label/Button da Unity não aceitaram hook.");

            if (patchedGuiTextMethods < guiTextCandidates || patchedModalTitles < modalTitleCandidates)
                CoreConsole.Warning("[ModTextTranslator] Algumas sobrecargas nativas da janela Rivett não aceitaram hook; as demais foram instaladas.");

            int guiControlCandidates = 0;
            int patchedGuiControls = 0;
            for (int i = 0; i < guiMethods.Length; i++)
            {
                MethodInfo method = guiMethods[i];
                if (method.Name != "Label" && method.Name != "Button")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length <= 1 || parameters[1].ParameterType != typeof(string))
                    continue;

                guiControlCandidates++;
                try
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(guiControlText));
                    patchedGuiControls++;
                }
                catch (Exception)
                {
                    // GUILayout ultimately draws through GUI.Label/Button on Unity versions
                    // where those managed entry points are patchable.
                }
            }

            if (patchedGuiControls == 0)
                CoreConsole.Warning("[ModTextTranslator] Nenhuma sobrecarga de GUI.Label/Button aceitou hook.");
        }

        private static void InstallRivettComputerSearchHooks(Assembly assembly)
        {
            Type terminalType = assembly.GetType(RivettTerminalTypeName, false);
            Type productType = assembly.GetType("RivettPartsService.Product", false);
            if (terminalType == null || productType == null)
                throw new MissingMemberException("TerminalCatalog/Product não encontrado para habilitar a busca traduzida.");

            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo build = terminalType.GetMethod("Build", instanceFlags, null, Type.EmptyTypes, null);
            PropertyInfo label = productType.GetProperty("Label", instanceFlags);
            MethodInfo labelGetter = label != null ? label.GetGetMethod(true) : null;
            MethodInfo beginBuild = typeof(ModTextTranslator).GetMethod(
                "BeforeRivettTerminalBuild", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo endBuild = typeof(ModTextTranslator).GetMethod(
                "AfterRivettTerminalBuild", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo includeTranslation = typeof(ModTextTranslator).GetMethod(
                "AfterRivettTerminalProductLabel", BindingFlags.Static | BindingFlags.NonPublic);
            rivettTerminalScreenField = terminalType.GetField("screen", instanceFlags);
            if (build == null || labelGetter == null || beginBuild == null || endBuild == null || includeTranslation == null
                || rivettTerminalScreenField == null)
                throw new MissingMemberException("Membros de pesquisa do terminal Rivett não encontrados.");

            EnsureHarmony();
            harmony.Patch(build, prefix: new HarmonyMethod(beginBuild), postfix: new HarmonyMethod(endBuild));
            harmony.Patch(labelGetter, postfix: new HarmonyMethod(includeTranslation));
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

        private static int LogRivettHookFailure(string message)
        {
            rivettResolved = true;
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

        private static void BeforeRivettTerminalText(ref string __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (string.IsNullOrEmpty(__0) || translations == null)
                return;

            string source = __0;
            string translated = TranslateRivettText(source, translations);
            if (IsRivettPhonePartLine(source))
                translated = FormatRivettPartCasing(translated);
            __0 = translated;
        }

        private static void BeforeRivettPhoneWindowDraw()
        {
            rivettPhoneWindowDrawing = true;
        }

        private static void AfterRivettPhoneWindowDraw()
        {
            rivettPhoneWindowDrawing = false;
        }

        private static void BeforeRivettPhoneWindowText(ref string __0)
        {
            TranslationDictionary translations = activeTranslations;
            if (!rivettPhoneWindowDrawing || string.IsNullOrEmpty(__0) || translations == null)
                return;

            __0 = TranslateRivettPhoneText(__0, translations);
        }

        private static void BeforeRivettPhoneWindowTitle(ref string __3)
        {
            TranslationDictionary translations = activeTranslations;
            if (!rivettPhoneWindowDrawing || string.IsNullOrEmpty(__3) || translations == null)
                return;

            __3 = TranslateRivettText(__3, translations);
        }

        private static void BeforeRivettPhoneGuiControlText(ref string __1)
        {
            TranslationDictionary translations = activeTranslations;
            if (!rivettPhoneWindowDrawing || string.IsNullOrEmpty(__1) || translations == null)
                return;

            __1 = TranslateRivettPhoneText(__1, translations);
        }

        private static string TranslateRivettPhoneText(string source, TranslationDictionary translations)
        {
            string translated = TranslateRivettText(source, translations);
            if (translated == source || !IsRivettPhonePartName(source))
                return translated;

            translated = translated.ToLowerInvariant();
            return translated.Length == 0 ? translated : char.ToUpperInvariant(translated[0]) + translated.Substring(1);
        }

        private static bool IsRivettPhonePartName(string source)
        {
            return !string.IsNullOrEmpty(source) && rivettPhonePartSourceNameSet.Contains(source);
        }

        private static void CaptureRivettPhonePartNames(Assembly assembly)
        {
            rivettPhonePartSourceNames.Clear();
            rivettPhonePartSourceNameSet.Clear();
            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            Type productType = assembly.GetType("RivettPartsService.Product", false);
            Type catalogType = assembly.GetType("RivettPartsService.Catalog", false);
            Type presentationType = assembly.GetType("RivettPartsService.CatalogPresentation", false);
            Type windowType = assembly.GetType("RivettPartsService.CatalogWindow", false);
            if (productType == null || catalogType == null || presentationType == null || windowType == null)
                return;

            FieldInfo productsField = catalogType.GetField("Products", staticFlags);
            FieldInfo nameField = productType.GetField("Name", instanceFlags);
            PropertyInfo labelProperty = productType.GetProperty("Label", instanceFlags);
            PropertyInfo groupProperty = productType.GetProperty("Group", instanceFlags);
            MethodInfo groupNameMethod = presentationType.GetMethod("GroupName", staticFlags, null, new Type[] { productType }, null);
            rivettPhoneSearchField = windowType.GetField("search", instanceFlags);
            rivettPhoneProductsField = productsField;
            rivettProductLabelProperty = labelProperty;
            rivettProductGroupProperty = groupProperty;
            rivettGroupNameMethod = groupNameMethod;
            Array products = productsField != null ? productsField.GetValue(null) as Array : null;
            if (products == null || nameField == null || labelProperty == null || groupProperty == null || groupNameMethod == null)
                return;

            for (int i = 0; i < products.Length; i++)
            {
                object product = products.GetValue(i);
                if (product == null)
                    continue;

                AddRivettPhonePartName(nameField.GetValue(product) as string);
                AddRivettPhonePartName(labelProperty.GetValue(product, null) as string);
                AddRivettPhonePartName(groupNameMethod.Invoke(null, new object[] { product }) as string);
            }

            RebuildRivettPhoneSearchIndex();
        }

        private static void AddRivettPhonePartName(string name)
        {
            if (string.IsNullOrEmpty(name) || !rivettPhonePartSourceNameSet.Add(name))
                return;

            rivettPhonePartSourceNames.Add(name);
        }

        private static void AfterRivettTerminalRender(ref string __result)
        {
            TranslationDictionary translations = activeTranslations;
            if (string.IsNullOrEmpty(__result) || translations == null)
                return;

            try
            {
                string[] lines = __result.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // RenderColored wraps status messages in one color tag. Translate
                    // the original message while retaining its terminal formatting.
                    const string colorStart = "<color=#FFFF00>";
                    const string colorEnd = "</color>";
                    if (line.StartsWith(colorStart, StringComparison.Ordinal)
                        && line.EndsWith(colorEnd, StringComparison.Ordinal))
                    {
                        string inner = line.Substring(colorStart.Length, line.Length - colorStart.Length - colorEnd.Length);
                        string translatedInner = TranslateRivettText(inner, translations);
                        if (translatedInner != inner)
                            lines[i] = colorStart + translatedInner + colorEnd;
                    }
                    else
                    {
                        string translatedLine = TranslateRivettText(line, translations);
                        if (IsRivettPhonePartLine(line))
                            translatedLine = FormatRivettPartCasing(translatedLine);
                        lines[i] = translatedLine;
                    }
                }
                __result = string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir a tela do computador do Rivett: " + ex.Message);
            }
        }

        private static void AfterRivettTerminalKey(object __instance)
        {
            TranslationDictionary translations = activeTranslations;
            if (__instance == null || translations == null || rivettMessageField == null)
                return;

            try
            {
                string message = rivettMessageField.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(message))
                    return;

                string translated = TranslateRivettText(message, translations);
                if (translated != message)
                    rivettMessageField.SetValue(__instance, translated);
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao traduzir mensagem do terminal Rivett: " + ex.Message);
            }
        }

        private static string TranslateRivettText(string source, TranslationDictionary translations)
        {
            if (string.IsNullOrEmpty(source) || translations == null)
                return source;

            const string coloredPlusPrefix = "<color=#00FFFF>[+]</color> ";
            const string coloredMinusPrefix = "<color=#00FFFF>[-]</color> ";
            if (source.StartsWith(coloredPlusPrefix, StringComparison.Ordinal))
                return coloredPlusPrefix + TranslateRivettCategory(source.Substring(coloredPlusPrefix.Length), translations);
            if (source.StartsWith(coloredMinusPrefix, StringComparison.Ordinal))
                return coloredMinusPrefix + TranslateRivettCategory(source.Substring(coloredMinusPrefix.Length), translations);
            if (source.StartsWith("[+] ", StringComparison.Ordinal))
                return "[+] " + TranslateRivettCategory(source.Substring(4), translations);
            if (source.StartsWith("[-] ", StringComparison.Ordinal))
                return "[-] " + TranslateRivettCategory(source.Substring(4), translations);

            string fieldPrefix = null;
            if (source.StartsWith("Variant: ", StringComparison.Ordinal))
                fieldPrefix = "Variant: ";
            else if (source.StartsWith("Colour: ", StringComparison.Ordinal))
                fieldPrefix = "Colour: ";
            else if (source.StartsWith("Upholstery: ", StringComparison.Ordinal))
                fieldPrefix = "Upholstery: ";
            if (fieldPrefix != null)
            {
                string translatedValue = TranslateRivettLabel(source.Substring(fieldPrefix.Length), translations);
                if (translatedValue != source.Substring(fieldPrefix.Length))
                {
                    string translatedPrefix;
                    if (translations.TryGetExact(fieldPrefix.TrimEnd(), "RivettPartsService/TerminalCatalog", out translatedPrefix))
                        fieldPrefix = translatedPrefix + " ";
                    return fieldPrefix + translatedValue;
                }
            }

            int quantitySeparator = source.IndexOf(" x ", StringComparison.Ordinal);
            if (quantitySeparator > 0 && IsDigits(source.Substring(0, quantitySeparator).Trim()))
            {
                string prefix = source.Substring(0, quantitySeparator + 3);
                string itemText = source.Substring(quantitySeparator + 3);
                const string incompatible = "! INCOMPATIBLE ";
                if (itemText.StartsWith(incompatible, StringComparison.Ordinal))
                    itemText = itemText.Substring(incompatible.Length);
                string translatedItem = TranslateRivettLabel(itemText, translations);
                if (translatedItem != itemText)
                    return prefix + (source.Substring(quantitySeparator + 3).StartsWith(incompatible, StringComparison.Ordinal) ? incompatible : "") + translatedItem;
            }

            if (source.StartsWith("Added: ", StringComparison.Ordinal))
            {
                int itemSeparator = source.IndexOf(" x ", StringComparison.Ordinal);
                if (itemSeparator > 6)
                {
                    string count = source.Substring(7, itemSeparator - 7);
                    string item = source.Substring(itemSeparator + 3);
                    string translatedItem = TranslateRivettLabel(item, translations);
                    if (translatedItem != item)
                        return "Adicionado: " + count + " x " + translatedItem;
                }
            }

            string translated;
            if (translations.TryTranslate(source, "RivettPartsService/TerminalCatalog", out translated)
                && !string.IsNullOrEmpty(translated))
            {
                return translated;
            }

            string translatedComposite = TranslateRivettLabel(source, translations);
            if (translatedComposite != source)
                return translatedComposite;

            // Catalog rows append dynamic prices after two spaces. Translate the
            // product/category label before the terminal truncates the row to 60 columns.
            int priceSeparator = source.LastIndexOf("  ", StringComparison.Ordinal);
            if (priceSeparator >= 0)
            {
                string price = source.Substring(priceSeparator + 2);
                if (price.EndsWith(" MK", StringComparison.OrdinalIgnoreCase)
                    && price.Length > 3)
                {
                    string amount = price.Substring(0, price.Length - 3).Trim();
                    if (IsDigits(amount))
                    {
                        string label = source.Substring(0, priceSeparator);
                        int leading = 0;
                        while (leading < label.Length && char.IsWhiteSpace(label[leading]))
                            leading++;
                        string labelText = label.Substring(leading);
                        string translatedLabel = TranslateRivettLabel(labelText, translations);
                        if (translatedLabel != labelText)
                        {
                            return label.Substring(0, leading) + translatedLabel + source.Substring(priceSeparator);
                        }
                    }
                }
            }

            // Translate product variants and basket lines component by component,
            // keeping their part codes, quantities and prices intact.
            int variantSeparator = source.IndexOf(" — ", StringComparison.Ordinal);
            if (variantSeparator >= 0)
            {
                string left = source.Substring(0, variantSeparator);
                string right = source.Substring(variantSeparator + 3);
                string translatedLeft;
                string translatedRight;
                bool leftChanged = translations.TryTranslate(left, "RivettPartsService/TerminalCatalog", out translatedLeft)
                    && !string.IsNullOrEmpty(translatedLeft);
                bool rightChanged = translations.TryTranslate(right, "RivettPartsService/TerminalCatalog", out translatedRight)
                    && !string.IsNullOrEmpty(translatedRight);
                if (leftChanged || rightChanged)
                    return (leftChanged ? translatedLeft : left) + " — " + (rightChanged ? translatedRight : right);
            }

            return source;
        }

        private static string TranslateRivettCategory(string source, TranslationDictionary translations)
        {
            int countStart = source.LastIndexOf(" (", StringComparison.Ordinal);
            if (countStart >= 0 && source.EndsWith(")", StringComparison.Ordinal))
            {
                string count = source.Substring(countStart + 2, source.Length - countStart - 3);
                if (IsDigits(count))
                {
                    string category = source.Substring(0, countStart);
                    return TranslateRivettLabel(category, translations) + source.Substring(countStart);
                }
            }

            return TranslateRivettLabel(source, translations);
        }

        private static bool IsRivettPhonePartLine(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            string candidate = source.TrimStart();
            for (int i = 0; i < rivettPhonePartSourceNames.Count; i++)
            {
                string partName = rivettPhonePartSourceNames[i];
                if (!candidate.StartsWith(partName, StringComparison.Ordinal))
                    continue;

                if (candidate.Length == partName.Length || char.IsWhiteSpace(candidate[partName.Length])
                    || candidate[partName.Length] == '\u2014')
                    return true;
            }

            return false;
        }

        private static string FormatRivettPartCasing(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string formatted = text.ToLowerInvariant();
            int firstLetter = 0;
            while (firstLetter < formatted.Length && !char.IsLetter(formatted[firstLetter]))
                firstLetter++;
            if (firstLetter >= formatted.Length)
                return formatted;

            return formatted.Substring(0, firstLetter)
                + char.ToUpperInvariant(formatted[firstLetter])
                + formatted.Substring(firstLetter + 1);
        }

        private static void AfterRivettPhoneMatches(object __instance, object __0, ref bool __result)
        {
            if (__result || __instance == null || __0 == null || activeTranslations == null
                || rivettPhoneSearchField == null || rivettProductGroupProperty == null)
                return;

            try
            {
                string search = rivettPhoneSearchField.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(search))
                    return;

                if (!string.Equals(search, rivettPhoneSearchCacheQuery, StringComparison.Ordinal))
                {
                    rivettPhoneSearchCacheQuery = search;
                    rivettPhoneSearchGroupCache.Clear();
                }

                string group = rivettProductGroupProperty.GetValue(__0, null) as string;
                if (string.IsNullOrEmpty(group))
                    return;

                bool matches;
                if (rivettPhoneSearchGroupCache.TryGetValue(group, out matches))
                {
                    if (matches)
                        __result = true;
                    return;
                }

                List<string> translatedNames;
                matches = rivettPhoneTranslatedSearchNames.TryGetValue(group, out translatedNames)
                    && RivettPhoneSearchMatches(translatedNames, search);
                rivettPhoneSearchGroupCache[group] = matches;
                if (matches)
                    __result = true;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao pesquisar peças traduzidas do Rivett: " + ex.Message);
            }
        }

        private static bool RivettPhoneSearchMatches(List<string> names, string search)
        {
            if (names == null || string.IsNullOrEmpty(search))
                return false;

            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static void RebuildRivettPhoneSearchIndex()
        {
            rivettPhoneTranslatedSearchNames.Clear();
            ClearRivettPhoneSearchCache();
            if (activeTranslations == null || rivettPhoneProductsField == null
                || rivettProductLabelProperty == null || rivettProductGroupProperty == null
                || rivettGroupNameMethod == null)
                return;

            try
            {
                Array products = rivettPhoneProductsField.GetValue(null) as Array;
                if (products == null)
                    return;

                for (int i = 0; i < products.Length; i++)
                {
                    object product = products.GetValue(i);
                    if (product == null)
                        continue;

                    string group = rivettProductGroupProperty.GetValue(product, null) as string;
                    if (string.IsNullOrEmpty(group))
                        continue;

                    string label = rivettProductLabelProperty.GetValue(product, null) as string;
                    AddRivettPhoneSearchName(group, TranslateRivettPhoneText(label, activeTranslations));
                    string groupName = rivettGroupNameMethod.Invoke(null, new object[] { product }) as string;
                    AddRivettPhoneSearchName(group, TranslateRivettPhoneText(groupName, activeTranslations));
                }
            }
            catch (Exception ex)
            {
                rivettPhoneTranslatedSearchNames.Clear();
                CoreConsole.Warning("[ModTextTranslator] Falha ao montar o índice de busca do telefone Rivett: " + ex.Message);
            }
        }

        private static void AddRivettPhoneSearchName(string group, string name)
        {
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(name))
                return;

            List<string> names;
            if (!rivettPhoneTranslatedSearchNames.TryGetValue(group, out names))
            {
                names = new List<string>();
                rivettPhoneTranslatedSearchNames.Add(group, names);
            }

            if (!names.Contains(name))
                names.Add(name);
        }

        private static void ClearRivettPhoneSearchCache()
        {
            rivettPhoneSearchGroupCache.Clear();
            rivettPhoneSearchCacheQuery = null;
        }

        private static void BeforeRivettTerminalBuild(object __instance)
        {
            rivettTerminalCatalogBuilding = false;
            if (__instance == null || rivettTerminalScreenField == null)
                return;

            try
            {
                string screen = rivettTerminalScreenField.GetValue(__instance) as string;
                rivettTerminalCatalogBuilding = string.Equals(screen, "catalog", StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                CoreConsole.Warning("[ModTextTranslator] Falha ao identificar a tela do catálogo do Rivett: " + ex.Message);
            }
        }

        private static void AfterRivettTerminalBuild()
        {
            rivettTerminalCatalogBuilding = false;
        }

        private static void AfterRivettTerminalProductLabel(ref string __result)
        {
            TranslationDictionary translations = activeTranslations;
            if (!rivettTerminalCatalogBuilding || string.IsNullOrEmpty(__result) || translations == null)
                return;

            string translated = TranslateRivettPhoneText(__result, translations);
            if (!string.Equals(translated, __result, StringComparison.Ordinal))
                __result = __result + " | " + translated;
        }

        private static string TranslateRivettLabel(string source, TranslationDictionary translations)
        {
            string translated;
            if (translations.TryTranslate(source, "RivettPartsService/TerminalCatalog", out translated)
                && !string.IsNullOrEmpty(translated))
            {
                return translated;
            }

            // Try the last suffix first so labels that already contain slashes
            // (for example, instrument-panel variants) can still match as a whole.
            int slashSeparator = source.LastIndexOf(" / ", StringComparison.Ordinal);
            if (slashSeparator >= 0)
            {
                string leftOfSlash = source.Substring(0, slashSeparator);
                string rightOfSlash = source.Substring(slashSeparator + 3);
                string translatedLeftOfSlash = TranslateRivettLabel(leftOfSlash, translations);
                string translatedRightOfSlash = TranslateRivettLabel(rightOfSlash, translations);
                if (translatedLeftOfSlash != leftOfSlash || translatedRightOfSlash != rightOfSlash)
                    return translatedLeftOfSlash + " / " + translatedRightOfSlash;
            }

            int separator = source.IndexOf(" — ", StringComparison.Ordinal);
            if (separator < 0)
                return source;

            string left = source.Substring(0, separator);
            string right = source.Substring(separator + 3);
            string translatedLeft = TranslateRivettLabel(left, translations);
            string translatedRight = TranslateRivettLabel(right, translations);
            if (translatedLeft == left && translatedRight == right)
                return source;

            return translatedLeft + " — " + translatedRight;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsDigit(value[i]))
                    return false;
            }
            return true;
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
