﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling;

public class LoreReactions : IServiceSingleton
{
    private readonly CharactersPull _charactersPull;
    private readonly UserAccounts _userAccounts;
    private readonly LoginFromConsole _logs;

    public LoreReactions(UserAccounts userAccounts, CharactersPull charactersPull, LoginFromConsole logs)
    {
        _userAccounts = userAccounts;
        _charactersPull = charactersPull;
        _logs = logs;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public SelectMenuBuilder GetLoreCharacterSelectMenu(DiscordAccountClass account)
    {
        var characterMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("lore-select-character")
            .WithPlaceholder("Выбрать персонажа");


        foreach (var character in _charactersPull.GetVisibleCharacters())
            characterMenu.AddOption(character.Name, character.Name);

        return characterMenu;
    }


    public EmbedBuilder GetLoreEmbed(CharacterClass character)
    {
        var embed = new EmbedBuilder();

        var pass = "";
        var characterPassivesList = character.Passive;
        foreach (var passive in characterPassivesList)
        {
            if (!passive.Visible) continue;
            pass += $"__**{passive.PassiveName}**__";
            pass += ": ";
            pass += passive.PassiveDescription;
            pass += "\n";
        }

        embed.WithTitle($"Лор - {character.Name}");

        if (character.Description.Length > 1)
            embed.WithDescription(character.Description);

        embed.AddField("Характеристики:", $"Name: {character.Name}\n" +
                                          $"Интеллект: {character.GetIntelligence()}\n" +
                                          $"Сила: {character.GetStrength()}\n" +
                                          $"Скорость: {character.GetSpeed()}\n" +
                                          $"Психика: {character.GetPsyche()}\n");
        embed.AddField("Пассивки:", $"{pass}");

        embed.WithColor(Color.Orange);

        
        embed.WithThumbnailUrl(character.AvatarCurrent);


        return embed;
    }


    public async Task ModifyLoreMessage(SocketMessageComponent button, CharacterClass character,
        DiscordAccountClass account)
    {
        var builder = new ComponentBuilder();
        var embed = GetLoreEmbed(character);

        builder.WithSelectMenu(GetLoreCharacterSelectMenu(account));

        await button.Message.ModifyAsync(message =>
        {
            message.Embed = embed.Build();
            message.Components = builder.Build();
        });
    }


    public async Task ReactionAddedLore(SocketMessageComponent button)
    {
        try
        {
            var allCharacters = _charactersPull.GetVisibleCharacters();
            var account = _userAccounts.GetAccount(button.User.Id);

            switch (button.Data.CustomId)
            {
                case "lore-select-character":
                    var character = allCharacters.Find(x => x.Name == string.Join("", button.Data.Values));
                    await ModifyLoreMessage(button, character, account);
                    break;
            }
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }
}