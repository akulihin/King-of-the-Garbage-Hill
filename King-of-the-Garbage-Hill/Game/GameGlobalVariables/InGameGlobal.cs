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
            AllSkipTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            AwdkaAfkTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Sirinoks.TrainingClass> AwdkaTeachToPlay = new List<Sirinoks.TrainingClass>();
        public readonly List<DeepList.Madness> AwdkaTeachToPlayTempStats = new List<DeepList.Madness>();
        public readonly List<Awdka.TrollingClass> AwdkaTrollingList = new List<Awdka.TrollingClass>();
        public readonly List<Awdka.TryingClass> AwdkaTryingList = new List<Awdka.TryingClass>();

        public readonly List<Darksci.LuckyClass> DarksciLuckyList = new List<Darksci.LuckyClass>();
        public readonly List<FriendsClass> DeepListDoubtfulTactic = new List<FriendsClass>();

        public readonly List<DeepList.Madness> DeepListMadnessList = new List<DeepList.Madness>();


        public readonly List<WhenToTriggerClass>
            DeepListMadnessTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<DeepList.Mockery> DeepListMockeryList = new List<DeepList.Mockery>();

        public readonly List<DeepList.SuperMindKnown>
            DeepListSupermindKnown = new List<DeepList.SuperMindKnown>();

        public readonly List<WhenToTriggerClass>
            DeepListSupermindTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<DeepList.Madness> GlebChallengerList = new List<DeepList.Madness>();

        public readonly List<WhenToTriggerClass>
            GlebChallengerTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            GlebComeBackTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Gleb.GlebSkipClass> GlebSkipList = new List<Gleb.GlebSkipClass>();

        public readonly List<WhenToTriggerClass>
            GlebSleepingTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            GlebTeaTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<HardKitty.DoebatsyaClass> HardKittyDoebatsya = new List<HardKitty.DoebatsyaClass>();
        public readonly List<HardKitty.MuteClass> HardKittyMute = new List<HardKitty.MuteClass>();

        public readonly List<LeCrisp.LeCrispImpactClass> LeCrispImpact = new List<LeCrisp.LeCrispImpactClass>();
        public readonly List<LolGod.PushAndDieClass> LolGodPushAndDieSubList = new List<LolGod.PushAndDieClass>();
        public readonly List<LolGod.Udyr> LolGodUdyrList = new List<LolGod.Udyr>();

        public readonly List<Mitsuki.GarbageClass> MitsukiGarbageList = new List<Mitsuki.GarbageClass>();

        public readonly List<WhenToTriggerClass>
            MitsukiNoPcTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            MylorikBooleTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Mylorik.MylorikRevengeClass> MylorikRevenge = new List<Mylorik.MylorikRevengeClass>();

        public readonly List<BotsBehavior.NanobotClass> NanobotsList = new List<BotsBehavior.NanobotClass>();
        public readonly List<Octopus.InkClass> OctopusInkList = new List<Octopus.InkClass>();

        public readonly List<Octopus.InvulnerabilityClass> OctopusInvulnerabilityList =
            new List<Octopus.InvulnerabilityClass>();

        public readonly List<Octopus.TentaclesClass> OctopusTentaclesList = new List<Octopus.TentaclesClass>();
        public readonly List<FriendsClass> PanthFirstBlood = new List<FriendsClass>();
        public readonly List<FriendsClass> PanthMark = new List<FriendsClass>();
        public readonly List<FriendsClass> PanthShame = new List<FriendsClass>();
        public readonly List<FriendsClass> SharkBoole = new List<FriendsClass>();
        public readonly List<Shark.SharkLeaderClass> SharkJawsLeader = new List<Shark.SharkLeaderClass>();
        public readonly List<FriendsClass> SharkJawsWin = new List<FriendsClass>();

        public readonly List<FriendsClass> SirinoksFriendsList = new List<FriendsClass>();
        public readonly List<Sirinoks.TrainingClass> SirinoksTraining = new List<Sirinoks.TrainingClass>();

        public readonly List<Tigr.ThreeZeroClass> TigrThreeZeroList = new List<Tigr.ThreeZeroClass>();
        public readonly List<Tigr.TigrTopClass> TigrTop = new List<Tigr.TigrTopClass>();
        public readonly List<WhenToTriggerClass> TigrTopWhen = new List<WhenToTriggerClass>();
        public readonly List<FriendsClass> TigrTwoBetterList = new List<FriendsClass>();
        public readonly List<Tolya.TolyaCountClass> TolyaCount = new List<Tolya.TolyaCountClass>();
        public readonly List<Tolya.TolyaTalkedlClass> TolyaTalked = new List<Tolya.TolyaTalkedlClass>();
        public readonly List<FriendsClass> TolyaRammusTimes = new List<FriendsClass>();
        public readonly List<FriendsClass> VampyrKilledList = new List<FriendsClass>();

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
            var toTriggerClass = new WhenToTriggerClass(player.Status.PlayerId, player.DiscordAccount.GameId);
            int when;
            var check = new List<int>();


            if (maxRandomNumber == 0)
            {
                when = _rand.Random(2, lastRound);
                if ((lastRound == 11 && when == 3) || when > 10) when = 10;
                toTriggerClass.WhenToTrigger.Add(when);
                return toTriggerClass;
            }


            if (isMandatory)
            {
                when = _rand.Random(1, lastRound);
                if ((lastRound == 11 && when == 3) || when > 10) when = 10;
                check.Add(when);
                toTriggerClass.WhenToTrigger.Add(when);
            }

            if (isMandatory2)
            {
                do
                {
                    when = _rand.Random(1, lastRound);
                    if ((lastRound == 11 && when == 3) || when > 10) when = 10;
                } while (check.Contains(when));

                check.Add(when);
                toTriggerClass.WhenToTrigger.Add(when);
            }

            if (isMandatory3)
            {
                do
                {
                    when = _rand.Random(1, lastRound);
                    if ((lastRound == 11 && when == 3) || when > 10) when = 10;
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
                        if ((lastRound == 11 && when == 3) || when > 10) when = 10;

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
                        if ((lastRound == 11 && when == 3) || when > 10) when = 10;

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
                        if ((lastRound == 11 && when == 3) || when > 10) when = 10;

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
                        if ((lastRound == 11 && when == 3) || when > 10) when = 10;

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