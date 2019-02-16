using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game
{
    public sealed class OctoGameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
    {
        public Task InitializeAsync()
            => Task.CompletedTask;

        private readonly UserAccounts _accounts;

        private readonly Global _global;

        public OctoGameUpdateMess(UserAccounts accounts, Global global)
        {
            _accounts = accounts;
            _global = global;
        }


        public async Task WaitMess(ulong userId)
        {
            var globalAccount = _global.Client.GetUser(userId);
            var account = _accounts.GetAccount(globalAccount);
            var mainPage = new EmbedBuilder();

            mainPage.WithAuthor(globalAccount);
            mainPage.WithFooter("Preparation time...");
            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Game is being ready", $"**Please wait until you will see emoji** {new Emoji("❌")}");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            account.MsgFromBotId = socketMsg.Id;

            if (!(socketMsg.Channel is IDMChannel))
                await socketMsg.RemoveAllReactionsAsync();

            await socketMsg.AddReactionAsync(new Emoji("⬅"));
            await socketMsg.AddReactionAsync(new Emoji("➡"));
            await socketMsg.AddReactionAsync(new Emoji("📖"));

            await socketMsg.AddReactionAsync(new Emoji("1⃣"));
            await socketMsg.AddReactionAsync(new Emoji("2⃣"));
            await socketMsg.AddReactionAsync(new Emoji("3⃣"));
            await socketMsg.AddReactionAsync(new Emoji("4⃣"));
            await socketMsg.AddReactionAsync(new Emoji("5⃣"));
            await socketMsg.AddReactionAsync(new Emoji("6⃣"));
            await socketMsg.AddReactionAsync(new Emoji("7⃣"));
            await socketMsg.AddReactionAsync(new Emoji("8⃣"));
            await socketMsg.AddReactionAsync(new Emoji("9⃣"));
            await socketMsg.AddReactionAsync(new Emoji("🐙"));

            await socketMsg.AddReactionAsync(new Emoji("❌"));


            account.IsPlaying = true;
            _accounts.SaveAccounts(userId);

            await MainPage(userId, socketMsg);
        }


        public async Task MainPage(ulong userId, IUserMessage socketMsg)
        {
            var globalAccount = _global.Client.GetUser(userId);
            var account = _accounts.GetAccount(globalAccount);



         

            var mainPage = FightPage(globalAccount, account);

            await socketMsg.ModifyAsync(message =>
            {
                message.Embed = null;
                message.Embed =  mainPage.Build(); });
        }




        public async Task Leaderboard(SocketReaction reaction, IUserMessage socketMsg)
        {
            var account = _accounts.GetAccount(reaction.User.Value.Id);

          var game =   _global.GamesList.Find(x => x.PlayersList.Any(b => b.DiscordId == account.DiscordId));
          var players = "";
          for (var i = 0; i < game.PlayersList.Count; i++)
          {
              players += $"{i + 1}. {game.PlayersList[i].DiscordUserName}\n";
          }

            var embed = new EmbedBuilder();
            embed.WithTitle("Кто же победит?");
            embed.WithDescription(players);

            //TODO: show a page
               await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }


        public async Task Logs(SocketReaction reaction, IUserMessage socketMsg)
        {
            var account = _accounts.GetAccount(reaction.User.Value.Id);

            var game =   _global.GamesList.Find(x => x.PlayersList.Any(b => b.DiscordId == account.DiscordId));

            var embed = new EmbedBuilder();
            embed.WithTitle("Logs");
            embed.WithDescription("TODO");

          
            await socketMsg.ModifyAsync(message => { message.Embed = embed.Build(); });
        }



        public async Task EndGame(SocketReaction reaction, IUserMessage socketMsg)
        {
            var userId = reaction.User.Value.Id;
            var globalAccount = _global.Client.GetUser(reaction.UserId);
            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;
            _accounts.SaveAccounts(userId);
            await socketMsg.DeleteAsync();
        }


        public EmbedBuilder FightPage(SocketUser globalAccount, AccountSettings account )
        {


           
            var mainPage = new EmbedBuilder();
            mainPage.WithAuthor(globalAccount);

            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Enemy:",
                $"**Name:** {account.DiscordUserName}\n" +
                $"**Интеллект:** {account.CharacterStats.Intelligence}\n" +
                $"**Сила:** {account.CharacterStats.Strength}\n" +
                $"**Скорость:** {account.CharacterStats.Speed}\n" +
                $"**Психика:** {account.CharacterStats.Psyche}\n" +
                $"**Справедливость:** {account.CharacterStats.Justice}\n" +
                $"");

            if (account.MoveListPage == 1)
            {
                mainPage.WithFooter($"Your Character");
            }
            if (account.MoveListPage == 2)
            {
                mainPage.WithFooter($"Leaderboard");
            }
            if (account.MoveListPage == 3)
            {
                mainPage.WithFooter($"LvL UP! Choose Wisely!");
            }


            if(account.CharacterStats.Avatar != null)
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