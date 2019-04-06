using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class BotsBehavior : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        private readonly GameReaction _gameReaction;
        private readonly SecureRandom _rand;

        public BotsBehavior(SecureRandom rand, GameReaction gameReaction, InGameGlobal gameGlobal)
        {
            _rand = rand;
            _gameReaction = gameReaction;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task HandleBotBehavior(GamePlayerBridgeClass player, GameClass game)
        {
            if (!player.IsBot() || player.Status.IsReady && player.Status.MoveListPage != 3) return;
            if (player.Status.MoveListPage == 1) await HandleAttack(player, game);

            if (player.Status.MoveListPage == 3) await HandleLvlUp(player, game);
        }

        public async Task HandleAttack(GamePlayerBridgeClass bot, GameClass game)
        {

            var nanobots = _gameGlobal.NanobotsList.Find(x => x.GameId == game.GameId);
            var sum = 0;
            var isBlock = 6;

            foreach (var p in nanobots.Nanobots)
            {
                if (bot.Status.PlayerId == p.Player.Status.PlayerId) p.Ten = 0;

                if (bot.Character.Justice.GetJusticeNow() == p.Player.Character.Justice.GetJusticeNow())
                    p.Ten -= 5;
                else if (bot.Character.Justice.GetJusticeNow() < p.Player.Character.Justice.GetJusticeNow())
                    p.Ten -= 6;

                if (bot.Status.PlaceAtLeaderBoard < p.Player.Status.PlaceAtLeaderBoard)
                    p.Ten -= 2;

                if (bot.Status.PlaceAtLeaderBoard - p.Player.Status.PlaceAtLeaderBoard == 1)
                    p.Ten -= 1;

                if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId && x.IsTooGood))
                    p.Ten -= 7;
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == p.Player.Status.PlayerId))
                    p.Ten -= 5;


                if (p.Player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGood)) p.Ten += 3;


                var count = game.PlayersList.FindAll(x => x.Status.WhoToAttackThisTurn == p.Player.Status.PlayerId)
                    .Count;
                p.Ten -= count;

                if (p.Ten <= 0)
                {
                    isBlock--;
                    p.Ten = 0;
                }
                  
                sum += p.Ten;
            }

         var isBlockCheck   = _rand.Random(1, 5);
            if (isBlockCheck > isBlock)
            {
                //block
                await _gameReaction.HandleAttackOrLvlUp(bot, null, -10);
                ResetTens(nanobots);
                return;
            }

            var rand = _rand.Random(1, sum);


            var a = nanobots.Nanobots.Find(x => x.Index == 1);
            var b = nanobots.Nanobots.Find(x => x.Index == 2);
            var c = nanobots.Nanobots.Find(x => x.Index == 3);
            var d = nanobots.Nanobots.Find(x => x.Index == 4);
            var e = nanobots.Nanobots.Find(x => x.Index == 5);
            var f = nanobots.Nanobots.Find(x => x.Index == 6);

            var whoToAttack = 0;

            if (rand <= a.Ten)
                whoToAttack = a.Player.Status.PlaceAtLeaderBoard;
            else if (rand <= a.Ten + b.Ten)
                whoToAttack = b.Player.Status.PlaceAtLeaderBoard;
            else if (rand <= a.Ten + b.Ten + c.Ten)
                whoToAttack = c.Player.Status.PlaceAtLeaderBoard;
            else if (rand <= a.Ten + b.Ten + c.Ten + d.Ten)
                whoToAttack = d.Player.Status.PlaceAtLeaderBoard;
            else if (rand <= a.Ten + b.Ten + c.Ten + d.Ten + e.Ten)
                whoToAttack = e.Player.Status.PlaceAtLeaderBoard;
            else if (rand <= a.Ten + b.Ten + c.Ten + d.Ten + e.Ten + f.Ten)
                whoToAttack = f.Player.Status.PlaceAtLeaderBoard;


            if (whoToAttack == 0)
                await _gameReaction.HandleAttackOrLvlUp(bot, null, -10);
            else
                await _gameReaction.HandleAttackOrLvlUp(bot, null, whoToAttack);

            ResetTens(nanobots);
            /*
сли random <= 
то a = цель
else если random <=a+b
то b = цель
         */
        }

        public void ResetTens(NanobotClass nanobots)
        {
            foreach (var p in nanobots.Nanobots) p.Ten = 10;
        }


        public async Task HandleLvlUp(GamePlayerBridgeClass player, GameClass game)
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
                new BiggestStatClass(3, speed),
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

        public class Nanobot
        {
            public int Index;
            public GamePlayerBridgeClass Player;
            public List<ulong> PlayersHeLostToLastRound = new List<ulong>();
            public int Ten;


            public Nanobot(GamePlayerBridgeClass player)
            {
                Player = player;
                Ten = 10;
                Index = player.Status.PlaceAtLeaderBoard;
            }
        }

        public class NanobotClass
        {
            public ulong GameId;
            public List<Nanobot> Nanobots = new List<Nanobot>();

            public NanobotClass(IReadOnlyList<GamePlayerBridgeClass> players)
            {
                GameId = players[0].DiscordAccount.GameId;
                foreach (var t in players) Nanobots.Add(new Nanobot(t));
            }
        }
    }
}