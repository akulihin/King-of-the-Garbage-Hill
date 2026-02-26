using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Maps internal game objects to DTOs for the web client.
/// Handles visibility rules (e.g., don't show opponent passives that are hidden).
/// </summary>
public static class GameStateMapper
{
    // Cache of locally available avatar filenames (lowercase → actual filename)
    private static readonly HashSet<string> _localAvatars;

    // Cache of all visible character names (for prediction dropdowns)
    private static readonly List<string> _allCharacterNames;

    // Full character catalog with base stats (for prediction avatar/stat lookup by non-admins)
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
                var visible = chars.Where(c => c.Tier >= 0 && !c.Passive.Any(p => p.PassiveName == "Выдуманный персонаж")).OrderBy(c => c.Name).ToList();
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
    public static GameStateDto ToDto(GameClass game, GamePlayerBridgeClass requestingPlayer = null)
    {
        var isAdmin = requestingPlayer != null && requestingPlayer.PlayerType == 2;

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
            IsDraftPickPhase = game.IsDraftPickPhase,
            DraftOptions = game.IsDraftPickPhase && requestingPlayer != null
                && game.DraftOptions.TryGetValue(requestingPlayer.GetPlayerId(), out var draftOpts)
                ? draftOpts.Select((c, i) => new DraftOptionDto
                {
                    Name = c.Name,
                    Avatar = GetLocalAvatarUrl(c.Avatar),
                    Intelligence = c.GetIntelligence(),
                    Psyche = c.GetPsyche(),
                    Speed = c.GetSpeed(),
                    Strength = c.GetStrength(),
                    Description = c.Description,
                    Tier = c.Tier,
                    Cost = i == 0 ? 0 : 5,
                    Passives = c.Passive.Select(p => new PassiveDto
                    {
                        Name = p.PassiveName,
                        Description = p.PassiveDescription,
                        Visible = p.Visible,
                    }).ToList(),
                }).ToList()
                : null,
            IsKratosEvent = game.IsKratosEvent,
            GlobalLogs = isAdmin ? game.GetGlobalLogs() : StripHiddenLogs(game.GetGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game),
            AllGlobalLogs = isAdmin ? game.GetAllGlobalLogs() : StripHiddenLogs(game.GetAllGlobalLogs(), game.HiddenGlobalLogSnippets, requestingPlayer, game),
            MyPlayerId = requestingPlayer?.GetPlayerId(),
            MyPlayerType = requestingPlayer?.PlayerType ?? 0,
            PreferWeb = requestingPlayer?.PreferWeb ?? false,
            AllCharacterNames = _allCharacterNames,
            AllCharacters = _allCharacters,
        };

        // Map structured fight log for web animation (scoped: only own fights get full details)
        var myUsername = requestingPlayer?.DiscordUsername;
        dto.FightLog = game.WebFightLog
                .Where(f => !f.HiddenFromNonAdmin || isAdmin
                            || (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername)))
                .Select(f => ScopeFightEntry(f, myUsername, isAdmin))
                .ToList();

        var viewerIsBug = requestingPlayer != null
            && requestingPlayer.GameCharacter.Passive.Any(p => p.PassiveName == "Exploit");

        foreach (var player in game.PlayersList)
        {
            var isMe = requestingPlayer != null && player.GetPlayerId() == requestingPlayer.GetPlayerId();
            dto.Players.Add(MapPlayer(player, isMe, isAdmin, game.PlayersList, game, viewerIsBug));
        }

        foreach (var team in game.Teams)
        {
            dto.Teams.Add(new TeamDto
            {
                TeamId = team.TeamId,
                PlayerIds = team.TeamPlayers.ToList(),
            });
        }

        // Build full chronicle for Летопись tab when game is finished
        if (game.IsFinished)
        {
            dto.FullChronicle = BuildFullChronicle(game);
        }

        // Populate loot box result for finished games (requesting player only)
        if (game.IsFinished && requestingPlayer != null)
        {
            var questData = requestingPlayer.Passives?.QuestDataRef;
            if (questData?.LastLootBox != null && questData.LastLootBoxGameId == game.GameId)
            {
                dto.LootBoxResult = new LootBoxResultDto
                {
                    Rarity = questData.LastLootBox.Rarity,
                    ZbsAmount = questData.LastLootBox.ZbsAmount,
                };
            }

            // Populate newly unlocked achievements
            var achData = requestingPlayer.Passives?.AchievementDataRef;
            if (achData?.NewlyUnlocked != null && achData.NewlyUnlocked.Count > 0)
            {
                foreach (var achId in achData.NewlyUnlocked)
                {
                    var def = AchievementService.GetDefinition(achId);
                    if (def == null) continue;
                    dto.NewlyUnlockedAchievements.Add(new AchievementEntryDto
                    {
                        Id = def.Id,
                        Name = def.Name,
                        Description = def.Description,
                        Category = def.Category.ToString(),
                        IsSecret = def.IsSecret,
                        Icon = def.Icon,
                        Rarity = def.Rarity,
                        Target = def.Target,
                        Current = def.Target,
                        IsUnlocked = true,
                    });
                }
            }
        }

        return dto;
    }

    private static PlayerDto MapPlayer(GamePlayerBridgeClass player, bool isMe, bool isAdmin, List<GamePlayerBridgeClass> allPlayers, GameClass game = null, bool viewerIsBug = false)
    {
        var hasDeathNote = player.GameCharacter.Passive.Any(p => p.PassiveName == "Тетрадь смерти");

        var dto = new PlayerDto
        {
            PlayerId = player.GetPlayerId(),
            DiscordUsername = player.DiscordUsername,
            IsBot = player.IsBot(),
            IsWebPlayer = player.IsWebPlayer,
            TeamId = player.TeamId,
            IsDead = player.Passives.IsDead,
            DeathSource = player.Passives.DeathSource,
            IsKira = isMe && hasDeathNote,
            Character = MapCharacter(player.GameCharacter, isMe, isAdmin),
            Status = MapStatus(player, isMe, isAdmin),
        };

        // Predictions are private — only visible to the owning player
        if (isMe)
        {
            dto.Predictions = player.Predict
                .Select(p => new PredictDto { PlayerId = p.PlayerId, CharacterName = p.CharacterName })
                .ToList();
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
                RevealedPlayers = eyes.RevealedPlayers.Select(rp =>
                {
                    var revealed = allPlayers.Find(x => x.GetPlayerId() == rp);
                    return new DeathNoteRevealedPlayerDto
                    {
                        PlayerId = rp,
                        CharacterName = revealed?.GameCharacter.Name ?? "?"
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

        // Dopa — tactic choice needed
        if (isMe && player.GameCharacter.Passive.Any(p => p.PassiveName == "Законодатель меты")
            && !player.Passives.DopaMetaChoice.Triggered)
        {
            dto.DopaChoiceNeeded = true;
        }

        // Баг — exploit state visible to the Баг player
        var hasExploit = player.GameCharacter.Passive.Any(p => p.PassiveName == "Exploit");
        if (isMe && hasExploit)
        {
            dto.IsBug = true;
            if (game != null)
            {
                dto.ExploitState = new ExploitStateDto
                {
                    TotalExploit = game.TotalExploit,
                    FixedCount = game.ExploitPlayersList.Count(x => x.Passives.IsExploitFixed),
                    TotalPlayers = game.ExploitPlayersList.Count,
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

        // Exploit markers visible to the Баг viewer on all player cards
        if (viewerIsBug)
        {
            dto.IsExploitable = player.Passives.IsExploitable;
            dto.IsExploitFixed = player.Passives.IsExploitFixed;
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
                        pas.Jew = new JewStateDto { StolenPsyche = player.Passives.LeCrispAssassins.AdditionalPsycheCurrent };
                        anySet = true;
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
                        pas.Saitama = new SaitamaStateDto { DeferredPoints = player.Passives.SaitamaUnnoticed.DeferredPoints, DeferredMoral = player.Passives.SaitamaUnnoticed.DeferredMoral };
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
                    case "Пацаны":
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
                            PokerCount = tbButcher.PokerCount,
                            RegenLevel = tbKimiko.RegenLevel,
                            KimikoDisabled = tbKimiko.IsDisabled,
                            TotalJusticeBlocked = tbKimiko.TotalJusticeBlocked,
                            KompromatCount = tbMM.KompromatTargets.Count,
                            NextAttackGathersKompromat = tbMM.NextAttackGathersKompromat,
                            KompromatEntries = tbMM.KompromatTargets.Select(targetId =>
                            {
                                var target = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                                return new TheBoysKompromatEntryDto
                                {
                                    TargetName = target?.DiscordUsername ?? "",
                                    Hint = tbMM.KompromatHints.GetValueOrDefault(targetId, ""),
                                };
                            }).ToList(),
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
                            ShenActive = shen.ActiveThisTurn,
                            ShenTargetPosition = shen.TargetPosition,
                            ColaBuried = capsule.Buried,
                            ColaBuriedPosition = capsule.BuriedAtPosition,
                            ColaBuriedRound = capsule.BuriedOnRound,
                            HistoryRewritten = chronicler.HistoryRewritten,
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
                            anySet = true;
                        }
                        break;
                }
            }

            // Per-player marks — only visible to the affected player on their own card
            if (player.Passives.SellerVparitGovnaRoundsLeft > 0 || player.Passives.SellerTacticBonusEarned > 0)
            {
                var seller = game.PlayersList.Find(x =>
                    x.Passives.SellerVparitGovna.MarkedPlayers.Contains(player.GetPlayerId()));
                pas.SellerMark = new SellerMarkStateDto
                {
                    RoundsRemaining = player.Passives.SellerVparitGovnaRoundsLeft,
                    Debt = player.Passives.SellerTacticBonusEarned,
                    SellerName = seller?.DiscordUsername ?? "",
                };
                anySet = true;
            }

            // Show cancer widget to the infected player
            if (player.Passives.HasToxicMateCancer)
            {
                var cancerSource = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.ToxicMateCancerSourceId);
                pas.ToxicMateCancerOnMe = new ToxicMateCancerOnMeDto
                {
                    SourceName = cancerSource?.DiscordUsername ?? "",
                };
                anySet = true;
            }

            // Show cat widget to the player who has a cat on them
            if (player.Passives.KotikiCatType != "")
            {
                var catOwner = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.KotikiCatOwnerId);
                var roundsDeployed = 0;
                if (catOwner != null)
                {
                    var ownerAmbush = catOwner.Passives.KotikiAmbush;
                    if (player.Passives.KotikiCatType == "Минька")
                        roundsDeployed = ownerAmbush.MinkaRoundsOnEnemy;
                }
                pas.KotikiCatOnMe = new KotikiCatOnMeDto
                {
                    CatType = player.Passives.KotikiCatType,
                    CatOwnerName = catOwner?.DiscordUsername ?? "",
                    RoundsDeployed = roundsDeployed,
                };
                anySet = true;
            }

            // Show pawn widget to the player who is a Johan pawn
            if (player.Passives.IsJohanPawn)
            {
                var pawnOwner = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.JohanPawnOwnerId);
                pas.MonsterPawnOnMe = new MonsterPawnOnMeDto
                {
                    PawnOwnerName = pawnOwner?.DiscordUsername ?? "",
                };
                anySet = true;
            }

            // Show Geralt monster type widget to affected player
            if (player.Passives.GeraltMonsterType != null)
            {
                var geraltPlayer = game.PlayersList.Find(x => x.GameCharacter.Name == "Геральт");
                var mType = player.Passives.GeraltMonsterType.Value;
                pas.GeraltMonsterOnMe = new GeraltMonsterOnMeDto
                {
                    MonsterType = mType.ToString(),
                    MonsterColor = Geralt.GetMonsterColor(mType),
                    MonsterEmoji = Geralt.GetMonsterEmoji(mType),
                    ContractsOnType = geraltPlayer?.Passives.GeraltContracts.GetCount(mType) ?? 0,
                };
                anySet = true;
            }

            if (anySet) dto.PassiveAbilityStates = pas;
        }

        return dto;
    }

    private static CharacterDto MapCharacter(CharacterClass character, bool isMe, bool isAdmin)
    {
        // Non-admin viewing an opponent → mask character identity and stats
        if (!isMe && !isAdmin)
        {
            return new CharacterDto
            {
                Name = "???",
                Avatar = "/art/avatars/guess.png",
                AvatarCurrent = "/art/avatars/guess.png",
                Description = "",
                Tier = 0,
                Intelligence = -1, // sentinel: hidden
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

        var dto = new CharacterDto
        {
            Name = character.Name,
            Avatar = GetLocalAvatarUrl(character.Avatar),
            AvatarCurrent = GetLocalAvatarUrl(character.AvatarCurrent),
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

        // Show all passives to the owning player, only visible ones to opponents (admin)
        foreach (var passive in character.Passive)
        {
            if (isMe || passive.Visible)
            {
                dto.Passives.Add(new PassiveDto
                {
                    Name = passive.PassiveName,
                    Description = passive.PassiveDescription,
                    Visible = passive.Visible,
                });
            }
        }

        return dto;
    }

    private static PlayerStatusDto MapStatus(GamePlayerBridgeClass player, bool isMe, bool isAdmin)
    {
        var status = player.Status;
        // Non-admin viewing an opponent: hide score (they only see place on leaderboard)
        var canSeeScore = isMe || isAdmin;

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
            IsBlock = status.IsBlock,
            IsSkip = status.IsSkip,
            IsAutoMove = status.IsAutoMove,
            ConfirmedPredict = status.ConfirmedPredict,
            ConfirmedSkip = status.ConfirmedSkip,
            LvlUpPoints = isMe ? status.LvlUpPoints : 0,
            MoveListPage = isMe ? status.MoveListPage : 1,
            PersonalLogs = isMe ? status.GetInGamePersonalLogs() : "",
            PreviousRoundLogs = previousRoundLogs,
            AllPersonalLogs = isMe ? status.InGamePersonalLogsAll : "",
            ScoreSource = isMe ? status.ScoreSource : "",
            DirectMessages = isMe ? new List<string>(player.WebMessages) : new List<string>(),
            MediaMessages = isMe ? player.WebMediaMessages.Select(m => new MediaMessageDto
            {
                PassiveName = m.PassiveName,
                Text = m.Text,
                FileUrl = m.FileUrl,
                FileType = m.FileType,
                RoundsToPlay = m.RoundsToPlay,
            }).ToList() : new List<MediaMessageDto>(),
            IsAramRollConfirmed = status.IsAramRollConfirmed,
            IsDraftPickConfirmed = status.IsDraftPickConfirmed,
            AramRerolledPassivesTimes = isMe ? status.AramRerolledPassivesTimes : 0,
            AramRerolledStatsTimes = isMe ? status.AramRerolledStatsTimes : 0,
        };

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

    /// <summary>
    /// Scope fight data visibility: full details only for fights involving the requesting player.
    /// Other fights get stripped of numeric details but keep outcome, participants, and drops (visible to all).
    /// </summary>
    private static FightEntryDto ScopeFightEntry(FightEntryDto f, string myUsername, bool isAdmin)
    {
        // Admins and participants in the fight get full data
        if (isAdmin) return f;
        if (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername)) return f;

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
            IsNemesisMe = f.IsNemesisMe,
            IsNemesisTarget = f.IsNemesisTarget,
            IsTooGoodMe = f.IsTooGoodMe,
            IsTooGoodEnemy = f.IsTooGoodEnemy,
            IsTooStronkMe = f.IsTooStronkMe,
            IsTooStronkEnemy = f.IsTooStronkEnemy,
            IsStatsBetterMe = f.IsStatsBetterMe,
            IsStatsBetterEnemy = f.IsStatsBetterEnemy,
            UsedRandomRoll = f.UsedRandomRoll,
            QualityDamageApplied = f.QualityDamageApplied,
            // Keep drops (visible to all players)
            Drops = f.Drops,
            DroppedPlayerName = f.DroppedPlayerName,
            // Keep round results (just win/loss direction, no magnitudes)
            Round1PointsWon = f.Round1PointsWon,
            PointsFromJustice = f.PointsFromJustice,
            TotalPointsWon = f.TotalPointsWon > 0 ? 1 : (f.TotalPointsWon < 0 ? -1 : 0),
            // Zero out all numeric details
            AttackerClass = "", DefenderClass = "",
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
            // Portal swaps are visible to everyone
            PortalGunSwap = f.PortalGunSwap,
        };
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
    public static string BuildFullChronicle(GameClass game)
    {
        // Build username → character name mapping
        var nameMap = game.PlayersList
            .Where(p => !string.IsNullOrWhiteSpace(p.DiscordUsername) && !string.IsNullOrWhiteSpace(p.GameCharacter.Name))
            .OrderByDescending(p => p.DiscordUsername.Length)
            .ToDictionary(p => p.DiscordUsername, p => p.GameCharacter.Name);

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
            .ToList();

        if (playersWithLogs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**--- Ключевые моменты по персонажам ---**");

            foreach (var p in playersWithLogs)
            {
                sb.AppendLine();
                sb.AppendLine($"**{p.GameCharacter.Name}** (#{p.Status.GetPlaceAtLeaderBoard()}, {p.Status.GetScore()} очков):");
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

        return sb.ToString().Trim();
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
