using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace TwitchLogger.SimpleGraphQL
{
    public static class TwitchGraphQL
    {
        private readonly static HttpClient _httpClient = new HttpClient();

        private readonly static string UseLiveSHA256 = "639d5f11bfb8bf3053b424d9ef650d04c4ebb7d94711d644afb08fe9a0fad5d9";
        private readonly static string ViewerFeedback_CreatorSHA256 = "0927ff9e12f5730f3deb9d9fbe1f7bcbbb65101fa2c3a0a4543cd00b83a3b553";
        private readonly static string ChatList_BadgesSHA256 = "86f43113c04606e6476e39dcd432dee47c994d77a83e54b732e11d4935f0cd08";
        private readonly static string BrowsePage_PopularBadgesSHA256 = "267d2d2a64e0a0d6206c039ea9948d14a9b300a927d52b2efc52d2486ff0ec65";
        private readonly static string EmotePicker_EmotePicker_UserSubscriptionProductsSHA256 = "71b5f829a4576d53b714c01d3176f192cbd0b14973eb1c3d0ee23d5d1b78fd7e";

        private const int MAX_OPERATIONS_IN_REQUEST = 35;

        public static TwitchEmote[] TwitchGlobalEmotes = new TwitchEmote[]
  {
        new TwitchEmote() { Id = "354", Token = "4Head"},
        new TwitchEmote() { Id = "555555579", Token = "8-)"},
        new TwitchEmote() { Id = "555555558", Token = ":("},
        new TwitchEmote() { Id = "2", Token = ":("},
        new TwitchEmote() { Id = "1", Token = ":)"},
        new TwitchEmote() { Id = "555555559", Token = ":-("},
        new TwitchEmote() { Id = "555555557", Token = ":-)"},
        new TwitchEmote() { Id = "555555586", Token = ":-/"},
        new TwitchEmote() { Id = "555555561", Token = ":-D"},
        new TwitchEmote() { Id = "555555581", Token = ":-O"},
        new TwitchEmote() { Id = "555555592", Token = ":-P"},
        new TwitchEmote() { Id = "555555568", Token = ":-Z"},
        new TwitchEmote() { Id = "555555588", Token = ":-\\"},
        new TwitchEmote() { Id = "555555583", Token = ":-o"},
        new TwitchEmote() { Id = "555555594", Token = ":-p"},
        new TwitchEmote() { Id = "555555566", Token = ":-z"},
        new TwitchEmote() { Id = "555555564", Token = ":-|"},
        new TwitchEmote() { Id = "555555585", Token = ":/"},
        new TwitchEmote() { Id = "10", Token = ":/"},
        new TwitchEmote() { Id = "555555560", Token = ":D"},
        new TwitchEmote() { Id = "3", Token = ":D"},
        new TwitchEmote() { Id = "555555580", Token = ":O"},
        new TwitchEmote() { Id = "8", Token = ":O"},
        new TwitchEmote() { Id = "555555591", Token = ":P"},
        new TwitchEmote() { Id = "12", Token = ":P"},
        new TwitchEmote() { Id = "555555567", Token = ":Z"},
        new TwitchEmote() { Id = "555555587", Token = ":\\"},
        new TwitchEmote() { Id = "555555582", Token = ":o"},
        new TwitchEmote() { Id = "555555593", Token = ":p"},
        new TwitchEmote() { Id = "555555565", Token = ":z"},
        new TwitchEmote() { Id = "555555563", Token = ":|"},
        new TwitchEmote() { Id = "5", Token = ":|"},
        new TwitchEmote() { Id = "555555589", Token = ";)"},
        new TwitchEmote() { Id = "11", Token = ";)"},
        new TwitchEmote() { Id = "555555590", Token = ";-)"},
        new TwitchEmote() { Id = "555555596", Token = ";-P"},
        new TwitchEmote() { Id = "555555598", Token = ";-p"},
        new TwitchEmote() { Id = "555555595", Token = ";P"},
        new TwitchEmote() { Id = "13", Token = ";P"},
        new TwitchEmote() { Id = "555555597", Token = ";p"},
        new TwitchEmote() { Id = "555555584", Token = "<3"},
        new TwitchEmote() { Id = "9", Token = "<3"},
        new TwitchEmote() { Id = "555555562", Token = ">("},
        new TwitchEmote() { Id = "4", Token = ">("},
        new TwitchEmote() { Id = "3792", Token = "ANELE"},
        new TwitchEmote() { Id = "emotesv2_9eade28238d64e83b0219a9025d4692d", Token = "AnotherRecord"},
        new TwitchEmote() { Id = "51838", Token = "ArgieB8"},
        new TwitchEmote() { Id = "50", Token = "ArsonNoSexy"},
        new TwitchEmote() { Id = "307827267", Token = "AsexualPride"},
        new TwitchEmote() { Id = "74", Token = "AsianGlow"},
        new TwitchEmote() { Id = "555555577", Token = "B)"},
        new TwitchEmote() { Id = "7", Token = "B)"},
        new TwitchEmote() { Id = "555555578", Token = "B-)"},
        new TwitchEmote() { Id = "30", Token = "BCWarrior"},
        new TwitchEmote() { Id = "301428702", Token = "BOP"},
        new TwitchEmote() { Id = "22639", Token = "BabyRage"},
        new TwitchEmote() { Id = "emotesv2_f9feac06649548448b3127dd9bd7710e", Token = "BangbooBounce"},
        new TwitchEmote() { Id = "115234", Token = "BatChest"},
        new TwitchEmote() { Id = "160394", Token = "BegWan"},
        new TwitchEmote() { Id = "86", Token = "BibleThump"},
        new TwitchEmote() { Id = "1904", Token = "BigBrother"},
        new TwitchEmote() { Id = "160395", Token = "BigPhish"},
        new TwitchEmote() { Id = "307827313", Token = "BisexualPride"},
        new TwitchEmote() { Id = "302537250", Token = "BlackLivesMatter"},
        new TwitchEmote() { Id = "114738", Token = "BlargNaut"},
        new TwitchEmote() { Id = "69", Token = "BloodTrail"},
        new TwitchEmote() { Id = "115233", Token = "BrainSlug"},
        new TwitchEmote() { Id = "4057", Token = "BrokeBack"},
        new TwitchEmote() { Id = "27602", Token = "BuddhaBar"},
        new TwitchEmote() { Id = "emotesv2_b5751982f59347b78f51691f2b08d445", Token = "BunnyCharge"},
        new TwitchEmote() { Id = "emotesv2_4acac638cffb4db49f376059f7077dae", Token = "CaitlynS"},
        new TwitchEmote() { Id = "166266", Token = "CarlSmile"},
        new TwitchEmote() { Id = "90129", Token = "ChefFrank"},
        new TwitchEmote() { Id = "emotesv2_0e0a3592d8334ef5a1cfcae6f3e76acb", Token = "ChewyYAY"},
        new TwitchEmote() { Id = "58127", Token = "CoolCat"},
        new TwitchEmote() { Id = "123171", Token = "CoolStoryBob"},
        new TwitchEmote() { Id = "49106", Token = "CorgiDerp"},
        new TwitchEmote() { Id = "191313", Token = "CrreamAwk"},
        new TwitchEmote() { Id = "116625", Token = "CurseLit"},
        new TwitchEmote() { Id = "973", Token = "DAESuppy"},
        new TwitchEmote() { Id = "73", Token = "DBstyle"},
        new TwitchEmote() { Id = "33", Token = "DansGame"},
        new TwitchEmote() { Id = "emotesv2_d9567e500d78441793bee538dcabc1da", Token = "DarkKnight"},
        new TwitchEmote() { Id = "461298", Token = "DarkMode"},
        new TwitchEmote() { Id = "111700", Token = "DatSheffy"},
        new TwitchEmote() { Id = "58135", Token = "DendiFace"},
        new TwitchEmote() { Id = "emotesv2_dcd06b30a5c24f6eb871e8f5edbd44f7", Token = "DinoDance"},
        new TwitchEmote() { Id = "114835", Token = "DogFace"},
        new TwitchEmote() { Id = "102242", Token = "DoritosChip"},
        new TwitchEmote() { Id = "110734", Token = "DxCat"},
        new TwitchEmote() { Id = "959018", Token = "EarthDay"},
        new TwitchEmote() { Id = "4339", Token = "EleGiggle"},
        new TwitchEmote() { Id = "376765", Token = "EntropyWins"},
        new TwitchEmote() { Id = "302426269", Token = "ExtraLife"},
        new TwitchEmote() { Id = "1441276", Token = "FBBlock"},
        new TwitchEmote() { Id = "1441281", Token = "FBCatch"},
        new TwitchEmote() { Id = "1441285", Token = "FBChallenge"},
        new TwitchEmote() { Id = "1441271", Token = "FBPass"},
        new TwitchEmote() { Id = "1441289", Token = "FBPenalty"},
        new TwitchEmote() { Id = "1441261", Token = "FBRun"},
        new TwitchEmote() { Id = "1441273", Token = "FBSpiral"},
        new TwitchEmote() { Id = "626795", Token = "FBtouchdown"},
        new TwitchEmote() { Id = "244", Token = "FUNgineer"},
        new TwitchEmote() { Id = "360", Token = "FailFish"},
        new TwitchEmote() { Id = "emotesv2_2734f1a85677416a9d8f846a2d1b4721", Token = "FallCry"},
        new TwitchEmote() { Id = "emotesv2_7f9b025d534544afaf679e13fbd47b88", Token = "FallHalp"},
        new TwitchEmote() { Id = "emotesv2_dee4ecfb7f0940bead9765da02c57ca9", Token = "FallWinning"},
        new TwitchEmote() { Id = "emotesv2_89f3f0761c7b4f708061e9e4be3b7d17", Token = "FamilyMan"},
        new TwitchEmote() { Id = "emotesv2_0cb91e8a01c741fe9d4a0607f70395db", Token = "FlawlessVictory"},
        new TwitchEmote() { Id = "302628600", Token = "FootBall"},
        new TwitchEmote() { Id = "302628617", Token = "FootGoal"},
        new TwitchEmote() { Id = "302628613", Token = "FootYellow"},
        new TwitchEmote() { Id = "emotesv2_db3385fb0ea54913bf58fa5554edfdf2", Token = "ForSigmar"},
        new TwitchEmote() { Id = "65", Token = "FrankerZ"},
        new TwitchEmote() { Id = "117701", Token = "FreakinStinkin"},
        new TwitchEmote() { Id = "98562", Token = "FutureMan"},
        new TwitchEmote() { Id = "307827321", Token = "GayPride"},
        new TwitchEmote() { Id = "307827326", Token = "GenderFluidPride"},
        new TwitchEmote() { Id = "emotesv2_291135bb36d24d33bf53860128b5095c", Token = "Getcamped"},
        new TwitchEmote() { Id = "32", Token = "GingerPower"},
        new TwitchEmote() { Id = "112291", Token = "GivePLZ"},
        new TwitchEmote() { Id = "304486301", Token = "GlitchCat"},
        new TwitchEmote() { Id = "304489128", Token = "GlitchLit"},
        new TwitchEmote() { Id = "304489309", Token = "GlitchNRG"},
        new TwitchEmote() { Id = "emotesv2_e41e4d6808224f25ae1fb625aa26de63", Token = "GoatEmotey"},
        new TwitchEmote() { Id = "emotesv2_c1f4899e65cf4f53b2fd98e15733973a", Token = "GoldPLZ"},
        new TwitchEmote() { Id = "3632", Token = "GrammarKing"},
        new TwitchEmote() { Id = "1584743", Token = "GunRun"},
        new TwitchEmote() { Id = "444572", Token = "HSCheers"},
        new TwitchEmote() { Id = "446979", Token = "HSWP"},
        new TwitchEmote() { Id = "emotesv2_8b0ac3eee4274a75868e3d0686d7b6f7", Token = "HarleyWink"},
        new TwitchEmote() { Id = "20225", Token = "HassaanChop"},
        new TwitchEmote() { Id = "30259", Token = "HeyGuys"},
        new TwitchEmote() { Id = "1713813", Token = "HolidayCookie"},
        new TwitchEmote() { Id = "1713816", Token = "HolidayLog"},
        new TwitchEmote() { Id = "1713819", Token = "HolidayPresent"},
        new TwitchEmote() { Id = "1713822", Token = "HolidaySanta"},
        new TwitchEmote() { Id = "1713825", Token = "HolidayTree"},
        new TwitchEmote() { Id = "357", Token = "HotPokket"},
        new TwitchEmote() { Id = "emotesv2_535e40afa0b34a9481997627b1b47d96", Token = "HungryPaimon"},
        new TwitchEmote() { Id = "emotesv2_b0c6ccb3b12b4f99a9cc83af365a09f1", Token = "ImTyping"},
        new TwitchEmote() { Id = "307827332", Token = "IntersexPride"},
        new TwitchEmote() { Id = "160396", Token = "InuyoFace"},
        new TwitchEmote() { Id = "133468", Token = "ItsBoshyTime"},
        new TwitchEmote() { Id = "15", Token = "JKanStyle"},
        new TwitchEmote() { Id = "114836", Token = "Jebaited"},
        new TwitchEmote() { Id = "emotesv2_031bf329c21040a897d55ef471da3dd3", Token = "Jebasted"},
        new TwitchEmote() { Id = "26", Token = "JonCarnage"},
        new TwitchEmote() { Id = "133537", Token = "KAPOW"},
        new TwitchEmote() { Id = "emotesv2_7c5d25facc384c47963d25a5057a0b40", Token = "KEKHeim"},
        new TwitchEmote() { Id = "25", Token = "Kappa"},
        new TwitchEmote() { Id = "80393", Token = "Kappa"},
        new TwitchEmote() { Id = "74510", Token = "KappaClaus"},
        new TwitchEmote() { Id = "55338", Token = "KappaPride"},
        new TwitchEmote() { Id = "70433", Token = "KappaRoss"},
        new TwitchEmote() { Id = "81997", Token = "KappaWealth"},
        new TwitchEmote() { Id = "160397", Token = "Kappu"},
        new TwitchEmote() { Id = "1902", Token = "Keepo"},
        new TwitchEmote() { Id = "40", Token = "KevinTurtle"},
        new TwitchEmote() { Id = "emotesv2_533b8c4a9f6e4bfbb528ad39974e3481", Token = "KingWorldCup"},
        new TwitchEmote() { Id = "1901", Token = "Kippa"},
        new TwitchEmote() { Id = "81273", Token = "KomodoHype"},
        new TwitchEmote() { Id = "160400", Token = "KonCha"},
        new TwitchEmote() { Id = "41", Token = "Kreygasm"},
        new TwitchEmote() { Id = "425618", Token = "LUL"},
        new TwitchEmote() { Id = "emotesv2_ecb0bfd49b3c4325864b948d46c8152b", Token = "LaundryBasket"},
        new TwitchEmote() { Id = "emotesv2_665235901db747b1bd395a5f1c0ab8a9", Token = "Lechonk"},
        new TwitchEmote() { Id = "307827340", Token = "LesbianPride"},
        new TwitchEmote() { Id = "emotesv2_adfadf0ae06a4258adc865761746b227", Token = "LionOfYara"},
        new TwitchEmote() { Id = "142140", Token = "MVGame"},
        new TwitchEmote() { Id = "30134", Token = "Mau5"},
        new TwitchEmote() { Id = "1290325", Token = "MaxLOL"},
        new TwitchEmote() { Id = "emotesv2_0be25a1663bd472495b91e0302cec166", Token = "MechaRobot"},
        new TwitchEmote() { Id = "1003187", Token = "MercyWing1"},
        new TwitchEmote() { Id = "1003189", Token = "MercyWing2"},
        new TwitchEmote() { Id = "81636", Token = "MikeHogu"},
        new TwitchEmote() { Id = "68856", Token = "MingLee"},
        new TwitchEmote() { Id = "emotesv2_a2dfbbbbf66f4a75b0f53db841523e6c", Token = "ModLove"},
        new TwitchEmote() { Id = "156787", Token = "MorphinTime"},
        new TwitchEmote() { Id = "28", Token = "MrDestructoid"},
        new TwitchEmote() { Id = "emotesv2_c0c9c932c82244ff920ad2134be90afb", Token = "MyAvatar"},
        new TwitchEmote() { Id = "emotesv2_53f6a6af8b0e453d874bbefee49b3e73", Token = "NewRecord"},
        new TwitchEmote() { Id = "emotesv2_1f524be9838146e3bc9e529c17f797d3", Token = "NiceTry"},
        new TwitchEmote() { Id = "138325", Token = "NinjaGrumpy"},
        new TwitchEmote() { Id = "90075", Token = "NomNom"},
        new TwitchEmote() { Id = "307827356", Token = "NonbinaryPride"},
        new TwitchEmote() { Id = "34875", Token = "NotATK"},
        new TwitchEmote() { Id = "58765", Token = "NotLikeThis"},
        new TwitchEmote() { Id = "555555572", Token = "O.O"},
        new TwitchEmote() { Id = "555555570", Token = "O.o"},
        new TwitchEmote() { Id = "81248", Token = "OSFrog"},
        new TwitchEmote() { Id = "555555571", Token = "O_O"},
        new TwitchEmote() { Id = "555555569", Token = "O_o"},
        new TwitchEmote() { Id = "6", Token = "O_o"},
        new TwitchEmote() { Id = "81103", Token = "OhMyDog"},
        new TwitchEmote() { Id = "66", Token = "OneHand"},
        new TwitchEmote() { Id = "100590", Token = "OpieOP"},
        new TwitchEmote() { Id = "16", Token = "OptimizePrime"},
        new TwitchEmote() { Id = "36", Token = "PJSalt"},
        new TwitchEmote() { Id = "102556", Token = "PJSugar"},
        new TwitchEmote() { Id = "92", Token = "PMSTwin"},
        new TwitchEmote() { Id = "28328", Token = "PRChase"},
        new TwitchEmote() { Id = "3668", Token = "PanicVis"},
        new TwitchEmote() { Id = "307827370", Token = "PansexualPride"},
        new TwitchEmote() { Id = "965738", Token = "PartyHat"},
        new TwitchEmote() { Id = "135393", Token = "PartyTime"},
        new TwitchEmote() { Id = "3412", Token = "PeoplesChamp"},
        new TwitchEmote() { Id = "27509", Token = "PermaSmug"},
        new TwitchEmote() { Id = "111300", Token = "PicoMause"},
        new TwitchEmote() { Id = "emotesv2_a25ad7124e584c949e2f63917e3d747a", Token = "PikaRamen"},
        new TwitchEmote() { Id = "1003190", Token = "PinkMercy"},
        new TwitchEmote() { Id = "4240", Token = "PipeHype"},
        new TwitchEmote() { Id = "1547903", Token = "PixelBob"},
        new TwitchEmote() { Id = "emotesv2_f202746ed88f4e7c872b50b1f7fd78cc", Token = "PizzaTime"},
        new TwitchEmote() { Id = "emotesv2_30050f4353aa4322b25b6b044703e5d1", Token = "PogBones"},
        new TwitchEmote() { Id = "305954156", Token = "PogChamp"},
        new TwitchEmote() { Id = "117484", Token = "Poooound"},
        new TwitchEmote() { Id = "724216", Token = "PopCorn"},
        new TwitchEmote() { Id = "emotesv2_cff32f43571543828847738e27acf4ef", Token = "PopGhost"},
        new TwitchEmote() { Id = "emotesv2_5d523adb8bbb4786821cd7091e47da21", Token = "PopNemo"},
        new TwitchEmote() { Id = "emotesv2_4c39207000564711868f3196cc0a8748", Token = "PoroSad"},
        new TwitchEmote() { Id = "emotesv2_e02650251d204198923de93a0c62f5f5", Token = "PotFriend"},
        new TwitchEmote() { Id = "425688", Token = "PowerUpL"},
        new TwitchEmote() { Id = "425671", Token = "PowerUpR"},
        new TwitchEmote() { Id = "38586", Token = "PraiseIt"},
        new TwitchEmote() { Id = "115075", Token = "PrimeMe"},
        new TwitchEmote() { Id = "160401", Token = "PunOko"},
        new TwitchEmote() { Id = "47", Token = "PunchTrees"},
        new TwitchEmote() { Id = "555555599", Token = "R)"},
        new TwitchEmote() { Id = "14", Token = "R)"},
        new TwitchEmote() { Id = "555555600", Token = "R-)"},
        new TwitchEmote() { Id = "114870", Token = "RaccAttack"},
        new TwitchEmote() { Id = "1900", Token = "RalpherZ"},
        new TwitchEmote() { Id = "22", Token = "RedCoat"},
        new TwitchEmote() { Id = "245", Token = "ResidentSleeper"},
        new TwitchEmote() { Id = "4338", Token = "RitzMitz"},
        new TwitchEmote() { Id = "134256", Token = "RlyTho"},
        new TwitchEmote() { Id = "107030", Token = "RuleFive"},
        new TwitchEmote() { Id = "emotesv2_0ebc590ba68447269831af61d8bc9e0d", Token = "RyuChamp"},
        new TwitchEmote() { Id = "52", Token = "SMOrc"},
        new TwitchEmote() { Id = "46", Token = "SSSsss"},
        new TwitchEmote() { Id = "emotesv2_fcbeed664f7c47d6ba3b57691275ef51", Token = "SUBprise"},
        new TwitchEmote() { Id = "160402", Token = "SabaPing"},
        new TwitchEmote() { Id = "64138", Token = "SeemsGood"},
        new TwitchEmote() { Id = "81249", Token = "SeriousSloth"},
        new TwitchEmote() { Id = "52492", Token = "ShadyLulu"},
        new TwitchEmote() { Id = "87", Token = "ShazBotstix"},
        new TwitchEmote() { Id = "emotesv2_819621bcb8f44566a1bd8ea63d06c58f", Token = "Shush"},
        new TwitchEmote() { Id = "300116349", Token = "SingsMic"},
        new TwitchEmote() { Id = "300116350", Token = "SingsNote"},
        new TwitchEmote() { Id = "89945", Token = "SmoocherZ"},
        new TwitchEmote() { Id = "1906", Token = "SoBayed"},
        new TwitchEmote() { Id = "2113050", Token = "SoonerLater"},
        new TwitchEmote() { Id = "191762", Token = "Squid1"},
        new TwitchEmote() { Id = "191763", Token = "Squid2"},
        new TwitchEmote() { Id = "191764", Token = "Squid3"},
        new TwitchEmote() { Id = "191767", Token = "Squid4"},
        new TwitchEmote() { Id = "90076", Token = "StinkyCheese"},
        new TwitchEmote() { Id = "304486324", Token = "StinkyGlitch"},
        new TwitchEmote() { Id = "17", Token = "StoneLightning"},
        new TwitchEmote() { Id = "114876", Token = "StrawBeary"},
        new TwitchEmote() { Id = "118772", Token = "SuperVinlin"},
        new TwitchEmote() { Id = "34", Token = "SwiftRage"},
        new TwitchEmote() { Id = "143490", Token = "TBAngel"},
        new TwitchEmote() { Id = "1899", Token = "TF2John"},
        new TwitchEmote() { Id = "508650", Token = "TPFufun"},
        new TwitchEmote() { Id = "323914", Token = "TPcrunchyroll"},
        new TwitchEmote() { Id = "38436", Token = "TTours"},
        new TwitchEmote() { Id = "112292", Token = "TakeNRG"},
        new TwitchEmote() { Id = "160403", Token = "TearGlove"},
        new TwitchEmote() { Id = "160404", Token = "TehePelo"},
        new TwitchEmote() { Id = "160392", Token = "ThankEgg"},
        new TwitchEmote() { Id = "145315", Token = "TheIlluminati"},
        new TwitchEmote() { Id = "emotesv2_cb05fb473a3a44ed9441db2b62e84cd9", Token = "TheOne"},
        new TwitchEmote() { Id = "18", Token = "TheRinger"},
        new TwitchEmote() { Id = "111351", Token = "TheTarFu"},
        new TwitchEmote() { Id = "7427", Token = "TheThing"},
        new TwitchEmote() { Id = "1898", Token = "ThunBeast"},
        new TwitchEmote() { Id = "111119", Token = "TinyFace"},
        new TwitchEmote() { Id = "864205", Token = "TombRaid"},
        new TwitchEmote() { Id = "114846", Token = "TooSpicy"},
        new TwitchEmote() { Id = "307827377", Token = "TransgenderPride"},
        new TwitchEmote() { Id = "120232", Token = "TriHard"},
        new TwitchEmote() { Id = "emotesv2_13b6dd7f3a3146ef8dc10f66d8b42a96", Token = "TwitchConHYPE"},
        new TwitchEmote() { Id = "166263", Token = "TwitchLit"},
        new TwitchEmote() { Id = "1220086", Token = "TwitchRPG"},
        new TwitchEmote() { Id = "300116344", Token = "TwitchSings"},
        new TwitchEmote() { Id = "196892", Token = "TwitchUnity"},
        new TwitchEmote() { Id = "479745", Token = "TwitchVotes"},
        new TwitchEmote() { Id = "134255", Token = "UWot"},
        new TwitchEmote() { Id = "111792", Token = "UnSane"},
        new TwitchEmote() { Id = "114856", Token = "UncleNox"},
        new TwitchEmote() { Id = "301696583", Token = "VirtualHug"},
        new TwitchEmote() { Id = "81274", Token = "VoHiYo"},
        new TwitchEmote() { Id = "106294", Token = "VoteNay"},
        new TwitchEmote() { Id = "106293", Token = "VoteYea"},
        new TwitchEmote() { Id = "114847", Token = "WTRuck"},
        new TwitchEmote() { Id = "1896", Token = "WholeWheat"},
        new TwitchEmote() { Id = "emotesv2_1fda4a1b40094c93af334f8b60868a7c", Token = "WhySoSerious"},
        new TwitchEmote() { Id = "28087", Token = "WutFace"},
        new TwitchEmote() { Id = "134254", Token = "YouDontSay"},
        new TwitchEmote() { Id = "4337", Token = "YouWHY"},
        new TwitchEmote() { Id = "62835", Token = "bleedPurple"},
        new TwitchEmote() { Id = "84608", Token = "cmonBruh"},
        new TwitchEmote() { Id = "112288", Token = "copyThis"},
        new TwitchEmote() { Id = "62834", Token = "duDudu"},
        new TwitchEmote() { Id = "112290", Token = "imGlitch"},
        new TwitchEmote() { Id = "35063", Token = "mcaT"},
        new TwitchEmote() { Id = "555555574", Token = "o.O"},
        new TwitchEmote() { Id = "555555576", Token = "o.o"},
        new TwitchEmote() { Id = "555555573", Token = "o_O"},
        new TwitchEmote() { Id = "555555575", Token = "o_o"},
        new TwitchEmote() { Id = "22998", Token = "panicBasket"},
        new TwitchEmote() { Id = "112289", Token = "pastaThat"},
        new TwitchEmote() { Id = "62833", Token = "riPepperonis"},
        new TwitchEmote() { Id = "62836", Token = "twitchRaid"},
  };

        static TwitchGraphQL()
        {
            _httpClient.DefaultRequestHeaders.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");
        }

        public static void AddDefaultRequestHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        }

        public static async Task<string> GetUserID(string login)
        {
            JArray operations = new JArray
            {
                GetUseLiveObject(login)
            };

            var result = await MakeGraphQLRequest(operations);
            var user = result[0]["data"]["user"];
            if (user.Type == JTokenType.Null)
                return string.Empty;

            return user["id"].ToString();
        }

        public static async Task<List<string>> GetUsersIds(IEnumerable<string> logins)
        {
            var useLiveObjects = logins.Select(x => GetUseLiveObject(x)).ToList();
            List<string> result = new List<string>();

            for (var i = 0; i < useLiveObjects.Count; i += MAX_OPERATIONS_IN_REQUEST)
            {
                JArray operations = new JArray
                {
                    useLiveObjects.Skip(i).Take(MAX_OPERATIONS_IN_REQUEST)
                };

                var resultRequest = await MakeGraphQLRequest(operations);
                result.AddRange(resultRequest.Select(x =>
                {
                    var user = x["data"]["user"];
                    if (user.Type == JTokenType.Null)
                        return string.Empty;

                    return user["id"].ToString();
                }));
            }

            return result;
        }

        public static async Task<string> GetClientIntegrity()
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = new Uri("https://gql.twitch.tv/integrity");
            requestMessage.Method = HttpMethod.Post;

            var result = await _httpClient.SendAsync(requestMessage);

            result.EnsureSuccessStatusCode();

            var stringData = await result.Content.ReadAsStringAsync();
            var jsonData = JObject.Parse(stringData);

            return jsonData["token"].ToString();
        }

        public static async Task<List<TwitchLiveChannel>> GetLiveChannels(string cursor)
        {
            JArray operations = new JArray
            {
                GetBrowsePage_PopularObject(cursor)
            };

            var listResult = new List<TwitchLiveChannel>();

            var result = await MakeGraphQLRequest(operations);
            Console.WriteLine(result.ToString());
            var edgesList = result[0]["data"]["streams"]["edges"] as JArray;
            foreach (var edge in edgesList)
            {
                listResult.Add(new TwitchLiveChannel()
                {
                    Id = (string)edge["node"]["broadcaster"]["id"],
                    DisplayName = (string)edge["node"]["broadcaster"]["displayName"],
                    Login = (string)edge["node"]["broadcaster"]["login"],
                    Cursor = (string)edge["cursor"]
                });
            }

            return listResult;
        }

        public static async Task<List<TwitchBadge>> GetChannelBadgesInfo(string channelLogin)
        {
            JArray operations = new JArray
            {
                GetChatList_BadgesObject(channelLogin)
            };

            var result = await MakeGraphQLRequest(operations);
            var data = result[0]["data"];
            var globalBadges = data["badges"].ToObject<List<TwitchBadge>>();
            var user = data["user"];
            if (user != null && user.Type != JTokenType.Null)
            {
                var broadcastBadges = data["user"]["broadcastBadges"].ToObject<List<TwitchBadge>>();
                globalBadges.AddRange(broadcastBadges);
            }
            return globalBadges;
        }

        public static async Task<List<TwitchEmote>> GetChannelEmotes(string channelId)
        {
            JArray operations = new JArray
            {
                GetEmotePicker_EmotePicker_UserSubscriptionProductsObject(channelId)
            };

            var emotesList = new List<TwitchEmote>();

            var result = await MakeGraphQLRequest(operations);
            var data = result[0]["data"];
            var user = data["user"];
            if (user == null || user.Type == JTokenType.Null)
                return emotesList;

            var subscriptionProducts = user["subscriptionProducts"] as JArray;
            foreach (var subscriptionProduct in subscriptionProducts)
            {
                var emotes = subscriptionProduct["emotes"].ToObject<List<TwitchEmote>>();
                emotesList.AddRange(emotes);
            }

            return emotesList;
        }

        public static async Task<TwitchUser> GetUserInfoById(string userId)
        {
            JArray operations = new JArray
            {
                GetViewerFeedback_CreatorObject(userId)
            };

            var result = await MakeGraphQLRequest(operations);
            return result[0]["data"]["creator"].ToObject<TwitchUser>();
        }

        public static async Task<List<TwitchUser>> GetUsersInfoById(IEnumerable<string> userIds)
        {
            var getViewerFeedback_CreatorObjects = userIds.Select(x => GetViewerFeedback_CreatorObject(x)).ToList();
            List<TwitchUser> result = new List<TwitchUser>();

            for (var i = 0; i < getViewerFeedback_CreatorObjects.Count; i += MAX_OPERATIONS_IN_REQUEST)
            {
                JArray operations = new JArray
                {
                    getViewerFeedback_CreatorObjects.Skip(i).Take(MAX_OPERATIONS_IN_REQUEST)
                };

                var resultRequest = await MakeGraphQLRequest(operations);
                result.AddRange(resultRequest.Select(x =>
                {
                    var user = x["data"]["creator"];
                    if (user.Type == JTokenType.Null)
                        return new TwitchUser();

                    return user.ToObject<TwitchUser>();
                }));
            }

            return result;
        }

        private static async Task<JArray> MakeGraphQLRequest(JArray operations)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            requestMessage.RequestUri = new Uri("https://gql.twitch.tv/gql");
            requestMessage.Method = HttpMethod.Post;
            requestMessage.Content = new StringContent(operations.ToString(), Encoding.UTF8, "application/json");

            var result = await _httpClient.SendAsync(requestMessage);

            result.EnsureSuccessStatusCode();

            var dataString = await result.Content.ReadAsStringAsync();

            return JArray.Parse(dataString);
        }

        private static JObject GetBrowsePage_PopularObject(string cursor)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();
            JObject options = new JObject();
            JObject recommendationsContext = new JObject();

            if (!string.IsNullOrEmpty(cursor))
                variables["cursor"] = cursor;

            variables["limit"] = 30;
            variables["platformType"] = "all";
            variables["options"] = options;

            options["recommendationsContext"] = recommendationsContext;
            recommendationsContext["platform"] = "web";
            options["sort"] = "VIEWER_COUNT";
            options["tags"] = new JArray();

            variables["sortTypeIsRecency"] = false;
            variables["freeformTagsEnabled"] = false;

            obj["operationName"] = "BrowsePage_Popular";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(BrowsePage_PopularBadgesSHA256);

            return obj;
        }

        private static JObject GetUseLiveObject(string login)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelLogin"] = login;

            obj["operationName"] = "UseLive";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(UseLiveSHA256);

            return obj;
        }

        private static JObject GetChatList_BadgesObject(string login)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelLogin"] = login;

            obj["operationName"] = "ChatList_Badges";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(ChatList_BadgesSHA256);

            return obj;
        }

        private static JObject GetEmotePicker_EmotePicker_UserSubscriptionProductsObject(string ownerId)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelOwnerID"] = ownerId;

            obj["operationName"] = "EmotePicker_EmotePicker_UserSubscriptionProducts";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(EmotePicker_EmotePicker_UserSubscriptionProductsSHA256);

            return obj;
        }

        private static JObject GetViewerFeedback_CreatorObject(string userId)
        {
            JObject obj = new JObject();
            JObject variables = new JObject();

            variables["channelID"] = userId;

            obj["operationName"] = "ViewerFeedback_Creator";
            obj["variables"] = variables;
            obj["extensions"] = GetExtensionsForObject(ViewerFeedback_CreatorSHA256);

            return obj;
        }

        private static JObject GetExtensionsForObject(string sha256)
        {
            JObject extensions = new JObject();
            JObject persistedQuery = new JObject();
            persistedQuery["version"] = 1;
            persistedQuery["sha256Hash"] = sha256;
            extensions["persistedQuery"] = persistedQuery;

            return extensions;
        }
    }
}