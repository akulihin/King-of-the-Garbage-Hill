using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class ScamRat
{
    public const string CharacterName = "ScamRat";
    public const string PassiveName = "Взрывной майнинг!";
    public const string SharingPassiveName = "Sharing is CARRYING!";
    public const string SharingPhraseSource = "Sharing is CARRYING";
    public const int IntelligenceCheckMinimum = 3;
    public const int IntelligenceCheckMaximum = 10;
    public const int SaleBonusPoints = 1;
    public const int JusticeCapLossPerSale = 1;
    public const int CarryPointsPerStolenWin = 1;
    public const int CarryStatCost = 1;
    public const int CarryBonusPointCost = 1;

    public sealed class State
    {
        public HashSet<Guid> EverGpuOwnerIds { get; set; } = new();
        public HashSet<Guid> ActiveGpuOwnerIds { get; set; } = new();
        public int LastIntelligenceRoll { get; set; }
        public Guid LastIntelligenceTargetId { get; set; } = Guid.Empty;
        public int LastExplosionRound { get; set; }
        public decimal LastExplosionPoints { get; set; }
        public decimal TotalExplosionPoints { get; set; }
        public int CarryPoints { get; set; }
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static bool HasPassive(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Passive?.Any(passive => passive.PassiveName == PassiveName) == true;

    public static bool HasSharingPassive(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Passive?.Any(passive =>
            passive.PassiveName == SharingPassiveName) == true;

    public static bool UsesCarryShop(GamePlayerBridgeClass player) =>
        Is(player) && HasSharingPassive(player);

    public static void TrySellGpu(GamePlayerBridgeClass holder, GameClass game)
    {
        if (!HasPassive(holder)
            || holder.Passives.IsDead
            || holder.Status.IsWonThisCalculation == Guid.Empty)
            return;

        var target = game.PlayersList.Find(player =>
            player.GetPlayerId() == holder.Status.IsWonThisCalculation);
        if (target == null
            || target.Passives.IsDead
            || UnknownBug.Is(target)
            || holder.IsTeamMember(game, target.GetPlayerId()))
            return;

        var state = holder.Passives.ScamRat;
        if (state.EverGpuOwnerIds.Contains(target.GetPlayerId()))
            return;

        var roll = SecureRandom.Next(IntelligenceCheckMinimum, IntelligenceCheckMaximum);
        state.LastIntelligenceRoll = roll;
        state.LastIntelligenceTargetId = target.GetPlayerId();
        if (roll <= target.FightCharacter.GetIntelligence()
            || !Homelander.CanTransferFrom(target, PassiveName))
            return;

        TransferExactBonusPoints(
            target,
            holder,
            SaleBonusPoints,
            PassiveName,
            FeedbackSourceVisibility.ProNeutralTarget);
        state.EverGpuOwnerIds.Add(target.GetPlayerId());
        state.ActiveGpuOwnerIds.Add(target.GetPlayerId());
        holder.GameCharacter.Justice.ReduceMaximumRealJustice(
            JusticeCapLossPerSale,
            PassiveName);
        holder.Status.AddInGamePersonalLogs(
            PhrasePayload.EncodeOwnerOnly(
                PassiveName,
                $"{PassiveName}: {target.DiscordUsername} купил видеокарту за {SaleBonusPoints} бонусное очко.",
                "Explosive Mining!",
                $"Explosive Mining!: {target.DiscordUsername} bought a graphics card for {SaleBonusPoints} bonus point.",
                "",
                "",
                holder.GetPlayerId()) + "\n");
        target.Status.AddInGamePersonalLogs(
            PhrasePayload.EncodeOwnerOnly(
                PassiveName,
                $"{PassiveName}: вы купили видеокарту у {holder.DiscordUsername}.",
                "Explosive Mining!",
                $"Explosive Mining!: you bought a graphics card from {holder.DiscordUsername}.",
                "",
                "",
                holder.GetPlayerId()) + "\n");
        game.Phrases.ScamRatGpuSale.SendLog(holder, false, isRandomOrder: false);
    }

    public static void ExplodeOnBlock(GameClass game)
    {
        if (game.IsKratosEvent || game.RoundNo > 10) return;

        foreach (var holder in game.PlayersList.Where(player =>
                     !player.Passives.IsDead
                     && player.Status.IsBlock
                     && HasPassive(player)))
        {
            Explode(holder, game);
        }
    }

    public static bool HasActiveGpu(GamePlayerBridgeClass holder, Guid playerId) =>
        holder?.Passives?.ScamRat?.ActiveGpuOwnerIds.Contains(playerId) == true;

    public static bool HasExplodedGpu(GamePlayerBridgeClass holder, Guid playerId) =>
        holder?.Passives?.ScamRat is { } state
        && state.EverGpuOwnerIds.Contains(playerId)
        && !state.ActiveGpuOwnerIds.Contains(playerId);

    public static bool CanCarryJointWin(
        GamePlayerBridgeClass holder,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!UsesCarryShop(holder) || target == null || game == null)
            return false;

        var holderStatTotal = GetStatTotal(holder.GameCharacter);
        var targetId = target.GetPlayerId();
        var otherAttackers = game.PlayersList
            .Where(attacker =>
                attacker.GetPlayerId() != holder.GetPlayerId()
                && attacker.GetPlayerId() != targetId
                && attacker.Status.WhoToAttackThisTurn.Contains(targetId)
                && !attacker.IsTeamMember(game, targetId))
            .ToList();

        return otherAttackers.Count > 0
               && otherAttackers.All(attacker =>
                   GetStatTotal(attacker.GameCharacter) < holderStatTotal);
    }

    public static void RecordCarryStolenWin(GamePlayerBridgeClass holder, GameClass game)
    {
        if (!UsesCarryShop(holder)) return;

        holder.Passives.ScamRat.CarryPoints += CarryPointsPerStolenWin;
        game.Phrases.ScamRatSharingSteal.SendLog(holder, false);
    }

    public static bool TryPurchaseStat(
        GamePlayerBridgeClass holder,
        GameClass game,
        int statIndex)
    {
        if (!UsesCarryShop(holder)
            || game == null
            || holder.Passives.ScamRat.CarryPoints < CarryStatCost
            || holder.Status.GetScore() < CarryBonusPointCost)
            return false;

        var before = statIndex switch
        {
            1 => holder.GameCharacter.GetIntelligence(),
            2 => holder.GameCharacter.GetStrength(),
            3 => holder.GameCharacter.GetSpeed(),
            4 => holder.GameCharacter.GetPsyche(),
            _ => -1,
        };
        if (before is < 0 or >= 10) return false;

        switch (statIndex)
        {
            case 1:
                holder.GameCharacter.AddIntelligence(1, SharingPhraseSource);
                break;
            case 2:
                holder.GameCharacter.AddStrength(1, SharingPhraseSource);
                break;
            case 3:
                holder.GameCharacter.AddSpeed(1, SharingPhraseSource);
                break;
            case 4:
                holder.GameCharacter.AddPsyche(1, SharingPhraseSource);
                break;
        }

        var after = statIndex switch
        {
            1 => holder.GameCharacter.GetIntelligence(),
            2 => holder.GameCharacter.GetStrength(),
            3 => holder.GameCharacter.GetSpeed(),
            4 => holder.GameCharacter.GetPsyche(),
            _ => before,
        };
        if (after <= before) return false;

        holder.Passives.ScamRat.CarryPoints -= CarryStatCost;
        holder.Status.AddBonusPoints(-CarryBonusPointCost, SharingPhraseSource);
        game.Phrases.ScamRatSharingPurchase.SendLog(holder, false);
        return true;
    }

    public static void SpendCarryPointsForBot(GamePlayerBridgeClass holder, GameClass game)
    {
        if (!holder.IsBot() || !UsesCarryShop(holder)) return;

        while (holder.Passives.ScamRat.CarryPoints >= CarryStatCost
               && holder.Status.GetScore() >= CarryBonusPointCost)
        {
            var availableStats = new List<(int Index, int Value)>
            {
                (1, holder.GameCharacter.GetIntelligence()),
                (2, holder.GameCharacter.GetStrength()),
                (3, holder.GameCharacter.GetSpeed()),
                (4, holder.GameCharacter.GetPsyche()),
            }.Where(stat => stat.Value < 10).ToList();
            if (availableStats.Count == 0) return;

            var minimum = availableStats.Min(stat => stat.Value);
            var weakestStats = availableStats
                .Where(stat => stat.Value == minimum)
                .ToList();
            var selected = weakestStats[
                SecureRandom.Next(0, weakestStats.Count - 1)].Index;
            if (!TryPurchaseStat(holder, game, selected)) return;
        }
    }

    private static void Explode(GamePlayerBridgeClass holder, GameClass game)
    {
        var state = holder.Passives.ScamRat;
        if (state.ActiveGpuOwnerIds.Count == 0) return;

        var ownerIds = state.ActiveGpuOwnerIds.ToList();
        state.ActiveGpuOwnerIds.Clear();
        state.LastExplosionRound = game.RoundNo;
        state.LastExplosionPoints = 0;

        foreach (var ownerId in ownerIds)
        {
            var owner = game.PlayersList.Find(player => player.GetPlayerId() == ownerId);
            if (owner == null) continue;

            var amount = owner.Status.GetPreviousRoundAbilityPoints();
            var scoreVictim = Naruto.ResolveScoreSuccessor(game, owner);
            if (amount <= 0
                || scoreVictim == null
                || UnknownBug.Is(scoreVictim)
                || !Homelander.CanTransferFrom(scoreVictim, PassiveName))
                continue;

            TransferExactBonusPoints(
                scoreVictim,
                holder,
                amount,
                PassiveName,
                FeedbackSourceVisibility.ProNeutralTarget);
            state.LastExplosionPoints += amount;
        }

        state.TotalExplosionPoints += state.LastExplosionPoints;
        holder.Status.AddInGamePersonalLogs(
            $"{PassiveName}: видеокарты взорваны, украдено {state.LastExplosionPoints} очк.\n");
    }

    private static int GetStatTotal(CharacterClass character) =>
        character.GetIntelligence()
        + character.GetStrength()
        + character.GetSpeed()
        + character.GetPsyche();

    public static void TransferExactBonusPoints(
        GamePlayerBridgeClass victim,
        GamePlayerBridgeClass receiver,
        decimal amount,
        string source,
        FeedbackSourceVisibility victimSourceVisibility = FeedbackSourceVisibility.NamedTarget)
    {
        victim.Status.AddBonusPoints(
            GetRoyalAdjustedArgument(victim, -amount), source, victimSourceVisibility);
        receiver.Status.AddBonusPoints(GetRoyalAdjustedArgument(receiver, amount), source);
    }

    private static decimal GetRoyalAdjustedArgument(GamePlayerBridgeClass player, decimal amount) =>
        JonSnow.IsKingActive(
            player.GameCharacter,
            player.Status.GetPlaceAtLeaderBoard())
            ? amount / 2
            : amount;
}
