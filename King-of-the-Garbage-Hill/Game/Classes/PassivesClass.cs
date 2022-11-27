using System;
using System.Linq;
using System.Security.Cryptography;
using King_of_the_Garbage_Hill.Game.Characters;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class PassivesClass 
{

    public Sirinoks.TrainingClass AwdkaTeachToPlay { get; set; } = new();
    public Awdka.TeachToPlayHistory AwdkaTeachToPlayHistory { get; set; } = new();
    public DeepList.Madness AwdkaTeachToPlayTempStats { get; set; } = new();
    public Awdka.TrollingClass AwdkaTrollingList { get; set; } = new();
    public Awdka.TryingClass AwdkaTryingList { get; set; } = new();

    public CraboRack.BokoBoole CraboRackBokoBoole { get; set; } = new();
    public CraboRack.Shell CraboRackShell { get; set; } = new();
    public DeepList.Madness CraboRackSidewaysBooleList { get; set; } = new();
    public WhenToTriggerClass CraboRackSidewaysBooleTriggeredWhen { get; set; } = new();

    public Darksci.LuckyClass DarksciLuckyList { get; set; } = new();
    public Darksci.DarksciType DarksciTypeList { get; set; } = new();


    public FriendsClass DeepListDoubtfulTactic { get; set; } = new();
    public DeepList.Madness DeepListMadnessList { get; set; } = new();
    public WhenToTriggerClass DeepListMadnessTriggeredWhen { get; set; } = new();
    public DeepList.Mockery DeepListMockeryList { get; set; } = new();
    public DeepList.SuperMindKnown DeepListSupermindKnown { get; set; } = new();
    public WhenToTriggerClass DeepListSupermindTriggeredWhen { get; set; } = new();

    public bool GlebSkip { get; set; } = new();
    public DeepList.Madness GlebChallengerList { get; set; } = new();
    public WhenToTriggerClass GlebChallengerTriggeredWhen { get; set; } = new();
    public WhenToTriggerClass GlebSleepingTriggeredWhen { get; set; } = new();
    public Gleb.GlebTeaClass GlebTea { get; set; } = new();
    public WhenToTriggerClass GlebTeaTriggeredWhen { get; set; } = new();
    public FriendsClass GlebSkipFriendList { get; set; } = new();

    public HardKitty.DoebatsyaClass HardKittyDoebatsya { get; set; } = new();
    public HardKitty.LonelinessClass HardKittyLoneliness { get; set; } = new();
    public HardKitty.MuteClass HardKittyMute { get; set; } = new();

    public LeCrisp.LeCrispAssassins LeCrispAssassins { get; set; } = new();
    public LeCrisp.LeCrispImpactClass LeCrispImpact { get; set; } = new();


    public LolGod.Udyr LolGodUdyrList { get; set; } = new();

    public Mitsuki.GarbageClass MitsukiGarbageList { get; set; } = new();
    public WhenToTriggerClass MitsukiNoPcTriggeredWhen { get; set; } = new();


    public Mylorik.MylorikRevengeClass MylorikRevenge { get; set; } = new();
    public Mylorik.MylorikSpanishClass MylorikSpanish { get; set; } = new();
    public Mylorik.MylorikSpartanClass MylorikSpartan { get; set; } = new();
    public Mylorik.MylorikBooleClass MylorikBoole { get; set; } = new();

    public Octopus.InkClass OctopusInkList { get; set; } = new();
    public Octopus.InvulnerabilityClass OctopusInvulnerabilityList { get; set; } = new();
    public Octopus.TentaclesClass OctopusTentaclesList { get; set; } = new();

    public FriendsClass SharkBoole { get; set; } = new();
    public Shark.SharkLeaderClass SharkJawsLeader { get; set; } = new();
    public FriendsClass SharkJawsWin { get; set; } = new();

    public Sirinoks.SirinoksFriendsClass SirinoksFriendsAttack { get; set; } = new();
    public FriendsClass SirinoksFriendsList { get; set; } = new();
    public Sirinoks.TrainingClass SirinoksTraining { get; set; } = new();

    public FriendsClass SpartanFirstBlood { get; set; } = new();
    public Spartan.TheyWontLikeIt SpartanMark { get; set; } = new();
    public FriendsClass SpartanShame { get; set; } = new();

    public Tigr.ThreeZeroClass TigrThreeZeroList { get; set; } = new();
    public Tigr.TigrTopClass TigrTop { get; set; } = new();
    public WhenToTriggerClass TigrTopWhen { get; set; } = new();
    public FriendsClass TigrTwoBetterList { get; set; } = new();

    public Tolya.TolyaCountClass TolyaCount { get; set; }
    public FriendsClass TolyaRammusTimes { get; set; } = new();
    public Tolya.TolyaTalkedlClass TolyaTalked { get; set; } = new();

    public Vampyr.HematophagiaClass VampyrHematophagiaList { get; set; } = new();


    public int WeedwickWeed { get; set; } = 0;
    public int WeedwickLastRoundWeed { get; set; } = 0;

    public bool KratosIsDead { get; set; } = false;


    public PassivesClass (GamePlayerBridgeClass player)
    {
        InitializePassives(player);
    }

    private int Random(int minValue, int maxValue)
    {
        maxValue += 1;
        if (minValue == maxValue) return minValue;
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxValue)}");

        var diff = (long)maxValue - minValue;
        var upperBound = uint.MaxValue / diff * diff;

        uint ui;
        do
        {
            var randomBytes = RandomNumberGenerator.GetBytes(555);
            ui = BitConverter.ToUInt32(randomBytes, 0);
        } while (ui >= upperBound);

        var result = (int)(minValue + ui % diff);
        return result;
    }

    private WhenToTriggerClass GetWhenToTrigger(int mandatoryTimes, int maxAdditionalTimes, int range, int lastRound = 10, int firstRound = 1)
    {
        var toTriggerClass = new WhenToTriggerClass();
        int when;

        //mandatory times
        for (var i = 0; i < mandatoryTimes; i++)
            while (true)
            {
                when = Random(firstRound, lastRound);
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
        var target = Random(1, range);

        if (target > maxAdditionalTimes) return toTriggerClass;

        for (var i = 0; i < target; i++)
            while (true)
            {
                when = Random(firstRound, lastRound);
                if (toTriggerClass.WhenToTrigger.Any(x => x == when)) continue;
                toTriggerClass.WhenToTrigger.Add(when);
                break;
            }
        //end additional times

        return toTriggerClass;
    }


    public void InitializePassives(GamePlayerBridgeClass player)
    {
        var characterName = player.Character.Name;
            WhenToTriggerClass when;
            switch (characterName)
            {
                case "DeepList":
                    when = GetWhenToTrigger( 1, 2, 5, 5);
                    DeepListSupermindTriggeredWhen = when;

                    when = GetWhenToTrigger( 2, 1, 3, 7, 4);
                    DeepListMadnessTriggeredWhen = when;
                    break;

                case "Тигр":
                    when = GetWhenToTrigger( 1, 1, 5, 8);
                    TigrTopWhen = when;
                    break;

                case "Толя":
                    TolyaCount = new Tolya.TolyaCountClass(Random(2, 3));
                    break;

                case "Mit*suki*":
                    when = GetWhenToTrigger( 1, 0, 0, 9, 2);
                    MitsukiNoPcTriggeredWhen = when;
                    break;

                case "Глеб":
                    //Спящее хуйло chance   
                    when = GetWhenToTrigger( 2, 2, 4, 9);
                    GlebSleepingTriggeredWhen = when;

                    //Претендент русского сервера
                    var li = when.WhenToTrigger.ToList();

                    bool flag;
                    do
                    {
                        when = GetWhenToTrigger( when.WhenToTrigger.Count, 0, 0, 10, 3);
                        flag = false;
                        foreach (var _ in li.Where(t => when.WhenToTrigger.Contains(t)))
                            flag = true;
                    } while (flag);

                    GlebChallengerTriggeredWhen = when;
                    //end Претендент русского сервера

                    break;
                case "Краборак":
                    //Хождение боком
                    when = GetWhenToTrigger( 3, 3, 10);
                    CraboRackSidewaysBooleTriggeredWhen = when;
                    //end Хождение боком
                    break;
            }
        
    }
}