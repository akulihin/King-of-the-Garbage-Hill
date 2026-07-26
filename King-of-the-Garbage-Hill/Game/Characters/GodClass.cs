using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

/// <summary>
/// Lore class used by mechanics that explicitly interact with gods. This is
/// separate from the mutable combat class derived from a character's top stat.
/// </summary>
public static class GodClass
{
    public const string ClassName = "Бог";

    public static bool IsGod(CharacterClass character) =>
        character?.Name is "Кира" or Madara.CharacterName or Homelander.CharacterName;

    public static bool IsGodDeathSource(string deathSource) =>
        deathSource is "Kira" or Madara.CharacterName or "Madara" or Homelander.CharacterName;
}
