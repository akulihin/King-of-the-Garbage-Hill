using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        characters = characters.Where(x => x.Name != "Молодой Глеб").ToList();
        return characters;
    }
    public List<CharacterClass> GetAllCharactersNoFilter()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<List<CharacterClass>>(json);
    }

    public List<Passive> GetAllVisiblePassives()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        var passives = new List<Passive>();
        
        foreach (var character in characters)
        {
            passives.AddRange(character.Passive.Where(x => x.Visible));
        }

        return passives;
    }
}