using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mitsuki : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

 

        public void HandleMitsukiAfter(GamePlayerBridgeClass player, GameClass game)
        {

            if (player.Status.WhoToAttackThisTurn != Guid.Empty && player.Status.WhoToAttackThisTurn == player.Status.IsWonThisCalculation)
            {
                var playerIamAttacking =
                    game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);
                switch (player.Character.GetCurrentSkillTarget())
                {
                    case "Интеллект":
                        if (playerIamAttacking.Character.GetClassStatInt() == 0)
                        {
                            player.Character.AddMainSkill(player.Status, "Много выебывается: ", true, true );
                        }
                        break;
                    case "Сила":
                        if (playerIamAttacking.Character.GetClassStatInt() == 1)
                        {
                            player.Character.AddMainSkill(player.Status, "Много выебывается: ", true, true);

                        }
                        break;
                    case "Скорость":
                        if (playerIamAttacking.Character.GetClassStatInt() == 2)
                        {
                            player.Character.AddMainSkill(player.Status, "Много выебывается: ", true, true);
                        }
                        break;
                }
            }

        }

        public class GarbageClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<GarbageSubClass> Training = new();

            public GarbageClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Training.Add(new GarbageSubClass(enemyId));
            }
        }

        public class GarbageSubClass
        {
            public Guid EnemyId;
            public int Times;

            public GarbageSubClass(Guid enemyId)
            {
                EnemyId = enemyId;
                Times = 1;
            }
        }
    }
}