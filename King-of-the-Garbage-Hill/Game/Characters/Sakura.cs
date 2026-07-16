using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Sakura
{
    public const string CharacterName = "Sakura";
    public const string OneOfThree = "Одна из трех";

    public static bool Is(GamePlayerBridgeClass player)
        => player?.GameCharacter?.Name == CharacterName
           && !player.Passives.PassiveAbilitiesDisabledByKimiko;

    public static bool Is(CharacterClass character)
        => character?.Name == CharacterName;

    public static bool Is(string characterName)
        => characterName == CharacterName;

    public static bool HasUncontestedSoloTopThree(GameClass game, GamePlayerBridgeClass player)
    {
        if (game.Teams.Count > 0 || !Is(player) || player.Passives.IsDead
            || player.GameCharacter.Passive.All(passive => passive.PassiveName != OneOfThree)
            || player.Status.GetPlaceAtLeaderBoard() > 3)
            return false;

        var sakuraScore = player.Status.GetScore();
        return game.PlayersList.Count(candidate =>
            !candidate.Passives.IsDead && candidate.Status.GetScore() >= sakuraScore) <= 3;
    }

    public static void RemoveForbiddenPredictions(GameClass game, GamePlayerBridgeClass player)
    {
        var sakuraIds = game.PlayersList.Where(Is).Select(candidate => candidate.GetPlayerId()).ToHashSet();
        player.Predict.RemoveAll(prediction =>
            Is(prediction.CharacterName) || sakuraIds.Contains(prediction.PlayerId));
    }
}
