using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameGlobalVariables
{
    public class InGameGlobal : IServiceSingleton
    {
        private readonly SecureRandom _rand;

        public readonly List<WhenToTriggerClass>
            AllSkipTriggeredWhen = new();

        public readonly List<WhenToTriggerClass>
            AwdkaAfkTriggeredWhen = new();

        public readonly List<Sirinoks.TrainingClass> AwdkaTeachToPlay = new();
        public readonly List<DeepList.Madness> AwdkaTeachToPlayTempStats = new();
        public readonly List<Awdka.TrollingClass> AwdkaTrollingList = new();
        public readonly List<Awdka.TryingClass> AwdkaTryingList = new();

        public readonly List<Darksci.LuckyClass> DarksciLuckyList = new();
        public readonly List<FriendsClass> DeepListDoubtfulTactic = new();

        public readonly List<DeepList.Madness> DeepListMadnessList = new();


        public readonly List<WhenToTriggerClass>
            DeepListMadnessTriggeredWhen = new();

        public readonly List<DeepList.Mockery> DeepListMockeryList = new();

        public readonly List<DeepList.SuperMindKnown>
            DeepListSupermindKnown = new();

        public readonly List<WhenToTriggerClass>
            DeepListSupermindTriggeredWhen = new();

        public readonly List<DeepList.Madness> GlebChallengerList = new();

        public readonly List<WhenToTriggerClass>
            GlebChallengerTriggeredWhen = new();

        public readonly List<WhenToTriggerClass>
            GlebComeBackTriggeredWhen = new();

        public readonly List<Gleb.GlebSkipClass> GlebSkipList = new();

        public readonly List<WhenToTriggerClass>
            GlebSleepingTriggeredWhen = new();

        public readonly List<WhenToTriggerClass>
            GlebTeaTriggeredWhen = new();

        public readonly List<HardKitty.DoebatsyaClass> HardKittyDoebatsya = new();
        public readonly List<HardKitty.MuteClass> HardKittyMute = new();

        public readonly List<LeCrisp.LeCrispImpactClass> LeCrispImpact = new();
        public readonly List<LolGod.PushAndDieClass> LolGodPushAndDieSubList = new();
        public readonly List<LolGod.Udyr> LolGodUdyrList = new();

        public readonly List<Mitsuki.GarbageClass> MitsukiGarbageList = new();

        public readonly List<WhenToTriggerClass>
            MitsukiNoPcTriggeredWhen = new();

        public readonly List<WhenToTriggerClass>
            MylorikBooleTriggeredWhen = new();

        public readonly List<Mylorik.MylorikRevengeClass> MylorikRevenge = new();

        public readonly List<BotsBehavior.NanobotClass> NanobotsList = new();
        public readonly List<Octopus.InkClass> OctopusInkList = new();

        public readonly List<Octopus.InvulnerabilityClass> OctopusInvulnerabilityList =
            new();

        public readonly List<Octopus.TentaclesClass> OctopusTentaclesList = new();
        public readonly List<FriendsClass> PanthFirstBlood = new();
        public readonly List<FriendsClass> PanthMark = new();
        public readonly List<FriendsClass> PanthShame = new();
        public readonly List<FriendsClass> SharkBoole = new();
        public readonly List<Shark.SharkLeaderClass> SharkJawsLeader = new();
        public readonly List<FriendsClass> SharkJawsWin = new();

        public readonly List<FriendsClass> SirinoksFriendsList = new();
        public readonly List<Sirinoks.TrainingClass> SirinoksTraining = new();

        public readonly List<Tigr.ThreeZeroClass> TigrThreeZeroList = new();
        public readonly List<Tigr.TigrTopClass> TigrTop = new();
        public readonly List<WhenToTriggerClass> TigrTopWhen = new();
        public readonly List<FriendsClass> TigrTwoBetterList = new();
        public readonly List<Tolya.TolyaCountClass> TolyaCount = new();
        public readonly List<FriendsClass> TolyaRammusTimes = new();
        public readonly List<Tolya.TolyaTalkedlClass> TolyaTalked = new();
        public readonly List<FriendsClass> VampyrKilledList = new();

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
    }
}