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
        private static readonly ConcurrentDictionary<ulong, List<MainAccountClass>> UserAccountsDictionary =
            new ConcurrentDictionary<ulong, List<MainAccountClass>>();

        private readonly DiscordShardedClient _client;
        private readonly UsersDataStorage _usersDataStorage;

        public UserAccounts(DiscordShardedClient client, UsersDataStorage usersDataStorage)
        {
            _client = client;
            _usersDataStorage = usersDataStorage;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }


        public List<MainAccountClass> GetOrAddUserAccountsForGuild(ulong userId)
        {
            return UserAccountsDictionary.GetOrAdd(userId, x => _usersDataStorage.LoadAccountSettings(userId).ToList());
        }

        public MainAccountClass GetAccount(IUser user)
        {
            return GetOrCreateAccount(user);
        }

        public MainAccountClass GetAccount(ulong userId)
        {
            if (userId > 1000)
                return GetOrCreateAccount(_client.GetUser(userId));
            return UserAccountsDictionary.GetOrAdd(userId, x => _usersDataStorage.LoadAccountSettings(userId).ToList())
                .FirstOrDefault();
        }

        /*
        public MainAccountClass GetBotAccount(ulong botId)
        {
            return UserAccountsDictionary.GetOrAdd(botId, x => UsersDataStorage.LoadAccountSettings(botId).ToList()).FirstOrDefault();
        }
        */
        public MainAccountClass GetOrCreateAccount(IUser user)
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

        public void SaveAccounts(MainAccountClass user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.DiscordId);
            _usersDataStorage.SaveAccountSettings(accounts, user.DiscordId);
        }


        public List<MainAccountClass> GetAllAccount()
        {
            var accounts = new List<MainAccountClass>();
            foreach (var values in UserAccountsDictionary.Values) accounts.AddRange(values);
            return accounts;
        }

        public MainAccountClass CreateUserAccount(IUser user)
        {
            var accounts = GetOrAddUserAccountsForGuild(user.Id);

            var newAccount = new MainAccountClass
            {
                DiscordId = user.Id,
                DiscordUserName = user.Username
            };

            accounts.Add(newAccount);
            SaveAccounts(user);
            return newAccount;
        }
    }
}