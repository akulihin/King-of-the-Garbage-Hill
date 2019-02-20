using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.DiscordMessages
{
    public sealed class GameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        private readonly UserAccounts _accounts;
        private readonly Global _global;
        private readonly AwaitForUserMessage _awaitForUser;

        public GameUpdateMess(UserAccounts accounts, Global global, AwaitForUserMessage awaitForUser)
        {
            _accounts = accounts;
            _global = global;
            _awaitForUser = awaitForUser;
        }


        public async Task ShowRulesAndChar(SocketUser user)
        {
            var gameRules = "**Правила игры:**\n" +
                            "Всем выпадает рандомная карта с персонажем. Игрокам не известно против кого они играют. Каждый ход игрок может напасть на кого-то, либо обороняться. В случае нападения игрок либо побеждает, получая очко, либо проигрывает, приносят очко врагу. В случае обороны, напавшие на игрока враги не могут его победить и уходят ни с чем.\n" +
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
                            "После каждого хода обновляется таблица лидеров, побеждает лучший игрок после 10и ходов.\n" +
                            "После каждого второго хода игрок может улучшить один из статов на +1.\n" +
                            "У каждого персонажа есть особые пассивки, используйте их как надо!";

            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);    
            embed.AddField("Твой Персонаж:", "TODO");
            embed.WithDescription(gameRules);


            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(GameBridgeClass gameBridge)
        {
            var globalAccount = _global.Client.GetUser(gameBridge.DiscordAccount.DiscordId);
  

            await ShowRulesAndChar(globalAccount);

            var mainPage = new EmbedBuilder();
            mainPage.WithAuthor(globalAccount);
            mainPage.WithFooter("Preparation time...");
            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Game is being ready", $"**Please wait until you will see emoji** {new Emoji("❌")}");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            gameBridge.Status.SocketMessageFromBot = socketMsg;

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

        public string LeaderBoard(DiscordAccountClass discordAccount)
        {
            var game = _global.GamesList.Find(x => x.GameId == discordAccount.GameId);
            var players = "";
            var playersList = game.PlayersList;
            for (var i = 0; i < playersList.Count; i++)
            {
                players += $"{i + 1}. {playersList[i].DiscordAccount.DiscordUserName}";


                if (discordAccount.DiscordId == playersList[i].DiscordAccount.DiscordId)
                    players += $" = {playersList[i].Status.Score}\n";
                else players += "\n";
            }

            return players;
        }

        public async Task EndGame(SocketReaction reaction, IUserMessage socketMsg)
        {
            var response = await _awaitForUser.FinishTheGameQuestion(reaction);
            if (!response) return;

            var globalAccount = _global.Client.GetUser(reaction.UserId);

            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;
            _accounts.SaveAccounts(account.DiscordId);

            await socketMsg.DeleteAsync();
            await globalAccount.SendMessageAsync("Thank you for playing!");
        }

        //Page 1
        public EmbedBuilder FightPage(GameBridgeClass gameBridge)
        {
            var account = gameBridge.DiscordAccount;
    //        gameBridge.Status.MoveListPage = 1;
            var character = gameBridge.Character;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Blue);

            embed.WithFooter($"{GetTimeLeft(account)}");
            embed.WithDescription(_global.GamesList.Find(x => x.GameId == account.GameId).PreviousGameLogs);

            embed.WithTitle("Царь Мусорной Горы");
            embed.AddField( "____",
                $"**Name:** {character.Name}\n" +
                $"**Интеллект:** {character.Intelligence}\n" +
                $"**Сила:** {character.Strength}\n" +
                $"**Скорость:** {character.Speed}\n" +
                $"**Психика:** {character.Psyche}\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"*Справедливость: {character.Justice.JusticeNow}*\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"{LeaderBoard(account)}");


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


            return embed;

        //    await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }

        //Page 3
        public EmbedBuilder LvlUpPage(GameBridgeClass gameBridge)
        {
        //    var status = gameBridge.Status;
            var account = gameBridge.DiscordAccount;
            var character = gameBridge.Character;

         //   status.MoveListPage = 3;
        //    _accounts.SaveAccounts(discordAccount.DiscordId);

            var embed= new EmbedBuilder();
            embed.WithColor(Color.Blue);
            embed.WithTitle("Подними один из статов");
            embed.WithFooter($"{GetTimeLeft(account)}");
            embed.WithDescription(
                $"1. **Интеллект:** {character.Intelligence}\n" +
                $"2. **Сила:** {character.Strength}\n" +
                $"3. **Скорость:** {character.Speed}\n" +
                $"4. **Психика:** {character.Psyche}\n");    

            if (character.Avatar != null)
                if (IsImageUrl(character.Avatar))
                    embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        public async Task UpdateMessage(GameBridgeClass gameBridge)
        {
            var embed = FightPage(gameBridge);
            switch (gameBridge.Status.MoveListPage)
            {
                case 1:
                   // embed = LogsPage(gameBridge);
                    break;
                case 2:
                    embed = LogsPage(gameBridge);
                    break;
                case 3:
                    embed = LvlUpPage(gameBridge);


                    break;
            }


            try
            {
                await gameBridge.Status.SocketMessageFromBot.ModifyAsync(message => { message.Embed = embed.Build(); });
            }
            catch
            {
                //
            }
        }

        public async Task UpdateMessage(GameBridgeClass gameBridge, EmbedBuilder embed)
        {
            await gameBridge.Status.SocketMessageFromBot.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public string GetTimeLeft(DiscordAccountClass discordAccount)
        {
            var game = _global.GamesList.Find(x => x.GameId == discordAccount.GameId);
            if (game.GameStatus == 1)
            {
                return "Времени осталось: " + (int) (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds) +
                       $"сек. • ход #{game.RoundNo}";
            }

            return $"Ведется подсчёт... • ход #{game.RoundNo}";
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