using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

//using Discord;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage
{
    public class CharactersUniquePhrase : IServiceSingleton
    {
        public PhraseClass AwdkaAfk;
        public PhraseClass AwdkaTeachToPlay;
        public PhraseClass AwdkaTrolling;
        public PhraseClass AwdkaTrollingReady;
        public PhraseClass AwdkaTrying;
        public PhraseClass DarksciDysmoral;
        public PhraseClass DarksciFuckThisGame;
        public PhraseClass DarksciLucky;
        public PhraseClass DarksciNotLucky;

        public PhraseClass DeepListDoubtfulTacticPhrase;

        //initialize variables 
        public PhraseClass DeepListMadnessPhrase;
        public PhraseClass DeepListSuperMindPhrase;

        public PhraseClass GlebChallengerPhrase;
        public PhraseClass GlebComeBackPhrase;
        public PhraseClass GlebSleepyPhrase;
        public PhraseClass GlebTeaPhrase;

        public PhraseClass HardKittyDoebatsyaPhrase;
        public PhraseClass HardKittyLonelyPhrase;
        public PhraseClass HardKittyMutedPhrase;
        public PhraseClass LeCrispAssassinsPhrase;
        public PhraseClass LeCrispImpactPhrase;
        public PhraseClass LeCrispJewPhrase;
        public PhraseClass MitsukiCheekyBriki;
        public PhraseClass MitsukiGarbageSmell;
        public PhraseClass MitsukiSchoolboy;
        public PhraseClass MitsukiTooMuchFucking;
        public PhraseClass MylorikBoolePhrase;
        public PhraseClass MylorikRevengeLostPhrase;
        public PhraseClass MylorikRevengeVictoryPhrase;
        public PhraseClass MylorikSpanishPhrase;
        public PhraseClass SirinoksDragonPhrase;
        public PhraseClass SirinoksFriendsPhrase;
        public PhraseClass TigrSnipe;
        public PhraseClass TigrThreeZero;
        public PhraseClass TigrTop;

        public PhraseClass TigrTwoBetter;
        public PhraseClass TolyaCountPhrase;

        public PhraseClass TolyaCountReadyPhrase;
        public PhraseClass TolyaJewPhrase;
        public PhraseClass TolyaRammus2Phrase;
        public PhraseClass TolyaRammus3Phrase;
        public PhraseClass TolyaRammus4Phrase;
        public PhraseClass TolyaRammus5Phrase;

        public PhraseClass TolyaRammusPhrase;
        public PhraseClass VampyrHematophagy;
        public PhraseClass VampyrNoAttack;
        public PhraseClass VampyrSuckspenStake;

        public PhraseClass VampyrVampyr;
        //end

        public CharactersUniquePhrase()
        {
            //add values
            VampyrHematophagy = new PhraseClass("Гематофагия");
            VampyrSuckspenStake = new PhraseClass("СОсиновый кол");
            VampyrNoAttack = new PhraseClass("СОсиновый кол");
            VampyrVampyr = new PhraseClass("Vampyr");


            TigrTwoBetter = new PhraseClass("Лучше с двумя, чем с адекватными");
            TigrThreeZero = new PhraseClass("3-0 обоссан");
            TigrTop = new PhraseClass("Тигр топ, а ты холоп");
            TigrSnipe = new PhraseClass("Стримснайпят и банят и банят и банят");

            DeepListMadnessPhrase = new PhraseClass("Безумие");
            DeepListDoubtfulTacticPhrase = new PhraseClass("Сомнительная тактика");
            DeepListSuperMindPhrase = new PhraseClass("Сверхразум");

            MylorikRevengeLostPhrase = new PhraseClass("Месть");
            MylorikRevengeVictoryPhrase = new PhraseClass("Месть");
            MylorikBoolePhrase = new PhraseClass("Буль");
            MylorikSpanishPhrase = new PhraseClass("Испанец");

            GlebChallengerPhrase = new PhraseClass("Претендент русского сервера");

            GlebSleepyPhrase = new PhraseClass("Спящее хуйло");

            GlebComeBackPhrase = new PhraseClass("Я щас приду");
            GlebTeaPhrase = new PhraseClass("Я за чаем");

            LeCrispJewPhrase = new PhraseClass("Еврей");
            LeCrispAssassinsPhrase = new PhraseClass("Гребанные ассассины");
            LeCrispImpactPhrase = new PhraseClass("Импакт");

            TolyaJewPhrase = new PhraseClass("Еврей");
            TolyaCountPhrase = new PhraseClass("Подсчет");
            TolyaCountReadyPhrase = new PhraseClass("Подсчет");
            TolyaRammusPhrase = new PhraseClass("Раммус мейн");
            TolyaRammus2Phrase = new PhraseClass("Раммус мейн");
            TolyaRammus3Phrase = new PhraseClass("Раммус мейн");
            TolyaRammus4Phrase = new PhraseClass("Раммус мейн");
            TolyaRammus5Phrase = new PhraseClass("Раммус мейн");

            HardKittyLonelyPhrase = new PhraseClass("Одиночество");
            HardKittyDoebatsyaPhrase = new PhraseClass("Доебаться");
            HardKittyMutedPhrase = new PhraseClass("Muted");

            SirinoksFriendsPhrase = new PhraseClass("Заводить друзей");
            SirinoksDragonPhrase = new PhraseClass("Дракон");

            MitsukiCheekyBriki = new PhraseClass("Дерзкая школота");
            MitsukiTooMuchFucking = new PhraseClass("Много выебывается");
            MitsukiGarbageSmell = new PhraseClass("Запах мусора");
            MitsukiSchoolboy = new PhraseClass("Школьник");

            AwdkaTeachToPlay = new PhraseClass("Научите играть");
            AwdkaTrolling = new PhraseClass("Произошел троллинг");
            AwdkaTrying = new PhraseClass("Я пытаюсь!");
            AwdkaAfk = new PhraseClass("АФКА");
            AwdkaTrollingReady = new PhraseClass("Произошел троллинг");

            DarksciNotLucky = new PhraseClass("Не повезло");
            DarksciLucky = new PhraseClass("Повезло");
            DarksciFuckThisGame = new PhraseClass("Да всё нахуй эту игру");
            DarksciDysmoral = new PhraseClass("Дизмораль");
            //end

            //add  as many phrases as you wany
            VampyrVampyr.PassiveLogRus.Add("Вампурская ноо-ооочь!");
            VampyrVampyr.PassiveLogRus.Add("Таинственный Вампур");
            VampyrHematophagy.PassiveLogRus.Add("Я - Вампур из Лондона!");
            VampyrHematophagy.PassiveLogRus.Add("Какая питательная кровь");
            VampyrHematophagy.PassiveLogRus.Add("Вампур-шампур!");
            VampyrHematophagy.PassiveLogRus.Add("Клыки как бур - Вампур");
            VampyrHematophagy.PassiveLogRus.Add("ВОТ МОЯ ФИНАЛЬНАЯ ФОРМА, теперь я - Vampyr!");
            VampyrSuckspenStake.PassiveLogRus.Add("Ай");
            VampyrSuckspenStake.PassiveLogRus.Add("Мое Вампурское сердце не выдержит");
            VampyrSuckspenStake.PassiveLogRus.Add("Ниеееет!");
            VampyrSuckspenStake.PassiveLogRus.Add("Пфхаааш!");
            VampyrNoAttack.PassiveLogRus.Add("Не хочу, не буду");
            VampyrNoAttack.PassiveLogRus.Add("Аа я баюс его");
            VampyrNoAttack.PassiveLogRus.Add("Это Хельсинг, начальнике");
            VampyrNoAttack.PassiveLogRus.Add("От него пахнет чесноком!");
            VampyrNoAttack.PassiveLogRus.Add("Он мне кол в жопу засунул");


            TigrTwoBetter.PassiveLogRus.Add("Лучше с двумя, чем с адекватными");
            TigrTwoBetter.PassiveLogRus.Add("Добро пожаловать в мой клан!");
            TigrThreeZero.PassiveLogRus.Add("Го 1v1");
            TigrThreeZero.PassiveLogRus.Add("2:0");
            TigrThreeZero.PassiveLogRus.Add("Изи 3-0, обоссан");
            TigrTop.PassiveLogRus.Add("Тигр топ, а ты холоп!");
            TigrTop.PassiveLogRus.Add("Я - ТОП1 БЕРСЕРК НА СЕРВЕРЕ!!!");
            TigrSnipe.PassiveLogRus.Add("ЕБАНЫЕ БАНЫ НА 10 ЛЕТ");

            DarksciNotLucky.PassiveLogRus.Add("Сука, не везет с командой!");
            DarksciNotLucky.PassiveLogRus.Add("И вот так каждое промо...");
            DarksciNotLucky.PassiveLogRus.Add("Что они творят?");
            DarksciNotLucky.PassiveLogRus.Add("Я тилтед");
            DarksciLucky.PassiveLogRus.Add("Золотой");
            DarksciLucky.PassiveLogRus.Add("Наконец-то!");
            DarksciFuckThisGame.PassiveLogRus.Add("Нахуй эту игру");
            DarksciDysmoral.PassiveLogRus.Add("Всё, у меня горит!");

            DeepListMadnessPhrase.PassiveLogRus.Add("Хаха. Ха. АХАХАХАХАХАХАХ!");
            DeepListMadnessPhrase.PassiveLogRus.Add("Стоп, кто... я?");
            DeepListMadnessPhrase.PassiveLogRus.Add("Заткнитесь!");
            DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Everything is going according to my plan");
            DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("My superior tactic will win");
            DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Я всё рассчитал, это работает.");
            DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Napoleon Wonnafcuk");
            DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Техника скрытого Листа - Гамбит");

            DeepListSuperMindPhrase.PassiveLogRus.Add("Поделив энтропию на ноль, вы поняли, что ");
            DeepListSuperMindPhrase.PassiveLogRus.Add("Используя свою дедукцию, вы поняли, что ");
            DeepListSuperMindPhrase.PassiveLogRus.Add("Сложив 2+2, вы каким-то чудом догадались, что ");


            MylorikRevengeLostPhrase.PassiveLogRus.Add("Ах вы суки блять!");
            MylorikRevengeLostPhrase.PassiveLogRus.Add("МММХ!");
            MylorikRevengeLostPhrase.PassiveLogRus.Add("ПРРРРРРУ");
            MylorikRevengeLostPhrase.PassiveLogRus.Add("Понерфайте!");
            MylorikRevengeLostPhrase.PassiveLogRus.Add("РАААЗЪЫБУ!");
            MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Вот так!");
            MylorikRevengeVictoryPhrase.PassiveLogRus.Add("ЭТО СПАРТА");
            MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Показал как надо");
            MylorikRevengeVictoryPhrase.PassiveLogRus.Add("ММ!");
            MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Слабый персонаж!");
            MylorikSpanishPhrase.PassiveLogRus.Add("Ааарива!");
            MylorikSpanishPhrase.PassiveLogRus.Add("Буэнос ночес!");
            MylorikSpanishPhrase.PassiveLogRus.Add("Ale handro!");
            MylorikBoolePhrase.PassiveLogRus.Add("Буль");


            GlebChallengerPhrase.PassiveLogRus.Add("А? БАРОН?!");
            GlebChallengerPhrase.PassiveLogRus.Add("Ща я покажу как надо");
            GlebChallengerPhrase.PassiveLogRus.Add("Глебка залетает!");
            GlebChallengerPhrase.PassiveLogRus.Add("В Претендентмобиль!");
            GlebChallengerPhrase.PassiveLogRus.Add("ЛИИИИРОЙ ДЖЕНКИНС");
            GlebSleepyPhrase.PassiveLogRus.Add("Zzzz...");
            GlebSleepyPhrase.PassiveLogRus.Add("Я... Я тут...");
            GlebSleepyPhrase.PassiveLogRus.Add("Только не делайте ремейк *Зевнул*");
            GlebComeBackPhrase.PassiveLogRus.Add("5 минут");
            GlebComeBackPhrase.PassiveLogRus.Add("Я щас приду");
            GlebTeaPhrase.PassiveLogRus.Add("Какао белого цвета");


            LeCrispJewPhrase.PassiveLogRus.Add("Я жру деньги!");
            LeCrispAssassinsPhrase.PassiveLogRus.Add("Гребанные ассассины");
            LeCrispAssassinsPhrase.PassiveLogRus.Add("Эх, жизнь АДК");
            LeCrispImpactPhrase.PassiveLogRus.Add("Импакт!");
            LeCrispImpactPhrase.PassiveLogRus.Add("шпещьмен");


            TolyaJewPhrase.PassiveLogRus.Add("Easy money");
            TolyaJewPhrase.PassiveLogRus.Add("Worth");
            TolyaCountReadyPhrase.PassiveLogRus.Add("Я готов это просчитать");
            TolyaCountPhrase.PassiveLogRus.Add("Ха! Подстчет!");
            TolyaCountPhrase.PassiveLogRus.Add("Так и знал!");
            TolyaCountPhrase.PassiveLogRus.Add("Предиктед");
            TolyaRammusPhrase.PassiveLogRus.Add("Okay.");
            TolyaRammusPhrase.PassiveLogRus.Add("Hm.");
            TolyaRammus2Phrase.PassiveLogRus.Add("Я живу и горю");
            TolyaRammus3Phrase.PassiveLogRus.Add("Изи разбайтил");
            TolyaRammus4Phrase.PassiveLogRus.Add("Посмотри сколько я поднял за раз!");
            TolyaRammus5Phrase.PassiveLogRus.Add("Оу, ну всё. ГГ.");


            HardKittyLonelyPhrase.PassiveLogRus.Add("Привет");
            HardKittyLonelyPhrase.PassiveLogRus.Add("Мне сегодня снилось, как...");
            HardKittyLonelyPhrase.PassiveLogRus.Add("Что делаешь?");
            HardKittyLonelyPhrase.PassiveLogRus.Add("Как дела?");
            HardKittyDoebatsyaPhrase.PassiveLogRus.Add("У вас продают молоко в пакетах?");
            HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Какой у тебя Windows?");
            HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Что лучше взять на MF?");
            HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Эй, э-эй...");
            HardKittyMutedPhrase.PassiveLogRus.Add("Muted");


            SirinoksFriendsPhrase.PassiveLogRus.Add("Го в пати");
            SirinoksFriendsPhrase.PassiveLogRus.Add("М/Ж?");
            SirinoksDragonPhrase.PassiveLogRus.Add("РООАР!");


            MitsukiCheekyBriki.PassiveLogRus.Add("Ну чё, ёпта, поиграем?");
            MitsukiTooMuchFucking.PassiveLogRus.Add("Алмаз!");
            MitsukiTooMuchFucking.PassiveLogRus.Add("Ну так-то всё правильно");
            MitsukiTooMuchFucking.PassiveLogRus.Add("Сука ня");
            MitsukiTooMuchFucking.PassiveLogRus.Add("Чё пацаны, аниме?");
            MitsukiTooMuchFucking.PassiveLogRus.Add("Наслаждайтесь жижей!");
            MitsukiGarbageSmell.PassiveLogRus.Add("Во что это я вляпался?");
            MitsukiGarbageSmell.PassiveLogRus.Add("Запахло мусором...");
            MitsukiSchoolboy.PassiveLogRus.Add("Да блять, своего компьютера-то нету");


            AwdkaTeachToPlay.PassiveLogRus.Add("Научите играть");
            AwdkaTeachToPlay.PassiveLogRus.Add("Я ничего не умею");
            AwdkaTeachToPlay.PassiveLogRus.Add("Я бесполезный");
            AwdkaTeachToPlay.PassiveLogRus.Add("Кстати, я хач.");
            AwdkaTeachToPlay.PassiveLogRus.Add("Как это работает?");
            AwdkaTeachToPlay.PassiveLogRus.Add("Киберспорт!");
            AwdkaTeachToPlay.PassiveLogRus.Add("Что это?");
            AwdkaTeachToPlay.PassiveLogRus.Add("Как там нажимать?");
            AwdkaTeachToPlay.PassiveLogRus.Add("Два игнайта на вуконга!");
            AwdkaTrolling.PassiveLogRus.Add("Произошел троллинг");
            AwdkaTrolling.PassiveLogRus.Add("Ого... Это как?");
            AwdkaTrolling.PassiveLogRus.Add("Что я сделаль?");
            AwdkaTrying.PassiveLogRus.Add("Сапаги сораходы! Дёшево!");
            AwdkaTrying.PassiveLogRus.Add("Флеш-ульт в клона лебланки");
            AwdkaTrying.PassiveLogRus.Add("Что начинает получаться");
            AwdkaTrying.PassiveLogRus.Add("Почти...");
            AwdkaTrying.PassiveLogRus.Add("В киберспорт!");
            AwdkaAfk.PassiveLogRus.Add("AFKA");
            AwdkaTrollingReady.PassiveLogRus.Add("троллинг готов к использованию");
            //end
        }


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        //class needed to send unique logs.
        public class PhraseClass
        {
            public List<string> PassiveLogEng = new List<string>();
            public List<string> PassiveLogRus = new List<string>();
            public string PassiveNameEng;
            public string PassiveNameRus;


            public PhraseClass(string passiveNameRus, string passiveNameEng = "")
            {
                PassiveNameRus = passiveNameRus;
                PassiveNameEng = passiveNameEng;
            }

            public async Task SendLog(GamePlayerBridgeClass player)
            {
                var random = new Random();
                var description = PassiveLogRus[random.Next(0, PassiveLogRus.Count)];


                if (player.DiscordAccount.IsLogs)
                    player.Status.AddInGamePersonalLogs($"{PassiveNameRus}: ");
                else
                    player.Status.InGamePersonalLogsAll += $"{PassiveNameRus}: ";

                player.Status.AddInGamePersonalLogs($"{description}\n");
                await Task.CompletedTask;
            }

            public async Task SendLog(GamePlayerBridgeClass player, GamePlayerBridgeClass player2)
            {
                if (player.Character.Name == "DeepList")
                {
                    var random = new Random();

                    var description = PassiveLogRus[random.Next(0, PassiveLogRus.Count)];

                    description += $"{player2.DiscordAccount.DiscordUserName} - {player2.Character.Name}";

                    if (player.DiscordAccount.IsLogs)
                        player.Status.AddInGamePersonalLogs($"{PassiveNameRus}: ");
                    else
                        player.Status.InGamePersonalLogsAll += $"{PassiveNameRus}: ";

                    player.Status.AddInGamePersonalLogs($"{description}\n");
                }

                await Task.CompletedTask;
            }
        }
    }
}