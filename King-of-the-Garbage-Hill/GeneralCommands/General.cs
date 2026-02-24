using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class General : ModuleBaseCustom
{
    private readonly UserAccounts _accounts;
    private readonly CharacterPassives _characterPassives;
    private readonly CommandsInMemory _commandsInMemory;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;
    private readonly GameUpdateMess _upd;
    private readonly StartGameLogic _startGameLogic;



    public General(UserAccounts accounts,
        HelperFunctions helperFunctions, CommandsInMemory commandsInMemory,
        Global global, GameUpdateMess upd,  CharacterPassives characterPassives, StartGameLogic startGameLogic)
    {
        _accounts = accounts;
        _helperFunctions = helperFunctions;
        _commandsInMemory = commandsInMemory;
        _global = global;
        _upd = upd;
        _characterPassives = characterPassives;
        _startGameLogic = startGameLogic;
    }


    [Command("игра")]
    [Alias("st", "start", "start game")]
    [Summary("запуск игры")]
    public async Task StartGameNormal(IUser player1 = null, IUser player2 = null, IUser player3 = null,
        IUser player4 = null,
        IUser player5 = null, IUser player6 = null)
    {
        player1 ??= Context.User;
        await StartGame(0, player1, player2, player3, player4, player5, player6);
    }

    [Command("игра")]
    [Alias("st", "start", "start game")]
    [Summary("запуск игры")]
    public async Task StartGameTeam(int team, IUser player1 = null, IUser player2 = null, IUser player3 = null,
        IUser player4 = null,
        IUser player5 = null, IUser player6 = null)
    {
        player1 ??= Context.User;
        await StartGame(team, player1, player2, player3, player4, player5, player6);
    }

    [Command("aram")]
    [Alias("ar", "a")]
    [Summary("Aram Mode")]
    public async Task StartAramGame(IUser player1 = null, IUser player2 = null, IUser player3 = null, IUser player4 = null, IUser player5 = null, IUser player6 = null)
    {
        player1 ??= Context.User;
        await StartGame(0, player1, player2, player3, player4, player5, player6, "aram");
    }


    [Command("stb")]
    [Summary("запуск игры")]
    public async Task StartGame(string mode = "Bot", uint times = 1000)
    {
        if (mode != "ShowResult" && mode != "Normal" && mode != "Bot")
        {
            await SendMessageAsync(
                $"Mode **{mode}** is not supported. Only **ShowResult**, **Bot** and **Normal** are available");
            return;
        }

        if (times > 10 && mode == "ShowResult")
        {
            await SendMessageAsync($"Mode **{mode}** Can be executed only 10 times. {times} is too much.");
            return;
        }


        for (var jj = 0; jj < times; jj++)
        {
            var players = new List<IUser>
            {
                null,
                null,
                null,
                null,
                null,
                null
            };


            //Заменить игрока на бота
            foreach (var player in players.Where(p => p != null)) _helperFunctions.SubstituteUserWithBot(player.Id);

            //получаем gameId
            var gameId = _global.GetNewtGamePlayingAndId();

            //ролл персонажей для игры
            var playersList = _startGameLogic.HandleCharacterRoll(players, gameId, mode:"bot");


            //тасуем игроков
            playersList = playersList.OrderBy(_ => Guid.NewGuid()).ToList();
            playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
            playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);

            //выдаем место в таблице
            for (var i = 0; i < playersList.Count; i++) playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);


            //создаем игру
            var game = new GameClass(playersList, gameId, Context.User.Id, 300, mode) { IsCheckIfReady = false };
            
            //отправить меню игры
            foreach (var player in playersList) await _upd.WaitMess(player, game);

            game.TestFightNumber = times;

            //это нужно для ботов
            game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

            //start the timer
            game.TimePassed.Start();
            _global.GamesList.Add(game);


            //handle predict
            if (mode == "Bot")
            {
                foreach (var player in game.PlayersList)
                {
                    foreach (var enemy in game.PlayersList.Where(x => x.GetPlayerId() != player.GetPlayerId()))
                    {
                        player.Predict.Add(new PredictClass(enemy.GameCharacter.Name, enemy.GetPlayerId()));
                    }
                }
            }

            //handle round #0
            await _characterPassives.HandleNextRound(game);

            game.IsCheckIfReady = true;
        }
    }


    [Command("Сложность")]
    [Alias("difficulty", "casual", "normal")]
    public async Task DifficultyChange()
    {
        var account = _accounts.GetAccount(Context.User);

        if (account.PlayerType == 0)
        {
            account.PlayerType = 1;
            await SendMessageAsync("Готово. Ваша сложность теперь \"**Казуальная**\".\n" +
                                   "Эта сложность показывает больше информации, чем в обычноый сложности. Она упрощает механику **предположений**.\n" +
                                   "Для смены сложности, просто напишите `*Сложность`");
        }
        else if (account.PlayerType == 1)
        {
            account.PlayerType = 0;
            await SendMessageAsync("Готово. Ваша сложность теперь \"**Обычная**\".\n" +
                                   "Эта сложность показывает столько информации, сколько было задумано разработчиками.\n" +
                                   "Для смены сложности, просто напишите `*Сложность`");
        }
    }

    [Command("время")]
    [Alias("uptime")]
    [Summary("Статистика бота")]
    public async Task UpTime()
    {
        _global.TimeSpendOnLastMessage.TryGetValue(Context.User.Id, out var watch);

        var time = DateTime.Now - _global.TimeBotStarted;

        var embed = new EmbedBuilder()
            // .WithAuthor(Context.Client.CurrentUser)
            // .WithTitle("My internal statistics")
            .WithColor(Color.DarkGreen)
            .WithCurrentTimestamp()
            .WithDescription("**Циферки:**\n" +
                             $"Работает: {time.Days}д {time.Hours}ч {time.Minutes}м + {time:ss\\.fff}с\n" +
                             $"Всего команд: {_global.TotalCommandsIssued}\n" +
                             $"Всего команд изменено: {_global.TotalCommandsChanged}\n" +
                             $"Всего команд удалено: {_global.TotalCommandsDeleted}\n" +    
                             $"Всего команд в памяти: {_commandsInMemory.CommandList.Count} (max {_commandsInMemory.MaximumCommandsInRam})\n" +
                             $"Задержка клиента: {_global.Client.Latency}\n" +
                             $"Время потрачено на обработку этой команды: {watch?.Elapsed:m\\:ss\\.ffff}s\n" +
                             "(Время считается с момента получения команды бота не включая задержки клиента и божественное вмешательство)\n");

        await SendMessageAsync(embed);
        // Context.User.GetAvatarUrl()
    }


    [Command("myPrefix")]
    [Summary("Показывает или меняет твой личный префикс этому боту.")]
    public async Task SetMyPrefix([Remainder] string prefix = null)
    {
        var account = _accounts.GetAccount(Context.User);

        if (prefix == null)
        {
            var myAccountPrefix = account.MyPrefix ?? "You don't have own prefix yet";

            await SendMessageAsync(
                $"Your prefix: **{myAccountPrefix}**");
            return;
        }

        if (prefix.Length < 100)
        {
            account.MyPrefix = prefix;
            if (prefix.Contains("everyone") || prefix.Contains("here"))
            {
                await SendMessageAsync(
                    "No `here` or `everyone` prefix allowed.");
                return;
            }


            await SendMessageAsync(
                $"Your own prefix is now **{prefix}**");
        }
        else
        {
            await SendMessageAsync(
                "Prefix have to be less than 100 characters");
        }
    }


    [Command("stats")]
    [Summary("Персональные статы")]
    public async Task GameStatsPersonal(IUser user = null)
    {
        await SendMessageAsync(_startGameLogic.GetStatsEmbed(_accounts.GetAccount(user?.Id ?? Context.User.Id)));
    }


    [Command("stats")]
    [Summary("Персональные статы")]
    public async Task GameStatsPersonal(ulong id)
    {
        await SendMessageAsync(_startGameLogic.GetStatsEmbed(_accounts.GetAccount(id)));
    }


    public async Task StartGame(int teamCount = 0, IUser player1 = null, IUser player2 = null, IUser player3 = null,
    IUser player4 = null, IUser player5 = null, IUser player6 = null, string mode = "normal")
    {
        var players = new List<IUser>
        {
            player1,
            player2,
            player3,
            player4,
            player5,
            player6
        };

        if (players.Contains(_global.Client.CurrentUser))
        {
            await SendMessageAsync("https://upload.wikimedia.org/wikipedia/commons/c/cc/Digital_rain_animation_medium_letters_shine.gif");
            return;
        }

        foreach (var player in players.Where(player => player != null).Where(player => player.IsBot))
        {
            await SendMessageAsync($"{player.Mention} незарегистрированный бот. По поводу франшизы пишите разработчикам игры!");
            return;
        }

        foreach (var player in players.Where(player => player != null && player.Id != Context.User.Id))
            if (_accounts.GetAccount(player.Id).IsPlaying)
            {
                await SendMessageAsync($"{player.Mention} сейчас играет! Подождите его!");
                return;
            }


        //Заменить игрока на бота
        foreach (var player in players.Where(p => p != null)) _helperFunctions.SubstituteUserWithBot(player.Id);

        //получаем gameId
        var gameId = _global.GetNewtGamePlayingAndId();

        //ролл персонажей для игры
        var playersList = new List<GamePlayerBridgeClass>();

        switch (mode)
        {
            case "normal":
                playersList = _startGameLogic.HandleCharacterRoll(players, gameId, teamCount);
                break;
            case "aram":
                playersList = _startGameLogic.HandleAramRoll(players, gameId);
                foreach (var player in playersList)
                {
                    player.Status.MoveListPage = 5;
                }
                foreach (var player in playersList.Where(x => x.DiscordId <= 1000000))
                {
                    player.Status.IsAramRollConfirmed = true;
                }
                break;
        }

        //тасуем игроков
        playersList = playersList.OrderBy(_ => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        var isDraftPick = API.Services.WebGameService.EnableDraftPick && mode == "normal";
        if (mode != "aram" && !isDraftPick)
        {
            playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);
        }
        //выдаем место в таблице
        for (var i = 0; i < playersList.Count; i++) playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);


        //командная игра
        var teamPool = new List<GamePlayerBridgeClass>
            { playersList[0], playersList[1], playersList[2], playersList[3], playersList[4], playersList[5] };
        var team1 = new GameClass.TeamPlay(1);
        var team2 = new GameClass.TeamPlay(2);
        var team3 = new GameClass.TeamPlay(3);
        var teamList = new List<GameClass.TeamPlay> { team1, team2, team3 };
        var count = 0;
        var teamId = 1;
        switch (teamCount)
        {
            case 2:
                foreach (var player in players)
                {
                    count++;
                    if (count > 2)
                        teamId = 2;
                    if (count > 4)
                        teamId = 3;
                    if (player != null)
                    {
                        var teamPooler = teamPool.Find(x => x.DiscordId == player.Id);
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                }

                break;
            case 3:
                foreach (var player in players)
                {
                    count++;
                    if (count > 3)
                        teamId = 2;
                    if (player != null)
                    {
                        var teamPooler = teamPool.Find(x => x.DiscordId == player.Id);
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                }

                break;
            case 4:
                foreach (var player in players)
                {
                    count++;
                    if (count > 2)
                        teamId = 2;
                    if (player != null)
                    {
                        var teamPooler = teamPool.Find(x => x.DiscordId == player.Id);
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPooler.TeamId = teamId;
                        teamPool.Remove(teamPooler);
                    }
                }

                break;
        }

        //Суппорт + Sirinoks team bias: ~22% chance to land on the same team
        if (teamCount > 0)
        {
            var support = playersList.Find(x => x.GameCharacter.Name == "Таинственный Суппорт");
            var sirinoks = playersList.Find(x => x.GameCharacter.Name == "Sirinoks");
            if (support != null && sirinoks != null)
            {
                var supportTeam = teamList.Find(x => x.TeamPlayers.Contains(support.GetPlayerId()));
                var sirinoksTeam = teamList.Find(x => x.TeamPlayers.Contains(sirinoks.GetPlayerId()));
                if (supportTeam != null && sirinoksTeam != null
                    && supportTeam.TeamId != sirinoksTeam.TeamId
                    && Random.Shared.Next(0, 100) < 22)
                {
                    var swapId = supportTeam.TeamPlayers.FirstOrDefault(id => id != support.GetPlayerId());
                    if (swapId != default)
                    {
                        var swapIdx = supportTeam.TeamPlayers.IndexOf(swapId);
                        var swapUsername = supportTeam.TeamPlayersUsernames[swapIdx];
                        var swapPlayer = playersList.Find(x => x.GetPlayerId() == swapId);

                        supportTeam.TeamPlayers.Remove(swapId);
                        supportTeam.TeamPlayersUsernames.Remove(swapUsername);
                        sirinoksTeam.TeamPlayers.Remove(sirinoks.GetPlayerId());
                        sirinoksTeam.TeamPlayersUsernames.Remove(sirinoks.DiscordUsername);

                        supportTeam.TeamPlayers.Add(sirinoks.GetPlayerId());
                        supportTeam.TeamPlayersUsernames.Add(sirinoks.DiscordUsername);
                        sirinoksTeam.TeamPlayers.Add(swapId);
                        sirinoksTeam.TeamPlayersUsernames.Add(swapUsername);

                        sirinoks.TeamId = supportTeam.TeamId;
                        if (swapPlayer != null) swapPlayer.TeamId = sirinoksTeam.TeamId;
                    }
                }
            }
        }

        if (teamCount > 0)
            foreach (var teamPayer in playersList)
            {
                var playerTeam = teamList.Find(x => x.TeamPlayers.Any(y => y == teamPayer.GetPlayerId()));

                foreach (var teamMemberId in playerTeam.TeamPlayers)
                {
                    var teamMember = playersList.Find(x => x.GetPlayerId() == teamMemberId);
                    if (teamPayer.GetPlayerId() == teamMember.GetPlayerId()) continue;

                    teamPayer.Predict.Add(new PredictClass(teamMember.GameCharacter.Name, teamMember.GetPlayerId()));
                }
            }

        //создаем игру
        var game = new GameClass(playersList, gameId, Context.User.Id) { IsCheckIfReady = false };
        if (mode == "aram")
        {
            game.IsAramPickPhase = true;
            game.TurnLengthInSecond = 600;
            game.GameMode = "Aram";
        }
        if (isDraftPick)
        {
            game.IsDraftPickPhase = true;
            var allAssigned = playersList.Select(x => x.GameCharacter).ToList();
            // NOTE: Can't use IsBot() here — SocketGameMessage is still null before WaitMess
            foreach (var player in playersList.Where(p => p.PlayerType != 404))
            {
                var account = _accounts.GetAccount(player.DiscordId);
                if (account == null) continue;
                // Include the player's natural roll as first option, then roll 2 more alternatives
                var originalCharacter = player.GameCharacter;
                var options = _startGameLogic.RollDraftOptions(account, allAssigned, 2);
                options.Insert(0, originalCharacter);
                game.DraftOptions[player.GetPlayerId()] = options;
                player.Status.MoveListPage = 6;
            }
            foreach (var p in playersList.Where(p => p.PlayerType == 404))
                p.Status.IsDraftPickConfirmed = true;
        }

        //отправить меню игры
        foreach (var player in playersList) await _upd.WaitMess(player, game);


        //это нужно для ботов
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

        //добавляем команды
        foreach (var team in teamList.Where(x => x.TeamPlayers.Count > 0)) game.Teams.Add(team);

        //start the timer
        game.TimePassed.Start();
        _global.GamesList.Add(game);


        //handle round #0
        if (mode == "normal" && !isDraftPick)
        {
            await _characterPassives.HandleNextRound(game);
            _characterPassives.HandleBotPredict(game);
        }

        foreach (var player in playersList) await _upd.UpdateMessage(player);

        // Send web link to each human player (deleted on next round)
        foreach (var player in playersList)
            await _helperFunctions.SendMsgAndDeleteItAfterRound(player, $"🌐 https://kotgh.ozvmusic.com/game/{gameId}", 0);

        game.IsCheckIfReady = true;
    }

}