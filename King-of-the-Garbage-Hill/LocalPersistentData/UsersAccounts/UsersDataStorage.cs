using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

public sealed class UserAccountsDataStorage : IServiceSingleton
{
    //Save all DiscordAccountClass

    private readonly LoginFromConsole _logs;

    public UserAccountsDataStorage(LoginFromConsole log)
    {
        _logs = log;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }


    public void SaveAccountSettings(DiscordAccountClass accounts, string idString, string json)
    {
        var filePath = $@"DataBase/UserAccounts/discordAccount-{idString}.json";
        try
        {
            WriteJsonAtomically(filePath, json);
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }


    public void SaveAccountSettings(DiscordAccountClass accounts, ulong userId)
    {
        var filePath = $@"DataBase/UserAccounts/discordAccount-{userId}.json";
        try
        {
            var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
            WriteJsonAtomically(filePath, json);
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }

    private static void WriteJsonAtomically(string filePath, string json)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory ?? ".",
            $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }


    public ConcurrentDictionary<ulong, DiscordAccountClass> LoadAllAccounts()
    {
        var dick = new ConcurrentDictionary<ulong, DiscordAccountClass>();
        var filePaths = Directory.GetFiles(@"DataBase/UserAccounts", "discordAccount-*.json");

        foreach (var file in filePaths)
        {
            const string prefix = "discordAccount-";
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
                || !ulong.TryParse(fileName.AsSpan(prefix.Length), out var id)
                || id == 0)
                continue;

            var json = File.ReadAllText(file);


            try
            {
                var acc = JsonConvert.DeserializeObject<DiscordAccountClass>(json);
                dick.GetOrAdd(id, acc);
            }
            catch (Exception exception)
            {
                _logs.Critical(exception.Message);
                _logs.Critical(exception.StackTrace);

                var newList = new DiscordAccountClass { DiscordId = id };
                SaveAccountSettings(newList, $"{id}-BACK_UP", json);
                dick.GetOrAdd(id, _ => newList);
            }
        }

        return dick;
    }
}
