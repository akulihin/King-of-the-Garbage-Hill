using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.LocalPersistentData.LoggingSystemJson
{
    public sealed class LoggingSystem : IServiceSingleton
    {
        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        private static readonly ConcurrentDictionary<string, List<GameLogsClass>> AllLogsDictionary =
            new ConcurrentDictionary<string, List<GameLogsClass>>();


        private readonly LoggingSystemDataStorage _loggingSystemDataStorage;

        public LoggingSystem( LoggingSystemDataStorage loggingSystemDataStorage)
        {
            _loggingSystemDataStorage = loggingSystemDataStorage;
        }

        public List<GameLogsClass> GetOrAddLogsToDictionary(int gameId)
        {
            var keyString = GetKeyString(gameId);
            return AllLogsDictionary.GetOrAdd(keyString, x => _loggingSystemDataStorage.LoadLogs(keyString).ToList());
        }


        public GameLogsClass GetLogs(int gameId)
        {
            return GetOrCreateLogs(gameId);
        }


        public GameLogsClass GetOrCreateLogs(int gameId)
        {
            var accounts = GetOrAddLogsToDictionary(gameId);
            var account = accounts.FirstOrDefault() ?? CreateNewLog(gameId);
            return account;
        }


        public void SaveCurrentFightLog(int gameId)
        {
            var accounts = GetOrAddLogsToDictionary(gameId);

            var keyString = GetKeyString(gameId);

            _loggingSystemDataStorage.SaveLogs(accounts, keyString);
        }


        public void SaveCompletedFight(int gameId)
        {
            var accounts = GetOrAddLogsToDictionary(gameId);

            var keyString = GetKeyString(gameId);

            _loggingSystemDataStorage.CompleteSaveLogs(accounts, keyString);
            AllLogsDictionary.Remove(keyString, out accounts);
        }

        public string GetKeyString(int gameId)
        {
            return $"{gameId}";
        }

        public GameLogsClass CreateNewLog(int gameId)
        {
            var accounts = GetOrAddLogsToDictionary(gameId);


            var newAccount = new GameLogsClass
            {
                GameId = gameId,
                IsBlock = false,
                IsSpecialStatus = false,
                IsWin = false,
                LostJusticeThisRound = 0,
                NewPlaceAtLeaderBoard = 0,
                PlayerId = 0,
                PlayerIdHeAttacked = 0,
                PlayerName = "",
                PreviousPlaceAtLeaderBoard = 0,
                ReceivedJusticeThisRound = 0,
                ReceivedPointsThisRound = 0,
                RoundNumber = 0,
                SpecialStatus = ""
            };

            accounts.Add(newAccount);
            SaveCurrentFightLog(gameId);
            return newAccount;
        }
    }
}