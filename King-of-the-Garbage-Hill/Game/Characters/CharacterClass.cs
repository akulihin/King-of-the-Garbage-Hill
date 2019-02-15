using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterClass
    {
        public int Intelligence;
        public List<Passive> PassiveSet;
        public int Psyche;
        public int Speed;
        public int Strength;

        public CharacterClass()
        {
            Intelligence = 0;
            Strength = 0;
            Speed = 0;
            Psyche = 0;
            PassiveSet = new List<Passive>();
        }

        public CharacterClass(int intelligence, int strength, int speed, int psyche, List<Passive> passiveSet)
        {
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
            PassiveSet = passiveSet;
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
}