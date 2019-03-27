using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;


namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
    public sealed class UserAccounts : IServiceSingleton
    {
        private static readonly ConcurrentDictionary<ulong, List<DiscordAccountClass>> UserAccountsDictionary =
            new ConcurrentDictionary<ulong, List<DiscordAccountClass>>();

        private readonly DiscordShardedClient _client;
        private readonly UserAccountsDataStorage _usersDataStorage;

        public UserAccounts(DiscordShardedClient client, UserAccountsDataStorage usersDataStorage)
        {
            _client = client;
            _usersDataStorage = usersDataStorage;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }


        public List<DiscordAccountClass> GetOrAddUserAccountsForGuild(ulong userId)
        {
            return UserAccountsDictionary.GetOrAdd(userId, x => _usersDataStorage.LoadAccountSettings(userId).ToList());
        }

        public DiscordAccountClass GetAccount(IUser user)
        {
            return GetOrCreateAccount(user);
        }

        public DiscordAccountClass GetAccount(ulong userId)
        {
            if (userId > 1000)
                return GetOrCreateAccount(_client.GetUser(userId));

            var toRet = UserAccountsDictionary.GetOrAdd(userId, x => _usersDataStorage.LoadAccountSettings(userId).ToList())
                .FirstOrDefault();

            if (toRet == null && userId < 1000)
            {
                return CreateBotAccount(userId);
            }
            return toRet;
        }




        /*
        public DiscordAccountClass GetBotAccount(ulong botId)
        {
            return UserAccountsDictionary.GetOrAdd(botId, x => UsersDataStorage.LoadAccountSettings(botId).ToList()).FirstOrDefault();
        }
        */
        public DiscordAccountClass GetOrCreateAccount(IUser user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.Id);
            var account = accounts.FirstOrDefault() ?? CreateUserAccount(user);
            return account;
        }


        public void SaveAccounts(ulong userId)
        {
            var accounts = GetOrAddUserAccountsForGuild(userId);
            _usersDataStorage.SaveAccountSettings(accounts, userId);
        }

        public void SaveAccounts(IUser user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.Id);
            _usersDataStorage.SaveAccountSettings(accounts, user.Id);
        }

        public void SaveAccounts(DiscordAccountClass user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.DiscordId);
            _usersDataStorage.SaveAccountSettings(accounts, user.DiscordId);
        }


        public List<DiscordAccountClass> GetAllAccount()
        {
            var accounts = new List<DiscordAccountClass>();
            foreach (var values in UserAccountsDictionary.Values) accounts.AddRange(values);
            return accounts;
        }

        public DiscordAccountClass CreateUserAccount(IUser user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.Id);

            var newAccount = new DiscordAccountClass
            {
                DiscordId = user.Id,
                DiscordUserName = user.Username,
                IsLogs = true
            };

            if (newAccount.DiscordUserName.Contains("<:war:557070460324675584>"))
            {
                newAccount.DiscordUserName =   newAccount.DiscordUserName.Replace("<:war:557070460324675584>", "404-228-1448");
            }

            if (newAccount.DiscordUserName.Contains("⟶"))
            {
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("⟶", "404-228-1448");
            }

            if (newAccount.DiscordUserName.Contains("\n"))
            {
                newAccount.DiscordUserName =  newAccount.DiscordUserName.Replace("\n", "404-228-1448");
            }

            accounts.Add(newAccount);
            SaveAccounts(user);
            return newAccount;
        }

        public DiscordAccountClass CreateBotAccount(ulong botId)
        {
            var accounts = GetOrAddUserAccountsForGuild(botId);

            var newAccount = new DiscordAccountClass
            {
                DiscordId = botId,
                DiscordUserName = "BOT",
                IsLogs = false
            };

            accounts.Add(newAccount);
            SaveAccounts(botId);
            return newAccount;
        }
    }
}