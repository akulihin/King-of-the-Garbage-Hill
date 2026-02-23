using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
// ReSharper disable RedundantAssignment
#pragma warning disable CS0219


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
        if (game.RoundNo > 10)
        {
            await _gameReaction.HandleAttack(player, null, -10);
            return;
        }
        
        //if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых") && game.RoundNo > 10)
        //{
        //    return;
        //}

        await HandleBotMoral(player, game);

        if (player.Status.LvlUpPoints > 0)
            await HandleLvlUpBot(player, game);

        // Kira bot: write Death Note and use Shinigami Eyes
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Тетрадь смерти"))
            HandleBotKira(player, game);

        await HandleBotAttack(player, game);
    }

    public async Task HandleBotMoralForSkill(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            if (bot.GameCharacter.Name == "Sirinoks")
            {
                if (game.RoundNo == 9)
                {
                    overwrite = true;
                }
            }

            if (bot.GameCharacter.Name == "DeepList")
            {
                var deepList = bot.Passives.DeepListMadnessTriggeredWhen;
                if (deepList != null)
                    if (deepList.WhenToTrigger.Contains(game.RoundNo))
                    {
                        overwrite = true;
                    }
            }

            //если хардкитти или осьминожка  или Вампур - всегда ждет 20 морали
            if (bot.GameCharacter.Name is "HardKitty" or "Осьминожка" or "Вампур")
                if (bot.GameCharacter.GetMoral() < 20)
                    return;

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.GameCharacter.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.GetPlaceAtLeaderBoard() == 6 && bot.GameCharacter.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.GetPlaceAtLeaderBoard() == 5 && bot.GameCharacter.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.GetPlaceAtLeaderBoard() == 4 && bot.GameCharacter.GetMoral() < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.GetPlaceAtLeaderBoard() == 3 && bot.GameCharacter.GetMoral() < 5 && !overwrite)
                return;
            //если бот на 2м месте то ждет 3
            if (bot.Status.GetPlaceAtLeaderBoard() == 2 && bot.GameCharacter.GetMoral() < 3 && !overwrite)
                return;
        }
        //end логика до 10го раунда

        //прожать всю момаль
        while (bot.GameCharacter.GetMoral() >= 1)
        {
            await _gameReaction.HandleMoralForSkill(bot);
        }
        //end прожать всю момаль
    }

    public async Task HandleBotMoralForPoints(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            //видвик не меняет мораль до самого конца игры, на 10м меняет всё на очки
            if (bot.GameCharacter.Name == "Weedwick")
            {
                return;
            }

            //если Осьминожка - всегда ждет 20 морали
            if (bot.GameCharacter.Name is "Осьминожка" or "HardKitty")
            {
                return;
            }


            if (bot.GameCharacter.Name is "Вампур")
            {
                if (bot.Status.GetPlaceAtLeaderBoard() == 6)
                    return;

                if (bot.Status.GetPlaceAtLeaderBoard() <= 2)
                {
                    if (bot.GameCharacter.GetMoral() < 13)
                        return;
                    overwrite = true;
                }
                else
                {
                    if (bot.GameCharacter.GetMoral() < 20)
                        return;

                    overwrite = true;
                }
            }
            

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.GameCharacter.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.GetPlaceAtLeaderBoard() == 6 && bot.GameCharacter.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.GetPlaceAtLeaderBoard() == 5 && bot.GameCharacter.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.GetPlaceAtLeaderBoard() == 4 && bot.GameCharacter.GetMoral() < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.GetPlaceAtLeaderBoard() == 3 && bot.GameCharacter.GetMoral() < 5 && !overwrite)
                return;
        }
        //end логика до 10го раунда



        //прожать всю момаль
        while (bot.GameCharacter.GetMoral() >= 5)
        {
            await _gameReaction.HandleMoralForScore(bot);
        }
        //end прожать всю момаль
    }

    public async Task HandleBotMoral(GamePlayerBridgeClass bot, GameClass game)
    {
        if (bot.Status.GetPlaceAtLeaderBoard() <= 2)
        {
            if (bot.GameCharacter.GetMoral() < 5)
            {
                await HandleBotMoralForSkill(bot, game);
                return;
            }
        }

        if (bot.GameCharacter.Name == "Dopa")
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Стая Гоблинов")
        {
            // Goblins prefer points for mine income and ziggurat building
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Котики")
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Продавец Сомнительных Тактик")
        {
            var sellerV = bot.Passives.SellerVparitGovna;
            if (sellerV.Cooldown <= 0)
            {
                // Prefer attacking unmarked players to spread marks
                await HandleBotMoralForPoints(bot, game);
            }
            else
            {
                await HandleBotMoralForSkill(bot, game);
            }
            return;
        }

        if (bot.GameCharacter.Name == "Салдорум")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Napoleon Wonnafcuk")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Sirinoks")
        {
            //логика до 10го раунда
            await HandleBotMoralForSkill(bot, game);
            //end логика до 10го раунда
            return;
        }

        /*if (bot.GameCharacter.Name == "Вампур" && game.RoundNo == 5)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }*/
        //If LeCrisp 10 psy and Place > 5, use all Score, use all Skill
        if (bot.GameCharacter.Name == "LeCrisp" && bot.GameCharacter.GetPsyche() >= 10 && game.RoundNo >= 5 && bot.Status.GetPlaceAtLeaderBoard() <= 4)
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }
        if (bot.GameCharacter.Name == "LeCrisp" && game.RoundNo <= 5)
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Загадочный Спартанец в маске")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "DeepList")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "mylorik")
        {
            var mylorikRevenge = bot.Passives.MylorikRevenge;
            if (mylorikRevenge != null)
            {
                var totalNotFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                var totalRevenges = mylorikRevenge.EnemyListPlayerIds.Count;

                //если на всех уже был запрокан луз или победа, то меняет мораль на скилл
                if (totalRevenges == 5)
                {
                    await HandleBotMoralForSkill(bot, game);
                    return;
                }

                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), меняет всю мораль на скилл
                var roundsLeft = 11 - game.RoundNo;
                if (totalNotFinishedRevenges >= roundsLeft)
                {
                    await HandleBotMoralForSkill(bot, game);
                    return;
                }
            }
        }


        await HandleBotMoralForPoints(bot, game);
    }


    private void HandleBotKira(GamePlayerBridgeClass bot, GameClass game)
    {
        var dn = bot.Passives.KiraDeathNote;
        var eyes = bot.Passives.KiraShinigamiEyes;

        // Use Shinigami Eyes if moral >= 25 and not already active (25% chance)
        if (bot.GameCharacter.GetMoral() >= 25 && !eyes.EyesActiveForNextAttack && _rand.Luck(1, 4))
        {
            bot.GameCharacter.AddMoral(-25, "Глаза бога смерти");
            eyes.EyesActiveForNextAttack = true;
            bot.Status.AddInGamePersonalLogs("Глаза бога смерти: Активированы!\n");
        }

        // Write Death Note if not already written this round
        if (dn.CurrentRoundTarget == Guid.Empty)
        {
            var candidates = game.PlayersList
                .Where(x => x.GetPlayerId() != bot.GetPlayerId()
                            && !x.Passives.KiraDeathNoteDead
                            && !x.Passives.KratosIsDead
                            && !dn.FailedTargets.Contains(x.GetPlayerId()))
                .ToList();

            if (candidates.Count > 0)
            {
                var target = candidates[_rand.Random(0, candidates.Count - 1)];
                dn.CurrentRoundTarget = target.GetPlayerId();

                // Bot has low intelligence about characters — pick from revealed or guess randomly
                var revealed = eyes.RevealedPlayers.Find(rp => rp == target.GetPlayerId());
                if (revealed != Guid.Empty && revealed != default)
                {
                    // Know the name from Shinigami Eyes — find it
                    var revealedPlayer = game.PlayersList.Find(x => x.GetPlayerId() == revealed);
                    dn.CurrentRoundName = revealedPlayer?.GameCharacter.Name ?? "???";
                }
                else
                {
                    // Guess randomly from known character names
                    var allNames = game.PlayersList.Select(x => x.GameCharacter.Name).Distinct().ToList();
                    dn.CurrentRoundName = allNames[_rand.Random(0, allNames.Count - 1)];
                }
            }
        }
    }

    public async Task HandleBotAttack(GamePlayerBridgeClass bot, GameClass game)
    {
        try
        {
            //local variables
            var allTargets = game!.NanobotsList.Find(x => x.GameId == game.GameId)!.Nanobots.Where(x => x.GetPlayerId() != bot.GetPlayerId()).ToList();

            if (game.RoundNo == 10)
            {
                foreach (var target in allTargets.ToList().Where(target => target.Player.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят")))
                {
                    allTargets.Remove(target);
                }
            }

            if (game.RoundNo is 9 or 10)
            {
                if (game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                {
                    foreach (var target in allTargets.ToList().Where(target => target.Player.GameCharacter.GetPsyche() <= 0 && target.Player.GameCharacter.Passive.Any(x => x.PassiveName == "Не повезло")))
                    {
                        allTargets.Remove(target);
                    }
                }
            }

            if (allTargets.Count < 4)
            {
                var testBoole = 0;
            }
            
            decimal maxRandomNumber = 0;
            var isBlock = allTargets.Count;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            var noBlock = 99999;
            var yesBlock = -99999;
            var botJustice = bot.GameCharacter.Justice.GetRealJusticeNow();
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
            var darksciTheOne = Guid.Empty;
            var awdkaFirst = 0;
            decimal spartanTarget = 0;
            //end character variables

            //local varaibles
            var isTargetTooGoodNumber = 7;
            var isLostLastRoundAndTargetIsBetterNumber = 5;
            var isJusticeTheSameNumber = 5;
            var isJusticeLessThanTargetNumber = 7;
            var isTargetFirstNumber = 1;
            var isTargetSecondWhenBotFirstNumber = 1;
            var isBotWonAndTooGoodNumber = 4;
            var isTargetNemesisNumber = 3;
            var isTargetTaretNumber = 1;
            var isTargetTooGood = false;
            var isJusticeTheSame = false;
            var isJusticeLessThanTarget = false;
            var isTargetFirst = false;
            var isTargetSecondWhenBotFirst = false;
            var isLostLastRoundAndTargetIsBetter = false;
            var isBotWonAndTooGood = false;
            var isTargetTaret = false;
            var isTargetNemesis = false;
            var howManyAttackingTheSameTarget = 0;
            var justiceDifference = 0;
            //

            //calculation Tens
            foreach (var target in allTargets)
            {
                var targetJustice = target.Player.GameCharacter.Justice.GetSeenJusticeNow();

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
                if (target.Player.Status.GetPlaceAtLeaderBoard() == 1)
                {
                    target.AttackPreference -= isTargetFirstNumber;
                    isTargetFirst = true;
                }

                //if player is second when we are first
                if (bot.Status.GetPlaceAtLeaderBoard() == 1 && target.Player.Status.GetPlaceAtLeaderBoard() == 2)
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
                    if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                    {
                        target.AttackPreference += isTargetTaretNumber;
                        isTargetTaret = true;
                    }

                //nemesis
                if (target.AttackPreference >= 5)
                    if (bot.GameCharacter.HasNemesisOver(target.Player.GameCharacter))
                    {
                        target.AttackPreference += isTargetNemesisNumber;
                        isTargetNemesis = true;
                    }


                //justice diff
                if (allTargets.All(x => x.Player.GameCharacter.Justice.GetSeenJusticeNow() < botJustice))
                {
                    justiceDifference = botJustice - targetJustice;
                    target.AttackPreference += justiceDifference;
                }

                //custom bot behavior
                switch (bot.GameCharacter.Name)
                {
                    case "Weedwick":
                        //bongs
                        target.AttackPreference *= target.Player.GameCharacter.GetWinStreak()*2;
                        //weed
                        target.AttackPreference += target.Player.Passives.WeedwickWeed;
                        //верхний этаж
                        target.AttackPreference += 6 - target.PlaceAtLeaderBoard();

                        //преференс врагов выше видвика по таблице + 3 (поставь это сразу перед умножением на 20 за волка)
                        if (bot.Status.GetPlaceAtLeaderBoard() > target.PlaceAtLeaderBoard())
                        {
                            target.AttackPreference += 3;
                        }

                        //wuf
                        if (target.Player.GameCharacter.Justice.GetRealJusticeNow() == 0)
                             target.AttackPreference *= 20;

                        if (target.Player.GameCharacter.Name == "DeepList")
                            target.AttackPreference = 0;
                        break;
                    case "DeepList":
                        var deepListMadness = bot.Passives.DeepListMadnessTriggeredWhen;
                        if (deepListMadness.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        if (target.Player.GameCharacter.Name == "Weedwick")
                            target.AttackPreference = 0;
                        break;
                    case "Тигр":

                        var tigr = bot.Passives.TigrThreeZeroList;
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
                            if (target.Player.GameCharacter.GetIntelligence() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetIntelligence();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetStrength() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetStrength();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetSpeed() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetSpeed();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetPsyche() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetPsyche();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }
                        }


                        var awdkaTrying = bot.Passives.AwdkaTryingList;
 
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

                        var darksciLucky = bot.Passives.DarksciLuckyList;
    
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
                                    if (darksciLuckyTheOne != null && darksciTheOne == Guid.Empty)
                                    {
                                        darksciTheOne = target.GetPlayerId();
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


                            if (game.RoundNo == 7 && bot.GameCharacter.GetPsyche() < 4 &&
                                darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                            if (game.RoundNo >= 8 && darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                        

                        break;
                    case "Злой Школьник":
                        if (target.AttackPreference >= 5)
                        {
                            if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                                target.AttackPreference += 3;

                            if (game.RoundNo < 5 && target.Player.GameCharacter.Name == "HardKitty")
                                target.AttackPreference = 0;

                            if (game.RoundNo > 5 && target.Player.GameCharacter.Name == "HardKitty" &&
                                target.AttackPreference >= 5) mandatoryAttack = target.PlaceAtLeaderBoard();
                        }

                        break;
                    case "mylorik":
                        var mylorikRevenge = bot.Passives.MylorikRevenge;
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
                            var mylorikSpartan = bot.Passives.MylorikSpartan;

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


                        if (target.Player.GameCharacter.Name == "HardKitty") target.AttackPreference -= 1;

                        break;

                    case "Братишка":
                        if (target.PlaceAtLeaderBoard() == bot.Status.GetPlaceAtLeaderBoard() + 1 ||
                            target.PlaceAtLeaderBoard() == bot.Status.GetPlaceAtLeaderBoard() - 1)
                        {
                            if (target.AttackPreference > 1) target.AttackPreference += 2;

                            if (target.AttackPreference >= 5) target.AttackPreference += 3;
                        }

                        break;
                    case "Sirinoks":

                        //После первого хода:  преференс -3 всем, кто не подходит под текущую мишень (мишень скилла).
                        if (game.RoundNo > 1)
                        {
                            if (!bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                            {
                                target.AttackPreference -= 3;
                            }
                            else
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        var siriFriends = bot.Passives.SirinoksFriendsList;

            
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
                                        mandatoryAttack = target.Player.Status.GetPlaceAtLeaderBoard();
                                    }
                                    else
                                    {
                                        target.AttackPreference = 0;
                                    }

                                    if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
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
                                    mandatoryAttack = allNotFriends.FirstOrDefault().Player.Status.GetPlaceAtLeaderBoard();
                        

                        if (game.RoundNo == 1 && target.Player.GameCharacter.Name == "Осьминожка")
                            target.AttackPreference = 0;
                        break;
                    case "Толя":

                        var tolyaCount = bot.Passives.TolyaCount;

                        if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == target.GetPlayerId()))
                        {
                            if (target.AttackPreference >= 5)
                            {
                                target.AttackPreference *= 2;
                                target.AttackPreference += 7;
                            }
                            else
                            {
                                target.AttackPreference *= 2;
                            }
                            
                            
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
                        if (target.Player.Passives.GlebTeaTriggeredWhen.WhenToTrigger.Contains(game.RoundNo))
                        {
                            target.AttackPreference = 0;
                        }

                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = bot.Passives.GlebChallengerTriggeredWhen;

                       
                        if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (isTargetTooGood)
                                target.AttackPreference += isTargetTooGoodNumber;
                            else if (isLostLastRoundAndTargetIsBetter)
                                target.AttackPreference += isLostLastRoundAndTargetIsBetterNumber;

                            //Под претендентом автоматически выбирает цель с наибольшим значением. 
                            var sorted = allTargets.OrderByDescending(x => x.AttackPreference).ToList();
                            mandatoryAttack = sorted.First().Player.Status.GetPlaceAtLeaderBoard();
                        }

                        break;
                    case "Загадочный Спартанец в маске":
                        /*if (game.RoundNo == 10)
                            if (target.Player.GameCharacter.Name == "Sirinoks")
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        */

                        var spartanMark = bot.Passives.SpartanMark;
                        var spartanShame = bot.Passives.SpartanShame;
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
                                    if (bot.GameCharacter.Justice.GetRealJusticeNow() > targetJustice && !isTargetTooGood)
                                        if (target.AttackPreference > spartanTarget)
                                        {
                                            mandatoryAttack = target.PlaceAtLeaderBoard();
                                            spartanTarget = target.AttackPreference;
                                        }
                                }
                            }
                        }


                        break;
                    case "Рик Санчез":
                        // Giant Beans — prioritize ingredient targets
                        var rickBeans = bot.Passives.RickGiantBeans;
                        if (rickBeans.IngredientsActive && rickBeans.IngredientTargets.Contains(target.GetPlayerId()))
                            target.AttackPreference += 10;

                        // Portal Gun — target #1 player when charged
                        var rickGun = bot.Passives.RickPortalGun;
                        if (rickGun.Invented && rickGun.Charges > 0 && target.PlaceAtLeaderBoard() == 1)
                            mandatoryAttack = target.PlaceAtLeaderBoard();

                        break;
                    case "Итачи":
                        // Prefer enemies with most crows (easier kills via Amaterasu)
                        var itachiCrows = bot.Passives.ItachiCrows;
                        if (itachiCrows.CrowCounts.TryGetValue(target.GetPlayerId(), out var crows) && crows > 0)
                            target.AttackPreference += crows * 3;
                        // Prefer enemies with low speed (Amaterasu auto-win)
                        if (target.Player.GameCharacter.GetSpeed() < bot.GameCharacter.GetSpeed())
                            target.AttackPreference += 5;
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

                        var vampyrHematophagiaList = bot.Passives.VampyrHematophagiaList;
                        

                        if (vampyrHematophagiaList.HematophagiaCurrent.Count < 5)
                        {
                            if (vampyrHematophagiaList.HematophagiaCurrent.Any(x => x.EnemyId == target.GetPlayerId()))
                            {
                                    //Вампур получает -3 всем на ком запрокан укус, если укусов еще не 5.
                                    target.AttackPreference -= 3;
                            }
                        }
                        

                        if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId()))
                            target.AttackPreference = 0;
                        break;
                    case "Салдорум":
                        if (bot.Passives.SaldorumKhokholList.MarkedEnemies.Contains(target.GetPlayerId())
                            || target.Player.GameCharacter.Name is "mylorik" or "Sirinoks")
                            target.AttackPreference += 5;
                        if (target.Player.GameCharacter.Name == "mylorik")
                            target.AttackPreference += 3;
                        break;
                    case "Napoleon Wonnafcuk":
                        var napBotAlliance = bot.Passives.NapoleonAlliance;
                        if (napBotAlliance.AllyId != Guid.Empty)
                        {
                            if (target.GetPlayerId() == napBotAlliance.AllyId)
                                target.AttackPreference -= 20;
                            var napBotAlly = game.PlayersList.Find(x => x.GetPlayerId() == napBotAlliance.AllyId);
                            if (napBotAlly != null && napBotAlly.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                target.AttackPreference += 10;
                        }
                        break;
                    case "Таинственный Суппорт":
                        var supportMark = bot.Passives.SupportPremade;
                        if (supportMark.MarkedPlayerId != Guid.Empty)
                        {
                            if (target.GetPlayerId() == supportMark.MarkedPlayerId)
                                target.AttackPreference += 15;
                        }
                        break;

                    case "Стая Гоблинов":
                        // Prefer attacking mine positions (1, 2, 6) for resource gain
                        if (target.PlaceAtLeaderBoard() is 1 or 2 or 6)
                            target.AttackPreference += 5;
                        // Avoid attacking much stronger targets (goblins are fragile)
                        if (target.Player.GameCharacter.GetStrength() > bot.GameCharacter.GetStrength() + 3)
                            target.AttackPreference -= 3;
                        break;

                    case "Котики":
                        // Prefer attacking enemies with cats on them (cat return bonus)
                        if (target.Player.Passives.KotikiCatOwnerId == bot.GetPlayerId())
                            target.AttackPreference += 20;
                        // Slightly prefer weaker enemies (better chance of winning for Штормяк)
                        if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                            target.AttackPreference += 3;
                        break;

                    case "Монстр без имени":
                        // Prefer enemies with high Justice (to steal via Близнец block)
                        var monsterTargetJustice = target.Player.GameCharacter.Justice.GetSeenJusticeNow();
                        if (monsterTargetJustice > 0) target.AttackPreference += monsterTargetJustice * 2;
                        break;
                }
                //end custom bot behavior


                //custom enemy
                switch (target.Player.GameCharacter.Name)
                {
                    case "Darksci":
                        if (game.RoundNo == 9)
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.GetAllGlobalLogs()
                            .Contains(
                                $"Толя запизделся и спалил, что {target.Player.DiscordUsername} - {target.Player.GameCharacter.Name}"))
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.PlayersList.Any(x => x.GameCharacter.Name == "mylorik"))
                        {
                            var mylorik = game.PlayersList.Find(x => x.GameCharacter.Name == "mylorik");
                            if (game.GetAllGlobalLogs().Contains($"{target.Player.DiscordUsername} психанул") &&
                                game.GetAllGlobalLogs().Contains($"{mylorik.DiscordUsername} психанул"))
                            {
                                if (target.AttackPreference > 5) target.AttackPreference += 5;
                                if (target.AttackPreference > 7) target.AttackPreference += 5;
                            }
                        }

                        if (bot.GameCharacter.Name is "DeepList" or "AWDKA")
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
                        //если есть старый глеб в игре, то толя бот кидает подсчет на 9м ходу на глеба, если место Толи в лидерборде <=3 (от топ1 до топ3) 
                        if (game.RoundNo == 9 && bot.GameCharacter.Name == "Толя" && bot.Status.GetPlaceAtLeaderBoard() <= 3)
                        {
                            var tolyaCount = bot.Passives.TolyaCount;
                            if (tolyaCount.IsReadyToUse)
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        }


                        var glebChallender = target.Player.Passives.GlebChallengerTriggeredWhen;
                        var glebSleeping = target.Player.Passives.GlebSleepingTriggeredWhen;

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

                //для всех ботов: если бот предположил братишку, то -1 преференс для врагов, которые рядом с братишкой по таблице. например братишка на 3м месте, значит нам нужно 3 - 1 и 3 +1 = 2 и 4. им преференс -1
                if (bot.Predict.Any(x => x.CharacterName == "Братишка"))
                {
                    var shark = game.PlayersList.Find(x => x.GetPlayerId() == bot.Predict.Find(x => x.CharacterName == "Братишка").PlayerId);
                    //6-5 = 1
                    //3-4 = -1
                    //5-4 = 1
                    //1-2 = -1
                    var placeDiff = target.PlaceAtLeaderBoard() - shark.Status.GetPlaceAtLeaderBoard();
                    if (placeDiff is 1 or -1)
                    {
                        target.AttackPreference -= 1;
                    }
                }

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


                switch (bot.GameCharacter.Name)
                {
                    case "mylorik":
                        //Если кол-во врагов с запроканным лузом но без победы = кол-во оставшихся ходов, то преференс ДРУГИХ врагов / 2
                        var mylorikRevenge = bot.Passives.MylorikRevenge;
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
                    if (!bot.IsTeamMember(game, target3.GetPlayerId()))
                        continue;

                    var realAttackPreference = target3.AttackPreference;
                    target3.AttackPreference = 0;

                    //custom bot behavior in teams. target == team member
                    switch (bot.GameCharacter.Name)
                    {
                        case "DeepList":
                            var deepListMockeryList = bot.Passives.DeepListMockeryList;

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
                            var darksciLucky = bot.Passives.DarksciLuckyList;
                            if (darksciLucky != null)
                            {
                               if(!darksciLucky.TouchedPlayers.Contains(target3.GetPlayerId()))
                                   target3.AttackPreference = realAttackPreference;
                            }
                            break;
                        case "Злой Школьник":
                            break;
                        case "mylorik":
                            var mylorikRevenge = bot.Passives.MylorikRevenge;
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
                            var siriFriends = bot.Passives.SirinoksFriendsList;
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                if (siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    mandatoryAttack = target3.PlaceAtLeaderBoard();

                            }
                            else if (game.RoundNo < 5)
                            {
                                var teammates = game.GetTeammates(bot);
                                mandatoryAttack = game.PlayersList.Find(x => x.GetPlayerId() == teammates[0]).Status.GetPlaceAtLeaderBoard();
                            }
                            else
                            {
                                //. снимается ноль с тех, кто не в друзьях.
                                if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    target3.AttackPreference = realAttackPreference;

                                //снимается 0 со всех тех, кто подходит под мишень.
                                if (bot.GameCharacter.HasSkillTargetOn(target3.Player.GameCharacter))
                                    target3.AttackPreference = realAttackPreference;

                                //если кол-во оставшихся ходов - 3 <= союзникам в друзьях, то нападает только на союзников которых можно добавить в друзья. (выбирает из них по мишени. если под мишень не подходит, то выбирает рандомно)
                                if (game.RoundNo < 9)
                                {
                                    if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                        if (bot.GameCharacter.HasSkillTargetOn(target3.Player.GameCharacter))
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
                            var vampyrHematophagiaList = bot.Passives.VampyrHematophagiaList;

                            if (vampyrHematophagiaList != null)
                            {
                                if (vampyrHematophagiaList.HematophagiaCurrent.All(x => x.EnemyId != target3.GetPlayerId()))
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
                    if (bot.IsTeamMember(game, target4.GetPlayerId()))
                        continue;

                    //custom bot behavior in teams. target == enemy
                    switch (bot.GameCharacter.Name)
                    {
                        case "AWDKA":
                            var platCount = 0;
                            var teamCount = 0;

                            // 0 на всех врагов, нападает только на союзников, чтобы проебать им, пока платина не будет на всех союзниках, либо пока не наступит 7ой ход
                            var awdkaTrying = bot.Passives.AwdkaTryingList;
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
            switch (bot.GameCharacter.Name)
            {
                case "Тигр":
                    var tigr = bot.Passives.TigrThreeZeroList;
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
                        if (bot.GameCharacter.GetPsyche() <= 1)
                        {
                            minimumRandomNumberForBlock = 4;
                            maximumRandomNumberForBlock = 5;
                        }

                    var darksciLucky = bot.Passives.DarksciLuckyList;
         
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

                    var min = allTargets.Min(x => x.Player.GameCharacter.Justice.GetSeenJusticeNow());
                    var check = allTargets.Find(x =>
                        x.Player.GameCharacter.Justice.GetSeenJusticeNow() == min);

                    if (check.Player.GameCharacter.Justice.GetSeenJusticeNow() >= botJustice)
                        minimumRandomNumberForBlock += 1;

                    break;
                case "Осьминожка":
                    isBlock = noBlock;
                    break;
                case "HardKitty":
                    isBlock = noBlock;
                    var hardKitty = bot.Passives.HardKittyDoebatsya;

                    if (allTargets.All(x => x.AttackPreference <= 3) && mandatoryAttack == -1)
                    {
                        var doebatsya = hardKitty.LostSeriesCurrent
                            .Where(x => allTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).FirstOrDefault();
                        if (doebathsyaTarget != null)
                            mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                                ?.PlaceAtLeaderBoard() ?? mandatoryAttack;
                    }

                    if (allTargets.Any(x => x.AttackPreference > 5) && mandatoryAttack == -1)
                    {
                        var doebathsyaTargets = allTargets.Where(x => x.AttackPreference > 5)
                            .OrderByDescending(x => x.AttackPreference).ToList();
                        var doebatsya = hardKitty.LostSeriesCurrent
                            .Where(x => doebathsyaTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).FirstOrDefault();
                        if (doebathsyaTarget != null)
                            mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                                ?.PlaceAtLeaderBoard() ?? mandatoryAttack;
                    }


                    break;
                case "Глеб":
                    isBlock = noBlock;
                    break;
                case "Краборак":
                    isBlock = noBlock;
                    break;
                case "Злой Школьник":
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

                case "Итачи":
                    isBlock = noBlock;
                    break;

                case "Toxic Mate":
                    isBlock = noBlock;
                    break;

                case "DeepList":
                    var deepList = bot.Passives.DeepListMadnessTriggeredWhen;
                    if (deepList.WhenToTrigger.Contains(game.RoundNo))
                    {
                        isBlock = noBlock;
                    }
                    break;

                case "Sirinoks":
                    if (game.RoundNo is 10 or 1)
                    {
                        isBlock = noBlock;
                    }
                    /*
                    else if (bot.Passives.SirinoksTraining.Training.Count == 0)
                    {
                        var siriFriends = bot.Passives.SirinoksFriendsList;
                        var siriFriend = allTargets.Find(x => x.GetPlayerId() == siriFriends?.FriendList.FirstOrDefault());
                        if(siriFriend != null)
                            if (siriFriend.Player.GameCharacter.Name != "Осьминожка")
                                mandatoryAttack = siriFriend.Player.Status.GetPlaceAtLeaderBoard();
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
                            bot.GameCharacter.Justice.GetRealJusticeNow() <=
                            x.Player.GameCharacter.Justice.GetSeenJusticeNow()))
                        if (game.RoundNo == 10)
                        {
                            minimumRandomNumberForBlock = 2;
                            maximumRandomNumberForBlock = 4;
                        }

                    // end на последнем ходу блок -2 (от 2 до 5)
                    break;

                case "Рик Санчез":
                    var rickPickle = bot.Passives.RickPickle;
                    var rickGun2 = bot.Passives.RickPortalGun;
                    // Never block when pickle is active or on penalty cooldown
                    if (rickPickle.PickleTurnsRemaining > 0 || rickPickle.PenaltyTurnsRemaining > 0)
                        isBlock = noBlock;
                    // If portal gun is charged, never block — always attack
                    else if (rickGun2.Invented && rickGun2.Charges > 0)
                        isBlock = noBlock;
                    // Block when 3+ opponents likely to attack (low stats scenario)
                    else if (bot.GameCharacter.GetStrength() + bot.GameCharacter.GetSpeed() + bot.GameCharacter.GetPsyche() <= 6)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    else
                        isBlock = noBlock;
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

                    var tolyaCount = bot.Passives.TolyaCount;

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
                        .FindAll(x => bot.GameCharacter.GetStrength() - x.Player.GameCharacter.GetStrength() <= -2).Count;

                    minimumRandomNumberForBlock += assassinsCount;

                    //end block chances
                    break;

                case "Стая Гоблинов":
                    // Build ziggurat when conditions are met
                    var gobBotPop = bot.Passives.GoblinPopulation;
                    var gobBotZig = bot.Passives.GoblinZiggurat;
                    var gobBotPlace = bot.Status.GetPlaceAtLeaderBoard();
                    if (gobBotPop.Warriors >= 1 && gobBotPop.Hobs >= 1 && gobBotPop.Workers >= 1 &&
                        bot.Status.GetScore() >= 3 && !gobBotZig.BuiltPositions.Contains(gobBotPlace))
                    {
                        isBlock = yesBlock; // Force block to build ziggurat
                    }
                    break;

                case "Котики":
                    // Block to trigger Штормяк taunt if there are still untaunted enemies
                    var kotikiStorm = bot.Passives.KotikiStorm;
                    var untaunted = game.PlayersList.Count(p =>
                        p.GetPlayerId() != bot.GetPlayerId() &&
                        !kotikiStorm.TauntedPlayers.Contains(p.GetPlayerId()));
                    if (untaunted > 0)
                        isBlock = yesBlock;
                    break;

                case "Монстр без имени":
                    // Default: block (trigger Близнец justice steal)
                    minimumRandomNumberForBlock = 3;
                    maximumRandomNumberForBlock = 4;
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
                whoToAttack = target.Player.Status.GetPlaceAtLeaderBoard();
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }


            if (!isAttacked && isBlock == noBlock)
            {
                var players = allTargets.ToList();
                whoToAttack = players[_rand.Random(0, players.Count - 1)].Player.Status.GetPlaceAtLeaderBoard();

                if (maxRandomNumber > 0)
                    await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340)
                        .SendMessageAsync(
                            $"**{bot.GameCharacter.Name}** Поставил блок, а ему нельзя. {randomNumber}/{maxRandomNumber} <= {totalPreference}\n" +
                            $"Round: {game.RoundNo}\n" +
                            $"Randomly Attacking {allTargets.Find(x => x.Player.Status.GetPlaceAtLeaderBoard() == whoToAttack).Player.GameCharacter.Name}");

                await AttackPlayer(bot, whoToAttack);
            }
            else if (!isAttacked)
            {
                var passives = bot.GameCharacter.Passive.Aggregate("(", (current, passive) => current + $"{passive.PassiveName}, ");
                passives = passives.Remove(passives.Length - 2);
                passives += ")";
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(
                    $"**{bot.GameCharacter.Name}** {passives} не напал ни на кого.\n" +
                    $"Round: {game.RoundNo}\n");
                await _gameReaction.HandleAttack(bot, null, -10);
            }

            // Dopa Макро — bot needs second attack
            if (isAttacked && !bot.Status.IsReady
                && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
            {
                var secondTargets = allTargets.Where(x =>
                    !bot.Status.WhoToAttackThisTurn.Contains(x.Player.GetPlayerId())).ToList();
                if (secondTargets.Any())
                {
                    var pick = secondTargets[_rand.Random(0, secondTargets.Count - 1)];
                    await AttackPlayer(bot, pick.Player.Status.GetPlaceAtLeaderBoard());
                }
                else if (allTargets.Any())
                {
                    var pick = allTargets[_rand.Random(0, allTargets.Count - 1)];
                    await AttackPlayer(bot, pick.Player.Status.GetPlaceAtLeaderBoard());
                }
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

            var intelligence = player.GameCharacter.GetIntelligence();
            var strength = player.GameCharacter.GetStrength();
            var speed = player.GameCharacter.GetSpeed();
            var psyche = player.GameCharacter.GetPsyche();

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

            if (player.GameCharacter.Name == "Толя" && strength < 8) skillNumber = 2;
            if (player.GameCharacter.Name == "Sirinoks" && intelligence < 10) skillNumber = 1;
            if (player.GameCharacter.Name == "Вампур" && psyche < 10) skillNumber = 4;
            if (player.GameCharacter.Name == "mylorik" && psyche < 10) skillNumber = 4;
            if (player.GameCharacter.Name == "Братишка" && strength < 10) skillNumber = 2;

            if (player.GameCharacter.Name == "LeCrisp" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Darksci" && psyche < 10) skillNumber = 4;

            if (player.GameCharacter.Name == "Злой Школьник" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Злой Школьник" && intelligence == 9) skillNumber = 1;
            if (player.GameCharacter.Name == "Злой Школьник" && strength == 10 && intelligence < 10) skillNumber = 1;

            if (player.GameCharacter.Name == "HardKitty" && speed < 10 && game.RoundNo < 6) skillNumber = 3;
            if (player.GameCharacter.Name == "HardKitty" && psyche < 10 && game.RoundNo > 6) skillNumber = 4;

            if (player.GameCharacter.Name == "Тигр" && psyche >= game.RoundNo && intelligence < 10) skillNumber = 1;
            if (player.GameCharacter.Name == "Тигр" && psyche < game.RoundNo) skillNumber = 4;

            if (player.GameCharacter.Name == "Глеб" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Глеб" && intelligence == 9) skillNumber = 1;

            if (player.GameCharacter.Name == "Weedwick")
            {
                if(speed < 5) skillNumber = 3;
                else if (psyche < 10) skillNumber = 4;
                else skillNumber = 1;
            }

            if (player.GameCharacter.Name == "Загадочный Спартанец в маске" && psyche < 10 && game.RoundNo <= 3) skillNumber = 4;
            if (player.GameCharacter.Name == "Загадочный Спартанец в маске" && speed < 10 && game.RoundNo > 3) skillNumber = 3;

            // Rick Sanchez — prioritize INT for portal gun invention (30+ INT needed)
            if (player.GameCharacter.Name == "Рик Санчез") skillNumber = 1;

            // Itachi — prioritize Intelligence, then Psyche
            if (player.GameCharacter.Name == "Итачи" && intelligence < 10) skillNumber = 1;
            if (player.GameCharacter.Name == "Итачи" && intelligence >= 10 && psyche < 10) skillNumber = 4;

            // Продавец — with 10x multiplier: prioritize INT for skill, then PSY, STR, SPD
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Закуп"))
            {
                if (intelligence < 10) skillNumber = 1;
                else if (psyche < 10) skillNumber = 4;
                else if (strength < 10) skillNumber = 2;
                else skillNumber = 3;
            }

            // Dopa — INT-focused build
            if (player.GameCharacter.Name == "Dopa")
            {
                if (intelligence < 10) skillNumber = 1;
                else if (psyche < 10) skillNumber = 4;
                else if (speed < 10) skillNumber = 3;
                else skillNumber = 2;
            }

            // Салдорум — PSY-focused build
            if (player.GameCharacter.Name == "Салдорум" && psyche < 10) skillNumber = 4;

            // Napoleon — PSY-focused build
            if (player.GameCharacter.Name == "Napoleon Wonnafcuk" && psyche < 10) skillNumber = 4;

            // Стая Гоблинов — balanced upgrade approach
            if (player.GameCharacter.Name == "Стая Гоблинов")
            {
                var gobPop = player.Passives.GoblinPopulation;
                if (game.RoundNo <= 5 && gobPop.WarriorUpgradeLevel < 4)
                    skillNumber = 2; // Warriors early (more combat power)
                else if (gobPop.WorkerUpgradeLevel < 2)
                    skillNumber = 3; // Workers mid (more resources)
                else if (!gobPop.FestivalUsed)
                    skillNumber = 4; // Double population late (only if not used yet)
                else if (gobPop.WarriorUpgradeLevel < 4)
                    skillNumber = 2; // Fall back to warriors
                else if (gobPop.WorkerUpgradeLevel < 4)
                    skillNumber = 3; // Fall back to workers
                else
                    skillNumber = 1; // All upgrades maxed, pick hob rate
            }

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
            return Player.Status.GetPlaceAtLeaderBoard();
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