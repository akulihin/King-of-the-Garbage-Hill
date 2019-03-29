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


        public async Task ShowRulesAndChar(SocketUser user, GameBridgeClass player)
        {
            if (player.IsBot()) return;

            var pass = "";
            var passList = player.Character.Passive;
            for (var i = 0; i < passList.Count; i++)
            {
                pass += $"__**{passList[i].PassiveName}**__";
                pass += ": ";
                pass += passList[i].PassiveDescription;
                pass += "\n";
            }

            var gameRules = "**Правила игры:**\n" +
                            "Всем выпадает рандомная карта с персонажем. Игрокам не известно против кого они играют. Каждый ход игрок может напасть на кого-то, либо обороняться. " +
                            "В случае нападения игрок либо побеждает, получая очко, либо проигрывает, приносят очко врагу. В случае нападения на обороняющегося игрока, бой не состоится и нападающий уйдет ни с чем, потеряв 1 __бонусное__ очко и 1 Справедливости. Обороняющийся получает +1 Справедливости.\n" +
                            "\n" +
                            "**Бой:**\n" +
                            "У всех персонажей есть 4 стата, чтобы победить в бою нужно выиграть по двум из трех пунктов:\n" +
                            "1) статы \n" +
                            "2) справедливость\n" +
                            "3) случайность \n" +
                            "\n" +
                            "1 - В битве статов наибольшую роль играет Контр - превосходящий стат (если ваш персонаж превосходит врага например в интеллекте, то ваш персонаж умнее). Умный персонаж побеждает Быстрого, Быстрый Сильного, а Сильный Умного.\n" +
                            "Второстепенную роль играет разница в общей сумме статов. Разница в Психике дополнительно дает небольшое преимущество.\n" +
                            "2 - Проигрывая, персонажи получют +1 справедливости (максимум 5), при победе они полностью ее теряют. Во втором пункте побеждает тот, у кого больше справедливости на момент сражения.\n" +
                            "3 - Обычный рандом, который чуть больше уважает СЛИШКОМ превосходящих игроков по первому пункту." +
                            "\n" +
                            "Очки напрямую влияют на место в таблице. Начиная с 5го хода,  все  получаемые очки, кроме __бонусных__, умножаются на 2, на 10ом ходу очки умножаются на 4.\n" +
                            "После каждого хода обновляется таблица лидеров, побеждает лучший игрок после 10и ходов.\n" +
                            "После каждого второго хода игрок может улучшить один из статов на +1.\n" +
                            "У каждого персонажа есть особые пассивки, используйте их как надо!";

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            embed.AddField("Твой Персонаж:", $"Name: {player.Character.Name}\n" +
                                             $"Интеллект: {player.Character.GetIntelligence()}\n" +
                                             $"Сила: {player.Character.GetStrength()}\n" +
                                             $"Скорость: {player.Character.GetSpeed()}\n" +
                                             $"Психика: {player.Character.GetPsyche()}\n");
            embed.AddField("Пассивки", $"{pass}");
            embed.WithDescription(gameRules);


            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(GameBridgeClass player)
        {
            if (player.IsBot()) return;

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

        public string LeaderBoard(DiscordAccountClass discordAccount, CharacterClass character)
        {
            var game = _global.GamesList.Find(x => x.GameId == discordAccount.GameId);
            if (game == null) return "ERROR 404";
            var players = "";
            var playersList = game.PlayersList;

            for (var i = 0; i < playersList.Count; i++)
            {
                players += $"{i + 1}. {playersList[i].DiscordAccount.DiscordUserName}";

                players += CustomLeaderBoard(discordAccount, character, playersList[i]);

                //TODO: REMOVE || playersList[i].IsBot()
                if (discordAccount.DiscordId == playersList[i].DiscordAccount.DiscordId || playersList[i].IsBot())
                    players +=
                        $" = {playersList[i].Status.GetScore()} (I: {playersList[i].Character.GetIntelligence()}, St: {playersList[i].Character.GetStrength()}, SP: {playersList[i].Character.GetSpeed()}, Psy: {playersList[i].Character.GetPsyche()}, J: {playersList[i].Character.Justice.GetJusticeNow()})\n";
                else
                    players += "\n";
            }

            return players;
        }

        public string CustomLeaderBoard(DiscordAccountClass player1Account, CharacterClass player1Char,
            GameBridgeClass player2)
        {
            var customString = "";
            switch (player1Char.Name)
            {

                case "HardKitty":
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == player1Account.GameId &&
                        x.PlayerDiscordId == player1Account.DiscordId);
                    var lostSeries = hardKitty?.LostSeries.Find(x => x.EnemyId == player2.DiscordAccount.DiscordId);
                    if (lostSeries != null)
                    {
                        customString += $" {new Emoji("<:sparta:561287745675329567>")} - {lostSeries.Series}";
                    }
                    break;
                case "Sirinoks":
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == player1Account.GameId && x.PlayerDiscordId == player1Account.DiscordId);

                    if (siri != null)
                    {
                        if (siri.FriendList.Contains(player2.DiscordAccount.DiscordId))
                        {
                            customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                        }
                    }

                    break;
                case "Загадочный Спартанец в маске":
                    var panth = _gameGlobal.PanthMark.Find(x =>
                        x.GameId == player1Account.GameId && x.PlayerDiscordId == player1Account.DiscordId);

                    if (panth.FriendList.Contains(player2.DiscordAccount.DiscordId))
                        customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                    break;


                case "DeepList":

                    //tactic
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerDiscordId == player1Account.DiscordId && player1Account.GameId == x.GameId);
                    if (deep != null)
                        if (deep.FriendList.Contains(player2.DiscordAccount.DiscordId))
                            customString += $" {new Emoji("<:yo:561287783704952845>")}";
                    //end tactic

                    //сверхразум
                    var currentList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                        x.DiscordId == player1Account.DiscordId && x.GameId == player1Account.GameId);
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(player2.DiscordAccount.DiscordId))
                            customString +=
                                $" PS: - {player2.Character.Name} ({player2.Character.GetIntelligence()}, " +
                                $"{player2.Character.GetStrength()}, {player2.Character.GetSpeed()}, " +
                                $"{player2.Character.GetPsyche()}, {player2.Character.Justice.GetJusticeNow()})";
                    //end сверхразум


                    break;

                case "mylorik":
                    var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                        x.GameId == player1Account.GameId && x.PlayerDiscordId == player1Account.DiscordId);
                    var find = mylorik?.EnemyListDiscordId.Find(x =>
                        x.EnemyDiscordId == player2.DiscordAccount.DiscordId && x.IsUnique);

                    if (find != null) customString += $" {new Emoji("<:sparta:561287745675329567>")}";
                    break;
                case "Тигр":

                    var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId == player1Account.GameId && x.PlayerDiscordId == player1Account.DiscordId);

                    var enemy = tigr?.FriendList.Find(x => x.EnemyId == player2.DiscordAccount.DiscordId);

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
        public EmbedBuilder FightPage(GameBridgeClass player)
        {
            var account = player.DiscordAccount;
            //        player.Status.MoveListPage = 1;
            var character = player.Character;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(account)}");

            var game = _global.GamesList.Find(x => x.GameId == account.GameId);
            var desc = "ERROR";

            if (game != null)
            {
                 desc = game.PreviousGameLogs;
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
                $"{LeaderBoard(account, character)}");


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
                if (IsImageUrl(character.Avatar))
                    embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        //Page 2
        public EmbedBuilder LogsPage(GameBridgeClass gameBridge)
        {
            var account = gameBridge.DiscordAccount;


            var game = _global.GamesList.Find(x => x.GameId == account.GameId);

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription(game.GameLogs);
            embed.WithColor(Color.Green);
            embed.WithFooter($"{GetTimeLeft(account)}");

            embed = CustomLogsPage(gameBridge, embed);
            return embed;

            //    await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public EmbedBuilder CustomLogsPage(GameBridgeClass gameBridge, EmbedBuilder embed)
        {
            switch (gameBridge.Character.Name)
            {
                case "DeepList":
                    break;
            }


            return embed;
        }

        //Page 3
        public EmbedBuilder LvlUpPage(GameBridgeClass gameBridge)
        {
            //    var status = player.Status;
            var account = gameBridge.DiscordAccount;
            var character = gameBridge.Character;

            //   status.MoveListPage = 3;
            //    _accounts.SaveAccounts(discordAccount.PlayerDiscordId);

            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == account.GameId).PreviousGameLogs;
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

        public async Task UpdateMessage(GameBridgeClass player)
        {
            var embed = FightPage(player);

            if (embed == null) return;

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

        public async Task UpdateMessageWithEmbed(GameBridgeClass player, EmbedBuilder embed)
        {
            if (!player.IsBot())
                await player.Status.SocketMessageFromBot.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public string GetTimeLeft(DiscordAccountClass discordAccount)
        {
            var game = _global.GamesList.Find(x => x.GameId == discordAccount.GameId);

            if (game == null)
            {
                discordAccount.IsPlaying = false;
                _accounts.SaveAccounts(discordAccount);
                return "END";
            }

            if (!_global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId).IsTimerToCheckEnabled)
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
