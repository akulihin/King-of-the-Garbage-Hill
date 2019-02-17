namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class MainAccountClass
    {
        public string DiscordUserName { get; set; }
        public ulong DiscordId { get; set; }
        public string MyPrefix { get; set; }
        public bool IsPlaying { get; set; }
        public CharacterClass CharacterStats { get; set; }     

        public int MoveListPage { get; set; }
        /*
         * 1 = main page ( your character + leaderboard)
         * 2 = lvlUp     (what stat to update)
         */
       
        public ulong MsgFromBotId { get; set; }

        public int Score { get; set; }
        public bool IsBlock { get; set; }
        public bool IsAbleToTurn { get; set; }
        public ulong PlaceAtLeaderBoard { get; set; }

        public ulong WhoToAttackThisTurn { get; set; }
        public ulong GameId { get; set; }

        public bool IsReady { get; set; }
    }
}
