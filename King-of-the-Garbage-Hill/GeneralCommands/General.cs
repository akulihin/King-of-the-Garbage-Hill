using System;
using System.Collections.Generic;
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

        
        private readonly SecureRandom _secureRandom;
        private readonly GameUpdateMess _upd;


        public General(UserAccounts accounts, SecureRandom secureRandom, 
             HelperFunctions helperFunctions, CommandsInMemory commandsInMemory,
            Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
            CharactersUniquePhrase phrase, InGameGlobal gameGlobal)
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


        public List<GamePlayerBridgeClass> HandleCharacterRoll(List<IUser> players, ulong gameId)
        {
            var allCharacters2 = _charactersPull.GetAllCharacters();
            var allCharacters = _charactersPull.GetAllCharacters();
            var reservedCharacters = new List<CharacterClass>();
            var playersList = new List<GamePlayerBridgeClass>();


            players = players.OrderBy(x => Guid.NewGuid()).ToList();

            //handle custom selected character part #1
            foreach (var character in from player in players where player != null select _accounts.GetAccount(player) into account where account.CharacterToGiveNextTime != null select allCharacters.Find(x => x.Name == account.CharacterToGiveNextTime))
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

                //выдать персонажей если их нет на аккаунте
                foreach (var character in from character in allCharacters2 let knownCharacter = account.CharacterChance.Find(x => x.CharacterName == character.Name) where knownCharacter == null select character)
                {
                    account.CharacterChance.Add(new DiscordAccountClass.CharacterChances(character.Name, character.Tier));
                }
                //end

                //handle custom selected character part #2
                if (account.CharacterToGiveNextTime != null)
                {
                    playersList.Add(new GamePlayerBridgeClass
                    {
                        Character = reservedCharacters.Find(x => x.Name == account.CharacterToGiveNextTime),
                        Status = new InGameStatus(),
                        DiscordId = account.DiscordId,
                        GameId = gameId,
                        DiscordUsername = account.DiscordUserName,
                        PlayerType = account.PlayerType
                    });
                    account.CharacterToGiveNextTime = null;
                    continue;
                }
                //end

                var allAvailableCharacters = new List<DiscordAccountClass.CharacterRollClass>();
                var totalPool = 1;
                
                foreach (var character in allCharacters)
                {
                    var range = GetRangeFromTier(character.Tier);
                    if (character.Tier == 4 && account.IsBot())
                    {
                        range *= 3;
                    }
                    var temp = totalPool + Convert.ToInt32(range * account.CharacterChance.Find(x => x.CharacterName == character.Name).Multiplier) - 1;
                    allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                    totalPool = temp + 1;
                }

                var randomIndex = _secureRandom.Random(1, totalPool);
                var rolledCharacter = allAvailableCharacters.Find(x => randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
                var characterToAssign = allCharacters.Find(x => x.Name == rolledCharacter.CharacterName);

                switch (characterToAssign.Name)
                {
                    case "LeCrisp":
                    {
                        var characterToRemove = allCharacters.Find(x => x.Name == "Толя");
                        if (characterToRemove != null)
                            allCharacters.Remove(characterToRemove);
                        break;
                    }
                    case "Толя":
                    {
                        var characterToRemove = allCharacters.Find(x => x.Name == "LeCrisp");
                        if (characterToRemove != null)
                            allCharacters.Remove(characterToRemove);
                        break;
                    }
                }

                playersList.Add(new GamePlayerBridgeClass
                {
                    Character = characterToAssign,
                    Status = new InGameStatus(),
                    DiscordId = account.DiscordId,
                    GameId = gameId,
                    DiscordUsername = account.DiscordUserName,
                    PlayerType = account.PlayerType
                });
                allCharacters.Remove(characterToAssign);
            }

            //Добавить персонажа в магазин человека
            foreach (var player in players)
            {
                if(player == null) continue;
                var account = _accounts.GetAccount(player);

                foreach (var playerInGame in playersList.Where(playerInGame => !account.SeenCharacters.Contains(playerInGame.Character.Name)))
                {
                    if(playerInGame.DiscordId == player.Id)
                        account.SeenCharacters.Add(playerInGame.Character.Name);
                }
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


        public List<GamePlayerBridgeClass> HandleEventsBeforeFirstRound(List<GamePlayerBridgeClass> playersList)
        {

            //Никому не нужен
            if (playersList.Any(x => x.Character.Name == "HardKitty"))
            {
                var tempHard = playersList.Find(x => x.Character.Name == "HardKitty");
                tempHard.Status.AddBonusPoints(-20, "Никому не нужен");
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
                    x.GameId == tigrTemp.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

                if (tigr != null && tigr.TimeCount > 0)
                {
                    var tigrIndex = playersList.IndexOf(tigrTemp);

                    playersList[tigrIndex] = playersList[0];
                    playersList[0] = tigrTemp;
                    tigr.TimeCount--;
                    //await game.Phrases.TigrTop.SendLog(tigrTemp);
                }
            }
            //Тигр топ, а ты холоп

            //Дерзкая школота
            if (playersList.Any(x => x.Character.Name == "Mit*suki*"))
            {
                var mitsukiTemp = playersList.Find(x => x.Character.Name == "Mit*suki*");
                mitsukiTemp.Character.AddExtraSkill(mitsukiTemp.Status, "Дерзкая школота: ", 100);
            }

  
            if (playersList.Any(x => x.Character.Name == "Mit*suki*"))
            {
                var tempHard = playersList.Find(x => x.Character.Name == "Mit*suki*");
                var hardIndex = playersList.IndexOf(tempHard);

                playersList[hardIndex] = playersList[0];
                playersList[0] = tempHard;
            }
            //end Дерзкая школота

            return playersList;
        }


        [Command("start")]
        [Alias("st", "start game")]
        [Summary("запуск игры")]
        public async Task StartGame(IUser player2 = null, IUser player3 = null, IUser player4 = null, IUser player5 = null, IUser player6 = null)
        {
            var players = new List<IUser>
            {
                Context.User,
                player2,
                player3,
                player4,
                player5,
                player6
            };

            foreach (var player in players.Where(player => player != null).Where(player => player.IsBot))
            {
                await SendMessAsync($"ERROR: {player.Mention}  is a bot!");
                return;
            }

            //Заменить игрока на бота
            foreach (var player in players.Where(p => p != null))
            {
                _helperFunctions.SubstituteUserWithBot(player.Id);
            }

            //получаем gameId
            var gameId = _global.GetNewtGamePlayingAndId();

            //ролл персонажей для игры
            var playersList = HandleCharacterRoll(players, gameId);


            //тасуем игроков
            playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
            playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
            playersList = HandleEventsBeforeFirstRound(playersList);

            //выдаем место в таблице
            for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;

            //это нужно для ботов
            _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

            //отправить меню игры
            foreach (var player in playersList) await _upd.WaitMess(player, playersList);

            //создаем игру
            var game = new GameClass(playersList, gameId) { IsCheckIfReady = false };


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


        [Command("start")]
        [Alias("st", "start game")]
        [Summary("запуск игры")]
        public async Task StartGameTestMode(int choice)
        {
            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            var allCharacters = _charactersPull.GetAllCharacters();

            if ( choice + 1 > allCharacters.Count)
            {
                await SendMessAsync($"ERROR: 404 - no such Character. Please select between 0 and {allCharacters.Count-1}");
                return;
            }

            var account = _accounts.GetAccount(Context.User);
            account.CharacterToGiveNextTime = allCharacters[choice].Name;

            var players = new List<IUser>
            {
                Context.User,
                null,
                null,
                null,
                null,
                null
            };

            //Заменить игрока на бота
            foreach (var player in players.Where(p => p != null))
            {
                _helperFunctions.SubstituteUserWithBot(player.Id);
            }

            //получаем gameId
            var gameId = _global.GetNewtGamePlayingAndId();

            //ролл персонажей для игры
            var playersList = HandleCharacterRoll(players, gameId);


            //тасуем игроков
            playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
            playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
            playersList = HandleEventsBeforeFirstRound(playersList);

            //выдаем место в таблице
            for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;

            //это нужно для ботов
            _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

            //отправить меню игры
            foreach (var player in playersList) await _upd.WaitMess(player, playersList);

            //создаем игру
            var game = new GameClass(playersList, gameId) { IsCheckIfReady = false };


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


        [Command("SetCharacter")]
        [Summary("set character to roll next game")]
        public async Task CharacterToGiveNextTime(string character, IUser player = null)
        {
            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            var allCharacters = _charactersPull.GetAllCharacters();
            var account = _accounts.GetAccount(Context.User);

            if (player != null)
            {
                account = _accounts.GetAccount(player);
            }

            var foundCharacter = allCharacters.Find(x => x.Name.ToLower() == character.ToLower());

            if (foundCharacter == null)
            {
                var extra = allCharacters.Aggregate("", (current, c) => current + $"`{c.Name}`\n");
                await SendMessAsync($"ERROR: 404 - No Such Character ({character})\nAvailable:\n{extra}");
                return;
            }

            account.CharacterToGiveNextTime = foundCharacter.Name;

            await SendMessAsync($"Done. {player.Mention} будет играть на {foundCharacter.Name} в следующей игре");
        }

        
        [Command("SetType")]
        [Summary("setting type of account: 0, 1, 2 ")]
        public async Task SetType(IUser user, int userType)
        {                    

            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            var account = _accounts.GetAccount(user);

            if (userType != 0 && userType != 1 && userType != 2)
            {
                await SendMessAsync("0 == **Normal**\n" +
                                    "1 == **Casual**\n" +
                                    "2 == **Admin**\n" +
                                    "are the only available options");
                return;
            }

            account.PlayerType = userType;
            var game = _global.GamesList.Find(x => x.PlayersList.Any(y => y.DiscordId == user.Id));
            if (game != null)
            {
                var playing  = game.PlayersList.Find(y => y.DiscordId == user.Id);
                playing.PlayerType = userType;
            }
            await SendMessAsync($"done. {user.Username} is now **{userType}**");
        }


        [Command("SetRound")]
        [Alias("sr")]
        [Summary("Select round 1-10")]
        public async Task SelectRound(int roundNo)
        {

            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            if (roundNo < 1 || roundNo > 10)
            {
                await SendMessAsync("select between 1 and 10");
                return;
            }

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
            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

            if (game == null) return;


            game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Status
                .SetScoreToThisNumber(number);

            foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
        }


        [Command("SetStat")]
        [Alias("set")]
        [Summary("Set a stat (in, sp, st, ps, js, sk, mr)")]
        public async Task SetCharacteristic(string name, int number)
        {
            if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
            {
                await SendMessAsync("only owners can use this command");
                return;
            }

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

            if (game == null) return;

            var player = game.PlayersList.Find(x => x.DiscordId == Context.User.Id);
            switch (name.ToLower())
            {
                case "in":
                    player.Character
                        .SetIntelligence(player.Status, number, "Читы: ");
                    break;
                case "sp":
                    player.Character
                        .SetSpeed(player.Status, number, "Читы: ");
                    break;
                case "st":
                    player.Character
                        .SetStrength(player.Status, number, "Читы: ");
                    break;
                case "ps":
                    player.Character
                        .SetPsyche(player.Status, number, "Читы: ");
                    break;
                case "js":
                    player.Character
                        .Justice.SetJusticeNow(player.Status, number, "Читы: ");
                    break;
                case "sk":
                    player.Character
                        .SetMainSkill(player.Status, number, "Читы: ");
                    break;
                case "mr":
                    player.Character
                        .SetMoral(player.Status, number, "Читы: ");
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



        [Command("stats")]
        [Summary("Персональные статы")]
        public async Task GameStatsPersonal(IUser user = null)
        {
            await SendMessAsync(GetStatsEmbed(_accounts.GetAccount(user?.Id ?? Context.User.Id)));
        }




        [Command("stats")]
        [Summary("Персональные статы")]
        public async Task GameStatsPersonal(ulong id)
        {
            await SendMessAsync(GetStatsEmbed(_accounts.GetAccount(id)));
        }


    }
}