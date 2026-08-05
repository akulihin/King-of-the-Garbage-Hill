using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CharacterPassives : IServiceSingleton
{
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly HelperFunctions _help;
    private readonly LoginFromConsole _log;
    private readonly SecureRandom _rand;
    private readonly CharactersPull _charactersPull;
    private readonly ClaudeHaikuService _haikuService;
    private readonly GameReaction _gameReaction;
    private readonly object _fairPredictionCatalogLock = new();
    private List<CharacterClass> _fairPredictionCatalog;

    public CharacterPassives(SecureRandom rand, HelperFunctions help,
        LoginFromConsole log, GameUpdateMess gameUpdateMess, CharactersPull charactersPull,
        ClaudeHaikuService haikuService, GameReaction gameReaction)
    {
        _rand = rand;
        _help = help;
        _log = log;
        _gameUpdateMess = gameUpdateMess;
        _charactersPull = charactersPull;
        _haikuService = haikuService;
        _gameReaction = gameReaction;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// When Saitama's win is deferred by "Неприметность", decide who actually pocketed the point.
    /// Normally it's <paramref name="naturalRecipientId"/> (the attacker who got the free win, or the
    /// co-attacker who got the kill). But if a Jew (Еврей), or an eligible ScamRat carry,
    /// also attacked <paramref name="fightTargetId"/>, that holder steals the point (see
    /// <see cref="HandleJews"/>), so Saitama reclaims it from the actual recipient. Edge case
    /// (several redirectors on one target): the first one is used to avoid inflating the docked total.
    /// </summary>
    private static Guid ResolveDeferredRecipient(GameClass game, GamePlayerBridgeClass saitama, Guid fightTargetId,
        Guid naturalRecipientId)
    {
        var naturalRecipient = game.PlayersList.Find(player => player.GetPlayerId() == naturalRecipientId);
        if (naturalRecipient != null
            && UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, naturalRecipient)))
            return Guid.Empty;

        var fightTarget = game.PlayersList.Find(player =>
            player.GetPlayerId() == fightTargetId);
        var jew = game.PlayersList.FirstOrDefault(p =>
            p.GetPlayerId() != saitama.GetPlayerId() &&
            p.GetPlayerId() != naturalRecipientId &&
            (p.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей")
             || ScamRat.CanCarryJointWin(p, fightTarget, game)) &&
            p.Status.WhoToAttackThisTurn.Contains(fightTargetId));
        var recipientId = jew?.GetPlayerId() ?? naturalRecipientId;
        var recipient = game.PlayersList.Find(player => player.GetPlayerId() == recipientId);
        return recipient != null && UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, recipient))
            ? Guid.Empty
            : recipientId;
    }

    private static void ApplyAttackTitanBoost(GamePlayerBridgeClass eren)
    {
        eren.FightCharacter.SetIntelligenceForOneFight(
            eren.FightCharacter.GetIntelligence() + 5, ErenYeager.AttackTitan);
        eren.FightCharacter.SetStrengthForOneFight(
            eren.FightCharacter.GetStrength() + 5, ErenYeager.AttackTitan);
        eren.FightCharacter.SetSpeedForOneFight(
            eren.FightCharacter.GetSpeed() + 5, ErenYeager.AttackTitan);
        eren.FightCharacter.SetPsycheForOneFight(
            eren.FightCharacter.GetPsyche() + 5, ErenYeager.AttackTitan);
    }

    private static void ApplyRasenganBoost(
        GamePlayerBridgeClass naruto,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        var jointAttackers = Naruto.GetJointAttackers(game, target, naruto);
        if (jointAttackers.Count < 2) return;

        var justice = jointAttackers.Sum(player => player.Passives.Naruto.JusticeSnapshot);
        naruto.FightCharacter.Justice.SetJusticeForOneFight(justice, Naruto.Rasengan);
        if (jointAttackers.Count == 2)
        {
            naruto.FightCharacter.SetStrengthForOneFight(
                naruto.FightCharacter.GetStrength() + 2, Naruto.Rasengan);
            naruto.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                Naruto.Rasengan,
                "РАСЕНГАН!",
                "Rasengan",
                "RASENGAN!") + "\n");
            return;
        }

        naruto.FightCharacter.SetIntelligenceForOneFight(
            naruto.FightCharacter.GetIntelligence() + 3, Naruto.Rasengan);
        naruto.FightCharacter.SetStrengthForOneFight(
            naruto.FightCharacter.GetStrength() + 3, Naruto.Rasengan);
        naruto.FightCharacter.SetSpeedForOneFight(
            naruto.FightCharacter.GetSpeed() + 3, Naruto.Rasengan);
        naruto.FightCharacter.SetPsycheForOneFight(
            naruto.FightCharacter.GetPsyche() + 3, Naruto.Rasengan);
        naruto.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            Naruto.Rasengan,
            "РАСЕНШУРИКЕН!!!!!!!!111",
            "Rasengan",
            "RASENSHURIKEN!!!!!!!!111") + "\n");
    }


    public List<GamePlayerBridgeClass> HandleEventsBeforeFirstRound(List<GamePlayerBridgeClass> playersList)
    {
        Naruto.InitializeTeam(playersList, () =>
            _charactersPull.GetAllCharactersNoFilter().Find(character =>
                character.Name == Naruto.CharacterName)
            ?? throw new InvalidOperationException("Наруто is missing from characters.json."));

        GordonFreeman.PlantInitialHeadcrabs(playersList);

        foreach (var player in playersList.ToList())
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Праведность":
                case "Скромность":
                case "Башня Vought":
                case "Супер \"Человек\"":
                case "Молоко":
                case "Лидер Семерки":
                    break;

                case Madara.GodOfShinobi:
                    if (player.GameCharacter.Name == Madara.CharacterName)
                    {
                        player.GameCharacter.SetMainSkill(0, Madara.ReanimatedBody, false);
                        player.GameCharacter.SetMoral(0, Madara.ReanimatedBody, false);
                        player.GameCharacter.RollSkillTargetForNextRound();
                        player.Predict.Clear();
                        player.Status.LvlUpPoints = 0;
                        player.Status.ConfirmedPredict = true;
                        player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                            Madara.GodOfShinobi,
                            "Ха? В каком я мире? Хм... \nЯ не знаю, кто такой этот Король Мусорной Горы, но я хочу его видеть! Смог ли этот Король достичь мира?",
                            "God of Shinobi",
                            "Huh? What world is this? Hmm...\nI don't know who this King of the Garbage Hill is, but I want to see him. Has this king achieved peace?") + "\n");
                    }
                    break;

                case "God Of War":
                    player.Status.AddInGamePersonalLogs("**Zeus! Your son has returned. I bring the destruction of Olympus!**\n");
                    break;

                case "Похищение души":
                    player.GameCharacter.SetClassSkillMultiplier(2);
                    break;

                case JonSnow.DumbBastard:
                    JonSnow.Initialize(player);
                    break;

                case "Искусство":
                    player.Status.AddInGamePersonalLogs(
                        "*Какая честь - умереть на поле боя... Начнем прямо сейчас!*\n");
                    break;

                case "Повторяет за myloran":
                    player.GameCharacter.AddIntelligence(player.GameCharacter.GetIntelligence() * -1 + 0, "Повторяет за myloran");
                    break;

                case "DeepList Pet":
                    if (playersList.Any(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "Weedwick Pet")))
                    {
                        player.Status.AddInGamePersonalLogs("**Чья эта безуманя собака?**: +4 Психики\n");
                        player.GameCharacter.AddPsyche(4, "Чья эта безуманя собака?", false);
                    }

                    break;

                case "Weedwick Pet":
                    if (playersList.Any(x => x.GameCharacter.Passive.Any(y => y.PassiveName == "DeepList Pet")))
                    {
                        player.Status.AddInGamePersonalLogs("**Чья эта безуманя собака?**: +4 Психики\n");
                        player.GameCharacter.AddPsyche(4, "Чья эта безуманя собака?", false);
                    }

                    player.Status.AddInGamePersonalLogs("Она всегда со мной, куда бы я не пошел...\n");
                    break;

                case "Первая кровь":
                    player.GameCharacter.SetAnySkillMultiplier(1);
                    break;

                case "Им это не понравится":
                    var excludedNames = new HashSet<string> { "Злой Школьник", "Глеб", "mylorik", "Загадочный Спартанец в маске" };
                    var candidates = playersList
                        .Where(p => p.GetPlayerId() != player.GetPlayerId()
                                    && !UnknownBug.Is(p)
                                    && !excludedNames.Contains(p.GameCharacter.Name))
                        .ToList();

                    Guid enemy1;
                    Guid enemy2;

                    // Most wanted: force Rick as enemy1
                    var rickMw1 = RickSanchez.FindMostWantedHolder(playersList, player);
                    if (rickMw1 != null)
                    {
                        enemy1 = rickMw1.GetPlayerId();
                        var mwPhrases = new[] {
                            "Да чего этим федералам надо от меня?!",
                            "Вся вселенная гоняется за рецептом моего особого топлива...",
                            "Боже! Может умнейший человек во вселенной просто спокойно провести время с внуком?!"
                        };
                        var mwPhrasesEnglish = new[] {
                            "What the hell do these Feds want from me?!",
                            "The whole universe is chasing the formula for my special fuel...",
                            "Jeez! Can the smartest man in the universe spend one quiet day with his grandson?!"
                        };
                        var mwIndex = _rand.Random(0, mwPhrases.Length - 1);
                        rickMw1.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                            "Most wanted", mwPhrases[mwIndex], "Most Wanted", mwPhrasesEnglish[mwIndex]) + "\n");
                    }
                    else if (candidates.Count > 0)
                    {
                        enemy1 = candidates[_rand.Random(0, candidates.Count - 1)].GetPlayerId();
                    }
                    else
                    {
                        // Fallback: pick any non-self player
                        var fallback = playersList.Where(p => p.GetPlayerId() != player.GetPlayerId()
                                                             && !UnknownBug.Is(p)).ToList();
                        enemy1 = fallback[_rand.Random(0, fallback.Count - 1)].GetPlayerId();
                    }

                    var candidates2 = candidates.Where(p => p.GetPlayerId() != enemy1).ToList();
                    if (candidates2.Count > 0)
                    {
                        enemy2 = candidates2[_rand.Random(0, candidates2.Count - 1)].GetPlayerId();
                    }
                    else
                    {
                        // Fallback: pick any non-self, non-enemy1 player
                        var fallback2 = playersList.Where(p => p.GetPlayerId() != player.GetPlayerId()
                                                              && p.GetPlayerId() != enemy1
                                                              && !UnknownBug.Is(p)).ToList();
                        enemy2 = fallback2.Count > 0
                            ? fallback2[_rand.Random(0, fallback2.Count - 1)].GetPlayerId()
                            : enemy1; // extreme edge case: only 2 players
                    }

                    player.Passives.SpartanMark.FriendList.Add(enemy1);
                    player.Passives.SpartanMark.FriendList.Add(enemy2);
                    break;

                case "Никому не нужен":
                    player.Status.HardKittyMinus(-30, "Никому не нужен");
                    player.Status.AddInGamePersonalLogs("Никому не нужен: -30 *Морали*\n");
                    var playerIndex = playersList.IndexOf(player);

                    for (var i = playerIndex; i < playersList.Count - 1; i++)
                        playersList[i] = playersList[i + 1];

                    playersList[^1] = player;
                    break;

                case ErenYeager.Sheep:
                    if (player.GameCharacter.Name != ErenYeager.CharacterName) break;
                    ErenYeager.MoveToLast(playersList, player);
                    player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                        ErenYeager.Sheep,
                        CharactersUniquePhrase.ErenSheepRoundPhrases[0],
                        GameLocalization.Text(ErenYeager.Sheep, GameLocalization.English),
                        CharactersUniquePhrase.ErenSheepRoundPhrasesEnglish[0]) + "\n");
                    break;

                case "Тигр топ, а ты холоп":
                    var tigr = player.Passives.TigrTop;

                    if (tigr is { TimeCount: > 0 } && !UnknownBug.Is(playersList.First()))
                    {
                        var tigrIndex = playersList.IndexOf(player);

                        playersList[tigrIndex] = playersList.First();
                        playersList[0] = player;
                        tigr.TimeCount--;
                        //game.Phrases.TigrTop.SendLog(tigrTemp);
                    }

                    break;

                case "Дерзкая школота":
                    player.GameCharacter.AddExtraSkill(100, "Дерзкая школота");
                    player.GameCharacter.SetIntelligence(9,"Дерзкая школота", false);
                    player.GameCharacter.SetStrength(9, "Дерзкая школота", false);
                    player.GameCharacter.SetSpeed(9, "Дерзкая школота", false);
                    player.GameCharacter.SetPsyche(9, "Дерзкая школота", false);
                    break;

                case "Main Ирелия":
                    player.GameCharacter.SetIntelligence(8, "Main Ирелия", false);
                    player.GameCharacter.SetStrength(8, "Main Ирелия", false);
                    player.GameCharacter.SetSpeed(8, "Main Ирелия", false);
                    player.GameCharacter.SetPsyche(8, "Main Ирелия", false);
                    break;

                    case "Много выебывается":
                        // First place, unless that would displace terminal-isolated unknown_bug.
                    if (!UnknownBug.Is(playersList.First()))
                    {
                        playerIndex = playersList.IndexOf(player);
                        playersList[playerIndex] = playersList.First();
                        playersList[0] = player;
                    }

                    //x3 class for target
                    //player.GameCharacter.SetTargetSkillMultiplier(2);
                    break;

                case "Лысина":
                    player.GameCharacter.AddExtraSkill(1000, "Лысина");
                    player.Status.AddInGamePersonalLogs("*100 отжиманий. 100 приседаний. 100 подъёмов корпуса. 10 км бега. КАЖДЫЙ ДЕНЬ. Побочный эффект - потеря волос.*\n");
                    break;

                case "Гигантские бобы":
                    player.Passives.RickGiantBeans.BaseIntelligence = player.GameCharacter.GetIntelligence();
                    break;

                case "Гений":
                    player.GameCharacter.AddIntelligence(4, "Гений");
                    break;

                case "L":
                    // Pick random enemy as L (prefer human players)
                    var lCandidates = playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId() && x.PlayerType != 404)
                        .ToList();
                    if (lCandidates.Count == 0)
                        lCandidates = playersList.Where(x => x.GetPlayerId() != player.GetPlayerId()).ToList();

                    // L is the sole random-mark exception to Rick's Most wanted.
                    var lTarget = lCandidates[_rand.Random(0, lCandidates.Count - 1)].GetPlayerId();

                    player.Passives.KiraL.LPlayerId = lTarget;
                    player.Status.AddInGamePersonalLogs($"Эй, Лайт, это Бог Смерти. Тебе выпал интересный противник: **{playersList.Find(x => x.GetPlayerId() == lTarget)!.DiscordUsername}** - это L.\n");
                    break;

                case "Неприметность":
                    // Compute top 2 enemies by combat power (Skill) to fight seriously against
                    var saitamaUnnoticed = player.Passives.SaitamaUnnoticed;
                    saitamaUnnoticed.SeriousTargets = playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .OrderByDescending(x => x.GameCharacter.GetSkill())
                        .Take(2)
                        .Select(x => x.GetPlayerId())
                        .ToList();
                    break;

                // Toxic Mate — "Fuck this game, I'm done.": start with -1000 moral
                case "Fuck this game, I'm done.":
                    player.GameCharacter.AddMoral(-1000, "Fuck this game, I'm done.");
                    break;

                // Toxic Mate — "FF 20": start with -20 bonus points
                case "FF 20":
                    player.Status.AddBonusPoints(-20, "FF 20");
                    break;

                // Toxic Mate — "INT": announce to all players
                case "INT":
                    foreach (var p in playersList)
                        p.Status.AddInGamePersonalLogs("**U are FoCKING retards!**\n");
                    break;

                case "Гоблины":
                    var gobPop = player.Passives.GoblinPopulation;
                    ApplyGoblinPopulationStats(player); // D7: sets the stat baseline (no external delta yet at game start)
                    // Воины дают +10% Скилла каждый (delta, чтобы не накапливалось между раундами)
                    var gobWarriorSkillDelta = gobPop.Warriors - gobPop.AppliedWarriorSkillBonus;
                    if (gobWarriorSkillDelta != 0)
                        player.GameCharacter.AddIntelligenceQualitySkillBonus(gobWarriorSkillDelta, "Гоблины", true);
                    gobPop.AppliedWarriorSkillBonus = gobPop.Warriors;
                    player.Status.AddInGamePersonalLogs($"Стая Гоблинов: {gobPop.TotalGoblins} гоблинов (⚔️{gobPop.Warriors} 🧙{gobPop.Hobs} ⛏️{gobPop.Workers})\n");
                    break;

                // TheBoys — Francie: shuffle opponents and assign first order (окно 3 хода)
                case "Francie":
                    var francie = player.Passives.TheBoysFrancie;
                    var opponents = SecureRandom.Shuffle(playersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .Select(x => x.GetPlayerId()));
                    francie.RemainingTargets = opponents;
                    // Most wanted: Рик всегда первая цель заказа
                    var rickMwFrancie = RickSanchez.FindMostWantedHolder(playersList, player);
                    if (rickMwFrancie != null && francie.RemainingTargets.Remove(rickMwFrancie.GetPlayerId()))
                        francie.RemainingTargets.Insert(0, rickMwFrancie.GetPlayerId());
                    if (francie.RemainingTargets.Count > 0)
                    {
                        francie.OrderTarget = francie.RemainingTargets[0];
                        francie.RemainingTargets.RemoveAt(0);
                        francie.OrderHistory.Add(francie.OrderTarget);
                        francie.OrderRoundsLeft = 3;
                        var targetName = playersList.Find(x => x.GetPlayerId() == francie.OrderTarget)?.DiscordUsername ?? "???";
                        player.Status.AddInGamePersonalLogs($"Заказ Француза: Цель — {targetName}. 3 хода на выполнение.\n");
                    }
                    break;

                // Salldorum — initialize chronicler position history
                case "Великий летописец":
                    if (player.GameCharacter.Name == "Salldorum")
                    {
                        player.Passives.SalldorumChronicler.PositionHistory = new List<int>();
                    }
                    break;

                // Геральт — assign monster types to 4 of 5 enemies
                case "Ведьмачьи заказы":
                    if (player.GameCharacter.Name == "Геральт")
                    {
                        var geraltContracts = player.Passives.GeraltContracts;
                        var enemies = playersList.Where(x => x.GetPlayerId() != player.GetPlayerId()
                                                           && !UnknownBug.Is(x)).ToList();

                        // Fixed type assignments
                        var fixedTypes = new Dictionary<string, Geralt.MonsterType>
                        {
                            { "Sirinoks", Geralt.MonsterType.Драконы },
                            { "Weedwick", Geralt.MonsterType.Волколаки },
                            { "Вампур", Geralt.MonsterType.Вампиры },
                            { "mylorik", Geralt.MonsterType.Утопцы },
                            { "Осьминожка", Geralt.MonsterType.Утопцы },
                            { "Краборак", Geralt.MonsterType.Утопцы },
                            { "Братишка", Geralt.MonsterType.Утопцы },
                        };

                        var assigned = new List<Guid>();
                        var typeCounts = new Dictionary<Geralt.MonsterType, int>
                        {
                            { Geralt.MonsterType.Утопцы, 0 },
                            { Geralt.MonsterType.Волколаки, 0 },
                            { Geralt.MonsterType.Вампиры, 0 },
                            { Geralt.MonsterType.Драконы, 0 },
                        };

                        // Assign fixed types first
                        foreach (var enemy in enemies)
                        {
                            if (fixedTypes.TryGetValue(enemy.GameCharacter.Name, out var monsterType) && assigned.Count < 4)
                            {
                                geraltContracts.EnemyTypes[enemy.GetPlayerId()] = monsterType;
                                enemy.Passives.GeraltMonsterType = monsterType;
                                assigned.Add(enemy.GetPlayerId());
                                typeCounts[monsterType]++;
                            }
                        }

                        // Assign random types to remaining (fill up to 4)
                        var unassigned = enemies.Where(x => !assigned.Contains(x.GetPlayerId())).ToList();
                        foreach (var enemy in unassigned)
                        {
                            if (assigned.Count >= 4) break;
                            // Pick least-used type
                            var leastUsedType = typeCounts.OrderBy(x => x.Value).ThenBy(_ => _rand.Random(0, 100)).First().Key;
                            geraltContracts.EnemyTypes[enemy.GetPlayerId()] = leastUsedType;
                            enemy.Passives.GeraltMonsterType = leastUsedType;
                            assigned.Add(enemy.GetPlayerId());
                            typeCounts[leastUsedType]++;
                        }

                        // Initialize ContractProcsOnEnemy
                        foreach (var enemyId in assigned)
                            geraltContracts.ContractProcsOnEnemy[enemyId] = 0;

                        // Start with 1 contract per assigned monster type
                        foreach (var type in typeCounts.Where(x => x.Value > 0).Select(x => x.Key))
                            geraltContracts.AddCount(type, 1);
                    }
                    break;
            }

        var initialGordon = playersList.Find(GordonFreeman.Is);
        if (initialGordon != null)
            GordonFreeman.HandleRoundPhrase(initialGordon, 1);
        Homelander.MoveToInitialLead(playersList);
        JonSnow.FinalizeInitialPositions(playersList);
        OmniMan.MoveFromInitialLead(playersList);

        return playersList;
    }


    //handle during fight
    public void HandleDefenseBeforeFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        OmniMan.HandleEnemyAttack(target, me, game);

        // Salldorum — Временная капсула: apply pending speed bonus (after DeepCopy)
        var capsuleDef = target.Passives.SalldorumTimeCapsule;
        if (capsuleDef.SpeedBonusPending > 0)
        {
            target.FightCharacter.AddSpeedForOneFight(capsuleDef.SpeedBonusPending, "Временная капсула");
            capsuleDef.SpeedBonusPending = 0;
        }

        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case Homelander.Modesty:
                    Homelander.SuppressJustice(target, me);
                    break;

                case JonSnow.IAmJonSnow:
                    JonSnow.ApplyBaseJustice(target);
                    break;

                case Madara.GodOfShinobi:
                    var shinobiDefenseSkill = Madara.GetGodOfShinobiSkill(target);
                    if (shinobiDefenseSkill > 0)
                        target.FightCharacter.SetSkillForOneFight(
                            shinobiDefenseSkill, Madara.GodOfShinobi);
                    break;

                case "Великий летописец":
                    ApplySalldorumChroniclerMultiplier(target, me, game);
                    break;

                case "Следит за игрой":
                    foreach (var metaPlayer in target.Passives.YongGlebMetaClass)
                    {
                        if (target.GetPlayerId() == metaPlayer && target.Status.IsBlock)
                        {
                            target.Status.AddBonusPoints(1, "Следит за игрой");
                            game.Phrases.YongGlebMeta.SendLog(target, true);
                        }
                    }
                    break;

                case "Оборотень":
                    /*
                    var myTempStrength = me.GameCharacter.GetStrength();
                    var targetTempStrength = target.GameCharacter.GetStrength();
                    me.GameCharacter.SetStrengthForOneFight(targetTempStrength, "Оборотень");
                    target.GameCharacter.SetStrengthForOneFight(myTempStrength, "Оборотень");*/

                    /*var myTempSkillMain = me.GameCharacter.GetSkillForOneFight();
                    var targetTempSkill = target.GameCharacter.GetSkillForOneFight();
                    me.GameCharacter.SetSkillForOneFight(targetTempSkill, "Оборотень");
                    target.GameCharacter.SetSkillForOneFight(myTempSkillMain, "Оборотень");*/
                    break;

                case "Сомнительная тактика":
                    var deep = target.Passives.DeepListDoubtfulTactic;

                    if (!deep.FriendList.Contains(me.GetPlayerId()))
                        target.Status.IsAbleToWin = false;
                    break;

                case ErenYeager.Fighter:
                    if (target.GameCharacter.Name == ErenYeager.CharacterName && !UnknownBug.Is(me))
                        me.Passives.ErenHatredMark = 2;
                    break;

                case ErenYeager.AttackTitan:
                    if (target.GameCharacter.Name == ErenYeager.CharacterName
                        && target.Passives.Eren.AttackTitanActiveThisRound)
                        ApplyAttackTitanBoost(target);
                    break;

                case "Неуязвимость":
                    me.FightCharacter.SetStrengthForOneFight(0, "Неуязвимость");
                    break;

                case "Панцирь":
                    var сraboRackShell = target.Passives.CraboRackShell;
                    if (сraboRackShell != null)
                        if (!сraboRackShell.FriendList.Contains(me.GetPlayerId()))
                        {
                            сraboRackShell.FriendList.Add(me.GetPlayerId());
                            сraboRackShell.CurrentAttacker = me.GetPlayerId();
                            target.GameCharacter.AddMoral(3, "Панцирь");
                            target.GameCharacter.AddExtraSkill(33, "Панцирь");
                            target.Status.IsBlock = true;
                        }

                    break;

                case "Хождение боком":
                    me.FightCharacter.SetSpeedForOneFight(0, "Хождение боком");
                    break;

                case "Ничего не понимает":

                    var shark = target.Passives.SharkBoole;

                    if (!shark.FriendList.Contains(me.GetPlayerId()))
                    {
                        shark.FriendList.Add(me.GetPlayerId());
                        me.GameCharacter.AddIntelligence(-1, "Ничего не понимает");
                    }

                    me.FightCharacter.SetIntelligenceForOneFight(0, "Ничего не понимает");
                    break;

                case "Я щас приду":
                    if (_rand.Luck(1, 9))
                    {
                        var acc = target.Passives.GlebChallengerTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                            return;


                        if (!target.Status.IsSkip)
                        {
                            target.Status.IsSkip = true;
                            target.Passives.GlebSkip = true;
                            game.Phrases.GlebComeBackPhrase.SendLog(target, true);

                            // Enemy sees message in their log
                            game.Phrases.GlebComeBackEnemy.SendLog(target, false);

                            var glebSkipFriendList = target.Passives.GlebSkipFriendList;
                            if (!glebSkipFriendList.FriendList.Contains(me.GetPlayerId()))
                                glebSkipFriendList.FriendList.Add(me.GetPlayerId());
                        }
                    }

                    break;

                case "Гребанные ассассины":
                    var ok = true;

                    //Сомнительная тактика
                    if (me.GameCharacter.Name == "DeepList")
                    {
                        deep = me.Passives.DeepListDoubtfulTactic;
                        if (!deep.FriendList.Contains(me.GetPlayerId()))
                            ok = false;
                    }
                    //end Сомнительная тактика

                    //10-7
                    if (me.FightCharacter.GetStrength() - target.FightCharacter.GetStrength() >= 3 && !target.Status.IsBlock && !target.Status.IsSkip && ok)
                    {
                        target.Status.IsAbleToWin = false;
                        game.Phrases.LeCrispAssassinsPhrase.SendLog(target, false);
                    }

                    break;

                case "Раммус мейн":
                    if (target.Status.IsBlock && game.RoundNo <= 10 && !UnknownBug.Is(me)
                        && !Sirinoks.BlocksAutowinFrom(me, target, game))
                    {
                        // target.Status.IsBlock = false;
                        me.Status.IsAbleToWin = false;
                        me.Status.IsArmorBreak = true;
                        var tolya = target.Passives.TolyaRammusTimes;
                        tolya.FriendList.Add(me.GetPlayerId());
                    }
                    break;

                case "Одиночество":
                    var hard = target.Passives.HardKittyLoneliness;
                    if (hard is { Activated: false })
                    {
                        target.Status.AddRegularPoints(1, "Одиночество");
                        game.Phrases.HardKittyLonelyPhrase.SendLog(target, true);
                        //uncomment it when DeepList desides to make it 1 per round again...
                        //hard.Activated = true;
                        var hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.GetPlayerId());
                        if (hardEnemy == null)
                        {
                            hard.AttackHistory.Add(new HardKitty.LonelinessSubClass(me.GetPlayerId()));
                            hardEnemy = hard.AttackHistory.Find(x => x.EnemyId == me.GetPlayerId());
                        }

                        switch (game.RoundNo)
                        {
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                                hardEnemy!.Times += 1;
                                break;
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                                hardEnemy!.Times += 2;
                                break;
                            case 10:
                                hardEnemy!.Times += 4;
                                break;
                        }
                    }

                    break;

                case "Запах мусора":
                    if (UnknownBug.Is(me)) break;

                    var mitsuki = target.Passives.MitsukiGarbageList;


                    var found = mitsuki.Training.Find(x => x.EnemyId == me.GetPlayerId());
                    if (found != null)
                        found.Times++;
                    else
                        mitsuki.Training.Add(new Mitsuki.GarbageSubClass(me.GetPlayerId()));

                    break;

                case "Неприметность":
                    if (game.RoundNo  >= 10) break;
                    var saitamaAtkUnnoticedAfter = target.Passives.SaitamaUnnoticed;
                    // Saitama holds back against enemies NOT in top 2 — they appear to win
                    var saitamaDefUnnoticed = target.Passives.SaitamaUnnoticed;
                    if (!saitamaDefUnnoticed.SeriousTargets.Contains(me.GetPlayerId()))
                    {
                        saitamaDefUnnoticed.PretendedLossThisFight = true;
                        target.Status.IsAbleToWin = false;
                        game.Phrases.SaitamaHoldsBack.SendLog(target, false);

                        // Bank the round-multiplied win point to reclaim on round 10, attributed to the
                        // attacker who pockets the free win (or a Jew who steals it). Saitama already loses
                        // this defensive fight (IsAbleToWin=false → he scores 0, the attacker gets +1), so we
                        // must NOT also dock him a regular point — that was double-penalising him.
                        var defRecipient = ResolveDeferredRecipient(game, target, target.GetPlayerId(), me.GetPlayerId());
                        if (defRecipient != Guid.Empty)
                            saitamaAtkUnnoticedAfter.AddDeferred(defRecipient, game.RoundNo);

                        // Bank the foregone underdog moral too (only applies when Saitama had the worse place).
                        // Deferred-only as well — no upfront moral loss; it is restored/converted on round 10.
                        var moralGain = target.Status.GetPlaceAtLeaderBoard() - me.Status.GetPlaceAtLeaderBoard();
                        if (moralGain > 0 && game.RoundNo > 1)
                        {
                            saitamaAtkUnnoticedAfter.DeferredMoral += moralGain;
                        }
                    }
                    else
                    {
                        game.Phrases.SaitamaSerious.SendLog(target, false);
                    }
                    break;

                case "Огурчик Рик":
                    if (!UnknownBug.Is(me) && target.Passives.RickPickle.PickleTurnsRemaining > 0
                        && !Sirinoks.BlocksAutowinFrom(me, target, game))
                    {
                        target.Passives.RickPickle.WasAttackedAsPickle = true;
                        me.Status.IsAbleToWin = false;
                    }
                    break;

                // Вороны (defense): reduce attacker speed by 20% per crow (rounded up)
                case "Вороны":
                    var crowsDef = target.Passives.ItachiCrows;
                    if (crowsDef.CrowCounts.TryGetValue(me.GetPlayerId(), out var crowCountDef) && crowCountDef > 0)
                    {
                        var attackerSpeedDef = me.FightCharacter.GetSpeed();
                        var ignoredDef = (int)Math.Ceiling(attackerSpeedDef * 0.20 * crowCountDef);
                        me.FightCharacter.AddSpeedForOneFight(-Math.Min(ignoredDef, attackerSpeedDef), "Вороны");
                    }
                    break;

                // Аматерасу: only works on attack now (removed from defense)


                // Napoleon — Мирный договор: enforce treaty from previous round
                case "Мирный договор":
                    if (!UnknownBug.Is(me)
                        && !Sirinoks.BlocksAutowinFrom(me, target, game)
                        && target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                    {
                        me.Status.IsAbleToWin = false;
                        target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Remove(me.GetPlayerId());
                        game.Phrases.NapoleonPeaceTreaty.SendLog(target, false);
                    }
                    break;

                // Napoleon — Меня надо знать в лицо: auto-win first fight vs each unique attacker
                case "Меня надо знать в лицо":
                    var napFirstFight = target.Passives.NapoleonFirstFightList;
                    if (!UnknownBug.Is(me)
                        && !Sirinoks.BlocksAutowinFrom(me, target, game)
                        && !napFirstFight.FriendList.Contains(me.GetPlayerId()))
                    {
                        napFirstFight.FriendList.Add(me.GetPlayerId());
                        me.Status.IsAbleToWin = false;
                        game.Phrases.NapoleonFace.SendLog(target, false);
                    }
                    break;

                case "Тоннели Гоблинов":
                    // 50% chance to escape if goblin speed >= enemy speed + 2
                    if (!Sirinoks.BlocksAutowinFrom(me, target, game)
                        && target.FightCharacter.GetSpeed() >= me.FightCharacter.GetSpeed() + 2)
                    {
                        if (_rand.Random(0, 99) < 50)
                        {
                            me.Status.IsAbleToWin = false;
                            game.Phrases.GoblinTunnelEscape.SendLog(target, false);
                            target.Status.AddInGamePersonalLogs("Тоннели Гоблинов: Сбежали!\n");
                        }
                    }
                    break;

                case "Гоблины":
                    // Stats already include warrior/hob bonuses via Set calls
                    break;

                // TheBoys — Kimiko: поглощение справедливости атакующего при обороне (Регенерация)
                case "Kimiko":
                    var kimikoDefBefore = target.Passives.TheBoysKimiko;
                    if (target.Passives.TheBoysButcher.SuperDickActive) break; // СуперМудень отключает Кимико
                    if (UnknownBug.Is(me)) break;
                    var currentJustice = me.FightCharacter.Justice.GetRealJusticeNow();
                    if (kimikoDefBefore.RegenLevel == 0)
                    {
                        if (kimikoDefBefore.IsDisabled) break;
                        var reduction = Math.Min(currentJustice, 1);
                        if (reduction > 0)
                        {
                            me.FightCharacter.Justice.SetJusticeForOneFight(
                                Math.Max(0, currentJustice - reduction), "Воля к жизни Кимико");
                            kimikoDefBefore.TotalJusticeBlocked += reduction;
                            target.Status.AddInGamePersonalLogs(
                                $"Kimiko поглотила {reduction} Справедливости (всего: {kimikoDefBefore.TotalJusticeBlocked})\n");
                            game.Phrases.TheBoysKimikoRegen.SendLog(target, false);
                        }
                    }
                    else
                    {
                        var stealLimit = kimikoDefBefore.RegenLevel + 1;
                        var fightJusticeBeforeSteal = currentJustice;
                        me.FightCharacter.Justice.SetJusticeForOneFight(
                            Math.Max(0, currentJustice - stealLimit), "Регенирация");
                        var fightJusticeStolen =
                            fightJusticeBeforeSteal - me.FightCharacter.Justice.GetRealJusticeNow();
                        if (fightJusticeStolen <= 0) break;

                        var kimikoFightJustice =
                            target.FightCharacter.Justice.GetRealJusticeNow();
                        target.FightCharacter.Justice.SetJusticeForOneFight(
                            kimikoFightJustice + fightJusticeStolen,
                            "Регенирация");

                        // Settle the real resource on GameCharacter. Other fights still read their
                        // independent round snapshot and therefore cannot observe this drain.
                        var persistentJusticeBeforeSteal =
                            me.GameCharacter.Justice.GetRealJusticeNow();
                        me.GameCharacter.Justice.SetRealJusticeNow(
                            Math.Max(0, persistentJusticeBeforeSteal - stealLimit),
                            "Регенирация",
                            isLog: false);
                        var stolenJustice = persistentJusticeBeforeSteal
                                            - me.GameCharacter.Justice.GetRealJusticeNow();
                        if (stolenJustice > 0)
                        {
                            target.GameCharacter.Justice.AddRealJusticeNow(stolenJustice);
                            kimikoDefBefore.TotalJusticeBlocked += stolenJustice;
                            target.Status.AddInGamePersonalLogs(
                                $"Kimiko похитила {stolenJustice} Справедливости (всего: {kimikoDefBefore.TotalJusticeBlocked})\n");
                            game.Phrases.TheBoysKimikoRegen.SendLog(target, false);
                            if (kimikoDefBefore.LivingWeapon)
                            {
                                target.Status.AddRegularPoints(stolenJustice, "Живое Оружие");
                                target.Passives.AchievementTracker.LivingWeaponJusticeBlocked += stolenJustice;
                            }
                        }
                    }
                    break;

                // Геральт — Масло (defense): disabled — oil only works on attack
                case "Масло":
                    break;

                // Геральт — Шевелись, Плотва (defense): disabled — Plotva only works on attack
                case "Шевелись, Плотва":
                    break;

                // Геральт — Медитация (defense): Lambert skill zero
                case "Медитация":
                    if (target.GameCharacter.Name == "Геральт" && target.Passives.GeraltMeditation.LambertActive)
                    {
                        target.FightCharacter.SetSkillForOneFight(0, "Медитация");
                    }
                    break;
            }

        // Napoleon ally treaty: if defender is Napoleon's ally, enforce treaty
        var napoleonForAlly = game.PlayersList.Find(x =>
            x.GameCharacter.Passive.Any(p => p.PassiveName == "Мирный договор") &&
            x.Passives.NapoleonAlliance.AllyId == target.GetPlayerId());
        if (!UnknownBug.Is(me) && napoleonForAlly != null
                                      && !Sirinoks.BlocksAutowinFrom(me, napoleonForAlly, game)
                                      && napoleonForAlly.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
        {
            me.Status.IsAbleToWin = false;
            napoleonForAlly.Passives.NapoleonPeaceTreaty.TreatyEnemies.Remove(me.GetPlayerId());
            game.Phrases.NapoleonPeaceTreaty.SendLog(target, false);
        }
    }

    public void HandleDefenseAfterBlockOrFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Shield":
                    if (target.GameCharacter.Name != DoomGuy.CharacterName || !target.Status.IsBlock
                        || me.Status.IsTargetBlocked != target.GetPlayerId()) break;
                    var doomShield = target.Passives.DoomGuy;
                    doomShield.BlocksThisRound++;
                    if (doomShield.GetActive(DoomGuy.Shield) == DoomGuy.HellBlock
                        && !doomShield.HellBlockUsed && doomShield.BlocksThisRound >= 2)
                    {
                        doomShield.HellBlockUsed = true;
                        target.GameCharacter.AddExtraSkill(666, DoomGuy.HellBlock);
                    }
                    if (doomShield.GetActive(DoomGuy.Shield) == DoomGuy.CounterAttack)
                    {
                        if (!UnknownBug.Is(me))
                        {
                            doomShield.CounterAttackMarks[me.GetPlayerId()] = game.RoundNo + 1;
                            target.Status.AddInGamePersonalLogs(
                                $"Контр-атака: {me.DiscordUsername} уязвим на следующий ход.\n");
                        }
                    }
                    break;

                // Napoleon — Мирный договор: register treaty when enemy attacks Napoleon's block
                case "Мирный договор":
                    if (target.Status.IsBlock && !UnknownBug.Is(me))
                    {
                        if (!target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                            target.Passives.NapoleonPeaceTreaty.TreatyEnemies.Add(me.GetPlayerId());
                    }
                    break;

                case "Гоблины тупые, но не идиоты":
                    // Ziggurat build logic moved to HandleEndOfRound (fires on block regardless of attacker)
                    break;

                // Геральт — block phrase
                case "Ведьмачьи заказы":
                    if (target.GameCharacter.Name == "Геральт" && target.Status.IsBlock)
                    {
                        game.Phrases.GeraltBlock.SendLog(target, false);
                    }
                    break;

                // TheBoys — Kimiko: +10 Скилла за победу в обороне, +20 за успешный блок
                case "Kimiko":
                    if (target.Passives.TheBoysButcher.SuperDickActive) break; // СуперМудень отключает Кимико
                    if (target.Status.IsBlock)
                        target.GameCharacter.AddExtraSkill(20, "Kimiko (блок)");
                    else if (target.Status.IsWonThisCalculation != Guid.Empty)
                        target.GameCharacter.AddExtraSkill(10, "Kimiko");
                    break;
            }

        // Napoleon ally treaty: if defender is Napoleon's ally and is blocking, register treaty on Napoleon
        if (target.Status.IsBlock)
        {
            var napoleonForAllyBlock = game.PlayersList.Find(x =>
                x.GameCharacter.Passive.Any(p => p.PassiveName == "Мирный договор") &&
                x.Passives.NapoleonAlliance.AllyId == target.GetPlayerId());
            if (!UnknownBug.Is(me) && napoleonForAllyBlock != null
                                      && !napoleonForAllyBlock.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(me.GetPlayerId()))
                napoleonForAllyBlock.Passives.NapoleonPeaceTreaty.TreatyEnemies.Add(me.GetPlayerId());
        }
    }


    public void HandleDefenseAfterBlockOrFightOrSkip(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Гребанные ассассины":
                    //5-2 = 3
                    if (me.FightCharacter.GetStrength() - target.FightCharacter.GetStrength() < 3)
                    {
                        var leCrip = target.Passives.LeCrispAssassins;
                        leCrip.AdditionalPsycheForNextRound += 1;
                    }

                    break;
            }
    }

    public void HandleDefenseAfterFight(GamePlayerBridgeClass target, GamePlayerBridgeClass me, GameClass game)
    {
        foreach (var passive in target.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case Homelander.Righteousness:
                    Homelander.RecordEnemyVictory(target, me, game);
                    break;

                case "Я щас приду":
                    var glebSkipFriendList = target.Passives.GlebSkipFriendList;
                    var glebSkipFriendListDone = target.Passives.GlebSkipFriendListDone;

                    if (glebSkipFriendList.FriendList.Contains(me.GetPlayerId()) &&
                        !glebSkipFriendListDone.FriendList.Contains(me.GetPlayerId()))
                    {
                        glebSkipFriendListDone.FriendList.Add(me.GetPlayerId());
                        me.GameCharacter.AddMoral(9, "Я щас приду", false);
                        me.Status.AddInGamePersonalLogs("Я щас приду: +9 *Морали*. Вы дождались Глеба!!! Празднуем!\n");
                    }

                    break;

                case "Импакт":
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var lePuska = target.Passives.LeCrispImpact;

                        lePuska.IsLost = true;
                    }

                    break;

                case "Mute":
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var hardKittyMute = target.Passives.HardKittyMute;

                        if (!hardKittyMute.UniquePlayers.Contains(me.GetPlayerId()))
                        {
                            hardKittyMute.UniquePlayers.Add(me.GetPlayerId());
                            me.Status.AddRegularPoints(1, "Mute");
                            game.Phrases.HardKittyMutedPhrase.SendLog(target, false);
                        }
                    }

                    break;

                case "Доебаться":
                    var hardKittyDoebatsya = target.Passives.HardKittyDoebatsya;

                    var found = hardKittyDoebatsya.LostSeriesCurrent.Find(x => x.EnemyPlayerId == me.GetPlayerId());
                    if (found != null) hardKittyDoebatsya.EnemyPlayersLostTo.Add(me.GetPlayerId());
                    //found.Series = 0;
                    //game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(target, false);
                    break;

                case "Гигантские бобы":
                    var beansDefAfter = target.Passives.RickGiantBeans;
                    if (beansDefAfter.IngredientsActive && beansDefAfter.IngredientTargets.Contains(me.GetPlayerId())
                        && target.Status.IsWonThisCalculation == me.GetPlayerId())
                    {
                        beansDefAfter.IngredientTargets.Remove(me.GetPlayerId());
                        beansDefAfter.BeanStacks++;
                        target.GameCharacter.AddStrength(-1, "Гигантские бобы");
                        target.GameCharacter.AddSpeed(-1, "Гигантские бобы");
                        target.MinusPsyche(game, -1, "Гигантские бобы");
                        var oldFakeBeansD = beansDefAfter.FakeIntelligence;
                        beansDefAfter.FakeIntelligence = beansDefAfter.BaseIntelligence * beansDefAfter.BeanStacks;
                        target.GameCharacter.AddIntelligence(beansDefAfter.FakeIntelligence - oldFakeBeansD, "Гигантские бобы");
                        game.Phrases.RickGiantBeansDrink.SendLog(target, false);
                        // Portal gun invention is handled by HandleEndOfRound (not here)
                        // to prevent the gun from auto-firing on the same fight it was invented
                    }
                    break;

                case "Гоблины":
                    // Goblins die when losing on defense (percentage-based)
                    if (target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var gobDefLossPop = target.Passives.GoblinPopulation;
                        var defDeathPct = 10 + 0.5*game.RoundNo*game.RoundNo/3;
                        if (target.Status.FightEnemyWasTooGood) defDeathPct += 5;
                        if (target.Status.FightEnemyWasTooStronk) defDeathPct += 5;
                        var defDeathCount = Math.Max(1, (int)Math.Ceiling(gobDefLossPop.TotalGoblins * defDeathPct / 100.0));
                        gobDefLossPop.TotalGoblins = Math.Max(1, gobDefLossPop.TotalGoblins - defDeathCount);
                        game.Phrases.GoblinDeath.SendLog(target, false);
                        target.Status.AddInGamePersonalLogs($"Гоблины: -{defDeathCount} ({defDeathPct}%). Осталось: {gobDefLossPop.TotalGoblins}\n");
                    }
                    break;

                case "Близнец":
                    // Близнец resolves only in DoomsdayMachine's authoritative successful-Block
                    // branch. Each stopped attacker contributes frozen Justice plus the ordinary
                    // +1 Block grant and queues one highest-stat copy; bypassed Blocks contribute
                    // nothing. Keep this no-op so transferred holders cannot double-dispatch it.
                    break;

                // TheBoys — Kimiko: выведение из строя при поражении в обороне (Живое Оружие даёт иммунитет)
                case "Kimiko":
                    if (!target.Passives.TheBoysButcher.SuperDickActive
                        && target.Passives.TheBoysKimiko.RegenLevel == 0
                        && target.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        target.Passives.TheBoysKimiko.DisabledNextRound = true;
                        game.Phrases.TheBoysKimikoDisabled.SendLog(target, false);
                    }
                    break;

                // Salldorum — Очко: +1 regular point when attacked by the nearest living enemy below
                case "Очко":
                    if (Salldorum.IsNearestLowerEnemy(game, target, me))
                    {
                        target.Status.AddRegularPoints(1, "Очко");
                        game.Phrases.SalldorumOchko.SendLog(target, false);
                    }
                    break;

                // Геральт — Ведьмачьи заказы (defense after fight): per-fight skill + demand tracking
                // Contract consumption and multi-fight injection handled by DoomsdayMachine
                case "Ведьмачьи заказы":
                    if (target.GameCharacter.Name == "Геральт")
                    {
                        var geraltDefContracts = target.Passives.GeraltContracts;
                        if (geraltDefContracts.ContractProcsOnEnemy.ContainsKey(me.GetPlayerId()))
                        {
                            target.GameCharacter.AddExtraSkill(20, "Ведьмачьи заказы");
                            geraltDefContracts.EnemiesFoughtThisRound.Add(me.GetPlayerId());

                            var geraltDefDemand = target.Passives.GeraltContractDemand;
                            if (!geraltDefDemand.CurrentPerTarget.TryGetValue(me.GetPlayerId(), out var defData))
                            {
                                defData = new Geralt.PerTargetFightData { TargetName = me.DiscordUsername };
                                geraltDefDemand.CurrentPerTarget[me.GetPlayerId()] = defData;
                            }
                            if (target.Status.IsWonThisCalculation == me.GetPlayerId())
                                defData.DefenseWins++;
                            else if (target.Status.IsLostThisCalculation != Guid.Empty)
                                defData.DefenseLosses++;
                            if (target.Status.FightEnemyWasTooGood)
                                defData.WasTooGood = true;
                            if (target.Status.FightEnemyWasTooStronk)
                                defData.WasTooStronk = true;
                            defData.TargetPosition = me.Status.GetPlaceAtLeaderBoard();
                        }
                    }
                    break;

            }
    }

    public void HandleAttackBeforeFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        // Salldorum — Временная капсула: apply pending speed bonus (after DeepCopy)
        var capsuleAtk = me.Passives.SalldorumTimeCapsule;
        if (capsuleAtk.SpeedBonusPending > 0)
        {
            me.FightCharacter.AddSpeedForOneFight(capsuleAtk.SpeedBonusPending, "Временная капсула");
            capsuleAtk.SpeedBonusPending = 0;
        }

        // Seller forced loss: marked player loses next attack
        if (me.Passives.SellerForcedLossNextAttack
            && !Sirinoks.BlocksAutowinFrom(me, target, game))
            me.Status.IsAbleToWin = false;

        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case Homelander.Righteousness:
                    Homelander.ArmLaser(me, target, game);
                    break;

                case Homelander.Modesty:
                    Homelander.SuppressJustice(me, target);
                    break;

                case Homelander.Milk:
                    Homelander.TryMilk(me, target);
                    break;

                case JonSnow.IAmJonSnow:
                    JonSnow.ApplyBaseJustice(me);
                    break;

                case Naruto.Rasengan:
                    if (me.GameCharacter.Name == Naruto.CharacterName)
                        ApplyRasenganBoost(me, target, game);
                    break;

                case Naruto.Summon:
                    if (me.GameCharacter.Name != Naruto.CharacterName) break;
                    me.Passives.Naruto.SummonAutoWinTarget = Guid.Empty;
                    if (UnknownBug.Is(target)
                        || Sirinoks.BlocksAutowinFrom(target, me, game)
                        || !Naruto.IsSoloAttack(game, me, target)) break;

                    if (Naruto.WonPoweredFightLastRound(me, target, game))
                    {
                        me.Passives.Naruto.SummonAutoWinTarget = target.GetPlayerId();
                        target.Status.IsAbleToWin = false;
                        game.Phrases.NarutoGamabuchiSuccess.SendLog(me, false, isRandomOrder: false);
                        game.Phrases.NarutoGamabuntaSuccess.SendLog(me, false, isRandomOrder: false);
                    }
                    else
                    {
                        game.Phrases.NarutoGamabuntaRefusal.SendLog(me, false);
                    }
                    break;

                // Salldorum — Великий летописец: this multiplier must be applied before
                // CalculateRounds reads FightCharacter.
                case "Великий летописец":
                    ApplySalldorumChroniclerMultiplier(me, target, game);
                    break;

                case "Монстр":
                    // The attack itself marks the target, even when Block/Skip prevents a fight.
                    // An absolute expiry lets different victims keep independent overlapping windows.
                    if (!UnknownBug.Is(target))
                        target.Passives.MonsterNoEscapeUntilRound = Math.Max(
                            target.Passives.MonsterNoEscapeUntilRound, game.RoundNo + 2);
                    break;

                case Madara.GodOfShinobi:
                    var shinobiAttackSkill = Madara.GetGodOfShinobiSkill(me);
                    if (shinobiAttackSkill > 0)
                        me.FightCharacter.SetSkillForOneFight(shinobiAttackSkill, Madara.GodOfShinobi);
                    break;

                case Madara.SusanooClones:
                    // A round-seven fight is mandatory, so an enemy Block/Skip cannot prevent it.
                    // unknown_bug's isolation holds: its submitted action is never broken through.
                    if (Madara.IsRoundSevenAutoWin(game, me) && !UnknownBug.Is(target))
                    {
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                    }

                    break;

                case ErenYeager.AttackTitan:
                    if (me.GameCharacter.Name == ErenYeager.CharacterName
                        && me.Passives.Eren.AttackTitanActiveThisRound)
                        ApplyAttackTitanBoost(me);
                    break;

                case UnknownBug.AutoWin:
                    target.Status.IsAbleToWin = false;
                    me.Status.IsArmorBreak = true;
                    me.Status.IsSkipBreak = true;
                    break;

                case "Глаза бога смерти":
                    var eyes = me.Passives.KiraShinigamiEyes;
                    if (eyes.EyesActiveForNextAttack)
                    {
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж")
                            || UnknownBug.Is(target) || Sakura.Is(target))
                        {
                            me.Status.AddInGamePersonalLogs("Глаза бога смерти: У этого монстра нет имени...\n");
                        }
                        else if (target.GetPlayerId() == Salldorum.ResolveRandomTargetId(
                                     game, me, me.Passives.KiraL.LPlayerId))
                        {
                            // Don't consume eyes on L — keep them for a useful target
                            me.Status.AddInGamePersonalLogs("Глаза бога смерти: Ты не можешь увидеть имя L...\n");
                        }
                        else
                        {
                            eyes.EyesActiveForNextAttack = false;
                            me.Status.AddInGamePersonalLogs($"Глаза бога смерти: {target.DiscordUsername} - это **{UnknownBug.PublicName(target)}**\n");
                            if (!eyes.RevealedPlayers.Contains(target.GetPlayerId()))
                                eyes.RevealedPlayers.Add(target.GetPlayerId());
                            Homelander.RecordReveal(game, me, target);
                            game.Phrases.KiraShinigamiEyes.SendLog(me, false);
                        }
                    }
                    break;

                case "Следит за игрой":
                    foreach (var metaPlayer in me.Passives.YongGlebMetaClass)
                    {
                        if (target.GetPlayerId() == metaPlayer)
                        {
                           me.Status.AddBonusPoints(1, "Следит за игрой");
                           game.Phrases.YongGlebMeta.SendLog(me, true);
                        }
                    }
                    break;

                case "Коммуникация":
                    if (game.RoundNo == 6)
                    {
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж")
                            || UnknownBug.Is(target) || Sakura.Is(target))
                        {
                            me.Status.AddInGamePersonalLogs("Коммуникация: Не удалось просветить\n");
                            break;
                        }
                        var commLogSnippet = $"Пиквард просветил {UnknownBug.PublicName(target)}";
                        game.AddGlobalLogs(commLogSnippet);
                        game.KiraHiddenLogSnippets.Add(commLogSnippet);
                        game.Phrases.YongGlebCommunication.SendLog(me, false);
                        me.Passives.AchievementTracker.YoungGlebPinkWardUsed = true;

                        // Auto-set prediction for all players who don't already have one for this target
                        foreach (var p in game.PlayersList)
                        {
                            if (p.Passives.IsDead) continue;
                            if (p.GetPlayerId() == target.GetPlayerId()) continue;
                            if (p.IsProMode && p.GetPlayerId() != me.GetPlayerId()) continue;
                            var existingPred = p.Predict.Find(pr => pr.PlayerId == target.GetPlayerId());
                            if (existingPred == null)
                                p.Predict.Add(new PredictClass(target.GameCharacter.Name, target.GetPlayerId()));
                        }
                        if (!game.PinkWardRevealedPlayerIds.Contains(target.GetPlayerId()))
                            game.PinkWardRevealedPlayerIds.Add(target.GetPlayerId());
                        Homelander.RecordReveal(game, me, target);
                    }
                    break;

                case "Сомнительная тактика":
                    var deep = me.Passives.DeepListDoubtfulTactic;

                    if (!deep.FriendList.Contains(target.GetPlayerId()))
                        me.Status.IsAbleToWin = false;

                    break;

                case "Возвращение из мертвых":
                    if (game.RoundNo >= 10 && !UnknownBug.Is(target))
                    {
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                    }

                    break;

                case "Охота на богов":
                    if (me.FightCharacter.HasSkillTargetOn(target.FightCharacter))
                    {
                        game.Phrases.KratosTarget.SendLog(me, false);
                        me.FightCharacter.SetSkillFightMultiplier(2);
                        if (game.IsKratosEvent && game.RoundNo > 10)
                            me.FightCharacter.SetSkillFightMultiplier(4);
                    }

                    break;

                case "Подсчет":
                    var tolya = me.Passives.TolyaCount;

                    if (tolya.IsReadyToUse
                        && me.Status.WhoToAttackThisTurn.Count != 0
                        && !UnknownBug.Is(target))
                    {
                        tolya.TargetList.Add(new Tolya.TolyaCountSubClass(target.GetPlayerId(), game.RoundNo));
                        tolya.IsReadyToUse = false;
                        tolya.Cooldown = _rand.Random(4, 5);
                    }

                    break;

                case "Оборотень":
                    var myTempStrength = me.FightCharacter.GetStrength();
                    var targetTempStrength = target.FightCharacter.GetStrength();
                    me.FightCharacter.SetStrengthForOneFight(targetTempStrength, "Оборотень");
                    target.FightCharacter.SetStrengthForOneFight(myTempStrength, "Оборотень");

                    /*var myTempSkillMain = me.GameCharacter.GetSkillForOneFight();
                    var targetTempSkill = target.GameCharacter.GetSkillForOneFight();
                    me.GameCharacter.SetSkillForOneFight(targetTempSkill, "Оборотень");
                    target.GameCharacter.SetSkillForOneFight(myTempSkillMain, "Оборотень");*/
                    break;

                case "Безжалостный охотник":
                    if (!UnknownBug.Is(target))
                    {
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                        if (target.Status.IsBlock || target.Status.IsSkip)
                            game.Phrases.WeedwickRuthlessHunter.SendLog(me, false);
                    }

                    // Most wanted: always sense Rick regardless of Justice
                    var isMostWantedHunter =
                        target.GameCharacter.Passive.Any(
                            x => x.PassiveName == RickSanchez.MostWanted)
                        || Salldorum.FindRandomTargetMagnet(game, me)?.GetPlayerId()
                        == target.GetPlayerId();
                    if (target.FightCharacter.Justice.GetRealJusticeNow() == 0 || isMostWantedHunter)
                    {
                        var tempSpeed = me.FightCharacter.GetSpeed() * 2;
                        me.FightCharacter.SetSpeedForOneFight(tempSpeed, "Безжалостный охотник");
                    }

                    break;

                case "Им это не понравится":
                    var spartanMark = me.Passives.SpartanMark;
                    if (spartanMark != null)
                        if (!UnknownBug.Is(target)
                            && !Sirinoks.BlocksAutowinFrom(target, me, game)
                            && target.Status.IsBlock && Salldorum.IsRedirectedRandomTarget(
                                game, me, target, spartanMark.FriendList))
                        {
                            spartanMark.BlockedPlayer = target.GetPlayerId();
                            me.Status.IsArmorBreak = true;
                            target.Status.IsAbleToWin = false;
                            game.Phrases.SpartanTheyWontLikeIt.SendLog(me, false);
                        }

                    break;

                case "DragonSlayer":
                    if (game.RoundNo == 10)
                        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Дракон"))
                        {
                            if (Sirinoks.BlocksAutowinFrom(target, me, game))
                                break;

                            var isBuffed = game.PlayersList.Any(p =>
                                p.GameCharacter.Passive.Any(x => x.PassiveName == "Buffing") &&
                                p.Passives.SupportPremade.MarkedPlayerId == target.GetPlayerId());

                            if (isBuffed)
                            {
                                game.AddGlobalLogs("**DragonSlayer**: Дракон под защитой Суппорта!\n");
                                break;
                            }

                            me.Passives.AchievementTracker.SpartanDragonSlayerTriggered = true;
                            target.Status.IsAbleToWin = false;
                            game.AddGlobalLogs("**Я DRAGONSLAYER!**\n" +
                                               $"{me.DiscordUsername} побеждает дракона и забирает **1000 голды**!");
                            foreach (var p in game.PlayersList) game.Phrases.SpartanDragonSlayer.SendLog(p, false);
                        }

                    break;

                case "Первая кровь":
                    var pant = me.Passives.SpartanFirstBlood;
                    if (pant.FriendList.Count == 0) pant.FriendList.Add(target.GetPlayerId());
                    break;

                case "Они позорят военное искусство":
                    var spartan = me.Passives.SpartanShame;

                    if (target.GameCharacter.Name == "mylorik" && !spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        me.Passives.AchievementTracker.SpartanRespectTriggeredThisFight = target.GetPlayerId();
                        me.Passives.AchievementTracker.SpartanRespectedOpponentIds.Add(target.GetPlayerId());
                        spartan.FriendList.Add(target.GetPlayerId());
                        me.GameCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        target.GameCharacter.AddPsyche(1, "ОН уважает военное искусство!");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (target.GameCharacter.Name == "Кратос" && !spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        me.GameCharacter.AddPsyche(1, "Отец?");
                        target.GameCharacter.AddPsyche(1, "Boi?");
                        game.Phrases.SpartanShameMylorik.SendLog(me, false);
                    }

                    if (!spartan.FriendList.Contains(target.GetPlayerId()))
                    {
                        spartan.FriendList.Add(target.GetPlayerId());
                        target.GameCharacter.AddStrength(-1, "Они позорят военное искусство");
                        target.GameCharacter.AddSpeed(-1, "Они позорят военное искусство");
                    }

                    break;

                case "Я за чаем":
                    var geblTea = me.Passives.GlebTea;

                    if (geblTea.Ready && me.Status.WhoToAttackThisTurn.Count != 0
                                      && !UnknownBug.Is(target))
                    {
                        geblTea.Ready = false;
                        target.Passives.GlebTeaTriggeredWhen = new WhenToTriggerClass(game.RoundNo + 1);
                        me.Status.AddRegularPoints(1, "Я за чаем");
                        game.Phrases.GlebTeaPhrase.SendLog(me, true);
                    }

                    break;

                case "Спокойствие":
                    var yongGlebTea = me.Passives.YongGlebTea;

                    if (yongGlebTea.IsReadyToUse && me.Status.WhoToAttackThisTurn.Count != 0
                                                 && !UnknownBug.Is(target))
                    {
                        yongGlebTea.IsReadyToUse = false;
                        yongGlebTea.Cooldown = 3;

                        target.Passives.GlebTeaTriggeredWhen = new WhenToTriggerClass(game.RoundNo + 1);
                        me.Status.AddRegularPoints(1, "Спокойствие");
                        game.Phrases.YongGlebTea.SendLog(me, true);
                    }
                    break;

                case "Заводить друзей":
                    var siri = me.Passives.SirinoksFriendsList;
                    var siriAttack = me.Passives.SirinoksFriendsAttack;

                    if (siri != null && siriAttack != null)
                        if (!UnknownBug.Is(target) && siri.FriendList.Contains(target.GetPlayerId()))
                            if (target.Status.IsBlock || target.Status.IsSkip)
                            {
                                siriAttack.EnemyId = target.GetPlayerId();
                                me.Status.IsArmorBreak = true;
                                me.Status.IsSkipBreak = true;
                            }


                    if (!siri!.FriendList.Contains(target.GetPlayerId()))
                    {
                        siri.FriendList.Add(target.GetPlayerId());
                        me.Status.AddRegularPoints(1, "Заводить друзей");
                        game.Phrases.SirinoksFriendsPhrase.SendLog(me, true);
                    }

                    break;

                case "Научите играть":
                    var awdka = me.Passives.AwdkaTeachToPlay;
                    var awdkaHistory = me.Passives.AwdkaTeachToPlayHistory;

                    var player2Stats = new List<Sirinoks.TrainingSubClass>
                    {
                        new(1, target.FightCharacter.GetIntelligence()),
                        new(2, target.FightCharacter.GetStrength()),
                        new(3, target.FightCharacter.GetSpeed()),
                        new(4, target.FightCharacter.GetPsyche())
                    };
                    var sup = player2Stats.OrderByDescending(x => x.StatNumber).ToList().First();

                    awdka.Training.Add(new Sirinoks.TrainingSubClass(sup.StatIndex, sup.StatNumber));


                    var enemy = awdkaHistory.History.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                    if (enemy == null)
                    {
                        awdkaHistory.History.Add(new Awdka.TeachToPlayHistoryListClass(target.GetPlayerId(),
                            $"{sup.StatIndex}", sup.StatNumber));
                    }
                    else
                    {
                        enemy.Text = $"{sup.StatIndex}";
                        enemy.Stat = sup.StatNumber;
                    }

                    break;

                case "Я пытаюсь!":
                    var awdkaTrying = me.Passives.AwdkaTryingList;
                    var awdkaTryingTarget = awdkaTrying?.TryingList.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                    if (awdkaTryingTarget is { IsUnique: true }) me.FightCharacter.SetSkillFightMultiplier(2);
                    break;


                case "Падальщик":
                    if (!UnknownBug.Is(target)
                        && target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        if (target.FightCharacter.Justice.GetRealJusticeNow() > 0)
                        {
                            var howMuchIgnores = 1;
                            target.Passives.VampyrIgnoresOneJustice = howMuchIgnores;
                            target.FightCharacter.Justice.SetJusticeForOneFight(
                                target.FightCharacter.Justice.GetRealJusticeNow() - howMuchIgnores,
                                "Падальщик");
                        }

                    break;
                case "Спарта":
                    var mylorikSpartan = me.Passives.MylorikSpartan;
                    var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    if (mylorikEnemy == null)
                    {
                        mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                        mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                    }

                    if (me.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                        //set FightMultiplier
                    {
                        switch (mylorikEnemy!.LostTimes)
                        {
                            case 1:
                                me.FightCharacter.SetSkillFightMultiplier(2);
                                break;
                            case 2:
                                me.FightCharacter.SetSkillFightMultiplier(4);
                                break;
                            case 3:
                                me.FightCharacter.SetSkillFightMultiplier(8);
                                break;
                            case 4:
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                            case 10:
                                me.FightCharacter.SetSkillFightMultiplier(16);
                                game.AddGlobalLogs(
                                    $"mylorik: Айсик, можно тортик? У меня {me.GameCharacter.GetSkill()} *Скилла*!");
                                break;
                            default:
                                me.FightCharacter.SetSkillFightMultiplier();
                                break;
                        }

                        if (me.FightCharacter.GetSkillFightMultiplier() > 1)
                            me.Status.AddInGamePersonalLogs(
                                $"Спарта: {(int)(me.FightCharacter.GetSkill())} *Скилла* против {target.DiscordUsername}\n");
                    }

                    break;

                case "Питается водорослями":
                    if (target.Status.GetPlaceAtLeaderBoard() >= 4) me.Status.AddBonusPoints(1, "Питается водорослями");
                    break;

                case "Неприметность":
                    // Saitama holds back against enemies NOT in top 2
                    //if (game.RoundNo  >= 10) break;
                    //var saitamaAtkUnnoticed = me.Passives.SaitamaUnnoticed;
                    //if (!saitamaAtkUnnoticed.SeriousTargets.Contains(target.GetPlayerId()))
                    //{
                    //    me.Status.IsAbleToWin = false;
                    //    game.Phrases.SaitamaHoldsBack.SendLog(me, false);
                    //}
                    //else
                    //{
                    //    game.Phrases.SaitamaSerious.SendLog(me, false);
                    //}
                    break;

                case "Портальная пушка":
                    var gunAtk = me.Passives.RickPortalGun;
                    if (gunAtk.Invented && gunAtk.Charges > 0 && !UnknownBug.Is(target)
                        && !Sirinoks.BlocksAutowinFrom(target, me, game))
                    {
                        target.Status.IsAbleToWin = false;
                        me.Status.IsArmorBreak = true;
                        me.Status.IsSkipBreak = true;
                    }
                    break;

                // Вороны: reduce target speed by 20% per crow (rounded up)
                case "Вороны":
                    var crowsAtk = me.Passives.ItachiCrows;
                    if (crowsAtk.CrowCounts.TryGetValue(target.GetPlayerId(), out var crowCount) && crowCount > 0)
                    {
                        var targetSpeedAtk = target.FightCharacter.GetSpeed();
                        var ignoredAtk = (int)Math.Ceiling(targetSpeedAtk * 0.20 * crowCount);
                        target.FightCharacter.AddSpeedForOneFight(-Math.Min(ignoredAtk, targetSpeedAtk), "Вороны");
                    }
                    break;

                // Аматерасу: auto-win only on attack, only vs adjacent leaderboard target
                case "Аматерасу":
                    var itachiSpeedAtk = me.FightCharacter.GetSpeed();
                    var targetEffectiveSpeedAtk = target.FightCharacter.GetSpeed();
                    var itachiPos = me.Status.GetPlaceAtLeaderBoard();
                    var targetPos = target.Status.GetPlaceAtLeaderBoard();
                    if (!Sirinoks.BlocksAutowinFrom(target, me, game)
                        && targetEffectiveSpeedAtk < itachiSpeedAtk && Math.Abs(itachiPos - targetPos) == 1)
                    {
                        target.Status.IsAbleToWin = false;
                        game.Phrases.ItachiAmaterasu.SendLog(me, false);
                    }
                    break;

                case "Впарить говна":
                    var sellerVparit = me.Passives.SellerVparitGovna;
                    if (sellerVparit.Cooldown <= 0 && !UnknownBug.Is(target))
                    {
                        // Add 500 skill BEFORE enabling siphon (so 500 is excluded)
                        var savedSiphon = target.GameCharacter.SkillSiphonBox;
                        target.GameCharacter.SkillSiphonBox = null;
                        target.GameCharacter.AddExtraSkill(500, "Впарить говна");
                        target.GameCharacter.SkillSiphonBox = savedSiphon ?? 0; // enable/restore siphon

                        // Track total skill added (for removal when mark expires)
                        target.Passives.SellerVparitGovnaTotalSkill += 500;
                        target.Passives.SellerVparitGovnaRoundsLeft = 4;

                        // Track in seller's list
                        if (!sellerVparit.MarkedPlayers.Contains(target.GetPlayerId()))
                            sellerVparit.MarkedPlayers.Add(target.GetPlayerId());

                        sellerVparit.Cooldown = 2;
                        game.Phrases.SellerVparit.SendLog(me, false);
                        game.Phrases.SellerVparitEnemy.SendLog(target, false);
                        target.Passives.SellerForcedLossNextAttack = true;
                    }
                    break;

                case "Макро":
                    me.Passives.DopaMacro.FightsProcessed++;
                    if (me.Passives.DopaMacro.FightsProcessed > 1)
                    {
                        me.Status.HideCurrentFight = true;
                        me.Status.IsShadowAction = true;
                    }
                    break;

                // Napoleon — Вступить в союз: form alliance on first attack; check joint attacks
                case "Вступить в союз":
                    var napAlliance = me.Passives.NapoleonAlliance;
                    if (napAlliance.AllyId == Guid.Empty)
                    {
                        napAlliance.AllyId = target.GetPlayerId();
                        target.Status.AddInGamePersonalLogs(
                            "Napoleon Wonnafuck предлагает вам вступить в союз, нападайте вместе на одну цель, для избежания поражений\n");
                        break;
                    }
                    var napAlly = game.PlayersList.Find(x => x.GetPlayerId() == napAlliance.AllyId);
                    if (napAlly != null && napAlly.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())
                        && !Sirinoks.BlocksAutowinFrom(target, me, game))
                    {
                        target.Status.IsAbleToWin = false;
                        me.GameCharacter.AddMoral(3, "Вступить в союз");
                    }
                    break;

                // Toxic Mate — "Aggress": if IsSkip or IsBlock was set by other passives, clear them
                case "Aggress":
                    if (me.Status.IsSkip || me.Status.IsBlock)
                    {
                        game.Phrases.ToxicMateAggressWontStop.SendLog(me, false);
                        me.Status.IsSkip = false;
                        me.Status.IsBlock = false;
                    }
                    break;

                // Таинственный Суппорт — "Premade": mark first target as partner
                case "Premade":
                    if (me.Passives.SupportPremade.MarkedPlayerId == Guid.Empty)
                    {
                        me.Passives.SupportPremade.MarkedPlayerId = target.GetPlayerId();
                        me.Status.AddInGamePersonalLogs($"Premade: {target.DiscordUsername} теперь твой напарник\n");
                        game.Phrases.SupportPremadeMark.SendLog(me, false);
                    }
                    break;

                // Таинственный Суппорт — "Buffing": buff marked player's lowest stat
                case "Buffing":
                    if (target.GetPlayerId() == me.Passives.SupportPremade.MarkedPlayerId)
                    {
                        var bInt = target.FightCharacter.GetIntelligence();
                        var bStr = target.FightCharacter.GetStrength();
                        var bSpd = target.FightCharacter.GetSpeed();
                        var bPsy = target.FightCharacter.GetPsyche();
                        var bMin = Math.Min(Math.Min(bInt, bStr), Math.Min(bSpd, bPsy));

                        if (bMin == bInt)
                            target.GameCharacter.AddIntelligence(2, "Buffing", isLog: false);
                        else if (bMin == bStr)
                            target.GameCharacter.AddStrength(2, "Buffing", isLog: false);
                        else if (bMin == bSpd)
                            target.GameCharacter.AddSpeed(2, "Buffing", isLog: false);
                        else
                            target.GameCharacter.AddPsyche(2, "Buffing", isLog: false);

                        me.Status.AddInGamePersonalLogs($"Buffing: Усилил {target.DiscordUsername}\n");
                    }
                    break;

                case "Гоблины":
                    me.Passives.GoblinLastAttackedPlayer = target.GetPlayerId();
                    var attackedPlayerIds = me.Passives.GoblinZiggurat.AttackedPlayerIds;
                    if (!attackedPlayerIds.Contains(target.GetPlayerId()))
                        attackedPlayerIds.Add(target.GetPlayerId());
                    break;

                case "Отличный рудник":
                    // Attacking mine position (1, 2, or 6) — raid for bonus points
                    var targetPlace = target.Status.GetPlaceAtLeaderBoard();
                    if (targetPlace is 1 or 2 or 6)
                    {
                        var raidWorkers = me.Passives.GoblinPopulation.Workers;
                        if (raidWorkers > 0)
                        {
                            me.Status.AddBonusPoints(raidWorkers, "Отличный рудник");
                            game.Phrases.GoblinMine.SendLog(me, false);
                            //me.Status.AddInGamePersonalLogs($"Рудник: Обчистили на {raidWorkers} очков!\n");
                        }
                    }
                    break;

                case "Близнец":
                    // me = Монстр (attacker). If any stat matches target → -1 Psyche
                    var meInt = me.FightCharacter.GetIntelligence();
                    var meStr = me.FightCharacter.GetStrength();
                    var meSpd = me.FightCharacter.GetSpeed();
                    var mePsy = me.FightCharacter.GetPsyche();
                    var tInt = target.FightCharacter.GetIntelligence();
                    var tStr = target.FightCharacter.GetStrength();
                    var tSpd = target.FightCharacter.GetSpeed();
                    var tPsy = target.FightCharacter.GetPsyche();
                    if (meInt == tInt || meStr == tStr || meSpd == tSpd || mePsy == tPsy)
                    {
                        me.MinusPsyche(game, -1, "Близнец");
                        //me.Status.AddInGamePersonalLogs("Близнец: Ваши статы совпали с врагом...");
                    }
                    break;

                // TheBoys — Butcher: кочерга умножает Скилл в бою (СуперМудень удваивает)
                case "Butcher":
                    var butcherAtk = me.Passives.TheBoysButcher;
                    if (butcherAtk.ButcherLeft) break;
                    var pokerCount = butcherAtk.PokerCount;
                    if (pokerCount > 0)
                    {
                        var pokerMult = 1 + pokerCount;
                        me.FightCharacter.SetSkillFightMultiplier(butcherAtk.SuperDickActive ? pokerMult * 2 : pokerMult);
                        game.Phrases.TheBoysPoker.SendLog(me, false);
                    }
                    break;

                // Геральт — Масло: oil effects when attacking
                case "Масло":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltOil = me.Passives.GeraltOil;
                        if (geraltOil.IsOilApplied)
                        {
                            var targetMonsterType = target.Passives.GeraltMonsterType;
                            if (targetMonsterType != null)
                            {
                                var oilTier = geraltOil.GetTier(targetMonsterType.Value);
                                if (oilTier >= 1)
                                {
                                    // Tier 1+: ignore 1 Justice
                                    var targetJustice = target.FightCharacter.Justice.GetRealJusticeNow();
                                    if (targetJustice > 0)
                                        target.FightCharacter.Justice.SetJusticeForOneFight(
                                            Math.Max(0, targetJustice - 1), "Масло");
                                }
                                if (oilTier >= 2)
                                {
                                    // Tier 2+: +2 Strength
                                    me.FightCharacter.SetStrengthForOneFight(
                                        me.FightCharacter.GetStrength() + 2, "Масло");
                                }
                                if (oilTier >= 3)
                                {
                                    // Tier 3: triple Skill
                                    me.FightCharacter.SetSkillForOneFight(
                                        me.FightCharacter.GetSkill() * 3, "Масло");
                                }
                            }
                            // Oil persists for entire round (reset in HandleEndOfRound)
                        }

                        // Contextual attack phrases
                        if (target.GameCharacter.Name == "Вампур")
                            game.Phrases.GeraltAttackVampire.SendLog(me, false);
                        else if (target.GameCharacter.Name == "TheBoys")
                            game.Phrases.GeraltAttackTheBoys.SendLog(me, false);
                        else if (target.GameCharacter.Name == "Кира" ||
                                 target.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж"))
                            game.Phrases.GeraltAttackMonster.SendLog(me, false);
                    }
                    break;

                // Геральт — Шевелись, Плотва: attack-only, contracts-only, bonus speed + extra contracts
                // Геральт — Шевелись, Плотва: once per round, on first contract fight only
                case "Шевелись, Плотва":
                    if (me.GameCharacter.Name == "Геральт" && !me.Passives.GeraltContracts.PlotvaPhrasedThisRound)
                    {
                        var geraltContracts = me.Passives.GeraltContracts;
                        if (geraltContracts.ContractProcsOnEnemy.ContainsKey(target.GetPlayerId()))
                        {
                            var geraltAtkPos = me.Status.GetPlaceAtLeaderBoard();
                            var targetAtkPos = target.Status.GetPlaceAtLeaderBoard();
                            if (geraltAtkPos > targetAtkPos + 1)
                            {
                                geraltContracts.PlotvaPhrasedThisRound = true;

                                var plotvaAtkSpeed = geraltAtkPos - targetAtkPos;
                                me.FightCharacter.AddSpeedForOneFight(plotvaAtkSpeed, "Шевелись, Плотва");
                                game.Phrases.GeraltPlotva.SendLog(me, false);

                                // Extra contracts: for each player between Geralt and target
                                if (!geraltContracts.PlotvaContractsGrantedThisRound)
                                {
                                    geraltContracts.PlotvaContractsGrantedThisRound = true;
                                    foreach (var p in game.PlayersList)
                                    {
                                        if (p.GetPlayerId() == me.GetPlayerId()) continue;
                                        var pPlace = p.Status.GetPlaceAtLeaderBoard();
                                        if (pPlace > targetAtkPos && pPlace < geraltAtkPos && p.Passives.GeraltMonsterType != null)
                                        {
                                            var extraContracts = _rand.Random(1, 1);
                                            if (extraContracts > 0)
                                            {
                                                geraltContracts.AddCount(p.Passives.GeraltMonsterType.Value, extraContracts);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                // Геральт — Медитация (attack): Lambert skill zero
                case "Медитация":
                    if (me.GameCharacter.Name == "Геральт" && me.Passives.GeraltMeditation.LambertActive)
                    {
                        me.FightCharacter.SetSkillForOneFight(0, "Медитация");
                    }
                    break;
            }
    }

    private static void ApplySalldorumChroniclerMultiplier(
        GamePlayerBridgeClass salldorum,
        GamePlayerBridgeClass opponent,
        GameClass game)
    {
        if (salldorum.GameCharacter.Name != "Salldorum")
            return;

        // FightCharacter is reused across the round. Clear a previous Chronicler target before
        // deciding whether this opponent receives the x3 multiplier.
        salldorum.FightCharacter.SetSkillFightMultiplier();
        if (game.RoundNo <= 3)
            return;

        var chroniclerRound = game.RoundNo - 3;
        var winCounts = new Dictionary<Guid, int>();
        foreach (var player in game.PlayersList)
        foreach (var loss in player.Status.WhoToLostEveryRound.Where(
                     entry => entry.RoundNo == chroniclerRound))
            winCounts[loss.EnemyId] = winCounts.GetValueOrDefault(loss.EnemyId) + 1;

        if (winCounts.Count == 0
            || !winCounts.TryGetValue(opponent.GetPlayerId(), out var opponentWins)
            || opponentWins != winCounts.Values.Max())
            return;

        salldorum.FightCharacter.SetSkillFightMultiplier(3);
        game.Phrases.SalldorumChroniclerTriple.SendLog(salldorum, false);
    }

    public void HandleAttackAfterFight(GamePlayerBridgeClass me, GamePlayerBridgeClass target, GameClass game)
    {
        // Seller: mark target as "outplay" after forced loss
        if (me.Passives.SellerForcedLossNextAttack)
        {
            if (!Sirinoks.BlocksAutowinFrom(me, target, game))
            {
                if (me.Status.IsLostThisCalculation != Guid.Empty
                    && !me.Passives.SellerOutplayTargets.Contains(target.GetPlayerId()))
                    me.Passives.SellerOutplayTargets.Add(target.GetPlayerId());
                me.Passives.SellerForcedLossNextAttack = false;
            }
        }

        foreach (var passive in me.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case Homelander.Righteousness:
                    Homelander.RecordEnemyVictory(me, target, game);
                    break;

                case OmniMan.ThinkMark:
                    OmniMan.HandleIntelligenceWin(me, target, game);
                    break;

                case OmniMan.UndergroundTrain:
                    OmniMan.HandleUndergroundTrainWin(me, target, game);
                    break;

                case "Много выебывается":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        if (me.FightCharacter.HasSkillTargetOn(target.FightCharacter))
                        {
                            me.GameCharacter.AddExtraSkill(40, "Много выебывается");
                        }
                    }
                    break;

                case "Выгодная сделка":
                    if (game.RoundNo == 10
                        && me.Status.IsWonThisCalculation == target.GetPlayerId()
                        && !UnknownBug.Is(target))
                    {
                        var debt = target.Passives.SellerTacticBonusEarned;
                        if (debt > 0)
                        {
                            var stolen = debt / 2;
                            if (Homelander.CanTransferFrom(target, "Выгодная сделка"))
                            {
                                target.Status.AddBonusPoints(-stolen, "Выгодная сделка");
                                me.Status.AddBonusPoints(stolen, "Выгодная сделка");
                            }
                        }
                    }
                    break;

                case "Возвращение из мертвых":
                    if (game.IsKratosEvent && game.RoundNo > 10)
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        {
                            // Goblins and Madara are immune to kill effects.
                            if (target.GameCharacter.Name == "Стая Гоблинов"
                                || Madara.IsMadara(target)
                                || UnknownBug.Is(target)) break;
                            if (Itachi.TryPreventDeath(target, game)) break;
                            game.AddGlobalLogs($"{UnknownBug.PublicName(me)} **УБИЛ** {UnknownBug.PublicName(target)}!");
                            game.AddGlobalLogs($"Они скинули **{target.DiscordUsername}**! Сволочи!");
                            game.Phrases.KratosEventKill.SendLog(me, true, isRandomOrder:false);
                            if (!JonSnow.TryEndWatch(target, game, "Kratos"))
                            {
                                target.Passives.IsDead = true;
                                target.Passives.DeathSource = "Kratos";
                            }
                            // Achievement: Kratos kill
                            me.Passives.AchievementTracker.KratosEventVictimIds.Add(target.GetPlayerId());
                            me.Passives.AchievementTracker.EnemiesKilledAsKratos++;
                            target.Passives.AchievementTracker.WasKilledByKratos = true;
                            // Монстр без имени: +1 regular point per death
                            foreach (var mp in game.PlayersList.Where(x => !x.Passives.IsDead
                                         && x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                            {
                                mp.Status.AddRegularPoints(1, "Монстр");
                                game.Phrases.MonsterDeath.SendLog(mp, false);
                            }
                        }
                    break;

                case "Weed":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        if (target.Passives.WeedwickWeed > 0)
                        {
                            me.Passives.AchievementTracker.WeedHarvested += target.Passives.WeedwickWeed;
                            me.GameCharacter.AddMoral(target.Passives.WeedwickWeed, "Weed");

                            switch (target.Passives.WeedwickWeed)
                            {
                                case 1:
                                    game.Phrases.WeedwickWeedYes1.SendLog(me, false);
                                    break;
                                case 2:
                                    game.Phrases.WeedwickWeedYes2.SendLog(me, false);
                                    break;
                                case 3:
                                    game.Phrases.WeedwickWeedYes3.SendLog(me, false);
                                    break;
                                case 4:
                                    game.Phrases.WeedwickWeedYes4.SendLog(me, false);
                                    break;
                                case 5:
                                    game.Phrases.WeedwickWeedYes5.SendLog(me, false);
                                    break;
                                case 6:
                                    game.Phrases.WeedwickWeedYes6.SendLog(me, false);
                                    break;
                                case 7:
                                    game.Phrases.WeedwickWeedYes7.SendLog(me, false);
                                    break;
                                case 8:
                                    game.Phrases.WeedwickWeedYes8.SendLog(me, false);
                                    break;
                                case 9:
                                    game.Phrases.WeedwickWeedYes9.SendLog(me, false);
                                    break;
                                case 10:
                                    game.Phrases.WeedwickWeedYes10.SendLog(me, false);
                                    break;
                                default:
                                    game.Phrases.WeedwickWeedYes11.SendLog(me, false);
                                    break;
                            }

                            target.Passives.WeedwickWeed = 0;
                            me.Passives.WeedwickLastRoundWeed = game.RoundNo;
                        }

                    break;

                case "Ценная добыча":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var preyWinStreak = target.FightCharacter.GetWinStreak();
                        if (preyWinStreak > 0)
                        {
                            if (me.Status.GetPlaceAtLeaderBoard() > target.Status.GetPlaceAtLeaderBoard())
                            {
                                me.Status.AddRegularPoints(preyWinStreak, "Ценная добыча");
                            }
                            else
                            {
                                me.Status.AddBonusPoints(preyWinStreak, "Ценная добыча");
                            }
                        }

                        switch (preyWinStreak)
                        {
                            case 0:
                                break;
                            case 1:
                                break;
                            case 2:
                                game.Phrases.WeedwickValuablePreyPoints1.SendLog(me, false);
                                break;
                            case 3:
                                game.Phrases.WeedwickValuablePreyPoints2.SendLog(me, false);
                                break;
                            case 4:
                                game.Phrases.WeedwickValuablePreyPoints3.SendLog(me, false);
                                break;
                            case 5:
                                game.Phrases.WeedwickValuablePreyPoints4.SendLog(me, false);
                                break;
                            case 6:
                                game.Phrases.WeedwickValuablePreyPoints5.SendLog(me, false);
                                break;
                            case 7:
                                game.Phrases.WeedwickValuablePreyPoints6.SendLog(me, false);
                                break;
                            default:
                                game.Phrases.WeedwickValuablePreyPoints7.SendLog(me, false);
                                break;
                        }

                        //calculate range
                        var range = me.FightCharacter.GetSpeedQualityResistInt();
                        // ReSharper disable once RedundantAssignment
                        range -= target.FightCharacter.GetSpeedQualityKiteBonus();

                        var placeDiff = me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard();
                        if (placeDiff < 0)
                            placeDiff *= -1;
                        //end calculate range

                        //WeedWick ignores range, so you calculated it for nothing! :)
                        range = 10;

                        if (placeDiff <= range && game.RoundNo > 1)
                        {
                            //обычный дроп (его тут нет, просто так тут это написал)
                            var achievementDropsBefore = target.GameCharacter.GetStrengthQualityDropTimes();
                            var harm = 0;

                            // 1/место в таблице.
                            if (_rand.Luck(1, target.Status.GetPlaceAtLeaderBoard()))
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/5
                            if (_rand.Luck(1, 5))
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            // 1/3 если враг топ1
                            if (_rand.Luck(1, 3) && target.Status.GetPlaceAtLeaderBoard() == 1)
                            {
                                harm++;
                                target.GameCharacter.LowerQualityResist(target, game, me);
                                game.Phrases.WeedwickValuablePreyDrop.SendLog(me, false);
                            }

                            if (harm > 0)
                            {
                                var achievementDropsAfter = target.GameCharacter.GetStrengthQualityDropTimes();
                                me.Passives.AchievementTracker.DropsCaused +=
                                    Math.Max(0, achievementDropsAfter - achievementDropsBefore);
                                var bongs = $"Вы нанесли {harm} дополнительного вреда по {target.DiscordUsername} ";
                                for (var i = 0; i < harm; i++) bongs += "<:bong:1046462826539130950>";
                                me.Status.AddInGamePersonalLogs($"*{bongs}*\n");
                            }
                        }
                    }

                    break;

                case "Им это не понравится":
                    var spartanMark = me.Passives.SpartanMark;
                    if (spartanMark != null)
                        if (spartanMark.BlockedPlayer == target.GetPlayerId())
                        {
                            target.Status.IsBlock = true;
                            spartanMark.BlockedPlayer = Guid.Empty;
                        }

                    break;

                case "Падальщик":
                    if (target.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                            me.GameCharacter.AddMoral(3, "Падальщик");
                    break;

                case "Вампуризм":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        me.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(
                            target.FightCharacter.Justice.GetRealJusticeNow()
                            + target.Passives.VampyrIgnoresOneJustice);
                    target.Passives.VampyrIgnoresOneJustice = 0;
                    break;

                case "Неуязвимость":
                    if (me.Status.IsLostThisCalculation != Guid.Empty) me.Passives.OctopusInvulnerabilityList.Count++;
                    break;

                case "Обучение":
                    var siri = me.Passives.SirinoksTraining;

                    if (me.Status.IsLostThisCalculation != Guid.Empty &&
                        me.Status.WhoToAttackThisTurn.Contains(me.Status.IsLostThisCalculation))
                    {
                        var playerSheLostLastTime =
                            game.PlayersList.Find(x => x.GetPlayerId() == me.Status.IsLostThisCalculation);
                        var intel = new List<Sirinoks.StatsClass>
                        {
                            new(1, playerSheLostLastTime!.GameCharacter.GetIntelligence()),
                            new(2, playerSheLostLastTime.GameCharacter.GetStrength()),
                            new(3, playerSheLostLastTime.GameCharacter.GetSpeed()),
                            new(4, playerSheLostLastTime.GameCharacter.GetPsyche())
                        };

                        var intel2 = new List<Sirinoks.StatsClass>();
                        foreach (var i in intel)
                            switch (i.Index)
                            {
                                case 1:
                                    if (me.GameCharacter.GetIntelligence() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 2:
                                    if (me.GameCharacter.GetStrength() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 3:
                                    if (me.GameCharacter.GetSpeed() < i.Number)
                                        intel2.Add(i);
                                    break;
                                case 4:
                                    if (me.GameCharacter.GetPsyche() < i.Number)
                                        intel2.Add(i);
                                    break;
                            }

                        if (intel2.Count > 0)
                        {
                            var best = intel2.OrderByDescending(x => x.Number).ToList().First();


                            if (siri.Training.Count == 0)
                            {
                                siri.Training.Add(new Sirinoks.TrainingSubClass(best.Index, best.Number));
                                siri.EnemyId = playerSheLostLastTime.GetPlayerId();
                            }
                        }
                    }

                    break;

                case "Заводить друзей":
                    var siriAttack = me.Passives.SirinoksFriendsAttack;

                    if (siriAttack != null)
                        if (siriAttack.EnemyId == target.GetPlayerId())
                            siriAttack.EnemyId = Guid.Empty;
                    break;

                case "Повезло":
                    var darscsi = me.Passives.DarksciLuckyList;

                    if (!darscsi.TouchedPlayers.Contains(target.GetPlayerId()))
                        darscsi.TouchedPlayers.Add(target.GetPlayerId());

                    if (darscsi.TouchedPlayers.Count == game.PlayersList.Count - 1 && darscsi.Triggered == false)
                    {
                        var darksciType = me.Passives.DarksciTypeList;
                        var darksciUnstableMultiplier = 1;
                        if (darksciType.IsStableType)
                        {
                            me.Status.AddBonusPoints(me.Status.GetScore(), "Повезло");
                        }
                        else
                        {
                            darksciUnstableMultiplier = 2;
                            me.Status.AddBonusPoints(me.Status.GetScore() * 2, "Повезло");
                        }

                        me.GameCharacter.AddPsyche(2 * darksciUnstableMultiplier, "Повезло");
                        me.GameCharacter.AddMoral(2 * darksciUnstableMultiplier, "Повезло");
                        darscsi.Triggered = true;
                        game.Phrases.DarksciLucky.SendLog(me, true);
                    }

                    break;

                case "Спарта":
                    if (me.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                    {
                        var mylorikSpartan = me.Passives.MylorikSpartan;
                        var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                        if (mylorikEnemy == null)
                        {
                            mylorikSpartan.Enemies.Add(new Mylorik.MylorikSpartanSubClass(target.GetPlayerId()));
                            mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                        }

                        if (me.Status.WhoToAttackThisTurn.Contains(me.Status.IsLostThisCalculation))
                            mylorikEnemy!.LostTimes++;

                        if (me.Status.WhoToAttackThisTurn.Contains(me.Status.IsWonThisCalculation))
                            mylorikEnemy!.LostTimes = 0;
                    }

                    //Спарта reset FightMultiplier
                    me.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "Я пытаюсь!":
                    //Я пытаюсь reset FightMultiplier
                    me.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "На мели":
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        // Bonus point for beating the skill-class target
                        if (me.FightCharacter.HasSkillTargetOn(target.FightCharacter))
                        {
                            me.Status.AddBonusPoints(1, "На мели");
                            game.Phrases.SaitamaBroke.SendLog(me, false);
                        }

                        // Extra point if nobody else attacked this target this round
                        var othersAttackedTarget = game.PlayersList.Any(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

                        if (!othersAttackedTarget)
                        {
                            // Lone-kill reward is a REGULAR point so it scales with the round multiplier (×1/×2/×4)
                            me.Status.AddRegularPoints(1, "На мели", true);
                            if (game.RoundNo >= 10 && target.GameCharacter.Passive.Any(x => x.PassiveName == "Дракон"))
                                me.Status.AddInGamePersonalLogs("На мели (монстр): Уровень Опасности: Дракон\n");
                            else
                                game.Phrases.SaitamaBrokeMonster.SendLog(me, false);

                            // Hide this fight from non-admin players
                            me.Status.HideCurrentFight = true;
                        }
                    }
                    break;

                case "Неприметность":
                    // If Saitama won against someone who was also attacked by another player,
                    // defer his points and moral (they go into the "box")
                    if (game.RoundNo  >= 10) break;

                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        var saitamaAtkUnnoticedAfter = me.Passives.SaitamaUnnoticed;

                        // Check if another player also attacked this same target (they get the kill instead)
                        var coAttacker = game.PlayersList.FirstOrDefault(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

                        if (coAttacker != null)
                        {
                            // Defer the win point (remove 1 from pending score) and bank the round-multiplied
                            // value, attributed to the co-attacker who got the kill (or a Jew who steals it).
                            me.Status.AddRegularPoints(-1, "Неприметность");
                            var atkRecipient = ResolveDeferredRecipient(game, me, target.GetPlayerId(), coAttacker.GetPlayerId());
                            if (atkRecipient != Guid.Empty)
                                saitamaAtkUnnoticedAfter.AddDeferred(atkRecipient, game.RoundNo);

                            // Defer moral too (underdog moral only applies when we had worse place)
                            var moralGain = me.Status.GetPlaceAtLeaderBoard() - target.Status.GetPlaceAtLeaderBoard();
                            if (moralGain > 0 && game.RoundNo > 1)
                            {
                                me.GameCharacter.AddMoral(-moralGain, "Неприметность", isFightMoral: true);
                                saitamaAtkUnnoticedAfter.DeferredMoral += moralGain;
                            }

                            game.Phrases.SaitamaUnnoticed.SendLog(me, false);
                        }
                    }
                    break;

                case "Гигантские бобы":
                    var beansAfter = me.Passives.RickGiantBeans;
                    if (beansAfter.IngredientsActive && beansAfter.IngredientTargets.Contains(target.GetPlayerId())
                        && me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        beansAfter.IngredientTargets.Remove(target.GetPlayerId());
                        beansAfter.BeanStacks++;
                        me.GameCharacter.AddStrength(-1, "Гигантские бобы");
                        me.GameCharacter.AddSpeed(-1, "Гигантские бобы");
                        me.MinusPsyche(game, -1, "Гигантские бобы");
                        var oldFakeBeans = beansAfter.FakeIntelligence;
                        beansAfter.FakeIntelligence = beansAfter.BaseIntelligence * beansAfter.BeanStacks;
                        me.GameCharacter.AddIntelligence(beansAfter.FakeIntelligence - oldFakeBeans, "Гигантские бобы");
                        game.Phrases.RickGiantBeansDrink.SendLog(me, false);
                        // Portal gun invention is handled by HandleEndOfRound (not here)
                        // to prevent the gun from auto-firing on the same fight it was invented
                    }
                    break;

                case "Портальная пушка":
                    var gunAfter = me.Passives.RickPortalGun;
                    if (gunAfter.Invented && gunAfter.Charges > 0
                        && me.Status.IsWonThisCalculation == target.GetPlayerId()
                        && !UnknownBug.Is(target)
                        && !Sirinoks.BlocksAutowinFrom(target, me, game))
                    {
                        gunAfter.Charges--;
                        me.Passives.AchievementTracker.PortalGunFires++;
                        gunAfter.SwapActive = true;
                        gunAfter.SwappedWith = target.GetPlayerId();
                        gunAfter.FiredThisRound = true;
                        foreach (var p in game.PlayersList)
                        {
                            if (UnknownBug.Is(p))
                                continue;

                            for (int i = 0; i < p.Status.WhoToAttackThisTurn.Count; i++)
                            {
                                if (p.Status.WhoToAttackThisTurn[i] == me.GetPlayerId())
                                    p.Status.WhoToAttackThisTurn[i] = target.GetPlayerId();
                                else if (p.Status.WhoToAttackThisTurn[i] == target.GetPlayerId())
                                    p.Status.WhoToAttackThisTurn[i] = me.GetPlayerId();
                            }
                        }

                        // Most wanted: headhunters follow Rick through the portal
                        foreach (var hunter in game.PlayersList.Where(p =>
                            p.GetPlayerId() != me.GetPlayerId() &&
                            p.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()) &&
                            p.GameCharacter.Passive.Any(pas =>
                                pas.PassiveName is "Им это не понравится" or "Безжалостный охотник"
                                    or "Подсчет" or "Сверхразум" or "Заводить друзей")))
                        {
                            for (int i = 0; i < hunter.Status.WhoToAttackThisTurn.Count; i++)
                            {
                                if (hunter.Status.WhoToAttackThisTurn[i] == target.GetPlayerId())
                                    hunter.Status.WhoToAttackThisTurn[i] = me.GetPlayerId();
                            }
                            me.Status.AddInGamePersonalLogs($"**{hunter.DiscordUsername} проследовал за Риком в портал.**\n");
                            game.Phrases.RickMostWantedPortalFollow.SendLog(me, false);
                        }

                        game.Phrases.RickPortalGunFired.SendLog(me, false);
                    }
                    break;

                case "Макро":
                    me.Passives.DopaMacro.FightsResolved++;
                    break;

                case "Доминация":
                    if (me.GameCharacter.Name == Dopa.CharacterName
                        && me.Passives.DopaMetaChoice.ChosenTactic != "Доминация") break;
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        me.GameCharacter.AddExtraSkill(20, "Доминация");
                        if (!UnknownBug.Is(target))
                            target.Status.AddBonusPoints(-1, "Доминация");
                        if (_rand.Luck(1, 3))
                            target.MinusPsyche(game, -1, "Доминация");
                        game.Phrases.DopaDomination.SendLog(me, false);
                    }
                    break;

                case "Роум":
                    if (me.GameCharacter.Name == Dopa.CharacterName
                        && me.Passives.DopaMetaChoice.ChosenTactic != "Роум") break;
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var myPlace = me.Status.GetPlaceAtLeaderBoard();
                        var targetPlace = target.Status.GetPlaceAtLeaderBoard();
                        if (Math.Abs(myPlace - targetPlace) > 1)
                        {
                            if (!UnknownBug.Is(target)
                                && Homelander.CanTransferFrom(target, "Роум"))
                            {
                                target.Status.AddBonusPoints(-1, "Роум");
                                me.Status.AddBonusPoints(1, "Роум");
                            }
                            target.GameCharacter.AddMoral(-3, "Роум");
                            me.GameCharacter.AddMoral(3, "Роум");
                            game.Phrases.DopaRoam.SendLog(me, false);
                        }
                    }
                    break;

                // Napoleon — Завоеватель: bonus point for winning vs enemy between Napoleon and Ally
                case "Завоеватель":
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var napAllyConq = me.Passives.NapoleonAlliance;
                        if (napAllyConq.AllyId != Guid.Empty)
                        {
                            var allyConq = game.PlayersList.Find(x => x.GetPlayerId() == napAllyConq.AllyId);
                            if (allyConq != null)
                            {
                                var napPlace = me.Status.GetPlaceAtLeaderBoard();
                                var allyPlace = allyConq.Status.GetPlaceAtLeaderBoard();
                                var enemyPlace = target.Status.GetPlaceAtLeaderBoard();
                                var minPlace = Math.Min(napPlace, allyPlace);
                                var maxPlace = Math.Max(napPlace, allyPlace);
                                if (enemyPlace > minPlace && enemyPlace < maxPlace)
                                {
                                    me.Status.AddBonusPoints(1, "Завоеватель");
                                    game.Phrases.NapoleonConqueror.SendLog(me, false);
                                }
                            }
                        }
                    }
                    break;

                case "Гоблины":
                    var gobAtkAfterPop = me.Passives.GoblinPopulation;
                    if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                    {
                        // Win: x2 growth, +1 vs tooGood, +2 vs tooStronk
                        var growth = 2 * gobAtkAfterPop.GrowthThisRound;
                        if (me.Status.FightEnemyWasTooGood) growth += 1;
                        if (me.Status.FightEnemyWasTooStronk) growth += 2;
                        gobAtkAfterPop.TotalGoblins += growth;
                        game.Phrases.GoblinGrowthAttack.SendLog(me, false);
                        me.Status.AddInGamePersonalLogs($"Гоблины: +{growth} гоблинов! Всего: {gobAtkAfterPop.TotalGoblins}\n");
                    }
                    else if (me.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        // Loss: kill goblins (percentage-based)
                        var atkDeathPct = 10 + 0.5*game.RoundNo*game.RoundNo/3;;
                        if (me.Status.FightEnemyWasTooGood) atkDeathPct += 5;
                        if (me.Status.FightEnemyWasTooStronk) atkDeathPct += 5;
                        var atkDeathCount = Math.Max(1, (int)Math.Ceiling(gobAtkAfterPop.TotalGoblins * atkDeathPct / 100.0));
                        gobAtkAfterPop.TotalGoblins = Math.Max(1, gobAtkAfterPop.TotalGoblins - atkDeathCount);
                        game.Phrases.GoblinDeath.SendLog(me, false);
                        me.Status.AddInGamePersonalLogs($"Гоблины: -{atkDeathCount} ({atkDeathPct}%). Осталось: {gobAtkAfterPop.TotalGoblins}\n");
                    }
                    break;

                // Геральт — Ведьмачьи заказы (attack after fight): contract skill bonus + phrases
                case "Ведьмачьи заказы":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltAtkContracts = me.Passives.GeraltContracts;
                        var fightCount = geraltAtkContracts.ContractsFoughtThisRound;

                        // Contract fight: give +10 skill per contract fight
                        if (fightCount > 0)
                        {
                            me.GameCharacter.AddExtraSkill(20, "Ведьмачьи заказы");
                        }
                        else if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        {
                            // Won vs non-contract enemy — loot
                            geraltAtkContracts.NonContractWinsThisRound++;
                            geraltAtkContracts.RareLootFoundThisRound = true;
                        }

                        // Demand tracking
                        if (fightCount > 0)
                        {
                            geraltAtkContracts.EnemiesFoughtThisRound.Add(target.GetPlayerId());

                            var demand = me.Passives.GeraltContractDemand;
                            if (!demand.CurrentPerTarget.TryGetValue(target.GetPlayerId(), out var atkData))
                            {
                                atkData = new Geralt.PerTargetFightData { TargetName = target.DiscordUsername };
                                demand.CurrentPerTarget[target.GetPlayerId()] = atkData;
                            }
                            if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                                atkData.AttackWins++;
                            else if (me.Status.IsLostThisCalculation != Guid.Empty)
                                atkData.AttackLosses++;
                            if (me.Status.FightEnemyWasTooGood)
                                atkData.WasTooGood = true;
                            if (me.Status.FightEnemyWasTooStronk)
                                atkData.WasTooStronk = true;
                            atkData.TargetPosition = target.Status.GetPlaceAtLeaderBoard();
                        }

                        // Nest phrase: if enemy has been proc'd more than once total
                        var targetEnemyId = target.GetPlayerId();
                        if (geraltAtkContracts.ContractProcsOnEnemy.TryGetValue(targetEnemyId, out var procs) && procs > 1)
                        {
                            game.Phrases.GeraltContractNest.SendLog(me, false);
                        }

                        // Win phrase
                        if (me.Status.IsWonThisCalculation == target.GetPlayerId())
                        {
                            game.Phrases.GeraltWin.SendLog(me, false);
                        }
                    }
                    break;
            }
    }


    // TheBoys — Смертельный вирус: распространение при бою между носителем и не-носителем (не блок/скип)
    private void TheBoysSpreadVirus(GamePlayerBridgeClass player, GameClass game)
    {
        var oppId = player.Status.IsWonThisCalculation != Guid.Empty
            ? player.Status.IsWonThisCalculation
            : player.Status.IsLostThisCalculation;
        if (oppId == Guid.Empty) return; // блок/скип — боя не было

        var opp = game.PlayersList.Find(x => x.GetPlayerId() == oppId);
        if (opp == null) return;

        var virusSourceId = player.Passives.TheBoysVirus
            ? player.Passives.TheBoysVirusSource
            : opp.Passives.TheBoysVirusSource;
        var virusSource = game.PlayersList.Find(x => x.GetPlayerId() == virusSourceId);
        if (virusSource?.Passives.TheBoysButcher.SuperDickActive == true) return;

        // Источник (Француз) вирусом не заражается.
        if (player.Passives.TheBoysVirus && !opp.Passives.TheBoysVirus
            && opp.GetPlayerId() != player.Passives.TheBoysVirusSource
            && !UnknownBug.Is(opp))
        {
            opp.Passives.TheBoysVirus = true;
            opp.Passives.TheBoysVirusSource = player.Passives.TheBoysVirusSource;
            game.AddGlobalLogs($"☣️ Вирус распространился на **{opp.DiscordUsername}**!");
        }
        else if (opp.Passives.TheBoysVirus && !player.Passives.TheBoysVirus
            && player.GetPlayerId() != opp.Passives.TheBoysVirusSource
            && !UnknownBug.Is(player))
        {
            player.Passives.TheBoysVirus = true;
            player.Passives.TheBoysVirusSource = opp.Passives.TheBoysVirusSource;
            game.AddGlobalLogs($"☣️ Вирус распространился на **{player.DiscordUsername}**!");
        }
    }

    public async Task HandleCharacterAfterFight(GamePlayerBridgeClass player, GameClass game, bool attack, bool defense)
    {
        TheBoysSpreadVirus(player, game);
        Madara.RecordResolvedFight(player, game, defense);
        Naruto.RecordResolvedTripleRasengan(player, game, attack);

        if (attack && GordonFreeman.Is(player)
            && (player.Status.IsWonThisCalculation != Guid.Empty
                || player.Status.IsLostThisCalculation != Guid.Empty))
        {
            var target = game.PlayersList.Find(candidate =>
                candidate.GetPlayerId() == player.Status.IsFighting);
            GordonFreeman.RescueHeadcrab(player, target);
        }

        if (player.GameCharacter.Name == DoomGuy.CharacterName
            && game.RoundNo == 10
            && player.Status.IsWonThisCalculation != Guid.Empty)
        {
            var defeated = game.PlayersList.Find(x =>
                x.GetPlayerId() == player.Status.IsWonThisCalculation);
            if (defeated?.GameCharacter.Name == "Sirinoks"
                && defeated.GameCharacter.Passive.Any(x => x.PassiveName == DoomGuy.DragonPassive))
                player.Passives.DoomGuy.DefeatedDragonSirinoks = true;
        }

        foreach (var p in game.PlayersList)
        foreach (var passive in p.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Подсчет":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var tolyaCount = p.Passives.TolyaCount;

                        if (tolyaCount.TargetList.Any(x =>
                                x.RoundNumber == game.RoundNo - 1 && x.Target == player.GetPlayerId()))
                        {
                            p.Status.AddRegularPoints(2, "Подсчет");
                            p.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(2);
                            game.Phrases.TolyaCountPhrase.SendLog(p, false);
                        }
                    }

                    break;

                case "Впарить говна":
                    if (p.GetPlayerId() != player.GetPlayerId())
                    {
                        // +1 base bonus for wins over outplay-marked enemies only
                        if (player.Passives.SellerVparitGovnaRoundsLeft > 0 &&
                            player.Passives.SellerOutplayTargets.Count > 0 &&
                            player.Passives.SellerOutplayTargets.Contains(player.Status.IsWonThisCalculation))
                        {
                            var bonusBefore = player.Status.GetBonusPointsEarnedThisRound();
                            player.Status.AddBonusPoints(1, "Впарить говна");
                            player.Passives.SellerTacticBonusEarned += player.Status.GetBonusPointsEarnedThisRound() - bonusBefore;
                        }
                    }
                    break;

                case "Выгодная сделка":
                    // p = seller (has this passive), player = fight participant
                    if (p.GetPlayerId() != player.GetPlayerId() &&
                        player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        bool isMarked = player.Passives.SellerVparitGovnaRoundsLeft > 0;
                        bool hasTactic = player.GameCharacter.Passive.Any(x => x.PassiveName == "Сомнительная тактика");
                        if (isMarked || hasTactic)
                        {
                            p.Passives.SellerProfitableDealsThisRound++;
                            p.GameCharacter.AddMoral(3, "Выгодная сделка");
                        }
                    }
                    break;

                case "Большой куш":
                    // p = seller, player = fight participant who attacked seller and won
                    if (attack && player.Status.IsWonThisCalculation == p.GetPlayerId())
                    {
                        if (_rand.Luck(1, 3))
                        {
                            if (Homelander.CanTransferFrom(p, "Большой куш"))
                            {
                                p.Status.AddBonusPoints(-3, "Большой куш");
                                player.Status.AddBonusPoints(3, "Большой куш");
                                game.Phrases.SellerBolshoiKushEnemy.SendLog(player, false);
                            }
                        }
                    }
                    break;

                // Таинственный Суппорт — "Premade": gain/lose points based on marked player's fights
                case "Premade":
                    if (p.Passives.SupportPremade.MarkedPlayerId != Guid.Empty &&
                        player.GetPlayerId() == p.Passives.SupportPremade.MarkedPlayerId)
                    {
                        if (player.Status.IsWonThisCalculation != Guid.Empty)
                            p.Status.AddRegularPoints(1, "Premade");
                        if (player.Status.IsLostThisCalculation != Guid.Empty)
                            p.Status.AddRegularPoints(-1, "Premade");

                        // Transfer carry's fight moral to support
                        var carryMoral = player.Status.MoralGainedThisFight;
                        if (carryMoral != 0)
                            p.GameCharacter.AddMoral(carryMoral, "Premade", isFightMoral: true);
                    }
                    break;

                // Toxic Mate — "Get cancer": transfer cancer when holder wins a fight
                case "Get cancer":
                    // p = Toxic Mate (cancer owner), player = fight participant
                    var cancerAll = p.Passives.ToxicMateCancer;
                    if (cancerAll.IsActive && !cancerAll.TransferredThisRound && attack && player.Status.IsWonThisCalculation != Guid.Empty
                        && player.Passives.HasToxicMateCancer && player.Passives.ToxicMateCancerSourceId == p.GetPlayerId())
                    {
                        var cancerTarget = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (cancerTarget != null && !UnknownBug.Is(cancerTarget))
                        {
                            // Remove cancer from current holder
                            player.Passives.HasToxicMateCancer = false;
                            player.Passives.ToxicMateCancerSourceId = Guid.Empty;
                            player.GameCharacter.BlockMoralGain = false;
                            cancerAll.TransferCount++;
                            cancerAll.TransferredThisRound = true;

                            if (cancerTarget.GetPlayerId() == p.GetPlayerId())
                            {
                                // Cancer returned to Toxic Mate — award bonus points and deactivate
                                p.Passives.AchievementTracker.ToxicCancerReturns++;
                                if (cancerAll.TransferCount > p.Passives.AchievementTracker.ToxicMaxTransferCount)
                                    p.Passives.AchievementTracker.ToxicMaxTransferCount = cancerAll.TransferCount;
                                var cancerBonus = cancerAll.TransferCount * 2;
                                p.Status.AddBonusPoints(cancerBonus, "Get cancer");
                                cancerAll.IsActive = false;
                                cancerAll.CurrentHolder = Guid.Empty;
                                game.Phrases.ToxicMateCancerReturn.SendLog(p, false);
                            }
                            else
                            {
                                // Transfer cancer to new victim
                                cancerTarget.Passives.HasToxicMateCancer = true;
                                cancerTarget.Passives.ToxicMateCancerSourceId = p.GetPlayerId();
                                cancerTarget.GameCharacter.BlockMoralGain = true;
                                cancerAll.CurrentHolder = cancerTarget.GetPlayerId();

                                var infectPhrases = game.Phrases.ToxicMateCancerInfect.PassiveLogRus;
                                var infectPhrase = infectPhrases[_rand.Random(0, infectPhrases.Count - 1)];
                                infectPhrase = infectPhrase.Replace("{name}", cancerTarget.DiscordUsername);
                                game.AddGlobalLogs($"Get cancer: {infectPhrase}");
                            }
                        }
                    }
                    break;
            }

        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case ScamRat.PassiveName:
                    ScamRat.TrySellGpu(player, game);
                    break;

                case Naruto.Summon:
                    if (player.GameCharacter.Name == Naruto.CharacterName && attack)
                        player.Passives.Naruto.SummonAutoWinTarget = Guid.Empty;
                    break;

                case ErenYeager.Sheep:
                    if (player.GameCharacter.Name != ErenYeager.CharacterName) break;
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                        player.GameCharacter.AddIntelligence(1, ErenYeager.Sheep);
                    else if (player.Status.IsWonThisCalculation != Guid.Empty)
                        player.GameCharacter.AddIntelligence(-2, ErenYeager.Sheep);
                    break;

                case ErenYeager.Fighter:
                {
                    if (player.GameCharacter.Name != ErenYeager.CharacterName) break;
                    var eren = player.Passives.Eren;

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var winner = game.PlayersList.Find(x =>
                            x.GetPlayerId() == player.Status.IsLostThisCalculation);
                        if (winner != null && !UnknownBug.Is(winner))
                            winner.Passives.ErenHatredMark = Math.Max(1, winner.Passives.ErenHatredMark);
                    }

                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var loser = game.PlayersList.Find(x =>
                            x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (loser?.Passives.ErenHatredMark > 0)
                        {
                            player.Status.AddBonusPoints(loser.Passives.ErenHatredMark, ErenYeager.Fighter);
                            loser.Passives.ErenHatredMark = 0;
                        }
                    }

                    if (attack && player.Status.IsFighting != Guid.Empty)
                    {
                        var enemy = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsFighting);
                        if (enemy != null
                            && enemy.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId())
                            && !eren.MutualAttackRewardsThisRound.Contains(enemy.GetPlayerId()))
                        {
                            eren.MutualAttackRewardsThisRound.Add(enemy.GetPlayerId());
                            eren.TatakeSoundSerial++;
                            player.Status.AddRegularPoints(2, ErenYeager.Fighter);
                        }
                    }

                    break;
                }

                case ErenYeager.Rumbling:
                    if (player.GameCharacter.Name == ErenYeager.CharacterName
                        && game.RoundNo == 10
                        && player.Status.IsLostThisCalculation != Guid.Empty)
                        player.Passives.Eren.Losses++;
                    break;

                case "Rune":
                {
                    if (player.GameCharacter.Name != DoomGuy.CharacterName) break;
                    var doom = player.Passives.DoomGuy;
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        doom.EverLost = true;
                        if (doom.GetActive(DoomGuy.Rune) == DoomGuy.Ascension
                            && doom.AscensionIntelligenceRemaining > 0)
                        {
                            doom.AscensionIntelligenceRemaining--;
                            player.GameCharacter.AddIntelligence(-1, DoomGuy.Ascension);
                        }
                    }

                    if (player.Status.IsWonThisCalculation != Guid.Empty
                        && doom.GetActive(DoomGuy.Rune) == DoomGuy.Extermination
                        && !doom.ExterminationAwarded)
                    {
                        if (!doom.ExterminationVictories.Contains(player.Status.IsWonThisCalculation))
                            doom.ExterminationVictories.Add(player.Status.IsWonThisCalculation);
                        if (doom.ExterminationVictories.Count >= game.PlayersList.Count - 1)
                        {
                            doom.ExterminationAwarded = true;
                            player.GameCharacter.AddIntelligence(1, DoomGuy.Extermination);
                            player.GameCharacter.AddStrength(1, DoomGuy.Extermination);
                            player.GameCharacter.AddSpeed(1, DoomGuy.Extermination);
                            player.GameCharacter.AddPsyche(1, DoomGuy.Extermination);
                            player.Status.AddBonusPoints(Math.Max(0, 10 - game.RoundNo), DoomGuy.Extermination);
                        }
                    }

                    if (player.Status.IsWonThisCalculation != Guid.Empty
                        && doom.GetActive(DoomGuy.Rune) == DoomGuy.GloryKill)
                    {
                        var defeated = game.PlayersList.Find(x =>
                            x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (DoomGuy.IsNearestEnemy(game, player, defeated))
                        {
                            var statReward = DoomGuy.HasMeleeBonus(player) ? 2 : 1;
                            player.GameCharacter.AddIntelligence(statReward, DoomGuy.GloryKill);
                            player.GameCharacter.AddStrength(statReward, DoomGuy.GloryKill);
                            player.GameCharacter.AddSpeed(statReward, DoomGuy.GloryKill);
                            player.GameCharacter.AddPsyche(statReward, DoomGuy.GloryKill);
                        }
                    }
                    break;
                }

                case "Mission":
                {
                    if (player.GameCharacter.Name != DoomGuy.CharacterName) break;
                    var doom = player.Passives.DoomGuy;
                    var resolvedFight = player.Status.IsWonThisCalculation != Guid.Empty
                                        || player.Status.IsLostThisCalculation != Guid.Empty;
                    if (resolvedFight && doom.GetActive(DoomGuy.Mission) == DoomGuy.MakeAMess)
                        player.Status.AddRegularPoints(1, DoomGuy.MakeAMess);

                    if (attack && player.Status.IsFighting != Guid.Empty
                               && doom.GetActive(DoomGuy.Mission) == DoomGuy.DemonNests
                               && player.Status.IsWonThisCalculation == player.Status.IsFighting
                               && doom.DemonNests.Remove(player.Status.IsFighting))
                    {
                        player.Status.AddRegularPoints(1, DoomGuy.DemonNests);
                        player.Status.AddInGamePersonalLogs("Адеские гнезда: гнездо уничтожено! +1 очко.\n");
                    }

                    if (attack
                        && player.Status.IsFighting != Guid.Empty
                        && player.Status.IsWonThisCalculation == player.Status.IsFighting)
                        DoomGuy.TryStealInfernalEnergy(
                            player,
                            game,
                            player.Status.IsFighting);
                    break;
                }

                case "Gun":
                {
                    if (player.GameCharacter.Name != DoomGuy.CharacterName
                        || player.Status.IsWonThisCalculation == Guid.Empty) break;
                    var doom = player.Passives.DoomGuy;
                    if (doom.GetActive(DoomGuy.Gun) == DoomGuy.Fists)
                    {
                        var defeated = game.PlayersList.Find(x =>
                            x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        var points = DoomGuy.HasMeleeBonus(player)
                                     && DoomGuy.IsNearestEnemy(game, player, defeated) ? 4 : 2;
                        player.Status.AddRegularPoints(points, DoomGuy.Fists);
                    }

                    if (doom.GetActive(DoomGuy.Gun) == DoomGuy.Chainsaw && !doom.ChainsawSpent)
                    {
                        var defeated = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (defeated != null)
                        {
                            doom.ChainsawSpent = true;
                            doom.ChainsawChoices = defeated.GameCharacter.Passive
                                .Where(x => !UnknownBug.HasSpecialPassive(x))
                                .Where(x => x.PassiveName is not GordonFreeman.Crowbar
                                    and not GordonFreeman.SilentHero
                                    and not GordonFreeman.WakeUp
                                    and not GordonFreeman.HalfLife3
                                    and not JonSnow.DumbBastard
                                    and not JonSnow.IAmJonSnow
                                    and not JonSnow.AnotherBastard
                                    and not JonSnow.BlackCastle
                                    and not JonSnow.ServerKing
                                    and not JonSnow.MyWatchHasEnded)
                                .Take(4).Select(x => x.DeepCopy()).ToList();
                            var requestedChoices = DoomGuy.HasMeleeBonus(player)
                                                   && DoomGuy.IsNearestEnemy(game, player, defeated)
                                ? 2
                                : 1;
                            doom.ChainsawSelectionsRemaining = Math.Min(requestedChoices, doom.ChainsawChoices.Count);
                            player.Status.AddInGamePersonalLogs($"Бензопила: {defeated.DiscordUsername} распилен. Выбери пассивку.\n");
                        }
                    }
                    break;
                }

                case "Возвращение из мертвых":
                    //failed
                    if (game.RoundNo > 10 && game.IsKratosEvent && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        if (Itachi.TryPreventDeath(player, game))
                            break;
                        player.Passives.IsDead = true;
                        player.Passives.DeathSource = "Kratos";
                        player.Passives.AchievementTracker.WasKilledByKratos = true;
                        // Монстр без имени: +1 regular point per death
                        foreach (var mp in game.PlayersList.Where(x => !x.Passives.IsDead
                                     && x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                        {
                            mp.Status.AddRegularPoints(1, "Монстр");
                            game.Phrases.MonsterDeath.SendLog(mp, false);
                        }
                    }

                    //start
                    else if (!game.IsKratosEvent && game.RoundNo == 10
                             && player.PlayerType != 404
                             && player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        await StartKratosEvent(game, player);
                    }

                    break;

                case "Охота на богов":
                    player.FightCharacter.SetSkillFightMultiplier();
                    break;

                case "Панцирь":
                    var сraboRackShell = player.Passives.CraboRackShell;
                    if (сraboRackShell != null)
                        if (сraboRackShell.CurrentAttacker != Guid.Empty)
                        {
                            сraboRackShell.CurrentAttacker = Guid.Empty;
                            player.Status.IsBlock = false;
                        }

                    break;

                case "Сомнительная тактика":
                    var deep = player.Passives.DeepListDoubtfulTactic;


                    if (!deep.FriendList.Contains(player.Status.IsFighting) &&
                        player.Status.IsLostThisCalculation == player.Status.IsFighting)
                    {
                        deep.FriendList.Add(player.Status.IsFighting);
                        game.Phrases.DeepListDoubtfulTacticFirstLostPhrase.SendLog(player, false);
                    }

                    if (deep.FriendList.Contains(player.Status.IsFighting))
                        if (player.Status.IsWonThisCalculation != Guid.Empty)
                        {
                            player.Status.AddRegularPoints(1, "Сомнительная тактика");
                            //me.Status.AddBonusPoints(1, "Сомнительная тактика");
                            game.Phrases.DeepListDoubtfulTacticPhrase.SendLog(player, false);
                        }

                    break;

                case "Стёб":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var target = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        //Стёб
                        var currentDeepList = player.Passives.DeepListMockeryList;

                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == target!.GetPlayerId());

                        if (currentDeepList2 != null)
                        {
                            currentDeepList2.Times++;

                            if (currentDeepList2.Times == 2 && !currentDeepList2.Triggered)
                            {
                                currentDeepList2.Triggered = true;

                                var howMuchToAdd = -1;

                                if (target!.GameCharacter.Name == "Злой Школьник")
                                {
                                    howMuchToAdd = -2;
                                    target.Status.AddInGamePersonalLogs(
                                        "MitSUKI: __Да сука, я щас ливну, заебали токсики!__\nDeepList: *хохочет*\n");
                                }

                                if (target.GameCharacter.Name != "LeCrisp")
                                {
                                    target.MinusPsyche(game, howMuchToAdd, "Стёб");
                                }


                                player.Status.AddRegularPoints(1, "Стёб");
                                game.Phrases.DeepListPokePhrase.SendLog(player, true);

                                // БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!
                                if (target!.GameCharacter.Name == "HardKitty")
                                    game.Phrases.DeepListMockeryHardKittyMilk.SendLog(player, false);

                                if (target.FightCharacter.GetPsyche() < 4)
                                    if (target.FightCharacter.Justice.GetRealJusticeNow() > 0)
                                        if (target.GameCharacter.Name != "LeCrisp")
                                            target.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(-1);
                            }
                        }
                        else
                        {
                            currentDeepList.WhoWonTimes.Add(new DeepList.MockerySub(target!.GetPlayerId(), 1));
                        }


                        //end Стёб
                    }

                    break;

                case "Месть":
                    //enemyIdLostTo may be 0
                    var mylorik = player.Passives.MylorikRevenge;

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        //check if very first lost
                        if (mylorik.EnemyListPlayerIds.All(x => x.EnemyPlayerId != player.Status.IsLostThisCalculation))
                        {
                            mylorik.EnemyListPlayerIds.Add(
                                new Mylorik.MylorikRevengeClassSub(player.Status.IsLostThisCalculation, game.RoundNo));
                            game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                        }
                    }
                    else
                    {
                        var find = mylorik?.EnemyListPlayerIds.Find(x =>
                            x.EnemyPlayerId == player.Status.IsWonThisCalculation && x.IsUnique);

                        if (find != null && find.RoundNumber != game.RoundNo)
                        {
                            player.Status.AddRegularPoints(2, "Месть");
                            player.GameCharacter.AddMoral(3, "Месть");
                            player.GameCharacter.AddPsyche(1, "Месть");
                            find.IsUnique = false;
                            game.Phrases.MylorikRevengeVictoryPhrase.SendLog(player, true);
                        }
                    }

                    break;

                case "Испанец":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var boole = player.Passives.MylorikSpanish;

                        if (_rand.Luck(1, 2))
                        {
                            boole.Times = 0;
                            player.GameCharacter.AddExtraSkill(10, "Испанец");
                            player.MinusPsyche(game, -1, "Испанец");
                            game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                        }
                        else
                        {
                            boole.Times++;

                            if (boole.Times == 2)
                            {
                                boole.Times = 0;
                                player.GameCharacter.AddExtraSkill(10, "Испанец");
                                player.MinusPsyche(game, -1, "Испанец");
                                game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                            }
                        }
                    }

                    break;

                case "Спящее хуйло":
                    if (player.Passives.GlebSkip && player.Status.WhoToAttackThisTurn.Count != 0)
                    {
                        player.Status.IsSkip = false;
                        player.Passives.GlebSkip = false;

                        // 33% chance "POSTAV ROLI" when waking up and NOT in Challenger mode
                        var glebChallenger = player.Passives.GlebChallengerTriggeredWhen;
                        if (!glebChallenger.WhenToTrigger.Contains(game.RoundNo) && _rand.Luck(1, 3))
                        {
                            game.Phrases.GlebWakeUpRoli.SendLog(player, false);
                        }
                    }

                    break;

                case "Импакт":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var lePuska = player.Passives.LeCrispImpact;


                        player.GameCharacter.AddMoral(lePuska.ImpactTimes + 1, "Импакт");
                    }

                    break;


                case "Доебаться":
                    var hardKitty = player.Passives.HardKittyDoebatsya;

                    if (player.Status.WhoToAttackThisTurn.Count != 0 && attack)
                        if (player.Status.WhoToAttackThisTurn.Contains(player.Status.IsLostThisCalculation) ||
                            player.Status.WhoToAttackThisTurn.Contains(player.Status.IsTargetBlocked) ||
                            player.Status.WhoToAttackThisTurn.Contains(player.Status.IsTargetSkipped))
                        {
                            var found = hardKitty.LostSeriesCurrent.Find(x =>
                                player.Status.WhoToAttackThisTurn.Contains(x.EnemyPlayerId));

                            if (found != null)
                                found.Series++;
                            else
                                hardKitty.LostSeriesCurrent.Add(
                                    new HardKitty.DoebatsyaSubClass(player.Status.WhoToAttackThisTurn[0]));
                        }

                    if (player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.WhoToAttackThisTurn.Contains(player.Status.IsWonThisCalculation) && attack)
                    {
                        var found = hardKitty.LostSeriesCurrent.Find(x =>
                            player.Status.WhoToAttackThisTurn.Contains(x.EnemyPlayerId));
                        if (found is { Series: > 0 })
                        {
                            if (found.Series >= 7) 
                                found.Series += 10;

                            player.Status.AddRegularPoints(found.Series * 2, "Доебаться");

                            if (found.Series >= 7)
                                game.Phrases.HardKittyDoebatsyaLovePhrase.SendLog(player, false);
                            else
                                game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
                            found.Series = 0;
                        }
                    }

                    break;

                case "Произошел троллинг":
                    if (player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.WhoToAttackThisTurn.Contains(player.Status.IsWonThisCalculation))
                    {
                        var awdka = player.Passives.AwdkaTrollingList;

                        var enemy = awdka.EnemyList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                        if (enemy == null)
                            awdka.EnemyList.Add(new Awdka.TrollingSubClass(player.Status.IsWonThisCalculation,
                                game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!
                                    .Status
                                    .GetScore()));
                        else
                            enemy.Score =
                                game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!
                                    .Status.GetScore();
                    }

                    break;

                case "Я пытаюсь!":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var awdka = player.Passives.AwdkaTryingList;


                        var enemy = awdka.TryingList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                        if (enemy == null)
                            awdka.TryingList.Add(new Awdka.TryingSubClass(player.Status.IsLostThisCalculation));
                        else
                            enemy.Times++;
                    }

                    break;

                case "Привет со дна":

                    /*//привет со дна
                    if (me.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var moral = me.Status.GetPlaceAtLeaderBoard() - game.PlayersList
                            .Find(x => x.GetPlayerId() == me.Status.IsWonThisCalculation).Status.GetPlaceAtLeaderBoard();
                        if (moral > 0)
                            me.FightCharacter.AddMoral(moral, "Привет со дна");
                    }
                    //end привет со дна*/

                    break;

                case "Не повезло":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        player.MinusPsyche(game, -1, "Не повезло");
                        game.Phrases.DarksciNotLucky.SendLog(player, false);
                    }

                    break;

                case "3-0 обоссан":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var tigr = player.Passives.TigrThreeZeroList;
                        tigr.WhoToWinThisRound.Add(player.Status.IsWonThisCalculation);
                    }

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var tigr = player.Passives.TigrThreeZeroList;
                        tigr.WhoToLostThisRound.Add(player.Status.IsLostThisCalculation);
                    }
                    break;

                case "Челюсти":
                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var shark = player.Passives.SharkJawsWin;


                        if (!shark.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            shark.FriendList.Add(player.Status.IsWonThisCalculation);
                            player.GameCharacter.AddSpeed(1, "Челюсти");
                        }
                    }

                    break;

                case "Первая кровь":
                    var spartan = player.Passives.SpartanFirstBlood;

                    if (spartan.FriendList.Count == 1)
                    {
                        if (spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                        {
                            player.GameCharacter.AddSpeed(1, "Первая кровь");
                            game.Phrases.SpartanFirstBlood.SendLog(player, false);
                            game.AddGlobalLogs("Они познают войну!");
                        }
                        else if (spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                        {
                            var ene = game.PlayersList.Find(x =>
                                x.GetPlayerId() == player.Status.IsLostThisCalculation);
                            ene!.GameCharacter.AddSpeed(1, "Первая кровь");
                        }

                        spartan.FriendList.Add(Guid.Empty);
                    }

                    break;

                case "Это привилегия - умереть от моей руки":
                    if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
                    {
                        game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation)!.GameCharacter
                            .Justice.AddJusticeForNextRoundFromSkill();
                        player.GameCharacter.AddIntelligence(-1, "Это привилегия");
                    }

                    break;

                case "Им это не понравится":
                    var spartanTheyWontLikeIt = player.Passives.SpartanMark;

                    var spartanWinTarget = game.PlayersList.Find(candidate =>
                        candidate.GetPlayerId() == player.Status.IsWonThisCalculation);
                    if (spartanWinTarget != null && Salldorum.IsRedirectedRandomTarget(
                            game, player, spartanWinTarget, spartanTheyWontLikeIt.FriendList))
                    {
                        player.Status.AddRegularPoints(1, "Им это не понравится");
                        player.Status.AddBonusPoints(1, "Им это не понравится");
                    }

                    break;

                case "Гематофагия":
                    var vampyr = player.Passives.VampyrHematophagiaList;

                    if (player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        var target = vampyr.HematophagiaCurrent.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);
                        var repeat = vampyr.HematophagiaAddEndofRound.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                        if (target == null && repeat == null)
                        {
                            var statIndex = 0;
                            var found = false;
                            var tries = 0;
                            while (!found)
                            {
                                tries++;
                                if (tries > 20)
                                {
                                    break;
                                }

                                statIndex = _rand.Random(1, 4);

                                //поскольку мы вернули вампуру прокачку, надо добавить в условие на психику, что оно работает только когда психика <=8
                                if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count < 4 && player.GameCharacter.GetPsyche() <= 8)
                                {
                                    if (!player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Any(x => x.StatIndex == 4))
                                    {
                                        statIndex = _rand.Random(4, 4);
                                    }
                                }

                                //поскольку мы вернули вампуру прокачку, надо добавить в условие на психику, что оно работает только когда психика <=8
                                if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count < 5 && player.GameCharacter.GetPsyche() <= 8)
                                {
                                    if (player.Passives.VampyrHematophagiaList.HematophagiaCurrent.Count(x => x.StatIndex == 4) < 2)
                                    {
                                        statIndex = _rand.Random(4, 4);
                                    }
                                }

                                switch (statIndex)
                                {
                                    case 1:
                                        if (player.GameCharacter.GetIntelligence() < 10) found = true;
                                        break;
                                    case 2:
                                        if (player.GameCharacter.GetStrength() < 10) found = true;

                                        break;
                                    case 3:
                                        if (player.GameCharacter.GetSpeed() < 10) found = true;

                                        break;
                                    case 4:
                                        if (player.GameCharacter.GetPsyche() < 10) found = true;

                                        break;
                                }
                            }
                            vampyr.HematophagiaAddEndofRound.Add(new Vampyr.HematophagiaSubClass(statIndex, player.Status.IsWonThisCalculation));
                        }
                    }

                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        var queuedBiteIds = vampyr.HematophagiaRemoveEndofRound
                            .Select(x => x.EnemyId)
                            .ToHashSet();
                        var target = vampyr.HematophagiaCurrent.Find(x =>
                            x.EnemyId == player.Status.IsLostThisCalculation
                            && !queuedBiteIds.Contains(x.EnemyId));

                        if (target == null)
                        {
                            var removableBites = vampyr.HematophagiaCurrent
                                .Where(x => !queuedBiteIds.Contains(x.EnemyId))
                                .ToList();
                            if (removableBites.Count > 0)
                            {
                                var randomIndex = _rand.Random(0, removableBites.Count - 1);
                                target = removableBites[randomIndex];
                            }
                        }

                        if (target != null)
                            vampyr.HematophagiaRemoveEndofRound.Add(target);
                    }

                    break;

                case "Огурчик Рик":
                    var pickleAfterFight = player.Passives.RickPickle;
                    if (pickleAfterFight.PickleTurnsRemaining > 0 && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        game.Phrases.RickPickleWin.SendLog(player, false);
                    }
                    break;

                // Вороны: place crow on attacked enemy if crow ready (after level-up)
                case "Вороны":
                    if (attack && player.Passives.ItachiCrows.CrowReadyToThrow)
                    {
                        var crowsAfter = player.Passives.ItachiCrows;
                        // Place crow on the fight target (win or lose); a blocked/skipped
                        // attack still lands the crow even though the fight never happened
                        var crowTargetId = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        if (crowTargetId == Guid.Empty)
                            crowTargetId = player.Status.IsTargetBlocked != Guid.Empty
                                ? player.Status.IsTargetBlocked
                                : player.Status.IsTargetSkipped;
                        var crowTarget = game.PlayersList.Find(candidate =>
                            candidate.GetPlayerId() == crowTargetId);
                        if (crowTarget != null && !UnknownBug.Is(crowTarget))
                        {
                            if (!crowsAfter.CrowCounts.ContainsKey(crowTargetId))
                                crowsAfter.CrowCounts[crowTargetId] = 0;
                            crowsAfter.CrowCounts[crowTargetId]++;
                            crowsAfter.CrowReadyToThrow = false;
                            game.Phrases.ItachiCrows.SendLog(player, false);
                        }
                    }
                    break;

                // Глаза Итачи: re-attack interrupt, then activate Tsukuyomi if charged (attack only, win or loss)
                case "Глаза Итачи":
                    var itachiTsuk = player.Passives.ItachiTsukuyomi;
                    var itachiFoughtTarget = player.Status.IsWonThisCalculation != Guid.Empty
                        ? player.Status.IsWonThisCalculation
                        : player.Status.IsLostThisCalculation;
                    var itachiFoughtPlayer = game.PlayersList.Find(candidate =>
                        candidate.GetPlayerId() == itachiFoughtTarget);

                    // Re-attack interrupt: attacking a target already under Tsukuyomi cancels it (no steal this turn)
                    if (attack && itachiFoughtTarget != Guid.Empty
                        && itachiTsuk.TsukuyomiActiveTarget == itachiFoughtTarget)
                    {
                        itachiTsuk.TsukuyomiActiveTarget = Guid.Empty;
                        itachiTsuk.TsukuyomiTargetThisRound = Guid.Empty;
                        game.Phrases.ItachiTsukuyomiEnd.SendLog(player, false);
                        break;
                    }

                    // One enemy never falls for the trick twice: a refused re-cast keeps the charge
                    if (attack && itachiTsuk.ChargeCounter >= 2 && itachiFoughtTarget != Guid.Empty
                               && !itachiTsuk.CaughtPlayers.Contains(itachiFoughtTarget)
                               && (itachiFoughtPlayer == null
                                   || !UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, itachiFoughtPlayer))))
                    {
                        itachiTsuk.TsukuyomiTargetThisRound = itachiFoughtTarget;
                        itachiTsuk.TsukuyomiActiveTarget = itachiFoughtTarget;
                        itachiTsuk.CaughtPlayers.Add(itachiFoughtTarget);
                        itachiTsuk.ChargeCounter = -2; // recharges over 2 rounds
                        game.Phrases.ItachiTsukuyomiActivate.SendLog(player, false);
                    }
                    break;

                // Таинственный Суппорт — "Stakes!": bonus point every 3rd round on non-marked win
                case "Stakes!":
                    if (game.RoundNo % 3 == 0 && attack &&
                        player.Status.IsWonThisCalculation != Guid.Empty &&
                        player.Status.IsWonThisCalculation != player.Passives.SupportPremade.MarkedPlayerId)
                    {
                        player.Status.AddRegularPoints(1, "Stakes!");
                        game.Phrases.SupportStakes.SendLog(player, false);
                    }
                    break;

                // Toxic Mate — "INT": +1 point on loss, first loss global log
                case "INT":
                    if (player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        player.Status.AddRegularPoints(1, "INT");
                        var cancerInt = player.Passives.ToxicMateCancer;
                        if (!cancerInt.FirstLossTriggered)
                        {
                            cancerInt.FirstLossTriggered = true;
                            game.AddGlobalLogs("**Ok. I'm trolling.**");
                        }
                    }
                    break;

                // Toxic Mate — "Get cancer": infect target on first win (cancer not yet active)
                case "Get cancer":
                    var cancerOwn = player.Passives.ToxicMateCancer;
                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty && !cancerOwn.IsActive)
                    {
                        var cancerVictim = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                        if (cancerVictim != null && !UnknownBug.Is(cancerVictim))
                        {
                            cancerOwn.IsActive = true;
                            cancerOwn.CurrentHolder = cancerVictim.GetPlayerId();
                            cancerOwn.TransferCount = 0;
                            cancerVictim.Passives.HasToxicMateCancer = true;
                            cancerVictim.Passives.ToxicMateCancerSourceId = player.GetPlayerId();
                            cancerVictim.GameCharacter.BlockMoralGain = true;

                            var infectMsgs = game.Phrases.ToxicMateCancerInfect.PassiveLogRus;
                            var infectMsg = infectMsgs[_rand.Random(0, infectMsgs.Count - 1)];
                            infectMsg = infectMsg.Replace("{name}", cancerVictim.DiscordUsername);
                            game.AddGlobalLogs($"Get cancer: {infectMsg}");
                        }
                    }
                    break;

                // Toxic Mate — "Aggress": +1 point if attack didn't result in a fight (target blocked/skipped)
                case "Aggress":
                    if (attack && player.Status.IsWonThisCalculation == Guid.Empty && player.Status.IsLostThisCalculation == Guid.Empty)
                    {
                        player.Status.AddRegularPoints(1, "Aggress");
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                        game.Phrases.ToxicMateAggressPoint.SendLog(player, false);
                    }
                    break;

                // Котики — Минька: always gain Moral (+1) and Skill (+10) from any fight
                case "Минька":
                    if (player.Status.IsWonThisCalculation != Guid.Empty || player.Status.IsLostThisCalculation != Guid.Empty)
                    {
                        // Immunity: if this is a transferred cat passive and the fight is against the cat owner, skip buff
                        var minkaFightTarget = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        var isTransferredMinkaVsOwner = player.Passives.KotikiCatOwnerId != Guid.Empty
                            && minkaFightTarget == player.Passives.KotikiCatOwnerId;

                        if (!isTransferredMinkaVsOwner)
                        {
                            player.GameCharacter.AddMoral(1, "Минька");
                            player.GameCharacter.AddExtraSkill(10, "Минька");
                            game.Phrases.KotikiMinka.SendLog(player, false);
                        }
                    }
                    break;

                // Котики — Штормяк: if taunted enemy lost to Котики → -1 Psyche, rage, give top stat
                case "Штормяк":
                    var stormAfterFight = player.Passives.KotikiStorm;
                    if (stormAfterFight.CurrentTauntTarget != Guid.Empty
                        && player.Status.IsWonThisCalculation == stormAfterFight.CurrentTauntTarget)
                    {
                        var tauntLoser = game.PlayersList.Find(x => x.GetPlayerId() == stormAfterFight.CurrentTauntTarget);
                        if (tauntLoser != null)
                        {
                            // -1 Psyche + rage log
                            tauntLoser.MinusPsyche(game, -1, "Штормяк");
                            game.Phrases.KotikiStormWin.SendLog(player, false);

                            // Give top stat: enemy's highest stat -1, Котики +1 same stat
                            var statValues = new[]
                            {
                                ("Int", tauntLoser.GameCharacter.GetIntelligence()),
                                ("Str", tauntLoser.GameCharacter.GetStrength()),
                                ("Spd", tauntLoser.GameCharacter.GetSpeed()),
                                ("Psy", tauntLoser.GameCharacter.GetPsyche())
                            };
                            var topStat = statValues.OrderByDescending(s => s.Item2).First();
                            switch (topStat.Item1)
                            {
                                case "Int":
                                    tauntLoser.GameCharacter.AddIntelligence(-1, "Штормяк");
                                    player.GameCharacter.AddIntelligence(1, "Штормяк");
                                    break;
                                case "Str":
                                    tauntLoser.GameCharacter.AddStrength(-1, "Штормяк");
                                    player.GameCharacter.AddStrength(1, "Штормяк");
                                    break;
                                case "Spd":
                                    tauntLoser.GameCharacter.AddSpeed(-1, "Штормяк");
                                    player.GameCharacter.AddSpeed(1, "Штормяк");
                                    break;
                                case "Psy":
                                    tauntLoser.MinusPsyche(game, -1, "Штормяк");
                                    player.GameCharacter.AddPsyche(1, "Штормяк");
                                    break;
                            }
                        }
                    }
                    break;

                // Котики — Кошачья засада: cat deploy/return after fight
                case "Кошачья засада":
                    var fightEnemyId = player.Status.IsWonThisCalculation != Guid.Empty
                        ? player.Status.IsWonThisCalculation
                        : player.Status.IsLostThisCalculation;

                    if (fightEnemyId != Guid.Empty)
                    {
                        var ambush = player.Passives.KotikiAmbush;
                        var fightEnemy = game.PlayersList.Find(x => x.GetPlayerId() == fightEnemyId);

                        if (fightEnemy != null)
                        {
                            // Cat return: only when we attack the enemy who has our cat
                            if (attack && fightEnemy.Passives.KotikiCatOwnerId == player.GetPlayerId())
                            {
                                var catType = fightEnemy.Passives.KotikiCatType;
                                game.Phrases.KotikiCatReturn.SendLog(player, false);
                                var isVictory = player.Status.IsWonThisCalculation == fightEnemyId;

                                if (isVictory && catType is "Минька" or "Штормяк")
                                    player.Passives.AchievementTracker.KotikiCatsReclaimed.Add(catType);

                                if (catType == "Минька")
                                {
                                    if (isVictory)
                                    {
                                        var roundsOnEnemy = ambush.MinkaRoundsOnEnemy;
                                        player.Status.AddBonusPoints(2, "Кошачья засада");
                                        player.GameCharacter.AddExtraSkill(33 * roundsOnEnemy, "Кошачья засада (Минька)");
                                    }
                                    ambush.MinkaOnPlayer = Guid.Empty;
                                    ambush.MinkaRoundsOnEnemy = 0;
                                    ambush.MinkaCooldown = 2;
                                }
                                else if (catType == "Штормяк")
                                {
                                    if (isVictory)
                                    {
                                        // Eat half of what the victim earned WHILE the cat sat on them
                                        // (score delta since deploy), not half of their entire score (finding M9).
                                        var earnedWhileSat = fightEnemy.Status.GetScore() - ambush.StormScoreSnapshot;
                                        var stolenPoints = Math.Floor(earnedWhileSat / 2);
                                        if (stolenPoints > 0
                                            && !UnknownBug.Is(fightEnemy)
                                            && Homelander.CanTransferFrom(
                                                fightEnemy, "Кошачья засада"))
                                        {
                                            fightEnemy.Status.AddBonusPoints(-stolenPoints, "Кошачья засада");
                                            player.Status.AddBonusPoints(stolenPoints, "Кошачья засада (Штормяк)");
                                        }
                                        fightEnemy.MinusPsyche(game, -1, "Кошачья засада");
                                    }
                                    ambush.StormOnPlayer = Guid.Empty;
                                    ambush.StormScoreSnapshot = 0;
                                    ambush.StormCooldown = 2;
                                }

                                // Remove cat passive from enemy, restore to owner
                                fightEnemy.GameCharacter.Passive.RemoveAll(x => x.PassiveName == catType
                                    && fightEnemy.Passives.KotikiCatOwnerId == player.GetPlayerId());
                                fightEnemy.Passives.KotikiCatType = "";
                                fightEnemy.Passives.KotikiCatOwnerId = Guid.Empty;

                                // Return "Рандомное поведение" from enemy to Котики when Storm returns
                                if (catType == "Штормяк")
                                {
                                    fightEnemy.GameCharacter.Passive.RemoveAll(x => x.PassiveName == "Рандомное поведение");
                                    if (!player.GameCharacter.Passive.Any(x => x.PassiveName == "Рандомное поведение"))
                                        player.GameCharacter.Passive.Add(new Passive("Рандомное поведение",
                                            "Штормяк время от времени выкидывает фокусы.", false));
                                }

                                // Restore passive to Котики
                                if (!player.GameCharacter.Passive.Any(x => x.PassiveName == catType))
                                {
                                    var desc = catType == "Минька"
                                        ? "Котики всегда получают Мораль и Скилл от боёв. Никогда не наносят вреда."
                                        : "Блок = провокация случайного врага. Враг атакует Котиков вторым действием. Если проигрывает: -1 Психика, даёт топ стат.";
                                    player.GameCharacter.Passive.Add(new Passive(catType, desc, true));
                                }
                            }
                            // Cat deploy: only on attack, 100% deploy, 50% cat choice, only 1 cat out at a time
                            else if (attack && fightEnemy.Passives.KotikiCatOwnerId == Guid.Empty
                                            && !UnknownBug.Is(fightEnemy))
                            {
                                // Only 1 cat deployed at a time
                                var minkaOut = ambush.MinkaOnPlayer != Guid.Empty;
                                var stormOut = ambush.StormOnPlayer != Guid.Empty;
                                if (minkaOut || stormOut) break; // a cat is already deployed

                                // Determine which cats are off cooldown
                                var minkaAvailable = ambush.MinkaCooldown <= 0;
                                var stormAvailable = ambush.StormCooldown <= 0;

                                if (minkaAvailable || stormAvailable)
                                {
                                    // 50% chance to pick which cat (if both available)
                                    string deployType;
                                    if (minkaAvailable && stormAvailable)
                                        deployType = _rand.Luck(1, 2) ? "Минька" : "Штормяк";
                                    else
                                        deployType = minkaAvailable ? "Минька" : "Штормяк";

                                    // Add passive to enemy
                                    fightEnemy.GameCharacter.Passive.Add(new Passive(
                                        deployType,
                                        $"Кот {deployType} от {player.DiscordUsername}",
                                        true
                                    ));
                                    fightEnemy.Passives.KotikiCatType = deployType;
                                    fightEnemy.Passives.KotikiCatOwnerId = player.GetPlayerId();

                                    if (deployType == "Минька")
                                        ambush.MinkaOnPlayer = fightEnemyId;
                                    else
                                    {
                                        ambush.StormOnPlayer = fightEnemyId;
                                        // Snapshot the victim's score now, so the return steal is half of what
                                        // they earn WHILE the cat sits (finding M9) — not half of their whole score.
                                        ambush.StormScoreSnapshot = fightEnemy.Status.GetScore();

                                        // Transfer "Рандомное поведение" to the enemy carrying Storm
                                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Рандомное поведение"))
                                        {
                                            fightEnemy.GameCharacter.Passive.Add(new Passive(
                                                "Рандомное поведение",
                                                "Штормяк время от времени выкидывает фокусы.",
                                                false));
                                            player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == "Рандомное поведение");
                                        }
                                    }

                                    // Remove deployed cat's passive from Котики until it returns
                                    player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == deployType);

                                    game.Phrases.KotikiCatDeploy.SendLog(player, false);
                                    player.Status.AddInGamePersonalLogs(
                                        $"Кошачья засада: {deployType} остался на {fightEnemy.DiscordUsername}!\n");
                                    fightEnemy.Status.AddInGamePersonalLogs(
                                        $"Кошачья засада: Кот {deployType} сидит на вас!\n");
                                }
                            }
                        }
                    }
                    break;

                // TheBoys — Francie: завершение заказа + Хим.оружие + Смертельный вирус
                case "Francie":
                    if (player.Passives.TheBoysButcher.SuperDickActive) break; // СуперМудень отключает Француза
                    var francieAfter = player.Passives.TheBoysFrancie;

                    if (attack && player.Status.IsWonThisCalculation != Guid.Empty)
                    {
                        // Заказ выполнен?
                        // Completion is settled in the fight hook before the round-transition expiry.
                        // OrderTarget is authoritative; the display counter must never cancel a final-turn win.
                        if (francieAfter.OrderTarget == player.Status.IsWonThisCalculation)
                        {
                            francieAfter.OrdersCompleted++;
                            player.Passives.AchievementTracker.TheBoysOrdersCompleted++;
                            francieAfter.OrderTarget = Guid.Empty;
                            francieAfter.OrderRoundsLeft = 0;
                            player.Status.AddRegularPoints(1, "Заказ Француза");
                            game.Phrases.TheBoysOrderComplete.SendLog(player, false);
                        }

                        // Хим.оружие: бонусные очки за честную победу
                        var chemLevel = francieAfter.ChemWeaponLevel;
                        if (chemLevel > 0)
                        {
                            var chemEnemy = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsWonThisCalculation);
                            if (chemEnemy != null && !chemEnemy.Status.FightEnemyWasTooGood && !chemEnemy.Status.FightEnemyWasTooStronk)
                            {
                                // m10: harder enemy pays more — +1 base, +1 if the enemy was TooGood for
                                // TheBoys, +1 if TooStronk (flags on player = "my enemy was too good/stronk"); × прокачки.
                                var chemDifficultyMult = 1 + (player.Status.FightEnemyWasTooGood ? 1 : 0)
                                                           + (player.Status.FightEnemyWasTooStronk ? 1 : 0);
                                player.Status.AddBonusPoints(chemLevel * chemDifficultyMult, "Хим.оружие");
                                game.Phrases.TheBoysChemWeapon.SendLog(player, false);
                            }
                        }
                    }

                    // Смертельный вирус: следующая атака (реальный бой) вешает вирус на цель
                    if (attack && francieAfter.VirusArmed)
                    {
                        var virusTargetId = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        if (virusTargetId != Guid.Empty)
                        {
                            var virusTarget = game.PlayersList.Find(x => x.GetPlayerId() == virusTargetId);
                            if (virusTarget != null && !UnknownBug.Is(virusTarget))
                            {
                                virusTarget.Passives.TheBoysVirus = true;
                                virusTarget.Passives.TheBoysVirusSource = player.GetPlayerId();
                                francieAfter.VirusArmed = false;
                                francieAfter.VirusUsed = true;
                                game.Phrases.TheBoysVirusApply.SendLog(player, false);
                            }
                        }
                    }
                    break;

                // TheBoys — Butcher: охота на супов (+Скилл за нападение на супа, очко если Скинул)
                case "Butcher":
                    if (player.Passives.TheBoysButcher.ButcherLeft) break;
                    if (attack)
                    {
                        var fightTargetId = player.Status.IsWonThisCalculation != Guid.Empty
                            ? player.Status.IsWonThisCalculation
                            : player.Status.IsLostThisCalculation;
                        if (fightTargetId != Guid.Empty)
                        {
                            var supTarget = game.PlayersList.Find(x => x.GetPlayerId() == fightTargetId);
                            if (supTarget != null && supTarget.Passives.TheBoysSupMark)
                            {
                                var superDick = player.Passives.TheBoysButcher.SuperDickActive;
                                player.GameCharacter.AddExtraSkill(superDick ? 20 : 10, "Butcher");
                                // +1 bonus point moved to the Drop path in DoomsdayMachine (finding M7):
                                // the point is for Dropping the sup ("Скинуть"), not for merely winning.
                                game.Phrases.TheBoysButcherHunt.SendLog(player, false);
                            }
                        }
                    }
                    break;

                // TheBoys — M.M.: сбор компромата + учёт исхода боёв раунда (для базовой психики)
                case "M.M.":
                    var mmData = player.Passives.TheBoysMM;
                    if (player.Status.IsWonThisCalculation != Guid.Empty) mmData.WonThisRound++;
                    if (player.Status.IsLostThisCalculation != Guid.Empty) mmData.LostThisRound++;

                    if (player.Passives.TheBoysButcher.SuperDickActive) break; // СуперМудень отключает М.М.

                    if (attack && mmData.NextAttackGathersKompromat)
                    {
                        // Fight must have happened (won or lost, but not block/skip)
                        if (player.Status.IsWonThisCalculation != Guid.Empty ||
                            player.Status.IsLostThisCalculation != Guid.Empty)
                        {
                            var fightTargetId = player.Status.IsWonThisCalculation != Guid.Empty
                                ? player.Status.IsWonThisCalculation
                                : player.Status.IsLostThisCalculation;
                            var fightTarget = game.PlayersList.Find(x => x.GetPlayerId() == fightTargetId);
                            if (fightTarget != null && !mmData.KompromatTargets.Contains(fightTargetId))
                            {
                                mmData.KompromatTargets.Add(fightTargetId);
                                Homelander.RecordReveal(game, player, fightTarget);
                                var hint = GetKompromatHint(fightTarget, game);
                                mmData.KompromatHints[fightTargetId] = hint;
                                player.Status.AddInGamePersonalLogs(
                                    $"Компромат М.М.: Досье на {fightTarget.DiscordUsername}: {hint}\n");
                                game.Phrases.TheBoysKompromatGathered.SendLog(player, false);
                            }
                            mmData.NextAttackGathersKompromat = false;
                        }
                    }
                    break;
            }
    }

    public void HandleRumblingAfterFights(GameClass game)
    {
        if (game.RoundNo != 10) return;

        var eren = game.PlayersList.Find(player =>
            player.GameCharacter.Name == ErenYeager.CharacterName
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == ErenYeager.Rumbling)
            && !player.Passives.IsDead);
        if (eren == null || eren.Passives.Eren.RumblingTriggered || eren.Passives.Eren.Losses >= 2) return;

        var projected = ErenYeager.ProjectRoundEndLeaderboard(game);
        var erenIndex = projected.IndexOf(eren);
        if (erenIndex < 0) return;

        eren.Passives.Eren.RumblingTriggered = true;
        eren.Passives.Eren.RumblingPlace = erenIndex + 1;

        // Eldia is the sixth position itself: only players strictly between Eren and place 6 die.
        var victims = projected
            .Skip(erenIndex + 1)
            .Take(Math.Max(0, projected.Count - erenIndex - 2))
            .Where(player => !player.Passives.IsDead
                             && !Madara.IsMadara(player)
                             && !UnknownBug.Is(player))
            .ToList();

        var actualVictims = new List<GamePlayerBridgeClass>();
        foreach (var victim in victims)
        {
            if (Itachi.TryPreventDeath(victim, game))
                continue;
            if (!JonSnow.TryEndWatch(victim, game, "Rumbling"))
            {
                victim.Passives.IsDead = true;
                victim.Passives.DeathSource = "Rumbling";
            }
            actualVictims.Add(victim);
            eren.Passives.AchievementTracker.RumblingVictimIds.Add(victim.GetPlayerId());
        }

        // Public aftermath intensity and every death-derived reward count only lethal events.
        // End-of-Watch still counts as a death; Izanagi does not.
        eren.Passives.Eren.RumblingKillCount = Math.Min(4, actualVictims.Count);

        if (actualVictims.Count == 0)
        {
            game.AddGlobalLogs(
                victims.Count == 0
                    ? $"Rumbling: Эрен остался на {erenIndex + 1} месте. Между ним и Элдией никого нет."
                    : $"Rumbling: Эрен остался на {erenIndex + 1} месте, но никто не погиб.");
            return;
        }

        game.AddGlobalLogs(
            $"Rumbling: Колоссальные титаны уничтожили {string.Join(", ", actualVictims.Select(x => x.DiscordUsername))}!");

        foreach (var monster in game.PlayersList.Where(x =>
                     !x.Passives.IsDead
                     && x.GameCharacter.Passive.Any(passive => passive.PassiveName == "Монстр")))
        {
            monster.Status.AddRegularPoints(actualVictims.Count, "Монстр");
            game.Phrases.MonsterDeath.SendLog(monster, false);
        }
    }
    //end handle during fight


    private static bool CanKratosReturnFromGod(GamePlayerBridgeClass kratos)
    {
        return kratos.Passives.IsDead
               && GodClass.IsGodDeathSource(kratos.Passives.DeathSource)
               && !kratos.Passives.KratosGodSlayerUsed
               && kratos.GameCharacter.Passive.Any(passive => passive.PassiveName == "Боги мне не указ");
    }

    private static async Task StartKratosEvent(GameClass game, GamePlayerBridgeClass kratos)
    {
        if (game.IsKratosEvent) return;

        game.IsKratosEvent = true;
        game.AddGlobalLogs("Бегите! На Гору Мусорной Горы идёт Кратос и НИЧТО его не остановит!");
        foreach (var player in game.PlayersList.Where(player => !player.IsBot()))
            await game.Phrases.KratosEventYes.SendLogSeparateWithFile(player, false,
                "DataBase/sound/Kratos.mp3", false, 15000, roundsToPlay: 5);

        kratos.GameCharacter.SetClassSkillMultiplier(4);
    }


    //after all fight
    public async Task HandleEndOfRound(GameClass game)
    {
        var eventKratos = game.PlayersList.Find(x =>
            x.GameCharacter.Name == "Кратос"
            && x.GameCharacter.Passive.Any(passive => passive.PassiveName == "Возвращение из мертвых"));
        if (game.IsKratosEvent && eventKratos?.Passives.IsDead == true
                               && !CanKratosReturnFromGod(eventKratos))
        {
            game.IsKratosEvent = false;
            game.AddGlobalLogs($"{UnknownBug.PublicName(eventKratos)} решил доверится богам зная последствия...");
            await game.Phrases.KratosEventFailed.SendLogSeparateWithFile(eventKratos, false,
                "DataBase/art/events/kratos_hell.png", false, 15000);
        }

        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            foreach (var passive in player.GameCharacter.Passive.ToList())
                switch (passive.PassiveName)
                {
                case Naruto.HaremJutsu:
                    if (player.GameCharacter.Name == Naruto.CharacterName)
                    {
                        var naruto = player.Passives.Naruto;
                        if (naruto.HaremActiveThisRound)
                        {
                            naruto.HaremActiveThisRound = false;
                            naruto.HaremDonorIdsThisRound.Clear();
                            naruto.HaremCooldown = Naruto.HaremCooldownTurns;
                        }
                        else if (naruto.HaremCooldown > 0)
                        {
                            naruto.HaremCooldown--;
                        }
                    }
                    break;

                case ErenYeager.Fighter:
                    if (player.GameCharacter.Name == ErenYeager.CharacterName)
                        player.Passives.Eren.MutualAttackRewardsThisRound.Clear();
                    break;

                case ErenYeager.AttackTitan:
                    if (player.GameCharacter.Name == ErenYeager.CharacterName)
                    {
                        var eren = player.Passives.Eren;
                        if (eren.AttackTitanActiveThisRound)
                        {
                            var wasAttacked = game.PlayersList.Any(enemy =>
                                enemy.GetPlayerId() != player.GetPlayerId()
                                && enemy.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId()));
                            if (!wasAttacked)
                                player.MinusPsyche(game, -2, ErenYeager.AttackTitan);
                            eren.AttackTitanActiveThisRound = false;
                            eren.AttackTitanCooldown = 1;
                        }
                        else if (eren.AttackTitanCooldown > 0)
                        {
                            eren.AttackTitanCooldown--;
                        }
                    }
                    break;

                case "Shield":
                    if (player.GameCharacter.Name == DoomGuy.CharacterName)
                    {
                        player.Passives.DoomGuy.BlocksThisRound = 0;
                        var doom = player.Passives.DoomGuy;
                        if (doom.SharkShieldActiveThisRound)
                        {
                            if (doom.SharkShieldAddedPassive)
                                player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == DoomGuy.SharkPassive);
                            doom.SharkShieldActiveThisRound = false;
                            doom.SharkShieldAddedPassive = false;
                        }
                    }
                    break;

                case "Mission":
                    if (player.GameCharacter.Name == DoomGuy.CharacterName)
                    {
                        var doom = player.Passives.DoomGuy;
                        if (player.Status.IsBlock) doom.EverBlocked = true;
                        if (game.RoundNo == 10 && doom.GetActive(DoomGuy.Mission) == DoomGuy.BecomeGod
                            && !doom.EverBlocked && !doom.EverLost && !doom.BecomeGodAwarded)
                        {
                            doom.BecomeGodAwarded = true;
                            player.Status.AddBonusPoints(20, DoomGuy.BecomeGod);
                        }
                    }
                    break;

                // TheBoys — M.M.: базовая психика команды (+1 если не проиграли ни разу; -1 и психует если проиграли все бои)
                case "M.M.":
                {
                    var mmBase = player.Passives.TheBoysMM;
                    if (!player.Passives.TheBoysButcher.SuperDickActive)
                    {
                        if (mmBase.LostThisRound == 0)
                        {
                            player.GameCharacter.AddPsyche(1, "M.M.");
                            foreach (var mate in game.PlayersList)
                                if (mate.GetPlayerId() != player.GetPlayerId() && player.IsTeamMember(game, mate.GetPlayerId()))
                                    mate.GameCharacter.AddPsyche(1, "M.M.");
                        }
                        else if (mmBase.WonThisRound == 0 && !mmBase.IsCalm)
                        {
                            player.MinusPsyche(game, -1, "M.M.");
                        }
                    }

                    // Прокачка Компромат: на 8м ходу — если весь компромат верно предсказан, +5 Морали за каждый
                    if (game.RoundNo == 8 && mmBase.KompromatTargets.Count > 0
                        && !player.Passives.TheBoysButcher.SuperDickActive)
                    {
                        var allWorked = mmBase.KompromatTargets.All(tid =>
                        {
                            var tp = game.PlayersList.Find(x => x.GetPlayerId() == tid);
                            return tp != null && player.Predict.Any(pr => pr.PlayerId == tid && pr.CharacterName == tp.GameCharacter.Name);
                        });
                        if (allWorked)
                        {
                            player.GameCharacter.AddMoral(mmBase.KompromatTargets.Count * 5, "Компромат М.М.");
                            player.Status.AddInGamePersonalLogs("Компромат М.М.: весь компромат сработал! М.М. успокаивается.\n");
                        }
                    }

                    mmBase.WonThisRound = 0;
                    mmBase.LostThisRound = 0;
                    break;
                }

                case "Возвращение из мертвых":
                    // Six event actions are rounds 11-16. If enemies remain after action 16, Kratos loses.
                    if (game.IsKratosEvent && game.RoundNo >= 16
                        && game.PlayersList.Any(x => x.GameCharacter.Name != "Кратос" && !x.Passives.IsDead))
                    {
                        game.IsKratosEvent = false;
                        game.AddGlobalLogs($"У {UnknownBug.PublicName(player)}а есть тактика и он ее придерживался...");
                        await game.Phrases.KratosEventNo.SendLogSeparateWithFile(player, false, "DataBase/art/events/kratos_death.jpg", false, 15000);
                    }
                    break;

                case "Лучше с двумя, чем с адекватными":
                    foreach (var t in game.PlayersList)
                    {
                        if (t.GameCharacter.GetIntelligence() != player.GameCharacter.GetIntelligence() && t.GameCharacter.GetPsyche() != player.GameCharacter.GetPsyche()) continue;

                        var tigr = player.Passives.TigrTwoBetterList;

                        if (!tigr.FriendList.Contains(t.GetPlayerId()))//&& tigr.FriendList.Count < 4
                        {
                            tigr.FriendList.Add(t.GetPlayerId());
                            // me.Status.AddRegularPoints();
                            player.Status.AddBonusPoints(3, "Лучше с двумя, чем с адекватными");
                            game.Phrases.TigrTwoBetter.SendLog(player, false);
                        }
                    }

                    break;

                case "Безумие":
                    var madd = player.Passives.DeepListMadnessList;

                    if (madd.RoundItTriggered == game.RoundNo)
                    {
                        var regularStats = madd.MadnessList.Find(x => x.Index == 1);
                        var madStats = madd.MadnessList.Find(x => x.Index == 2);


                        var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                        var str = player.GameCharacter.GetStrength() - madStats.Str;
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;


                        player.GameCharacter.SetIntelligence(regularStats.Intel + intel, "Безумие", false);
                        player.GameCharacter.SetStrength(regularStats.Str + str, "Безумие", false);
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Безумие", false);
                        player.GameCharacter.SetPsyche(regularStats.Psyche + psy, "Безумие", false);
                        player.GameCharacter.SetAnySkillMultiplier();
                        player.Passives.DeepListMadnessList = new DeepList.Madness();

                        player.GameCharacter.AddPsyche(-1, "Безумие");
                    }

                    break;

                case "Претендент русского сервера":
                    var glebChall = player.Passives.GlebChallengerList;

                    if (glebChall.RoundItTriggered == game.RoundNo)
                    {
                        //x3 point:
                        player.Status.SetScoresToGiveAtEndOfRound(player.Status.GetScoresToGiveAtEndOfRound() * 3,
                            "Претендент русского сервера");

                        //end x3 point:
                        var regularStats = glebChall.MadnessList.Find(x => x.Index == 1);
                        var madStats = glebChall.MadnessList.Find(x => x.Index == 2);


                        var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                        var str = player.GameCharacter.GetStrength() - madStats.Str;
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;


                        player.GameCharacter.SetIntelligence(regularStats.Intel + intel,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetStrength(regularStats.Str + str,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed,
                            "Претендент русского сервера", false);
                        player.GameCharacter.SetPsyche(regularStats.Psyche + psy,
                            "Претендент русского сервера", false);
                        player.GameCharacter.AddExtraSkill(-99, "Претендент русского сервера", false);
                        player.GameCharacter.SetAnySkillMultiplier();
                        player.Passives.GlebChallengerList = new DeepList.Madness();
                    }

                    break;

                case "Хождение боком":
                    var craboRack = player.Passives.CraboRackSidewaysBooleList;

                    if (craboRack.RoundItTriggered == game.RoundNo)
                    {
                        var regularStats = craboRack.MadnessList.Find(x => x.Index == 1);
                        var madStats = craboRack.MadnessList.Find(x => x.Index == 2);
                        var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                        player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Хождение боком", false);
                        player.Passives.CraboRackSidewaysBooleList = new DeepList.Madness();
                    }

                    break;

                case "Гребанные ассассины":
                    var leCrip = player.Passives.LeCrispAssassins;

                    if (leCrip.AdditionalPsycheCurrent > 0)
                        player.GameCharacter.AddPsyche(leCrip.AdditionalPsycheCurrent * -1, "Гребанные ассассины", false);
                    if (leCrip.AdditionalPsycheForNextRound > 0)
                        player.GameCharacter.AddPsyche(leCrip.AdditionalPsycheForNextRound, "Гребанные ассассины");

                    leCrip.AdditionalPsycheCurrent = leCrip.AdditionalPsycheForNextRound;
                    leCrip.AdditionalPsycheForNextRound = 0;
                    break;

                case "Импакт":
                    var leImpact = player.Passives.LeCrispImpact;

                    if (leImpact.IsLost)
                    {
                        leImpact.ImpactTimes = 0;
                    }
                    else
                    {
                        leImpact.ImpactTimes += 1;
                        player.Status.AddBonusPoints(1, "Импакт");
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill();
                        game.Phrases.LeCrispImpactPhrase.SendLog(player, false, $"(x{leImpact.ImpactTimes}) ");
                    }

                    leImpact.IsLost = false;
                    break;

                case "Великий Комментатор":
                    if (game.RoundNo is >= 3 and <= 6)
                    {
                        if (_rand.Luck(1, 5))
                        {
                            var tolyaTalked = player.Passives.TolyaTalked;
                            if (tolyaTalked.PlayerHeTalkedAbout.Count < 2)
                            {
                                var randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];

                                // A successful Shen leap temporarily overrides Most wanted for
                                // current-round random revelations too.
                                var shenMagnet = Salldorum.FindRandomTargetMagnet(game, player);
                                var rickMw = RickSanchez.FindMostWantedHolder(
                                    game.PlayersList, player);
                                if (shenMagnet != null
                                    && !tolyaTalked.PlayerHeTalkedAbout.Contains(shenMagnet.GetPlayerId()))
                                    randomPlayer = shenMagnet;
                                else if (rickMw != null && rickMw.GetPlayerId() != player.GetPlayerId()
                                                        && !tolyaTalked.PlayerHeTalkedAbout.Contains(rickMw.GetPlayerId()))
                                    randomPlayer = rickMw;

                                while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()))
                                    randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];


                                if (randomPlayer.GetPlayerId() == player.GetPlayerId())
                                    do
                                    {
                                        randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];
                                    } while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()));

                                if (randomPlayer.GetPlayerId() == player.GetPlayerId())
                                    do
                                    {
                                        randomPlayer = game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)];
                                    } while (tolyaTalked.PlayerHeTalkedAbout.Contains(randomPlayer.GetPlayerId()));


                                tolyaTalked.PlayerHeTalkedAbout.Add(randomPlayer.GetPlayerId());

                                // Выдуманный персонаж: Монстра нельзя просветить
                                if (randomPlayer.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж")
                                    || UnknownBug.Is(randomPlayer) || Sakura.Is(randomPlayer))
                                {
                                    var tolyaFailSnippet = $"Толя попытался что-то разузнать про {randomPlayer.DiscordUsername}, но не удалось просветить";
                                    game.AddGlobalLogs(tolyaFailSnippet);
                                }
                                else
                                {
                                    var tolyaLogSnippet = $"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {UnknownBug.PublicName(randomPlayer)}";
                                    game.AddGlobalLogs(tolyaLogSnippet);
                                    game.KiraHiddenLogSnippets.Add(tolyaLogSnippet);

                                    // Auto-set prediction for all players who don't already have one for this target
                                    foreach (var p in game.PlayersList)
                                    {
                                        if (p.Passives.IsDead) continue;
                                        if (p.GetPlayerId() == randomPlayer.GetPlayerId()) continue;
                                        if (p.IsProMode && p.GetPlayerId() != player.GetPlayerId()) continue;
                                        var existingPred = p.Predict.Find(pr => pr.PlayerId == randomPlayer.GetPlayerId());
                                        if (existingPred == null)
                                            p.Predict.Add(new PredictClass(randomPlayer.GameCharacter.Name, randomPlayer.GetPlayerId()));
                                    }
                                    if (!game.PinkWardRevealedPlayerIds.Contains(randomPlayer.GetPlayerId()))
                                        game.PinkWardRevealedPlayerIds.Add(randomPlayer.GetPlayerId());
                                    Homelander.RecordReveal(game, player, randomPlayer);
                                }
                            }
                        }
                    }

                    break;

                case "Раммус мейн":
                    var tolya = player.Passives.TolyaRammusTimes;
                    if (tolya != null)
                    {
                        var rammusCount = 0;
                        switch (tolya.FriendList.Count)
                        {
                            case 1:
                                game.Phrases.TolyaRammusPhrase.SendLog(player, false);
                                rammusCount = 1;
                                break;
                            case 2:
                                game.Phrases.TolyaRammus2Phrase.SendLog(player, false);
                                rammusCount = 2;
                                break;
                            case 3:
                                game.Phrases.TolyaRammus3Phrase.SendLog(player, false);
                                rammusCount = 3;
                                break;
                            case 4:
                                game.Phrases.TolyaRammus4Phrase.SendLog(player, false);
                                rammusCount = 4;
                                break;
                            case 5:
                                game.Phrases.TolyaRammus5Phrase.SendLog(player, false);
                                rammusCount = 5;
                                break;
                        }

                        if (rammusCount > 0)
                        {
                            player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                            player.GameCharacter.AddMoral(rammusCount * rammusCount, "Раммус мейн");
                        }

                        tolya.FriendList.Clear();
                    }

                    break;

                case "Привет со дна":
                    var extraPoints = game.SkipPlayersThisRound + game.PlayersList.Count(p => p.Status.IsBlock);
                    if (extraPoints > 0)
                        player.Status.AddBonusPoints(extraPoints, "Привет со дна");
                    break;

                case "Обучение":
                    //There is a second part in "GetLvlUp()" !!!!!!!!!! <<<<<<<<<<
                    var siri = player.Passives.SirinoksTraining;

                    if (siri != null && siri.Training.Count >= 1)
                    {
                        var stats = siri.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                        switch (stats.StatIndex)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(1, "Обучение");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(1, "Обучение");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(1, "Обучение");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(1, "Обучение");
                                break;
                        }

                        Sirinoks.TryCompleteTraining(player);
                    }

                    break;

                case "Одиночество":
                    var hard = player.Passives.HardKittyLoneliness;
                    if (hard != null) hard.Activated = false;
                    break;

                case "3-0 обоссан":
                    var tigrThreeZero = player.Passives.TigrThreeZeroList;
                    /*
                     if 0:0 and win-lost: lost => win
                     if 1:0 and win-lost: lost => win
                     if 2:0 and win-lost: win => lost
                    */
                    foreach (var lost in tigrThreeZero.WhoToLostThisRound.ToList())
                    {
                        var threeZero = tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == lost);
                        if (threeZero == null)
                        {
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                            continue;
                        }

                        if (tigrThreeZero.WhoToWinThisRound.Contains(lost))
                        {
                            if (threeZero.WinsSeries >= 2)
                            {
                                // This loss is paired with a simultaneous win that is allowed
                                // to cross 2 → 3. Consume every matching loss now so the final
                                // cleanup cannot erase the triggered series.
                                tigrThreeZero.WhoToLostThisRound.RemoveAll(id => id == lost);
                                continue;
                            }

                            threeZero.WinsSeries = 0;
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                        }
                        else
                        {
                            threeZero.WinsSeries = 0;
                            tigrThreeZero.WhoToLostThisRound.Remove(lost);
                        }
                    }

                    foreach (var win in tigrThreeZero.WhoToWinThisRound.ToList())
                    {
                        var threeZero = tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == win);
                        if (threeZero == null)
                        {
                            tigrThreeZero.FriendList.Add(new Tigr.ThreeZeroSubClass(win));
                            continue;
                        }
                        threeZero.WinsSeries++;


                        if (threeZero.WinsSeries < 3 || !threeZero.IsUnique) 
                            continue;

                        player.Status.AddRegularPoints(3, "3-0 обоссан");
                        player.GameCharacter.AddExtraSkill(30, "3-0 обоссан");
                        player.GameCharacter.AddMoral(3, "3-0 обоссан");

                        var enemyAcc = game.PlayersList.Find(x => x.GetPlayerId() == win);
                        if (enemyAcc == null) continue;

                        enemyAcc.GameCharacter.AddIntelligence(-1, "3-0 обоссан");
                        enemyAcc.MinusPsyche(game, -1, "3-0 обоссан");

                        game.Phrases.TigrThreeZero.SendLog(player, false);

                        threeZero.IsUnique = false;
                    }

                    foreach (var threeZero in tigrThreeZero.WhoToLostThisRound.ToList().Select(lost => tigrThreeZero.FriendList.Find(x => x.EnemyPlayerId == lost)))
                    {
                        if (threeZero != null)
                            threeZero.WinsSeries = 0;
                    }
                    
                    tigrThreeZero.WhoToLostThisRound.Clear();
                    tigrThreeZero.WhoToWinThisRound.Clear();
                    break;
                
                case "Доебаться":
                    var hardKitty = player.Passives.HardKittyDoebatsya;
                    for (var i = hardKitty.EnemyPlayersLostTo.Count - 1; i >= 0; i--)
                    {
                        var found = hardKitty.LostSeriesCurrent.Find(x =>
                            x.EnemyPlayerId == hardKitty.EnemyPlayersLostTo[i]);
                        if (found != null)
                        {
                            found.Series = 0;
                            game.Phrases.HardKittyDoebatsyaAnswerPhrase.SendLog(player, false);
                        }

                        hardKitty.EnemyPlayersLostTo.RemoveAt(i);
                    }

                    break;

                case "Им это не понравится":
                    if (game.RoundNo is 2 or 4 or 6 or 8)
                    {
                        var spartan = player.Passives.SpartanMark;
                        spartan.FriendList.Clear();

                        Guid enemy1;
                        Guid enemy2;

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy1 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Злой Школьник" && game.RoundNo < 4)
                                enemy1 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Вампур" && game.RoundNo >= 4)
                                enemy1 = player.GetPlayerId();
                            if (UnknownBug.Is(game.PlayersList[randIndex]))
                                enemy1 = player.GetPlayerId();
                        } while (enemy1 == player.GetPlayerId());

                        // Most wanted: force Rick as enemy1
                        var rickMw2 = RickSanchez.FindMostWantedHolder(game.PlayersList, player);
                        if (rickMw2 != null)
                            enemy1 = rickMw2.GetPlayerId();

                        do
                        {
                            var randIndex = _rand.Random(0, game.PlayersList.Count - 1);
                            enemy2 = game.PlayersList[randIndex].GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Глеб" or "mylorik" or
                                "Загадочный Спартанец в маске")
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Злой Школьник" && game.RoundNo < 4)
                                enemy2 = player.GetPlayerId();
                            if (game.PlayersList[randIndex].GameCharacter.Name is "Вампур" && game.RoundNo >= 4)
                                enemy2 = player.GetPlayerId();
                            if (UnknownBug.Is(game.PlayersList[randIndex]))
                                enemy2 = player.GetPlayerId();
                            if (enemy2 == enemy1)
                                enemy2 = player.GetPlayerId();
                        } while (enemy2 == player.GetPlayerId());


                        spartan.FriendList.Add(enemy2);
                        spartan.FriendList.Add(enemy1);
                    }

                    break;

                case "Дерзкая школота":
                    if (!player.Status.IsSkip)
                    {
                        player.GameCharacter.AddExtraSkill(-20, "Дерзкая школота");

                        var randStat1 = _rand.Random(1, 4);
                        var randStat2 = _rand.Random(1, 4);
                        switch (randStat1)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(-1, "Дерзкая школота");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(-1, "Дерзкая школота");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(-1, "Дерзкая школота");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(-1, "Дерзкая школота");
                                break;
                        }

                        switch (randStat2)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(-1, "Дерзкая школота");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(-1, "Дерзкая школота");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(-1, "Дерзкая школота");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(-1, "Дерзкая школота");
                                break;
                        }
                    }

                    break;

                case "Много выебывается":

                    var noAttack = true;

                    foreach (var target in game.PlayersList)
                    {
                        if (target.GetPlayerId() == player.GetPlayerId()) continue;
                        if (target.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId()))
                            noAttack = false;
                    }

                    if (noAttack)
                    {
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFuckingNoAttack.SendLog(player, true);
                    }

                    break;

                case "Гематофагия":
                    var vampyr = player.Passives.VampyrHematophagiaList;


                    for (var i = vampyr.HematophagiaAddEndofRound.Count - 1; i >= 0; i--)
                    {
                        var hematophagia = vampyr.HematophagiaAddEndofRound[i];
                        switch (hematophagia.StatIndex)
                        {
                            case 1:
                                player.GameCharacter.AddIntelligence(2, "Гематофагия");
                                break;
                            case 2:
                                player.GameCharacter.AddStrength(2, "Гематофагия");
                                break;
                            case 3:
                                player.GameCharacter.AddSpeed(2, "Гематофагия");
                                break;
                            case 4:
                                player.GameCharacter.AddPsyche(2, "Гематофагия");
                                break;
                        }

                        vampyr.HematophagiaCurrent.Add(new Vampyr.HematophagiaSubClass(hematophagia.StatIndex, hematophagia.EnemyId));
                        vampyr.HematophagiaAddEndofRound.RemoveAt(i);
                    }

                    for (var i = vampyr.HematophagiaRemoveEndofRound.Count - 1; i >= 0; i--)
                    {
                        var hematophagia = vampyr.HematophagiaRemoveEndofRound[i];
                        var activeBite = vampyr.HematophagiaCurrent.Find(x =>
                            x.EnemyId == hematophagia.EnemyId);

                        if (activeBite != null)
                        {
                            vampyr.HematophagiaCurrent.Remove(activeBite);

                            switch (activeBite.StatIndex)
                            {
                                case 1:
                                    player.GameCharacter.AddIntelligence(-2, "СОсиновый кол");
                                    break;
                                case 2:
                                    player.GameCharacter.AddStrength(-2, "СОсиновый кол");
                                    break;
                                case 3:
                                    player.GameCharacter.AddSpeed(-2, "СОсиновый кол");
                                    break;
                                case 4:
                                    player.GameCharacter.AddPsyche(-2, "СОсиновый кол");
                                    break;
                            }

                            player.Status.AddRegularPoints(-1, "СОсиновый кол");
                        }

                        vampyr.HematophagiaRemoveEndofRound.RemoveAt(i);
                    }

                    break;

                case "Неприметность":
                    // Recalculate top 2 serious targets every round based on current combat power
                    var saitamaEndUnnoticed = player.Passives.SaitamaUnnoticed;
                    saitamaEndUnnoticed.SeriousTargets = game.PlayersList
                        .Where(x => x.GetPlayerId() != player.GetPlayerId())
                        .OrderByDescending(x => x.GameCharacter.GetSkill())
                        .Take(2)
                        .Select(x => x.GetPlayerId())
                        .ToList();
                    break;

                case "Ищет достойного противника":

                    break;

                case "Гигантские бобы":
                    // Ingredient assignment now happens in GetLvlUp (GameReactions.cs)
                    break;

                case "Портальная пушка":
                    var gunEnd = player.Passives.RickPortalGun;
                    if (!gunEnd.Invented && player.GameCharacter.GetIntelligence() >= 30)
                    {
                        gunEnd.Invented = true;
                        gunEnd.Charges++;
                        game.Phrases.RickPortalGunInvented.SendLog(player, false);
                    }
                    // Portal gun x2 multiplier: double round score when portal was fired
                    // (disabled by Подсчет and other multiplier-disabling passives)
                    if (gunEnd.FiredThisRound)
                    {
                        var isMultiplierDisabled = game.PlayersList.Any(p2 =>
                            p2.GameCharacter.Passive.Any(pas => pas.PassiveName == "Подсчет") &&
                            p2.Passives.TolyaCount.TargetList.Any(x =>
                                x.RoundNumber == game.RoundNo - 1 && x.Target == player.GetPlayerId()));
                        if (!isMultiplierDisabled)
                        {
                            var currentScore = player.Status.GetScoresToGiveAtEndOfRound();
                            player.Status.SetScoresToGiveAtEndOfRound(currentScore * 2, "Портальная пушка");
                            player.Status.AddInGamePersonalLogs("Портальная пушка: Очки из двух мульти-вселенных! x2\n");
                        }
                        gunEnd.FiredThisRound = false;
                    }
                    break;

                case "Огурчик Рик":
                    var pickleEnd = player.Passives.RickPickle;
                    if (pickleEnd.PickleTurnsRemaining > 0)
                    {
                        pickleEnd.PickleTurnsRemaining--;
                        if (pickleEnd.PickleTurnsRemaining == 0 && !pickleEnd.WasAttackedAsPickle)
                            pickleEnd.PenaltyTurnsRemaining = 1;
                    }
                    break;

                case "Тетрадь смерти":
                    var deathNote = player.Passives.KiraDeathNote;
                    if (deathNote.CurrentRoundTarget != Guid.Empty)
                    {
                        var dnTarget = game.PlayersList.Find(x => x.GetPlayerId() == deathNote.CurrentRoundTarget);
                        if (dnTarget != null)
                        {
                            // A notebook target can be used only once. This lock is independent of the
                            // outcome: an earlier death, Jon's rebirth or Itachi's Izanagi cannot reopen it.
                            if (!deathNote.FailedTargets.Contains(dnTarget.GetPlayerId()))
                                deathNote.FailedTargets.Add(dnTarget.GetPlayerId());

                            if (dnTarget.Passives.IsDead)
                            {
                                player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                                    "Тетрадь смерти",
                                    "Цель уже мертва. Тетрадь не может убить её повторно.",
                                    "Death Note",
                                    "The target is already dead. The notebook cannot kill them twice.") + "\n");
                                deathNote.CurrentRoundTarget = Guid.Empty;
                                deathNote.CurrentRoundName = "";
                                break;
                            }

                            if (Sakura.Is(dnTarget))
                            {
                                player.Status.AddInGamePersonalLogs(
                                    "Тетрадь смерти: у этой цели нет доступного имени.\n");
                                deathNote.CurrentRoundTarget = Guid.Empty;
                                deathNote.CurrentRoundName = "";
                                break;
                            }

                            // 15% chance Kira writes on glass instead of the Death Note
                            if (_rand.Luck(15))
                            {
                                player.Status.AddInGamePersonalLogs("Рюк: ЛАЙТ, ТЫ ПИШЕШЬ НА СТЕКЛЕ\n");
                                deathNote.CurrentRoundTarget = Guid.Empty;
                                deathNote.CurrentRoundName = "";
                                break;
                            }

                            var writtenName = deathNote.CurrentRoundName.Trim();
                            var actualName = dnTarget.GameCharacter.Name;
                            if (!UnknownBug.Is(dnTarget)
                                && string.Equals(writtenName, actualName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Goblins and Madara are immune to kill effects.
                                var targetIsKillImmune =
                                    dnTarget.GameCharacter.Name == "Стая Гоблинов" || Madara.IsMadara(dnTarget);
                                var deathPreventedByIzanagi =
                                    !targetIsKillImmune && Itachi.TryPreventDeath(dnTarget, game);
                                var causedDeath = !targetIsKillImmune && !deathPreventedByIzanagi;
                                deathNote.Entries.Add(new Characters.Kira.DeathNoteEntry
                                {
                                    TargetPlayerId = dnTarget.GetPlayerId(),
                                    WrittenName = writtenName,
                                    RoundWritten = game.RoundNo,
                                    WasCorrect = true,
                                    CausedDeath = causedDeath,
                                });

                                if (causedDeath)
                                {
                                    if (!JonSnow.TryEndWatch(dnTarget, game, "Kira"))
                                    {
                                        dnTarget.Passives.IsDead = true;
                                        dnTarget.Passives.DeathSource = "Kira";
                                    }
                                    dnTarget.Passives.AchievementTracker.WasKilledByKira = true;
                                    if (dnTarget.GameCharacter.Name == "Кира")
                                        player.Passives.AchievementTracker.SurvivedKiraAttempt = false; // killer gets "kill_a_god" tracked at game end
                                    // Монстр без имени: +1 regular point per real death
                                    foreach (var mp in game.PlayersList.Where(x => !x.Passives.IsDead
                                                 && x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                                    {
                                        mp.Status.AddRegularPoints(1, "Монстр");
                                        game.Phrases.MonsterDeath.SendLog(mp, false);
                                    }
                                    var isL = dnTarget.GetPlayerId() == Salldorum.ResolveRandomTargetId(
                                        game, player, player.Passives.KiraL.LPlayerId);
                                    var pts = isL ? 4 : 2;
                                    player.Status.AddRegularPoints(pts, "Тетрадь смерти");
                                    player.GameCharacter.AddIntelligence(-1, "Гений");
                                    var deathLog = $"{dnTarget.DiscordUsername} умер от сердечного приступа...";
                                    game.AddGlobalLogs(deathLog);
                                    game.Phrases.KiraDeathNoteKill.SendLog(player, true);

                                    // Kira killed L — special dialogue
                                    if (isL)
                                    {
                                        game.AddGlobalLogs(
                                            $"В связи с загадочными обстоятельствами, известный детектив по кличке **L** мертв. Его настоящее имя было {dnTarget.DiscordUsername}\n" +
                                            "**Kira:** Ну и что LLLLLLL???!?! КТО ТЕПЕРЬ... КТО ТЕПЕРЬ... эм... КТО ИЗ НАС ПОБЕДИЛ???!?! ХАХХХАХАХАХ! ГАВ ГАВ ГАВ");
                                    }
                                }
                                else if (deathPreventedByIzanagi)
                                {
                                    player.Status.AddInGamePersonalLogs(
                                        "Тетрадь смерти: имя было верным, но Изанаги предотвратил смерть. Очки за убийство не начислены.\n");
                                }
                                else
                                {
                                    player.Status.AddInGamePersonalLogs(
                                        "Тетрадь смерти: имя было верным, но цель невосприимчива к убийству.\n");
                                }
                            }
                            else
                            {
                                deathNote.Entries.Add(new Characters.Kira.DeathNoteEntry
                                {
                                    TargetPlayerId = dnTarget.GetPlayerId(),
                                    WrittenName = writtenName,
                                    RoundWritten = game.RoundNo,
                                    WasCorrect = false,
                                    CausedDeath = false,
                                });
                                game.Phrases.KiraDeathNoteFailed.SendLog(player, false);
                            }
                        }
                        deathNote.CurrentRoundTarget = Guid.Empty;
                        deathNote.CurrentRoundName = "";
                    }
                    break;

                case "L":
                    var kiraL = player.Passives.KiraL;
                    if (kiraL.LPlayerId != Guid.Empty && !kiraL.IsArrested)
                    {
                        // Check if Kira and L fought this round (either lost to the other)
                        var activeL = Salldorum.ResolveRandomTargetId(game, player, kiraL.LPlayerId);
                        var lPlayer = game.PlayersList.Find(x => x.GetPlayerId() == activeL);
                        if (lPlayer != null)
                        {
                            var kiraLostToL = player.Status.WhoToLostEveryRound.Any(y =>
                                y.RoundNo == game.RoundNo && y.EnemyId == activeL);
                            var lLostToKira = lPlayer.Status.WhoToLostEveryRound.Any(y => y.RoundNo == game.RoundNo && y.EnemyId == player.GetPlayerId());
                            if (!kiraLostToL && !lLostToKira)
                            {
                                player.GameCharacter.AddMoral(5, "L");
                                game.Phrases.KiraLNoFight.SendLog(player, false);
                            }
                        }
                    }
                    break;

                // Глаза Итачи: steal points from active target + charge
                case "Глаза Итачи":
                    var tsukuyomi = player.Passives.ItachiTsukuyomi;

                    // Steal points from active target
                    if (tsukuyomi.TsukuyomiActiveTarget != Guid.Empty)
                    {
                        var tsukuyomiVictim = game.PlayersList.Find(x => x.GetPlayerId() == tsukuyomi.TsukuyomiActiveTarget);
                        if (tsukuyomiVictim != null
                            && !UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, tsukuyomiVictim)))
                        {
                            // Stolen regular points scale by the round multiplier (×1/×2/×4) of the
                            // round they were stolen on (HandleEndOfRound runs before RoundNo++).
                            // Bonus points are flat everywhere, so they stay unscaled.
                            var roundMultiplier = game.RoundNo switch { <= 4 => 1, <= 9 => 2, _ => 4 };
                            var stolenRegularPoints = GordonFreeman.Is(tsukuyomiVictim)
                                ? GordonFreeman.ProjectRegularSettlement(tsukuyomiVictim, game)
                                : tsukuyomiVictim.Status.GetScoresToGiveAtEndOfRound() * roundMultiplier;
                            var stolenPoints = stolenRegularPoints
                                             + tsukuyomiVictim.Status.GetBonusPointsEarnedThisRound();
                            var scoreVictim = Naruto.ResolveScoreSuccessor(game, tsukuyomiVictim);
                            if (stolenPoints > 0
                                && Homelander.CanTransferFrom(scoreVictim, "Глаза Итачи"))
                            {
                                if (stolenRegularPoints > 0)
                                    GordonFreeman.MarkCurrentAttemptStolenByItachi(
                                        tsukuyomiVictim, player, game);
                                player.Status.AddBonusPoints(stolenPoints, "Глаза Итачи");
                                tsukuyomi.TotalStolenPoints += stolenPoints;
                                if (!tsukuyomi.StolenFromPlayers.ContainsKey(tsukuyomi.TsukuyomiActiveTarget))
                                    tsukuyomi.StolenFromPlayers[tsukuyomi.TsukuyomiActiveTarget] = 0;
                                tsukuyomi.StolenFromPlayers[tsukuyomi.TsukuyomiActiveTarget] += stolenPoints;
                                game.Phrases.ItachiTsukuyomiSteal.SendLog(player, false);
                                if (Madara.IsMadara(tsukuyomiVictim))
                                    game.Phrases.MadaraItachiStole.SendLog(tsukuyomiVictim, false, isRandomOrder: false);
                            }
                        }
                        tsukuyomi.TsukuyomiActiveTarget = Guid.Empty;
                        if (tsukuyomi.TsukuyomiTargetThisRound == Guid.Empty)
                            game.Phrases.ItachiTsukuyomiEnd.SendLog(player, false);
                    }

                    // Charge counter (cap at 2)
                    if (tsukuyomi.ChargeCounter < 2)
                    {
                        tsukuyomi.ChargeCounter++;
                        if (tsukuyomi.ChargeCounter >= 2)
                            game.Phrases.ItachiTsukuyomiCharge.SendLog(player, false);
                    }
                    break;

                case "Выгодная сделка":
                    var deals = player.Passives.SellerProfitableDealsThisRound;
                    if (deals > 0)
                    {
                        player.Status.AddBonusPoints(deals, "Выгодная сделка");
                        if (deals >= 3)
                            game.Phrases.SellerProfitBig.SendLog(player, false);
                        else
                            game.Phrases.SellerProfit.SendLog(player, false);
                    }
                    player.Passives.SellerProfitableDealsThisRound = 0;
                    break;

                case "Взгляд в будущее":
                    if (player.Passives.DopaVision.Cooldown > 0) break;

                    // Dopa's two Макро selections; a Block records his OWN id (self-target),
                    // so a participant may be Dopa himself.
                    var visionParticipants = player.Status.WhoToAttackThisTurn.Distinct().ToList();
                    if (visionParticipants.Count < 2) break;

                    var visionAId = visionParticipants[0];
                    var visionBId = visionParticipants[1];
                    var visionDopaId = player.GetPlayerId();
                    var visionA = game.PlayersList.Find(x => x.GetPlayerId() == visionAId);
                    var visionB = game.PlayersList.Find(x => x.GetPlayerId() == visionBId);

                    // Count every predicted attack by AIM — a target's Block/Skip is irrelevant
                    // (the promise is about who attacks, not whether the fight resolves). Repeated
                    // queue entries are separate procs, e.g. twelve Geralt contract fights pay twelve
                    // times. Dopa's own attacks never count; when he blocked, only the other side
                    // attacking him (his self-ID participant) can score.
                    var visionCount = 0;
                    if (visionAId != visionDopaId && visionA != null)
                        visionCount += visionA.Status.WhoToAttackThisTurn.Count(targetId => targetId == visionBId);
                    if (visionBId != visionDopaId && visionB != null)
                        visionCount += visionB.Status.WhoToAttackThisTurn.Count(targetId => targetId == visionAId);

                    if (visionCount > 0)
                    {
                        var farmActive = player.GameCharacter.Passive.Any(x => x.PassiveName == "Фарм")
                            && (player.GameCharacter.Name != Dopa.CharacterName
                                || player.Passives.DopaMetaChoice.ChosenTactic == "Фарм");
                        var pointsPerProc = farmActive ? 4 : 2;
                        for (var proc = 0; proc < visionCount; proc++)
                        {
                            player.Status.AddRegularPoints(pointsPerProc, "Взгляд в будущее");
                            player.GameCharacter.AddExtraSkill(50, "Взгляд в будущее");
                            player.Passives.AchievementTracker.DopaVisionProcs++;
                        }
                        player.Passives.DopaVision.Cooldown = 1;
                        game.Phrases.DopaVisionProc.SendLog(player, false);
                    }
                    break;

                case "Великий летописец":
                    // 1. See others' logs
                    foreach (var other in game.PlayersList.Where(p => p.GetPlayerId() != player.GetPlayerId()))
                    {
                        var otherLogs = other.Status.GetInGamePersonalLogs();
                        if (!string.IsNullOrEmpty(otherLogs))
                            player.Status.AddInGamePersonalLogs($"[{other.DiscordUsername}]: {otherLogs}\n");
                    }

                    // 2. 20% chance to corrupt a random enemy's logs
                    if (_rand.Random(0, 99) < 20)
                    {
                        var enemies = game.PlayersList
                            .Where(p => p.GetPlayerId() != player.GetPlayerId()
                                     && !string.IsNullOrEmpty(p.Status.GetInGamePersonalLogs()))
                            .ToList();
                        if (enemies.Count > 0)
                        {
                            var victim = enemies[_rand.Random(0, enemies.Count - 1)];
                            var lines = victim.Status.GetInGamePersonalLogs().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            if (lines.Length > 0)
                            {
                                var idx = _rand.Random(0, lines.Length - 1);
                                if (_rand.Random(0, 99) < 50)
                                    lines[idx] = "██████████████████";
                                else
                                    lines[idx] = "Салдорум был здесь...";
                                victim.Status.SetInGamePersonalLogs(string.Join('\n', lines) + '\n');
                                player.Passives.SaldorumCorruptionCount++;
                            }
                        }
                    }
                    break;

                // Napoleon — Вступить в союз: target info now shown via ⚔️ icon in leaderboard
                case "Вступить в союз":
                    break;

                // Таинственный Суппорт — "Protect": block gives +1 justice
                case "Protect":
                    if (player.Status.IsBlock)
                    {
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                        game.Phrases.SupportProtect.SendLog(player, false);
                    }
                    break;

                // Toxic Mate — "Tilted": +50 only when the whole round had ZERO battles (everyone
                // skipped/blocked/no-showed → no fight was calculated). No per-skip bonus (finding M8).
                // game.AnyFightThisRound is set the moment any fight resolves; IsWonThisCalculation is
                // NOT usable here — ResetFight clears it before HandleEndOfRound runs.
                case "Tilted":
                    if (!game.AnyFightThisRound)
                    {
                        player.Status.AddBonusPoints(50, "Tilted");
                        game.AddGlobalLogs("__**OPEN MID!** +20 **очков**__");
                    }
                    break;

                case "Отличный рудник":
                    // Mine income based on pre-sort position (so goblins get income even if they move away after sort)
                    // Skip round 1 — initial placement is randomized
                    var gobMinePlaceNow = player.Status.GetPlaceAtLeaderBoard();
                    if (game.RoundNo > 1 && gobMinePlaceNow is 1 or 2 or 6)
                    {
                        var gobMinePopEor = player.Passives.GoblinPopulation;
                        if (gobMinePopEor.Workers > 0)
                        {
                            player.Status.AddBonusPoints(gobMinePopEor.Workers, "Отличный рудник");
                            game.Phrases.GoblinMine.SendLog(player, false);
                            //player.Status.AddInGamePersonalLogs($"Рудник: +{gobMinePopEor.Workers} очков от трудяг!\n");
                        }
                    }
                    break;

                case "Гоблины тупые, но не идиоты":
                    var gobZigIntent = player.Passives.GoblinZiggurat;
                    if (player.Status.IsBlock)
                    {
                        // Forced/legacy block paths may not have captured the button position.
                        if (!gobZigIntent.WantsToBuild || gobZigIntent.PendingBuildPosition <= 0)
                        {
                            gobZigIntent.WantsToBuild = true;
                            gobZigIntent.PendingBuildPosition = player.Status.GetPlaceAtLeaderBoard();
                        }
                        var learnedPassive = ResolveGoblinZigguratBuild(
                            player, game, gobZigIntent.PendingBuildPosition);
                        if (learnedPassive != null)
                            await _gameUpdateMess.UpdateCharacterMessage(player);
                    }
                    gobZigIntent.WantsToBuild = false;
                    gobZigIntent.PendingBuildPosition = 0;
                    break;

                // Котики — Штормяк: reset taunt target at end of round
                case "Штормяк":
                    // Only reset for Котики's own passive (not transferred Storm cat)
                    if (player.Passives.KotikiCatOwnerId == Guid.Empty)
                        player.Passives.KotikiStorm.CurrentTauntTarget = Guid.Empty;
                    break;

                // Котики — Кошачья засада: track rounds and cooldowns
                case "Кошачья засада":
                    var ambushEor = player.Passives.KotikiAmbush;
                    if (ambushEor.MinkaOnPlayer != Guid.Empty)
                        ambushEor.MinkaRoundsOnEnemy++;
                    if (ambushEor.MinkaCooldown > 0)
                        ambushEor.MinkaCooldown--;
                    if (ambushEor.StormCooldown > 0)
                        ambushEor.StormCooldown--;
                    break;

                case "Francie":
                    if (player.Passives.TheBoysButcher.SuperDickActive) break;
                    var francieEor = player.Passives.TheBoysFrancie;
                    if (game.RoundNo is 3 or 6 or 9
                        && francieEor.OrderTarget != Guid.Empty)
                    {
                        francieEor.OrdersFailed++;
                        player.Status.AddRegularPoints(-1, "Заказ Француза");
                        game.Phrases.TheBoysOrderFailed.SendLog(player, false);
                        francieEor.OrderTarget = Guid.Empty;
                        francieEor.OrderRoundsLeft = 0;
                    }
                    break;

                // Котики — Рандомное поведение: per-round cleanup
                case "Рандомное поведение":
                {
                    var rbOwnerEor = player.Passives.KotikiCatOwnerId != Guid.Empty
                        ? game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.KotikiCatOwnerId)
                        : player;
                    if (rbOwnerEor == null) rbOwnerEor = player;
                    var rbEor = rbOwnerEor.Passives.KotikiRandomBehavior;
                    rbEor.SelectedTrickThisRound = 0;
                    rbEor.FightTargetAttackerId = Guid.Empty;
                    rbEor.FightTargetDefenderId = Guid.Empty;
                    rbEor.FightProcessed = false;
                    break;
                }

                // Монстр без имени — Пейзаж конца света: round 10 apocalypse
                case "Пейзаж конца света":
                    if (game.RoundNo == 10)
                    {
                        var deadNames = new List<string>();
                        foreach (var pawn in game.PlayersList.Where(x =>
                            x.Passives.IsJohanPawn &&
                            x.Passives.JohanPawnOwnerId == player.GetPlayerId() &&
                            !x.Passives.IsDead &&
                            !UnknownBug.Is(x)))
                        {
                            // Pawns who blocked or skipped survive
                            if (pawn.Status.IsBlock || pawn.Status.IsSkip || Madara.IsMadara(pawn)) continue;
                            if (Itachi.TryPreventDeath(pawn, game)) continue;
                            if (!JonSnow.TryEndWatch(pawn, game, "Monster"))
                            {
                                pawn.Passives.IsDead = true;
                                pawn.Passives.DeathSource = "Monster";
                            }
                            player.Passives.AchievementTracker.MonsterPawnExecutions++;
                            deadNames.Add(UnknownBug.PublicName(pawn));
                            player.Status.AddRegularPoints(1, "Монстр");
                        }

                        if (deadNames.Count > 0)
                        {
                            game.AddGlobalLogs($"{string.Join(", ", deadNames)} убили друг друга. Их тела были найдены в небольшом немецком городке.");
                            game.Phrases.MonsterApocalypse.SendLog(player, false);
                        }

                        // Non-pawns who fought Monster this round get a reward
                        foreach (var fighter in game.PlayersList.Where(x =>
                            !Naruto.IsDispersedClone(x) &&
                            !x.Passives.IsJohanPawn &&
                            x.GetPlayerId() != player.GetPlayerId() &&
                            x.Status.WhoToAttackThisTurn.Contains(player.GetPlayerId())))
                        {
                            fighter.Status.AddRegularPoints(7, "Пейзаж конца света");
                            fighter.Status.AddBonusPoints(10, "Пейзаж конца света");
                            fighter.Passives.AchievementTracker.WitnessedMonsterApocalypse = true;
                            game.AddGlobalLogs("Я увидел... Зверя... с семью головами и десятью рогами! Я выстрелил!");
                        }
                    }
                    break;

                // Геральт — Медитация: oil, witcher senses, lambert, no-moral phrase
                case "Медитация":
                    if (player.GameCharacter.Name == "Геральт" && player.Status.IsBlock)
                    {
                        var geraltOilEor = player.Passives.GeraltOil;
                        var geraltMedEor = player.Passives.GeraltMeditation;
                        var geraltContractsEor = player.Passives.GeraltContracts;

                        // Oil: apply oil
                        geraltOilEor.IsOilApplied = true;

                        // Witcher Senses: reveal enemy with most contracts (not yet revealed)
                        var unrevealed = game.PlayersList.Where(x =>
                            x.GetPlayerId() != player.GetPlayerId() &&
                            !x.Passives.IsDead &&
                            x.Passives.GeraltMonsterType != null &&
                            !geraltMedEor.RevealedEnemies.Contains(x.GetPlayerId())).ToList();

                        if (unrevealed.Count > 0)
                        {
                            // Prefer enemy with most contracts of their type
                            var hintTarget = unrevealed
                                .OrderByDescending(x => geraltContractsEor.GetCount(x.Passives.GeraltMonsterType!.Value))
                                .ThenBy(_ => _rand.Random(0, 100))
                                .First();

                            geraltMedEor.RevealedEnemies.Add(hintTarget.GetPlayerId());

                            BilingualGeneratedText hint;
                            // For human players, generate one replay-safe bilingual hint via AI.
                            if (!player.IsBot() && !UnknownBug.Is(hintTarget))
                            {
                                var monsterTypeName = Geralt.GetMonsterTypeName(hintTarget.Passives.GeraltMonsterType!.Value);
                                try
                                {
                                    hint = _haikuService.GenerateWitcherHintPairAsync(
                                        hintTarget.GameCharacter.Name,
                                        hintTarget.GameCharacter.Description,
                                        monsterTypeName
                                    ).GetAwaiter().GetResult();
                                }
                                catch
                                {
                                    hint = null;
                                }
                            }
                            else
                            {
                                hint = null;
                            }

                            // Fall back as a pair too, so live switching and replays remain bilingual.
                            if (hint == null)
                            {
                                var russianHint = Geralt.WitcherSensesHints.TryGetValue(hintTarget.GameCharacter.Name, out var h)
                                    ? h : "Что-то странное. Неизвестный зверь.";
                                hint = new BilingualGeneratedText(
                                    russianHint, GameLocalization.Text(russianHint, GameLocalization.English));
                            }

                            player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                                "Чутьё", $"{hint.Russian} ({hintTarget.DiscordUsername})",
                                "Witcher senses", $"{hint.English} ({hintTarget.DiscordUsername})") + "\n");
                            Homelander.RecordReveal(game, player, hintTarget);
                        }

                        // Lambert: 10% chance, one-time per game (m16)
                        if (!geraltMedEor.LambertUsed && _rand.Luck(10))
                        {
                            geraltMedEor.LambertActive = true;
                            geraltMedEor.LambertUsed = true;
                            geraltMedEor.LambertSkillLost = player.GameCharacter.GetSkill();
                            game.Phrases.GeraltLambert.SendLog(player, false, suffix: $" - {(int)geraltMedEor.LambertSkillLost} *Скилла*.");
                        }

                        // No moral phrase
                        game.Phrases.GeraltNoMoral.SendLog(player, true);
                    }
                    break;

                // Геральт — Ведьмачьи заказы: end-of-round contract phrases + reset
                case "Ведьмачьи заказы":
                    if (player.GameCharacter.Name == "Геральт")
                    {
                        var geraltEorContracts = player.Passives.GeraltContracts;

                        // Contract phrase based on fights this round
                        var foughtThisRound = geraltEorContracts.ContractsFoughtThisRound;
                        if (foughtThisRound == 1) game.Phrases.GeraltContract1.SendLog(player, false);
                        else if (foughtThisRound == 2) game.Phrases.GeraltContract2.SendLog(player, false);
                        else if (foughtThisRound == 3) game.Phrases.GeraltContract3.SendLog(player, false);
                        else if (foughtThisRound == 4) game.Phrases.GeraltContract4.SendLog(player, false);
                        else if (foughtThisRound >= 5) game.Phrases.GeraltContract5Plus.SendLog(player, false);

                        // Loot phrases for non-contract wins
                        if (geraltEorContracts.NonContractWinsThisRound == 1)
                            game.Phrases.GeraltLoot.SendLog(player, false);
                        else if (geraltEorContracts.NonContractWinsThisRound > 1)
                            game.Phrases.GeraltLootMulti.SendLog(player, false);

                        // Snapshot current demand data → previous (for demand buttons during ready phase)
                        var demand = player.Passives.GeraltContractDemand;

                        // Deep-copy CurrentPerTarget → PrevPerTarget
                        demand.PrevPerTarget = demand.CurrentPerTarget.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new Geralt.PerTargetFightData
                            {
                                AttackWins = kvp.Value.AttackWins,
                                AttackLosses = kvp.Value.AttackLosses,
                                DefenseWins = kvp.Value.DefenseWins,
                                DefenseLosses = kvp.Value.DefenseLosses,
                                WasTooGood = kvp.Value.WasTooGood,
                                WasTooStronk = kvp.Value.WasTooStronk,
                                TargetPosition = kvp.Value.TargetPosition,
                                TargetName = kvp.Value.TargetName
                            });

                        demand.PrevContractsFought = geraltEorContracts.ContractsFoughtThisRound;
                        demand.PrevGeraltPosition = player.Status.GetPlaceAtLeaderBoard();
                        demand.PrevLambertWasActive = player.Passives.GeraltMeditation.LambertActive;
                        demand.PrevWasBlocking = player.Status.IsBlock;
                        demand.PrevAllContractsFought = geraltEorContracts.EnemyTypes.Count > 0
                            && geraltEorContracts.EnemiesFoughtThisRound.IsSupersetOf(geraltEorContracts.EnemyTypes.Keys);

                        // Advance resolution
                        if (demand.AdvancePending)
                        {
                            player.Status.AddRegularPoints(2, "Чеканная монета (аванс)");

                            var advanceInvoice = demand.CalculateInvoice();
                            var advTotal = advanceInvoice.Total;

                            int advDispleasure;
                            if (advTotal <= 0) advDispleasure = 5;
                            else if (advTotal < 3) advDispleasure = 3;
                            else if (advTotal < 4) advDispleasure = 2;
                            else if (advTotal < 5) advDispleasure = 1;
                            else if (advTotal >= 6) advDispleasure = demand.Displeasure > 0 ? -1 : 0;
                            else advDispleasure = 0;

                            demand.Displeasure += advDispleasure;
                            if (demand.Displeasure < 0) demand.Displeasure = 0;
                            demand.TotalSuccessfulDemands++;
                            demand.AdvancePending = false;

                            if (advDispleasure > 0)
                                player.Status.AddInGamePersonalLogs($"Аванс: Недовольство +{advDispleasure} (счёт: {advTotal})\n");
                            else if (advDispleasure < 0)
                                player.Status.AddInGamePersonalLogs($"Аванс: Недовольство {advDispleasure} (счёт: {advTotal})\n");
                            else
                                player.Status.AddInGamePersonalLogs($"Аванс: Нейтрально (счёт: {advTotal})\n");

                            // Death by pitchforks
                            if (demand.Displeasure >= 11)
                            {
                                if (!Itachi.TryPreventDeath(player, game))
                                {
                                    player.Passives.IsDead = true;
                                    player.Passives.DeathSource = "Pitchforks";
                                    player.Status.AddBonusPointsIgnoringFloor(-500, "Вилы разъяренной толпы");
                                    game.AddGlobalLogs($"Жители деревни подняли {player.DiscordUsername} на вилы за жадность! Ведьмак мёртв.");
                                    player.Status.AddInGamePersonalLogs("Чеканная монета: Толпа с вилами! Вы мертвы. -500 очков.\n");
                                }
                            }
                        }

                        // Reset current accumulators
                        demand.CurrentPerTarget = new Dictionary<Guid, Geralt.PerTargetFightData>();
                        demand.DemandedThisPhase = false;
                        demand.DemandedForNext = false;

                        // Reset round counters
                        geraltEorContracts.ContractsFoughtThisRound = 0;
                        geraltEorContracts.NonContractWinsThisRound = 0;
                        geraltEorContracts.PlotvaPhrasedThisRound = false;
                        geraltEorContracts.PlotvaContractsGrantedThisRound = false;
                        geraltEorContracts.EnemiesFoughtThisRound.Clear();
                        geraltEorContracts.RareLootFoundThisRound = false;
                        demand.QuestCompletedThisRound = false;

                        // Reset oil (Медитация re-applies if blocking, runs after this)
                        player.Passives.GeraltOil.IsOilApplied = false;

                        // Lambert reset
                        if (player.Passives.GeraltMeditation.LambertActive)
                            player.Passives.GeraltMeditation.LambertActive = false;
                    }
                    break;
                }
        }

        // A fatal round-10 Death Note opens the same event as a round-10 fight loss.
        // Боги мне не указ revives Kratos when round 11 opens, so the event then continues normally.
        if (!game.IsKratosEvent && game.RoundNo == 10
                                && eventKratos is { PlayerType: not 404 }
                                && CanKratosReturnFromGod(eventKratos))
            await StartKratosEvent(game, eventKratos);

        // High Elo repeated loss — any player losing to a high-elo character for 2nd+ consecutive time
        var highEloNames = new HashSet<string> { "DeepList", "mylorik", "Глеб", "Dopa", "Загадочный Спартанец в маске" };
        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            if (player.Status.IsLostThisCalculation == Guid.Empty) continue;
            var enemy = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.IsLostThisCalculation);
            if (enemy == null || !highEloNames.Contains(enemy.GameCharacter.Name)) continue;

            // Check if also lost to this same enemy last round
            if (player.Status.WhoToLostEveryRound.Any(x =>
                    x.RoundNo == game.RoundNo - 1 && x.EnemyId == enemy.GetPlayerId()))
            {
                game.Phrases.HighEloLoss.SendLog(player, false);
            }
        }

        // LeCrisp Stonks — earned more than 10 regular points this round
        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            if (player.GameCharacter.Name != "LeCrisp") continue;
            if (player.Status.GetScoresToGiveAtEndOfRound() > 10)
            {
                game.Phrases.LeCrispStonks.SendLog(player, false);
            }
        }

        // Salldorum — record the position actually occupied during this round.
        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            if (player.GameCharacter.Name != "Salldorum") continue;
            var posHistory = player.Passives.SalldorumChronicler.PositionHistory;
            var currentPos = player.Status.GetPlaceAtLeaderBoard();
            while (posHistory.Count < game.RoundNo)
                posHistory.Add(0);
            posHistory[game.RoundNo - 1] = currentPos;
        }

        // Котики — Рандомное поведение: Trick 3 vase chain (game-wide event)
        {
            // Find the original Котики player who has RandomBehavior state
            var kotikiOwner = game.PlayersList.Find(x => x.GameCharacter.Name == "Котики" && !x.Passives.IsDead);
            if (kotikiOwner != null)
            {
                var rbVase = kotikiOwner.Passives.KotikiRandomBehavior;
                if (rbVase.VasePendingTargets.Count > 0)
                {
                    var nextRoundPending = new List<Guid>();
                    foreach (var targetId in rbVase.VasePendingTargets.Distinct().ToList())
                    {
                        if (rbVase.VaseImmunePlayerIds.Contains(targetId)) continue;
                        var vaseTarget = game.PlayersList.Find(x => x.GetPlayerId() == targetId);
                        if (vaseTarget == null || vaseTarget.Passives.IsDead || UnknownBug.Is(vaseTarget)) continue;

                        var skill = vaseTarget.FightCharacter.GetSkill();
                        bool caught;
                        if (skill <= 20)
                            caught = false;
                        else
                            caught = _rand.Random(1, 100) <= Math.Min((int)(skill / 3), 100);

                        if (caught)
                        {
                            vaseTarget.Status.AddBonusPoints(1, "Скинул вазу");
                            rbVase.VaseImmunePlayerIds.Add(targetId);
                            game.Phrases.KotikiStormVaseCatch.SendLog(vaseTarget, false);
                        }
                        else
                        {
                            vaseTarget.Status.AddBonusPoints(-1, "Скинул вазу");
                            game.Phrases.KotikiStormVaseDrop.SendLog(vaseTarget, false);

                            // Add neighbors (position ±1) to next round pending
                            var pos = vaseTarget.Status.GetPlaceAtLeaderBoard();
                            foreach (var neighbor in game.PlayersList)
                            {
                                var neighborPos = neighbor.Status.GetPlaceAtLeaderBoard();
                                if ((neighborPos == pos - 1 || neighborPos == pos + 1) &&
                                    neighbor.GetPlayerId() != targetId)
                                {
                                    nextRoundPending.Add(neighbor.GetPlayerId());
                                }
                            }
                        }
                    }

                    rbVase.VasePendingTargets = nextRoundPending;
                }
            }
        }

        GordonFreeman.MatureHeadcrabs(game);
        var jon = JonSnow.Find(game.PlayersList);
        if (jon != null)
            JonSnow.ClearRoundState(jon);
    }

    public void RestoreOctopusInk(GameClass game)
    {
        foreach (var player in game.PlayersList.Where(x =>
                     !x.Passives.IsDead
                     && x.GameCharacter.Passive.Any(passive => passive.PassiveName == "Чернильная завеса")))
        {
            var octopusInk = player.Passives.OctopusInkList;
            var octopusInv = player.Passives.OctopusInvulnerabilityList;
            if (octopusInk.RealScoreList.Count == 0 && octopusInv.Count == 0) continue;
            var protectedTransferScore = octopusInk.RealScoreList
                .Where(entry => entry.RealScore < 0)
                .Where(entry =>
                {
                    var affectedPlayer = game.PlayersList.Find(candidate =>
                        candidate.GetPlayerId() == entry.PlayerId);
                    if (affectedPlayer == null) return false;
                    var scoreTarget = Naruto.ResolveScoreSuccessor(game, affectedPlayer);
                    return UnknownBug.Is(scoreTarget)
                           || Homelander.IsProtected(
                               scoreTarget.GameCharacter, scoreTarget.Status, "Чернильная завеса");
                })
                .Sum(entry => -entry.RealScore);

            foreach (var inkScore in octopusInk.RealScoreList.ToList())
            {
                var affectedPlayer = game.PlayersList.Find(x => x.GetPlayerId() == inkScore.PlayerId);
                if (affectedPlayer == null) continue;
                var scoreTarget = Naruto.ResolveScoreSuccessor(game, affectedPlayer);
                if (scoreTarget.Passives.IsDead) continue;
                if (inkScore.RealScore < 0 && UnknownBug.Is(scoreTarget)) continue;
                if (inkScore.RealScore < 0
                    && !Homelander.CanTransferFrom(scoreTarget, "Чернильная завеса"))
                    continue;

                // D11: when a living Itachi will reclaim the same earned point through Цукуеми,
                // charge the victim only once while preserving Octopus's duplicated credit.
                if (inkScore.RealScore < 0 && inkScore.PlayerId != player.GetPlayerId()
                    && game.PlayersList.Any(itachi =>
                        !itachi.Passives.IsDead
                        && itachi.GameCharacter.Passive.Any(p => p.PassiveName == "Глаза Итачи")
                        && itachi.Passives.ItachiTsukuyomi.StolenFromPlayers.TryGetValue(
                            inkScore.PlayerId, out var stolenAmount)
                        && stolenAmount > 0))
                {
                    affectedPlayer.Status.AddInGamePersonalLogs(
                        "🐙 Чернильная завеса: это очко уже забрал Итачи — списываем один раз.\n");
                    continue;
                }

                var restoredScore = inkScore.RealScore;
                if (affectedPlayer.GetPlayerId() == player.GetPlayerId() && restoredScore > 0)
                    restoredScore = Math.Max(0, restoredScore - protectedTransferScore);
                if (restoredScore != 0)
                    scoreTarget.Status.AddBonusPoints(restoredScore, "🐙");
            }

            player.Status.AddBonusPoints(octopusInv.Count, "🐙");
            octopusInk.RealScoreList.Clear();
            octopusInv.Count = 0;
        }
    }

    public async Task HandleNextRound(GameClass game)
    {
        if (game.RoundNo == 11)
            RestoreOctopusInk(game);

        if (game.RoundNo is 4 or 7 or 10)
            GordonFreeman.PlantHeadcrabs(game);

        var gordon = game.PlayersList.Find(player =>
            player.GameCharacter.Name == GordonFreeman.CharacterName);
        if (gordon != null)
        {
            GordonFreeman.PublishHalfLifeAnnouncementAtTurnStart(gordon, game);
            GordonFreeman.HandleRoundPhrase(gordon, game.RoundNo);
        }

        var madara = Madara.Find(game);
        if (madara != null && !madara.Passives.IsDead)
        {
            Madara.SendDeferredFightPhrase(madara, game);
            madara.GameCharacter.SetMainSkill(0, Madara.ReanimatedBody, false);
            madara.GameCharacter.SetMoral(0, Madara.ReanimatedBody, false);
            madara.Predict.Clear();
            madara.Status.LvlUpPoints = 0;
            madara.Status.ConfirmedPredict = true;

            if (game.RoundNo == 8 && !madara.Passives.Madara.ThemeStarted)
            {
                madara.Passives.Madara.ThemeStarted = true;
                Madara.SetUnableToAct(madara);
                var themePhrase = game.Phrases.MadaraRoundEightTheme;
                game.AddGlobalLogs(PhrasePayload.Encode(
                    themePhrase.PassiveNameRus,
                    themePhrase.PassiveLogRus[0],
                    themePhrase.PassiveNameEng,
                    themePhrase.PassiveLogEng[0]));
                foreach (var listener in game.PlayersList)
                    await game.Phrases.MadaraRoundEightTheme.SendLogSeparateWithFile(
                        listener, false, Madara.ThemeFile, false, 0, isRandomOrder: false, roundsToPlay: 1);
            }

            if (game.RoundNo == 9)
                Madara.ResolveRoundNine(madara, game);

            if (madara.Passives.Madara.Sealed)
                Madara.SetUnableToAct(madara);
        }

        foreach (var player in game.PlayersList)
        {
            foreach (var passive in player.GameCharacter.Passive.ToList())
            {
                if (player.Passives.IsDead
                    && passive.PassiveName is not "Глаз Шусуи" and not "Боги мне не указ")
                    continue;

                switch (passive.PassiveName)
                {
                    case Homelander.Modesty:
                        if (game.RoundNo == 9)
                            Homelander.EvaluatePredictions(game);
                        break;

                    case ErenYeager.Sheep:
                        if (player.GameCharacter.Name == ErenYeager.CharacterName
                            && game.RoundNo is >= 2 and <= 8)
                        {
                            var eren = player.Passives.Eren;
                            player.GameCharacter.AddIntelligence(1, ErenYeager.Sheep);
                            eren.RageGained++;

                            player.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
                                ErenYeager.Sheep,
                                CharactersUniquePhrase.ErenSheepRoundPhrases[game.RoundNo - 1],
                                GameLocalization.Text(ErenYeager.Sheep, GameLocalization.English),
                                CharactersUniquePhrase.ErenSheepRoundPhrasesEnglish[game.RoundNo - 1]) + "\n");
                        }
                        break;

                    case ErenYeager.Rumbling:
                        if (player.GameCharacter.Name == ErenYeager.CharacterName
                            && game.RoundNo == 10
                            && !player.Passives.IsDead
                            && !player.Passives.Eren.RumblingWarningPlayed)
                        {
                            player.Passives.Eren.RumblingWarningPlayed = true;
                            game.AddGlobalLogs(
                                "Армин: **Внимание!** Нам всем скоро конец! Поэтому... Весь мир должен объединиться и убить... Эрена Йегера!");
                        }
                        break;

                    case "Rune":
                        if (player.GameCharacter.Name == DoomGuy.CharacterName && player.IsBot()
                            && game.RoundNo == 1 && !player.Passives.DoomGuy.RollMode)
                            DoomGuy.ActivateRollMode(player);
                        break;

                    case "Shield":
                        if (player.GameCharacter.Name == DoomGuy.CharacterName)
                        {
                            var doom = player.Passives.DoomGuy;
                            foreach (var expired in doom.CounterAttackMarks
                                         .Where(x => x.Value < game.RoundNo)
                                         .Select(x => x.Key).ToList())
                                doom.CounterAttackMarks.Remove(expired);
                            if (doom.ShockSkipRound == game.RoundNo && doom.ShockSkipTarget != Guid.Empty)
                            {
                                var shocked = game.PlayersList.Find(x => x.GetPlayerId() == doom.ShockSkipTarget);
                                if (shocked != null && !shocked.Passives.IsDead && !UnknownBug.Is(shocked))
                                {
                                    // Ordinary forced-skip shape (Буль/АФКА/Школьник/Тигр ban): held ready
                                    // but NOT confirmed, so a human must acknowledge the lost turn with the
                                    // standard Confirm-Skip control. Do not auto-confirm it again (M29): the
                                    // readiness floor in CheckIfReady already stops it from stalling a round,
                                    // and bots finalize it through BotsBehavior.CompleteForcedSkip.
                                    shocked.Status.IsSkip = true;
                                    shocked.Status.TurnInterference = TurnInterferenceKind.Enemy;
                                    shocked.Status.ConfirmedSkip = false;
                                    shocked.Status.IsBlock = false;
                                    shocked.Status.IsReady = true;
                                    shocked.Status.WhoToAttackThisTurn = new List<Guid>();
                                    shocked.Status.AddInGamePersonalLogs("Шоковый щит: следующий ход пропущен.\n");
                                }
                                doom.ShockSkipTarget = Guid.Empty;
                            }
                        }
                        break;

                    case "Mission":
                        if (player.GameCharacter.Name == DoomGuy.CharacterName
                            && player.Passives.DoomGuy.GetActive(DoomGuy.Mission) == DoomGuy.DemonNests)
                            DoomGuy.SpawnDemonNest(player, game);
                        break;

                    case "Коммуникация":
                        if (game.RoundNo == 6)
                        {
                            game.Phrases.YongGlebCommunicationReady.SendLog(player, false);
                        }
                        break;

                    case "Следит за игрой":
                        // Compute default bot attack preferences for Gleb
                        var metaTargets = game.PlayersList
                            .Where(x => x.GetPlayerId() != player.GetPlayerId())
                            .Select(t =>
                            {
                                decimal pref = 10;
                                var botJustice = player.GameCharacter.Justice.GetRealJusticeNow();
                                var targetJustice = t.GameCharacter.Justice.GetSeenJusticeNow();

                                if (botJustice == targetJustice) pref -= 5;
                                else if (botJustice < targetJustice) pref -= 7;

                                if (t.Status.GetPlaceAtLeaderBoard() == 1) pref -= 1;
                                if (player.Status.GetPlaceAtLeaderBoard() == 1 && t.Status.GetPlaceAtLeaderBoard() == 2) pref -= 1;

                                if (player.Status.WhoToLostEveryRound.Any(x =>
                                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == t.GetPlayerId() && x.IsTooGoodEnemy))
                                    pref -= 7;
                                else if (t.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.GetPlayerId() && x.IsTooGoodMe))
                                    pref -= 7;
                                else if (player.Status.WhoToLostEveryRound.Any(x =>
                                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == t.GetPlayerId() && x.IsStatsBetterEnemy))
                                    pref -= 5;

                                if (t.Status.WhoToLostEveryRound.Any(x =>
                                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == player.GetPlayerId() && x.IsTooGoodEnemy))
                                    pref += 4;

                                if (pref >= 5 && player.GameCharacter.HasSkillTargetOn(t.GameCharacter)) pref += 1;
                                if (pref >= 5 && player.GameCharacter.HasNemesisOver(t.GameCharacter)) pref += 3;

                                return new { Player = t, Pref = pref };
                            })
                            .OrderByDescending(x => x.Pref)
                            .Take(3)
                            .Select(x => x.Player.GetPlayerId())
                            .ToList();

                        // Most wanted: Рик всегда среди мета-меток
                        var rickMwMeta = RickSanchez.FindMostWantedHolder(game.PlayersList, player);
                        if (rickMwMeta != null && !metaTargets.Contains(rickMwMeta.GetPlayerId()))
                        {
                            if (metaTargets.Count == 3)
                                metaTargets.RemoveAt(metaTargets.Count - 1);
                            metaTargets.Insert(0, rickMwMeta.GetPlayerId());
                        }

                        player.Passives.YongGlebMetaClass = metaTargets;
                        break;

                    case "L":
                        if (game.RoundNo >= 8)
                        {
                            var kiraLNext = player.Passives.KiraL;
                            if (kiraLNext.LPlayerId != Guid.Empty && !kiraLNext.IsArrested)
                            {
                                var lPlayerNext = game.PlayersList.Find(x => x.GetPlayerId() == kiraLNext.LPlayerId);
                                if (lPlayerNext != null)
                                {
                                    // Check if L correctly predicted Kira
                                    var lPredictedKira = lPlayerNext.Predict.Any(p =>
                                        p.PlayerId == player.GetPlayerId() &&
                                        string.Equals(p.CharacterName, "Кира", StringComparison.OrdinalIgnoreCase));
                                    if (lPredictedKira)
                                    {
                                        // Goblins are immune to kill effects
                                        if (player.GameCharacter.Name == "Стая Гоблинов") break;
                                        if (Itachi.TryPreventDeath(player, game)) break;
                                        kiraLNext.IsArrested = true;
                                        player.Passives.IsDead = true;
                                        player.Passives.DeathSource = "Kira";
                                        // Монстр без имени: +1 regular point per death
                                        foreach (var mp in game.PlayersList.Where(x => !x.Passives.IsDead
                                                     && x.GameCharacter.Passive.Any(y => y.PassiveName == "Монстр")))
                                        {
                                            mp.Status.AddRegularPoints(1, "Монстр");
                                            game.Phrases.MonsterDeath.SendLog(mp, false);
                                        }
                                        player.Status.AddBonusPointsIgnoringFloor(-500, "Арест Киры");

                                        game.AddGlobalLogs(
                                            "**L:** Эй, Кира.\n" +
                                            "**Kira:** Да?\n" +
                                            "**L:** Ты арестован. Ты Кира. Я думал так начать ко всем обращаться, но им оказался ты.\n\n" +
                                            $"**L:** Я поймал Киру... Причем совершенно случайно. Оказалось что он играл со мной в одну текстовую онлайн игру под ником {player.DiscordUsername} и пытался убить своих оппонентов с помощью какой-то тетрадки. Кто бы мог подумать. \n" +
                                            "А еще я уронил мороженное на тетрадь, она заляпалась и испортилась, теперь никто больше не будет умирать. Но не волнуйтесь, мороженное я слизал. \n" +
                                            "**Рюк:** Лайт, ну ты чего, совсем дурачок что ли? Зачем ты вообще играл в этот мусор? Нафига ты мне такой нужен. Запишу тебя в __свою__ тетрадь. \n" +
                                            "Kira -500 **очков**\n" +
                                            "Рюк +500 **очков**");
                                    }
                                }
                            }
                        }
                        break;

                    case "Ищет достойного противника":
                        if (game.RoundNo == 11)
                        {
                            // Round 10 just finished; RoundNo is already 11.
                            var saitamaWorthy = player.Passives.SaitamaUnnoticed;

                            var saitamaBeatTop1All = game.PlayersList.FindAll(x => x.Status.WhoToLostEveryRound.Any(y => y.RoundNo == 10 && y.EnemyId == player.GetPlayerId()));
                            var saitamaBeatTop1 = saitamaBeatTop1All.FindAll(x => x.Status.WhoToLostEveryRound.Any(y => y.RoundNo == 10 && y.PlaceAtLeaderBoardMe == 1 && y.WhoAttacked == player.GetPlayerId()));

                            if (saitamaBeatTop1.Count > 0)
                            {
                                // ONE PUUUUUUNCH! Reclaim all deferred points (zero-sum) and convert restored moral to score.
                                var reclaimableLedger = saitamaWorthy.Ledger.Where(entry =>
                                {
                                    var recipient = game.PlayersList.Find(candidate =>
                                        candidate.GetPlayerId() == entry.RecipientId);
                                    if (recipient == null) return false;
                                    var scoreRecipient = Naruto.ResolveScoreSuccessor(game, recipient);
                                    return !UnknownBug.Is(scoreRecipient)
                                           && Homelander.CanTransferFrom(
                                               scoreRecipient, "Ищет достойного противника");
                                }).ToList();
                                var totalDeferred = reclaimableLedger.Sum(entry => entry.Points);
                                // Record the restored amount for the "one_punch" achievement (≥20 → unlock).
                                player.Passives.AchievementTracker.SaitamaDeferredPoints = totalDeferred;
                                if (totalDeferred > 0)
                                {
                                    // Give Saitama the banked (already round-multiplied) total...
                                    player.Status.AddBonusPoints(totalDeferred, "🐙🐙🐙Ищет достойного противника🐙🐙🐙");

                                    // ...and take it back from each player who pocketed it (or the Jew who stole it).
                                    foreach (var entry in reclaimableLedger)
                                    {
                                        var recipient = game.PlayersList.Find(x => x.GetPlayerId() == entry.RecipientId);
                                        if (recipient != null)
                                            Naruto.ResolveScoreSuccessor(game, recipient).Status
                                                .AddBonusPoints(-entry.Points, "Ищет достойного противника");
                                    }
                                }
                                saitamaWorthy.Ledger.Clear();

                                var deferredMoral = saitamaWorthy.DeferredMoral;
                                if (deferredMoral > 0)
                                {
                                    // Restore the foregone moral, then exchange it for score via the game's tiered
                                    // conversion (HandleMoralForScore: ≥20→+10, ≥13→+5, ≥8→+2, ≥5→+1, one tier per call).
                                    player.GameCharacter.AddMoral(deferredMoral, "Ищет достойного противника");
                                    saitamaWorthy.DeferredMoral = 0;

                                    while (player.GameCharacter.GetMoral() >= 5)
                                        await _gameReaction.HandleMoralForScore(player);

                                    // The per-round moral flush (DoomsdayMachine ~213) won't run after round 10, so flush here.
                                    var moralPoints = player.GameCharacter.GetBonusPointsFromMoral();
                                    if (moralPoints != 0)
                                        player.Status.AddBonusPoints(moralPoints, "Мораль");
                                    player.GameCharacter.SetBonusPointsFromMoral(0);
                                }

                                game.AddGlobalLogs($"{player.DiscordUsername} наконец показал свою ИСТИННУЮ СИЛУ! ONE PUUUUUUNCH!!!");
                            }
                        }
                        break;

                    case "Чернильная завеса":
                        // Settled once at the round-11 boundary (or explicitly when a Kratos event ends).
                        break;

                    case "Они позорят военное искусство":
                        if (game.RoundNo == 10)
                            player.GameCharacter.SetStrength(0, "Они позорят военное искусство");
                        break;

                    case "Буль":
                        if (player.GameCharacter.GetPsyche() < 7)
                        {

                            if (_rand.Luck(1, 10 + player.GameCharacter.GetPsyche() * 5))
                            {
                                player.Status.IsSkip = true;
                                player.Status.ConfirmedSkip = false;
                                player.Status.IsBlock = false;
                                player.Status.IsReady = true;
                                player.Status.WhoToAttackThisTurn = new List<Guid>();

                                game.Phrases.MylorikBoolePhrase.SendLog(player, false);
                            }
                        }

                        var boole = player.Passives.MylorikBoole;
                        if (boole.IsBoole && player.GameCharacter.GetPsyche() > 0)
                        {
                            player.GameCharacter.AddStrength(-2, "Буль", false);
                            player.GameCharacter.AddExtraSkill(-22, "Буль", false);
                            boole.IsBoole = !boole.IsBoole;
                        }

                        if (!boole.IsBoole && player.GameCharacter.GetPsyche() <= 0)
                        {
                            player.GameCharacter.AddStrength(2, "Буль");
                            player.GameCharacter.AddExtraSkill(22, "Буль");
                            boole.IsBoole = !boole.IsBoole;
                        }

                        break;

                    case "Повторяет за myloran":
                        if (game.RoundNo == 5)
                        {
                            player.Status.AddInGamePersonalLogs(
                                "ZaRDaK: Ты никогда не возьмешь даймонд, Лорик. Удачи в промо.\nmylorik: ММММММММММ!!!!!  +4 Интеллекта.\n");
                            player.GameCharacter.AddIntelligence(4, "Повторяет за myloran", false);
                        }

                        if (game.RoundNo == 10)
                        {
                            player.Status.AddInGamePersonalLogs(
                                "ZaRDaK: Ты так и не апнул чалланджер? Хах, неудивительно.\nmylorik закупился у продавца сомнительных тактик: +228 *Скилла*!\n");
                            player.GameCharacter.AddExtraSkill(228, "Повторяет за myloran", false);
                        }

                        break;

                    case "Стримснайпят и банят и банят и банят":
                        if (game.RoundNo == 10)
                            Tigr.ApplyRoundTenBan(player, game);

                        break;

                    case "Тигр топ, а ты холоп":
                        var tigr = player.Passives.TigrTopWhen;
                        if (tigr.WhenToTrigger.Contains(game.RoundNo))
                            player.Passives.TigrTop = new Tigr.TigrTopClass();
                        break;

                    case "Дерзкая школота":
                        if (game.RoundNo == 1)
                        {
                            game.Phrases.MitsukiCheekyBriki.SendLog(player, true);
                            player.Status.AddRegularPoints(1, "Много выебывается");
                            game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                        }

                        break;

                    case "Школьник":
                        var acc = player.Passives.MitsukiNoPcTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            game.Phrases.MitsukiSchoolboy.SendLog(player, true);
                            player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(5);
                        }

                        break;

                    case "АФКА":
                        var afkaChance = 32 - (game.RoundNo - player.GameCharacter.GetLastMoralRound()) * 4;
                        if (afkaChance <= 0)
                            afkaChance = 1;
                        if (_rand.Luck(1, afkaChance))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            game.Phrases.AwdkaAfk.SendLog(player, true);
                        }

                        break;

                    case "Я пытаюсь!":
                        var awdkaa = player.Passives.AwdkaTryingList;

                        foreach (var enemy in awdkaa.TryingList)
                            if (enemy != null)
                                if (enemy.Times >= 2 && enemy.IsUnique == false)
                                {
                                    player.Status.LvlUpPoints += 2;
                                    player.GameCharacter.AddExtraSkill(20, "Я пытаюсь!");
                                    await _gameUpdateMess.UpdateMessage(player);
                                    enemy.IsUnique = true;
                                    game.Phrases.AwdkaTrying.SendLog(player, true);
                                }

                        break;

                    case "Научите играть":
                        var awdkaTempStats = player.Passives.AwdkaTeachToPlayTempStats;

                        var awdka = player.Passives.AwdkaTeachToPlay;

                        //remove stats from previos time
                        if (awdkaTempStats.MadnessList.Count >= 2)
                        {
                            var regularStats = awdkaTempStats.MadnessList.Find(x => x.Index == 1);
                            var madStats = awdkaTempStats.MadnessList.Find(x => x.Index == 2);

                            var intel = player.GameCharacter.GetIntelligence() - madStats.Intel;
                            var str = player.GameCharacter.GetStrength() - madStats.Str;
                            var speed = player.GameCharacter.GetSpeed() - madStats.Speed;
                            var psy = player.GameCharacter.GetPsyche() - madStats.Psyche;

                            var intelToGive = regularStats.Intel + intel;
                            if (intelToGive > 10)
                                intelToGive = 10;
                            player.GameCharacter.SetIntelligence(intelToGive, "Научите играть", false);
                            player.GameCharacter.SetStrength(regularStats.Str + str, "Научите играть", false);
                            player.GameCharacter.SetSpeed(regularStats.Speed + speed, "Научите играть", false);
                            player.GameCharacter.SetPsyche(regularStats.Psyche + psy, "Научите играть", false);
                            player.GameCharacter.SetIntelligenceExtraText("");
                            player.GameCharacter.SetStrengthExtraText("");
                            player.GameCharacter.SetSpeedExtraText("");
                            player.GameCharacter.SetPsycheExtraText("");
                            player.Passives.AwdkaTeachToPlayTempStats.MadnessList.Clear();
                        }
                        //end remove stats


                        //crazy shit
                        player.Passives.AwdkaTeachToPlayTempStats = new DeepList.Madness
                        {
                            RoundItTriggered = game.RoundNo
                        };

                        awdkaTempStats = player.Passives.AwdkaTeachToPlayTempStats;

                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(1,
                            player.GameCharacter.GetIntelligence(),
                            player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                            player.GameCharacter.GetPsyche()));
                        //end crazy shit

                        if (awdka.Training.Count == 0) break;
                        //find out  the biggest stat
                        var bestSkill = awdka.Training.OrderByDescending(x => x.StatNumber).ToList().First();

                        var intel1 = player.GameCharacter.GetIntelligence();
                        var str1 = player.GameCharacter.GetStrength();
                        var speed1 = player.GameCharacter.GetSpeed();
                        var pshy1 = player.GameCharacter.GetPsyche();

                        switch (bestSkill.StatIndex)
                        {
                            case 1:
                                intel1 = bestSkill.StatNumber;
                                player.GameCharacter.SetIntelligenceExtraText(
                                    $" (<:volibir:894286361895522434> Интеллект {intel1})");
                                break;
                            case 2:
                                str1 = bestSkill.StatNumber;
                                player.GameCharacter.SetStrengthExtraText(
                                    $" (<:volibir:894286361895522434> Сила {str1})");
                                break;
                            case 3:
                                speed1 = bestSkill.StatNumber;
                                player.GameCharacter.SetSpeedExtraText(
                                    $" (<:volibir:894286361895522434> Скорость {speed1})");
                                break;
                            case 4:
                                pshy1 = bestSkill.StatNumber;
                                player.GameCharacter.SetPsycheExtraText(
                                    $" (<:volibir:894286361895522434> Психика {pshy1})");
                                break;
                        }

                        if (intel1 >= player.GameCharacter.GetIntelligence())
                            player.GameCharacter.SetIntelligence(intel1, "Научите играть");

                        if (str1 >= player.GameCharacter.GetStrength())
                            player.GameCharacter.SetStrength(str1, "Научите играть");

                        if (speed1 >= player.GameCharacter.GetSpeed())
                            player.GameCharacter.SetSpeed(speed1, "Научите играть");

                        if (pshy1 >= player.GameCharacter.GetPsyche())
                            player.GameCharacter.SetPsyche(pshy1, "Научите играть");
                        //end find out  the biggest stat

                        //crazy shit 2
                        awdkaTempStats.MadnessList.Add(new DeepList.MadnessSub(2, intel1, str1, speed1, pshy1));
                        player.Passives.AwdkaTeachToPlay = new Sirinoks.TrainingClass();
                        //end crazy shit 2

                        game.Phrases.AwdkaTeachToPlay.SendLog(player, true);
                        break;

                    case "Я за чаем":
                        var luck = _rand.Luck(1, 8);

                        var glebChalleger = player.Passives.GlebChallengerTriggeredWhen;


                        if (glebChalleger.WhenToTrigger.Contains(game.RoundNo))
                            luck = _rand.Luck(1, 7);

                        // Most wanted: bigger chance for tea when Rick is in the game
                        if (!luck && RickSanchez.FindMostWantedHolder(game.PlayersList, player) != null)
                            luck = _rand.Luck(1, 4);


                        var glebTea = player.Passives.GlebTea;

                        if (luck)
                        {
                            glebTea.Ready = true;
                            glebTea.TimesRolled++;
                        }

                        if (game.RoundNo == 9 && glebTea.TimesRolled == 0) glebTea.Ready = true;

                        if (glebTea.Ready)
                            game.Phrases.GlebTeaReadyPhrase.SendLog(player, true);
                        break;

                    case "Спящее хуйло":
                        acc = player.Passives.GlebSleepingTriggeredWhen;


                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            player.Status.IsSkip = true;
                            player.Status.ConfirmedSkip = false;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();

                            player.GameCharacter.AddExtraSkill(-30, "Спящее хуйло");

                            player.GameCharacter.AvatarCurrent = player.GameCharacter.GetEventAvatar("Спящее хуйло");
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }
                        else
                        {
                            player.GameCharacter.AvatarCurrent = player.GameCharacter.Avatar;
                        }

                        if (game.RoundNo == 11)
                        {
                            player.GameCharacter.AvatarCurrent = player.GameCharacter.GetEventAvatar("Спящее хуйло");
                            game.Phrases.GlebSleepyPhrase.SendLog(player, false);
                        }

                        // Gleb sees Sirinoks become dragon on round 10
                        if (game.RoundNo == 10 && game.PlayersList.Any(x => x.GameCharacter.Name == "Sirinoks"))
                        {
                            game.AddGlobalLogs($"\n{player.DiscordUsername}: Ogo, drakon, nihuya sebe");
                        }

                        break;

                    case "Претендент русского сервера":
                        acc = player.Passives.GlebChallengerTriggeredWhen;

                        if (game.RoundNo == 10 && !acc.WhenToTrigger.Contains(game.RoundNo) &&
                            player.Status.GetPlaceAtLeaderBoard() > 2)
                        {
                            // шанс = 1 / (40 - место глеба в таблице * 4)
                            if (_rand.Luck(1, 40 - player.Status.GetPlaceAtLeaderBoard() * 4)) acc.WhenToTrigger.Add(game.RoundNo);
                        }

                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            //just check
                            player.Passives.GlebChallengerList = new DeepList.Madness
                            {
                                MadnessList = new List<DeepList.MadnessSub>(),
                                RoundItTriggered = game.RoundNo
                            };

                            var gleb = player.Passives.GlebChallengerList;
                            gleb.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                player.GameCharacter.GetPsyche()));

                            //  var randomNumber =  _rand.Random(1, 100);

                            var intel = player.GameCharacter.GetIntelligence() >= 10 ? 10 : 9;
                            var str = player.GameCharacter.GetStrength() >= 10 ? 10 : 9;
                            var speed = player.GameCharacter.GetSpeed() >= 10 ? 10 : 9;
                            var pshy = player.GameCharacter.GetPsyche() >= 10 ? 10 : 9;


                            player.GameCharacter.SetIntelligence(intel, "Претендент русского сервера");
                            player.GameCharacter.SetStrength(str, "Претендент русского сервера");
                            player.GameCharacter.SetSpeed(speed, "Претендент русского сервера");
                            player.GameCharacter.SetPsyche(pshy, "Претендент русского сервера");
                            player.GameCharacter.AddExtraSkill(99, "Претендент русского сервера");
                            player.GameCharacter.SetTargetSkillMultiplier(2);


                            gleb.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));

                            //game.Phrases.GlebChallengerPhrase.SendLog(player, true);
                            await game.Phrases.GlebChallengerSeparatePhrase.SendLogSeparateWithFile(player, true, "DataBase/sound/Irelia.mp3", true, 0, roundsToPlay: 1);
                        }

                        break;

                    case "Хождение боком":
                        acc = player.Passives.CraboRackSidewaysBooleTriggeredWhen;

                        if (acc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            //just check
                            player.Passives.CraboRackSidewaysBooleList = new DeepList.Madness
                            {
                                MadnessList = new List<DeepList.MadnessSub>(),
                                RoundItTriggered = game.RoundNo
                            };

                            var craboRack = player.Passives.CraboRackSidewaysBooleList;
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                player.GameCharacter.GetPsyche()));


                            var speed = 10;

                            player.GameCharacter.SetSpeed(speed, "Хождение боком");
                            craboRack.MadnessList.Add(new DeepList.MadnessSub(2, player.GameCharacter.GetIntelligence(),
                                player.GameCharacter.GetStrength(), speed, player.GameCharacter.GetPsyche()));
                            game.Phrases.CraboRackSidewaysBoolePhrase.SendLog(player, true);
                        }

                        break;

                    case "Сверхразум":
                        var currentDeepList = player.Passives.DeepListSupermindTriggeredWhen;

                        if (currentDeepList != null)
                            if (currentDeepList.WhenToTrigger.Any(x => x == game.RoundNo))
                            {
                                GamePlayerBridgeClass randPlayer;

                                // Most wanted: force discover Rick first
                                var rickMwSm = RickSanchez.FindMostWantedHolder(
                                    game.PlayersList, player);
                                if (rickMwSm != null
                                    && !player.Passives.DeepListSupermindKnown.KnownPlayers.Contains(rickMwSm.GetPlayerId()))
                                {
                                    randPlayer = rickMwSm;
                                }
                                else
                                {
                                    var knownCheck = player.Passives.DeepListSupermindKnown;
                                    var discoverablePlayers = game.PlayersList.Where(candidate =>
                                        candidate.GetPlayerId() != player.GetPlayerId()
                                        && !UnknownBug.Is(candidate)
                                        && !Sakura.Is(candidate)
                                        && (knownCheck == null
                                            || !knownCheck.KnownPlayers.Contains(candidate.GetPlayerId()))).ToList();
                                    if (discoverablePlayers.Count == 0)
                                        break;
                                    randPlayer = discoverablePlayers[_rand.Random(0, discoverablePlayers.Count - 1)];
                                }

                                var check = player.Passives.DeepListSupermindKnown;
                                check.KnownPlayers.Add(randPlayer.GetPlayerId());

                                // Auto-set prediction for the discovered character
                                var existingPred = player.Predict.Find(p => p.PlayerId == randPlayer.GetPlayerId());
                                if (existingPred != null)
                                    existingPred.CharacterName = randPlayer.GameCharacter.Name;
                                else
                                    player.Predict.Add(new PredictClass(randPlayer.GameCharacter.Name, randPlayer.GetPlayerId()));

                                Homelander.RecordReveal(game, player, randPlayer);
                                game.Phrases.DeepListSuperMindPhrase.SendLog(player, randPlayer, true);
                            }

                        break;

                    case "Безумие":
                        var madd = player.Passives.DeepListMadnessTriggeredWhen;

                        if (madd != null)
                            if (madd.WhenToTrigger.Contains(game.RoundNo))
                            {
                                
                                //trigger maddness
                                //me.Status.AddBonusPoints(-3, "Безумие");
                                //just check
                                player.Passives.DeepListMadnessList = new DeepList.Madness
                                {
                                    MadnessList = new List<DeepList.MadnessSub>(),
                                    RoundItTriggered = game.RoundNo
                                };

                                var curr = player.Passives.DeepListMadnessList;
                                curr.MadnessList.Add(new DeepList.MadnessSub(1, player.GameCharacter.GetIntelligence(),
                                    player.GameCharacter.GetStrength(), player.GameCharacter.GetSpeed(),
                                    player.GameCharacter.GetPsyche()));


                                var intel = 0;
                                var str = 0;
                                var speed = 0;
                                var pshy = 0;

                                for (var i = 0; i < 4; i++)
                                {
                                    var statNumber = _rand.Random(0, 10);

                                    switch (i)
                                    {
                                        case 0:
                                            intel = statNumber;
                                            break;
                                        case 1:
                                            str = statNumber;
                                            break;
                                        case 2:
                                            speed = statNumber;
                                            break;
                                        case 3:
                                            pshy = statNumber;
                                            break;
                                    }
                                }

                                player.GameCharacter.SetIntelligence(intel, "Безумие");
                                player.GameCharacter.SetStrength(str, "Безумие");
                                player.GameCharacter.SetSpeed(speed, "Безумие");
                                player.GameCharacter.SetPsyche(pshy, "Безумие");
                                //2 это х3
                                player.GameCharacter.SetAnySkillMultiplier(3);
                                //me.Status.AddBonusPoints(-3, "Безумие");

                                game.Phrases.DeepListMadnessPhrase.SendLog(player, true);

                                // БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!
                                if (game.PlayersList.Any(x => x.GameCharacter.Name == "HardKitty"))
                                    game.Phrases.DeepListMadnessHardKittyMilk.SendLog(player, false);

                                curr.MadnessList.Add(new DeepList.MadnessSub(2, intel, str, speed, pshy));
                            }

                        break;

                    case "Дракон":
                        if (game.RoundNo == 9)
                            player.Status.AddInGamePersonalLogs(
                                "Дракон: __**Бля, чо за хуйня со мной происходит!?**__\n");

                        if (game.RoundNo == 10)
                        {
                            player.GameCharacter.SetIntelligence(10, "Дракон");
                            player.GameCharacter.SetStrength(10, "Дракон");
                            player.GameCharacter.SetSpeed(10, "Дракон");
                            player.GameCharacter.SetPsyche(10, "Дракон");

                            //me.GameCharacter.AddExtraSkill((int)me.GameCharacter.GetSkill(), "Дракон");

                            var pointsToGive = player.GameCharacter.GetSkill() / 10;


                            var siri = player.Passives.SirinoksFriendsList;

                            if (siri != null)
                                for (var i = player.Status.GetPlaceAtLeaderBoard() + 1;
                                     i < game.PlayersList.Count + 1;
                                     i++)
                                {
                                    var player2 = game.PlayersList[i - 1];
                                    if (siri.FriendList.Contains(player2.GetPlayerId()))
                                        pointsToGive -= 1;
                                }

                            player.Status.AddBonusPoints(pointsToGive, "Дракон");
                            game.Phrases.SirinoksDragonPhrase.SendLog(player, true);
                        }

                        break;

                    case "Vampyr":
                        if (game.RoundNo == 1)
                        {
                            game.Phrases.VampyrVampyr.SendLog(player, true);
                            if (game.PlayersList.Any(x => x.GameCharacter.Name == "mylorik"))
                                game.AddGlobalLogs(
                                    " \n<:Y_:562885385395634196> *mylorik: Гребанный Вампур!* <:Y_:562885385395634196>",
                                    "\n\n");
                        }

                        break;

                    case "Вампуризм":
                        if (game.RoundNo is 2 or 4 or 6 or 8 or 10)
                        {
                            var vampirismState = player.Passives.VampyrHematophagiaList;
                            if (vampirismState.HematophagiaCurrent.Count > 0)
                                player.GameCharacter.AddMoral(
                                    vampirismState.HematophagiaCurrent.Count, "Вампуризм");
                        }
                        break;

                    case "Огурчик Рик":
                        var pickleNext = player.Passives.RickPickle;
                        if (pickleNext.PickleTurnsRemaining > 0)
                        {
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            player.Status.IsReady = true;
                            player.Status.TurnInterference = TurnInterferenceKind.Self;
                            // Show the Skip button and hold readiness so the human controls the
                            // pickle turn instead of the bot (auto-move is also disabled for pickle
                            // Rick in CheckIfReady). We deliberately do NOT set IsSkip — pickle must
                            // stay attackable-but-invulnerable so WasAttackedAsPickle/penalty still work.
                            player.Status.ConfirmedSkip = false;
                        }
                        if (pickleNext.PenaltyTurnsRemaining > 0)
                        {
                            pickleNext.PenaltyTurnsRemaining--;
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            game.Phrases.RickPicklePenalty.SendLog(player, true);
                        }
                        break;

                    // Глаза Итачи: move this-round target to active target for next round
                    case "Глаза Итачи":
                        var tsukuyomiNext = player.Passives.ItachiTsukuyomi;
                        if (tsukuyomiNext.TsukuyomiTargetThisRound != Guid.Empty)
                        {
                            tsukuyomiNext.TsukuyomiActiveTarget = tsukuyomiNext.TsukuyomiTargetThisRound;
                            tsukuyomiNext.TsukuyomiTargetThisRound = Guid.Empty;
                        }
                        break;

                    // Compatibility fallback for a stale/in-flight death created before the lethal
                    // source could call the immediate Izanagi interceptor.
                    case "Глаз Шусуи":
                        if (!player.Passives.ItachiShisuiUsed && player.Passives.IsDead)
                            Itachi.TryPreventDeath(player, game);
                        break;

                    // Боги мне не указ: resurrect once if killed by a member of the Бог lore class.
                    case "Боги мне не указ":
                        if (!player.Passives.KratosGodSlayerUsed && player.Passives.IsDead
                                                                     && GodClass.IsGodDeathSource(player.Passives.DeathSource))
                        {
                            var survivedKiraAttempt = player.Passives.DeathSource == "Kira";
                            player.Passives.IsDead = false;
                            player.Passives.DeathSource = "";
                            player.Passives.KratosGodSlayerUsed = true;
                            var godsCannotCommandMe = player.GameCharacter.Passive.Find(x => x.PassiveName == "Боги мне не указ");
                            if (godsCannotCommandMe != null) godsCannotCommandMe.Visible = true;
                            player.Passives.AchievementTracker.WasRevived = true;
                            if (survivedKiraAttempt)
                                player.Passives.AchievementTracker.SurvivedKiraAttempt = true;
                            player.GameCharacter.AddExtraSkill(228, "Боги мне не указ");
                            if (game.IsKratosEvent)
                            {
                                // Dead players are carried into the next round as ready blockers.
                                // Reopen Kratos's action so the resurrection does not consume an event turn.
                                player.Status.IsReady = false;
                                player.Status.IsBlock = false;
                            }
                            game.AddGlobalLogs($"**{UnknownBug.PublicName(player)}:** Боги мне не указ!");
                        }
                        break;

                    case "Впарить говна":
                        // Decrement cooldown
                        var sellerVNext = player.Passives.SellerVparitGovna;
                        sellerVNext.Cooldown = Math.Max(0, sellerVNext.Cooldown - 1);

                        // Decrement mark timers on all players
                        foreach (var marked in game.PlayersList)
                        {
                            if (marked.Passives.SellerVparitGovnaRoundsLeft > 0)
                            {
                                marked.Passives.SellerVparitGovnaRoundsLeft--;
                                if (marked.Passives.SellerVparitGovnaRoundsLeft <= 0)
                                {
                                    // Mark expired — remove temporary skill
                                    marked.GameCharacter.AddExtraSkill(
                                        -marked.Passives.SellerVparitGovnaTotalSkill, "Впарить говна", false);
                                    marked.Passives.SellerVparitGovnaTotalSkill = 0;

                                    // Collect siphoned skill into seller's box and stop siphoning
                                    var siphoned = marked.GameCharacter.SkillSiphonBox ?? 0;
                                    player.Passives.SellerSecretBuild.AccumulatedSkill += siphoned;
                                    marked.GameCharacter.SkillSiphonBox = null;

                                    // Clear outplay marks and forced loss flag
                                    marked.Passives.SellerOutplayTargets.Clear();
                                    marked.Passives.SellerForcedLossNextAttack = false;
                                }
                            }
                        }
                        break;

                    case "Секретный билд":
                        if (game.RoundNo == 10)
                        {
                            // Collect remaining active siphons from still-marked players
                            decimal totalSiphoned = player.Passives.SellerSecretBuild.AccumulatedSkill;
                            foreach (var marked in game.PlayersList)
                            {
                                if (marked.GameCharacter.SkillSiphonBox.HasValue)
                                {
                                    totalSiphoned += marked.GameCharacter.SkillSiphonBox.Value;
                                    marked.GameCharacter.SkillSiphonBox = null;
                                }
                            }

                            if (totalSiphoned > 0)
                            {
                                player.GameCharacter.AddExtraSkill(totalSiphoned, "Секретный билд", isLog:false);
                                player.Status.AddInGamePersonalLogs($"Пришло время играть по-настоящему. Мой секретный билд: +{totalSiphoned} Скилла\n");
                                game.Phrases.SellerSecretBuild.SendLog(player, false);
                            }
                        }
                        break;

                    case "Макро":
                        player.Passives.DopaMacro.FightsProcessed = 0;
                        player.Passives.DopaMacro.FightsResolved = 0;
                        player.Passives.DopaMacro.DuplicateTargetSkipRound = 0;
                        break;

                    case "Get cancer":
                        player.Passives.ToxicMateCancer.TransferredThisRound = false;
                        break;

                    case "Взгляд в будущее":
                        if (player.Passives.DopaVision.Cooldown > 0)
                        {
                            player.Passives.DopaVision.Cooldown--;
                            if (player.Passives.DopaVision.Cooldown == 0)
                                game.Phrases.DopaVisionReady.SendLog(player, false);
                        }
                        break;

                    // Монстр без имени — Выдуманный персонаж: round 9 → mark pawns + bonus
                    case "Выдуманный персонаж":
                        if (game.RoundNo == 9)
                        {
                            // Monster's correct predictions → those players become Johan's pawns
                            foreach (var prediction in player.Predict)
                            {
                                var predTarget = game.PlayersList.Find(x => x.GetPlayerId() == prediction.PlayerId);
                                if (predTarget != null && !UnknownBug.Is(predTarget) &&
                                    string.Equals(predTarget.GameCharacter.Name, prediction.CharacterName, StringComparison.OrdinalIgnoreCase))
                                {
                                    predTarget.Passives.IsJohanPawn = true;
                                    predTarget.Passives.JohanPawnOwnerId = player.GetPlayerId();
                                    predTarget.Status.AddInGamePersonalLogs("Ты стал пешкой Йохана...\n");
                                }
                            }

                            // If anyone tried to guess Monster's identity → +3 bonus
                            var anyTriedToGuessMonster = game.PlayersList.Any(x =>
                                x.GetPlayerId() != player.GetPlayerId() &&
                                x.Predict.Any(p => p.PlayerId == player.GetPlayerId()));

                            if (anyTriedToGuessMonster)
                            {
                                player.Status.AddBonusPoints(3, "Выдуманный персонаж");
                                game.AddGlobalLogs("Я бы хотел найти того, кто во всём виноват... Но у Монстра нет имени.");
                            }
                        }
                        break;

                    // Монстр без имени — Пейзаж конца света: round 10 warning
                    case "Пейзаж конца света":
                        if (game.RoundNo == 10)
                        {
                            game.AddGlobalLogs("Йохан: Я позволю вам узреть \"Пейзаж конца света\", доктор Тэнма.");
                            game.AddGlobalLogs("**Если вы нападете на Йохана, получите победу.**");
                            var tenmaMsg = "Тэнма: ~~Нет! Глупцы! Если он знает вашу личность... Не делайте этого! Пропустите ход!~~";
                            game.AddGlobalLogs(tenmaMsg);
                            // Тэнма's message disappears after ~10 seconds
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(10_000);
                                    if (!game.IsFinished)
                                        game.HiddenGlobalLogSnippets.Add(tenmaMsg);
                                }
                                catch { /* game may have ended */ }
                            });
                        }
                        break;

                    case "Близнец":
                        MonsterWithoutName.ApplyPendingStatCopies(player);
                        break;

                    // TheBoys — Francie: заказы, окно 3 хода (новый заказ на раундах 4, 7)
                    case "Francie":
                        if (player.Passives.TheBoysButcher.SuperDickActive) break; // СуперМудень отключает Француза
                        var francieNR = player.Passives.TheBoysFrancie;
                        if (game.RoundNo is 4 or 7)
                        {
                            if (francieNR.RemainingTargets.Count > 0)
                            {
                                // Most wanted: если Рик ещё в очереди — заказ на него
                                var rickMwFrancieNR = RickSanchez.FindMostWantedHolder(game.PlayersList, player);
                                if (rickMwFrancieNR != null && francieNR.RemainingTargets.Remove(rickMwFrancieNR.GetPlayerId()))
                                    francieNR.RemainingTargets.Insert(0, rickMwFrancieNR.GetPlayerId());
                                francieNR.OrderTarget = francieNR.RemainingTargets[0];
                                francieNR.RemainingTargets.RemoveAt(0);
                                francieNR.OrderHistory.Add(francieNR.OrderTarget);
                                francieNR.OrderRoundsLeft = 3;
                                var orderTargetName = game.PlayersList.Find(x => x.GetPlayerId() == francieNR.OrderTarget)?.DiscordUsername ?? "???";
                                player.Status.AddInGamePersonalLogs($"Заказ Француза: Новая цель — {orderTargetName}. 3 хода.\n");
                                game.Phrases.TheBoysOrderNew.SendLog(player, false);
                            }
                        }
                        else if (francieNR.OrderRoundsLeft > 0)
                        {
                            francieNR.OrderRoundsLeft--;
                        }
                        break;

                    // TheBoys — Kimiko: recovery/disable state (Регенирация x1+ даёт иммунитет)
                    case "Kimiko":
                        var kimikoNR = player.Passives.TheBoysKimiko;
                        if (player.Passives.TheBoysButcher.SuperDickActive)
                        {
                            kimikoNR.IsDisabled = false;
                            kimikoNR.DisabledNextRound = false;
                            break;
                        }
                        if (kimikoNR.RegenLevel > 0)
                        {
                            kimikoNR.IsDisabled = false;
                            kimikoNR.DisabledNextRound = false;
                            break;
                        }
                        if (kimikoNR.DisabledNextRound)
                        {
                            kimikoNR.IsDisabled = true;
                            kimikoNR.DisabledNextRound = false;
                        }
                        else if (kimikoNR.IsDisabled)
                        {
                            kimikoNR.IsDisabled = false;
                            game.Phrases.TheBoysKimikoRecovered.SendLog(player, false);
                        }
                        break;

                    // Геральт — Ведьмачьи заказы: spawn 1 contract each round (skip round 1 — initial contracts already granted)
                    case "Ведьмачьи заказы":
                        if (player.GameCharacter.Name == "Геральт" && game.RoundNo > 1)
                        {
                            var geraltNrContracts = player.Passives.GeraltContracts;
                            // Only spawn types that have assigned enemies
                            var assignedTypes = geraltNrContracts.EnemyTypes.Values.Distinct().ToArray();
                            // Most wanted: контракт всегда падает в тип монстра Рика
                            var rickMwGeralt = RickSanchez.FindMostWantedHolder(game.PlayersList, player);
                            var randomType = rickMwGeralt != null && geraltNrContracts.EnemyTypes
                                .TryGetValue(rickMwGeralt.GetPlayerId(), out var rickMwType)
                                ? rickMwType
                                : assignedTypes[_rand.Random(0, assignedTypes.Length - 1)];

                            geraltNrContracts.AddCount(randomType, 1);
                            var names = Geralt.GetNames(randomType);
                            var randomName = names[_rand.Random(0, names.Length - 1)];

                            game.Phrases.GeraltContractSpawn.SendLog(player, false, suffix: $"\n{randomName} ({Geralt.GetMonsterEmoji(randomType)})");
                        }
                        break;

                    // Котики — Рандомное поведение: Storm picks a random trick each round
                    case "Рандомное поведение":
                        if (game.RoundNo >= 2)
                        {
                            // Find the original Котики player who owns this Storm
                            var rbOwner = player.Passives.KotikiCatOwnerId != Guid.Empty
                                ? game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.KotikiCatOwnerId)
                                : player;
                            if (rbOwner == null) rbOwner = player;
                            var rb = rbOwner.Passives.KotikiRandomBehavior;

                            // Build weighted pool: fight(3), bite(1), vase(3 if !VaseUsed)
                            var trickPool = new List<int>();
                            for (var i = 0; i < 3; i++) trickPool.Add(1); // fight x3
                            trickPool.Add(2); // bite x1
                            if (!rb.VaseUsed)
                                for (var i = 0; i < 3; i++) trickPool.Add(3); // vase x3

                            var selectedTrick = trickPool[_rand.Random(0, trickPool.Count - 1)];
                            rb.SelectedTrickThisRound = selectedTrick;
                            rb.FightProcessed = false;

                            switch (selectedTrick)
                            {
                                case 2: // Кусь за жопу
                                {
                                    // Target pool: all players except 1st place; CAN include Storm's owner
                                    var biteCandidates = game.PlayersList
                                        .Where(x => x.Status.GetPlaceAtLeaderBoard() != 1
                                                    && !x.Passives.IsDead
                                                    && !UnknownBug.Is(x))
                                        .ToList();
                                    if (biteCandidates.Count > 0)
                                    {
                                        var biteTarget = biteCandidates[_rand.Random(0, biteCandidates.Count - 1)];
                                        rb.BiteTargetId = biteTarget.GetPlayerId();
                                        rb.BiteLockActiveUntilRound = game.RoundNo + 1;
                                        rb.BiteBonusPending = true;
                                        game.Phrases.KotikiStormBite.SendLog(biteTarget, false);
                                        biteTarget.Status.AddInGamePersonalLogs("Штормяк кусь за жопу! Вы прикованы к позиции!\n");
                                    }
                                    else
                                    {
                                        rb.SelectedTrickThisRound = 0; // no valid target
                                    }
                                    break;
                                }
                                case 3: // Скинул вазу
                                {
                                    rb.VaseUsed = true;
                                    // Select a target (exclude passive holder)
                                    var vaseCandidates = game.PlayersList
                                        .Where(x => x.GetPlayerId() != player.GetPlayerId()
                                                    && !x.Passives.IsDead
                                                    && !UnknownBug.Is(x))
                                        .ToList();
                                    if (vaseCandidates.Count > 0)
                                    {
                                        var vaseTarget = vaseCandidates[_rand.Random(0, vaseCandidates.Count - 1)];
                                        rb.VasePendingTargets.Add(vaseTarget.GetPlayerId());
                                    }
                                    else
                                    {
                                        rb.SelectedTrickThisRound = 0;
                                    }
                                    break;
                                }
                                // case 1 (fight): fight pair chosen later in DoomsdayMachine
                            }
                        }
                        break;
                }
            }

            if (player.Passives.IsDead) continue;

            //Я за чаем
            var isSkip = player.Passives.GlebTeaTriggeredWhen;

            var hasPortalGun = player.GameCharacter.Passive.Any(x => x.PassiveName == "Портальная пушка") &&
                player.Passives.RickPortalGun.Invented && player.Passives.RickPortalGun.Charges > 0;

            if (isSkip.WhenToTrigger.Contains(game.RoundNo) && !hasPortalGun && !UnknownBug.Is(player))
            {
                player.Status.IsSkip = true;
                player.Status.TurnInterference = TurnInterferenceKind.Enemy;
                player.Status.ConfirmedSkip = false;
                player.Status.IsBlock = false;
                player.Status.IsReady = true;
                player.Status.WhoToAttackThisTurn = new List<Guid>();
                player.Status.AddInGamePersonalLogs("Тебя усыпили...\n");
            }
            //end Я за чаем
        }

        // Таинственный Суппорт — "Premade": prevent marked player from skipping
        foreach (var supporter in game.PlayersList)
        {
            if (supporter.Passives.IsDead) continue;
            if (!supporter.GameCharacter.Passive.Any(x => x.PassiveName == "Premade")) continue;
            var markedId = supporter.Passives.SupportPremade.MarkedPlayerId;
            if (markedId == Guid.Empty) continue;
            var marked = game.PlayersList.Find(x => x.GetPlayerId() == markedId);
            // "кроме банов": the anti-skip must NOT lift the round-10 Тигр ban (finding M10) —
            // un-banning breaks the other systems (targeting, Тигр-топ) that assume he's banned.
            var markedIsBanned = marked != null && Tigr.IsRoundTenBanned(marked, game.RoundNo);
            if (marked != null && marked.Status.IsSkip && !marked.Status.ConfirmedSkip && !markedIsBanned)
            {
                marked.Status.IsSkip = false;
                marked.Status.IsReady = false;
                game.Phrases.SupportPremadeAntiSkip.SendLog(supporter, false);
            }
        }

    }






    public static void ApplyDopaChoice(GamePlayerBridgeClass player, GameClass game, string tactic)
    {
        player.Passives.DopaMetaChoice.Triggered = true;
        player.Passives.DopaMetaChoice.ChosenTactic = tactic;

        var allTactics = new[] { "Стомп", "Фарм", "Доминация", "Роум" };
        foreach (var t in allTactics.Where(t => t != tactic))
            player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == t);

        var meta = player.GameCharacter.Passive.Find(x => x.PassiveName == Dopa.Meta);
        if (meta != null)
        {
            meta.PassiveDescription = tactic switch
            {
                "Стомп" => "+9 Силы и 99 *Скилла*.",
                "Фарм" => "\"Взгляд в будущее\" приносит вдвое больше очков.",
                "Доминация" => "Победы приносят Допе +20 *Скилла*, а цель теряет **бонусное** очко и иногда психику. (шанс 33%)",
                "Роум" => "При победе над врагами, не стоящими по соседству в таблице, **Крадет** у них **бонусное** очко и 3 *Морали*.",
                _ => meta.PassiveDescription,
            };
        }

        if (tactic == "Стомп")
        {
            player.GameCharacter.AddStrength(9, "Стомп");
            player.GameCharacter.AddExtraSkill(99, "Стомп");
        }

        game.Phrases.DopaMetaChosen.SendLog(player, false);
        player.Status.AddInGamePersonalLogs($"Тактика выбрана: {tactic}\n");
    }

    public void HandleNextRoundAfterSorting(GameClass game)
    {
        var madara = Madara.Find(game);
        if (madara != null && !madara.Passives.IsDead)
        {
            var state = madara.Passives.Madara;
            if (state.ResolvedFights > 0 && !state.TopOnePhraseSent
                && madara.Status.GetPlaceAtLeaderBoard() == 1)
            {
                state.TopOnePhraseSent = true;
                game.Phrases.MadaraTopOne.SendLog(madara, false, isRandomOrder: false);
            }

        }

        foreach (var player in game.PlayersList)
        {
            if (player.Passives.IsDead) continue;
            foreach (var passive in player.GameCharacter.Passive.ToList())
                switch (passive.PassiveName)
                {
                case ErenYeager.Sheep:
                    if (player.GameCharacter.Name == ErenYeager.CharacterName && game.RoundNo == 9)
                        player.Status.AddBonusPoints(
                            player.Status.GetPlaceAtLeaderBoard(), ErenYeager.Sheep);
                    break;

                case "Weed":
                    var diff = game.RoundNo - player.Passives.WeedwickLastRoundWeed;
                    if (diff >= 2)
                    {
                        game.Phrases.WeedwickWeedNo.SendLog(player, false);
                        player.MinusPsyche(game, -1, "Weed");
                    }

                    break;

                case "Булькает":
                    if (player.Status.GetPlaceAtLeaderBoard() != 1)
                        player.GameCharacter.Justice.AddRealJusticeNow();
                    break;

                // TheBoys — Butcher: назначить метки супов на этот ход (2 ротационные + супергерои всегда)
                case "Butcher":
                {
                    player.Passives.TheBoysButcher.SuperDickDropsThisTurn = 0;

                    // Сброс старых меток на всех игроках
                    foreach (var pl in game.PlayersList)
                    {
                        pl.Passives.TheBoysSupMark = false;
                    }
                    if (player.Passives.TheBoysButcher.ButcherLeft) break;

                    var butcherOrderTarget = player.Passives.TheBoysButcher.SuperDickActive
                        ? Guid.Empty
                        : player.Passives.TheBoysFrancie.OrderTarget;
                    var enemies = game.PlayersList.Where(x => x.GetPlayerId() != player.GetPlayerId()
                                                            && !UnknownBug.Is(x)).ToList();

                    // 1) супергерои помечаются всегда (бесплатная метка)
                    foreach (var enemy in enemies)
                    {
                        if (TheBoys.IsPermanentSup(enemy, game.RoundNo))
                        {
                            enemy.Passives.TheBoysSupMark = true;
                        }
                    }

                    // Most wanted: Рик всегда занимает одну из двух ротационных меток (даже будучи целью заказа Франци)
                    var rotatingMarks = 2;
                    var rickMwButcher = RickSanchez.FindMostWantedHolder(enemies, player);
                    if (rickMwButcher != null && !rickMwButcher.Passives.TheBoysSupMark)
                    {
                        rickMwButcher.Passives.TheBoysSupMark = true;
                        rotatingMarks--;
                    }

                    // 2) ещё 2 случайные метки: приоритет — под текущую классовую Мишень, исключая Француз-цель и уже помеченных
                    var candidates = enemies
                        .Where(x => !x.Passives.TheBoysSupMark && x.GetPlayerId() != butcherOrderTarget)
                        .ToList();
                    var byTarget = SecureRandom.Shuffle(candidates
                        .Where(x => player.GameCharacter.HasSkillTargetOn(x.GameCharacter)));
                    var rest = SecureRandom.Shuffle(candidates
                        .Where(x => !player.GameCharacter.HasSkillTargetOn(x.GameCharacter)));
                    foreach (var enemy in byTarget.Concat(rest).Take(rotatingMarks))
                        enemy.Passives.TheBoysSupMark = true;
                    break;
                }

                case "Челюсти":
                    if (game.RoundNo > 1)
                    {
                        var shark = player.Passives.SharkJawsLeader;


                        if (!shark.FriendList.Contains(player.Status.GetPlaceAtLeaderBoard()))
                        {
                            shark.FriendList.Add(player.Status.GetPlaceAtLeaderBoard());
                            player.GameCharacter.AddSpeed(1, "Челюсти");
                        }
                    }

                    break;

                case "Тигр топ, а ты холоп":
                    if (player.Status.GetPlaceAtLeaderBoard() == 1 && game.RoundNo is > 1 and < 10)
                    {
                        player.GameCharacter.AddPsyche(1, "Тигр топ, а ты холоп");
                        player.GameCharacter.AddMoral(3, "Тигр топ, а ты холоп");
                        game.Phrases.TigrTop.SendLog(player, false);
                    }

                    break;

                case "Permaban":
                    if (game.RoundNo == 10 && player.Status.GetPlaceAtLeaderBoard() == 1)
                    {
                        player.Passives.DopaPermabanTriggered = true;
                        Tigr.ApplyRoundTenBan(player, game);
                    }
                    break;

                case "Много выебывается":
                    if (player.Status.GetPlaceAtLeaderBoard() == 1)
                    {
                        player.Status.AddRegularPoints(1, "Много выебывается");
                        game.Phrases.MitsukiTooMuchFucking.SendLog(player, false);
                    }

                    break;

                case "Запах мусора":
                    if (game.RoundNo == 11)
                    {
                        var mitsuki = player.Passives.MitsukiGarbageList;
                        if (mitsuki != null)
                        {
                            var count = 0;
                            foreach (var t in mitsuki.Training.Where(x => x.Times >= 2))
                            {
                                var player2 = game.PlayersList.Find(x => x.GetPlayerId() == t.EnemyId);
                                if (player2 != null)
                                {
                                    var scoreTarget = Naruto.ResolveScoreSuccessor(game, player2);
                                    if (UnknownBug.Is(scoreTarget)) continue;

                                    scoreTarget.Status.AddBonusPoints(-5, "Запах мусора");

                                    game.Phrases.MitsukiGarbageSmell.SendLog(scoreTarget, true);
                                    count++;
                                }
                            }

                            game.AddGlobalLogs($"Mitsuki отнял в общей сумме {count * 5} очков.");
                        }
                    }

                    break;

                case "Раскинуть щупальца":
                    if (game.RoundNo > 1)
                    {
                        var octo = player.Passives.OctopusTentaclesList;
                        if (!octo.LeaderboardPlace.Contains(player.Status.GetPlaceAtLeaderBoard()))
                        {
                            octo.LeaderboardPlace.Add(player.Status.GetPlaceAtLeaderBoard());
                            player.Status.AddRegularPoints(1, "Раскинуть щупальца");
                        }
                    }

                    break;

                case "Никому не нужен":
                    if (game.RoundNo is 9 or 7 or 5 or 3)
                    {
                        var hardKitty = player.Passives.HardKittyDoebatsya;
                        foreach (var target in game.PlayersList)
                        {
                            if (player.GetPlayerId() == target.GetPlayerId()) continue;
                            var found = hardKitty.LostSeriesCurrent.Find(x => x.EnemyPlayerId == target.GetPlayerId());

                            if (found != null)
                                found.Series++;
                            else
                                hardKitty.LostSeriesCurrent.Add(new HardKitty.DoebatsyaSubClass(target.GetPlayerId()));
                        }
                    }

                    break;

                case "Не повезло":
                    var darksciType = player.Passives.DarksciTypeList;

                    if (darksciType.IsStableType)
                    {
                        player.GameCharacter.AddExtraSkill(20, "Не повезло");
                        player.GameCharacter.AddMoral(2, "Не повезло");
                    }

                    break;

                case "Дизмораль":

                    if (game.RoundNo == 9)
                    {
                        //Дизмораль Part #1
                        game.Phrases.DarksciDysmoral.SendLog(player, true);
                        game.AddGlobalLogs($"{player.DiscordUsername}: Всё, у меня горит!");
                        //end Дизмораль Part #2

                        // The −5 lands when the mandatory level-up is spent, and bots/auto-move spend it at
                        // the very END of the round (readiness pipeline), so a marker written at that moment
                        // is never readable: CalculateAllFights replaces GlobalLogs right after. When the
                        // freeze is already unavoidable, announce it here — this block is what everyone reads
                        // while choosing round-9 actions. Not a prediction: during the action phase only his
                        // own pending level-up can move Psyche, and it adds at most +1 (M162).
                        var dysmoralMaxPsycheGain =
                            player.GameCharacter.PsycheCappedAtZero || Cthulhu.IsHerald(game, player) ? 0 : 1;
                        if (player.GameCharacter.GetPsyche() + dysmoralMaxPsycheGain - 5 <= 0
                            && !game.GetGlobalLogs().Contains("Нахуй эту игру"))
                            game.AddGlobalLogs($"{player.DiscordUsername}: Нахуй эту игру..");
                    }


                    /*
                       _        _
                      ( `-.__.-' )
                       `-.    .-'
                          \  /
                           ||
                           ||
                          //\\
                         //  \\
                        ||    ||
                        ||____||
                        ||====||
                         \\  //
                          \\//
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           ||
                           []
                    */
                    //Да всё нахуй эту игру (3, 6 and 9 are in LVL up): Part #1
                    if (game.RoundNo != 9 && game.RoundNo != 7 && game.RoundNo != 5 && game.RoundNo != 3)
                        if (player.GameCharacter.GetPsyche() <= 0)
                        {
                            player.Status.IsSkip = true;
                            player.Status.IsBlock = false;
                            player.Status.IsReady = true;
                            player.Status.WhoToAttackThisTurn = new List<Guid>();
                            game.Phrases.DarksciFuckThisGame.SendLog(player, true);

                            // Per-ROUND guard (not per-game): the marker has to be present in every round it
                            // describes, so a round-9 write must not silence round 10 while he is still
                            // frozen — that is the round where players need it most (M162).
                            if (game.RoundNo == 10 && !game.GetGlobalLogs().Contains("Нахуй эту игру"))
                                game.AddGlobalLogs(
                                    $"{player.DiscordUsername}: Нахуй эту игру..");
                        }

                    //end Да всё нахуй эту игру: Part #1
                    break;


                case "Подсчет":
                    var tolya = player.Passives.TolyaCount;

                    tolya.Cooldown--;

                    if (tolya.Cooldown <= 0)
                    {
                        tolya.IsReadyToUse = true;
                        game.Phrases.TolyaCountReadyPhrase.SendLog(player, false);
                    }

                    if (tolya.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1))
                    {
                        var targetEntry = tolya.TargetList.Find(y => y.RoundNumber == game.RoundNo - 1);
                        var targetPlayer = targetEntry != null
                            ? game.PlayersList.Find(x => x.GetPlayerId() == targetEntry.Target)
                            : null;
                        if (targetPlayer != null)
                            player.Status.AddInGamePersonalLogs(
                                $"Подсчет: __Ставлю на то, что {targetPlayer.DiscordUsername} получит пизды!__\n");
                    }
                    break;

                case "Спокойствие":
                    var yongGleb = player.Passives.YongGlebTea;
                    yongGleb.Cooldown--;

                    if (yongGleb.Cooldown <= 0)
                    {
                        yongGleb.IsReadyToUse = true;
                        game.Phrases.YongGlebTeaReady.SendLog(player, true);
                    }
                    break;

                case "Тупорылая Акула":
                    if (player.GameCharacter.GetPsyche() == 10 && !player.IsBot())
                    {
                        player.Passives.AchievementTracker.TransformedFromMylorik = true;
                        player.GameCharacter.Name = "Братишка";
                        player.GameCharacter.Passive = new List<Passive>();
                        player.GameCharacter.Passive = _charactersPull.GetRollableCharacters().Find(x => x.Name == "Братишка").Passive;
                        player.Status.AddInGamePersonalLogs("Братишка: **Буууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууууль**\n");
                    }

                    break;

                case "Огурчик Рик":
                    if (player.Passives.RickPickle.PickleTurnsRemaining > 0)
                    {
                        player.Status.MoveListPage = 1;
                        player.Status.IsReady = true;
                    }
                    break;

                case "Гоблины":
                    // Auto-grow goblins each round
                    var gobEndPop = player.Passives.GoblinPopulation;
                    var autoGrowth = gobEndPop.GrowthThisRound;
                    gobEndPop.TotalGoblins += autoGrowth;
                    // Update persistent stat bonuses based on new population (D7: keeps external stat debuffs)
                    ApplyGoblinPopulationStats(player);
                    // Воины дают +10% Скилла каждый (delta от нового населения)
                    var gobEndWarriorSkillDelta = gobEndPop.Warriors - gobEndPop.AppliedWarriorSkillBonus;
                    if (gobEndWarriorSkillDelta != 0)
                        player.GameCharacter.AddIntelligenceQualitySkillBonus(gobEndWarriorSkillDelta, "Гоблины", true);
                    gobEndPop.AppliedWarriorSkillBonus = gobEndPop.Warriors;
                    player.Status.AddInGamePersonalLogs($"Гоблины: +{autoGrowth} прирост. Всего: {gobEndPop.TotalGoblins} (⚔️{gobEndPop.Warriors} 🧙{gobEndPop.Hobs} ⛏️{gobEndPop.Workers})\n");
                    break;

                case "Отличный рудник":
                    // Mine income moved to HandleEndOfRound (uses pre-sort position)
                    break;

                case "Гоблины тупые, но не идиоты":
                    var gobZigEnd = player.Passives.GoblinZiggurat;
                    var placeEnd = player.Status.GetPlaceAtLeaderBoard();

                    // Entering a built position arms exactly one future score-sort hold. Remaining
                    // on the same Ziggurat does not re-arm it; Goblins must leave and enter again.
                    if (gobZigEnd.BuiltPositions.Contains(placeEnd))
                    {
                        if (gobZigEnd.OccupiedZigguratPosition != placeEnd)
                        {
                            gobZigEnd.ZigguratStayRoundsLeft = 1;
                            gobZigEnd.IsInZiggurat = true;
                            if (game.RoundNo == 10 && placeEnd == 1)
                                gobZigEnd.TopPositionVisitedOnRoundTen = true;
                        }
                        gobZigEnd.OccupiedZigguratPosition = placeEnd;
                    }
                    else
                    {
                        gobZigEnd.IsInZiggurat = false;
                        gobZigEnd.OccupiedZigguratPosition = 0;
                        gobZigEnd.ZigguratStayRoundsLeft = 0;
                    }

                    // Occupancy bonuses remain active while Goblins stand on the built cell; only
                    // the position protection expires after its single action turn.
                    if (gobZigEnd.BuiltPositions.Contains(placeEnd))
                    {
                        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(1);
                        player.GameCharacter.AddMoral(5, "Зиккурат");
                    }
                    break;

                // (Geralt senses moved to HandleEndOfRound Медитация)

                // Котики — Рандомное поведение: bite bonus check
                case "Рандомное поведение":
                {
                    var rbBiteOwner = player.Passives.KotikiCatOwnerId != Guid.Empty
                        ? game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.KotikiCatOwnerId)
                        : player;
                    if (rbBiteOwner == null) rbBiteOwner = player;
                    var rbBite = rbBiteOwner.Passives.KotikiRandomBehavior;

                    if (rbBite.BiteBonusPending && rbBite.BiteLockActiveUntilRound < game.RoundNo)
                    {
                        var biteTarget = game.PlayersList.Find(x => x.GetPlayerId() == rbBite.BiteTargetId);
                        if (!Naruto.IsDispersedClone(player)
                            && biteTarget != null && rbBite.BiteLockPosition > 0 &&
                            biteTarget.Status.GetPlaceAtLeaderBoard() == rbBite.BiteLockPosition)
                        {
                            // Storm carrier gets +10 bonus points
                            player.Status.AddBonusPoints(10, "Кусь за жопу");
                            game.Phrases.KotikiStormBiteBonus.SendLog(player, false);
                        }

                        rbBite.BiteBonusPending = false;
                        rbBite.BiteTargetId = Guid.Empty;
                        rbBite.BiteLockPosition = -1;
                    }
                    break;
                }
                }
        }

        // Natural score sorting can also return Salldorum to the fixed cache cell.
        foreach (var player in game.PlayersList.Where(candidate =>
                     candidate.GameCharacter.Name == "Salldorum" && !candidate.Passives.IsDead))
            Salldorum.TryDrinkAvailableTimeCapsule(player, game);
        JonSnow.FinalizePositionEffects(game);
    }
    //end after all fight


    //predict bot
    public List<string> GetCharactersBasedOnClassAndRound(string characterClass, int round)
    {
        //Умный => Сильный => Быстрый
        var characters = new List<string>();
        switch (characterClass)
        {
            case "(**Умный** ?) ":
                characters = new List<string> { "DeepList", "Глеб", "LeCrisp", "Толя" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        characters = new List<string> { "DeepList", "Глеб", "Толя" };
                        break;
                    case 6:
                        characters = new List<string> { "DeepList", "Глеб", "Толя" };
                        break;
                    case 7:
                        characters = new List<string> { "DeepList", "Толя" };
                        break;
                    case 8:
                        characters = new List<string> { "DeepList", "Толя" };
                        break;
                }

                break;
            case "(**Сильный** ?) ":
                characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp" };
                        break;
                    case 6:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp" };
                        break;
                    case 7:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp",  "Глеб" };
                        break;
                    case 8:
                        characters = new List<string> { "HardKitty", "Тигр", "Загадочный Спартанец в маске", "LeCrisp", "Глеб" };
                        break;
                }

                break;
            case "(**Быстрый** ?) ":
                characters = new List<string> { "mylorik", "Осьминожка", "Darksci", "Братишка", "Краборак" };
                switch (round)
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                        break;
                }

                break;
        }

        characters.Add("Sirinoks");
        characters.Add("Злой Школьник");
        characters.Add("AWDKA");
        characters.Add("Вампур");
        characters.Add("Итачи");


        return characters;
    }

    /// <summary>
    /// Prediction AI for strict L2/L3 bots. This method deliberately accepts the live player objects only
    /// as public leaderboard identities: target id, username and place. Candidate characters come from the
    /// same visible prediction catalogue as the player UI; target GameCharacter/Passives/action flags are
    /// never consulted. Exact entries are retained only when a player-facing reveal created them.
    /// </summary>
    private void HandleFairBotPredict(GamePlayerBridgeClass player, GameClass game, int effectiveDifficulty)
    {
        var targets = game.PlayersList
            .Where(target => target.GetPlayerId() != player.GetPlayerId())
            .ToList();
        var catalog = GetFairPredictionCatalog()
            .Where(character => !Sakura.Is(character.Name) && !UnknownBug.Is(character.Name))
            .Where(character => game.Teams.Count > 0 || !character.TeamModeOnly)
            .GroupBy(character => character.Name)
            .Select(group => group.First())
            .ToList();
        if (catalog.Count == 0 || targets.Count == 0)
            return;

        // Include the current player projection directly as well: a round with no FightEntry rows can still
        // contain a public reveal, and CaptureVisibleRound intentionally has no combat snapshot to retain.
        var visibleHistory = BotInformation.VisibleGlobalHistory(player) + "\n"
                             + BotInformation.VisibleCurrentGlobalLogs(player, game);
        var splitPersonalLogs = player.Status.InGamePersonalLogsAll.Split("|||", StringSplitOptions.None);
        var lastRoundPersonal = splitPersonalLogs.Length > 1 && game.RoundNo > 1
            ? splitPersonalLogs[^2]
            : "";
        var currentPersonal = player.Status.GetInGamePersonalLogs();

        SeedFairExactPredictions(player, game, targets, catalog, visibleHistory);

        var exactByTarget = targets
            .Select(target => (Target: target, Evidence: BotInformation.PredictionFor(player, target.GetPlayerId())))
            .Where(pair => pair.Evidence is { IsExactReveal: true })
            .ToDictionary(pair => pair.Target.GetPlayerId(), pair => pair.Evidence!.CharacterName);
        var knownRosterNames = exactByTarget.Values
            .Append(player.GameCharacter.Name)
            .ToHashSet(StringComparer.Ordinal);

        var candidates = catalog
            .Where(character => character.Name != player.GameCharacter.Name)
            .Where(character => !knownRosterNames.Contains(character.Name))
            .ToList();
        ApplyFairRosterConstraints(candidates, catalog, knownRosterNames);
        if (candidates.Count == 0)
            candidates = catalog.Where(character => character.Name != player.GameCharacter.Name).ToList();

        var unresolvedTargets = targets
            .Where(target => !exactByTarget.ContainsKey(target.GetPlayerId()))
            .ToList();
        if (effectiveDifficulty == 2)
        {
            foreach (var target in unresolvedTargets)
            {
                var scored = candidates
                    .Select(candidate => (Candidate: candidate, Score: FairPredictionScore(
                        player, target, candidate, game, false, lastRoundPersonal, currentPersonal,
                        visibleHistory)))
                    .OrderByDescending(choice => choice.Score)
                    .ToList();
                if (scored.Count == 0)
                    continue;

                var oldEvidence = BotInformation.PredictionFor(player, target.GetPlayerId());
                CharacterClass selected;
                int confidence;
                string evidence;
                if (scored[0].Score < 45
                    && oldEvidence != null
                    && candidates.Any(candidate => candidate.Name == oldEvidence.CharacterName))
                {
                    selected = candidates.First(candidate => candidate.Name == oldEvidence.CharacterName);
                    confidence = Math.Max(25, oldEvidence.Confidence);
                    evidence = oldEvidence.Evidence;
                }
                else if (scored[0].Score < 45)
                {
                    selected = PickFairPredictionByPrior(candidates);
                    confidence = 25;
                    evidence = "visible character-catalogue prior";
                }
                else
                {
                    var tied = scored.Where(choice => choice.Score == scored[0].Score).ToList();
                    var selectedChoice = oldEvidence != null
                                         && tied.Any(choice => choice.Candidate.Name == oldEvidence.CharacterName)
                        ? tied.First(choice => choice.Candidate.Name == oldEvidence.CharacterName)
                        : tied[_rand.Random(0, tied.Count - 1)];
                    selected = selectedChoice.Candidate;
                    var secondScore = scored.Count > 1 ? scored[1].Score : 0;
                    confidence = Math.Clamp(40 + (selectedChoice.Score - secondScore) / 2, 40, 85);
                    evidence = "earned class, own-fight or visible-log evidence";
                }

                RecordFairPredictionChoice(player, target.GetPlayerId(), selected.Name,
                    confidence, evidence, game.RoundNo);
            }

            return;
        }

        // L3 combines the same legal evidence with roster rules. Assign the most constrained target first,
        // then remove that character from the remaining public pool. This is inference, not a roster read.
        var available = candidates.ToList();
        var pending = unresolvedTargets.ToList();
        while (pending.Count > 0 && available.Count > 0)
        {
            var targetChoices = pending.Select(target =>
            {
                var ranked = available
                    .Select(candidate => (Candidate: candidate, Score: FairPredictionScore(
                        player, target, candidate, game, true, lastRoundPersonal, currentPersonal,
                        visibleHistory)))
                    .OrderByDescending(choice => choice.Score)
                    .ToList();
                var margin = ranked[0].Score - (ranked.Count > 1 ? ranked[1].Score : 0);
                return (Target: target, Ranked: ranked, Margin: margin);
            }).OrderByDescending(choice => choice.Margin)
              .ThenByDescending(choice => choice.Ranked[0].Score)
              .ThenBy(choice => choice.Target.Status.GetPlaceAtLeaderBoard())
              .First();

            var best = targetChoices.Ranked[0];
            var confidence = Math.Clamp(35 + best.Score / 3 + targetChoices.Margin / 2, 35, 92);
            RecordFairPredictionChoice(player, targetChoices.Target.GetPlayerId(), best.Candidate.Name,
                confidence, "accumulated public evidence plus all-different roster inference", game.RoundNo);

            pending.Remove(targetChoices.Target);
            available.RemoveAll(candidate => candidate.Name == best.Candidate.Name);
        }
    }

    private List<CharacterClass> GetFairPredictionCatalog()
    {
        // CharactersPull deserializes characters.json on every call. Prediction runs once per bot per
        // round and simulations run many games concurrently, so retain the immutable public definitions.
        // Every consumer below creates its own filtered list before applying roster constraints.
        if (_fairPredictionCatalog != null)
            return _fairPredictionCatalog;
        lock (_fairPredictionCatalogLock)
        {
            _fairPredictionCatalog ??= _charactersPull.GetVisibleCharacters();
            return _fairPredictionCatalog;
        }
    }

    private void SeedFairExactPredictions(
        GamePlayerBridgeClass player,
        GameClass game,
        List<GamePlayerBridgeClass> targets,
        List<CharacterClass> catalog,
        string visibleHistory)
    {
        // Public Толя reveals include both the username and exact catalogue value.
        foreach (var target in targets)
        foreach (var character in catalog)
        {
            if (!visibleHistory.Contains(
                    $"Толя запизделся и спалил, что {target.DiscordUsername} - {character.Name}",
                    StringComparison.Ordinal))
                continue;
            BotInformation.RecordPrediction(player, target.GetPlayerId(), character.Name, 100,
                "public Толя reveal", game.RoundNo, true);
        }

        // Сверхразум and Naruto sibling setup write the exact value into the owner's own prediction slot.
        // The id lists are private owner-visible state; no opponent state is dereferenced here.
        var ownerRevealIds = new HashSet<Guid>();
        if (player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Сверхразум"))
            ownerRevealIds.UnionWith(player.Passives.DeepListSupermindKnown.KnownPlayers);
        if (Naruto.IsNaruto(player))
            ownerRevealIds.UnionWith(player.Passives.Naruto.NarutoPlayerIds
                .Where(id => id != player.GetPlayerId()));

        foreach (var targetId in ownerRevealIds)
        {
            var existing = player.Predict.Find(prediction => prediction.PlayerId == targetId);
            if (existing == null || catalog.All(character => character.Name != existing.CharacterName))
                continue;
            BotInformation.RecordPrediction(player, targetId, existing.CharacterName, 100,
                "owner-visible exact reveal", game.RoundNo, true);
        }

        // Коммуникация exposes one catalogue name globally and the UI exposes its Pink-Ward target id.
        // Толя targets above are already resolved, so a single remaining id/name pair is unambiguous.
        var unresolvedPinkTargets = targets.Where(target =>
                game.PinkWardRevealedPlayerIds.Contains(target.GetPlayerId())
                && BotInformation.PredictionFor(player, target.GetPlayerId()) is not { IsExactReveal: true })
            .ToList();
        var communicationNames = visibleHistory.Split('\n')
            .Where(line => line.Contains("Пиквард просветил ", StringComparison.Ordinal))
            .SelectMany(line => catalog.Where(character =>
                line.Contains($"Пиквард просветил {character.Name}", StringComparison.Ordinal)))
            .Select(character => character.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unresolvedPinkTargets.Count == 1 && communicationNames.Count == 1)
            BotInformation.RecordPrediction(player, unresolvedPinkTargets[0].GetPlayerId(),
                communicationNames[0], 100, "public Коммуникация reveal", game.RoundNo, true);
    }

    private static void ApplyFairRosterConstraints(
        List<CharacterClass> candidates,
        List<CharacterClass> catalog,
        HashSet<string> knownRosterNames)
    {
        if (knownRosterNames.Contains("LeCrisp"))
            candidates.RemoveAll(character => character.Name == "Толя");
        if (knownRosterNames.Contains("Толя"))
            candidates.RemoveAll(character => character.Name == "LeCrisp");
        if (knownRosterNames.Contains("HardKitty"))
            candidates.RemoveAll(character => character.Name == ErenYeager.CharacterName);
        if (knownRosterNames.Contains(ErenYeager.CharacterName))
            candidates.RemoveAll(character => character.Name == "HardKitty");

        var knownTierFour = catalog.Any(character =>
            character.Tier == 4 && knownRosterNames.Contains(character.Name));
        if (knownTierFour)
            candidates.RemoveAll(character => character.Tier == 4);
    }

    private CharacterClass PickFairPredictionByPrior(List<CharacterClass> candidates)
    {
        // Монстр is a legal catalogue hypothesis but never a useful value to submit: it cannot score and
        // submitting any value against the real Монстр feeds his punish. Only strong reveal-failure evidence
        // below makes the bot deliberately leave a target blank.
        var priorCandidates = candidates.Where(candidate => candidate.Name != "Монстр без имени").ToList();
        if (priorCandidates.Count == 0)
            priorCandidates = candidates;
        var total = priorCandidates.Sum(FairPredictionPrior);
        var roll = _rand.Random(1, Math.Max(1, total));
        foreach (var candidate in priorCandidates)
        {
            roll -= FairPredictionPrior(candidate);
            if (roll <= 0)
                return candidate;
        }

        return priorCandidates[^1];
    }

    private static void RecordFairPredictionChoice(
        GamePlayerBridgeClass player,
        Guid targetId,
        string characterName,
        int confidence,
        string evidence,
        int round)
    {
        if (characterName != "Монстр без имени")
        {
            BotInformation.RecordPrediction(player, targetId, characterName, confidence, evidence, round);
            return;
        }

        var old = BotInformation.PredictionFor(player, targetId);
        if (old != null && old.Confidence > confidence)
            return;
        player.AiKnowledge.PredictionEvidence[targetId] = new BotPredictionEvidence
        {
            CharacterName = characterName,
            Confidence = Math.Clamp(confidence, 0, 100),
            Evidence = evidence + "; abstain because Монстр cannot be scored",
            RoundUpdated = round,
        };
        player.Predict.RemoveAll(prediction => prediction.PlayerId == targetId);
    }

    private static int FairPredictionPrior(CharacterClass candidate) => candidate.Tier switch
    {
        >= 6 => 18,
        5 => 14,
        4 => 12,
        3 => 9,
        2 => 8,
        1 => 7,
        _ => 6,
    };

    private static int FairPredictionScore(
        GamePlayerBridgeClass player,
        GamePlayerBridgeClass target,
        CharacterClass candidate,
        GameClass game,
        bool advanced,
        string lastRoundPersonal,
        string currentPersonal,
        string visibleHistory)
    {
        var score = FairPredictionPrior(candidate);
        var targetId = target.GetPlayerId();
        var knownTell = player.Status.KnownPlayerClass.Find(known => known.EnemyId == targetId);
        var knownClass = ParseFairKnownClass(knownTell?.Text);
        if (knownClass != SkillClassType.None)
            score += candidate.GetSkillClassType() == knownClass ? 55 : -24;

        player.AiKnowledge.Opponents.TryGetValue(targetId, out var memory);
        if (memory != null && memory.LastObservedFightRound > 0)
        {
            var observedClass = ParseFairKnownClass(memory.LastObservedClass);
            if (observedClass != SkillClassType.None)
            {
                var age = Math.Max(0, game.RoundNo - memory.LastObservedFightRound);
                var classWeight = Math.Max(12, (advanced ? 48 : 34) - age * 4);
                score += candidate.GetSkillClassType() == observedClass ? classWeight : -classWeight / 3;
            }
        }

        var personalContext = lastRoundPersonal + "\n" + currentPersonal;
        var targetMentioned = personalContext.Contains(target.DiscordUsername, StringComparison.Ordinal);
        if (targetMentioned)
        {
            if (personalContext.Contains("Коммуникация: Не удалось просветить", StringComparison.Ordinal)
                && candidate.Name == "Монстр без имени")
                score += 80;
            if (personalContext.Contains("Ничего не понимает", StringComparison.Ordinal))
            {
                if (candidate.Name == "Братишка") score += 48;
                if (candidate.Name == DoomGuy.CharacterName) score += 20;
            }
            if (personalContext.Contains("Они позорят военное искусство", StringComparison.Ordinal)
                && candidate.Name == "Загадочный Спартанец в маске")
                score += 52;
            if (personalContext.Contains("Стёб", StringComparison.Ordinal) && candidate.Name == "DeepList")
                score += 45;
            if (personalContext.Contains("Панцирь", StringComparison.Ordinal) && candidate.Name == "Краборак")
                score += 38;
        }

        score += AwdkaFairPredictionScore(player, target, candidate, lastRoundPersonal);

        if (!advanced)
            return score;

        // IsBot is part of the public player DTO. L3 knows the natural strict-bot roll rule, but
        // treats it only as a prior because disconnected humans and admin-forced line-ups are exceptions.
        if (target.IsBot())
            score += candidate.Tier >= 4 || candidate.Name == "Кира" ? 20 : -10;

        if (memory != null)
        {
            var attacks = BotInformation.RecentAverage(memory.AttacksByRound, game.RoundNo, 6);
            if (attacks >= 1.5m && candidate.Name == "Dopa")
                score += 30;

            var defenseRate = BotInformation.DefenseRate(memory, game.RoundNo, 6);
            if (defenseRate >= 0.55m && candidate.Name is "Рик Санчез" or "Геральт" or "Salldorum")
                score += 8;
            if (defenseRate >= 0.55m && candidate.Name == DoomGuy.CharacterName)
                score += 12;
        }

        // These lines name their speaker in the public log. They are strong but still fallible tells,
        // because transferred passives and copied effects exist in the ruleset.
        if (visibleHistory.Contains($"{target.DiscordUsername}: Всё, у меня горит!", StringComparison.Ordinal)
            && candidate.Name == "Darksci")
            score += 72;
        if (visibleHistory.Contains($"{target.DiscordUsername}: ЕБАННЫЕ БАНЫ", StringComparison.Ordinal)
            && candidate.Name == "Darksci")
            score += 72;
        if (visibleHistory.Contains(
                $"Толя попытался что-то разузнать про {target.DiscordUsername}, но не удалось просветить",
                StringComparison.Ordinal)
            && candidate.Name == "Монстр без имени")
            score += 90;

        if (target.Status.GetPlaceAtLeaderBoard() == 6 && candidate.Name == "HardKitty")
            score += 7;

        var oldEvidence = BotInformation.PredictionFor(player, targetId);
        if (oldEvidence != null && oldEvidence.CharacterName == candidate.Name)
            score += Math.Min(10, oldEvidence.Confidence / 10);

        return score;
    }

    private static SkillClassType ParseFairKnownClass(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return SkillClassType.None;
        if (value.Contains("Умный", StringComparison.Ordinal)
            || value.Contains("Интеллект", StringComparison.Ordinal))
            return SkillClassType.Intelligence;
        if (value.Contains("Сильный", StringComparison.Ordinal)
            || value.Contains("Сила", StringComparison.Ordinal))
            return SkillClassType.Strength;
        if (value.Contains("Быстрый", StringComparison.Ordinal)
            || value.Contains("Скорость", StringComparison.Ordinal))
            return SkillClassType.Speed;
        return SkillClassType.None;
    }

    private static int AwdkaFairPredictionScore(
        GamePlayerBridgeClass player,
        GamePlayerBridgeClass target,
        CharacterClass candidate,
        string lastRoundPersonal)
    {
        if (player.GameCharacter.Name != "AWDKA"
            || !lastRoundPersonal.Contains(target.DiscordUsername, StringComparison.Ordinal))
            return 0;

        var bonus = 0;
        if (player.GameCharacter.GetIntelligenceString().Contains(":volibir:", StringComparison.Ordinal))
        {
            bonus += candidate.Name switch
            {
                "DeepList" when player.GameCharacter.GetIntelligence() == 10 => 65,
                "Злой Школьник" when player.GameCharacter.GetIntelligence() == 9 => 65,
                "Толя" when player.GameCharacter.GetIntelligence() == 8 => 65,
                "Вампур" when player.GameCharacter.GetIntelligence() == 6 => 65,
                "Sirinoks" when player.GameCharacter.GetIntelligence() == 5 => 65,
                _ => 0,
            };
        }
        if (player.GameCharacter.GetStrengthString().Contains(":volibir:", StringComparison.Ordinal))
        {
            bonus += candidate.Name switch
            {
                "Загадочный Спартанец в маске" when player.GameCharacter.GetStrength() == 10 => 65,
                "Тигр" when player.GameCharacter.GetStrength() == 9 => 65,
                _ => 0,
            };
        }
        if (player.GameCharacter.GetSpeedString().Contains(":volibir:", StringComparison.Ordinal))
        {
            bonus += candidate.Name switch
            {
                "Краборак" when player.GameCharacter.GetSpeed() == 10 => 65,
                "mylorik" when player.GameCharacter.GetSpeed() == 9 => 65,
                "Darksci" when player.GameCharacter.GetSpeed() == 8 => 65,
                _ => 0,
            };
        }
        if (player.GameCharacter.GetPsycheString().Contains(":volibir:", StringComparison.Ordinal))
        {
            bonus += candidate.Name switch
            {
                "Осьминожка" when player.GameCharacter.GetPsyche() == 10 => 65,
                "Краборак" when player.GameCharacter.GetPsyche() == 9 => 65,
                "HardKitty" when player.GameCharacter.GetPsyche() == 8
                                  && target.Status.GetPlaceAtLeaderBoard() == 6 => 65,
                "Глеб" when player.GameCharacter.GetPsyche() == 8 => 55,
                "LeCrisp" when player.GameCharacter.GetPsyche() == 7 => 65,
                _ => 0,
            };
        }

        return bonus;
    }

    [SuppressMessage("ReSharper", "UnusedVariable")]
    public void HandleBotPredict(GameClass game)
    {
        //
        foreach (var player in game.PlayersList)
            try
            {
                if (!player.IsBot()) continue;
                if (player.Passives.IsDead) continue;
                // IsBot() also includes disconnected humans. Only strict bots get autonomous AI
                // knowledge, and their L2/L3 memory is populated through the player-visible boundary.
                var effAiDifficulty = player.AiDifficulty >= 0 ? player.AiDifficulty : game.AiDifficulty;
                if (player.PlayerType == 404 && effAiDifficulty >= 2)
                    BotInformation.CaptureVisibleRound(player, game);
                // This exact scripted row is installed before inference so fair L2/L3 roster logic treats
                // Madara as already resolved, then reasserted in finally over every legacy/exact-reveal path.
                Madara.EnforcePostRoundSevenBotPrediction(player, game);
                if (game.RoundNo >= 9) continue;
                // Kira uses Death Note, not predictions
                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Тетрадь смерти")) continue;
                if (Madara.IsMadara(player))
                {
                    player.Predict.Clear();
                    player.Status.ConfirmedPredict = true;
                    continue;
                }

                // Булькает: no predictions at all. Existing entries are kept on purpose — a Goblin whose
                // Ziggurat learned the passive made them legitimately before it had the passive.
                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Булькает"))
                {
                    player.Status.ConfirmedPredict = true;
                    continue;
                }

                if (player.PlayerType == 404 && effAiDifficulty >= 2)
                {
                    HandleFairBotPredict(player, game, effAiDifficulty);
                    continue;
                }

                var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

                var lastRoundEvents = "";
                if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
                    lastRoundEvents = splitLogs[^2];
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var globalLogs = game.GetGlobalLogs();
                var leaderboard = _gameUpdateMess.LeaderBoard(player);
                var knownCLass = player.Status.KnownPlayerClass;


                switch (player.GameCharacter.Name)
                {
                    case "AWDKA":
                        try
                        {
                            if (lastRoundEvents.Contains("напал на игрока"))
                            {
                                var playerName = lastRoundEvents.Split("напал на игрока")[1].Split("\n")[0].TrimStart();
                                var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                                if (player.GameCharacter.GetIntelligenceString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetIntelligenceString()
                                        .Replace("Интеллект ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("DeepList",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Злой Школьник",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Толя", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(
                                                    new PredictClass("Вампур", playerClass.GetPlayerId()));
                                            break;
                                        case 5:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Sirinoks", playerClass.GetPlayerId()));
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetStrengthString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetStrengthString()
                                        .Replace("Сила ", "")
                                        .Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Загадочный Спартанец в маске",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Тигр", playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            break;
                                        case 5:
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetSpeedString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetSpeedString()
                                        .Replace("Скорость ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("mylorik",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Darksci",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            break;
                                        case 6:
                                            break;
                                        case 5:
                                            break;
                                    }
                                }

                                if (player.GameCharacter.GetPsycheString().Contains(":volibir:"))
                                {
                                    var stat = Convert.ToInt32(player.GameCharacter.GetPsycheString()
                                        .Replace("Психика ", "").Split(" (")[0]);
                                    switch (stat)
                                    {
                                        case 10:

                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Осьминожка",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 9:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Краборак",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 8:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()) &&
                                                playerClass.Status.GetPlaceAtLeaderBoard() == 6)
                                                player.Predict.Add(new PredictClass("HardKitty",
                                                    playerClass.GetPlayerId()));
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("Глеб", playerClass.GetPlayerId()));
                                            break;
                                        case 7:
                                            if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()))
                                                player.Predict.Add(new PredictClass("LeCrisp",
                                                    playerClass.GetPlayerId()));
                                            break;
                                        case 6:

                                            break;
                                        case 5:
                                            break;
                                    }
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            _log.Critical(exception.Message);
                            _log.Critical(exception.StackTrace);
                        }

                        break;
                    case "DeepList":
                        var deepList = player.Passives.DeepListSupermindKnown;

                        if (deepList != null)
                            foreach (var knownPlayer in deepList.KnownPlayers)
                            {
                                var playerClass = game.PlayersList.Find(x => x.GetPlayerId() == knownPlayer);
                                if (UnknownBug.Is(playerClass) || Sakura.Is(playerClass)) continue;

                                if (player.Predict.All(x => x.PlayerId != playerClass!.GetPlayerId()) &&
                                    playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass(playerClass.GameCharacter.Name,
                                        playerClass.GetPlayerId()));
                            }

                        break;
                }

                //game.AddGlobalLogs($"Толя запизделся и спалил, что {randomPlayer.DiscordUsername} - {randomPlayer.GameCharacter.Name}");
                //100%
                try
                {
                    if (globalLogs.Contains("Толя запизделся"))
                    {
                        var playerName =
                            globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^2];
                        var playerCharacter =
                            globalLogs.Split("запизделся и спалил")[1].Replace(", что ", "").Split(" - ")[^1]
                                .Replace("\n", "");
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                        if (playerClass.GetPlayerId() != player.GetPlayerId())
                        {
                            if (player.Predict.Any(x => x.PlayerId == playerClass.GetPlayerId()))
                                player.Predict.Remove(player.Predict.Find(x =>
                                    x.PlayerId == playerClass.GetPlayerId()));
                            player.Predict.Add(new PredictClass(playerCharacter, playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }

                //100%
                try
                {
                    if (lastRoundEvents.Contains("Ничего не понимает"))
                    {
                        var playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                        var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                        if (player.Predict.Any(x => x.PlayerId == playerClass!.GetPlayerId()))
                            player.Predict.Remove(player.Predict.Find(x => x.PlayerId == playerClass!.GetPlayerId()));
                        player.Predict.Add(new PredictClass("Братишка", playerClass.GetPlayerId()));
                    }
                }
                catch
                {
                    //ignored
                }

                //not 100%
                try
                {
                    if (lastRoundEvents.Contains("Они позорят военное искусство"))
                    {
                        var removedTimes = 0;
                        foreach (var line in globalLogs.Split("\n"))
                        {
                            if (!line.Contains("⟶")) continue;
                            if (!line.Contains(player.DiscordUsername)) continue;
                            string playerName;
                            if (lastRoundEvents.Contains(" напал на игрока "))
                            {
                                playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                                if (line.Contains(playerName) && removedTimes == 0)
                                {
                                    removedTimes++;
                                    continue;
                                }
                            }

                            playerName = line.Split("  ⟶")[0].Replace($"{player.DiscordUsername}  ", "")
                                .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "")
                                .Trim();
                            var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);
                            if (playerClass != null)
                                if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) &&
                                    playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass("Загадочный Спартанец в маске", playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }


                //not 100%
                try
                {
                    if (lastRoundEvents.Contains("Стёб"))
                    {
                        var removedTimes = 0;
                        foreach (var line in globalLogs.Split("\n"))
                        {
                            if (!line.Contains("⟶")) continue;
                            if (!line.Contains(player.DiscordUsername)) continue;
                            string playerName;
                            if (lastRoundEvents.Contains(" напал на игрока "))
                            {
                                playerName = lastRoundEvents.Split(" напал на игрока ")[1].Split("\n")[0];
                                if (line.Contains(playerName) && removedTimes == 0)
                                {
                                    removedTimes++;
                                    continue;
                                }
                            }

                            playerName = line.Split("  ⟶")[0].Replace($"{player.DiscordUsername}  ", "")
                                .Replace($" {player.DiscordUsername}", "").Replace("<:war:561287719838547981>", "")
                                .Trim();
                            var playerClass = game.PlayersList.Find(x => x.DiscordUsername == playerName);

                            if(playerClass != null)
                                if (player.Predict.All(x => x.PlayerId != playerClass.GetPlayerId()) && playerClass.GetPlayerId() != player.GetPlayerId())
                                    player.Predict.Add(new PredictClass("DeepList", playerClass.GetPlayerId()));
                        }
                    }
                }
                catch
                {
                    //ignored
                }
            }
            catch (Exception exception)
            {
                _log.Critical(exception.Message);
                _log.Critical(exception.StackTrace);
            }
            finally
            {
                try
                {
                    Sakura.RemoveForbiddenPredictions(game, player);
                }
                finally
                {
                    Dopa.ReassertMacroPrediction(player);
                    EnforceMonsterWrongPrediction(player, game);
                    Madara.EnforcePostRoundSevenBotPrediction(player, game);
                }
            }
    }
    //end predict bot

    private void EnforceMonsterWrongPrediction(
        GamePlayerBridgeClass bot,
        GameClass game)
    {
        if (bot?.PlayerType != 404
            || bot.Passives.IsDead
            || bot.GameCharacter.DoomRollMode
            || Madara.IsMadara(bot)
            || bot.GameCharacter.Passive.Any(passive =>
                passive.PassiveName is "Тетрадь смерти" or "AdminPlayerType" or "Булькает"))
            return;

        var monster = game.PlayersList.FirstOrDefault(player =>
            player.GetPlayerId() != bot.GetPlayerId()
            && !player.Passives.IsDead
            && player.GameCharacter.Name == MonsterWithoutName.CharacterName
            && player.GameCharacter.Passive.Any(passive =>
                passive.PassiveName == "Выдуманный персонаж"));
        if (monster == null) return;

        var wrongCandidates = GetFairPredictionCatalog()
            .Where(character =>
                character.Name != MonsterWithoutName.CharacterName
                && character.Name != "Sakura"
                && (!character.TeamModeOnly || game.Teams.Count > 0))
            .Select(character => character.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (wrongCandidates.Count == 0) return;

        var wrongPrediction = wrongCandidates[
            _rand.Random(0, wrongCandidates.Count - 1)];
        bot.Predict.RemoveAll(prediction =>
            prediction.PlayerId == monster.GetPlayerId());
        bot.Predict.Add(new PredictClass(
            wrongPrediction,
            monster.GetPlayerId()));
        bot.Status.ConfirmedPredict = true;
    }


    //unique
    public void HandleShark(GameClass game)
    {
        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Лежит на дне":
                    var enemyTop = game.PlayersList.Find(x =>
                        x.Status.GetPlaceAtLeaderBoard() - 1 == player.Status.GetPlaceAtLeaderBoard());
                    var enemyBottom = game.PlayersList.Find(x =>
                        x.Status.GetPlaceAtLeaderBoard() + 1 == player.Status.GetPlaceAtLeaderBoard());
                    if (enemyTop != null && enemyTop.Status.IsLostThisCalculation != Guid.Empty)
                        player.Status.AddRegularPoints(1, "Лежит на дне");

                    if (enemyBottom != null && enemyBottom.Status.IsLostThisCalculation != Guid.Empty)
                        player.Status.AddRegularPoints(1, "Лежит на дне");
                    break;
            }
    }

    public async Task<(int Point, List<Guid> Recipients)> HandleJews(
        GamePlayerBridgeClass me,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        // unknown_bug has no base victory point to steal; do not mint one for a Jew or
        // expose a fake recipient to delayed score ledgers.
        if (UnknownBug.Is(me))
            return (1, new List<Guid>());

        var jews = new List<GamePlayerBridgeClass>();
        var creditedRecipients = new List<Guid>();
        var toReturn = 1;

        if (me.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей")
            || ScamRat.HasSharingPassive(me)
            || me.GameCharacter.Passive.Any(x => x.PassiveName == "Вступить в союз"))
        {
            return (toReturn, new List<Guid> { me.GetPlayerId() });
        }

        foreach (var player in game.PlayersList)
        foreach (var passive in player.GameCharacter.Passive.ToList())
            switch (passive.PassiveName)
            {
                case "Еврей":
                    if (player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())
                        && !jews.Contains(player))
                        jews.Add(player);
                    break;
                case ScamRat.SharingPassiveName:
                    if (player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())
                        && ScamRat.CanCarryJointWin(player, target, game)
                        && !jews.Contains(player))
                        jews.Add(player);
                    break;
            }

        switch (jews.Count)
        {
            case 0:
                return (toReturn, new List<Guid> { me.GetPlayerId() });
            default:
                //1 jews or more!
                foreach (var jew in jews)
                {
                    if (me.GameCharacter.Name == "DeepList" && jew.GameCharacter.Name == "LeCrisp")
                    {
                        game.Phrases.LeCrispBoolingPhrase.SendLog(jew, false);
                        continue;
                    }

                    var isScamRatCarry = ScamRat.UsesCarryShop(jew);
                    var pointSource = isScamRatCarry
                        ? ScamRat.SharingPhraseSource
                        : "Еврей";
                    jew.Status.AddRegularPoints(1, pointSource, isNaturalWin: true);
                    creditedRecipients.Add(jew.GetPlayerId());
                    if (isScamRatCarry)
                    {
                        ScamRat.RecordCarryStolenWin(jew, game);
                    }
                    else
                    {
                        switch (jew.GameCharacter.Name)
                        {
                            case "Толя":
                                game.Phrases.TolyaJewPhrase.SendLog(jew, true);
                                break;

                            case "LeCrisp":
                                game.Phrases.LeCrispJewPhrase.SendLog(jew, true);
                                break;

                            default:
                                foreach (var player in game.PlayersList)
                                    switch (player.GameCharacter.Name)
                                    {
                                        case "Толя":
                                            game.Phrases.TolyaJewPhrase.SendLog(jew, true);
                                            break;

                                        case "LeCrisp":
                                            game.Phrases.LeCrispJewPhrase.SendLog(jew, true);
                                            break;
                                    }

                                break;
                        }
                    }

                    if (jews.Count > 1 && !jew.IsBot())
                        try
                        {
                            await _help.SendMsgAndDeleteItAfterRound(jew, "__**МЫ**__ жрём деньги!", 10000);
                        }
                        catch (Exception exception)
                        {
                            _log.Critical(exception.Message);
                            _log.Critical(exception.StackTrace);
                        }

                    toReturn = 0;
                }

                break;
        }

        return (toReturn, toReturn == 0
            ? creditedRecipients
            : new List<Guid> { me.GetPlayerId() });
    }

    public async Task<int> HandleOctopus(GamePlayerBridgeClass octopus, GamePlayerBridgeClass attacker, GameClass game)
    {
        if (octopus.GameCharacter.Passive.All(x => x.PassiveName != "Чернильная завеса")) return 0;

        //Сомнительная тактика
        if (attacker.GameCharacter.Passive.Any(x => x.PassiveName == "Сомнительная тактика"))
        {
            var deepListDoubtfulTactic = attacker.Passives.DeepListDoubtfulTactic;

            if (deepListDoubtfulTactic != null)
                if (!deepListDoubtfulTactic.FriendList.Contains(octopus.GetPlayerId()))
                    return 0;
        }
        //end Сомнительная тактика

        if (UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, attacker))) return 1;


        var enemyIds = new List<Guid> { attacker.GetPlayerId() };

        //jew
        var (point, recipients) = await HandleJews(attacker, octopus, game);

        if (point == 0)
            enemyIds = recipients;
        //end jew

        enemyIds = enemyIds.Where(enemyId =>
        {
            var scoreRecipient = game.PlayersList.Find(player => player.GetPlayerId() == enemyId);
            return scoreRecipient == null
                   || !UnknownBug.Is(Naruto.ResolveScoreSuccessor(game, scoreRecipient));
        }).ToList();

        foreach (var enemyId in enemyIds)
        {
            var octopusInkList = octopus.Passives.OctopusInkList;


            var enemyRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == enemyId);
            var octopusRealScore = octopusInkList.RealScoreList.Find(x => x.PlayerId == octopus.GetPlayerId());

            if (octopusRealScore == null)
            {
                octopus.Passives.OctopusInkList.RealScoreList.Add(new Octopus.InkSubClass(enemyId, game.RoundNo, -1));
                octopus.Passives.OctopusInkList.RealScoreList.Add(new Octopus.InkSubClass(octopus.GetPlayerId(),
                    game.RoundNo, 1));
            }
            else
            {
                if (enemyRealScore == null)
                {
                    octopusInkList.RealScoreList.Add(new Octopus.InkSubClass(enemyId, game.RoundNo, -1));
                    octopusRealScore.AddRealScore(game.RoundNo);
                }
                else
                {
                    enemyRealScore.AddRealScore(game.RoundNo, -1);
                    octopusRealScore.AddRealScore(game.RoundNo);
                }
            }
        }

        return 1;
    }

    // TheBoys — M.M. kompromat hints per character
    private string GetKompromatHint(GamePlayerBridgeClass target, GameClass game)
    {
        var characterName = target.GameCharacter.Name;
        var highInTable = target.Status.GetPlaceAtLeaderBoard() < 4;
        List<string> suppliedHints = characterName switch
        {
            "DeepList" => highInTable
                ? new() { "Йо, этот безумец явно что-то замышляет!" }
                : new() { "Он просто сидит и стебётся над всеми. Никакой угрозы." },
            "mylorik" => highInTable
                ? new() { "Этот чувак явно повернут на мести... Мы можем это использовать?" }
                : new() { "Я читал о нем в новостях: \"Человек был найден утопленным\". Тогда почему он всё еще жив!?" },
            "Глеб" when target.Passives.GlebChallengerList.RoundItTriggered == game.RoundNo
                            || target.Passives.GlebChallengerTriggeredWhen.WhenToTrigger.Contains(game.RoundNo)
                => new() { "Он слишком опасен! Да я вижу, что он старик! Но у него все статы на 9!!! Валим нахер!" },
            "Глеб" => highInTable
                ? new() { "Нет! Не бери его cup of chay! В него что-то... намешано!" }
                : new()
                {
                    "Старикан явно скрывает свое прошлое... Похоже, когда-то он был звездой.",
                    "Просто спящий старпёр. Думаешь, он что-то затевает?",
                    "Этот дед официально числится спящим с 2018 года.",
                },
            "LeCrisp" => highInTable
                ? new() { "Участник перестрелки. Лицензии на оружие нихера. Тогда откуда он высрал винтовку?" }
                : new()
                {
                    "Нихера не пойму... Гражданство израиля есть, а ведет себя как нищий. Видать, арабские ассассины виноваты...",
                    "Нарыл. Он оформил страховку против \"булинга\". Походу не помогло.",
                },
            "Толя" => highInTable
                ? new()
                {
                    "Меня ЗАЕБАЛИ его разговоры. Чур в следующией раз Хьюи на прослушке.",
                    "Сидит на жопе ровно, но деньги плывут только так. Просчетливый, сука.",
                }
                : new() { "Этот круглик - просто непробиваемая голова." },
            "HardKitty" => new()
            {
                "С ним аккуратно... Он сам неприметный, но у него тысячи друзей.",
                "Фу блять, знал я одного любителя молока.",
            },
            "Sirinoks" => highInTable
                ? new() { "Чувак, я в ахере. Смотри на нее! С одной стороны девка, с другой..." }
                : new() { "Обычная девушка, заводит друзей, никакой активности." },
            "Злой Школьник" => highInTable
                ? new() { "Он деградирует как настоящий царь горы, если бы та состояла из мусора." }
                : new() { "Этот лох нам не опасен. Залупается, но не кусается." },
            "AWDKA" => highInTable
                ? new() { "Не, к нему я не полезу. У этого извращенца фетиш, ты знаешь на что? На медведей. Ты вдумайся." }
                : new() { "Что за хач? Написано, \"мечтает стать pro\". Это троллинг какой-то?" },
            "Осьминожка" => highInTable
                ? new() { "Щупальца?! Ну нахер, я не япошка." }
                : new()
                {
                    "Мелкий моллюск. Думаешь, он в чем-то замешан?",
                    "Он притворяется нищим, но толкает свои чернила на черночернильном рынке. Ублюдок.",
                },
            "Darksci" => highInTable
                ? new()
                {
                    "Находится в черных списках всех казино. Однажды он разозлился и разъебал крупье одним лишь криком. Походу супер...",
                    "Я накопал, что у этого перца долг в казино.",
                }
                : new() { "Невезучий говнюк, проебал все деньги на ставках. Используем?" },
            "Тигр" => highInTable
                ? new() { "Бля, чел. У этого пидора стояк на золотой дождь. С меня хватит, я ухожу!" }
                : new() { "Этот неадекват состоит в клане. Да блять, как добланный ниндзя. Причем настолько древнем, что в нем осталось всего три засранца." },
            "Братишка" => highInTable
                ? new()
                {
                    "Выглядит акулой, но знаешь что? Я видел, как в этого шалуна засовывают плюш.",
                    "Лучше не лезть к этой акуле бизнеса. Хер оттяпает. Будет короче на -1.\nFrancie: \"У тебя хер и так уже -1\"",
                }
                : new()
                {
                    "По документам... Он на дне. Причем уже давно.",
                    "Нихуя я не нашел. Пришлось нырять на дно морское. Я тебе не Гидрант.",
                },
            _ => null,
        };

        if (suppliedHints != null)
            return suppliedHints[_rand.Random(0, suppliedHints.Count - 1)];

        var existingHints = new Dictionary<string, List<string>>
        {
            ["Кратос"] = new() { "Этот парень явно любит кого-то душить. Вместе с его проблемами.", "Бородатый мужик с цепями. Очень злой." },
            ["Стая Гоблинов"] = new() { "Судя по запаху, целей как минимум двадцать.", "Они строят что-то подозрительное." },
            ["Котики"] = new() { "Повсюду шерсть... и подозрительное мурчание.", "Один из них точно невиновен. Другой — нет." },
            ["Vampyr"] = new() { "Следы укусов на шее. Чесноком не пахнет.", "Подозреваемый избегает солнечного света." },
            ["Загадочный Спартанец в маске"] = new() { "У цели обнаружен комплекс бога. И копьё.", "THIS. IS... подозрительно." },
            ["Weedwick"] = new() { "Цель пахнет... травами. Лечебными, конечно.", "Подозреваемый подозрительно расслаблен." },
            ["Сайтама"] = new() { "Лысый. Один удар. Больше данных нет.", "Цель скучает. Это опасно." },
            ["Dopa"] = new() { "Ранг: Претендент. Подозрительно высокий.", "Цель выбирает мету. Всегда." },
            ["Кира"] = new() { "У цели обнаружена подозрительная тетрадь.", "Подозреваемый пишет имена. Много имён." },
            ["Монстр без имени"] = new() { "Нет данных. Нет имени. Нет лица.", "Этого человека не существует в базах данных." },
            ["Итачи"] = new() { "Глаза... красные глаза. Не смотри в них.", "Подозреваемый — мастер иллюзий." },
        };

        if (existingHints.TryGetValue(characterName, out var list))
            return list[_rand.Random(0, list.Count - 1)];

        // Generic hint for characters not in the dictionary
        var generic = new List<string>
        {
            "Досье собрано, но данные зашифрованы.",
            "М.М.: Что-то тут нечисто... но я пока не уверен.",
            "Компромат есть, но нужно больше данных.",
            "Подозреваемый ведёт себя подозрительно. Как и все остальные.",
        };
        return generic[_rand.Random(0, generic.Count - 1)];
    }
    //end unique

    private Passive ResolveGoblinZigguratBuild(
        GamePlayerBridgeClass player,
        GameClass game,
        int buildPosition)
    {
        var ziggurat = player.Passives.GoblinZiggurat;
        var population = player.Passives.GoblinPopulation;

        if (population.Warriors < 1 || population.Hobs < 1 || population.Workers < 1)
        {
            game.Phrases.GoblinZigguratNoMoney.SendLog(player, false);
            return null;
        }
        if (player.Status.GetScore() <= 3)
        {
            game.Phrases.GoblinZigguratNoMoney.SendLog(player, false);
            return null;
        }
        if (ziggurat.BuiltPositions.Contains(buildPosition))
        {
            player.Status.AddInGamePersonalLogs("Зиккурат уже построен на этом месте!\n");
            return null;
        }

        player.Status.AddBonusPoints(-3, GoblinSwarm.ZigguratPassive);
        population.ZigguratWorkerDeductions++;
        game.Phrases.GoblinZigguratWorkerDeath.SendLog(player, false);
        player.Status.AddInGamePersonalLogs(
            $"Зиккурат: -1 трудяга. Трудяг осталось: {population.Workers}\n");

        ziggurat.BuiltPositions.Add(buildPosition);
        var stillOnBuiltPosition =
            player.Status.GetPlaceAtLeaderBoard() == buildPosition;
        ziggurat.IsInZiggurat = stillOnBuiltPosition;
        ziggurat.OccupiedZigguratPosition = stillOnBuiltPosition ? buildPosition : 0;
        ziggurat.ZigguratStayRoundsLeft = stillOnBuiltPosition ? 1 : 0;
        if (stillOnBuiltPosition && game.RoundNo == 10 && buildPosition == 1)
            ziggurat.TopPositionVisitedOnRoundTen = true;

        // Any previously attacked enemy is a valid teacher. The old single-target pointer made a
        // later target with no eligible Standalone passive erase otherwise valid learning sources.
        var attackedIds = ziggurat.AttackedPlayerIds.Count > 0
            ? ziggurat.AttackedPlayerIds
            : new List<Guid> { player.Passives.GoblinLastAttackedPlayer };
        var standalonePassives = game.PlayersList
            .Where(enemy => attackedIds.Contains(enemy.GetPlayerId()))
            .SelectMany(enemy => enemy.GameCharacter.Passive)
            .Where(passive => passive.Standalone
                              && passive.PassiveName != "Еврей"
                              && !ziggurat.LearnedPassives.Contains(passive.PassiveName)
                              && player.GameCharacter.Passive.All(existing =>
                                  existing.PassiveName != passive.PassiveName))
            .GroupBy(passive => passive.PassiveName)
            .Select(group => group.First())
            .ToList();

        Passive learnedPassive = null;
        if (standalonePassives.Count > 0)
        {
            learnedPassive =
                standalonePassives[_rand.Random(0, standalonePassives.Count - 1)];
            ziggurat.LearnedPassives.Add(learnedPassive.PassiveName);
            var learnedCopy = learnedPassive.DeepCopy();
            learnedCopy.Visible = true;
            player.GameCharacter.Passive.Add(learnedCopy);
            player.Status.AddInGamePersonalLogs(
                $"Отлично! Гоблины постарались как следует и научились производить: {learnedPassive.PassiveName}\n");
        }
        game.Phrases.GoblinZigguratBuild.SendLog(player, false);
        player.Status.AddInGamePersonalLogs(
            $"Зиккурат построен на месте {buildPosition}! Позиция защищена.\n");
        return learnedPassive;
    }

    // Стая Гоблинов: recompute Str/Int/Psyche from population size, but PRESERVE any external stat
    // change (e.g. Спартанец's −1 Сила) on top of the population base rather than overwriting it (finding D7).
    // The external delta = how far the stat currently sits from the base we last applied.
    private static void ApplyGoblinPopulationStats(GamePlayerBridgeClass player)
    {
        var pop = player.Passives.GoblinPopulation;
        var gc = player.GameCharacter;
        var strBase = pop.Hobs;
        var intBase = pop.Hobs;
        var psyBase = 5 + pop.Hobs;
        var extStr = pop.LastAppliedStrBase == -228 ? 0 : gc.GetStrength() - pop.LastAppliedStrBase;
        var extInt = pop.LastAppliedIntBase == -228 ? 0 : gc.GetIntelligence() - pop.LastAppliedIntBase;
        var extPsy = pop.LastAppliedPsycheBase == -228 ? 0 : gc.GetPsyche() - pop.LastAppliedPsycheBase;
        gc.SetStrength(strBase + extStr, "Гоблины");
        gc.SetIntelligence(intBase + extInt, "Гоблины");
        gc.SetPsyche(psyBase + extPsy, "Гоблины");
        pop.LastAppliedStrBase = strBase;
        pop.LastAppliedIntBase = intBase;
        pop.LastAppliedPsycheBase = psyBase;
    }
}
