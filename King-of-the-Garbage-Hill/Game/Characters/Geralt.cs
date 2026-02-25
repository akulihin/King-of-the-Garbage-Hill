using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Geralt
{
    public class ContractsClass
    {
        // Key = enemy player ID, Value = list of monster types on that enemy
        public Dictionary<Guid, List<string>> ContractMap { get; set; } = new();
        public List<string> OilInventory { get; set; } = new();
        public bool OilsActivated { get; set; } = false;
        public bool IsMeditating { get; set; } = false;
        public bool WasAttackedDuringMeditation { get; set; } = false;
        public bool LambertMixup { get; set; } = false;
        public Dictionary<Guid, Queue<string>> CurrentRoundFightQueue { get; set; } = new();
        public int LastOilBonusCount { get; set; } = 0;
    }

    public class DetectiveClass
    {
        public List<Guid> HintedPlayers { get; set; } = new();
    }
}
