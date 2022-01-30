using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class BotsBehavior : IServiceSingleton
{
    private readonly InGameGlobal _gameGlobal;
    private readonly GameReaction _gameReaction;
    private readonly SecureRandom _rand;
    private readonly Global _global;
    private readonly Logs _logs;

    public BotsBehavior(SecureRandom rand, GameReaction gameReaction, InGameGlobal gameGlobal, Global @global, Logs logs)
    {
        _rand = rand;
        _gameReaction = gameReaction;
        _gameGlobal = gameGlobal;
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
        if (player.Status.MoveListPage == 3) await HandleLvlUpBot(player, game);
        if (player.Status.MoveListPage == 1) await HandleBotAttack(player, game);
    }

    public void HandleBotMoralForSkill(GamePlayerBridgeClass bot, GameClass game)
    {
        var moral = bot.Character.GetMoral();
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            if (bot.Character.Name == "Sirinoks" && game.RoundNo < 9)
            {
                if (bot.Status.PlaceAtLeaderBoard == 6 && moral < 21)
                {
                    return;
                }
                if (bot.Status.PlaceAtLeaderBoard is 5 or 4 && moral < 13)
                {
                    return;
                }
                if (bot.Status.PlaceAtLeaderBoard == 3 && moral < 8)
                {
                    return;
                }
            }

        }
        //end логика до 10го раунда

        //прожать всю момаль
        if (moral >= 20)
        {
            bot.Character.AddMoral(bot.Status, -20, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  114, "Обмен Морали");
        }

        if (moral >= 13)
        {
            bot.Character.AddMoral(bot.Status, -13, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  69, "Обмен Морали");
        }

        if (moral >= 8)
        {
            bot.Character.AddMoral(bot.Status, -8, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  39, "Обмен Морали");
        }

        if (moral >= 5)
        {
            bot.Character.AddMoral(bot.Status, -5, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  24, "Обмен Морали");
        }

        if (moral >= 3)
        {
            bot.Character.AddMoral(bot.Status, -3, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  14, "Обмен Морали");
        }

        if (moral >= 2)
        {
            bot.Character.AddMoral(bot.Status, -2, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  9, "Обмен Морали");
        }

        if (moral >= 1)
        {
            bot.Character.AddMoral(bot.Status, -1, "Обмен Морали", true, true);
            bot.Character.AddExtraSkill(bot.Status,  4, "Обмен Морали");
        }
        //end прожать всю момаль
    }

    public void HandleBotMoralForPoints(GamePlayerBridgeClass bot, GameClass game)
    {
        var moral = bot.Character.GetMoral();
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            //если хардкитти или осьминожка  или Вампур - всегда ждет 21 морали
            if (bot.Character.Name is "HardKitty" or "Осьминожка" or "Вампур")
                if (moral < 21)
                    return;

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.Character.Name == "Darksci")
            {
                if (game.RoundNo >= 6)
                    overwrite = true;
            }

            //если бот на последнем месте - ждет 21
            if (bot.Status.PlaceAtLeaderBoard == 6 && moral < 21 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.PlaceAtLeaderBoard == 5 && moral < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.PlaceAtLeaderBoard == 4 && moral < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.PlaceAtLeaderBoard == 3 && moral < 5 && !overwrite)
                return;
            //если бот на 2м месте то ждет 3
            if (bot.Status.PlaceAtLeaderBoard == 2 && moral < 3 && !overwrite)
                return;

        }
        //end логика до 10го раунда

        //прожать всю момаль
        if (moral >= 20)
        {
            bot.Character.AddMoral(bot.Status, -20, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(13);
        }

        if (moral >= 13)
        {
            bot.Character.AddMoral(bot.Status, -13, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(8);
        }

        if (moral >= 8)
        {
            bot.Character.AddMoral(bot.Status, -8, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(4);
        }

        if (moral >= 5)
        {
            bot.Character.AddMoral(bot.Status, -5, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(2);
        }

        if (moral >= 3)
        {
            bot.Character.AddMoral(bot.Status, -3, "Обмен Морали", true, true);
            bot.Character.AddBonusPointsFromMoral(1);
        }
        //end прожать всю момаль
    }

    public void HandleBotMoral(GamePlayerBridgeClass bot, GameClass game)
    {
        if (bot.Status.PlaceAtLeaderBoard == 1)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.Character.Name == "Sirinoks")
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.Character.Name == "Вампур" && game.RoundNo == 5)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }

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
           var deepList = _gameGlobal.DeepListMadnessTriggeredWhen.Find(x => x.PlayerId == bot.Status.PlayerId && x.GameId == game.GameId);
           if (deepList != null)
           {
               if (deepList.WhenToTrigger.Contains(game.RoundNo))
               {
                   HandleBotMoralForSkill(bot, game);
                   return;
               }
           }
        }

        if (bot.Character.Name == "mylorik")
        {
            var mylorikRevenge = _gameGlobal.MylorikRevenge.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
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
            var allTargets = _gameGlobal.NanobotsList.Find(x => x.GameId == game.GameId).Nanobots.Where(x => x.Player.Status.PlayerId != bot.Status.PlayerId).ToList();

            if (game.RoundNo == 10)
            {
                allTargets = allTargets.Where(x => x.Player.Character.Name != "Тигр").ToList();
            }

            if (game.RoundNo == 9 || game.RoundNo == 10)
            {
                if (game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                {
                    allTargets = allTargets.Where(x => x.Player.Character.Name != "Darksci").ToList();
                }
            }

            double maxRandomNumber = 0;
            var isBlock = allTargets.Count;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            var noBlock = 99999;
            //end local variables

            //character variables
            var DarksciTheOne = Guid.Empty;
            var AwdkaFirst = 0;
            //end character variables

            //calculation Tens
            foreach (var target in allTargets)
            {

                //if justice is the same
                if (bot.Character.Justice.GetJusticeNow() == target.Player.Character.Justice.GetJusticeNow())
                    target.AttackPreference -= 5;
                //if bot justice less than platers
                else if (bot.Character.Justice.GetJusticeNow() < target.Player.Character.Justice.GetJusticeNow())
                    target.AttackPreference -= 7;

                //if player is first
                if (target.Player.Status.PlaceAtLeaderBoard == 1)
                    target.AttackPreference -= 1;

                //if player is second when we are first
                if (bot.Status.PlaceAtLeaderBoard == 1 && target.Player.Status.PlaceAtLeaderBoard == 2)
                    target.AttackPreference -= 1;


                //bug: WARNING!
                //IMPORTANT:
                //mylorik и Глеб имеют эту же запись у себя, где они это меняют
                //если меняешь что-то тут, то нужно менять у mylorik и Глеб руками!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                //если на прошлом бою враг был toogood
                //-= 7
                if (bot.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId && x.IsTooGoodEnemy))
                    target.AttackPreference -= 7;
                else if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGoodMe))
                    target.AttackPreference -= 7;
                //если на прошлом ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId &&
                             x.IsStatsBetterEnemy))
                    target.AttackPreference -= 5;
                //если на прошлом-1 ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 2 && x.EnemyId == target.Player.Status.PlayerId &&
                             x.IsStatsBetterEnemy))
                    target.AttackPreference -= 5;
                //bug: warning ends here.


                //won and too good
                if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGoodEnemy))
                    target.AttackPreference += 4;


                //how many players are attacking the same player
                var count = allTargets.FindAll(x => x.Player.Status.WhoToAttackThisTurn == target.Player.Status.PlayerId)
                    .Count;
                target.AttackPreference -= count;


                //target
                if (target.AttackPreference >= 5)
                    if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                        target.AttackPreference += 1;

                //contr
                if (target.AttackPreference >= 5)
                    if (bot.Character.GetWhoIContre() == target.Player.Character.GetSkillClass())
                        target.AttackPreference += 3;


                //justice diff
                if (allTargets.All(x => x.Player.Character.Justice.GetJusticeNow() < bot.Character.Justice.GetJusticeNow()))
                    target.AttackPreference += bot.Character.Justice.GetJusticeNow() - target.Player.Character.Justice.GetJusticeNow();

                //custom bot behavior
                switch (bot.Character.Name)
                {

                    case "AWDKA":
                        
                        //авдка получает +1 к преференсу на всех всегда.
                        //target.AttackPreference += 1;
                        //На первом ходу нападает на того, кто имет максимально высокий стат. (например 10 силы)
                        
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

                        
                        var awdkaTrying = _gameGlobal.AwdkaTryingList.Find(x => x.PlayerId == bot.Status.PlayerId && game.GameId == x.GameId);
                        if (awdkaTrying != null)
                        {
                            var awdkaTryingTarget = awdkaTrying.TryingList.Find(x => x.EnemyPlayerId == target.Player.Status.PlayerId);
                            if (awdkaTryingTarget != null)
                            {
                                //-2 тем, на ком есть стак платины(до тех пор, пока он еще не на всех)
                                if (awdkaTrying.TryingList.Count(x => x.IsUnique) < 5)
                                {
                                    if (awdkaTryingTarget.IsUnique)
                                        target.AttackPreference -= 2;
                                }

                                //Если номер хода < = 5: +3 ко всем, на ком есть стак бронзы, но еще нет стака платины
                                if (game.RoundNo <= 5)
                                {
                                    if (!awdkaTryingTarget.IsUnique)
                                        target.AttackPreference += 3;
                                }
                            }
                        }

                        
                        //Авдака запоминает на каком ходу была последняя победа над каждым из игроков. дальше формула: Преференс - 3 + Номера текущего хода - Номер хода последней победы
                        var triggered = false;
                        for (var i = target.Player.Status.WhoToLostEveryRound.Count - 1; i >= 0; i--)
                        {
                            var won = target.Player.Status.WhoToLostEveryRound[0];
                            if (won.EnemyId == bot.Status.PlayerId)
                            {
                                target.AttackPreference = target.AttackPreference * ((game.RoundNo - won.RoundNo)/2);
                                triggered = true;
                                break;
                            }
                        }
                        if (!triggered)
                        {
                            target.AttackPreference = target.AttackPreference * (game.RoundNo / 2);
                        }
                        

                        break;
                    case "HardKitty":
                        if (game.RoundNo < 5)
                        {
                            mandatoryAttack = allTargets.First().PlaceAtLeaderBoard();
                        }

                        if (target.PlaceAtLeaderBoard() == 1)
                        {
                            target.AttackPreference += 1;
                        }

                        break;
                    case "Darksci":
                        if (game.RoundNo < 8)
                        {
                            if (target.Player.Character.Justice.GetJusticeNow() > bot.Character.Justice.GetJusticeNow())
                            {
                                target.AttackPreference -= 3;
                            }
                        }

                        var darksciLucky = _gameGlobal.DarksciLuckyList.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                        if (darksciLucky != null)
                        {
                            if (!darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId))
                            {
                                if (target.AttackPreference > 1)
                                {
                                    target.AttackPreference += 3;
                                    if (game.RoundNo < 5)
                                    {
                                        target.AttackPreference += 3;
                                    }
                                }

                            }

                            if (!darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId) && darksciLucky.TouchedPlayers.Count == 4)
                            {
                                target.AttackPreference = 0;
                            }

                            // Если ОДИН из тех, на ком не запрокан стак, уже побеждал даркси ПО СТАТАМ, то его значение = 0.
                            if (darksciLucky.TouchedPlayers.Count != 5)
                            {
                                if (!darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId))
                                {
                                    var darksciLuckyTheOne = bot.Status.WhoToLostEveryRound.Find(x => x.EnemyId == target.Player.Status.PlayerId && x.IsStatsBetterEnemy);
                                    if (darksciLuckyTheOne != null && DarksciTheOne == Guid.Empty)
                                    {
                                        DarksciTheOne = target.Player.Status.PlayerId;
                                        target.AttackPreference = 0;
                                    }
                                }
                            }

                            //Если незапроканных стаков = кол-во оставшихся ходов - 3, то выбирает цель только из них. (пока не останется 1) 

                            var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                            var roundsLeft = 11 - (game.RoundNo + 3);
                            if (notTouched >= roundsLeft)
                            {
                                if (darksciLucky.TouchedPlayers.Count < 5)
                                {
                                    if (darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId))
                                    {
                                        target.AttackPreference = 0;
                                    }
                                }
                            }


                            if (game.RoundNo == 7 && bot.Character.GetPsyche() < 4 && darksciLucky.TouchedPlayers.Count != 5)
                            {
                                if (!darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId))
                                {
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                                }
                            }
                            if (game.RoundNo >= 8 && darksciLucky.TouchedPlayers.Count != 5)
                            {
                                if (!darksciLucky.TouchedPlayers.Contains(target.Player.Status.PlayerId))
                                {
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                                }
                            }
                        }

                        break;
                    case "Mit*suki*":
                        if (target.AttackPreference >= 5)
                        {
                            if (bot.Character.GetCurrentSkillClassTarget() == target.Player.Character.GetSkillClass())
                            {
                                target.AttackPreference += 3;
                            }

                            if (game.RoundNo < 5 && target.Player.Character.Name == "HardKitty")
                            {
                                target.AttackPreference = 0;
                            }

                            if (game.RoundNo > 5 && target.Player.Character.Name == "HardKitty" && target.AttackPreference >= 5)
                            {
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }
                        }
                        break;
                    case "mylorik":
                        var mylorikRevenge = _gameGlobal.MylorikRevenge.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                        if (mylorikRevenge != null)
                        {
                            var revengeEnemy = mylorikRevenge.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == target.Player.Status.PlayerId);
                            var totalFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => !x.IsUnique).Count;
                            var totalNotFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;


                            if (revengeEnemy != null)
                            {
                                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), то х2 преф
                                if (revengeEnemy.IsUnique)
                                {
                                    var leftRound = 11 - game.RoundNo;
                                    if (totalNotFinishedRevenges >= leftRound)
                                    {
                                        target.AttackPreference *= 3;
                                    }
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
                                if (!revengeEnemy.IsUnique && totalFinishedRevenges < 5)
                                {
                                    target.AttackPreference -= 4;
                                }


                                if (game.RoundNo > 5)
                                {
                                    //после 5го хода: преф игроков С Лузом но БЕЗ победы не может опуститься ниже 4 
                                    if (revengeEnemy.IsUnique)
                                    {
                                        if (target.AttackPreference < 4)
                                            target.AttackPreference = 4;
                                    }
                                }
                            }
                            else
                            {

                                //на первых 4х ходах если у врага больше справедливости и не запрокана ни одна метка, то преф +2 * разницу в вашей справедливости ( с положительным знаком)
                                if (game.RoundNo <= 4)
                                {
                                    if (target.Player.Character.Justice.GetJusticeNow() > bot.Character.Justice.GetJusticeNow())
                                    {
                                        target.AttackPreference += 2 * (target.Player.Character.Justice.GetJusticeNow() - bot.Character.Justice.GetJusticeNow());
                                    }
                                }

                                //Если на врагах еще не запрокан луз мести - их преференс +5-игроки с запроканым лузом или победой. 
                                target.AttackPreference += 5 - mylorikRevenge.EnemyListPlayerIds.Count;

                                //Первые 4 хода: + 17 тем у кого справедливости больше чем у тебя, если на них не запрокан луз мести.
                                if (game.RoundNo <= 4 && bot.Character.Justice.GetJusticeNow() < target.Player.Character.Justice.GetJusticeNow())
                                    target.AttackPreference += 17;
                            }
                        }


                        //"-5 за more stats" и "-7 за toogood" из базовых условий десяток   / 1 + кол-во стаков сломанного щита
                        if (game.RoundNo >= 5)
                        {
                            var mylorikSpartan = _gameGlobal.MylorikSpartan.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                            if (mylorikSpartan != null)
                            {
                                var spartanEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.Player.Status.PlayerId);
                                if (spartanEnemy != null)
                                {
                                    //mylorik это делит тортиками
                                    //если на прошлом бою враг был toogood
                                    //-= 7
                                    if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId && x.IsTooGoodEnemy))
                                        target.AttackPreference += 7 - 7 / (1 + spartanEnemy.LostTimes);
                                    else if (target.Player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId && x.IsTooGoodMe))
                                        target.AttackPreference += 7 - 7 / (1 + spartanEnemy.LostTimes);
                                    //если на прошлом ты проиграл И у врага больше статов
                                    //-= 5
                                    else if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId && x.IsStatsBetterEnemy))
                                        target.AttackPreference += 5 - 5 / (1 + spartanEnemy.LostTimes);
                                    //если на прошлом-1 ты проиграл И у врага больше статов
                                    //-= 5
                                    else if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 2 && x.EnemyId == target.Player.Status.PlayerId && x.IsStatsBetterEnemy))
                                        target.AttackPreference += 5 - 5 / (1 + spartanEnemy.LostTimes);
                                }
                            }
                        }


                        break;
                    case "Краборак":

                        if (allTargets.Any(x => x.PlaceAtLeaderBoard() >= 4 && x.AttackPreference > 0))
                        {
                            if (target.PlaceAtLeaderBoard() < 4)
                            {
                                target.AttackPreference -= 4;
                            }
                        }


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
                            if (bot.Character.GetCurrentSkillClassTarget() != target.Player.Character.GetSkillClass())
                                target.AttackPreference -= 3;

                        var siriFriends = _gameGlobal.SirinoksFriendsList.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                        if (siriFriends != null)
                        {
                            //+5 к значению тех, кто еще не друг.
                            if (!siriFriends.FriendList.Contains(target.Player.Status.PlayerId) &&
                                target.AttackPreference > 0)
                                target.AttackPreference += 5;


                            //До начала 6го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 6)
                            {
                                var sirisFried = allTargets.Find(x => x.Player.Status.PlayerId == siriFriends.FriendList.First());

                                if (target.Player.Status.PlayerId != sirisFried.Player.Status.PlayerId)
                                {
                                    target.AttackPreference = 0;
                                }
                                else
                                {
                                    if (target.AttackPreference > 3)
                                        mandatoryAttack = sirisFried.Player.Status.PlaceAtLeaderBoard;
                                    else
                                        target.AttackPreference = 0;
                                }
                            }


                            //Если кол-во оставшихся ходов == кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                            var nonFiendsLeft = 5 - siriFriends.FriendList.Count;
                            var roundsLeft = 11 - game.RoundNo;
                            var allNotFriends = allTargets.FindAll(x => !siriFriends.FriendList.Contains(x.Player.Status.PlayerId));


                            if (nonFiendsLeft >= roundsLeft)
                                if (allNotFriends is { Count: > 0 })
                                    mandatoryAttack = allNotFriends.FirstOrDefault().Player.Status.PlaceAtLeaderBoard;
                        }

                        if (game.RoundNo == 1 && target.Player.Character.Name == "Осьминожка")
                        {
                            target.AttackPreference = 0;
                        }
                        break;
                    case "Толя":
                        //Jew
                        foreach (var v in allTargets)
                            if (v.Player.Status.WhoToAttackThisTurn == target.Player.Status.PlayerId)
                                target.AttackPreference += 6;
                        //end Jew
                        break;

                    case "LeCrisp":
                        //Jew
                        foreach (var v in allTargets)
                            if (v.Player.Status.WhoToAttackThisTurn == target.Player.Status.PlayerId)
                                target.AttackPreference += 6;
                        //end Jew
                        break;


                    case "Глеб":

                        if (target.Player.Status.IsSkip)
                            target.AttackPreference = 0;

                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = _gameGlobal.GlebChallengerTriggeredWhen.Find(x =>
                            x.PlayerId == bot.Status.PlayerId && game.GameId == x.GameId);

                        if (glebAcc != null)
                            if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                            {
                                //глеб это анулирует
                                //если на прошлом бою враг был toogood
                                //-= 7
                                if (bot.Status.WhoToLostEveryRound.Any(x =>
                                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId &&
                                        x.IsTooGoodEnemy))
                                    target.AttackPreference += 7;
                                else if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.Status.PlayerId &&
                                             x.IsTooGoodMe))
                                    target.AttackPreference += 7;
                                //если на прошлом ты проиграл И у врага больше статов
                                //-= 5
                                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId &&
                                             x.IsStatsBetterEnemy))
                                    target.AttackPreference += 5;
                                //если на прошлом-1 ты проиграл И у врага больше статов
                                //-= 5
                                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 2 && x.EnemyId == target.Player.Status.PlayerId &&
                                             x.IsStatsBetterEnemy))
                                    target.AttackPreference += 5;

                                //Под претендентом автоматически выбирает цель с наибольшим значением. 
                                var sorted = allTargets.OrderByDescending(x => x.AttackPreference).ToList();
                                mandatoryAttack = sorted.First().Player.Status.PlaceAtLeaderBoard;
                            }

                        break;
                    case "Загадочный Спартанец в маске":
                        break;
                    case "Вампур":
                        if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.Player.Status.PlayerId))
                        {
                            target.AttackPreference = 0;
                        }
                        break;
                }
                //end custom bot behavior


                //custom enemy
                switch (target.Player.Character.Name)
                {
                    case "Darksci":
                        if (game.RoundNo == 9)
                        {
                            if (target.AttackPreference > 5)
                            {
                                target.AttackPreference += 5;
                            }
                            if (target.AttackPreference > 7)
                            {
                                target.AttackPreference += 5;
                            }
                        }

                        if (game.GetAllGlobalLogs().Contains($"Толя запизделся и спалил, что {target.Player.DiscordUsername} - {target.Player.Character.Name}"))
                        {
                            if (target.AttackPreference > 5)
                            {
                                target.AttackPreference += 5;
                            }
                            if (target.AttackPreference > 7)
                            {
                                target.AttackPreference += 5;
                            }
                        }

                        if (game.PlayersList.Any(x => x.Character.Name == "mylorik"))
                        {
                            var mylorik = game.PlayersList.Find(x => x.Character.Name == "mylorik");
                            if (game.GetAllGlobalLogs().Contains($"{target.Player.DiscordUsername} психанул") && game.GetAllGlobalLogs().Contains($"{mylorik.DiscordUsername} психанул"))
                            {
                                if (target.AttackPreference > 5)
                                {
                                    target.AttackPreference += 5;
                                }
                                if (target.AttackPreference > 7)
                                {
                                    target.AttackPreference += 5;
                                }
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
                        if (bot.Character.Justice.GetJusticeNow() <= target.Player.Character.Justice.GetJusticeNow())
                            target.AttackPreference += 3;
                        break;
                    case "Глеб":
                        var glebChallender = _gameGlobal.GlebChallengerTriggeredWhen.Find(x => x.GameId == game.GameId && x.PlayerId == target.Player.Status.PlayerId);
                        var glebSleeping = _gameGlobal.GlebSleepingTriggeredWhen.Find(x => x.GameId == game.GameId && x.PlayerId == target.Player.Status.PlayerId);

                        var totalChallengers = 0;
                        var totalSleeps = 0;

                        for (var i = 1; i < game.RoundNo + 1; i++)
                        {
                            if (glebChallender.WhenToTrigger.Contains(i))
                            {
                                totalChallengers++;
                            }
                        }
                        for (var i = 1; i < game.RoundNo + 1; i++)
                        {
                            if (glebSleeping.WhenToTrigger.Contains(i))
                            {
                                totalSleeps++;
                            }
                        }

                        if (totalChallengers >= 1 && totalSleeps >= 2)
                        {
                            target.AttackPreference /= 2;
                        }

                        break;
                }
                //end custom enemy


                if (target.AttackPreference <= 0)
                {
                    isBlock--;
                    target.AttackPreference = 0;
                }

                maxRandomNumber += target.AttackPreference;
            }
            //end calculation Tens

            //custom behaviour After calculation Tens
            switch (bot.Character.Name)
            {
                case "AWDKA":
                    isBlock = noBlock;
                    break;
                case "Darksci":
                    minimumRandomNumberForBlock += 1;
                    if (game.RoundNo > 1 && bot.Character.Justice.GetJusticeNow() == 0)
                    {
                        minimumRandomNumberForBlock += 1;
                    }

                    if (game.RoundNo is 3 or 5 or 9)
                    {
                        if (bot.Character.GetPsyche() <= 1)
                        {
                            minimumRandomNumberForBlock = 4;
                            maximumRandomNumberForBlock = 5;
                        }
                    }

                    var darksciLucky = _gameGlobal.DarksciLuckyList.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                    if (darksciLucky != null)
                    {
                        var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                        var roundsLeft = 11 - (game.RoundNo + 3);
                        if (notTouched >= roundsLeft)
                        {
                            if (darksciLucky.TouchedPlayers.Count < 5)
                            {
                                if (mandatoryAttack == -1)
                                {
                                    var listofTargets = allTargets.Where(x => !darksciLucky.TouchedPlayers.Contains(x.Player.Status.PlayerId)).ToList();

                                    if (listofTargets.Count > 0)
                                        mandatoryAttack = listofTargets.First().PlaceAtLeaderBoard();
                                }
                            }
                        }
                    }

                    break;
                case "Братишка":
                    if (bot.Character.Justice.GetJusticeNow() != 5)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    var min = allTargets.Min(x => x.Player.Character.Justice.GetJusticeNow());
                    var check = allTargets.Find(x =>
                        x.Player.Character.Justice.GetJusticeNow() == min);

                    if (check.Player.Character.Justice.GetJusticeNow() >= bot.Character.Justice.GetJusticeNow())
                    {
                        minimumRandomNumberForBlock += 1;
                    }

                    break;
                case "Осьминожка":
                    isBlock = noBlock;
                    break;
                case "HardKitty":
                    isBlock = noBlock;
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);

                    if (allTargets.All(x => x.AttackPreference <= 3) && mandatoryAttack == -1)
                    {

                        var doebatsya = hardKitty.LostSeries.Where(x => allTargets.Any(y => y.Player.Status.PlayerId == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).First();
                        mandatoryAttack = allTargets.Find(x => x.Player.Status.PlayerId == doebathsyaTarget.EnemyPlayerId).PlaceAtLeaderBoard();
                    }

                    if (allTargets.Any(x => x.AttackPreference > 5) && mandatoryAttack == -1)
                    {
                        var doebathsyaTargets = allTargets.Where(x => x.AttackPreference > 5).OrderByDescending(x => x.AttackPreference).ToList();
                        var doebatsya = hardKitty.LostSeries.Where(x => doebathsyaTargets.Any(y => y.Player.Status.PlayerId == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).First();
                        mandatoryAttack = allTargets.Find(x => x.Player.Status.PlayerId == doebathsyaTarget.EnemyPlayerId).PlaceAtLeaderBoard();
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
                    //Если кол-во врагов с запроканным лузом но без победы = кол-во оставшихся ходов, то преференс ДРУГИХ врагов / 2
                    var mylorikRevenge = _gameGlobal.MylorikRevenge.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                    if (mylorikRevenge != null)
                    {

                        var totalFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                        var roundsLeft = 11 - game.RoundNo;
                        if (totalFinishedRevenges >= roundsLeft)
                        {
                            maxRandomNumber = 0;
                            foreach (var target in allTargets)
                            {
                                if (!mylorikRevenge.EnemyListPlayerIds.Any(x => x.IsUnique && x.EnemyPlayerId == target.Player.Status.PlayerId))
                                {
                                    if (target.AttackPreference >= 2)
                                        target.AttackPreference /= 2;
                                }
                                maxRandomNumber += target.AttackPreference;
                            }
                        }
                    }

                    break;
                case "Sirinoks":
                    if (game.RoundNo is 10 or 1)
                        isBlock = noBlock;
                    else if (_gameGlobal.SirinoksTraining.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId) == null)
                    {
                        var siriFriends = _gameGlobal.SirinoksFriendsList.Find(x => x.GameId == game.GameId && x.PlayerId == bot.Status.PlayerId);
                        var siriFriend = allTargets.Find(x => x.Player.Status.PlayerId == siriFriends?.FriendList.FirstOrDefault());
                        if (siriFriend.Player.Character.Name != "Осьминожка")
                        {
                            mandatoryAttack = siriFriend.Player.Status.PlaceAtLeaderBoard;
                        }
                    }
                    break;

                case "Загадочный Спартанец в маске":
                    //на последнем ходу блок -2 (от 2 до 5)
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

            double totalPreference = 0;
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
                    await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync($"**{bot.Character.Name}** Поставил блок, а ему нельзя. {randomNumber}/{maxRandomNumber} <= {totalPreference}\n" +
                    $"Round: {game.RoundNo}\n" +

                    $"Randomly Attacking {allTargets.Find(x => x.Player.Status.PlaceAtLeaderBoard == whoToAttack).Player.Character.Name}");

                await AttackPlayer(bot, whoToAttack);
            }
            else if (!isAttacked)
            {
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync($"**{bot.Character.Name}** не напал ни на кого.\n" +
                    $"Round: {game.RoundNo}\n");
                await _gameReaction.HandleAttack(bot, null, -10);
            }

            ResetTens(allTargets);
        }
        catch (Exception e)
        {
            await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync($"{e.Message}\n{e.StackTrace}");
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

            if (player.Character.Name == "Mit*suki*" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Братишка" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "LeCrisp" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Darksci" && psyche < 10) skillNumber = 4;
            if (player.Character.Name == "Тигр" && psyche < 10 && player.Status.PlaceAtLeaderBoard == 1)
                skillNumber = 1;
            if (player.Character.Name == "Тигр" && psyche < 10 && player.Status.PlaceAtLeaderBoard != 1)
                skillNumber = 4;
            //if (player.Character.Name == "mylorik" && speed < 10 && game.RoundNo <= 3) skillNumber = 3;
            if (player.Character.Name == "mylorik" && psyche < 10) skillNumber = 4;
            if (player.Character.Name == "Глеб" && strength < 10) skillNumber = 2;
            if (player.Character.Name == "Загадочный Спартанец в маске" && psyche < 10 && game.RoundNo <= 3)
                skillNumber = 4;
            if (player.Character.Name == "Загадочный Спартанец в маске" && speed < 10 && game.RoundNo > 3)
                skillNumber = 3;


            await _gameReaction.HandleLvlUp(player, null, skillNumber);
        } while (player.Status.LvlUpPoints > 1);

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
        public double AttackPreference;

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