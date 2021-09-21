using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Store;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public class StoreReactionHandling : IServiceSingleton
    {
        private readonly StoreLogic _storeLogic;
        private readonly UserAccounts _userAccounts;

        public StoreReactionHandling(UserAccounts userAccounts, StoreLogic storeLogic)
        {
            _userAccounts = userAccounts;
            _storeLogic = storeLogic;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ReactionAddedStore(Cacheable<IUserMessage, ulong> cash, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            try
            {
                await cash.DownloadAsync();
                if (!cash.HasValue) return;
                var title_str = cash.Value.Embeds.FirstOrDefault()?.Title;

                if (title_str == null)
                    //await channel.SendMessageAsync("ERROR: Embed Title == null");
                    return;

                var title = title_str.Split(" - ");

                if (title.Length < 2)
                    //await channel.SendMessageAsync("ERROR: Embed Title len < 2");
                    return;

                if (title[0] != "Магазин") return;

                var account = _userAccounts.GetAccount(reaction.UserId);
                var character = account.CharacterChance.Find(x => x.CharacterName == title[1]);

                if (character == null)
                {
                    await channel.SendMessageAsync($"ERROR: character named {title[1]} was not found");
                    return;
                }

                var cost = 0;

                switch (reaction.Emote.Name)
                {
                    //Уменьшить шанс на 1% - 20 ZP
                    case "1⃣":
                        cost = 20;
                        if (character.Multiplier <= 0.0)
                        {
                            await channel.SendMessageAsync(
                                $"У персонажа {character.CharacterName} и так минимальный бонусный шанс - {character.Multiplier}");
                            return;
                        }

                        if (account.ZbsPoints < cost)
                        {
                            await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                            return;
                        }

                        character.Multiplier -= 0.01;
                        character.Changes++;
                        account.ZbsPoints -= cost;

                        await channel.SendMessageAsync(
                            $"Готово. Бонусный шанш {character.CharacterName} = {character.Multiplier}");

                        await cash.Value.ModifyAsync(message =>
                        {
                            message.Content = "";
                            message.Embed = null;
                            message.Embed = _storeLogic.GetStoreEmbed(character, account, reaction.User.Value).Build();
                        });
                        break;

                    //Увеличить шанс на 1% - 20 ZP
                    case "2⃣":
                        cost = 20;
                        if (character.Multiplier >= 2.0)
                        {
                            await channel.SendMessageAsync(
                                $"У персонажа {character.CharacterName} и так максимальный бонусный шанс - {character.Multiplier}");
                            return;
                        }

                        if (account.ZbsPoints < cost)
                        {
                            await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                            return;
                        }

                        character.Multiplier += 0.01;
                        character.Changes++;
                        account.ZbsPoints -= cost;

                        await channel.SendMessageAsync(
                            $"Готово. Бонусный шанш {character.CharacterName} = {character.Multiplier}");

                        await cash.Value.ModifyAsync(message =>
                        {
                            message.Content = "";
                            message.Embed = null;
                            message.Embed = _storeLogic.GetStoreEmbed(character, account, reaction.User.Value).Build();
                        });
                        break;

                    //Вернуть все ZBS Points за этого персонажа - 10 ZP
                    case "3⃣":
                        cost = 0;
                        if (account.ZbsPoints < cost)
                        {
                            await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                            return;
                        }

                        character.Multiplier = 1.0;
                        var zbsPointsToReturn = character.Changes * 20;
                        account.ZbsPoints += zbsPointsToReturn;
                        account.ZbsPoints -= cost;
                        character.Changes = 0;

                        await channel.SendMessageAsync(
                            $"Готово. Бонусный шанш {character.CharacterName} = {character.Multiplier}\n" +
                            $"Ты вернул {zbsPointsToReturn} ZBS Points");

                        await cash.Value.ModifyAsync(message =>
                        {
                            message.Content = "";
                            message.Embed = null;
                            message.Embed = _storeLogic.GetStoreEmbed(character, account, reaction.User.Value).Build();
                        });
                        break;

                    //Вернуть все ZBS Points за ВСЕХ персонажей - 50 ZP
                    case "4⃣":
                        cost = 0;
                        if (account.ZbsPoints < cost)
                        {
                            await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
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
                        await channel.SendMessageAsync(
                            $"Готово. Бонусный шанш **Всех Персонажей** = {character.Multiplier}\n" +
                            $"Ты вернул {zbsPointsToReturn} ZBS Points");

                        await cash.Value.ModifyAsync(message =>
                        {
                            message.Content = "";
                            message.Embed = null;
                            message.Embed = _storeLogic.GetStoreEmbed(character, account, reaction.User.Value).Build();
                        });
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