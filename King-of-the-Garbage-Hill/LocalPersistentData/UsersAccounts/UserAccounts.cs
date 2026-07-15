using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

public sealed class UserAccounts : IServiceSingleton
{
    private readonly DiscordShardedClient _client;


    private readonly ConcurrentDictionary<ulong, DiscordAccountClass> _userAccountsDictionary;
    private readonly UserAccountsDataStorage _usersDataStorage;
    private Timer _loopingTimer;
    private int _saving;
    private string _executionPath;

    private static ulong _nextWebId = 9_000_000_000_000_000_000;
    private static readonly object _webIdLock = new();

    /// <summary>
    /// Set by <c>--sim</c> before the container is built. The harness only ever plays bot accounts,
    /// which it resets at start anyway, so it neither loads nor writes the account store: startup skips
    /// ~12k file reads, the 60s flush never runs, and — decisively — several simulator processes can run
    /// in parallel without racing each other on <c>DataBase/UserAccounts</c> (a concurrent atomic-replace
    /// could otherwise make a listed file vanish before it was read, killing startup) or corrupting each
    /// other's bot pity/history mid-run.
    /// </summary>
    public static bool DisableDiskPersistence { get; set; }

    public UserAccounts(DiscordShardedClient client, UserAccountsDataStorage usersDataStorage)
    {
        _client = client;
        _usersDataStorage = usersDataStorage;
        _userAccountsDictionary = DisableDiskPersistence
            ? new ConcurrentDictionary<ulong, DiscordAccountClass>()
            : _usersDataStorage.LoadAllAccounts();
        foreach (var (userId, account) in _userAccountsDictionary)
        {
            if (MigrateUnknownBugAccount(account))
                _usersDataStorage.SaveAccountSettings(account, userId);
            GameLocalization.SetUserLanguage(userId, account.Language);
        }
        ClearPlayingStatus();
        if (!DisableDiskPersistence)
            SaveAllAccountsTimer();
        _executionPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
    }


    public async Task InitializeAsync()
    {
        // Resume web ID counter from max existing web account ID + 1
        foreach (var kv in _userAccountsDictionary)
        {
            if (kv.Key >= 9_000_000_000_000_000_000 && kv.Key >= _nextWebId)
                _nextWebId = kv.Key + 1;
        }
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
        // Bot
        if (userId <= 1000000)
        {
            _userAccountsDictionary.TryGetValue(userId, out var account);
            return account ?? CreateBotAccount(userId);
        }

        // Web-only player
        if (userId >= 9_000_000_000_000_000_000)
            return GetOrCreateWebAccount(userId);

        // Discord human
        var user = _client.GetUser(userId);
        if (user == null)
        {
            // User not in cache — fall back to dictionary lookup
            _userAccountsDictionary.TryGetValue(userId, out var cached);
            return cached;
        }
        return GetOrCreateAccount(user);
    }

    public DiscordAccountClass GetOrCreateAccount(IUser user)
    {
        var accounts = GetOrAddUserAccount(user.Id);
        var account = accounts ?? CreateUserAccount(user);
        return account;
    }


    public bool SaveAccount(DiscordAccountClass account)
    {
        if (account == null) return false;
        // Reported as a successful write: sim accounts are in-memory only, and callers roll a
        // transaction back on false.
        if (DisableDiskPersistence) return true;

        lock (account)
        {
            return _usersDataStorage.SaveAccountSettings(account, account.DiscordId);
        }
    }

    private static bool MigrateUnknownBugAccount(DiscordAccountClass account)
    {
        if (account == null) return false;

        var changed = false;

        if (account.CharacterChance != null)
        {
            var secretChances = account.CharacterChance
                .Where(chance => UnknownBug.Is(chance.CharacterName))
                .ToList();
            if (secretChances.Count > 0)
            {
                var mergedChance = secretChances.Find(chance =>
                                       chance.CharacterName == UnknownBug.CharacterName)
                                   ?? secretChances[0];
                var refund = secretChances.Sum(chance => CalculateStoreRefund(chance.Changes));
                if (refund > 0)
                    account.ZbsPoints += refund;

                if (secretChances.Count > 1
                    || mergedChance.CharacterName != UnknownBug.CharacterName
                    || mergedChance.Multiplier != 1.0
                    || mergedChance.LootBoxBonusPercentagePoints != 0
                    || mergedChance.Changes != 0)
                    changed = true;

                account.CharacterChance.RemoveAll(chance =>
                    UnknownBug.Is(chance.CharacterName) && !ReferenceEquals(chance, mergedChance));
                mergedChance.CharacterName = UnknownBug.CharacterName;
                mergedChance.Multiplier = 1.0;
                mergedChance.LootBoxBonusPercentagePoints = 0;
                mergedChance.Changes = 0;
            }
        }

        if (account.SeenCharacters != null
            && account.SeenCharacters.RemoveAll(UnknownBug.Is) > 0)
            changed = true;

        if (account.CharacterStatistics != null)
        {
            var secretStatistics = account.CharacterStatistics
                .Where(stat => UnknownBug.Is(stat.CharacterName))
                .ToList();
            if (secretStatistics.Count > 0)
            {
                var mergedStatistics = secretStatistics.Find(stat =>
                                           stat.CharacterName == UnknownBug.CharacterName)
                                       ?? secretStatistics[0];
                var mergedPlays = secretStatistics.Aggregate(0UL, (total, stat) => total + stat.Plays);
                var mergedWins = secretStatistics.Aggregate(0UL, (total, stat) => total + stat.Wins);
                var lastPlayedAt = secretStatistics.Max(stat => stat.LastPlayedAt);

                if (secretStatistics.Count > 1
                    || mergedStatistics.CharacterName != UnknownBug.CharacterName)
                    changed = true;

                account.CharacterStatistics.RemoveAll(stat =>
                    UnknownBug.Is(stat.CharacterName) && !ReferenceEquals(stat, mergedStatistics));
                mergedStatistics.CharacterName = UnknownBug.CharacterName;
                mergedStatistics.Plays = mergedPlays;
                mergedStatistics.Wins = mergedWins;
                mergedStatistics.LastPlayedAt = lastPlayedAt;
            }

            if (!account.HasNaturallyRolledUnknownBug
                && secretStatistics.Any(stat => stat.Plays > 0))
            {
                // Completed legacy games are conclusive evidence that the private natural
                // roll happened at least once, even though old account files lacked the bit.
                account.HasNaturallyRolledUnknownBug = true;
                changed = true;
            }
        }

        if (account.MatchHistory != null)
        {
            foreach (var match in account.MatchHistory.Where(match =>
                         UnknownBug.Is(match.CharacterName)
                         && match.CharacterName != UnknownBug.CharacterName))
            {
                match.CharacterName = UnknownBug.CharacterName;
                changed = true;
            }
        }

        if (account.CharacterMastery != null)
        {
            var secretMastery = account.CharacterMastery
                .Where(entry => UnknownBug.Is(entry.Key))
                .ToList();
            if (secretMastery.Count > 0)
            {
                var mergedMastery = secretMastery.Sum(entry => entry.Value);
                if (secretMastery.Count > 1
                    || secretMastery[0].Key != UnknownBug.CharacterName)
                    changed = true;

                foreach (var entry in secretMastery)
                    account.CharacterMastery.Remove(entry.Key);
                account.CharacterMastery[UnknownBug.CharacterName] = mergedMastery;
            }
        }

        if (UnknownBug.Is(account.CharacterPlayedLastTime)
            && account.CharacterPlayedLastTime != UnknownBug.CharacterName)
        {
            account.CharacterPlayedLastTime = UnknownBug.CharacterName;
            changed = true;
        }

        if (UnknownBug.Is(account.CharacterToGiveNextTime))
        {
            account.CharacterToGiveNextTime = null;
            changed = true;
        }

        if (account.Achievements?.Progress != null
            && account.Achievements.Progress.RemoveAll(progress =>
                progress.AchievementId is "c_bug_patch" or "c_bug_release") > 0)
            changed = true;
        if (account.Achievements?.NewlyUnlocked != null
            && account.Achievements.NewlyUnlocked.RemoveAll(id =>
                id is "c_bug_patch" or "c_bug_release") > 0)
            changed = true;

        return changed;
    }

    private static int CalculateStoreRefund(int changes)
    {
        var normalizedChanges = System.Math.Max(0, changes);
        return normalizedChanges * 10
               + normalizedChanges * (normalizedChanges - 1) / 2;
    }

    private void SaveAllAccounts(object sender, ElapsedEventArgs e)
    {
        if("F:\\git\\King-of-the-Garbage-Hill\\King-of-the-Garbage-Hill\\bin\\Debug\\net6.0" == _executionPath) 
            return;
        if (System.Threading.Interlocked.Exchange(ref _saving, 1) != 0)
            return;

        try
        {
            foreach (var account in _userAccountsDictionary.Values)
                SaveAccount(account);
        }
        finally
        {
            System.Threading.Volatile.Write(ref _saving, 0);
        }
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
            PlayerType = 0, // 0 == Normal, 1 == Casual, 2 == Admin, 404 == Bot
            ZbsPoints = 0,
            IsNewPlayer = true,
            PassedTutorial = false,
            MyPrefix = "*",
            Language = GameLocalization.Russian,
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
            IsPlaying = false,
            PlayerType = 404,
            ZbsPoints = 0,
            IsNewPlayer = true,
            PassedTutorial = false,
            MyPrefix = "*",
            Language = GameLocalization.Russian,
        };

        _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

        return newAccount;
    }

    public DiscordAccountClass GetOrCreateWebAccount(ulong webUserId)
    {
        _userAccountsDictionary.TryGetValue(webUserId, out var account);
        return account ?? CreateWebAccount(webUserId, "WebPlayer");
    }

    public DiscordAccountClass CreateWebAccount(ulong webUserId, string username)
    {
        var newAccount = new DiscordAccountClass
        {
            DiscordId = webUserId,
            DiscordUserName = username,
            IsPlaying = false,
            PlayerType = 0,
            ZbsPoints = 0,
            IsNewPlayer = true,
            PassedTutorial = false,
            MyPrefix = "*",
            Language = GameLocalization.Russian,
        };

        _userAccountsDictionary.GetOrAdd(newAccount.DiscordId, newAccount);

        return newAccount;
    }

    public ulong GenerateWebUserId()
    {
        lock (_webIdLock)
        {
            return _nextWebId++;
        }
    }
}
