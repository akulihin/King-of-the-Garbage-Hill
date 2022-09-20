using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.LocalPersistentData.ServerAccounts;

public class ServerSettings
{
    public string ServerName { get; set; }
    public ulong ServerId { get; set; }
    public string Prefix { get; set; }
    public int ServerActivityLog { get; set; }

    public List<string> LastHardKittyStatus { get; set; } = new();

}