using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage
{
    public class CharactersPull : IServiceTransient
    {
        public List<CharacterClass> AllCharacters;


        public CharactersPull()
        {
            var filePath = @"DataBase/OctoDataBase/characters.json";
            var json = File.ReadAllText(filePath);
            AllCharacters = JsonConvert.DeserializeObject<List<CharacterClass>>(json);
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
