using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.Localization;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Maps internal game objects to DTOs for the web client.
/// Handles visibility rules (e.g., don't show opponent passives that are hidden).
/// </summary>
public static class GameStateMapper
{
    private const string HiddenCharacterAvatar =
        "https://r2.ozvmusic.com/kotgh/art/avatars/unknown_fixvalues.png";

    // Cache of locally available avatar filenames (lowercase → actual filename)
    private static readonly HashSet<string> _localAvatars;

    // Cache of all public character names; live prediction catalogs are scoped to account unlocks.
    private static readonly List<string> _allCharacterNames;

    // Full public catalog with base stats; live prediction catalogs are scoped to account unlocks.
    private static readonly List<DTOs.CharacterInfoDto> _allCharacters;

    public static List<string> GetAllCharacterNames() => _allCharacterNames;
    public static List<CharacterInfoDto> GetAllCharacters() => _allCharacters;

    static GameStateMapper()
    {
        _localAvatars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var avatarDir = Path.Combine(AppContext.BaseDirectory, "DataBase", "art", "avatars");
        if (Directory.Exists(avatarDir))
        {
            foreach (var file in Directory.GetFiles(avatarDir))
            {
                _localAvatars.Add(Path.GetFileName(file));
            }
            Console.WriteLine($"[WebAPI] Loaded {_localAvatars.Count} local avatars from {avatarDir}");
        }

        // Load character names and full catalog for predict dropdowns
        _allCharacterNames = new List<string>();
        _allCharacters = new List<DTOs.CharacterInfoDto>();
        try
        {
            var charsPath = Path.Combine(AppContext.BaseDirectory, "DataBase", "characters.json");
            if (File.Exists(charsPath))
            {
                var json = File.ReadAllText(charsPath);
                var chars = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Game.Classes.CharacterClass>>(json);
                var visible = chars
                    .Where(c => c.Tier >= 0
                                && !UnknownBug.Is(c)
                                && !c.Passive.Any(p => p.PassiveName == "Выдуманный персонаж"))
                    .OrderBy(c => c.Name)
                    .ToList();
                _allCharacterNames = visible.Select(c => c.Name).ToList();
                _allCharacters = visible.Select(c => new DTOs.CharacterInfoDto
                {
                    Name = c.Name,
                    Avatar = GetLocalAvatarUrl(c.Avatar),
                    Description = c.Description,
                    Tier = c.Tier,
                    Intelligence = c.GetIntelligence(),
                    Strength = c.GetStrength(),
                    Speed = c.GetSpeed(),
                    Psyche = c.GetPsyche(),
                }).ToList();
                Console.WriteLine($"[WebAPI] Loaded {_allCharacterNames.Count} character names for predictions");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAPI] Failed to load character names: {ex.Message}");
        }
    }

    /// <summary>
    /// Map a GameClass to a GameStateDto, scoped to the requesting player.
    /// </summary>
    public static GameStateDto ToDto(GameClass game, GamePlayerBridgeClass requestingPlayer = null,
        DiscordAccountClass requestingAccount = null)
    {
        var isAdmin = requestingPlayer != null && requestingPlayer.PlayerType == 2;
        var viewerIsTerminal = UnknownBug.Is(requestingPlayer);
        var (assumptionCharacterNames, assumptionCharacters) = GetAssumptionCatalog(requestingAccount);
        var canInspectPlayers = isAdmin || requestingPlayer?.GameCharacter.Passive.Any(
            passive => passive.PassiveName == UnknownBug.AdminPlayerType) == true;
        var isAdeptChooser = game.CthulhuState.AdeptStageActive
                             && Cthulhu.IsUntransformed(requestingPlayer);
        var depthsCallPromptActive = IsDepthsCallPromptActive(game, requestingPlayer);
        var privateDraftHeading = isAdeptChooser
            ? "Выбери адепта"
            : depthsCallPromptActive
                ? "Откликнуться на зов глубин"
                : null;
        List<DraftOptionDto> scopedDraftOptions = null;
        if ((game.IsDraftPickPhase || isAdeptChooser) && requestingPlayer != null
            && game.DraftOptions.TryGetValue(requestingPlayer.GetPlayerId(), out var draftOpts))
        {
            scopedDraftOptions = draftOpts
                .Select((character, index) => (Character: character, OriginalIndex: index))
                .Where(option => !UnknownBug.Is(option.Character))
                .Select(option => new DraftOptionDto
                {
                    Name = option.Character.Name,
                    Avatar = GetLocalAvatarUrl(option.Character.Avatar),
                    Intelligence = option.Character.GetIntelligence(),
                    Psyche = option.Character.GetPsyche(),
                    Speed = option.Character.GetSpeed(),
                    Strength = option.Character.GetStrength(),
                    Description = option.Character.Description,
                    Tier = option.Character.Tier,
                    Cost = isAdeptChooser || option.OriginalIndex == 0 ? 0 : 5,
                    Passives = option.Character.Passive
                        .Where(passive => passive.Visible && passive.PassiveName != Madara.EternalTsukuyomi)
                        .Select(passive => new PassiveDto
                        {
                            Name = passive.PassiveName,
                            Description = passive.PassiveDescription,
                            Visible = passive.Visible,
                        }).ToList(),
                }).ToList();
            if (scopedDraftOptions.Count == 0)
                scopedDraftOptions = null;
        }

        var dto = new GameStateDto
        {
            GameId = game.GameId,
            RoundNo = game.RoundNo,
            TurnLengthInSecond = game.TurnLengthInSecond,
            TimePassedSeconds = game.TimePassed.Elapsed.TotalSeconds,
            GameVersion = game.GameVersion,
            GameMode = game.GameMode,
            IsFinished = game.IsFinished,
            IsAramPickPhase = game.IsAramPickPhase,
            IsDraftPickPhase = game.IsDraftPickPhase || isAdeptChooser || depthsCallPromptActive,
            DraftOptions = scopedDraftOptions,
            DraftPickHeading = requestingPlayer == null || privateDraftHeading == null
                ? privateDraftHeading
                : GameLocalization.TextForClient(requestingPlayer.DiscordId, privateDraftHeading),
            DraftPickAcceptLabel = depthsCallPromptActive ? Localized("Да") : null,
            DraftPickDeclineLabel = depthsCallPromptActive ? Localized("Нет") : null,
            DraftPickSelectLabel = isAdeptChooser ? Localized("Выбрать") : null,
            IsKratosEvent = game.IsKratosEvent,
            IsRumblingWarningActive = ErenYeager.IsRumblingWarningActive(game),
            RumblingKillCount = ErenYeager.GetRumblingKillCount(game),
            IsRoundTransitionPaused = game.IsRoundTransitionPaused,
            TransitionDeadlineUtc = game.TransitionDeadlineUtc?.ToString("o"),
            HalfLifeReleaseSerial = game.HalfLifeReleaseSerial,
            AbyssSerial = game.CthulhuState.AbyssSerial,
            OmniManInvasionSerial = OmniMan.GetInvasionSerial(game),
            OmniManUndergroundTrainSerial = OmniMan.Is(requestingPlayer)
                ? OmniMan.GetUndergroundTrainSerial(game)
                : 0,
            OmniManUndergroundTrainPhrase = OmniMan.Is(requestingPlayer)
                ? OmniMan.GetUndergroundTrainPhrase(game)
                : null,
            GlobalLogs = requestingPlayer == null
                ? (isAdmin ? game.GetGlobalLogs() : StripHiddenLogs(game.GetGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game))
                : GameLocalization.TextForClient(requestingPlayer.DiscordId,
                    isAdmin ? game.GetGlobalLogs() : StripHiddenLogs(game.GetGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game)),
            AllGlobalLogs = requestingPlayer == null
                ? (isAdmin ? game.GetAllGlobalLogs() : StripHiddenLogs(game.GetAllGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game))
                : GameLocalization.TextForClient(requestingPlayer.DiscordId,
                    isAdmin ? game.GetAllGlobalLogs() : StripHiddenLogs(game.GetAllGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game)),
            MyPlayerId = requestingPlayer?.GetPlayerId(),
            MyPlayerType = requestingPlayer?.PlayerType ?? 0,
            IsProMode = requestingPlayer?.IsProMode ?? false,
            PreferWeb = requestingPlayer?.PreferWeb ?? false,
            AllCharacterNames = viewerIsTerminal || requestingPlayer?.GameCharacter.DoomRollMode == true || Madara.IsMadara(requestingPlayer)
                ? new List<string>() : assumptionCharacterNames,
            AllCharacters = viewerIsTerminal || requestingPlayer?.GameCharacter.DoomRollMode == true || Madara.IsMadara(requestingPlayer)
                ? new List<CharacterInfoDto>() : assumptionCharacters,
        };

        if (!viewerIsTerminal)
        {
            dto.GlobalLogs = SanitizePrivateCharacterText(dto.GlobalLogs);
            dto.AllGlobalLogs = SanitizePrivateCharacterText(dto.AllGlobalLogs);
        }
        if (requestingPlayer?.IsProMode == true && !isAdmin)
        {
            dto.GlobalLogs = MaskProActionLabels(dto.GlobalLogs);
            dto.AllGlobalLogs = MaskProActionLabels(dto.AllGlobalLogs);
        }

        // Map structured fight log for web animation (scoped: only own fights get full details)
        var myUsername = requestingPlayer?.DiscordUsername;
        var fightAdmin = isAdmin && !viewerIsTerminal;
        var streamTarget = viewerIsTerminal && requestingPlayer.Passives.UnknownBug.StreamTargetPlayerId != Guid.Empty
            ? game.PlayersList.Find(player => player.GetPlayerId() == requestingPlayer.Passives.UnknownBug.StreamTargetPlayerId)
            : null;
        var streamUsername = streamTarget?.DiscordUsername;
        dto.FightLog = game.WebFightLog
                .Where(f => !f.HiddenFromNonAdmin || fightAdmin
                            || (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername))
                            || (streamUsername != null && f.WinnerName == streamUsername))
                .Select(f => ScopeFightEntry(f, myUsername, fightAdmin,
                    streamUsername != null && f.WinnerName == streamUsername,
                    !viewerIsTerminal
                    && (UnknownBug.Is(f.AttackerCharName) || UnknownBug.Is(f.DefenderCharName))))
                .Select(f => MaskProFightOutcome(
                    f,
                    myUsername,
                    requestingPlayer?.IsProMode == true && !fightAdmin))
                .Select(f => MaskPrivateFightIdentity(f, viewerIsTerminal))
                .ToList();
        foreach (var fight in dto.FightLog)
            ApplyBoardEntityDisplay(fight);

        var viewerIsTheBoys = requestingPlayer?.GameCharacter.Name == "TheBoys";
        var viewerIsHomelander = Homelander.Is(requestingPlayer);
        var viewerIsOmniMan = OmniMan.Is(requestingPlayer);

        foreach (var player in game.PlayersList)
        {
            var isMe = requestingPlayer != null && player.GetPlayerId() == requestingPlayer.GetPlayerId();
            dto.Players.Add(MapPlayer(player, requestingPlayer, isMe, isAdmin, canInspectPlayers,
                game.PlayersList, game, viewerIsTerminal, viewerIsTheBoys, viewerIsHomelander,
                viewerIsOmniMan));
        }

        if (Cthulhu.IsNechtoActive(game) && !game.IsFinished)
        {
            var displayName = Localized(Cthulhu.Nechto);
            dto.Players.Add(new PlayerDto
            {
                PlayerId = Cthulhu.NechtoRowId,
                DiscordUsername = Cthulhu.Nechto,
                DisplayUsername = displayName,
                IsBot = true,
                IsBoardEntity = true,
                Character = new CharacterDto
                {
                    Name = Cthulhu.Nechto,
                    DisplayName = displayName,
                    Avatar = "/art/avatars/nechto.png",
                    AvatarCurrent = "/art/avatars/nechto.png",
                    Intelligence = 10,
                    Strength = 10,
                    Speed = 10,
                    Psyche = 10,
                    Passives = new List<PassiveDto>(),
                },
                Status = new PlayerStatusDto
                {
                    Place = Cthulhu.NechtoPlace,
                    Score = 0,
                },
            });
        }

        foreach (var team in game.Teams)
        {
            dto.Teams.Add(new TeamDto
            {
                TeamId = team.TeamId,
                PlayerIds = team.TeamPlayers.ToList(),
            });
        }

        // Map Pink Ward revealed player IDs
        dto.PinkWardRevealedPlayerIds = new List<Guid>(game.PinkWardRevealedPlayerIds);

        // Build full chronicle for Летопись tab when game is finished
        if (game.IsFinished)
        {
            var fullChronicle = BuildFullChronicle(game, requestingPlayer);
            dto.FullChronicle = requestingPlayer == null
                ? fullChronicle
                : GameLocalization.TextForUser(requestingPlayer.DiscordId, fullChronicle);
        }

        // Populate newly unlocked achievements for finished games (requesting player only)
        if (game.IsFinished && requestingPlayer != null)
        {
            // Populate newly unlocked achievements
            var achData = requestingPlayer.Passives?.AchievementDataRef;
            if (achData?.NewlyUnlocked != null && achData.NewlyUnlocked.Count > 0)
            {
                foreach (var achId in achData.NewlyUnlocked.Distinct(StringComparer.Ordinal))
                {
                    var def = AchievementService.GetDefinition(achId);
                    if (def == null) continue;
                    var progress = achData.Progress?.Find(x => x.AchievementId == achId);
                    dto.NewlyUnlockedAchievements.Add(new AchievementEntryDto
                    {
                        Id = def.Id,
                        Name = def.Name,
                        NameRu = def.NameRu,
                        Description = def.Description,
                        DescriptionRu = def.DescriptionRu,
                        SecretHint = def.SecretHint,
                        SecretHintRu = def.SecretHintRu,
                        Category = def.Category.ToString(),
                        IsSecret = def.IsSecret,
                        Icon = def.Icon,
                        Rarity = def.Rarity,
                        CharacterNames = def.CharacterNames?.ToList() ?? new List<string>(),
                        RewardZbs = def.RewardZbs,
                        RewardLootBoxes = def.RewardLootBoxes,
                        Target = def.Target,
                        Current = def.Target,
                        IsUnlocked = true,
                        UnlockedAt = progress?.UnlockedAt?.ToString("o"),
                    });
                }
            }
        }

        ApplyEternalTsukuyomiProjection(dto, game, requestingPlayer);
        if (requestingPlayer?.IsProMode == true && !isAdmin)
            dto.FullChronicle = null;

        return dto;
    }

    private static PlayerDto MapPlayer(GamePlayerBridgeClass player, GamePlayerBridgeClass requestingPlayer,
        bool isMe, bool isAdmin, bool canInspectPlayers, List<GamePlayerBridgeClass> allPlayers, GameClass game = null,
        bool viewerIsTerminal = false, bool viewerIsTheBoys = false, bool viewerIsHomelander = false,
        bool viewerIsOmniMan = false)
    {
        var hasDeathNote = player.GameCharacter.Passive.Any(p => p.PassiveName == "Тетрадь смерти");

        var dto = new PlayerDto
        {
            PlayerId = player.GetPlayerId(),
            DiscordUsername = player.DiscordUsername,
            IsBot = player.IsBot(),
            IsWebPlayer = player.IsWebPlayer,
            TeamId = player.TeamId,
            IsNarutoAlly = requestingPlayer != null && !isMe && Naruto.IsNarutoPair(requestingPlayer, player),
            IsMadaraRedTiger = Madara.IsRedTiger(game, player),
            IsDead = player.Passives.IsDead,
            DeathSource = player.Passives.DeathSource,
            IsKira = isMe && hasDeathNote,
            Character = MapCharacter(player.GameCharacter, isMe, canInspectPlayers, game?.IsFinished ?? false),
            Status = MapStatus(
                player,
                requestingPlayer,
                game,
                isMe,
                canInspectPlayers,
                game?.IsFinished ?? false),
        };
        if (viewerIsHomelander
            && requestingPlayer != null
            && !isMe
            && !requestingPlayer.IsTeamMember(game, player.GetPlayerId()))
        {
            if (Homelander.HasPassive(requestingPlayer, Homelander.Righteousness))
                dto.HomelanderRagePercent =
                    Homelander.RagePercentFor(requestingPlayer, player.GetPlayerId());
            dto.HomelanderIdentityRevealer =
                Homelander.WasRevealedBy(requestingPlayer, player.GetPlayerId());
        }
        if (viewerIsOmniMan
            && requestingPlayer != null
            && !isMe
            && !requestingPlayer.IsTeamMember(game, player.GetPlayerId()))
        {
            dto.OmniManIdiot = OmniMan.IsIdiot(requestingPlayer, player.GetPlayerId());
            dto.OmniManGuardiansAsleep =
                OmniMan.IsSleepingFromGuardians(requestingPlayer, player, game);
        }
        if (isMe && game != null)
        {
            dto.IsDeepSession = Cthulhu.IsUntransformed(player)
                                || Cthulhu.IsHerald(game, player);
            dto.DepthsCallPromptActive = IsDepthsCallPromptActive(game, player);
            dto.AdeptChoiceAvailable =
                Cthulhu.CanChooseAdept(game, player);
            dto.AdeptChoiceLabel = dto.AdeptChoiceAvailable
                ? GameLocalization.TextForClient(player.DiscordId, "Выбрать адепта")
                : null;
            dto.AdeptChoiceTooltip = dto.AdeptChoiceAvailable
                ? GameLocalization.TextForClient(player.DiscordId, "Открыть выбор адепта")
                : null;
            if (dto.IsDeepSession)
                ApplyPrivateCharacterDisplay(dto.Character);
            if (Cthulhu.IsUntransformed(player))
                dto.Character.StatDisplayOverride = "∞";
            if (Cthulhu.IsHerald(game, player))
            {
                var morok = dto.Character.Passives.Find(passive =>
                    passive.Name == Cthulhu.Morok);
                if (morok != null)
                    morok.Theme = "deep";
            }
        }

        // Predictions — visible to the owning player, and to everyone at game end
        var isFinished = game?.IsFinished ?? false;
        if ((isMe || isFinished) && !(isMe && Madara.IsMadara(player)))
        {
            dto.Predictions = player.Predict
                .Where(prediction => viewerIsTerminal || !UnknownBug.Is(prediction.CharacterName))
                .Where(prediction => !Sakura.Is(prediction.CharacterName)
                                     && !Sakura.Is(allPlayers.Find(target =>
                                         target.GetPlayerId() == prediction.PlayerId)))
                .Select(p =>
                {
                    var predDto = new PredictDto { PlayerId = p.PlayerId, CharacterName = p.CharacterName };
                    // At game end, populate actual character info for prediction results
                    if (isFinished)
                    {
                        var target = allPlayers.Find(x => x.GetPlayerId() == p.PlayerId);
                        if (target != null)
                        {
                            if (UnknownBug.Is(target) && !viewerIsTerminal)
                            {
                                predDto.ActualCharacterName = "???";
                                predDto.ActualAvatar = UnknownBug.MissingAvatar;
                            }
                            else
                            {
                                predDto.ActualCharacterName = target.GameCharacter.Name;
                                predDto.IsCorrect = string.Equals(p.CharacterName, target.GameCharacter.Name, StringComparison.OrdinalIgnoreCase);
                                if (predDto.IsCorrect != true)
                                    predDto.ActualAvatar = GetLocalAvatarUrl(target.GameCharacter.Avatar);
                            }
                        }
                    }
                    return predDto;
                })
                .ToList();
            if (isMe)
                dto.CharacterMasteryPoints = player.CharacterMasteryPoints;
        }

        // Death Note state — only visible to the Kira player
        if (isMe && hasDeathNote)
        {
            var dn = player.Passives.KiraDeathNote;
            var eyes = player.Passives.KiraShinigamiEyes;
            var kiraL = player.Passives.KiraL;

            dto.DeathNote = new DeathNoteDto
            {
                CurrentRoundTarget = dn.CurrentRoundTarget,
                CurrentRoundName = dn.CurrentRoundName,
                Entries = dn.Entries.Select(e => new DeathNoteEntryDto
                {
                    TargetPlayerId = e.TargetPlayerId,
                    WrittenName = e.WrittenName,
                    RoundWritten = e.RoundWritten,
                    WasCorrect = e.WasCorrect,
                }).ToList(),
                FailedTargets = new List<Guid>(dn.FailedTargets),
                LPlayerId = kiraL.LPlayerId,
                IsArrested = kiraL.IsArrested,
                ShinigamiEyesActive = eyes.EyesActiveForNextAttack,
                RevealedPlayers = eyes.RevealedPlayers
                    .Where(rp => !Sakura.Is(allPlayers.Find(x => x.GetPlayerId() == rp)))
                    .Select(rp =>
                {
                    var revealed = allPlayers.Find(x => x.GetPlayerId() == rp);
                    return new DeathNoteRevealedPlayerDto
                    {
                        PlayerId = rp,
                        CharacterName = revealed == null || (UnknownBug.Is(revealed) && !viewerIsTerminal)
                            ? "?"
                            : revealed.GameCharacter.Name
                    };
                }).ToList(),
            };
        }

        // Portal Gun state — only visible to the Rick player
        if (isMe && player.GameCharacter.Passive.Any(p => p.PassiveName == "Портальная пушка"))
        {
            var gun = player.Passives.RickPortalGun;
            dto.PortalGun = new PortalGunDto
            {
                Invented = gun.Invented,
                Charges = gun.Charges,
            };
        }

        // Darksci choice — round 1, not yet triggered
        if (isMe && game != null && game.RoundNo == 1
            && player.GameCharacter.Passive.Any(p => p.PassiveName == "Мне (не)везет")
            && !player.Passives.DarksciTypeList.Triggered)
        {
            dto.DarksciChoiceNeeded = true;
        }

        // Young Gleb — round 1, hasn't transformed yet
        if (isMe && game != null && game.RoundNo == 1
            && player.GameCharacter.Passive.Any(p => p.PassiveName == "Yong Gleb")
            && player.GameCharacter.Name != "Молодой Глеб")
        {
            dto.YoungGlebAvailable = true;
        }

        // Private terminal state is visible only to its owner.
        if (isMe && viewerIsTerminal)
        {
            dto.IsTerminalMode = true;
            if (game != null)
            {
                var activeNode = game.PlayersList.Find(candidate => candidate.Passives.IsExploitable);
                var state = player.Passives.UnknownBug;
                dto.TerminalState = new TerminalStateDto
                {
                    BufferedPoints = game.TotalExploit,
                    StreamTargetPlayerId = state.StreamTargetPlayerId == Guid.Empty ? null : state.StreamTargetPlayerId,
                    ActiveNodePlayerId = activeNode?.GetPlayerId(),
                    IsNodeActive = !game.ExploitClosed && activeNode != null,
                    CommitSerial = state.CommitSerial,
                    LastCommitPoints = state.LastCommitPoints,
                };
            }
        }

        // Tsukuyomi state — only visible to the Itachi player
        if (isMe && player.GameCharacter.Passive.Any(p => p.PassiveName == "Глаза Итачи"))
        {
            var tsukuyomiState = player.Passives.ItachiTsukuyomi;
            dto.TsukuyomiState = new TsukuyomiStateDto
            {
                ChargeCounter = Math.Max(0, tsukuyomiState.ChargeCounter),
                IsReady = tsukuyomiState.ChargeCounter >= 2,
                TotalStolenPoints = tsukuyomiState.TotalStolenPoints,
            };
        }

        // Marker projection is private to the terminal viewer.
        if (viewerIsTerminal)
            dto.HasTerminalMarker = player.Passives.IsExploitable;

        // Butcher marks are secret: only the TheBoys viewer sees the icon on marked enemy rows.
        if (viewerIsTheBoys && !isMe)
        {
            dto.IsTheBoysSupTarget = player.Passives.TheBoysSupMark;
            dto.IsTheBoysVirusTarget =
                player.Passives.TheBoysVirus
                && player.Passives.TheBoysVirusSource == requestingPlayer.GetPlayerId();
        }

        // Passive ability widgets — only visible to the owning player
        if (isMe && game != null)
        {
            var pas = new PassiveAbilityStatesDto();
            bool anySet = false;

            foreach (var passive in player.GameCharacter.Passive)
            {
                switch (passive.PassiveName)
                {
                    case JonSnow.ServerKing:
                        if (player.GameCharacter.Name == JonSnow.CharacterName
                            && pas.JonSnow == null)
                        {
                            var jon = player.Passives.JonSnow;
                            pas.JonSnow = new JonSnowStateDto
                            {
                                Skill = player.GameCharacter.GetSkill(),
                                SkillTarget = JonSnow.KingSkillThreshold,
                                IsKing = JonSnow.IsKing(player),
                                KingBlockedByCastle = JonSnow.IsKing(player)
                                                       && player.Status.GetPlaceAtLeaderBoard()
                                                       == JonSnow.BlackCastlePlace,
                                BastardIntelligenceBonus =
                                    player.GameCharacter.JonSnowBastardIntelligenceBonus,
                                BlackCastleActive = jon.BlackCastleActive,
                                BlackCastleTurnsRemaining = jon.BlackCastleActive
                                    ? Math.Max(0, jon.BlackCastleReleaseAfterRound - game.RoundNo + 1)
                                    : 0,
                                WatchEnded = jon.WatchEnded,
                                LoyaltyVictories = jon.LoyaltyVictories,
                                WeakestPlayers = game.PlayersList
                                    .Where(candidate =>
                                        jon.WeakestPlayerIds.Contains(candidate.GetPlayerId()))
                                    .OrderByDescending(candidate =>
                                        candidate.Status.GetPlaceAtLeaderBoard())
                                    .Select(candidate => new JonSnowWeakestPlayerDto
                                    {
                                        PlayerId = candidate.GetPlayerId(),
                                        PlayerName = candidate.DiscordUsername,
                                    }).ToList(),
                            };
                            anySet = true;
                        }
                        break;

                    case GordonFreeman.Crowbar:
                        if (player.GameCharacter.Name == GordonFreeman.CharacterName
                            && pas.Gordon == null)
                        {
                            var gordon = player.Passives.Gordon;
                            var halfLife = gordon.HalfLife;
                            pas.Gordon = new GordonStateDto
                            {
                                ResolvedFights = gordon.ResolvedFights,
                                CrowbarProgress = gordon.ResolvedFights % 3,
                                WakeUsed = gordon.WakeUsed,
                                CanWake = GordonFreeman.CanWake(player, game),
                                WakeReservedForTsukuyomi = gordon.WakeReservedForEternalTsukuyomi,
                                HeadcrabsRemoved = gordon.HeadcrabsRemoved,
                                ZombieCount = game.PlayersList.Count(candidate =>
                                    candidate.Passives.GordonHeadcrab.IsZombie),
                                ActiveHeadcrabs = game.PlayersList
                                    .Where(candidate => candidate.Passives.GordonHeadcrab.IsActive
                                                        && candidate.Passives.GordonHeadcrab.SourceId == player.GetPlayerId())
                                    .Select(candidate => new GordonHeadcrabDto
                                    {
                                        PlayerId = candidate.GetPlayerId(),
                                        PlayerName = candidate.DiscordUsername,
                                        RoundsLeft = Math.Max(0,
                                            candidate.Passives.GordonHeadcrab.ExpiresAfterRound - game.RoundNo + 1),
                                    }).ToList(),
                                HalfLife = new GordonHalfLifeStateDto
                                {
                                    Announced = halfLife.Announced,
                                    Finished = halfLife.Finished,
                                    Released = halfLife.Released,
                                    Postponements = halfLife.Postponements,
                                    CanAnnounce = GordonFreeman.CanAnnounceHalfLife3(player, game),
                                    PendingDecision = halfLife.PendingDecision,
                                    DecisionKind = halfLife.PendingReleaseConfirmation ? "release" : "failure",
                                    DecisionSerial = halfLife.DecisionSerial,
                                    DeadlineUtc = halfLife.DeadlineUtc?.ToString("o"),
                                    RawPoints = halfLife.RawPoints,
                                    SuperMultiplierDisabled = halfLife.SuperMultiplierDisabled,
                                    Exponent = halfLife.Exponent,
                                    FinalPoints = halfLife.FinalPoints,
                                    FreezeLabel = GordonFreeman.GetFreezeLabel(gordon),
                                    PostponeLabel = GordonFreeman.GetPostponeLabel(gordon),
                                    DecisionMessage = GordonFreeman.GetDecisionMessage(gordon),
                                },
                            };
                            anySet = true;
                        }
                        break;

                    case Naruto.HaremJutsu:
                        if (player.GameCharacter.Name == Naruto.CharacterName)
                        {
                            pas.Naruto = new NarutoStateDto
                            {
                                HaremActive = player.Passives.Naruto.HaremActiveThisRound,
                                HaremCooldown = player.Passives.Naruto.HaremCooldown,
                            };
                            anySet = true;
                        }
                        break;

                    case ErenYeager.Sheep:
                        if (player.GameCharacter.Name == ErenYeager.CharacterName)
                        {
                            var eren = player.Passives.Eren;
                            pas.Eren = new ErenStateDto
                            {
                                RageGained = eren.RageGained,
                                Losses = eren.Losses,
                                AttackTitanActive = eren.AttackTitanActiveThisRound,
                                AttackTitanCooldown = eren.AttackTitanCooldown,
                                AttackTitanSoundSerial = eren.AttackTitanSoundSerial,
                                TatakeSoundSerial = eren.TatakeSoundSerial,
                                RumblingTriggered = eren.RumblingTriggered,
                                RumblingPlace = eren.RumblingPlace,
                                HatredMarks = game.PlayersList
                                    .Where(enemy => enemy.Passives.ErenHatredMark > 0)
                                    .Select(enemy => new ErenHatredMarkDto
                                    {
                                        PlayerName = enemy.DiscordUsername,
                                        Marks = enemy.Passives.ErenHatredMark,
                                    })
                                    .ToList(),
                            };
                            anySet = true;
                        }
                        break;

                    case "Rune":
                        if (player.GameCharacter.Name == DoomGuy.CharacterName)
                        {
                            var doom = player.Passives.DoomGuy;
                            var stage = player.Status.LvlUpPoints > 0 ? DoomGuy.StageForRound(game.RoundNo) : "";
                            pas.DoomGuy = new DoomGuyStateDto
                            {
                                RollMode = doom.RollMode,
                                RollAvailable = game.RoundNo == 1 && !doom.RollMode,
                                CurrentStage = stage,
                                CurrentOptions = DoomGuy.GetOptions(doom, stage).Select(x => new DoomModuleDto
                                {
                                    Name = x.Name,
                                    Stage = x.Stage,
                                    Description = x.Description,
                                    Reward = x.Reward,
                                }).ToList(),
                                ActiveModules = new Dictionary<string, string>(doom.ActiveModules),
                                DemonNestNames = doom.DemonNests
                                    .Select(id => game.PlayersList.Find(x => x.GetPlayerId() == id)?.DiscordUsername ?? "")
                                    .Where(x => x.Length > 0).ToList(),
                                BfgCharged = doom.BfgCharged,
                                RailgunCharged = doom.RailgunCharged,
                                AscensionIntelligenceRemaining = doom.AscensionIntelligenceRemaining,
                                ManeuversSpeedRemaining = doom.ManeuversSpeedRemaining,
                                ExterminationVictories = doom.ExterminationVictories.Count,
                                ExterminationAwarded = doom.ExterminationAwarded,
                                ShockShieldUsed = doom.ShockShieldUsed,
                                BlocksThisRound = doom.BlocksThisRound,
                                HellBlockUsed = doom.HellBlockUsed,
                                CounterAttackMarkedNames = doom.CounterAttackMarks
                                    .Where(mark => mark.Value == game.RoundNo)
                                    .Select(mark => game.PlayersList.Find(x => x.GetPlayerId() == mark.Key)?.DiscordUsername ?? "")
                                    .Where(x => x.Length > 0).ToList(),
                                SharkShieldActive = doom.SharkShieldActiveThisRound,
                                EverBlocked = doom.EverBlocked,
                                EverLost = doom.EverLost,
                                BecomeGodAwarded = doom.BecomeGodAwarded,
                                ChainsawSpent = doom.ChainsawSpent,
                                ChainsawSelectionsRemaining = doom.ChainsawSelectionsRemaining,
                                ChainsawChoices = doom.ChainsawChoices.Select(x => new DoomCopiedPassiveDto
                                {
                                    Name = x.PassiveName,
                                    Description = x.PassiveDescription,
                                }).ToList(),
                                CopiedPassiveName = doom.CopiedPassiveName,
                                CopiedPassiveNames = doom.CopiedPassiveNames.ToList(),
                            };
                            anySet = true;
                        }
                        break;
                    case "Буль":
                        var psyche = player.GameCharacter.GetPsyche();
                        pas.Bulk = new BulkStateDto
                        {
                            DrownChance = psyche < 7 ? (int)Math.Round(100.0 / (10 + psyche * 5)) : 0,
                            IsBuffed = player.Passives.MylorikBoole.IsBoole,
                        };
                        anySet = true;
                        break;
                    case "Я за чаем":
                        pas.Tea = new TeaStateDto { IsReady = player.Passives.GlebTea.Ready };
                        anySet = true;
                        break;
                    case "Еврей":
                        // M2: the PROFIT widget is fed from LeCrisp-only assassin state; Толя shares
                        // the "Еврей" passive but has no such state, so gate to LeCrisp (mirrors the
                        // Геральт Name gate below) — otherwise Толя sees a dead "PROFIT: 0" widget.
                        if (player.GameCharacter.Name == "LeCrisp")
                        {
                            pas.Jew = new JewStateDto { StolenPsyche = player.Passives.LeCrispAssassins.AdditionalPsycheCurrent };
                            anySet = true;
                        }
                        break;
                    case "Одиночество":
                        var hist = player.Passives.HardKittyLoneliness.AttackHistory;
                        pas.HardKitty = new HardKittyStateDto { FriendsCount = hist.Sum(h => h.Times) };
                        anySet = true;
                        break;
                    case "Обучение":
                        var tr = player.Passives.SirinoksTraining;
                        var lastTraining = tr.Training.LastOrDefault();
                        var statIdx = lastTraining?.StatIndex ?? 0;
                        pas.Training = new TrainingStateDto
                        {
                            CurrentStatIndex = statIdx,
                            StatName = statIdx switch { 1 => "INT", 2 => "STR", 3 => "SPD", 4 => "PSY", _ => "—" },
                            TargetStatValue = lastTraining?.StatNumber ?? 0,
                        };
                        anySet = true;
                        break;
                    case "Дракон":
                        pas.Dragon = new DragonStateDto { IsAwakened = game.RoundNo >= 10, RoundsUntilAwaken = Math.Max(0, 10 - game.RoundNo) };
                        anySet = true;
                        break;
                    case "Запах мусора":
                        var garb = player.Passives.MitsukiGarbageList.Training;
                        pas.Garbage = new GarbageStateDto { MarkedCount = garb.Count(t => t.Times >= 2), TotalTracked = garb.Count };
                        anySet = true;
                        break;
                    case "Научите играть":
                        var copyHist = player.Passives.AwdkaTeachToPlayHistory.History;
                        var lastCopy = copyHist.LastOrDefault();
                        var copyStatIndex = lastCopy?.Text ?? "0";
                        pas.Copycat = new CopycatStateDto
                        {
                            CopiedStatName = copyStatIndex switch { "1" => "INT", "2" => "STR", "3" => "SPD", "4" => "PSY", _ => "—" },
                            HistoryCount = copyHist.Count,
                        };
                        anySet = true;
                        break;
                    case "Чернильная завеса":
                        var ink = player.Passives.OctopusInkList.RealScoreList;
                        pas.InkScreen = new InkScreenStateDto { FakeDefeatCount = ink.Count, TotalDeferredScore = ink.Sum(i => i.RealScore) };
                        anySet = true;
                        break;
                    case "Тигр топ, а ты холоп":
                        pas.TigerTop = new TigerTopStateDto { IsActive = player.Status.GetPlaceAtLeaderBoard() == 1, SwapsRemaining = player.Passives.TigrTop.TimeCount };
                        anySet = true;
                        break;
                    case "Челюсти":
                        pas.Jaws = new JawsStateDto
                        {
                            CurrentSpeed = player.GameCharacter.GetSpeed(),
                            UniqueDefeated = player.Passives.SharkJawsWin.FriendList.Count,
                            UniquePositions = player.Passives.SharkJawsLeader.FriendList.Count,
                        };
                        anySet = true;
                        break;
                    case "Это привилегия - умереть от моей руки":
                        var markedGuids = player.Passives.SpartanMark.FriendList.Where(x => x != Guid.Empty).ToList();
                        pas.Privilege = new PrivilegeStateDto
                        {
                            MarkedCount = markedGuids.Count,
                            MarkedNames = markedGuids
                                .Select(id => game.PlayersList.Find(p => p.GetPlayerId() == id)?.DiscordUsername ?? "")
                                .Where(n => n != "")
                                .ToList(),
                        };
                        anySet = true;
                        break;
                    case "Вампуризм":
                        pas.Vampirism = new VampirismStateDto
                        {
                            ActiveFeeds = player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count,
                            IgnoredJustice = player.Passives.VampyrIgnoresOneJustice,
                        };
                        anySet = true;
                        break;
                    case "Weed":
                        pas.Weed = new WeedStateDto
                        {
                            TotalWeedAvailable = allPlayers.Where(p => p.GetPlayerId() != player.GetPlayerId()).Sum(p => p.Passives.WeedwickWeed),
                            LastHarvestRound = player.Passives.WeedwickLastRoundWeed,
                        };
                        anySet = true;
                        break;
                    case "Неприметность":
                        pas.Saitama = new SaitamaStateDto { DeferredPoints = player.Passives.SaitamaUnnoticed.GetTotalDeferred(), DeferredMoral = player.Passives.SaitamaUnnoticed.DeferredMoral };
                        anySet = true;
                        break;
                    case "Глаза бога смерти":
                        pas.ShinigamiEyes = new ShinigamiEyesWidgetDto { IsActive = player.Passives.KiraShinigamiEyes.EyesActiveForNextAttack };
                        anySet = true;
                        break;
                    case "Макро":
                        pas.Dopa = new DopaStateDto
                        {
                            VisionReady = player.Passives.DopaVision.Cooldown == 0,
                            VisionCooldown = player.Passives.DopaVision.Cooldown,
                            ChosenTactic = player.Passives.DopaMetaChoice.ChosenTactic,
                            MetaChoiceReady = player.Status.LvlUpPoints > 0
                                && player.Passives.DopaMetaChoice.StatLevelUpsTaken >= 1
                                && !player.Passives.DopaMetaChoice.Triggered,
                            NeedSecondAttack = player.Status.WhoToAttackThisTurn.Count == 1 && !player.Status.IsReady,
                        };
                        anySet = true;
                        break;
                    case "Гоблины":
                        var gobPop = player.Passives.GoblinPopulation;
                        var gobZig = player.Passives.GoblinZiggurat;
                        pas.GoblinSwarm = new GoblinSwarmStateDto
                        {
                            TotalGoblins = gobPop.TotalGoblins,
                            Warriors = gobPop.Warriors,
                            Hobs = gobPop.Hobs,
                            Workers = gobPop.Workers,
                            HobRate = gobPop.HobRate,
                            WarriorRate = gobPop.WarriorRate,
                            WorkerRate = gobPop.WorkerRate,
                            HobUpgradeLevel = gobPop.HobUpgradeLevel,
                            WarriorUpgradeLevel = gobPop.WarriorUpgradeLevel,
                            WorkerUpgradeLevel = gobPop.WorkerUpgradeLevel,
                            ZigguratPositions = gobZig.BuiltPositions,
                            IsInZiggurat = gobZig.IsInZiggurat,
                            FestivalUsed = gobPop.FestivalUsed,
                        };
                        anySet = true;
                        break;
                    case "Кошачья засада":
                        var kotikiAmbush = player.Passives.KotikiAmbush;
                        var kotikiStorm = player.Passives.KotikiStorm;
                        pas.Kotiki = new KotikiStateDto
                        {
                            TauntedCount = kotikiStorm.TauntedPlayers.Count,
                            TauntedMax = game.PlayersList.Count - 1,
                            MinkaOnPlayerName = kotikiAmbush.MinkaOnPlayer != Guid.Empty
                                ? game.PlayersList.Find(x => x.GetPlayerId() == kotikiAmbush.MinkaOnPlayer)?.DiscordUsername ?? ""
                                : "",
                            StormOnPlayerName = kotikiAmbush.StormOnPlayer != Guid.Empty
                                ? game.PlayersList.Find(x => x.GetPlayerId() == kotikiAmbush.StormOnPlayer)?.DiscordUsername ?? ""
                                : "",
                            MinkaCooldown = kotikiAmbush.MinkaCooldown,
                            StormCooldown = kotikiAmbush.StormCooldown,
                            MinkaRoundsOnEnemy = kotikiAmbush.MinkaRoundsOnEnemy,
                        };
                        anySet = true;
                        break;
                    case "Впарить говна":
                        pas.Seller = new SellerStateDto
                        {
                            Cooldown = player.Passives.SellerVparitGovna.Cooldown,
                            MarkedCount = player.Passives.SellerVparitGovna.MarkedPlayers.Count,
                            SecretBuildSkill = player.Passives.SellerSecretBuild.AccumulatedSkill
                                + game.PlayersList
                                    .Where(p => p.GameCharacter.SkillSiphonBox.HasValue)
                                    .Sum(p => p.GameCharacter.SkillSiphonBox.Value),
                        };
                        anySet = true;
                        break;
                    case ScamRat.PassiveName:
                        if (pas.ScamRat == null)
                        {
                            var scamRat = player.Passives.ScamRat;
                            pas.ScamRat = new ScamRatStateDto
                            {
                                ActiveGpuCount = scamRat.ActiveGpuOwnerIds.Count,
                                SoldGpuCount = scamRat.EverGpuOwnerIds.Count,
                                CarryPoints = scamRat.CarryPoints,
                                MaximumJustice = player.GameCharacter.Justice.GetMaximumRealJustice(),
                                LastIntelligenceRoll = scamRat.LastIntelligenceRoll,
                                LastExplosionPoints = scamRat.LastExplosionPoints,
                                TotalExplosionPoints = scamRat.TotalExplosionPoints,
                                ActiveGpuOwners = game.PlayersList
                                    .Where(candidate =>
                                        scamRat.ActiveGpuOwnerIds.Contains(candidate.GetPlayerId()))
                                    .Select(candidate => candidate.DiscordUsername)
                                    .ToList(),
                            };
                            anySet = true;
                        }
                        break;
                    case "Монстр":
                        pas.Monster = new MonsterStateDto
                        {
                            PawnCount = game.PlayersList.Count(x => x.Passives.IsJohanPawn && x.Passives.JohanPawnOwnerId == player.GetPlayerId()),
                        };
                        anySet = true;
                        break;
                    case "Огурчик Рик":
                        var pickle = player.Passives.RickPickle;
                        pas.PickleRick = new PickleRickStateDto
                        {
                            PickleTurnsRemaining = pickle.PickleTurnsRemaining,
                            WasAttackedAsPickle = pickle.WasAttackedAsPickle,
                            PenaltyTurnsRemaining = pickle.PenaltyTurnsRemaining,
                        };
                        anySet = true;
                        break;
                    case "Гигантские бобы":
                        var beans = player.Passives.RickGiantBeans;
                        pas.GiantBeans = new GiantBeansStateDto
                        {
                            BeanStacks = beans.BeanStacks,
                            IngredientsActive = beans.IngredientsActive,
                            IngredientTargetCount = beans.IngredientTargets.Count,
                        };
                        anySet = true;
                        break;
                    case "Подсчет":
                        pas.TolyaCount = new TolyaCountStateDto
                        {
                            IsReady = player.Passives.TolyaCount.IsReadyToUse,
                            Cooldown = player.Passives.TolyaCount.Cooldown,
                        };
                        anySet = true;
                        break;
                    case "Импакт":
                        pas.Impact = new ImpactStateDto
                        {
                            Streak = player.Passives.LeCrispImpact.ImpactTimes,
                        };
                        anySet = true;
                        break;
                    case "Повезло":
                        pas.Darksci = new DarksciStateDto
                        {
                            IsStableType = player.Passives.DarksciTypeList.IsStableType,
                            TypeChosen = player.Passives.DarksciTypeList.Triggered,
                            UniqueEnemiesLeft = 5 - player.Passives.DarksciLuckyList.TouchedPlayers.Count,
                        };
                        anySet = true;
                        break;
                    case "Сомнительная тактика":
                        pas.DeepList = new DeepListStateDto
                        {
                            KnownCount = player.Passives.DeepListSupermindKnown.KnownPlayers.Count,
                            MockeryTriggered = player.Passives.DeepListMockeryList.WhoWonTimes.Count(x => x.Triggered),
                        };
                        anySet = true;
                        break;
                    case "Панцирь":
                        pas.CraboRack = new CraboRackStateDto
                        {
                            ShellsUsed = player.Passives.CraboRackShell.FriendList.Count,
                        };
                        anySet = true;
                        break;
                    case "Вступить в союз":
                        var ally = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.NapoleonAlliance.AllyId);
                        pas.Napoleon = new NapoleonStateDto
                        {
                            AllyName = ally?.DiscordUsername ?? "",
                            TreatyCount = player.Passives.NapoleonPeaceTreaty.TreatyEnemies.Count,
                        };
                        anySet = true;
                        break;
                    case "Premade":
                        var carry = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.SupportPremade.MarkedPlayerId);
                        pas.Support = new SupportStateDto
                        {
                            CarryName = carry?.DiscordUsername ?? "",
                        };
                        anySet = true;
                        break;
                    case "Get cancer":
                        var cancerHolder = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.ToxicMateCancer.CurrentHolder);
                        pas.ToxicMate = new ToxicMateStateDto
                        {
                            CancerActive = player.Passives.ToxicMateCancer.IsActive,
                            TransferCount = player.Passives.ToxicMateCancer.TransferCount,
                            CurrentHolderName = cancerHolder?.DiscordUsername ?? "",
                        };
                        anySet = true;
                        break;
                    case "Спокойствие":
                        pas.YongGleb = new YongGlebStateDto
                        {
                            TeaReady = player.Passives.YongGlebTea.IsReadyToUse,
                            TeaCooldown = player.Passives.YongGlebTea.Cooldown,
                        };
                        anySet = true;
                        break;
                    case "Шэн":
                        var shen = player.Passives.SalldorumShen;
                        var capsule = player.Passives.SalldorumTimeCapsule;
                        var chronicler = player.Passives.SalldorumChronicler;
                        pas.Salldorum = new SalldorumStateDto
                        {
                            ShenCharges = shen.Charges,
                            ColaBuried = capsule.Buried,
                            ColaBuriedPosition = capsule.BuriedAtPosition,
                            ColaBuriedRound = capsule.BuriedOnRound,
                            ColaReady = capsule.Buried
                                        && game.RoundNo - capsule.BuriedOnRound >= Salldorum.TimeCapsuleMinimumAge,
                            ColaReadyRound = capsule.BuriedOnRound + Salldorum.TimeCapsuleMinimumAge,
                            ColaDrinks = capsule.DrinkCount,
                            HistoryRewritten = chronicler.HistoryRewritten,
                            RewrittenRound = chronicler.RewrittenRound,
                            PositionHistory = chronicler.PositionHistory.ToList(),
                        };
                        anySet = true;
                        break;

                    case "Ведьмачьи заказы":
                        if (player.GameCharacter.Name == "Геральт")
                        {
                            var geraltContracts = player.Passives.GeraltContracts;
                            var geraltOil = player.Passives.GeraltOil;
                            var geraltMed = player.Passives.GeraltMeditation;
                            pas.Geralt = new GeraltStateDto
                            {
                                DrownersContracts = geraltContracts.Drowners,
                                WerewolvesContracts = geraltContracts.Werewolves,
                                VampiresContracts = geraltContracts.Vampires,
                                DragonsContracts = geraltContracts.Dragons,
                                DrownersOilTier = geraltOil.DrownersOilTier,
                                WerewolvesOilTier = geraltOil.WerewolvesOilTier,
                                VampiresOilTier = geraltOil.VampiresOilTier,
                                DragonsOilTier = geraltOil.DragonsOilTier,
                                IsOilApplied = geraltOil.IsOilApplied,
                                RevealedCount = geraltMed.RevealedEnemies.Count,
                                LambertUsed = geraltMed.LambertUsed,
                                LambertActive = geraltMed.LambertActive,
                                EnemyMonsterTypes = geraltContracts.EnemyTypes
                                    .Select(kvp =>
                                    {
                                        var tp = game.PlayersList.Find(x => x.GetPlayerId() == kvp.Key);
                                        return new { Name = tp?.DiscordUsername ?? "???", Type = kvp.Value.ToString() };
                                    })
                                    .ToDictionary(x => x.Name, x => x.Type),
                            };
                            var demandState = player.Passives.GeraltContractDemand;
                            pas.Geralt.QuestCompletedThisRound = demandState.QuestCompletedThisRound;
                            pas.Geralt.RareLootFoundThisRound = geraltContracts.RareLootFoundThisRound;
                            pas.Geralt.Displeasure = demandState.Displeasure;
                            pas.Geralt.DemandedThisPhase = demandState.DemandedThisPhase;
                            pas.Geralt.CanDemandPrevious = !demandState.DemandedThisPhase && !player.Passives.IsDead && demandState.PrevContractsFought > 0;
                            pas.Geralt.CanDemandNext = !demandState.DemandedForNext && !player.Passives.IsDead && demandState.Displeasure < 5 && !demandState.AdvancePending;
                            pas.Geralt.AdvancePending = demandState.AdvancePending;
                            if (pas.Geralt.CanDemandPrevious)
                            {
                                var invoice = demandState.CalculateInvoice();
                                pas.Geralt.InvoicePredictedCoins = invoice.PredictedCoins;
                                pas.Geralt.InvoicePredictedDispleasure = invoice.PredictedDispleasure;
                                // Only admins see the full breakdown
                                if (isAdmin)
                                {
                                    pas.Geralt.InvoiceItems = invoice.LineItems
                                        .Select(li => new InvoiceLineItemDto { Label = li.Label, Points = li.Points })
                                        .ToList();
                                    pas.Geralt.InvoiceTotal = invoice.Total;
                                }
                            }
                            anySet = true;
                        }
                        break;
                }
            }

            if (player.GameCharacter.Name == "TheBoys")
            {
                var tbFrancie = player.Passives.TheBoysFrancie;
                var tbButcher = player.Passives.TheBoysButcher;
                var tbKimiko = player.Passives.TheBoysKimiko;
                var tbMM = player.Passives.TheBoysMM;
                pas.TheBoys = new TheBoysStateDto
                {
                    ChemWeaponLevel = tbFrancie.ChemWeaponLevel,
                    OrderTargetName = tbFrancie.OrderTarget != Guid.Empty
                        ? game.PlayersList.Find(x => x.GetPlayerId() == tbFrancie.OrderTarget)?.DiscordUsername ?? ""
                        : "",
                    OrderRoundsLeft = tbFrancie.OrderRoundsLeft,
                    OrdersCompleted = tbFrancie.OrdersCompleted,
                    OrdersFailed = tbFrancie.OrdersFailed,
                    VirusArmed = tbFrancie.VirusArmed,
                    VirusUsed = tbFrancie.VirusUsed,
                    PokerCount = tbButcher.PokerCount,
                    SuperDickActive = tbButcher.SuperDickActive,
                    ButcherLeft = tbButcher.ButcherLeft,
                    ActiveCombination = tbButcher.ActiveCombination,
                    RegenLevel = tbKimiko.RegenLevel,
                    KimikoDisabled = tbKimiko.IsDisabled,
                    TotalJusticeBlocked = tbKimiko.TotalJusticeBlocked,
                    LivingWeapon = tbKimiko.LivingWeapon,
                    MMUpgradeLevel = tbMM.UpgradeLevel,
                    KompromatCount = tbMM.KompromatTargets.Count,
                    NextAttackGathersKompromat = tbMM.NextAttackGathersKompromat,
                    IsCalm = tbMM.IsCalm,
                    KompromatEntries = tbMM.KompromatTargets.Select(targetId =>
                    {
                        var target = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                        return new TheBoysKompromatEntryDto
                        {
                            TargetName = target?.DiscordUsername ?? "",
                            Hint = tbMM.KompromatHints.GetValueOrDefault(targetId, ""),
                        };
                    }).ToList(),
                    LastRevealedMember = player.Passives.TheBoysLastRevealedMember,
                    RevealSerial = player.Passives.TheBoysRevealSerial,
                    LastUnlockedAbility = player.Passives.TheBoysLastUnlockedAbility,
                    LastUnlockWasCombination = player.Passives.TheBoysLastUnlockWasCombination,
                    UnlockSerial = player.Passives.TheBoysUnlockSerial,
                    VirusNames = game.PlayersList
                        .Where(x => x.Passives.TheBoysVirus && x.Passives.TheBoysVirusSource == player.GetPlayerId())
                        .Select(x => x.DiscordUsername)
                        .ToList(),
                };
                anySet = true;
            }

            if (anySet) dto.PassiveAbilityStates = pas;
        }

        return dto;
    }

    private static CharacterDto MapCharacter(CharacterClass character, bool isMe, bool canInspect, bool isFinished = false)
    {
        // This identity remains masked for everyone except the player who actually rolled it.
        if (!isMe && UnknownBug.Is(character))
            return HiddenCharacter();

        // Normal opponent visibility still opens for inspectors and after game completion.
        if (!isMe && !canInspect && !isFinished)
            return HiddenCharacter();

        var privateTerminal = isMe && UnknownBug.Is(character);
        var avatar = privateTerminal ? UnknownBug.MissingAvatar : GetLocalAvatarUrl(character.Avatar);
        var avatarCurrent = privateTerminal ? UnknownBug.MissingAvatar : GetLocalAvatarUrl(character.AvatarCurrent);

        var dto = new CharacterDto
        {
            Name = character.Name,
            Avatar = avatar,
            AvatarCurrent = avatarCurrent,
            Description = isMe ? character.Description : "",
            Tier = character.Tier,
            Intelligence = character.GetIntelligence(),
            Strength = character.GetStrength(),
            Speed = character.GetSpeed(),
            Psyche = character.GetPsyche(),
            SkillDisplay = character.GetSkillDisplay(),
            MoralDisplay = character.GetMoralStringWeb(),
            Justice = character.Justice.GetRealJusticeNow(),
            SeenJustice = character.Justice.GetSeenJusticeNow(),
            SkillClass = character.GetSkillClass(),
            SkillTarget = isMe ? character.GetCurrentSkillClassTarget() : "",
            ClassStatDisplayText = character.GetClassStatDisplayTextWeb(),

            // Quality resists
            IntelligenceResist = isMe ? character.GetIntelligenceQualityResistInt() : 0,
            StrengthResist = isMe ? character.GetStrengthQualityResistInt() : 0,
            SpeedResist = isMe ? character.GetSpeedQualityResistInt() : 0,
            PsycheResist = isMe ? character.GetPsycheQualityResistInt() : 0,

            // Quality bonuses
            IntelligenceBonusText = isMe ? GetIntelligenceBonusText(character) : "",
            StrengthBonusText = isMe ? (character.GetStrengthQualityDropBonus() ? "+1 Drop Power" : "") : "",
            SpeedBonusText = isMe ? GetSpeedBonusText(character) : "",
            PsycheBonusText = isMe ? GetPsycheBonusText(character) : "",
        };

        if (Madara.HasReanimatedBody(character))
        {
            dto.SkillDisplay = "";
            dto.MoralDisplay = "";
            dto.SkillTarget = "";
            dto.IntelligenceResist = 0;
            dto.StrengthResist = 0;
            dto.SpeedResist = 0;
            dto.PsycheResist = 0;
            dto.IntelligenceBonusText = "";
            dto.StrengthBonusText = "";
            dto.SpeedBonusText = "";
            dto.PsycheBonusText = "";
        }

        // Hidden passives normally stay absent until revealed. TheBoys alone receives anonymous locked
        // placeholders so its four ultimates and secret combinations remain visible as unopened slots.
        foreach (var passive in character.Passive)
        {
            if (passive.PassiveName == Madara.EternalTsukuyomi) continue;
            if (passive.Visible)
            {
                dto.Passives.Add(new PassiveDto
                {
                    Name = passive.PassiveName,
                    Description = passive.PassiveDescription,
                    Visible = passive.Visible,
                });
            }
            else if (isMe && character.Name == "TheBoys")
            {
                dto.Passives.Add(new PassiveDto
                {
                    Name = "",
                    Description = "",
                    Visible = false,
                });
            }
        }

        return dto;
    }

    private static CharacterDto HiddenCharacter()
    {
        return new CharacterDto
        {
            Name = "???",
            Avatar = HiddenCharacterAvatar,
            AvatarCurrent = HiddenCharacterAvatar,
            Description = "",
            Tier = 0,
            Intelligence = -1,
            Strength = -1,
            Speed = -1,
            Psyche = -1,
            SkillDisplay = "?",
            MoralDisplay = "?",
            Justice = -1,
            SeenJustice = -1,
            SkillClass = "?",
            SkillTarget = "",
            ClassStatDisplayText = "",
            Passives = new List<PassiveDto>(),
        };
    }

    private static void ApplyEternalTsukuyomiProjection(
        GameStateDto dto, GameClass game, GamePlayerBridgeClass requestingPlayer)
    {
        if (!game.IsFinished || !Madara.IsEternalTsukuyomiActive(game))
            return;

        if (requestingPlayer == null)
        {
            const string hiddenResult = "Результат игры скрыт.";
            dto.GlobalLogs = hiddenResult;
            dto.AllGlobalLogs = hiddenResult;
            dto.FullChronicle = hiddenResult;
            dto.FightLog.Clear();
            foreach (var player in dto.Players)
            {
                player.Status.Score = 0;
                player.Status.Place = 0;
                player.Status.ScoreBreakdown = null;
                player.Status.PlaceHistory.Clear();
                player.Predictions.Clear();
            }
            return;
        }

        var madara = Madara.Find(game);
        if (madara == null) return;

        if (Madara.IsMadara(requestingPlayer)
            || UnknownBug.Is(requestingPlayer)
            || GordonFreeman.SeesEternalTsukuyomiReality(requestingPlayer, game))
        {
            // Madara, reserved Gordon and immune unknown_bug see the captured authoritative
            // ending. No one receives or executes a round-10 action at this terminal boundary.
            if (!UnknownBug.Is(requestingPlayer))
                dto.FightLog.Clear();
            return;
        }

        var projectedLogs = GameLocalization.TextForUser(
            requestingPlayer.DiscordId, Madara.GetProjectedFinalLogs(game, requestingPlayer));
        if (!UnknownBug.Is(requestingPlayer))
            projectedLogs = SanitizePrivateCharacterText(projectedLogs);
        dto.GlobalLogs = projectedLogs;
        dto.AllGlobalLogs = projectedLogs;
        dto.FullChronicle = projectedLogs;

        var illusoryBonus = Madara.GetIllusoryBonus(game, requestingPlayer);
        var projectedOrder = Madara.GetIllusoryOrder(game, requestingPlayer);
        for (var i = 0; i < projectedOrder.Count; i++)
        {
            var projectedPlayer = dto.Players.Find(player => player.PlayerId == projectedOrder[i].GetPlayerId());
            if (projectedPlayer == null) continue;
            projectedPlayer.Status.Place = i + 1;
            if (projectedPlayer.PlayerId != requestingPlayer.GetPlayerId())
                projectedPlayer.Status.ScoreBreakdown = null;
            if (projectedPlayer.Status.PlaceHistory.Count == 0
                || projectedPlayer.Status.PlaceHistory[^1].Round != game.RoundNo)
                projectedPlayer.Status.PlaceHistory.Add(new PlaceHistoryDto { Round = game.RoundNo, Place = i + 1 });
            else
                projectedPlayer.Status.PlaceHistory[^1].Place = i + 1;
        }

        var viewerDto = dto.Players.Find(player => player.PlayerId == requestingPlayer.GetPlayerId());
        if (viewerDto != null)
        {
            viewerDto.IsDead = false;
            viewerDto.DeathSource = "";
            viewerDto.Status.Score += illusoryBonus;
            viewerDto.Status.ScoreBreakdown ??= new ScoreBreakdownDto();
            viewerDto.Status.ScoreBreakdown.Entries.Add(new ScoreEntryDto
            {
                Source = Madara.EternalTsukuyomi,
                Points = illusoryBonus,
                IsBonus = true,
            });
        }

        var illusoryTargets = Madara.GetIllusoryTargets(game, requestingPlayer)
            .Select(targetId => game.PlayersList.Find(player => player.GetPlayerId() == targetId))
            .Where(target => target != null)
            .ToList();
        if (illusoryTargets.Count == 0)
            illusoryTargets.Add(madara);

        dto.FightLog = illusoryTargets
            .Select(target => new FightEntryDto
            {
                AttackerName = requestingPlayer.DiscordUsername,
                AttackerCharName = requestingPlayer.GameCharacter.Name,
                AttackerAvatar = GetLocalAvatarUrl(
                    requestingPlayer.GameCharacter.AvatarCurrent ?? requestingPlayer.GameCharacter.Avatar),
                DefenderName = target!.DiscordUsername,
                DefenderCharName = target.GameCharacter.Name,
                DefenderAvatar = GetLocalAvatarUrl(
                    target.GameCharacter.AvatarCurrent ?? target.GameCharacter.Avatar),
                Outcome = "win",
                WinnerName = requestingPlayer.DiscordUsername,
                TotalPointsWon = 1,
                Round1PointsWon = 1,
            })
            .Select(fight => MaskPrivateFightIdentity(fight, UnknownBug.Is(requestingPlayer)))
            .ToList();
    }

    private static PlayerStatusDto MapStatus(
        GamePlayerBridgeClass player,
        GamePlayerBridgeClass requestingPlayer,
        GameClass game,
        bool isMe,
        bool isAdmin,
        bool isFinished = false)
    {
        var status = player.Status;
        // Non-admin viewing an opponent: hide score (they only see place on leaderboard, unless game is finished)
        var canSeeScore = isMe || isAdmin || isFinished;
        // A pending block/skip is private turn information. Keep it for the owner,
        // inspectable/admin views, and completed-game projections only.
        var canSeePrivateAction = isMe || isAdmin || isFinished;

        // Extract previous round logs from InGamePersonalLogsAll (split by "|||")
        var previousRoundLogs = "";
        if (isMe)
        {
            var splitLogs = status.InGamePersonalLogsAll.Split("|||");
            if (splitLogs.Length > 1 && splitLogs[^2].Length > 3)
            {
                previousRoundLogs = splitLogs[^2];
            }
        }

        var dto = new PlayerStatusDto
        {
            Score = canSeeScore ? status.GetScore() : -1,
            Place = status.GetPlaceAtLeaderBoard(),
            IsReady = status.IsReady,
            IsBlock = canSeePrivateAction && status.IsBlock,
            IsSkip = canSeePrivateAction && status.IsSkip,
            TurnInterference = isMe
                ? status.TurnInterference switch
                {
                    TurnInterferenceKind.Self => "self",
                    TurnInterferenceKind.Enemy => "enemy",
                    _ => "none",
                }
                : "none",
            IsAutoMove = status.IsAutoMove,
            ConfirmedPredict = status.ConfirmedPredict,
            ConfirmedSkip = canSeePrivateAction && status.ConfirmedSkip,
            LvlUpPoints = isMe ? status.LvlUpPoints : 0,
            MoveListPage = isMe ? status.MoveListPage : 1,
            PersonalLogs = isMe
                ? MapPersonalLogs(status.GetInGamePersonalLogs(), player, requestingPlayer, game)
                : "",
            PreviousRoundLogs = isMe
                ? MapPersonalLogs(previousRoundLogs, player, requestingPlayer, game)
                : previousRoundLogs,
            AllPersonalLogs = isMe
                ? MapPersonalLogs(status.InGamePersonalLogsAll, player, requestingPlayer, game)
                : "",
            ScoreSource = isMe
                ? MapPersonalLogs(status.ScoreSource, player, requestingPlayer, game, forClient: false)
                : "",
            DirectMessages = isMe ? player.WebMessages.Select(x => GameLocalization.TextForClient(player.DiscordId, x)).ToList() : new List<string>(),
            MediaMessages = isMe ? player.WebMediaMessages.Select(m => new MediaMessageDto
            {
                // Keep the canonical and authored English variants together. Replay snapshots are
                // language-neutral; MediaMessages.vue chooses the viewer's current locale.
                PassiveName = m.PassiveName,
                Text = m.Text,
                PassiveNameEnglish = m.PassiveNameEnglish,
                TextEnglish = m.TextEnglish,
                FileUrl = m.FileUrl,
                FileType = m.FileType,
                RoundsToPlay = m.RoundsToPlay,
            }).ToList() : new List<MediaMessageDto>(),
            IsAramRollConfirmed = status.IsAramRollConfirmed,
            IsDraftPickConfirmed = status.IsDraftPickConfirmed,
            AramRerolledPassivesTimes = isMe ? status.AramRerolledPassivesTimes : 0,
            AramRerolledStatsTimes = isMe ? status.AramRerolledStatsTimes : 0,
        };

        // Structured score breakdown (owner/admin/finished only). Final-round mechanics such as
        // Запах мусора and Осьминожка can add bonus entries after the normal round snapshot, so the
        // finished projection includes those still-current awarded bonus entries as well. Pending
        // regular entries belong to a never-played next round and must not appear in the final feed.
        if ((isMe || isAdmin || isFinished) && (isMe || !UnknownBug.Is(player)))
        {
            var entries = status.PreviousRoundScoreEntries.AsEnumerable();
            if (isFinished && status.ScoreEntries.Count > 0)
                entries = entries.Concat(status.ScoreEntries.Where(entry => entry.IsBonus));

            var mappedEntries = entries.Select(entry => new ScoreEntryDto
            {
                Source = MaskProScoreSource(entry.Source, requestingPlayer, game),
                Points = entry.Points,
                IsBonus = entry.IsBonus,
                IsNegative = entry.Points < 0,
            }).ToList();

            if (status.WasRoundScoreMultiplierReducedByTolya)
            {
                mappedEntries.Insert(0, new ScoreEntryDto
                {
                    Source = Tolya.RoundMultiplierPenaltySource,
                    Points = 0,
                    IsBonus = false,
                    IsNegative = true,
                    HidePoints = true,
                });
            }

            dto.ScoreBreakdown = new ScoreBreakdownDto
            {
                RoundMultiplier = status.ActualRoundMultiplier,
                ExpectedRoundMultiplier = status.ExpectedRoundMultiplier,
                Entries = mappedEntries,
            };
        }

        foreach (var entry in status.PlaceAtLeaderBoardHistory)
        {
            dto.PlaceHistory.Add(new PlaceHistoryDto
            {
                Round = entry.GameRound,
                Place = entry.Place,
            });
        }

        return dto;
    }

    /// <summary>
    /// Converts a remote avatar URL (Discord CDN, imgur, etc.) to a local /art/avatars/ path
    /// if the file exists locally. Otherwise returns the original URL.
    /// </summary>
    public static string GetLocalAvatarUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        try
        {
            // Extract filename from the URL
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);

            if (!string.IsNullOrEmpty(filename) && _localAvatars.Contains(filename))
            {
                return $"/art/avatars/{filename}";
            }
        }
        catch
        {
            // URL parsing failed — return as-is
        }

        return url;
    }

    // ── Quality bonus text helpers (mirror the logic from CharacterClass Get*Resist methods) ──

    private static string GetIntelligenceBonusText(CharacterClass character)
    {
        var skillBonus = character.GetIntelligenceQualitySkillBonus();
        if (skillBonus == 1.0m) return "";
        var pct = (skillBonus - 1) * 100;
        var plus = pct > 0 ? "+" : "";
        return $"{plus}{Math.Round(pct)}% Skill";
    }

    private static string GetSpeedBonusText(CharacterClass character)
    {
        var kite = character.GetSpeedQualityKiteBonus();
        return kite > 0 ? $"+{kite} Kite Distance" : "";
    }

    private static string GetPsycheBonusText(CharacterClass character)
    {
        var moralBonus = character.GetPsycheQualityMoralBonus();
        if (moralBonus == 1.0m) return "";
        var pct = (moralBonus - 1) * 100;
        var plus = pct > 0 ? "+" : "";
        return $"{plus}{Math.Round(pct)}% Moral";
    }

    private static readonly HashSet<string> ProVisiblePassiveSources = new(StringComparer.Ordinal)
    {
        "Запах мусора",
        "Чернильная завеса",
        "Еврей",
        "2kxaoc",
    };

    private static HashSet<string> GetHiddenProPassiveNames(
        GamePlayerBridgeClass viewer,
        GameClass game)
    {
        if (viewer?.IsProMode != true || viewer.PlayerType == 2 || game == null)
            return new HashSet<string>(StringComparer.Ordinal);

        var ownNames = viewer.GameCharacter.Passive
            .Select(passive => passive.PassiveName)
            .ToHashSet(StringComparer.Ordinal);
        return game.PlayersList
            .Where(other => other.GetPlayerId() != viewer.GetPlayerId())
            .SelectMany(other => other.GameCharacter.Passive)
            .Select(passive => passive.PassiveName)
            .Where(name => !ownNames.Contains(name) && !ProVisiblePassiveSources.Contains(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string MapPersonalLogs(
        string text,
        GamePlayerBridgeClass owner,
        GamePlayerBridgeClass viewer,
        GameClass game,
        bool forClient = true)
    {
        var hiddenNames = GetHiddenProPassiveNames(viewer, game);
        if (hiddenNames.Count > 0)
            text = PhrasePayload.MaskPassiveNames(text, hiddenNames, hidePhraseBody: true);

        var localized = forClient
            ? GameLocalization.TextForClient(owner.DiscordId, text)
            : GameLocalization.TextForUser(owner.DiscordId, text);
        foreach (var passiveName in hiddenNames)
            localized = localized.Replace(passiveName, "❓", StringComparison.Ordinal);

        return hiddenNames.Count == 0
            ? localized
            : localized
                .Replace("Неизвестно", "❓", StringComparison.Ordinal)
                .Replace("Unknown", "❓", StringComparison.Ordinal);
    }

    private static bool IsDepthsCallPromptActive(
        GameClass game,
        GamePlayerBridgeClass player) =>
        game?.CthulhuState.DepthsCallStageActive == true
        && player != null
        && game.CthulhuState.DepthsCallAnswers.TryGetValue(
            player.GetPlayerId(), out var depthsAnswer)
        && depthsAnswer == null;

    private static LocalizedText Localized(string canonicalText) =>
        new(canonicalText, GameLocalization.Text(canonicalText, GameLocalization.English));

    private static void ApplyPrivateCharacterDisplay(CharacterDto character)
    {
        character.DisplayName = Localized(character.Name);
        if (!string.IsNullOrEmpty(character.Description))
            character.DisplayDescription = Localized(character.Description);

        foreach (var passive in character.Passives)
        {
            passive.DisplayName = Localized(passive.Name);
            if (!string.IsNullOrEmpty(passive.Description))
                passive.DisplayDescription = Localized(passive.Description);
        }
    }

    private static void ApplyBoardEntityDisplay(FightEntryDto fight)
    {
        if (fight.AttackerCharName == Cthulhu.Nechto)
        {
            fight.AttackerCharDisplayName = Localized(fight.AttackerCharName);
            if (fight.AttackerName == Cthulhu.Nechto)
                fight.AttackerDisplayName = Localized(fight.AttackerName);
        }

        if (fight.DefenderCharName == Cthulhu.Nechto)
        {
            fight.DefenderCharDisplayName = Localized(fight.DefenderCharName);
            if (fight.DefenderName == Cthulhu.Nechto)
                fight.DefenderDisplayName = Localized(fight.DefenderName);
        }
    }

    private static string MaskProScoreSource(
        string source,
        GamePlayerBridgeClass viewer,
        GameClass game)
    {
        if (string.IsNullOrEmpty(source))
            return source;
        return GetHiddenProPassiveNames(viewer, game)
            .Any(name => source.Contains(name, StringComparison.Ordinal))
            ? "❓"
            : source;
    }

    private static string MaskProActionLabels(string logs)
    {
        if (string.IsNullOrEmpty(logs))
            return logs;
        return logs
            .Replace("(Блок)", "(?)", StringComparison.Ordinal)
            .Replace("(Скип)", "(?)", StringComparison.Ordinal)
            .Replace("(Block)", "(?)", StringComparison.Ordinal)
            .Replace("(Skip)", "(?)", StringComparison.Ordinal);
    }

    private static FightEntryDto MaskProFightOutcome(
        FightEntryDto fight,
        string myUsername,
        bool shouldMask)
    {
        if (!shouldMask
            || fight == null
            || fight.AttackerName == myUsername
            || fight.DefenderName == myUsername
            || fight.Outcome is not ("block" or "skip"))
            return fight;

        fight.Outcome = "unknown";
        fight.WinnerName = "";
        fight.TotalPointsWon = 0;
        return fight;
    }

    /// <summary>
    /// Scope fight data visibility: full details only for fights involving the requesting player.
    /// Other fights get stripped of numeric details but keep outcome, participants, and drops (visible to all).
    /// </summary>
    private static FightEntryDto ScopeFightEntry(FightEntryDto f, string myUsername, bool isAdmin,
        bool grantStreamPerspective = false, bool forceRedaction = false)
    {
        // The private character's combat factors stay owner-only even when the viewer
        // is the other participant or an ordinary admin inspecting the finished game.
        if (!forceRedaction)
        {
            if (isAdmin || grantStreamPerspective) return f;
            if (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername)) return f;
        }

        // Non-participant: strip detailed numeric data, keep participant info + outcome + drops
        return new FightEntryDto
        {
            // Keep: participant identity & outcome
            AttackerName = f.AttackerName,
            AttackerCharName = f.AttackerCharName,
            AttackerAvatar = f.AttackerAvatar,
            DefenderName = f.DefenderName,
            DefenderCharName = f.DefenderCharName,
            DefenderAvatar = f.DefenderAvatar,
            Outcome = f.Outcome,
            WinnerName = f.WinnerName,
            // Keep booleans (no numeric leak)
            IsNemesisMe = !forceRedaction && f.IsNemesisMe,
            IsNemesisTarget = !forceRedaction && f.IsNemesisTarget,
            IsTooGoodMe = !forceRedaction && f.IsTooGoodMe,
            IsTooGoodEnemy = !forceRedaction && f.IsTooGoodEnemy,
            IsTooStronkMe = !forceRedaction && f.IsTooStronkMe,
            IsTooStronkEnemy = !forceRedaction && f.IsTooStronkEnemy,
            IsStatsBetterMe = !forceRedaction && f.IsStatsBetterMe,
            IsStatsBetterEnemy = !forceRedaction && f.IsStatsBetterEnemy,
            UsedRandomRoll = !forceRedaction && f.UsedRandomRoll,
            QualityDamageApplied = !forceRedaction && f.QualityDamageApplied,
            HomelanderLaser = f.HomelanderLaser,
            // Keep drops (visible to all players)
            Drops = f.Drops,
            DroppedPlayerName = f.DroppedPlayerName,
            // Outcome already carries direction; per-round/Justice magnitudes stay private.
            Round1PointsWon = 0,
            PointsFromJustice = 0,
            TotalPointsWon = f.TotalPointsWon > 0 ? 1 : (f.TotalPointsWon < 0 ? -1 : 0),
            // Zero out all numeric details
            AttackerClass = "", DefenderClass = "",
            AttackerOriginalClass = "", DefenderOriginalClass = "",
            VersatilityIntel = 0, VersatilityStr = 0, VersatilitySpeed = 0,
            ScaleMe = 0, ScaleTarget = 0,
            NemesisMultiplier = 0,
            SkillMultiplierMe = 0, SkillMultiplierTarget = 0,
            PsycheDifference = 0,
            WeighingMachine = 0,
            RandomForPoint = 0,
            NemesisWeighingDelta = 0, ScaleWeighingDelta = 0,
            VersatilityWeighingDelta = 0, PsycheWeighingDelta = 0,
            SkillWeighingDelta = 0, JusticeWeighingDelta = 0,
            TooGoodRandomChange = 0, TooStronkRandomChange = 0,
            JusticeRandomChange = 0, NemesisRandomChange = 0,
            JusticeMe = 0, JusticeTarget = 0,
            RandomNumber = 0, MaxRandomNumber = 0,
            MoralChange = 0,
            AttackerMoralChange = 0, DefenderMoralChange = 0,
            ResistIntelDamage = 0, ResistStrDamage = 0, ResistPsycheDamage = 0,
            IntellectualDamage = false, EmotionalDamage = false,
            JusticeChange = 0, SkillGainedFromTarget = 0, SkillGainedFromClassAttacker = 0, SkillGainedFromClassDefender = 0,
            SkillDifferenceRandomModifier = 0,
            NemesisMultiplierSkillDifference = 0,
            // Portal swaps are visible on ordinary scoped fights, but the terminal
            // opponent projection exposes no mechanic-derived flags at all.
            PortalGunSwap = !forceRedaction && f.PortalGunSwap,
            // The intervention itself is public so the All Fights receipt survives
            // scoping. Its side, numeric delta, and flip result remain participant-only.
            StormAppeared = !forceRedaction && f.StormAppeared,
        };
    }

    private static FightEntryDto MaskPrivateFightIdentity(FightEntryDto fight, bool viewerIsTerminal)
    {
        if (viewerIsTerminal)
            return fight;

        var hideAttacker = UnknownBug.Is(fight.AttackerCharName);
        var hideDefender = UnknownBug.Is(fight.DefenderCharName);
        if (!hideAttacker && !hideDefender)
            return fight;

        var projection = fight.CopyForProjection();
        if (hideAttacker)
        {
            projection.AttackerCharName = "???";
            projection.AttackerAvatar = UnknownBug.MissingAvatar;
        }
        if (hideDefender)
        {
            projection.DefenderCharName = "???";
            projection.DefenderAvatar = UnknownBug.MissingAvatar;
        }
        return projection;
    }

    /// <summary>Remove hidden fight text snippets from global logs for non-admin players.
    /// Also strips Kira-hidden log snippets for players with the "Гений" passive.</summary>
    private static string StripHiddenLogs(string logs, List<string> hiddenSnippets,
        GamePlayerBridgeClass requestingPlayer, GameClass game)
    {
        if (string.IsNullOrEmpty(logs))
            return logs;

        if (hiddenSnippets != null && hiddenSnippets.Count > 0)
            foreach (var snippet in hiddenSnippets)
                logs = logs.Replace(snippet, "");

        // Genius: strip character-revealing logs for Kira
        if (requestingPlayer != null
            && requestingPlayer.GameCharacter.Passive.Any(p => p.PassiveName == "Гений")
            && game.KiraHiddenLogSnippets != null && game.KiraHiddenLogSnippets.Count > 0)
        {
            foreach (var snippet in game.KiraHiddenLogSnippets)
                logs = logs.Replace(snippet, "");
        }

        return logs;
    }

    /// <summary>
    /// Builds the full game chronicle (same structure as what gets sent to the LLM).
    /// Contains: Fight History (global logs with round numbers), then per-player personal logs.
    /// Replaces Discord usernames with character names throughout.
    /// </summary>
    public static string BuildFullChronicle(GameClass game, GamePlayerBridgeClass requestingPlayer = null)
    {
        var canRevealPrivateCharacter = UnknownBug.Is(requestingPlayer);

        // Build username → character name mapping
        var nameMap = game.PlayersList
            .Where(p => !string.IsNullOrWhiteSpace(p.DiscordUsername) && !string.IsNullOrWhiteSpace(p.GameCharacter.Name))
            .OrderByDescending(p => p.DiscordUsername.Length)
            .ToDictionary(p => p.DiscordUsername,
                p => UnknownBug.Is(p) && !canRevealPrivateCharacter ? "???" : p.GameCharacter.Name);

        var sb = new StringBuilder();

        // Section 1: Fight History (global logs already contain round headers like "Раунд #N")
        var globalLogs = game.GetAllGlobalLogs() ?? "";
        if (!string.IsNullOrWhiteSpace(globalLogs))
        {
            sb.AppendLine("**--- Fight History ---**");
            sb.AppendLine(ReplaceUsernames(globalLogs.Trim(), nameMap));
        }

        // Section 2: Per-player personal logs with round numbers
        var playersWithLogs = game.PlayersList
            .OrderBy(p => p.Status.GetPlaceAtLeaderBoard())
            .Where(p => !string.IsNullOrWhiteSpace(p.Status.InGamePersonalLogsAll))
            .Where(p => canRevealPrivateCharacter || !UnknownBug.Is(p))
            .ToList();

        if (playersWithLogs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**--- Ключевые моменты по персонажам ---**");

            foreach (var p in playersWithLogs)
            {
                sb.AppendLine();
                var heading = UnknownBug.Is(p) && !canRevealPrivateCharacter ? "???" : p.GameCharacter.Name;
                sb.AppendLine($"**{heading}** (#{p.Status.GetPlaceAtLeaderBoard()}, {p.Status.GetScore()} очков):");
                var rounds = p.Status.InGamePersonalLogsAll.Split("|||")
                    .Select(r => r.Trim())
                    .Where(r => r.Length > 0)
                    .ToList();
                for (var i = 0; i < rounds.Count; i++)
                {
                    sb.AppendLine($"*Раунд #{i + 1}:*");
                    sb.AppendLine(ReplaceUsernames(rounds[i], nameMap));
                    sb.AppendLine($"--");
                }
            }
        }

        var chronicle = sb.ToString().Trim();
        return canRevealPrivateCharacter ? chronicle : SanitizePrivateCharacterText(chronicle);
    }

    private static (List<string> Names, List<CharacterInfoDto> Characters) GetAssumptionCatalog(
        DiscordAccountClass account)
    {
        if (account == null) return (_allCharacterNames, _allCharacters);

        HashSet<string> unlockedCharacterNames;
        lock (account)
        {
            unlockedCharacterNames = new HashSet<string>(
                account.SeenCharacters ?? new List<string>(),
                StringComparer.Ordinal);
        }

        var characters = _allCharacters
            .Where(character => unlockedCharacterNames.Contains(character.Name))
            .ToList();
        return (characters.Select(character => character.Name).ToList(), characters);
    }

    private static string SanitizePrivateCharacterText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var privateName in new[]
                 {
                     UnknownBug.CharacterName,
                     UnknownBug.LegacyCharacterName,
                     Cthulhu.CharacterName
                 })
        {
            var isolatedName = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(privateName)}(?![\p{{L}}\p{{N}}_])";
            text = Regex.Replace(text, isolatedName, "???", RegexOptions.CultureInvariant);
        }

        return text;
    }

    /// <summary>
    /// Replaces all Discord usernames in text with character names.
    /// </summary>
    private static string ReplaceUsernames(string text, Dictionary<string, string> nameMap)
    {
        if (string.IsNullOrEmpty(text) || nameMap.Count == 0) return text;
        foreach (var pair in nameMap)
            text = text.Replace(pair.Key, pair.Value);
        return text;
    }
}
