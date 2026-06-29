using Discord.Commands;
using Discord.WebSocket;
using Discord;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using King_of_the_Garbage_Hill.Game.Services;
namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class ServerManagement : ModuleBaseCustom
    {
        private readonly HelperFunctions _helperFunctions;
        private readonly Global _global;
        private readonly DiscordWidgetService _widgetService;
        private readonly UserAccounts _accounts;

        public ServerManagement(HelperFunctions helperFunctions, Global global, DiscordWidgetService widgetService, UserAccounts accounts)
        {
            _helperFunctions = helperFunctions;
            _global = global;
            _widgetService = widgetService;
            _accounts = accounts;
        }


        [Command("чистка", RunMode = RunMode.Async)]
        [Alias("purge", "clean", "убрать", "clear", "delete", "remove")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        [Summary(
    "Удаляет сообщения. Можно указать пользователя, от которого удалять сообщения, например, \"clear 15 @ORIX\", что удалит сообщение только этого пользователя")]
        public async Task Delete(int amount, IUser user = null)
        {
            try
            {
                if (amount is > 500 or < 1)
                {
                    await SendMessageAsync("Лимит 500 Сообщений");
                    return;
                }


                if (user == null)
                {
                    var messages = await Context.Channel.GetMessagesAsync(Context.Message, Direction.Before, amount)
                        .FlattenAsync();

                    var filteredMessages = messages.Where(x => (DateTimeOffset.UtcNow - x.Timestamp).TotalDays <= 14)
                        .ToList();
                    var messagesCount = filteredMessages.Count();

                    if (messagesCount == 0)
                    {
                        await _helperFunctions.DeleteMessOverTime(await SendMessageAsync("Нечего удалять."), 3);
                        return;
                    }

                    filteredMessages.Add(Context.Message);

                    await ((ITextChannel)Context.Channel).DeleteMessagesAsync(filteredMessages);
                    await _helperFunctions.DeleteMessOverTime(await SendMessageAsync($"Готово. Удалено {messagesCount} {(messagesCount > 1 ? "сообщений" : "сообщение")}."), 3);
                }
                else
                {
                    var messages = await Context.Channel
                        .GetMessagesAsync(Context.Message, Direction.Before, amount + 200).FlattenAsync();
                    var filteredMessages = messages.Where(x => (DateTimeOffset.UtcNow - x.Timestamp).TotalDays <= 14)
                    .ToList();

                    if (filteredMessages.Count == 0)
                    {
                        await _helperFunctions.DeleteMessOverTime(await SendMessageAsync("Нечего удалять."), 3);
                        return;
                    }


                    var messagesToDelete = new List<ulong>();
                    var count = 0;


                    for (var i = 0; i < filteredMessages.Count - 1; i++)
                    {
                        if (count == amount)
                            continue;
                        if (filteredMessages[i].Author == user as SocketUser)
                        {
                            messagesToDelete.Add(filteredMessages[i].Id);
                            count++;
                        }
                    }

                    if (count <= 0)
                    {
                        await _helperFunctions.DeleteMessOverTime(await SendMessageAsync("Нечего удалять."), 3);
                        return;
                    }

                    messagesToDelete.Add(Context.Message.Id);
                    await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messagesToDelete);
                    await _helperFunctions.DeleteMessOverTime(await SendMessageAsync($"Готово. Удалено {messagesToDelete.Count()} {(messagesToDelete.Count() > 1 ? "сообщений" : "сообщение")}."), 3);
                }

                var embed = new EmbedBuilder();
                embed.WithColor(52, 235, 211);
                embed.AddField($"🛡**PURGE** {amount}", $"Used By {Context.User.Mention} in {Context.Channel}")
                    .WithThumbnailUrl(Context.User.GetAvatarUrl())
                    .WithCurrentTimestamp();


                await _global.Client.GetGuild(561282595799826432).GetTextChannel(1049047168650055750)
                    .SendMessageAsync("", false, embed.Build());
            }
            catch (Exception e)
            {
                await SendMessageAsync(e.Message);
            }
        }


        [Command("widget_s")]
        public async Task SetupAsync()
        {
            var authorizeButton = new ButtonBuilder()
            {
                Style = ButtonStyle.Link,
                Label = "Authorize",
                Url = $"https://discord.com/oauth2/authorize?client_id=901706293977432124&response_type=token&scope=openid+sdk.social_layer_presence"
            };
            var row = new ActionRowBuilder().AddComponents(authorizeButton);
            var components = new ComponentBuilder().AddRow(row).Build();
            await SendMessageAsync($"please click the button below to authorize.", components: components);
        }

        [Command("widget")]
        public async Task SyncUserDiscordWidget(string stat_text_left = null, string stat_text_right = null, int? favorite_number = null, ulong? discord_id = null)
        {
            var account = _accounts.GetAccount(Context.User);
            if (discord_id.HasValue)
            {
                account = _accounts.GetAccount(discord_id.Value);
            }

            if (account == null)
            {
                await SendMessageAsync("No account found.");
                return;
            }

            if (stat_text_left != null) account.WidgetStatTextLeft = stat_text_left;
            if (stat_text_right != null) account.WidgetStatTextRight = stat_text_right;
            if (favorite_number.HasValue) account.WidgetFavoriteNumber = favorite_number.Value;

            if (!account.WidgetAuthorized)
            {
                await SendMessageAsync("Widget not authorized yet — run `*widget_s` first and click the authorize button.");
                return;
            }

            var success = await _widgetService.SyncAsync(account.DiscordId);
            await SendMessageAsync(success
                ? "Discord widget updated."
                : "Failed to update Discord widget (see server logs).");
        }

    }
}