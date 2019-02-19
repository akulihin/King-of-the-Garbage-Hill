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
        //Save all DiscordAccountClass

        private readonly LoginFromConsole _log;

        public UsersDataStorage(LoginFromConsole log)
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

        public  IEnumerable<DiscordAccountClass> LoadAccountSettings(ulong userId)
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

    }
}