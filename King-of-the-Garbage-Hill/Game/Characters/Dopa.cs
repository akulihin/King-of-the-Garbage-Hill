namespace King_of_the_Garbage_Hill.Game.Characters;

public class Dopa
{
    public class MacroClass
    {
        public int FightsProcessed { get; set; } = 0;
    }

    public class VisionClass
    {
        public int Cooldown { get; set; } = 0;
    }

    public class MetaChoiceClass
    {
        public bool Triggered { get; set; } = false;
        public string ChosenTactic { get; set; } = "";
    }
}
