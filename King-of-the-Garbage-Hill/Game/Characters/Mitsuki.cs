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
                var playerIamAttacking = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation);
                
                var howMuchToAdd = player.Character.GetSkillMainOnly() switch
                {
                    10 => 10,
                    19 => 9,
                    27 => 8,
                    34 => 7,
                    40 => 6,
                    45 => 5,
                    49 => 4,
                    52 => 3,
                    54 => 2,
                    _ => 0
                };

                switch (player.Character.GetCurrentSkillTarget())
                {
                    case "Интеллект":
                        if (playerIamAttacking.Character.GetClassStatInt() == 0)
                        {
                            player.Character.AddExtraSkill(player.Status, "Много выебывается: ", howMuchToAdd*2, true );
                        }
                        break;
                    case "Сила":
                        if (playerIamAttacking.Character.GetClassStatInt() == 1)
                        {
                            player.Character.AddExtraSkill(player.Status, "Много выебывается: ", howMuchToAdd * 2, true);

                        }
                        break;
                    case "Скорость":
                        if (playerIamAttacking.Character.GetClassStatInt() == 2)
                        {
                            player.Character.AddExtraSkill(player.Status, "Много выебывается: ", howMuchToAdd * 2, true);
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