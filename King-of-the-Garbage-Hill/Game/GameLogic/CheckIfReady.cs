using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CheckIfReady : IServiceSingleton
    {
        private readonly Global _global;
        private readonly CalculateStage2 _stage2;
        private readonly GameUpdateMess _upd;
        private readonly GameReaction _gameReaction;
        private readonly SecureRandom _rand;
        public Timer LoopingTimer;


        public CheckIfReady(Global global, GameUpdateMess upd, CalculateStage2 stage2, GameReaction gameReaction, SecureRandom rand)
        {
            _global = global;
            _upd = upd;
            _stage2 = stage2;
            _gameReaction = gameReaction;
            _rand = rand;
            CheckTimer();
        }


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public Task CheckTimer()
        {
            LoopingTimer = new Timer
            {
                AutoReset = true,
                Interval = 5000,
                Enabled = true
            };

            LoopingTimer.Elapsed += CheckIfEveryoneIsReady;


            return Task.CompletedTask;
        }

        private async void CheckIfEveryoneIsReady(object sender, ElapsedEventArgs e)
        {
            var games = _global.GamesList;
            for (var i = 0; i < games.Count; i++)
            {
                var game = games[i];
                var isTimerToCheckEnabled = _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                    .IsTimerToCheckEnabled;
                if (!isTimerToCheckEnabled) return;

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;

                for (var k = 0; k < players.Count; k++)
                {
                 await HandleBotBehavior(players[k], game);

                    if (players[k].Status.IsReady) readyCount++;

                    if (players[k].Status.SocketMessageFromBot != null)
                        if (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds >= -6)
                            await _upd.UpdateMessage(players[k]);
                }

                if ((readyCount == readyTargetCount ||
                     game.TimePassed.Elapsed.TotalSeconds >= game.TurnLengthInSecond) && game.GameStatus == 1)
                {
                    _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                        .IsTimerToCheckEnabled = false;


                    for (var k = 0; k < players.Count; k++)
                        if (players[k].Status.SocketMessageFromBot != null)
                            await _upd.UpdateMessage(players[k]);


                    await _stage2.DeepListMind(game);

                    _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                        .IsTimerToCheckEnabled = true;
                }
            }
        }

        /*
1) нападает на случайного врага, у которого меньше справедливости, чем у него.  (если нет таких, то на того, у кого равная справедливость с ним)

2) если у нескольких игроков справедливости больше, а меньше нет ни у кого, дальше идет такая формула:
ролишь рандом от 1 до 5
rand = 3
если (rand)  -   (кол-во врагов с равной справедливостью)  < 0
то атакует рандомного врага с равной справедливостью
если (rand)  -   (кол-во врагов с равной справедливостью)  >= 0
то ставит блок
        */
        private async Task HandleBotBehavior(GameBridgeClass player, GameClass game)
        {
            if (!player.IsBot()) return;
            if (player.Status.MoveListPage == 1)
            {
                int randomPlayer;
                var playerToAttack =
                    game.PlayersList.FindAll(x => x.Character.Justice.JusticeNow == player.Character.Justice.JusticeNow && x.Character.Name != player.Character.Name);

                var randomCheck = _rand.Random(0, playerToAttack.Count);
                
                if (playerToAttack.Count >= 1 && randomCheck - playerToAttack.Count <= 0)
                {
                    randomPlayer = _rand.Random(0, playerToAttack.Count-1);
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
                } while (randomPlayer ==  player.Status.PlaceAtLeaderBoard);

                await _gameReaction.HandleAttackOrLvlUp(player, null, randomPlayer);
            }

            if (player.Status.MoveListPage == 3)
            {
                if(player.Character.Intelligence < 10)
                    await _gameReaction.HandleAttackOrLvlUp(player, null, 1);
                else if(player.Character.Strength < 10)
                    await _gameReaction.HandleAttackOrLvlUp(player, null, 2);
                else if(player.Character.Speed < 10)
                    await _gameReaction.HandleAttackOrLvlUp(player, null, 3);
                else if(player.Character.Psyche < 10)
                    await _gameReaction.HandleAttackOrLvlUp(player, null, 4);
            }
            
        }

        public class IsTimerToCheckEnabledClass
        {
            public ulong GameId;
            public bool IsTimerToCheckEnabled;

            public IsTimerToCheckEnabledClass(ulong gameId)
            {
                IsTimerToCheckEnabled = true;
                GameId = gameId;
            }
        }
    }
}