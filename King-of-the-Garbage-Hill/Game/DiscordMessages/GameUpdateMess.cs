using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.DiscordMessages
{
    public sealed class GameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
    {
        private readonly UserAccounts _accounts;
        private readonly AwaitForUserMessage _awaitForUser;
        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly HelperFunctions _helperFunctions;
        private readonly LoginFromConsole _log;


        public GameUpdateMess(UserAccounts accounts, Global global, InGameGlobal gameGlobal,
            AwaitForUserMessage awaitForUser, HelperFunctions helperFunctions, LoginFromConsole log)
        {
            _accounts = accounts;
            _global = global;
            _gameGlobal = gameGlobal;
            _awaitForUser = awaitForUser;
            _helperFunctions = helperFunctions;
            _log = log;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public async Task ShowRulesAndChar(SocketUser user, GamePlayerBridgeClass player)
        {
            var pass = "";
            var passList = player.Character.Passive;
            for (var i = 0; i < passList.Count; i++)
            {
                pass += $"__**{passList[i].PassiveName}**__";
                pass += ": ";
                pass += passList[i].PassiveDescription;
                pass += "\n";
            }


            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            if (player.Character.Avatar != null)
                embed.WithImageUrl(player.Character.Avatar);
            embed.AddField("Твой Персонаж:", $"Name: {player.Character.Name}\n" +
                                             $"Интеллект: {player.Character.GetIntelligence()}\n" +
                                             $"Сила: {player.Character.GetStrength()}\n" +
                                             $"Скорость: {player.Character.GetSpeed()}\n" +
                                             $"Психика: {player.Character.GetPsyche()}\n");
            embed.AddField("Пассивки", $"{pass}");


            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(GamePlayerBridgeClass player, List<GamePlayerBridgeClass> players)
        {
            if (player.DiscordId <= 1000000) return;

            var globalAccount = _global.Client.GetUser(player.DiscordId);


            await ShowRulesAndChar(globalAccount, player);

            var mainPage = new EmbedBuilder();
            mainPage.WithAuthor(globalAccount);
            mainPage.WithFooter("Preparation time...");
            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Game is being ready", "**Please wait for the main menu**");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            player.Status.SocketMessageFromBot = socketMsg;

            //   await socketMsg.AddReactionAsync(new Emoji("➡"));
            // await socketMsg.AddReactionAsync(new Emoji("📖"));
            //   await socketMsg.AddReactionAsync(new Emoji("⬆"));
            //   await socketMsg.AddReactionAsync(new Emoji("8⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("9⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("🐙"));


            //    await MainPage(userId, socketMsg);
        }

        public string LeaderBoard(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            if (game == null) return "ERROR 404";
            var players = "";
            var playersList = game.PlayersList;

            for (var i = 0; i < playersList.Count; i++)
            {
                players += CustomLeaderBoardBeforeNumber(player, playersList[i], game, i + 1);

                players += $"{i + 1}. {playersList[i].DiscordUsername}";

                players += CustomLeaderBoardAfterPlayer(player, playersList[i], game);

                //TODO: REMOVE || playersList[i].IsBot()
                if (player.Status.PlayerId == playersList[i].Status.PlayerId)
                    players +=
                        $" = **{playersList[i].Status.GetScore()} Score**";


                players += "\n";
            }

            return players;
        }

        public string CustomLeaderBoardBeforeNumber(GamePlayerBridgeClass player1, GamePlayerBridgeClass player2,
            GameClass game, int number)
        {
            var customString = "";

            switch (player1.Character.Name)
            {
                case "Осьминожка":
                    var octoTentacles = _gameGlobal.OctopusTentaclesList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (!octoTentacles.LeaderboardPlace.Contains(number)) customString += "🐙";


                    break;

                case "Братишка":
                    var shark = _gameGlobal.SharkJawsLeader.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (!shark.FriendList.Contains(number)) customString += "🐙";
                    break;
            }

            return customString + " ";
        }

        public string CustomLeaderBoardAfterPlayer(GamePlayerBridgeClass player1, GamePlayerBridgeClass player2,
            GameClass game)
        {
            var customString = " ";
            //|| player1.DiscordId == 238337696316129280 || player1.DiscordId == 181514288278536193


            switch (player1.Character.Name)
            {
                case "AWDKA":
                    if (player2.Status.PlayerId == player1.Status.PlayerId) break;

                    var awdka = _gameGlobal.AwdkaTryingList.Find(x => x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);
                    var awdkaTrainingHistory = _gameGlobal.AwdkaTeachToPlayHistory.Find(x => x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    var awdkaTrying = awdka.TryingList.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                    if (awdkaTrying != null)
                    {
                        if (!awdkaTrying.IsUnique) customString += "<:bronze:565744159680626700>";
                        else customString += "<:plat:565745613208158233>";
                    }


                   
                    if (awdkaTrainingHistory != null)
                    {
                        var awdkaTrainingHistoryEnemy = awdkaTrainingHistory.History.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);
                        if (awdkaTrainingHistoryEnemy != null)
                        {
                            var statText = awdkaTrainingHistoryEnemy.Text switch
                            {
                                "1" => "Интеллект",
                                "2" => "Сила",
                                "3" => "Скорость",
                                "4" => "Психика",
                                _ => ""
                            };
                            customString += $" (**{statText} {awdkaTrainingHistoryEnemy.Stat}** ?)";
                        }
                    }
                    //(<:volibir:894286361895522434> сила 10 ?)


                        break;
                case "Братишка":
                    var shark = _gameGlobal.SharkJawsWin.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);
                    if (!shark.FriendList.Contains(player2.Status.PlayerId) &&
                        player2.Status.PlayerId != player1.Status.PlayerId)
                        customString += "<:jaws:565741834219945986>";
                    break;

                case "Darksci":
                    var dar = _gameGlobal.DarksciLuckyList.Find(x =>
                        x.GameId == game.GameId &&
                        x.PlayerId == player1.Status.PlayerId);

                    if (!dar.TouchedPlayers.Contains(player2.Status.PlayerId) &&
                        player2.Status.PlayerId != player1.Status.PlayerId)
                        customString += "<:Darksci:565598465531576352>";


                    break;
                case "Вампур":
                    var vamp = _gameGlobal.VampyrKilledList.Find(x =>
                        x.GameId == player1.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (vamp != null)
                        if (!vamp.FriendList.Contains(player2.Status.PlayerId) &&
                            player2.Status.PlayerId != player1.Status.PlayerId)
                            customString += "<:Y_:562885385395634196>";
                    break;

                case "HardKitty":
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == player1.GameId &&
                        x.PlayerId == player1.Status.PlayerId);
                    var lostSeries = hardKitty?.LostSeries.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);
                    if (lostSeries != null)
                        customString += $"<:393:563063205811847188> - {lostSeries.Series}";
                    break;
                case "Sirinoks":
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == player1.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (siri != null)
                        if (!siri.FriendList.Contains(player2.Status.PlayerId) &&
                            player2.Status.PlayerId != player1.Status.PlayerId)
                            customString += "<:fr:563063244097585162>";
                    break;
                case "Загадочный Спартанец в маске":
                {
                    var panthShame = _gameGlobal.PanthShame.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);
                    if (!panthShame.FriendList.Contains(player2.Status.PlayerId) &&
                        player2.Status.PlayerId != player1.Status.PlayerId)
                        customString += "<:yasuo:895819754428833833>";
                }
                {
                    var panthMark = _gameGlobal.PanthMark.Find(x =>
                        x.GameId == player1.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (panthMark.FriendList.Contains(player2.Status.PlayerId))
                        customString += "<:sparta:561287745675329567>";
                }

                    break;


                case "DeepList":

                    //tactic
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && player1.GameId == x.GameId);
                    if (deep != null)
                        if (deep.FriendList.Contains(player2.Status.PlayerId) &&
                            player2.Status.PlayerId != player1.Status.PlayerId)
                            customString += "<:yo:561287783704952845>";
                    //end tactic

                    //сверхразум
                    var currentList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && x.GameId == player1.GameId);
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(player2.Status.PlayerId))
                            customString +=
                                $"PS: - {player2.Character.Name} (I: {player2.Character.GetIntelligence()} | " +
                                $"St: {player2.Character.GetStrength()} | Sp: {player2.Character.GetSpeed()} | " +
                                $"Ps: {player2.Character.GetPsyche()} | J: {player2.Character.Justice.GetJusticeNow()})";
                    //end сверхразум


                    //стёб
                    var currentDeepList =
                        _gameGlobal.DeepListMockeryList.Find(x =>
                            x.PlayerId == player1.Status.PlayerId && game.GameId == x.GameId);

                    if (currentDeepList != null)
                    {
                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                        if (currentDeepList2 != null)
                        {
                            if (currentDeepList2.Times % 2 == 1)
                                customString += "**лол**";
                            else
                                customString += "**кек**";
                        }
                    }
    
                    //end стёб


                    break;

                case "mylorik":
                    var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                        x.GameId == player1.GameId && x.PlayerId == player1.Status.PlayerId);
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == player2.Status.PlayerId);

                    if (find != null && find.IsUnique) customString += "<:sparta:561287745675329567>";
                    if (find != null && !find.IsUnique) customString += "❌";

                    break;
                case "Тигр":
                    var tigr1 = _gameGlobal.TigrTwoBetterList.Find(x => x.PlayerId == player1.Status.PlayerId && x.GameId == player1.GameId);

                    if (tigr1 != null)
                        //if (tigr1.FriendList.Contains(player2.Status.PlayerId) && player2.Status.PlayerId != player1.Status.PlayerId)
                        if (tigr1.FriendList.Contains(player2.Status.PlayerId))
                            customString += "<:pepe_down:896514760823144478>";

                    var tigr2 = _gameGlobal.TigrThreeZeroList.Find(x => x.GameId == player1.GameId && x.PlayerId == player1.Status.PlayerId);

                    var enemy = tigr2?.FriendList.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                    if (enemy != null)
                    {
                        if (enemy.WinsSeries == 1)
                            customString += "1:0";
                        else if (enemy.WinsSeries == 2)
                            customString += "2:0";
                        else if (enemy.WinsSeries == 3) customString += "3:0, обоссан";
                    }

                    break;
            }

            if (game.RoundNo == 11 || player1.UserType == "admin")
            {
                customString += $"(as **{player2.Character.Name}**) = {player2.Status.GetScore()} Score";
                customString +=
                    $"(I: {player2.Character.GetIntelligence()} | St: {player2.Character.GetStrength()} | Sp: {player2.Character.GetSpeed()} | Ps: {player2.Character.GetPsyche()})";
            }

            return customString;
        }

        public async Task EndGame(SocketMessageComponent button)
        {
            _helperFunctions.SubstituteUserWithBot(button.User.Id);
            var globalAccount = _global.Client.GetUser(button.User.Id);
            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;


            //  await socketMsg.DeleteAsync();
            await globalAccount.SendMessageAsync("Thank you for playing!");
        }

        //Page 1
        public EmbedBuilder FightPage(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var character = player.Character;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Blue);
            embed.WithTitle("King of the Garbage Hill");
            var roundNumber = game.RoundNo;

       
            if (roundNumber > 10)
            {
                roundNumber = 10;
            }

            var multiplier = roundNumber switch
            {
                <= 4 => 1,
                <= 9 => 2,
                _ => 4
            };
            //Претендент русского сервера
            if (player.Status.GetInGamePersonalLogs().Contains("Претендент русского сервера"))
            {
                multiplier *= 3;
            }
            //end Претендент русского сервера

           game = _global.GamesList.Find(x => x.GameId == player.GameId);


            var desc = game.GetPreviousGameLogs();

            embed.WithDescription(desc.Replace(player.DiscordUsername,
                $"**{player.DiscordUsername}**"));

            if (desc.Length >= 2048)
                _global.Client.GetUser(181514288278536193).CreateDMChannelAsync().Result
                    .SendMessageAsync("PreviousGameLogs >= 2048");

            embed.AddField("<:e_:562879579694301184>",
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"**Интеллект:** {character.GetIntelligenceString()}\n" +
                $"**Сила:** {character.GetStrengthString()}\n" +
                $"**Скорость:** {character.GetSpeedString()}\n" +
                $"**Психика:** {character.GetPsycheString()}\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"*Справедливость: {character.Justice.GetJusticeNow()}\n" +
                $"Мораль: {character.GetMoral()}\n" +
                $"Скилл: {character.GetSkill()} (Мишень: **{character.GetCurrentSkillTarget()}**)*\n" +
                $"Множитель очков: **X{multiplier}**\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                "<:e_:562879579694301184>\n" +
                $"{LeaderBoard(player)}" +
                "<:e_:562879579694301184>");


            var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");
            if (game != null && splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
                embed.AddField("События прошлого раунда:",
                    $"{splitLogs[^2]}<:e_:562879579694301184>");
            else
                embed.AddField("События прошлого раунда:",
                    "В прошлом раунде ничего не произошло. Странно...\n<:e_:562879579694301184>");

            embed.AddField("События этого раунда:",
                player.Status.GetInGamePersonalLogs().Length >= 2
                    ? $"{player.Status.GetInGamePersonalLogs()}"
                    : "Еще ничего не произошло. Наверное...");

            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        //Page 2
        public EmbedBuilder LogsPage(GamePlayerBridgeClass player)
        {
            var account = _accounts.GetAccount(player.DiscordId);


            var game = _global.GamesList.Find(x => x.GameId == player.GameId);

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription(game.GetAllGameLogs());
            embed.WithColor(Color.Green);
            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.WithCurrentTimestamp();

            return embed;
        }

        //Page 3
        public EmbedBuilder LvlUpPage(GamePlayerBridgeClass player)
        {
            //    var status = player.Status;
            var account = _accounts.GetAccount(player.DiscordId);
            var character = player.Character;

            //   status.MoveListPage = 3;
            //    _accounts.SaveAccounts(discordAccount.PlayerDiscordId);

            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == player.GameId).GetPreviousGameLogs();
            if (desc == null)
                return null;
            embed.WithDescription(desc.Replace(player.DiscordUsername,
                $"**{player.DiscordUsername}**"));

            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.WithCurrentTimestamp();
            embed.AddField("_____",
                "__Подними один из статов на 1:__\n \n" +
                $"1. **Интеллект:** {character.GetIntelligence()}\n" +
                $"2. **Сила:** {character.GetStrength()}\n" +
                $"3. **Скорость:** {character.GetSpeed()}\n" +
                $"4. **Психика:** {character.GetPsyche()}\n");

            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }


        public async Task UpdateMessage(GamePlayerBridgeClass player)
        {
            if (player.IsBot()) return;

            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var playerChoiceAttackList = new List<Emoji>
                {new("1⃣"), new("2⃣"), new("3⃣"), new("4⃣"), new("5⃣"), new("6⃣")};

            var embed = new EmbedBuilder();
            var playerIsReady = (player.Status.IsSkip || player.Status.IsReady) || game.RoundNo > 10;
            var attackMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("attack-select")
                .WithDisabled(playerIsReady)
                .WithPlaceholder("Выбор цели");

    

            if (game != null)
                for (var i = 0; i < playerChoiceAttackList.Count; i++)
                {
                    var playerToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == i + 1);
                    if (playerToAttack == null) continue;
                    if (playerToAttack.DiscordId != player.DiscordId)
                        attackMenu.AddOption(playerToAttack.DiscordUsername, $"{i + 1}",
                            emote: playerChoiceAttackList[i]);
                }


            var charMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("char-select")
                .WithPlaceholder("Выбор прокачки")
                .AddOption("Интеллект", "1")
                .AddOption("Сила", "2")
                .AddOption("Скорость", "3")
                .AddOption("Психика", "4");

            var builder = new ComponentBuilder();

            switch (player.Status.MoveListPage)
            {
                case 1:
                    embed = FightPage(player);
                    builder = new ComponentBuilder();

                    builder.WithButton("Блок", "block", row: 0, style: ButtonStyle.Success, disabled: playerIsReady);

                    builder.WithSelectMenu(attackMenu, 1);

                    if (game != null && game.RoundNo <= 10)
                    {
                        if (player.Character.GetMoral() >= 15)
                            builder.WithButton("Обменять 15 Морали на 15 бонусных очков", "moral", row: 0, style: ButtonStyle.Secondary);
                        else if (player.Character.GetMoral() >= 10)
                            builder.WithButton("Обменять 10 Морали на 8 бонусных очков", "moral", row: 0, style: ButtonStyle.Secondary);
                        else if (player.Character.GetMoral() >= 5)
                            builder.WithButton("Обменять 5 Морали на 2 бонусных очка", "moral", row: 0, style: ButtonStyle.Secondary);
                        else if (player.Character.GetMoral() >= 3)
                            builder.WithButton("Обменять 3 Морали на 1 бонусное очко", "moral", row: 0, style: ButtonStyle.Secondary);
                        else
                            builder.WithButton("Недостаточно очков морали", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                    }
                    else
                    {
                        if (player.Character.GetMoral() >= 15)
                            builder.WithButton("Обменять 15 Морали на 15 бонусных очков (Конец игры)", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                        else if (player.Character.GetMoral() >= 10)
                            builder.WithButton("Обменять 10 Морали на 8 бонусных очков (Конец игры)", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                        else if (player.Character.GetMoral() >= 5)
                            builder.WithButton("Обменять 5 Морали на 2 бонусных очка (Конец игры)", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                        else if (player.Character.GetMoral() >= 3)
                            builder.WithButton("Обменять 3 Морали на 1 бонусное очко (Конец игры)", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                        else
                            builder.WithButton("Недостаточно очков морали", "moral", row: 0, style: ButtonStyle.Secondary, disabled: true);
                    }

                    builder.WithButton("Завершить Игру", "end", row: 0, style: ButtonStyle.Danger);
                    break;
                case 2:
                    embed = LogsPage(player);
                    builder = new ComponentBuilder();
                    break;
                case 3:
                    embed = LvlUpPage(player);
                    builder = new ComponentBuilder().WithSelectMenu(charMenu);
                    break;
            }

            embed.WithFooter($"{GetTimeLeft(player)} |{embed.Length}|");
            

            await UpdateMessageWithEmbed(player, embed, builder);
        }


        public async Task UpdateMessageWithEmbed(GamePlayerBridgeClass player, EmbedBuilder embed,
            ComponentBuilder builder)
        {
            try
            {
                if (!player.IsBot() && !embed.Footer.Text.Contains("ERROR"))
                    await player.Status.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });
            }
            catch (Exception e)
            {
               _log.Critical(e.StackTrace);
            }
        }


        public string GetTimeLeft(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);

            if (game == null) return "ERROR";

            if (player.Status.IsReady)
                return
                    $"Ты походил • Ожидаем других игроков • ({game.TimePassed.Elapsed.Seconds}/{game.TurnLengthInSecond}с)";
            return
                $"Ожидаем твой ход • ({game.TimePassed.Elapsed.Seconds}/{game.TurnLengthInSecond}с)";
            /*
            //if (!game.IsCheckIfReady)
           //     return $"Ведется подсчёт, пожалуйста подожди... • ход #{game.RoundNo}";


            if (game.GameStatus == 1)
                return $"• ход #{game.RoundNo}";
             //   return "Времени осталось: " + (int) (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds) +
             //          $"сек. • ход #{game.RoundNo}";

            return $"Ведется подсчёт, пожалуйста подожди... • ход #{game.RoundNo}";
            */
        }

        private bool IsImageUrl(string url)
        {
            var req = (HttpWebRequest) WebRequest.Create(url);
            req.Method = "HEAD";
            using (var resp = req.GetResponse())
            {
                return resp.ContentType.ToLower(CultureInfo.InvariantCulture)
                    .StartsWith("image/");
            }
        }


        public async Task SendMsgAndDeleteIt(GamePlayerBridgeClass player, string msg = "Принято", int seconds = 6)
        {
            try{
                if (!player.IsBot())
                {
                    var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(msg);
#pragma warning disable 4014
                    _helperFunctions.DeleteMessOverTime(mess2, seconds);
#pragma warning restore 4014
                }
            }
            catch (Exception e)
            {
                _log.Critical(e.StackTrace);
            }
        }
    }
}