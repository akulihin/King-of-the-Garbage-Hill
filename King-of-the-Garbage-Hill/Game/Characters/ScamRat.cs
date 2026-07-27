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
    public const int IntelligenceCheckMinimum = 3;
    public const int IntelligenceCheckMaximum = 10;
    public const int SaleBonusPoints = 1;
    public const int JusticeCapLossPerSale = 1;

    public sealed class State
    {
        public HashSet<Guid> EverGpuOwnerIds { get; set; } = new();
        public HashSet<Guid> ActiveGpuOwnerIds { get; set; } = new();
        public int LastIntelligenceRoll { get; set; }
        public Guid LastIntelligenceTargetId { get; set; } = Guid.Empty;
        public int LastExplosionRound { get; set; }
        public decimal LastExplosionPoints { get; set; }
        public decimal TotalExplosionPoints { get; set; }
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static bool HasPassive(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Passive?.Any(passive => passive.PassiveName == PassiveName) == true;

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
        holder.Status.AddInGamePersonalLogs(
            $"{PassiveName}: проверка {target.DiscordUsername}: {roll} против {target.FightCharacter.GetIntelligence()} Интеллекта.\n");
        if (roll <= target.FightCharacter.GetIntelligence()
            || !Homelander.CanTransferFrom(target, PassiveName))
            return;

        TransferExactBonusPoints(target, holder, SaleBonusPoints);
        state.EverGpuOwnerIds.Add(target.GetPlayerId());
        state.ActiveGpuOwnerIds.Add(target.GetPlayerId());
        holder.GameCharacter.Justice.ReduceMaximumRealJustice(
            JusticeCapLossPerSale,
            PassiveName);
        holder.Status.AddInGamePersonalLogs(
            $"{PassiveName}: {target.DiscordUsername} купил видеокарту за {SaleBonusPoints} бонусное очко.\n");
        target.Status.AddInGamePersonalLogs(
            $"{PassiveName}: вы купили видеокарту у {holder.DiscordUsername}.\n");
        game.Phrases.ScamRatSale.SendLog(holder, false, isRandomOrder: false);
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

            TransferExactBonusPoints(scoreVictim, holder, amount);
            state.LastExplosionPoints += amount;
        }

        state.TotalExplosionPoints += state.LastExplosionPoints;
        holder.Status.AddInGamePersonalLogs(
            $"{PassiveName}: видеокарты взорваны, украдено {state.LastExplosionPoints} очк.\n");
        game.Phrases.ScamRatExplosion.SendLog(holder, false, isRandomOrder: false);
    }

    private static void TransferExactBonusPoints(
        GamePlayerBridgeClass victim,
        GamePlayerBridgeClass receiver,
        decimal amount)
    {
        victim.Status.AddBonusPoints(GetRoyalAdjustedArgument(victim, -amount), PassiveName);
        receiver.Status.AddBonusPoints(GetRoyalAdjustedArgument(receiver, amount), PassiveName);
    }

    private static decimal GetRoyalAdjustedArgument(GamePlayerBridgeClass player, decimal amount) =>
        JonSnow.IsKingActive(
            player.GameCharacter,
            player.Status.GetPlaceAtLeaderBoard())
            ? amount / 2
            : amount;
}
