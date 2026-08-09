using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>Grab trap and region reward rules of the Famous Capturing Ship.</summary>
public static class BattleshipCapturingMechanics
{
    public static (int row, int col) GetGrabCell(Ship ship)
    {
        if (ship.PreservedGrabRow.HasValue && ship.PreservedGrabCol.HasValue)
            return (ship.PreservedGrabRow.Value, ship.PreservedGrabCol.Value);

        var (rowOffset, colOffset) = ship.Orientation switch
        {
            Orientation.Vertical => (1, 0),
            Orientation.HorizontalReverse => (0, -1),
            Orientation.VerticalReverse => (-1, 0),
            _ => (0, 1),
        };
        return (ship.Row + rowOffset, ship.Col + colOffset);
    }

    public static Ship FindLiveGrabShip(BattleshipPlayer boardOwner, int row, int col) =>
        boardOwner?.Board.PlacedShips.FirstOrDefault(ship =>
            IsActiveAbilitySource(ship, "grab_summon") && GetGrabCell(ship) == (row, col));

    public static bool TryGrabSummon(
        BattleshipGame game,
        BattleshipPlayer summonOwner,
        BattleshipPlayer boardOwner,
        Summon summon,
        int row,
        int col)
    {
        if (game == null || summonOwner == null || boardOwner == null || summon == null ||
            !summon.IsAlive || summon.IsBoardingShip || summon.OwnerId == boardOwner.DiscordId ||
            FindLiveGrabShip(boardOwner, row, col) == null)
            return false;

        summon.IsAlive = false;
        foreach (var cell in boardOwner.Board.Grid.Cast<Cell>())
        {
            if (cell.SummonRef == summon)
                cell.SummonRef = null;
        }
        var deathCell = boardOwner.Board.GetCell(row, col);
        if (deathCell != null && deathCell.SummonDeaths.All(marker => marker.SummonId != summon.Id))
        {
            deathCell.SummonDeaths.Add(new SummonMarker
            {
                SummonId = summon.Id,
                Type = summon.Type,
                IsBoardingShip = summon.IsBoardingShip,
                SourceShipName = summon.SourceShipName,
            });
        }

        boardOwner.PendingSummons.Add(CreateFreePending(
            summon.Type,
            summon.Speed,
            summon.CollisionDamage,
            summon.RevealRadius,
            SourceDisplayName(summon.Type)));

        if (HasLivingMast(summonOwner))
        {
            game.AddLogFor(summonOwner.DiscordId,
                $"[Мачта] \"Капитан, {SummonAccusative(summon.Type)} захватили, Ироды!\"");
        }
        game.AddLogFor(boardOwner.DiscordId,
            $"{SummonNominative(summon.Type)} захвачен , My Lord.");
        return true;
    }

    /// <summary>
    /// Grants the active capturing-ship owner a base summon when their Pirate Boat applies a
    /// first Capture. The target loses one future summon entitlement, floored at zero.
    /// </summary>
    public static bool GrantCaptureReward(
        BattleshipGame game,
        BattleshipPlayer capturingPlayer,
        BattleshipPlayer targetOwner,
        Ship capturedShip)
    {
        if (game == null || capturingPlayer == null || targetOwner == null || capturedShip == null ||
            !HasLiveCaptureRewardSource(capturingPlayer))
            return false;

        SummonType? reward = capturedShip.Regions.Contains(Region.South)
            ? SummonType.PirateBoat
            : capturedShip.Regions.Contains(Region.West)
                ? SummonType.Ram
                : capturedShip.Regions.Contains(Region.East)
                    ? SummonType.Scout
                    : null;
        targetOwner.MaxSummonSlots = Math.Max(0, targetOwner.MaxSummonSlots - 1);
        if (!reward.HasValue) return true;

        var pending = reward.Value switch
        {
            SummonType.Ram => CreateFreePending(reward.Value, 2, 4, 1, "Таран"),
            SummonType.Scout => CreateFreePending(reward.Value, 1, 0, 1, "Разведчик"),
            _ => CreateFreePending(reward.Value, 1, 0, 1, "Пиратская лодка"),
        };
        capturingPlayer.PendingSummons.Add(pending);
        game.AddLogFor(capturingPlayer.DiscordId,
            $"Знаменитый захватывающий корабль даёт призыв: {pending.SourceShipName}.");
        return true;
    }

    public static bool HasLiveCaptureRewardSource(BattleshipPlayer player)
    {
        if (player == null) return false;
        if (player.Board.PlacedShips.Any(ship => IsActiveAbilitySource(ship, "capture_reward")))
            return true;

        // Boarding snapshots expose transferable ability keys before and after their mandatory
        // deployment. Keeping the pending snapshot active removes deployment-order exploits.
        if (player.PendingSummons.Any(pending => pending.IsBoarding &&
                pending.BoardingAbilities.Contains("capture_reward") &&
                pending.BoardingDecks.Any(deck => !deck.IsDestroyed) &&
                !pending.BoardingStatuses.Any(status => status is
                    ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze)))
            return true;

        return player.Summons.Any(summon => summon.IsAlive && summon.IsBoardingShip &&
            summon.BoardingAbilities.Contains("capture_reward") &&
            !summon.BoardingStatuses.Any(status => status is
                ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze));
    }

    public static bool IsActiveAbilitySource(Ship ship, string ability) =>
        ship is { IsDestroyed: false } &&
        ship.Abilities.Contains(ability) &&
        !ship.Statuses.Any(status => status is
            ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze);

    public static bool HasLivingMast(BattleshipPlayer player) =>
        BattleshipGameEngine.HasLivingMast(player);

    private static PendingSummonDeploy CreateFreePending(
        SummonType type,
        int speed,
        int collisionDamage,
        int revealRadius,
        string sourceName) => new()
    {
        Type = type,
        AllowedColumns = Enumerable.Range(0, 10).ToList(),
        IsFree = true,
        Speed = Math.Max(1, speed),
        CollisionDamage = collisionDamage,
        RevealRadius = Math.Max(1, revealRadius),
        SourceShipName = sourceName,
    };

    private static string SourceDisplayName(SummonType type) => SummonNominative(type);

    private static string SummonNominative(SummonType type) => type switch
    {
        SummonType.Scout => "Разведчик",
        SummonType.Ram => "Таран",
        SummonType.PirateBoat => "Пиратская лодка",
        SummonType.CursedBoat => "Проклятая лодка",
        SummonType.Brander => "Брандер",
        _ => type.ToString(),
    };

    private static string SummonAccusative(SummonType type) => type switch
    {
        SummonType.Scout => "Разведчика",
        SummonType.Ram => "Таран",
        SummonType.PirateBoat => "Пиратскую лодку",
        SummonType.CursedBoat => "Проклятую лодку",
        SummonType.Brander => "Брандер",
        _ => type.ToString(),
    };
}
