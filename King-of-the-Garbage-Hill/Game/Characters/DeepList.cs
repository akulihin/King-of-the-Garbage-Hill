using System;
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

        public DeepList(InGameGlobal gameGlobal, CharactersUniquePhrase phrase)
        {
            _gameGlobal = gameGlobal;
            _phrase = phrase;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public void HandleDeepList(GamePlayerBridgeClass player)
        {
        }

        public async Task HandleDeepListTactics(GamePlayerBridgeClass player, GameClass game)
        {
            //Doubtful tactic
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);


            if (!deep.FriendList.Contains(player.Status.IsFighting) && !game.PlayersList
                .Find(x => x.Status.PlayerId == player.Status.IsFighting).Status.IsBlock)
            {
                deep.FriendList.Add(player.Status.IsFighting);

                player.Status.IsAbleToWin = false;
                _phrase.DeepListDoubtfulTacticFirstLostPhrase.SendLog(player);
            }


            //end Doubtful tactic
            await Task.CompletedTask;
        }

        public void HandleDeepListAfter(GamePlayerBridgeClass player, GameClass game)
        {
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);


            player.Status.IsAbleToWin = true;
            if (deep.FriendList.Contains(player.Status.IsFighting))
                if (player.Status.IsWonThisCalculation != Guid.Empty)
                {
                    player.Status.AddRegularPoints();
                    _phrase.DeepListDoubtfulTacticPhrase.SendLog(player);
                }


            //end Doubtful tactic

            // Стёб
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var player2 = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);
                HandleMockery(player, player2, game);
            }

            //end Стёб
        }


        public void HandleMockery(GamePlayerBridgeClass player, GamePlayerBridgeClass player2, GameClass game)
        {
            //Стёб
            var currentDeepList =
                _gameGlobal.DeepListMockeryList.Find(x =>
                    x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

            if (currentDeepList != null)
            {
                var currentDeepList2 =
                    currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                if (currentDeepList2 != null)
                {
                    currentDeepList2.Times++;

                    if (currentDeepList2.Times % 2 != 0 && currentDeepList2.Times != 1)
                    {
                        player2.Character.AddPsyche(player2.Status, -1, true, "Стёб: ");
                        player2.MinusPsycheLog(game);
                        player.Status.AddRegularPoints();
                        _phrase.DeepListPokePhrase.SendLog(player);
                        if (player2.Character.GetPsyche() < 4) player2.Character.Justice.AddJusticeForNextRound(-1);
                    }
                }
                else
                {
                    var toAdd = new Mockery(new List<MockerySub> {new(player2.Status.PlayerId, 1)},
                        game.GameId, player.Status.PlayerId);
                    _gameGlobal.DeepListMockeryList.Add(toAdd);
                }
            }
            else
            {
                var toAdd = new Mockery(new List<MockerySub> {new(player2.Status.PlayerId, 1)},
                    game.GameId, player.Status.PlayerId);
                _gameGlobal.DeepListMockeryList.Add(toAdd);
            }

            //end Стёб
        }


        public class Mockery
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<MockerySub> WhoWonTimes;

            public Mockery(List<MockerySub> whoWonTimes, ulong gameId, Guid playerId)
            {
                WhoWonTimes = whoWonTimes;
                GameId = gameId;
                PlayerId = playerId;
            }
        }

        public class MockerySub
        {
            public Guid EnemyPlayerId;
            public int Times;

            public MockerySub(Guid enemyPlayerId, int times)
            {
                EnemyPlayerId = enemyPlayerId;
                Times = times;
            }
        }

        public class SuperMindKnown
        {
            public ulong GameId;
            public List<Guid> KnownPlayers = new();
            public Guid PlayerId;

            public SuperMindKnown(Guid playerId, ulong gameId, Guid player2Id)
            {
                PlayerId = playerId;
                GameId = gameId;
                KnownPlayers.Add(player2Id);
            }
        }

        public class Madness
        {
            public ulong GameId;
            public List<MadnessSub> MadnessList = new();
            public Guid PlayerId;
            public int RoundItTriggered;

            public Madness(Guid playerId, ulong gameId, int roundItTriggered)
            {
                PlayerId = playerId;
                GameId = gameId;
                RoundItTriggered = roundItTriggered;
            }
        }

        public class MadnessSub
        {
            public int Index;
            public int Intel;
            public int Psyche;
            public int Speed;
            public int Str;

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