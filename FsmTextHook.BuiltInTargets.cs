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

            // Intro
            AddTargetRule(byKey, "Intro/title/name", "", "State 1", 0, "was born.");

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
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/250/Texts/TeleTextResults", "", "State 2", 0, "Rallisprint-SM Per\u00e4j\u00e4rvi, Lauantai");
            AddTargetRule(byKey, "Systems/Teletext/VKTekstiTV/PAGES/250/Texts/TeleTextResults", "", "State 3", 0, "RALLISPRINT-SM PER\u00c4J\u00c4RVI, SUNNUNTAI");
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

            // Payment
            AddTargetRule(byKey, "STORE/StoreCashRegister/Register", "", "", -1, "PRICE TOTAL:");
            AddTargetRule(byKey, "REPAIRSHOP/LOD/Store/ShopCashRegister/Register", "", "", -1, "PRICE TOTAL:");
            AddTargetRule(byKey, "INSPECTION/LOD/inspection_desk/InspectionCashRegister/Register", "", "", -1, "PRICE TOTAL:");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/PayMoneyAdvert", "", "", -1, "AD DELIVERY PAYMENT");
            AddTargetRule(byKey, "PLAYER/Pivot/AnimPivot/Camera/FPSCamera/2Spanner/Pivot/Ruler", "", "", -1, "CONDITION");
            AddTargetRule(byKey, "RALLY/Sunday/FinishArea/Stuff/PayMoney", "", "", -1, "PRICE MONEY");
            AddTargetRule(byKey, "JOBS/Farm/Farmer/Walker/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/Mummola/LOD/GrannyTalking/Granny/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseShit1/LOD/ShitNPC/ShitMan1/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/finger_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseShit2/LOD/ShitNPC/ShitMan2/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/finger_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseShit3/LOD/ShitNPC/ShitMan3/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/finger_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseShit4/LOD/ShitNPC/ShitMan4/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/finger_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseShit5/LOD/ShitNPC/ShitMan5/skeleton/pelvis/RotationPivot/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/finger_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseDrunk/Moving/JokkeHiker1/Pivot/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "KILJUGUY/HikerPivot/JokkeHiker2/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseDrunk/BeerCampOld/BeerCamp/KiljuBuyer/Char/skeleton/pelvis/spine_middle/spine_upper/collar_left/shoulder_left/arm_left/hand_left/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "JOBS/HouseWood1/LOD/NPC/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "YARD/UNCLE/UncleWalking/Uncle/Char/skeleton/pelvis/spine_middle/spine_upper/collar_right/shoulder_right/arm_right/hand_right/PayMoney", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "REPAIRSHOP/JunkYardJob/PayMoney", "", "", -1, "MONEY");
            AddTargetRule(byKey, "JOBS/StrawberryField/LOD/Functions/Money", "", "", -1, "TAKE MONEY");
            AddTargetRule(byKey, "CABIN/Cabin/Ventti/Table/GAME/Gamestuff/Stand", "", "", -1, "STAND AT");
            AddTargetRule(byKey, "CABIN/Cabin/Ventti/Table/GAME/Gamestuff/Bet", "", "", -1, "CURRENT BET");

            // STORE: product prices
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Sausages", "", "Init", 6, "SAUSAGES");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Beer", "", "Init", 6, "BEER");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/MacaronBox", "", "Init", 6, "MACARON BOX");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Pizza", "", "Init", 6, "PIZZA");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Chips", "", "Init", 6, "CHIPS");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Juice", "", "Init", 6, "JUICE CONCENTRATE");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Yeast", "", "Init", 6, "YEAST");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Sugar", "", "Init", 6, "SUGAR");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Coffee", "", "Init", 6, "COFFEE");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/FoodProducts/Milk", "", "Init", 6, "MILK");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/CarProducts/TwoStroke", "", "Init", 6, "TWO STROKE FUEL");
            AddTargetRule(byKey, "STORE/LOD/ActivateStore/CarProducts/Sparkplugs", "", "Init", 6, "SPARK PLUGS");

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
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "Use", "State 1", 0, "Insert boot disk and press RETURN...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/POS/NoOS", "Use", "State 3", 0, "Error reading disk.");

            // COMPUTER: TELEBBS chat/status
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/Software/StatusBar", "", "State 1", 0, "NOT CONNECTED");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Wait", 0, "Press RETURN to set your handle");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/Initialize", "", "Too short", 0, "Needs to be at least one characters long!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "Download", 1, "Sending...");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "Fail", 0, "Sending Failed!");
            AddTargetRule(byKey, "YARD/Building/BEDROOM1/COMPUTER/SYSTEM/TELEBBS/CONLINE/CHAT", "", "", -1, "Could Not Connect");
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
