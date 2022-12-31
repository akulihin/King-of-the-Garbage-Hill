using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling;

public class StoreReactions : IServiceSingleton
{
    private readonly CharactersPull _charactersPull;
    private readonly SecureRandom _random;
    private readonly UserAccounts _userAccounts;
    private readonly int _basePrice = 10;
    private readonly LoginFromConsole _logs;
    public StoreReactions(UserAccounts userAccounts, CharactersPull charactersPull, SecureRandom random, LoginFromConsole logs)
    {
        _userAccounts = userAccounts;
        _charactersPull = charactersPull;
        _random = random;
        _logs = logs;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public SelectMenuBuilder GetStoreCharacterSelectMenu(DiscordAccountClass account)
    {
        var characterMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("store-select-character")
            .WithPlaceholder("Выбрать персонажа");


        foreach (var character in account.SeenCharacters) characterMenu.AddOption(character, character);

        return characterMenu;
    }


    public List<ButtonBuilder> GetStoreButtons()
    {
        var buttons = new List<ButtonBuilder>
        {
            new("Поднять шанс на 1%", "store-up-1", ButtonStyle.Secondary),
            new("Поднять шанс на 10%", "store-up-10", ButtonStyle.Secondary),
            new("Опустить шанс на 1%", "store-down-1", ButtonStyle.Secondary),
            new("Опустить шанс на 10%", "store-down-10", ButtonStyle.Secondary),
            new("Сбросить все изменения", "store-return-character", ButtonStyle.Secondary),
            new("Сбросить все изменения за всех персонажей", "store-return-all-characters", ButtonStyle.Secondary)
        };

        return buttons;
    }


    public string GetMerchantGif()
    {
        var merchants = new List<string>
        {
            "https://media.discordapp.net/attachments/895072182051430401/901882201027784724/Resident_Evil_4__Meeting_The_Merchant_4K_60FPS_1.gif",
            "https://cdn.discordapp.com/attachments/895072182051430401/901881759061389382/Resident_Evil_4__Meeting_The_Merchant_4K_60FPS.gif"
        };
        return merchants[_random.Random(0, merchants.Count - 1)];
    }

    public EmbedBuilder GetStoreEmbed(IUser user, string characterName)
    {
        var allCharacters = _charactersPull.GetVisibleCharacters();
        var account = _userAccounts.GetAccount(user);
        var characterChance = account.CharacterChance.Find(x => x.CharacterName == characterName);
        var character = allCharacters.Find(x => x.Name == characterName);
        var embed = new EmbedBuilder();
        var cost = _basePrice + characterChance!.Changes;
        var cost10 = 0;
        for (var i = 0; i < 10; i++)
        {
            cost10 += _basePrice + characterChance.Changes + i;
        }


        embed.WithTitle($"Магазин - {characterChance.CharacterName}");

        embed.AddField("Персонаж:", $"{characterChance.CharacterName}", true);
        embed.AddField("Бонусный шанс:", $"{Math.Round(characterChance.Multiplier, 2)}", true);
        embed.AddField("ZBS Points:", $"{account.ZbsPoints}");
        embed.AddField("Стоимость", $"Уменьшить шанс на 1% - {cost} ZP\n" +
                                    $"Уменьшить шанс на 10% - {cost10} ZP\n\n" +
                                    $"Увеличить шанс на 1% - {cost} ZP\n" +
                                    $"Увеличить шанс на 10% - {cost10} ZP\n\n" +
                                    "Вернуть все ZBS Points за **этого** персонажа - 0 ZP\n" +
                                    "Вернуть все ZBS Points за **всех** персонажей - 0 ZP\n");

        embed.WithFooter("WELCOME! Straaanger...");
        embed.WithColor(Color.DarkPurple);
        
        embed.WithThumbnailUrl(character!.AvatarCurrent);
        embed.WithImageUrl(GetMerchantGif());

        return embed;
    }


    public async Task ModifyStoreMessage(SocketMessageComponent button, DiscordAccountClass.CharacterChances character,
        DiscordAccountClass account)
    {
        var builder = new ComponentBuilder();
        var embed = GetStoreEmbed(button.User, character.CharacterName);
        var i = 0;
        foreach (var b in GetStoreButtons())
        {
            i++;
            switch (i)
            {
                case > 0 and <= 2:
                    builder.WithButton(b);
                    break;
                case > 2 and <= 4:
                    builder.WithButton(b, 1);
                    break;
                case > 4 and <= 6:
                    builder.WithButton(b, 2);
                    break;
                case > 6:
                    builder.WithButton(b, 3);
                    break;
            }
        }

        builder.WithSelectMenu(GetStoreCharacterSelectMenu(account), 2);

        await button.Message.ModifyAsync(message =>
        {
            message.Embed = embed.Build();
            message.Components = builder.Build();
        });
    }


    public async Task ReactionAddedStore(SocketMessageComponent button)
    {
        try
        {
            if (!button.Data.CustomId.Contains("store")) return;

            var titleStr = button.Message.Embeds.FirstOrDefault()?.Title;

            if (titleStr == null)
                //await channel.SendMessageAsync("ERROR: Embed Title == null");
                return;

            var title = titleStr.Split(" - ");

            if (title.Length < 2)
                //await channel.SendMessageAsync("ERROR: Embed Title len < 2");
                return;

            if (title.First() != "Магазин") return;

            var account = _userAccounts.GetAccount(button.User.Id);
            var character = account.CharacterChance.Find(x => x.CharacterName == title[1]);

            if (character == null)
            {
                await button.Channel.SendMessageAsync($"ERROR: character named {title[1]} was not found");
                return;
            }

            var cost = _basePrice + character.Changes;
            var cost10 = 0;
            for (var i = 0; i < 10; i++)
            {
                cost10 += _basePrice + character.Changes + i;
            }

            switch (button.Data.CustomId)
            {
                case "store-select-character":
                    character = account.CharacterChance.Find(
                        x => x.CharacterName == string.Join("", button.Data.Values));
                    await ModifyStoreMessage(button, character, account);
                    break;
                //Уменьшить шанс на 1% - 20 ZP
                case "store-down-1":

                    if (character.Multiplier <= 0.5)
                    {
                        await button.Channel.SendMessageAsync(
                            $"У персонажа {character.CharacterName} и так минимальный бонусный шанс - {character.Multiplier}");
                        return;
                    }

                    if (account.ZbsPoints < cost)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    character.Multiplier -= 0.01;
                    character.Changes++;
                    account.ZbsPoints -= cost;

                    await ModifyStoreMessage(button, character, account);
                    break;

                case "store-down-10":

                    if (character.Multiplier <= 0.5)
                    {
                        await button.Channel.SendMessageAsync(
                            $"У персонажа {character.CharacterName} и так минимальный бонусный шанс - {character.Multiplier}");
                        return;
                    }

                    if (account.ZbsPoints < cost10)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost10}.");
                        return;
                    }

                    character.Multiplier -= 0.1;
                    character.Changes += 10;
                    account.ZbsPoints -= cost10;

                    await ModifyStoreMessage(button, character, account);
                    break;

                //Увеличить шанс на 1% - 20 ZP
                case "store-up-1":

                    if (character.Multiplier >= 2.0)
                    {
                        await button.Channel.SendMessageAsync(
                            $"У персонажа {character.CharacterName} и так максимальный бонусный шанс - {character.Multiplier}");
                        return;
                    }

                    if (account.ZbsPoints < cost)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    character.Multiplier += 0.01;
                    character.Changes++;
                    account.ZbsPoints -= cost;

                    await ModifyStoreMessage(button, character, account);
                    break;
                //Увеличить шанс на 10% - 20 ZP
                case "store-up-10":
                    if (character.Multiplier >= 2.0)
                    {
                        await button.Channel.SendMessageAsync(
                            $"У персонажа {character.CharacterName} и так максимальный бонусный шанс - {character.Multiplier}");
                        return;
                    }


                    
                    if (account.ZbsPoints < cost10)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost10}.");
                        return;
                    }

                    character.Multiplier += 0.1;
                    character.Changes += 10;
                    account.ZbsPoints -= cost10;

                    await ModifyStoreMessage(button, character, account);
                    break;

                //Вернуть все ZBS Points за этого персонажа - 10 ZP
                case "store-return-character":

                    if (account.ZbsPoints < cost)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    var zbsPointsToReturn = 0;

                    for (var i = 1; i < character.Changes+1; i++)
                    {
                        zbsPointsToReturn += _basePrice + character.Changes - i;
                    }

                    character.Multiplier = 1.0;
                    character.Changes = 0;
                    account.ZbsPoints += zbsPointsToReturn;


                    await ModifyStoreMessage(button, character, account);
                    break;

                //Вернуть все ZBS Points за ВСЕХ персонажей - 50 ZP
                case "store-return-all-characters":

                    if (account.ZbsPoints < cost)
                    {
                        await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    zbsPointsToReturn = 0;

                    foreach (var c in account.CharacterChance)
                    {
                        for (var i = 1; i < c.Changes + 1; i++)
                        {
                            zbsPointsToReturn += _basePrice + c.Changes - i;
                        }
                        c.Multiplier = 1.0;
                        c.Changes = 0;
                    }

                    account.ZbsPoints += zbsPointsToReturn;


                    await ModifyStoreMessage(button, character, account);
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