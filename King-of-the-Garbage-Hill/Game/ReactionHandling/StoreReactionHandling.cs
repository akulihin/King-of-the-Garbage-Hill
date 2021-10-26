using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public class StoreReactionHandling : IServiceSingleton
    {
        private readonly CharactersPull _charactersPull;
        private readonly UserAccounts _userAccounts;
        private readonly SecureRandom _random;

        public StoreReactionHandling(UserAccounts userAccounts, CharactersPull charactersPull, SecureRandom random)
        {
            _userAccounts = userAccounts;
            _charactersPull = charactersPull;
            _random = random;
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


            foreach (var character in account.SeenCharacters)
            {
                characterMenu.AddOption(character , character);
            }

            return characterMenu;
        }


        public List<ButtonBuilder> GetStoreButtons()
        {
            var buttons = new List<ButtonBuilder>
            {
                new("Поднять шанс на 1% ", "store-up-1", ButtonStyle.Secondary),
                new("Опустить шанс на 1%", "store-down-1", ButtonStyle.Secondary),
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
            var allCharacters = _charactersPull.GetAllCharacters();
            var account = _userAccounts.GetAccount(user);
            var characterChance = account.CharacterChance.Find(x => x.CharacterName == characterName);
            var character = allCharacters.Find(x => x.Name == characterName);
            var embed = new EmbedBuilder();

            embed.WithAuthor(user);
            embed.WithTitle($"Магазин - {characterChance.CharacterName}");

            embed.AddField("Персонаж:", $"{characterChance.CharacterName}", true);
            embed.AddField("Тир:", $"{character.Tier}", true);
            embed.AddField("Бонусный шанс:", $"{characterChance.Multiplier}", true);
            embed.AddField("ZBS Points:", $"{account.ZbsPoints}");
            embed.AddField("Стоимость", $"Уменьшить шанс на 1% - 20 ZP\n" +
                                      $"Увеличить шанс на 1% - 20 ZP\n" +
                                      $"Вернуть все ZBS Points за **этого** персонажа - ~~10~~ 0 ZP\n" +
                                      $"Вернуть все ZBS Points за **всех** персонажей - ~~50~~ 0 ZP\n");
            embed.WithCurrentTimestamp();
            embed.WithFooter("WELCOME! Straaanger...");
            embed.WithColor(Color.DarkPurple);
            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);
            embed.WithImageUrl(GetMerchantGif());

            return embed;

        }


        public async Task ModifyStoreMessage(SocketMessageComponent button, DiscordAccountClass.CharacterChances character, DiscordAccountClass account)
        {
            var builder = new ComponentBuilder();
            var embed = GetStoreEmbed(button.User, character.CharacterName);
            var i = 0;
            foreach (var b in GetStoreButtons())
            {
                i++;
                if (i > 2)
                    builder.WithButton(b, 1);
                else
                    builder.WithButton(b);
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
                var title_str = button.Message.Embeds.FirstOrDefault()?.Title;

                if (title_str == null)
                    //await channel.SendMessageAsync("ERROR: Embed Title == null");
                    return;

                var title = title_str.Split(" - ");

                if (title.Length < 2)
                    //await channel.SendMessageAsync("ERROR: Embed Title len < 2");
                    return;

                if (title[0] != "Магазин") return;

                var account = _userAccounts.GetAccount(button.User.Id);
                var character = account.CharacterChance.Find(x => x.CharacterName == title[1]);

                if (character == null)
                {
                    await button.Channel.SendMessageAsync($"ERROR: character named {title[1]} was not found");
                    return;
                }

                var cost = 10 + character.Changes;

                switch (button.Data.CustomId)
                {
                    case "store-select-character":
                        character = account.CharacterChance.Find(x => x.CharacterName == string.Join("", button.Data.Values));
                        await ModifyStoreMessage(button, character, account);
                        break;
                    //Уменьшить шанс на 1% - 20 ZP
                    case "store-down-1":
                        
                        if (character.Multiplier <= 0.0)
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

                    //Вернуть все ZBS Points за этого персонажа - 10 ZP
                    case "store-return-character":
                       
                        if (account.ZbsPoints < cost)
                        {
                            await button.Channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                            return;
                        }

                        character.Multiplier = 1.0;
                        var zbsPointsToReturn = character.Changes * 20;
                        account.ZbsPoints += zbsPointsToReturn;
                        account.ZbsPoints -= cost;
                        character.Changes = 0;


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
                        account.ZbsPoints -= cost;

                        foreach (var c in account.CharacterChance)
                        {
                            c.Multiplier = 1.0;
                            zbsPointsToReturn += c.Changes * 20;
                            c.Changes = 0;
                        }

                        account.ZbsPoints += zbsPointsToReturn;


                        await ModifyStoreMessage(button, character, account);
                        break;
                }
            }
            catch
            {
                //ingored
            }
        }
    }
}