using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Extends display time for longer subtitle FSM states. Text replacement
    /// stays in GuiTextMonitor so the subtitle and shadow remain paired.
    /// </summary>
    public sealed class SubtitleTimingHandler : ITranslationSurface
    {
        private sealed class TimingRule
        {
            public readonly string ObjectPath;
            public readonly string StateName;
            public readonly int ActionIndex;
            public readonly float Seconds;
            public readonly int PreferredFsmIndex;
            public readonly string SourceText;
            public readonly string Key;

            public TimingRule(string objectPath, string stateName, int actionIndex, float seconds, int preferredFsmIndex, string sourceText)
            {
                ObjectPath = objectPath;
                StateName = stateName;
                ActionIndex = actionIndex;
                Seconds = seconds;
                PreferredFsmIndex = preferredFsmIndex;
                SourceText = sourceText;
                Key = objectPath + "|" + preferredFsmIndex + "|" + stateName + "|" + actionIndex;
            }
        }

        private readonly List<TimingRule> rules = new List<TimingRule>();
        private readonly HashSet<string> resolvedRules = new HashSet<string>();
        private readonly Dictionary<string, GameObject> objectCache = new Dictionary<string, GameObject>();

        public SubtitleTimingHandler()
        {
            rules.AddRange(CreateRules());
        }

        public string Name { get { return "SubtitleTimingHandler"; } }
        public SurfaceCadence Cadence { get { return SurfaceCadence.OncePerScene; } }
        public bool IsComplete { get { return true; } }

        public void Initialize(TranslationContext ctx)
        {
            Reset();
        }

        public int InitialPass()
        {
            if (Application.loadedLevelName != "GAME")
                return 0;

            return ApplyTimings();
        }

        public int MonitorTick(float deltaTime)
        {
            return 0;
        }

        public void Reset()
        {
            resolvedRules.Clear();
            objectCache.Clear();
        }

        public void ClearTranslations()
        {
            Reset();
        }

        public static void PopulateRetainedSubtitleDurations(TranslationDictionary translations, Dictionary<string, float> durations)
        {
            if (durations == null)
                return;

            durations.Clear();
            if (translations == null)
                return;

            List<TimingRule> timingRules = CreateRules();
            for (int i = 0; i < timingRules.Count; i++)
            {
                TimingRule rule = timingRules[i];
                if (string.IsNullOrEmpty(rule.SourceText))
                    continue;

                string translated;
                if (translations.TryGetExact(rule.SourceText, out translated) && !string.IsNullOrEmpty(translated))
                    durations[translated] = rule.Seconds;
            }
        }

        private static List<TimingRule> CreateRules()
        {
            List<TimingRule> createdRules = new List<TimingRule>();

            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 34", -1, 30f,
                "\"And drunk again? Please do not puke inside the frozen fish fridge, like that one occasion.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 15", -1, 30f,
                "\"They say fuel price is high. I say, they haven't seen anything yet. It will cost more than milk one day.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 21", -1, 30f,
                "\"You know the green car that drives the backroads and never stops for gas? I think the car runs with alcohol.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 22", -1, 30f,
                "\"I do not think that mosquito spray really works. Even after spraying full can those little punks keep flying around.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 16", -1, 30f,
                "\"This economic regression. It can get quite bad. I might need to discount sausage prices.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 11", -1, 30f,
                "\"I can't understand today's music at all. Must be something that has been made for punks.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 24", -1, 30f,
                "\"Those punks. Why do they keep calling me in the middle of the night? I have since unplugged my phone for the night.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 5", -1, 30f,
                "\"Did you know I used to be a wrestler? Not professional though.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 6", -1, 30f,
                "\"What and odd summer. It rains and then rain stops.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 7", -1, 30f,
                "\"Have you listened a radio lately? It is full of punks these days.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 8", -1, 30f,
                "\"I used to be a quite a fisherman. Thats one thing I used to be.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 10", -1, 30f,
                "\"I used to have a dog. Again one thing I used to have.\"");
            AddRule(createdRules, "STORE/TeimoInShop/Pivot/Speak", "State 27", -1, 30f,
                "\"So, are you participating the rally? I was once second when there were two competitors. Sometimes you come out alive.\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Marriage 2", -1, 25f,
                "\"My wife is going to move to Vaasa and get herself a finnswede man. Those are so clean and sober! 30 years of marriage down the drain.\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 2", -1, 25f,
                "\"My wife did not know she had a winning Lottery ticket. I took it and got the money myself... 5 million marks!\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 3", 3, 60f,
                "\"I have the money in a hidden suitcase. But I can't use the money because wife would get suspicious... She would leave me if she had that money!\"");
            AddRule(createdRules, "KILJUGUY/HikerPivot/JokkeHiker2", "Lotto1 4", 3, 60f,
                "\"I need to act like I always do... I am richest drunk bum there is! At least my wife stays with me. Not with some dorky finnswede tomato farmer.\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Drunk lift", -1, 25f,
                "\"I tried to call everybody. Please can you pick me up from the Pub and drive me home?\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Moving", -1, 25f,
                "\"My wife left me. I bought a apartment with nice lakeview. Could you come by and help me with moving my stuff?\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Fleetari rally", 3, 80f,
                "\"I can't believe it... You are rally winner. I must admit you have such big balls. I thought you were going to die, but you won!\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "State 12", 2, 80f,
                "\"It's Fleetari here! You moron, bring back my car or I make sure your shit bucket car does not see another day!\"");
            AddRule(createdRules, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "Fleetari shit", 6, 80f,
                "\"It is Fleetari here. Want to earn 10 bottles of booze? Dump some shit at the front of the Lindell inspection shop. That sucker deserves it.\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "Race", 2, 50f,
                "\"So you would like to race with that shit bucket of yours? Which one is faster?\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 3", 2, 25f,
                "\"Who is this pussy-ass idiot?\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 23", 2, 25f,
                "\"Is Teimo selling you that shit? He should sell some Kurjala instead. Everything is just shit.\"");
            AddRule(createdRules, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "State 14", 2, 25f,
                "\"Stop dancing with your fist female. I will smack your face and you fly like a wooden javelin!\"");
            AddRule(createdRules, "JOBS/Mummola/TalkEngine", "Speak 1", 2, 60f,
                "\"Your dad is quite sober man. I thought he would start drinking after being rejected from 1972 Olympics.\"");
            AddRule(createdRules, "REPAIRSHOP/LOD/Office/Fleetari", "Say fuck", -1, 25f,
                "\"That car of yours ruins my driveway.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", -1, 25f,
                "\"Now that I am drunk, I need to avoid those crap wells... There are no cops to lift me up. There were once, I was able to get out.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "No license", 3, 25f, 1,
                "\"Damn, I was speeding and got caught! So I lost my drivers license... I could have explained that I need it for my job, but well...\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", -1, 25f,
                "\"I've been thinking that there should be alcohol in the clouds. If it rains, you could drink it. Or fill up bottles and sell it.\"");
            AddRule(createdRules, "YARD/UNCLE/Home/UncleDrinking/Uncle", "No license 2", 1, 25f, 1,
                "\"I haven't exactly paid my income taxes, so basically in legal terms, I am not doing any work either... He he.\"");

            return createdRules;
        }

        private static void AddRule(List<TimingRule> targetRules, string objectPath, string stateName, int actionIndex, float seconds, string sourceText)
        {
            AddRule(targetRules, objectPath, stateName, actionIndex, seconds, -1, sourceText);
        }

        private static void AddRule(List<TimingRule> targetRules, string objectPath, string stateName, int actionIndex, float seconds, int preferredFsmIndex, string sourceText)
        {
            targetRules.Add(new TimingRule(objectPath, stateName, actionIndex, seconds, preferredFsmIndex, sourceText));
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

                bool changed;
                if (TryApplyRule(rule, out changed))
                {
                    resolvedRules.Add(rule.Key);
                    if (changed)
                        applied++;
                }
            }

            return applied;
        }

        private bool TryApplyRule(TimingRule rule, out bool changed)
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
                if (TryApplyToFsm(fsms[rule.PreferredFsmIndex], rule, out changed))
                    return true;
            }

            for (int i = 0; i < fsms.Length; i++)
            {
                if (i == rule.PreferredFsmIndex)
                    continue;

                if (TryApplyToFsm(fsms[i], rule, out changed))
                    return true;
            }

            return false;
        }

        private bool TryApplyToFsm(PlayMakerFSM fsm, TimingRule rule, out bool changed)
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

            return TrySetWaitTime(state.Actions[rule.ActionIndex], rule.Seconds, out changed);
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

        private static bool TrySetWaitTime(object action, float seconds, out bool changed)
        {
            changed = false;
            if (action == null)
                return false;

            FieldInfo timeField = FsmUtils.GetField(action.GetType(), "time");
            if (timeField == null)
                return false;

            object current = timeField.GetValue(action);
            FsmFloat fsmFloat = current as FsmFloat;
            if (fsmFloat != null)
            {
                if (Math.Abs(fsmFloat.Value - seconds) > 0.001f)
                {
                    fsmFloat.Value = seconds;
                    changed = true;
                }
                return true;
            }

            if (timeField.FieldType == typeof(float))
            {
                float currentValue = current is float ? (float)current : 0f;
                if (Math.Abs(currentValue - seconds) > 0.001f)
                {
                    timeField.SetValue(action, seconds);
                    changed = true;
                }
                return true;
            }

            return false;
        }
    }
}
