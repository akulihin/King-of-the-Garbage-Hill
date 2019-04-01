
using System.Collections.Generic;


namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class FriendsClass
    {
        public ulong GameId { get; set; }
        public ulong PlayerDiscordId { get; set; }
        public List<ulong> FriendList = new List<ulong>();

        public FriendsClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
        {
            PlayerDiscordId = playerDiscordId;
            GameId = gameId;
            FriendList.Add(enemyId);
        }
        public FriendsClass(ulong playerDiscordId, ulong gameId)
        {
            PlayerDiscordId = playerDiscordId;
            GameId = gameId;
        }
    }
}
