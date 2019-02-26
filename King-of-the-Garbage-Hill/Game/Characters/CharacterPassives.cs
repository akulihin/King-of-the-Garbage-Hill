using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterPassives : IServiceSingleton
    {
        private static readonly List<WhenToTriggerClass>
            DeepListMadnessTriggeredWhen = new List<WhenToTriggerClass>(); // TODO:

        private static readonly List<WhenToTriggerClass>
            DeepListSupermindTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<WhenToTriggerClass>
            MylorikBooleTriggeredWhen = new List<WhenToTriggerClass>();


        private static readonly List<WhenToTriggerClass>
            AllSkipTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<WhenToTriggerClass>
            GlebSleepingTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<WhenToTriggerClass>
            GlebChallengerTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<WhenToTriggerClass>
            GlebComeBackTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<WhenToTriggerClass>
            GlebTeaTriggeredWhen = new List<WhenToTriggerClass>();

        private static readonly List<ulong> DeepListDoubtfulTactic = new List<ulong>();

        private static readonly List<MylorikRevengeClass> MylorikRevenge = new List<MylorikRevengeClass>();

        private static readonly List<Mockery> DeepListMockeryList = new List<Mockery>();
        private readonly HelperFunctions _help;


        private readonly SecureRandom _rand;

        public CharacterPassives(SecureRandom rand, HelperFunctions help)
        {
            _rand = rand;
            _help = help;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task HandleEveryAttack(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;

            switch (characterName)
            {
                case "Глеб":
                    var rand = _rand.Random(1, 8);
                    if (rand == 1)
                        GlebSleepingTriggeredWhen.Find(x =>
                                x.DiscordId == player.DiscordAccount.DiscordId && game.GameId == x.GameId).WhenToTrigger
                            .Add(game.RoundNo + 1);
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleEveryAttackFromMe(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;

            switch (characterName)
            {
                case "Глеб":
                    var rand = _rand.Random(1, 10);
                    if (rand == 1)
                        AllSkipTriggeredWhen.Add(new WhenToTriggerClass(player.Status.WhoToAttackThisTurn, game.GameId,
                            game.RoundNo + 1));
                    break;
            }

            await Task.CompletedTask;
        }


        public async Task HandleCharacterBeforeCalculations(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    HandleDeepList(player);
                    break;
                case "mylorik":
                    await HandleMylorik(player, game);
                    break;
                case "Глеб":
                    HandleGleb(player);
                    break;
                case "LeCrisp":
                    HandleLeCrisp(player);
                    break;
                case "Толя":
                    HandleTolya(player);
                    break;
                case "HardKitty":
                    HandleHardKitty(player);
                    break;
                case "Sirinoks":
                    HandleSirinoks(player);
                    break;
                case "Mitsuki":
                    HandleMitsuki(player);
                    break;
                case "AWDKA":
                    HandleAWDKA(player);
                    break;
                case "Осьминожка":
                    HandleOctopus(player);
                    break;
                case "Darksi":
                    HandleDarksi(player);
                    break;
                case "Тигр":
                    HandleTigr(player);
                    break;
                case "Братишка":
                    HandleShark(player);
                    break;
                case "????":
                    HandleDeepList2(player);
                    break;
            }

            await Task.CompletedTask;
        }

        public async Task HandleCharacterAfterCalculations(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    HandleDeepListAfter(player, game);
                    break;
                case "mylorik":
                    HandleMylorikAfter(player);
                    break;
                case "Глеб":
                    HandleGlebAfter(player);
                    break;
                case "LeCrisp":
                    HandleLeCrispAfter(player);
                    break;
                case "Толя":
                    HandleTolyaAfter(player);
                    break;
                case "HardKitty":
                    HandleHardKittyAfter(player);
                    break;
                case "Sirinoks":
                    HandleSirinoksAfter(player);
                    break;
                case "Mitsuki":
                    HandleMitsukiAfter(player);
                    break;
                case "AWDKA":
                    HandleAWDKAAfter(player);
                    break;
                case "Осьминожка":
                    HandleOctopusAfter(player);
                    break;
                case "Darksi":
                    HandleDarksiAfter(player);
                    break;
                case "Тигр":
                    HandleTigrAfter(player);
                    break;
                case "Братишка":
                    HandleSharkAfter(player);
                    break;
                case "????":
                    HandleDeepList2After(player);
                    break;
            }

            await Task.CompletedTask;
        }


        public void CalculatePassiveChances(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                WhenToTriggerClass when;
                switch (characterName)
                {
                    case "DeepList":

                        when = GetWhenToTrigger(player, true, 7, 1);
                        DeepListSupermindTriggeredWhen.Add(when);

                        break;
                    case "mylorik":
                        when = GetWhenToTrigger(player, false, 10, 2);
                        MylorikBooleTriggeredWhen.Add(when);
                        break;

                    case "Глеб":
                        when = GetWhenToTrigger(player, false, 8, 3);
                        GlebSleepingTriggeredWhen.Add(when);
                        int[] temp = {-1, -1, -1};
                        if (when.WhenToTrigger.Count >= 1)
                            temp[0] = when.WhenToTrigger[0];
                        if (when.WhenToTrigger.Count >= 2)
                            temp[1] = when.WhenToTrigger[1];
                        if (when.WhenToTrigger.Count >= 3)
                            temp[2] = when.WhenToTrigger[2];


                        do
                        {
                            when = GetWhenToTrigger(player, true, 12, 2);
                        } while (when.WhenToTrigger.All(x => x == temp[0] || x == temp[1] || x == temp[2]));

                        GlebChallengerTriggeredWhen.Add(when);

                        break;
                }
            }
        }


        public WhenToTriggerClass GetWhenToTrigger(GameBridgeClass player, bool isMandatory, int maxRandomNumber,
            int maxTimes)
        {
            var toTriggerClass = new WhenToTriggerClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId);
            int when;


            if (isMandatory)
            {
                when = _rand.Random(1, 10);
                toTriggerClass.WhenToTrigger.Add(when);
            }


            var rand = _rand.Random(1, maxRandomNumber);
            var times = 0;

            switch (rand)
            {
                case 1 when maxTimes >= 1:
                {
                    while (times < rand)
                    {
                        when = _rand.Random(1, 10);

                        if (toTriggerClass.WhenToTrigger.All(x => x != when))
                        {
                            toTriggerClass.WhenToTrigger.Add(when);
                            times++;
                        }
                    }

                    break;
                }
                case 2 when maxTimes >= 2:
                {
                    while (times < rand)
                    {
                        when = _rand.Random(1, 10);

                        if (toTriggerClass.WhenToTrigger.All(x => x != when))
                        {
                            toTriggerClass.WhenToTrigger.Add(when);
                            times++;
                        }
                    }

                    break;
                }
                case 3 when maxTimes >= 3:
                {
                    while (times < rand)
                    {
                        when = _rand.Random(1, 10);

                        if (toTriggerClass.WhenToTrigger.All(x => x != when))
                        {
                            toTriggerClass.WhenToTrigger.Add(when);
                            times++;
                        }
                    }

                    break;
                }
            }

            return toTriggerClass;
        }


        public async Task HandleNextRound(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                switch (characterName)
                {
                    case "mylorik":
                        var acc = MylorikBooleTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = true;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;
                                var mess = await player.Status.SocketMessageFromBot.Channel
                                    .SendMessageAsync("Ты буль.");
                                await _help.DeleteMessOverTime(mess, 15);
                            }

                        break;
                    case "Глеб":
                        acc = GlebSleepingTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Status.IsSkip = true;
                                player.Status.IsBlock = true;
                                player.Status.IsAbleToTurn = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = 0;
                                var mess =
                                    await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("Ты уснул.");
                                await _help.DeleteMessOverTime(mess, 15);
                            }


                        acc = GlebChallengerTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Character.Intelligence += 9;
                                player.Character.Speed += 9;
                                player.Character.Strength += 9;
                                player.Character.Psyche += 9;
                                var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    "Ты ведь претендент русского сервера!");

                                await _help.DeleteMessOverTime(mess, 15);
                            }

                        break;
                    case "DeepList":

                        //Сверхразум
                        var currentDeepList = DeepListSupermindTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && game.GameId == x.GameId);

                        if (currentDeepList != null)
                        {
                            if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                            {
                                var randPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    $"хм... {randPlayer.DiscordAccount.DiscordUserName} это {randPlayer.Character.Name}!\n" +
                                    $"Его статы: Cила {randPlayer.Character.Strength}, Скорость {randPlayer.Character.Speed}" +
                                    $", Ум {randPlayer.Character.Intelligence}");
                                await _help.DeleteMessOverTime(mess, 45);
                            }
                        }
                        else
                        {
                            Console.WriteLine("DEEP LIST SUPERMIND PASSIVE ERRORR!!!!!!!!!!!!!!!!!");
                        }


                        //end Сверхразум


                        break;
                }

                var isSkip = AllSkipTriggeredWhen.Find(x =>
                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId &&
                    x.WhenToTrigger.Contains(game.RoundNo));

                if (isSkip != null)
                {
                    player.Status.IsSkip = true;
                    player.Status.IsBlock = true;
                    player.Status.IsAbleToTurn = false;
                    player.Status.IsReady = true;
                    player.Status.WhoToAttackThisTurn = 0;
                    var mess = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                        "Хм... ТЫ пропустишь этот ход....");
                    await _help.DeleteMessOverTime(mess, 15);
                }
            }

            await Task.CompletedTask;
        }

        public async Task HandleEndOfRound(GameClass game)
        {
            foreach (var player in game.PlayersList)
            {
                var characterName = player.Character.Name;
                switch (characterName)
                {
                    case "Глеб":
                        var acc = GlebChallengerTriggeredWhen.Find(x =>
                            x.DiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

                        if (acc != null)
                            if (acc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                player.Character.Intelligence -= 9;
                                player.Character.Speed -= 9;
                                player.Character.Strength -= 9;
                                player.Character.Psyche -= 9;
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("Был.");
                            }


                        break;
                }
            }

            await Task.CompletedTask;
        }


        private void HandleDeepList(GameBridgeClass player)
        {
            //Doubtful tactic
            if (DeepListDoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn))
            {
                player.Character.Strength++;
                //continiue
            }
            else
            {
                DeepListDoubtfulTactic.Add(player.Status.WhoToAttackThisTurn);
                player.Character.Strength++;
                player.Status.IsAbleToWin = false;
            }
            //end Doubtful tactic

            //MADNESS

            //TODO: new balance 

            //end MADNESS


            //Стёб

            //only after

            //end Стёб
        }

        public void HandleMylorikRevenge(GameBridgeClass player1, ulong player2Id, ulong gameId)
        {
            var mylorik = MylorikRevenge.Find(x =>
                x.GameId == gameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);


            if (mylorik == null)
            {
                MylorikRevenge.Add(new MylorikRevengeClass(player1.DiscordAccount.DiscordId, gameId,
                    player2Id));
            }
            else
            {
                if (mylorik.EnemyListDiscordId.All(x => x.EnemyDiscordId != player2Id))
                {
                    mylorik.EnemyListDiscordId.Add(new MylorikRevengeClassSub(player2Id));
                    return;
                }

                var find = mylorik.EnemyListDiscordId.Find(x =>
                    x.EnemyDiscordId == player2Id && x.IsUnique);
                if (find != null)
                {
                    player1.Status.Score++;
                    find.IsUnique = false;
                }
            }
        }

        private async Task HandleMylorik(GameBridgeClass player, GameClass game)
        {
            //Boole


            await Task.CompletedTask;
            //end Boole
        }

        private void HandleGleb(GameBridgeClass player)
        {
        }

        private void HandleLeCrisp(GameBridgeClass player)
        {
        }

        private void HandleTolya(GameBridgeClass player)
        {
        }

        private void HandleHardKitty(GameBridgeClass player)
        {
        }

        private void HandleSirinoks(GameBridgeClass player)
        {
        }

        private void HandleMitsuki(GameBridgeClass player)
        {
        }

        private void HandleAWDKA(GameBridgeClass player)
        {
        }

        private void HandleOctopus(GameBridgeClass player)
        {
        }

        private void HandleDarksi(GameBridgeClass player)
        {
        }

        private void HandleTigr(GameBridgeClass player)
        {
        }

        private void HandleShark(GameBridgeClass player)
        {
        }

        private void HandleDeepList2(GameBridgeClass player)
        {
        }

        public void HandleMockery(GameBridgeClass player, GameBridgeClass player2, GameClass game)
        {
            //Стёб
            var currentDeepList =
                DeepListMockeryList.Find(x =>
                    x.PlayerDiscordId == player.DiscordAccount.DiscordId && game.GameId == x.GameId);

            if (currentDeepList != null)
            {
                var currentDeepList2 =
                    currentDeepList.WhoWonTimes.Find(x => x.EnemyDiscordId == player2.DiscordAccount.DiscordId);

                if (currentDeepList2 != null)
                {
                    currentDeepList2.Times++;

                    if (currentDeepList2.Times % 2 != 0 && currentDeepList2.Times != 1)
                    {
                        player2.Character.Psyche--;
                        if (player2.Character.Psyche < 4) player2.Character.Justice.JusticeForNextRound--;
                    }
                }
                else
                {
                    var toAdd = new Mockery(new List<MockerySub> {new MockerySub(player2.DiscordAccount.DiscordId, 1)},
                        game.GameId, player.DiscordAccount.DiscordId);
                    DeepListMockeryList.Add(toAdd);
                }
            }
            else
            {
                var toAdd = new Mockery(new List<MockerySub> {new MockerySub(player2.DiscordAccount.DiscordId, 1)},
                    game.GameId, player.DiscordAccount.DiscordId);
                DeepListMockeryList.Add(toAdd);
            }

            //end Стёб
        }

        private void HandleDeepListAfter(GameBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            player.Status.IsAbleToWin = true;
            if (DeepListDoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn)) player.Character.Strength--;
            //end Doubtful tactic

            // Стёб
            if (player.Status.IsWonLastTime != 0)
            {
                var player2 = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime);
                HandleMockery(player, player2, game);
            }

            //end Стёб
        }

        private void HandleMylorikAfter(GameBridgeClass player)
        {
            //Revenge
            if (player.Status.IsWonLastTime != 0)
                HandleMylorikRevenge(player, player.Status.IsWonLastTime, player.DiscordAccount.GameId);
            //end Revenge

            //Spanish
            if (player.Status.IsWonLastTime == 0)
            {
                var rand = _rand.Random(1, 2);

                if (rand == 1) player.Character.Justice.JusticeForNextRound--;
            }

            //end Spanish
        }

        private void HandleGlebAfter(GameBridgeClass player)
        {
        }

        private void HandleLeCrispAfter(GameBridgeClass player)
        {
        }

        private void HandleTolyaAfter(GameBridgeClass player)
        {
        }

        private void HandleHardKittyAfter(GameBridgeClass player)
        {
        }

        private void HandleSirinoksAfter(GameBridgeClass player)
        {
        }

        private void HandleMitsukiAfter(GameBridgeClass player)
        {
        }

        private void HandleAWDKAAfter(GameBridgeClass player)
        {
        }

        private void HandleOctopusAfter(GameBridgeClass player)
        {
        }

        private void HandleDarksiAfter(GameBridgeClass player)
        {
        }

        private void HandleTigrAfter(GameBridgeClass player)
        {
        }

        private void HandleSharkAfter(GameBridgeClass player)
        {
        }

        private void HandleDeepList2After(GameBridgeClass player)
        {
        }

        public class WhenToTriggerClass
        {
            public ulong DiscordId;
            public ulong GameId;
            public List<int> WhenToTrigger;

            public WhenToTriggerClass(ulong discordId, ulong gameId)
            {
                DiscordId = discordId;
                WhenToTrigger = new List<int>();
                GameId = gameId;
            }

            public WhenToTriggerClass(ulong discordId, ulong gameId, int when)
            {
                DiscordId = discordId;
                WhenToTrigger = new List<int> {when};
                GameId = gameId;
            }
        }

        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListDiscordId;
            public ulong GameId;
            public ulong PlayerDiscordId;

            public MylorikRevengeClass(ulong playerDiscordId, ulong gameID, ulong firstLost)
            {
                PlayerDiscordId = playerDiscordId;
                EnemyListDiscordId = new List<MylorikRevengeClassSub> {new MylorikRevengeClassSub(firstLost)};
                GameId = gameID;
            }
        }

        public class MylorikRevengeClassSub
        {
            public ulong EnemyDiscordId;
            public bool IsUnique;

            public MylorikRevengeClassSub(ulong enemyDiscordId)
            {
                EnemyDiscordId = enemyDiscordId;
                IsUnique = true;
            }
        }

        //after


        public class Mockery
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<MockerySub> WhoWonTimes;

            public Mockery(List<MockerySub> whoWonTimes, ulong gameId, ulong playerDiscordId)
            {
                WhoWonTimes = whoWonTimes;
                GameId = gameId;
                PlayerDiscordId = playerDiscordId;
            }
        }

        public class MockerySub
        {
            public ulong EnemyDiscordId;
            public int Times;

            public MockerySub(ulong enemyDiscordId, int times)
            {
                EnemyDiscordId = enemyDiscordId;
                Times = times;
            }
        }
    }
}