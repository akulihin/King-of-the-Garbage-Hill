using System.Diagnostics.CodeAnalysis;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
    [SuppressMessage("ReSharper", "InconsistentNaming")] 
    public class AccountSettings
    {
        public string DiscordUserName { get; set; }
        public ulong DiscordId { get; set; }
        public string MyPrefix { get; set; }
        public int PlayingStatus { get; set; }

       
    }
}
