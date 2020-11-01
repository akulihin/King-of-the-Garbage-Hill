using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Store
{
    public class StoreLogic : IServiceSingleton
    {
        private readonly SecureRandom _random;

        public StoreLogic(SecureRandom random)
        {
            _random = random;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public EmbedBuilder GetStoreEmbed(DiscordAccountClass.ChampionChances champion, DiscordAccountClass account, IUser user)
        {
            var embed = new EmbedBuilder();
            embed.WithAuthor(user);
            embed.WithTitle($"Магазин - {champion.CharacterName}");
            embed.WithDescription($"Ты выбрал персонажа **{champion.CharacterName}**");
            embed.AddField("Текущий бонусный шанс", $"{champion.Multiplier}");
            embed.AddField("Текущее Количество ZBS Points", $"{account.ZbsPoints}");
            embed.AddField("Варинты", $"{new Emoji("1⃣")} Уменьшить шанс на 1% - 20 ZP\n" +
                                      $"{new Emoji("2⃣")} Увеличить шанс на 1% - 20 ZP\n" +
                                      $"{new Emoji("3⃣")} Вернуть все ZBS Points за этого персонажа - ~~10~~ 0 ZP\n" +
                                      $"{new Emoji("4⃣")} Вернуть все ZBS Points за ВСЕХ персонажей - ~~50~~ 0 ZP\n");
            embed.WithCurrentTimestamp();
            embed.WithFooter("WELCOME! Stranger...");
            embed.WithColor(Color.DarkPurple);
            embed.WithThumbnailUrl(
                "https://media.giphy.com/media/lbAgIgQ6Dytkk/giphy.gif");
            return embed;
        }
    }
}
