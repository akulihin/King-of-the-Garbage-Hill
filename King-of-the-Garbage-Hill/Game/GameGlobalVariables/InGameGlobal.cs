using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameGlobalVariables;

public class InGameGlobal : IServiceSingleton
{
    private readonly SecureRandom _rand;
    public readonly List<WhenToTriggerClass> AwdkaAfkTriggeredWhen = new();

    public readonly List<Sirinoks.TrainingClass> AwdkaTeachToPlay = new();
    public readonly List<Awdka.TeachToPlayHistory> AwdkaTeachToPlayHistory = new();
    public readonly List<DeepList.Madness> AwdkaTeachToPlayTempStats = new();
    public readonly List<Awdka.TrollingClass> AwdkaTrollingList = new();
    public readonly List<Awdka.TryingClass> AwdkaTryingList = new();

    public readonly List<Darksci.LuckyClass> DarksciLuckyList = new();

    public readonly List<FriendsClass> DeepListDoubtfulTactic = new();
    public readonly List<DeepList.Madness> DeepListMadnessList = new();
    public readonly List<WhenToTriggerClass> DeepListMadnessTriggeredWhen = new();
    public readonly List<DeepList.Mockery> DeepListMockeryList = new();
    public readonly List<DeepList.SuperMindKnown> DeepListSupermindKnown = new();
    public readonly List<WhenToTriggerClass> DeepListSupermindTriggeredWhen = new();

    public readonly List<DeepList.Madness> GlebChallengerList = new();
    public readonly List<WhenToTriggerClass> GlebChallengerTriggeredWhen = new();
    public readonly List<Gleb.GlebSkipClass> GlebSkipList = new();
    public readonly List<WhenToTriggerClass> GlebSleepingTriggeredWhen = new();
    public readonly List<Gleb.GlebTeaClass> GlebTea = new();
    public readonly List<WhenToTriggerClass> GlebTeaTriggeredWhen = new();
    public readonly List<HardKitty.DoebatsyaClass> HardKittyDoebatsya = new();
    public readonly List<HardKitty.LonelinessClass> HardKittyLoneliness = new();

    public readonly List<HardKitty.MuteClass> HardKittyMute = new();
    public readonly List<LeCrisp.LeCrispAssassins> LeCrispAssassins = new();

    public readonly List<LeCrisp.LeCrispImpactClass> LeCrispImpact = new();


    public readonly List<LolGod.PushAndDieClass> LolGodPushAndDieSubList = new();
    public readonly List<LolGod.Udyr> LolGodUdyrList = new();

    public readonly List<Mitsuki.GarbageClass> MitsukiGarbageList = new();
    public readonly List<WhenToTriggerClass> MitsukiNoPcTriggeredWhen = new();


    public readonly List<Mylorik.MylorikRevengeClass> MylorikRevenge = new();
    public readonly List<Mylorik.MylorikSpanishClass> MylorikSpanish = new();
    public readonly List<Mylorik.MylorikSpartanClass> MylorikSpartan = new();

    public readonly List<BotsBehavior.NanobotClass> NanobotsList = new();


    public readonly List<Octopus.InkClass> OctopusInkList = new();
    public readonly List<Octopus.InvulnerabilityClass> OctopusInvulnerabilityList = new();
    public readonly List<Octopus.TentaclesClass> OctopusTentaclesList = new();

    public readonly List<FriendsClass> SharkBoole = new();
    public readonly List<Shark.SharkDontUnderstand> SharkDontUnderstand = new();
    public readonly List<Shark.SharkLeaderClass> SharkJawsLeader = new();
    public readonly List<FriendsClass> SharkJawsWin = new();

    public readonly List<Sirinoks.SirinoksFriendsClass> SirinoksFriendsAttack = new();
    public readonly List<FriendsClass> SirinoksFriendsList = new();
    public readonly List<Sirinoks.TrainingClass> SirinoksTraining = new();

    public readonly List<FriendsClass> SpartanFirstBlood = new();
    public readonly List<Spartan.TheyWontLikeIt> SpartanMark = new();
    public readonly List<FriendsClass> SpartanShame = new();

    public readonly List<Tigr.ThreeZeroClass> TigrThreeZeroList = new();
    public readonly List<Tigr.TigrTopClass> TigrTop = new();
    public readonly List<WhenToTriggerClass> TigrTopWhen = new();
    public readonly List<FriendsClass> TigrTwoBetterList = new();

    public readonly List<Tolya.TolyaCountClass> TolyaCount = new();
    public readonly List<FriendsClass> TolyaRammusTimes = new();
    public readonly List<Tolya.TolyaTalkedlClass> TolyaTalked = new();
    public readonly List<TutorialReactions.TutorialGame> Tutorials = new();

    public readonly List<Vampyr.HematophagiaClass> VampyrHematophagiaList = new();
    public readonly List<Vampyr.ScavengerClass> VampyrScavengerList = new();


    public InGameGlobal(SecureRandom rand)
    {
        _rand = rand;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    //TODO: 
    // 1) chnage IsManatory to 1 array and 1 loop instead of 3
    // 2) change the switch method to 1 loop
    public WhenToTriggerClass GetWhenToTrigger(GamePlayerBridgeClass player, bool isMandatory, int maxRandomNumber,
        int maxTimes, bool isMandatory2 = false, int lastRound = 10, bool isMandatory3 = false)
    {
        var toTriggerClass = new WhenToTriggerClass(player.Status.PlayerId, player.GameId);
        int when;
        var check = new List<int>();

        //probably only for mitsuki
        if (maxRandomNumber == 0)
        {
            when = _rand.Random(2, lastRound);
            if (lastRound >= 11 && when == 3 || when > 10) when = 10;
            toTriggerClass.WhenToTrigger.Add(when);
            return toTriggerClass;
        }


        if (isMandatory)
        {
            when = _rand.Random(1, lastRound);
            if (lastRound >= 11 && when == 3 || when > 10) when = 10;
            check.Add(when);
            toTriggerClass.WhenToTrigger.Add(when);
        }

        if (isMandatory2)
        {
            do
            {
                when = _rand.Random(1, lastRound);
                if (lastRound >= 11 && when == 3 || when > 10) when = 10;
            } while (check.Contains(when));

            check.Add(when);
            toTriggerClass.WhenToTrigger.Add(when);
        }

        if (isMandatory3)
        {
            do
            {
                when = _rand.Random(1, lastRound);
                if (lastRound == 11 && when == 3 || when > 10) when = 10;
            } while (check.Contains(when));

            check.Add(when);
            toTriggerClass.WhenToTrigger.Add(when);
        }

        var rand = _rand.Random(1, maxRandomNumber);
        var times = 0;

        switch (rand)
        {
            case 1 when maxTimes >= 1:
            {
                while (times < rand)
                {
                    when = _rand.Random(1, lastRound);
                    if (lastRound == 11 && when == 3 || when > 10) when = 10;

                    if (toTriggerClass.WhenToTrigger.All(x => x != when))
                    {
                        toTriggerClass.WhenToTrigger.Add(when);
                        times++;
                    }
                }

                break;
            }
            case 2 when maxTimes >= 2:
            {
                while (times < rand)
                {
                    when = _rand.Random(1, lastRound);
                    if (lastRound == 11 && when == 3 || when > 10) when = 10;

                    if (toTriggerClass.WhenToTrigger.All(x => x != when))
                    {
                        toTriggerClass.WhenToTrigger.Add(when);
                        times++;
                    }
                }

                break;
            }
            case 3 when maxTimes >= 3:
            {
                while (times < rand)
                {
                    when = _rand.Random(1, lastRound);
                    if (lastRound == 11 && when == 3 || when > 10) when = 10;

                    if (toTriggerClass.WhenToTrigger.All(x => x != when))
                    {
                        toTriggerClass.WhenToTrigger.Add(when);
                        times++;
                    }
                }

                break;
            }
            case 4 when maxTimes >= 4:
            {
                while (times < rand)
                {
                    when = _rand.Random(1, lastRound);
                    if (lastRound == 11 && when == 3 || when > 10) when = 10;

                    if (toTriggerClass.WhenToTrigger.All(x => x != when))
                    {
                        toTriggerClass.WhenToTrigger.Add(when);
                        times++;
                    }
                }

                break;
            }
        }

        return toTriggerClass;
    }


    public void CalculatePassiveChances(GameClass game)
    {
        foreach (var player in game.PlayersList)
        {
            var characterName = player.Character.Name;
            WhenToTriggerClass when;
            switch (characterName)
            {
                case "HardKitty":
                    HardKittyMute.Add(new HardKitty.MuteClass(player.Status.PlayerId, game.GameId));
                    HardKittyLoneliness.Add(
                        new HardKitty.LonelinessClass(player.Status.PlayerId, game.GameId));
                    HardKittyDoebatsya.Add(new HardKitty.DoebatsyaClass(player.Status.PlayerId,
                        game.GameId));
                    break;
                case "Осьминожка":
                    OctopusTentaclesList.Add(new Octopus.TentaclesClass(player.Status.PlayerId,
                        game.GameId));
                    break;
                case "Darksci":
                    DarksciLuckyList.Add(new Darksci.LuckyClass(player.Status.PlayerId,
                        game.GameId));
                    break;
                case "Бог ЛоЛа":
                    LolGodPushAndDieSubList.Add(
                        new LolGod.PushAndDieClass(player.Status.PlayerId, game.GameId, game.PlayersList));
                    LolGodUdyrList.Add(new LolGod.Udyr(player.Status.PlayerId, game.GameId));
                    break;
                case "Вампур":
                    VampyrHematophagiaList.Add(
                        new Vampyr.HematophagiaClass(player.Status.PlayerId, game.GameId));
                    VampyrScavengerList.Add(new Vampyr.ScavengerClass(player.Status.PlayerId,
                        game.GameId));
                    break;
                case "Sirinoks":
                    SirinoksFriendsList.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                    SirinoksFriendsAttack.Add(
                        new Sirinoks.SirinoksFriendsClass(player.Status.PlayerId, game.GameId));

                    break;
                case "Братишка":
                    SharkJawsLeader.Add(new Shark.SharkLeaderClass(player.Status.PlayerId,
                        game.GameId));
                    SharkDontUnderstand.Add(
                        new Shark.SharkDontUnderstand(player.Status.PlayerId, game.GameId));

                    SharkJawsWin.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                    SharkBoole.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                    break;
                case "Загадочный Спартанец в маске":


                    //Им это не понравится
                    Guid enemy1;
                    Guid enemy2;

                    do
                    {
                        var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                        enemy1 = game.PlayersList[randIndex].Status.PlayerId;
                        if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" or "Глеб" or "mylorik" or
                            "Загадочный Спартанец в маске")
                            enemy1 = player.Status.PlayerId;
                    } while (enemy1 == player.Status.PlayerId);

                    do
                    {
                        var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                        enemy2 = game.PlayersList[randIndex].Status.PlayerId;
                        if (game.PlayersList[randIndex].Character.Name is "Mit*suki*" or "Глеб" or "mylorik" or
                            "Загадочный Спартанец в маске")
                            enemy2 = player.Status.PlayerId;
                        if (enemy2 == enemy1)
                            enemy2 = player.Status.PlayerId;
                    } while (enemy2 == player.Status.PlayerId);

                    SpartanMark.Add(new Spartan.TheyWontLikeIt(player.Status.PlayerId, game.GameId, enemy1));
                    var Spartan = SpartanMark.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);
                    Spartan.FriendList.Add(enemy2);
                    //end Им это не понравится

                    SpartanShame.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                    SpartanFirstBlood.Add(new FriendsClass(player.Status.PlayerId, game.GameId));


                    break;
                case "DeepList":
                    DeepListDoubtfulTactic.Add(new FriendsClass(player.Status.PlayerId,
                        game.GameId));

                    when = GetWhenToTrigger(player, true, 6, 2, false, 6);
                    DeepListSupermindTriggeredWhen.Add(when);

                    when = GetWhenToTrigger(player, true, 6, 2);
                    DeepListMadnessTriggeredWhen.Add(when);

                    break;
                case "mylorik":
                    MylorikSpartan.Add(
                        new Mylorik.MylorikSpartanClass(player.Status.PlayerId, game.GameId));
                    MylorikSpanish.Add(
                        new Mylorik.MylorikSpanishClass(player.Status.PlayerId, game.GameId));
                    break;
                case "LeCrisp":
                    LeCrispAssassins.Add(new LeCrisp.LeCrispAssassins(player.Status.PlayerId,
                        game.GameId));
                    LeCrispImpact.Add(new LeCrisp.LeCrispImpactClass(player.Status.PlayerId,
                        game.GameId));
                    break;
                case "Тигр":
                    TigrTwoBetterList.Add(
                        new FriendsClass(player.Status.PlayerId, game.GameId));
                    when = GetWhenToTrigger(player, true, 10, 1);
                    TigrTopWhen.Add(when);
                    break;
                case "AWDKA":
                    when = GetWhenToTrigger(player, false, 10, 1);
                    AwdkaAfkTriggeredWhen.Add(when);
                    AwdkaTrollingList.Add(new Awdka.TrollingClass(player.Status.PlayerId, game.GameId));
                    AwdkaTeachToPlayHistory.Add(
                        new Awdka.TeachToPlayHistory(player.Status.PlayerId, game.GameId));
                    AwdkaTryingList.Add(new Awdka.TryingClass(player.Status.PlayerId, game.GameId));
                    break;

                case "Толя":
                    TolyaCount.Add(new Tolya.TolyaCountClass(game.GameId, player.Status.PlayerId));
                    TolyaTalked.Add(new Tolya.TolyaTalkedlClass(game.GameId, player.Status.PlayerId));
                    TolyaRammusTimes.Add(new FriendsClass(player.Status.PlayerId, game.GameId));
                    break;

                case "Mit*suki*":
                    when = GetWhenToTrigger(player, true, 0, 0);
                    MitsukiNoPcTriggeredWhen.Add(when);
                    break;

                case "Глеб":
                    GlebTea.Add(new Gleb.GlebTeaClass(player.Status.PlayerId, game.GameId));
                    //Спящее хуйло chance   
                    when = GetWhenToTrigger(player, true, 3, 3);
                    GlebSleepingTriggeredWhen.Add(when);

                    //Претендент русского сервера
                    var li = new List<int>();

                    foreach (var t in when.WhenToTrigger) li.Add(t);

                    bool flag;
                    do
                    {
                        when = GetWhenToTrigger(player, true, 6, 3, true, 12);
                        flag = false;
                        for (var i = 0; i < li.Count; i++)
                            if (when.WhenToTrigger.Contains(li[i]))
                                flag = true;
                    } while (flag);

                    GlebChallengerTriggeredWhen.Add(when);
                    //end Претендент русского сервера

                    break;
            }
        }
    }
}