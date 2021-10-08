using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace King_of_the_Garbage_Hill.DiscordFramework.Extensions
{
    public class ModuleBaseCustom : ModuleBase<SocketCommandContextCustom>
    {

        protected async Task DeleteMessage(IUserMessage userMessage,
            int timeInSeconds)
        {
            var seconds = timeInSeconds * 1000;
            await Task.Delay(seconds);
            await userMessage.DeleteAsync();
        }

        protected virtual async Task<IUserMessage> SendMessAsync(EmbedBuilder embed, int delete = 0, MessageComponent components = null )
        {
            switch (Context.MessageContentForEdit)
            {
                case null:
                    {
                        var message = await Context.Channel.SendMessageAsync("", false, embed.Build(), component: components);


                        UpdateGlobalCommandList(message, Context);

#pragma warning disable 4014
                        if (delete > 0) DeleteMessage(message, delete);
#pragma warning restore 4014
                        return message;
                    }
                case "edit":
                    {
                        foreach (var t in Context.CommandsInMemory.CommandList.Where(t =>
                            t.MessageUserId == Context.Message.Id))
                        {
                            await t.BotSocketMsg.ModifyAsync(message =>
                            {
                                message.Content = "";
                                message.Embed = null;
                                message.Embed = embed.Build();
                            });
                            return t.BotSocketMsg;
                        }

                        return null;
                    }
            }
            return null;
        }


        protected virtual async Task<IUserMessage> SendMessAsync([Remainder] string regularMess = null,
            int delete = 0)
        {
            switch (Context.MessageContentForEdit)
            {
                case null:
                    {
                        var message = await Context.Channel.SendMessageAsync($"{regularMess}");

                        UpdateGlobalCommandList(message, Context);
#pragma warning disable 4014
                        if (delete > 0) DeleteMessage(message, delete);
#pragma warning restore 4014
                        return message;
                    }
                case "edit":
                    {
                        foreach (var t in Context.CommandsInMemory.CommandList.Where(t => t.MessageUserId == Context.Message.Id))
                        {
                            await t.BotSocketMsg.ModifyAsync(message =>
                            {
                                message.Content = "";
                                message.Embed = null;
                                if (regularMess != null) message.Content = regularMess;
                            });
                            return t.BotSocketMsg;
                        }

                        break;
                    }
            }

            return null;
        }


        protected virtual async Task<IUserMessage> SendMessAsync([Remainder] string regularMess,
            SocketCommandContextCustom context)
        {
            switch (context.MessageContentForEdit)
            {
                case null:
                    {
                        var message = await context.Channel.SendMessageAsync($"{regularMess}");

                        UpdateGlobalCommandList(message, context);
                        return message;
                    }
                case "edit":
                    {
                        foreach (var t in context.CommandsInMemory.CommandList.Where(t => t.MessageUserId == context.Message.Id))
                        {
                            await t.BotSocketMsg.ModifyAsync(message =>
                            {
                                message.Content = "";
                                message.Embed = null;
                                if (regularMess != null) message.Content = regularMess;
                            });
                            return t.BotSocketMsg;
                        }

                        break;
                    }
            }

            return null;
        }


        private static void UpdateGlobalCommandList(IUserMessage message, SocketCommandContextCustom context)
        {
            try
            {
                context.CommandsInMemory.CommandList.Insert(0,
                    new CommandsInMemory.CommandRam(context.Message, message));
                if (context.CommandsInMemory.CommandList.Count > context.CommandsInMemory.MaximumCommandsInRam)
                    context.CommandsInMemory.CommandList.RemoveAt(
                        (int)context.CommandsInMemory.MaximumCommandsInRam - 1);
            }
            catch (Exception e)
            {
               Console.WriteLine(e.Message);
            }
        }
    }
}