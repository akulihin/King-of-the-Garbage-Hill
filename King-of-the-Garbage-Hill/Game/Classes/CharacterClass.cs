using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class CharacterClass
    {
        public CharacterClass(int intelligence, int strength, int speed, int psyche, string name)
        {
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
            Justice = new JusticeClass();
            Name = name;
        }

        public string Name { get; set; }
        private int Intelligence { get; set; }
        private string IntelligenceExtraText { get; set; }
        private int Psyche { get; set; }
        private string PsycheExtraText { get; set; }
        private int Speed { get; set; }
        private string SpeedExtraText { get; set; }
        private int Strength { get; set; }
        private string StrengthExtraText { get; set; }
        private double Skill { get; set; }
        private string CurrentSkillTarget { get; set; } = "Ничего";
        private int Moral { get; set; }
        private int BonusPointsFromMoral { get; set; }
        public JusticeClass Justice { get; set; }
        public string Avatar { get; set; }
        public List<Passive> Passive { get; set; }

        public int GetBonusPointsFromMoral()
        {
            return BonusPointsFromMoral;
        }

        public void SetBonusPointsFromMoral(int newBonusPointsFromMoral)
        {
            BonusPointsFromMoral = newBonusPointsFromMoral;
        }

        public string GetCurrentSkillTarget()
        {
            return CurrentSkillTarget;
        }

        public void RollCurrentSkillTarget()
        {
            switch (CurrentSkillTarget)
            {
                case "Интеллект":
                    CurrentSkillTarget = "Сила";
                    break;
                case "Сила":
                    CurrentSkillTarget = "Скорость";
                    break;
                case "Скорость":
                    CurrentSkillTarget = "Интеллект";
                    break;
                case "Ничего":
                    CurrentSkillTarget = RandomCurrentSkillTarget();
                    break;
            }
        }

        public string RandomCurrentSkillTarget()
        {
            var skillsSet = new List<string> {"Интеллект", "Скорость", "Сила"};
            var rand = new Random();
            return skillsSet[rand.Next(0, 2)];
        }

        public double GetSkill()
        {
            return Skill;
        }

        public void SetSkill(InGameStatus status, double howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} скилла\n");
            Skill = howMuchToSet;
        }

        public void AddSkill(InGameStatus status, string skillName, bool isLog = true)
        {
            status.AddInGamePersonalLogs($" + скилл (за {skillName} врага)\n");


            var howMuchToAdd = Skill switch
            {
                0 => 10,
                10 => 9,
                19 => 8,
                27 => 7,
                34 => 6,
                40 => 5,
                45 => 4,
                49 => 3,
                52 => 2,
                54 => 1,
                _ => 0
            };

            Skill += howMuchToAdd;
        }

        public int GetMoral()
        {
            return Moral;
        }

        public void SetMoral(InGameStatus status, int howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} морали\n");
            Moral = howMuchToSet;
        }

        public void AddMoral(InGameStatus status, int howMuchToAdd = 0, string skillName = "")
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}+{howMuchToAdd} морали\n");
            else if (howMuchToAdd < 0) status.AddInGamePersonalLogs($"{skillName}{howMuchToAdd} морали\n");

            Moral += howMuchToAdd;
            if (Moral < 0) Moral = 0;
        }


        public void AddIntelligence(InGameStatus status, int howMuchToAdd = 1, string skillName = "")
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}+{howMuchToAdd} интеллект\n");
            else if (howMuchToAdd < 0) status.AddInGamePersonalLogs($"{skillName}{howMuchToAdd} интеллект\n");


            Intelligence += howMuchToAdd;

            if (Intelligence < 0)
                Intelligence = 0;
            if (Intelligence > 10)
                Intelligence = 10;
        }

        public int GetIntelligence()
        {
            return Intelligence;
        }

        public string GetIntelligenceString()
        {
            return $"{Intelligence}{IntelligenceExtraText}";
        }

        public void SetIntelligence(InGameStatus status, int howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} интеллект\n");

            Intelligence = howMuchToSet;
            if (Intelligence < 0)
                Intelligence = 0;
            if (Intelligence > 10)
                Intelligence = 10;
        }

        public void SetIntelligenceExtraText(string newIntelligenceExtraText)
        {
            IntelligenceExtraText = newIntelligenceExtraText;
        }

        public void AddPsyche(InGameStatus status, int howMuchToAdd = 1, string skillName = "")
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}+{howMuchToAdd} психика\n");
            else if (howMuchToAdd < 0) status.AddInGamePersonalLogs($"{skillName}{howMuchToAdd} психика\n");


            Psyche += howMuchToAdd;

            if (Psyche < 0)
                Psyche = 0;
            if (Psyche > 10)
                Psyche = 10;
        }

        public int GetPsyche()
        {
            return Psyche;
        }

        public string GetPsycheString()
        {
            return $"{Psyche}{PsycheExtraText}";
        }

        public void SetPsyche(InGameStatus status, int howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} психики\n");
            Psyche = howMuchToSet;
            if (Psyche < 0)
                Psyche = 0;
            if (Psyche > 10)
                Psyche = 10;
        }

        public void SetPsycheExtraText(string newPsycheExtraText)
        {
            PsycheExtraText = newPsycheExtraText;
        }

        public void AddSpeed(InGameStatus status, int howMuchToAdd = 1, string skillName = "")
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}+{howMuchToAdd} скорость\n");
            else if (howMuchToAdd < 0) status.AddInGamePersonalLogs($"{skillName}{howMuchToAdd} скорость\n");


            Speed += howMuchToAdd;
            if (Speed < 0)
                Speed = 0;
            if (Speed > 10)
                Speed = 10;
        }

        public int GetSpeed()
        {
            return Speed;
        }

        public string GetSpeedString()
        {
            return $"{Speed}{SpeedExtraText}";
        }

        public void SetSpeed(InGameStatus status, int howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} скорости\n");
            Speed = howMuchToSet;
            if (Speed < 0)
                Speed = 0;
            if (Speed > 10)
                Speed = 10;
        }

        public void SetSpeedExtraText(string newSpeedExtraText)
        {
            SpeedExtraText = newSpeedExtraText;
        }

        public void AddStrength(InGameStatus status, int howMuchToAdd = 1, string skillName = "")
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}+{howMuchToAdd} сила\n");
            else if (howMuchToAdd < 0) status.AddInGamePersonalLogs($"{skillName}{howMuchToAdd} сила\n");


            Strength += howMuchToAdd;
            if (Strength < 0)
                Strength = 0;
            if (Strength > 10)
                Strength = 10;
        }

        public int GetStrength()
        {
            return Strength;
        }

        public string GetStrengthString()
        {
            return $"{Strength}{StrengthExtraText}";
        }

        public void SetStrength(InGameStatus status, int howMuchToSet = 1, string skillName = "")
        {
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} силы\n");
            Strength = howMuchToSet;
            if (Strength < 0)
                Strength = 0;
            if (Strength > 10)
                Strength = 10;
        }

        public void SetStrengthExtraText(string newStrengthExtraText)
        {
            StrengthExtraText = newStrengthExtraText;
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
        public JusticeClass()
        {
            JusticeNow = 0;
            JusticeForNextRound = 0;
            IsWonThisRound = false;
        }

        private int JusticeNow { get; set; }
        private int JusticeForNextRound { get; set; }
        public bool IsWonThisRound { get; set; }

        public void AddJusticeNow(int howMuchToAdd = 1)
        {
            JusticeNow += howMuchToAdd;

            if (JusticeNow < 0)
                JusticeNow = 0;
            if (JusticeNow > 5)
                JusticeNow = 5;
        }

        public int GetJusticeNow()
        {
            return JusticeNow;
        }

        public void SetJusticeNow(InGameStatus status, int howMuchToSet = 1, string skillName = "", bool isLog = true)
        {
            if(isLog)
                status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} справедливости\n");
            JusticeNow = howMuchToSet;
        }

        public void AddJusticeForNextRound(int howMuchToAdd = 1)
        {
            JusticeForNextRound += howMuchToAdd;

            if (JusticeForNextRound < 0)
                JusticeForNextRound = 0;

            if (JusticeForNextRound > 10)
                JusticeForNextRound = 10;
        }

        public int GetJusticeForNextRound()
        {
            return JusticeForNextRound;
        }

        public void SetJusticeForNextRound(int newJusticeForNextRound)
        {
            JusticeForNextRound = newJusticeForNextRound;
        }
    }
}