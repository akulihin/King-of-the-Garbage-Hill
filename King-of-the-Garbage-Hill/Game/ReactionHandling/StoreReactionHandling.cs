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
            await cash.DownloadAsync();
            if (!cash.HasValue) return;
            var title = cash.Value.Embeds.FirstOrDefault()?.Title.Split(" - ");
            if (title == null)
            {
                await channel.SendMessageAsync("ERROR: Embed Title == null");
                return;
            }

            if (title.Length < 2)
            {
                await channel.SendMessageAsync("ERROR: Embed Title len < 2");
                return;
            }

            if (title[0] != "Магазин") return;

            var account = _userAccounts.GetAccount(reaction.UserId);
            var champion = account.ChampionChance.Find(x => x.CharacterName == title[1]);

            if (champion == null)
            {
                await channel.SendMessageAsync($"ERROR: champion named {title[1]} was not found");
                return;
            }

            var cost = 0;
            
            switch (reaction.Emote.Name)
            {

                //Уменьшить шанс на 1% - 20 ZP
                case "1⃣":
                    cost = 20;
                    if (champion.Multiplier <= 0.0)
                    {
                        await channel.SendMessageAsync(
                            $"У персонажа {champion.CharacterName} и так минимальный бонусный шанс - {champion.Multiplier}");
                        return;
                    }

                    if (account.ZbsPoints < cost)
                    {
                        await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    champion.Multiplier -= 0.01;
                    champion.Changes++;
                    account.ZbsPoints -= cost;

                    await channel.SendMessageAsync(
                        $"Готово. Бонусный шанш {champion.CharacterName} = {champion.Multiplier}");

                    await cash.Value.ModifyAsync(message =>
                    {
                        message.Content = "";
                        message.Embed = null;
                        message.Embed = _storeLogic.GetStoreEmbed(champion, account, reaction.User.Value).Build();
                    });
                    break;

                //Увеличить шанс на 1% - 20 ZP
                case "2⃣":
                    cost = 20;
                    if (champion.Multiplier >= 2.0)
                    {
                        await channel.SendMessageAsync(
                            $"У персонажа {champion.CharacterName} и так максимальный бонусный шанс - {champion.Multiplier}");
                        return;
                    }

                    if (account.ZbsPoints < cost)
                    {
                        await channel.SendMessageAsync($"У тебя недостаточно ZBS Points, нужно {cost}.");
                        return;
                    }

                    champion.Multiplier += 0.01;
                    champion.Changes++;
                    account.ZbsPoints -= cost;

                    await channel.SendMessageAsync(
                        $"Готово. Бонусный шанш {champion.CharacterName} = {champion.Multiplier}");

                    await cash.Value.ModifyAsync(message =>
                    {
                        message.Content = "";
                        message.Embed = null;
                        message.Embed = _storeLogic.GetStoreEmbed(champion, account, reaction.User.Value).Build();
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

                    champion.Multiplier = 1.0;
                    var zbsPointsToReturn = champion.Changes * 20;
                    account.ZbsPoints += zbsPointsToReturn;
                    account.ZbsPoints -= cost;
                    champion.Changes = 0;

                    await channel.SendMessageAsync(
                        $"Готово. Бонусный шанш {champion.CharacterName} = {champion.Multiplier}\n" +
                        $"Ты вернул {zbsPointsToReturn} ZBS Points");

                    await cash.Value.ModifyAsync(message =>
                    {
                        message.Content = "";
                        message.Embed = null;
                        message.Embed = _storeLogic.GetStoreEmbed(champion, account, reaction.User.Value).Build();
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

                    foreach (var c in account.ChampionChance)
                    {
                        c.Multiplier = 1.0;
                        zbsPointsToReturn += c.Changes * 20;
                        c.Changes = 0;
                    }

                    account.ZbsPoints += zbsPointsToReturn;
                    await channel.SendMessageAsync(
                        $"Готово. Бонусный шанш **Всех Персонажей** = {champion.Multiplier}\n" +
                        $"Ты вернул {zbsPointsToReturn} ZBS Points");

                    await cash.Value.ModifyAsync(message =>
                    {
                        message.Content = "";
                        message.Embed = null;
                        message.Embed = _storeLogic.GetStoreEmbed(champion, account, reaction.User.Value).Build();
                    });
                    break;
            }
        }
    }
}