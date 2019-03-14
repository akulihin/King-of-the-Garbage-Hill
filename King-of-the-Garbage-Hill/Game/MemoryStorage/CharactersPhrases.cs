﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage
{
    public class CharactersUniquePhrase : IServiceSingleton
    {
        public PhraseClass AwdkaAfk;
        public PhraseClass AwdkaTeachToPlay;
        public PhraseClass AwdkaTrolling;
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
        public PhraseClass MylorikPhrase;
        public PhraseClass MylorikRevengeLostPhrase;
        public PhraseClass MylorikRevengeVictoryPhrase;
        public PhraseClass MylorikSpanishPhrase;
        public PhraseClass SirinoksDragonPhrase;
        public PhraseClass SirinoksFriendsPhrase;
        public PhraseClass TolyaCountPhrase;
        public PhraseClass TolyaJewPhrase;

        public PhraseClass TolyaRammusPhrase;
        //end

        public CharactersUniquePhrase()
        {
            //add values
            DeepListMadnessPhrase = new PhraseClass("Безумие");
            DeepListDoubtfulTacticPhrase = new PhraseClass("Сомнительная тактика");
            DeepListSuperMindPhrase = new PhraseClass("Сверхразум");

            MylorikRevengeLostPhrase = new PhraseClass("Месть");
            MylorikRevengeVictoryPhrase = new PhraseClass("Месть");
            MylorikPhrase = new PhraseClass("Буль");
            MylorikSpanishPhrase = new PhraseClass("Испанец");

            GlebChallengerPhrase = new PhraseClass("Претендент русского сервера");

            LeCrispJewPhrase = new PhraseClass("Еврей||");
            LeCrispAssassinsPhrase = new PhraseClass("Гребанные ассассины");
            LeCrispImpactPhrase = new PhraseClass("Импакт");

            TolyaJewPhrase = new PhraseClass("Еврей");
            TolyaCountPhrase = new PhraseClass("Подсчет");
            TolyaRammusPhrase = new PhraseClass(" Раммус мейн");

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

            DarksciNotLucky = new PhraseClass("Не повезло");
            DarksciLucky = new PhraseClass("Повезло");
            DarksciFuckThisGame = new PhraseClass("Да всё нахуй эту игру");
            DarksciDysmoral = new PhraseClass("Дизмораль");
            //end

            //add  as many phrases as you wany

            DarksciNotLucky.PassiveLogRus.Add("Сука, не везет с командой!");
            DarksciNotLucky.PassiveLogRus.Add("И вот так каждое промо...");
            DarksciNotLucky.PassiveLogRus.Add("Что они творят?");
            DarksciNotLucky.PassiveLogRus.Add("Я тилтед");
            DarksciLucky.PassiveLogRus.Add("Золотой");
            DarksciLucky.PassiveLogRus.Add("Наконец-то!");
            DarksciFuckThisGame.PassiveLogRus.Add("Нахуй эту игру");
            DarksciDysmoral.PassiveLogRus.Add("Всё, у меня горит!");

            DeepListMadnessPhrase.PassiveLogRus.Add("Хаха. Ха. || АХАХАХАХАХАХАХ!");
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
            MylorikPhrase.PassiveLogRus.Add("Буль");


            GlebChallengerPhrase.PassiveLogRus.Add("А? БАРОН?!");
            GlebChallengerPhrase.PassiveLogRus.Add("Ща я покажу как надо");
            GlebChallengerPhrase.PassiveLogRus.Add("Глебка залетает!");
            GlebChallengerPhrase.PassiveLogRus.Add("В Претендентмобиль!");
            GlebChallengerPhrase.PassiveLogRus.Add("ЛИИИИРОЙ ДЖЕНКИНС");


            LeCrispJewPhrase.PassiveLogRus.Add("Я жру деньги!");
            LeCrispAssassinsPhrase.PassiveLogRus.Add("Гребанные ассассины");
            LeCrispImpactPhrase.PassiveLogRus.Add("Импакт!");
            LeCrispImpactPhrase.PassiveLogRus.Add("шпещьмен");


            TolyaJewPhrase.PassiveLogRus.Add("Easy money");
            TolyaJewPhrase.PassiveLogRus.Add("Worth");
            TolyaCountPhrase.PassiveLogRus.Add("Ха! Подстчет!");
            TolyaRammusPhrase.PassiveLogRus.Add("Okay.");
            TolyaRammusPhrase.PassiveLogRus.Add("Hm.");
            TolyaRammusPhrase.PassiveLogRus.Add("Я живу и горю");


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
            // SirinoksFriendsPhrase.PassiveLogRus.Add("");
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

            public async Task SendLog(GameBridgeClass player)
            {
                if (player.DiscordAccount.IsPlaying)
                {
                    var random = new Random();
                    var embed = new EmbedBuilder();
                    var description = PassiveLogRus[random.Next(0, PassiveLogRus.Count)];

                    if (description.Contains("||"))
                    {
                        var twoPhrases = description.Split("||");
                        var embed2 = new EmbedBuilder();
                        embed2.WithColor(random.Next(0, 255), random.Next(0, 255), random.Next(0, 255));
                        embed.WithDescription(twoPhrases[0]);
                        var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("", false,
                            embed2.Build());
#pragma warning disable 4014
                        DeleteMessOverTime(mess2, 10);
#pragma warning restore 4014
                        await Task.Delay(2000);
                        description = twoPhrases[1];
                    }

                    embed.WithDescription(description);

                    if (player.DiscordAccount.IsLogs) embed.WithFooter(PassiveNameRus);
                    embed.WithColor(random.Next(0, 255), random.Next(0, 255), random.Next(0, 255));

                    if (!player.IsBot())
                    {
                        var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("", false,
                            embed.Build());
#pragma warning disable 4014
                        DeleteMessOverTime(mess, 10);
#pragma warning restore 4014
                    }
                }
            }

            private async Task DeleteMessOverTime(IUserMessage message, int timeInSeconds)
            {
                var seconds = timeInSeconds * 1000;
                await Task.Delay(seconds);
                await message.DeleteAsync();
            }
        }
    }
}