using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.ServerAccounts;

public sealed class ServerDataStorage : IServiceSingleton
{
    private readonly LoginFromConsole _log;

    public ServerDataStorage(LoginFromConsole log)
    {
        _log = log;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    public void SaveServerSettings(IEnumerable<ServerSettings> accounts, string idString, string json)
    {
        var filePath = $@"DataBase/ServerAccounts/account-{idString}.json";
        try
        {
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            _log.Warning($"Save SERVER AccountSettings (3 params): {e.Message}");
        }
    }

    public void SaveServerSettings(IEnumerable<ServerSettings> accounts, ulong serverId)
    {
        var filePath = $@"DataBase/ServerAccounts/account-{serverId}.json";
        try
        {
            var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            _log.Warning($"Save SERVER AccountSettings (2 params): {e.Message}");
        }
    }

    //Get ServerSettings


    public IEnumerable<ServerSettings> LoadServerSettings(ulong serverId)
    {
        var filePath = $@"DataBase/ServerAccounts/account-{serverId}.json";
        if (!File.Exists(filePath))
        {
            var newList = new List<ServerSettings>();
            SaveServerSettings(newList, serverId);
            return newList;
        }

        var json = File.ReadAllText(filePath);

        try
        {
            return JsonConvert.DeserializeObject<List<ServerSettings>>(json);
        }
        catch (Exception e)
        {
            _log.Warning($"LoadAccountSettings, BACK UP CREATED: {e}");

            var newList = new List<ServerSettings>();
            SaveServerSettings(newList, $"{serverId}-BACK_UP", json);
            return newList;
        }
    }


    public ConcurrentDictionary<ulong, List<ServerSettings>> LoadAllServersSettings()
    {
        var directoryPath = @"DataBase/ServerAccounts";
        var allFiles = new DirectoryInfo(directoryPath).GetFiles("*.json"); //Getting Text files
        var allAccounts = new ConcurrentDictionary<ulong, List<ServerSettings>>();

        foreach (var file in allFiles)
        {
            var filePath = file.FullName;
            ulong userId;
            try
            {
                userId = Convert.ToUInt64(file.Name.Replace("account-", "").Replace(".json", ""));
            }
            catch
            {
                continue;
            }

            if (!File.Exists(filePath))
            {
                var newList = new List<ServerSettings>();
                SaveServerSettings(newList, userId);
                allAccounts.AddOrUpdate(userId, newList, (key, oldValue) => newList);
            }


            var json = File.ReadAllText(filePath);

            try
            {
                var newList = JsonConvert.DeserializeObject<List<ServerSettings>>(json);
                allAccounts.AddOrUpdate(userId, newList, (key, oldValue) => newList);
            }
            catch (Exception e)
            {
                _log.Warning($"LoadAllServersSettings, BACK UP CREATED: {e}");

                var newList = new List<ServerSettings>();
                SaveServerSettings(newList, $"{userId}-BACK_UP", json);
                allAccounts.AddOrUpdate(userId, newList, (key, oldValue) => newList);
            }
        }

        return allAccounts;
    }
}