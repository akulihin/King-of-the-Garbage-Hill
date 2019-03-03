using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class InGameGlobal : IServiceSingleton
    {
        private readonly SecureRandom _rand;


        public readonly List<WhenToTriggerClass>
            AllSkipTriggeredWhen = new List<WhenToTriggerClass>();

        public readonly List<ulong> DeepListDoubtfulTactic = new List<ulong>();

        public readonly List<WhenToTriggerClass>
            DeepListMadnessTriggeredWhen = new List<WhenToTriggerClass>();

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

        public InGameGlobal(SecureRandom rand)
        {
            _rand = rand;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public WhenToTriggerClass GetWhenToTrigger(GameBridgeClass player, bool isMandatory, int maxRandomNumber,
            int maxTimes)
        {
            var toTriggerClass = new WhenToTriggerClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId);
            int when;


            if (isMandatory)
            {
                when = _rand.Random(1, 10);
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
                        when = _rand.Random(1, 10);

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
                        when = _rand.Random(1, 10);

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
                        when = _rand.Random(1, 10);

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