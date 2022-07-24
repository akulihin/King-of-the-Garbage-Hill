using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace King_of_the_Garbage_Hill.DiscordFramework.Extensions;

public class ModuleBaseCustom : ModuleBase<SocketCommandContextCustom>
{
    protected async Task DeleteMessage(IUserMessage userMessage,
        int timeInSeconds)
    {
        var seconds = timeInSeconds * 1000;
        await Task.Delay(seconds);
        await userMessage.DeleteAsync();
    }

    protected virtual async Task SendMessageAsync(EmbedBuilder embed, int delete = 0, MessageComponent components = null)
    {
        if (Context.SlashCommand != null)
        {
            await Context.SlashCommand.RespondAsync(embed: embed.Build(), ephemeral: true);
            return;
        }

        if (Context.ContextSlash != null)
        {
            await Context.ContextSlash.RespondAsync(embed: embed.Build(), ephemeral: true);
            return;
        }

        switch (Context.MessageContentForEdit)
        {
            case null:
                {
                    IUserMessage message;
                    if (Context.DmChannel == null)
                    {
                        message = await Context.Channel.SendMessageAsync("", false, embed.Build(), components: components);
                    }
                    else
                    {
                        message = await Context.DmChannel.SendMessageAsync("", false, embed.Build(), components: components);
                    }



                    UpdateGlobalCommandList(message, Context);

#pragma warning disable 4014
                    if (delete > 0) DeleteMessage(message, delete);
#pragma warning restore 4014
                    break;
                }
            case "edit":
                {
                    foreach (var t in Context.CommandsInMemory.CommandList.Where(t =>
                                 t.MessageUserId == Context.Message.Id))
                        await t.BotSocketMsg.ModifyAsync(message =>
                        {
                            message.Content = "";
                            message.Embed = null;
                            message.Embed = embed.Build();
                            message.Components = components;
                        });
                    break;
                }
        }
    }


    protected virtual async Task<IUserMessage> SendMessageAsync([Remainder] string regularMess = null, int delete = 0, MessageComponent components = null)
    {
        if (Context.SlashCommand != null)
        {
            await Context.SlashCommand.RespondAsync(regularMess, components: components, ephemeral: true);
            return null;
        }

        if (Context.ContextSlash != null)
        {
            await Context.ContextSlash.RespondAsync(regularMess, components: components, ephemeral: true);
            return null;
        }

        switch (Context.MessageContentForEdit)
        {
            case null:
                {
                    IUserMessage message;
                    if (Context.DmChannel == null)
                    {
                        message = await Context.Channel.SendMessageAsync($"{regularMess}", components: components);
                    }
                    else
                    {
                        message = await Context.DmChannel.SendMessageAsync($"{regularMess}", components: components);
                    }


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
                            if (regularMess != null) message.Content = regularMess;
                            message.Components = components;
                        });
                        return t.BotSocketMsg;
                    }

                    break;
                }
        }

        return null;
    }


    protected virtual async Task<IUserMessage> SendMessageAsync([Remainder] string regularMess, SocketCommandContextCustom context)
    {
        /*if (Context.SlashCommand != null)
        {
            await Context.SlashCommand.RespondAsync(regularMess, ephemeral: true);
            return null;
        }

        if (Context.ContextSlash != null)
        {
            await Context.ContextSlash.RespondAsync(regularMess, ephemeral: true);
            return null;
        }*/

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
                    foreach (var t in context.CommandsInMemory.CommandList.Where(t =>
                                 t.MessageUserId == context.Message.Id))
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