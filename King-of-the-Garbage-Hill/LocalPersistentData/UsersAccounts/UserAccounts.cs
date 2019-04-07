using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
    public sealed class UserAccounts : IServiceSingleton
    {
        private readonly DiscordShardedClient _client;
        private readonly ConcurrentDictionary<ulong, DiscordAccountClass> _userAccountsDictionary;
        private readonly UserAccountsDataStorage _usersDataStorage;

        public UserAccounts(DiscordShardedClient client, UserAccountsDataStorage usersDataStorage)
        {
            _client = client;
            _usersDataStorage = usersDataStorage;
            _userAccountsDictionary = _usersDataStorage.LoadAllAccounts();
            ClearPlayingStatus();
            SaveAllAccountsTimer();
        }

        private Timer LoopingTimer { get; set; }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }


        public void SaveAllAccountsTimer()
        {
            LoopingTimer = new Timer
            {
                AutoReset = true,
                Interval = 30000,
                Enabled = true
            };

            LoopingTimer.Elapsed += SaveAllAccounts;
        }

        public void ClearPlayingStatus()
        {
            var accounts = GetAllAccount();
            foreach (var a in accounts)
            {
                a.GameId = 1000000000000000000;
                a.IsPlaying = false;
                SaveAccounts(a.DiscordId);
            }

            SaveAllAccounts(null, null);
        }

        public DiscordAccountClass GetOrAddUserAccount(ulong userId)
        {
            _userAccountsDictionary.TryGetValue(userId, out var account);
            return account;
        }

        public DiscordAccountClass GetAccount(IUser user)
        {
            return GetOrCreateAccount(user);
        }

        public DiscordAccountClass GetAccount(ulong userId)
        {
            //return a human
            if (userId > 1000000)
                return GetOrCreateAccount(_client.GetUser(userId));


            //return a bot


            _userAccountsDictionary.TryGetValue(userId, out var account);

            if (account != null)
                return account;

            return CreateBotAccount(userId);
        }

        public DiscordAccountClass GetOrCreateAccount(IUser user)
        {
            var accounts = GetOrAddUserAccount(user.Id);
            var account = accounts ?? CreateUserAccount(user);
            return account;
        }


        //obsolete 
        public void SaveAccounts(ulong userId)
        {
            //    var accounts = GetOrAddUserAccount(userId);
            //    _usersDataStorage.SaveAccountSettings(accounts, userId);
        }

        //obsolete 
        public void SaveAccounts(IUser user)
        {
            //    var accounts = GetOrAddUserAccount(user.Id);
            //    _usersDataStorage.SaveAccountSettings(accounts, user.Id);
        }

        //obsolete 
        public void SaveAccounts(DiscordAccountClass user)
        {
            //    var accounts = GetOrAddUserAccount(user.DiscordId);
            //    _usersDataStorage.SaveAccountSettings(accounts, user.DiscordId);
        }

        private void SaveAllAccounts(object sender, ElapsedEventArgs e)
        {
            foreach (var account in _userAccountsDictionary.Values)
                _usersDataStorage.SaveAccountSettings(account, account.DiscordId);
        }


        public List<DiscordAccountClass> GetAllAccount()
        {
            var accounts = new List<DiscordAccountClass>();
            foreach (var account in _userAccountsDictionary.Values) accounts.Add(account);
            return accounts;
        }

        public DiscordAccountClass CreateUserAccount(IUser user)
        {
            var newAccount = new DiscordAccountClass
            {
                DiscordId = user.Id,
                DiscordUserName = user.Username,
                IsLogs = true,
                IsPlaying = false,
                GameId = 1000000
            };

            if (newAccount.DiscordUserName.Contains("<:war:561287719838547981>"))
                newAccount.DiscordUserName =
                    newAccount.DiscordUserName.Replace("<:war:561287719838547981>", "404-228-1448");

            if (newAccount.DiscordUserName.Contains("⟶"))
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("⟶", "404-228-1448");

            if (newAccount.DiscordUserName.Contains("\n"))
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("\n", "404-228-1448");

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);
            SaveAccounts(user);
            return newAccount;
        }

        public DiscordAccountClass CreateBotAccount(ulong botId)
        {
            var newAccount = new DiscordAccountClass
            {
                DiscordId = botId,
                DiscordUserName = "BOT",
                IsLogs = false,
                IsPlaying = false,
                GameId = 1000000
            };

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);
            SaveAccounts(botId);
            return newAccount;
        }
    }
}