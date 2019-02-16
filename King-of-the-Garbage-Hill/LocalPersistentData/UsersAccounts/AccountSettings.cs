
using King_of_the_Garbage_Hill.Game.Characters;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
    public class AccountSettings
    {
        public string DiscordUserName { get; set; }
        public ulong DiscordId { get; set; }
        public string MyPrefix { get; set; }
        public bool IsPlaying { get; set; }
        public CharacterClass CharacterStats { get; set; }     

        public int MoveListPage { get; set; }
        /*
         * 1 = main page ( your character only
         * 2 = leader board
         * 3 = lvlUp
         */
       
        public ulong MsgFromBotId { get; set; }
    }
}
