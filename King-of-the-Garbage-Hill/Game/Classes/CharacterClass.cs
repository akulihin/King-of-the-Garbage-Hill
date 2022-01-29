using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class CharacterClass
{
    public CharacterClass(int intelligence, int strength, int speed, int psyche, string name, string description, int tier, string avatar)
    {
        Intelligence = intelligence;
        Strength = strength;
        Speed = speed;
        Psyche = psyche;
        Justice = new JusticeClass();
        Name = name;
        Description = description;
        Tier = tier;
        SkillMultiplier = 0;
        SkillFightMultiplier = 1;
        ExtraWeight = 0;
        Avatar = avatar;
        AvatarCurrent = avatar;
    }

    public string Name { get; set; }

    public int ExtraWeight { get; set; }
    private int Intelligence { get; set; }
    private string IntelligenceExtraText { get; set; }
    private int Psyche { get; set; }
    private string PsycheExtraText { get; set; }
    private int Speed { get; set; }
    private string SpeedExtraText { get; set; }
    private int Strength { get; set; }
    private string StrengthExtraText { get; set; }
    private double SkillMain { get; set; }
    private double SkillExtra { get; set; }
    private int SkillMultiplier { get; set; }
    private int SkillFightMultiplier { get; set; }
    private string CurrentSkillTarget { get; set; } = "Ничего";
    private int Moral { get; set; }
    private int BonusPointsFromMoral { get; set; }

    public JusticeClass Justice { get; set; }

    public List<AvatarEventClass> AvatarEvent = new();

    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public List<Passive> Passive { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }
    



    public string GetClassStatDisplayText()
    {
        if (Intelligence == 0 && Strength == 0 && Speed == 0) return "***Братишка***... буль-буль...";
        if (Intelligence >= Strength && Intelligence >= Speed)
            return "***Умный*** нападает на тех, кто без *Справедливости*.";
        if (Strength >= Intelligence && Strength >= Speed) return "***Сильный*** побеждает!";
        if (Speed >= Intelligence && Speed >= Strength) return "***Быстрый*** успевает во все битвы...";
        return "***Братишка***... буль-буль...";
    }

     /*
     Intelligence => Speed
     Strength => Intelligence
     Speed => Strength
     */

     public string GetSkillClass()
    {
        if (Intelligence == 0 && Strength == 0 && Speed == 0) return "Буль";

        if (Intelligence >= Strength && Intelligence >= Speed) return "Интеллект";
        if (Strength >= Intelligence && Strength >= Speed) return "Сила";
        if (Speed >= Intelligence && Speed >= Strength) return "Скорость";

        return "Буль";
    }

    public string GetWhoIContre()
    {
        if (Intelligence == 0 && Strength == 0 && Speed == 0) return "Буль";

        if (Intelligence >= Strength && Intelligence >= Speed) return "Скорость";
        if (Strength >= Intelligence && Strength >= Speed) return "Интеллект";
        if (Speed >= Intelligence && Speed >= Strength) return "Сила";

        return "Буль";
    }


    public int GetBonusPointsFromMoral()
    {
        return BonusPointsFromMoral;
    }

    public void SetBonusPointsFromMoral(int newBonusPointsFromMoral)
    {
        BonusPointsFromMoral = newBonusPointsFromMoral;
    }

    public void AddBonusPointsFromMoral(int newBonusPointsFromMoral)
    {
        BonusPointsFromMoral += newBonusPointsFromMoral;
    }

    public string GetCurrentSkillClassTarget()
    {
        return CurrentSkillTarget;
    }

    public void RollSkillTargetForNextRound()
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
                var skillsSet = new List<string> { "Интеллект", "Скорость", "Сила" };
                var rand = new Random();
                CurrentSkillTarget = skillsSet[rand.Next(0, 2)];
                break;
        }
    }


    public void SetSkillMultiplier(int skillMultiplier = 0)
    {
        SkillMultiplier = skillMultiplier;
    }

    public void SetSkillFightMultiplier(int skillFightMultiplier = 1)
    {
        SkillFightMultiplier = skillFightMultiplier;
    }


    public double GetSkill()
    {
        return (SkillMain + SkillExtra) * SkillFightMultiplier;
    }

    public double GetSkillMainOnly()
    {
        return SkillMain;
    }

    public void SetMainSkill(InGameStatus status, double howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - SkillMain;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} *Скилла*\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} *Скилла*\n");
        }

        SkillMain = howMuchToSet;
    }

    public void AddMainSkill(InGameStatus status, string skillName, bool isLog = true)
    {
        var howMuchToAdd = SkillMain switch
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

        SkillMain += howMuchToAdd;
        SkillExtra += howMuchToAdd;

        var total = howMuchToAdd + howMuchToAdd;

        var multiplier = total * SkillMultiplier;
        total += multiplier;
        SkillExtra += multiplier;

        if (isLog)
            status.AddInGamePersonalLogs($" +{total} *Cкилла* (за {skillName} врага)\n");
    }

    public void AddExtraSkill(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (SkillMultiplier > 0 && howMuchToAdd > 0)
            howMuchToAdd *= SkillMultiplier + 1;
        if (isLog)
        {
            if (howMuchToAdd > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} *Cкилла*\n");
            else
                status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} *Cкилла*\n");
        }

        SkillExtra += howMuchToAdd;
    }

    public int GetMoral()
    {
        return Moral;
    }

    public void SetMoral(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - Moral;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} *Морали*\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} *Морали*\n");
        }

        Moral = howMuchToSet;
    }

    public void AddMoral(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true, bool isMoralPoints = false)
    {
        //привет со дна
        if (howMuchToAdd < 0 && status.CharacterName == "Осьминожка" && !isMoralPoints)
        {
            return;
        }
        //end привет со дна


        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} *Морали*\n");
        if (howMuchToAdd < 0 && Moral == 0)
            isLog = false;

        Moral += howMuchToAdd;

        if (Moral < 0)
            howMuchToAdd = Moral * -1 + howMuchToAdd;

        if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} *Морали*\n");

        if (Moral < 0) Moral = 0;
    }


    public void AddIntelligence(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Интеллект\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Интеллект\n");


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

    public void SetIntelligence(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - Intelligence;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Интеллект\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Интеллект\n");
        }

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

    public void AddPsyche(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Психика\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Психика\n");


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

    public void SetPsyche(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - Psyche;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Психика\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Психика\n");
        }

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

    public void AddSpeed(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Скорость\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Скорость\n");


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

    public void SetSpeed(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - Speed;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Скорость\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Скорость\n");
        }

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

    public void AddStrength(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Сила\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Сила\n");


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

    public void SetStrength(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - Strength;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Сила\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Сила\n");
        }

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
    public bool Visible;

    public Passive()
    {
        PassiveDescription = "";
        PassiveName = "";
        Visible = false;
    }

    public Passive(string passiveName, string passiveDescription, bool visible)
    {
        PassiveDescription = passiveDescription;
        PassiveName = passiveName;
        Visible = visible;
    }
}

public class AvatarEventClass
{
    public string EventName { get; set; }
    public string Url { get; set; }

    public AvatarEventClass(string eventName, string url)
    {
        EventName = eventName;
        Url = url;
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


    public void AddJusticeNow(InGameStatus status, int howMuchToAdd = 1)
    {   
        
        //Болевой порог
        if (status.CharacterName == "Краборак")
        {
            var rand = new Random();
            var max = howMuchToAdd;
            var extraPoints = 0;
            for (var i = 0; i < max; i++)
            {
                var result = rand.Next(0, 2);
                if (result != 0) continue;
                howMuchToAdd--;
                extraPoints++;
            }

            if(extraPoints > 0)
                status.AddRegularPoints(extraPoints, "Болевой порог");
            if(howMuchToAdd == 0)
                return;
        }
        //end Болевой порог

        var justricePhrases = new List<string>
        {
            "Ты сможешь!", "Еще немного!", "Верь в себя!", "Верь в мою веру в тебя!",
            "Не повeзло, но всё получится!",
            "Справедливость на нашей стороне!", "Мы им покажем!"
        };
        JusticeNow += howMuchToAdd;

        if (howMuchToAdd > 0)
            status.AddInGamePersonalLogs(
                $"*Справедливость*: ***+ {howMuchToAdd}!***<:e_:562879579694301184>{justricePhrases[new Random().Next(0, justricePhrases.Count)]}\n");

        if (JusticeNow < 0)
            JusticeNow = 0;
        if (JusticeNow > 5)
            JusticeNow = 5;
    }

    public int GetJusticeNow()
    {
        return JusticeNow;
    }

    public void SetJusticeNow(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} Справедливости\n");
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