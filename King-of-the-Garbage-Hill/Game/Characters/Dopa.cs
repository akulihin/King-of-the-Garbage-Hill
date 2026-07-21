namespace King_of_the_Garbage_Hill.Game.Characters;

public class Dopa
{
    public const string CharacterName = "Dopa";
    public const string Macro = "Макро";
    public const string Vision = "Взгляд в будущее";
    public const string Permaban = "Permaban";
    public const string Meta = "Мета";

    public class MacroClass
    {
        public int FightsProcessed { get; set; } = 0;
        public int FightsResolved { get; set; } = 0;
    }

    public class VisionClass
    {
        public int Cooldown { get; set; } = 0;
    }

    public class MetaChoiceClass
    {
        public bool Triggered { get; set; } = false;
        public string ChosenTactic { get; set; } = "";
        public int StatLevelUpsTaken { get; set; } = 0;
    }
}
