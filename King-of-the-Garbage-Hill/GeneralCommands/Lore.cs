using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class Lore : ModuleBaseCustom
{
    private readonly UserAccounts _accounts;
    private readonly CharactersPull _charactersPull;
    private readonly LoreReactions _loreReactions;

    public Lore(UserAccounts accounts, LoreReactions loreReactions, CharactersPull charactersPull)
    {
        _accounts = accounts;
        _loreReactions = loreReactions;
        _charactersPull = charactersPull;
    }


    [Command("Лор")]
    [Alias("l", "lore")]
    [Summary("Лор и описание всех персонажей")]
    public async Task TutorialCommand()
    {
        var account = _accounts.GetAccount(Context.User);

        var allCharacters = _charactersPull.GetAllCharacters();
        var character = allCharacters.Find(x => x.Name == "DeepList");

        var builder = new ComponentBuilder();
        var embed = _loreReactions.GetLoreEmbed(character);

        builder.WithSelectMenu(_loreReactions.GetLoreCharacterSelectMenu(account));

        await SendMessageAsync(embed, components: builder.Build());
    }
}