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
        private Timer _loopingTimer;

        public UserAccounts(DiscordShardedClient client, UserAccountsDataStorage usersDataStorage)
        {
            _client = client;
            _usersDataStorage = usersDataStorage;
            _userAccountsDictionary = _usersDataStorage.LoadAllAccounts();
            ClearPlayingStatus();
            SaveAllAccountsTimer();
        }


        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }


        internal Task SaveAllAccountsTimer()
        {
            _loopingTimer = new Timer
            {
                AutoReset = true,
                Interval = 60000,
                Enabled = true
            };
            _loopingTimer.Elapsed += SaveAllAccounts;
            return Task.CompletedTask;
        }

        public void ClearPlayingStatus()
        {
            var accounts = GetAllAccount();
            foreach (var a in accounts) a.IsPlaying = false;
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


        private void SaveAllAccounts(object sender, ElapsedEventArgs e)
        {
            foreach (var a in _userAccountsDictionary)
                _usersDataStorage.SaveAccountSettings(a.Value, a.Key);
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
                IsPlaying = false,
                PlayerType = 0,
                ZbsPoints = 0,
                IsNewPlayer = true,
                PassedTutorial = false,
                MyPrefix = "*",
            };

            if (newAccount.DiscordUserName.Contains("<:war:561287719838547981>"))
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("<:war:561287719838547981>", "404-228-1448");

            if (newAccount.DiscordUserName.Contains("⟶"))
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("⟶", "404-228-1448");

            if (newAccount.DiscordUserName.Contains("\n"))
                newAccount.DiscordUserName = newAccount.DiscordUserName.Replace("\n", "404-228-1448");

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

            return newAccount;
        }

        public DiscordAccountClass CreateBotAccount(ulong botId)
        {
            var newAccount = new DiscordAccountClass
            {
                DiscordId = botId,
                DiscordUserName = "BOT",
                IsPlaying = false,
                PlayerType = 404,
                ZbsPoints = 0,
                IsNewPlayer = true,
                PassedTutorial = false,
                MyPrefix = "*",
            };

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

            return newAccount;
        }
    }
}