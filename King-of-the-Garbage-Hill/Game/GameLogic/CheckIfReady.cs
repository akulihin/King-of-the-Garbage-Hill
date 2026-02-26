using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CheckIfReady : IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly BotsBehavior _botsBehavior;
    private readonly CharacterPassives _characterPassives;
    private readonly GameReaction _gameReaction;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly Global _global;
    private readonly HelperFunctions _help;
    private readonly LoginFromConsole _logs;
    private readonly DoomsdayMachine _round;
    private readonly GameUpdateMess _upd;


    private int _finishedGames;
    private int _looping;
    public Timer LoopingTimer;

    public CheckIfReady(Global global, GameUpdateMess upd, DoomsdayMachine round,
        GameUpdateMess gameUpdateMess, BotsBehavior botsBehavior, LoginFromConsole logs, UserAccounts accounts,
        HelperFunctions help, GameReaction gameReaction, CharacterPassives characterPassives)
    {
        _global = global;
        _upd = upd;
        _round = round;
        _gameUpdateMess = gameUpdateMess;
        _botsBehavior = botsBehavior;
        _logs = logs;
        _accounts = accounts;
        _help = help;
        _gameReaction = gameReaction;
        _characterPassives = characterPassives;
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
        var playerWhoWon = game.PlayersList.Where(x => !x.Passives.IsDead).FirstOrDefault()
                           ?? game.PlayersList.First();

        //if won phrases
        switch (playerWhoWon.GameCharacter.Name)
        {
            case "HardKitty":
                game.AddGlobalLogs("HarDKitty больше не одинок! Как много друзей!!!");

                var hard = playerWhoWon.Passives.HardKittyLoneliness;

                if (hard != null)
                    foreach (var enemy in game.PlayersList)
                    {
                        var hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == enemy.GetPlayerId());
                        if (hardEnemy != null)
                            game.PlayersList.Find(x => x.GetPlayerId() == hardEnemy.EnemyId)!.Status
                                .AddInGamePersonalLogs(
                                    $"HarDKitty больше не одинок! Вы принесли ему {hardEnemy.Times} очков.\n");
                    }

                break;

            case "Кратос":
                game.AddGlobalLogs("Я умер как **Воин**, вернулся как **Бог**, а закончил **Королем Мусорной Горы**!");
                break;

            case "Saitama":
                game.AddGlobalLogs("Наконец-то я победил Кинга! Пойду дальше геройствовать.");
                break;

            case "Монстр без имени":
                game.AddGlobalLogs("Я должен исчезнуть из этого мира, а вместе со мной и все, кто когда либо видел меня.");
                break;

            case "Рик Санчез":
                game.AddGlobalLogs("Я прошерстил тысячу так же мусорных гор, но ни на одной Рика Прайма так и не было.");
                break;
        }

        //if lost phrases
        foreach (var player in game.PlayersList.Where(x => x.Status.GetPlaceAtLeaderBoard() != 1))
            switch (player.GameCharacter.Name)
            {
                case "HardKitty":
                    player.Status.AddInGamePersonalLogs("Даже имя мое написать нормально не можете");
                    break;
                case "Злой Школьник":
                    player.Status.AddInGamePersonalLogs("Блять, суки, че вы меня таким слабым сделали?");
                    break;
                case "Тигр":
                    player.Status.AddInGamePersonalLogs("Обоссанная игра, обоссанный баланс");
                    break;
                case "Saitama":
                    player.Status.AddInGamePersonalLogs("Да ну эти видеоигры! Монстров убивать проще... Как Кинг всё время выигрывает?");
                    break;
            }

        // Салдорум end-game corruption count
        foreach (var player in game.PlayersList.Where(x => x.GameCharacter.Name == "Салдорум"))
        {
            if (player.Passives.SaldorumCorruptionCount > 0)
                player.Status.AddInGamePersonalLogs(
                    $"Великий летописец: испорчено {player.Passives.SaldorumCorruptionCount} записей за игру");
        }

        //unique
        if (game.PlayersList.Any(x => x.GameCharacter.Name == "DeepList") &&
            game.PlayersList.Any(x => x.GameCharacter.Name == "mylorik"))
        {
            var mylorik = game.PlayersList.Find(x => x.GameCharacter.Name == "mylorik");
            var deepList = game.PlayersList.Find(x => x.GameCharacter.Name == "DeepList");

            var genius = true;

            foreach (var deepListPredict in deepList!.Predict)
            {
                genius = mylorik!.Predict.Any(x =>
                    x.PlayerId == deepListPredict.PlayerId && x.CharacterName == deepListPredict.CharacterName);
                if (!genius) break;
            }

            if (genius)
                foreach (var mylorikPredict in mylorik!.Predict)
                {
                    genius = deepList!.Predict.Any(x =>
                        x.PlayerId == mylorikPredict.PlayerId && x.CharacterName == mylorikPredict.CharacterName);
                    if (!genius) break;
                }

            if (genius)
                game.AddGlobalLogs("DeepList & mylorik: Гении мыслят одинакого или одно целое уничтожает воду.");
        }

        //
        try
        {
            if (game.PlayersList.Count == 6 && game.PlayersList.Count(x => x.Passives.IsDead) != 5)
                if (playerWhoWon.Status.PlaceAtLeaderBoardHistory.Find(x => x.GameRound == 10)!.Place != 1)
                    if (game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == 1)!.Status.GetScore() !=
                        game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == 2)!.Status.GetScore())
                        game.AddGlobalLogs(
                            $"**{playerWhoWon.DiscordUsername}** вырывает **очко** на последних секундах!");
        }
        catch
        {
            //ignored
        }

        // Кира end-game events
        var kiraPlayer = game.PlayersList.Find(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Тетрадь смерти"));
        if (kiraPlayer != null)
        {
            var kiraDeathNote = kiraPlayer.Passives.KiraDeathNote;
            var kills = kiraDeathNote.Entries.Count(x => x.WasCorrect);
            var totalEntries = kiraDeathNote.Entries.Count;
            var kiraWon = kiraPlayer.Status.GetPlaceAtLeaderBoard() == 1;
            var killedAll = game.PlayersList
                .Where(x => x.GetPlayerId() != kiraPlayer.GetPlayerId())
                .All(x => x.Passives.IsDead && x.Passives.DeathSource == "Kira");

            // Kira never wrote in the Death Note at all
            if (totalEntries == 0 && !kiraPlayer.Passives.IsDead)
            {
                if (kiraWon)
                {
                    game.AddGlobalLogs(
                        "**Рюк:** Что? Как?! Он ведь даже не использовал Тетрадь Смерти.\n" +
                        "**Ягами Лайт:** Я ее продал Миками Теру за 500 гривен. Он заработал мне денег, убивая конкурентов компании Юцуба.");
                }
                else
                {
                    game.AddGlobalLogs(
                        "**Рюк:** Лайт, это Тетрадь Смерти. Тебе надо записывать в нее имена, чтобы я... Чтобы ты стал богом.\n" +
                        "**Ягами Лайт:** Да нафига мне это надо. Мне еще к экзаменам готовиться. Надеюсь продать ее за 10 гривен.\n" +
                        "**Рюк:** Тебе в руки попало оружие массового убийства, а ты хочешь его продать за 10 гривен как лох?!\n" +
                        "**Ягами Лайт:** Что? Так она убивать может... Тогда продам за 500.");
                }
            }
            // Kira killed everyone except L
            else if (!killedAll && kills > 0)
            {
                var aliveNonKira = game.PlayersList
                    .Where(x => x.GetPlayerId() != kiraPlayer.GetPlayerId() && !(x.Passives.IsDead && x.Passives.DeathSource == "Kira"))
                    .ToList();
                if (aliveNonKira.Count == 1 && aliveNonKira[0].GameCharacter.Passive.Any(p => p.PassiveName == "L"))
                {
                    game.AddGlobalLogs(
                        "**L:** А куда все подевались? Почему я один?");
                }
            }

            // Kira killed everyone
            if (killedAll)
            {
                game.AddGlobalLogs(
                    "**Kira:** Теперь я... Бог... \n" +
                    "**Рюк:** А не лох...");
            }
            // Kira won but didn't guess anyone correctly (had entries but all failed)
            else if (kiraWon && kills == 0 && totalEntries > 0)
            {
                game.AddGlobalLogs(
                    "**Kira:** А? Что? Как это произошло?\n" +
                    "**Рюк:** Зря я сбрасывал Тетрадь Смерти в Украину.");
            }
            // Kira is first place (but didn't kill everyone, and had some kills)
            else if (kiraWon)
            {
                game.AddGlobalLogs(
                    "**Kira:** Всё идет по моему плану...\n" +
                    "**Рюк:** Какие планы дальше? Убьешь следователей?\n" +
                    "**Kira:** А нафига? Буду и дальше игнорить **L** и убивать преступников.");
            }

            // Kira had no kills at all (wrote entries but all failed)
            if (kills == 0 && totalEntries > 0 && !kiraPlayer.Passives.IsDead)
            {
                game.AddGlobalLogs(
                    "**Kira:** Стоп, а почему никто не умер?\n" +
                    "**L:** Похоже никакого Киры здесь нет, попробую поискать в Беларуси.\n" +
                    "**Рюк:** Лайт, ты писал на стекле.\n" +
                    "**Kira:** Блин, что же делать? А, я ведь могу со стекла переписать, пусть они уже после игры помрут, кек. Я должен доказать **L** что я **бог**! __А не лох!__");
            }
        }
    }


    private async Task HandleLastRound(GameClass game)
    {
        game.IsCheckIfReady = false;

        // Геральт — pitchfork death: if Geralt finishes last
        var geraltLast = game.PlayersList.Find(x =>
            x.GameCharacter.Name == "Геральт" && x.Status.GetPlaceAtLeaderBoard() == 6);
        if (geraltLast != null)
        {
            game.AddGlobalLogs("Крестьяне с вилами настигли Ведьмака... Работа неблагодарная.");
        }

        foreach (var player in game.PlayersList)
        {
            player.Status.ConfirmedSkip = true;
        }

        foreach (var player in game.PlayersList)
        {
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "AdminPlayerType"))
            {
                foreach (var enemy in game.PlayersList.Where(x  => x.GetPlayerId() != player.GetPlayerId()))
                {
                    player.Predict.Add(new PredictClass(enemy.GameCharacter.Name, enemy.GetPlayerId()));
                }
            } 
        }

        //predict (skip Kira — uses Death Note instead of predictions; skip Monster — can't be predicted)
        if (game.PlayersList.Count == 6 && game.PlayersList.Count(x => x.IsBot()) <= 5)
            foreach (var player in from player in game.PlayersList
                     where !player.GameCharacter.Passive.Any(p => p.PassiveName == "Тетрадь смерти")
                     from predict in player.Predict
                     let enemy = game.PlayersList.Find(x => x.GetPlayerId() == predict.PlayerId)
                     where enemy!.GameCharacter.Name == predict.CharacterName
                     where !enemy.GameCharacter.Passive.Any(p => p.PassiveName == "Выдуманный персонаж")
                     select player)
            {
                var predBonus = player.GameCharacter.Passive.Any(p => p.PassiveName == "Великий летописец") ? 2 : 1;
                player.Status.AddBonusPoints(predBonus, "Предположение");
            }
        // predict

        // TheBoys — Компромат М.М.: multiply prediction bonus by kompromat count
        foreach (var boysPlayer in game.PlayersList.Where(x =>
                     x.GameCharacter.Passive.Any(p => p.PassiveName == "Компромат М.М.")))
        {
            var kompromatCount = boysPlayer.Passives.TheBoysMM.KompromatTargets.Count;
            if (kompromatCount > 1)
            {
                // Count how many correct predictions this player made
                var correctPredictions = 0;
                foreach (var predict in boysPlayer.Predict)
                {
                    var enemy = game.PlayersList.Find(x => x.GetPlayerId() == predict.PlayerId);
                    if (enemy != null && enemy.GameCharacter.Name == predict.CharacterName
                        && !enemy.GameCharacter.Passive.Any(p => p.PassiveName == "Выдуманный персонаж"))
                        correctPredictions++;
                }

                if (correctPredictions > 0)
                {
                    // Already got 1× from the normal loop, add (multiplier - 1)× more
                    var extraMultiplier = kompromatCount - 1;
                    var predBonus = boysPlayer.GameCharacter.Passive.Any(p => p.PassiveName == "Великий летописец") ? 2 : 1;
                    var extraPoints = correctPredictions * predBonus * extraMultiplier;
                    boysPlayer.Status.AddBonusPoints(extraPoints, "Компромат М.М.");
                    boysPlayer.Status.AddInGamePersonalLogs(
                        $"Компромат М.М.: {correctPredictions} верных предположений × {kompromatCount} компромата = +{extraPoints} бонусных очков\n");
                    game.Phrases.TheBoysKompromatReward.SendLog(boysPlayer, false);
                }
            }
        }
        // end TheBoys kompromat

        // Tsukuyomi end-game deduction: deduct stolen points from victims
        foreach (var itachiPlayer in game.PlayersList.Where(x =>
                     x.GameCharacter.Passive.Any(p => p.PassiveName == "Глаза Итачи")))
        {
            var tsukuyomiEnd = itachiPlayer.Passives.ItachiTsukuyomi;
            foreach (var (victimId, stolenAmount) in tsukuyomiEnd.StolenFromPlayers)
            {
                if (stolenAmount <= 0) continue;
                var victim = game.PlayersList.Find(x => x.GetPlayerId() == victimId);
                if (victim == null) continue;
                victim.Status.AddBonusPoints(-stolenAmount, "Глаза Итачи");
                victim.Status.AddInGamePersonalLogs(
                    $"Вы заработали *{stolenAmount} очков*, но всё это было в глазах у Итачи...\n*-{stolenAmount} очков*\n");
                game.Phrases.ItachiTsukuyomiReveal.SendLog(victim, false);
            }
        }
        // end Tsukuyomi

        //sort
        game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        for (var k = 0; k < game.PlayersList.Count; k++)
            game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);
        //end sorting

        try
        {
            //Произошел троллинг
            var awdkas = game.PlayersList.Where(x =>
                x.GameCharacter.Passive.Any(y => y.PassiveName == "Произошел троллинг"));
            foreach (var awdka in awdkas)
            {
                var awdkaTroll = awdka.Passives.AwdkaTrollingList;


                var enemy = awdkaTroll.EnemyList.Find(x =>
                    x.EnemyId == game.PlayersList.Find(y => y.Status.GetPlaceAtLeaderBoard() == 1)!.GetPlayerId());

                var trolledText = "";
                if (enemy != null)
                {
                    var tolled = game.PlayersList.Find(x => x.GetPlayerId() == enemy.EnemyId);

                    trolledText = tolled!.GameCharacter.Name switch
                    {
                        "DeepList" => "Лист Затроллился, хех",
                        "mylorik" => "Лорик Затроллился, МММ!",
                        "Глеб" => "Спящее Хуйло",
                        "LeCrisp" => "ЛеПуська Затроллилась",
                        "Толя" => "Раммус Продал Тормейл",
                        "HardKitty" => "Пакет Молока Пролился На Клавиатуру",
                        "Sirinoks" => "Айсик Затроллилась#",
                        "Злой Школьник" => "МитСУКИ Затроллился",
                        "AWDKA" => "AWDKA Затроллился сам по себе...",
                        "Осьминожка" => "Осьминожка Забулькался",
                        "Darksci" => "Даркси Нe Повeзло...",
                        "Братишка" => "Братишка Забулькался",
                        "Загадочный Спартанец в маске" => "Спатанец Затроллился!? А-я-йо...",
                        "Вампур" => "ВампYр Затроллился",
                        "Тигр" => "Тигр Обоссался, и кто теперь обоссан!?",
                        "Краборак" => "За**Краборак**чился",
                        "Weedwick" => "Видвик Забулькался, ауф!",
                        "Сайтама" => "Сайтама Затроллился одним ударом!",
                        "Рик Санчез" => "Рик Затроллился, Wubba Lubba Dub Dub!",
                        "Кира" => "Кира записал себя в тетрадь...",
                        "Молодой Глеб" => "Молодой Глеб Затроллился, но хотя бы не уснул",
                        "Баг" => "Баг Затроллился, ошибка 404",
                        "Кратос" => "Кратос пал от руки троллинга!",
                        "Итачи" => "Итачи попал в свою же иллюзию!",
                        _ => ""
                    };

                    var bonusTrolling = 0;

                    foreach (var predict in awdka.Predict)
                    {
                        var found = game.PlayersList.Find(x =>
                            predict.PlayerId == x.GetPlayerId() && predict.CharacterName == x.GameCharacter.Name
                            && !x.GameCharacter.Passive.Any(p => p.PassiveName == "Выдуманный персонаж"));
                        if (found != null) bonusTrolling += 1;
                    }

                    awdka.Status.AddBonusPoints(bonusTrolling + (enemy.Score + 1) / 2,
                        $"**Произошел Троллинг:** {trolledText} ");
                    game.Phrases.AwdkaTrolling.SendLog(awdka, true);
                }

                //sort
                game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                for (var k = 0; k < game.PlayersList.Count; k++)
                    game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);
                //end sorting

                if (enemy != null && game.PlayersList.First().GameCharacter.Passive
                        .Any(x => x.PassiveName == "Произошел троллинг"))
                    game.AddGlobalLogs($"**Произошел Троллинг:** {trolledText} ");
            }
            //end Произошел троллинг
        }

        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }

        foreach (var t in game.PlayersList)
            t.Status.PlaceAtLeaderBoardHistory.Add(
                new InGameStatus.PlaceAtLeaderBoardHistoryClass(game.RoundNo, t.Status.GetPlaceAtLeaderBoard()));

        // Premade: if Support and Carry both in top 2, Support wins
        var supportPlayer = game.PlayersList.FirstOrDefault(x =>
            x.GameCharacter.Passive.Any(p => p.PassiveName == "Premade") &&
            x.Passives.SupportPremade.MarkedPlayerId != Guid.Empty);
        if (supportPlayer != null)
        {
            var carryPlayer = game.PlayersList.Find(x =>
                x.GetPlayerId() == supportPlayer.Passives.SupportPremade.MarkedPlayerId);
            if (carryPlayer != null)
            {
                var suppPos = supportPlayer.Status.GetPlaceAtLeaderBoard();
                var carryPos = carryPlayer.Status.GetPlaceAtLeaderBoard();
                if (suppPos <= 2 && carryPos <= 2 && suppPos > carryPos)
                {
                    var diff = carryPlayer.Status.GetScore() - supportPlayer.Status.GetScore() + 1;
                    supportPlayer.Status.AddBonusPoints(diff, "Premade");
                    game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                    for (var k = 0; k < game.PlayersList.Count; k++)
                        game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);
                    game.AddGlobalLogs("Суппорт и Carry оба в топ 2! Суппорт засчитан как победитель.");
                }
            }
        }

        // Одна из трех: if player with this passive is in top 3, they win
        var top3Player = game.PlayersList.FirstOrDefault(x =>
            x.GameCharacter.Passive.Any(p => p.PassiveName == "Одна из трех") &&
            x.Status.GetPlaceAtLeaderBoard() <= 3 &&
            !x.Passives.IsDead);
        if (top3Player != null)
        {
            game.AddGlobalLogs("**Sakura:** Я одна из легендарной тройки. И этого вполне достаточно!");
        }

        var playerWhoWon = top3Player
                           ?? game.PlayersList.Where(x => !x.Passives.IsDead).FirstOrDefault()
                           ?? game.PlayersList.First();
        HandlePostGameEvents(game);


        if (playerWhoWon.Status.AutoMoveTimes >= 10) playerWhoWon.DiscordUsername = "НейроБот";

        if (playerWhoWon.Status.AutoMoveTimes >= 9 &&
            playerWhoWon.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят"))
            playerWhoWon.DiscordUsername = "НейроБот";

        var isTeam = false;
        decimal wonScore = 0;
        decimal team1Score = 0;
        decimal team2Score = 0;
        decimal team3Score = 0;
        var wonTeam = 0;
        if (game.Teams.Count > 0)
        {
            isTeam = true;
            foreach (var player in game.PlayersList)
                if (game.Teams.Find(x => x.TeamPlayers.Contains(player.Status.PlayerId))!.TeamId == 1)
                    team1Score += player.Status.GetScore();
                else if (game.Teams.Find(x => x.TeamPlayers.Contains(player.Status.PlayerId))!.TeamId == 2)
                    team2Score += player.Status.GetScore();
                else
                    team3Score += player.Status.GetScore();

            if (team1Score == team2Score && team3Score == 0)
            {
                game.AddGlobalLogs("\n**Ничья**");
            }
            else if (team1Score == team2Score && team1Score == team3Score)
            {
                game.AddGlobalLogs("\n**Ничья**");
            }
            else
            {
                if (team1Score > team2Score && team1Score > team3Score)
                {
                    wonTeam = 1;
                    wonScore = team1Score;
                }
                else if (team2Score > team1Score && team2Score > team3Score)
                {
                    wonTeam = 2;
                    wonScore = team2Score;
                }
                else if (team3Score > team1Score && team3Score > team2Score)
                {
                    wonTeam = 3;
                    wonScore = team3Score;
                }

                if (wonTeam == 0)
                {
                    game.AddGlobalLogs("\n**Ничья**");
                }
                else
                {
                    game.AddGlobalLogs($"\nКоманда #{wonTeam} победила набрав {wonScore} Очков!");

                    if (wonTeam != 1)
                        game.AddGlobalLogs($"\nКоманда #1 Набрала {team1Score} Очков.");
                    if (wonTeam != 2)
                        game.AddGlobalLogs($"Команда #2 Набрала {team2Score} Очков.");
                    if (wonTeam != 3)
                        if (team3Score > 0)
                            game.AddGlobalLogs($"Команда #3 Набрала {team3Score} Очков.");
                }
            }
        }
        else
        {
            game.AddGlobalLogs(
                game.PlayersList.FindAll(x => x.Status.GetScore() == playerWhoWon.Status.GetScore()).Count > 1
                    ? "\n**Ничья**"
                    : $"\n**{playerWhoWon.DiscordUsername}** победил, играя за **{playerWhoWon.GameCharacter.Name}**");
            if (!playerWhoWon.IsBot() && !playerWhoWon.IsWebPlayer && !playerWhoWon.PreferWeb)
                if (game.PlayersList.FindAll(x => x.Status.GetScore() == playerWhoWon.Status.GetScore())
                        .Count == 1)
                {
#pragma warning disable CS4014
                    playerWhoWon.DiscordStatus.SocketGameMessage.Channel.SendMessageAsync(
                        "__**Победа! Теперь вы Король этой Мусорной Горы. Пока-что...**__");
                    playerWhoWon.DiscordStatus.SocketGameMessage.Channel
                        .SendMessageAsync("https://tenor.com/bELKU.gif");
#pragma warning restore CS4014
                }
        }

        //todo: need to redo this system    
        //_finishedGameLog.CreateNewLog(game);


        foreach (var player in game.PlayersList)
        {
            await _gameUpdateMess.UpdateMessage(player);

            var account = _accounts.GetAccount(player.DiscordId);
            account.IsPlaying = false;
            player.GameId = 1000000;


            account.TotalPlays++;
            if (account.TotalPlays > 10) account.IsNewPlayer = false;
            account.TotalWins += player.Status.GetPlaceAtLeaderBoard() == 1 ? 1 : (ulong)0;
            account.MatchHistory.Add(new DiscordAccountClass.MatchHistoryClass(player.GameCharacter.Name,
                player.Status.GetScore(), player.Status.GetPlaceAtLeaderBoard()));

            // Character mastery points
            var masteryPointsToAdd = player.Status.GetPlaceAtLeaderBoard() switch
            {
                1 => 10, 2 => 7, 3 => 5, 4 => 3, 5 => 2, 6 => 1, _ => 0
            };
            if (!player.Passives.IsDead)
                account.CharacterMastery[player.GameCharacter.Name] =
                    account.CharacterMastery.GetValueOrDefault(player.GameCharacter.Name, 0) + masteryPointsToAdd;

            /*
            account.ZbsPoints += (player.Status.GetPlaceAtLeaderBoard() - 6) * -1 + 1;
            if (player.Status.GetPlaceAtLeaderBoard() == 1)
                account.ZbsPoints += 4;
            */

            var zbsPointsToGive = 0;
            switch (player.Status.GetPlaceAtLeaderBoard())
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

            if (player.Status.GetScore() == playerWhoWon.Status.GetScore())
                zbsPointsToGive = 100;

            if (isTeam && wonTeam > 0)
            {
                var winningTeam = game.Teams.Find(x => x.TeamId == wonTeam);
                if (winningTeam != null)
                    zbsPointsToGive = winningTeam.TeamPlayers.Contains(player.Status.PlayerId) ? 100 : 50;
            }

            if (player.Passives.IsDead) zbsPointsToGive = 0;

            account.ZbsPoints += zbsPointsToGive;

            // Quest progress tracking
            QuestService.TrackGameEnd(account, player, game);

            // Loot box for top 2 (alive players only) — deferred to lobby
            if (player.Status.GetPlaceAtLeaderBoard() <= 2 && !player.Passives.IsDead)
            {
                account.PendingLootBoxes++;
            }

            // Achievement tracking
            // Set final state before evaluation
            var tracker = player.Passives.AchievementTracker;
            tracker.FinishedWithZeroPsyche = player.GameCharacter.GetPsyche() <= 0;
            tracker.FinishedWithMaxPsyche = player.GameCharacter.GetPsyche() >= 10;
            AchievementService.TrackGameEnd(account, player, game);
            player.Passives.AchievementDataRef = account.Achievements;

            // Pity system: increment counters for tiers not played this game
            foreach (var tier in new[] { 1, 2, 3, 4, 5, 6 })
            {
                if (tier != player.GameCharacter.Tier)
                    account.TierPity[tier] = account.TierPity.GetValueOrDefault(tier, 0) + 1;
            }

            var characterStatistics =
                account.CharacterStatistics.Find(x =>
                    x.CharacterName == player.GameCharacter.Name);

            if (characterStatistics == null)
            {
                account.CharacterStatistics.Add(
                    new DiscordAccountClass.CharacterStatisticsClass(player.GameCharacter.Name,
                        player.Status.GetPlaceAtLeaderBoard() == 1 ? 1 : (ulong)0));
            }
            else
            {
                characterStatistics.Plays++;
                characterStatistics.Wins += player.Status.GetPlaceAtLeaderBoard() == 1 ? 1 : (ulong)0;
            }

            var performanceStatistics =
                account.PerformanceStatistics.Find(x =>
                    x.Place == player.Status.GetPlaceAtLeaderBoard());

            if (performanceStatistics == null)
                account.PerformanceStatistics.Add(
                    new DiscordAccountClass.PerformanceStatisticsClass(player.Status.GetPlaceAtLeaderBoard()));
            else
                performanceStatistics.Times++;
            try
            {
                if (!player.IsBot() && !player.IsWebPlayer && !player.PreferWeb)
                    await player.DiscordStatus.SocketGameMessage.Channel.SendMessageAsync(
                        $"Спасибо за игру!\nВы заработали **{zbsPointsToGive}** ZBS points!\n\nВы можете потратить их в магазине - `*store`\nА вы заметили? Это многопользовательская игра до 6 игроков! Вы можете начать игру с другом пинганув его! Например `*st @Boole`");
            }
            catch (Exception exception)
            {
                _logs.Critical(exception.Message);
                _logs.Critical(exception.StackTrace);
            }
        }

        // Save replay before removing the game
        try { _global.OnReplaySave?.Invoke(game); }
        catch (Exception ex) { _logs.Critical($"Replay save failed: {ex.Message}"); }

        // Broadcast final state to web clients BEFORE removing the game.
        // Without this, PreferWeb players never see the last round's results because
        // HandleLastRound completes too fast (no Discord API delay) and the game
        // is removed from GamesList before the SignalR timer can push the final state.
        if (_global.OnGameFinished != null)
        {
            try
            {
                await _global.OnGameFinished(game);
            }
            catch (Exception ex)
            {
                _logs.Critical($"OnGameFinished broadcast failed: {ex.Message}");
            }
        }

        await NotifyOwner(game);
        _global.GamesList.Remove(game);
    }

    private async Task NotifyOwner(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            _global.WinRates.TryGetValue(player.GameCharacter.Name, out var winrate);
            if (winrate == null)
                _global.WinRates.TryAdd(player.GameCharacter.Name, new Global.WinRateClass(player.GameCharacter.Name));
            _global.WinRates.TryGetValue(player.GameCharacter.Name, out winrate);


            winrate!.GameTimes++;

            switch (player.Status.GetPlaceAtLeaderBoard())
            {
                case 1:
                    winrate.Top1++;
                    break;
                case 2:
                    winrate.Top2++;
                    break;
                case 3:
                    winrate.Top3++;
                    break;
                case 4:
                    winrate.Top4++;
                    break;
                case 5:
                    winrate.Top5++;
                    break;
                case 6:
                    winrate.Top6++;
                    break;
            }

            winrate.WinRate = winrate.Top1 / winrate.GameTimes * 100;
            winrate.CharacterName = player.GameCharacter.Name;
            winrate.Elo = winrate.Top1 / winrate.GameTimes * 100 * 3 + winrate.Top2 / winrate.GameTimes * 100 * 2 +
                          winrate.Top3 / winrate.GameTimes * 100 - winrate.Top4 / winrate.GameTimes * 100 -
                          winrate.Top5 / winrate.GameTimes * 100 * 2 - winrate.Top6 / winrate.GameTimes * 100 * 3;
        }

        _finishedGames++;

        var eloPlusTop = new List<EloPlusTop>();

        //top1 winrate
        if (_finishedGames == game.TestFightNumber)
        {
            var winRates = _global.WinRates.Values.ToList();

            var text =
                $"**--------------------------------------------------------------------**\nTotal Games: {_global.GetLastGamePlayingAndId()}\n**TOP1**\n";

            var index = 1;
            foreach (var winRate in winRates.OrderByDescending(x => x.WinRate))
            {
                eloPlusTop.Add(new EloPlusTop(index, winRate.CharacterName));
                text +=
                    $"{index}. {winRate.CharacterName}: {winRate.WinRate.ToString("0.##")}% ({winRate.Top1}/{winRate.GameTimes})\n";
                index++;
            }

            await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(text);
        }

        //elo winrate
        if (_finishedGames == game.TestFightNumber)
        {
            var winRates = _global.WinRates.Values.ToList();


            var text = "**____**\n**ELO**\n";
            var index = 1;
            foreach (var winRate in winRates.OrderByDescending(x => x.Elo))
            {
                eloPlusTop.Find(x => x.CharacterName == winRate.CharacterName).PlaceAtTheLeaderBoard += index;
                text += $"{index}. {winRate.CharacterName}: {(int)(winRate.Elo * 10)}\n";
                index++;
            }

            text += "**--------------------------------------------------------------------**";
            await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(text);
        }
        //elo winrate end


        //elo+top winrate
        if (_finishedGames == game.TestFightNumber)
        {
            _finishedGames = 0;

            var text = "**____**\n**ELO+TOP**\n";
            var index = 1;
            foreach (var winRate in eloPlusTop.OrderBy(x => x.PlaceAtTheLeaderBoard))
            {
                text += $"{index}. {winRate.CharacterName}: {winRate.PlaceAtTheLeaderBoard}\n";
                index++;
            }

            text += "**--------------------------------------------------------------------**";
            await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(text);
        }
        //elo elo+top winrate

        try
        {
            if (game.GameMode == "ShowResult")
            {
                var channel = _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340);
                await channel.SendMessageAsync($"Game #{game.GameId}\n" +
                                               $"Vesrion: {game.GameVersion}\n" +
                                               $"1. **{game.PlayersList.First().GameCharacter.Name} - {game.PlayersList.First().Status.GetScore()}**\n" +
                                               $"2. {game.PlayersList[1].GameCharacter.Name} - {game.PlayersList[1].Status.GetScore()}\n" +
                                               $"3. {game.PlayersList[2].GameCharacter.Name} - {game.PlayersList[2].Status.GetScore()}\n" +
                                               $"4. {game.PlayersList[3].GameCharacter.Name} - {game.PlayersList[3].Status.GetScore()}\n" +
                                               $"5. {game.PlayersList[4].GameCharacter.Name} - {game.PlayersList[4].Status.GetScore()}\n" +
                                               $"6. {game.PlayersList[5].GameCharacter.Name} - {game.PlayersList[5].Status.GetScore()}\n<:e_:562879579694301184>\n");
            }
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
        //top1 winrate end
    }


    public async Task<string> API_PlayerIsReady(string body = "default value")
    {
        _logs.Info("Player is ready");
        _logs.Info(body);
        var games = _global.GamesList;
        var options1 = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
            WriteIndented = true
        };
        
        if (games.Count > 0)
        {
            var game1 = _global.GamesList[0];
            var jsonString = JsonSerializer.Serialize(game1, options1);
            return jsonString;
        }

        return "Not Ready";
    }

    private async void CheckIfEveryoneIsReady(object sender, ElapsedEventArgs e)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _looping, 1, 0) != 0) return;
        try
        {

        var games = _global.GamesList;

        for (var i = 0; i < games.Count; i++)
            try
            {
                var game = games[i];

                //protection against double calculations
                if (!game.IsCheckIfReady) continue;

                //round 11 is the end of the game, no fights on round 11
                if (game.RoundNo >= 11 && !game.IsKratosEvent) game.IsFinished = true;

                //protection against infinite games
                if (game.RoundNo >= 20) game.IsFinished = true;

                if (game.IsFinished)
                {
                    await HandleLastRound(game);
                    continue;
                }

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count(x => !x.IsBot());
                var readyCount = 0;

                //ARAM
                if (game.IsAramPickPhase)
                    if (players.Count(x => x.Status.IsAramRollConfirmed) == 6)
                    {
                        await _characterPassives.HandleNextRound(game);
                        _characterPassives.HandleBotPredict(game);

                        foreach (var player in players)
                        {
                            await _upd.SendCharacterMessage(player);
                            await _upd.DeleteGameMessage(player);
                            await _upd.WaitMess(player, game);
                        }

                        game.IsAramPickPhase = false;

                        foreach (var player in players)
                        {
                            player.Status.MoveListPage = 1;
                            await _upd.UpdateMessage(player);
                        }

                        _characterPassives.HandleEventsBeforeFirstRound(players);
                        for (var j = 0; j < players.Count; j++) players[j].Status.SetPlaceAtLeaderBoard(j + 1);
                    }


                //end ARAM

                // Draft Pick phase — wait until all humans have selected a character
                if (game.IsDraftPickPhase)
                {
                    if (game.PlayersList.All(x => x.Status.IsDraftPickConfirmed))
                    {
                        game.IsDraftPickPhase = false;
                        game.DraftOptions.Clear();

                        // Run deferred initialization (same as normal game creation)
                        var draftPlayersList = _characterPassives.HandleEventsBeforeFirstRound(game.PlayersList);
                        game.PlayersList = draftPlayersList;
                        for (var j = 0; j < draftPlayersList.Count; j++)
                            draftPlayersList[j].Status.SetPlaceAtLeaderBoard(j + 1);

                        await _characterPassives.HandleNextRound(game);
                        _characterPassives.HandleBotPredict(game);

                        // Reset turn timer so the first real round gets a fresh countdown
                        game.TimePassed.Restart();

                        // Reset Discord players from draft page (6) to game page (1)
                        foreach (var player in game.PlayersList.Where(p => p.PlayerType != 404))
                        {
                            player.Status.MoveListPage = 1;
                            await _upd.UpdateMessage(player);
                        }
                    }
                    continue; // Don't process turns while in draft phase
                }
                //end Draft Pick

                //Возвращение из мертвых
                if (game.IsKratosEvent)
                    foreach (var player in players.Where(x =>
                                 x.GameCharacter.Passive.All(y => y.PassiveName != "Возвращение из мертвых")))
                    {
                        player.Status.IsReady = true;
                        player.Status.IsBlock = true;
                    }


                //end Возвращение из мертвых

                // Auto-ready dead players
                foreach (var player in players.Where(x => x.Passives.IsDead))
                {
                    player.Status.IsReady = true;
                    player.Status.IsBlock = true;
                    player.Status.ConfirmedPredict = true;
                }

                foreach (var player in players.Where(x => !x.IsBot()))
                {
                    //if (game.TimePassed.Elapsed.TotalSeconds < 30) continue;
                    if (game.TimePassed.Elapsed.TotalSeconds > 30 && player.Status.TimesUpdated == 0)
                    {
                        player.Status.TimesUpdated++;
                        await _upd.UpdateMessage(player);
                    }

                    if (game.TimePassed.Elapsed.TotalSeconds > 90 && player.Status.TimesUpdated == 1)
                    {
                        player.Status.TimesUpdated++;
                        await _upd.UpdateMessage(player);
                    }

                    if (game.TimePassed.Elapsed.TotalSeconds > 150 && player.Status.TimesUpdated == 2)
                    {
                        player.Status.TimesUpdated++;
                        await _upd.UpdateMessage(player);
                    }

                    if (game.TimePassed.Elapsed.TotalSeconds > 210 && player.Status.TimesUpdated == 3)
                    {
                        player.Status.TimesUpdated++;
                        await _upd.UpdateMessage(player);
                    }

                    if (game.TimePassed.Elapsed.TotalSeconds > 270 && player.Status.TimesUpdated == 4)
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
                foreach (var t in players.Where(t =>
                             !t.IsBot() && !t.Status.IsAutoMove && t.Status.WhoToAttackThisTurn.Count == 0 &&
                             t.Status.IsBlock == false && t.Status.IsSkip == false))
                {
                    _logs.Warning($"\nWARN: {t.DiscordUsername} didn't do anything - Auto Move!\n");
                    t.Status.IsAutoMove = true;
                    t.Status.ConfirmedPredict = true;
                    var textAutomove = "Вы не походили. Использовался Авто Ход\n";
                    t.Status.AddInGamePersonalLogs(textAutomove);
                    t.Status.ChangeMindWhat = textAutomove;
                }

                // Dopa Макро — if only one action was completed, auto-move the second
                foreach (var t in players.Where(t =>
                             !t.IsBot() && !t.Status.IsReady && !t.Status.IsSkip
                             && t.GameCharacter.Passive.Any(x => x.PassiveName == "Макро")
                             && t.Status.WhoToAttackThisTurn.Count == 1))
                {
                    t.Status.IsAutoMove = true;
                    t.Status.AddInGamePersonalLogs("Макро: Второе действие не выбрано. Использовался Авто Ход\n");
                }


                //handle bots
                //Произошел троллинг
                foreach (var player in game.PlayersList
                             .Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Произошел троллинг"))
                             .ToList())
                {
                    var hardIndex = game.PlayersList.IndexOf(player);

                    for (var k = hardIndex; k < game.PlayersList.Count - 1; k++)
                        game.PlayersList[k] = game.PlayersList[k + 1];

                    game.PlayersList[^1] = player;
                }

                for (var k = 0; k < game.PlayersList.Count; k++)
                    game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);

                //end Произошел троллинг
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


                //Никому не нужен
                foreach (var player in game.PlayersList
                             .Where(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Никому не нужен")).ToList())
                {
                    var hardIndex = game.PlayersList.IndexOf(player);

                    for (var k = hardIndex; k < game.PlayersList.Count - 1; k++)
                        game.PlayersList[k] = game.PlayersList[k + 1];

                    game.PlayersList[^1] = player;
                }


                //end Никому не нужен
                //выдаем место в таблице
                for (var k = 0; k < game.PlayersList.Count; k++)
                    game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);


                //end //AWDKA last

                // Геральт: skip works as block (meditation)
                foreach (var geralt in players.Where(p =>
                    p.Status.IsSkip && p.GameCharacter.Name == "Геральт"))
                {
                    geralt.Status.IsSkip = false;
                    geralt.Status.IsBlock = true;
                }

                // Aggress — Toxic Mate auto-attacks random target instead of auto-blocking
                foreach (var t in players.Where(t =>
                             t.Status.WhoToAttackThisTurn.Count == 0 && t.Status.IsBlock == false &&
                             t.Status.IsSkip == false && t.GameCharacter.Passive.Any(x => x.PassiveName == "Aggress")))
                {
                    var targets = players.Where(p => p.GetPlayerId() != t.GetPlayerId()
                        && !p.Passives.IsDead).ToList();
                    if (targets.Count > 0)
                    {
                        t.Status.WhoToAttackThisTurn.Add(targets[Random.Shared.Next(targets.Count)].GetPlayerId());
                        t.Status.IsReady = true;
                    }
                }

                // Salldorum — Шэн: force players below Shen position to attack Salldorum
                foreach (var sallo in players.Where(p =>
                             p.GameCharacter.Name == "Salldorum" &&
                             p.Passives.SalldorumShen.ActiveThisTurn &&
                             !p.Passives.IsDead))
                {
                    var shenPos = sallo.Passives.SalldorumShen.TargetPosition;
                    foreach (var victim in players.Where(p =>
                                 p.GetPlayerId() != sallo.GetPlayerId() &&
                                 !p.Passives.IsDead &&
                                 p.Status.GetPlaceAtLeaderBoard() > shenPos))
                    {
                        if (!victim.Status.WhoToAttackThisTurn.Contains(sallo.GetPlayerId()))
                            victim.Status.WhoToAttackThisTurn.Add(sallo.GetPlayerId());
                    }
                }

                // Salldorum — Временная капсула: first block buries cola
                foreach (var sallo in players.Where(p =>
                             p.GameCharacter.Name == "Salldorum" &&
                             p.Status.IsBlock &&
                             !p.Passives.SalldorumTimeCapsule.FirstBlockUsed))
                {
                    var capsule = sallo.Passives.SalldorumTimeCapsule;
                    capsule.FirstBlockUsed = true;
                    capsule.Buried = true;
                    capsule.BuriedAtPosition = sallo.Status.GetPlaceAtLeaderBoard();
                    capsule.BuriedOnRound = game.RoundNo;
                    game.Phrases.SalldorumTimeCapsuleBury.SendLog(sallo, false);
                    sallo.Status.AddInGamePersonalLogs($"Временная капсула: Кола закопана на позиции {capsule.BuriedAtPosition} (раунд {game.RoundNo}).\n");
                }

                // Штормяк — taunt on block: force random enemy to attack the blocker as second action
                foreach (var taunter in players.Where(t =>
                             t.Status.IsBlock &&
                             t.GameCharacter.Passive.Any(x => x.PassiveName == "Штормяк")))
                {
                    var isOriginalKotiki = taunter.Passives.KotikiCatOwnerId == Guid.Empty;
                    // Only original Котики tracks TauntedPlayers (once per enemy per game)
                    // Transferred Storm cat can taunt every round but Котики is immune
                    var eligibleTargets = players.Where(p =>
                        p.GetPlayerId() != taunter.GetPlayerId() &&
                        // Exclude dead players
                        !p.Passives.IsDead &&
                        // Immunity: Котики immune to transferred Storm taunts
                        !(p.GameCharacter.Passive.Any(x => x.PassiveName == "Кошачья засада") && !isOriginalKotiki) &&
                        // Original Котики: once per enemy per game
                        (!isOriginalKotiki || !taunter.Passives.KotikiStorm.TauntedPlayers.Contains(p.GetPlayerId()))
                    ).ToList();

                    if (eligibleTargets.Count > 0)
                    {
                        var target = eligibleTargets[Random.Shared.Next(eligibleTargets.Count)];
                        if (!target.Status.WhoToAttackThisTurn.Contains(taunter.GetPlayerId()))
                            target.Status.WhoToAttackThisTurn.Add(taunter.GetPlayerId());

                        // Always track who was taunted (needed for block bypass in DoomsdayMachine)
                        taunter.Passives.KotikiStorm.CurrentTauntTarget = target.GetPlayerId();

                        if (isOriginalKotiki)
                        {
                            taunter.Passives.KotikiStorm.TauntedPlayers.Add(target.GetPlayerId());
                        }

                        taunter.Status.AddInGamePersonalLogs($"Штормяк провоцирует {target.DiscordUsername}!\n");
                        target.Status.AddInGamePersonalLogs($"Штормяк провоцирует вас! Атакуйте {taunter.DiscordUsername}!\n");
                    }
                }

                foreach (var t in players.Where(t =>
                             t.Status.WhoToAttackThisTurn.Count == 0 && t.Status.IsBlock == false &&
                             t.Status.IsSkip == false))
                {
                    t.Status.IsBlock = true;
                    t.Status.IsReady = true;
                    var text =
                        $"\nCRIT: round #{game.RoundNo} | {t.DiscordUsername} ({t.GameCharacter.Name}) didn't do anything and auto move didn't as well.!\n";
                    await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                        .SendMessageAsync(text);
                    _logs.Critical(text);
                }

                // Монстр: players Monster attacked last round cannot block or skip
                foreach (var victim in players.Where(v =>
                    v.Passives.MonsterNoEscape &&
                    !v.Passives.IsDead &&
                    !(game.RoundNo == 10 && v.GameCharacter.Passive.Any(
                        x => x.PassiveName == "Стримснайпят и банят и банят и банят"))))
                {
                    if (victim.Status.IsBlock || victim.Status.IsSkip)
                    {
                        victim.Status.IsBlock = false;
                        victim.Status.IsSkip = false;
                        if (victim.Status.WhoToAttackThisTurn.Count == 0)
                        {
                            var targets = players.Where(p =>
                                p.GetPlayerId() != victim.GetPlayerId() && !p.Passives.IsDead).ToList();
                            if (targets.Count > 0)
                                victim.Status.WhoToAttackThisTurn.Add(
                                    targets[Random.Shared.Next(targets.Count)].GetPlayerId());
                        }
                        victim.Status.IsReady = true;
                        victim.Status.AddInGamePersonalLogs(
                            "Монстр: Ты не можешь сбежать от того, кто уже внутри.\n");
                    }
                }

                //delete messages from prev round. No await.
                foreach (var player in game.PlayersList)
                    _help.DeleteItAfterRound(player);


                //moral
                //прожать всю момаль
                if (game.RoundNo == 10)
                    foreach (var player in game.PlayersList)
                        while (player.GameCharacter.GetMoral() >= 5)
                            await _gameReaction.HandleMoralForScore(player);


                //player.Status.AddBonusPoints(player.GameCharacter.GetBonusPointsFromMoral(), "Мораль");
                //end прожать всю момаль
                //end moral
                foreach (var player in game.PlayersList)
                {
                    player.GameCharacter.ResetMoralBonus();
                    player.GameCharacter.ResetStrengthQualityDropTimes();
                    player.Status.ResetFightingData();
                }

                await _round.CalculateAllFights(game);

                // Pity system: reset played tier on round 2 (skip round 1 remakes)
                if (game.RoundNo == 2)
                {
                    foreach (var player in game.PlayersList)
                    {
                        var acc = _accounts.GetAccount(player.DiscordId);
                        acc.TierPity[player.GameCharacter.Tier] = 0;
                    }
                }

                foreach (var player in game.PlayersList) player.GameCharacter.SetMoralBonus();

                foreach (var t in players.Where(x => !x.IsBot()))
                    try
                    {
                        var extraText = "";
                        if (game.RoundNo <= 10) extraText = $"Раунд #{game.RoundNo}";

                        if (game.RoundNo == 8 && game.GameMode != "Aram")
                        {
                            t.Status.ConfirmedPredict = false;
                            extraText = "Это последний раунд, когда можно сделать **предложение**!";

                            // Kira uses Death Note instead of predictions — auto-confirm
                            if (t.GameCharacter.Passive.Any(p => p.PassiveName == "Тетрадь смерти"))
                                t.Status.ConfirmedPredict = true;
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
            catch (Exception exception)
            {
                _logs.Critical(exception.Message);
                _logs.Critical(exception.StackTrace);
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                    .SendMessageAsync($"Game #{games[i].GameId}, Round #{games[i].RoundNo}\n{exception.Message}");
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                    .SendMessageAsync(exception.StackTrace);
            }

        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _looping, 0);
        }
    }

    public class EloPlusTop
    {
        public string CharacterName;
        public int PlaceAtTheLeaderBoard;

        public EloPlusTop(int placeAtTheLeaderBoard, string characterName)
        {
            PlaceAtTheLeaderBoard = placeAtTheLeaderBoard;
            CharacterName = characterName;
        }
    }
}