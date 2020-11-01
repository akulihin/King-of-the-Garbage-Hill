using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.BotFramework.Extensions;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class Store : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;
        private readonly AwaitForUserMessage _awaitForUserMessage;

        public Store(UserAccounts userAccounts, AwaitForUserMessage awaitForUserMessage)
        {
            _accounts = userAccounts;
            _awaitForUserMessage = awaitForUserMessage;
        }

        [Command("store")]
        [Alias("магазин")]
        [Summary(
            "Открывает магазин")]
        public async Task StartStore()
        {
            var account = _accounts.GetAccount(Context.User);

            var text = "Напиши в чат цифру персонажа, которого хочешь изменить\n\n";
            var choiceList = new List<StoreChoice>();

            var i = 1;
            foreach (var c in account.ChampionChance)
            {
                text += $"{i}. {c.CharacterName}\n";
                choiceList.Add(new StoreChoice(i, c.CharacterName));
                i++;
            }

            await SendMessAsync(text);

            var response = await _awaitForUserMessage.AwaitMessage(Context.User.Id, Context.Channel.Id, 10);

            if (response == null)
            {
                await SendMessAsync("Ты не выбрал персонажа");
                return;
            }

            var choice = 0;

            try
            {
                choice = Convert.ToInt32(response.Content);
            }
            catch
            {
                await SendMessAsync("Ты написал не цифру.");
                return;
            }

            var chosenChampion = choiceList.Find(x => x.Index == choice);

            if (chosenChampion == null)
            {
                await SendMessAsync("Ты не выбрал персонажа которого не было в списке");
                return;
            }

            var champion = account.ChampionChance.Find(x => x.CharacterName == chosenChampion.CharacterName);

            var embed = new EmbedBuilder();
            embed.WithAuthor(Context.User);
            embed.WithTitle("Магазин");
            embed.WithDescription($"Ты выбрал персонажа **{champion.CharacterName}**");
            embed.AddField("Текущий бонусный шанс", $"{champion.Multiplier}");
            embed.AddField("Текущее Количество ZBS Points", $"{account.ZBSPoints}");
            embed.AddField("Варинты", $"{new Emoji("1⃣")} сбросить шанс до 0 - 50 ZP\n" +
                                      $"{new Emoji("2⃣")} Уменьшить шанс на 1% - 20 ZP\n" +
                                      $"{new Emoji("3⃣")} Увеличить шанс на 1% - 20 ZP");
            embed.WithCurrentTimestamp();
            embed.WithFooter("WELCOME! Stranger...");
            embed.WithColor(Color.DarkPurple);
            embed.WithThumbnailUrl(
                "https://media.giphy.com/media/lbAgIgQ6Dytkk/giphy.gif");
            var socketMsg = await SendMessAsync(embed);
            await socketMsg.AddReactionsAsync(new IEmote[] {new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣") });

        }

        public class StoreChoice
        {
            public string CharacterName;
            public int Index;

            public StoreChoice(int index, string characterName)
            {
                Index = index;
                CharacterName = characterName;
            }
        }
    }
}