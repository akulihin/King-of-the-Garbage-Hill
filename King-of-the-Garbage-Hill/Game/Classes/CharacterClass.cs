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

    private bool IntelligenceQualitySkillBonus { get; set; }
    private bool StrengthQualityDropBonus { get; set; }
    private bool SpeedQualityRangeBonus { get; set; }
    private bool PsycheQualityMoralBonus { get; set; }
    private int IntelligenceQualitySkillDebuff { get; set; }
    private int StrengthQualityDropDebuff { get; set; }
    private int PsycheQualityMoralDebuff { get; set; }


    public JusticeClass Justice { get; set; }

    public List<AvatarEventClass> AvatarEvent = new();

    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public List<Passive> Passive { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }


    public void LowerQualityResist(string discordUsername, GameClass game, InGameStatus status, int howMuch, bool strengthBonus)
    {
        if (game.RoundNo == 1) return;

        IntelligenceQualityResist -= howMuch;
        PsycheQualityResist -= howMuch;
        if(strengthBonus)
            StrengthQualityResist -= howMuch+1;
        else
            StrengthQualityResist -= howMuch;

        if (IntelligenceQualityResist < 0)
        {
            SetIntelligenceResist();
            IntelligenceQualitySkillDebuff++;
            //status.AddInGamePersonalLogs($"Quality: -10% *Скиллa*\n"); //english "a" so it wouldn't combine with all other "skill" logs
        }

        if (StrengthQualityResist < 0)
        {
            SetStrengthResist();
            if (status.PlaceAtLeaderBoard != 6)
            {
                StrengthQualityDropDebuff = game.RoundNo;
                status.AddBonusPoints(-1, "Quality");
                game.AddGlobalLogs($"Они скинули **{discordUsername}**! Сволочи!");
            }
        }

        if (PsycheQualityResist < 0)
        {
            SetPsycheResist();
            PsycheQualityMoralDebuff++;
            //status.AddInGamePersonalLogs($"Quality: -10% *Морaли*\n"); //english "a" so it wouldn't combine with all other "skill" logs
        }
    }


    public string GetIntelligenceQualityResist()
    {
        var spacing = " ";
        var s = "  ";
        for (var i = 0; i < 14; i++)
        {
            spacing += s;
        }
        if (Intelligence < 10 && (Strength == 10 || Psyche == 10 || Speed == 10))
            spacing += $"{s}{s}";
        var text = $"{spacing}<:Anal:1000841467935338518> {IntelligenceQualityResist}";
        var debuffTemp = IntelligenceQualitySkillDebuff;
        if (IntelligenceQualitySkillBonus)
            debuffTemp -= 1;
        switch (debuffTemp)
        {
            case > 0:
                text += $" **(-{debuffTemp * 10}% Skill)**";
                break;
            case < 0:
                text += $" **(+{debuffTemp * 10 * -1}% Skill)**";
                break;
        }
        return text;
    }

    public string GetStrengthQualityResist()
    {
        var spacing = " ";
        var s = " ";
        for (var i = 0; i < 21; i++)
        {
            spacing += s;
        }
        if (Strength < 10 && (Speed == 10 || Intelligence == 10 || Psyche == 10))
            spacing += $"{s}{s}";
        var text = $"{spacing}<:Usto:1000845686872473611> {StrengthQualityResist}";
        if (StrengthQualityDropBonus)
            text += " **(+1 Drop Power)**";
        return text;
    }

    public string GetSpeedQualityResist()
    {
        var spacing = "  ";
        var s = " ";
        for (var i = 0; i < 12; i++)
        {
            spacing += s;
        }
        if(Speed < 10 && (Strength == 10 || Intelligence == 10 || Psyche == 10))
            spacing += $"{s}{s}";

        var text = $"{spacing}<:Mobi:1000841939500925118> {SpeedQualityResist}";;
        if (SpeedQualityRangeBonus)
            text += " **(+1 Kite Distance)**";
        return text;
    }

    public string GetPsycheQualityResist()
    {
        var spacing = " ";
        var s = " ";
        for (var i = 0; i < 14; i++)
        {
            spacing += s;
        }
        if (Psyche < 10 && (Strength == 10 || Intelligence == 10 || Speed == 10))
            spacing += $"{s}{s}";
        var text = $"{spacing}<:Spok:1000842206145413210> {PsycheQualityResist}";

        var debuffTemp = PsycheQualityMoralDebuff;
        if (PsycheQualityMoralBonus)
            debuffTemp -= 1;
        switch (debuffTemp)
        {
            case > 0:
                text += $" **(-{debuffTemp * 10}% Moral)**";
                break;
            case < 0:
                text += $" **(+{debuffTemp * 10 * -1}% Moral)**";
                break;
        }

        return text;
    }

    public int GetSpeedQualityResistInt()
    {
        return SpeedQualityResist;
    }

    public void SetIntelligenceResist()
    {
        IntelligenceQualitySkillBonus = false;

        IntelligenceQualityResist = Intelligence switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => IntelligenceQualityResist
        };

        if (Intelligence > 9)
            IntelligenceQualitySkillBonus = true;
    }
    public void SetStrengthResist()
    {
        StrengthQualityDropBonus = false;

        StrengthQualityResist = Strength switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => StrengthQualityResist
        };

        if (Strength > 9)
            StrengthQualityDropBonus = true;
    }

    public void SetSpeedResist()
    {
        SpeedQualityRangeBonus = false;

        SpeedQualityResist = Speed switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 5,
            _ => SpeedQualityResist
        };

        if (Speed > 9)
            SpeedQualityRangeBonus = true;
    }

    public void SetPsycheResist()
    {
        PsycheQualityMoralBonus = false;

        PsycheQualityResist = Psyche switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => PsycheQualityResist
        };

        if (Psyche > 9)
            PsycheQualityMoralBonus = true;
    }

    public void UpdateIntelligenceResist(int statOld, int statNew)
    {
        IntelligenceQualitySkillBonus = false;

        var resistOld = 0;
        var resistNew = 0;

        resistOld = statOld switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistOld
        };

        resistNew = statNew switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistNew
        };

        var resistDiff = resistNew - resistOld;
        IntelligenceQualityResist += resistDiff;

        if (statNew > 9)
            IntelligenceQualitySkillBonus = true;
    }

    public void UpdateStrengthResist(int statOld, int statNew)
    {
        StrengthQualityDropBonus = false;

        var resistOld = 0;
        var resistNew = 0;

        resistOld = statOld switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistOld
        };

        resistNew = statNew switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistNew
        };

        var resistDiff = resistNew - resistOld;
        StrengthQualityResist += resistDiff;

        if (statNew > 9)
            StrengthQualityDropBonus = true;
    }

    public void UpdateSpeedResist(int statOld, int statNew)
    {
        SpeedQualityRangeBonus = false;

        var resistOld = 0;
        var resistNew = 0;

        resistOld = statOld switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 5,
            _ => resistOld
        };

        resistNew = statNew switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 5,
            _ => resistNew
        };

        var resistDiff = resistNew - resistOld;
        SpeedQualityResist += resistDiff;

        if (statNew > 9)
            SpeedQualityRangeBonus = true;
    }

    public void UpdatePsycheResist(int statOld, int statNew)
    {
        PsycheQualityMoralBonus = false;

        var resistOld = 0;
        var resistNew = 0;

        resistOld = statOld switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistOld
        };

        resistNew = statNew switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => resistNew
        };

        var resistDiff = resistNew - resistOld;
        PsycheQualityResist += resistDiff;

        if (statNew > 9)
            PsycheQualityMoralBonus = true;
    }

    public bool GetIntelligenceQualitySkillBonus()
    {
        return IntelligenceQualitySkillBonus;
    }
    public bool GetStrengthQualityDropBonus()
    {
        return StrengthQualityDropBonus;
    }
    public bool GetSpeedQualityRangeBonus()
    {
        return SpeedQualityRangeBonus;
    }
    public bool GetPsycheQualityMoralBonus()
    {
        return PsycheQualityMoralBonus;
    }

    public int GetStrengthQualityDropDebuff()
    {
        return StrengthQualityDropDebuff;
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
        if(GetIntelligenceQualitySkillBonus())
            skillDebuff += 0.1;
        return (SkillMain + SkillExtra) * SkillFightMultiplier * skillDebuff;
    }

    public string GetSkillDisplay()
    {
        return $"{(int)GetSkill()}";
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
            status.AddInGamePersonalLogs($"Класс: +{total} *Cкилла* (за {skillName} врага)\n");
    }

    public void AddExtraSkill(InGameStatus status, int howMuchToAdd, string skillName, bool isLog = true)
    {
        var skillText = "Cкилла"; //russian "а"
        if (skillName != "Обмен Морали" && skillName != "Класс")
        {
            skillName = $"|>boole<|{skillName}";
            skillText = "Cкиллa"; // english "a"
        }

        if (status.CharacterName == "Братишка")
            return;

        if (SkillMultiplier > 0 && howMuchToAdd > 0)
            howMuchToAdd *= (int)(SkillMultiplier + 1);
        if (isLog)
        {
            status.AddInGamePersonalLogs(howMuchToAdd > 0
                ? $"{skillName}: +{howMuchToAdd} *{skillText}*\n"
                : $"{skillName}: {howMuchToAdd} *{skillText}*\n");
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
        if (GetPsycheQualityMoralBonus())
            moralDebuff += 0.1;

        return (int) (Moral* moralDebuff);
    }


    public void SetMoral(InGameStatus status, int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
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
        if (skillName != "Обмен Морали" && skillName != "Победа" && skillName != "Поражение")
        {
            skillName = $"|>boole<|{skillName}";
        }
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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Интеллект\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Интеллект\n");

        var intelligenceOld = Intelligence;
        Intelligence += howMuchToAdd;
        var intelligenceNew = Intelligence;

        UpdateIntelligenceResist(intelligenceOld, intelligenceNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Intelligence;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Интеллект\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Интеллект\n");
        }

        var intelligenceOld = Intelligence;
        Intelligence = howMuchToSet;
        var intelligenceNew = Intelligence;

        UpdateIntelligenceResist(intelligenceOld, intelligenceNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Психика\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Психика\n");


        var psycheOld = Psyche;
        Psyche += howMuchToAdd;
        var psycheNew = Psyche;

        UpdatePsycheResist(psycheOld, psycheNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Psyche;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Психика\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Психика\n");
        }

        var psycheOld = Psyche;
        Psyche = howMuchToSet;
        var psycheNew = Psyche;

        UpdatePsycheResist(psycheOld, psycheNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Скорость\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Скорость\n");

        var speedOld = Speed;
        Speed += howMuchToAdd;
        var speedNew = Speed;

        UpdateSpeedResist(speedOld, speedNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Speed;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Скорость\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Скорость\n");
        }

        var speedOld = Speed;
        Speed = howMuchToSet;
        var speedNew = Speed;

        UpdateSpeedResist(speedOld, speedNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Сила\n");
        else if (howMuchToAdd < 0 && isLog) status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Сила\n");

        var strengthOld = Strength;
        Strength += howMuchToAdd;
        var strengthNew = Strength;

        UpdateStrengthResist(strengthOld, strengthNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Strength;
            if (diff > 0)
                status.AddInGamePersonalLogs($"{skillName}: +{diff} Сила\n");
            else if (diff < 0) status.AddInGamePersonalLogs($"{skillName}: {diff} Сила\n");
        }

        var strengthOld = Strength;
        Strength = howMuchToSet;
        var strengthNew = Strength;

        UpdateStrengthResist(strengthOld, strengthNew);

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
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
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