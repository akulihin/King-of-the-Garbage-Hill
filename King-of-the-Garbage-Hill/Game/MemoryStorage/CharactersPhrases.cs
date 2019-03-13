using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage
{

    public class CharactersUniquePhrase : IServiceSingleton
    {
        //initialize variables 
        public PhraseClass DeepListMadnessPhrase;
        public PhraseClass DeepListDoubtfulTacticPhrase;
        public PhraseClass DeepListSuperMindPhrase;
        public PhraseClass GlebChallengerPhrase;
        public PhraseClass LeCrispAssassinsPhrase;
        public PhraseClass LeCrispImpactPhrase;
        public PhraseClass LeCrispJewPhrase;
        public PhraseClass MylorikRevengeLostPhrase;
        public PhraseClass MylorikRevengeVictoryPhrase;
        public PhraseClass MylorikPhrase;
        public PhraseClass MylorikSpanishPhrase;
        public PhraseClass TolyaCountPhrase;
        public PhraseClass TolyaJewPhrase;
        public PhraseClass TolyaRammusPhrase;
        public PhraseClass HardKittyLonelyPhrase;
        public PhraseClass HardKittyDoebatsyaPhrase;
        public PhraseClass HardKittyMutedPhrase;
        public PhraseClass SirinoksFriendsPhrase;
        public PhraseClass SirinoksDragonPhrase;
        //end

        public CharactersUniquePhrase()
        {
            //add values
            DeepListMadnessPhrase = new PhraseClass("Безумие");
            DeepListDoubtfulTacticPhrase = new PhraseClass("Сомнительная тактика");
            DeepListSuperMindPhrase = new PhraseClass("Сверхразум");
            MylorikRevengeLostPhrase = new PhraseClass("Месть");
            MylorikRevengeVictoryPhrase = new PhraseClass("Месть");
            MylorikPhrase = new PhraseClass("Буль" );
            MylorikSpanishPhrase = new PhraseClass("Испанец");
            GlebChallengerPhrase = new PhraseClass("Претендент русского сервера" );
            LeCrispJewPhrase = new PhraseClass("Еврей||" );
            LeCrispAssassinsPhrase = new PhraseClass("Гребанные ассассины" );
            LeCrispImpactPhrase = new PhraseClass("Импакт" );
            TolyaJewPhrase = new PhraseClass("Еврей" );
            TolyaCountPhrase = new PhraseClass("Подсчет" );
            TolyaRammusPhrase = new PhraseClass(" Раммус мейн" );
            HardKittyLonelyPhrase = new PhraseClass("Одиночество" );
            HardKittyDoebatsyaPhrase = new PhraseClass("Доебаться");
            HardKittyMutedPhrase = new  PhraseClass("Muted");
            SirinoksFriendsPhrase = new PhraseClass("Заводить друзей");
            SirinoksDragonPhrase = new PhraseClass("Дракон");
            //end

            //add  as many phrases as you wany
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
            //end
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
                        var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync( "", false, embed2.Build());
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
                        var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync( "", false, embed.Build());
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



        public Task InitializeAsync() => Task.CompletedTask;
    }

}
