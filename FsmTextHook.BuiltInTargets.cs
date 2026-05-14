using System.Collections.Generic;

namespace MSC_Localization_Core
{
    /// <summary>
    /// Hardcoded FSM target registrations for <see cref="FsmTextHook"/>.
    /// Split out as a partial class purely for readability - the registration
    /// table dwarfs the rest of the hook and obscures its actual logic when
    /// inlined.
    /// </summary>
    public partial class FsmTextHook
    {
        private void AddBuiltInTargets(Dictionary<string, FsmTarget> byKey)
        {
            // Radio (MainMenu)
            AddTargetRule(byKey, "Radio/Folk", "", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "", -1, "RADIO NOT IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "Off", 1, "RADIO IMPORTED");
            AddTargetRule(byKey, "Radio/Folk", "", "Off", 1, "RADIO NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "", -1, "NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "", -1, "CD NOT IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "State 1", 0, "CD'S IMPORTED");
            AddTargetRule(byKey, "Radio/CD", "", "State 1", 0, "CD NOT IMPORTED");

            // TV Teletext command bar
            AddTargetRule(byKey, "Systems/Teletext", "", "", -1, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "", -1, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "Load", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "Load", 0, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "Open page", 0, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "Open page", 0, "haku");
            AddTargetRule(byKey, "Systems/Teletext", "", "State 1", 4, "stop");
            AddTargetRule(byKey, "Systems/Teletext", "", "State 1", 4, "haku");

            // Teletext weather page
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "", "State 3", 0, "PILVIST\u00c4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "", "State 4", 0, "VESISADETTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "", "State 5", 0, "UKKOSTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/CurrentWeather", "", "State 6", 0, "SELKE\u00c4\u00c4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "", "State 3", 0, "PILVIST\u00c4");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "", "State 4", 0, "VESISADETTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "", "State 5", 0, "UKKOSTA");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/188/Texts/Forecast", "", "State 6", 0, "SELKE\u00c4\u00c4");

            // Teletext rally pages
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/250/Texts/TeleTextResults", "", "State 2", 0, "Rallisprintin SM-osakilpailu Per\u00e4j\u00e4rvi, Alivieska");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/250/Texts/TeleTextResults", "", "State 3", 0, "RALLISPRINT-SM PER\u00c4J\u00c4RVI, SUNNUNTAI");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/250/Texts/TeleTextResults", "", "State 3", 0, "RALLISPRINT-SM PER\u00c4J\u00c4RVI, LAUANTAI");
            AddTargetRule(byKey, "RALLY/RallyTV/Program/RallyTVGUI/Results", "", "State 2", 0, "Rallisprintin SM-osakilpailu Per\u00e4j\u00e4rvi, Alivieska");
            AddTargetRule(byKey, "RALLY/RallyTV/Program/RallyTVGUI/Results", "", "State 3", 0, "Rallisprint-SM Per\u00e4j\u00e4rvi, Sunnantai");

            // Sheets: Rally results / penalties
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 1", 0, "Junior Cup");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 1", 0, "- Class points");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerResults", "Data", "State 3", 2, "Amateur Cup");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "Time penalty:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 6, "sec.");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 7, "Parc Ferme violation:");
            AddTargetRule(byKey, "Sheets/RallyResults/PlayerPenalties", "Data", "State 1", 8, "Jump start violation:");

            // Sheets: Traffic Ticket (DUI / speeding fine descriptions)
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 4, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Calc fine 2", 5, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus. 100km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 4, "km/h 80km/h rajoitetulla");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "100kmh", 5, "km/h at 100km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 5, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "km/h 80km/h rajoitetulla");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 4, "km/h 45km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "80kmh", 4, "km/h 80km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "km/h at 80km/h limit zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "45kmh", 5, "km/h at 45km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "DUI. Alc. breath test");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Rattijuopumus. Puhelluskokeessa");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "per mille.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "promillea.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Speeding.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Ylinopeus. 100km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Ylinopeus. 80km/h rajoitusalueella");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "Ylinopeus.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 100km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 45km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h 45km/h rajoituksella.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h limit zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h zone.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h at 80km/h vehicle limit.");
            AddTargetRule(byKey, "Sheets/TrafficTicket/TicketData", "Data", "Fetch data", 11, "km/h 80km/h rajoitetulla");

            // Sheets: Enviro Crime ticket
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 8, "litraa lietett\u00e4 kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Calc fine 5", 9, "litres.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "litraa lietett\u00e4 kaadettu maastoon.");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "Illegal dumping of waste,");
            AddTargetRule(byKey, "Sheets/EnviroCrime/TicketData", "Data", "Fetch data", 11, "litres.");

            // Sheets: Unpaid fines and arrest warrant
            AddTargetRule(byKey, "YARD/Building/Dynamics/Fines", "", "State 2", 1, "UNPAID FINES,");
            AddTargetRule(byKey, "Sheets/Arrestwarrant/Texts/Description", "", "State 1", 2, "n. 180cm,");
            AddTargetRule(byKey, "Sheets/Arrestwarrant/Texts/Description", "", "State 1", 2, "kg / likainen");

            // Long subtitle FSM text
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 34", 2, "\"And drunk again? Please do not puke inside the frozen fish fridge, like that one occasion.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 15", 2, "\"They say fuel price is high. I say, they haven't seen anything yet. It will cost more than milk one day.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 21", 2, "\"You know the green car that drives the backroads and never stops for gas? I think the car runs with alcohol.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 22", 2, "\"I do not think that mosquito spray really works. Even after spraying full can those little punks keep flying around.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 16", 2, "\"This economic regression. It can get quite bad. I might need to discount sausage prices.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 11", 2, "\"I can't understand today's music at all. Must be something that has been made for punks.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 24", 2, "\"Those punks. Why do they keep calling me in the middle of the night? I have since unplugged my phone for the night.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 5", 2, "\"Did you know I used to be a wrestler? Not professional though.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 6", 2, "\"What and odd summer. It rains and then rain stops.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 7", 2, "\"Have you listened a radio lately? It is full of punks these days.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 8", 2, "\"I used to be a quite a fisherman. Thats one thing I used to be.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 10", 2, "\"I used to have a dog. Again one thing I used to have.\"");
            AddTargetRule(byKey, "STORE/TeimoInShop/Pivot/Speak", "", "State 27", 2, "\"So, are you participating the rally? I was once second when there were two competitors. Sometimes you come out alive.\"");
            AddTargetRule(byKey, "KILJUGUY/HikerPivot/JokkeHiker2", "", "Marriage 2", 1, "\"My wife is going to move to Vaasa and get herself a finnswede man. Those are so clean and sober! 30 years of marriage down the drain.\"");
            AddTargetRule(byKey, "KILJUGUY/HikerPivot/JokkeHiker2", "", "Lotto1 2", 1, "\"My wife did not know she had a winning Lottery ticket. I took it and got the money myself... 5 million marks!\"");
            AddTargetRule(byKey, "KILJUGUY/HikerPivot/JokkeHiker2", "", "Lotto1 3", 1, "\"I have the money in a hidden suitcase. But I can't use the money because wife would get suspicious... She would leave me if she had that money!\"");
            AddTargetRule(byKey, "KILJUGUY/HikerPivot/JokkeHiker2", "", "Lotto1 4", 1, "\"I need to act like I always do... I am richest drunk bum there is! At least my wife stays with me. Not with some dorky finnswede tomato farmer.\"");
            AddTargetRule(byKey, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "", "Drunk lift", 0, "\"I tried to call everybody. Please can you pick me up from the Pub and drive me home?\"");
            AddTargetRule(byKey, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "", "Moving", 0, "\"My wife left me. I bought a apartment with nice lakeview. Could you come by and help me with moving my stuff?\"");
            AddTargetRule(byKey, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "", "Fleetari rally", 1, "\"I can't believe it... You are rally winner. I must admit you have such big balls. I thought you were going to die, but you won!\"");
            AddTargetRule(byKey, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "", "State 12", 0, "\"It's Fleetari here! You moron, bring back my car or I make sure your shit bucket car does not see another day!\"");
            AddTargetRule(byKey, "YARD/Building/LIVINGROOM/Telephone/Logic/Ring", "", "Fleetari shit", 1, "\"It is Fleetari here. Want to earn 10 bottles of booze? Dump some shit at the front of the Lindell inspection shop. That sucker deserves it.\"");
            AddTargetRule(byKey, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "", "Race", 0, "\"So you would like to race with that shit bucket of yours? Which one is faster?\"");
            AddTargetRule(byKey, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "", "State 3", 0, "\"Who is this pussy-ass idiot?\"");
            AddTargetRule(byKey, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "", "State 23", 0, "\"Is Teimo selling you that shit? He should sell some Kurjala instead. Everything is just shit.\"");
            AddTargetRule(byKey, "NPC_CARS/Amikset/KYLAJANI/Driver/Animations", "", "State 14", 0, "\"Stop dancing with your fist female. I will smack your face and you fly like a wooden javelin!\"");
            AddTargetRule(byKey, "JOBS/Mummola/TalkEngine", "", "Speak 1", 1, "\"Your dad is quite sober man. I thought he would start drinking after being rejected from 1972 Olympics.\"");
            AddTargetRule(byKey, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", "", -1, "\"Now that I am drunk, I need to avoid those crap wells... There are no cops to lift me up. There were once, I was able to get out.\"");
            AddTargetRule(byKey, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", "No license", 2, "\"Damn, I was speeding and got caught! So I lost my drivers license... I could have explained that I need it for my job, but well...\"");
            AddTargetRule(byKey, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", "", -1, "\"I've been thinking that there should be alcohol in the clouds. If it rains, you could drink it. Or fill up bottles and sell it.\"");
            AddTargetRule(byKey, "YARD/UNCLE/Home/UncleDrinking/Uncle", "", "No license 2", 0, "\"I haven't exactly paid my income taxes, so basically in legal terms, I am not doing any work either... He he.\"");

            // COMPUTER: POS boot / shell command output
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "", "State 1", 0, "Starting RS-POS...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "", "State 3", 0, "HIMEM is testing extended memory...done.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "", "State 4", 0, "Copyright (C) Royalsoft Corp 1982-1991. All Rights Reserved.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/BootSequence", "", "State 5", 0, "Megamedia Pro Family, v.2.45 Copyright (C) 1992, All Rights Reserved.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Error", 0, "Incorrect command.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Error", 0, "The system cannot find the path specified.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Error 2", 0, "Incorrect command.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Format disk", 0, "Formatting... 0%");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Format drive", 0, "Formatting... 0%");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Copy disk", 1, "Copying...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Data error", 1, "Data error reading drive A");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Write new line 2", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Reset POS 2", 0, "CONNECTION CLOSED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Reset POS 2", 0, "Quit (exit) Call (atdt #) Baud (mode baud=*)");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Error 2", 0, "Not enough memory");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Calling...", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Waiting...", 0, "CONNECTION ESTABLISHED: WAITING");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Calling....", 0, "ESTABLISHING CONNECTION...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Wrong number", 0, "COULD NOT CONNECT");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Incorrect", 0, "INCORRECT BAUD SETTING");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "New baud", 0, "BAUD SETTING CHANGED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Mem error", 1, "Not enough memory");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Copyying", 4, "Copying...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Remove mem", 3, "Formatting...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Remove mem 2", 3, "Formatting...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Dir list A", 3, "Volume in drive A is A");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Dir list C", 3, "Volume in drive C is C");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "Spezzer", 1, "EN JOY! ING UR 'PUTER? DIS IS SPE77ER SPOOKING!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/CommandPrompt", "", "State 3", 1, ":::::FUCK UR P0RN MAKE YA MOMMA BUY U NEW 'PUTER:::: HA HA:::: SPE77ER DA NAME!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "", "State 1", 0, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "", "State 3", 0, "Error reading disk.");

            // COMPUTER: TELEBBS chat/status
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "State 1", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Wait", 0, "Press RETURN to set your handle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Too short", 0, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "Download", 1, "Sending...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "Fail", 0, "Sending Failed!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "Upload", 1, "Sending...");

            // COMPUTER: Kaappis-Fishgame
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Reset", 1, "And here we go!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "k\u00e4nnikala 6", 1, "Out of beer, GAME OVER!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 1", 1, "You drink a beer.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 2", 1, "You drink another beer.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 3", 1, "Here goes another beer!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 4", 1, "Four down, two to go!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kaljalaskuri", "", "Kalja 5", 1, "You have only one beer left!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Peruskala", 0, "There's something on the hook!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Karkasi", 0, "It got away!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Ahven", 0, "That's a fine looking PERCH!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Hauki", 0, "Wow! What a PIKE!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "S\u00e4rki", 0, "A ROACH! What a waste of time!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Lahna", 0, "BREAM me up, Scotty!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kalakukko", 0, "What's this? Flying FISHCOCK?!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Sakko", 0, "God damnit!! A FINE!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "K\u00e4nnikala", 0, "SOAK stole one of your beers!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Erikoiskala", 0, "Oh, this feels like a big one!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "UKK", 0, "It's legendary URHO KALAVA KEKKONEN!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Kultakala", 0, "Oh boy! A GOLDFISH!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rahas\u00e4kki", 0, "Bless me bagpipes! A MONEYBAG!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Tonnikala", 0, "TON-A-FISH! Yabbadabbadoo!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/Kala", "", "Rosvo", 0, "ROBBER stole all your money! FUCK!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/MENU", "", "State 1", 5, "Press enter");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Fishgame/MENU", "", "Play", 2, "mk");

            // COMPUTER: Kaappis-Grilli
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Grilli/Asiakkaat", "", "Game over", 0, "Game over");

            // COMPUTER: PROCYON ProPilkki
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 7", 1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 8", 1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 11", 1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 16", 1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Tuloksesi virallisessa punnituksessa:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Yhteispaino:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/Saalis", "", "State 21", 1, "My\u00f6h\u00e4styit punnituksesta! Tuloksesi mit\u00e4t\u00f6itiin.");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 7", 0, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 8", 0, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 11", 0, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 16", 0, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Pelaajan Nimi");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Ahven");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Kiiski");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Lahna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Siika");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "S\u00e4rki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Peli/Elementit/CPUpelaajat", "", "State 21", 0, "Hauki");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/SuurinKala", "", "State 1", 5, "Suurin kala:");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset/TuloksetKisa/SuurinKala", "", "State 1", 5, "g");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Grammat", 3, "g");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Tulokset", "", "Kalan paino", 2, "g");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/PROCYON-ProPilkki/Alkuvalikko/Asetukset", "", "State 2", 0, "Pelaajan Nimi");

            // COMPUTER: Kaappis-Wildvest
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "State 2", 0, "Win! Press enter");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "YOU LOSE");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/Kaappis-Wildvest/Mekaanikka", "", "Lose", 0, "Traumatized! Game over!");

            // COMPUTER: RAMI Simppa&Jokke adventure text
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Antenna", "", "", -1, "Antenna");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Wine bottle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/ItemsHeld/Item09-WineBottle", "", "", -1, "Sacramental wine");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/RAMI-Simppa&Jokke/Seikailu-Tekstit/Text-Lersman-Oven", "", "", -1, "Oven");
        }

    }
}
