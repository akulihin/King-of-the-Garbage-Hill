using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class General : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;
        private readonly CharacterPassives _characterPassives;
        private readonly CharactersPull _charactersPull;

        private readonly CommandsInMemory _commandsInMemory;
        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly HelperFunctions _helperFunctions;
        private readonly OctoNamePull _octoNmaNamePull;
        private readonly OctoPicPull _octoPicPull;
        private readonly CharactersUniquePhrase _phrase;
        private readonly SecureRandom _secureRandom;
        private readonly GameUpdateMess _upd;


        public General(UserAccounts accounts, SecureRandom secureRandom, OctoPicPull octoPicPull,
            OctoNamePull octoNmaNamePull, HelperFunctions helperFunctions, CommandsInMemory commandsInMemory,
            Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
            CharactersUniquePhrase phrase, InGameGlobal gameGlobal)
        {
            _accounts = accounts;
            _secureRandom = secureRandom;
            _octoPicPull = octoPicPull;
            _octoNmaNamePull = octoNmaNamePull;
            _helperFunctions = helperFunctions;
            _commandsInMemory = commandsInMemory;
            _global = global;
            _upd = upd;
            _charactersPull = charactersPull;
            _characterPassives = characterPassives;
            _phrase = phrase;
            _gameGlobal = gameGlobal;
        }

        [Command("show logs names")]
        [Alias("sln", "sin")]
        [Summary(
            "default is \'true\'. Если \'true\' Ты видишь название пассивок под сообщением с логами. Заюзай эту команду чтобы поменять на \'false\' и обратно\n" +
            "Логи это уникальные фразы при активации определенной пасивки персонажа")]
        public async Task ChangeLogsState()
        {
            var account = _accounts.GetAccount(Context.User);
            if (account.IsLogs)
            {
                account.IsLogs = false;
                await SendMessAsync(
                    "Ты больше не увидишь название пассивок под сообщением с логами. Заюзай жту команду еще раз, чтобы их видить. Пример - https://i.imgur.com/R4JkRZR.png");
            }
            else
            {
                account.IsLogs = true;
                await SendMessAsync(
                    "Ты видишь название пассивок под сообщением с логами.  Заюзай жту команду еще раз, чтобы их **НЕ** видить. Пример - https://i.imgur.com/eFvjRf5.png");
            }
        }


        [Command("SetType")]
        [Summary("setting type of account: player, admin")]
        public async Task SetType(SocketUser user, string userType)
        {
            userType = userType.ToLower();
            var account = _accounts.GetAccount(user);

            if (userType != "admin" && userType != "player")
            {
                await SendMessAsync("**admin** OR **player** only available options");
                return;
            }

            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            account.UserType = userType;

            await SendMessAsync($"done. {user.Username} is now **{userType}**");
        }


        [Command("SetRound")]
        [Alias("sr")]
        [Summary("Select round 1-10")]
        public async Task SelectRound(int roundNo)
        {
            if (roundNo < 1 || roundNo > 10) return;

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

            if (game == null) return;

            game.RoundNo = roundNo;

            foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
        }


        [Command("SetScore")]
        [Summary("Set your score")]
        public async Task SetScore(int number)
        {
            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

            if (game == null) return;


            game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Status
                .SetScoreToThisNumber(number);

            foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
        }


        [Command("SetStat")]
        [Alias("set")]
        [Summary("Set a stat (in, sp, st, ps)")]
        public async Task SetCharacteristic(string name, int number)
        {
            if (number < 1 || number > 10) return;

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

            if (game == null) return;


            switch (name.ToLower())
            {
                case "in":
                    game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Character
                        .SetIntelligence(number);
                    break;
                case "sp":
                    game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Character
                        .SetSpeed(number);
                    break;
                case "st":
                    game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Character
                        .SetStrength(number);
                    break;
                case "ps":
                    game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Character
                        .SetPsyche(number);
                    break;
                default:
                    return;
            }


            foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
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

            await SendMessAsync(embed);
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

                await SendMessAsync(
                    $"Your prefix: **{myAccountPrefix}**");
                return;
            }

            if (prefix.Length < 100)
            {
                account.MyPrefix = prefix;
                if (prefix.Contains("everyone") || prefix.Contains("here"))
                {
                    await SendMessAsync(
                        "No `here` or `everyone` prefix allowed.");
                    return;
                }


                await SendMessAsync(
                    $"Your own prefix is now **{prefix}**");
            }
            else
            {
                await SendMessAsync(
                    "Prefix have to be less than 100 characters");
            }
        }


        [Command("octo")]
        [Alias("окто", "octopus", "Осьминог", "Осьминожка", "Осьминога", "o", "oct", "о")]
        [Summary("Куда же без осьминожек")]
        public async Task OctopusPicture()
        {
            var octoIndex = _secureRandom.Random(0, _octoPicPull.OctoPics.Length - 1);
            var octoToPost = _octoPicPull.OctoPics[octoIndex];


            var color1Index = _secureRandom.Random(0, 255);
            var color2Index = _secureRandom.Random(0, 255);
            var color3Index = _secureRandom.Random(0, 255);

            var randomIndex = _secureRandom.Random(0, _octoNmaNamePull.OctoNameRu.Length - 1);
            var randomOcto = _octoNmaNamePull.OctoNameRu[randomIndex];

            var embed = new EmbedBuilder();
            embed.WithDescription($"{randomOcto} found:");
            embed.WithFooter("lil octo notebook");
            embed.WithColor(color1Index, color2Index, color3Index);
            embed.WithAuthor(Context.User);
            embed.WithImageUrl("" + octoToPost);

            await SendMessAsync(embed);

            switch (octoIndex)
            {
                case 19:
                {
                    var lll = await Context.Channel.SendMessageAsync("Ooooo, it was I who just passed Dark Souls!");
#pragma warning disable 4014
                    _helperFunctions.DeleteMessOverTime(lll, 6);
#pragma warning restore 4014
                    break;
                }
                case 9:
                {
                    var lll = await Context.Channel.SendMessageAsync("I'm drawing an octopus :3");
#pragma warning disable 4014
                    _helperFunctions.DeleteMessOverTime(lll, 6);
#pragma warning restore 4014
                    break;
                }
                case 26:
                {
                    var lll = await Context.Channel.SendMessageAsync(
                        "Oh, this is New Year! time to gift turtles!!");
#pragma warning disable 4014
                    _helperFunctions.DeleteMessOverTime(lll, 6);
#pragma warning restore 4014
                    break;
                }
            }
        }


        [Command("BotGame")]
        [Alias("bot")]
        [Summary("запуск игры для ботов")]
        public async Task StartGameTestBotVsBot()
        {
            for (var uhg = 0; uhg < 10000; uhg++)
            {
                for (var k = 0; k < 100; k++)
                {
                    _helperFunctions.SubstituteUserWithBot(Context.User.Id);

                    var players = new List<IUser> {null};

                    var gameId = _global.GetNewtGamePlayingAndId();

                    var f = 6 - players.Count;
                    for (var i = 0; i < f; i++) players.Add(null);

                    var playersList = HandleCharacterRoll(players, gameId);


                    ////////////////////////////////////////////////////// FIRST SORTING/////////////////////////////////////////////////
                    //randomize order
                    playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
                    playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();

                    //HardKitty unique
                    if (playersList.Any(x => x.Character.Name == "HardKitty"))
                    {
                        var tempHard = playersList.Find(x => x.Character.Name == "HardKitty");
                        var hardIndex = playersList.IndexOf(tempHard);

                        for (var i = hardIndex; i < playersList.Count - 1; i++)
                            playersList[i] = playersList[i + 1];

                        playersList[playersList.Count - 1] = tempHard;
                    }
                    //end //HardKitty unique


                    //Tigr Unique
                    if (playersList.Any(x => x.Character.Name == "Тигр"))
                    {
                        var tigrTemp = playersList.Find(x => x.Character.Name == "Тигр");

                        var tigr = _gameGlobal.TigrTop.Find(x =>
                            x.GameId == tigrTemp.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

                        if (tigr != null && tigr.TimeCount > 0)
                        {
                            var tigrIndex = playersList.IndexOf(tigrTemp);

                            playersList[tigrIndex] = playersList[0];
                            playersList[0] = tigrTemp;
                            tigr.TimeCount--;
                            //await _phrase.TigrTop.SendLog(tigrTemp);
                        }
                    }
                    //end Tigr Unique

                    //sort
                    for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;
                    //end sorting
                    //////////////////////////////////////////////////////END FIRST SORTING/////////////////////////////////////////////////

                    _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

                    //send  a wait message
                    foreach (var player in playersList) await _upd.WaitMess(player, playersList);


                    var game = new GameClass(playersList, gameId) {IsCheckIfReady = false};

                    //vampyr unique
                    if (playersList.Any(x => x.Character.Name == "Вампур"))
                    {
                        _phrase.VampyrVampyr.SendLog(playersList.Find(x => x.Character.Name == "Вампур"));
                        if (playersList.Any(x => x.Character.Name == "mylorik"))
                            game.AddPreviousGameLogs(
                                " \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
                                "\n\n", false);
                    }
                    //end vampyr unique


                    //start the timer
                    game.TimePassed.Start();
                    _global.GamesList.Add(game);


                    //get all the chances before the game starts
                    _characterPassives.CalculatePassiveChances(game);

                    //handle round #0

                    await _characterPassives.HandleNextRound(game);


                    foreach (var player in playersList) await _upd.UpdateMessage(player);

                    game.IsCheckIfReady = true;
                }

                await Task.Delay(100000);
            }
        }


        [Command("rules")]
        [Summary("Правила игры")]
        public async Task Rules()
        {
            var gameRules = "**Правила игры:**\n" +
                            "Шести игрокам выпадает случайный персонаж. Игрокам не известно против кого они играют. Каждый ход игрок может напасть на кого-то, либо обороняться. " +
                            "В случае нападения игрок либо побеждает, получая очко, либо проигрывает, приносят очко врагу. В случае нападения на обороняющегося игрока, бой не состоится и нападающий потеряет 1 __бонусное__ очко и 1 Справедливости. Обороняющийся получит +1 Справедливости.\n" +
                            "\n" +
                            "**Бой:**\n" +
                            "У всех персонажей есть 4 стата, чтобы победить в бою нужно выиграть по двум из трех пунктов:\n" +
                            "1) статы \n" +
                            "2) справедливость\n" +
                            "3) случайность \n" +
                            "\n" +
                            "1 - В битве статов немалую роль играет Контр - превосходящий стат (если ваш персонаж превосходит врага например в интеллекте, то ваш персонаж умнее). Умный персонаж побеждает Быстрого, Быстрый Сильного, а Сильный Умного.\n" +
                            "Второстепенную роль играет разница в общей сумме статов. Разница в Психике дополнительно дает небольшое преимущество.\n" +
                            "2 - Проигрывая, персонажи получают +1 справедливости (максимум 5), при победе они полностью ее теряют. Во втором пункте побеждает тот, у кого больше справедливости на момент сражения.\n" +
                            "3 - Обычный рандом, который чуть больше уважает СЛИШКОМ превосходящих игроков по первому пункту." +
                            "\n" +
                            "Очки напрямую влияют на место в таблице. Начиная с 5го хода,  все  получаемые очки, кроме __бонусных__, умножаются на 2, на 10ом ходу очки умножаются на 4.\n" +
                            "После каждого хода обновляется таблица лидеров, побеждает лучший игрок после 10и ходов.\n" +
                            "После каждого второго хода игрок может улучшить один из статов на +1.\n" +
                            "У каждого персонажа есть особые пассивки, используйте их как надо!";

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            embed.WithDescription(gameRules);


            await Context.User.SendMessageAsync("", false, embed.Build());
        }


        public List<GamePlayerBridgeClass> HandleCharacterRoll(List<IUser> players, ulong gameId)
        {
            var allCharacters = _charactersPull.GetAllCharacters();
            var playersList = new List<GamePlayerBridgeClass>();

            //shuffle player list
            players = players.OrderBy(x => Guid.NewGuid()).ToList();

            foreach (var player in players)
            {
                var account = player != null
                    ? _accounts.GetAccount(player.Id)
                    : _helperFunctions.GetFreeBot(playersList);

                account.IsPlaying = true;
                var tempCharacterChances = account.CharacterChance.ConvertAll(x =>
                    new DiscordAccountClass.CharacterChances(x.CharacterName, x.CharacterChanceMin,
                        x.CharacterChanceMax, x.Multiplier));

                foreach (var tempCharacterChance in tempCharacterChances)
                    if (tempCharacterChance.Multiplier > 1 || tempCharacterChance.Multiplier < 1)
                    {
                        var tempMax = tempCharacterChance.CharacterChanceMax;
                        var tempMin = tempCharacterChance.CharacterChanceMin;

                        var tempRealChance = tempMax - tempMin + 1;

                        var tempMultipliedChance = Convert.ToInt32(tempRealChance * tempCharacterChance.Multiplier);

                        if (tempRealChance == tempMultipliedChance) continue;

                        var diff = tempMultipliedChance - tempRealChance;


                        foreach (var t in tempCharacterChances.Where(x =>
                            x.CharacterChanceMax > tempCharacterChance.CharacterChanceMax))
                        {
                            t.CharacterChanceMax += diff;
                            t.CharacterChanceMin += diff;
                        }

                        tempCharacterChance.CharacterChanceMax += diff;
                    }

                while (true)
                {
                    var r = _secureRandom.Random(1,
                        tempCharacterChances.OrderByDescending(x => x.CharacterChanceMax).ToList()[0]
                            .CharacterChanceMax);
                    var cr = tempCharacterChances.Find(x => r >= x.CharacterChanceMin && r <= x.CharacterChanceMax);
                    var character = allCharacters.Find(x => x.Name == cr.CharacterName);
                    if (character == null) continue;
                    playersList.Add(new GamePlayerBridgeClass
                    {
                        Character = character, Status = new InGameStatus(), DiscordId = account.DiscordId,
                        GameId = gameId, DiscordUsername = account.DiscordUserName, IsLogs = account.IsLogs,
                        UserType = account.UserType
                    });
                    allCharacters.Remove(character);
                    break;
                }
            }


            return playersList;
        }


        [Command("start")]
        [Alias("st", "start game")]
        [Summary("запуск игры")]
        public async Task StartGameTest(IUser socketPlayer2 = null)
        {
            _helperFunctions.SubstituteUserWithBot(Context.User.Id);

            var players = new List<IUser> {Context.User};
            if (socketPlayer2 != null) players.Add(socketPlayer2);
            var gameId = _global.GetNewtGamePlayingAndId();

            var f = 6 - players.Count;
            for (var i = 0; i < f; i++) players.Add(null);

            var playersList = HandleCharacterRoll(players, gameId);


            ////////////////////////////////////////////////////// FIRST SORTING/////////////////////////////////////////////////
            //randomize order
            playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
            playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();

            //HardKitty unique
            if (playersList.Any(x => x.Character.Name == "HardKitty"))
            {
                var tempHard = playersList.Find(x => x.Character.Name == "HardKitty");
                var hardIndex = playersList.IndexOf(tempHard);

                for (var i = hardIndex; i < playersList.Count - 1; i++)
                    playersList[i] = playersList[i + 1];

                playersList[playersList.Count - 1] = tempHard;
            }
            //end //HardKitty unique


            //Tigr Unique
            if (playersList.Any(x => x.Character.Name == "Тигр"))
            {
                var tigrTemp = playersList.Find(x => x.Character.Name == "Тигр");

                var tigr = _gameGlobal.TigrTop.Find(x =>
                    x.GameId == tigrTemp.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

                if (tigr != null && tigr.TimeCount > 0)
                {
                    var tigrIndex = playersList.IndexOf(tigrTemp);

                    playersList[tigrIndex] = playersList[0];
                    playersList[0] = tigrTemp;
                    tigr.TimeCount--;
                    //await _phrase.TigrTop.SendLog(tigrTemp);
                }
            }
            //end Tigr Unique

            //sort
            for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;
            //end sorting
            //////////////////////////////////////////////////////END FIRST SORTING/////////////////////////////////////////////////

            _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

            //send  a wait message
            foreach (var player in playersList) await _upd.WaitMess(player, playersList);


            var game = new GameClass(playersList, gameId) {IsCheckIfReady = false};

            //vampyr unique
            if (playersList.Any(x => x.Character.Name == "Вампур"))
            {
                _phrase.VampyrVampyr.SendLog(playersList.Find(x => x.Character.Name == "Вампур"));
                if (playersList.Any(x => x.Character.Name == "mylorik"))
                    game.AddPreviousGameLogs(
                        " \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
                        "\n\n", false);
            }
            //end vampyr unique


            //start the timer
            game.TimePassed.Start();
            _global.GamesList.Add(game);


            //get all the chances before the game starts
            _characterPassives.CalculatePassiveChances(game);

            //handle round #0

            await _characterPassives.HandleNextRound(game);


            foreach (var player in playersList) await _upd.UpdateMessage(player);

            game.IsCheckIfReady = true;
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
                    totalPoints += (ulong) v.Score;
                else
                    totalPoints += (ulong) (v.Score * -1);

            embed.WithAuthor(Context.User);
            // embed.WithDescription("буль-буль");

            embed.AddField("ZBS Points", $"{account.ZbsPoints}", true);
            embed.AddField("Тип Пользователя", $"{account.UserType}", true);
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
                    $"{mostChance.CharacterName} - {mostChance.Multiplier} ({(mostChance.CharacterChanceMax - mostChance.CharacterChanceMin + 1) * mostChance.Multiplier})",
                    true);
            if (leastChance != null)
                embed.AddField("Самый маленький шанс",
                    $"{leastChance.CharacterName} - {leastChance.Multiplier} ({leastChance.CharacterChanceMax - leastChance.CharacterChanceMin + 1})",
                    true);

            embed.WithFooter("циферки");
            embed.WithCurrentTimestamp();

            return embed;
        }


        [Command("stats")]
        [Summary("Персональные статы")]
        public async Task GameStatsPersonal(IUser user = null)
        {
            await SendMessAsync(GetStatsEmbed(_accounts.GetAccount(user?.Id ?? Context.User.Id)));
        }

        [Command("inv")]
        public async Task Invite()
        {
            foreach (var guild in _global.Client.Guilds)
            {
                try
                {
                    await SendMessAsync($"{guild.Name}" +
                                        $"\n{guild.GetVanityInviteAsync().Result.Url}");
                }
                catch
                {
                    //
                }
            }
        }


        [Command("stats")]
        [Summary("Персональные статы")]
        public async Task GameStatsPersonal(ulong id)
        {
            await SendMessAsync(GetStatsEmbed(_accounts.GetAccount(id)));
        }


        [Command("top")]
        [Summary("Мировые статы")]
        public async Task GameStatsWorld()
        {
            var allAccounts = _accounts.GetAllAccount();
        }


        //yes, this is my shit.
        [Command("es")]
        [Summary("CAREFUL! Better not to use it, ever.")]
        [RequireOwner]
        public async Task Asdasd()
        {
            return;
            var allcha = new List<CharacterClass>();
            //  int cc = 0;  
            string ll;
            var at = "";

            var file = new StreamReader(@"DataBase/esy.txt");
            while ((ll = file.ReadLine()) != null)
                at += ll;
            //         cc++;  

            var c = at.Split("||");

            for (var i = 0; i < c.Length; i++)
            {
                var parts = c[i].Split("WW");
                var part1 = parts[0];
                var part2 = parts[1];

                var p = part1.Split("UU");
                var name = p[0];
                var t = p[1].Replace("Интеллект", " ");
                t = t.Replace("Сила", " ");
                t = t.Replace("Скорость", " ");
                t = t.Replace("Психика", " ");
                var hh = t.Split(" ");
                var oo = new int[4];
                var ind = 0;
                for (var mm = 0; mm < hh.Length; mm++)
                    if (hh[mm] != "")
                    {
                        oo[ind] = int.Parse(hh[mm]);
                        ind++;
                    }

                var intel = oo[0];
                var str = oo[1];

                var pe = oo[2];
                var psy = oo[3];
                allcha.Add(new CharacterClass(intel, str, pe, psy, name));
                var pass = new List<Passive>();
                var passives = part2.Split(":");
                for (var k = 0; k < passives.Length - 1; k++)
                {
                    pass.Add(new Passive(passives[k], passives[k + 1]));
                    k++;
                }

                allcha[allcha.Count - 1].Passive = pass;
            }


            var json = JsonConvert.SerializeObject(allcha.ToArray());
            File.WriteAllText(@"D:\characters.json", json);
            await Task.CompletedTask;
        }


        [Command("updMaxRam")]
        [RequireOwner]
        [Summary(
            "updates maximum number of commands bot will save in memory (default 1000 every time you launch this app)")]
        public async Task ChangeMaxNumberOfCommandsInRam(uint number)
        {
            _commandsInMemory.MaximumCommandsInRam = number;
            await SendMessAsync($"now I will store {number} of commands");
        }


        [Command("clearMaxRam")]
        [RequireOwner]
        [Summary("CAREFUL! This will delete ALL commands in ram")]
        public async Task ClearCommandsInRam()
        {
            var toBeDeleted = _commandsInMemory.CommandList.Count;
            _commandsInMemory.CommandList.Clear();
            await SendMessAsync($"I have deleted {toBeDeleted} commands");
        }
    }
}