using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class DeepList : IServiceSingleton
    { 
        private readonly SecureRandom _rand;
        private readonly GameUpdateMess _upd;
        private readonly InGameGlobal _gameGlobal;

        public DeepList(GameUpdateMess upd, SecureRandom rand, InGameGlobal gameGlobal)
        {
            _upd = upd;
            _rand = rand;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        
        public void HandleDeepList(GameBridgeClass player)
        {
            //Doubtful tactic
            if (_gameGlobal.DeepListDoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn))
            {
                player.Character.Strength++;
                //continiue
            }
            else
            {
                _gameGlobal.DeepListDoubtfulTactic.Add(player.Status.WhoToAttackThisTurn);
                player.Character.Strength++;
                player.Status.IsAbleToWin = false;
            }
        }

        public void HandleDeepListAfter(GameBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            player.Status.IsAbleToWin = true;
            if (_gameGlobal.DeepListDoubtfulTactic.Contains(player.Status.WhoToAttackThisTurn))
            {
                player.Character.Strength--;
                if (player.Status.IsWonLastTime != 0)
                {
                    player.Status.Score++;
                }
            }
            //end Doubtful tactic

            // Стёб
            if (player.Status.IsWonLastTime != 0)
            {
                var player2 = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime);
                HandleMockery(player, player2, game);
            }

            //end Стёб
        }

             
        public void HandleMockery(GameBridgeClass player, GameBridgeClass player2, GameClass game)
        {
            //Стёб
            var currentDeepList =
                _gameGlobal.DeepListMockeryList.Find(x =>
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
                        player.Status.Score++;
                        if (player2.Character.Psyche < 4)
                        {
                            player2.Character.Justice.JusticeForNextRound--;
                        }
                    }
                }
                else
                {
                    var toAdd = new Mockery(new List<MockerySub> {new MockerySub(player2.DiscordAccount.DiscordId, 1)},
                        game.GameId, player.DiscordAccount.DiscordId);
                    _gameGlobal.DeepListMockeryList.Add(toAdd);
                }
            }
            else
            {
                var toAdd = new Mockery(new List<MockerySub> {new MockerySub(player2.DiscordAccount.DiscordId, 1)},
                    game.GameId, player.DiscordAccount.DiscordId);
                _gameGlobal.DeepListMockeryList.Add(toAdd);
            }

            //end Стёб
        }

        
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

        public class SuperMindKnown
        {
            public ulong DiscordId;
            public ulong GameId;
            public List<ulong> KnownPlayers = new List<ulong>();

            public SuperMindKnown(ulong discordId, ulong gameId, ulong player2Id)
            {
                DiscordId = discordId;
                GameId = gameId;
                KnownPlayers.Add(player2Id);
            }
        }
    }
}
