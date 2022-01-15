using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameGlobalVariables;

public class InGameGlobal : IServiceSingleton
{
    private readonly Logs _logs;
    private readonly SecureRandom _rand;
    public readonly List<WhenToTriggerClass> AwdkaAfkTriggeredWhen = new();

    public readonly List<Sirinoks.TrainingClass> AwdkaTeachToPlay = new();
    public readonly List<Awdka.TeachToPlayHistory> AwdkaTeachToPlayHistory = new();
    public readonly List<DeepList.Madness> AwdkaTeachToPlayTempStats = new();
    public readonly List<Awdka.TrollingClass> AwdkaTrollingList = new();
    public readonly List<Awdka.TryingClass> AwdkaTryingList = new();

    public readonly List<CraboRack.BakoBoole> BtratishkaDontUnderstand = new();
    public readonly List<CraboRack.BakoBoole> CraboRackBakoBoole = new();
    public readonly List<CraboRack.Shell> CraboRackShell = new();
    public readonly List<DeepList.Madness> CraboRackSidewaysBooleList = new();


    public readonly List<WhenToTriggerClass> CraboRackSidewaysBooleTriggeredWhen = new();

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

    public InGameGlobal(SecureRandom rand, Logs logs)
    {
        _rand = rand;
        _logs = logs;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public WhenToTriggerClass GetWhenToTrigger(GamePlayerBridgeClass player, int mandatoryTimes, int maxAdditionalTimes,
        int range, int lastRound = 10, int firstRound = 1)
    {
        if (lastRound > 10)
        {
            _logs.Critical("CRIT: lastRound > 10");
            throw new IndexOutOfRangeException("lastRound > 10");
        }

        if (firstRound < 1)
        {
            _logs.Critical("CRIT: firstRound < 1");
            throw new IndexOutOfRangeException("firstRound < 1");
        }

        if (mandatoryTimes + maxAdditionalTimes > lastRound)
        {
            _logs.Critical("CRIT: mandatoryTimes + maxAdditionalTimes > lastRound");
            throw new IndexOutOfRangeException("mandatoryTimes + maxAdditionalTimes > lastRound");
        }

        if (maxAdditionalTimes > range)
        {
            _logs.Critical("CRIT: maxAdditionalTimes > range");
            throw new IndexOutOfRangeException("maxAdditionalTimes > range");
        }

        if (mandatoryTimes < 0 || range < 0 || maxAdditionalTimes < 0 || lastRound < 0)
        {
            _logs.Critical(
                $"CRIT: less than 0! mandatoryTimes=={mandatoryTimes}, range=={range}, maxAdditionalTimes=={maxAdditionalTimes}, lastRound=={lastRound}");
            throw new IndexOutOfRangeException(
                $"CRIT: less than 0! mandatoryTimes=={mandatoryTimes}, range=={range}, maxAdditionalTimes=={maxAdditionalTimes}, lastRound=={lastRound}");
        }


        var toTriggerClass = new WhenToTriggerClass(player.Status.PlayerId, player.GameId);
        int when;

        //mandatory times
        for (var i = 0; i < mandatoryTimes; i++)
            while (true)
            {
                when = _rand.Random(firstRound, lastRound);
                if (toTriggerClass.WhenToTrigger.Any(x => x == when)) continue;
                toTriggerClass.WhenToTrigger.Add(when);
                break;
            }
        //end mandatory times

        /*
        //additional times new
        var target = _rand.Random(1, range);
        for (var i = 0; i < maxAdditionalTimes; i++)
        {
            var rand = _rand.Random(1, range);
            if (rand != target) continue;

            while (true)
            {
                when = _rand.Random(firstRound, lastRound);
                if (toTriggerClass.WhenToTrigger.Any(x => x == when)) continue;
                toTriggerClass.WhenToTrigger.Add(when);
                break;
            }
        }
        //end additional times
        */


        //additional times old
        var target = _rand.Random(1, range);

        if (target > maxAdditionalTimes) return toTriggerClass;

        for (var i = 0; i < target; i++)
            while (true)
            {
                when = _rand.Random(firstRound, lastRound);
                if (toTriggerClass.WhenToTrigger.Any(x => x == when)) continue;
                toTriggerClass.WhenToTrigger.Add(when);
                break;
            }
        //end additional times

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
                    SharkJawsLeader.Add(new Shark.SharkLeaderClass(player.Status.PlayerId, game.GameId));
                    BtratishkaDontUnderstand.Add(new CraboRack.BakoBoole(player.Status.PlayerId, game.GameId));
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
                    DeepListDoubtfulTactic.Add(new FriendsClass(player.Status.PlayerId, game.GameId));

                    when = GetWhenToTrigger(player, 1, 2, 5, 6);
                    DeepListSupermindTriggeredWhen.Add(when);

                    when = GetWhenToTrigger(player, 2, 2, 5, 7, 3);
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
                    when = GetWhenToTrigger(player, 1, 1, 5, 8);
                    TigrTopWhen.Add(when);
                    break;
                case "AWDKA":
                    when = GetWhenToTrigger(player, 0, 1, 5);
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
                    when = GetWhenToTrigger(player, 1, 0, 0, 10, 2);
                    MitsukiNoPcTriggeredWhen.Add(when);
                    break;

                case "Глеб":
                    GlebTea.Add(new Gleb.GlebTeaClass(player.Status.PlayerId, game.GameId));
                    //Спящее хуйло chance   
                    when = GetWhenToTrigger(player, 1, 3, 3, 9);
                    GlebSleepingTriggeredWhen.Add(when);

                    //Претендент русского сервера
                    var li = new List<int>();

                    foreach (var t in when.WhenToTrigger) li.Add(t);

                    bool flag;
                    do
                    {
                        when = GetWhenToTrigger(player, 2, 3, 6);
                        flag = false;
                        for (var i = 0; i < li.Count; i++)
                            if (when.WhenToTrigger.Contains(li[i]))
                                flag = true;
                    } while (flag);

                    GlebChallengerTriggeredWhen.Add(when);
                    //end Претендент русского сервера

                    break;
                case "Краборак":
                    //Бокобуль
                    when = GetWhenToTrigger(player, 3, 3, 10);
                    CraboRackSidewaysBooleTriggeredWhen.Add(when);
                    //end Бокобуль

                    //Панцирь
                    CraboRackShell.Add(new CraboRack.Shell(player.Status.PlayerId, game.GameId));
                    CraboRackBakoBoole.Add(new CraboRack.BakoBoole(player.Status.PlayerId, game.GameId));
                    //end Панцирь
                    break;
            }
        }
    }
}