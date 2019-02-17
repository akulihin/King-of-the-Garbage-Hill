using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts
{
       public sealed class UsersDataStorage : IServiceSingleton
    {
        //Save all MainAccountClass

        private readonly LoginFromConsole _log;

        public UsersDataStorage(LoginFromConsole log)
        {
            _log = log;
        }

        public async Task InitializeAsync()
            => await Task.CompletedTask;


        public void SaveAccountSettings(IEnumerable<MainAccountClass> accounts, string idString, string json)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/account-{idString}.json";
            try
            {
                File.WriteAllText(filePath, json);
            }
            catch(Exception e)
            {
                _log.Critical($"Save USER MainAccountClass (3 params): {e.Message}");
              
            }
        }


        public void SaveAccountSettings(IEnumerable<MainAccountClass> accounts, ulong userId)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/account-{userId}.json";
            try
            {
                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                _log.Critical($"Save USER MainAccountClass (2 params): {e.Message}");

            }
        }

        //Get MainAccountClass

        public  IEnumerable<MainAccountClass> LoadAccountSettings(ulong userId)
        {
            var filePath = $@"DataBase/OctoDataBase/UserAccounts/account-{userId}.json";
            if (!File.Exists(filePath))
            {
                var newList = new List<MainAccountClass>();
                SaveAccountSettings(newList, userId);
                return newList;
            }

            var json = File.ReadAllText(filePath);

            try
            {
                return JsonConvert.DeserializeObject<List<MainAccountClass>>(json);
            }
            catch (Exception e)
            {
                _log.Critical($"LoadAccountSettings, BACK UP CREATED: {e}");
            
                var newList = new List<MainAccountClass>();
                SaveAccountSettings(newList, $"{userId}-BACK_UP", json);
                return newList;
            }
        }

    }
}