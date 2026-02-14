using King_of_the_Garbage_Hill.Game.Classes;
using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
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


        public (bool isTooGoodMe, bool isTooGoodEnemy, bool isTooStronkMe, bool isTooStronkEnemy, bool isStatsBetterMe, bool isStatsBettterEnemy, int pointsWined, int isContrLost, decimal randomForPoint, decimal weighingMachine, decimal contrMultiplier, int skillMultiplierMe, int skillMultiplierTarget) CalculateStep1(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, bool isLog = false)
        {
            bool isTooGoodMe = false, isTooGoodEnemy = false, isTooStronkMe = false, isTooStronkEnemy = false, isStatsBetterMe = false, isStatsBettterEnemy = false;

            var pointsWined = 0;
            var isContrLost = 0;

            decimal randomForPoint = 50;
            decimal weighingMachine = 0;
            decimal contrMultiplier = 1;

            var skillMultiplierMe = 1;
            var skillMultiplierTarget = 1;

            var me = player.GameCharacter;
            var target = playerIamAttacking.GameCharacter;




            if (me.GetWhoIContre() == target.GetSkillClass())
            {
                contrMultiplier = (decimal)1.5;

                skillMultiplierMe = 2;
                isContrLost -= 1;
                weighingMachine += 2;
            }

            if (target.GetWhoIContre() == me.GetSkillClass())
            {
                skillMultiplierTarget = 2;
                isContrLost += 1;
                weighingMachine -= 2;
            }

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
                player.Status.AddFightingData($"**Skill: {me.GetSkill()}**");
                player.Status.AddFightingData($"**SkillEnemy: {target.GetSkill()}**");
                playerIamAttacking.Status.AddFightingData($"**Skill: {target.GetSkill()}**");
                playerIamAttacking.Status.AddFightingData($"**SkillEnemy: {me.GetSkill()}**");
            }



            var scaleMe = me.GetIntelligence() + me.GetStrength() + me.GetSpeed() + me.GetPsyche() + me.GetSkill() * skillMultiplierMe / 60;
            var scaleTarget = target.GetIntelligence() + target.GetStrength() + target.GetSpeed() + target.GetPsyche() + target.GetSkill() * skillMultiplierTarget / 60;
            weighingMachine += scaleMe - scaleTarget;

            if (isLog)
            {
                player.Status.AddFightingData($"scaleMe: {Math.Round(scaleMe, 2)}");
                player.Status.AddFightingData($"scaleEnemy: {Math.Round(scaleTarget, 2)}");
                playerIamAttacking.Status.AddFightingData($"scaleMe: {Math.Round(scaleTarget, 2)}");
                playerIamAttacking.Status.AddFightingData($"scaleEnemy: {Math.Round(scaleMe, 2)}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }


            switch (WhoIsBetter(player.GameCharacter, playerIamAttacking.GameCharacter))
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


            //tooGOOD
            var tooGoodDebug = weighingMachine;
            switch (weighingMachine)
            {
                case >= 13:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsTooGood: Me");
                        playerIamAttacking.Status.AddFightingData($"WhoIsTooGood: Enemy");
                    }

                    isTooGoodMe = true;
                    randomForPoint = 75;

                    break;
                case <= -13:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"WhoIsTooGood: Enemy");
                        playerIamAttacking.Status.AddFightingData($"WhoIsTooGood: Me");
                    }

                    isTooGoodEnemy = true;
                    randomForPoint = 25;
                    break;
            }

            if (isLog)
            {
                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");
                playerIamAttacking.Status.AddFightingData($"randomForPoint: {randomForPoint}");
            }


            //1.2 = 200 * 2 / 500 * 1.5
            var myWtf = me.GetSkill() * skillMultiplierMe / 600 * contrMultiplier;
            //0.1 = 50 * 1 / 500
            var targetWtf = target.GetSkill() * skillMultiplierTarget / 600;
            // 10 * (1 + (1.2-0.1)) - 10
            //var wtf = scaleMe * (1 + (myWtf - targetWtf)) - scaleMe;
            //29.64 * 1.846 - 24.9 * 1.19 - 29.64 + 24.9
            var wtf = scaleMe * (1 + myWtf) - scaleTarget * (1 + targetWtf) - scaleMe + scaleTarget;

            weighingMachine += wtf;

            if (isLog)
            {
                player.Status.AddFightingData($"wtfMe: {Math.Round(myWtf, 2)}");
                player.Status.AddFightingData($"wtfEnemy: {Math.Round(targetWtf, 2)}");
                playerIamAttacking.Status.AddFightingData($"wtfMe: {Math.Round(targetWtf, 2)}");
                playerIamAttacking.Status.AddFightingData($"wtfEnemy: {Math.Round(myWtf, 2)}");

                player.Status.AddFightingData($"wtf: {Math.Round(wtf, 2)}");
                playerIamAttacking.Status.AddFightingData($"wtf: {Math.Round(wtf, 2)}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }


            //tooSTONK
            switch (weighingMachine)
            {
                case >= 30:
                    if (isLog)
                    {
                        player.Status.AddFightingData($"**WhoIsTooSTONK: Me**");
                        playerIamAttacking.Status.AddFightingData($"**WhoIsTooSTONK: Enemy**");
                    }

                    isTooStronkMe = true;

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
                        playerIamAttacking.Status.AddInGamePersonalLogs($"DEBUG: You tooSTONK {(int)Math.Ceiling(wtf)} (vs {player.DiscordUsername}, {player.GameCharacter.Name}) \n");
                    }
                    isTooStronkEnemy = true;

                    tooStronkAdd = weighingMachine / 2;
                    if (tooStronkAdd < -20)
                    {
                        tooStronkAdd = -20;
                    }

                    randomForPoint += tooStronkAdd;
                    break;
            }


            weighingMachine += player.GameCharacter.Justice.GetRealJusticeNow() - playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow();

            if (isLog)
            {
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");
                player.Status.AddFightingData($"randomForPoint: {randomForPoint}");

                player.Status.AddFightingData($"JusticeMe: {player.GameCharacter.Justice.GetRealJusticeNow()}");
                player.Status.AddFightingData($"JusticeEnemy: {playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow()}");
                playerIamAttacking.Status.AddFightingData($"JusticeMe: {playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow()}");
                playerIamAttacking.Status.AddFightingData($"JusticeEnemy: {player.GameCharacter.Justice.GetRealJusticeNow()}");

                player.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
                playerIamAttacking.Status.AddFightingData($"weighingMachine: {Math.Round(weighingMachine, 2)}");
            }


            switch (weighingMachine)
            {
                case > 0:
                    pointsWined++;
                    isContrLost -= 1;
                    isStatsBetterMe = true;
                    break;
                case < 0:
                    pointsWined--;
                    isContrLost += 1;
                    isStatsBettterEnemy = true;
                    break;
            }

            if (isLog)
            {
                player.Status.AddFightingData($"pointsWined: {pointsWined}");
                playerIamAttacking.Status.AddFightingData($"pointsWined: {pointsWined}");
            }

            return (isTooGoodMe, isTooGoodEnemy, isTooStronkMe, isTooStronkEnemy, isStatsBetterMe, isStatsBettterEnemy, pointsWined, isContrLost, randomForPoint, weighingMachine, contrMultiplier, skillMultiplierMe, skillMultiplierTarget);
        }




        public int CalculateStep2(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, bool isLog = false)
        {
            var pointsWined = 0;
            if (player.GameCharacter.Justice.GetRealJusticeNow() > playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow())
                pointsWined++;
            if (player.GameCharacter.Justice.GetRealJusticeNow() < playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow())
                pointsWined--;

            if (isLog)
            {
                if (player.GameCharacter.Justice.GetRealJusticeNow() > playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow() ||
                    player.GameCharacter.Justice.GetRealJusticeNow() < playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow())
                {
                    player.Status.AddFightingData($"pointsWined (Justice): {pointsWined}");
                    playerIamAttacking.Status.AddFightingData($"pointsWined (Justice): {pointsWined}");
                }
            }

            return pointsWined;
        }

        public (int pointsWined, int randomNumber, decimal maxRandomNumber) CalculateStep3(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking, decimal randomForPoint, decimal contrMultiplier, bool isLog = false)
        {
            var pointsWined = 0;


            decimal maxRandomNumber = 100;
            if (player.GameCharacter.Justice.GetRealJusticeNow() > 1 || playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow() > 1)
            {
                var myJustice = player.GameCharacter.Justice.GetRealJusticeNow() * contrMultiplier;
                var targetJustice = playerIamAttacking.GameCharacter.Justice.GetRealJusticeNow();
                maxRandomNumber -= (myJustice - targetJustice) * 5;
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

            return (pointsWined, randomNumber, maxRandomNumber);
        }


    }
}
