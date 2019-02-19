using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.LoggingSystemJson
{
    public sealed class LoggingSystemDataStorage : IServiceSingleton
    {
        //Save all DiscordAccountClass

        private readonly LoginFromConsole _log;

        public LoggingSystemDataStorage(LoginFromConsole log)
        {
            _log = log;
        }

        public void SaveLogs(IEnumerable<GameLogsClass> accounts, string keyString, string json)
        {
            var filePath = $@"DataBase/OctoDataBase/Logging/{keyString}.json";
            try
            {
                File.WriteAllText(filePath, json);
            }
            catch(Exception e)
            {
                _log.Critical($"Failed To WRITE (SaveLogs) (3 params) : {e.Message}");
              
            }
        }


        public void SaveLogs(IEnumerable<GameLogsClass> accounts, string keyString)
        {
            var filePath = $@"DataBase/OctoDataBase/Logging/{keyString}.json";
            try
            {
                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                _log.Critical($"Failed To WRITE (SaveLogs) (2 params) : {e.Message}");
            }
        }

        public void CompleteSaveLogs(IEnumerable<GameLogsClass> accounts, string keyString)
        {
            var index = 1;
            var filePath = $@"DataBase/OctoDataBase/Logging/{keyString}-{index}.json";

            while (File.Exists(filePath))
            {
                filePath = $@"DataBase/OctoDataBase/Logging/{keyString}-{index++}.json";
            }

            try
            {
                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                _log.Critical($"Failed To WRITE (CompleteSaveLogs) (3 params) : {e.Message}");

            }
        }

        //Get DiscordAccountClass

        public IEnumerable<GameLogsClass> LoadLogs(string keyString)
        {
            var filePath = $@"DataBase/OctoDataBase/Logging/{keyString}.json";
            if (!File.Exists(filePath))
            {
                var newList = new List<GameLogsClass>();
                SaveLogs(newList, keyString);
                return newList;
            }

            var json = File.ReadAllText(filePath);

            try
            {
                return JsonConvert.DeserializeObject<List<GameLogsClass>>(json);
            }
            catch (Exception e)
            {
                _log.Critical($"Failed to READ (LoadLogs), Back up created: {e.Message}");
                
                var newList = new List<GameLogsClass>();
                SaveLogs(newList, $"{keyString}-BACK_UP-{DateTime.UtcNow}", json);
                return newList;
            }
        }


        public async Task InitializeAsync()
            => await Task.CompletedTask;
    }
}