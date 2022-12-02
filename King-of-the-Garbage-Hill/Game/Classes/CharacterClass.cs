using System;
using System.Collections.Generic;
using System.Linq;

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
        TargetSkillMultiplier = 0;
        ExtraSkillMultiplier = 0;
        SkillFightMultiplier = 1;
        Avatar = avatar;
        AvatarCurrent = avatar;
    }

    public CharacterClass DeepCopy()
    {
        var other = (CharacterClass)MemberwiseClone();

        //NO COPY
        //other.Moral = Moral;
        //other.BonusPointsFromMoral = BonusPointsFromMoral;
        //other.LastMoralRound = LastMoralRound;

        //LINKS, not a deep copy
        other.AvatarEvent = AvatarEvent;
        other.Status = Status;
        other.Justice = Justice;

        //Copy
        other.Passive = Passive.Select(x => x.DeepCopy()).ToList();
        //other.Justice = Justice.DeepCopy();
        other.Name = Name;
        other.Avatar = Avatar;
        other.AvatarCurrent = AvatarCurrent;
        other.Description = Description;
        other.Tier = Tier;
        other.WonTimes = WonTimes;
        other.WinStreak = WinStreak;
        other.Intelligence = Intelligence;
        other.IntelligenceExtraText = IntelligenceExtraText;
        other.Psyche = Psyche;
        other.PsycheExtraText = PsycheExtraText;
        other.Speed = Speed;
        other.SpeedExtraText = SpeedExtraText;
        other.Strength = Strength;
        other.StrengthExtraText = StrengthExtraText;
        other.SkillMain = SkillMain;
        other.SkillExtra = SkillExtra;
        other.TargetSkillMultiplier = TargetSkillMultiplier;
        other.ExtraSkillMultiplier = ExtraSkillMultiplier;
        other.SkillFightMultiplier = SkillFightMultiplier;
        other.CurrentSkillTarget = CurrentSkillTarget;
        other.IntelligenceQualityResist = IntelligenceQualityResist;
        other.StrengthQualityResist = StrengthQualityResist;
        other.SpeedQualityBonus = SpeedQualityBonus;
        other.PsycheQualityResist = PsycheQualityResist;
        other.IsIntelligenceQualitySkillBonus = IsIntelligenceQualitySkillBonus;
        other.IntelligenceQualitySkillBonus = IntelligenceQualitySkillBonus;
        other.StrengthQualityDropBonus = StrengthQualityDropBonus;
        other.StrengthQualityDropDebuff = StrengthQualityDropDebuff;
        other.IsSpeedQualityKiteBonus = IsSpeedQualityKiteBonus;
        other.SpeedQualityKiteBonus = SpeedQualityKiteBonus;
        other.PsycheQualityMoralBonus = PsycheQualityMoralBonus;
        other.PsycheQualityMoralDebuff = PsycheQualityMoralDebuff;

        return other;
    }

    // PUBLIC
    public string Name { get; set; }

    public JusticeClass Justice { get; set; }

    public List<AvatarEventClass> AvatarEvent = new();
    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public List<Passive> Passive { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }


    // PRIVATE
    private InGameStatus Status { get; set; }
    private int LastMoralRound { get; set; } = 1;
    private int WonTimes { get; set; }
    private int WinStreak { get; set; }

    private int Intelligence { get; set; }
    private string IntelligenceExtraText { get; set; }
    private int Psyche { get; set; }
    private string PsycheExtraText { get; set; }
    private int Speed { get; set; }
    private string SpeedExtraText { get; set; }
    private int Strength { get; set; }
    private string StrengthExtraText { get; set; }
    private decimal SkillMain { get; set; }
    private decimal SkillExtra { get; set; }
    private int TargetSkillMultiplier { get; set; }
    private int ExtraSkillMultiplier { get; set; }
    private int SkillFightMultiplier { get; set; }
    private string CurrentSkillTarget { get; set; } = "Ничего";
    private int Moral { get; set; }
    private int BonusPointsFromMoral { get; set; }

    private int IntelligenceQualityResist { get; set; }
    private int StrengthQualityResist { get; set; }
    private int SpeedQualityBonus { get; set; }
    private int PsycheQualityResist { get; set; }

    private bool IsIntelligenceQualitySkillBonus { get; set; }
    private int IntelligenceQualitySkillBonus { get; set; }
    private bool StrengthQualityDropBonus { get; set; }
    private int StrengthQualityDropDebuff { get; set; }
    private bool IsSpeedQualityKiteBonus { get; set; }
    private int SpeedQualityKiteBonus { get; set; }
    private bool PsycheQualityMoralBonus { get; set; }
    private int PsycheQualityMoralDebuff { get; set; }

    public void SetStatus(InGameStatus status)
    {
        Status = status;
        Justice.SetStatus(status);
    }

    public int GetLastMoralRound()
    {
        return LastMoralRound;
    }

    public void SetLastMoralRound(int howMuchToSet)
    {
        LastMoralRound = howMuchToSet;
    }

    public int GetWinStreak()
    {
        return WinStreak;
    }

    public void AddWinStreak(int howMuchToAdd = 1)
    {
        WinStreak += howMuchToAdd;
        WonTimes += howMuchToAdd;
    }
    public void SetWinStreak(int howMuchToSet = 0)
    {
        WinStreak = howMuchToSet;
    }

    public int GetWonTimes()
    {
        return WonTimes;
    }

    public void AddWonTimes(int howMuchToAdd = 1)
    {
        WonTimes += howMuchToAdd;
    }

    public void SetWonTimes(int howMuchToSet)
    {
        WonTimes = howMuchToSet;
    }

    public void HandleDrop(string discordUsername, GameClass game)
    {
        SetStrengthResist();
        if (Status.GetPlaceAtLeaderBoard() == 6) return;

        StrengthQualityDropDebuff = game.RoundNo;
        Status.AddBonusPoints(-1, "Quality");
        game.AddGlobalLogs($"Они скинули **{discordUsername}**! Сволочи!");
    }

    public void LowerQualityResist(string discordUsername, GameClass game, bool strengthBonus)
    {
        if (game.RoundNo == 1) return;
        
        var howMuch = 1;

        IntelligenceQualityResist -= howMuch;
        PsycheQualityResist -= howMuch;
        if(strengthBonus)
            StrengthQualityResist -= howMuch+1;
        else
            StrengthQualityResist -= howMuch;

        if (IntelligenceQualityResist < 0)
        {
            SetIntelligenceResist();

            //Испанец
            if (Status.CharacterName == "mylorik")
            {
                var mylorik = game.PlayersList.Find(x => x.GetPlayerId() == Status.PlayerId);
                mylorik!.GameCharacter.AddMoral(1, "Испанец", false);
                mylorik.Status.AddInGamePersonalLogs("Испанец: То, что мертво, умереть не может! +1 *Мораль*\n");
            }
            //end Испанец
            else
            {
                IntelligenceQualitySkillBonus--;
            }
            
        }

        if (StrengthQualityResist < 0)
        {
            SetStrengthResist();

            //Испанец
            if (Status.CharacterName == "mylorik") 
            {
                var mylorik = game.PlayersList.Find(x => x.GetPlayerId() == Status.PlayerId);
                mylorik!.GameCharacter.AddMoral( 1, "Испанец", false);
                mylorik.Status.AddInGamePersonalLogs("Испанец: То, что мертво, умереть не может! +1 *Мораль*\n");
            }
            //end Испанец
            else
            {
                HandleDrop(discordUsername, game);
            }
                
        }

        if (PsycheQualityResist < 0)
        {
            SetPsycheResist();

            //Испанец
            if (Status.CharacterName == "mylorik")
            {
                var mylorik = game.PlayersList.Find(x => x.GetPlayerId() == Status.PlayerId);
                mylorik!.GameCharacter.AddMoral( 1, "Испанец", false);
                mylorik.Status.AddInGamePersonalLogs("Испанец: То, что мертво, умереть не может! +1 *Мораль*\n");
            }
            //end Испанец
            else
            {
                PsycheQualityMoralDebuff++;
            }
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

        if (GetIntelligenceQualitySkillBonus() != (decimal)1.0)
        {
            var skillBonus = GetIntelligenceQualitySkillBonus();
            var skillBonusPrecent = (skillBonus - 1) * 100;
            var plus = "";
            if (skillBonusPrecent > 0)
                plus = "+";
            text += $" **({plus}{(int)skillBonusPrecent}% Skill)**";
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

    public void SetStrengthQualityResist(int howMuchToSet = 0)
    {
        StrengthQualityResist = howMuchToSet;
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

        var text = $"{spacing}<:Mobi:1000841939500925118> {GetSpeedQualityResistInt()}";
        if (GetSpeedQualityKiteBonus() > 0)
            text += $" **(+{GetSpeedQualityKiteBonus()} Kite Distance)**";
        return text;
    }

    public int GetSpeedQualityKiteBonus()
    {
        var toReturn = SpeedQualityKiteBonus;
        if (GetIsSpeedQualityKiteBonus())
            toReturn++;

        //Импакт
        if (Name == "LeCrisp")
            toReturn = 2;
        //end Импакт

        return toReturn;
    }

    public void AddSpeedQualityRangeBonus(int howMuchToAdd)
    {
        SpeedQualityKiteBonus += howMuchToAdd;
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
        //Импакт
        if (Name == "LeCrisp")
            SpeedQualityBonus = 6;
        //end Импакт

        return SpeedQualityBonus;
    }

    public void SetIntelligenceResist()
    {
        IntelligenceQualityResist = Intelligence switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => IntelligenceQualityResist
        };

        IsIntelligenceQualitySkillBonus = Intelligence > 9;
    }

    public void SetStrengthResist()
    {
        StrengthQualityResist = Strength switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => StrengthQualityResist
        };

        StrengthQualityDropBonus = Strength > 9;
    }

    public void SetSpeedResist()
    {
        SpeedQualityBonus = Speed switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 5,
            _ => GetSpeedQualityResistInt()
        };

        IsSpeedQualityKiteBonus = Speed > 9;
    }

    public void SetPsycheResist()
    {
        PsycheQualityResist = Psyche switch
        {
            >= 0 and <= 3 => 1,
            >= 4 and <= 7 => 2,
            >= 8 => 3,
            _ => PsycheQualityResist
        };

        PsycheQualityMoralBonus = Psyche > 9;
    }

    public void UpdateIntelligenceResist(int statOld, int statNew)
    {
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

        if(IntelligenceQualityResist < 0)
        {
            IntelligenceQualityResist = 0;
        }

        IsIntelligenceQualitySkillBonus = Intelligence > 9;
    }

    public void UpdateStrengthResist(int statOld, int statNew)
    {
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

        if (StrengthQualityResist < 0)
        {
            StrengthQualityResist = 0;
        }

        StrengthQualityDropBonus = Strength > 9;
    }

    public void UpdateSpeedResist(int statOld, int statNew)
    {
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
        SpeedQualityBonus += resistDiff;

        IsSpeedQualityKiteBonus = Speed > 9;
    }

    public void UpdatePsycheResist(int statOld, int statNew)
    {
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

        if (PsycheQualityResist < 0)
        {
            PsycheQualityResist = 0;
        }

        PsycheQualityMoralBonus = Psyche > 9;
    }

    public void AddIntelligenceQualitySkillBonus(int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd*10}% *Скилла*\n");
        else if (howMuchToAdd < 0 && isLog) Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd*10}% *Скилла*\n");

        IntelligenceQualitySkillBonus += howMuchToAdd;
    }

    public decimal GetIntelligenceQualitySkillBonus()
    {
        decimal toReturn = 1;

        var index = IntelligenceQualitySkillBonus;
        if (IsIntelligenceQualitySkillBonus) index++;

        switch (index)
        {
            case > 0:
            {
                for (var i = 0; i < index; i++)
                {
                    toReturn += (decimal) 0.1;
                }
                break;
            }
            case < 0:
            {
                index *= -1;
                for (var i = 0; i < index; i++)
                {
                    toReturn -= (decimal) 0.1;
                }
                break;
            }
        }

        return toReturn;
    }

    public bool GetStrengthQualityDropBonus()
    {
        return StrengthQualityDropBonus;
    }

    public bool GetIsSpeedQualityKiteBonus()
    {
        return IsSpeedQualityKiteBonus;
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

    public void SetTargetSkillMultiplier(int targetSkillMultiplier = 0)
    {
        //2 это х3
        TargetSkillMultiplier = targetSkillMultiplier;
    }

    public void SetExtraSkillMultiplier(int extraSkillMultiplier = 0)
    {
        //2 это х3
        ExtraSkillMultiplier = extraSkillMultiplier;
    }

    public void SetAnySkillMultiplier(int extraAnyMultiplier = 0)
    {
        //2 это х3
        SetTargetSkillMultiplier(extraAnyMultiplier);
        SetExtraSkillMultiplier(extraAnyMultiplier);
    }

    public int GetTargetSkillMultiplier()
    {
        return TargetSkillMultiplier;
    }

    public int GetExtraSkillMultiplier()
    {
        return ExtraSkillMultiplier;
    }

    public void SetSkillFightMultiplier(int skillFightMultiplier = 1)
    {
        SkillFightMultiplier = skillFightMultiplier;
    }

    public int GetSkillFightMultiplier()
    {
        return SkillFightMultiplier;
    }

    public decimal GetSkill()
    {
        return (SkillMain + SkillExtra) * SkillFightMultiplier * GetIntelligenceQualitySkillBonus();
    }

    public string GetSkillDisplay()
    {
        return $"{(int)GetSkill()}";
    }

    public decimal GetSkillMainOnly()
    {
        return SkillMain;
    }

    public void SetMainSkill(decimal howMuchToSet, string skillName, bool isLog = true)
    {
        if (isLog)
        {
            var diff = howMuchToSet - SkillMain;
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} *Скилла*\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} *Скилла*\n");
        }

        SkillMain = howMuchToSet;
    }

    public void AddMainSkill(string skillName, bool isLog = true)
    {
        if (Status.CharacterName == "Братишка")
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

        var multiplier = total * TargetSkillMultiplier;
        total += multiplier;
        SkillExtra += multiplier;

        if (isLog)
            Status.AddInGamePersonalLogs($"Мишень: +{total} *Cкилла* (за {skillName} врага)\n");
    }

    public void AddExtraSkill(int howMuchToAdd, string skillName, bool isLog = true)
    {
        var skillText = "Cкилла"; //russian "а"
        if (skillName != "Обмен Морали" && skillName != "Класс")
        {
            skillName = $"|>boole<|{skillName}";
            skillText = "Cкиллa"; // english "a"
        }

        if (Status.CharacterName == "Братишка")
            return;

        if (ExtraSkillMultiplier > 0 && howMuchToAdd > 0)
            howMuchToAdd *= ExtraSkillMultiplier + 1;
        
        if (isLog)
        {
            Status.AddInGamePersonalLogs(howMuchToAdd > 0
                ? $"{skillName}: +{howMuchToAdd} *{skillText}*\n"
                : $"{skillName}: {howMuchToAdd} *{skillText}*\n");
        }

        SkillExtra += howMuchToAdd;
    }

    public int GetMoral()
    {
        decimal moralDebuff = 1;
        for (var i = 0; i < PsycheQualityMoralDebuff; i++)
        {
            moralDebuff -= (decimal)0.1;
        }
        if (GetPsycheQualityMoralBonus())
            moralDebuff += (decimal)0.1;

        return (int) (Moral* moralDebuff);
    }

    public void SetMoral(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - GetMoral();
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} *Морали*\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} *Морали*\n");
        }

        LastMoralRound = Status.RoundNumber;
        Moral = howMuchToSet;
    }

    public void AddMoral(int howMuchToAdd, string skillName, bool isLog = true, bool isMoralPoints = false)
    {
        if (skillName != "Обмен Морали" && skillName != "Победа" && skillName != "Поражение")
        {
            skillName = $"|>boole<|{skillName}";
        }

        if (Status.CharacterName == "Братишка")
            return;

        //привет со дна
        if (howMuchToAdd < 0 && Status.CharacterName == "Осьминожка" && !isMoralPoints)
        {
            return;
        }

        if (Status.CharacterName == "Осьминожка")
        {
            howMuchToAdd = 4;   
        }
        //end привет со дна



        if (howMuchToAdd >= 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} *Морали*\n");
        if (howMuchToAdd < 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} *Морали*\n");

        LastMoralRound = Status.RoundNumber;
        Moral += howMuchToAdd;
    }

    public void NormalizeMoral()
    {
        if (Moral < 0)
            Moral = 0;
    }
    
    public void AddIntelligence(int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Интеллект\n");
        else if (howMuchToAdd < 0 && isLog) Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Интеллект\n");

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

    public void SetIntelligence(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Intelligence;
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} Интеллект\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} Интеллект\n");
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

    public void SetIntelligenceForOneFight(int howMuchToSet, string skillName)
    {
        //Set Stat only for one fight, not for the whole round!
        //Only used with "GameCharacter" because this overwrites "FightCharacter" mechanics

        if (howMuchToSet < 0)
            return;
        Status.RealIntelligence = GetIntelligence();
        Status.TempIntelligence = howMuchToSet;
        SetIntelligence(howMuchToSet, skillName, false);
    }

    public void SetIntelligenceExtraText(string newIntelligenceExtraText)
    {
        IntelligenceExtraText = newIntelligenceExtraText;
    }

    public void AddPsyche(int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Психика\n");
        else if (howMuchToAdd < 0 && isLog) Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Психика\n");


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

    public void SetPsyche(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Psyche;
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} Психика\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} Психика\n");
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

    public void SetPsycheForOneFight(int howMuchToSet, string skillName)
    {
        //Set Stat only for one fight, not for the whole round!
        //Only used with "GameCharacter" because this overwrites "FightCharacter" mechanics

        if (howMuchToSet < 0)
            return;
        Status.RealPsyche = GetPsyche();
        Status.TempPsyche = howMuchToSet;
        SetPsyche(howMuchToSet, skillName, false);
    }

    public void SetPsycheExtraText(string newPsycheExtraText)
    {
        PsycheExtraText = newPsycheExtraText;
    }

    public void AddSpeed(int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Скорость\n");
        else if (howMuchToAdd < 0 && isLog) Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Скорость\n");

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

    public void SetSpeed(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Speed;
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} Скорость\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} Скорость\n");
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

    public void SetSpeedForOneFight(int howMuchToSet, string skillName)
    {
        //Set Stat only for one fight, not for the whole round!
        //Only used with "GameCharacter" because this overwrites "FightCharacter" mechanics

        if (howMuchToSet < 0)
            return;
        Status.RealSpeed = GetSpeed();
        Status.TempSpeed = howMuchToSet;
        SetSpeed(howMuchToSet, skillName, false);
    }

    public void SetSpeedExtraText(string newSpeedExtraText)
    {
        SpeedExtraText = newSpeedExtraText;
    }

    public void AddStrength(int howMuchToAdd, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (howMuchToAdd > 0 && isLog)
            Status.AddInGamePersonalLogs($"{skillName}: +{howMuchToAdd} Сила\n");
        else if (howMuchToAdd < 0 && isLog) Status.AddInGamePersonalLogs($"{skillName}: {howMuchToAdd} Сила\n");

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

    public void SetStrength(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
        {
            var diff = howMuchToSet - Strength;
            if (diff > 0)
                Status.AddInGamePersonalLogs($"{skillName}: +{diff} Сила\n");
            else if (diff < 0) Status.AddInGamePersonalLogs($"{skillName}: {diff} Сила\n");
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

    public void SetStrengthForOneFight(int howMuchToSet, string skillName)
    {
        //Set Stat only for one fight, not for the whole round!
        //Only used with "GameCharacter" because this overwrites "FightCharacter" mechanics

        if (howMuchToSet < 0)
            return;
        Status.RealStrength = GetStrength();
        Status.TempStrength = howMuchToSet;
        SetStrength(howMuchToSet, skillName, false);
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

    public Passive DeepCopy()
    {
        var other = (Passive)MemberwiseClone();

        other.PassiveDescription = PassiveDescription;
        other.PassiveName = PassiveName;
        other.Visible = Visible;

        return other;
    }

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
    public JusticeClass DeepCopy()
    {
        var other = (JusticeClass)MemberwiseClone();

        //LINKS, not a deep copy
        other.Status = Status;

        //Copy
        other.RealJusticeNow = RealJusticeNow;
        other.SeenJusticeNow = SeenJusticeNow;
        other.JusticeForNextRoundFromFights = JusticeForNextRoundFromFights;
        other.JusticeForNextRoundFromSkills = JusticeForNextRoundFromSkills;
        other.IsWonThisRound = IsWonThisRound;

        return other;
    }

    public JusticeClass()
    {
        RealJusticeNow = 0;
        SeenJusticeNow = 0;
        JusticeForNextRoundFromFights = 0;
        JusticeForNextRoundFromSkills = 0;
        IsWonThisRound = false;
    }

    private int RealJusticeNow { get; set; }
    private int SeenJusticeNow { get; set; }
    private int JusticeForNextRoundFromFights { get; set; }
    private int JusticeForNextRoundFromSkills{ get; set; }
    public bool IsWonThisRound { get; set; }
    private InGameStatus Status { get; set; }


    public void SetStatus(InGameStatus status)
    {
        Status = status;
    }


    public void HandleEndOfRoundJustice()
    {
        if (IsWonThisRound)
        {
            RealJusticeNow = 0;
            SeenJusticeNow = 0;
        }

        var howMuchToAdd = JusticeForNextRoundFromFights + JusticeForNextRoundFromSkills;

        SeenJusticeNow += JusticeForNextRoundFromFights;
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
        if (Status.CharacterName == "Краборак")
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
                Status.AddRegularPoints(extraPoints, "Болевой порог");
            if (howMuchToAdd == 0)
                return;
        }
        //end Болевой порог



        RealJusticeNow += howMuchToAdd;
        if (RealJusticeNow < 0)
            RealJusticeNow = 0;
        if (RealJusticeNow > 5)
            RealJusticeNow = 5;

        if (howMuchToAdd > 0)
            Status.AddInGamePersonalLogs(
                $"*Справедливость*: ***+ {howMuchToAdd}!***<:e_:562879579694301184>{justricePhrases[new Random().Next(0, justricePhrases.Count)]}\n");
    }


    public int GetRealJusticeNow()
    {
        return RealJusticeNow;
    }

    public int GetSeenJusticeNow()
    {
        return SeenJusticeNow;
    }


    public void AddRealJusticeNow(int howMuchToAdd = 1)
    {
        RealJusticeNow += howMuchToAdd;

        if (RealJusticeNow < 0)
            RealJusticeNow = 0;
        if (RealJusticeNow > 5)
            RealJusticeNow = 5;
    }

    public void SetRealJusticeNow(int howMuchToSet, string skillName, bool isLog = true)
    {
        if (skillName != "Прокачка" && skillName != "Читы")
        {
            skillName = $"|>boole<|{skillName}";
        }
        if (isLog)
            Status.AddInGamePersonalLogs($"{skillName}={howMuchToSet} Справедливости\n");
        RealJusticeNow = howMuchToSet;
    }

    public void SetJusticeForOneFight(int howMuchToSet, string skillName)
    {
        //Set Stat only for one fight, not for the whole round!
        //Only used with "GameCharacter" because this overwrites "FightCharacter" mechanics

        if (howMuchToSet < 0)
            return;
        Status.RealJustice = GetRealJusticeNow();
        Status.TempJustice = howMuchToSet;
        SetRealJusticeNow(howMuchToSet, skillName, false);
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