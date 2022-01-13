using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage;

public class CharactersPull : IServiceSingleton
{
    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }
    /*
    private readonly List<CharacterClass> _allCharacters;


    public CharactersPull()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        _allCharacters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
    }
    */

    public List<CharacterClass> GetAllCharacters()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<List<CharacterClass>>(json);
    }
}