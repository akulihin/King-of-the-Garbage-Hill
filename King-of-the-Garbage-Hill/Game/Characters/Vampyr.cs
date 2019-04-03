using System;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Vampyr : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly SecureRandom _rand;

        public Vampyr(InGameGlobal gameGlobal, SecureRandom rand)
        {
            _gameGlobal = gameGlobal;
            _rand = rand;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleVampyr(GamePlayerBridgeClass player)
        {
            //    throw new System.NotImplementedException();
        }

        public void HandleVampyrAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Падальщик
            var enemy = game.PlayersList.Find(x =>
                x.Status.PlayerId == player.Status.WhoToAttackThisTurn);

            if (enemy != null)
                if (enemy.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                    enemy.Character.Justice.SetJusticeNow(enemy.Character.Justice.GetJusticeNow() + 1);
            //end Падальщик

            //Гематофагия
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var vamp = _gameGlobal.VampyrKilledList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                if (!vamp.FriendList.Contains(player.Status.IsWonThisCalculation))
                {
                    vamp.FriendList.Add(player.Status.IsWonThisCalculation);
                    player.Status.AddBonusPoints();

                    for (var i = 0; i < 2; i++)
                    {
                        var index = _rand.Random(1, 4);

                        switch (index)
                        {
                            case 1:
                                var intel = player.Character.GetIntelligence();
                                if (intel >= 10)
                                {
                                    i--;
                                    continue;
                                }

                                player.Character.AddIntelligence(player.Status);
                                break;
                            case 2:
                                intel = player.Character.GetStrength();
                                if (intel >= 10)
                                {
                                    i--;
                                    continue;
                                }

                                player.Character.AddStrength(player.Status);
                                break;
                            case 3:
                                intel = player.Character.GetSpeed();
                                if (intel >= 10)
                                {
                                    i--;
                                    continue;
                                }

                                player.Character.AddSpeed(player.Status);
                                break;
                            case 4:
                                intel = player.Character.GetPsyche();
                                if (intel >= 10)
                                {
                                    i--;
                                    continue;
                                }

                                player.Character.AddPsyche(player.Status);
                                break;
                        }
                    }
                }
            }
            //end Гематофагия


            //Осиновый кол
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                player.Status.AddBonusPoints(-1);

                for (var i = 0; i < 1; i++)
                {
                    var index = _rand.Random(1, 4);

                    switch (index)
                    {
                        case 1:
                            var intel = player.Character.GetIntelligence();
                            if (intel <= 0)
                            {
                                i--;
                                continue;
                            }

                            player.Character.AddIntelligence(player.Status, -1);
                            break;
                        case 2:
                            intel = player.Character.GetStrength();
                            if (intel <= 0)
                            {
                                i--;
                                continue;
                            }

                            player.Character.AddStrength(player.Status, -1);
                            break;
                        case 3:
                            intel = player.Character.GetSpeed();
                            if (intel <= 0)
                            {
                                i--;
                                continue;
                            }

                            player.Character.AddSpeed(player.Status, -1);
                            break;
                        case 4:
                            intel = player.Character.GetPsyche();
                            if (intel <= 0)
                            {
                                i--;
                                continue;
                            }

                            player.Character.AddPsyche(player.Status, -1);
                            break;
                    }
                }
            }

            //end Осиновый кол
        }
    }
}