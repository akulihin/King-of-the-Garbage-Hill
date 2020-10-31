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

        private readonly List<DiscordAccountClass.ChampionChances> _defaultChampionChances =
            new List<DiscordAccountClass.ChampionChances>
            {
                new DiscordAccountClass.ChampionChances("DeepList", 7),
                new DiscordAccountClass.ChampionChances("mylorik", 7),
                new DiscordAccountClass.ChampionChances("Глеб", 7),
                new DiscordAccountClass.ChampionChances("LeCrisp", 7),
                new DiscordAccountClass.ChampionChances("Толя", 7),
                new DiscordAccountClass.ChampionChances("HardKitty", 7),
                new DiscordAccountClass.ChampionChances("Sirinoks", 7),
                new DiscordAccountClass.ChampionChances("Mit*suki*", 7),
                new DiscordAccountClass.ChampionChances("AWDKA", 7),
                new DiscordAccountClass.ChampionChances("Осьминожка", 2),
                new DiscordAccountClass.ChampionChances("Darksci", 7),
                new DiscordAccountClass.ChampionChances("Тигр", 7),
                new DiscordAccountClass.ChampionChances("Братишка", 2),
                new DiscordAccountClass.ChampionChances("Загадочный Спартанец в маске", 5),
                new DiscordAccountClass.ChampionChances("Вампур", 5)
            };

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
                GameId = 1000000,
                ZBSPoints = 0,
                ChampionChance = _defaultChampionChances
            };

            if (newAccount.DiscordUserName.Contains("<:war:561287719838547981>"))
                newAccount.DiscordUserName =
                    newAccount.DiscordUserName.Replace("<:war:561287719838547981>", "404-228-1448");

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
                IsLogs = false,
                IsPlaying = false,
                GameId = 1000000,
                UserType = "player",
                ZBSPoints = 0,
                ChampionChance = _defaultChampionChances
            };

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

            return newAccount;
        }
    }
}