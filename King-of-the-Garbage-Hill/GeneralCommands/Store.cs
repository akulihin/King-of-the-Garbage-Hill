using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using King_of_the_Garbage_Hill.BotFramework.Extensions;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class Store : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;

        public Store(UserAccounts userAccounts)
        {
            _accounts = userAccounts;
        }

        [Command("store")]
        [Alias("магазин")]
        [Summary(
            "Открывает магазин")]
        public async Task StartStore()
        {
            await Task.CompletedTask;
        }
    }
}
