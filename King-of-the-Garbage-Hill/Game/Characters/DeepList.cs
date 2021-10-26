using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class DeepList : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        

        public DeepList(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }




        public async Task HandleDeepListTactics(GamePlayerBridgeClass player, GameClass game)
        {
            //Сомнительная тактика
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

            if (deep != null)
            {
                if (!deep.FriendList.Contains(player.Status.IsFighting))
                {
                    player.Status.IsAbleToWin = false;
                }
            }



            //end Сомнительная тактика
            await Task.CompletedTask;
        }

        public void HandleDeepListAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Сомнительная тактика
            var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                x.PlayerId == player.Status.PlayerId && player.GameId == x.GameId);

            

            if (deep != null)
            {
                if (!deep.FriendList.Contains(player.Status.IsFighting) && player.Status.IsLostThisCalculation == player.Status.IsFighting)
                {
                    player.Status.IsAbleToWin = true;
                    deep.FriendList.Add(player.Status.IsFighting);
                    game.Phrases.DeepListDoubtfulTacticFirstLostPhrase.SendLog(player, false);
                }
            }

            if (deep != null)
            {
                if (deep.FriendList.Contains(player.Status.IsFighting))
                {
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        player.Status.AddRegularPoints(1, "Сомнительная тактика");
                        game.Phrases.DeepListDoubtfulTacticPhrase.SendLog(player, false);
                    }
                }
            }
            //end Сомнительная тактика

            // Стёб
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var player2 = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);
                HandleMockery(player, player2, game);
            }

            //end Стёб
        }


        public void HandleMockery(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
        {
            //Стёб
            var currentDeepList = _gameGlobal.DeepListMockeryList.Find(x => x.PlayerId == me.Status.PlayerId && game.GameId == x.GameId);

            if (currentDeepList != null)
            {
                var currentDeepList2 = currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == target.Status.PlayerId);

                if (currentDeepList2 != null)
                {
                    currentDeepList2.Times++;

                    if (currentDeepList2.Times == 2)
                    {

                        
                        target.Character.AddPsyche(target.Status, -1, "Стёб: ");
                        target.MinusPsycheLog(game);
                        me.Status.AddRegularPoints(1, "Стёб");
                        game.Phrases.DeepListPokePhrase.SendLog(me, true);
                        if (target.Character.GetPsyche() < 4)
                            if (target.Character.Justice.GetJusticeNow() > 0)
                                target.Character.Justice.AddJusticeForNextRound(-1);
                    }
                }
                else
                {
                    currentDeepList.WhoWonTimes.Add(new MockerySub(target.Status.PlayerId, 1));
                }
            }
            else
            {
                var toAdd = new Mockery(new List<MockerySub> {new(target.Status.PlayerId, 1)}, game.GameId, me.Status.PlayerId);
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
            public bool Triggered;

            public MockerySub(Guid enemyPlayerId, int times)
            {
                EnemyPlayerId = enemyPlayerId;
                Times = times;
                Triggered = false;
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