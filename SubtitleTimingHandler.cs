using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Extends display time for longer subtitle FSM states and raises the
    /// vanilla subtitle UI clamp so translated multiline text is not cut early.
    /// </summary>
    public sealed class SubtitleTimingHandler : ITranslationSurface
    {
        private const float SubtitleUiMaxSeconds = 120f;
        private const string SubtitleUiState = "State 3";
        private const int SubtitleUiClampActionIndex = 4;

        private static readonly string[] SubtitleUiPaths = new string[]
        {
            "GUI/Indicators/Subtitles",
            "GUI/Indicators/Subtitles/Shadow",
        };

        private sealed class TimingRule
        {
            public readonly string ObjectPath;
            public readonly string StateName;
            public readonly int ActionIndex;
            public readonly int PreferredFsmIndex;
            public readonly string SourceText;
            public readonly string Key;
            public readonly string SourceKey;

            public TimingRule(string objectPath, string stateName, int actionIndex, int preferredFsmIndex, string sourceText)
            {
                ObjectPath = objectPath;
                StateName = stateName;
                ActionIndex = actionIndex;
                PreferredFsmIndex = preferredFsmIndex;
                SourceText = sourceText;
                Key = objectPath + "|" + preferredFsmIndex + "|" + stateName + "|" + actionIndex;
                SourceKey = Key + "|" + (sourceText ?? string.Empty);
            }
        }

        private readonly List<TimingRule> rules = new List<TimingRule>();
        private readonly HashSet<string> resolvedRules = new HashSet<string>();
        private readonly HashSet<string> translatedSourceRules = new HashSet<string>();
        private readonly Dictionary<string, GameObject> objectCache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, float> subtitleDurationsByText = new Dictionary<string, float>();
        private readonly Dictionary<string, float> audioDurationsBySourceKey = new Dictionary<string, float>();
        private TranslationDictionary translations;

        public SubtitleTimingHandler()
        {
            rules.AddRange(CreateRules());
        }

        public string Name { get { return "SubtitleTimingHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.OncePerScene; } }
        public bool IsComplete { get { return true; } }

        public void Initialize(TranslationContext ctx)
        {
            translations = ctx.Translations;
            Reset();
        }

        public int InitialPass()
        {
            if (Application.loadedLevelName != "GAME")
                return 0;

            int applied = ApplySourceTranslations();
            applied += ApplyTimings();
            applied += ApplySubtitleUiTiming();
            return applied;
        }

        public int MonitorTick(float deltaTime)
        {
            return 0;
        }

        public void Reset()
        {
            resolvedRules.Clear();
            translatedSourceRules.Clear();
            subtitleDurationsByText.Clear();
            audioDurationsBySourceKey.Clear();
            objectCache.Clear();
        }

        public void ClearTranslations()
        {
            Reset();
        }

        private static List<TimingRule> CreateRules()
        {
            List<TimingRule> createdRules = new List<TimingRule>();

            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 34", -1,
                "\"And drunk again? Please do not puke inside the frozen fish fridge, like that one occasion.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 15", -1,
                "\"They say fuel price is high. I say, they haven't seen anything yet. It will cost more than milk one day.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 21", -1,
                "\"You know the green car that drives the backroads and never stops for gas? I think the car runs with alcohol.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 22", -1,
                "\"I do not think that mosquito spray really works. Even after spraying full can those little punks keep flying around.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 16", -1,
                "\"This economic regression. It can get quite bad. I might need to discount sausage prices.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 11", -1,
                "\"I can't understand today's music at all. Must be something that has been made for punks.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 24", -1,
                "\"Those punks. Why do they keep calling me in the middle of the night? I have since unplugged my phone for the night.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 5", -1,
                "\"Did you know I used to be a wrestler? Not professional though.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 6", -1,
                "\"What and odd summer. It rains and then rain stops.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 7", -1,
                "\"Have you listened a radio lately? It is full of punks these days.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 8", -1,
                "\"I used to be a quite a fisherman. Thats one thing I used to be.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 10", -1,
                "\"I used to have a dog. Again one thing I used to have.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 27", -1,
                "\"So, are you participating the rally? I was once second when there were two competitors. Sometimes you come out alive.\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Marriage 2", -1,
                "\"My wife is going to move to Vaasa and get herself a finnswede man. Those are so clean and sober! 30 years of marriage down the drain.\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 2", -1,
                "\"My wife did not know she had a winning Lottery ticket. I took it and got the money myself... 5 million marks!\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 3", 3,
                "\"I have the money in a hidden suitcase. But I can't use the money because wife would get suspicious... She would leave me if she had that money!\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 4", 3,
                "\"I need to act like I always do... I am richest drunk bum there is! At least my wife stays with me. Not with some dorky finnswede tomato farmer.\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Drunk lift", -1,
                "\"I tried to call everybody. Please can you pick me up from the Pub and drive me home?\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Moving", -1,
                "\"My wife left me. I bought a apartment with nice lakeview. Could you come by and help me with moving my stuff?\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Fleetari rally", 3,
                "\"I can't believe it... You are rally winner. I must admit you have such big balls. I thought you were going to die, but you won!\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "State 12", 2,
                "\"It's Fleetari here! You moron, bring back my car or I make sure your shit bucket car does not see another day!\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Fleetari shit", 6,
                "\"It is Fleetari here. Want to earn 10 bottles of booze? Dump some shit at the front of the Lindell inspection shop. That sucker deserves it.\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "Race", 2,
                "\"So you would like to race with that shit bucket of yours? Which one is faster?\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 3", 2,
                "\"Who is this pussy-ass idiot?\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 23", 2,
                "\"Is Teimo selling you that shit? He should sell some Kurjala instead. Everything is just shit.\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 14", 2,
                "\"Stop dancing with your fist female. I will smack your face and you fly like a wooden javelin!\"");
            AddRule(createdRules, "JOBS/Mummola/TalkEngine", "Speak 1", 2,
                "\"Your dad is quite sober man. I thought he would start drinking after being rejected from 1972 Olympics.\"");
            AddRule(createdRules, "REPAIRSHOP/LOD/Office/Fleetari", "Say fuck", -1,
                "\"That car of yours ruins my driveway.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", -1,
                "\"Now that I am drunk, I need to avoid those crap wells... There are no cops to lift me up. There were once, I was able to get out.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "No license", 3, 1,
                "\"Damn, I was speeding and got caught! So I lost my drivers license... I could have explained that I need it for my job, but well...\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", -1,
                "\"I've been thinking that there should be alcohol in the clouds. If it rains, you could drink it. Or fill up bottles and sell it.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "No license 2", 1, 1,
                "\"I haven't exactly paid my income taxes, so basically in legal terms, I am not doing any work either... He he.\"");

            return createdRules;
        }

        private static void AddRule(List<TimingRule> targetRules, string objectPath, string stateName, int actionIndex, string sourceText)
        {
            AddRule(targetRules, objectPath, stateName, actionIndex, -1, sourceText);
        }

        private static void AddRule(List<TimingRule> targetRules, string objectPath, string stateName, int actionIndex, int preferredFsmIndex, string sourceText)
        {
            targetRules.Add(new TimingRule(objectPath, stateName, actionIndex, preferredFsmIndex, sourceText));
        }

        private int ApplyTimings()
        {
            int applied = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                TimingRule rule = rules[i];
                if (resolvedRules.Contains(rule.Key))
                    continue;
                if (rule.ActionIndex < 0)
                    continue;

                float seconds;
                if (!audioDurationsBySourceKey.TryGetValue(rule.SourceKey, out seconds))
                    continue;

                bool changed;
                if (TryApplyRule(rule, seconds, out changed))
                {
                    resolvedRules.Add(rule.Key);
                    if (changed)
                        applied++;
                }
            }

            return applied;
        }

        private bool TryApplyRule(TimingRule rule, float seconds, out bool changed)
        {
            changed = false;

            GameObject go = FindTargetObject(rule.ObjectPath);
            if (go == null)
                return false;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            if (fsms == null || fsms.Length == 0)
                return false;

            if (rule.PreferredFsmIndex >= 0 && rule.PreferredFsmIndex < fsms.Length)
            {
                if (TryApplyToFsm(fsms[rule.PreferredFsmIndex], rule, seconds, out changed))
                    return true;
            }

            for (int i = 0; i < fsms.Length; i++)
            {
                if (i == rule.PreferredFsmIndex)
                    continue;

                if (TryApplyToFsm(fsms[i], rule, seconds, out changed))
                    return true;
            }

            return false;
        }

        private bool TryApplyToFsm(PlayMakerFSM fsm, TimingRule rule, float seconds, out bool changed)
        {
            changed = false;
            if (fsm == null)
                return false;

            try
            {
                if (fsm.Fsm != null && !fsm.Fsm.Initialized)
                    fsm.Fsm.InitData();
            }
            catch
            {
                return false;
            }

            FsmState state = FindState(fsm, rule.StateName);
            if (state == null || state.Actions == null || rule.ActionIndex < 0 || rule.ActionIndex >= state.Actions.Length)
                return false;

            return TrySetFloatField(state.Actions[rule.ActionIndex], "time", seconds, out changed);
        }

        private static bool TranslateObjectStringFields(object instance, string source, string translated, int depth)
        {
            if (instance == null || string.IsNullOrEmpty(source) || depth > 2)
                return false;

            bool changed = false;
            FieldInfo[] fields = FsmUtils.GetFields(instance.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.IsStatic || ShouldSkipSourceField(field))
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

                if (field.FieldType == typeof(string))
                {
                    string stringValue = value as string;
                    if (!TextMatchesSource(stringValue, source))
                        continue;

                    field.SetValue(instance, translated);
                    changed = true;
                    continue;
                }

                FsmString fsmString = value as FsmString;
                if (fsmString != null)
                {
                    if (TextMatchesSource(fsmString.Value, source))
                        changed |= FsmUtils.SetFsmStringValue(fsmString, translated);
                    continue;
                }

                FsmString[] fsmStrings = value as FsmString[];
                if (fsmStrings != null)
                {
                    for (int j = 0; j < fsmStrings.Length; j++)
                    {
                        FsmString item = fsmStrings[j];
                        if (item != null && TextMatchesSource(item.Value, source))
                            changed |= FsmUtils.SetFsmStringValue(item, translated);
                    }

                    continue;
                }

                string[] strings = value as string[];
                if (strings != null)
                {
                    for (int j = 0; j < strings.Length; j++)
                    {
                        if (!TextMatchesSource(strings[j], source))
                            continue;

                        strings[j] = translated;
                        changed = true;
                    }

                    continue;
                }

                if (ShouldScanNestedSourceValue(field, value))
                    changed |= TranslateObjectStringFields(value, source, translated, depth + 1);
            }

            return changed;
        }

        private static bool TextMatchesSource(string value, string source)
        {
            return string.Equals(value, source, StringComparison.Ordinal);
        }

        private static bool ShouldSkipSourceField(FieldInfo field)
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
                || name == "result"
                || name == "storeResult"
                || name == "output"
                || name == "outputString"
                || name == "eventTarget"
                || name == "gameObject"
                || name == "target"
                || name == "targetObject";
        }

        private static bool ShouldScanNestedSourceValue(FieldInfo field, object value)
        {
            if (value == null || ShouldSkipSourceField(field))
                return false;

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsArray)
                return false;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;

            string ns = type.Namespace ?? string.Empty;
            if (!ns.StartsWith("HutongGames.PlayMaker", StringComparison.Ordinal))
                return false;

            string typeName = type.Name;
            return typeName == "FsmVar"
                || typeName == "FsmProperty"
                || typeName == "NamedVariable";
        }

        private int ApplySubtitleUiTiming()
        {
            int applied = 0;
            for (int i = 0; i < SubtitleUiPaths.Length; i++)
            {
                bool changed;
                if (TryApplySubtitleUiTiming(SubtitleUiPaths[i], out changed) && changed)
                    applied++;
            }

            return applied;
        }

        private int ApplySourceTranslations()
        {
            if (translations == null)
                return 0;

            int applied = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                TimingRule rule = rules[i];
                if (rule == null || translatedSourceRules.Contains(rule.SourceKey) || string.IsNullOrEmpty(rule.SourceText))
                    continue;

                string translated;
                if (!translations.TryGetExact(rule.SourceText, out translated) || string.IsNullOrEmpty(translated))
                    continue;

                bool changed;
                bool resolved;
                if (TryTranslateRuleSource(rule, translated, out changed, out resolved))
                {
                    if (resolved)
                        translatedSourceRules.Add(rule.SourceKey);
                    if (changed)
                        applied++;
                }
            }

            return applied;
        }

        private bool TryTranslateRuleSource(TimingRule rule, string translated, out bool changed, out bool resolved)
        {
            changed = false;
            resolved = false;

            GameObject go = FindTargetObject(rule.ObjectPath);
            if (go == null)
                return false;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            if (fsms != null && fsms.Length > 0)
            {
                if (rule.PreferredFsmIndex >= 0 && rule.PreferredFsmIndex < fsms.Length)
                    changed |= TryTranslateFsmSource(fsms[rule.PreferredFsmIndex], rule, translated, out resolved);

                for (int i = 0; i < fsms.Length; i++)
                {
                    if (i == rule.PreferredFsmIndex)
                        continue;

                    bool fsmResolved;
                    changed |= TryTranslateFsmSource(fsms[i], rule, translated, out fsmResolved);
                    resolved |= fsmResolved;
                }
            }

            bool proxyResolved;
            changed |= TryTranslateArrayListSources(go, rule.SourceText, translated, out proxyResolved);
            resolved |= proxyResolved;
            return resolved;
        }

        private bool TryTranslateFsmSource(PlayMakerFSM fsm, TimingRule rule, string translated, out bool resolved)
        {
            resolved = false;
            if (fsm == null)
                return false;

            try
            {
                if (fsm.Fsm != null && !fsm.Fsm.Initialized)
                    fsm.Fsm.InitData();
            }
            catch
            {
                return false;
            }

            if (fsm.FsmStates == null)
                return false;

            bool changed = false;
            if (!string.IsNullOrEmpty(rule.StateName))
            {
                FsmState state = FindState(fsm, rule.StateName);
                if (state == null)
                    return false;

                resolved = true;
                float audioSeconds;
                if (TryGetStateAudioDuration(state, out audioSeconds))
                    RegisterAudioDuration(rule, translated, audioSeconds);

                return TranslateStateSourceStrings(state, rule.SourceText, translated);
            }

            for (int i = 0; i < fsm.FsmStates.Length; i++)
            {
                FsmState state = fsm.FsmStates[i];
                if (state == null)
                    continue;

                resolved = true;
                bool stateChanged = TranslateStateSourceStrings(state, rule.SourceText, translated);
                if (stateChanged)
                {
                    float audioSeconds;
                    if (TryGetStateAudioDuration(state, out audioSeconds))
                        RegisterAudioDuration(rule, translated, audioSeconds);
                }

                changed |= stateChanged;
            }

            return changed;
        }

        private static bool TranslateStateSourceStrings(FsmState state, string source, string translated)
        {
            if (state == null || state.Actions == null)
                return false;

            bool changed = false;
            for (int i = 0; i < state.Actions.Length; i++)
            {
                object action = state.Actions[i];
                if (action == null)
                    continue;

                changed |= TranslateObjectStringFields(action, source, translated, 0);
            }

            return changed;
        }

        private static bool TryTranslateArrayListSources(GameObject go, string source, string translated, out bool resolved)
        {
            resolved = false;
            if (go == null)
                return false;

            PlayMakerArrayListProxy[] proxies = go.GetComponentsInChildren<PlayMakerArrayListProxy>(true);
            if (proxies == null || proxies.Length == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < proxies.Length; i++)
            {
                PlayMakerArrayListProxy proxy = proxies[i];
                if (proxy == null)
                    continue;

                resolved = true;
                changed |= TranslateArrayList(proxy._arrayList, source, translated);
                changed |= TranslateStringList(proxy.preFillStringList, source, translated);
            }

            return changed;
        }

        private void RegisterAudioDuration(TimingRule rule, string translatedText, float seconds)
        {
            if (rule == null || string.IsNullOrEmpty(translatedText) || seconds <= 0f)
                return;

            audioDurationsBySourceKey[rule.SourceKey] = seconds;
            subtitleDurationsByText[translatedText] = seconds;
        }

        private static bool TryGetStateAudioDuration(FsmState state, out float seconds)
        {
            seconds = 0f;
            if (state == null || state.Actions == null)
                return false;

            for (int i = 0; i < state.Actions.Length; i++)
            {
                float actionSeconds;
                if (!TryGetActionAudioDuration(state.Actions[i], out actionSeconds))
                    continue;

                if (actionSeconds > seconds)
                    seconds = actionSeconds;
            }

            return seconds > 0f;
        }

        private static bool TryGetActionAudioDuration(object action, out float seconds)
        {
            seconds = 0f;
            if (action == null)
                return false;

            FieldInfo[] fields = FsmUtils.GetFields(action.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || field.IsStatic)
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

                float fieldSeconds;
                if (TryGetAudioDurationFromValue(value, out fieldSeconds) && fieldSeconds > seconds)
                    seconds = fieldSeconds;
            }

            return seconds > 0f;
        }

        private static bool TryGetAudioDurationFromValue(object value, out float seconds)
        {
            seconds = 0f;
            if (value == null)
                return false;

            AudioClip clip = value as AudioClip;
            if (clip != null)
            {
                seconds = clip.length;
                return seconds > 0f;
            }

            AudioClip[] clips = value as AudioClip[];
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AudioClip item = clips[i];
                    if (item != null && item.length > seconds)
                        seconds = item.length;
                }

                return seconds > 0f;
            }

            AudioSource audioSource = value as AudioSource;
            if (audioSource != null && audioSource.clip != null)
            {
                seconds = audioSource.clip.length;
                return seconds > 0f;
            }

            FsmObject fsmObject = value as FsmObject;
            if (fsmObject != null)
                return TryGetAudioDurationFromValue(fsmObject.Value, out seconds);

            return false;
        }

        private bool EnsureSubtitleAudioDurationAction(FsmState state, TextMesh textMesh, FsmFloat durationVariable)
        {
            if (state == null || state.Actions == null || textMesh == null || durationVariable == null)
                return false;

            for (int i = 0; i < state.Actions.Length; i++)
            {
                ApplySubtitleAudioDurationAction existing = state.Actions[i] as ApplySubtitleAudioDurationAction;
                if (existing == null)
                    continue;

                existing.textMesh = textMesh;
                existing.durationVariable = durationVariable;
                existing.durationsByText = subtitleDurationsByText;
                return false;
            }

            ApplySubtitleAudioDurationAction injection = new ApplySubtitleAudioDurationAction
            {
                textMesh = textMesh,
                durationVariable = durationVariable,
                durationsByText = subtitleDurationsByText,
            };

            List<FsmStateAction> actions = new List<FsmStateAction>(state.Actions.Length + 1);
            for (int i = 0; i < state.Actions.Length; i++)
            {
                actions.Add(state.Actions[i]);
                if (i == SubtitleUiClampActionIndex)
                    actions.Add(injection);
            }

            state.Actions = actions.ToArray();
            return true;
        }

        private static bool TranslateArrayList(ArrayList list, string source, string translated)
        {
            if (list == null || list.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                string value = list[i] as string;
                if (!TextMatchesSource(value, source))
                    continue;

                list[i] = translated;
                changed = true;
            }

            return changed;
        }

        private static bool TranslateStringList(List<string> list, string source, string translated)
        {
            if (list == null || list.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (!TextMatchesSource(list[i], source))
                    continue;

                list[i] = translated;
                changed = true;
            }

            return changed;
        }

        private bool TryApplySubtitleUiTiming(string objectPath, out bool changed)
        {
            changed = false;

            GameObject go = FindTargetObject(objectPath);
            if (go == null)
                return false;

            PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
            if (fsms == null || fsms.Length == 0)
                return false;

            for (int i = 0; i < fsms.Length; i++)
            {
                PlayMakerFSM fsm = fsms[i];
                if (fsm == null)
                    continue;

                try
                {
                    if (fsm.Fsm != null && !fsm.Fsm.Initialized)
                        fsm.Fsm.InitData();
                }
                catch
                {
                    continue;
                }

                FsmState state = FindState(fsm, SubtitleUiState);
                if (state == null || state.Actions == null || SubtitleUiClampActionIndex >= state.Actions.Length)
                    continue;

                object clampAction = state.Actions[SubtitleUiClampActionIndex];
                bool clampChanged;
                changed |= TrySetFloatField(clampAction, "maxValue", SubtitleUiMaxSeconds, out clampChanged) && clampChanged;

                FsmFloat durationVariable = TryGetFsmFloatField(clampAction, "floatVariable");
                if (durationVariable == null || subtitleDurationsByText.Count == 0)
                    return true;

                TextMesh textMesh = go.GetComponent<TextMesh>();
                changed |= EnsureSubtitleAudioDurationAction(state, textMesh, durationVariable);
                return true;
            }

            return false;
        }

        private GameObject FindTargetObject(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            GameObject cached;
            if (objectCache.TryGetValue(path, out cached) && cached != null)
                return cached;

            GameObject found = GameObject.Find(path);
            if (found == null)
            {
                string leafName = path.Substring(path.LastIndexOf('/') + 1);
                Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    if (t == null || t.name != leafName)
                        continue;

                    if (LocalizationUtils.GetGameObjectPath(t.gameObject) == path)
                    {
                        found = t.gameObject;
                        break;
                    }
                }
            }

            if (found != null)
                objectCache[path] = found;

            return found;
        }

        private static FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            if (fsm == null || string.IsNullOrEmpty(stateName))
                return null;

            FsmState[] states = fsm.FsmStates;
            if (states == null)
                return null;

            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].Name == stateName)
                    return states[i];
            }

            return null;
        }

        private static FsmFloat TryGetFsmFloatField(object action, string fieldName)
        {
            if (action == null || string.IsNullOrEmpty(fieldName))
                return null;

            FieldInfo field = FsmUtils.GetField(action.GetType(), fieldName);
            if (field == null)
                return null;

            return field.GetValue(action) as FsmFloat;
        }

        private static bool TrySetFloatField(object action, string fieldName, float value, out bool changed)
        {
            changed = false;
            if (action == null)
                return false;

            FieldInfo valueField = FsmUtils.GetField(action.GetType(), fieldName);
            if (valueField == null)
                return false;

            object current = valueField.GetValue(action);
            FsmFloat fsmFloat = current as FsmFloat;
            if (fsmFloat != null)
            {
                if (Math.Abs(fsmFloat.Value - value) > 0.001f)
                {
                    fsmFloat.Value = value;
                    changed = true;
                }
                return true;
            }

            if (valueField.FieldType == typeof(float))
            {
                float currentValue = current is float ? (float)current : 0f;
                if (Math.Abs(currentValue - value) > 0.001f)
                {
                    valueField.SetValue(action, value);
                    changed = true;
                }
                return true;
            }

            return false;
        }

        public sealed class ApplySubtitleAudioDurationAction : FsmStateAction
        {
            public TextMesh textMesh;
            public FsmFloat durationVariable;
            public Dictionary<string, float> durationsByText;

            public override void OnEnter()
            {
                Apply();
            }

            private void Apply()
            {
                if (textMesh == null || durationVariable == null || durationsByText == null || durationsByText.Count == 0)
                    return;

                string text = textMesh.text;
                if (string.IsNullOrEmpty(text))
                    return;

                float seconds;
                if (!durationsByText.TryGetValue(text, out seconds) || seconds <= 0f)
                    return;

                durationVariable.Value = seconds;
            }
        }
    }
}
