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
    
    /*
     * -2 - Character substitution, like Yong Gleb
     * -1 - Secret Characters, not visible, but rollable
     * 0-6 - Regular characters
     */

    public List<CharacterClass> GetVisibleCharacters()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        characters = characters.Where(x => x.Tier >= 0).ToList();
        return characters;
    }

    public List<CharacterClass> GetRollableCharacters()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        characters = characters.Where(x => x.Tier >= -1).ToList();
        return characters;
    }

    public List<CharacterClass> GetAllCharactersNoFilter()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<List<CharacterClass>>(json);
    }

    public List<Passive> GetAramPassives()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json).Where(x => x.Tier != -1);
        var passives = new List<Passive>();

        foreach (var character in characters)
        {
            foreach (var passive in character.Passive.Where(x => x.Visible))
            {
                if (passives.All(x => x.PassiveName != passive.PassiveName))
                {
                    passives.Add(passive);
                }
            }
        }

        return passives;
    }

    public List<Passive> GetAllPassives()
    {
        var filePath = @"DataBase/characters.json";
        var json = File.ReadAllText(filePath);
        var characters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        var passives = new List<Passive>();

        foreach (var character in characters)
        {
            foreach (var passive in character.Passive)
            {
                if (passives.All(x => x.PassiveName != passive.PassiveName))
                {
                    passives.Add(passive);
                }
            }
        }

        return passives;
    }
}