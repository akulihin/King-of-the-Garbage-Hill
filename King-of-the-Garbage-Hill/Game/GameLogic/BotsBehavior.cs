using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class BotsBehavior : IServiceSingleton
{
    
    private readonly GameReaction _gameReaction;
    private readonly Global _global;
    private readonly LoginFromConsole _logs;
    private readonly SecureRandom _rand;

    public BotsBehavior(SecureRandom rand, GameReaction gameReaction, Global global, LoginFromConsole logs)
    {
        _rand = rand;
        _gameReaction = gameReaction;
        _global = global;
        _logs = logs;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task HandleBotBehavior(GamePlayerBridgeClass player, GameClass game)
    {
        HandleBotMoral(player, game);
        if (player.Status.MoveListPage == 3) 
            await HandleLvlUpBot(player, game);
        if (player.Status.MoveListPage == 1) 
            await HandleBotAttack(player, game);
    }

    public void HandleBotMoralForSkill(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            if (bot.Character.Name == "Sirinoks")
            {
                if (game.RoundNo == 9)
                {
                    overwrite = true;
                }
            }

            if (bot.Character.Name == "DeepList")
            {
                var deepList = bot.Character.Passives.DeepListMadnessTriggeredWhen;
                if (deepList != null)
                    if (deepList.WhenToTrigger.Contains(game.RoundNo))
                    {
                        overwrite = true;
                    }
            }

            //если хардкитти или осьминожка  или Вампур - всегда ждет 20 морали
            if (bot.Character.Name is "HardKitty" or "Осьминожка" or "Вампур")
                if (bot.Character.GetMoral() < 20)
                    return;

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.Character.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.PlaceAtLeaderBoard == 6 && bot.Character.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.PlaceAtLeaderBoard == 5 && bot.Character.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.PlaceAtLeaderBoard == 4 && bot.Character.GetMoral() < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.PlaceAtLeaderBoard == 3 && bot.Character.GetMoral() < 5 && !overwrite)
                return;
            //если бот на 2м месте то ждет 3
            if (bot.Status.PlaceAtLeaderBoard == 2 && bot.Character.GetMoral() < 3 && !overwrite)
                return;
        }
        //end логика до 10го раунда

        //прожать всю момаль
        if (bot.Character.GetMoral() >= 20)
        {
            bot.Character.AddMoral(bot.Status, -20, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 100, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 13)
        {
            bot.Character.AddMoral(bot.Status, -13, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 50, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 8)
        {
            bot.Character.AddMoral(bot.Status, -8, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 30, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 5)
        {
            bot.Character.AddMoral(bot.Status, -5, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 18, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 3)
        {
            bot.Character.AddMoral(bot.Status, -3, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 10, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 2)
        {
            bot.Character.AddMoral(bot.Status, -2, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 6, "Обмен Морали");
        }

        if (bot.Character.GetMoral() >= 1)
        {
            bot.Character.AddMoral(bot.Status, -1, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status, 2, "Обмен Морали");
        }
        //end прожать всю момаль
    }

    public void HandleBotMoralForPoints(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            if (bot.Character.Name == "Осьминожка")
            {
                return;
            }

            //если хардкитти  или Вампур - всегда ждет 20 морали
            if (bot.Character.Name is "HardKitty")
                if (bot.Character.GetMoral() < 20)
                    return;

            if (bot.Character.Name is "Вампур")
            {
                if (bot.Status.PlaceAtLeaderBoard >= 5)
                    return;
                if (bot.Status.PlaceAtLeaderBoard <= 2)
                {
                    if (bot.Character.GetMoral() < 13)
                        return;
                }
                else
                {
                    if (bot.Character.GetMoral() < 20)
                        return;
                }
            }
            

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.Character.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.PlaceAtLeaderBoard == 6 && bot.Character.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.PlaceAtLeaderBoard == 5 && bot.Character.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.PlaceAtLeaderBoard == 4 && bot.Character.GetMoral() < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.PlaceAtLeaderBoard == 3 && bot.Character.GetMoral() < 5 && !overwrite)
                return;
        }
        //end логика до 10го раунда

        //прожать всю момаль
        if (bot.Character.GetMoral() >= 20)
        {
            bot.Character.AddMoral(bot.Status, -20, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(10);
        }

        if (bot.Character.GetMoral() >= 13)
        {
            bot.Character.AddMoral(bot.Status, -13, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(5);
        }

        if (bot.Character.GetMoral() >= 8)
        {
            bot.Character.AddMoral(bot.Status, -8, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(2);
        }

        if (bot.Character.GetMoral() >= 5)
        {
            bot.Character.AddMoral(bot.Status, -5, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(1);
        }
        //end прожать всю момаль
    }

    public void HandleBotMoral(GamePlayerBridgeClass bot, GameClass game)
    {

        if (bot.Status.PlaceAtLeaderBoard <= 2)
        {
            if (bot.Character.GetMoral() < 5)
            {
                HandleBotMoralForSkill(bot, game);
                return;
            }
        }

        if (bot.Character.Name == "Sirinoks")
        {
            //логика до 10го раунда
            HandleBotMoralForSkill(bot, game);
            //end логика до 10го раунда
            return;
        }

        /*if (bot.Character.Name == "Вампур" && game.RoundNo == 5)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }*/

        if (bot.Character.Name == "LeCrisp" && game.RoundNo <= 5)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.Character.Name == "Загадочный Спартанец в маске")
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.Character.Name == "DeepList")
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.Character.Name == "mylorik")
        {
            var mylorikRevenge = bot.Character.Passives.MylorikRevenge;
            if (mylorikRevenge != null)
            {
                var totalNotFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                var totalRevenges = mylorikRevenge.EnemyListPlayerIds.Count;

                //если на всех уже был запрокан луз или победа, то меняет мораль на скилл
                if (totalRevenges == 5)
                {
                    HandleBotMoralForSkill(bot, game);
                    return;
                }

                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), меняет всю мораль на скилл
                var roundsLeft = 11 - game.RoundNo;
                if (totalNotFinishedRevenges >= roundsLeft)
                {
                    HandleBotMoralForSkill(bot, game);
                    return;
                }
            }
        }


        HandleBotMoralForPoints(bot, game);
    }


    public async Task HandleBotAttack(GamePlayerBridgeClass bot, GameClass game)
    {
        try
        {
            //local variables
            var allTargets = game.NanobotsList.Find(x => x.GameId == game.GameId).Nanobots.Where(x => x.GetPlayerId() != bot.GetPlayerId()).ToList();

            if (game.RoundNo == 10) allTargets = allTargets.Where(x => x.Player.Character.Name != "Тигр").ToList();

            if (game.RoundNo is 9 or 10)
                if (game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                    allTargets = allTargets.Where(x => x.Player.Character.Name != "Darksci").ToList();

            decimal maxRandomNumber = 0;
            var isBlock = allTargets.Count;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            var noBlock = 99999;
            var yesBlock = -99999;
            var botJustice = bot.Character.Justice.GetRealJusticeNow();
            //end local variables

            //edit block for team
            switch (game.Teams.Count)
            {
                case 2:
                    isBlock += 1;
                    break;
                case 3:
                    isBlock += 2;
                    break;
            }
            //end

            //character variables
            var DarksciTheOne = Guid.Empty;
            var AwdkaFirst = 0;
            decimal SpartanTarget = 0;
            //end character variables

            //local varaibles
            var isTargetTooGoodNumber = 7;
            var isLostLastRoundAndTargetIsBetterNumber = 5;
            var isJusticeTheSameNumber = 5;
            var isJusticeLessThanTargetNumber = 7;
            var isTargetFirstNumber = 1;
            var isTargetSecondWhenBotFirstNumber = 1;
            var isBotWonAndTooGoodNumber = 4;
            var isTargetContrNumber = 3;
            var isTargetTaretNumber = 1;
            var isTargetTooGood = false;
            var isJusticeTheSame = false;
            var isJusticeLessThanTarget = false;
            var isTargetFirst = false;
            var isTargetSecondWhenBotFirst = false;
            var isLostLastRoundAndTargetIsBetter = false;
            var isBotWonAndTooGood = false;
            var isTargetTaret = false;
            var isTargetContr = false;
            var howManyAttackingTheSameTarget = 0;
            var justiceDifference = 0;
            //

            //calculation Tens
            foreach (var target in allTargets)
            {
                var targetJustice = target.Player.Character.Justice.GetSeenJusticeNow();

                //if justice is the same
                if (botJustice == targetJustice)
                {
                    target.AttackPreference -= isJusticeTheSameNumber;
                    isJusticeTheSame = true;
                }
                //if bot justice less than platers
                else if (botJustice < targetJustice)
                {
                    target.AttackPreference -= isJusticeLessThanTargetNumber;
                    isJusticeLessThanTarget = true;
                }

                //if player is first
                if (target.Player.Status.PlaceAtLeaderBoard == 1)
                {
                    target.AttackPreference -= isTargetFirstNumber;
                    isTargetFirst = true;
                }

                //if player is second when we are first
                if (bot.Status.PlaceAtLeaderBoard == 1 && target.Player.Status.PlaceAtLeaderBoard == 2)
                {
                    target.AttackPreference -= isTargetSecondWhenBotFirstNumber;
                    isTargetSecondWhenBotFirst = true;
                }



                //если на прошлом бою враг был toogood
                //-= 7
                if (bot.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId() &&
                        x.IsTooGoodEnemy))
                {
                    target.AttackPreference -= isTargetTooGoodNumber;
                    isTargetTooGood = true;
                }
                else if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.GetPlayerId() && x.IsTooGoodMe))
                {
                    target.AttackPreference -= isTargetTooGoodNumber;
                    isTargetTooGood = true;
                }
                //если на прошлом ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId() &&
                             x.IsStatsBetterEnemy))
                {
                    target.AttackPreference -= isLostLastRoundAndTargetIsBetterNumber;
                    isLostLastRoundAndTargetIsBetter = true;
                }
                //если на прошлом-1 ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 2 && x.EnemyId == target.GetPlayerId() &&
                             x.IsStatsBetterEnemy))
                {
                    target.AttackPreference -= isLostLastRoundAndTargetIsBetterNumber;
                    isLostLastRoundAndTargetIsBetter = true;
                }
           


                //won and too good
                if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.GetPlayerId() && x.IsTooGoodEnemy))
                {
                    target.AttackPreference += isBotWonAndTooGoodNumber;
                    isBotWonAndTooGood = true;
                }


                //how many players are attacking the same player
                howManyAttackingTheSameTarget = allTargets
                    .FindAll(x => x.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())).Count;
                target.AttackPreference -= howManyAttackingTheSameTarget;


                //target
                if (target.AttackPreference >= 5)
                    if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                    {
                        target.AttackPreference += isTargetTaretNumber;
                        isTargetTaret = true;
                    }

                //contr
                if (target.AttackPreference >= 5)
                    if (bot.Character.GetWhoIContre() == target.Player.Character.GetSkillClass())
                    {
                        target.AttackPreference += isTargetContrNumber;
                        isTargetContr = true;
                    }


                //justice diff
                if (allTargets.All(x => x.Player.Character.Justice.GetSeenJusticeNow() < botJustice))
                {
                    justiceDifference = botJustice - targetJustice;
                    target.AttackPreference += justiceDifference;
                }

                //custom bot behavior
                switch (bot.Character.Name)
                {
                    case "Weedwick":
                        if (target.Player.Character.Name == "DeepList")
                            target.AttackPreference = 0;
                        break;
                    case "DeepList":
                        var deepListMadness = bot.Character.Passives.DeepListMadnessTriggeredWhen;
                        if (deepListMadness.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        if (target.Player.Character.Name == "Weedwick")
                            target.AttackPreference = 0;
                        break;
                    case "Тигр":

                        var tigr = bot.Character.Passives.TigrThreeZeroList;
                        if (tigr != null)
                        {
                            if (target.AttackPreference <= 3)
                            {
                                if (tigr.FriendList.Any(x =>
                                        x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries == 1 && x.IsUnique))
                                    target.AttackPreference -= 2;
                                if (tigr.FriendList.Any(x =>
                                        x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries == 2 && x.IsUnique))
                                    target.AttackPreference = 0;
                            }

                            switch (target.AttackPreference)
                            {
                                case >= 8:
                                {
                                    if (tigr.FriendList.Any(x =>
                                            x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries >= 1 && x.IsUnique))
                                        target.AttackPreference += 9;

                                    break;
                                }
                                case >= 6:
                                {
                                    if (tigr.FriendList.Any(x =>
                                            x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries >= 1 && x.IsUnique))
                                        target.AttackPreference += 3;

                                    break;
                                }
                            }
                        }

                        break;


                    case "AWDKA":

                        if (game.RoundNo == 1)
                        {
                            if (target.Player.Character.GetIntelligence() > AwdkaFirst)
                            {
                                AwdkaFirst = target.Player.Character.GetIntelligence();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.Character.GetStrength() > AwdkaFirst)
                            {
                                AwdkaFirst = target.Player.Character.GetStrength();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.Character.GetSpeed() > AwdkaFirst)
                            {
                                AwdkaFirst = target.Player.Character.GetSpeed();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.Character.GetPsyche() > AwdkaFirst)
                            {
                                AwdkaFirst = target.Player.Character.GetPsyche();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }
                        }


                        var awdkaTrying = bot.Character.Passives.AwdkaTryingList;
 
                            var awdkaTryingTarget =
                                awdkaTrying.TryingList.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                            if (awdkaTryingTarget != null)
                            {
                                //-2 тем, на ком есть стак платины(до тех пор, пока он еще не на всех)
                                if (awdkaTrying.TryingList.Count(x => x.IsUnique) < 5)
                                    if (awdkaTryingTarget.IsUnique)
                                        target.AttackPreference -= 2;

                                if (game.RoundNo <= 4)
                                    if (!awdkaTryingTarget.IsUnique)
                                        target.AttackPreference = 15 - target.AttackPreference;
                                if (game.RoundNo <= 5)
                                    if (!awdkaTryingTarget.IsUnique)
                                        target.AttackPreference += 5;
                            }
                            else
                            {
                                if (game.RoundNo <= 5) target.AttackPreference += 5;
                            }
                        


                        var triggered = false;

                        if (game.RoundNo > 5)
                        {
                            var wons = target.Player.Status.WhoToLostEveryRound.OrderByDescending(x => x.RoundNo)
                                .ToList();
                            foreach (var won in wons)
                                if (won.EnemyId == bot.GetPlayerId() && won.WhoAttacked == bot.GetPlayerId())
                                {
                                    var places = 6;
                                    if (target.PlaceAtLeaderBoard() == 6) places = 7;
                                    target.AttackPreference *= ((game.RoundNo - won.RoundNo +
                                                                 (decimal)((places - target.PlaceAtLeaderBoard()) *
                                                                           (game.RoundNo - won.RoundNo - 1))) / 2);
                                    triggered = true;
                                    break;
                                }


                            if (!triggered)
                            {
                                var places = 6;
                                if (target.PlaceAtLeaderBoard() == 6) places = 7;
                                target.AttackPreference *= ((game.RoundNo +
                                                             (decimal)(places - target.PlaceAtLeaderBoard()) *
                                                             (game.RoundNo - 1)) / 2);
                            }
                        }

                        break;
                    case "HardKitty":
                        if (game.RoundNo < 5) mandatoryAttack = allTargets.First().PlaceAtLeaderBoard();

                        if (target.PlaceAtLeaderBoard() == 1) target.AttackPreference += 1;

                        break;
                    case "Darksci":
                        if (game.RoundNo < 8)
                            if (targetJustice > botJustice)
                                target.AttackPreference -= 3;

                        var darksciLucky = bot.Character.Passives.DarksciLuckyList;
    
                            if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                if (target.AttackPreference > 1)
                                {
                                    target.AttackPreference += 3;
                                    if (game.RoundNo < 5) target.AttackPreference += 3;
                                }

                            if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()) &&
                                darksciLucky.TouchedPlayers.Count == 4) target.AttackPreference = 0;

                            // Если ОДИН из тех, на ком не запрокан стак, уже побеждал даркси ПО СТАТАМ, то его значение = 0.
                            if (darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                {
                                    var darksciLuckyTheOne = bot.Status.WhoToLostEveryRound.Find(x =>
                                        x.EnemyId == target.GetPlayerId() && x.IsStatsBetterEnemy);
                                    if (darksciLuckyTheOne != null && DarksciTheOne == Guid.Empty)
                                    {
                                        DarksciTheOne = target.GetPlayerId();
                                        target.AttackPreference = 0;
                                    }
                                }

                            //Если незапроканных стаков = кол-во оставшихся ходов - 3, то выбирает цель только из них. (пока не останется 1) 

                            var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                            var roundsLeft2 = 11 - (game.RoundNo + 3);
                            if (notTouched >= roundsLeft2)
                                if (darksciLucky.TouchedPlayers.Count < 5)
                                    if (darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                        target.AttackPreference = 0;


                            if (game.RoundNo == 7 && bot.Character.GetPsyche() < 4 &&
                                darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                            if (game.RoundNo >= 8 && darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                        

                        break;
                    case "Mit*suki*":
                        if (target.AttackPreference >= 5)
                        {
                            if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                                target.AttackPreference += 3;

                            if (game.RoundNo < 5 && target.Player.Character.Name == "HardKitty")
                                target.AttackPreference = 0;

                            if (game.RoundNo > 5 && target.Player.Character.Name == "HardKitty" &&
                                target.AttackPreference >= 5) mandatoryAttack = target.PlaceAtLeaderBoard();
                        }

                        break;
                    case "mylorik":
                        var mylorikRevenge = bot.Character.Passives.MylorikRevenge;
                        var revengeEnemy =
                                mylorikRevenge.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                            var totalFinishedRevenges =
                                mylorikRevenge.EnemyListPlayerIds.FindAll(x => !x.IsUnique).Count;
                            var totalNotFinishedRevenges =
                                mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;


                            if (revengeEnemy != null)
                            {
                                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), то х2 преф
                                if (revengeEnemy.IsUnique)
                                {
                                    var leftRound = 11 - game.RoundNo;
                                    if (totalNotFinishedRevenges >= leftRound) target.AttackPreference *= 3;
                                }

                                //первые 4 хода, Не нападает на тех, на ком уже запрокан луз мести, но не запрокана победа мести. если запроканы еще не все 
                                if (revengeEnemy.IsUnique && game.RoundNo <= 4)
                                    if (totalFinishedRevenges < 5)
                                        target.AttackPreference = 0;

                                // {Начиная с 5 хода: Если преференс врага С запроканным лузом но БЕЗ победы   >= 8, то +20
                                if (game.RoundNo >= 5 && target.AttackPreference >= 8 && revengeEnemy.IsUnique)
                                    target.AttackPreference += 20;
                                // {Начиная с 6 хода: Если преференс врага С запроканным лузом но БЕЗ победы   >= 5, то +10
                                else if (game.RoundNo >= 6 && target.AttackPreference >= 5 && revengeEnemy.IsUnique)
                                    target.AttackPreference += 10;

                                //преф - 4 тем, на ком уже запрокана ПОБЕДА мести, если запроканы еще не все
                                if (!revengeEnemy.IsUnique && totalFinishedRevenges < 5) target.AttackPreference -= 4;


                                if (game.RoundNo > 5)
                                    //после 5го хода: преф игроков С Лузом но БЕЗ победы не может опуститься ниже 4 
                                    if (revengeEnemy.IsUnique)
                                        if (target.AttackPreference < 4)
                                            target.AttackPreference = 4;
                            }
                            else
                            {
                                //на первых 4х ходах если у врага больше справедливости и не запрокана ни одна метка, то преф +2 * разницу в вашей справедливости ( с положительным знаком)
                                if (game.RoundNo <= 4)
                                    if (targetJustice > botJustice)
                                        target.AttackPreference += 2 * (targetJustice - botJustice);

                                //Если на врагах еще не запрокан луз мести - их преференс +5-игроки с запроканым лузом или победой. 
                                target.AttackPreference += 5 - mylorikRevenge.EnemyListPlayerIds.Count;

                                //Первые 4 хода: + 17 тем у кого справедливости больше чем у тебя, если на них не запрокан луз мести.
                                if (game.RoundNo <= 4 && botJustice < targetJustice)
                                    target.AttackPreference += 17;
                            }
                        


                        //"-5 за more stats" и "-7 за toogood" из базовых условий десяток   / 1 + кол-во стаков сломанного щита
                        if (game.RoundNo >= 5)
                        {
                            var mylorikSpartan = bot.Character.Passives.MylorikSpartan;

                                var spartanEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                                if (spartanEnemy != null)
                                {
                                    if (isTargetTooGood)
                                        target.AttackPreference += isTargetTooGoodNumber - isTargetTooGoodNumber / (1 + spartanEnemy.LostTimes);
                                    else if (isLostLastRoundAndTargetIsBetter)
                                        target.AttackPreference += isLostLastRoundAndTargetIsBetterNumber - isLostLastRoundAndTargetIsBetterNumber / (1 + spartanEnemy.LostTimes);
                                }
                            
                        }


                        break;
                    case "Краборак":

                        if (allTargets.Any(x => x.PlaceAtLeaderBoard() >= 4 && x.AttackPreference > 0))
                            if (target.PlaceAtLeaderBoard() < 4)
                                target.AttackPreference -= 4;


                        if (target.Player.Character.Name == "HardKitty") target.AttackPreference -= 1;

                        break;

                    case "Братишка":
                        if (target.PlaceAtLeaderBoard() == bot.Status.PlaceAtLeaderBoard + 1 ||
                            target.PlaceAtLeaderBoard() == bot.Status.PlaceAtLeaderBoard - 1)
                        {
                            if (target.AttackPreference > 1) target.AttackPreference += 2;

                            if (target.AttackPreference >= 5) target.AttackPreference += 3;
                        }

                        break;
                    case "Sirinoks":

                        //После первого хода:  преференс -3 всем, кто не подходит под текущую мишень (мишень скилла).
                        if (game.RoundNo > 1)
                        {
                            if (bot.Character.GetCurrentSkillClassTarget() != target.Player.Character.GetSkillClass())
                            {
                                target.AttackPreference -= 3;
                            }
                            else
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        var siriFriends = bot.Character.Passives.SirinoksFriendsList;

            
                            //+5 к значению тех, кто еще не друг.
                            if (!siriFriends.FriendList.Contains(target.GetPlayerId()) && target.AttackPreference > 0)
                            {
                                target.AttackPreference += 5;
                            }


                            //До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                var sirisFried = allTargets.Find(x => x.GetPlayerId() == siriFriends.FriendList.First());

                                if (target.GetPlayerId() != sirisFried.GetPlayerId())
                                {
                                    target.AttackPreference = 0;
                                }
                                else
                                {
                                    if (target.AttackPreference > 3)
                                    {
                                        mandatoryAttack = target.Player.Status.PlaceAtLeaderBoard;
                                    }
                                    else
                                    {
                                        target.AttackPreference = 0;
                                    }

                                    if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                                    {
                                        mandatoryAttack = target.PlaceAtLeaderBoard();
                                    }
                                }
                            }


                            //Если кол-во оставшихся ходов == кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                            var nonFiendsLeft = 5 - siriFriends.FriendList.Count;
                            var roundsLeft = 11 - game.RoundNo;
                            var allNotFriends =
                                allTargets.FindAll(x => !siriFriends.FriendList.Contains(x.GetPlayerId()));


                            if (nonFiendsLeft >= roundsLeft)
                                if (allNotFriends is { Count: > 0 })
                                    mandatoryAttack = allNotFriends.FirstOrDefault().Player.Status.PlaceAtLeaderBoard;
                        

                        if (game.RoundNo == 1 && target.Player.Character.Name == "Осьминожка")
                            target.AttackPreference = 0;
                        break;
                    case "Толя":

                        var tolyaCount = bot.Character.Passives.TolyaCount;

                        if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == target.GetPlayerId()))
                        {
                            if (target.AttackPreference >= 5)
                                target.AttackPreference += 3;

                            target.AttackPreference += 4;
                            
                            if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId()))
                                target.AttackPreference += 5;
                        }

                        if (tolyaCount.IsReadyToUse)
                        {
                            if (!isTargetTooGood)
                                target.AttackPreference = 13 - target.AttackPreference;
                        }
                        else
                        {
                            var jewAamount = 6;
                            if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1))
                                jewAamount = 2;

                            //Jew
                            foreach (var v in allTargets)
                                if (v.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                    target.AttackPreference += jewAamount;
                            //end Jew
                        }

                        break;

                    case "LeCrisp":
                        //Jew
                        foreach (var v in allTargets)
                            if (v.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                target.AttackPreference += 6;
                        //end Jew
                        break;


                    case "Глеб":

                        if (target.Player.Status.IsSkip)
                            target.AttackPreference = 0;

                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = bot.Character.Passives.GlebChallengerTriggeredWhen;

                       
                        if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (isTargetTooGood)
                                target.AttackPreference += isTargetTooGoodNumber;
                            else if (isLostLastRoundAndTargetIsBetter)
                                target.AttackPreference += isLostLastRoundAndTargetIsBetterNumber;

                            //Под претендентом автоматически выбирает цель с наибольшим значением. 
                            var sorted = allTargets.OrderByDescending(x => x.AttackPreference).ToList();
                            mandatoryAttack = sorted.First().Player.Status.PlaceAtLeaderBoard;
                        }

                        break;
                    case "Загадочный Спартанец в маске":
                        /*if (game.RoundNo == 10)
                            if (target.Player.Character.Name == "Sirinoks")
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        */

                        var spartanMark = bot.Character.Passives.SpartanMark;
                        var spartanShame = bot.Character.Passives.SpartanShame;
                        if (spartanMark != null && spartanShame != null)
                        {
                            if (game.RoundNo <= 4)
                            {
                                if (spartanShame.FriendList.Contains(target.GetPlayerId()))
                                {
                                    target.AttackPreference -= 3;
                                }
                                else
                                {
                                    if (spartanMark.FriendList.Contains(target.GetPlayerId()))
                                        target.AttackPreference += 10;
                                }
                            }
                            else
                            {
                                if (!spartanMark.FriendList.Contains(target.GetPlayerId()))
                                {
                                    target.AttackPreference -= 4;
                                }
                                else
                                {
                                    if (bot.Character.Justice.GetRealJusticeNow() > targetJustice && !isTargetTooGood)
                                        if (target.AttackPreference > SpartanTarget)
                                        {
                                            mandatoryAttack = target.PlaceAtLeaderBoard();
                                            SpartanTarget = target.AttackPreference;
                                        }
                                }
                            }
                        }


                        break;
                    case "Вампур":
                        if (target.Player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        {
                            if (isJusticeTheSame)
                            {
                                //Вампур не получает -5 same jst если враг проиграл на прошлом ходу
                                target.AttackPreference += isJusticeTheSameNumber;
                            }

                            if (isJusticeLessThanTarget)
                            {
                                //Вампур меняет -7 more jst на -4   если враг проиграл на прошлом ходу
                                target.AttackPreference += isJusticeLessThanTargetNumber;
                                target.AttackPreference -= 4;
                            }
                        }
                        //Преференс врагов += (их справедливость *2)
                        target.AttackPreference += targetJustice * 2;

                        var vampyrHematophagiaList = bot.Character.Passives.VampyrHematophagiaList;
                        
                        if (vampyrHematophagiaList != null)
                        {
                            if (vampyrHematophagiaList.Hematophagia.Count < 5)
                            {
                                if (vampyrHematophagiaList.Hematophagia.Any(x => x.EnemyId == target.GetPlayerId()))
                                {
                                    //Вампур получает -3 всем на ком запрокан укус, если укусов еще не 5.
                                    target.AttackPreference -= 3;
                                }
                            }
                        }

                        if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId()))
                            target.AttackPreference = 0;
                        break;
                }
                //end custom bot behavior


                //custom enemy
                switch (target.Player.Character.Name)
                {
                    case "Darksci":
                        if (game.RoundNo == 9)
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.GetAllGlobalLogs()
                            .Contains(
                                $"Толя запизделся и спалил, что {target.Player.DiscordUsername} - {target.Player.Character.Name}"))
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.PlayersList.Any(x => x.Character.Name == "mylorik"))
                        {
                            var mylorik = game.PlayersList.Find(x => x.Character.Name == "mylorik");
                            if (game.GetAllGlobalLogs().Contains($"{target.Player.DiscordUsername} психанул") &&
                                game.GetAllGlobalLogs().Contains($"{mylorik.DiscordUsername} психанул"))
                            {
                                if (target.AttackPreference > 5) target.AttackPreference += 5;
                                if (target.AttackPreference > 7) target.AttackPreference += 5;
                            }
                        }

                        if (bot.Character.Name is "DeepList" or "AWDKA")
                        {
                            if (target.AttackPreference >= 7)
                            {
                                target.AttackPreference += 4;
                            }
                        }

                        break;
                    case "HardKitty":
                        if (game.RoundNo <= 4) target.AttackPreference /= 5;
                        break;
                    case "Sirinoks":
                        if (game.RoundNo <= 4) target.AttackPreference -= 4;

                        if (game.RoundNo == 10) target.AttackPreference -= 1;
                        break;
                    case "Вампур":
                        if (botJustice <= targetJustice)
                            target.AttackPreference += 3;
                        break;
                    case "Глеб":
                        var glebChallender = target.Player.Character.Passives.GlebChallengerTriggeredWhen;
                        var glebSleeping = target.Player.Character.Passives.GlebSleepingTriggeredWhen;

                        var totalChallengers = 0;
                        var totalSleeps = 0;

                        for (var i = 1; i < game.RoundNo + 1; i++)
                            if (glebChallender.WhenToTrigger.Contains(i))
                                totalChallengers++;
                        for (var i = 1; i < game.RoundNo + 1; i++)
                            if (glebSleeping.WhenToTrigger.Contains(i))
                                totalSleeps++;

                        if (totalChallengers >= 1 && totalSleeps >= 2) target.AttackPreference /= 2;

                        break;
                }
                //end custom enemy
            }

            
            foreach (var target2 in allTargets)
            {
                if(target2.AttackPreference >= 6)
                {
                    if (allTargets.Where(x => x.GetPlayerId() != target2.GetPlayerId()).ToList()
                        .All(x => x.AttackPreference < target2.AttackPreference))
                    {
                        target2.AttackPreference += 2;
                    }
                }


                switch (bot.Character.Name)
                {
                    case "mylorik":
                        //Если кол-во врагов с запроканным лузом но без победы = кол-во оставшихся ходов, то преференс ДРУГИХ врагов / 2
                        var mylorikRevenge = bot.Character.Passives.MylorikRevenge;
                        if (mylorikRevenge != null)
                        {
                            var totalFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                            var roundsLeft = 11 - game.RoundNo;
                            if (totalFinishedRevenges >= roundsLeft)
                            {
                                if (!mylorikRevenge.EnemyListPlayerIds.Any(x => x.IsUnique && x.EnemyPlayerId == target2.GetPlayerId()))
                                {
                                    if (target2.AttackPreference >= 2)
                                    {
                                        target2.AttackPreference /= 2;
                                    }
                                }
                            }
                        }
                        break;
                }
            }

            if (game.Teams.Count > 0)
            {
                //team bot behavior. target == team member
                foreach (var target3 in allTargets)
                {
                    if (!bot.isTeamMember(game, target3.GetPlayerId()))
                        continue;

                    var realAttackPreference = target3.AttackPreference;
                    target3.AttackPreference = 0;

                    //custom bot behavior in teams. target == team member
                    switch (bot.Character.Name)
                    {
                        case "DeepList":
                            var deepListMockeryList = bot.Character.Passives.DeepListMockeryList;

                            var currentDeepList2 =
                                deepListMockeryList?.WhoWonTimes.Find(x => x.EnemyPlayerId == target3.GetPlayerId());

                            if (currentDeepList2 is { Times: 1 }) target3.AttackPreference = realAttackPreference;

                            break;
                        case "Тигр":
                            break;
                        case "AWDKA":
                            target3.AttackPreference = realAttackPreference;
                            break;
                        case "HardKitty":
                            break;
                        case "Darksci":
                            var darksciLucky = bot.Character.Passives.DarksciLuckyList;
                            if (darksciLucky != null)
                            {
                               if(!darksciLucky.TouchedPlayers.Contains(target3.GetPlayerId()))
                                   target3.AttackPreference = realAttackPreference;
                            }
                            break;
                        case "Mit*suki*":
                            break;
                        case "mylorik":
                            var mylorikRevenge = bot.Character.Passives.MylorikRevenge;
                            if (mylorikRevenge != null)
                            {
                                var revengeEnemy = mylorikRevenge.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == target3.GetPlayerId());

                                //ноль выключается если союзнику можно отмстить
                                if (revengeEnemy is { IsUnique: true }) target3.AttackPreference = realAttackPreference;

                                //если отмстил уже всем врагам, то выключается ноль на союзниках, которым еще не мстил
                                var finishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => !x.IsUnique);
                                var teamCount = game.GetTeammates(bot).Count;
                                if (finishedRevenges.Count >= 5 - teamCount)
                                {
                                    if (finishedRevenges.All(x => x.EnemyPlayerId != target3.GetPlayerId()))
                                    {
                                        target3.AttackPreference = realAttackPreference;
                                    }
                                }
                            }
                            break;
                        case "Краборак":
                            break;
                        case "Братишка":
                            break;
                        case "Sirinoks":
                            //До начала 5го хода может нападать только на одну цель - союзника
                            var siriFriends = bot.Character.Passives.SirinoksFriendsList;
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                if (siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    mandatoryAttack = target3.PlaceAtLeaderBoard();

                            }
                            else if (game.RoundNo < 5)
                            {
                                var teammates = game.GetTeammates(bot);
                                mandatoryAttack = game.PlayersList.Find(x => x.GetPlayerId() == teammates[0]).Status.PlaceAtLeaderBoard;
                            }
                            else
                            {
                                //. снимается ноль с тех, кто не в друзьях.
                                if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    target3.AttackPreference = realAttackPreference;

                                //снимается 0 со всех тех, кто подходит под мишень.
                                if (bot.Character.GetCurrentSkillClassTarget() == target3.Player.Character.GetSkillClass())
                                    target3.AttackPreference = realAttackPreference;

                                //если кол-во оставшихся ходов - 3 <= союзникам в друзьях, то нападает только на союзников которых можно добавить в друзья. (выбирает из них по мишени. если под мишень не подходит, то выбирает рандомно)
                                if (game.RoundNo < 9)
                                {
                                    if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                        if (bot.Character.GetCurrentSkillClassTarget() == target3.Player.Character.GetSkillClass())
                                            mandatoryAttack = target3.PlaceAtLeaderBoard();
                                }
                            }

                            break;
                        case "Толя":
                            var enemyCount = allTargets.Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(target3.GetPlayerId()));
                            if (enemyCount >= 2)
                                target3.AttackPreference = realAttackPreference;
                            break;
                        case "LeCrisp":
                            enemyCount = allTargets.Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(target3.GetPlayerId()));
                            if (enemyCount >= 2)
                                target3.AttackPreference = realAttackPreference;
                            break;
                        case "Глеб":
                            break;
                        case "Загадочный Спартанец в маске":
                            break;
                        case "Вампур":
                            var vampyrHematophagiaList = bot.Character.Passives.VampyrHematophagiaList;

                            if (vampyrHematophagiaList != null)
                            {
                                if (vampyrHematophagiaList.Hematophagia.All(x => x.EnemyId != target3.GetPlayerId()))
                                {
                                    target3.AttackPreference = realAttackPreference;
                                }
                            }
                            break;
                    }
                    //end custom bot behavior
                }

                
                //team bot behavior. target == enemy
                foreach (var target4 in allTargets)
                {
                    if (bot.isTeamMember(game, target4.GetPlayerId()))
                        continue;

                    //custom bot behavior in teams. target == enemy
                    switch (bot.Character.Name)
                    {
                        case "AWDKA":
                            var platCount = 0;
                            var teamCount = 0;

                            // 0 на всех врагов, нападает только на союзников, чтобы проебать им, пока платина не будет на всех союзниках, либо пока не наступит 7ой ход
                            var awdkaTrying = bot.Character.Passives.AwdkaTryingList;
                            if (awdkaTrying != null)
                            {
                                foreach (var teammate in game.GetTeammates(bot))
                                {
                                    teamCount++;
                                    var awdkaTryingTarget = awdkaTrying.TryingList.Find(x => x.EnemyPlayerId == teammate);
                                    if (awdkaTryingTarget is { IsUnique: true })
                                    {
                                        platCount++;
                                    }
                                }
                            }
                            if (platCount != teamCount && game.RoundNo < 7)
                                target4.AttackPreference = 0;
                            break;
                    }
                    //end custom bot behavior
                }
            }


            //count maxRandomNumber and isBlock
            foreach (var target2 in allTargets)
            {
                if (target2.AttackPreference <= 0)
                {
                    isBlock--;
                    target2.AttackPreference = 0;
                }

                maxRandomNumber += target2.AttackPreference;
            }

            //end calculation Tens


            //custom behaviour After calculation Tens
            switch (bot.Character.Name)
            {
                case "Тигр":
                    var tigr = bot.Character.Passives.TigrThreeZeroList;
                    if (game.RoundNo == 4)
                            if (tigr.FriendList.Any(x => x.WinsSeries == 2 && x.IsUnique))
                                isBlock = yesBlock;

                        var lowRates = allTargets.Where(x => x.AttackPreference <= 3).ToList();
                        var countLowRate = 0;
                        if (lowRates.Count() >= 2)
                        {
                            foreach (var lowRate in lowRates)
                                if (tigr.FriendList.Any(x =>
                                        x.EnemyPlayerId == lowRate.GetPlayerId() && x.WinsSeries == 2 && x.IsUnique))
                                    countLowRate++;

                            if (countLowRate >= 2) isBlock = yesBlock;
                        }
                    


                    break;
                case "AWDKA":
                    isBlock = noBlock;
                    break;
                case "Darksci":
                    minimumRandomNumberForBlock += 1;
                    if (game.RoundNo > 1 && botJustice == 0) minimumRandomNumberForBlock += 1;

                    if (game.RoundNo is 3 or 5 or 9)
                        if (bot.Character.GetPsyche() <= 1)
                        {
                            minimumRandomNumberForBlock = 4;
                            maximumRandomNumberForBlock = 5;
                        }

                    var darksciLucky = bot.Character.Passives.DarksciLuckyList;
         
                        var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                        var roundsLeft = 11 - (game.RoundNo + 3);
                        if (notTouched >= roundsLeft)
                            if (darksciLucky.TouchedPlayers.Count < 5)
                                if (mandatoryAttack == -1)
                                {
                                    var listofTargets = allTargets.Where(x =>
                                        !darksciLucky.TouchedPlayers.Contains(x.GetPlayerId())).ToList();

                                    if (listofTargets.Count > 0)
                                        mandatoryAttack = listofTargets.First().PlaceAtLeaderBoard();
                                }
                    

                    break;
                case "Братишка":
                    if (botJustice != 5)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }

                    var min = allTargets.Min(x => x.Player.Character.Justice.GetSeenJusticeNow());
                    var check = allTargets.Find(x =>
                        x.Player.Character.Justice.GetSeenJusticeNow() == min);

                    if (check.Player.Character.Justice.GetSeenJusticeNow() >= botJustice)
                        minimumRandomNumberForBlock += 1;

                    break;
                case "Осьминожка":
                    isBlock = noBlock;
                    break;
                case "HardKitty":
                    isBlock = noBlock;
                    var hardKitty = bot.Character.Passives.HardKittyDoebatsya;

                    if (allTargets.All(x => x.AttackPreference <= 3) && mandatoryAttack == -1)
                    {
                        var doebatsya = hardKitty.LostSeries
                            .Where(x => allTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).First();
                        mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                            .PlaceAtLeaderBoard();
                    }

                    if (allTargets.Any(x => x.AttackPreference > 5) && mandatoryAttack == -1)
                    {
                        var doebathsyaTargets = allTargets.Where(x => x.AttackPreference > 5)
                            .OrderByDescending(x => x.AttackPreference).ToList();
                        var doebatsya = hardKitty.LostSeries
                            .Where(x => doebathsyaTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).First();
                        mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                            .PlaceAtLeaderBoard();
                    }


                    break;
                case "Глеб":
                    isBlock = noBlock;
                    break;
                case "Краборак":
                    isBlock = noBlock;
                    break;
                case "Mit*suki*":
                    switch (game.RoundNo)
                    {
                        case < 8:
                            isBlock = noBlock;
                            break;
                        case 10:
                            minimumRandomNumberForBlock = 3;
                            maximumRandomNumberForBlock = 4;
                            break;
                    }

                    break;
                case "mylorik":
                    isBlock = noBlock;
                    break;
                case "Sirinoks":
                    if (game.RoundNo is 10 or 1)
                    {
                        isBlock = noBlock;
                    }
                    /*
                    else if (bot.Character.Passives.SirinoksTraining.Training.Count == 0)
                    {
                        var siriFriends = bot.Character.Passives.SirinoksFriendsList;
                        var siriFriend = allTargets.Find(x => x.GetPlayerId() == siriFriends?.FriendList.FirstOrDefault());
                        if(siriFriend != null)
                            if (siriFriend.Player.Character.Name != "Осьминожка")
                                mandatoryAttack = siriFriend.Player.Status.PlaceAtLeaderBoard;
                    }
                    */

                    break;

                case "Загадочный Спартанец в маске":
                    //на последнем ходу блок -2 (от 2 до 5)
                    if (game.RoundNo < 10)
                    {
                        isBlock = noBlock;
                    }

                    if (allTargets.All(x =>
                            bot.Character.Justice.GetRealJusticeNow() <=
                            x.Player.Character.Justice.GetSeenJusticeNow()))
                        if (game.RoundNo == 10)
                        {
                            minimumRandomNumberForBlock = 2;
                            maximumRandomNumberForBlock = 4;
                        }

                    // end на последнем ходу блок -2 (от 2 до 5)
                    break;

                case "Толя":
                    //rammus
                    var count = allTargets.FindAll(x => x.AttackPreference >= 10).Count;
                    if (count <= 0)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    //end rammus

                    var tolyaCount = bot.Character.Passives.TolyaCount;

                    if (tolyaCount.IsReadyToUse)
                        if (game.RoundNo is 3 or 8)
                            isBlock = yesBlock;
                    break;


                case "LeCrisp":
                    //block chances
                    if (game.RoundNo <= 4)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }

                    if (game.RoundNo == 1)
                    {
                        minimumRandomNumberForBlock = 3;
                        maximumRandomNumberForBlock = 4;
                    }

                    var assassinsCount = allTargets
                        .FindAll(x => bot.Character.GetStrength() - x.Player.Character.GetStrength() <= -2).Count;

                    minimumRandomNumberForBlock += assassinsCount;

                    //end block chances
                    break;
            }
            //end custom behaviour After calculation Tens


            //mandatory attack
            var isAttacked = false;
            if (mandatoryAttack >= 0) isAttacked = await AttackPlayer(bot, mandatoryAttack);

            //block
            if (minimumRandomNumberForBlock > maximumRandomNumberForBlock)
                maximumRandomNumberForBlock = minimumRandomNumberForBlock;

            var isBlockCheck = _rand.Random(minimumRandomNumberForBlock, maximumRandomNumberForBlock);
            if (isBlockCheck > isBlock && !isAttacked && mandatoryAttack == -1)
            {
                //block
                await _gameReaction.HandleAttack(bot, null, -10);
                ResetTens(allTargets);
                return;
            }

            //"random" attack
            var randomNumber = _rand.Random(1, (int)Math.Ceiling(maxRandomNumber));

            decimal totalPreference = 0;
            int whoToAttack;
            foreach (var target in allTargets)
            {
                totalPreference += target.AttackPreference;
                var rounded = (int)Math.Ceiling(totalPreference);
                if (randomNumber > rounded || isAttacked) continue;
                whoToAttack = target.Player.Status.PlaceAtLeaderBoard;
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }


            if (!isAttacked && isBlock == noBlock)
            {
                var players = allTargets.ToList();
                whoToAttack = players[_rand.Random(0, players.Count - 1)].Player.Status.PlaceAtLeaderBoard;

                if (maxRandomNumber > 0)
                    await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                        .SendMessageAsync(
                            $"**{bot.Character.Name}** Поставил блок, а ему нельзя. {randomNumber}/{maxRandomNumber} <= {totalPreference}\n" +
                            $"Round: {game.RoundNo}\n" +
                            $"Randomly Attacking {allTargets.Find(x => x.Player.Status.PlaceAtLeaderBoard == whoToAttack).Player.Character.Name}");

                await AttackPlayer(bot, whoToAttack);
            }
            else if (!isAttacked)
            {
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(
                    $"**{bot.Character.Name}** не напал ни на кого.\n" +
                    $"Round: {game.RoundNo}\n");
                await _gameReaction.HandleAttack(bot, null, -10);
            }

            ResetTens(allTargets);
        }
        catch (Exception e)
        {
            await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                .SendMessageAsync($"{e.Message}\n{e.StackTrace}");
            _logs.Critical(e.Message);
            _logs.Critical(e.StackTrace);
        }
    }

    public async Task<bool> AttackPlayer(GamePlayerBridgeClass bot, int whoToAttack)
    {
        return await _gameReaction.HandleAttack(bot, null, whoToAttack);
    }

    public void ResetTens(List<Nanobot> nanobots)
    {
        foreach (var p in nanobots) p.AttackPreference = 10;
    }


    public async Task HandleLvlUpBot(GamePlayerBridgeClass player, GameClass game)
    {
        do
        {
            int skillNumber;

            var intelligence = player.Character.GetIntelligence();
            var strength = player.Character.GetStrength();
            var speed = player.Character.GetSpeed();
            var psyche = player.Character.GetPsyche();

            var stats = new List<BiggestStatClass>
            {
                new(1, intelligence),
                new(2, strength),
                new(3, speed),
                new(4, psyche)
            };

            stats = stats.OrderByDescending(x => x.StatCount).ToList();

            if (stats.First().StatCount < 10)
                skillNumber = stats.First().StatIndex;
            else if (stats[1].StatCount < 10)
                skillNumber = stats[1].StatIndex;
            else if (stats[2].StatCount < 10)
                skillNumber = stats[2].StatIndex;
            else if (stats[3].StatCount < 10)
                skillNumber = stats[3].StatIndex;
            else
                skillNumber = 4;

            //game.RoundNo is 3 or 5 or 7 or 9

            if (player.Character.Name == "Толя" && strength < 8) skillNumber = 2;
            if (player.Character.Name == "Sirinoks" && intelligence < 10) skillNumber = 1;
            if (player.Character.Name == "Вампур" && psyche < 10) skillNumber = 4;
            if (player.Character.Name == "mylorik" && psyche < 10) skillNumber = 4;
            if (player.Character.Name == "Братишка" && intelligence < 10) skillNumber = 1;
            if (player.Character.Name == "LeCrisp" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Darksci" && psyche < 10) skillNumber = 4;

            if (player.Character.Name == "Mit*suki*" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Mit*suki*" && intelligence == 9) skillNumber = 1;
            if (player.Character.Name == "Mit*suki*" && strength == 10 && intelligence < 10) skillNumber = 1;

            if (player.Character.Name == "HardKitty" && speed < 10 && game.RoundNo < 6) skillNumber = 3;
            if (player.Character.Name == "HardKitty" && psyche < 10 && game.RoundNo > 6) skillNumber = 4;

            if (player.Character.Name == "Тигр" && psyche >= game.RoundNo) skillNumber = 1;
            if (player.Character.Name == "Тигр" && psyche < game.RoundNo) skillNumber = 4;

            if (player.Character.Name == "Глеб" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Глеб" && intelligence == 9) skillNumber = 1;

            if (player.Character.Name == "Загадочный Спартанец в маске" && psyche < 10 && game.RoundNo <= 3) skillNumber = 4;
            if (player.Character.Name == "Загадочный Спартанец в маске" && speed < 10 && game.RoundNo > 3) skillNumber = 3;


            await _gameReaction.HandleLvlUp(player, null, skillNumber);
        } while (player.Status.LvlUpPoints > 0);

        player.Status.MoveListPage = 1;
    }

    public class BiggestStatClass
    {
        public int StatCount;
        public int StatIndex;

        public BiggestStatClass(int statIndex, int statCount)
        {
            StatIndex = statIndex;
            StatCount = statCount;
        }
    }

    public class Nanobot
    {
        public decimal AttackPreference;

        public GamePlayerBridgeClass Player;


        public Nanobot(GamePlayerBridgeClass player)
        {
            Player = player;
            AttackPreference = 10;
        }

        public int PlaceAtLeaderBoard()
        {
            return Player.Status.PlaceAtLeaderBoard;
        }

        public Guid GetPlayerId()
        {
            return Player.Status.PlayerId;
        }
    }

    public class NanobotClass
    {
        public ulong GameId;
        public List<Nanobot> Nanobots = new();

        public NanobotClass(IReadOnlyList<GamePlayerBridgeClass> players)
        {
            GameId = players.First().GameId;
            foreach (var t in players) Nanobots.Add(new Nanobot(t));
        }
    }
}