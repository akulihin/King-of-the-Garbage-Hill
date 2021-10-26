using System;
using System.Collections.Generic;
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


        public void HandleVampyrAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Гематофагия

            var vampyr = _gameGlobal.VampyrHematophagiaList.Find(x => x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);
            
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var target = vampyr.Hematophagia.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);
                if (target == null)
                {
                    var statIndex = 0;

                    var found = false;
                    while (!found)
                    {
                        statIndex = _rand.Random(1, 4);
                        switch (statIndex)
                        {
                            case 1:
                                if (player.Character.GetIntelligence() < 10)
                                {
                                    player.Character.AddIntelligence(player.Status, 2, "Гематофагия: ");
                                    found = true;
                                }
                                break;
                            case 2:
                                if (player.Character.GetStrength() < 10)
                                {
                                    player.Character.AddStrength(player.Status, 2, "Гематофагия: ");
                                    found = true;
                                }
                                break;
                            case 3:
                                if (player.Character.GetSpeed() < 10)
                                {
                                    player.Character.AddSpeed(player.Status, 2, "Гематофагия: ");
                                    found = true;
                                }
                                break;
                            case 4:
                                if (player.Character.GetPsyche() < 10)
                                {
                                    player.Character.AddPsyche(player.Status, 2, "Гематофагия: ");
                                    found = true;
                                }
                                break;
                        }
                    }

                    vampyr.Hematophagia.Add(new HematophagiaSubClass(statIndex, player.Status.IsWonThisCalculation));
                }
            }
            
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var target = vampyr.Hematophagia.Find(x => x.EnemyId == player.Status.IsLostThisCalculation);

                if (target != null)
                {
                    vampyr.Hematophagia.Remove(target);
                    switch (target.StatIndex)
                    {
                        case 1:
                            player.Character.AddIntelligence(player.Status, -2, "СОсиновый кол: ");
                            player.Status.AddRegularPoints(-1, "СОсиновый кол: ", true);
                            break;
                        case 2:
                            player.Character.AddStrength(player.Status, -2, "СОсиновый кол: ");
                            player.Status.AddRegularPoints(-1, "СОсиновый кол", true);
                            break;
                        case 3:
                            player.Character.AddSpeed(player.Status, -2, "СОсиновый кол: ");
                            player.Status.AddRegularPoints(-1, "СОсиновый кол", true);
                            break;
                        case 4:
                            player.Character.AddPsyche(player.Status, -2, "СОсиновый кол: ");
                            player.Status.AddRegularPoints(-1, "СОсиновый кол", true);
                            break;
                    }

                }
                else
                {
                    if (vampyr.Hematophagia.Count > 0)
                    {
                        var randomIndex = _rand.Random(0, vampyr.Hematophagia.Count - 1);
                        target = vampyr.Hematophagia[randomIndex];
                        vampyr.Hematophagia.Remove(target);
                        switch (target.StatIndex)
                        {
                            case 1:
                                player.Character.AddIntelligence(player.Status, -2, "Гематофагия: ");
                                break;
                            case 2:
                                player.Character.AddStrength(player.Status, -2, "Гематофагия: ");
                                break;
                            case 3:
                                player.Character.AddSpeed(player.Status, -2, "Гематофагия: ");
                                break;
                            case 4:
                                player.Character.AddPsyche(player.Status, -2, "Гематофагия: ");
                                break;
                        }

                    }
                }

            }
            //end Гематофагия



        }

        public class ScavengerClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public Guid EnemyId = Guid.Empty;
            public int EnemyJustice = 0;

            public ScavengerClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }



        public class HematophagiaClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<HematophagiaSubClass> Hematophagia = new();

            public HematophagiaClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class HematophagiaSubClass
        {
            public int StatIndex;
      
            public Guid EnemyId;


            public HematophagiaSubClass(int statIndex, Guid enemyId)
            {
                StatIndex = statIndex;
                EnemyId = enemyId;
            }
        }
    }
}