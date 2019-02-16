using System.Collections.Generic;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass {

        public int RoundNo;
        public List<AccountSettings> PlayersList;
        public ulong PlayerTurn;


        public GameClass(List<AccountSettings> playersList)
        {
            RoundNo = 1;
            PlayersList = playersList;

        PlayerTurn = 0;
    }

    }
}
