using System.IO;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill.Helpers
{
    public static class Json
    {
        public static T ObjectFromJsonFile<T>(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T ParseAsJsonFilePath<T>(this string path)
        {
            return ObjectFromJsonFile<T>(path);
        }
    }
}
