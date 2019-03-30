using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class DeepList : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly CharactersUniquePhrase _phrase;
        public DeepList( InGameGlobal gameGlobal, CharactersUniquePhrase phrase)
        {
            _gameGlobal = gameGlobal;
            _phrase = phrase;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        
        public void HandleDeepList(GamePlayerBridgeClass player)
        {

        }

        public async Task HandleDeepListTactics(GamePlayerBridgeClass player)
        {
            //Doubtful tactic
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerDiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

            if (deep == null)
            {
                _gameGlobal.DeepListDoubtfulTactic.Add(new Sirinoks.FriendsClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId, player.Status.IsFighting));
              
                player.Status.IsAbleToWin = false;
     
            }
            else
            {
                if (deep.FriendList.Contains(player.Status.IsFighting))
                { 
                  
                }
                else
                {
                    deep.FriendList.Add(player.Status.IsFighting);
                   
                    player.Status.IsAbleToWin = false;

                }

            }


            //end Doubtful tactic
            await Task.CompletedTask;
        }

        public async Task HandleDeepListAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerDiscordId == player.DiscordAccount.DiscordId && player.DiscordAccount.GameId == x.GameId);

            if (deep != null)
            {
                player.Status.IsAbleToWin = true;
                if (deep.FriendList.Contains(player.Status.IsFighting))
                {
                  
                    if (player.Status.IsWonLastTime != 0)
                    {
                        player.Status.AddRegularPoints();
                        await _phrase.DeepListDoubtfulTacticPhrase.SendLog(player);
                    }
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

             
        public void HandleMockery(GamePlayerBridgeClass player, GamePlayerBridgeClass player2, GameClass game)
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
                        player2.Character.AddPsyche(player2.Status, -1);
                        player2.MinusPsycheLog(game);

                        player.Status.AddRegularPoints();
                        if (player2.Character.GetPsyche() < 4)
                        {
                            player2.Character.Justice.AddJusticeForNextRound(-1);
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

        public class Madness
        {
            public ulong DiscordId;
            public ulong GameId;
            public int RoundItTriggered;
            public List<MadnessSub> MadnessList = new List<MadnessSub>();
            public Madness(ulong discordId, ulong gameId, int roundItTriggered)
            {
                DiscordId = discordId;
                GameId = gameId;
                RoundItTriggered = roundItTriggered;
            }

            public Madness()
            {

            }
        }

        public class MadnessSub
        {
            public int Index;
            public int Intel;
            public int Str;
            public int Speed;
            public int Psyche;

            public MadnessSub(int index, int intel, int str, int speed, int psyche)
            {
                Index = index;
                Intel = intel;
                Str = str;
                Speed = speed;
                Psyche = psyche;
            }
        }


    }
}
