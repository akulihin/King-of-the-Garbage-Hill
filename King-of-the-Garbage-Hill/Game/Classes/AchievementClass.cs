using System;
using System.Collections.Generic;
using System.Linq;

namespace King_of_the_Garbage_Hill.Game.Classes;

public enum AchievementCategory
{
    Combat,
    Victory,
    Score,
    Character,
    Secret,
    Mastery,
    Social
}

public class AchievementDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string SecretHint { get; set; } // shown before unlock for secret achievements
    public AchievementCategory Category { get; set; }
    public bool IsSecret { get; set; }
    public int Target { get; set; } = 1; // for progress-based achievements
    public string Icon { get; set; } // CSS class or emoji
    public string Rarity { get; set; } // "common", "uncommon", "rare", "epic", "legendary"

    public AchievementDefinition(string id, string name, string description, AchievementCategory category,
        string icon = "star", string rarity = "common", int target = 1,
        bool isSecret = false, string secretHint = "???")
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Icon = icon;
        Rarity = rarity;
        Target = target;
        IsSecret = isSecret;
        SecretHint = secretHint;
    }
}

public class AchievementProgress
{
    public string AchievementId { get; set; }
    public int Current { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }

    public AchievementProgress() { }

    public AchievementProgress(string id)
    {
        AchievementId = id;
        Current = 0;
        IsUnlocked = false;
    }
}

public class AchievementData
{
    public List<AchievementProgress> Progress { get; set; } = new();
    public List<string> NewlyUnlocked { get; set; } = new(); // cleared after client reads them
}

/// <summary>
/// Tracks in-game stats for achievement evaluation at game end.
/// Set on PassivesClass during gameplay, read at game end.
/// </summary>
public class InGameAchievementTracker
{
    public int TotalFightsWon { get; set; }
    public int TotalFightsLost { get; set; }
    public int TotalBlocksUsed { get; set; }
    public int ConsecutiveWins { get; set; }
    public int MaxConsecutiveWins { get; set; }
    public int RoundsAtFirst { get; set; }
    public int RoundsAtLast { get; set; }
    public int DifferentPlayersDefeated { get; set; }
    public HashSet<string> DefeatedPlayerNames { get; set; } = new();
    public int TimesPickled { get; set; }
    public int KiraKills { get; set; }
    public int KratosKills { get; set; }
    public bool WasKilledByKira { get; set; }
    public bool WasKilledByKratos { get; set; }
    public bool WasKilledByMonster { get; set; }
    public bool SurvivedKiraAttempt { get; set; }
    public bool BuiltZiggurat { get; set; }
    public int PortalGunSwaps { get; set; }
    public bool WasDragonForm { get; set; }
    public bool PickleRickTriggered { get; set; }
    public int VampireFeedCount { get; set; }
    public int GoblinCount { get; set; }
    public int StolenStats { get; set; }
    public bool FinishedWithZeroPsyche { get; set; }
    public bool FinishedWithMaxPsyche { get; set; }
    public bool NeverBlocked { get; set; } = true;
    public bool NeverAttacked { get; set; } = true;
    public int JusticeReached { get; set; }
    public int LvlUpsUsed { get; set; }
    public int MoralToPointsUsed { get; set; }
    public bool WasRevived { get; set; }
    public int EnemiesKilledAsKratos { get; set; }
    public bool KilledAllAsKratos { get; set; }
    public int SalldorumHistoryRewrites { get; set; }
    public int TheBoysOrdersCompleted { get; set; }
    public int GeraltContractsCompleted { get; set; }
    public int NapoleonTreaties { get; set; }
    public int KotikiTaunts { get; set; }
    public int DeepListMockeries { get; set; }
    public int ExploitsFired { get; set; }
    public int SaitamaDeferredPoints { get; set; }
    public bool DarksciChosenStable { get; set; }
    public int SellerMarksPlaced { get; set; }
    public bool MonsterPawnUsed { get; set; }
    public decimal HighestSingleRoundScore { get; set; }
    public bool CameFromLastToFirst { get; set; }
    public bool WentFromFirstToLast { get; set; }
}

public static class AchievementService
{
    public static readonly List<AchievementDefinition> AllAchievements = new()
    {
        // ══════════════════════════════════════════════════════════════
        // VICTORY achievements
        // ══════════════════════════════════════════════════════════════
        new("first_win", "First Blood", "Win your first game", AchievementCategory.Victory, "crown", "common"),
        new("wins_10", "Veteran", "Win 10 games", AchievementCategory.Victory, "crown", "uncommon", 10),
        new("wins_50", "Champion", "Win 50 games", AchievementCategory.Victory, "crown", "rare", 50),
        new("wins_100", "Legend", "Win 100 games", AchievementCategory.Victory, "crown", "epic", 100),
        new("top3_25", "Consistent", "Finish top 3 in 25 games", AchievementCategory.Victory, "medal", "uncommon", 25),
        new("play_100", "Dedicated", "Play 100 games", AchievementCategory.Victory, "games", "uncommon", 100),
        new("play_500", "Addicted", "Play 500 games", AchievementCategory.Victory, "games", "epic", 500),
        new("comeback_king", "Comeback King", "Win after being 6th place at any point", AchievementCategory.Victory,
            "rocket", "rare"),
        new("flawless", "Flawless Victory", "Win without losing a single fight", AchievementCategory.Victory,
            "shield", "epic"),

        // ══════════════════════════════════════════════════════════════
        // SCORE achievements
        // ══════════════════════════════════════════════════════════════
        new("score_50", "Half Century", "Score 50+ points in a single game", AchievementCategory.Score, "fire", "common"),
        new("score_100", "Centurion", "Score 100+ points in a single game", AchievementCategory.Score, "fire", "uncommon"),
        new("score_150", "Dominator", "Score 150+ points in a single game", AchievementCategory.Score, "fire", "rare"),
        new("score_200", "Unstoppable Force", "Score 200+ points in a single game", AchievementCategory.Score, "fire", "epic"),
        new("score_250", "Beyond Limits", "Score 250+ points in a single game", AchievementCategory.Score, "fire", "legendary"),

        // ══════════════════════════════════════════════════════════════
        // COMBAT achievements
        // ══════════════════════════════════════════════════════════════
        new("fights_won_5", "Brawler", "Win 5 fights in a single game", AchievementCategory.Combat, "sword", "common"),
        new("fights_won_8", "Gladiator", "Win 8 fights in a single game", AchievementCategory.Combat, "sword", "uncommon"),
        new("fights_won_10", "Destroyer", "Win 10+ fights in a single game", AchievementCategory.Combat, "sword", "rare"),
        new("beat_everyone", "Beat Them All", "Defeat all 5 opponents at least once in a game", AchievementCategory.Combat,
            "swords", "rare"),
        new("win_streak_5", "On Fire", "Win 5 consecutive fights in a game", AchievementCategory.Combat, "flame", "rare"),
        new("block_master", "Fortress", "Block 5+ times in a single game", AchievementCategory.Combat, "shield", "common"),
        new("pacifist", "Pacifist", "Never attack in an entire game (block/skip every round)", AchievementCategory.Combat,
            "peace", "rare"),
        new("no_blocks", "All In", "Win a game without blocking once", AchievementCategory.Combat, "dice", "uncommon"),
        new("justice_max", "Scales of Justice", "Reach 5 Justice in a game", AchievementCategory.Combat, "balance", "uncommon"),

        // ══════════════════════════════════════════════════════════════
        // CHARACTER-SPECIFIC achievements (visible)
        // ══════════════════════════════════════════════════════════════
        new("play_chars_5", "Explorer", "Play 5 different characters", AchievementCategory.Character, "compass", "common", 5),
        new("play_chars_10", "Versatile", "Play 10 different characters", AchievementCategory.Character, "compass", "uncommon", 10),
        new("play_chars_20", "Shapeshifter", "Play 20 different characters", AchievementCategory.Character, "compass", "rare", 20),
        new("play_chars_30", "Collector", "Play 30+ different characters", AchievementCategory.Character, "compass", "epic", 30),
        new("win_3_chars", "Multi-Talented", "Win with 3 different characters", AchievementCategory.Character, "stars", "uncommon", 3),
        new("win_10_chars", "True Master", "Win with 10 different characters", AchievementCategory.Character, "stars", "rare", 10),

        // ══════════════════════════════════════════════════════════════
        // MASTERY achievements
        // ══════════════════════════════════════════════════════════════
        new("max_stat", "Maxed Out", "Reach 10 in any stat during a game", AchievementCategory.Mastery, "sparkle", "common"),
        new("all_stats_8", "Well Rounded", "Have all 4 stats at 8+ simultaneously", AchievementCategory.Mastery,
            "circle", "rare"),
        new("high_skill", "Skill Ceiling", "Reach 100+ Skill in a game", AchievementCategory.Mastery, "bolt", "uncommon"),
        new("moral_master", "Morale Boost", "Use Moral for Points 3+ times in a game", AchievementCategory.Mastery,
            "heart", "common"),
        new("lvlup_all", "Balanced Growth", "Level up each stat at least once in a game", AchievementCategory.Mastery,
            "arrows", "uncommon"),
        new("psyche_zero_win", "Berserker", "Win a game with 0 Psyche", AchievementCategory.Mastery, "skull", "rare"),
        new("max_psyche_win", "Zen Master", "Win with 10 Psyche", AchievementCategory.Mastery, "lotus", "uncommon"),

        // ══════════════════════════════════════════════════════════════
        // SOCIAL / META achievements
        // ══════════════════════════════════════════════════════════════
        new("predict_correct_3", "Mind Reader", "Get 3+ correct predictions in a game", AchievementCategory.Social,
            "eye", "uncommon"),
        new("predict_correct_5", "Oracle", "Get 5 correct predictions in a game", AchievementCategory.Social,
            "crystal", "rare"),
        new("win_from_web", "Digital Warrior", "Win a game from the web client", AchievementCategory.Social, "globe", "common"),

        // ══════════════════════════════════════════════════════════════
        // SECRET achievements — hidden until unlocked
        // ══════════════════════════════════════════════════════════════

        // --- Kira secrets ---
        new("kill_a_god", "Kill a God", "Kill Kira with the Death Note",
            AchievementCategory.Secret, "skull-crossbones", "legendary",
            isSecret: true, secretHint: "The irony of divine justice..."),
        new("kira_perfect", "Perfect Crime", "Kill 3+ players with Death Note in one game",
            AchievementCategory.Secret, "notebook", "epic",
            isSecret: true, secretHint: "According to keikaku..."),
        new("survive_death_note", "Plot Armor", "Survive a Death Note kill attempt (Kratos revival or Goblin immunity)",
            AchievementCategory.Secret, "shield-cross", "rare",
            isSecret: true, secretHint: "Not even death can stop some..."),
        new("kira_kills_l", "Justice Prevails?", "As Kira, successfully kill L",
            AchievementCategory.Secret, "detective", "epic",
            isSecret: true, secretHint: "The world's greatest detective meets their match"),

        // --- Kratos secrets ---
        new("destroy_olympus", "Destroy Olympus", "Kill all living opponents as Kratos",
            AchievementCategory.Secret, "mountain", "legendary",
            isSecret: true, secretHint: "When one god isn't enough..."),
        new("kratos_revive", "Boy, I'm Not Done", "Survive Kira's Death Note as Kratos (God Slayer revival)",
            AchievementCategory.Secret, "axe", "epic",
            isSecret: true, secretHint: "Death is merely an inconvenience"),

        // --- Rick secrets ---
        new("pickle_rick", "I'm Pickle Rick!", "Transform into Pickle Rick",
            AchievementCategory.Secret, "pickle", "uncommon",
            isSecret: true, secretHint: "The funniest thing I've ever seen"),
        new("portal_master", "Interdimensional", "Use Portal Gun swap 3+ times in a game",
            AchievementCategory.Secret, "portal", "rare",
            isSecret: true, secretHint: "Wubba lubba dub dub!"),

        // --- Saitama secrets ---
        new("one_punch", "One Punch", "As Saitama, have 20+ deferred points restored in round 10",
            AchievementCategory.Secret, "fist", "epic",
            isSecret: true, secretHint: "The hero nobody noticed..."),

        // --- Goblin secrets ---
        new("goblin_victory", "Ziggurat Ascension", "Win as Goblins with Ziggurat at position 1 on round 10",
            AchievementCategory.Secret, "pyramid", "legendary",
            isSecret: true, secretHint: "A monument to cunning"),

        // --- Dragon secrets ---
        new("dragon_awakening", "Dragon Unleashed", "Trigger Dragon form on round 10 (all stats = 10)",
            AchievementCategory.Secret, "dragon", "rare",
            isSecret: true, secretHint: "Dormant power awakens..."),

        // --- Vampire secrets ---
        new("blood_feast", "Blood Feast", "Steal stats from 4+ different opponents as Vampire",
            AchievementCategory.Secret, "bat", "rare",
            isSecret: true, secretHint: "An insatiable thirst..."),

        // --- Monster secrets ---
        new("apocalypse_survivor", "Apocalypse Survivor", "Survive round 10 when Monster triggers their end-game effect",
            AchievementCategory.Secret, "radiation", "rare",
            isSecret: true, secretHint: "The world ends, but you remain"),
        new("monster_pawn", "Puppet Master", "As Monster, convert a player into your pawn",
            AchievementCategory.Secret, "puppet", "uncommon",
            isSecret: true, secretHint: "Everyone is a piece on the board"),

        // --- Itachi secrets ---
        new("itachi_revive", "Izanagi", "Come back from the dead as Itachi (use Shisui's eye)",
            AchievementCategory.Secret, "eye-glow", "epic",
            isSecret: true, secretHint: "Forbidden technique..."),

        // --- DeepList secrets ---
        new("mockery_master", "Supreme Troll", "Trigger DeepList's mockery 5+ times in a game",
            AchievementCategory.Secret, "jester", "rare",
            isSecret: true, secretHint: "200 IQ plays"),

        // --- Geralt secrets ---
        new("witcher_contracts", "Professional", "Complete 3+ Witcher contracts in a game as Geralt",
            AchievementCategory.Secret, "medallion", "rare",
            isSecret: true, secretHint: "Wind's howling..."),

        // --- Salldorum secrets ---
        new("rewrite_history", "Temporal Paradox", "Use Salldorum's history rewrite ability",
            AchievementCategory.Secret, "hourglass", "uncommon",
            isSecret: true, secretHint: "Time is merely a suggestion"),

        // --- Napoleon secrets ---
        new("grand_alliance", "Grand Alliance", "Form 3+ treaties as Napoleon in a game",
            AchievementCategory.Secret, "handshake", "rare",
            isSecret: true, secretHint: "Diplomacy is war by other means"),

        // --- Kotiki secrets ---
        new("cat_army", "Cat Army", "Taunt 4+ enemies with Kotiki in a game",
            AchievementCategory.Secret, "cat", "rare",
            isSecret: true, secretHint: "Meow meow meow meow"),

        // --- TheBoys secrets ---
        new("boys_orders", "Following Orders", "Complete 3+ orders as TheBoys in a game",
            AchievementCategory.Secret, "badge", "rare",
            isSecret: true, secretHint: "Diabolical!"),

        // --- Bug/Exploit secrets ---
        new("exploit_chain", "Zero Day", "Exploit 3+ players in a single game as Bug",
            AchievementCategory.Secret, "bug", "rare",
            isSecret: true, secretHint: "The system has vulnerabilities"),

        // --- Seller secrets ---
        new("shady_dealer", "Black Market", "Place marks on 4+ players as Seller in a game",
            AchievementCategory.Secret, "briefcase", "rare",
            isSecret: true, secretHint: "Everyone has a price"),

        // --- Darksci secrets ---
        new("lucky_gambler", "Against All Odds", "Win as Darksci after choosing unstable",
            AchievementCategory.Secret, "dice-six", "rare",
            isSecret: true, secretHint: "Fortune favors the bold"),

        // --- Rare game states ---
        new("last_to_first", "From Ashes", "Go from last place to first place in a single game",
            AchievementCategory.Secret, "phoenix", "epic",
            isSecret: true, secretHint: "The ultimate reversal"),
        new("first_to_last", "How The Mighty Fall", "Go from first place to last place",
            AchievementCategory.Secret, "falling", "rare",
            isSecret: true, secretHint: "Pride comes before the fall..."),
        new("died_but_won", "Ghost Victory", "Die during the game but still finish 1st",
            AchievementCategory.Secret, "ghost", "legendary",
            isSecret: true, secretHint: "Victory transcends mortality"),
        new("zero_score", "Participation Trophy", "Finish a game with 0 or negative score",
            AchievementCategory.Secret, "trophy-broken", "uncommon",
            isSecret: true, secretHint: "At least you tried..."),
        new("all_blocked", "Turtle Strategy", "Block every single round of a game (10 blocks)",
            AchievementCategory.Secret, "turtle", "epic",
            isSecret: true, secretHint: "The best offense is... no offense"),
        new("round1_lead", "Early Bird", "Hold 1st place from round 1 through the end",
            AchievementCategory.Secret, "bird", "rare",
            isSecret: true, secretHint: "The one who never fell"),
    };

    private static readonly Dictionary<string, AchievementDefinition> _byId =
        AllAchievements.ToDictionary(a => a.Id);

    public static AchievementDefinition GetDefinition(string id)
    {
        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public static void EnsureInitialized(DiscordAccountClass account)
    {
        account.Achievements ??= new AchievementData();
    }

    /// <summary>
    /// Try to unlock an achievement. Returns true if newly unlocked.
    /// </summary>
    public static bool TryUnlock(DiscordAccountClass account, string achievementId, int progress = 1)
    {
        EnsureInitialized(account);

        var def = GetDefinition(achievementId);
        if (def == null) return false;

        var existing = account.Achievements.Progress.Find(p => p.AchievementId == achievementId);
        if (existing == null)
        {
            existing = new AchievementProgress(achievementId);
            account.Achievements.Progress.Add(existing);
        }

        if (existing.IsUnlocked) return false;

        existing.Current += progress;
        if (existing.Current >= def.Target)
        {
            existing.Current = def.Target;
            existing.IsUnlocked = true;
            existing.UnlockedAt = DateTimeOffset.UtcNow;
            account.Achievements.NewlyUnlocked.Add(achievementId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Set progress to specific value (for "play X different characters" style achievements).
    /// </summary>
    public static bool SetProgress(DiscordAccountClass account, string achievementId, int value)
    {
        EnsureInitialized(account);

        var def = GetDefinition(achievementId);
        if (def == null) return false;

        var existing = account.Achievements.Progress.Find(p => p.AchievementId == achievementId);
        if (existing == null)
        {
            existing = new AchievementProgress(achievementId);
            account.Achievements.Progress.Add(existing);
        }

        if (existing.IsUnlocked) return false;

        existing.Current = value;
        if (existing.Current >= def.Target)
        {
            existing.Current = def.Target;
            existing.IsUnlocked = true;
            existing.UnlockedAt = DateTimeOffset.UtcNow;
            account.Achievements.NewlyUnlocked.Add(achievementId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluate all achievements at game end based on in-game tracker + account history.
    /// </summary>
    public static void TrackGameEnd(DiscordAccountClass account, GamePlayerBridgeClass player, GameClass game)
    {
        EnsureInitialized(account);
        var tracker = player.Passives.AchievementTracker ?? new InGameAchievementTracker();

        var place = player.Status.GetPlaceAtLeaderBoard();
        var score = player.Status.GetScore();
        var isWin = place == 1;
        var charName = player.GameCharacter.Name;

        // ── Victory achievements ──
        if (isWin) TryUnlock(account, "first_win");
        if (isWin) TryUnlock(account, "wins_10");
        if (isWin) TryUnlock(account, "wins_50");
        if (isWin) TryUnlock(account, "wins_100");
        if (place <= 3) TryUnlock(account, "top3_25");
        TryUnlock(account, "play_100");
        TryUnlock(account, "play_500");

        if (isWin && tracker.CameFromLastToFirst) TryUnlock(account, "comeback_king");
        if (isWin && tracker.TotalFightsLost == 0 && tracker.TotalFightsWon > 0) TryUnlock(account, "flawless");

        // ── Score achievements ──
        if (score >= 50) TryUnlock(account, "score_50");
        if (score >= 100) TryUnlock(account, "score_100");
        if (score >= 150) TryUnlock(account, "score_150");
        if (score >= 200) TryUnlock(account, "score_200");
        if (score >= 250) TryUnlock(account, "score_250");

        // ── Combat achievements ──
        if (tracker.TotalFightsWon >= 5) TryUnlock(account, "fights_won_5");
        if (tracker.TotalFightsWon >= 8) TryUnlock(account, "fights_won_8");
        if (tracker.TotalFightsWon >= 10) TryUnlock(account, "fights_won_10");
        if (tracker.DefeatedPlayerNames.Count >= 5) TryUnlock(account, "beat_everyone");
        if (tracker.MaxConsecutiveWins >= 5) TryUnlock(account, "win_streak_5");
        if (tracker.TotalBlocksUsed >= 5) TryUnlock(account, "block_master");
        if (tracker.NeverAttacked) TryUnlock(account, "pacifist");
        if (isWin && tracker.NeverBlocked) TryUnlock(account, "no_blocks");
        if (tracker.JusticeReached >= 5) TryUnlock(account, "justice_max");

        // ── Character diversity achievements ──
        var uniqueCharsPlayed = account.CharacterStatistics.Select(c => c.CharacterName).Distinct().Count();
        SetProgress(account, "play_chars_5", uniqueCharsPlayed);
        SetProgress(account, "play_chars_10", uniqueCharsPlayed);
        SetProgress(account, "play_chars_20", uniqueCharsPlayed);
        SetProgress(account, "play_chars_30", uniqueCharsPlayed);
        var uniqueCharsWon = account.CharacterStatistics.Where(c => c.Wins > 0).Select(c => c.CharacterName).Distinct().Count();
        SetProgress(account, "win_3_chars", uniqueCharsWon);
        SetProgress(account, "win_10_chars", uniqueCharsWon);

        // ── Mastery achievements ──
        var gc = player.GameCharacter;
        if (gc.GetIntelligence() >= 10 || gc.GetStrength() >= 10 || gc.GetSpeed() >= 10 || gc.GetPsyche() >= 10)
            TryUnlock(account, "max_stat");
        if (gc.GetIntelligence() >= 8 && gc.GetStrength() >= 8 && gc.GetSpeed() >= 8 && gc.GetPsyche() >= 8)
            TryUnlock(account, "all_stats_8");
        if (gc.GetSkill() >= 100) TryUnlock(account, "high_skill");
        if (tracker.MoralToPointsUsed >= 3) TryUnlock(account, "moral_master");
        if (isWin && tracker.FinishedWithZeroPsyche) TryUnlock(account, "psyche_zero_win");
        if (isWin && tracker.FinishedWithMaxPsyche) TryUnlock(account, "max_psyche_win");

        // ── Social achievements ──
        var correctPredictions = player.Predict.Count(p =>
        {
            var target = game.PlayersList.Find(x => x.GetPlayerId() == p.PlayerId);
            return target != null && target.GameCharacter.Name == p.CharacterName;
        });
        if (correctPredictions >= 3) TryUnlock(account, "predict_correct_3");
        if (correctPredictions >= 5) TryUnlock(account, "predict_correct_5");
        if (isWin && (player.IsWebPlayer || player.PreferWeb)) TryUnlock(account, "win_from_web");

        // ── Secret achievements ──

        // Kira-related
        if (tracker.KiraKills >= 3) TryUnlock(account, "kira_perfect");
        if (tracker.SurvivedKiraAttempt) TryUnlock(account, "survive_death_note");
        // "kill_a_god" — player used Death Note to kill Kira (the character)
        // Check if any of the dead players was Kira and was killed by this player's Death Note
        if (charName == "Кира")
        {
            foreach (var victim in game.PlayersList.Where(p => p.Passives.IsDead && p.Passives.DeathSource == "Kira" && p.GameCharacter.Name == "Кира"))
            {
                // Kira killed another Kira (shouldn't happen in normal game, but handle edge case)
                TryUnlock(account, "kill_a_god");
            }
            // Check if L was killed (KiraKills > standard implies L kill with extra count)
            if (tracker.KiraKills > game.PlayersList.Count(p => p.Passives.IsDead && p.Passives.DeathSource == "Kira"))
            {
                // L kill happened (we added extra count for L kill)
                TryUnlock(account, "kira_kills_l");
            }
        }
        // Anyone who kills Kira character gets "kill_a_god" (e.g., through Death Note, Kratos, Monster)
        foreach (var victim in game.PlayersList.Where(p => p.Passives.IsDead && p.GameCharacter.Name == "Кира"))
        {
            if (victim.Passives.DeathSource == "Kira" && charName == "Кира")
            {
                // Already handled above
            }
            else if (victim.Passives.DeathSource == "Kratos" && charName == "Кратос")
            {
                TryUnlock(account, "kill_a_god");
            }
        }

        // Kratos — check if all non-Kratos players are dead
        if (charName == "Кратос")
        {
            var allOthersDead = game.PlayersList
                .Where(p => p.GameCharacter.Name != "Кратос")
                .All(p => p.Passives.IsDead);
            if (allOthersDead && tracker.EnemiesKilledAsKratos > 0)
                TryUnlock(account, "destroy_olympus");
        }
        if (tracker.WasRevived && charName == "Кратос") TryUnlock(account, "kratos_revive");

        // Rick
        if (tracker.PickleRickTriggered) TryUnlock(account, "pickle_rick");
        if (tracker.PortalGunSwaps >= 3) TryUnlock(account, "portal_master");

        // Saitama
        if (tracker.SaitamaDeferredPoints >= 20) TryUnlock(account, "one_punch");

        // Goblin
        if (tracker.BuiltZiggurat && isWin && charName == "Стая Гоблинов") TryUnlock(account, "goblin_victory");

        // Dragon
        if (tracker.WasDragonForm) TryUnlock(account, "dragon_awakening");

        // Vampire
        if (tracker.VampireFeedCount >= 4) TryUnlock(account, "blood_feast");

        // Monster
        if (tracker.MonsterPawnUsed) TryUnlock(account, "monster_pawn");

        // Itachi
        if (tracker.WasRevived && charName == "Итачи") TryUnlock(account, "itachi_revive");

        // DeepList
        if (tracker.DeepListMockeries >= 5) TryUnlock(account, "mockery_master");

        // Geralt
        if (tracker.GeraltContractsCompleted >= 3) TryUnlock(account, "witcher_contracts");

        // Salldorum
        if (tracker.SalldorumHistoryRewrites > 0) TryUnlock(account, "rewrite_history");

        // Napoleon
        if (tracker.NapoleonTreaties >= 3) TryUnlock(account, "grand_alliance");

        // Kotiki
        if (tracker.KotikiTaunts >= 4) TryUnlock(account, "cat_army");

        // TheBoys
        if (tracker.TheBoysOrdersCompleted >= 3) TryUnlock(account, "boys_orders");

        // Bug
        if (tracker.ExploitsFired >= 3) TryUnlock(account, "exploit_chain");

        // Seller
        if (tracker.SellerMarksPlaced >= 4) TryUnlock(account, "shady_dealer");

        // Darksci (character name in Russian)
        if (isWin && !tracker.DarksciChosenStable && charName == "Darksci") TryUnlock(account, "lucky_gambler");

        // Rare game states
        if (tracker.CameFromLastToFirst) TryUnlock(account, "last_to_first");
        if (tracker.WentFromFirstToLast) TryUnlock(account, "first_to_last");
        if (player.Passives.IsDead && isWin) TryUnlock(account, "died_but_won");
        if (score <= 0) TryUnlock(account, "zero_score");
        if (tracker.TotalBlocksUsed >= 10) TryUnlock(account, "all_blocked");
        if (tracker.RoundsAtFirst >= 10 && isWin) TryUnlock(account, "round1_lead");

    }

    /// <summary>
    /// Check for achievements unlocked by specific kill events during the game.
    /// Called from CharacterPassives when a death note kill happens.
    /// </summary>
    public static void TrackKiraKill(InGameAchievementTracker tracker, string victimCharName, string killerCharName)
    {
        tracker.KiraKills++;
        if (victimCharName == "Кира") // Kira killed Kira or someone killed Kira
        {
            // "kill_a_god" tracked separately
        }
    }
}
