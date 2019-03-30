using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameGlobalVariables
{
    public class InGameGlobal : IServiceSingleton
    {
        private readonly SecureRandom _rand;

        public readonly List<LeCrisp.LeCrispImpactClass> LeCrispImpact = new List<LeCrisp.LeCrispImpactClass>();

        public readonly List<WhenToTriggerClass>
            AllSkipTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Sirinoks.FriendsClass> DeepListDoubtfulTactic = new List<Sirinoks.FriendsClass>();

        public readonly List<WhenToTriggerClass>
            DeepListMadnessTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            AwdkaAfkTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<DeepList.Madness> DeepListMadnessList = new List<DeepList.Madness>();
        public readonly List<DeepList.Madness> GlebChallengerList = new List<DeepList.Madness>();
        public readonly List<DeepList.Madness> AwdkaTeachToPlayTempStats = new List<DeepList.Madness>();

        public readonly List<Gleb.GlebSkipClass> GlebSkipList = new List<Gleb.GlebSkipClass>();

        public readonly List<DeepList.Mockery> DeepListMockeryList = new List<DeepList.Mockery>();

        public readonly List<WhenToTriggerClass>
            DeepListSupermindTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<DeepList.SuperMindKnown>
            DeepListSupermindKnown = new List<DeepList.SuperMindKnown>();

        public readonly List<WhenToTriggerClass>
            GlebChallengerTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            GlebComeBackTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            GlebSleepingTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            GlebTeaTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<WhenToTriggerClass>
            MylorikBooleTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Mylorik.MylorikRevengeClass> MylorikRevenge = new List<Mylorik.MylorikRevengeClass>();
        public readonly List<Tolya.TolyaCountClass> TolyaCount = new List<Tolya.TolyaCountClass>();
        public readonly List<HardKitty.DoebatsyaClass> HardKittyDoebatsya = new List<HardKitty.DoebatsyaClass>();
        public readonly List<HardKitty.MuteClass> HardKittyMute = new List<HardKitty.MuteClass>();
        public readonly List<Sirinoks.TrainingClass> SirinoksTraining = new List<Sirinoks.TrainingClass>();
        public readonly List<Sirinoks.FriendsClass> SirinoksFriendsList = new List<Sirinoks.FriendsClass>();
        public readonly List<Mitsuki.GarbageClass> MitsukiGarbageList = new List<Mitsuki.GarbageClass>();

        public readonly List<WhenToTriggerClass>
            MitsukiNoPcTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<Sirinoks.TrainingClass> AwdkaTeachToPlay = new List<Sirinoks.TrainingClass>();
        public readonly List<Awdka.TryingClass> AwdkaTryingList = new List<Awdka.TryingClass>();
        public readonly List<Octopus.TentaclesClass> OctopusTentaclesList = new List<Octopus.TentaclesClass>();
        public readonly List<Octopus.InkClass> OctopusInkList = new List<Octopus.InkClass>();

        public readonly List<Octopus.InvulnerabilityClass> OctopusInvulnerabilityList =
            new List<Octopus.InvulnerabilityClass>();

        public readonly List<Darksci.LuckyClass> DarksciLuckyList = new List<Darksci.LuckyClass>();
        public readonly List<Awdka.TrollingClass> AwdkaTrollingList = new List<Awdka.TrollingClass>();
        public readonly List<Sirinoks.FriendsClass> TigrTwoBetterList = new List<Sirinoks.FriendsClass>();
        public readonly List<Tigr.ThreeZeroClass> TigrThreeZeroList = new List<Tigr.ThreeZeroClass>();
        public readonly List<WhenToTriggerClass> TigrTopWhen = new List<WhenToTriggerClass>();
        public readonly List<Tigr.TigrTopClass> TigrTop= new List<Tigr.TigrTopClass>();
        
        public readonly List<Sirinoks.FriendsClass> SharkBoole = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> SharkJawsWin = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> SharkJawsLeader = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> PanthShame = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> PanthFirstBlood = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> PanthMark = new List<Sirinoks.FriendsClass>();
        public readonly List<Sirinoks.FriendsClass> TolyaRammusTimes = new List<Sirinoks.FriendsClass>();
        
        public InGameGlobal(SecureRandom rand)
        {
            _rand = rand;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public WhenToTriggerClass GetWhenToTrigger(GamePlayerBridgeClass player, bool isMandatory, int maxRandomNumber,
            int maxTimes, bool isMandatory2 = false, int lastRound = 10)
        {
            var toTriggerClass = new WhenToTriggerClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId);
            int when;
            var check = 0;

            //I folgot why I used "maxRandomNumber = ="...
            if (maxRandomNumber == 0)
            {
                when = _rand.Random(2, lastRound);
                toTriggerClass.WhenToTrigger.Add(when);
                return toTriggerClass;
            }


            if (isMandatory)
            {
                when = _rand.Random(1, lastRound);
                check = when;
                toTriggerClass.WhenToTrigger.Add(when);
            }

            if (isMandatory2)
            {
                do
                {
                    when = _rand.Random(1, lastRound);
                } while (check == when);

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