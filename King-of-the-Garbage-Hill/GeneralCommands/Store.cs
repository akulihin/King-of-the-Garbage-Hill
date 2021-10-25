using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class Store : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;
        private readonly StoreReactionHandling _storeReactionHandling;

        public Store(UserAccounts userAccounts, StoreReactionHandling storeReactionHandling)
        {
            _accounts = userAccounts;
   
            _storeReactionHandling = storeReactionHandling;
        }


        [Command("store")]
        [Alias("магазин")]
        [Summary(
            "Открывает магазин")]
        public async Task StartStore()
        {
            var account = _accounts.GetAccount(Context.User);

            if (account.SeenCharacters.Count == 0)
            {
                await SendMessAsync("Ты еще ни разу не сыграл! Магазин закрыт.");
                return;
            }


            var character = account.CharacterChance.Find(x => x.CharacterName == account.SeenCharacters[0]);

            var builder = new ComponentBuilder();
            var embed = _storeReactionHandling.GetStoreEmbed(Context.User, character.CharacterName);

            var i = 0;
            foreach (var b in _storeReactionHandling.GetStoreButtons())
            {
                i++;
                if (i > 2)
                    builder.WithButton(b, 1);
                else
                    builder.WithButton(b);
            }
            builder.WithSelectMenu(_storeReactionHandling.GetStoreCharacterSelectMenu(account), 2);

            await SendMessAsync(embed, components: builder.Build());
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