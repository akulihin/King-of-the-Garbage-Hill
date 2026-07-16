using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class JonSnow
{
    public const string CharacterName = "Джон Сноу";
    public const string DumbBastard = "Тупой бастард";
    public const string ServerKing = "Король Сервера";
    public const string IAmJonSnow = "Я Джон Сноу";
    public const string AnotherBastard = "Еще один бастард";
    public const string BlackCastle = "Черный Замок";
    public const string MyWatchHasEnded = "Мой дозор окончен";

    public const int KingSkillThreshold = 228;
    public const int BlackCastlePlace = 4;
    public const int BlackCastleTurns = 3;

    public sealed class State
    {
        public HashSet<Guid> WeakestPlayerIds { get; set; } = new();
        public HashSet<Guid> RedirectedAttackersThisRound { get; set; } = new();
        public Guid DifficultyEnemyId { get; set; } = Guid.Empty;
        public int DifficultyBonusThisFight { get; set; }
        public bool BlackCastleActive { get; set; }
        public int BlackCastleReleaseAfterRound { get; set; }
        public bool WatchEnded { get; set; }
        public string WatchDeathSource { get; set; } = "";
        public int WatchDeathRound { get; set; }
        public int LoyaltyVictories { get; set; }
    }

    public static bool Is(CharacterClass character) =>
        character?.Name == CharacterName;

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static GamePlayerBridgeClass Find(IEnumerable<GamePlayerBridgeClass> players) =>
        players?.FirstOrDefault(Is);

    public static bool HasPassive(GamePlayerBridgeClass player, string passiveName) =>
        Is(player)
        && !player.Passives.PassiveAbilitiesDisabledByKimiko
        && player.GameCharacter.Passive.Any(passive => passive.PassiveName == passiveName);

    public static bool HasPassive(CharacterClass character, string passiveName) =>
        Is(character)
        && character.Passive.Any(passive => passive.PassiveName == passiveName);

    public static bool IsKing(CharacterClass character) =>
        Is(character)
        && character.Passive.Any(passive =>
            passive.PassiveName == ServerKing && passive.Visible);

    public static bool IsKing(GamePlayerBridgeClass player) =>
        Is(player)
        && !player.Passives.PassiveAbilitiesDisabledByKimiko
        && IsKing(player.GameCharacter);

    public static bool IsKingActive(CharacterClass character, int place) =>
        IsKing(character) && place != BlackCastlePlace;

    public static bool IsKingActive(GamePlayerBridgeClass player) =>
        IsKing(player) && player.Status.GetPlaceAtLeaderBoard() != BlackCastlePlace;

    public static void Initialize(GamePlayerBridgeClass player)
    {
        if (!HasPassive(player, DumbBastard)) return;
        player.GameCharacter.SetAnySkillMultiplier(1);
    }

    public static void TryBecomeKing(CharacterClass character)
    {
        if (!HasPassive(character, DumbBastard)
            || character.GetSkill() < KingSkillThreshold)
            return;

        character.Passive.RemoveAll(passive => passive.PassiveName == DumbBastard);
        var king = character.Passive.Find(passive => passive.PassiveName == ServerKing);
        if (king != null)
            king.Visible = true;
        character.JonSnowBecameKing = true;

        character.SetAnySkillMultiplier();

        var intelligenceBonus = Math.Min(
            character.JonSnowBastardIntelligenceBonus,
            character.GetIntelligence());
        if (intelligenceBonus > 0)
            character.AddIntelligence(-intelligenceBonus, DumbBastard);
        character.JonSnowBastardIntelligenceBonus = 0;
    }

    public static void ApplyBaseJustice(GamePlayerBridgeClass player)
    {
        if (!HasPassive(player, IAmJonSnow)) return;
        ApplyStatBonus(
            player,
            player.FightCharacter.Justice.GetRealJusticeNow(),
            IAmJonSnow);
    }

    public static void ApplyDifficultyJustice(
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        CalculateRounds calculateRounds)
    {
        var jon = HasPassive(attacker, IAmJonSnow)
            ? attacker
            : HasPassive(defender, IAmJonSnow)
                ? defender
                : null;
        if (jon == null) return;

        var enemy = jon.GetPlayerId() == attacker.GetPlayerId() ? defender : attacker;
        var quality = calculateRounds.CalculateStep1(attacker, defender);
        var isJonAttacker = jon.GetPlayerId() == attacker.GetPlayerId();
        var tooStronk = isJonAttacker ? quality.IsTooStronkEnemy : quality.IsTooStronkMe;
        var tooGood = isJonAttacker ? quality.IsTooGoodEnemy : quality.IsTooGoodMe;
        var difficultyBonus = tooStronk ? 2 : tooGood ? 1 : 0;

        var redirectedBonus = jon.GetPlayerId() == defender.GetPlayerId()
                              && jon.Passives.JonSnow.RedirectedAttackersThisRound.Contains(
                                  attacker.GetPlayerId())
            ? 1
            : 0;

        var state = jon.Passives.JonSnow;
        state.DifficultyEnemyId = enemy.GetPlayerId();
        state.DifficultyBonusThisFight = difficultyBonus;

        var totalBonus = difficultyBonus + redirectedBonus;
        if (totalBonus <= 0) return;

        var currentJustice = jon.FightCharacter.Justice.GetRealJusticeNow();
        jon.FightCharacter.Justice.SetJusticeForOneFight(
            currentJustice + totalBonus,
            difficultyBonus > 0 ? IAmJonSnow : AnotherBastard);
        ApplyStatBonus(
            jon,
            totalBonus,
            difficultyBonus > 0 ? IAmJonSnow : AnotherBastard);

        if (difficultyBonus > 0)
        {
            var difficulty = tooStronk ? "toostronk" : "toogood";
            LogEffect(
                jon,
                positive: true,
                IAmJonSnow,
                $"(Враг {difficulty}, + {difficultyBonus} справедливости): Я Джон Сноу.",
                $"(Enemy {difficulty}, +{difficultyBonus} Justice): I am Jon Snow.");
        }
    }

    private static void ApplyStatBonus(
        GamePlayerBridgeClass player,
        int bonus,
        string source)
    {
        if (bonus <= 0) return;

        player.FightCharacter.SetIntelligenceForOneFight(
            player.FightCharacter.GetIntelligence() + bonus, source);
        player.FightCharacter.SetStrengthForOneFight(
            player.FightCharacter.GetStrength() + bonus, source);
        player.FightCharacter.SetSpeedForOneFight(
            player.FightCharacter.GetSpeed() + bonus, source);
        player.FightCharacter.SetPsycheForOneFight(
            player.FightCharacter.GetPsyche() + bonus, source);
    }

    public static void HandleResolvedFight(
        GameClass game,
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        GamePlayerBridgeClass winner,
        GamePlayerBridgeClass loser)
    {
        var jon = Find(game.PlayersList);
        if (jon == null || jon.Passives.IsDead) return;

        var state = jon.Passives.JonSnow;
        var jonWon = winner.GetPlayerId() == jon.GetPlayerId();
        var jonLost = loser.GetPlayerId() == jon.GetPlayerId();
        var enemy = jonWon ? loser : jonLost ? winner : null;

        if (jonWon
            && enemy != null
            && HasPassive(jon, DumbBastard)
            && state.DifficultyEnemyId == enemy.GetPlayerId()
            && state.DifficultyBonusThisFight > 0)
        {
            var intelligenceBefore = jon.GameCharacter.GetIntelligence();
            jon.GameCharacter.AddIntelligence(
                state.DifficultyBonusThisFight,
                DumbBastard);
            jon.GameCharacter.JonSnowBastardIntelligenceBonus +=
                jon.GameCharacter.GetIntelligence() - intelligenceBefore;
        }

        if (jonLost && HasPassive(jon, DumbBastard))
        {
            if (state.WatchEnded)
                LogAfterWatch(jon, positive: false);
            else
                game.Phrases.JonSnowDefeat.SendLog(
                    jon, false, isRandomOrder: false);
        }

        if (defender.GetPlayerId() == jon.GetPlayerId()
            && state.RedirectedAttackersThisRound.Remove(attacker.GetPlayerId()))
        {
            LogEffect(
                jon,
                positive: true,
                AnotherBastard,
                $"{attacker.DiscordUsername}: Что это, еще один бастард?",
                $"{attacker.DiscordUsername}: What is this, another bastard?");
        }

        AwardBlackCastleLoyalty(game, jon, winner);

        if (jonWon || jonLost)
        {
            state.DifficultyEnemyId = Guid.Empty;
            state.DifficultyBonusThisFight = 0;
        }
    }

    private static void AwardBlackCastleLoyalty(
        GameClass game,
        GamePlayerBridgeClass jon,
        GamePlayerBridgeClass winner)
    {
        if (!HasPassive(jon, BlackCastle)) return;

        var jonPlace = jon.Status.GetPlaceAtLeaderBoard();
        var winnerPlace = winner.Status.GetPlaceAtLeaderBoard();
        var sameSide = jonPlace is >= 1 and <= 3 && winnerPlace is >= 1 and <= 3
                       || jonPlace is >= 5 and <= 6 && winnerPlace is >= 5 and <= 6;
        if (!sameSide) return;

        var royal = IsKingActive(jon);
        jon.Status.AddBonusPoints(1, BlackCastle);
        jon.Passives.JonSnow.LoyaltyVictories++;

        if (royal)
        {
            const int royalPointsAwarded = 2;
            if (jon.Passives.JonSnow.WatchEnded)
                LogAfterWatch(jon, positive: true, royalPointsAwarded);
            else
                game.Phrases.JonSnowServerKing.SendLog(
                    jon,
                    false,
                    isRandomOrder: false,
                    suffix: " Король Сервера!");
        }
    }

    public static void RefreshWeakestPlayers(IEnumerable<GamePlayerBridgeClass> players)
    {
        var playerList = players?.ToList() ?? new List<GamePlayerBridgeClass>();
        var jon = Find(playerList);
        if (jon == null) return;

        var state = jon.Passives.JonSnow;
        state.WeakestPlayerIds.Clear();
        if (jon.Passives.IsDead || !HasPassive(jon, AnotherBastard)) return;

        foreach (var player in playerList
                     .Where(player => !player.Passives.IsDead)
                     .OrderByDescending(player => player.Status.GetPlaceAtLeaderBoard())
                     .Take(2))
            state.WeakestPlayerIds.Add(player.GetPlayerId());
    }

    public static void RedirectBastardAttacks(GameClass game)
    {
        if (game == null
            || game.IsKratosEvent
            || Madara.IsEternalTsukuyomiRound(game))
            return;

        var jon = Find(game.PlayersList);
        if (jon == null
            || jon.Passives.IsDead
            || !HasPassive(jon, AnotherBastard))
            return;

        var state = jon.Passives.JonSnow;
        state.RedirectedAttackersThisRound.Clear();
        var jonId = jon.GetPlayerId();

        foreach (var targetId in jon.Status.WhoToAttackThisTurn.Distinct())
        {
            var target = game.PlayersList.Find(player =>
                player.GetPlayerId() == targetId);
            if (target == null
                || target.Passives.IsDead
                || UnknownBug.Is(target)
                || game.RoundNo == 10 && target.GameCharacter.Passive.Any(passive =>
                    passive.PassiveName == "Стримснайпят и банят и банят и банят"))
                continue;

            var redirectedIndex = target.Status.WhoToAttackThisTurn.FindIndex(
                attackedId => state.WeakestPlayerIds.Contains(attackedId));
            if (redirectedIndex < 0) continue;

            if (target.Status.WhoToAttackThisTurn[redirectedIndex] != jonId)
                target.Status.WhoToAttackThisTurn[redirectedIndex] = jonId;
            state.RedirectedAttackersThisRound.Add(target.GetPlayerId());
        }
    }

    public static void ClearRoundState(GamePlayerBridgeClass jon)
    {
        if (!Is(jon)) return;
        jon.Passives.JonSnow.RedirectedAttackersThisRound.Clear();
        jon.Passives.JonSnow.DifficultyEnemyId = Guid.Empty;
        jon.Passives.JonSnow.DifficultyBonusThisFight = 0;
    }

    public static void ExpireBlackCastleBeforeScoreSort(GameClass game)
    {
        var jon = Find(game?.PlayersList);
        if (jon == null) return;

        var state = jon.Passives.JonSnow;
        if (state.BlackCastleActive
            && game.RoundNo > state.BlackCastleReleaseAfterRound)
        {
            state.BlackCastleActive = false;
            state.BlackCastleReleaseAfterRound = 0;
        }
    }

    public static List<GamePlayerBridgeClass> ApplyLeaderboardRules(
        IEnumerable<GamePlayerBridgeClass> players)
    {
        var ordered = players?.ToList() ?? new List<GamePlayerBridgeClass>();
        var jon = Find(ordered);
        if (jon == null || jon.Passives.IsDead) return ordered;

        if (jon.Passives.JonSnow.BlackCastleActive)
        {
            MoveExactlyToIndex(ordered, jon, BlackCastlePlace - 1);
            return ordered;
        }

        if (IsKing(jon))
            MoveUpToIndex(ordered, jon, 2);

        return ordered;
    }

    private static void MoveExactlyToIndex(
        List<GamePlayerBridgeClass> players,
        GamePlayerBridgeClass player,
        int targetIndex)
    {
        if (players.Count == 0) return;

        var currentIndex = players.IndexOf(player);
        targetIndex = Math.Clamp(targetIndex, 0, players.Count - 1);
        if (currentIndex < 0 || currentIndex == targetIndex) return;

        players.RemoveAt(currentIndex);
        players.Insert(targetIndex, player);
    }

    private static void MoveUpToIndex(
        List<GamePlayerBridgeClass> players,
        GamePlayerBridgeClass player,
        int targetIndex)
    {
        if (players.Count == 0) return;

        var currentIndex = players.IndexOf(player);
        targetIndex = Math.Clamp(targetIndex, 0, players.Count - 1);
        if (currentIndex < 0 || currentIndex <= targetIndex) return;

        players.RemoveAt(currentIndex);
        players.Insert(targetIndex, player);
    }

    public static void FinalizePositionEffects(GameClass game)
    {
        var jon = Find(game?.PlayersList);
        if (jon == null) return;

        var state = jon.Passives.JonSnow;
        var place = jon.Status.GetPlaceAtLeaderBoard();

        if (state.BlackCastleActive && place != BlackCastlePlace)
        {
            state.BlackCastleActive = false;
            state.BlackCastleReleaseAfterRound = 0;
        }

        if (!state.BlackCastleActive
            && !jon.Passives.IsDead
            && HasPassive(jon, BlackCastle)
            && place == BlackCastlePlace)
        {
            state.BlackCastleActive = true;
            state.BlackCastleReleaseAfterRound =
                game.RoundNo + BlackCastleTurns - 1;
            if (state.WatchEnded)
                LogAfterWatch(jon, positive: false);
            else
                game.Phrases.JonSnowCastleEnter.SendLog(
                    jon, false, isRandomOrder: false);
        }

        RefreshWeakestPlayers(game.PlayersList);
    }

    public static void FinalizeInitialPositions(
        IReadOnlyCollection<GamePlayerBridgeClass> players)
    {
        var jon = Find(players);
        if (jon == null) return;

        if (!jon.Passives.IsDead
            && HasPassive(jon, BlackCastle)
            && jon.Status.GetPlaceAtLeaderBoard() == BlackCastlePlace)
        {
            var state = jon.Passives.JonSnow;
            state.BlackCastleActive = true;
            state.BlackCastleReleaseAfterRound = BlackCastleTurns;
            LogEffect(
                jon,
                positive: false,
                BlackCastle,
                "Помогите! Я застрял!",
                "Help! I'm stuck!");
        }

        RefreshWeakestPlayers(players);
    }

    public static void HandleFinalPosition(GameClass game)
    {
        var jon = Find(game?.PlayersList);
        if (jon == null
            || jon.Passives.IsDead
            || !jon.Passives.JonSnow.BlackCastleActive
            || jon.Status.GetPlaceAtLeaderBoard() != BlackCastlePlace)
            return;

        var natural = game.PlayersList
            .OrderBy(player => player.Passives.IsDead)
            .ThenByDescending(player => player.Status.GetScore())
            .ToList();
        var naturalPlace = natural.IndexOf(jon) + 1;

        if (naturalPlace is >= 1 and <= 3)
        {
            if (jon.Passives.JonSnow.WatchEnded)
                LogAfterWatch(jon, positive: false);
            else
                game.Phrases.JonSnowFinalCastleLow.SendLog(
                    jon, false, isRandomOrder: false);
        }
        else if (naturalPlace is >= 5 and <= 6)
        {
            if (jon.Passives.JonSnow.WatchEnded)
                LogAfterWatch(jon, positive: true);
            else
                game.Phrases.JonSnowFinalCastleHigh.SendLog(
                    jon, false, isRandomOrder: false);
        }
    }

    public static bool TryEndWatch(
        GamePlayerBridgeClass player,
        GameClass game,
        string deathSource)
    {
        if (!HasPassive(player, MyWatchHasEnded)
            || player.Passives.JonSnow.WatchEnded)
            return false;

        var state = player.Passives.JonSnow;
        state.WatchEnded = true;
        state.WatchDeathSource = deathSource;
        state.WatchDeathRound = game.RoundNo;

        var lostIntelligence = player.GameCharacter.GetIntelligence();
        player.Passives.IsDead = false;
        player.Passives.DeathSource = "";
        player.Passives.GordonHeadcrab.ClearActive();
        player.Passives.GordonHeadcrab.IsZombie = true;
        player.GameCharacter.IntelligenceCappedAtZero = true;
        player.GameCharacter.AddIntelligence(
            -lostIntelligence,
            MyWatchHasEnded,
            isLog: false);
        player.Passives.AchievementTracker.WasRevived = true;

        game.AddGlobalLogs(PhrasePayload.Encode(
            MyWatchHasEnded,
            $"**Мой дозор окончен...** Но вот я здесь. - {lostIntelligence} Интеллекта",
            "My Watch Has Ended",
            $"**My watch has ended...** Yet here I stand. -{lostIntelligence} Intelligence"));
        GordonFreeman.ReevaluateAllZombiesPenalty(game);
        return true;
    }

    private static void LogEffect(
        GamePlayerBridgeClass player,
        bool positive,
        string passiveName,
        string russian,
        string english)
    {
        if (player.Passives.JonSnow.WatchEnded)
        {
            LogAfterWatch(player, positive);
            return;
        }

        player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            passiveName,
            russian,
            passiveName switch
            {
                DumbBastard => "Dumb Bastard",
                ServerKing => "Server King",
                IAmJonSnow => "I Am Jon Snow",
                AnotherBastard => "Another Bastard",
                BlackCastle => "Castle Black",
                _ => passiveName,
            },
            english) + "\n");
    }

    private static void LogAfterWatch(
        GamePlayerBridgeClass player,
        bool positive,
        int repetitions = 1)
    {
        var russianLine = positive
            ? "Мой дозор окончен: \"She muh queen\""
            : "Мой дозор окончен: \"I dun wan it\"";
        var englishLine = positive
            ? "My watch has ended: \"She muh queen\""
            : "My watch has ended: \"I dun wan it\"";
        player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            MyWatchHasEnded,
            string.Join(" ", Enumerable.Repeat(russianLine, repetitions)),
            "My Watch Has Ended",
            string.Join(" ", Enumerable.Repeat(englishLine, repetitions))) + "\n");
    }
}
