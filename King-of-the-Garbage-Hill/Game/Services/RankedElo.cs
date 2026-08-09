namespace King_of_the_Garbage_Hill.Game.Services;

/// <summary>
/// Authoritative Ranked rating rules. Placement uses the economy active at the player's
/// rating immediately before settlement; the >1400 table begins strictly at 1401.
/// </summary>
public static class RankedElo
{
    public const int AdvancedEconomyThreshold = 1200;
    public const int EliteEconomyThreshold = 1400;
    public const int ShinigamiPenalty = -5;
    public const int BlackjackRecovery = 5;

    public static int GetPlacementDelta(int currentRating, int place)
    {
        if (currentRating > EliteEconomyThreshold)
        {
            return place switch
            {
                1 => 20,
                2 => 0,
                3 => -1,
                4 => -3,
                5 => -5,
                6 => -10,
                _ => 0,
            };
        }

        if (currentRating >= AdvancedEconomyThreshold)
        {
            return place switch
            {
                1 => 20,
                2 => 0,
                3 => 0,
                4 => -1,
                5 => -3,
                6 => -5,
                _ => 0,
            };
        }

        return place switch
        {
            1 => 10,
            2 => 5,
            3 => 1,
            4 => 0,
            5 => -1,
            6 => -5,
            _ => 0,
        };
    }
}
