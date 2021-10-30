using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public class LoreReactions : IServiceSingleton
    {
        private readonly CharactersPull _charactersPull;
        private readonly UserAccounts _userAccounts;
        private readonly SecureRandom _random;

        public LoreReactions(UserAccounts userAccounts, CharactersPull charactersPull, SecureRandom random)
        {
            _userAccounts = userAccounts;
            _charactersPull = charactersPull;
            _random = random;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public SelectMenuBuilder GetLoreCharacterSelectMenu(DiscordAccountClass account)
        {
            var characterMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("lore-select-character")
                .WithPlaceholder("Выбрать персонажа");


            foreach (var character in _charactersPull.GetAllCharacters())
            {
                characterMenu.AddOption(character.Name , character.Name);
            }

            return characterMenu;
        }


        public EmbedBuilder GetLoreEmbed(CharacterClass character)
        {
            var embed = new EmbedBuilder();

            var pass = "";
            var characterPassivesList = character.Passive;
            foreach (var passive in characterPassivesList)
            {
                if (!passive.Visible) continue;
                pass += $"__**{passive.PassiveName}**__";
                pass += ": ";
                pass += passive.PassiveDescription;
                pass += "\n";
            }

            embed.WithTitle($"Лор - {character.Name}");

            if(character.Description.Length > 1)
                embed.WithDescription(character.Description);

            embed.AddField("Характеристики:", $"Name: {character.Name}\n" +
                                              $"Интеллект: {character.GetIntelligence()}\n" +
                                              $"Сила: {character.GetStrength()}\n" +
                                              $"Скорость: {character.GetSpeed()}\n" +
                                              $"Психика: {character.GetPsyche()}\n");
            embed.AddField("Пассивки:", $"{pass}");

            embed.WithColor(Color.Orange);

            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);
            if (character.Avatar != null)
                embed.WithImageUrl(character.Avatar);


            return embed;

        }


        public async Task ModifyLoreMessage(SocketMessageComponent button, CharacterClass character, DiscordAccountClass account)
        {
            var builder = new ComponentBuilder();
            var embed = GetLoreEmbed(character);

            builder.WithSelectMenu(GetLoreCharacterSelectMenu(account));

            await button.Message.ModifyAsync(message =>
            {
                message.Embed = embed.Build();
                message.Components = builder.Build();
            });
        }


        public async Task ReactionAddedLore(SocketMessageComponent button)
        {
            try
            {
                var allCharacters = _charactersPull.GetAllCharacters();
                var account = _userAccounts.GetAccount(button.User.Id);
                var character = allCharacters.Find(x => x.Name == "DeepList");

                switch (button.Data.CustomId)
                {
                    case "lore-select-character":
                        character = allCharacters.Find(x => x.Name == string.Join("", button.Data.Values));
                        await ModifyLoreMessage(button, character, account);
                        break;
                }
            }
            catch
            {
                //ingored
            }
        }
    }
}