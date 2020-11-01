using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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


        public GameUpdateMess(UserAccounts accounts, Global global, InGameGlobal gameGlobal,
            AwaitForUserMessage awaitForUser, HelperFunctions helperFunctions)
        {
            _accounts = accounts;
            _global = global;
            _gameGlobal = gameGlobal;
            _awaitForUser = awaitForUser;
            _helperFunctions = helperFunctions;
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
            embed.AddField("Твой Персонаж:", $"Name: {player.Character.Name}\n" +
                                             $"Интеллект: {player.Character.GetIntelligence()}\n" +
                                             $"Сила: {player.Character.GetStrength()}\n" +
                                             $"Скорость: {player.Character.GetSpeed()}\n" +
                                             $"Психика: {player.Character.GetPsyche()}\n");
            embed.AddField("Пассивки", $"{pass}");


            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(GamePlayerBridgeClass player)
        {
            if (player.DiscordAccount.DiscordId <= 1000000) return;

            var globalAccount = _global.Client.GetUser(player.DiscordAccount.DiscordId);


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

            await socketMsg.AddReactionsAsync(new IEmote[] { new Emoji("🛡"), new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣"), new Emoji("5⃣"), new Emoji("6⃣"), new Emoji("❌") });
            //   await socketMsg.AddReactionAsync(new Emoji("⬆"));
            //   await socketMsg.AddReactionAsync(new Emoji("8⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("9⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("🐙"));


            //    await MainPage(userId, socketMsg);
        }

        public string LeaderBoard(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.DiscordAccount.GameId);
            if (game == null) return "ERROR 404";
            var players = "";
            var playersList = game.PlayersList;

            for (var i = 0; i < playersList.Count; i++)
            {
                players += CustomLeaderBoardBeforeNumber(player, playersList[i], game, i + 1);

                players += $"{i + 1}. {playersList[i].DiscordAccount.DiscordUserName}";

                players += CustomLeaderBoardAfterPlayer(player, playersList[i], game);

                //TODO: REMOVE || playersList[i].IsBot()
                if (player.Status.PlayerId == playersList[i].Status.PlayerId)
                    players +=
                        $" = {playersList[i].Status.GetScore()} Score";


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
            //|| player1.DiscordAccount.DiscordId == 238337696316129280 || player1.DiscordAccount.DiscordId == 181514288278536193
            if (game.RoundNo == 11 || player1.DiscordAccount.UserType == "admin")
            {
                customString += $"(as **{player2.Character.Name}**) = {player2.Status.GetScore()} Score";
                customString +=
                    $"(I: {player2.Character.GetIntelligence()} | St: {player2.Character.GetStrength()} | Sp: {player2.Character.GetSpeed()} | Ps: {player2.Character.GetPsyche()})";
            }

            switch (player1.Character.Name)
            {
                case "AWDKA":
                    if (player2.Status.PlayerId == player1.Status.PlayerId) break;

                    var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    var awdkaTrying = awdka.TryingList.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                    if (awdkaTrying != null)
                    {
                        if (!awdkaTrying.IsUnique) customString += "<:bronze:565744159680626700>";
                        else customString += "<:plat:565745613208158233>";
                    }


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
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (vamp != null)
                        if (!vamp.FriendList.Contains(player2.Status.PlayerId) &&
                            player2.Status.PlayerId != player1.Status.PlayerId)
                            customString += "<:Y_:562885385395634196>";
                    break;

                case "HardKitty":
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId &&
                        x.PlayerId == player1.Status.PlayerId);
                    var lostSeries = hardKitty?.LostSeries.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);
                    if (lostSeries != null)
                        customString += $"<:393:563063205811847188> - {lostSeries.Series}";
                    break;
                case "Sirinoks":
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

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
                        customString += "<:yasuo:445323301137547264>";
                }
                {
                    var panthMark = _gameGlobal.PanthMark.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (panthMark.FriendList.Contains(player2.Status.PlayerId))
                        customString += "<:sparta:561287745675329567>";
                }

                    break;


                case "DeepList":

                    //tactic
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && player1.DiscordAccount.GameId == x.GameId);
                    if (deep != null)
                        if (deep.FriendList.Contains(player2.Status.PlayerId) &&
                            player2.Status.PlayerId != player1.Status.PlayerId)
                            customString += "<:yo:561287783704952845>";
                    //end tactic

                    //сверхразум
                    var currentList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && x.GameId == player1.DiscordAccount.GameId);
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(player2.Status.PlayerId))
                            customString +=
                                $"PS: - {player2.Character.Name} (I: {player2.Character.GetIntelligence()} | " +
                                $"St: {player2.Character.GetStrength()} | Sp: {player2.Character.GetSpeed()} | " +
                                $"Ps: {player2.Character.GetPsyche()} | J: {player2.Character.Justice.GetJusticeNow()})";
                    //end сверхразум


                    break;

                case "mylorik":
                    var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == player2.Status.PlayerId);

                    if (find != null && find.IsUnique) customString += "<:sparta:561287745675329567>";
                    if (find != null && !find.IsUnique) customString += "❌";

                    break;
                case "Тигр":

                    var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    var enemy = tigr?.FriendList.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

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

            return customString;
        }

        public async Task EndGame(SocketReaction reaction, IUserMessage socketMsg)
        {
            var response = await _awaitForUser.FinishTheGameQuestion(reaction);
            if (!response) return;
            _helperFunctions.SubstituteUserWithBot(reaction.User.Value.Id);

            var globalAccount = _global.Client.GetUser(reaction.UserId);
            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;


            //  await socketMsg.DeleteAsync();
            await globalAccount.SendMessageAsync("Thank you for playing!");
        }

        //Page 1
        public EmbedBuilder FightPage(GamePlayerBridgeClass player, GameClass game = null)
        {
            var account = player.DiscordAccount;
            //        player.Status.MoveListPage = 1;
            var character = player.Character;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(player)}");

            if (game == null) game = _global.GamesList.Find(x => x.GameId == account.GameId);

            var desc = "ERROR";

            if (game != null) desc = game.GetPreviousGameLogs();

            embed.WithDescription(desc.Replace(player.DiscordAccount.DiscordUserName, $"**{player.DiscordAccount.DiscordUserName}**"));

            if (desc.Length >= 2048)
                _global.Client.GetUser(181514288278536193).GetOrCreateDMChannelAsync().Result
                    .SendMessageAsync("PreviousGameLogs >= 2048");

            embed.AddField("<:e_:562879579694301184>",
                $"**Интеллект:** {character.GetIntelligence()}\n" +
                $"**Сила:** {character.GetStrength()}\n" +
                $"**Скорость:** {character.GetSpeed()}\n" +
                $"**Психика:** {character.GetPsyche()}\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"*Справедливость: {character.Justice.GetJusticeNow()}\n" +
                $"Мораль: {character.GetMoral()}\n" +
                $"Скилл: {character.GetSkill()} (**{character.GetCurrentSkillTarget()}**)*\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                "<:e_:562879579694301184>\n" +
                $"{LeaderBoard(player)}" +
                "<:e_:562879579694301184>");


            var splitted = player.Status.InGamePersonalLogsAll.Split("|||");
            if (game != null && splitted.Length > 1 && splitted[splitted.Length - 2].Length > 3 && game.RoundNo > 1)
                embed.AddField("События прошлого раунда:",
                    $"{splitted[splitted.Length - 2]}<:e_:562879579694301184>");
            else
                embed.AddField("События прошлого раунда:",
                    "В прошлом раунде ничего не произошло. Странно...\n<:e_:562879579694301184>");

            if (player.Status.GetInGamePersonalLogs().Length >= 2)
                embed.AddField("События этого раунда:", $"{player.Status.GetInGamePersonalLogs()}");
            else
                embed.AddField("События этого раунда:", "Еще ничего не произошло. Наверное...");


            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);


            return embed;
        }

        //Page 2
        public EmbedBuilder LogsPage(GamePlayerBridgeClass player)
        {
            var account = player.DiscordAccount;


            var game = _global.GamesList.Find(x => x.GameId == account.GameId);

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription(game.GetAllGameLogs());
            embed.WithColor(Color.Green);
            embed.WithFooter($"{GetTimeLeft(player)}");

            return embed;
        }

        //Page 3
        public EmbedBuilder LvlUpPage(GamePlayerBridgeClass player)
        {
            //    var status = player.Status;
            var account = player.DiscordAccount;
            var character = player.Character;

            //   status.MoveListPage = 3;
            //    _accounts.SaveAccounts(discordAccount.PlayerDiscordId);

            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == account.GameId).GetPreviousGameLogs();
            if (desc == null)
                return null;
            embed.WithDescription(desc);

            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.AddField("_____",
                "__Подними один из статов на 1:__\n \n" +
                $"1. **Интеллект:** {character.GetIntelligence()}\n" +
                $"2. **Сила:** {character.GetStrength()}\n" +
                $"3. **Скорость:** {character.GetSpeed()}\n" +
                $"4. **Психика:** {character.GetPsyche()}\n");

            if (character.Avatar != null)
                if (IsImageUrl(character.Avatar))
                    embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        //Page 4
        public EmbedBuilder MoralPage(GamePlayerBridgeClass player)
        {

            var account = player.DiscordAccount;
            var character = player.Character;


            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == account.GameId).GetPreviousGameLogs();
            if (desc == null)
                return null;
            embed.WithDescription(desc);

            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.AddField("_____",
                "__Ты можешь  1:__\n \n" +
                $"1. **Интеллект:** {character.GetIntelligence()}\n" +
                $"2. **Сила:** {character.GetStrength()}\n" +
                $"3. **Скорость:** {character.GetSpeed()}\n" +
                $"4. **Психика:** {character.GetPsyche()}\n");

            if (character.Avatar != null)
                if (IsImageUrl(character.Avatar))
                    embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        public async Task UpdateMessage(GamePlayerBridgeClass player, GameClass game = null)
        {
            var embed = FightPage(player, game);

            if (embed == null || player.IsBot()) return;

            switch (player.Status.MoveListPage)
            {
                case 1:
                    // embed = LogsPage(player);
                    break;
                case 2:
                    embed = LogsPage(player);
                    break;
                case 3:
                    embed = LvlUpPage(player);
                    break;
            }

            await UpdateMessageWithEmbed(player, embed);
        }


        public async Task UpdateMessageWithEmbed(GamePlayerBridgeClass player, EmbedBuilder embed)
        {
            try
            {
                if (!player.IsBot() && !embed.Footer.Text.Contains("ERROR"))
                    await player.Status.SocketMessageFromBot.ModifyAsync(message => { message.Embed = embed.Build(); });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }


        public string GetTimeLeft(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.DiscordAccount.GameId);

            if (game == null) return "ERROR";

            if (player.Status.IsReady)
                return $"Ты походил • Ожидаем других игроков • ход #{game.RoundNo}";
            return $"Ожидаем твой ход • ход #{game.RoundNo}";
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
            if (!player.IsBot())
            {
                var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(msg);
#pragma warning disable 4014
                _helperFunctions.DeleteMessOverTime(mess2, seconds);
#pragma warning restore 4014
            }
        }
    }
}
