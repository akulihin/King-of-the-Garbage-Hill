using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.BotFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
       public sealed class UserAccountsDataStorage : IServiceSingleton
    {
        //Save all DiscordAccountClass

        private readonly LoginFromConsole _log;

        public UserAccountsDataStorage(LoginFromConsole log)
        {
            _log = log;
        }

        public async Task InitializeAsync()
            => await Task.CompletedTask;


        public void SaveAccountSettings(IEnumerable<DiscordAccountClass> accounts, string idString, string json)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/discordAccount-{idString}.json";
            try
            {
                File.WriteAllText(filePath, json);
            }
            catch(Exception e)
            {
                _log.Critical($"Save USER DiscordAccountClass (3 params): {e.Message}");
              
            }
        }


        public void SaveAccountSettings(IEnumerable<DiscordAccountClass> accounts, ulong userId)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/discordAccount-{userId}.json";
            try
            {
                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                _log.Critical($"Save USER DiscordAccountClass (2 params): {e.Message}");

            }
        }

        //Get DiscordAccountClass

        public IEnumerable<DiscordAccountClass> LoadAccountSettings(ulong userId)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/discordAccount-{userId}.json";
            if (!File.Exists(filePath))
            {
                var newList = new List<DiscordAccountClass>();
                SaveAccountSettings(newList, userId);
                return newList;
            }

            var json = File.ReadAllText(filePath);

            try
            {
                return JsonConvert.DeserializeObject<List<DiscordAccountClass>>(json);
            }
            catch (Exception e)
            {
                _log.Critical($"LoadAccountSettings, BACK UP CREATED: {e}");
            
                var newList = new List<DiscordAccountClass>();
                SaveAccountSettings(newList, $"{userId}-BACK_UP", json);
                return newList;
            }
        }


        public ConcurrentDictionary<ulong, List<DiscordAccountClass>> LoadAllAccounts()
        {
            var dick = new ConcurrentDictionary<ulong, List<DiscordAccountClass>>();
            var filePaths = Directory.GetFiles(@"DataBase/OctoDataBase/UserAccounts");

            foreach (var file in filePaths)
            {
                var json = File.ReadAllText(file);

                var id = Convert.ToUInt64(file.Split("-")[1].Split(".")[0]);

                if(id == 0) continue;

                try
                {
                    var acc = JsonConvert.DeserializeObject<List<DiscordAccountClass>>(json);
                    dick.GetOrAdd(id, x => acc);
                }
                catch (Exception e)
                {
                    _log.Critical($"LoadAccountSettings, BACK UP CREATED: {e}");
            
                    var newList = new List<DiscordAccountClass>();
                    SaveAccountSettings(newList, $"{id}-BACK_UP", json);
                    dick.GetOrAdd(id, x => newList);
                }
            }
            return dick;
        }
    }
}