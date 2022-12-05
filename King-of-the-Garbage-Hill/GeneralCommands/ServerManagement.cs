using Discord.Commands;
using Discord.WebSocket;
using Discord;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class ServerManagement : ModuleBaseCustom
    {
        private readonly HelperFunctions _helperFunctions;
        private readonly Global _global;

        public ServerManagement(HelperFunctions helperFunctions, Global global)
        {
            _helperFunctions = helperFunctions;
            _global = global;
        }


        [Command("чистка", RunMode = RunMode.Async)]
        [Alias("purge", "clean", "убрать", "clear")]
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


    }
}