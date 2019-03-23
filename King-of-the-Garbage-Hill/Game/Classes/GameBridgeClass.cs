

namespace King_of_the_Garbage_Hill.Game.Classes
{
  public  class GameBridgeClass
  {
      public DiscordAccountClass DiscordAccount { get; set; }
      public CharacterClass Character { get; set; }

      public InGameStatus Status { get; set; }
      public bool IsBot()
      {
          return DiscordAccount.DiscordId <= 1000;
      }

      public void MinusPsycheLog(GameClass game)
      {
          game.PreviousGameLogs += $"{DiscordAccount.DiscordUserName} психанул";
      }
  }
}
