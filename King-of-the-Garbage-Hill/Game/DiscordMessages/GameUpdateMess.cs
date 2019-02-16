using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            embed.AddField("Правила Игры", "TODO");
            embed.AddField("Твой Персонаж", "TODO");
            

            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(ulong userId)
        {

            var globalAccount = _global.Client.GetUser(userId);
            var account = _accounts.GetAccount(globalAccount);

            await  ShowRulesAndChar(globalAccount);

            var mainPage = new EmbedBuilder();
            mainPage.WithAuthor(globalAccount);
            mainPage.WithFooter("Preparation time...");
            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Game is being ready", $"**Please wait until you will see emoji** {new Emoji("❌")}");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            account.MsgFromBotId = socketMsg.Id;

            if (!(socketMsg.Channel is IDMChannel))
                await socketMsg.RemoveAllReactionsAsync();

            await socketMsg.AddReactionAsync(new Emoji("🛡"));
         //   await socketMsg.AddReactionAsync(new Emoji("➡"));
            await socketMsg.AddReactionAsync(new Emoji("📖"));

            await socketMsg.AddReactionAsync(new Emoji("1⃣"));
            await socketMsg.AddReactionAsync(new Emoji("2⃣"));
            await socketMsg.AddReactionAsync(new Emoji("3⃣"));
            await socketMsg.AddReactionAsync(new Emoji("4⃣"));
            await socketMsg.AddReactionAsync(new Emoji("5⃣"));
            await socketMsg.AddReactionAsync(new Emoji("6⃣"));
            await socketMsg.AddReactionAsync(new Emoji("⬆"));
           
            //   await socketMsg.AddReactionAsync(new Emoji("8⃣"));
            //   await socketMsg.AddReactionAsync(new Emoji("9⃣"));
            //    await socketMsg.AddReactionAsync(new Emoji("🐙"));

            await socketMsg.AddReactionAsync(new Emoji("❌"));


            account.IsPlaying = true;
            _accounts.SaveAccounts(userId);

            await MainPage(userId, socketMsg);
        }


        public async Task MainPage(ulong userId, IUserMessage socketMsg)
        {
            var globalAccount = _global.Client.GetUser(userId);
            var account = _accounts.GetAccount(globalAccount);

            account.MoveListPage = 1;
            _accounts.SaveAccounts(account.DiscordId);

            var mainPage = FightPage(globalAccount, account);

            await socketMsg.ModifyAsync(message =>
            {
                message.Embed = null;
                message.Embed = mainPage.Build();
            });
        }


        public string Leaderboard(AccountSettings account)
        {
            var game = _global.GamesList.Find(x => x.PlayersList.Any(b => b.DiscordId == account.DiscordId));
            var players = "";
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                players += $"{i + 1}. {game.PlayersList[i].DiscordUserName}";
                if (account.DiscordId == game.PlayersList[i].DiscordId) players += $" - {game.PlayersList[i].Score}\n";
                else players += "\n";
            }
      
            return players;
        }


        public async Task Logs(SocketReaction reaction, IUserMessage socketMsg)
        {


            var account = _accounts.GetAccount(reaction.User.Value.Id);

            if (account.MoveListPage == 2)
            {
                await MainPage(reaction.UserId, socketMsg);
                return;
            }

            account.MoveListPage = 2;
            _accounts.SaveAccounts(account.DiscordId);

            var game = _global.GamesList.Find(x => x.PlayersList.Any(b => b.DiscordId == account.DiscordId));

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription("TODO");
            embed.WithColor(Color.Green);
           


            await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public async Task EndGame(SocketReaction reaction, IUserMessage socketMsg)
        {

            var response = await  _awaitForUser.FinishTheGameQuestion(reaction);
            if (!response)
            {
                return;
            }
     
            var globalAccount = _global.Client.GetUser(reaction.UserId);
            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;
            _accounts.SaveAccounts(account.DiscordId);
            await socketMsg.DeleteAsync();
            await globalAccount.SendMessageAsync("Thank you for playing!");     
        }


        public EmbedBuilder FightPage(SocketUser globalAccount, AccountSettings account)
        {
            var leaders = Leaderboard(account);
            var mainPage = new EmbedBuilder();
            account.CharacterStats.Avatar = globalAccount.GetAvatarUrl();
            _accounts.SaveAccounts(account.DiscordId);

            mainPage.WithColor(Color.Blue);
            mainPage.WithTitle("Царь Мусорной Горы");
            mainPage.WithDescription(
                $"**Name:** {account.DiscordUserName}\n" +
                $"**Интеллект:** {account.CharacterStats.Intelligence}\n" +
                $"**Сила:** {account.CharacterStats.Strength}\n" +
                $"**Скорость:** {account.CharacterStats.Speed}\n" +
                $"**Психика:** {account.CharacterStats.Psyche}\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                     $"*Справедливость: {account.CharacterStats.Justice}*\n" +
                "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                $"{leaders}");
         
          


            if (account.CharacterStats.Avatar != null)
                if (IsImageUrl(account.CharacterStats.Avatar))
                    mainPage.WithThumbnailUrl(account.CharacterStats.Avatar);

            return mainPage;
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