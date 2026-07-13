using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
// ReSharper disable RedundantAssignment
#pragma warning disable CS0219


namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class BotsBehavior : IServiceSingleton
{
    
    private readonly GameReaction _gameReaction;
    private readonly Global _global;
    private readonly LoginFromConsole _logs;
    private readonly SecureRandom _rand;
    private readonly CharactersPull _charactersPull;
    private readonly CharacterPassives _characterPassives;

    public BotsBehavior(SecureRandom rand, GameReaction gameReaction, Global global, LoginFromConsole logs,
        CharactersPull charactersPull, CharacterPassives characterPassives)
    {
        _rand = rand;
        _gameReaction = gameReaction;
        _global = global;
        _logs = logs;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // ── AI difficulty (docs/BALANCE-CONSTANTS.md → "Bot AI difficulty") ──
    // The acting bot's effective AI level: a per-player --ai-probe override (≥0) wins over the game default.
    private static int EffectiveDifficulty(GamePlayerBridgeClass p, GameClass game)
        => p.AiDifficulty >= 0 ? p.AiDifficulty : game.AiDifficulty;
    private static bool Dumb(GamePlayerBridgeClass p, GameClass game)  => EffectiveDifficulty(p, game) <= 0;   // L0: pure random
    private static bool Smart(GamePlayerBridgeClass p, GameClass game) => EffectiveDifficulty(p, game) >= 2;   // L2+
    private static bool Omni(GamePlayerBridgeClass p, GameClass game)  => EffectiveDifficulty(p, game) >= 3
                                                                          && game.RoundNo >= game.AiFullKnowledgeRound;  // L3 window
    private const int SmartTargetTaretNumberEarly = 3;
    private const int SmartKnownClassNemesisNumber = 2;
    private const int SmartPredictAvoidNumber = 2;
    private const int SmartMoralWaitPlace3 = 8;   // L1: 5
    private const int SmartMoralWaitPlace4 = 13;  // L1: 8
    private const int OmniPredictConfidence = 2;
    private const int OmniReverseNemesisNumber = 3;
    private const int OmniVersatilityNumber = 2;
    // ── Phase 1: universal global-mechanic mastery (all Smart/Omni-gated; L1 frozen) ──
    private const int SmartTargetTaretNumberLate = 2;   // L2: value the Мишень capture late-game too (early stays 3)
    private const int SmartNemesisBonus = 2;            // L2: nemesis is a big real edge (+2 weigh, ×1.5 skill, justice ×mult)
    private const int OmniDominateNumber = 3;           // L3: dominating all 3 offensive stats reaches TooGOOD → near-certain crush
    private const int SmartMoralWaitLeader = 8;         // L2: leaders convert moral→points at the 8-tier, not the wasteful 5-tier (L1: 5)
    private const int SmartPsycheFloor = 4;             // L2: generic level-up keeps ≥4 Psyche (pool guard vs moral-break / tilt) before over-stacking
    private const int SmartCommitMultiplier = 2;        // L2: commit harder to a clearly-best target (weighted-random otherwise dilutes good heuristics)
    private const int SmartKnownDefensePenalty = 10;    // L2: don't donate an attack into a visible block/skip
    private const int SmartDefenseBreakBonus = 8;       // L2: exploit a visible defense when this kit bypasses it
    private const int SmartDropReadyBonus = 4;          // L2: next in-range Harm breaks the Strength pool and Drops
    // ── Phase 3: per-character pilot fixes (Smart/Omni-gated so L1 stays the control) ──
    private const int SmartSellerMarkFloor = 20;        // L2: Продавец marks on ATTACK regardless of win/loss — spreading marks dominates winnability

    public async Task HandleBotBehavior(GamePlayerBridgeClass player, GameClass game)
    {
        // Dead bots (killed by Kira, Kratos, etc.) should not act
        if (player.Passives.IsDead)
            return;

        // Spend every pending point before committing any kind of turn action. This must precede
        // forced-skip confirmation and every character-specific action path.
        EnsureBotPlaystyle(player, game);
        if (player.Status.LvlUpPoints > 0)
            await HandleLvlUpBot(player, game);

        // Клоны Сусано force only the exact prediction. The bot's actual round-eight action
        // remains a normal AI choice after the shared 30-second reaction delay.
        Madara.ForceRoundEightBotPrediction(player, game);

        // Forced skips are already complete actions. In particular, Шоковый щит must not let a
        // bot immediately replace the skip with its ordinary attack decision.
        if (CompleteForcedSkip(player))
            return;

        if (game.RoundNo > 10)
        {
            await _gameReaction.HandleAttack(player, null, -10);
            return;
        }

        if (Madara.IsMadara(player) && (game.RoundNo == 8 || player.Passives.Madara.Sealed))
        {
            Madara.SetUnableToAct(player);
            return;
        }

        //if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых") && game.RoundNo > 10)
        //{
        //    return;
        //}

        // L0 (Dumb): pure-random baseline — skip the strategic moral + Kira sub-AIs entirely.
        // Moral auto-cashes at round 10 (CheckIfReady force-dump); level-up + attack branch to random internally.
        if (!Dumb(player, game))
            await HandleBotMoral(player, game);

        // Kira bot: write Death Note and use Shinigami Eyes
        if (!Dumb(player, game) && player.GameCharacter.Passive.Any(x => x.PassiveName == "Тетрадь смерти"))
            HandleBotKira(player, game);

        await HandleBotAttack(player, game);
    }

    private void EnsureBotPlaystyle(GamePlayerBridgeClass player, GameClass game)
    {
        if (!Smart(player, game) || player.PlayerType != 404 || player.AiPlaystyle.Length > 0)
            return;

        string Pick(params string[] options) => options[_rand.Random(0, options.Length - 1)];

        switch (player.GameCharacter.Name)
        {
            case "Dopa":
                // Законодатель меты normally resolves while entering round 1, before the bot acts.
                // Record and pilot that actual persistent choice; only roll here as a defensive fallback.
                var tactic = player.Passives.DopaMetaChoice.Triggered
                    ? player.Passives.DopaMetaChoice.ChosenTactic
                    : Pick("Стомп", "Фарм", "Доминация", "Роум");
                player.AiPlaystyle = $"Dopa:{tactic}";
                if (!player.Passives.DopaMetaChoice.Triggered)
                    _characterPassives.ApplyDopaChoice(player, game, tactic);
                break;
            case "Darksci":
                var darksciPlan = Pick("Stable", "Unstable");
                player.AiPlaystyle = $"Darksci:{darksciPlan}";
                var darksciType = player.Passives.DarksciTypeList;
                darksciType.Triggered = true;
                darksciType.IsStableType = darksciPlan == "Stable";
                if (darksciType.IsStableType)
                {
                    player.GameCharacter.AddExtraSkill(20, "Не повезло");
                    player.GameCharacter.AddMoral(2, "Не повезло");
                }
                break;
            case "Глеб" when player.GameCharacter.Passive.Any(x => x.PassiveName == "Yong Gleb"):
                var glebPlan = Pick("Classic", "Young");
                player.AiPlaystyle = $"Глеб:{glebPlan}";
                if (glebPlan == "Young")
                    ApplyYoungGleb(player, game);
                break;
            case "TheBoys":
                player.AiPlaystyle = $"TheBoys:{Pick("Francie", "Butcher", "Kimiko", "M.M.")}";
                break;
            case "Стая Гоблинов":
                player.AiPlaystyle = $"Goblins:{Pick("Horde", "Army", "Economy", "Ziggurat")}";
                break;
            case "Рик Санчез":
                player.AiPlaystyle = $"Rick:{Pick("Portal", "Beans")}";
                break;
            case "Итачи":
                player.AiPlaystyle = $"Itachi:{Pick("Crows", "Tsukuyomi")}";
                break;
            case "Кратос":
                player.AiPlaystyle = $"Kratos:{Pick("GodHunter", "Ragnarok")}";
                break;
            case "Котики":
                player.AiPlaystyle = $"Котики:{Pick("Ambush", "Storm")}";
                break;
            case "Толя":
                player.AiPlaystyle = $"Толя:{Pick("Count", "Rammus")}";
                break;
            case "Монстр без имени":
                player.AiPlaystyle = $"Monster:{Pick("Twin", "Apocalypse")}";
                break;
            case "Таинственный Суппорт":
                player.AiPlaystyle = $"Support:{Pick("Carry", "Stakes")}";
                break;
            default:
                player.AiPlaystyle = "Adaptive";
                break;
        }
    }

    private void ApplyYoungGleb(GamePlayerBridgeClass player, GameClass game)
    {
        var character = _charactersPull.GetAllCharactersNoFilter().First(x => x.Name == "Молодой Глеб");

        // Name deliberately stays "Глеб": prediction and passive dispatch depend on it. The form is
        // identified by Main Ирелия, exactly like the Discord/web transformation paths.
        player.GameCharacter.Passive = character.Passive;
        player.GameCharacter.Avatar = character.Avatar;
        player.GameCharacter.AvatarCurrent = character.Avatar;
        player.GameCharacter.Description = character.Description;
        player.GameCharacter.Tier = character.Tier;
        player.GameCharacter.SetIntelligence(character.GetIntelligence(), "yong-gleb", false);
        player.GameCharacter.SetStrength(character.GetStrength(), "yong-gleb", false);
        player.GameCharacter.SetSpeed(character.GetSpeed(), "yong-gleb", false);
        player.GameCharacter.SetPsyche(character.GetPsyche(), "yong-gleb", false);

        player.Status.IsSkip = false;
        player.Status.ConfirmedSkip = true;
        player.Status.IsReady = false;
        player.Status.WhoToAttackThisTurn = new List<Guid>();
        player.GameCharacter.AddExtraSkill(30, "Спящее хуйло", false);
        player.Status.ClearInGamePersonalLogs();

        // The bot transforms during its first action, after round 1's normal Следит за игрой pass.
        // Seed the opening three meta targets now so the Young form does not lose its core mechanic
        // for an entire round merely because its choice is automated.
        player.Passives.YongGlebMetaClass = game.PlayersList
            .Where(x => x.GetPlayerId() != player.GetPlayerId() && !x.Passives.IsDead)
            .OrderByDescending(x => EstimateOmniFightEdge(player, x))
            .Take(3)
            .Select(x => x.GetPlayerId())
            .ToList();
    }

    private static bool HasPlaystyle(GamePlayerBridgeClass player, string playstyle)
        => player.AiPlaystyle.EndsWith($":{playstyle}", StringComparison.Ordinal);

    private static bool CanBreakKnownDefense(GamePlayerBridgeClass bot, GamePlayerBridgeClass target, GameClass game)
    {
        if (bot.GameCharacter.Passive.Any(x => x.PassiveName is "AutoWin" or "Безжалостный охотник"))
            return true;
        if (game.IsKratosEvent && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых"))
            return true;
        if (bot.GameCharacter.Name == "Рик Санчез"
            && bot.Passives.RickPortalGun.Invented && bot.Passives.RickPortalGun.Charges > 0)
            return true;
        if (target.Status.IsBlock && bot.GameCharacter.Name == "Загадочный Спартанец в маске"
            && bot.Passives.SpartanMark.FriendList.Contains(target.GetPlayerId()))
            return true;
        if (bot.GameCharacter.Name == "Sirinoks"
            && bot.Passives.SirinoksFriendsList.FriendList.Contains(target.GetPlayerId()))
            return true;
        return false;
    }

    private static bool ProgressesIntoKnownDefense(GamePlayerBridgeClass bot, GamePlayerBridgeClass target)
    {
        if (bot.GameCharacter.Name == "Продавец Сомнительных Тактик"
            && bot.Passives.SellerVparitGovna.Cooldown <= 0)
            return true;
        if (bot.GameCharacter.Name == "Толя" && bot.Passives.TolyaCount.IsReadyToUse)
            return true;
        if (bot.GameCharacter.Name == "Кира"
            && bot.Passives.KiraShinigamiEyes.EyesActiveForNextAttack
            && !bot.Passives.KiraShinigamiEyes.RevealedPlayers.Contains(target.GetPlayerId()))
            return true;
        if (bot.GameCharacter.Name == "Napoleon Wonnafcuk"
            && bot.Passives.NapoleonAlliance.AllyId == Guid.Empty)
            return true;
        if (bot.GameCharacter.Name == "Таинственный Суппорт")
            return bot.Passives.SupportPremade.MarkedPlayerId == Guid.Empty
                   || bot.Passives.SupportPremade.MarkedPlayerId == target.GetPlayerId();
        if (bot.GameCharacter.Name == "Sirinoks"
            && !bot.Passives.SirinoksFriendsList.FriendList.Contains(target.GetPlayerId()))
            return true;
        return false;
    }

    private static bool IsHarmImmune(GamePlayerBridgeClass target)
    {
        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Boole Family"))
            return true;
        if (target.GameCharacter.Passive.Any(x => x.PassiveName == "Испанец"))
            return true;
        if (target.GameCharacter.Name == "TheBoys")
        {
            var kimiko = target.Passives.TheBoysKimiko;
            return !target.Passives.TheBoysButcher.SuperDickActive
                   && (kimiko.LivingWeapon || !kimiko.IsDisabled);
        }
        return false;
    }

    private static bool UsesStandardWinPlan(GamePlayerBridgeClass bot)
    {
        if (bot.GameCharacter.Name is "Toxic Mate" or "mylorik" or "DeepList" or "AWDKA" or "HardKitty" or "Баг")
            return false;
        if (bot.GameCharacter.Name == "Продавец Сомнительных Тактик"
            && bot.Passives.SellerVparitGovna.Cooldown <= 0)
            return false;
        return true;
    }

    private static decimal PsycheFightTerm(int difference) => difference switch
    {
        > 0 and <= 3 => 1,
        >= 4 and <= 5 => 2,
        >= 6 => 4,
        < 0 and >= -3 => -1,
        >= -5 and <= -4 => -2,
        <= -6 => -4,
        _ => 0,
    };

    // L3: cheap, side-effect-free approximation of CalculateStep1 on current persistent stats. Character
    // hooks can still override a fight, so this is a preference signal rather than a guaranteed outcome.
    private static decimal EstimateOmniFightEdge(GamePlayerBridgeClass bot, GamePlayerBridgeClass target)
    {
        var me = bot.GameCharacter;
        var enemy = target.GameCharacter;
        var meNemesis = me.HasNemesisOver(enemy);
        var enemyNemesis = enemy.HasNemesisOver(me);
        decimal edge = (meNemesis ? 2 : 0) - (enemyNemesis ? 2 : 0);

        var meSkillBonus = meNemesis ? 1 : 0;
        var enemySkillBonus = enemyNemesis ? 1 : 0;
        var meScale = me.GetIntelligence() + me.GetStrength() + me.GetSpeed() + me.GetPsyche()
                      + me.GetSkill(meSkillBonus) / 60;
        var enemyScale = enemy.GetIntelligence() + enemy.GetStrength() + enemy.GetSpeed() + enemy.GetPsyche()
                         + enemy.GetSkill(enemySkillBonus) / 60;
        edge += meScale - enemyScale;

        var wins = (me.GetIntelligence() > enemy.GetIntelligence() ? 1 : 0)
                   + (me.GetStrength() > enemy.GetStrength() ? 1 : 0)
                   + (me.GetSpeed() > enemy.GetSpeed() ? 1 : 0);
        var losses = (me.GetIntelligence() < enemy.GetIntelligence() ? 1 : 0)
                     + (me.GetStrength() < enemy.GetStrength() ? 1 : 0)
                     + (me.GetSpeed() < enemy.GetSpeed() ? 1 : 0);
        if (wins > losses) edge += 5;
        else if (losses > wins) edge -= 5;

        edge += PsycheFightTerm(me.GetPsyche() - enemy.GetPsyche());

        var nemesisMultiplier = meNemesis ? 1.5m : 1m;
        var mySkill = me.GetSkill(meSkillBonus) / 650 * nemesisMultiplier;
        var targetSkill = enemy.GetSkill(enemySkillBonus) / 650;
        edge += meScale * (1 + mySkill) - enemyScale * (1 + targetSkill) - meScale + enemyScale;
        edge += me.Justice.GetRealJusticeNow() - enemy.Justice.GetRealJusticeNow();
        return edge;
    }

    public async Task HandleBotMoralForSkill(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            if (bot.GameCharacter.Name == "Sirinoks")
            {
                if (game.RoundNo == 9)
                {
                    overwrite = true;
                }
            }

            if (bot.GameCharacter.Name == "DeepList")
            {
                var deepList = bot.Passives.DeepListMadnessTriggeredWhen;
                if (deepList != null)
                    if (deepList.WhenToTrigger.Contains(game.RoundNo))
                    {
                        overwrite = true;
                    }
            }

            //если хардкитти или осьминожка  или Вампур - всегда ждет 20 морали
            if (bot.GameCharacter.Name is "HardKitty" or "Осьминожка" or "Вампур")
                if (bot.GameCharacter.GetMoral() < 20)
                    return;

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.GameCharacter.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.GetPlaceAtLeaderBoard() == 6 && bot.GameCharacter.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.GetPlaceAtLeaderBoard() == 5 && bot.GameCharacter.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8
            if (bot.Status.GetPlaceAtLeaderBoard() == 4 && bot.GameCharacter.GetMoral() < 8 && !overwrite)
                return;
            //если бот на 3м месте то ждет 5
            if (bot.Status.GetPlaceAtLeaderBoard() == 3 && bot.GameCharacter.GetMoral() < 5 && !overwrite)
                return;
            //если бот на 2м месте то ждет 3
            if (bot.Status.GetPlaceAtLeaderBoard() == 2 && bot.GameCharacter.GetMoral() < 3 && !overwrite)
                return;
        }
        //end логика до 10го раунда

        //прожать всю момаль
        while (bot.GameCharacter.GetMoral() >= 1)
        {
            await _gameReaction.HandleMoralForSkill(bot);
        }
        //end прожать всю момаль
    }

    public async Task HandleBotMoralForPoints(GamePlayerBridgeClass bot, GameClass game)
    {
        //логика до 10го раунда
        if (game.RoundNo < 10)
        {
            var overwrite = false;

            //видвик не меняет мораль до самого конца игры, на 10м меняет всё на очки
            if (bot.GameCharacter.Name == "Weedwick")
            {
                return;
            }

            //Геральт не получает мораль — use demand mechanic instead
            if (bot.GameCharacter.Name == "Геральт")
            {
                var botDemand = bot.Passives.GeraltContractDemand;
                // "За следующий" — take advance when displeasure is safely low
                if (!botDemand.DemandedForNext && !botDemand.AdvancePending && botDemand.Displeasure < 3)
                {
                    botDemand.DemandedForNext = true;
                    botDemand.TotalDemandsMade++;
                    botDemand.AdvancePending = true;
                }
                // "За прошлый" — only if invoice predicts coins with no displeasure
                if (!botDemand.DemandedThisPhase && botDemand.PrevContractsFought > 0)
                {
                    var invoice = botDemand.CalculateInvoice();
                    if (invoice.PredictedCoins >= 1 && invoice.PredictedDispleasure == 0)
                    {
                        botDemand.DemandedThisPhase = true;
                        botDemand.TotalDemandsMade++;
                        botDemand.TotalSuccessfulDemands++;
                        bot.Status.AddRegularPoints(invoice.PredictedCoins, "Чеканная монета");

                        if (invoice.AdditionalCoins > 0)
                            bot.Status.AddRegularPoints(invoice.AdditionalCoins, "Выжил чудом");
                        if (invoice.AdditionalDispleasure > 0)
                            botDemand.Displeasure += invoice.AdditionalDispleasure;
                    }
                }
                return;
            }

            //если Осьминожка - всегда ждет 20 морали
            if (bot.GameCharacter.Name is "Осьминожка" or "HardKitty")
            {
                return;
            }


            if (bot.GameCharacter.Name is "Вампур")
            {
                if (bot.Status.GetPlaceAtLeaderBoard() == 6)
                    return;

                if (bot.Status.GetPlaceAtLeaderBoard() <= 2)
                {
                    if (bot.GameCharacter.GetMoral() < 13)
                        return;
                    overwrite = true;
                }
                else
                {
                    if (bot.GameCharacter.GetMoral() < 20)
                        return;

                    overwrite = true;
                }
            }
            

            //Начиная с 6го хода Darksci меняет всю мораль на очки
            if (bot.GameCharacter.Name == "Darksci")
                if (game.RoundNo >= 6)
                    overwrite = true;

            //если бот на последнем месте - ждет 20
            if (bot.Status.GetPlaceAtLeaderBoard() == 6 && bot.GameCharacter.GetMoral() < 20 && !overwrite)
                return;
            //если бот на 5м месте то ждет 13
            if (bot.Status.GetPlaceAtLeaderBoard() == 5 && bot.GameCharacter.GetMoral() < 13 && !overwrite)
                return;
            //если бот на 4м месте то ждет 8 (L2-7: smart bots wait 13 — higher conversion tiers pay strictly more per moral)
            if (bot.Status.GetPlaceAtLeaderBoard() == 4 && bot.GameCharacter.GetMoral() < (Smart(bot, game) ? SmartMoralWaitPlace4 : 8) && !overwrite)
                return;
            //если бот на 3м месте то ждет 5 (L2-7: smart bots wait 8)
            if (bot.Status.GetPlaceAtLeaderBoard() == 3 && bot.GameCharacter.GetMoral() < (Smart(bot, game) ? SmartMoralWaitPlace3 : 5) && !overwrite)
                return;
            // L2-10: leaders (place ≤ 2) waste the 5-tier (5 moral → +1). moral→score is UNMULTIPLIED
            // (AddBonusPoints) and round 10 force-dumps leftovers, so waiting for the 8-tier (→ +2) pays 2× —
            // L1 leaders still dump at 5, which the while-loop below does.
            if (Smart(bot, game) && bot.Status.GetPlaceAtLeaderBoard() <= 2
                && bot.GameCharacter.GetMoral() < SmartMoralWaitLeader && !overwrite)
                return;
        }
        //end логика до 10го раунда



        //прожать всю момаль
        while (bot.GameCharacter.GetMoral() >= 5)
        {
            await _gameReaction.HandleMoralForScore(bot);
        }
        //end прожать всю момаль
    }

    public async Task HandleBotMoral(GamePlayerBridgeClass bot, GameClass game)
    {
        if (bot.Status.GetPlaceAtLeaderBoard() <= 2)
        {
            if (bot.GameCharacter.GetMoral() < 5)
            {
                await HandleBotMoralForSkill(bot, game);
                return;
            }
        }

        // Сайтама: hoard moral early, cash out rounds 9-10
        if (bot.GameCharacter.Name == "Сайтама")
        {
            if (game.RoundNo < 9)
            {
                await HandleBotMoralForSkill(bot, game);
                return;
            }
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        // Toxic Mate: starts at -1000 moral, nothing to convert
        if (bot.GameCharacter.Name == "Toxic Mate")
            return;

        // Dopa: tactic-dependent moral strategy
        if (bot.GameCharacter.Name == "Dopa")
        {
            var dopaTactic = bot.Passives.DopaMetaChoice.ChosenTactic;
            if (dopaTactic is "Стомп" or "Доминация"
                || Smart(bot, game) && dopaTactic == "Роум")
                await HandleBotMoralForSkill(bot, game);
            else
                await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Стая Гоблинов")
        {
            // Goblins prefer points for mine income and ziggurat building
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        // Кира: hoard moral for Shinigami Eyes (costs 25)
        if (bot.GameCharacter.Name == "Кира")
        {
            var kiraEyes = bot.Passives.KiraShinigamiEyes;
            var unrevealedCount = game.PlayersList.Count(x =>
                x.GetPlayerId() != bot.GetPlayerId() && !x.Passives.IsDead &&
                !kiraEyes.RevealedPlayers.Contains(x.GetPlayerId()));
            if (unrevealedCount > 0 && bot.GameCharacter.GetMoral() < 25)
                return; // Save moral for Eyes
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        // Рик Санчез: skill for fights (portal gun is INT-driven via level-up)
        if (bot.GameCharacter.Name == "Рик Санчез")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        // Таинственный Суппорт: skill to win fights
        if (bot.GameCharacter.Name == "Таинственный Суппорт")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Котики")
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "TheBoys")
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Продавец Сомнительных Тактик")
        {
            var sellerV = bot.Passives.SellerVparitGovna;
            if (sellerV.Cooldown <= 0)
            {
                // Prefer attacking unmarked players to spread marks
                await HandleBotMoralForPoints(bot, game);
            }
            else
            {
                await HandleBotMoralForSkill(bot, game);
            }
            return;
        }

        if (bot.GameCharacter.Name == "Salldorum")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Napoleon Wonnafcuk")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Sirinoks")
        {
            //логика до 10го раунда
            await HandleBotMoralForSkill(bot, game);
            //end логика до 10го раунда
            return;
        }

        /*if (bot.GameCharacter.Name == "Вампур" && game.RoundNo == 5)
        {
            HandleBotMoralForSkill(bot, game);
            return;
        }*/
        //If LeCrisp 10 psy and Place > 5, use all Score, use all Skill
        if (bot.GameCharacter.Name == "LeCrisp" && bot.GameCharacter.GetPsyche() >= 10 && game.RoundNo >= 5 && bot.Status.GetPlaceAtLeaderBoard() <= 4)
        {
            await HandleBotMoralForPoints(bot, game);
            return;
        }
        if (bot.GameCharacter.Name == "LeCrisp" && game.RoundNo <= 5)
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "Загадочный Спартанец в маске")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "DeepList")
        {
            await HandleBotMoralForSkill(bot, game);
            return;
        }

        if (bot.GameCharacter.Name == "mylorik")
        {
            var mylorikRevenge = bot.Passives.MylorikRevenge;
            if (mylorikRevenge != null)
            {
                var totalNotFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                var totalRevenges = mylorikRevenge.EnemyListPlayerIds.Count;

                //если на всех уже был запрокан луз или победа, то меняет мораль на скилл
                if (totalRevenges == 5)
                {
                    await HandleBotMoralForSkill(bot, game);
                    return;
                }

                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), меняет всю мораль на скилл
                var roundsLeft = 11 - game.RoundNo;
                if (totalNotFinishedRevenges >= roundsLeft)
                {
                    await HandleBotMoralForSkill(bot, game);
                    return;
                }
            }
        }


        await HandleBotMoralForPoints(bot, game);
    }


    private void HandleBotKira(GamePlayerBridgeClass bot, GameClass game)
    {
        var dn = bot.Passives.KiraDeathNote;
        var eyes = bot.Passives.KiraShinigamiEyes;

        // Shinigami Eyes: prioritize unrevealed leaders
        if (bot.GameCharacter.GetMoral() >= 25 && !eyes.EyesActiveForNextAttack)
        {
            var unrevealed = game.PlayersList
                .Where(x => x.GetPlayerId() != bot.GetPlayerId()
                            && !x.Passives.IsDead
                            && !eyes.RevealedPlayers.Contains(x.GetPlayerId()))
                .OrderBy(x => x.Status.GetPlaceAtLeaderBoard())
                .ToList();

            if (unrevealed.Count > 0)
            {
                var shouldUseEyes = false;
                // Always use on unrevealed leaders (place 1-2)
                if (unrevealed.Any(x => x.Status.GetPlaceAtLeaderBoard() <= 2))
                    shouldUseEyes = true;
                // Late game: use more aggressively
                else if (game.RoundNo >= 7)
                    shouldUseEyes = true;
                // Random 25% otherwise
                else if (_rand.Luck(1, 4))
                    shouldUseEyes = true;

                if (shouldUseEyes)
                {
                    bot.GameCharacter.AddMoral(-25, "Глаза бога смерти");
                    eyes.EyesActiveForNextAttack = true;
                    bot.Status.AddInGamePersonalLogs("Глаза бога смерти: Активированы!\n");
                }
            }
        }

        // Write Death Note
        if (dn.CurrentRoundTarget == Guid.Empty)
        {
            var candidates = game.PlayersList
                .Where(x => x.GetPlayerId() != bot.GetPlayerId()
                            && !x.Passives.IsDead
                            && !dn.FailedTargets.Contains(x.GetPlayerId()))
                .ToList();

            if (candidates.Count > 0)
            {
                // Priority 1: write KNOWN names (revealed via Eyes) — guaranteed kills
                var knownTargets = candidates
                    .Where(x => eyes.RevealedPlayers.Contains(x.GetPlayerId()))
                    .OrderBy(x => x.Status.GetPlaceAtLeaderBoard())
                    .ToList();

                if (knownTargets.Count > 0)
                {
                    var target = knownTargets.First();
                    dn.CurrentRoundTarget = target.GetPlayerId();
                    dn.CurrentRoundName = target.GameCharacter.Name;
                }
                else
                {
                    // Priority 2: target leaders, guess name
                    var target = candidates
                        .OrderBy(x => x.Status.GetPlaceAtLeaderBoard())
                        .First();
                    dn.CurrentRoundTarget = target.GetPlayerId();

                    // Eliminate names known to belong to other (revealed) players
                    var knownNames = eyes.RevealedPlayers
                        .Select(rp => game.PlayersList.Find(p => p.GetPlayerId() == rp))
                        .Where(p => p != null)
                        .Select(p => p!.GameCharacter.Name)
                        .ToHashSet();

                    // Eliminate names already tried and failed on this target
                    var failedNames = dn.Entries
                        .Where(e => e.TargetPlayerId == target.GetPlayerId() && !e.WasCorrect)
                        .Select(e => e.WrittenName)
                        .ToHashSet();

                    var availableNames = game.PlayersList
                        .Select(x => x.GameCharacter.Name).Distinct()
                        .Where(n => !knownNames.Contains(n) && !failedNames.Contains(n))
                        .ToList();

                    dn.CurrentRoundName = availableNames.Count > 0
                        ? availableNames[_rand.Random(0, availableNames.Count - 1)]
                        : game.PlayersList[_rand.Random(0, game.PlayersList.Count - 1)].GameCharacter.Name;
                }
            }
        }
    }

    public async Task HandleBotAttack(GamePlayerBridgeClass bot, GameClass game)
    {
        try
        {
            //local variables
            var allTargets = game!.NanobotsList.Find(x => x.GameId == game.GameId)!.Nanobots
                .Where(x => x.GetPlayerId() != bot.GetPlayerId()
                    && !x.Player.Passives.IsDead
                    && !Naruto.IsNarutoPair(bot, x.Player)).ToList();

            if (game.RoundNo == 10)
            {
                foreach (var target in allTargets.ToList().Where(target => target.Player.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят")))
                {
                    allTargets.Remove(target);
                }
            }

            if (game.RoundNo is 9 or 10)
            {
                if (game.GetAllGlobalLogs().Contains("Нахуй эту игру"))
                {
                    foreach (var target in allTargets.ToList().Where(target => target.Player.GameCharacter.GetPsyche() <= 0 && target.Player.GameCharacter.Passive.Any(x => x.PassiveName == "Не повезло")))
                    {
                        allTargets.Remove(target);
                    }
                }
            }

            if (await TryForceRumblingAttack(bot, game, allTargets)) return;

            // L0 (Dumb): pure-random attack/block, respecting real cannot-block / cannot-attack rules.
            if (Dumb(bot, game))
            {
                await HandleBotAttackRandom(bot, game, allTargets);
                return;
            }

            decimal maxRandomNumber = 0;
            var isBlock = allTargets.Count;
            var minimumRandomNumberForBlock = 1;
            var maximumRandomNumberForBlock = 4;
            var mandatoryAttack = -1;
            var noBlock = 99999;
            var yesBlock = -99999;
            var botJustice = bot.GameCharacter.Justice.GetRealJusticeNow();
            //end local variables

            //edit block for team
            switch (game.Teams.Count)
            {
                case 2:
                    isBlock += 1;
                    break;
                case 3:
                    isBlock += 2;
                    break;
            }
            //end

            //character variables
            var darksciTheOne = Guid.Empty;
            decimal darksciUnstableBestEdge = decimal.MinValue;
            decimal youngGlebBestPreference = decimal.MinValue;
            var awdkaFirst = 0;
            decimal spartanTarget = 0;
            //end character variables

            //local varaibles
            var isTargetTooGoodNumber = 7;
            var isLostLastRoundAndTargetIsBetterNumber = 5;
            var isJusticeTheSameNumber = 5;
            var isJusticeLessThanTargetNumber = 7;
            var isTargetFirstNumber = 1;
            var isTargetSecondWhenBotFirstNumber = 1;
            var isBotWonAndTooGoodNumber = 4;
            var isTargetNemesisNumber = 3;
            var isTargetTaretNumber = 1;
            var isTargetTooGood = false;
            var isJusticeTheSame = false;
            var isJusticeLessThanTarget = false;
            var isTargetFirst = false;
            var isTargetSecondWhenBotFirst = false;
            var isLostLastRoundAndTargetIsBetter = false;
            var isBotWonAndTooGood = false;
            var isTargetTaret = false;
            var isTargetNemesis = false;
            var howManyAttackingTheSameTarget = 0;
            var justiceDifference = 0;
            //

            // L2-1: Мишень capture is the biggest repeatable skill faucet (ladder 10,9,…,1 granted ×2 as
            // Main+Extra) — smart bots value it, most in the early rounds where the ladder is highest.
            if (Smart(bot, game)) isTargetTaretNumber = game.RoundNo <= 4 ? SmartTargetTaretNumberEarly : SmartTargetTaretNumberLate;

            //calculation Tens
            foreach (var target in allTargets)
            {
                // L3-4: omniscient bots read the justice that actually enters the fight math
                var targetJustice = Omni(bot, game)
                    ? target.Player.GameCharacter.Justice.GetRealJusticeNow()
                    : target.Player.GameCharacter.Justice.GetSeenJusticeNow();

                //if justice is the same
                if (botJustice == targetJustice)
                {
                    target.AttackPreference -= isJusticeTheSameNumber;
                    isJusticeTheSame = true;
                }
                //if bot justice less than platers
                else if (botJustice < targetJustice)
                {
                    target.AttackPreference -= isJusticeLessThanTargetNumber;
                    isJusticeLessThanTarget = true;
                }

                //if player is first
                if (target.Player.Status.GetPlaceAtLeaderBoard() == 1)
                {
                    target.AttackPreference -= isTargetFirstNumber;
                    isTargetFirst = true;
                }

                //if player is second when we are first
                if (bot.Status.GetPlaceAtLeaderBoard() == 1 && target.Player.Status.GetPlaceAtLeaderBoard() == 2)
                {
                    target.AttackPreference -= isTargetSecondWhenBotFirstNumber;
                    isTargetSecondWhenBotFirst = true;
                }



                //если на прошлом бою враг был toogood
                //-= 7
                if (bot.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId() &&
                        x.IsTooGoodEnemy))
                {
                    target.AttackPreference -= isTargetTooGoodNumber;
                    isTargetTooGood = true;
                }
                else if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.GetPlayerId() && x.IsTooGoodMe))
                {
                    target.AttackPreference -= isTargetTooGoodNumber;
                    isTargetTooGood = true;
                }
                //если на прошлом ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId() &&
                             x.IsStatsBetterEnemy))
                {
                    target.AttackPreference -= isLostLastRoundAndTargetIsBetterNumber;
                    isLostLastRoundAndTargetIsBetter = true;
                }
                //если на прошлом-1 ты проиграл И у врага больше статов
                //-= 5
                else if (bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 2 && x.EnemyId == target.GetPlayerId() &&
                             x.IsStatsBetterEnemy))
                {
                    target.AttackPreference -= isLostLastRoundAndTargetIsBetterNumber;
                    isLostLastRoundAndTargetIsBetter = true;
                }
                // L2-4: stat-decided losses stay valid one more round (stats only move on level-ups)
                else if (Smart(bot, game) && bot.Status.WhoToLostEveryRound.Any(x =>
                             x.RoundNo == game.RoundNo - 3 && x.EnemyId == target.GetPlayerId() &&
                             x.IsStatsBetterEnemy))
                {
                    target.AttackPreference -= isLostLastRoundAndTargetIsBetterNumber;
                    isLostLastRoundAndTargetIsBetter = true;
                }



                //won and too good
                if (target.Player.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == bot.GetPlayerId() && x.IsTooGoodEnemy))
                {
                    target.AttackPreference += isBotWonAndTooGoodNumber;
                    isBotWonAndTooGood = true;
                }


                //how many players are attacking the same player
                howManyAttackingTheSameTarget = allTargets
                    .FindAll(x => x.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())).Count;
                target.AttackPreference -= howManyAttackingTheSameTarget;


                //target
                if (target.AttackPreference >= 5)
                    if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                    {
                        target.AttackPreference += isTargetTaretNumber;
                        isTargetTaret = true;
                    }

                //nemesis
                if (target.AttackPreference >= 5)
                    if (bot.GameCharacter.HasNemesisOver(target.Player.GameCharacter))
                    {
                        target.AttackPreference += isTargetNemesisNumber;
                        // L2 (Phase 1): nemesis is a big real edge (+2 weighing, ×1.5 skill, amplified justice
                        // window) — smart bots weight class-counter targets harder as a first-class driver.
                        if (Smart(bot, game)) target.AttackPreference += SmartNemesisBonus;
                        isTargetNemesis = true;
                    }

                // L2-2: use the legitimately-known class tells (KnownPlayerClass) for nemesis targeting
                if (Smart(bot, game))
                {
                    var knownTell = bot.Status.KnownPlayerClass.Find(x => x.EnemyId == target.GetPlayerId());
                    if (knownTell != null)
                    {
                        var myType = bot.GameCharacter.GetSkillClassType();
                        var counterKeyword = CharacterClass.ClassToKnownKeyword(CharacterClass.NemesisOf(myType));
                        var countersMeKeyword = CharacterClass.ClassToKnownKeyword(
                            CharacterClass.NemesisOf(CharacterClass.NemesisOf(myType))); // 3-cycle: nemesis-of-nemesis counters me
                        if (counterKeyword != "" && knownTell.Text.Contains(counterKeyword) && target.AttackPreference >= 5)
                            target.AttackPreference += SmartKnownClassNemesisNumber;
                        else if (countersMeKeyword != "" && knownTell.Text.Contains(countersMeKeyword))
                            target.AttackPreference -= SmartKnownClassNemesisNumber;
                    }
                }

                // L3-2: omniscient bots avoid enemies who counter them (reverse nemesis is a true-read = cheat)
                if (Omni(bot, game) && target.Player.GameCharacter.HasNemesisOver(bot.GameCharacter))
                    target.AttackPreference -= OmniReverseNemesisNumber;

                // L3-3: true-stat versatility check (Step-1 ±5 term, CalculateRounds versatility, on real stats)
                if (Omni(bot, game))
                {
                    var statWins =
                        (bot.GameCharacter.GetIntelligence() > target.Player.GameCharacter.GetIntelligence() ? 1 : 0) +
                        (bot.GameCharacter.GetStrength() > target.Player.GameCharacter.GetStrength() ? 1 : 0) +
                        (bot.GameCharacter.GetSpeed() > target.Player.GameCharacter.GetSpeed() ? 1 : 0);
                    if (statWins >= 2 && target.AttackPreference >= 5)
                        target.AttackPreference += OmniVersatilityNumber;
                    else if (statWins == 0)
                        target.AttackPreference -= OmniVersatilityNumber;
                    // L3 (Phase 1): dominating all three offensive stats reaches TooGOOD (weighing ≥13, the
                    // enemy's roll-window collapses to 30) — a near-certain crush, so hunt it hard.
                    if (statWins == 3 && target.AttackPreference >= 5)
                        target.AttackPreference += OmniDominateNumber;
                }

                //justice diff
                if (allTargets.All(x => x.Player.GameCharacter.Justice.GetSeenJusticeNow() < botJustice))
                {
                    justiceDifference = botJustice - targetJustice;
                    target.AttackPreference += justiceDifference;
                }
                // L2-3: per-target justice gradient (L1 only rewards it when ALL targets are below the bot)
                else if (Smart(bot, game) && botJustice > targetJustice)
                {
                    target.AttackPreference += botJustice - targetJustice;
                }

                // L2-16: act on visible defensive choices. A normal attack into Block loses a bonus point
                // and feeds Justice; Skip wastes the turn. Kits with Armor/SkipBreak instead exploit it,
                // while mark/reveal/buff skills that fire before defense receive only a small penalty.
                if (Smart(bot, game) && (target.Player.Status.IsBlock || target.Player.Status.IsSkip))
                {
                    if (CanBreakKnownDefense(bot, target.Player, game))
                        target.AttackPreference += SmartDefenseBreakBonus;
                    else
                        target.AttackPreference -= ProgressesIntoKnownDefense(bot, target.Player)
                            ? 2
                            : SmartKnownDefensePenalty;
                }

                // L2-17: understand Harm reach and a primed Strength pool. An in-range win damages all
                // quality pools; when Strength resist is already 0, the next Harm also costs a bonus point
                // and pushes the victim down (except place 6). Butcher's repeated Harm makes this stronger.
                if (Smart(bot, game) && game.RoundNo > 1
                    && !bot.GameCharacter.Passive.Any(x => x.PassiveName == "Минька")
                    && (!IsHarmImmune(target.Player) || bot.Passives.TheBoysButcher.SuperDickActive))
                {
                    var harmRange = bot.GameCharacter.GetSpeedQualityResistInt()
                                    - target.Player.GameCharacter.GetSpeedQualityKiteBonus();
                    var placeDistance = Math.Abs(bot.Status.GetPlaceAtLeaderBoard() - target.PlaceAtLeaderBoard());
                    if (placeDistance <= harmRange)
                    {
                        target.AttackPreference += 1;
                        if (target.PlaceAtLeaderBoard() != 6
                            && target.Player.GameCharacter.GetStrengthQualityResistInt() == 0)
                        {
                            target.AttackPreference += SmartDropReadyBonus;
                            if (bot.GameCharacter.Name == "TheBoys")
                            {
                                var butcherHarms = 1 + bot.Passives.TheBoysButcher.PokerCount;
                                if (bot.Passives.TheBoysButcher.SuperDickActive) butcherHarms *= 2;
                                target.AttackPreference += butcherHarms - 1;
                            }
                        }
                    }
                }

                // L3-6: evaluate the actual global fight terms together instead of treating nemesis,
                // versatility, psyche, skill and justice as unrelated hints. Lose-to-win kits opt out.
                if (Omni(bot, game) && UsesStandardWinPlan(bot))
                {
                    var fightEdge = EstimateOmniFightEdge(bot, target.Player);
                    if (fightEdge >= 13)
                        target.AttackPreference += 8;
                    else if (fightEdge >= 5)
                        target.AttackPreference += 3;
                    else if (fightEdge <= -13)
                        target.AttackPreference -= 10;
                    else if (fightEdge <= -5)
                        target.AttackPreference -= 4;
                }

                //custom bot behavior
                switch (bot.GameCharacter.Name)
                {
                    case "Weedwick":
                        //bongs
                        target.AttackPreference *= target.Player.GameCharacter.GetWinStreak()*2;
                        //weed
                        target.AttackPreference += target.Player.Passives.WeedwickWeed;
                        //верхний этаж
                        target.AttackPreference += 6 - target.PlaceAtLeaderBoard();

                        //преференс врагов выше видвика по таблице + 3 (поставь это сразу перед умножением на 20 за волка)
                        if (bot.Status.GetPlaceAtLeaderBoard() > target.PlaceAtLeaderBoard())
                        {
                            target.AttackPreference += 3;
                        }

                        //wuf
                        if (target.Player.GameCharacter.Justice.GetRealJusticeNow() == 0)
                             target.AttackPreference *= 20;

                        if (target.Player.GameCharacter.Name == "DeepList")
                            target.AttackPreference = 0;
                        break;
                    case "DeepList":
                        var deepListMadness = bot.Passives.DeepListMadnessTriggeredWhen;
                        if (deepListMadness.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        if (target.Player.GameCharacter.Name == "Weedwick")
                            target.AttackPreference = 0;
                        break;
                    case "Кира":
                        if (target.GetPlayerId() == bot.Passives.KiraL.LPlayerId)
                            target.AttackPreference = 0;
                        // Prefer high-ranked non-L targets
                        else if (target.PlaceAtLeaderBoard() <= 2)
                            target.AttackPreference += 3;
                        // L2: when Eyes are armed, spend the attack on a useful unrevealed identity. L and
                        // Монстр do not consume/reveal, so never let them trap the active Eyes.
                        if (Smart(bot, game) && bot.Passives.KiraShinigamiEyes.EyesActiveForNextAttack)
                        {
                            if (target.Player.GameCharacter.Passive.Any(x => x.PassiveName == "Выдуманный персонаж"))
                                target.AttackPreference -= 8;
                            else if (!bot.Passives.KiraShinigamiEyes.RevealedPlayers.Contains(target.GetPlayerId()))
                                target.AttackPreference += 12;
                            else
                                target.AttackPreference -= 6;
                        }
                        break;
                    case "Кратос":
                        // Охота на богов doubles fight skill against Мишень; Клинки хаоса add both
                        // leaderboard neighbours, so centre targets can turn one action into three fights.
                        if (Smart(bot, game))
                        {
                            if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                                target.AttackPreference += 10;
                            target.AttackPreference += allTargets.Count(x =>
                                Math.Abs(x.PlaceAtLeaderBoard() - target.PlaceAtLeaderBoard()) == 1) * 3;

                            // Ragnarok plan deliberately seeks a losing round-10 fight to start the
                            // resurrection event. GodHunter keeps selecting the strongest ordinary fight.
                            if (game.RoundNo == 10 && HasPlaystyle(bot, "Ragnarok"))
                            {
                                var ragnarokEdge = Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player)
                                    : target.AttackPreference - 10;
                                target.AttackPreference -= Math.Clamp(
                                    ragnarokEdge, -10m, 10m);
                            }
                        }
                        break;
                    case "Тигр":

                        var tigr = bot.Passives.TigrThreeZeroList;
                        if (tigr != null)
                        {
                            if (target.AttackPreference <= 3)
                            {
                                if (tigr.FriendList.Any(x =>
                                        x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries == 1 && x.IsUnique))
                                    target.AttackPreference -= 2;
                                if (tigr.FriendList.Any(x =>
                                        x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries == 2 && x.IsUnique))
                                    target.AttackPreference = 0;
                            }

                            switch (target.AttackPreference)
                            {
                                case >= 8:
                                {
                                    if (tigr.FriendList.Any(x =>
                                            x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries >= 1 && x.IsUnique))
                                        target.AttackPreference += 9;

                                    break;
                                }
                                case >= 6:
                                {
                                    if (tigr.FriendList.Any(x =>
                                            x.EnemyPlayerId == target.GetPlayerId() && x.WinsSeries >= 1 && x.IsUnique))
                                        target.AttackPreference += 3;

                                    break;
                                }
                            }
                        }

                        break;


                    case "AWDKA":

                        if (game.RoundNo == 1)
                        {
                            if (target.Player.GameCharacter.GetIntelligence() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetIntelligence();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetStrength() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetStrength();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetSpeed() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetSpeed();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }

                            if (target.Player.GameCharacter.GetPsyche() > awdkaFirst)
                            {
                                awdkaFirst = target.Player.GameCharacter.GetPsyche();
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }
                        }


                        var awdkaTrying = bot.Passives.AwdkaTryingList;
 
                            var awdkaTryingTarget =
                                awdkaTrying.TryingList.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                            if (awdkaTryingTarget != null)
                            {
                                //-2 тем, на ком есть стак платины(до тех пор, пока он еще не на всех)
                                if (awdkaTrying.TryingList.Count(x => x.IsUnique) < 5)
                                    if (awdkaTryingTarget.IsUnique)
                                        target.AttackPreference -= 2;

                                if (game.RoundNo <= 4)
                                    if (!awdkaTryingTarget.IsUnique)
                                        target.AttackPreference = 15 - target.AttackPreference;
                                if (game.RoundNo <= 5)
                                    if (!awdkaTryingTarget.IsUnique)
                                        target.AttackPreference += 5;
                            }
                            else
                            {
                                if (game.RoundNo <= 5) target.AttackPreference += 5;
                            }
                        


                        var triggered = false;

                        if (game.RoundNo > 5)
                        {
                            var wons = target.Player.Status.WhoToLostEveryRound.OrderByDescending(x => x.RoundNo)
                                .ToList();
                            foreach (var won in wons)
                                if (won.EnemyId == bot.GetPlayerId() && won.WhoAttacked == bot.GetPlayerId())
                                {
                                    var places = 6;
                                    if (target.PlaceAtLeaderBoard() == 6) places = 7;
                                    target.AttackPreference *= ((game.RoundNo - won.RoundNo +
                                                                 (decimal)((places - target.PlaceAtLeaderBoard()) *
                                                                           (game.RoundNo - won.RoundNo - 1))) / 2);
                                    triggered = true;
                                    break;
                                }


                            if (!triggered)
                            {
                                var places = 6;
                                if (target.PlaceAtLeaderBoard() == 6) places = 7;
                                target.AttackPreference *= ((game.RoundNo +
                                                             (decimal)(places - target.PlaceAtLeaderBoard()) *
                                                             (game.RoundNo - 1)) / 2);
                            }
                        }

                        break;
                    case "HardKitty":
                        if (game.RoundNo < 5) mandatoryAttack = allTargets.First().PlaceAtLeaderBoard();

                        if (target.PlaceAtLeaderBoard() == 1) target.AttackPreference += 1;

                        break;
                    case "Darksci":
                        if (game.RoundNo < 8)
                            if (targetJustice > botJustice)
                                target.AttackPreference -= 3;

                        var darksciLucky = bot.Passives.DarksciLuckyList;
    
                            if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                if (target.AttackPreference > 1)
                                {
                                    target.AttackPreference += 3;
                                    if (game.RoundNo < 5) target.AttackPreference += 3;
                                }

                            if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()) &&
                                darksciLucky.TouchedPlayers.Count == 4) target.AttackPreference = 0;

                            // Если ОДИН из тех, на ком не запрокан стак, уже побеждал даркси ПО СТАТАМ, то его значение = 0.
                            if (darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                {
                                    var darksciLuckyTheOne = bot.Status.WhoToLostEveryRound.Find(x =>
                                        x.EnemyId == target.GetPlayerId() && x.IsStatsBetterEnemy);
                                    if (darksciLuckyTheOne != null && darksciTheOne == Guid.Empty)
                                    {
                                        darksciTheOne = target.GetPlayerId();
                                        target.AttackPreference = 0;
                                    }
                                }

                            //Если незапроканных стаков = кол-во оставшихся ходов - 3, то выбирает цель только из них. (пока не останется 1) 

                            var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                            var roundsLeft2 = 11 - (game.RoundNo + 3);
                            if (notTouched >= roundsLeft2)
                                if (darksciLucky.TouchedPlayers.Count < 5)
                                    if (darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                        target.AttackPreference = 0;


                            if (game.RoundNo == 7 && bot.GameCharacter.GetPsyche() < 4 &&
                                darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                            if (game.RoundNo >= 8 && darksciLucky.TouchedPlayers.Count != 5)
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                    mandatoryAttack = target.PlaceAtLeaderBoard();

                            // Unstable pays triple current score only after touching all five enemies.
                            // Complete the circuit immediately; a loss still counts, so ordinary fight
                            // strength is secondary until the multiplier has triggered.
                            if (Smart(bot, game) && HasPlaystyle(bot, "Unstable") && !darksciLucky.Triggered)
                            {
                                if (!darksciLucky.TouchedPlayers.Contains(target.GetPlayerId()))
                                {
                                    var untouchedEdge = Omni(bot, game)
                                        ? EstimateOmniFightEdge(bot, target.Player)
                                        : target.AttackPreference - 10;
                                    target.AttackPreference = Math.Max(target.AttackPreference,
                                        18 + Math.Clamp(untouchedEdge, -8m, 8m));

                                    // A loss costs Psyche and can force repeated skips, which is worse than
                                    // delaying the triple-score trigger. Force only the safest viable new
                                    // contact (or the final late-game chance); otherwise retain block odds.
                                    var targetIsDefending = target.Player.Status.IsBlock || target.Player.Status.IsSkip;
                                    if (!targetIsDefending && untouchedEdge > darksciUnstableBestEdge
                                        && (untouchedEdge >= -3 || game.RoundNo >= 7)
                                        && bot.GameCharacter.GetPsyche() > 2)
                                    {
                                        darksciUnstableBestEdge = untouchedEdge;
                                        mandatoryAttack = target.PlaceAtLeaderBoard();
                                    }
                                }
                                else
                                {
                                    target.AttackPreference = 0;
                                }
                            }
                        

                        break;
                    case "Злой Школьник":
                        if (target.AttackPreference >= 5)
                        {
                            if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                                target.AttackPreference += 3;

                            if (game.RoundNo < 5 && target.Player.GameCharacter.Name == "HardKitty")
                                target.AttackPreference = 0;

                            if (game.RoundNo > 5 && target.Player.GameCharacter.Name == "HardKitty" &&
                                target.AttackPreference >= 5) mandatoryAttack = target.PlaceAtLeaderBoard();
                        }

                        break;
                    case "mylorik":
                        var mylorikRevenge = bot.Passives.MylorikRevenge;
                        var revengeEnemy =
                                mylorikRevenge.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == target.GetPlayerId());
                            var totalFinishedRevenges =
                                mylorikRevenge.EnemyListPlayerIds.FindAll(x => !x.IsUnique).Count;
                            var totalNotFinishedRevenges =
                                mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;


                            if (revengeEnemy != null)
                            {
                                //Если кол-во оставшихся ходов = незапроканных побед (но с лузом), то х2 преф
                                if (revengeEnemy.IsUnique)
                                {
                                    var leftRound = 11 - game.RoundNo;
                                    if (totalNotFinishedRevenges >= leftRound) target.AttackPreference *= 3;
                                }

                                //первые 4 хода, Не нападает на тех, на ком уже запрокан луз мести, но не запрокана победа мести. если запроканы еще не все 
                                if (revengeEnemy.IsUnique && game.RoundNo <= 4)
                                    if (totalFinishedRevenges < 5)
                                        target.AttackPreference = 0;

                                // {Начиная с 5 хода: Если преференс врага С запроканным лузом но БЕЗ победы   >= 8, то +20
                                if (game.RoundNo >= 5 && target.AttackPreference >= 8 && revengeEnemy.IsUnique)
                                    target.AttackPreference += 20;
                                // {Начиная с 6 хода: Если преференс врага С запроканным лузом но БЕЗ победы   >= 5, то +10
                                else if (game.RoundNo >= 6 && target.AttackPreference >= 5 && revengeEnemy.IsUnique)
                                    target.AttackPreference += 10;

                                //преф - 4 тем, на ком уже запрокана ПОБЕДА мести, если запроканы еще не все
                                if (!revengeEnemy.IsUnique && totalFinishedRevenges < 5) target.AttackPreference -= 4;


                                if (game.RoundNo > 5)
                                    //после 5го хода: преф игроков С Лузом но БЕЗ победы не может опуститься ниже 4 
                                    if (revengeEnemy.IsUnique)
                                        if (target.AttackPreference < 4)
                                            target.AttackPreference = 4;
                            }
                            else
                            {
                                //на первых 4х ходах если у врага больше справедливости и не запрокана ни одна метка, то преф +2 * разницу в вашей справедливости ( с положительным знаком)
                                if (game.RoundNo <= 4)
                                    if (targetJustice > botJustice)
                                        target.AttackPreference += 2 * (targetJustice - botJustice);

                                //Если на врагах еще не запрокан луз мести - их преференс +5-игроки с запроканым лузом или победой. 
                                target.AttackPreference += 5 - mylorikRevenge.EnemyListPlayerIds.Count;

                                //Первые 4 хода: + 17 тем у кого справедливости больше чем у тебя, если на них не запрокан луз мести.
                                if (game.RoundNo <= 4 && botJustice < targetJustice)
                                    target.AttackPreference += 17;
                            }
                        


                        //"-5 за more stats" и "-7 за toogood" из базовых условий десяток   / 1 + кол-во стаков сломанного щита
                        if (game.RoundNo >= 5)
                        {
                            var mylorikSpartan = bot.Passives.MylorikSpartan;

                                var spartanEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == target.GetPlayerId());
                                if (spartanEnemy != null)
                                {
                                    if (isTargetTooGood)
                                        target.AttackPreference += isTargetTooGoodNumber - isTargetTooGoodNumber / (1 + spartanEnemy.LostTimes);
                                    else if (isLostLastRoundAndTargetIsBetter)
                                        target.AttackPreference += isLostLastRoundAndTargetIsBetterNumber - isLostLastRoundAndTargetIsBetterNumber / (1 + spartanEnemy.LostTimes);
                                }
                            
                        }


                        break;
                    case "Краборак":

                        if (allTargets.Any(x => x.PlaceAtLeaderBoard() >= 4 && x.AttackPreference > 0))
                            if (target.PlaceAtLeaderBoard() < 4)
                                target.AttackPreference -= 4;


                        if (target.Player.GameCharacter.Name == "HardKitty") target.AttackPreference -= 1;

                        break;

                    case "Братишка":
                        if (target.PlaceAtLeaderBoard() == bot.Status.GetPlaceAtLeaderBoard() + 1 ||
                            target.PlaceAtLeaderBoard() == bot.Status.GetPlaceAtLeaderBoard() - 1)
                        {
                            if (target.AttackPreference > 1) target.AttackPreference += 2;

                            if (target.AttackPreference >= 5) target.AttackPreference += 3;
                        }

                        break;
                    case "Sirinoks":

                        //После первого хода:  преференс -3 всем, кто не подходит под текущую мишень (мишень скилла).
                        if (game.RoundNo > 1)
                        {
                            if (!bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                            {
                                target.AttackPreference -= 3;
                            }
                            else
                            {
                                target.AttackPreference += 3;
                            }
                        }

                        var siriFriends = bot.Passives.SirinoksFriendsList;

            
                            //+5 к значению тех, кто еще не друг.
                            if (!siriFriends.FriendList.Contains(target.GetPlayerId()) && target.AttackPreference > 0)
                            {
                                target.AttackPreference += 5;
                            }


                            //До начала 5го хода может нападать только на одну цель. Если значение цели 0 - то блок.
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                var sirisFried = allTargets.Find(x => x.GetPlayerId() == siriFriends.FriendList.First());

                                if (sirisFried == null || target.GetPlayerId() != sirisFried.GetPlayerId())
                                {
                                    target.AttackPreference = 0;
                                }
                                else
                                {
                                    if (target.AttackPreference > 3)
                                    {
                                        mandatoryAttack = target.Player.Status.GetPlaceAtLeaderBoard();
                                    }
                                    else
                                    {
                                        target.AttackPreference = 0;
                                    }

                                    if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                                    {
                                        mandatoryAttack = target.PlaceAtLeaderBoard();
                                    }
                                }
                            }


                            //Если кол-во оставшихся ходов == кол-во незапроканных друзей, то выбирает цель только из тех, кто еще не друг.
                            var nonFiendsLeft = 5 - siriFriends.FriendList.Count;
                            var roundsLeft = 11 - game.RoundNo;
                            var allNotFriends =
                                allTargets.FindAll(x => !siriFriends.FriendList.Contains(x.GetPlayerId()));


                            if (nonFiendsLeft >= roundsLeft)
                                if (allNotFriends is { Count: > 0 })
                                    mandatoryAttack = allNotFriends.FirstOrDefault().Player.Status.GetPlaceAtLeaderBoard();
                        

                        if (game.RoundNo == 1 && target.Player.GameCharacter.Name == "Осьминожка")
                            target.AttackPreference = 0;
                        break;
                    case "Толя":

                        var tolyaCount = bot.Passives.TolyaCount;

                        if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == target.GetPlayerId()))
                        {
                            if (target.AttackPreference >= 5)
                            {
                                target.AttackPreference *= 2;
                                target.AttackPreference += 7;
                            }
                            else
                            {
                                target.AttackPreference *= 2;
                            }
                            
                            
                            if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId()))
                                target.AttackPreference += 5;
                        }

                        if (tolyaCount.IsReadyToUse)
                        {
                            if (!isTargetTooGood)
                                target.AttackPreference = 13 - target.AttackPreference;
                        }
                        else
                        {
                            var jewAamount = 6;
                            if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1))
                                jewAamount = 2;

                            //Jew
                            foreach (var v in allTargets)
                                if (v.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                    target.AttackPreference += jewAamount;
                            //end Jew
                        }

                        break;

                    case "LeCrisp":
                        //Jew
                        foreach (var v in allTargets)
                            if (v.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                target.AttackPreference += 6;
                        //end Jew
                        break;


                    case "Глеб":
                        if (Smart(bot, game)
                            && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                        {
                            if (bot.Passives.YongGlebMetaClass.Contains(target.GetPlayerId()))
                            {
                                target.AttackPreference += 16;
                                if (target.AttackPreference > youngGlebBestPreference)
                                {
                                    youngGlebBestPreference = target.AttackPreference;
                                    mandatoryAttack = target.PlaceAtLeaderBoard();
                                }
                            }

                            if (bot.Passives.YongGlebTea.IsReadyToUse)
                            {
                                var chargedPortal = target.Player.GameCharacter.Name == "Рик Санчез"
                                                    && target.Player.Passives.RickPortalGun.Charges > 0;
                                target.AttackPreference += chargedPortal ? -8 : 5 + (6 - target.PlaceAtLeaderBoard());
                            }

                            // Old-Gleb sleep/challenger schedules remain in PassivesClass state but no longer
                            // dispatch after transformation; do not let them steer the Young form's AI.
                            break;
                        }
                        if (target.Player.Passives.GlebTeaTriggeredWhen.WhenToTrigger.Contains(game.RoundNo))
                        {
                            target.AttackPreference = 0;
                        }

                        //Во время претендента забывает о всех -5 и -7 за луз по статам, но вспоминает после окончания претендента.
                        var glebAcc = bot.Passives.GlebChallengerTriggeredWhen;

                       
                        if (glebAcc.WhenToTrigger.Contains(game.RoundNo))
                        {
                            if (isTargetTooGood)
                                target.AttackPreference += isTargetTooGoodNumber;
                            else if (isLostLastRoundAndTargetIsBetter)
                                target.AttackPreference += isLostLastRoundAndTargetIsBetterNumber;

                            //Под претендентом автоматически выбирает цель с наибольшим значением. 
                            var sorted = allTargets.OrderByDescending(x => x.AttackPreference).ToList();
                            mandatoryAttack = sorted.First().Player.Status.GetPlaceAtLeaderBoard();
                        }

                        break;
                    case "Загадочный Спартанец в маске":
                        /*if (game.RoundNo == 10)
                            if (target.Player.GameCharacter.Name == "Sirinoks")
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        */

                        var spartanMark = bot.Passives.SpartanMark;
                        var spartanShame = bot.Passives.SpartanShame;
                        if (spartanMark != null && spartanShame != null)
                        {
                            if (game.RoundNo <= 4)
                            {
                                if (spartanShame.FriendList.Contains(target.GetPlayerId()))
                                {
                                    target.AttackPreference -= 3;
                                }
                                else
                                {
                                    if (spartanMark.FriendList.Contains(target.GetPlayerId()))
                                        target.AttackPreference += 10;
                                }
                            }
                            else
                            {
                                if (!spartanMark.FriendList.Contains(target.GetPlayerId()))
                                {
                                    target.AttackPreference -= 4;
                                }
                                else
                                {
                                    if (bot.GameCharacter.Justice.GetRealJusticeNow() > targetJustice && !isTargetTooGood)
                                        if (target.AttackPreference > spartanTarget)
                                        {
                                            mandatoryAttack = target.PlaceAtLeaderBoard();
                                            spartanTarget = target.AttackPreference;
                                        }
                                }
                            }
                        }


                        break;
                    case "Сайтама":
                        var saitamaUn = bot.Passives.SaitamaUnnoticed;
                        if (game.RoundNo < 10)
                        {
                            // Prefer non-serious targets (Неприметность deferred points)
                            if (!saitamaUn.SeriousTargets.Contains(target.GetPlayerId()))
                                target.AttackPreference += 5;
                            else
                                target.AttackPreference -= 5;
                            // Prefer low-ranked targets (guaranteed deferred proc)
                            if (target.PlaceAtLeaderBoard() >= 4)
                                target.AttackPreference += 3;
                            // L2: SeriousTargets affects Saitama only while DEFENDING. On attack the real
                            // payoff is На мели: hit Мишень and be the sole attacker. Undo the legacy
                            // offense-only serious/low-place weights above, then value the actual trigger.
                            if (Smart(bot, game))
                            {
                                target.AttackPreference += saitamaUn.SeriousTargets.Contains(target.GetPlayerId()) ? 5 : -5;
                                if (target.PlaceAtLeaderBoard() >= 4)
                                    target.AttackPreference -= 3;
                                if (bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                                    target.AttackPreference += 10;
                                if (howManyAttackingTheSameTarget == 0)
                                    target.AttackPreference += 6;
                            }
                        }
                        if (game.RoundNo == 10 && target.PlaceAtLeaderBoard() == 1)
                            mandatoryAttack = 1;
                        break;
                    case "Toxic Mate":
                        var toxicCancer = bot.Passives.ToxicMateCancer;
                        if (!toxicCancer.IsActive && !toxicCancer.FirstLossTriggered)
                        {
                            // Phase 1: no losses yet — attack STRONGEST to LOSE (INT = +1 point per loss)
                            var targetPowerTm = target.Player.GameCharacter.GetStrength() + target.Player.GameCharacter.GetSpeed();
                            var botPowerTm = bot.GameCharacter.GetStrength() + bot.GameCharacter.GetSpeed();
                            if (targetPowerTm > botPowerTm)
                                target.AttackPreference += 8;
                        }
                        else if (!toxicCancer.IsActive && toxicCancer.FirstLossTriggered)
                        {
                            // Phase 2: need ONE win to start cancer — attack weakest
                            if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                target.AttackPreference += 10;
                            else
                                target.AttackPreference -= 5;
                        }
                        else if (toxicCancer.IsActive)
                        {
                            // Cancer active — attack leaders to LOSE (more INT points from top players)
                            if (target.PlaceAtLeaderBoard() <= 2)
                                target.AttackPreference += 8;
                            // Don't attack cancer holder (let them spread it naturally)
                            if (target.GetPlayerId() == toxicCancer.CurrentHolder)
                                target.AttackPreference -= 5;
                        }
                        break;
                    case "Dopa":
                        var dopaTacticAtk = bot.Passives.DopaMetaChoice.ChosenTactic;
                        var dopaVisionAtk = bot.Passives.DopaVision;
                        // Vision synergy: prefer targets being attacked by others
                        var attackersOnDopaTarget = allTargets
                            .Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()));

                        switch (dopaTacticAtk)
                        {
                            case "Стомп":
                                // Brawler: leverage +9 STR advantage, attack weak targets
                                if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                    target.AttackPreference += 7;
                                if (target.PlaceAtLeaderBoard() is 3 or 4)
                                    target.AttackPreference += 3;
                                if (attackersOnDopaTarget > 0 && dopaVisionAtk.Cooldown == 0)
                                    target.AttackPreference += 4;
                                break;
                            case "Фарм":
                                // Passive income: maximize Vision procs (+4 pts each)
                                if (attackersOnDopaTarget > 0 && dopaVisionAtk.Cooldown == 0)
                                {
                                    target.AttackPreference += 10;
                                    if (Smart(bot, game)) target.AttackPreference = Math.Max(target.AttackPreference, 30);
                                }
                                // Safe fights preferred
                                if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                    target.AttackPreference += 3;
                                break;
                            case "Доминация":
                                // Bully: win = +20 Skill + steal from enemy
                                if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                    target.AttackPreference += 8;
                                if (target.PlaceAtLeaderBoard() <= 2)
                                    target.AttackPreference += 5;
                                if (target.Player.GameCharacter.GetPsyche() <= 3)
                                    target.AttackPreference += 3;
                                if (attackersOnDopaTarget > 0 && dopaVisionAtk.Cooldown == 0)
                                    target.AttackPreference += 4;
                                break;
                            case "Роум":
                                // Roamer: win vs non-adjacent = steal bonus + moral
                                var dopaPlace = bot.Status.GetPlaceAtLeaderBoard();
                                var dopaTargetDist = Math.Abs(dopaPlace - target.PlaceAtLeaderBoard());
                                if (dopaTargetDist > 1)
                                {
                                    target.AttackPreference += 8;
                                    target.AttackPreference += dopaTargetDist * 2;
                                }
                                else
                                    target.AttackPreference -= 3;
                                if (target.Player.GameCharacter.GetMoral() > 5)
                                    target.AttackPreference += 3;
                                if (attackersOnDopaTarget > 0 && dopaVisionAtk.Cooldown == 0)
                                    target.AttackPreference += 4;
                                break;
                            default:
                                // Tactic not yet chosen — generic preference
                                if (target.PlaceAtLeaderBoard() <= 2)
                                    target.AttackPreference += 3;
                                break;
                        }
                        break;
                    case "Рик Санчез":
                        var rickBeans = bot.Passives.RickGiantBeans;
                        var rickGun = bot.Passives.RickPortalGun;
                        // Portal Gun charged — mandatory attack on #1
                        if (rickGun.Invented && rickGun.Charges > 0 && target.PlaceAtLeaderBoard() == 1)
                        {
                            mandatoryAttack = target.PlaceAtLeaderBoard();
                            break;
                        }
                        // Ingredient stacks require a WIN (attack or defense). L1 keeps its historical
                        // stronger-target guess; L2/L3 use the fight edge and the persistent Beans plan.
                        if (rickBeans.IngredientsActive && rickBeans.IngredientTargets.Contains(target.GetPlayerId()))
                        {
                            target.AttackPreference += 12;
                            if (Smart(bot, game))
                            {
                                var rickEdge = Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player)
                                    : target.AttackPreference - 10;
                                target.AttackPreference += rickEdge >= 0 ? 10 : -6;
                                if (HasPlaystyle(bot, "Beans"))
                                    target.AttackPreference += 4;
                            }
                            else if (game.RoundNo <= 6 && rickBeans.BeanStacks < 5)
                            {
                                var targetPowerRick = target.Player.GameCharacter.GetStrength() + target.Player.GameCharacter.GetSpeed();
                                var botPowerRick = bot.GameCharacter.GetStrength() + bot.GameCharacter.GetSpeed();
                                if (targetPowerRick > botPowerRick)
                                    target.AttackPreference += 5;
                            }
                        }
                        // Gun invented but no charges — avoid wasteful fights
                        if (rickGun.Invented && rickGun.Charges == 0)
                            target.AttackPreference -= 2;
                        break;
                    case "Итачи":
                        var itachiCrows = bot.Passives.ItachiCrows;
                        var itachiTsukuyomi = bot.Passives.ItachiTsukuyomi;
                        // Re-attacking the active Цукуеми target cancels the theft. Keep the two
                        // persistent plans distinct: Crows concentrates speed suppression; Tsukuyomi
                        // preserves the ledger and prioritizes high-income leaders when charged.
                        if (Smart(bot, game))
                        {
                            if (itachiTsukuyomi.TsukuyomiActiveTarget == target.GetPlayerId())
                                target.AttackPreference -= 25;
                            if (HasPlaystyle(bot, "Crows")
                                && itachiCrows.CrowCounts.ContainsKey(target.GetPlayerId()))
                                target.AttackPreference += 6;
                            if (HasPlaystyle(bot, "Tsukuyomi")
                                && itachiTsukuyomi.ChargeCounter >= 2
                                && target.PlaceAtLeaderBoard() <= 2)
                                target.AttackPreference += 8;
                        }
                        // Concentrate crow stacking: strongly prefer targets with existing crows
                        if (itachiCrows.CrowCounts.TryGetValue(target.GetPlayerId(), out var crows) && crows > 0)
                        {
                            target.AttackPreference += crows * 5;
                            if (crows >= 3)
                                target.AttackPreference += 10;
                        }
                        else if (itachiCrows.CrowCounts.Count(x => x.Value > 0) < 2)
                        {
                            // Haven't started stacking — pick fast enemies first
                            if (target.Player.GameCharacter.GetSpeed() >= bot.GameCharacter.GetSpeed())
                                target.AttackPreference += 4;
                        }
                        else
                        {
                            // Already stacking on targets — avoid splitting
                            target.AttackPreference -= 3;
                        }
                        // Amaterasu: strongly prefer adjacent leaderboard + faster
                        var itachiPos = bot.Status.GetPlaceAtLeaderBoard();
                        var itachiTargetPos = target.PlaceAtLeaderBoard();
                        if (Math.Abs(itachiPos - itachiTargetPos) == 1 &&
                            target.Player.GameCharacter.GetSpeed() < bot.GameCharacter.GetSpeed())
                            target.AttackPreference += 8;
                        // Tsukuyomi charged: prefer leaders
                        if (itachiTsukuyomi.ChargeCounter >= 2 && target.PlaceAtLeaderBoard() <= 2)
                            target.AttackPreference += 7;
                        // General speed advantage
                        if (target.Player.GameCharacter.GetSpeed() < bot.GameCharacter.GetSpeed())
                            target.AttackPreference += 3;
                        break;
                    case "Вампур":
                        if (target.Player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1))
                        {
                            if (isJusticeTheSame)
                            {
                                //Вампур не получает -5 same jst если враг проиграл на прошлом ходу
                                target.AttackPreference += isJusticeTheSameNumber;
                            }

                            if (isJusticeLessThanTarget)
                            {
                                //Вампур меняет -7 more jst на -4   если враг проиграл на прошлом ходу
                                target.AttackPreference += isJusticeLessThanTargetNumber;
                                target.AttackPreference -= 4;
                            }
                        }
                        //Преференс врагов += (их справедливость *2)
                        target.AttackPreference += targetJustice * 2;

                        var vampyrHematophagiaList = bot.Passives.VampyrHematophagiaList;
                        

                        if (vampyrHematophagiaList.HematophagiaCurrent.Count < 5)
                        {
                            if (vampyrHematophagiaList.HematophagiaCurrent.Any(x => x.EnemyId == target.GetPlayerId()))
                            {
                                    //Вампур получает -3 всем на ком запрокан укус, если укусов еще не 5.
                                    target.AttackPreference -= 3;
                            }
                        }
                        

                        if (bot.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == target.GetPlayerId()))
                            target.AttackPreference = 0;
                        break;
                    // Dead Khokhol-legacy Salldorum targeting removed (C1) — the live current-kit
                    // targeting is the "Salldorum" (Chronicler) case further down this switch.
                    case "Napoleon Wonnafcuk":
                        var napBotAlliance = bot.Passives.NapoleonAlliance;
                        if (napBotAlliance.AllyId == Guid.Empty)
                        {
                            // No ally yet — pick mid-ranked player as ally (first attack forms alliance)
                            if (target.PlaceAtLeaderBoard() is 3 or 4)
                                target.AttackPreference += 8;
                            else if (target.PlaceAtLeaderBoard() is 2 or 5)
                                target.AttackPreference += 4;
                            if (target.PlaceAtLeaderBoard() is 1 or 6)
                                target.AttackPreference -= 5;
                        }
                        else
                        {
                            // Never attack ally
                            if (target.GetPlayerId() == napBotAlliance.AllyId)
                            {
                                target.AttackPreference = 0;
                                break;
                            }
                            // Joint attack synergy: prefer same target as ally
                            var napBotAlly = game.PlayersList.Find(x => x.GetPlayerId() == napBotAlliance.AllyId);
                            if (napBotAlly != null && napBotAlly.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId()))
                                target.AttackPreference += 15;
                            // Завоеватель: prefer targets BETWEEN Napoleon and ally on leaderboard
                            if (napBotAlly != null)
                            {
                                var napPlace = bot.Status.GetPlaceAtLeaderBoard();
                                var allyPlace = napBotAlly.Status.GetPlaceAtLeaderBoard();
                                var minP = Math.Min(napPlace, allyPlace);
                                var maxP = Math.Max(napPlace, allyPlace);
                                if (target.PlaceAtLeaderBoard() > minP && target.PlaceAtLeaderBoard() < maxP)
                                    target.AttackPreference += 5;
                            }
                            // Treaty targets: lower priority (they can't win against us anyway)
                            if (bot.Passives.NapoleonPeaceTreaty.TreatyEnemies.Contains(target.GetPlayerId()))
                                target.AttackPreference -= 3;
                        }
                        break;
                    case "Таинственный Суппорт":
                        var supportMark = bot.Passives.SupportPremade;
                        if (Smart(bot, game) && supportMark.MarkedPlayerId != Guid.Empty)
                        {
                            if (HasPlaystyle(bot, "Carry") && target.GetPlayerId() == supportMark.MarkedPlayerId)
                                target.AttackPreference += 8;
                            if (HasPlaystyle(bot, "Stakes") && game.RoundNo % 3 == 0
                                && target.GetPlayerId() != supportMark.MarkedPlayerId)
                                target.AttackPreference += 8;
                        }
                        if (supportMark.MarkedPlayerId == Guid.Empty)
                        {
                            // No carry yet — mark strongest player (highest total stats)
                            var candidatePower = target.Player.GameCharacter.GetStrength() +
                                                target.Player.GameCharacter.GetIntelligence() +
                                                target.Player.GameCharacter.GetSpeed();
                            target.AttackPreference += candidatePower;
                            if (target.PlaceAtLeaderBoard() <= 2)
                                target.AttackPreference += 10;
                        }
                        else
                        {
                            // Stakes rounds (3, 6, 9): prefer non-Carry for +1 point
                            if (game.RoundNo % 3 == 0 && target.GetPlayerId() != supportMark.MarkedPlayerId)
                            {
                                target.AttackPreference += 12;
                                if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                    target.AttackPreference += 5;
                            }
                            // Non-stakes: buff Carry
                            else if (target.GetPlayerId() == supportMark.MarkedPlayerId)
                                target.AttackPreference += 15;
                            // Late game: if Carry falling, shift to self-scoring
                            if (game.RoundNo >= 8)
                            {
                                var carry = game.PlayersList.Find(x => x.GetPlayerId() == supportMark.MarkedPlayerId);
                                if (carry != null && carry.Status.GetPlaceAtLeaderBoard() > 3 &&
                                    target.GetPlayerId() != supportMark.MarkedPlayerId)
                                    target.AttackPreference += 5;
                            }
                        }
                        break;

                    case "Стая Гоблинов":
                        var gobPop = bot.Passives.GoblinPopulation;
                        if (Smart(bot, game))
                        {
                            if (HasPlaystyle(bot, "Ziggurat")
                                && target.Player.GameCharacter.Passive.Any(p =>
                                    p.Standalone && p.PassiveName != "Еврей"
                                    && !bot.Passives.GoblinZiggurat.LearnedPassives.Contains(p.PassiveName)))
                                target.AttackPreference += 12;
                            if (HasPlaystyle(bot, "Economy") && target.PlaceAtLeaderBoard() is 1 or 2 or 6)
                                target.AttackPreference += 6;
                            if (HasPlaystyle(bot, "Army") && (Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player) >= 0
                                    : target.AttackPreference >= 10))
                                target.AttackPreference += 6;
                            if (HasPlaystyle(bot, "Horde") && (Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player) >= 0
                                    : target.AttackPreference >= 10))
                                target.AttackPreference += 4;
                        }
                        // Mine positions (1, 2, 6): scale with worker count
                        if (target.PlaceAtLeaderBoard() is 1 or 2 or 6)
                            target.AttackPreference += 3 + gobPop.WorkerUpgradeLevel;
                        // Early game: prefer winnable fights (army growth)
                        if (game.RoundNo <= 4)
                        {
                            if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                                target.AttackPreference += 7;
                            else if (target.Player.GameCharacter.GetStrength() > bot.GameCharacter.GetStrength() + 2)
                                target.AttackPreference -= 5;
                        }
                        // Mid-late: mine + winnable
                        if (game.RoundNo >= 5 && target.PlaceAtLeaderBoard() is 1 or 2 or 6 &&
                            target.Player.GameCharacter.GetStrength() <= bot.GameCharacter.GetStrength() + 1)
                            target.AttackPreference += 8;
                        // Avoid much stronger (loss = army death)
                        var gobStrDiff = target.Player.GameCharacter.GetStrength() - bot.GameCharacter.GetStrength();
                        if (gobStrDiff > 3)
                            target.AttackPreference -= gobStrDiff;
                        break;

                    case "Котики":
                        if (Smart(bot, game))
                        {
                            if (HasPlaystyle(bot, "Ambush")
                                && target.Player.Passives.KotikiCatOwnerId == bot.GetPlayerId())
                                target.AttackPreference += 10;
                            if (HasPlaystyle(bot, "Storm")
                                && !bot.Passives.KotikiStorm.TauntedPlayers.Contains(target.GetPlayerId()))
                                target.AttackPreference += 5;
                        }
                        // Cat on target: highest priority (collect cat back for bonus)
                        if (target.Player.Passives.KotikiCatOwnerId == bot.GetPlayerId())
                            target.AttackPreference += 20;
                        // Штормяк: prefer high-stat enemies for taunt steal value
                        var kotikiMaxStat = Math.Max(
                            Math.Max(target.Player.GameCharacter.GetIntelligence(), target.Player.GameCharacter.GetStrength()),
                            Math.Max(target.Player.GameCharacter.GetSpeed(), target.Player.GameCharacter.GetPsyche()));
                        target.AttackPreference += kotikiMaxStat / 2;
                        // Prefer weaker for win chance
                        if (target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                            target.AttackPreference += 4;
                        // Reduce preference for already-taunted
                        if (bot.Passives.KotikiStorm.TauntedPlayers.Contains(target.GetPlayerId()))
                            target.AttackPreference -= 3;
                        break;

                    case "Монстр без имени":
                        var monsterTargetJustice = target.Player.GameCharacter.Justice.GetSeenJusticeNow();
                        if (Smart(bot, game))
                        {
                            var isStatTwin = bot.GameCharacter.GetIntelligence() == target.Player.GameCharacter.GetIntelligence()
                                             || bot.GameCharacter.GetStrength() == target.Player.GameCharacter.GetStrength()
                                             || bot.GameCharacter.GetSpeed() == target.Player.GameCharacter.GetSpeed()
                                             || bot.GameCharacter.GetPsyche() == target.Player.GameCharacter.GetPsyche();
                            if (isStatTwin)
                                target.AttackPreference -= 8; // Близнец: attacking a twin costs 1 Psyche
                            if (HasPlaystyle(bot, "Apocalypse") && target.PlaceAtLeaderBoard() <= 2)
                                target.AttackPreference += 5;
                        }
                        // Prefer high-Justice targets (steal via Близнец block)
                        if (monsterTargetJustice > 0)
                            target.AttackPreference += monsterTargetJustice * 2;
                        // Round 10: prefer attacking (apocalypse strategy)
                        if (game.RoundNo == 10)
                        {
                            target.AttackPreference += 10;
                            if (target.PlaceAtLeaderBoard() <= 2)
                                target.AttackPreference += 5;
                        }
                        // When attacking: prefer 0-Justice (Близнец stat match is less risky)
                        if (monsterTargetJustice == 0)
                            target.AttackPreference += 3;
                        break;

                    case "TheBoys":
                        var botFrancie = bot.Passives.TheBoysFrancie;
                        var botButcher = bot.Passives.TheBoysButcher;
                        if (Smart(bot, game))
                        {
                            if (HasPlaystyle(bot, "Francie") && botFrancie.ChemWeaponLevel > 0
                                && (Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player) >= 0
                                    : target.AttackPreference >= 10))
                                target.AttackPreference += 5;
                            if (HasPlaystyle(bot, "Butcher") && target.Player.Passives.TheBoysSupMark)
                                target.AttackPreference += 8;
                            if (HasPlaystyle(bot, "M.M.")
                                && bot.Passives.TheBoysMM.NextAttackGathersKompromat
                                && !bot.Passives.TheBoysMM.KompromatTargets.Contains(target.GetPlayerId()))
                                target.AttackPreference += 10;
                            if (HasPlaystyle(bot, "Kimiko"))
                            {
                                var kimikoEdge = Omni(bot, game)
                                    ? EstimateOmniFightEdge(bot, target.Player)
                                    : target.AttackPreference - 10;
                                if (kimikoEdge >= 0)
                                    target.AttackPreference += 8;
                                else if (kimikoEdge <= -5)
                                    target.AttackPreference -= 8;
                            }
                        }
                        // ORDER COMPLETION is priority
                        if (botFrancie.OrderTarget == target.Player.GetPlayerId())
                        {
                            target.AttackPreference += 20;
                            // Urgent: only 1 round left
                            if (botFrancie.OrderRoundsLeft == 1)
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        }
                        // Kompromat gathering: prefer untouched targets
                        if (bot.Passives.TheBoysMM.NextAttackGathersKompromat &&
                            !bot.Passives.TheBoysMM.KompromatTargets.Contains(target.Player.GetPlayerId()))
                            target.AttackPreference += 10;
                        // Chemical weapon: prefer beatable targets (win = +ChemWeaponLevel bonus)
                        if (botFrancie.ChemWeaponLevel > 0 &&
                            target.Player.GameCharacter.GetStrength() < bot.GameCharacter.GetStrength())
                            target.AttackPreference += botFrancie.ChemWeaponLevel * 2;
                        // Poker multiplier: prefer skill targets
                        if (botButcher.PokerCount > 0 && bot.GameCharacter.HasSkillTargetOn(target.Player.GameCharacter))
                            target.AttackPreference += 3;
                        // Butcher: prefer hunting marked "sups" (+Skill, очко за дроп)
                        if (target.Player.Passives.TheBoysSupMark)
                            target.AttackPreference += 8;
                        // Смертельный вирус: prefer a target to plant/spread the virus
                        if (botFrancie.VirusArmed)
                            target.AttackPreference += 6;
                        // Prefer leaders
                        if (target.Player.Status.GetPlaceAtLeaderBoard() <= 3)
                            target.AttackPreference += 3;
                        break;

                    case "Продавец Сомнительных Тактик":
                        var sellerVAtk = bot.Passives.SellerVparitGovna;
                        if (sellerVAtk.Cooldown <= 0)
                        {
                            // CD ready: prefer UNMARKED targets (spread marks)
                            if (!sellerVAtk.MarkedPlayers.Contains(target.GetPlayerId()))
                            {
                                target.AttackPreference += 10;
                                // Phase 3 (L2): the Seller has 0 stats and wins ~no fights, but the mark applies
                                // on ATTACK regardless of win/loss — so spreading a mark to a fresh enemy beats any
                                // "winnable fight" the generic offense terms found. Set a dominant floor so the
                                // loss-aversion / L2-5 avoidance / L2-12 commit don't steer it to a bad target.
                                // (L1 keeps its +10 and pilots the Seller better than the pre-fix L3 — the bug.)
                                if (Smart(bot, game) && target.AttackPreference < SmartSellerMarkFloor)
                                    target.AttackPreference = SmartSellerMarkFloor;
                            }
                            else
                                target.AttackPreference -= 5;
                        }
                        else
                        {
                            // CD active: prefer beatable targets
                            if (target.Player.GameCharacter.GetSkill() < bot.GameCharacter.GetSkill())
                                target.AttackPreference += 3;
                        }
                        // Round 10: target marked players (steal 50% via Выгодная сделка)
                        if (game.RoundNo == 10 && sellerVAtk.MarkedPlayers.Contains(target.GetPlayerId()))
                        {
                            target.AttackPreference += (int)(target.Player.Status.GetScore() / 2);
                            if (target.PlaceAtLeaderBoard() == 1)
                                target.AttackPreference += 15;
                        }
                        break;

                    case "Salldorum":
                        // Великий летописец: prefer player who won most 3 rounds ago (x3 skill)
                        if (game.RoundNo > 3)
                        {
                            var chroniclerRound = game.RoundNo - 3;
                            var winCounts = game.PlayersList
                                .SelectMany(player => player.Status.WhoToLostEveryRound)
                                .Where(loss => loss.RoundNo == chroniclerRound)
                                .GroupBy(loss => loss.EnemyId)
                                .ToDictionary(group => group.Key, group => group.Count());
                            if (winCounts.Count > 0
                                && winCounts.TryGetValue(target.GetPlayerId(), out var targetWins)
                                && targetWins == winCounts.Values.Max())
                                target.AttackPreference += targetWins * 5 + 3;
                        }
                        // A charged Шэн makes the next attack occupy the selected target's cell.
                        if (bot.Passives.SalldorumShen.Charges > 0
                            && target.Player.Status.GetPlaceAtLeaderBoard() < bot.Status.GetPlaceAtLeaderBoard())
                            target.AttackPreference += 8;
                        // Prefer 0 Justice
                        if (target.Player.GameCharacter.Justice.GetRealJusticeNow() == 0)
                            target.AttackPreference += 3;
                        break;

                    case "Геральт":
                        var geraltBotContracts = bot.Passives.GeraltContracts;
                        var geraltBotOilAtk = bot.Passives.GeraltOil;
                        var targetMonster = target.Player.Passives.GeraltMonsterType;
                        if (targetMonster != null)
                        {
                            var contractCount = geraltBotContracts.GetCount(targetMonster.Value);
                            var oilTier = geraltBotOilAtk.GetTier(targetMonster.Value);
                            // Base contract bonus
                            target.AttackPreference += contractCount * 4;
                            // Oil synergy when applied
                            if (geraltBotOilAtk.IsOilApplied)
                            {
                                target.AttackPreference += oilTier * 3;
                                if (oilTier >= 3) target.AttackPreference += 10;
                            }
                            // High contracts: consuming gives extra fights + skill
                            if (contractCount >= 3) target.AttackPreference += 8;
                        }
                        else
                        {
                            // No monster type (5th enemy) — lower priority
                            target.AttackPreference -= 5;
                        }
                        // Plotva speed bonus: prefer targets where rank > target+1
                        var geraltPos = bot.Status.GetPlaceAtLeaderBoard();
                        var geraltTargetPos = target.PlaceAtLeaderBoard();
                        if (geraltPos > geraltTargetPos + 1)
                        {
                            target.AttackPreference += 4;
                            var enemiesBetween = allTargets.Count(x =>
                                x.PlaceAtLeaderBoard() > geraltTargetPos && x.PlaceAtLeaderBoard() < geraltPos);
                            target.AttackPreference += enemiesBetween * 2;
                        }
                        // Prefer leaders (invoice rewards attacking top positions)
                        if (geraltTargetPos <= 2)
                            target.AttackPreference += 3;
                        break;
                    case "Баг":
                        if (!Smart(bot, game))
                            break;

                        // Exploit is a global pot. Patching early permanently removes a carrier and usually
                        // cashes almost nothing; let losses accumulate, then guarantee the round-10 cash-out.
                        if (target.Player.Passives.IsExploitable)
                        {
                            if (game.RoundNo == 10)
                            {
                                target.AttackPreference = Math.Max(target.AttackPreference, 40 + game.TotalExploit);
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                            }
                            else
                            {
                                target.AttackPreference = 0;
                            }

                            break;
                        }

                        // PointFunnel copies this target's win points. Prefer players who have committed
                        // outgoing attacks, especially multi-attackers and (at L3) visibly favorable fights.
                        var funnelTargets = target.Player.Status.WhoToAttackThisTurn
                            .Select(id => game.PlayersList.Find(x => x.GetPlayerId() == id))
                            .Where(x => x != null && x.GetPlayerId() != target.GetPlayerId())
                            .ToList();
                        target.AttackPreference += funnelTargets.Count * 5;
                        if (Omni(bot, game))
                            target.AttackPreference += funnelTargets.Count(x =>
                                EstimateOmniFightEdge(target.Player, x!) >= 0) * 4;
                        if (target.Player.Status.IsBlock || target.Player.Status.IsSkip)
                            target.AttackPreference = 0;
                        break;
                }
                //end custom bot behavior


                //custom enemy
                switch (target.Player.GameCharacter.Name)
                {
                    case "Darksci":
                        if (game.RoundNo == 9)
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.GetAllGlobalLogs()
                            .Contains(
                                $"Толя запизделся и спалил, что {target.Player.DiscordUsername} - {target.Player.GameCharacter.Name}"))
                        {
                            if (target.AttackPreference > 5) target.AttackPreference += 5;
                            if (target.AttackPreference > 7) target.AttackPreference += 5;
                        }

                        if (game.PlayersList.Any(x => x.GameCharacter.Name == "mylorik"))
                        {
                            var mylorik = game.PlayersList.Find(x => x.GameCharacter.Name == "mylorik");
                            if (game.GetAllGlobalLogs().Contains($"{target.Player.DiscordUsername} психанул") &&
                                game.GetAllGlobalLogs().Contains($"{mylorik.DiscordUsername} психанул"))
                            {
                                if (target.AttackPreference > 5) target.AttackPreference += 5;
                                if (target.AttackPreference > 7) target.AttackPreference += 5;
                            }
                        }

                        if (bot.GameCharacter.Name is "DeepList" or "AWDKA")
                        {
                            if (target.AttackPreference >= 7)
                            {
                                target.AttackPreference += 4;
                            }
                        }

                        break;
                    case "HardKitty":
                        if (game.RoundNo <= 4) target.AttackPreference /= 5;
                        break;
                    case "Sirinoks":
                        if (game.RoundNo <= 4) target.AttackPreference -= 4;

                        if (game.RoundNo == 10) target.AttackPreference -= 1;
                        break;
                    case "Вампур":
                        if (botJustice <= targetJustice)
                            target.AttackPreference += 3;
                        break;
                    case "Глеб":
                        //если есть старый глеб в игре, то толя бот кидает подсчет на 9м ходу на глеба, если место Толи в лидерборде <=3 (от топ1 до топ3) 
                        if (game.RoundNo == 9 && bot.GameCharacter.Name == "Толя" && bot.Status.GetPlaceAtLeaderBoard() <= 3)
                        {
                            var tolyaCount = bot.Passives.TolyaCount;
                            if (tolyaCount.IsReadyToUse)
                                mandatoryAttack = target.PlaceAtLeaderBoard();
                        }


                        var glebChallender = target.Player.Passives.GlebChallengerTriggeredWhen;
                        var glebSleeping = target.Player.Passives.GlebSleepingTriggeredWhen;

                        var totalChallengers = 0;
                        var totalSleeps = 0;

                        for (var i = 1; i < game.RoundNo + 1; i++)
                            if (glebChallender.WhenToTrigger.Contains(i))
                                totalChallengers++;
                        for (var i = 1; i < game.RoundNo + 1; i++)
                            if (glebSleeping.WhenToTrigger.Contains(i))
                                totalSleeps++;

                        if (totalChallengers >= 1 && totalSleeps >= 2) target.AttackPreference /= 2;

                        break;
                }
                //end custom enemy

                //для всех ботов: если бот предположил братишку, то -1 преференс для врагов, которые рядом с братишкой по таблице. например братишка на 3м месте, значит нам нужно 3 - 1 и 3 +1 = 2 и 4. им преференс -1
                if (bot.Predict.Any(x => x.CharacterName == "Братишка"))
                {
                    var shark = game.PlayersList.Find(p => p.GetPlayerId() == bot.Predict.Find(pr => pr.CharacterName == "Братишка")!.PlayerId);
                    if (shark != null)
                    {
                        //6-5 = 1
                        //3-4 = -1
                        //5-4 = 1
                        //1-2 = -1
                        var placeDiff = target.PlaceAtLeaderBoard() - shark.Status.GetPlaceAtLeaderBoard();
                        if (placeDiff is 1 or -1)
                        {
                            target.AttackPreference -= 1;
                        }
                    }
                }

                // L2-5: avoid attacking into predicted punish-passives (uses bot.Predict beyond the Братишка rule)
                if (Smart(bot, game))
                {
                    var predictWeight = Omni(bot, game) ? OmniPredictConfidence : 1;   // L3-1: predictions are certain
                    var predicted = bot.Predict.Find(x => x.PlayerId == target.GetPlayerId());
                    if (predicted != null)
                    {
                        // Краборак «Панцирь»: first attack per enemy = auto-block, +3 моральки +33 скилла ему
                        if (predicted.CharacterName == "Краборак"
                            && !bot.Status.WhoToLostEveryRound.Any(x => x.EnemyId == target.GetPlayerId())
                            && !target.Player.Status.WhoToLostEveryRound.Any(x => x.EnemyId == bot.GetPlayerId()))
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;

                        // Толя «Раммус»: мораль = attackers² — don't join a pile on Толя
                        if (predicted.CharacterName == "Толя"
                            && allTargets.Any(x => x.Player.Status.WhoToAttackThisTurn.Contains(target.GetPlayerId())))
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;

                        // ── Phase 1: opponent-awareness — don't feed the enemy's kit (per opposing character) ──
                        // Осьминожка «Неуязвимость»: a slippery/invulnerable target — attacking wastes the turn
                        // and the win can be flipped away from you.
                        if (predicted.CharacterName == "Осьминожка")
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;

                        // Монстр без имени: attacking feeds his Близнец justice-steal and his drop economy.
                        if (predicted.CharacterName == "Монстр без имени")
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;

                        // Toxic Mate: beating him hands him Intelligence (lose-to-win) — don't feed a beatable one.
                        if (predicted.CharacterName == "Toxic Mate")
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;

                        // mylorik: attacking-and-losing him plants a revenge mark on you (he then hunts you) —
                        // avoid handing him the first mark.
                        if (predicted.CharacterName == "mylorik"
                            && !target.Player.Status.WhoToLostEveryRound.Any(x => x.EnemyId == bot.GetPlayerId()))
                            target.AttackPreference -= SmartPredictAvoidNumber * predictWeight;
                    }
                }

            }

            
            foreach (var target2 in allTargets)
            {
                if(target2.AttackPreference >= 6)
                {
                    if (allTargets.Where(x => x.GetPlayerId() != target2.GetPlayerId()).ToList()
                        .All(x => x.AttackPreference < target2.AttackPreference))
                    {
                        target2.AttackPreference += 2;
                        // L2-12 (bolder targeting): the cumulative weighted-random pick dilutes good heuristics —
                        // commit harder to a clearly-best target by amplifying its share, so smart bots reliably
                        // take the strongest fight the nemesis/versatility/Мишень/crush terms already identified.
                        // Phase 3: exempt Толя — its bespoke targeting already ×2's + inverts (13−pref) + piles;
                        // a second multiply over-commits it to one target and breaks that plan (measured −0.48
                        // L3-vs-L1 regression). Other multiplier chars (mylorik ×3 etc.) measured positive, so
                        // they keep the commit.
                        if (Smart(bot, game) && bot.GameCharacter.Name != "Толя")
                            target2.AttackPreference *= SmartCommitMultiplier;
                    }
                }


                switch (bot.GameCharacter.Name)
                {
                    case "mylorik":
                        //Если кол-во врагов с запроканным лузом но без победы = кол-во оставшихся ходов, то преференс ДРУГИХ врагов / 2
                        var mylorikRevenge = bot.Passives.MylorikRevenge;
                        if (mylorikRevenge != null)
                        {
                            var totalFinishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => x.IsUnique).Count;
                            var roundsLeft = 11 - game.RoundNo;
                            if (totalFinishedRevenges >= roundsLeft)
                            {
                                if (!mylorikRevenge.EnemyListPlayerIds.Any(x => x.IsUnique && x.EnemyPlayerId == target2.GetPlayerId()))
                                {
                                    if (target2.AttackPreference >= 2)
                                    {
                                        target2.AttackPreference /= 2;
                                    }
                                }
                            }
                        }
                        break;
                }
            }

            if (game.Teams.Count > 0)
            {
                //team bot behavior. target == team member
                foreach (var target3 in allTargets)
                {
                    if (!bot.IsTeamMember(game, target3.GetPlayerId()))
                        continue;

                    var realAttackPreference = target3.AttackPreference;
                    target3.AttackPreference = 0;

                    //custom bot behavior in teams. target == team member
                    switch (bot.GameCharacter.Name)
                    {
                        case "DeepList":
                            var deepListMockeryList = bot.Passives.DeepListMockeryList;

                            var currentDeepList2 =
                                deepListMockeryList?.WhoWonTimes.Find(x => x.EnemyPlayerId == target3.GetPlayerId());

                            if (currentDeepList2 is { Times: 1 }) target3.AttackPreference = realAttackPreference;

                            break;
                        case "Тигр":
                            break;
                        case "AWDKA":
                            target3.AttackPreference = realAttackPreference;
                            break;
                        case "HardKitty":
                            break;
                        case "Darksci":
                            var darksciLucky = bot.Passives.DarksciLuckyList;
                            if (darksciLucky != null)
                            {
                               if(!darksciLucky.TouchedPlayers.Contains(target3.GetPlayerId()))
                                   target3.AttackPreference = realAttackPreference;
                            }
                            break;
                        case "Злой Школьник":
                            break;
                        case "mylorik":
                            var mylorikRevenge = bot.Passives.MylorikRevenge;
                            if (mylorikRevenge != null)
                            {
                                var revengeEnemy = mylorikRevenge.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == target3.GetPlayerId());

                                //ноль выключается если союзнику можно отмстить
                                if (revengeEnemy is { IsUnique: true }) target3.AttackPreference = realAttackPreference;

                                //если отмстил уже всем врагам, то выключается ноль на союзниках, которым еще не мстил
                                var finishedRevenges = mylorikRevenge.EnemyListPlayerIds.FindAll(x => !x.IsUnique);
                                var teamCount = game.GetTeammates(bot).Count;
                                if (finishedRevenges.Count >= 5 - teamCount)
                                {
                                    if (finishedRevenges.All(x => x.EnemyPlayerId != target3.GetPlayerId()))
                                    {
                                        target3.AttackPreference = realAttackPreference;
                                    }
                                }
                            }
                            break;
                        case "Краборак":
                            break;
                        case "Братишка":
                            break;
                        case "Sirinoks":
                            //До начала 5го хода может нападать только на одну цель - союзника
                            var siriFriends = bot.Passives.SirinoksFriendsList;
                            if (siriFriends.FriendList.Count == 1 && game.RoundNo < 5)
                            {
                                if (siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    mandatoryAttack = target3.PlaceAtLeaderBoard();

                            }
                            else if (game.RoundNo < 5)
                            {
                                var teammates = game.GetTeammates(bot);
                                mandatoryAttack = game.PlayersList.Find(x => x.GetPlayerId() == teammates[0]).Status.GetPlaceAtLeaderBoard();
                            }
                            else
                            {
                                //. снимается ноль с тех, кто не в друзьях.
                                if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                    target3.AttackPreference = realAttackPreference;

                                //снимается 0 со всех тех, кто подходит под мишень.
                                if (bot.GameCharacter.HasSkillTargetOn(target3.Player.GameCharacter))
                                    target3.AttackPreference = realAttackPreference;

                                //если кол-во оставшихся ходов - 3 <= союзникам в друзьях, то нападает только на союзников которых можно добавить в друзья. (выбирает из них по мишени. если под мишень не подходит, то выбирает рандомно)
                                if (game.RoundNo < 9)
                                {
                                    if (!siriFriends.FriendList.Contains(target3.GetPlayerId()))
                                        if (bot.GameCharacter.HasSkillTargetOn(target3.Player.GameCharacter))
                                            mandatoryAttack = target3.PlaceAtLeaderBoard();
                                }
                            }

                            break;
                        case "Толя":
                            var enemyCount = allTargets.Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(target3.GetPlayerId()));
                            if (enemyCount >= 2)
                                target3.AttackPreference = realAttackPreference;
                            break;
                        case "LeCrisp":
                            enemyCount = allTargets.Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(target3.GetPlayerId()));
                            if (enemyCount >= 2)
                                target3.AttackPreference = realAttackPreference;
                            break;
                        case "Глеб":
                            break;
                        case "Загадочный Спартанец в маске":
                            break;
                        case "Вампур":
                            var vampyrHematophagiaList = bot.Passives.VampyrHematophagiaList;

                            if (vampyrHematophagiaList != null)
                            {
                                if (vampyrHematophagiaList.HematophagiaCurrent.All(x => x.EnemyId != target3.GetPlayerId()))
                                {
                                    target3.AttackPreference = realAttackPreference;
                                }
                            }
                            break;
                    }
                    //end custom bot behavior
                }

                
                //team bot behavior. target == enemy
                foreach (var target4 in allTargets)
                {
                    if (bot.IsTeamMember(game, target4.GetPlayerId()))
                        continue;

                    //custom bot behavior in teams. target == enemy
                    switch (bot.GameCharacter.Name)
                    {
                        case "AWDKA":
                            var platCount = 0;
                            var teamCount = 0;

                            // 0 на всех врагов, нападает только на союзников, чтобы проебать им, пока платина не будет на всех союзниках, либо пока не наступит 7ой ход
                            var awdkaTrying = bot.Passives.AwdkaTryingList;
                            if (awdkaTrying != null)
                            {
                                foreach (var teammate in game.GetTeammates(bot))
                                {
                                    teamCount++;
                                    var awdkaTryingTarget = awdkaTrying.TryingList.Find(x => x.EnemyPlayerId == teammate);
                                    if (awdkaTryingTarget is { IsUnique: true })
                                    {
                                        platCount++;
                                    }
                                }
                            }
                            if (platCount != teamCount && game.RoundNo < 7)
                                target4.AttackPreference = 0;
                            break;
                    }
                    //end custom bot behavior
                }
            }


            //count maxRandomNumber and isBlock
            foreach (var target2 in allTargets)
            {
                if (target2.AttackPreference <= 0)
                {
                    isBlock--;
                    target2.AttackPreference = 0;
                }

                maxRandomNumber += target2.AttackPreference;
            }

            //end calculation Tens


            //custom behaviour After calculation Tens
            switch (bot.GameCharacter.Name)
            {
                case "Тигр":
                    var tigr = bot.Passives.TigrThreeZeroList;
                    if (game.RoundNo == 4)
                            if (tigr.FriendList.Any(x => x.WinsSeries == 2 && x.IsUnique))
                                isBlock = yesBlock;

                    var lowRates = allTargets.Where(x => x.AttackPreference <= 3).ToList();
                    var countLowRate = 0;
                    if (lowRates.Count() >= 2)
                    {
                        foreach (var lowRate in lowRates)
                            if (tigr.FriendList.Any(x =>
                                    x.EnemyPlayerId == lowRate.GetPlayerId() && x.WinsSeries == 2 && x.IsUnique))
                                countLowRate++;

                        if (countLowRate >= 2) isBlock = yesBlock;
                    }
                    


                    break;
                case "AWDKA":
                    isBlock = noBlock;
                    break;
                case "Сайтама":
                    if (game.RoundNo == 10)
                    {
                        // Round 10: MUST attack #1 — never block
                        isBlock = noBlock;
                        var firstPlace = allTargets.Find(x => x.Player.Status.GetPlaceAtLeaderBoard() == 1);
                        if (firstPlace != null && mandatoryAttack == -1)
                            mandatoryAttack = 1;
                    }
                    else if (game.RoundNo <= 3)
                    {
                        // Early: block often (stay unnoticed, accumulate deferred bonus)
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    else if (game.RoundNo <= 6)
                    {
                        // Mid-game: occasionally block
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 5;
                    }
                    else
                    {
                        // Rounds 7-9: attack to accumulate deferred points before round 10 payoff
                        isBlock = noBlock;
                    }
                    break;
                case "Darksci":
                    minimumRandomNumberForBlock += 1;
                    if (game.RoundNo > 1 && botJustice == 0) minimumRandomNumberForBlock += 1;

                    if (game.RoundNo is 3 or 5 or 9)
                        if (bot.GameCharacter.GetPsyche() <= 1)
                        {
                            minimumRandomNumberForBlock = 4;
                            maximumRandomNumberForBlock = 5;
                        }

                    var darksciLucky = bot.Passives.DarksciLuckyList;

                    if (Smart(bot, game) && HasPlaystyle(bot, "Unstable") && !darksciLucky.Triggered
                        && darksciUnstableBestEdge >= -3 && bot.GameCharacter.GetPsyche() > 2)
                        isBlock = noBlock;
         
                        var notTouched = 5 - darksciLucky.TouchedPlayers.Count;
                        var roundsLeft = 11 - (game.RoundNo + 3);
                        if (notTouched >= roundsLeft)
                            if (darksciLucky.TouchedPlayers.Count < 5)
                                if (mandatoryAttack == -1)
                                {
                                    var listofTargets = allTargets.Where(x =>
                                        !darksciLucky.TouchedPlayers.Contains(x.GetPlayerId())).ToList();

                                    if (listofTargets.Count > 0)
                                        mandatoryAttack = listofTargets.First().PlaceAtLeaderBoard();
                                }
                    

                    break;
                case "Братишка":
                    if (botJustice != 5)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }

                    // No valid targets (all opponents dead / round-10 Тигр-ban) — skip the Justice
                    // block-nudge; the bot blocks via the isBlock==0 path. See AUDIT-FINDINGS M16/M14.
                    if (allTargets.Count > 0)
                    {
                        var min = allTargets.Min(x => x.Player.GameCharacter.Justice.GetSeenJusticeNow());
                        var check = allTargets.Find(x =>
                            x.Player.GameCharacter.Justice.GetSeenJusticeNow() == min);

                        if (check.Player.GameCharacter.Justice.GetSeenJusticeNow() >= botJustice)
                            minimumRandomNumberForBlock += 1;
                    }

                    break;
                case "Осьминожка":
                    isBlock = noBlock;
                    break;
                case "HardKitty":
                    isBlock = noBlock;
                    var hardKitty = bot.Passives.HardKittyDoebatsya;

                    if (allTargets.All(x => x.AttackPreference <= 3) && mandatoryAttack == -1)
                    {
                        var doebatsya = hardKitty.LostSeriesCurrent
                            .Where(x => allTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).FirstOrDefault();
                        if (doebathsyaTarget != null)
                            mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                                ?.PlaceAtLeaderBoard() ?? mandatoryAttack;
                    }

                    if (allTargets.Any(x => x.AttackPreference > 5) && mandatoryAttack == -1)
                    {
                        var doebathsyaTargets = allTargets.Where(x => x.AttackPreference > 5)
                            .OrderByDescending(x => x.AttackPreference).ToList();
                        var doebatsya = hardKitty.LostSeriesCurrent
                            .Where(x => doebathsyaTargets.Any(y => y.GetPlayerId() == x.EnemyPlayerId)).ToList();
                        var doebathsyaTarget = doebatsya.OrderByDescending(x => x.Series).FirstOrDefault();
                        if (doebathsyaTarget != null)
                            mandatoryAttack = allTargets.Find(x => x.GetPlayerId() == doebathsyaTarget.EnemyPlayerId)
                                ?.PlaceAtLeaderBoard() ?? mandatoryAttack;
                    }


                    break;
                case "Глеб":
                    isBlock = noBlock;
                    break;
                case "Краборак":
                    isBlock = noBlock;
                    break;
                case "Злой Школьник":
                    switch (game.RoundNo)
                    {
                        case < 8:
                            isBlock = noBlock;
                            break;
                        case 10:
                            minimumRandomNumberForBlock = 3;
                            maximumRandomNumberForBlock = 4;
                            break;
                    }

                    break;
                case "mylorik":
                    isBlock = noBlock;
                    break;

                case "Итачи":
                    isBlock = noBlock;
                    break;

                case "Toxic Mate":
                    isBlock = noBlock;
                    break;

                case "Кратос":
                    if (Smart(bot, game))
                        isBlock = noBlock; // blades/Мишень/event plans all require an attack
                    break;

                case "Dopa":
                    // Макро needs 2 attacks per round — never block
                    isBlock = noBlock;
                    break;

                case "DeepList":
                    var deepList = bot.Passives.DeepListMadnessTriggeredWhen;
                    if (deepList.WhenToTrigger.Contains(game.RoundNo))
                    {
                        isBlock = noBlock;
                    }
                    break;

                case "Sirinoks":
                    if (game.RoundNo is 10 or 1)
                    {
                        isBlock = noBlock;
                    }
                    /*
                    else if (bot.Passives.SirinoksTraining.Training.Count == 0)
                    {
                        var siriFriends = bot.Passives.SirinoksFriendsList;
                        var siriFriend = allTargets.Find(x => x.GetPlayerId() == siriFriends?.FriendList.FirstOrDefault());
                        if(siriFriend != null)
                            if (siriFriend.Player.GameCharacter.Name != "Осьминожка")
                                mandatoryAttack = siriFriend.Player.Status.GetPlaceAtLeaderBoard();
                    }
                    */

                    break;

                case "Загадочный Спартанец в маске":
                    //на последнем ходу блок -2 (от 2 до 5)
                    if (game.RoundNo < 10)
                    {
                        isBlock = noBlock;
                    }

                    if (allTargets.All(x =>
                            bot.GameCharacter.Justice.GetRealJusticeNow() <=
                            x.Player.GameCharacter.Justice.GetSeenJusticeNow()))
                        if (game.RoundNo == 10)
                        {
                            minimumRandomNumberForBlock = 2;
                            maximumRandomNumberForBlock = 4;
                        }

                    // end на последнем ходу блок -2 (от 2 до 5)
                    break;

                case "Рик Санчез":
                    var rickPickle = bot.Passives.RickPickle;
                    var rickGun2 = bot.Passives.RickPortalGun;
                    // Never block when pickle is active or on penalty cooldown
                    if (rickPickle.PickleTurnsRemaining > 0 || rickPickle.PenaltyTurnsRemaining > 0)
                        isBlock = noBlock;
                    // If portal gun is charged, never block — always attack
                    else if (rickGun2.Invented && rickGun2.Charges > 0)
                        isBlock = noBlock;
                    // Block when 3+ opponents likely to attack (low stats scenario)
                    else if (bot.GameCharacter.GetStrength() + bot.GameCharacter.GetSpeed() + bot.GameCharacter.GetPsyche() <= 6)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    else
                        isBlock = noBlock;
                    break;
                case "Толя":
                    //rammus
                    var count = allTargets.FindAll(x => x.AttackPreference >= 10).Count;
                    if (count <= 0)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    //end rammus

                    var tolyaCount = bot.Passives.TolyaCount;

                    if (tolyaCount.IsReadyToUse)
                        if (game.RoundNo is 3 or 8)
                            isBlock = yesBlock;
                    if (Smart(bot, game))
                    {
                        var incomingOnTolya = allTargets.Count(x =>
                            x.Player.Status.WhoToAttackThisTurn.Contains(bot.GetPlayerId()));
                        if (HasPlaystyle(bot, "Rammus") && incomingOnTolya > 0)
                            isBlock = yesBlock; // auto-win all incoming fights; moral = attackers²
                        else if (HasPlaystyle(bot, "Count") && tolyaCount.IsReadyToUse)
                            isBlock = noBlock;  // apply Подсчет instead of wasting its ready round
                    }
                    break;


                case "LeCrisp":
                    //block chances
                    if (game.RoundNo <= 4)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }

                    if (game.RoundNo == 1)
                    {
                        minimumRandomNumberForBlock = 3;
                        maximumRandomNumberForBlock = 4;
                    }

                    var assassinsCount = allTargets
                        .FindAll(x => bot.GameCharacter.GetStrength() - x.Player.GameCharacter.GetStrength() <= -2).Count;

                    minimumRandomNumberForBlock += assassinsCount;

                    //end block chances
                    break;

                case "Napoleon Wonnafcuk":
                    var napAllianceBlock = bot.Passives.NapoleonAlliance;
                    if (napAllianceBlock.AllyId == Guid.Empty)
                    {
                        // No ally yet: must attack to form alliance
                        isBlock = noBlock;
                    }
                    else
                    {
                        // Block for treaty registration when multiple attackers
                        var attackersOnNap = allTargets.Count(x =>
                            x.Player.Status.WhoToAttackThisTurn.Contains(bot.GetPlayerId()));
                        if (attackersOnNap >= 2)
                        {
                            minimumRandomNumberForBlock = 3;
                            maximumRandomNumberForBlock = 4;
                        }
                        else if (game.RoundNo % 3 == 0)
                        {
                            minimumRandomNumberForBlock = 2;
                            maximumRandomNumberForBlock = 4;
                        }
                    }
                    break;

                case "Таинственный Суппорт":
                    var supportMarkBlock = bot.Passives.SupportPremade;
                    if (supportMarkBlock.MarkedPlayerId == Guid.Empty)
                    {
                        isBlock = noBlock; // Must attack to mark carry
                    }
                    else if (game.RoundNo % 3 == 0)
                    {
                        isBlock = noBlock; // Stakes round: must attack non-Carry
                    }
                    else if (game.RoundNo % 2 == 0)
                    {
                        // Block for Justice (Protect passive)
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 3;
                    }
                    if (Smart(bot, game) && supportMarkBlock.MarkedPlayerId != Guid.Empty
                        && HasPlaystyle(bot, "Carry") && game.RoundNo % 3 != 0)
                        isBlock = noBlock; // keep buffing the Carry instead of banking self-Justice
                    break;

                case "Продавец Сомнительных Тактик":
                    var sellerVBlock = bot.Passives.SellerVparitGovna;
                    if (sellerVBlock.Cooldown <= 0)
                        isBlock = noBlock; // Must attack to apply mark
                    else
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 5;
                    }
                    break;

                case "Стая Гоблинов":
                    var gobBotPop = bot.Passives.GoblinPopulation;
                    var gobBotZig = bot.Passives.GoblinZiggurat;
                    var gobBotPlace = bot.Status.GetPlaceAtLeaderBoard();
                    var gobCanBuild = gobBotPop.Warriors >= 1 && gobBotPop.Hobs >= 1 && gobBotPop.Workers >= 1
                                     && bot.Status.GetScore() >= 3
                                     && !gobBotZig.BuiltPositions.Contains(gobBotPlace);
                    // Build ziggurat on mine position (best value)
                    if (gobCanBuild && gobBotPlace is 1 or 2 or 6)
                        isBlock = yesBlock;
                    // Late game: build anywhere if haven't built yet
                    else if (gobCanBuild && game.RoundNo >= 7 && gobBotZig.BuiltPositions.Count == 0)
                        isBlock = yesBlock;
                    // Low population: consider defensive blocking
                    else if (gobBotPop.TotalGoblins < 10)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    if (Smart(bot, game) && HasPlaystyle(bot, "Ziggurat")
                        && gobCanBuild && game.RoundNo >= 3)
                        isBlock = yesBlock; // establish locks/passive-learning across several positions
                    break;

                case "Котики":
                    var kotikiStormBlock = bot.Passives.KotikiStorm;
                    var untauntedBlock = game.PlayersList.Count(p =>
                        p.GetPlayerId() != bot.GetPlayerId() &&
                        !p.Passives.IsDead &&
                        !kotikiStormBlock.TauntedPlayers.Contains(p.GetPlayerId()));
                    // Block every 3rd round for taunt (when untaunted enemies exist)
                    if (untauntedBlock > 0 && game.RoundNo >= 2 && game.RoundNo % 3 == 0)
                        isBlock = yesBlock;
                    // Also block when cats deployed on enemies (can't collect this turn)
                    else if (bot.Passives.KotikiAmbush.MinkaOnPlayer != Guid.Empty ||
                             bot.Passives.KotikiAmbush.StormOnPlayer != Guid.Empty)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    // Late game: attack to collect cats and score
                    if (game.RoundNo >= 8)
                        isBlock = noBlock;
                    if (Smart(bot, game))
                    {
                        var catsDeployed = bot.Passives.KotikiAmbush.MinkaOnPlayer != Guid.Empty
                                           || bot.Passives.KotikiAmbush.StormOnPlayer != Guid.Empty;
                        if (HasPlaystyle(bot, "Ambush") && catsDeployed)
                            isBlock = noBlock; // re-attack the carrier to recover and cash the cat
                        else if (HasPlaystyle(bot, "Storm") && catsDeployed)
                            isBlock = noBlock; // let Storm accrue score, then retrieve it before taunting again
                    }
                    break;

                case "Монстр без имени":
                    if (game.RoundNo == 10)
                    {
                        // Round 10: don't block — apocalypse strategy (bait enemies into fighting)
                        isBlock = noBlock;
                    }
                    else if (game.RoundNo <= 2)
                    {
                        // Early: always block (accumulate Justice via Близнец)
                        isBlock = yesBlock;
                    }
                    else
                    {
                        // Mid/late: heavily prefer blocking
                        minimumRandomNumberForBlock = 3;
                        maximumRandomNumberForBlock = 4;
                        // If no enemies have Justice, attacking is more valuable
                        if (allTargets.All(x => x.Player.GameCharacter.Justice.GetSeenJusticeNow() == 0))
                        {
                            minimumRandomNumberForBlock = 2;
                            maximumRandomNumberForBlock = 5;
                        }
                    }
                    if (Smart(bot, game))
                    {
                        if (HasPlaystyle(bot, "Apocalypse") && game.RoundNo >= 3)
                            isBlock = noBlock; // spread Монстр's no-block mark before the final landscape
                        else if (HasPlaystyle(bot, "Twin") && allTargets.Any(x =>
                                     x.Player.Status.WhoToAttackThisTurn.Contains(bot.GetPlayerId())
                                     && x.Player.GameCharacter.Justice.GetRealJusticeNow() > 0))
                            isBlock = yesBlock; // steal every incoming attacker's real Justice
                    }
                    break;

                case "TheBoys":
                    // TheBoys never blocks — orders have deadlines
                    isBlock = noBlock;
                    break;

                case "Sakura":
                    if (Smart(bot, game) && game.RoundNo >= 8)
                    {
                        var sakuraPlace = bot.Status.GetPlaceAtLeaderBoard();
                        var incoming = allTargets.Count(x =>
                            x.Player.Status.WhoToAttackThisTurn.Contains(bot.GetPlayerId()));
                        var fourth = game.PlayersList.FirstOrDefault(x =>
                            x.Status.GetPlaceAtLeaderBoard() == 4);
                        var topThreeCushion = fourth == null
                            ? decimal.MaxValue
                            : bot.Status.GetScore() - fourth.Status.GetScore();

                        // Top three is Sakura's win condition, but unconditional late blocking measured
                        // worse: it surrendered too many ×2/×4 attacks. Defend only a narrow, visible threat.
                        if (sakuraPlace <= 3 && game.RoundNo == 10 && incoming > 0 && topThreeCushion <= 4)
                            isBlock = yesBlock;
                        else
                            isBlock = noBlock;
                    }
                    break;

                case "Баг":
                    if (Smart(bot, game) && game.RoundNo == 10
                        && game.ExploitPlayersList.Any(x => x.Passives.IsExploitable))
                        isBlock = noBlock;
                    break;

                case "Salldorum":
                    var salCapsule = bot.Passives.SalldorumTimeCapsule;
                    // Block once early for capsule burial
                    if (!salCapsule.FirstBlockUsed && game.RoundNo <= 3)
                        isBlock = yesBlock;
                    // Rewrite history through the same resolver humans use.
                    if (!bot.Passives.SalldorumChronicler.HistoryRewritten && game.RoundNo >= 5 && game.RoundNo < 8)
                    {
                        var bestRound = Enumerable.Range(1, game.RoundNo - 1)
                            .OrderByDescending(round => bot.Status.WhoToLostEveryRound
                                .Where(loss => loss.RoundNo == round)
                                .Select(loss => loss.EnemyId)
                                .Distinct()
                                .Count() * (round <= 4 ? 1 : 2))
                            .First();
                        Salldorum.RewriteHistory(bot, game, bestRound);
                    }
                    break;

                case "Геральт":
                    var geraltBotOil = bot.Passives.GeraltOil;
                    var geraltMedBlock = bot.Passives.GeraltMeditation;
                    // Oil not applied — must meditate
                    if (!geraltBotOil.IsOilApplied)
                        isBlock = yesBlock;
                    // Still unrevealed enemies early — meditate for senses
                    else if (geraltMedBlock.RevealedEnemies.Count < 3 && game.RoundNo <= 6)
                    {
                        minimumRandomNumberForBlock = 2;
                        maximumRandomNumberForBlock = 4;
                    }
                    else
                        isBlock = noBlock;
                    // Late game: never block (need to fight for contracts)
                    if (game.RoundNo >= 8)
                        isBlock = noBlock;
                    break;
            }

            // Монстр без имени: bots in 1st place force block on round 10
            if (game.RoundNo == 10 && bot.Status.GetPlaceAtLeaderBoard() == 1
                && game.PlayersList.Any(p => p.GameCharacter.Name == "Монстр без имени")
                && isBlock != noBlock)
            {
                isBlock = yesBlock;
            }

            // L2-6: round-10 block economics — leader defends the crown, everyone else attacks (×4 round,
            // justice is worthless now). Untouched-generic guard preserves every bespoke round-10 block rule.
            if (Smart(bot, game) && game.RoundNo == 10 && isBlock != noBlock && isBlock != yesBlock
                && minimumRandomNumberForBlock == 1 && maximumRandomNumberForBlock == 4)
            {
                if (bot.Status.GetPlaceAtLeaderBoard() == 1) isBlock = yesBlock;   // defend the crown
                else isBlock = noBlock;                                            // ×4 round: justice is worthless now, attack
            }

            // L2-8: at 0 justice you lose every tiebreak and get milked by Умный attackers; if no target
            // scored ≥6 the heuristics found no good fight — raise the block-roll floor 1→2 (rounds 2-9).
            if (Smart(bot, game) && game.RoundNo is >= 2 and <= 9 && botJustice == 0
                && isBlock != noBlock && isBlock != yesBlock
                && minimumRandomNumberForBlock == 1 && maximumRandomNumberForBlock == 4
                && allTargets.All(x => x.AttackPreference < 6))
            {
                minimumRandomNumberForBlock = 2;
            }

            // L2-13 (block economics): leader under fire. A place-1/2 smart bot with ≥2 known incoming
            // attackers and no crush of its own gains more from BLOCKING — it cancels those fights (no drop,
            // denies each attacker their win + a bonus point) and banks +1 justice — than from a marginal
            // attack. Rounds 2-9 only (round 10 is L2-6); only the untouched-generic block case.
            if (Smart(bot, game) && game.RoundNo is >= 2 and <= 9
                && bot.Status.GetPlaceAtLeaderBoard() <= 2
                && isBlock != noBlock && isBlock != yesBlock
                && minimumRandomNumberForBlock == 1 && maximumRandomNumberForBlock == 4
                && allTargets.Count(x => x.Player.Status.WhoToAttackThisTurn.Contains(bot.GetPlayerId())) >= 2
                && allTargets.All(x => x.AttackPreference < 8))
            {
                minimumRandomNumberForBlock = 3;
            }

            // L2-14 (block economics): losing + low justice with no good fight — bank justice for a comeback.
            // A place ≥4 bot at justice ≤ 1 with no target ≥ 6 gains more from blocking (+1 justice next round,
            // the underdog tiebreak/roll-window fuel) than from a coin-flip attack it will likely lose. Extends
            // L2-8 (which only covers justice == 0) to the losing-low-justice case.
            if (Smart(bot, game) && game.RoundNo is >= 2 and <= 9
                && bot.Status.GetPlaceAtLeaderBoard() >= 4 && botJustice is > 0 and <= 1
                && isBlock != noBlock && isBlock != yesBlock
                && minimumRandomNumberForBlock == 1 && maximumRandomNumberForBlock == 4
                && allTargets.All(x => x.AttackPreference < 6))
            {
                minimumRandomNumberForBlock = 2;
            }

            //end custom behaviour After calculation Tens


            //mandatory attack
            var isAttacked = false;
            if (mandatoryAttack >= 0) isAttacked = await AttackPlayer(bot, mandatoryAttack);

            //block
            if (minimumRandomNumberForBlock > maximumRandomNumberForBlock)
                maximumRandomNumberForBlock = minimumRandomNumberForBlock;

            var isBlockCheck = _rand.Random(minimumRandomNumberForBlock, maximumRandomNumberForBlock);
            if (isBlockCheck > isBlock && !isAttacked && mandatoryAttack == -1)
            {
                //block
                await _gameReaction.HandleAttack(bot, null, -10);
                ResetTens(allTargets);
                return;
            }

            //"random" attack
            var randomNumber = _rand.Random(1, Math.Max(1, (int)Math.Ceiling(maxRandomNumber)));

            decimal totalPreference = 0;
            int whoToAttack;
            foreach (var target in allTargets)
            {
                totalPreference += target.AttackPreference;
                var rounded = (int)Math.Ceiling(totalPreference);
                if (randomNumber > rounded || isAttacked) continue;
                whoToAttack = target.Player.Status.GetPlaceAtLeaderBoard();
                isAttacked = await AttackPlayer(bot, whoToAttack);
            }


            if (!isAttacked && isBlock == noBlock)
            {
                var players = allTargets.ToList();
                if (players.Count == 0)
                {
                    // No valid targets left (everyone else dead / round-10 banned) — block instead of
                    // indexing an empty list. This threw IndexOutOfRange, masked by the Discord NRE
                    // into a frozen game. Mirrors the block-and-return above. See AUDIT-FINDINGS M14.
                    await _gameReaction.HandleAttack(bot, null, -10);
                    ResetTens(allTargets);
                    return;
                }
                whoToAttack = players[_rand.Random(0, players.Count - 1)].Player.Status.GetPlaceAtLeaderBoard();

                if (maxRandomNumber > 0)
                    await _global.TrySendServiceMessage(
                        $"**{bot.GameCharacter.Name}** Поставил блок, а ему нельзя. {randomNumber}/{maxRandomNumber} <= {totalPreference}\n" +
                        $"Round: {game.RoundNo}\n" +
                        $"Randomly Attacking {allTargets.Find(x => x.Player.Status.GetPlaceAtLeaderBoard() == whoToAttack).Player.GameCharacter.Name}");

                await AttackPlayer(bot, whoToAttack);
            }
            else if (!isAttacked)
            {
                var passives = bot.GameCharacter.Passive.Aggregate("(", (current, passive) => current + $"{passive.PassiveName}, ");
                passives = passives.Remove(passives.Length - 2);
                passives += ")";
                await _global.TrySendServiceMessage(
                    $"**{bot.GameCharacter.Name}** {passives} не напал ни на кого.\n" +
                    $"Round: {game.RoundNo}\n");
                await _gameReaction.HandleAttack(bot, null, -10);
            }

            // Dopa Макро — bot needs second attack (smart Vision-aware targeting)
            if (isAttacked && !bot.Status.IsReady
                && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
            {
                var secondTargets = allTargets.Where(x =>
                    !bot.Status.WhoToAttackThisTurn.Contains(x.Player.GetPlayerId())).ToList();
                if (secondTargets.Any())
                {
                    // Try to trigger Vision: find target that is fighting our first target
                    var firstTargetId = bot.Status.WhoToAttackThisTurn.FirstOrDefault();
                    var visionTarget = secondTargets.Find(x =>
                        x.Player.Status.WhoToAttackThisTurn.Contains(firstTargetId));

                    if (visionTarget != null && bot.Passives.DopaVision.Cooldown == 0)
                    {
                        await AttackPlayer(bot, visionTarget.Player.Status.GetPlaceAtLeaderBoard());
                    }
                    else
                    {
                        // Fallback: prefer target being attacked by most others (busy = more Vision potential)
                        var bestTarget = secondTargets
                            .OrderByDescending(x => allTargets.Count(a =>
                                a.Player.Status.WhoToAttackThisTurn.Contains(x.GetPlayerId())))
                            .First();
                        await AttackPlayer(bot, bestTarget.Player.Status.GetPlaceAtLeaderBoard());
                    }
                }
                else if (allTargets.Any())
                {
                    var pick = allTargets[_rand.Random(0, allTargets.Count - 1)];
                    await AttackPlayer(bot, pick.Player.Status.GetPlaceAtLeaderBoard());
                }
            }

            ResetTens(allTargets);
        }
        catch (Exception e)
        {
            // Log + report to the sim harness FIRST so a genuine exception is never masked by the
            // diagnostic send (which no-ops when Discord is offline). See docs/AUDIT-FINDINGS.md M13.
            _logs.Critical(e.Message);
            _logs.Critical(e.StackTrace);
            _global.SimErrorSink?.Invoke(game.GameId, game.RoundNo, e);
            await _global.TrySendServiceMessage($"{e.Message}\n{e.StackTrace}");
        }
    }

    // L0 (Dumb): pure-random attack/block baseline for experiments. No strategy — but respects every
    // genuine constraint: characters that literally can't block, targets that can't be attacked, and the
    // Макро two-attack rule (so games never freeze).
    private async Task HandleBotAttackRandom(GamePlayerBridgeClass bot, GameClass game, List<Nanobot> allTargets)
    {
        // No valid target (all dead / round-10 banned) → block (mirrors the M14 empty-target fallback).
        if (allTargets.Count == 0)
        {
            await _gameReaction.HandleAttack(bot, null, -10);
            return;
        }

        // "cannot block" = the two hard passives the block button itself rejects (GameReactions.cs:316-326).
        // Монстр-marked players are force-attacked by the engine regardless (CheckIfReady.cs:1266-1289) —
        // legal either way — so no extra guard is needed for that case.
        var canBlock = !bot.GameCharacter.Passive.Any(x =>
            x.PassiveName == "Спарта" || x.PassiveName == "Aggress");

        // Uniform over {N targets, +1 block slot if allowed}: ~1/(N+1) block chance.
        var slots = allTargets.Count + (canBlock ? 1 : 0);
        if (canBlock && _rand.Random(1, slots) == slots)
        {
            await _gameReaction.HandleAttack(bot, null, -10);
            return;
        }

        // Attack a random target; if HandleAttack rejects it (pet pair / Vampyr re-attack / round-10 ban)
        // drop it and retry another. HandleAttack returning false = "cannot attack this target".
        var pool = allTargets.ToList();
        var attacked = false;
        while (pool.Count > 0 && !attacked)
        {
            var i = _rand.Random(0, pool.Count - 1);
            var pick = pool[i];
            pool.RemoveAt(i);
            attacked = await AttackPlayer(bot, pick.PlaceAtLeaderBoard());
        }

        // Макро (Dopa): the first attack returns true but does NOT set IsReady — without a second attack the
        // turn never completes and the round can freeze. Replicate the smart path's second-attack safeguard.
        if (attacked && !bot.Status.IsReady
            && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
        {
            var second = allTargets
                .Where(x => !bot.Status.WhoToAttackThisTurn.Contains(x.GetPlayerId())).ToList();
            if (second.Count > 0)
                await AttackPlayer(bot, second[_rand.Random(0, second.Count - 1)].PlaceAtLeaderBoard());
        }

        // Nothing attackable → block (engine force-attacks anyway if the bot legitimately can't block).
        if (!attacked)
            await _gameReaction.HandleAttack(bot, null, -10);
    }

    public async Task<bool> AttackPlayer(GamePlayerBridgeClass bot, int whoToAttack)
    {
        return await _gameReaction.HandleAttack(bot, null, whoToAttack);
    }

    public void ResetTens(List<Nanobot> nanobots)
    {
        foreach (var p in nanobots) p.AttackPreference = 10;
    }


    public async Task HandleLvlUpBot(GamePlayerBridgeClass player, GameClass game)
    {
        // L0 (Dumb): random level-up among legal (non-maxed) stats. Mirrors the smart path's <10 filter
        // and all-maxed →4 fallback (so no new infinite-loop risk), just without the character heuristics.
        if (Dumb(player, game))
        {
            while (player.Status.LvlUpPoints > 0)
            {
                var options = new List<int>();
                if (player.GameCharacter.GetIntelligence() < 10) options.Add(1);
                if (player.GameCharacter.GetStrength() < 10) options.Add(2);
                if (player.GameCharacter.GetSpeed() < 10) options.Add(3);
                if (player.GameCharacter.GetPsyche() < 10) options.Add(4);
                var pick = options.Count > 0 ? options[_rand.Random(0, options.Count - 1)] : 4;
                await _gameReaction.HandleLvlUp(player, null, pick);
            }

            player.Status.MoveListPage = 1;
            return;
        }

        do
        {
            int skillNumber;

            var intelligence = player.GameCharacter.GetIntelligence();
            var strength = player.GameCharacter.GetStrength();
            var speed = player.GameCharacter.GetSpeed();
            var psyche = player.GameCharacter.GetPsyche();

            var stats = new List<BiggestStatClass>
            {
                new(1, intelligence),
                new(2, strength),
                new(3, speed),
                new(4, psyche)
            };

            stats = stats.OrderByDescending(x => x.StatCount).ToList();

            if (stats.First().StatCount < 10)
                skillNumber = stats.First().StatIndex;
            else if (stats[1].StatCount < 10)
                skillNumber = stats[1].StatIndex;
            else if (stats[2].StatCount < 10)
                skillNumber = stats[2].StatIndex;
            else if (stats[3].StatCount < 10)
                skillNumber = stats[3].StatIndex;
            else
                skillNumber = 4;

            // L2-11: keep a minimum Psyche before over-stacking one offensive stat — a broken Psyche pool
            // costs −20% Мораль and low Psyche invites tilt/skip passives + loses the ±psyche fight term.
            // Only nudges the generic pick (the per-character builds below still win) and only once the
            // top stat is already tall, so it doesn't slow a character's core stat race.
            if (Smart(player, game) && psyche < SmartPsycheFloor && stats.First().StatCount >= 8)
                skillNumber = 4;

            //game.RoundNo is 3 or 5 or 7 or 9

            if (player.GameCharacter.Name == "Толя" && strength < 8) skillNumber = 2;
            if (Smart(player, game) && player.GameCharacter.Name == "Толя")
            {
                if (HasPlaystyle(player, "Count"))
                    skillNumber = strength < 10 ? 2 : 1;
                else if (HasPlaystyle(player, "Rammus"))
                    skillNumber = psyche < 8 ? 4 : 1;
            }
            if (player.GameCharacter.Name == "Sirinoks" && intelligence < 10) skillNumber = 1;
            if (player.GameCharacter.Name == "Вампур" && psyche < 10) skillNumber = 4;
            if (player.GameCharacter.Name == "mylorik" && psyche < 10) skillNumber = 4;
            if (player.GameCharacter.Name == "Братишка" && strength < 10) skillNumber = 2;

            if (player.GameCharacter.Name == "LeCrisp" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Darksci" && psyche < 10) skillNumber = 4;

            if (player.GameCharacter.Name == "Злой Школьник" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Злой Школьник" && intelligence == 9) skillNumber = 1;
            if (player.GameCharacter.Name == "Злой Школьник" && strength == 10 && intelligence < 10) skillNumber = 1;

            if (player.GameCharacter.Name == "HardKitty" && speed < 10 && game.RoundNo < 6) skillNumber = 3;
            if (player.GameCharacter.Name == "HardKitty" && psyche < 10 && game.RoundNo > 6) skillNumber = 4;

            if (player.GameCharacter.Name == "Тигр" && psyche >= game.RoundNo && intelligence < 10) skillNumber = 1;
            if (player.GameCharacter.Name == "Тигр" && psyche < game.RoundNo) skillNumber = 4;

            if (player.GameCharacter.Name == "Глеб" && strength < 10) skillNumber = 2;
            if (player.GameCharacter.Name == "Глеб" && intelligence == 9) skillNumber = 1;
            if (Smart(player, game) && player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                skillNumber = 2; // concentrate forced nerfs in STR; preserve INT/Speed/Psyche 8 and versatility

            if (player.GameCharacter.Name == "Weedwick")
            {
                if(speed < 5) skillNumber = 3;
                else if (psyche < 10) skillNumber = 4;
                else skillNumber = 1;
            }

            if (player.GameCharacter.Name == "Загадочный Спартанец в маске" && psyche < 10 && game.RoundNo <= 3) skillNumber = 4;
            if (player.GameCharacter.Name == "Загадочный Спартанец в маске" && speed < 10 && game.RoundNo > 3) skillNumber = 3;

            // Сайтама — STR for round 10 fight, then PSY
            if (player.GameCharacter.Name == "Сайтама" && strength < 10) skillNumber = 2;
            else if (player.GameCharacter.Name == "Сайтама" && psyche < 10) skillNumber = 4;

            if (Smart(player, game) && player.GameCharacter.Name == "TheBoys")
            {
                skillNumber = HasPlaystyle(player, "Francie") ? 1
                    : HasPlaystyle(player, "Butcher") ? 2
                    : HasPlaystyle(player, "Kimiko") ? 3
                    : 4; // M.M.; four focused upgrades unlock the selected ultimate
            }

            if (Smart(player, game) && player.GameCharacter.Name == "Кратос")
            {
                if (HasPlaystyle(player, "GodHunter"))
                    skillNumber = strength < 10 ? 2 : speed < 10 ? 3 : 1;
                else
                    skillNumber = speed < 10 ? 3 : strength < 10 ? 2 : 1;
            }

            if (Smart(player, game) && player.GameCharacter.Name == "Монстр без имени")
            {
                if (HasPlaystyle(player, "Apocalypse"))
                    skillNumber = speed < 8 ? 3
                        : strength < 8 ? 2
                        : intelligence < 10 ? 1
                        : psyche < 10 ? 4
                        : speed < 10 ? 3
                        : 2;
                else
                    skillNumber = intelligence < 10 ? 1
                        : psyche < 10 ? 4
                        : strength < 10 ? 2
                        : speed < 10 ? 3
                        : 4;
            }
            // Rick Sanchez — prioritize INT for portal gun invention (30+ INT needed)
            if (player.GameCharacter.Name == "Рик Санчез") skillNumber = 1;

            // Itachi: Crows races Speed for Аматерасу; Tsukuyomi builds reliable fight scale.
            if (player.GameCharacter.Name == "Итачи")
            {
                if (Smart(player, game) && HasPlaystyle(player, "Tsukuyomi"))
                {
                    if (intelligence < 10) skillNumber = 1;
                    else if (psyche < 10) skillNumber = 4;
                    else skillNumber = 3;
                }
                else
                {
                    if (speed < 10) skillNumber = 3;
                    else if (intelligence < 10) skillNumber = 1;
                    else skillNumber = 4;
                }
            }

            // Таинственный Суппорт — STR to win fights
            if (player.GameCharacter.Name == "Таинственный Суппорт" && strength < 10) skillNumber = 2;

            // Продавец — with 10x multiplier: prioritize INT for skill, then PSY, STR, SPD
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Закуп"))
            {
                if (intelligence < 10) skillNumber = 1;
                else if (psyche < 10) skillNumber = 4;
                else if (strength < 10) skillNumber = 2;
                else skillNumber = 3;
            }

            // Dopa's game-start tactic is a persistent playstyle and needs its own build.
            if (player.GameCharacter.Name == "Dopa")
            {
                var dopaTactic = player.Passives.DopaMetaChoice.ChosenTactic;
                if (Smart(player, game) && dopaTactic == "Доминация")
                    skillNumber = strength < 10 ? 2 : speed < 10 ? 3 : 1;
                else if (Smart(player, game) && dopaTactic == "Роум")
                    skillNumber = speed < 10 ? 3 : strength < 8 ? 2 : 1;
                else if (Smart(player, game) && dopaTactic == "Фарм")
                    skillNumber = speed < 8 ? 3 : psyche < 10 ? 4 : 1;
                else
                {
                    if (intelligence < 10) skillNumber = 1;
                    else if (psyche < 10) skillNumber = 4;
                    else if (speed < 10) skillNumber = 3;
                    else skillNumber = 2;
                }
            }

            // Salldorum — PSY-focused build
            if (player.GameCharacter.Name == "Salldorum" && psyche < 10) skillNumber = 4;

            // Napoleon — PSY-focused build
            if (player.GameCharacter.Name == "Napoleon Wonnafcuk" && psyche < 10) skillNumber = 4;

            // Геральт — upgrade oil for type with most contracts AND lowest tier
            if (player.GameCharacter.Name == "Геральт")
            {
                var geraltBotOilLvl = player.Passives.GeraltOil;
                var geraltBotContractsLvl = player.Passives.GeraltContracts;
                var oilOptions = new[]
                {
                    (type: 1, tier: geraltBotOilLvl.DrownersOilTier, contracts: geraltBotContractsLvl.Drowners),
                    (type: 2, tier: geraltBotOilLvl.WerewolvesOilTier, contracts: geraltBotContractsLvl.Werewolves),
                    (type: 3, tier: geraltBotOilLvl.VampiresOilTier, contracts: geraltBotContractsLvl.Vampires),
                    (type: 4, tier: geraltBotOilLvl.DragonsOilTier, contracts: geraltBotContractsLvl.Dragons),
                };
                // Prefer type with most contracts that isn't maxed
                var bestOil = oilOptions
                    .Where(x => x.tier < 3)
                    .OrderByDescending(x => x.contracts)
                    .ThenBy(x => x.tier)
                    .FirstOrDefault();
                if (bestOil != default) skillNumber = bestOil.type;
                else skillNumber = 1;
            }

            // Стая Гоблинов: each persistent plan spends all four custom upgrades coherently.
            if (player.GameCharacter.Name == "Стая Гоблинов")
            {
                var gobPop = player.Passives.GoblinPopulation;
                if (Smart(player, game) && HasPlaystyle(player, "Horde"))
                {
                    if (gobPop.HobUpgradeLevel < 3) skillNumber = 1;
                    else if (!gobPop.FestivalUsed) skillNumber = 4;
                    else skillNumber = 2;
                }
                else if (Smart(player, game) && HasPlaystyle(player, "Army"))
                {
                    if (gobPop.WarriorUpgradeLevel < 3) skillNumber = 2;
                    else if (!gobPop.FestivalUsed) skillNumber = 4;
                    else skillNumber = 3;
                }
                else if (Smart(player, game) && HasPlaystyle(player, "Economy"))
                {
                    if (gobPop.WorkerUpgradeLevel < 3) skillNumber = 3;
                    else if (!gobPop.FestivalUsed) skillNumber = 4;
                    else skillNumber = 1;
                }
                else if (Smart(player, game) && HasPlaystyle(player, "Ziggurat"))
                {
                    if (gobPop.WorkerUpgradeLevel < 2) skillNumber = 3;
                    else if (!gobPop.FestivalUsed) skillNumber = 4;
                    else skillNumber = 1;
                }
                else if (game.RoundNo <= 5 && gobPop.WarriorUpgradeLevel < 4)
                    skillNumber = 2; // L1 legacy: Warriors early
                else if (gobPop.WorkerUpgradeLevel < 2)
                    skillNumber = 3;
                else if (!gobPop.FestivalUsed)
                    skillNumber = 4;
                else if (gobPop.WarriorUpgradeLevel < 4)
                    skillNumber = 2;
                else if (gobPop.WorkerUpgradeLevel < 4)
                    skillNumber = 3;
                else
                    skillNumber = 1;
            }

            await _gameReaction.HandleLvlUp(player, null, skillNumber);
        } while (player.Status.LvlUpPoints > 0);

        player.Status.MoveListPage = 1;
    }

    private static bool CompleteForcedSkip(GamePlayerBridgeClass player)
    {
        if (!player.Status.IsSkip)
            return false;

        player.Status.WhoToAttackThisTurn.Clear();
        player.Status.IsReady = true;
        player.Status.ConfirmedPredict = true;
        return true;
    }

    public class BiggestStatClass
    {
        public int StatCount;
        public int StatIndex;

        public BiggestStatClass(int statIndex, int statCount)
        {
            StatIndex = statIndex;
            StatCount = statCount;
        }
    }

    public class Nanobot
    {
        public decimal AttackPreference;

        public GamePlayerBridgeClass Player;


        public Nanobot(GamePlayerBridgeClass player)
        {
            Player = player;
            AttackPreference = 10;
        }

        public int PlaceAtLeaderBoard()
        {
            return Player.Status.GetPlaceAtLeaderBoard();
        }

        public Guid GetPlayerId()
        {
            return Player.Status.PlayerId;
        }
    }

    public class NanobotClass
    {
        public ulong GameId;
        public List<Nanobot> Nanobots = new();

        public NanobotClass(IReadOnlyList<GamePlayerBridgeClass> players)
        {
            GameId = players.First().GameId;
            foreach (var t in players) Nanobots.Add(new Nanobot(t));
        }
    }

    private async Task<bool> TryForceRumblingAttack(
        GamePlayerBridgeClass bot,
        GameClass game,
        List<Nanobot> allTargets)
    {
        // Forced skips and other unable-to-act states return before HandleBotAttack reaches this rule.
        var rumblingEren = game.RoundNo == 10
            ? allTargets.Find(x =>
                x.Player.GameCharacter.Name == ErenYeager.CharacterName
                && x.Player.GameCharacter.Passive.Any(p => p.PassiveName == ErenYeager.Rumbling))
            : null;
        if (rumblingEren == null
            || bot.Status.GetPlaceAtLeaderBoard() <= rumblingEren.PlaceAtLeaderBoard()
            || bot.Status.GetPlaceAtLeaderBoard() >= 6)
            return false;

        if (!await AttackPlayer(bot, rumblingEren.PlaceAtLeaderBoard()))
            return false;

        // Dopa's Macro keeps the turn open until a second distinct target is submitted.
        if (!bot.Status.IsReady
            && bot.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
        {
            var secondTarget = allTargets.FirstOrDefault(x =>
                !bot.Status.WhoToAttackThisTurn.Contains(x.GetPlayerId()));
            if (secondTarget != null)
                await AttackPlayer(bot, secondTarget.PlaceAtLeaderBoard());
        }

        return true;
    }
}
