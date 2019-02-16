using System.Collections.Generic;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CharacterClass
    {
 

        public int Intelligence; 
        public int Psyche;
        public int Speed;
        public int Strength;
        public int Justice;
        public List<Passive> PassiveSet;
        public string Avatar;

        public CharacterClass()
        {
            Intelligence = 0;
            Strength = 0;
            Speed = 0;
            Psyche = 0;
            Justice = 0;
            PassiveSet = new List<Passive>();
        }

        public CharacterClass(int intelligence, int strength, int speed, int psyche, int justice, List<Passive> passiveSet)
        {
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
            PassiveSet = passiveSet;
            Justice = justice;
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