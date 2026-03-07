using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Validates ship placement on the 10x10 grid.
/// Rules: no overlaps, 1-cell spacing between ships, Tetra/Far ships in rows 8-9 (bottom two).
/// </summary>
public static class PlacementValidator
{
    public const int GridSize = 10;

    public static (bool valid, string error) ValidatePlacement(Board board, Ship ship, int row, int col, Orientation orientation)
    {
        var cells = GetOccupiedCells(ship.Decks.Count, row, col, orientation);

        // Check bounds
        foreach (var (r, c) in cells)
        {
            if (r < 0 || r >= GridSize || c < 0 || c >= GridSize)
                return (false, "Корабль выходит за пределы поля.");
        }

        // Tetra/Far ships CAN be placed anywhere (rows 0-9).
        // But if any deck is on row 8-9, ALL decks must stay within rows 8-9.
        if (ship.Range is RangeClass.Far or RangeClass.Tetra)
        {
            var anyInBottom = cells.Any(c => c.row >= 8);
            var anyOutside = cells.Any(c => c.row < 8);
            if (anyInBottom && anyOutside)
                return (false, "Если дальнобойный/тетра корабль стоит в рядах 9-10, все палубы должны быть в рядах 9-10.");
        }

        // Non-Tetra/Far ships cannot be placed in rows 8-9
        if (ship.Range is not (RangeClass.Far or RangeClass.Tetra))
        {
            foreach (var (r, _) in cells)
            {
                if (r >= 8)
                    return (false, "Только дальнобойные и тетра-корабли могут быть в рядах 9-10.");
            }
        }

        // Check overlaps and spacing with existing ships
        foreach (var (r, c) in cells)
        {
            var cell = board.GetCell(r, c);
            if (cell?.ShipRef != null)
                return (false, "Клетка уже занята другим кораблём.");
        }

        // Check spacing (use max of both ships' Space values for gap)
        foreach (var existingShip in board.PlacedShips)
        {
            if (existingShip.Id == ship.Id) continue; // skip self when re-placing

            var spacing = System.Math.Max(ship.Space, existingShip.Space);
            var existingCells = existingShip.GetOccupiedCells();
            foreach (var (er, ec) in existingCells)
            {
                foreach (var (nr, nc) in cells)
                {
                    var rowDist = System.Math.Abs(er - nr);
                    var colDist = System.Math.Abs(ec - nc);
                    if (rowDist <= spacing && colDist <= spacing)
                        return (false, $"Слишком близко к кораблю {existingShip.Name}. Нужен зазор в {spacing} клетку(и).");
                }
            }
        }

        return (true, null);
    }

    public static List<(int row, int col)> GetOccupiedCells(int deckCount, int row, int col, Orientation orientation)
    {
        var cells = new List<(int, int)>();
        for (var i = 0; i < deckCount; i++)
        {
            var r = orientation == Orientation.Vertical ? row + i : row;
            var c = orientation == Orientation.Horizontal ? col + i : col;
            cells.Add((r, c));
        }
        return cells;
    }

    /// <summary>
    /// Check if all ships in fleet are placed on the board.
    /// </summary>
    public static (bool valid, string error) ValidateAllPlaced(List<Ship> fleet, Board board)
    {
        foreach (var ship in fleet)
        {
            if (!ship.IsPlaced)
                return (false, $"Корабль {ship.Name} не размещён.");
        }
        return (true, null);
    }
}
