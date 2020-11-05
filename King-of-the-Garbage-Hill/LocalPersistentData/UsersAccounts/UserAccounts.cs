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

        private readonly List<DiscordAccountClass.CharacterChances> _defaultCharacterChances =
            new List<DiscordAccountClass.CharacterChances>
            {
                //default 100 range
                new DiscordAccountClass.CharacterChances("DeepList", 1, 100),
                new DiscordAccountClass.CharacterChances("mylorik", 101, 200),
                new DiscordAccountClass.CharacterChances("Глеб", 201, 300),
                new DiscordAccountClass.CharacterChances("LeCrisp", 301, 400),
                new DiscordAccountClass.CharacterChances("Толя", 401, 500),
                new DiscordAccountClass.CharacterChances("HardKitty", 501, 600),
                new DiscordAccountClass.CharacterChances("Sirinoks", 601, 700),
                new DiscordAccountClass.CharacterChances("Mit*suki*", 701, 800),
                new DiscordAccountClass.CharacterChances("AWDKA", 801, 900),
                new DiscordAccountClass.CharacterChances("Darksci", 901, 1000),
                
                //default 50 range
                new DiscordAccountClass.CharacterChances("Загадочный Спартанец в маске", 1001, 1050),
                new DiscordAccountClass.CharacterChances("Тигр", 1051, 1100),
                new DiscordAccountClass.CharacterChances("Вампур", 1101, 1150),

                //default 40 range
                new DiscordAccountClass.CharacterChances("Братишка", 1151, 1190),
                new DiscordAccountClass.CharacterChances("Осьминожка", 1191, 1230)
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
                ZbsPoints = 0,
                CharacterChance = _defaultCharacterChances
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
                UserType = "player",
                ZbsPoints = 0,
                CharacterChance = _defaultCharacterChances
            };

            _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

            return newAccount;
        }
    }
}