
using System.Collections.Generic;


namespace King_of_the_Garbage_Hill.Game.Classes
{

        public class WhenToTriggerClass
        {
            public ulong DiscordId;
            public ulong GameId;
            public List<int> WhenToTrigger;

            public WhenToTriggerClass(ulong discordId, ulong gameId)
            {
                DiscordId = discordId;
                WhenToTrigger = new List<int>();
                GameId = gameId;
            }

            public WhenToTriggerClass(ulong discordId, ulong gameId, int when)
            {
                DiscordId = discordId;
                WhenToTrigger = new List<int> {when};
                GameId = gameId;
            }
        
    }
}
