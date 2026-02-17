using King_of_the_Garbage_Hill.Game.Classes;
using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    /// <summary>Result of CalculateStep1 — contains all intermediate values for web fight animation.</summary>
    public class Step1Result
    {
        // Existing fields (previously in tuple)
        public bool IsTooGoodMe, IsTooGoodEnemy, IsTooStronkMe, IsTooStronkEnemy;
        public bool IsStatsBetterMe, IsStatsBetterEnemy;
        public int PointsWon, IsContrLost;
        public decimal RandomForPoint, WeighingMachine, ContrMultiplier, SkillDifferenceRandomModifier, ContrMultiplierSkillDifference;
        public decimal SkillMultiplierMe, SkillMultiplierTarget;

        // Per-step weighing deltas (for web animation)
        public decimal ContrWeighingDelta;       // +2/-2/0 from contre
        public decimal ScaleWeighingDelta;       // scaleMe - scaleTarget
        public decimal WhoIsBetterWeighingDelta; // +5/-5
        public decimal PsycheWeighingDelta;      // mapped +1/+2/+4/-1/-2/-4 (NOT raw psyche diff)
        public decimal SkillWeighingDelta;       // skillDifference
        public decimal JusticeWeighingDelta;     // justiceMe - justiceTarget (in step1 weighing)

        // Random modifiers (for Round 3 display)
        public decimal TooGoodRandomChange;      // 75 or 25 (the set value), 0 if not triggered
        public decimal TooStronkRandomChange;    // the tooStronkAdd delta, 0 if not triggered

        // WhoIsBetter stat breakdown (+1 me better, -1 enemy better, 0 equal)
        public int WhoIsBetterIntel, WhoIsBetterStr, WhoIsBetterSpeed;

        // Intermediate values
        public decimal ScaleMe, ScaleTarget;
    }

    public class CalculateRounds : IServiceSingleton
    {
        private readonly SecureRandom _random;

        public CalculateRounds(SecureRandom random)
        {
            _random = random;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public int WhoIsBetter(CharacterClass me, CharacterClass target)
        {

            int intel = 0, speed = 0, str = 0;

            if (me.GetIntelligence() - target.GetIntelligence() > 0)
                intel = 1;
            if (me.GetIntelligence() - target.GetIntelligence() < 0)
                intel = -1;

            if (me.GetSpeed() - target.GetSpeed() > 0)
                speed = 1;
            if (me.GetSpeed() - target.GetSpeed() < 0)
                speed = -1;

            if (me.GetStrength() - target.GetStrength() > 0)
                str = 1;
            if (me.GetStrength() - target.GetStrength() < 0)
                str = -1;


            if (intel + speed + str >= 2)
                return 1;
            if (intel + speed + str <= -2)
                return 2;

            if (intel == 1 && str != -1)
                return 1;
            if (str == 1 && speed != -1)
                return 1;
            if (speed == 1 && intel != -1)
                return 1;

            return 2;
        }


        public Step1Result CalculateStep1(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, bool isLog = false)
        {
            var r = new Step1Result();

            var pointsWined = 0;
            var isContrLost = 0;

            decimal randomForPoint = 50;
            decimal weighingMachine = 0;
            decimal contrMultiplier = 1;

            var skillMultiplierMe = 0;
            var skillMultiplierTarget = 0;

            var me = player.FightCharacter;
            var target = playerIamAttacking.FightCharacter;


            // ── 1. Contre ──────────────────────────────────────────────
            var wmBefore = weighingMachine;

            if (me.GetWhoIContre() == target.GetSkillClass())
            {
                contrMultiplier = (decimal)1.5;

                skillMultiplierMe += 1;
                isContrLost -= 1;
                weighingMachine += 2;
            }

            if (target.GetWhoIContre() == me.GetSkillClass())
            {
                skillMultiplierTarget += 1;
                isContrLost += 1;
                weighingMachine -= 2;
            }

            r.ContrWeighingDelta = weighingMachine - wmBefore;

            if (weighingMachine != 0 && isLog)
            {
                player.Status.AddFightingData($"GetWhoIContre: {me.GetWhoIContre()}");
                player.Status.AddFightingData($"SkillClassEnemy: {target.GetSkillClass()}");
                playerIamAttacking.Status.AddFightingData($"GetWhoIContre: {target.GetWhoIContre()}");
                playerIamAttacking.Status.AddFightingData($"SkillClassEnemy: {me.GetSkillClass()}");

                player.Status.AddFightingData($"skillMultiplierMe: {skillMultiplierMe}");
                player.Status.AddFightingData($"skillMultiplierEnemy: {skillMultiplierTarget}");
                playerIamAttacking.Status.AddFightingData($"skillMultiplierMe: {skillMultiplierTarget}");
                playerIamAttacking.Status.AddFightingData($"skillMultiplierEnemy: {skillMultiplierMe}");

                player.Status.AddFightingData($"contrMultiplierMe: {contrMultiplier}");
                playerIamAttacking.Status.AddFightingData($"contrMultiplierEnemy: {contrMultiplier}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }

            if (isLog)
            {
                player.Status.AddFightingData($"**Skill: {me.GetSkill(skillMultiplierMe)}**");
                player.Status.AddFightingData($"**SkillEnemy: {target.GetSkill(skillMultiplierTarget)}**");
                playerIamAttacking.Status.AddFightingData($"**Skill: {target.GetSkill(skillMultiplierTarget)}**");
                playerIamAttacking.Status.AddFightingData($"**SkillEnemy: {me.GetSkill(skillMultiplierMe)}**");
            }


            // ── 2. Scale (stats + skill*multiplier) ────────────────────
            wmBefore = weighingMachine;

            var scaleMe = me.GetIntelligence() + me.GetStrength() + me.GetSpeed() + me.GetPsyche() + me.GetSkill(skillMultiplierMe)  / 60;
            var scaleTarget = target.GetIntelligence() + target.GetStrength() + target.GetSpeed() + target.GetPsyche() + target.GetSkill(skillMultiplierTarget) / 60;
            weighingMachine += scaleMe - scaleTarget;

            r.ScaleWeighingDelta = weighingMachine - wmBefore;
            r.ScaleMe = scaleMe;
            r.ScaleTarget = scaleTarget;

            if (isLog)
            {
                player.Status.AddFightingData($"scaleMe: {Math.Round(scaleMe, 2)}");
                player.Status.AddFightingData($"scaleEnemy: {Math.Round(scaleTarget, 2)}");
                playerIamAttacking.Status.AddFightingData($"scaleMe: {Math.Round(scaleTarget, 2)}");
                playerIamAttacking.Status.AddFightingData($"scaleEnemy: {Math.Round(scaleMe, 2)}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }


            // ── 3. WhoIsBetter ─────────────────────────────────────────
            wmBefore = weighingMachine;

            // Capture individual stat comparisons for display
            r.WhoIsBetterIntel = me.GetIntelligence() > target.GetIntelligence() ? 1 : me.GetIntelligence() < target.GetIntelligence() ? -1 : 0;
            r.WhoIsBetterStr = me.GetStrength() > target.GetStrength() ? 1 : me.GetStrength() < target.GetStrength() ? -1 : 0;
            r.WhoIsBetterSpeed = me.GetSpeed() > target.GetSpeed() ? 1 : me.GetSpeed() < target.GetSpeed() ? -1 : 0;

            switch (WhoIsBetter(me, target))
            {
                case 1:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsBetter: Me");
                        playerIamAttacking.Status.AddFightingData($"WhoIsBetter: Enemy");
                    }

                    weighingMachine += 5;
                    break;
                case 2:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsBetter: Enemy");
                        playerIamAttacking.Status.AddFightingData($"WhoIsBetter: me");
                    }

                    weighingMachine -= 5;
                    break;
            }

            r.WhoIsBetterWeighingDelta = weighingMachine - wmBefore;


            // ── 4. Psyche difference ───────────────────────────────────
            wmBefore = weighingMachine;

            var psycheDifference = me.GetPsyche() - target.GetPsyche();

            if (isLog)
            {
                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                player.Status.AddFightingData($"psycheDifference: {psycheDifference}");
                playerIamAttacking.Status.AddFightingData($"psycheDifference: {psycheDifference}");
            }

            switch (psycheDifference)
            {
                case > 0 and <= 3:
                    weighingMachine += 1;
                    break;
                case >= 4 and <= 5:
                    weighingMachine += 2;
                    break;
                case >= 6:
                    weighingMachine += 4;
                    break;
                case < 0 and >= -3:
                    weighingMachine -= 1;
                    break;
                case >= -5 and <= -4:
                    weighingMachine -= 2;
                    break;
                case <= -6:
                    weighingMachine -= 4;
                    break;
            }

            r.PsycheWeighingDelta = weighingMachine - wmBefore;


            // ── 5. TooGOOD (affects random, not weighing) ──────────────
            var tooGoodDebug = weighingMachine;
            var rfpBefore = randomForPoint;
            switch (weighingMachine)
            {
                case >= 13:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsTooGood: Me");
                        playerIamAttacking.Status.AddFightingData($"WhoIsTooGood: Enemy");
                    }

                    r.IsTooGoodMe = true;
                    randomForPoint = 75;

                    break;
                case <= -13:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsTooGood: Enemy");
                        playerIamAttacking.Status.AddFightingData($"WhoIsTooGood: Me");
                    }

                    r.IsTooGoodEnemy = true;
                    randomForPoint = 25;
                    break;
            }

            r.TooGoodRandomChange = randomForPoint - rfpBefore;

            if (isLog)
            {
                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");
                playerIamAttacking.Status.AddFightingData($"randomForPoint: {randomForPoint}");
            }


            // ── 6. Skill difference ────────────────────────────────────
            wmBefore = weighingMachine;

            //1.2 = 200 * 2 / 500 * 1.5
            var mySkill             = me.GetSkill(skillMultiplierMe) / 600 * contrMultiplier;
            var mySkillWithoutContr = me.GetSkill(skillMultiplierMe) / 600;
        
            //0.1 = 50 * 1 / 500
            var targetSkill = target.GetSkill(skillMultiplierTarget) / 600;
            // 10 * (1 + (1.2-0.1)) - 10
            //var wtf = scaleMe * (1 + (myWtf - targetWtf)) - scaleMe;
            //29.64 * 1.846 - 24.9 * 1.19 - 29.64 + 24.9
            var skillDifference = scaleMe * (1 + mySkill) - scaleTarget * (1 + targetSkill) - scaleMe + scaleTarget;
            var skillDifferenceWithoutContr = scaleMe * (1 + mySkillWithoutContr) - scaleTarget * (1 + targetSkill) - scaleMe + scaleTarget;

            //this skill difference used in Web animation
            var contrMultiplierSkillDifference = skillDifference - skillDifferenceWithoutContr;

            weighingMachine += skillDifference;

            r.SkillWeighingDelta = weighingMachine - wmBefore;

            if (isLog)
            {
                player.Status.AddFightingData($"skillMe: {Math.Round(mySkill, 2)}");
                player.Status.AddFightingData($"skillEnemy: {Math.Round(targetSkill, 2)}");
                playerIamAttacking.Status.AddFightingData($"skillMe: {Math.Round(targetSkill, 2)}");
                playerIamAttacking.Status.AddFightingData($"skillEnemy: {Math.Round(mySkill, 2)}");

                player.Status.AddFightingData($"skill: {Math.Round(skillDifference, 2)}");
                playerIamAttacking.Status.AddFightingData($"skill: {Math.Round(skillDifference, 2)}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }


            // ── 7. TooSTONK (affects random, not weighing) ─────────────
            rfpBefore = randomForPoint;
            switch (weighingMachine)
            {
                case >= 30:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"**WhoIsTooSTONK: Me**");
                        playerIamAttacking.Status.AddFightingData($"**WhoIsTooSTONK: Enemy**");
                    }

                    r.IsTooStronkMe = true;

                    var tooStronkAdd = weighingMachine / 2;
                    if (tooStronkAdd > 20)
                    {
                        tooStronkAdd = 20;
                    }

                    randomForPoint += tooStronkAdd;
                    break;

                case <= -30:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"**WhoIsTooSTONK: Enemy**");
                        playerIamAttacking.Status.AddFightingData($"**WhoIsTooSTONK: Me**");
                    }

                    if (playerIamAttacking.DiscordId == 238337696316129280)
                    {
                        playerIamAttacking.Status.AddInGamePersonalLogs($"DEBUG: You tooSTONK {(int)Math.Ceiling(skillDifference)} (vs {player.DiscordUsername}, {player.GameCharacter.Name}) \n");
                    }
                    r.IsTooStronkEnemy = true;

                    tooStronkAdd = weighingMachine / 2;
                    if (tooStronkAdd < -20)
                    {
                        tooStronkAdd = -20;
                    }

                    randomForPoint += tooStronkAdd;
                    break;
            }

            r.TooStronkRandomChange = randomForPoint - rfpBefore;


            // ── 8. Justice (in weighing machine) ───────────────────────
            wmBefore = weighingMachine;

            weighingMachine += me.Justice.GetRealJusticeNow() - target.Justice.GetRealJusticeNow();

            r.JusticeWeighingDelta = weighingMachine - wmBefore;

            if (isLog)
            {
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");

                player.Status.AddFightingData($"JusticeMe: {me.Justice.GetRealJusticeNow()}");
                player.Status.AddFightingData($"JusticeEnemy: {target.Justice.GetRealJusticeNow()}");
                playerIamAttacking.Status.AddFightingData($"JusticeMe: {target.Justice.GetRealJusticeNow()}");
                playerIamAttacking.Status.AddFightingData($"JusticeEnemy: {me.Justice.GetRealJusticeNow()}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }

            // ── 8. Random Modifier based on skill difference ───────────────────────
            var skillDifferenceRandomModifier = (me.GetSkill(skillMultiplierMe)  - target.GetSkill(skillMultiplierTarget)) / 60;

            if (skillDifferenceRandomModifier > 0)
            {
                randomForPoint += skillDifferenceRandomModifier;
            }
            else
            {
                randomForPoint -= skillDifferenceRandomModifier;
            }


            // ── Round 1 result ─────────────────────────────────────────
            switch (weighingMachine)
            {
                case > 0:
                    pointsWined++;
                    isContrLost -= 1;
                    r.IsStatsBetterMe = true;
                    break;
                case < 0:
                    pointsWined--;
                    isContrLost += 1;
                    r.IsStatsBetterEnemy = true;
                    break;
            }

            if (isLog)
            {
                player.Status.AddFightingData($"pointsWined: {pointsWined}");
                playerIamAttacking.Status.AddFightingData($"pointsWined: {pointsWined}");
            }

            // Populate result object
            r.PointsWon = pointsWined;
            r.IsContrLost = isContrLost;
            r.RandomForPoint = randomForPoint;
            r.WeighingMachine = weighingMachine;
            r.ContrMultiplier = contrMultiplier;
            r.SkillMultiplierMe = me.GetSkillFightMultiplier() + skillMultiplierMe;
            r.SkillMultiplierTarget = target.GetSkillFightMultiplier() + skillMultiplierTarget;
            r.SkillDifferenceRandomModifier = skillDifferenceRandomModifier;
            r.ContrMultiplierSkillDifference = contrMultiplierSkillDifference;

            return r;
        }




        public int CalculateStep2(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, bool isLog = false)
        {
            var me = player.FightCharacter;
            var target = playerIamAttacking.FightCharacter;
            var pointsWined = 0;
            if (me.Justice.GetRealJusticeNow() > target.Justice.GetRealJusticeNow())
                pointsWined++;
            if (me.Justice.GetRealJusticeNow() < target.Justice.GetRealJusticeNow())
                pointsWined--;

            if (isLog)
            {
                if (me.Justice.GetRealJusticeNow() > target.Justice.GetRealJusticeNow() ||
                    me.Justice.GetRealJusticeNow() < target.Justice.GetRealJusticeNow())
                {
                    player.Status.AddFightingData($"pointsWined (Justice): {pointsWined}");
                    playerIamAttacking.Status.AddFightingData($"pointsWined (Justice): {pointsWined}");
                }
            }

            return pointsWined;
        }

        public (int pointsWined, int randomNumber, decimal maxRandomNumber, decimal justiceRandomChange, decimal contrRandomChange) CalculateStep3(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, decimal randomForPoint, decimal contrMultiplier, bool isLog = false)
        {
            decimal justiceRandomChange = 0;
            decimal contrRandomChange = 0;
            var pointsWined = 0;
            var me = player.FightCharacter;
            var target = playerIamAttacking.FightCharacter;

            decimal maxRandomNumber = 100;
            if (me.Justice.GetRealJusticeNow() > 1 || target.Justice.GetRealJusticeNow() > 1)
            {
                var myJustice = me.Justice.GetRealJusticeNow() * contrMultiplier;
                var targetJustice = target.Justice.GetRealJusticeNow();
                maxRandomNumber -= (myJustice - targetJustice) * 5;
            }

            // decompose the maxRandom shift into pure-justice and contr parts
            var fightJusticeMe = me.Justice.GetRealJusticeNow();
            var fightJusticeTarget = target.Justice.GetRealJusticeNow();
            if (fightJusticeMe > 1 || fightJusticeTarget > 1)
            {
                justiceRandomChange = (fightJusticeMe - fightJusticeTarget) * 5;
                contrRandomChange = fightJusticeMe * (contrMultiplier - 1) * 5;
            }


            var randomNumber = _random.Random(1, (int)Math.Ceiling(maxRandomNumber));



            if (randomNumber <= randomForPoint)
            {
                pointsWined++;
            }
            else
            {
                pointsWined--;
            }

            if (isLog)
            {
                player.Status.AddFightingData($"maxRandomNumber: {maxRandomNumber}");
                playerIamAttacking.Status.AddFightingData($"maxRandomNumber: {maxRandomNumber}");

                player.Status.AddFightingData($"randomNumber: {randomNumber}");
                playerIamAttacking.Status.AddFightingData($"randomNumber: {randomNumber}");

                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");
                playerIamAttacking.Status.AddFightingData($"randomForPoint: {randomForPoint}");

                player.Status.AddFightingData($"pointsWined (Random): {pointsWined}");
                playerIamAttacking.Status.AddFightingData($"pointsWined (Random): {pointsWined}");
            }

            return (pointsWined, randomNumber, maxRandomNumber, justiceRandomChange, contrRandomChange);
        }


    }
}
