using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class General : ModuleBaseCustom
{
    private readonly UserAccounts _accounts;
    private readonly CharacterPassives _characterPassives;
    private readonly CharactersPull _charactersPull;

    private readonly CommandsInMemory _commandsInMemory;
    private readonly InGameGlobal _gameGlobal;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;


    private readonly SecureRandom _secureRandom;
    private readonly GameUpdateMess _upd;
    private readonly CheckIfReady _checkIfReady;


    public General(UserAccounts accounts, SecureRandom secureRandom,
        HelperFunctions helperFunctions, CommandsInMemory commandsInMemory,
        Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
        CharactersUniquePhrase phrase, InGameGlobal gameGlobal, CheckIfReady checkIfReady)
    {
        _accounts = accounts;
        _secureRandom = secureRandom;
        _helperFunctions = helperFunctions;
        _commandsInMemory = commandsInMemory;
        _global = global;
        _upd = upd;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
        _gameGlobal = gameGlobal;
        _checkIfReady = checkIfReady;
    }


    public int GetRangeFromTier(int tier)
    {
        switch (tier)
        {
            case 6:
                return 100;
            case 5:
                return 50;
            case 4:
                return 40;
            case 3:
                return 30;
            case 2:
                return 20;
            case 1:
                return 10;
            default:
                return 0;
        }
    }


    public List<GamePlayerBridgeClass> HandleCharacterRoll(List<IUser> players, ulong gameId, int team = 0)
    {
        var allCharacters2 = _charactersPull.GetAllCharacters();
        var allCharacters = _charactersPull.GetAllCharacters();
        if (team > 0)
        {
            allCharacters2 = allCharacters2.Where(x => x.Name != "HardKitty").ToList();
            allCharacters = allCharacters.Where(x => x.Name != "HardKitty").ToList();
        }
        var reservedCharacters = new List<CharacterClass>();
        var playersList = new List<GamePlayerBridgeClass>();


        players = players.OrderBy(x => Guid.NewGuid()).ToList();

        //handle custom selected character part #1
        foreach (var character in from player in players
                 where player != null
                 select _accounts.GetAccount(player)
                 into account
                 where account.CharacterToGiveNextTime != null
                 select allCharacters.Find(x => x.Name == account.CharacterToGiveNextTime))
        {
            reservedCharacters.Add(character);
            allCharacters.Remove(character);
        }
        //end


        foreach (var account in players.Select(player => player != null
                     ? _accounts.GetAccount(player.Id)
                     : _helperFunctions.GetFreeBot(playersList)))
        {
            account.IsPlaying = true;

            try
            {
                if (!account.IsBot())
                {
                    var temp = players.Where(x => x != null).ToList().Find(x => x.Id == account.DiscordId);
                    if(temp != null)
                        account.DiscordUserName = temp.Username;
                }
            }
            catch
            {
                //ignored
            }
            

            //выдать персонажей если их нет на аккаунте
            foreach (var character in from character in allCharacters2
                     let knownCharacter = account.CharacterChance.Find(x => x.CharacterName == character.Name)
                     where knownCharacter == null
                     select character)
                account.CharacterChance.Add(new DiscordAccountClass.CharacterChances(character.Name, character.Tier));
            //end

            //handle custom selected character part #2
            if (account.CharacterToGiveNextTime != null)
            {
                playersList.Add(new GamePlayerBridgeClass
                {
                    Character = reservedCharacters.Find(x => x.Name == account.CharacterToGiveNextTime),
                    Status = new InGameStatus(reservedCharacters.Find(x => x.Name == account.CharacterToGiveNextTime).Name),
                    DiscordId = account.DiscordId,
                    GameId = gameId,
                    DiscordUsername = account.DiscordUserName,
                    PlayerType = account.PlayerType
                });
                account.CharacterPlayedLastTime = account.CharacterToGiveNextTime;
                account.CharacterToGiveNextTime = null;
                continue;
            }
            //end

            var allAvailableCharacters = new List<DiscordAccountClass.CharacterRollClass>();
            var totalPool = 1;

            var allCharactersExclusive = allCharacters.Where(x => x.Name != account.CharacterPlayedLastTime).ToList();
            foreach (var character in allCharactersExclusive)
            {
                var range = GetRangeFromTier(character.Tier);
                if (character.Tier == 4 && account.IsBot()) range *= 3;
                var temp = totalPool + Convert.ToInt32(range * account.CharacterChance.Find(x => x.CharacterName == character.Name).Multiplier) - 1;
                allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                totalPool = temp + 1;
            }

            var randomIndex = _secureRandom.Random(1, totalPool-1);
            var rolledCharacter = allAvailableCharacters.Find(x => randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
            var characterToAssign = allCharactersExclusive.Find(x => x.Name == rolledCharacter.CharacterName);

            switch (characterToAssign.Name)
            {
                case "LeCrisp":
                {
                    var characterToRemove = allCharactersExclusive.Find(x => x.Name == "Толя");
                    if (characterToRemove != null)
                        allCharacters.Remove(characterToRemove);
                    break;
                }
                case "Толя":
                {
                    var characterToRemove = allCharactersExclusive.Find(x => x.Name == "LeCrisp");
                    if (characterToRemove != null)
                        allCharacters.Remove(characterToRemove);
                    break;
                }
            }

            playersList.Add(new GamePlayerBridgeClass
            {
                Character = characterToAssign,
                Status = new InGameStatus(characterToAssign.Name),
                DiscordId = account.DiscordId,
                GameId = gameId,
                DiscordUsername = account.DiscordUserName,
                PlayerType = account.PlayerType
            });
            account.CharacterPlayedLastTime = characterToAssign.Name;
            allCharacters.Remove(characterToAssign);
        }

        //Добавить персонажа в магазин человека
        foreach (var player in players)
        {
            if (player == null) continue;
            var account = _accounts.GetAccount(player);

            foreach (var playerInGame in playersList.Where(playerInGame =>
                         !account.SeenCharacters.Contains(playerInGame.Character.Name)))
                if (playerInGame.DiscordId == player.Id)
                    account.SeenCharacters.Add(playerInGame.Character.Name);
        }

        return playersList;
    }


    public EmbedBuilder GetStatsEmbed(DiscordAccountClass account)
    {
        var embed = new EmbedBuilder();
        var mostWins = account.CharacterStatistics.OrderByDescending(x => x.Wins).ToList().ElementAtOrDefault(0);
        var leastWins = account.CharacterStatistics.OrderByDescending(x => x.Wins)
            .ElementAtOrDefault(account.CharacterStatistics.Count - 1);
        var mostPlays = account.CharacterStatistics.OrderByDescending(x => x.Plays).ElementAtOrDefault(0);
        var leastPlays = account.CharacterStatistics.OrderByDescending(x => x.Plays)
            .ElementAtOrDefault(account.CharacterStatistics.Count - 1);
        var mostPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place).ElementAtOrDefault(0);
        var leastPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place)
            .ElementAtOrDefault(account.PerformanceStatistics.Count - 1);
        var topPoints = account.MatchHistory.OrderByDescending(x => x.Score).ElementAtOrDefault(0);
        var mostChance = account.CharacterChance.OrderByDescending(x => x.Multiplier).ElementAtOrDefault(0);
        var leastChance = account.CharacterChance.OrderByDescending(x => x.Multiplier)
            .ElementAtOrDefault(account.CharacterChance.Count - 1);

        ulong totalPoints = 0;

        foreach (var v in account.MatchHistory)
            if (v.Score > 0)
                totalPoints += (ulong)v.Score;
            else
                totalPoints += (ulong)(v.Score * -1);

        embed.WithAuthor(Context.User);
        // embed.WithDescription("буль-буль");

        embed.AddField("ZBS Points", $"{account.ZbsPoints}", true);
        embed.AddField("Тип Пользователя", $"{account.PlayerType}", true);
        embed.AddField("Всего Игр", $"{account.TotalPlays}", true);
        embed.AddField("Всего Топ 1", $"{account.TotalWins}", true);

        if (totalPoints > 0)
            embed.AddField("Среднее количество очков за игру",
                $"{totalPoints / account.TotalWins} - ({totalPoints}/{account.TotalWins})");
        if (topPoints != null)
            embed.AddField("Больше всего очков за игру",
                $"{topPoints.CharacterName} - {topPoints.Score} (#{topPoints.Place}) {topPoints.Date.Month}.{topPoints.Date.Day}.{topPoints.Date.Year}",
                true);
        if (mostWins != null)
            embed.AddField("Больше всего побед", $"{mostWins.CharacterName} - {mostWins.Wins}/{mostWins.Plays}",
                true);
        if (leastWins != null)
            embed.AddField("Меньше всего побед", $"{leastWins.CharacterName} - {leastWins.Wins}/{leastWins.Plays}",
                true);
        if (mostPlays != null)
            embed.AddField("Больше всего игр", $"{mostPlays.CharacterName} - {mostPlays.Wins}/{mostPlays.Plays}",
                true);
        if (leastPlays != null)
            embed.AddField("Меньше всего игр", $"{leastPlays.CharacterName} - {leastPlays.Wins}/{leastPlays.Plays}",
                true);
        if (mostPlace != null)
            embed.AddField("Самое частое место", $"Топ {mostPlace.Place} - {mostPlace.Times}/{account.TotalPlays}",
                true);
        if (leastPlace != null)
            embed.AddField("Самое редкое место",
                $"Топ {leastPlace.Place} - {leastPlace.Times}/{account.TotalPlays}",
                true);
        if (mostChance != null)
            embed.AddField("Самый большой шанс",
                $"{mostChance.CharacterName} - {mostChance.Multiplier} ",
                true);
        if (leastChance != null)
            embed.AddField("Самый маленький шанс",
                $"{leastChance.CharacterName} - {leastChance.Multiplier} ",
                true);

        embed.WithFooter("циферки");
        embed.WithCurrentTimestamp();

        return embed;
    }


    public async Task <List<GamePlayerBridgeClass>> HandleEventsBeforeFirstRound(List<GamePlayerBridgeClass> playersList)
    {
        //Загадочный Спартанец в маске
        if (playersList.Any(x => x.Character.Name == "Загадочный Спартанец в маске"))
        {
            var spartan = playersList.Find(x => x.Character.Name == "Загадочный Спартанец в маске");
            spartan.Character.SetSkillMultiplier(1);
        }
        //end Загадочный Спартанец в маске

        //Никому не нужен
        if (playersList.Any(x => x.Character.Name == "HardKitty"))
        {
            var tempHard = playersList.Find(x => x.Character.Name == "HardKitty");
            tempHard.Status.HardKittyMinus(-20, "Никому не нужен");
            tempHard.Status.AddInGamePersonalLogs("Никому не нужен: -20 *Морали*\n");
            var hardIndex = playersList.IndexOf(tempHard);

            for (var i = hardIndex; i < playersList.Count - 1; i++)
                playersList[i] = playersList[i + 1];

            playersList[^1] = tempHard;
        }
        //end Никому не нужен


        //Тигр топ, а ты холоп
        if (playersList.Any(x => x.Character.Name == "Тигр"))
        {
            var tigrTemp = playersList.Find(x => x.Character.Name == "Тигр");

            var tigr = _gameGlobal.TigrTop.Find(x =>
                x.GameId == tigrTemp.GameId && x.PlayerId == tigrTemp.GetPlayerId());

            if (tigr != null && tigr.TimeCount > 0)
            {
                var tigrIndex = playersList.IndexOf(tigrTemp);

                playersList[tigrIndex] = playersList.First();
                playersList[0]= tigrTemp;
                tigr.TimeCount--;
                //game.Phrases.TigrTop.SendLog(tigrTemp);
            }
        }
        //Тигр топ, а ты холоп

        //Дерзкая школота
        if (playersList.Any(x => x.Character.Name == "Mit*suki*"))
        {
            var mitsukiTemp = playersList.Find(x => x.Character.Name == "Mit*suki*");
            mitsukiTemp.Character.AddExtraSkill(mitsukiTemp.Status,  100, "Дерзкая школота");
        }


        if (playersList.Any(x => x.Character.Name == "Mit*suki*"))
        {
            var tempHard = playersList.Find(x => x.Character.Name == "Mit*suki*");
            var hardIndex = playersList.IndexOf(tempHard);

            playersList[hardIndex] = playersList.First();
            playersList[0] = tempHard;
        }
        //end Дерзкая школота


        //Повторяет за myloran
        if (playersList.Any(x => x.Character.Name == "mylorik"))
        {
            var mylorikTemp = playersList.Find(x => x.Character.Name == "mylorik");
            mylorikTemp.Character.AddStrength(mylorikTemp.Status, (mylorikTemp.Character.GetStrength()*-1)+3, "Повторяет за myloran", true);
        }
        //end Повторяет за myloran

        return playersList;
    }


    public async Task StartGame(int teamCount = 0, IUser player1 = null, IUser player2 = null, IUser player3 = null, IUser player4 = null, IUser player5 = null, IUser player6 = null)
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
            await SendMessageAsync($"https://upload.wikimedia.org/wikipedia/commons/c/cc/Digital_rain_animation_medium_letters_shine.gif");
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
        var playersList = HandleCharacterRoll(players, gameId, teamCount);

        //тасуем игроков
        playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        playersList = await HandleEventsBeforeFirstRound(playersList);

        //выдаем место в таблице
        for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;

        //это нужно для ботов
        _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

        //командная игра
        var teamPool = new List<GamePlayerBridgeClass> { playersList[0], playersList[1], playersList[2], playersList[3], playersList[4], playersList[5] };
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
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
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
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
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
                        teamPool.Remove(teamPooler);
                    }
                    else
                    {
                        var teamPooler = teamPool.First(x => x.IsBot());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayers.Add(teamPooler.GetPlayerId());
                        teamList.Find(x => x.TeamId == teamId)?.TeamPlayersUsernames.Add(teamPooler.DiscordUsername);
                        teamPool.Remove(teamPooler);
                    }
                }
                break;
        }

        if (teamCount > 0)
        {
            foreach (var teamPayer in playersList)
            {
                var playerTeam = teamList.Find(x => x.TeamPlayers.Any(y => y == teamPayer.GetPlayerId()));

                foreach (var teamMemberId in playerTeam.TeamPlayers)
                {
                    var teamMember = playersList.Find(x => x.GetPlayerId() == teamMemberId);
                    if (teamPayer.GetPlayerId() == teamMember.GetPlayerId()) continue;

                    teamPayer.Predict.Add(new PredictClass(teamMember.Character.Name, teamMember.GetPlayerId()));
                }
            }
        }

        //отправить меню игры
        foreach (var player in playersList) await _upd.WaitMess(player, playersList);

        //создаем игру
        var game = new GameClass(playersList, gameId, Context.User.Id) { IsCheckIfReady = false };

        //добавляем команды
        foreach (var team in teamList.Where(x => x.TeamPlayers.Count > 0))
        {
            game.Teams.Add(team);
        }

        //start the timer
        game.TimePassed.Start();
        _global.GamesList.Add(game);

        //get all the chances before the game starts
        _gameGlobal.CalculatePassiveChances(game);

        //handle round #0
        await _characterPassives.HandleNextRound(game);

        foreach (var player in playersList) await _upd.UpdateMessage(player);
        game.IsCheckIfReady = true;
    }



    [Command("игра")]
    [Alias("st", "start", "start game")]
    [Summary("запуск игры")]
    public async Task StartGameNormal(IUser player1 = null, IUser player2 = null, IUser player3 = null, IUser player4 = null,
        IUser player5 = null, IUser player6 = null)
    {
        player1 ??= Context.User;
        await StartGame(0, player1, player2, player3, player4, player5, player6);
    }

    [Command("игра")]
    [Alias("st", "start", "start game")]
    [Summary("запуск игры")]
    public async Task StartGameTeam(int team, IUser player1 = null, IUser player2 = null, IUser player3 = null, IUser player4 = null,
        IUser player5 = null, IUser player6 = null)
    {
        player1 ??= Context.User;
        await StartGame(team, player1, player2, player3, player4, player5, player6);
    }



    [Command("stb")]
    [Summary("запуск игры")]
    public async Task StartGame(string mode = "ShowResult", uint times = 10)
    {
        if (mode != "ShowResult" && mode != "Normal")
        {
            await SendMessageAsync($"Mode **{mode}** is not supported. Only **ShowResult** and **Normal** are available");
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
            var playersList = HandleCharacterRoll(players, gameId);


            //тасуем игроков
            playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
            playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
            playersList = await HandleEventsBeforeFirstRound(playersList);

            //выдаем место в таблице
            for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;

            //это нужно для ботов
            _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

            //отправить меню игры
            foreach (var player in playersList) await _upd.WaitMess(player, playersList);

            //создаем игру
            var game = new GameClass(playersList, gameId, Context.User.Id, 300, mode) { IsCheckIfReady = false };


            //start the timer
            game.TimePassed.Start();
            _global.GamesList.Add(game);

            //get all the chances before the game starts
            _gameGlobal.CalculatePassiveChances(game);

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
            .WithFooter("Версия: 2.0")
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
        await SendMessageAsync(GetStatsEmbed(_accounts.GetAccount(user?.Id ?? Context.User.Id)));
    }


    [Command("stats")]
    [Summary("Персональные статы")]
    public async Task GameStatsPersonal(ulong id)
    {
        await SendMessageAsync(GetStatsEmbed(_accounts.GetAccount(id)));
    }
}