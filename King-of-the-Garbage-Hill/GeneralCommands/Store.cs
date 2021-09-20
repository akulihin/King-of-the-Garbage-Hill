using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Store;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class Store : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;
        private readonly AwaitForUserMessage _awaitForUserMessage;
        private readonly StoreLogic _storeLogic;

        public Store(UserAccounts userAccounts, AwaitForUserMessage awaitForUserMessage, StoreLogic storeLogic)
        {
            _accounts = userAccounts;
            _awaitForUserMessage = awaitForUserMessage;
            _storeLogic = storeLogic;
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
            foreach (var c in account.CharacterChance)
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

            var chosenCharacter = choiceList.Find(x => x.Index == choice);

            if (chosenCharacter == null)
            {
                await SendMessAsync("Ты не выбрал персонажа которого не было в списке");
                return;
            }

            var character = account.CharacterChance.Find(x => x.CharacterName == chosenCharacter.CharacterName);

            var socketMsg = await SendMessAsync(_storeLogic.GetStoreEmbed(character, account, Context.User));
            await socketMsg.AddReactionsAsync(new IEmote[]
                {new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣")});
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