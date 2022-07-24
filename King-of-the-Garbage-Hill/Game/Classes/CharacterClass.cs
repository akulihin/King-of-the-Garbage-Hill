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
    private double SkillMultiplier { get; set; }
    private int SkillFightMultiplier { get; set; }
    private string CurrentSkillTarget { get; set; } = "Ничего";
    private int Moral { get; set; }
    private int BonusPointsFromMoral { get; set; }
    public int LastMoralRound { get; set; } = 1;

    private int IntelligenceQualityResist { get; set; }
    private int StrengthQualityResist { get; set; }
    private int SpeedQualityResist { get; set; }

    private int PsycheQualityResist { get; set; }

    private int IntelligenceQualitySkillBonus { get; set; }
    private int StrengthQualityDropBonus { get; set; }
    private int SpeedQualityRangeBonus { get; set; }
    private int PsycheQualityMoralBonus { get; set; }
    private int IntelligenceQualitySkillDebuff { get; set; }
    private bool StrengthQualityDropDebuff { get; set; }
    private int PsycheQualityMoralDebuff { get; set; }


    public JusticeClass Justice { get; set; }

    public List<AvatarEventClass> AvatarEvent = new();

    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public List<Passive> Passive { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }


    private static string GetQualityResist(int resist)
    {
        var text = "";
        const string icon = "O";
        for (var i = 0; i < resist; i++)
        {
            text += $"{icon}";
        }
        return text;
    }

    public void LowerQualityResist(int howMuch = 1)
    {
        IntelligenceQualityResist -= howMuch;
        StrengthQualityResist -= howMuch;
        PsycheQualityResist -= howMuch;

        if (IntelligenceQualityResist < 0)
        {
            UpdateIntelligenceResist();
            IntelligenceQualitySkillDebuff++;
        }

        if (StrengthQualityResist < 0)
        {
            UpdateStrengthResist();
            StrengthQualityDropDebuff = true;
        }

        if (PsycheQualityResist < 0)
        {
            UpdatePsycheResist();
            PsycheQualityMoralDebuff++;
        }
    }


    public string GetIntelligenceQualityResist()
    {
        var text = GetQualityResist(IntelligenceQualityResist);
        if (IntelligenceQualitySkillDebuff > 0)
            text += $" **(-{IntelligenceQualitySkillDebuff*10}% Skill)**";
        return text;
    }

    public string GetStrengthQualityResist()
    {
        return GetQualityResist(StrengthQualityResist);
    }

    public string GetSpeedQualityResist()
    {
        return GetQualityResist(SpeedQualityResist);
    }

    public int GetSpeedQualityResistInt()
    {
        return SpeedQualityResist;
    }

    public string GetPsycheQualityResist()
    {
        var text = GetQualityResist(PsycheQualityResist);
        if (PsycheQualityMoralDebuff > 0)
            text += $" **(-{PsycheQualityMoralDebuff*10}% Moral)**";
        return text;
    }

    public void UpdateIntelligenceResist()
    {
        IntelligenceQualitySkillBonus = 0;
        if (Intelligence > 0)
            IntelligenceQualityResist = 1;
        if (Intelligence > 3)
            IntelligenceQualityResist = 2;
        if (Intelligence > 7)
            IntelligenceQualityResist = 3;
        if (Intelligence > 9)
            IntelligenceQualitySkillBonus = 1;
    }
    public void UpdateStrengthResist()
    {
        StrengthQualityDropBonus = 0;
        if (Strength > 0)
            StrengthQualityResist = 1;
        if (Strength > 3)
            StrengthQualityResist = 2;
        if (Strength > 7)
            StrengthQualityResist = 3;
        if (Strength > 9)
            StrengthQualityDropBonus = 1;
    }

    public void UpdateSpeedResist()
    {
        SpeedQualityRangeBonus = 0;
        if (Speed > 0)
            SpeedQualityResist = 1;
        if (Speed > 3)
            SpeedQualityResist = 2;
        if (Speed > 7)
            SpeedQualityResist = 3;
        if (Speed > 9)
            SpeedQualityRangeBonus = 1;
    }

    public void UpdatePsycheResist()
    {
        PsycheQualityMoralBonus = 0;
        if (Psyche > 0)
            PsycheQualityResist = 1;
        if (Psyche > 3)
            PsycheQualityResist = 2;
        if (Psyche > 7)
            PsycheQualityResist = 3;
        if (Psyche > 9)
            PsycheQualityMoralBonus = 1;
    }

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


    public void SetSkillMultiplier(double skillMultiplier = 0)
    {
        //2 это х3
        SkillMultiplier = skillMultiplier;
    }

    public void AddSkillMultiplier(double skillMultiplier = 0)
    {
        //2 это х3
        SkillMultiplier += skillMultiplier;
    }

    public void SetSkillFightMultiplier(int skillFightMultiplier = 1)
    {
        SkillFightMultiplier = skillFightMultiplier;
    }

    public int GetSkillFightMultiplier()
    {
        return SkillFightMultiplier;
    }


    public double GetSkill()
    {
        double skillDebuff = 1;
        for (var i = 0; i < IntelligenceQualitySkillDebuff; i++)
        {
            skillDebuff -= 0.1;
        }
        return (SkillMain + SkillExtra) * SkillFightMultiplier * skillDebuff;
    }

    public string GetSkillDisplay()
    {
        var skillText= $"{SkillMain + SkillExtra}";
        
        /*if (SkillFightMultiplier > 1)
        {
            skillText += $", **x{SkillFightMultiplier}**";
        }*/

        return skillText;
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
        if (status.CharacterName == "Братишка")
            return;
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

        var multiplier = (int) (total * SkillMultiplier);
        total += multiplier;
        SkillExtra += multiplier;

        if (isLog)
            status.AddInGamePersonalLogs($" +{total} *Cкилла* (за {skillName} врага)\n");
    }

    public void AddExtraSkill(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (status.CharacterName == "Братишка")
            return;
        if (SkillMultiplier > 0 && howMuchToAdd > 0)
            howMuchToAdd *= (int)(SkillMultiplier + 1);
        if (isLog)
        {
            status.AddInGamePersonalLogs(howMuchToAdd > 0
                ? $"{skillName}: +{howMuchToAdd} *Cкилла*\n"
                : $"{skillName}: {howMuchToAdd} *Cкилла*\n");
        }

        SkillExtra += howMuchToAdd;
    }

    public int GetMoral()
    {
        double moralDebuff = 1;
        for (var i = 0; i < PsycheQualityMoralDebuff; i++)
        {
            moralDebuff -= 0.1;
        }

        return (int) (Moral* moralDebuff);
    }

    public void SetMoral(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - GetMoral();
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} *Морали*\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} *Морали*\n");
        }

        LastMoralRound = status.RoundNumber;
        Moral = howMuchToSet;
    }

    public void AddMoral(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true, bool isMoralPoints = false)
    {
        if (status.CharacterName == "Братишка")
            return;
        //привет со дна
        if (howMuchToAdd < 0 && status.CharacterName == "Осьминожка" && !isMoralPoints)
        {
            return;
        }

        if (status.CharacterName == "Осьминожка")
        {
            howMuchToAdd = 4;   
        }
        //end привет со дна


        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} *Морали*\n");
        if (howMuchToAdd < 0 && GetMoral() == 0)
            isLog = false;

        LastMoralRound = status.RoundNumber;
        Moral += howMuchToAdd;

        if (GetMoral() < 0)
            howMuchToAdd = GetMoral() * -1 + howMuchToAdd;

        if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} *Морали*\n");

        if (GetMoral() < 0) Moral = 0;
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
        if (Name == "Dopa")
            return "200IQ";

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
        FullJusticeNow = 0;
        FightJusticeNow = 0;
        JusticeForNextRoundFromFights = 0;
        JusticeForNextRoundFromSkills = 0;
        IsWonThisRound = false;
    }

    private int FullJusticeNow { get; set; }
    private int FightJusticeNow { get; set; }
    private int JusticeForNextRoundFromFights { get; set; }
    private int JusticeForNextRoundFromSkills{ get; set; }
    public bool IsWonThisRound { get; set; }


    public void HandleEndOfRoundJustice(InGameStatus status)
    {
        if (IsWonThisRound)
        {
            FullJusticeNow = 0;
            FightJusticeNow = 0;
        }

        var howMuchToAdd = JusticeForNextRoundFromFights + JusticeForNextRoundFromSkills;

        FightJusticeNow += JusticeForNextRoundFromFights;
        JusticeForNextRoundFromFights = 0;
        JusticeForNextRoundFromSkills = 0;
        IsWonThisRound = false;

        var justricePhrases = new List<string>
        {
            "Ты сможешь!", "Еще немного!", "Верь в себя!", "Верь в мою веру в тебя!",
            "Не повeзло, но всё получится!",
            "Справедливость на нашей стороне!", "Мы им покажем!"
        };


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

            if (extraPoints > 0)
                status.AddRegularPoints(extraPoints, "Болевой порог");
            if (howMuchToAdd == 0)
                return;
        }
        //end Болевой порог



        FullJusticeNow += howMuchToAdd;
        if (FullJusticeNow < 0)
            FullJusticeNow = 0;
        if (FullJusticeNow > 5)
            FullJusticeNow = 5;

        if (howMuchToAdd > 0)
            status.AddInGamePersonalLogs(
                $"*Справедливость*: ***+ {howMuchToAdd}!***<:e_:562879579694301184>{justricePhrases[new Random().Next(0, justricePhrases.Count)]}\n");
    }


    public int GetFullJusticeNow()
    {
        return FullJusticeNow;
    }

    public int GetFightJusticeNow()
    {
        return FightJusticeNow;
    }


    public void AddFullJusticeNow(int howMuchToAdd = 1)
    {
        FullJusticeNow += howMuchToAdd;

        if (FullJusticeNow < 0)
            FullJusticeNow = 0;
        if (FullJusticeNow > 5)
            FullJusticeNow = 5;
    }

    public void SetFullJusticeNow(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
            status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} Справедливости\n");
        FullJusticeNow = howMuchToSet;
    }

    public void AddJusticeForNextRoundFromFight(int howMuchToAdd = 1)
    {
        JusticeForNextRoundFromFights += howMuchToAdd;
    }

    public void AddJusticeForNextRoundFromSkill(int howMuchToAdd = 1)
    {
        JusticeForNextRoundFromSkills += howMuchToAdd;
    }

}