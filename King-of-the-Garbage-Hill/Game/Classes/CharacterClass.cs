namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class CharacterClass
    {

        public string Name;
        public int Intelligence; 
        public int Psyche;
        public int Speed;
        public int Strength;
        public JusticeClass Justice;
        public string Avatar;



        public CharacterClass(int intelligence, int strength, int speed, int psyche, string name)
        {
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
            Justice = new JusticeClass();
            Name = name;
        }
    }

    public class Passive
    {
        public string PassiveDescription;
        public string PassiveName;

        public Passive()
        {
            PassiveDescription = "";
            PassiveName = "";
        }

        public Passive(string passiveName, string passiveDescription)
        {
            PassiveDescription = passiveDescription;
            PassiveName = passiveName;
        }
    }

    public class JusticeClass
    {
        public int JusticeNow;
        public int JusticeForNextRound;
        public bool IsWonThisRound;

        public JusticeClass()
        {
            JusticeNow = 0;
            JusticeForNextRound = 0;
            IsWonThisRound = false;
        }
    }
}