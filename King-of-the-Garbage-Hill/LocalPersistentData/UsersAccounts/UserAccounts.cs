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
                //default 100 range
                new DiscordAccountClass.ChampionChances("DeepList", 0, 99),
                new DiscordAccountClass.ChampionChances("mylorik", 100, 199),
                new DiscordAccountClass.ChampionChances("Глеб", 200, 299),
                new DiscordAccountClass.ChampionChances("LeCrisp", 300, 399),
                new DiscordAccountClass.ChampionChances("Толя", 400, 499),
                new DiscordAccountClass.ChampionChances("HardKitty", 500, 599),
                new DiscordAccountClass.ChampionChances("Sirinoks", 600, 699),
                new DiscordAccountClass.ChampionChances("Mit*suki*", 700, 799),
                new DiscordAccountClass.ChampionChances("AWDKA", 800, 899),
                new DiscordAccountClass.ChampionChances("Darksci", 900, 999),
                
                //default 50 range
                new DiscordAccountClass.ChampionChances("Загадочный Спартанец в маске", 1000, 1049),
                new DiscordAccountClass.ChampionChances("Тигр", 1050, 1099),
                new DiscordAccountClass.ChampionChances("Вампур", 1100, 1149),

                //default 40 range
                new DiscordAccountClass.ChampionChances("Братишка", 1150, 1189),
                new DiscordAccountClass.ChampionChances("Осьминожка", 1190, 1229)
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
                Interval = 10000,
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