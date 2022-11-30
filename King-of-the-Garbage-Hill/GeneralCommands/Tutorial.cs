using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.ReactionHandling;

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class Tutorial : ModuleBaseCustom
{
    private readonly TutorialReactions _tutorial;
    private readonly TutorialClass _tutorialClass;

    public Tutorial(TutorialReactions tutorial, TutorialClass tutorialClass)
    {
        _tutorial = tutorial;
        _tutorialClass = tutorialClass;
    }


    [Command("обучение")]
    [Alias("tt", "tutorial")]
    [Summary("Обучение игры")]
    public async Task TutorialCommand()
    {
        var game = _tutorialClass.Tutorials.Find(x => x.DiscordPlayerId == Context.User.Id);
        if (game != null)
            _tutorialClass.Tutorials.Remove(game);

        var botMsg = await Context.User.CreateDMChannelAsync().Result.SendMessageAsync("Boole!");
        _tutorialClass.Tutorials.Add(new TutorialReactions.TutorialGame(Context.User, botMsg));
        game = _tutorialClass.Tutorials.Find(x => x.DiscordPlayerId == Context.User.Id);

        var embed = new EmbedBuilder();
        var builder = new ComponentBuilder();

        embed.WithTitle(_tutorial.GetTitleTutorial());
        embed.AddField(_tutorial.GetStatsBoardTutorial(game));
        embed.AddField(_tutorial.GetLeaderBoardTutorial(game));
        embed.WithFooter(_tutorial.GetFooter());

        builder.WithSelectMenu(_tutorial.GetAttackMenuTutorial(game));

        await game!.SocketMessageFromBot.ModifyAsync(message =>
        {
            message.Content = "";
            message.Embed = embed.Build();
            message.Components = builder.Build();
        });

        await _tutorial.SendMessageTutorial(game);
    }
}