using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.BotFramework.Extensions;
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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.


        private readonly UserAccounts _accounts;
        private readonly SecureRandom _secureRandom;
        private readonly OctoPicPull _octoPicPull;
        private readonly OctoNamePull _octoNmaNamePull;
        private readonly CharactersPull _charactersPull;
        private readonly HelperFunctions _helperFunctions;
        private readonly CharacterPassives _characterPassives;

        private readonly CommandsInMemory _commandsInMemory;
        private readonly Global _global;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _upd;
        private readonly CharactersUniquePhrase _phrase;


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
                SendMessAsync(
                    "Ты больше не увидишь название пассивок под сообщением с логами. Заюзай жту команду еще раз, чтобы их видить. Пример - https://i.imgur.com/R4JkRZR.png");
            }
            else
            {
                account.IsLogs = true;
                SendMessAsync(
                    "Ты видишь название пассивок под сообщением с логами.  Заюзай жту команду еще раз, чтобы их **НЕ** видить. Пример - https://i.imgur.com/eFvjRf5.png");
            }

          
        }



        [Command("setType")]
        [Summary("setting type of account: player, admin")]
        public async Task SetType(SocketUser user, string userType)
        {
            userType = userType.ToLower();
            var account = _accounts.GetAccount(user);

            if(userType != "admin" && userType != "player") {
                   SendMessAsync($"**admin** OR **player** only available options");
                return; 
                }
            if(Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193) {
                 SendMessAsync("only owners can use this command");
                return;
                }

            account.UserType = userType;

            SendMessAsync($"done. {user.Username} is now **{userType}**");
        }


        [Command("sr")]
        [Summary("Select round 1-10")]
        public async Task SelectRound(int roundNo)
        {
            if(roundNo < 1 || roundNo > 10) return;

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordAccount.DiscordId == Context.User.Id));

            if (game == null) return;

            game.RoundNo = roundNo;

            foreach (var t in game.PlayersList)
            {
               await _upd.UpdateMessage(t);
            }
        }


        [Command("SetScore")]
        [Summary("Set your score")]
        public async Task SetScore(int number)
        {

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordAccount.DiscordId == Context.User.Id));

            if (game == null) return;


            game.PlayersList.Find(x => x.DiscordAccount.DiscordId == Context.User.Id).Status.SetScoreToThisNumber(number);

            foreach (var t in game.PlayersList)
            {
                await _upd.UpdateMessage(t);
            }
        }


        [Command("set")]
        [Summary("Set a stat (in, sp, st, ps)")]
        public async Task SetCharacteristic(string name, int number)
        {
            if (number < 1 || number > 10) return;

            var game = _global.GamesList.Find(
                l => l.PlayersList.Any(x => x.DiscordAccount.DiscordId == Context.User.Id));

            if (game == null) return;


            switch (name.ToLower())
            {

                case "in":
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == Context.User.Id).Character.SetIntelligence(number);
                    break;
                case "sp":
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == Context.User.Id).Character.SetSpeed(number);
                    break;
                case "st":
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == Context.User.Id).Character.SetStrength(number);
                    break;
                case "ps":
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == Context.User.Id).Character.SetPsyche(number);
                    break;
                default:
                    return;
            }
            

            foreach (var t in game.PlayersList)
            {
                await _upd.UpdateMessage(t);
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
                    _helperFunctions.DeleteMessOverTime(lll, 6);
                    break;
                }
                case 9:
                {
                    var lll = await Context.Channel.SendMessageAsync("I'm drawing an octopus :3");
                    _helperFunctions.DeleteMessOverTime(lll, 6);
                    break;
                }
                case 26:
                {
                    var lll = await Context.Channel.SendMessageAsync(
                        "Oh, this is New Year! time to gift turtles!!");
                    _helperFunctions.DeleteMessOverTime(lll, 6);
                    break;
                }
            }
        }

        [Command("b")]
        public async Task StartGameTestBotVsBot()
        {
            for (var k = 0; k < 100; k++)
            {
                var rawList = new List<SocketUser>
                {
                    null,
                    null,
                    null,
                    null,
                    null,
                    null
                };
                var playersList = new List<GamePlayerBridgeClass>();


                var count = rawList.Count;
                var gameId = _global.GetNewtGamePlayingAndId();

                for (var i = 0; i < count; i++)
                {
                    var account = _helperFunctions.GetFreeBot(playersList, gameId);
                    playersList.Add(account);
                }


                //randomize order
                playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();

                for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;
                //end  randomize order

                var game = new GameClass(playersList, gameId);


                //HardKitty unique (should be last in order, always
                if (game.PlayersList.Any(x => x.Character.Name == "HardKitty"))
                {
                    var tempHard = game.PlayersList.Find(x => x.Character.Name == "HardKitty");
                    var hardIndex = game.PlayersList.IndexOf(tempHard);

                    for (var i = hardIndex; i < game.PlayersList.Count - 1; i++)
                        game.PlayersList[i] = game.PlayersList[i + 1];

                    game.PlayersList[game.PlayersList.Count - 1] = tempHard;
                }

                //end HardKitty unique


                //send non bot users  a wait message
                Parallel.ForEach(playersList, async player => { await _upd.WaitMess(player); });


                //start the timer
                game.TimePassed.Start();
                _global.GamesList.Add(game);


                //get all the chances before the game starts
                _characterPassives.CalculatePassiveChances(game);

                //handle round #1
                await _characterPassives.HandleNextRound(game);
                // await Task.Delay(1000);
            }
        }


        /*
Царь мусорный горы:
Текстовая, пошаговая игра, базирующаяся на локальных мемах. Более двадцати уникальных персонажей с особенными статами и неповторимыми пассивками. Сложная тактическая система боя. Перехитри их всех!

         */


        /*
Шести игрокам выпадают случайне персонажи с уникальными статами и пассивками, каждый из которых может атаковать выбранную цель или обороняться. Победы приносят игрокам очки, передвигая их вверх по таблице. Побеждает лидирующий игрок по истечению десяти ходов.
Важно Напишите в чат /rules для получения описания боевой системы.
         */

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

                [Command("st")]
        [Summary("запуск игры")]
        public async Task StartGameTest(SocketUser tolya = null)
        {
            _helperFunctions.SubstituteUserWithBot(Context.User.Id);


            var rawList = new List<SocketUser>
            {
                null,
                null,
                null,
                null,
                null
            };
            var playersList = new List<GamePlayerBridgeClass>();


            var accountDeep = _accounts.GetAccount(Context.User);
            accountDeep.GameId = _global.GetNewtGamePlayingAndId();
            accountDeep.IsPlaying = true;

            var characters = _charactersPull.GetAllCharacters();
            var characterDeep = characters[_secureRandom.Random(0, characters.Count()-1)]; //TODO: should be random someday 
            characters.Remove(characterDeep);
            playersList.Add(new GamePlayerBridgeClass
                {DiscordAccount = accountDeep, Character = characterDeep, Status = new InGameStatus()});

            if (tolya != null)
            {
                var tolAccount = _accounts.GetAccount(tolya);
                tolAccount.GameId = accountDeep.GameId;
                tolAccount.IsPlaying = true;
                var characterTolya  = characters[_secureRandom.Random(0, characters.Count()-1)];
                characters.Remove(characterTolya);
                playersList.Add(new GamePlayerBridgeClass
                {
                    DiscordAccount = tolAccount, Character = characterTolya,
                    Status = new InGameStatus()
                });
            }


            var count = rawList.Count;
            if (tolya != null) count--;

            for (var i = 0; i < count; i++)
            {
                var user = rawList[i];

                if (user != null) continue;

                var account = _helperFunctions.GetFreeBot(playersList, accountDeep.GameId);

                playersList.Add(account);
            }


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
                    x.GameId == tigrTemp.DiscordAccount.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

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
            foreach (var player in playersList) await _upd.WaitMess(player);


            var game = new GameClass(playersList, accountDeep.GameId) {IsCheckIfReady = false};

            //vampyr unique
            if (playersList.Any(x => x.Character.Name == "Вампур"))
            {
                _phrase.VampyrVampyr.SendLog(playersList.Find(x => x.Character.Name == "Вампур"));
                if (playersList.Any(x => x.Character.Name == "mylorik"))
                    game.AddPreviousGameLogs(
                        $" \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
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



        [Command("st")]
        [Summary("запуск игры")]
        public async Task StartGameTest(int charIndex1, SocketUser tolya = null, int charIndex2 = 0)
        {
            _helperFunctions.SubstituteUserWithBot(Context.User.Id);


            var rawList = new List<SocketUser>
            {
                null,
                null,
                null,
                null,
                null
            };
            var playersList = new List<GamePlayerBridgeClass>();


            var accountDeep = _accounts.GetAccount(Context.User);
            accountDeep.GameId = _global.GetNewtGamePlayingAndId();
            accountDeep.IsPlaying = true;

            var characters = _charactersPull.GetAllCharacters();
            var characterDeep = characters[charIndex1]; //TODO: should be random someday 
            playersList.Add(new GamePlayerBridgeClass
                {DiscordAccount = accountDeep, Character = characterDeep, Status = new InGameStatus()});

            if (tolya != null)
            {
                var tolAccount = _accounts.GetAccount(tolya);
                tolAccount.GameId = accountDeep.GameId;
                tolAccount.IsPlaying = true;

                playersList.Add(new GamePlayerBridgeClass
                {
                    DiscordAccount = tolAccount, Character =characters[charIndex2],
                    Status = new InGameStatus()
                });
            }


            var count = rawList.Count;
            if (tolya != null) count--;

            for (var i = 0; i < count; i++)
            {
                var user = rawList[i];

                if (user != null) continue;

                var account = _helperFunctions.GetFreeBot(playersList, accountDeep.GameId);

                playersList.Add(account);
            }


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
                    x.GameId == tigrTemp.DiscordAccount.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

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
            foreach (var player in playersList) await _upd.WaitMess(player);


            var game = new GameClass(playersList, accountDeep.GameId) {IsCheckIfReady = false};

            //vampyr unique
            if (playersList.Any(x => x.Character.Name == "Вампур"))
            {
                _phrase.VampyrVampyr.SendLog(playersList.Find(x => x.Character.Name == "Вампур"));
                if (playersList.Any(x => x.Character.Name == "mylorik"))
                    game.AddPreviousGameLogs(
                        $" \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
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

        // OWNER COMMANDS:


        //yes, this is my shit.
        [Command("es")]
        [Summary("CAREFUL! Better not to use it, ever.")]
        [RequireOwner]
        public async Task Asdasd()
        {
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
        }


        [Command("updMaxRam")]
        [RequireOwner]
        [Summary(
            "updates maximum number of commands bot will save in memory (default 1000 every time you launch this app)")]
        public async Task ChangeMaxNumberOfCommandsInRam(uint number)
        {
            _commandsInMemory.MaximumCommandsInRam = number;
            SendMessAsync($"now I will store {number} of commands");
        }

        [Command("clearMaxRam")]
        [RequireOwner]
        [Summary("CAREFUL! This will delete ALL commands in ram")]
        public async Task ClearCommandsInRam()
        {
            var toBeDeleted = _commandsInMemory.CommandList.Count;
            _commandsInMemory.CommandList.Clear();
            SendMessAsync($"I have deleted {toBeDeleted} commands");
        }
    }
}