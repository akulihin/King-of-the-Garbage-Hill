using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterPassives : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        private readonly SecureRandom _rand;

        public CharacterPassives(SecureRandom rand)
        {
            _rand = rand;
        }

        public async Task HandleCharacterBeforeCalculations(GameBridgeClass player, GameClass game)
        {
            var characterName = player.Character.Name;
            switch (characterName)
            {
                case "DeepList":
                    await HandleDeepList(player, game);
                    break;
                case "mylorik":
                    HandleMylorik(player);
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
        }

        private static readonly List<WhenToTriggerClass> DeepListMadnessTriggeredWhen = new List<WhenToTriggerClass>();


        private static readonly List<ulong> DoubtfulTactic = new List<ulong>();


        private async Task HandleDeepList(GameBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            if (DoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn))
            {
                player.Character.Strength++;
                //continiue
            }
            else
            {
                DoubtfulTactic.Add(player.Status.WhoToAttackThisTurn);
                player.Character.Strength++;
                player.Status.IsAbleToWin = false;
            }
            //end Doubtful tactic

            //MADNESS

            if (DeepListMadnessTriggeredWhen.Any(x =>
                x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId))
            {
                if (player.Status.IsAbleToWin)
                    if (DeepListMadnessTriggeredWhen.Find(x => x.DiscordId == player.DiscordAccount.DiscordId)
                        .WhenToTrigger.Any(x => x == game.RoundNo))
                    {
                        var randStr = _rand.Random(0, 10);
                        var randSpeed = _rand.Random(0, 10);
                     //   var randInt = _rand.Random(0, 10);
                        var randPs= _rand.Random(0, 10);

                        player.Character.Strength = randStr;
                        player.Character.Speed = randSpeed;
                        //   player.Character.Intelligence = randInt;
                        player.Character.Psyche = randPs;
                        await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("Ты сошел с ума...");
                    }
            }
            else
            {
                DeepListMadnessTriggeredWhen.Add(new WhenToTriggerClass(player.DiscordAccount.DiscordId,
                    game.GameId));
                var rand = _rand.Random(1, 8);
                if (rand == 1 || rand == 2)
                {  
                    do
                    {
                        var when = _rand.Random(1, 10);

                        if (DeepListMadnessTriggeredWhen
                            .Find(x => x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId)
                            .WhenToTrigger.All(b => b != when))
                            DeepListMadnessTriggeredWhen.Find(x => x.DiscordId == player.DiscordAccount.DiscordId)
                                .WhenToTrigger.Add(when);
                    } while (DeepListMadnessTriggeredWhen.Find(x => x.DiscordId == player.DiscordAccount.DiscordId)
                                 .WhenToTrigger.Count < rand);
                }
            }


            //end MADNESS

            //Сверхразум
            if (player.Status.IsAbleToWin)
            {
                var rand = _rand.Random(-10000, 10000);

                if (rand > -228 && rand < 228)
                {
                    var randPlayer = _rand.Random(0, game.PlayersList.Count - 1);
                    await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                        $"хм... {player.DiscordAccount.DiscordUserName} это {player.Character.Name}!\n" +
                        $"Его статы: Cила {player.Character.Strength}, Скорость {player.Character.Speed}" +
                        $", Ум {player.Character.Intelligence}");
                }
            }
            //end Сверхразум

            //Стёб

            //only after

            //end Стёб
        }

        private void HandleMylorik(GameBridgeClass player)
        {
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

        //after


        private static int WonTimesLastTime;

        private void HandleDeepListAfter(GameBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            player.Status.IsAbleToWin = true;
            if (DoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn)) player.Character.Strength--;
            //end Doubtful tactic

            //Стёб
            if (player.Status.WonTimes % 2 == 0 && player.Status.WonTimes != WonTimesLastTime)
            {
                WonTimesLastTime = player.Status.WonTimes;
                var player2 =
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.WhoToAttackThisTurn);

                player2.Character.Psyche--;

                if (player2.Character.Psyche < 4) player2.Character.Justice.JusticeForNextRound--;
            }

            //end Стёб
        }

        private void HandleMylorikAfter(GameBridgeClass player)
        {
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
    }
}