using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class BotsBehavior : IServiceSingleton
    {
        private readonly GameReaction _gameReaction;
        private readonly SecureRandom _rand;

        public BotsBehavior(SecureRandom rand, GameReaction gameReaction)
        {
            _rand = rand;
            _gameReaction = gameReaction;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task HandleBotBehavior(GameBridgeClass player, GameClass game)
        {
            if (!player.IsBot() || player.Status.IsReady && player.Status.MoveListPage != 3) return;
            if (player.Status.MoveListPage == 1)
            {
                int randomPlayer;

                var playerToAttack =
                    game.PlayersList.FindAll(x =>
                        x.Character.Justice.GetJusticeNow() == player.Character.Justice.GetJusticeNow() &&
                        x.Character.Name != player.Character.Name);

                var randomCheck = _rand.Random(0, playerToAttack.Count);

                if (playerToAttack.Count >= 1 && randomCheck - playerToAttack.Count <= 0)
                {
                    randomPlayer = _rand.Random(0, playerToAttack.Count - 1);
                    var userToAttack = playerToAttack[randomPlayer];
                    await _gameReaction.HandleAttackOrLvlUp(player, null, userToAttack.Status.PlaceAtLeaderBoard);
                    return;
                }

                if (playerToAttack.Count >= 1 && randomCheck - playerToAttack.Count > 0)
                {
                    await _gameReaction.HandleAttackOrLvlUp(player, null, -10);
                    return;
                }

                do
                {
                    randomPlayer = _rand.Random(1, 6);
                } while (randomPlayer == player.Status.PlaceAtLeaderBoard);

                await _gameReaction.HandleAttackOrLvlUp(player, null, randomPlayer);
            }

            if (player.Status.MoveListPage == 3)
            {
                var skillNu = 1;

                var intel = player.Character.GetIntelligence();
                var str = player.Character.GetStrength();
                var speed = player.Character.GetSpeed();
                var psy = player.Character.GetPsyche();

                var stats = new List<BiggestStatClass>
                {
                    new BiggestStatClass(1, intel),
                    new BiggestStatClass(2, str),
                    new BiggestStatClass(3, speed) ,
                     new BiggestStatClass(4, psy)
                };

                stats = stats.OrderByDescending(x => x.StatCount).ToList();

                if (stats[1].StatCount < 7)
                {
                    skillNu = stats[1].StatIndex;
                }
                else if (stats[0].StatCount < 10)
                {
                    skillNu = stats[0].StatIndex;
                }
                else if (stats[1].StatCount < 10)
                {
                    skillNu = stats[1].StatIndex;
                }
                else if (stats[2].StatCount < 10)
                {
                    skillNu = stats[2].StatIndex;
                }
                else if (stats[3].StatCount < 10)
                {
                    skillNu = stats[3].StatIndex;
                }
                else
                {
                    player.Status.MoveListPage = 1;
                    return;
                }

                if (player.Character.Name == "LeCrisp" && str < 10) skillNu = 2;
                if (player.Character.Name == "Даркси" && psy < 10) skillNu = 4;
                if (player.Character.Name == "Тигр" && psy < 10 && game.RoundNo <= 6) skillNu = 4;

                await _gameReaction.HandleAttackOrLvlUp(player, null, skillNu);
                player.Status.MoveListPage = 1;
            }
        }

        public class BiggestStatClass
        {
            public int StatCount;
            public int StatIndex;

            public BiggestStatClass(int statIndex, int statCount)
            {
                StatIndex = statIndex;
                StatCount = statCount;
            }
        }
    }
}