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
        private readonly HelperFunctions _helperFunctions;
        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;


        public GameUpdateMess(UserAccounts accounts, Global global, InGameGlobal gameGlobal, AwaitForUserMessage awaitForUser, HelperFunctions helperFunctions)
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
            mainPage.AddField("Game is being ready", $"**Please wait until you will see emoji** {new Emoji("❌")}");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            player.Status.SocketMessageFromBot = socketMsg;

            await socketMsg.AddReactionAsync(new Emoji("🛡"));
            //   await socketMsg.AddReactionAsync(new Emoji("➡"));
            // await socketMsg.AddReactionAsync(new Emoji("📖"));
            await socketMsg.AddReactionAsync(new Emoji("1⃣"));
            await socketMsg.AddReactionAsync(new Emoji("2⃣"));
            await socketMsg.AddReactionAsync(new Emoji("3⃣"));
            await socketMsg.AddReactionAsync(new Emoji("4⃣"));
            await socketMsg.AddReactionAsync(new Emoji("5⃣"));
            await socketMsg.AddReactionAsync(new Emoji("6⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("⬆"));
            //   await socketMsg.AddReactionAsync(new Emoji("8⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("9⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("🐙"));
            await socketMsg.AddReactionAsync(new Emoji("❌"));


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
                players += $"{i + 1}. {playersList[i].DiscordAccount.DiscordUserName}";

                players += CustomLeaderBoard(player, playersList[i]);

                //TODO: REMOVE || playersList[i].IsBot()
                if (player.Status.PlayerId == playersList[i].Status.PlayerId)
                    players +=
                        $" = {playersList[i].Status.GetScore()} Score";

                  
                    players += "\n";
            }

            return players;
        }

        public string CustomLeaderBoard( GamePlayerBridgeClass player1, GamePlayerBridgeClass player2)
        {


            var customString = "";

            if (player1.DiscordAccount.DiscordId == 181514288278536193 ||
                player1.DiscordAccount.DiscordId == 238337696316129280)
            {
                customString +=       $" =  {player2.Status.GetScore()} (I: {player2.Character.GetIntelligence()}, St: {player2.Character.GetStrength()}, SP: {player2.Character.GetSpeed()}, Psy: {player2.Character.GetPsyche()}, J: {player2.Character.Justice.GetJusticeNow()})";

            }

            switch (player1.Character.Name)
            {

                case "HardKitty":
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId &&
                        x.PlayerId == player1.Status.PlayerId);
                    var lostSeries = hardKitty?.LostSeries.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);
                    if (lostSeries != null)
                    {
                        customString += $" {new Emoji("<:sparta:561287745675329567>")} - {lostSeries.Series}";
                    }
                    break;
                case "Sirinoks":
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (siri != null)
                    {
                        if (siri.FriendList.Contains(player2.Status.PlayerId))
                        {
                            customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                        }
                    }

                    break;
                case "Загадочный Спартанец в маске":
                    var panth = _gameGlobal.PanthMark.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (panth.FriendList.Contains(player2.Status.PlayerId))
                        customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                    break;


                case "DeepList":

                    //tactic
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && player1.DiscordAccount.GameId == x.GameId);
                    if (deep != null)
                        if (deep.FriendList.Contains(player2.Status.PlayerId))
                            customString += $" {new Emoji("<:yo:561287783704952845>")}";
                    //end tactic

                    //сверхразум
                    var currentList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                        x.PlayerId == player1.Status.PlayerId && x.GameId == player1.DiscordAccount.GameId);
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(player2.Status.PlayerId))
                            customString +=
                                $" PS: - {player2.Character.Name} ({player2.Character.GetIntelligence()}, " +
                                $"{player2.Character.GetStrength()}, {player2.Character.GetSpeed()}, " +
                                $"{player2.Character.GetPsyche()}, {player2.Character.Justice.GetJusticeNow()})";
                    //end сверхразум


                    break;

                case "mylorik":
                    var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                        x.GameId == player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == player2.Status.PlayerId && x.IsUnique);

                    if (find != null) customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                    break;
                case "Тигр":

                    var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId ==player1.DiscordAccount.GameId && x.PlayerId == player1.Status.PlayerId);

                    var enemy = tigr?.FriendList.Find(x => x.EnemyPlayerId == player2.Status.PlayerId);

                    if (enemy != null)
                    {
                        if (enemy.WinsSeries == 1)
                            customString += " 1:0";
                        else if (enemy.WinsSeries == 2)
                            customString += " 2:0";
                        else if (enemy.WinsSeries == 3) customString += " 3:0, обоссан";
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
            
            _accounts.SaveAccounts(account.DiscordId);

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

            embed.WithFooter($"{GetTimeLeft(account)}");

            if (game == null)
            {
                game = _global.GamesList.Find(x => x.GameId == account.GameId);
            }
       
            var desc = "ERROR";

            if (game != null)
            {
                 desc = game.GetPreviousGameLogs();
            }
  
            embed.WithDescription(desc);

            if (desc.Length >= 2048)
                _global.Client.GetUser(181514288278536193).GetOrCreateDMChannelAsync().Result
                    .SendMessageAsync("PreviousGameLogs >= 2048");

            embed.WithTitle("Царь Мусорной Горы");
            embed.AddField("____",
                $"**Name:** {character.Name}\n" +
                $"**Интеллект:** {character.GetIntelligence()}\n" +
                $"**Сила:** {character.GetStrength()}\n" +
                $"**Скорость:** {character.GetSpeed()}\n" +
                $"**Психика:** {character.GetPsyche()}\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"*Справедливость: {character.Justice.GetJusticeNow()}*\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"{LeaderBoard(player)}");


            var splitted = player.Status.InGamePersonalLogsAll.Split("|||");
            if (game != null && (splitted.Length > 1 && splitted[splitted.Length-2].Length > 3 && game.RoundNo > 1))
            {
                embed.AddField("События прошлого раунда:", $"{splitted[splitted.Length-2]}");
            }
            if (player.Status.GetInGamePersonalLogs().Length >= 2)
            {
                embed.AddField("События этого раунда:", $"{player.Status.GetInGamePersonalLogs()}");
            }


            if (character.Avatar != null)
                    embed.WithThumbnailUrl(character.Avatar);
            

            return embed;
        }

        //Page 2
        public EmbedBuilder LogsPage(GamePlayerBridgeClass gamePlayerBridge)
        {
            var account = gamePlayerBridge.DiscordAccount;


            var game = _global.GamesList.Find(x => x.GameId == account.GameId);

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription(game.GetAllGameLogs()); // WILL CAUSE AN ERROR! 
            embed.WithColor(Color.Green);
            embed.WithFooter($"{GetTimeLeft(account)}");

            embed = CustomLogsPage(gamePlayerBridge, embed);
            return embed;

            //    await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public EmbedBuilder CustomLogsPage(GamePlayerBridgeClass gamePlayerBridge, EmbedBuilder embed)
        {
            switch (gamePlayerBridge.Character.Name)
            {
                case "DeepList":
                    break;
            }


            return embed;
        }

        //Page 3
        public EmbedBuilder LvlUpPage(GamePlayerBridgeClass gamePlayerBridge)
        {
            //    var status = player.Status;
            var account = gamePlayerBridge.DiscordAccount;
            var character = gamePlayerBridge.Character;

            //   status.MoveListPage = 3;
            //    _accounts.SaveAccounts(discordAccount.PlayerDiscordId);

            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == account.GameId).GetPreviousGameLogs();
            if (desc == null)
                return null;
            embed.WithDescription(desc);

            embed.WithColor(Color.Blue);
            embed.WithTitle("Подними один из статов");
            embed.WithFooter($"{GetTimeLeft(account)}");
            embed.AddField("____",
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
                case int n when n >= 3:
                    embed = LvlUpPage(player);
                    break;
            }

            await UpdateMessageWithEmbed(player, embed);
        }




        public async Task UpdateMessageWithEmbed(GamePlayerBridgeClass player, EmbedBuilder embed)
        {
            if (!player.IsBot() && !embed.Footer.Text.Contains("ERROR"))
                await player.Status.SocketMessageFromBot.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public string GetTimeLeft(DiscordAccountClass discordAccount)
        {
            var game = _global.GamesList.Find(x => x.GameId == discordAccount.GameId);

            if (game == null)
            {
                return "ERROR";
            }

            if (!game.IsCheckIfReady)
                return $"Ведется подсчёт, пожалуйста подожди... • ход #{game.RoundNo}";


            if (game.GameStatus == 1)
                return "Времени осталось: " + (int) (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds) +
                       $"сек. • ход #{game.RoundNo}";

            return $"Ведется подсчёт, пожалуйста подожди... • ход #{game.RoundNo}";
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
    }
}
