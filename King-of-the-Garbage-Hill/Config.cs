﻿using System;
using System.IO;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using Newtonsoft.Json;

namespace King_of_the_Garbage_Hill
{
    public sealed class Config : IServiceSingleton
    {

        public Config(LoginFromConsole log)
        {
            try
            {
                JsonConvert.PopulateObject(File.ReadAllText(@"DataBase/OctoDataBase/config.json"), this);
            }
            catch (Exception ex)
            {
                log.Critical(ex);
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        public Task InitializeAsync() => Task.CompletedTask;

        [JsonProperty("Token")] public string Token { get; private set; }
    }
}