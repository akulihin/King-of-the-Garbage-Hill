using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.LocalPersistentData.LoggingSystemJson
{
    public interface ILoggingSystem
    {
        List<GameLogsClass> GetOrAddLogsToDictionary(ulong userId1, ulong userId2);
        GameLogsClass GetLogs(ulong userId1, ulong userId2);
        GameLogsClass GetOrCreateLogs(ulong userId1, ulong userId2);
        void SaveCurrentFightLog(ulong userId1, ulong userId2);
        GameLogsClass CreateNewLog(ulong userId1, ulong userId2);
        void SaveCompletedFight(ulong userId1, ulong userId2);

    }
}