using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Naruto
{
    public const string CharacterName = "Наруто";
    public const string HaremJutsu = "Гарем но джутсу";
    public const string ShadowClones = "Теневые";
    public const string Rasengan = "Расенган";
    public const string Summon = "Призыв";
    public const int HaremCooldownTurns = 2;

    public sealed class State
    {
        public bool IsClone { get; set; }
        public Guid OriginalPlayerId { get; set; }
        public List<Guid> NarutoPlayerIds { get; set; } = new();
        public bool HaremActiveThisRound { get; set; }
        public int HaremCooldown { get; set; }
        public int HaremProvokedSkips { get; set; }
        public int JusticeSnapshot { get; set; }
        public Guid SummonAutoWinTarget { get; set; }
        public bool ShadowSettlementResolved { get; set; }
        public bool HasDispersed { get; set; }
        public decimal ShadowPointsTransferred { get; set; }
        public bool ResolvedTripleRasenganAgainstEren { get; set; }
        public bool ResolvedTripleRasenganAgainstMadara { get; set; }
    }

    public static bool IsNaruto(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName
        && !player.Passives.PassiveAbilitiesDisabledByKimiko;

    public static bool IsClone(GamePlayerBridgeClass player) =>
        IsNaruto(player) && player.Passives.Naruto.IsClone;

    public static bool CanChooseBlock(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name != CharacterName || !player.Passives.Naruto.IsClone;

    public static bool IsDispersedClone(GamePlayerBridgeClass player) =>
        IsClone(player) && player.Passives.Naruto.HasDispersed;

    public static bool IsNarutoPair(GamePlayerBridgeClass first, GamePlayerBridgeClass second)
    {
        if (!IsNaruto(first) || !IsNaruto(second) || first.GetPlayerId() == second.GetPlayerId())
            return false;

        var originalId = first.Passives.Naruto.OriginalPlayerId;
        return originalId != Guid.Empty && originalId == second.Passives.Naruto.OriginalPlayerId;
    }

    public static int GetBotActionTargetSlotCount(
        GamePlayerBridgeClass player,
        GameClass game,
        int legalTargetCount)
    {
        if (!IsNaruto(player) || !CanChooseBlock(player) || game == null) return legalTargetCount;

        // The original Naruto's two living siblings are deliberately illegal targets, but the
        // count-based L0/L1 action roll must retain the same attack-vs-Block balance as every other
        // six-player bot. Clones cannot choose Block at all.
        var livingSiblingCount = game.PlayersList.Count(target =>
            !target.Passives.IsDead && IsNarutoPair(player, target));
        return legalTargetCount + livingSiblingCount;
    }

    public static bool CanUseRoster(int strictBotCount, bool originalIsBot) =>
        strictBotCount - (originalIsBot ? 1 : 0) >= 2;

    public static bool CanInitializeForDraft(GameClass game) =>
        game != null && game.Teams.Count == 0
                     && game.PlayersList.Count(player => player.PlayerType == 404) >= 2;

    public static void InitializeTeam(
        List<GamePlayerBridgeClass> players,
        Func<CharacterClass> freshNarutoFactory)
    {
        var narutos = players.Where(IsNaruto).ToList();
        if (narutos.Count == 0) return;

        var initializedOriginal = narutos.FirstOrDefault(player =>
            !player.Passives.Naruto.IsClone
            && player.Passives.Naruto.OriginalPlayerId == player.GetPlayerId());
        if (initializedOriginal != null)
        {
            SeedSiblingPredictions(players, initializedOriginal.Passives.Naruto.OriginalPlayerId);
            return;
        }

        var original = narutos.FirstOrDefault(player => player.PlayerType != 404) ?? narutos[0];
        var cloneCandidates = narutos
            .Where(player => player != original && player.PlayerType == 404)
            .Concat(players.Where(player =>
                player != original && player.PlayerType == 404 && !IsNaruto(player)))
            .Distinct()
            .Take(2)
            .ToList();

        if (cloneCandidates.Count != 2)
            throw new InvalidOperationException(
                "Наруто requires two strict bot seats for Теневые clones.");

        // Forced simulation line-ups may contain the same deserialized CharacterClass instance
        // more than once. Reinstall every member from a fresh definition so the three Narutos
        // never share mutable character/status state.
        if (narutos.Count > 1)
        {
            var mastery = original.CharacterMasteryPoints;
            InstallFreshCharacter(original, freshNarutoFactory());
            original.CharacterMasteryPoints = mastery;
        }

        foreach (var clone in cloneCandidates)
            InstallFreshCharacter(clone, freshNarutoFactory());

        var originalId = original.GetPlayerId();
        var trio = new[] { original }.Concat(cloneCandidates).ToList();
        var trioIds = trio.Select(player => player.GetPlayerId()).ToList();

        original.Passives.Naruto.IsClone = false;
        original.Passives.Naruto.OriginalPlayerId = originalId;
        original.Passives.Naruto.NarutoPlayerIds = trioIds.ToList();

        foreach (var clone in cloneCandidates)
        {
            clone.Passives.Naruto.IsClone = true;
            clone.Passives.Naruto.OriginalPlayerId = originalId;
            clone.Passives.Naruto.NarutoPlayerIds = trioIds.ToList();
        }

        SeedSiblingPredictions(players, originalId);
    }

    private static void InstallFreshCharacter(GamePlayerBridgeClass player, CharacterClass character)
    {
        character.SetStatus(player.Status);
        player.GameCharacter = character;
        player.Status.GameCharacter = character;
        player.Passives = new PassivesClass();

        character.SetIntelligenceResist();
        character.SetStrengthResist(player);
        character.SetSpeedResist();
        character.SetPsycheResist();
        player.FightCharacter = character.DeepCopy();
        player.CharacterMasteryPoints = 0;
    }

    private static void SeedSiblingPredictions(
        IEnumerable<GamePlayerBridgeClass> players,
        Guid originalId)
    {
        var trio = players.Where(player =>
            IsNaruto(player) && player.Passives.Naruto.OriginalPlayerId == originalId).ToList();

        foreach (var naruto in trio)
        foreach (var sibling in trio.Where(player => player.GetPlayerId() != naruto.GetPlayerId()))
        {
            var existing = naruto.Predict.Find(prediction => prediction.PlayerId == sibling.GetPlayerId());
            if (existing == null)
                naruto.Predict.Add(new PredictClass(CharacterName, sibling.GetPlayerId()));
            else
                existing.CharacterName = CharacterName;
        }
    }

    public static void SanitizeMutualTargets(GameClass game)
    {
        foreach (var naruto in game.PlayersList.Where(IsNaruto))
        {
            var removed = naruto.Status.WhoToAttackThisTurn.RemoveAll(targetId =>
            {
                var target = game.PlayersList.Find(player => player.GetPlayerId() == targetId);
                return target != null && IsNarutoPair(naruto, target);
            });
            if (removed == 0 || naruto.Status.WhoToAttackThisTurn.Count > 0) continue;

            naruto.Status.IsBlock = false;
            naruto.Status.IsSkip = true;
            naruto.Status.ConfirmedSkip = true;
            naruto.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                ShadowClones,
                "Наруто не могут нападать друг на друга. Действие пропущено.",
                "Shadow Clones",
                "Narutos cannot attack one another. The action was skipped.") + "\n");
        }
    }

    public static void ResolveHaremQueues(GameClass game)
    {
        var harems = game.PlayersList.Where(player =>
            IsNaruto(player)
            && CanChooseBlock(player)
            && !player.Passives.IsDead
            && player.Status.IsBlock
            && player.Passives.Naruto.HaremCooldown == 0
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == HaremJutsu)).ToList();
        if (harems.Count == 0) return;

        foreach (var harem in harems)
        {
            harem.Status.IsBlock = false;
            harem.Passives.Naruto.HaremActiveThisRound = true;
        }

        var queues = game.PlayersList.ToDictionary(
            player => player.GetPlayerId(),
            player => ValidFightTargets(game, player, player.Status.WhoToAttackThisTurn).ToList());
        var canceledAttackers = new HashSet<Guid>();

        foreach (var harem in harems)
        {
            var attackers = game.PlayersList.Where(attacker =>
                attacker.GetPlayerId() != harem.GetPlayerId()
                && !UnknownBug.Is(attacker)
                && !Madara.IsRoundSevenAutoWin(game, attacker)
                && queues[attacker.GetPlayerId()].Contains(harem.GetPlayerId())).ToList();

            foreach (var _ in attackers)
                RewardHaremSkip(harem);

            foreach (var attacker in attackers)
                canceledAttackers.Add(attacker.GetPlayerId());
        }

        game.SkipPlayersThisRound += canceledAttackers.Count;
        foreach (var attackerId in canceledAttackers)
        {
            var attacker = game.PlayersList.Find(player => player.GetPlayerId() == attackerId);
            if (attacker == null) continue;
            attacker.Status.WhoToAttackThisTurn.Clear();
            attacker.Status.IsBlock = false;
            attacker.Status.IsSkip = true;
            attacker.Status.TurnInterference = TurnInterferenceKind.Enemy;
        }
    }

    public static bool TryCancelHaremFights(
        GameClass game,
        GamePlayerBridgeClass attacker,
        IEnumerable<Guid> queuedTargetIds)
    {
        if (UnknownBug.Is(attacker)) return false;
        // Клоны Сусано: a round-seven Madara attack must resolve, so the harem cannot cancel it.
        if (Madara.IsRoundSevenAutoWin(game, attacker)) return false;

        var validTargets = ValidFightTargets(game, attacker, queuedTargetIds).ToList();
        var haremTargets = validTargets
            .Select(id => game.PlayersList.Find(player => player.GetPlayerId() == id))
            .Where(player => player != null && player.Passives.Naruto.HaremActiveThisRound)
            .Distinct()
            .ToList();
        if (haremTargets.Count == 0) return false;

        foreach (var harem in haremTargets)
            RewardHaremSkip(harem);

        game.SkipPlayersThisRound++;
        attacker.Status.WhoToAttackThisTurn.Clear();
        attacker.Status.IsBlock = false;
        attacker.Status.IsSkip = true;
        attacker.Status.TurnInterference = TurnInterferenceKind.Enemy;
        return true;
    }

    private static void RewardHaremSkip(GamePlayerBridgeClass harem)
    {
        harem.Status.AddRegularPoints(1, HaremJutsu);
        harem.Passives.Naruto.HaremProvokedSkips++;
        harem.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            HaremJutsu,
            "Техника соблазнения!",
            "Harem Jutsu",
            "Seduction Technique!") + "\n");
    }

    private static IEnumerable<Guid> ValidFightTargets(
        GameClass game,
        GamePlayerBridgeClass attacker,
        IEnumerable<Guid> targetIds)
    {
        foreach (var targetId in targetIds)
        {
            if (targetId == attacker.GetPlayerId()) continue;
            var target = game.PlayersList.Find(player => player.GetPlayerId() == targetId);
            if (target == null || target.Passives.IsDead || IsNarutoPair(attacker, target)) continue;
            yield return targetId;
        }
    }

    public static void SnapshotJustice(GameClass game)
    {
        foreach (var naruto in game.PlayersList.Where(IsNaruto))
            naruto.Passives.Naruto.JusticeSnapshot = naruto.GameCharacter.Justice.GetRealJusticeNow();
    }

    public static List<GamePlayerBridgeClass> GetJointAttackers(
        GameClass game,
        GamePlayerBridgeClass target,
        GamePlayerBridgeClass naruto)
    {
        return game.PlayersList.Where(player =>
                IsNaruto(player)
                && (player.GetPlayerId() == naruto.GetPlayerId() || IsNarutoPair(player, naruto))
                && !player.Passives.IsDead
                && !player.Status.IsBlock
                && !player.Status.IsSkip
                && player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
            .Distinct()
            .ToList();
    }

    public static bool IsSoloAttack(
        GameClass game,
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass target) =>
        IsNaruto(attacker) && GetJointAttackers(game, target, attacker).Count == 1;

    public static bool WonPoweredFightLastRound(
        GamePlayerBridgeClass naruto,
        GamePlayerBridgeClass target,
        GameClass game) =>
        naruto.Status.WhoToLostEveryRound.Any(loss =>
            loss.RoundNo == game.RoundNo - 1
            && loss.EnemyId == target.GetPlayerId()
            && (loss.IsTooGoodEnemy || loss.IsTooStronkEnemy));

    public static bool IsSummonAutoWin(
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass target) =>
        IsNaruto(attacker)
        && attacker.Passives.Naruto.SummonAutoWinTarget == target.GetPlayerId();

    public static void RecordResolvedTripleRasengan(
        GamePlayerBridgeClass attacker,
        GameClass game,
        bool attack)
    {
        if (!attack || !IsNaruto(attacker)) return;

        var targetId = attacker.Status.IsWonThisCalculation != Guid.Empty
            ? attacker.Status.IsWonThisCalculation
            : attacker.Status.IsLostThisCalculation;
        if (targetId == Guid.Empty) return;

        var target = game.PlayersList.Find(player => player.GetPlayerId() == targetId);
        if (target == null || GetJointAttackers(game, target, attacker).Count != 3) return;

        var original = game.PlayersList.Find(player =>
            IsNaruto(player)
            && !player.Passives.Naruto.IsClone
            && player.GetPlayerId() == attacker.Passives.Naruto.OriginalPlayerId);
        if (original == null) return;

        if (game.RoundNo == 10 && target.GameCharacter.Name == ErenYeager.CharacterName)
            original.Passives.Naruto.ResolvedTripleRasenganAgainstEren = true;
        if (game.RoundNo == 8 && Madara.IsMadara(target))
            original.Passives.Naruto.ResolvedTripleRasenganAgainstMadara = true;
    }

    public static bool HeroesFailedToSaveWorld(GamePlayerBridgeClass original, GameClass game)
    {
        if (original?.GameCharacter?.Name != CharacterName || original.Passives.Naruto.IsClone)
            return false;

        var state = original.Passives.Naruto;
        var eren = game.PlayersList.Find(player =>
            player.GameCharacter.Name == ErenYeager.CharacterName
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == ErenYeager.Rumbling));
        var failedToStopRumbling = state.ResolvedTripleRasenganAgainstEren
                                   && eren?.Passives.Eren.RumblingTriggered == true;

        var madara = Madara.Find(game);
        var failedToSealMadara = state.ResolvedTripleRasenganAgainstMadara
                                 && madara != null
                                 && !madara.Passives.Madara.Sealed;
        return failedToStopRumbling || failedToSealMadara;
    }

    public static bool PredictionAwardsPoints(
        GamePlayerBridgeClass predictor,
        GamePlayerBridgeClass target) =>
        !IsDispersedClone(predictor) && !IsNarutoPair(predictor, target);

    public static GamePlayerBridgeClass ResolveScoreSuccessor(
        GameClass game,
        GamePlayerBridgeClass player)
    {
        if (!IsDispersedClone(player)) return player;

        return game.PlayersList.Find(candidate =>
                   IsNaruto(candidate)
                   && !candidate.Passives.Naruto.IsClone
                   && candidate.GetPlayerId() == player.Passives.Naruto.OriginalPlayerId)
               ?? player;
    }

    private static int ProjectClonePredictionPoints(
        GameClass game,
        GamePlayerBridgeClass clone)
    {
        if (game.PlayersList.Count != 6 || game.PlayersList.Count(player => player.IsBot()) > 5)
            return 0;
        if (clone.GameCharacter.DoomRollMode
            || clone.GameCharacter.Passive.Any(passive => passive.PassiveName == "Тетрадь смерти"))
            return 0;

        return clone.Predict.Count(prediction =>
        {
            var target = game.PlayersList.Find(player =>
                player.GetPlayerId() == prediction.PlayerId);
            return target != null
                   && target.GameCharacter.Name == prediction.CharacterName
                   && target.GameCharacter.Passive.All(passive =>
                       passive.PassiveName != "Выдуманный персонаж")
                   && PredictionAwardsPoints(clone, target);
        });
    }

    public static void SettleShadowClones(GameClass game)
    {
        if (game.RoundNo != 10) return;

        var original = game.PlayersList.Find(player =>
            IsNaruto(player)
            && !player.Passives.Naruto.IsClone
            && player.Passives.Naruto.OriginalPlayerId == player.GetPlayerId());
        if (original == null || original.Passives.Naruto.ShadowSettlementResolved) return;

        var clones = game.PlayersList.Where(player =>
            IsClone(player)
            && player.Passives.Naruto.OriginalPlayerId == original.GetPlayerId()).ToList();
        if (clones.Count != 2) return;

        original.Passives.Naruto.ShadowSettlementResolved = true;
        decimal total = 0;
        var newDeaths = 0;
        foreach (var clone in clones)
        {
            var predictionPoints = ProjectClonePredictionPoints(game, clone);
            if (predictionPoints > 0)
                clone.Status.AddBonusPoints(predictionPoints, "Предположение");

            total += clone.Status.DrainSettledScoreForTransfer(game, ShadowClones);
            if (!clone.Passives.IsDead)
            {
                clone.Passives.IsDead = true;
                clone.Passives.DeathSource = ShadowClones;
                newDeaths++;
            }

            clone.Passives.Naruto.HasDispersed = true;
            clone.Passives.Naruto.ShadowSettlementResolved = true;
        }

        original.Status.AddSettledScore(total, ShadowClones);
        original.Passives.Naruto.ShadowPointsTransferred = total;
        game.AddGlobalLogs("Теневые: клоны Наруто рассеялись и передали очки оригиналу.");

        if (newDeaths > 0)
        {
            foreach (var monster in game.PlayersList.Where(player =>
                         !player.Passives.IsDead
                         && player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Монстр")))
            {
                monster.Status.AddRegularPoints(newDeaths, "Монстр");
                game.Phrases.MonsterDeath.SendLog(monster, false);
            }
        }

        MoveDispersedClonesToBottom(game.PlayersList);
    }

    public static List<GamePlayerBridgeClass> OrderLeaderboard(
        IEnumerable<GamePlayerBridgeClass> players)
    {
        var playerList = players.ToList();
        foreach (var clone in playerList.Where(IsDispersedClone))
            clone.Status.DiscardScoreAfterDeath();

        List<GamePlayerBridgeClass> OrderByCurrentScore() =>
            JonSnow.ApplyLeaderboardRules(playerList
                .OrderBy(player => player.Passives.IsDead)
                .ThenBy(IsDispersedClone)
                .ThenByDescending(player => player.Status.GetScore())
                .ToList());

        var ordered = OrderByCurrentScore();
        var homelander = ordered.FirstOrDefault(Homelander.Is);
        var sevenWasActive = homelander?.Status.HomelanderSevenPointsActive == true;
        Homelander.UpdateSevenPointsAvailability(ordered);

        // Праведность is intentionally hysteretic: an active Seven helps Homelander retain first
        // place, but after that effective score loses the lead the seven points freeze immediately
        // and the remaining rows must be ranked again without them. A frozen Seven reactivates only
        // after Homelander reaches first place on his underlying score.
        if (sevenWasActive && homelander?.Status.HomelanderSevenPointsActive == false)
            ordered = OrderByCurrentScore();

        return ordered;
    }

    public static void MoveDispersedClonesToBottom(List<GamePlayerBridgeClass> players)
    {
        foreach (var clone in players.Where(IsDispersedClone))
            clone.Status.DiscardScoreAfterDeath();

        var ordered = players.Where(player => !IsDispersedClone(player))
            .Concat(players.Where(IsDispersedClone))
            .ToList();
        players.Clear();
        players.AddRange(ordered);
        for (var index = 0; index < players.Count; index++)
            players[index].Status.SetPlaceAtLeaderBoard(index + 1);
    }
}
