using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.FinishedGameLog;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CheckIfReady : IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly BotsBehavior _botsBehavior;
    private readonly FinishedGameLog _finishedGameLog;
    private readonly InGameGlobal _gameGlobal;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly Global _global;
    private readonly HelperFunctions _help;
    private readonly Logs _logs;
    private readonly SecureRandom _random;
    private readonly CalculateRound _round;
    private readonly GameUpdateMess _upd;
    private bool _looping;
    public Timer LoopingTimer;

    public CheckIfReady(Global global, GameUpdateMess upd, CalculateRound round, FinishedGameLog finishedGameLog,
        GameUpdateMess gameUpdateMess, BotsBehavior botsBehavior, Logs logs, UserAccounts accounts,
        InGameGlobal gameGlobal, HelperFunctions help, SecureRandom random)
    {
        _global = global;
        _upd = upd;
        _round = round;
        _finishedGameLog = finishedGameLog;
        _gameUpdateMess = gameUpdateMess;
        _botsBehavior = botsBehavior;
        _logs = logs;
        _accounts = accounts;
        _gameGlobal = gameGlobal;
        _help = help;
        _random = random;
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
            Interval = 100,
            Enabled = true
        };

        LoopingTimer.Elapsed += CheckIfEveryoneIsReady;
        return Task.CompletedTask;
    }

    private void HandlePostGameEvents(GameClass game)
    {
        var playerWhoWon = game.PlayersList[0];

        //if won phrases
        switch (playerWhoWon.Character.Name)
        {
            case "HardKitty":
                game.AddGlobalLogs("HarDKitty больше не одинок! Как много друзей!!!");

                var hard = _gameGlobal.HardKittyLoneliness.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == playerWhoWon.Status.PlayerId);

                if (hard != null)
                    foreach (var enemy in game.PlayersList)
                    {
                        var hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == enemy.Status.PlayerId);
                        if (hardEnemy != null)
                            game.PlayersList.Find(x => x.Status.PlayerId == hardEnemy.EnemyId).Status
                                .AddInGamePersonalLogs(
                                    $"HarDKitty больше не одинок! Вы принесли ему {hardEnemy.Times} очков.\n");
                    }

                break;
        }

        //if lost phrases
        foreach (var player in game.PlayersList.Where(x => x.Status.PlaceAtLeaderBoard != 1))
            switch (player.Character.Name)
            {
                case "HardKitty":
                    player.Status.AddInGamePersonalLogs("Даже имя мое написать нормально не можете");
                    break;
                case "Mit*suki*":
                    player.Status.AddInGamePersonalLogs("Блять, суки, че вы меня таким слабым сделали?");
                    break;
                case "Тигр":
                    player.Status.AddInGamePersonalLogs("Обоссанная игра, обоссанный баланс");
                    break;
            }

        //unique
        if (game.PlayersList.Any(x => x.Character.Name == "DeepList") &&
            game.PlayersList.Any(x => x.Character.Name == "mylorik"))
        {
            var mylorik = game.PlayersList.Find(x => x.Character.Name == "mylorik");
            var deepList = game.PlayersList.Find(x => x.Character.Name == "DeepList");

            foreach (var deepListPredict in deepList.Predict)
                if (mylorik.Predict.Any(x =>
                        x.PlayerId == deepListPredict.PlayerId && x.CharacterName == deepListPredict.CharacterName))
                    game.AddGlobalLogs(
                        "DeepList & mylorik: Гении мыслят одинакого или одно целое уничтожает воду.");
        }

        if (playerWhoWon.Status.PlaceAtLeaderBoardHistory.Find(x => x.GameRound == 10).Place != 1)
            game.AddGlobalLogs($"**{playerWhoWon.DiscordUsername}** вырывает **очко** на последних секундах!");

        
    }


    private async Task HandleLastRound(GameClass game)
    {
        game.IsCheckIfReady = false;
        //predict
        foreach (var player in from player in game.PlayersList
                 from predict in player.Predict
                 let enemy = game.PlayersList.Find(x => x.Status.PlayerId == predict.PlayerId)
                 where enemy.Character.Name == predict.CharacterName
                 select player) player.Status.AddBonusPoints(3, "Предположение: ");
        // predict


        //predict bot
        foreach (var bot in game.PlayersList)
        {
            if (!bot.IsBot()) continue;

            if (game.GetAllGlobalLogs().Contains("Толя запизделся"))
                bot.Status.AddBonusPoints(3, "Предположение: ");

            if (bot.Character.Name == "AWDKA") bot.Status.AddBonusPoints(6, "Предположение: ");

            if (game.PlayersList.All(x => _accounts.GetAccount(x.DiscordId).TotalPlays >= 50))
            {
                bot.Status.AddBonusPoints(3, "Предположение: ");
                //if (bot.Character.Name == "DeepList") bot.Status.AddBonusPoints(3, "Предположение: ");
            }
        }
        //end bot

        //sort
        game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        for (var k = 0; k < game.PlayersList.Count; k++)
            game.PlayersList[k].Status.PlaceAtLeaderBoard = k + 1;
        //end sorting


        try
        {
            //case "AWDKA":
            var AWDKA = game.PlayersList.Find(x => x.Character.Name == "AWDKA");
            //trolling
            if (AWDKA != null)
            {
                var awdkaTroll = _gameGlobal.AwdkaTrollingList.Find(x =>
                    x.GameId == AWDKA.GameId &&
                    x.PlayerId == AWDKA.Status.PlayerId);


                var enemy = awdkaTroll.EnemyList.Find(x =>
                    x.EnemyId == game.PlayersList.Find(y => y.Status.PlaceAtLeaderBoard == 1).Status.PlayerId);

                var trolledText = "";
                if (enemy != null)
                {
                    var tolled = game.PlayersList.Find(x => x.Status.PlayerId == enemy.EnemyId);

                    trolledText = tolled.Character.Name switch
                    {
                        "DeepList" => "Лист Затроллился, хех",
                        "mylorik" => "Лорик Затроллился, МММ!",
                        "Глеб" => "Спящее Хуйло",
                        "LeCrisp" => "ЛеПуська Затроллилась",
                        "Толя" => "Раммус Продал Тормейл",
                        "HardKitty" => "Пакет Молока Пролился На Клавиатуру",
                        "Sirinoks" => "Айсик Затроллилась#",
                        "Mit*suki*" => "МитСУКИ Затроллился",
                        "AWDKA" => "AWDKA Затроллился сам по себе...",
                        "Осьминожка" => "Осьминожка Забулькался",
                        "Darksci" => "Даркси Не Повезло...",
                        "Братишка" => "Братишка Забулькался",
                        "Загадочный Спартанец в маске" => "Спатанец Затроллился!? А-я-йо...",
                        "Вампур" => "ВампYр Затроллился",
                        "Тигр" => "Тигр Обоссался, и кто теперь обоссан!?",
                        "Краборак" => "За**Краборак**чился",
                        _ => ""
                    };

                    AWDKA.Status.AddBonusPoints((enemy.Score + 1) / 2, $"**Произошел Троллинг:** {trolledText} ");
                    game.Phrases.AwdkaTrolling.SendLog(AWDKA, true);
                }

                //sort
                game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                for (var k = 0; k < game.PlayersList.Count; k++)
                    game.PlayersList[k].Status.PlaceAtLeaderBoard = k + 1;
                //end sorting

                if (enemy != null && game.PlayersList[0].Character.Name == "AWDKA")
                    game.AddGlobalLogs($"**Произошел Троллинг:** {trolledText} ");
            }
            //end //trolling
        }

        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }


        for (var k = 0; k < game.PlayersList.Count; k++)
            game.PlayersList[k].Status.PlaceAtLeaderBoardHistory.Add(
                new InGameStatus.PlaceAtLeaderBoardHistoryClass(game.RoundNo,
                    game.PlayersList[k].Status.PlaceAtLeaderBoard));

        game.WhoWon = game.PlayersList[0].Status.PlayerId;
        HandlePostGameEvents(game);

        game.AddGlobalLogs(
            game.PlayersList.FindAll(x => x.Status.GetScore() == game.PlayersList[0].Status.GetScore())
                .Count > 1
                ? "\n**Ничья**"
                : $"\n**{game.PlayersList[0].DiscordUsername}** победил, играя за **{game.PlayersList[0].Character.Name}**");


        _finishedGameLog.CreateNewLog(game);


        foreach (var player in game.PlayersList)
        {
            await _gameUpdateMess.UpdateMessage(player);

            var account = _accounts.GetAccount(player.DiscordId);
            account.IsPlaying = false;
            player.GameId = 1000000;


            account.TotalPlays++;
            if (account.TotalPlays > 10) account.IsNewPlayer = false;
            account.TotalWins += player.Status.PlaceAtLeaderBoard == 1 ? 1 : (ulong)0;
            account.MatchHistory.Add(
                new DiscordAccountClass.MatchHistoryClass(player.Character.Name, player.Status.GetScore(),
                    player.Status.PlaceAtLeaderBoard));

            /*
            account.ZbsPoints += (player.Status.PlaceAtLeaderBoard - 6) * -1 + 1;
            if (player.Status.PlaceAtLeaderBoard == 1)
                account.ZbsPoints += 4;
            */

            var zbsPointsToGive = 0;
            switch (player.Status.PlaceAtLeaderBoard)
            {
                case 1:
                    zbsPointsToGive = 100;
                    break;
                case 2:
                    zbsPointsToGive = 50;
                    break;
                case 3:
                    zbsPointsToGive = 40;
                    break;
                case 4:
                    zbsPointsToGive = 30;
                    break;
                case 5:
                    zbsPointsToGive = 20;
                    break;
                case 6:
                    zbsPointsToGive = 10;
                    break;
            }

            account.ZbsPoints += zbsPointsToGive;

            var characterStatistics =
                account.CharacterStatistics.Find(x =>
                    x.CharacterName == player.Character.Name);

            if (characterStatistics == null)
            {
                account.CharacterStatistics.Add(
                    new DiscordAccountClass.CharacterStatisticsClass(player.Character.Name,
                        player.Status.PlaceAtLeaderBoard == 1 ? 1 : (ulong)0));
            }
            else
            {
                characterStatistics.Plays++;
                characterStatistics.Wins += player.Status.PlaceAtLeaderBoard == 1 ? 1 : (ulong)0;
            }

            var performanceStatistics =
                account.PerformanceStatistics.Find(x =>
                    x.Place == player.Status.PlaceAtLeaderBoard);

            if (performanceStatistics == null)
                account.PerformanceStatistics.Add(
                    new DiscordAccountClass.PerformanceStatisticsClass(player.Status.PlaceAtLeaderBoard));
            else
                performanceStatistics.Times++;
            try
            {
                if (!player.IsBot())
                    await player.Status.SocketMessageFromBot.Channel.SendMessageAsync($"Спасибо за игру!\nВы заработали **{zbsPointsToGive}** ZBS points!\n\nВы можете потратить их в магазине - `*store`\nА вы знали? Это многопользовательская игра до 6 игроков! Вы можете начать игру с другом пинганв его! Например `*st @Boole`");
            }
            catch (Exception exception)
            {
                _logs.Critical(exception.Message);
                _logs.Critical(exception.StackTrace);
            }
        }

        await NotifyOwner(game);
        _global.GamesList.Remove(game);
    }

    private async Task NotifyOwner(GameClass game)
    {
        _logs.Error(game.GameId);
        foreach (var player in game.PlayersList)
        {
            _global.WinRates.TryGetValue(player.Character.Name, out var winrate);

            
            if (winrate == null)
            {
                var value = new Global.WinRateClass(0, 0);
                _global.WinRates.AddOrUpdate(player.Character.Name, value, (_, _) => value);
            }

            _global.WinRates.TryGetValue(player.Character.Name, out winrate);
            winrate.GameTimes++;
            if (player.Status.PlaceAtLeaderBoard == 1)
            {
                winrate.WinTimes++;
            }
            winrate.WinRate = (double)winrate.WinTimes / winrate.GameTimes * 100;
            winrate.CharacterName = player.Character.Name;
            _global.WinRates.AddOrUpdate(player.Character.Name, winrate, (_, _) => winrate);

        }
        _logs.Error(game.GameId);
        try
        {
            if (game.GameMode == "ShowResult")
            {
                var channel = _global.Client.GetGuild(561282595799826432).GetTextChannel(930706511632691222);
                await channel.SendMessageAsync($"Game #{game.GameId}\n" +
                                               $"Vesrion: {game.GameVersion}\n" +
                                               $"1. **{game.PlayersList[0].Character.Name} - {game.PlayersList[0].Status.GetScore()}**\n" +
                                               $"2. {game.PlayersList[1].Character.Name} - {game.PlayersList[1].Status.GetScore()}\n" +
                                               $"3. {game.PlayersList[2].Character.Name} - {game.PlayersList[2].Status.GetScore()}\n" +
                                               $"4. {game.PlayersList[3].Character.Name} - {game.PlayersList[3].Status.GetScore()}\n" +
                                               $"5. {game.PlayersList[4].Character.Name} - {game.PlayersList[4].Status.GetScore()}\n" +
                                               $"6. {game.PlayersList[5].Character.Name} - {game.PlayersList[5].Status.GetScore()}\n<:e_:562879579694301184>\n");
            }
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }


    private async void CheckIfEveryoneIsReady(object sender, ElapsedEventArgs e)
    {
        if (_looping) return;
        _looping = true;

        var games = _global.GamesList;

        for (var i = 0; i < games.Count; i++)
        {
            var game = games[i];

            //protection against double calculations
            if (!game.IsCheckIfReady) continue;

            //round 11 is the end of the game, no fights on round 11
            if (game.RoundNo == 11)
            {
                await HandleLastRound(game);
                continue;
            }

            var players = _global.GamesList[i].PlayersList;
            var readyTargetCount = players.Count(x => !x.IsBot());
            var readyCount = 0;
            foreach (var player in players.Where(x => !x.IsBot()))
            {
                //if (game.TimePassed.Elapsed.TotalSeconds < 30) continue;

                if (game.TimePassed.Elapsed.TotalSeconds > 30 && player.Status.TimesUpdated == 0)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }
                if (game.TimePassed.Elapsed.TotalSeconds > 60 && player.Status.TimesUpdated == 1)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }
                if (game.TimePassed.Elapsed.TotalSeconds > 90 && player.Status.TimesUpdated == 2)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }
                if (game.TimePassed.Elapsed.TotalSeconds > 120 && player.Status.TimesUpdated == 3)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }
                if (game.TimePassed.Elapsed.TotalSeconds > 150 && player.Status.TimesUpdated == 4)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }
                if (game.TimePassed.Elapsed.TotalSeconds > 180 && player.Status.TimesUpdated == 5)
                {
                    player.Status.TimesUpdated++;
                    await _upd.UpdateMessage(player);
                }

                if (game.TimePassed.Elapsed.TotalSeconds < 50 && !player.Status.ConfirmedSkip) continue;
                if (player.Status.IsReady && player.Status.ConfirmedPredict)
                    readyCount++;
            }


            if (readyCount != readyTargetCount &&
                !(game.TimePassed.Elapsed.TotalSeconds >= game.TurnLengthInSecond))
                continue;

            //Calculating the game
            game.IsCheckIfReady = false;



            //If did do anything - Block
            foreach (var t in players.Where(t => !t.IsBot() && !t.Status.IsAutoMove && t.Status.WhoToAttackThisTurn == Guid.Empty && t.Status.IsBlock == false && t.Status.IsSkip == false))
            {
                _logs.Warning($"\nWARN: {t.DiscordUsername} didn't do anything - Auto Move!\n");
                t.Status.IsAutoMove = true;
                var textAutomove = $"Ты не походил. Использовался Авто Ход\n";
                t.Status.AddInGamePersonalLogs(textAutomove);
                t.Status.ChangeMindWhat = textAutomove;
            }

            //If did do anything - LvL up a random stat
            foreach (var t in players.Where(t => !t.IsBot() && t.Status.MoveListPage == 3))
            {
                _logs.Warning($"\nWARN: {t.DiscordUsername} didn't do anything - Auto LvL!\n");
                t.Status.IsAutoMove = true;
                var textAutomove = $"Ты не походил. Использовался Авто Ход\n";
                t.Status.AddInGamePersonalLogs(textAutomove);
                t.Status.ChangeMindWhat = textAutomove;
            }

            //handle bots
            foreach (var t in players.Where(x => x.IsBot() || x.Status.IsAutoMove))
                try
                {
                    await _botsBehavior.HandleBotBehavior(t, game);
                }
                catch (Exception exception)
                {
                    _logs.Critical(exception.Message);
                    _logs.Critical(exception.StackTrace);
                }

            foreach (var t in players.Where(t => t.Status.WhoToAttackThisTurn == Guid.Empty && t.Status.IsBlock == false && t.Status.IsSkip == false))
            {
                _logs.Critical($"\nCRIT: {t.DiscordUsername} didn't do anything  and auto move didn't as well.!\n");
            }

            //delete messages from prev round. No await.
                foreach (var player in game.PlayersList) await _help.DeleteItAfterRound(player);

            await _round.CalculateAllFights(game);
            //await Task.Delay(1);

            foreach (var t in players.Where(x => !x.IsBot()))
                try
                {
                    var extraText = "";
                    if (game.RoundNo <= 10) extraText = $"Раунд #{game.RoundNo}";

                    if (game.RoundNo == 8)
                    {
                        t.Status.ConfirmedPredict = false;
                        extraText = "Это последний раунд, когда можно сделать **предложение**!";
                    }

                    if (game.RoundNo == 9) t.Status.ConfirmedPredict = true;

                    await _upd.UpdateMessage(t, extraText);
                }
                catch (Exception exception)
                {
                    _logs.Critical(exception.Message);
                    _logs.Critical(exception.StackTrace);
                }

            game.IsCheckIfReady = true;
        }

        _looping = false;
    }


}