using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Clash.Models;

namespace King_of_the_Garbage_Hill.Clash.Logic;

/// <summary>
/// The bot consumes the same personalized DTO as a player. Hidden enemy drafts are
/// therefore structurally unavailable to its policy.
/// </summary>
public static class ClashBotAI
{
    public static List<string> ChooseArmy(int width, int length)
    {
        var ids = ClashCatalog.All.Select(unit => unit.Id).ToList();
        var result = new List<string>(width * length);
        for (var index = 0; index < width * length; index++)
            result.Add(ids[index % ids.Count]);
        return result;
    }

    public static ClashBotDecision Decide(ClashGameStateDto state)
    {
        if (state?.Guest is not { IsBot: true, IsMe: true } bot)
            return new ClashBotDecision();

        if (state.CanPlace && state.RequiredPlacementRow is int placementRow)
        {
            var boardRow = state.Length + placementRow;
            var column = FirstEmptyColumn(state, boardRow);
            var unit = bot.Hand.FirstOrDefault();
            if (column >= 0 && unit != null)
            {
                return new ClashBotDecision
                {
                    Kind = ClashBotActionKind.PlaceUnit,
                    UnitInstanceId = unit.InstanceId,
                    Row = placementRow,
                    Column = column,
                };
            }
        }

        if (state.CanConfirmPlacement)
            return new ClashBotDecision { Kind = ClashBotActionKind.ConfirmPlacement };

        if (state.CanPlaceReinforcement)
        {
            var unit = bot.Hand.FirstOrDefault();
            if (unit != null)
            {
                for (var localRow = 2; localRow < state.Length; localRow++)
                {
                    var column = FirstEmptyColumn(state, state.Length + localRow);
                    if (column < 0) continue;
                    return new ClashBotDecision
                    {
                        Kind = ClashBotActionKind.PlaceReinforcement,
                        UnitInstanceId = unit.InstanceId,
                        Row = localRow,
                        Column = column,
                    };
                }
            }
            return new ClashBotDecision { Kind = ClashBotActionKind.Continue };
        }

        if (state.CanContinue)
            return new ClashBotDecision { Kind = ClashBotActionKind.Continue };

        return new ClashBotDecision();
    }

    private static int FirstEmptyColumn(ClashGameStateDto state, int boardRow)
    {
        for (var column = 0; column < state.Width; column++)
        {
            var cell = state.BoardCells.FirstOrDefault(candidate =>
                candidate.BoardRow == boardRow && candidate.Column == column);
            if (cell?.Unit == null) return column;
        }
        return -1;
    }
}
