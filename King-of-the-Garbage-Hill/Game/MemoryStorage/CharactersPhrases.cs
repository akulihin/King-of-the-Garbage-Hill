using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

//using Discord;

namespace King_of_the_Garbage_Hill.Game.MemoryStorage;

public class CharactersUniquePhrase
{
    //initialize variables 
    public PhraseClass AwdkaAfk;
    public PhraseClass AwdkaTeachToPlay;
    public PhraseClass AwdkaTrolling;
    public PhraseClass AwdkaTrying;

    public PhraseClass DarksciDysmoral;
    public PhraseClass DarksciFuckThisGame;
    public PhraseClass DarksciLucky;
    public PhraseClass DarksciNotLucky;
    public PhraseClass DeepListDoubtfulTacticFirstLostPhrase;

    public PhraseClass DeepListDoubtfulTacticPhrase;
    public PhraseClass DeepListMadnessPhrase;
    public PhraseClass DeepListPokePhrase;
    public PhraseClass DeepListSuperMindPhrase;


    public PhraseClass FirstСommandment;
    public PhraseClass FirstСommandmentLost;
    public PhraseClass FourthСommandment;
    public PhraseClass SecondСommandment;
    public PhraseClass SecondСommandmentBan;
    public PhraseClass ThirdСommandment;

    public PhraseClass GlebChallengerPhrase;
    public PhraseClass GlebChallengerSeparatePhrase;
    public PhraseClass GlebComeBackPhrase;
    public PhraseClass GlebSleepyPhrase;
    public PhraseClass GlebTeaPhrase;
    public PhraseClass GlebTeaReadyPhrase;


    public PhraseClass YongGlebIrelia;
    public PhraseClass YongGlebCommunication;
    public PhraseClass YongGlebCommunicationReady;
    public PhraseClass YongGlebMeta;
    public PhraseClass YongGlebTea;
    public PhraseClass YongGlebTeaReady;

    public PhraseClass HardKittyDoebatsyaAnswerPhrase;
    public PhraseClass HardKittyDoebatsyaPhrase;
    public PhraseClass HardKittyDoebatsyaLovePhrase;
    public PhraseClass HardKittyLonelyPhrase;
    public PhraseClass HardKittyMutedPhrase;

    public PhraseClass LeCrispAssassinsPhrase;
    public PhraseClass LeCrispBoolingPhrase;
    public PhraseClass LeCrispImpactPhrase;
    public PhraseClass LeCrispJewPhrase;

    public PhraseClass MitsukiCheekyBriki;
    public PhraseClass MitsukiGarbageSmell;
    public PhraseClass MitsukiSchoolboy;
    public PhraseClass MitsukiTooMuchFucking;
    public PhraseClass MitsukiTooMuchFuckingNoAttack;

    public PhraseClass MylorikBoolePhrase;
    public PhraseClass MylorikRevengeLostPhrase;
    public PhraseClass MylorikRevengeVictoryPhrase;
    public PhraseClass MylorikSpanishPhrase;


    public PhraseClass SirinoksDragonPhrase;
    public PhraseClass SirinoksFriendsPhrase;
    public PhraseClass SpartanDragonSlayer;

    public PhraseClass SpartanShameMylorik;
    public PhraseClass SpartanTheyWontLikeIt;
    public PhraseClass SpartanFirstBlood;

    public PhraseClass TigrSnipe;
    public PhraseClass TigrThreeZero;
    public PhraseClass TigrTop;
    public PhraseClass TigrTwoBetter;

    public PhraseClass TolyaCountPhrase;
    public PhraseClass TolyaCountReadyPhrase;
    public PhraseClass TolyaJewPhrase;
    public PhraseClass TolyaRammus2Phrase;
    public PhraseClass TolyaRammus3Phrase;
    public PhraseClass TolyaRammus4Phrase;
    public PhraseClass TolyaRammus5Phrase;
    public PhraseClass TolyaRammusPhrase;

    public PhraseClass VampyrHematophagy;
    public PhraseClass VampyrNoAttack;
    public PhraseClass VampyrSuckspenStake;
    public PhraseClass VampyrVampyr;
    public PhraseClass VampyrTheLoh;

    public PhraseClass CraboRackSidewaysBoolePhrase;

    public PhraseClass WeedwickRuthlessHunter;
    public PhraseClass WeedwickValuablePreyPoints1;
    public PhraseClass WeedwickValuablePreyPoints2;
    public PhraseClass WeedwickValuablePreyPoints3;
    public PhraseClass WeedwickValuablePreyPoints4;
    public PhraseClass WeedwickValuablePreyPoints5;
    public PhraseClass WeedwickValuablePreyPoints6;
    public PhraseClass WeedwickValuablePreyPoints7;
    public PhraseClass WeedwickValuablePreyDrop;
    public PhraseClass WeedwickWeedYes1;
    public PhraseClass WeedwickWeedYes2;
    public PhraseClass WeedwickWeedYes3;
    public PhraseClass WeedwickWeedYes4;
    public PhraseClass WeedwickWeedYes5;
    public PhraseClass WeedwickWeedYes6;
    public PhraseClass WeedwickWeedYes7;
    public PhraseClass WeedwickWeedYes8;
    public PhraseClass WeedwickWeedYes9;
    public PhraseClass WeedwickWeedYes10;
    public PhraseClass WeedwickWeedYes11;

    public PhraseClass WeedwickWeedNo;


    public PhraseClass KratosEventYes;
    public PhraseClass KratosEventNo;
    public PhraseClass KratosEventFailed;
    public PhraseClass KratosEventKill;
    public PhraseClass KratosTarget;

    public PhraseClass SaitamaBroke;
    public PhraseClass SaitamaBrokeMonster;
    public PhraseClass SaitamaUnnoticed;
    public PhraseClass SaitamaHoldsBack;
    public PhraseClass SaitamaSerious;

    public PhraseClass RickGiantBeans;
    public PhraseClass RickGiantBeansDrink;
    public PhraseClass RickPickleTransform;
    public PhraseClass RickPickleWin;
    public PhraseClass RickPicklePenalty;
    public PhraseClass RickPortalGunInvented;
    public PhraseClass RickPortalGunFired;
    public PhraseClass RickMostWanted;
    public PhraseClass RickMostWantedProc;
    public PhraseClass RickMostWantedPortalFollow;

    public PhraseClass KiraDeathNoteKill;
    public PhraseClass KiraDeathNoteFailed;
    public PhraseClass KiraShinigamiEyes;
    public PhraseClass KiraLNoFight;
    public PhraseClass KiraArrested;

    public PhraseClass ItachiCrows;
    public PhraseClass ItachiIzanagi;
    public PhraseClass ItachiAmaterasu;
    public PhraseClass ItachiTsukuyomiCharge;
    public PhraseClass ItachiTsukuyomiActivate;
    public PhraseClass ItachiTsukuyomiEnd;
    public PhraseClass ItachiTsukuyomiSteal;
    public PhraseClass ItachiTsukuyomiReveal;

    public PhraseClass SellerVparit;
    public PhraseClass SellerVparitEnemy;
    public PhraseClass SellerZakup;
    public PhraseClass SellerProfit;
    public PhraseClass SellerProfitBig;
    public PhraseClass SellerSecretBuild;
    public PhraseClass SellerBolshoiKushEnemy;

    // Dopa
    public PhraseClass DopaVisionReady;
    public PhraseClass DopaVisionProc;
    public PhraseClass DopaMetaChosen;
    public PhraseClass DopaImpact;
    public PhraseClass DopaDomination;
    public PhraseClass DopaRoam;

    // Салдорум
    public PhraseClass SaldorumSurprise;
    public PhraseClass SaldorumSalo;
    public PhraseClass SaldorumNinja;
    public PhraseClass SaldorumChronicler;

    // Napoleon
    public PhraseClass NapoleonAlliance;
    public PhraseClass NapoleonConqueror;
    public PhraseClass NapoleonPeaceTreaty;
    public PhraseClass NapoleonFace;

    // Toxic Mate
    public PhraseClass ToxicMateIntFirstLoss;
    public PhraseClass ToxicMateCancerInfect;
    public PhraseClass ToxicMateCancerReturn;
    public PhraseClass ToxicMateAggressPoint;
    public PhraseClass ToxicMateAggressWontStop;
    public PhraseClass ToxicMateTiltedReact;
    public PhraseClass ToxicMateTiltedOpenMid;

    // Таинственный Суппорт
    public PhraseClass SupportStakes;
    public PhraseClass SupportProtect;
    public PhraseClass SupportPremadeMark;
    public PhraseClass SupportPremadeAntiSkip;

    // Misc new phrases
    public PhraseClass HighEloLoss;
    public PhraseClass SirinoksGeniusPhrase;
    public PhraseClass SirinoksBlockNoPhrase;
    public PhraseClass GlebDragonReaction;
    public PhraseClass GlebWakeUpRoli;
    public PhraseClass GlebPsyche10;
    public PhraseClass GlebComeBackEnemy;
    public PhraseClass LeCrispStonks;
    public PhraseClass DeepListMadnessHardKittyMilk;
    public PhraseClass DeepListMockeryHardKittyMilk;
    // Стая Гоблинов
    public PhraseClass GoblinTunnelEscape;
    public PhraseClass GoblinGrowthAttack;
    public PhraseClass GoblinDeath;
    public PhraseClass GoblinMine;
    public PhraseClass GoblinZigguratBuild;
    public PhraseClass GoblinZigguratNoMoney;
    public PhraseClass GoblinZigguratWorkerDeath;

    // Котики
    public PhraseClass KotikiMinka;
    public PhraseClass KotikiStormTaunt;
    public PhraseClass KotikiStormWin;
    public PhraseClass KotikiCatDeploy;
    public PhraseClass KotikiCatReturn;
    public PhraseClass KotikiLevelUp;

    // TheBoys
    public PhraseClass TheBoysOrderNew;
    public PhraseClass TheBoysOrderComplete;
    public PhraseClass TheBoysOrderFailed;
    public PhraseClass TheBoysChemWeapon;
    public PhraseClass TheBoysPoker;
    public PhraseClass TheBoysKimikoRegen;
    public PhraseClass TheBoysKimikoDisabled;
    public PhraseClass TheBoysKimikoRecovered;
    public PhraseClass TheBoysKompromatGathered;
    public PhraseClass TheBoysKompromatReward;

    // Salldorum
    public PhraseClass SalldorumShen;
    public PhraseClass SalldorumOchko;
    public PhraseClass SalldorumTimeCapsuleBury;
    public PhraseClass SalldorumTimeCapsulePickup;
    public PhraseClass SalldorumChroniclerTriple;
    public PhraseClass SalldorumChroniclerRewrite;
    public PhraseClass SalldorumChroniclerRewriteGlobal;

    // Геральт
    public PhraseClass GeraltContractSpawn;
    public PhraseClass GeraltContractWin;
    public PhraseClass GeraltContractLost;
    public PhraseClass GeraltMeditation;
    public PhraseClass GeraltMeditationInterrupted;
    public PhraseClass GeraltOilActivate;
    public PhraseClass GeraltOilUsed;
    public PhraseClass GeraltPlotva;
    public PhraseClass GeraltLambertMixup;
    public PhraseClass GeraltBountyStolen;
    public PhraseClass GeraltDetective;
    public PhraseClass GeraltMultiContract;

    // Монстр без имени
    public PhraseClass MonsterDeath;
    public PhraseClass MonsterDrop;
    public PhraseClass MonsterTwinSteal;
    public PhraseClass MonsterApocalypse;

    //end
    public PhraseClass AutoMove;
    public PhraseClass JusticePhrase;

    public CharactersUniquePhrase()
    {
        AutoMove = new PhraseClass("Авто Ход");

        JusticePhrase = new PhraseClass("Справедливость");

        //add values
        FirstСommandment = new PhraseClass("Первая заповедь");
        FirstСommandmentLost = new PhraseClass("Первая заповедь");
        SecondСommandment = new PhraseClass("Вторая заповедь");
        ThirdСommandment = new PhraseClass("Третья заповедь");
        SecondСommandmentBan = new PhraseClass("Третья заповедь");
        FourthСommandment = new PhraseClass("Четвертая заповедь");

        VampyrHematophagy = new PhraseClass("Гематофагия");
        VampyrSuckspenStake = new PhraseClass("СОсиновый кол");
        VampyrNoAttack = new PhraseClass("СОсиновый кол");
        VampyrVampyr = new PhraseClass("VampУr");
        VampyrTheLoh = new PhraseClass("Vampyr Позорный");


        SpartanShameMylorik = new PhraseClass("Искусство");
        SpartanDragonSlayer = new PhraseClass("2kxaoc");
        SpartanTheyWontLikeIt = new PhraseClass("Guard Break");
        SpartanFirstBlood = new PhraseClass("Первая кровь");


        TigrTwoBetter = new PhraseClass("Лучше с двумя, чем с адекватными");
        TigrThreeZero = new PhraseClass("3-0 обоссан");
        TigrTop = new PhraseClass("Тигр топ, а ты холоп");
        TigrSnipe = new PhraseClass("Стримснайпят и банят и банят и банят");

        DeepListMadnessPhrase = new PhraseClass("Безумие");

        DeepListPokePhrase = new PhraseClass("__Стёб__");
        DeepListDoubtfulTacticPhrase = new PhraseClass("Сомнительная тактика");
        DeepListDoubtfulTacticFirstLostPhrase = new PhraseClass("Сомнительная тактика");
        DeepListSuperMindPhrase = new PhraseClass("Сверхразум");

        MylorikRevengeLostPhrase = new PhraseClass("Месть");
        MylorikRevengeVictoryPhrase = new PhraseClass("Месть");
        MylorikBoolePhrase = new PhraseClass("Буль");
        MylorikSpanishPhrase = new PhraseClass("Испанец");

        GlebChallengerPhrase = new PhraseClass("Претендент русского сервера");
        GlebChallengerSeparatePhrase = new PhraseClass("Претендент русского сервера");
        GlebSleepyPhrase = new PhraseClass("Спящее хуйло");
        GlebComeBackPhrase = new PhraseClass("Я щас приду");
        GlebTeaPhrase = new PhraseClass("Я за чаем");
        GlebTeaReadyPhrase = new PhraseClass("__**Я за чаем**");

        YongGlebIrelia = new PhraseClass("Main Ирелия");
        YongGlebCommunication = new PhraseClass("Коммуникация");
        YongGlebCommunicationReady = new PhraseClass("Коммуникация");
        YongGlebMeta = new PhraseClass("Следит за игрой");
        YongGlebTea = new PhraseClass("Спокойствие");
        YongGlebTeaReady = new PhraseClass("__**Спокойствие**");

        LeCrispJewPhrase = new PhraseClass("Еврей");
        LeCrispBoolingPhrase = new PhraseClass("Булинг");
        LeCrispAssassinsPhrase = new PhraseClass("Гребанные ассассины");
        LeCrispImpactPhrase = new PhraseClass("Импакт");

        TolyaJewPhrase = new PhraseClass("Еврей");
        TolyaCountPhrase = new PhraseClass("Подсчет");
        TolyaCountReadyPhrase = new PhraseClass("__**Подсчет**");
        TolyaRammusPhrase = new PhraseClass("Раммус мейн");
        TolyaRammus2Phrase = new PhraseClass("Раммус мейн");
        TolyaRammus3Phrase = new PhraseClass("Раммус мейн");
        TolyaRammus4Phrase = new PhraseClass("Раммус мейн");
        TolyaRammus5Phrase = new PhraseClass("Раммус мейн");

        HardKittyLonelyPhrase = new PhraseClass("Одиночество");
        HardKittyDoebatsyaPhrase = new PhraseClass("Доебаться");
        HardKittyDoebatsyaLovePhrase = new PhraseClass("Доебаться");
        HardKittyMutedPhrase = new PhraseClass("Mute");
        HardKittyDoebatsyaAnswerPhrase = new PhraseClass("Доебаться");

        SirinoksFriendsPhrase = new PhraseClass("Заводить друзей");
        SirinoksDragonPhrase = new PhraseClass("Дракон");

        MitsukiCheekyBriki = new PhraseClass("Дерзкая школота");
        MitsukiTooMuchFucking = new PhraseClass("Много выебывается");
        MitsukiTooMuchFuckingNoAttack = new PhraseClass("Много выебывается");
        MitsukiGarbageSmell = new PhraseClass("Запах мусора");
        MitsukiSchoolboy = new PhraseClass("Школьник");

        AwdkaTeachToPlay = new PhraseClass("Научите играть");
        AwdkaTrolling = new PhraseClass("Произошел троллинг");
        AwdkaTrying = new PhraseClass("Я пытаюсь!");
        AwdkaAfk = new PhraseClass("АФКА");


        DarksciNotLucky = new PhraseClass("Не повезло");
        DarksciLucky = new PhraseClass("Повезло");
        DarksciFuckThisGame = new PhraseClass("Да всё нахуй эту игру");
        DarksciDysmoral = new PhraseClass("Дизмораль");

        CraboRackSidewaysBoolePhrase = new PhraseClass("Хождение боком");

        WeedwickRuthlessHunter = new PhraseClass("Безжалостный охотник");
        WeedwickValuablePreyPoints1 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints2 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints3 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints4 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints5 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints6 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyPoints7 = new PhraseClass("Ценная добыча");
        WeedwickValuablePreyDrop = new PhraseClass("Ценная добыча");
        WeedwickWeedYes1 = new PhraseClass("Weed");
        WeedwickWeedYes2 = new PhraseClass("Weed");
        WeedwickWeedYes3 = new PhraseClass("Weed");
        WeedwickWeedYes4 = new PhraseClass("Weed");
        WeedwickWeedYes5 = new PhraseClass("Weed");
        WeedwickWeedYes6 = new PhraseClass("Weed");
        WeedwickWeedYes7 = new PhraseClass("Weed");
        WeedwickWeedYes8 = new PhraseClass("Weed");
        WeedwickWeedYes9 = new PhraseClass("Weed");
        WeedwickWeedYes10 = new PhraseClass("Weed");
        WeedwickWeedYes11 = new PhraseClass("Weed");
        WeedwickWeedNo = new PhraseClass("Weed");

        KratosEventYes = new PhraseClass("Возвращение из мертвых");
        KratosEventNo = new PhraseClass("Возвращение из мертвых");
        KratosEventFailed = new PhraseClass("Возвращение из мертвых");
        KratosEventKill = new PhraseClass("Возвращение из мертвых");
        KratosTarget = new PhraseClass("Охота на богов");

        SaitamaBroke = new PhraseClass("На мели");
        SaitamaBrokeMonster = new PhraseClass("На мели (монстр)");
        SaitamaUnnoticed = new PhraseClass("Неприметность (напал кто-то еще)");
        SaitamaHoldsBack = new PhraseClass("Неприметность (снисходителен)");
        SaitamaSerious = new PhraseClass("Неприметность (топ2 по силе)");

        RickGiantBeans = new PhraseClass("Гигантские бобы");
        RickGiantBeansDrink = new PhraseClass("Гигантские бобы");
        RickPickleTransform = new PhraseClass("Огурчик Рик");
        RickPickleWin = new PhraseClass("Огурчик Рик");
        RickPicklePenalty = new PhraseClass("Огурчик Рик");
        RickPortalGunInvented = new PhraseClass("Портальная пушка");
        RickPortalGunFired = new PhraseClass("Портальная пушка");
        RickMostWanted = new PhraseClass("Most wanted");
        RickMostWantedProc = new PhraseClass("Most wanted");
        RickMostWantedPortalFollow = new PhraseClass("Most wanted");

        KiraDeathNoteKill = new PhraseClass("Тетрадь смерти");
        KiraDeathNoteFailed = new PhraseClass("Тетрадь смерти");
        KiraShinigamiEyes = new PhraseClass("Глаза бога смерти");
        KiraLNoFight = new PhraseClass("L");
        KiraArrested = new PhraseClass("L");

        // Стая Гоблинов
        GoblinTunnelEscape = new PhraseClass("Тоннели Гоблинов");
        GoblinGrowthAttack = new PhraseClass("Гоблины");
        GoblinDeath = new PhraseClass("Гоблины");
        GoblinMine = new PhraseClass("Отличный рудник");
        GoblinZigguratBuild = new PhraseClass("Гоблины тупые, но не идиоты");
        GoblinZigguratNoMoney = new PhraseClass("Гоблины тупые, но не идиоты");
        GoblinZigguratWorkerDeath = new PhraseClass("Гоблины тупые, но не идиоты");

        // Котики
        KotikiMinka = new PhraseClass("Минька");
        KotikiStormTaunt = new PhraseClass("Штормяк");
        KotikiStormWin = new PhraseClass("Штормяк");
        KotikiCatDeploy = new PhraseClass("Кошачья засада");
        KotikiCatReturn = new PhraseClass("Кошачья засада");
        KotikiLevelUp = new PhraseClass("lvl-мяк");

        // TheBoys
        TheBoysOrderNew = new PhraseClass("Заказ Француза");
        TheBoysOrderComplete = new PhraseClass("Заказ Француза");
        TheBoysOrderFailed = new PhraseClass("Заказ Француза");
        TheBoysChemWeapon = new PhraseClass("Хим.оружие");
        TheBoysPoker = new PhraseClass("Кочерга Бучера");
        TheBoysKimikoRegen = new PhraseClass("Регенерация Кимико");
        TheBoysKimikoDisabled = new PhraseClass("Регенерация Кимико");
        TheBoysKimikoRecovered = new PhraseClass("Регенерация Кимико");
        TheBoysKompromatGathered = new PhraseClass("Компромат М.М.");
        TheBoysKompromatReward = new PhraseClass("Компромат М.М.");

        // Монстр без имени
        MonsterDeath = new PhraseClass("Монстр");
        MonsterDrop = new PhraseClass("Монстр");
        MonsterTwinSteal = new PhraseClass("Близнец");
        MonsterApocalypse = new PhraseClass("Пейзаж конца света");

        //end

        //
        AutoMove.PassiveLogRus.Add("Ты что, бот?");
        AutoMove.PassiveLogRus.Add("А ну играй сам! Я для кого игру делал?");
        AutoMove.PassiveLogRus.Add("Слышь, не трогай эту кнопку, она для админов");
        AutoMove.PassiveLogRus.Add("Сложно самому походить что ли?");
        AutoMove.PassiveLogRus.Add("Серьезно, уже пора показывать скилл. САМОМУ.");
        AutoMove.PassiveLogRus.Add("Я щас заблокирую это кнопку, если еще раз нажмешь.");
        AutoMove.PassiveLogRus.Add("Нет, я щас лучше тебя заблокирую. Будешь как Тигр сидеть");
        AutoMove.PassiveLogRus.Add("Давай-давай, игра уже щас закончится... Ну же... Ну... НУ!!! ПОХОДИ");
        AutoMove.PassiveLogRus.Add("ДАВАЙ, ВСЕГО ОДИН ХОД");
        AutoMove.PassiveLogRus.Add("Пиздец. Всю игру просидел в автоходе... Вы там вообще живой? Может вы умер?... Глеб?");
        //

        //
        JusticePhrase.PassiveLogRus.Add("Ты сможешь!");
        JusticePhrase.PassiveLogRus.Add("Еще немного!");
        JusticePhrase.PassiveLogRus.Add("Верь в себя!");
        JusticePhrase.PassiveLogRus.Add("Верь в мою веру в тебя!");
        JusticePhrase.PassiveLogRus.Add("Не повeзло, но всё получится!");
        JusticePhrase.PassiveLogRus.Add("Справедливость на нашей стороне!");
        JusticePhrase.PassiveLogRus.Add("Мы им покажем!");
        //

        //add  as many phrases as you wany
        FirstСommandment.PassiveLogRus.Add("Если фидишь то пропушь, если пушишь то нафидь");
        FirstСommandmentLost.PassiveLogRus.Add("Тебе нужна помощь бога");
        FirstСommandmentLost.PassiveLogRus.Add("Багословляю, сын мой");
        FirstСommandmentLost.PassiveLogRus.Add("Ты пришел к богу");
        FirstСommandmentLost.PassiveLogRus.Add("Просветление");
        FirstСommandmentLost.PassiveLogRus.Add("Только вера спасет");
        SecondСommandment.PassiveLogRus.Add(
            "И как боженька сказал - \"Бань удира, я зассал\" - и как доктор говорит - \"Не забанил - инвалид\"");
        SecondСommandmentBan.PassiveLogRus.Add("Изыди!");
        SecondСommandmentBan.PassiveLogRus.Add("Нечесть");
        SecondСommandmentBan.PassiveLogRus.Add("Бань удира.");
        ThirdСommandment.PassiveLogRus.Add("Не флейми в чате");

        FourthСommandment.PassiveLogRus.Add("Играй руками");

        VampyrVampyr.PassiveLogRus.Add("Вампурская ноо-ооочь!");
        VampyrVampyr.PassiveLogRus.Add("Таинственный Вампур");
        VampyrHematophagy.PassiveLogRus.Add("Я - Вампур из Лондона!");
        VampyrHematophagy.PassiveLogRus.Add("Какая питательная кровь");
        VampyrHematophagy.PassiveLogRus.Add("Вампур-шампур!");
        VampyrHematophagy.PassiveLogRus.Add("Клыки как бур - Вампур");
        VampyrHematophagy.PassiveLogRus.Add("ВОТ МОЯ ФИНАЛЬНАЯ ФОРМА, теперь я - Vampyr!");
        VampyrSuckspenStake.PassiveLogRus.Add("Ай");
        VampyrSuckspenStake.PassiveLogRus.Add("Мое Вампурское сердце не выдержит");
        VampyrSuckspenStake.PassiveLogRus.Add("Ниеееет!");
        VampyrSuckspenStake.PassiveLogRus.Add("Пфхаааш!");
        VampyrSuckspenStake.PassiveLogRus.Add("Прибавляется, отнимается, прибавляется, отнимается");

        VampyrNoAttack.PassiveLogRus.Add("Не хочу, не буду");
        VampyrNoAttack.PassiveLogRus.Add("Аа я баюс его");
        VampyrNoAttack.PassiveLogRus.Add("Это Хельсинг, начальнике");
        VampyrNoAttack.PassiveLogRus.Add("От него пахнет чесноком!");
        VampyrNoAttack.PassiveLogRus.Add("Он мне кол в жопу засунул");


        VampyrTheLoh.PassiveLogRus.Add("Никаких статов для тебя, поешь чеснока");
        VampyrTheLoh.PassiveLogRus.Add("Иди отсюда, Вампур позорный");
        VampyrTheLoh.PassiveLogRus.Add("А ну хватит кусаться!");
        VampyrTheLoh.PassiveLogRus.Add("Клыки наточил?");



        SpartanShameMylorik.PassiveLogRus.Add("ОН уважает военное искуство!");
        SpartanDragonSlayer.PassiveLogRus.Add("*Oторвался от остальных на 2000 голды*");
        SpartanTheyWontLikeIt.PassiveLogRus.Add("Твой щит - ничто для моего копья!");
        SpartanTheyWontLikeIt.PassiveLogRus.Add("Настакал blackcleaver!");
        SpartanTheyWontLikeIt.PassiveLogRus.Add("Мое копье создано что бы пронзить НЕБЕСА!");
        SpartanTheyWontLikeIt.PassiveLogRus.Add("Кину копье, напрыгну, отхуярю щитом");
        SpartanTheyWontLikeIt.PassiveLogRus.Add("Я твой фанат. Я пантеон.");
        SpartanFirstBlood.PassiveLogRus.Add("Кулю на эти деньги сапоги! +1 Скорость");


        TigrTwoBetter.PassiveLogRus.Add("Лучше с двумя, чем с адекватными");
        TigrTwoBetter.PassiveLogRus.Add("Добро пожаловать в мой клан!");

        TigrThreeZero.PassiveLogRus.Add("Го 1v1");
        TigrThreeZero.PassiveLogRus.Add("2:0");
        TigrThreeZero.PassiveLogRus.Add("Изи 3-0, обоссан");
        TigrTop.PassiveLogRus.Add("Тигр топ, а ты холоп!");
        TigrTop.PassiveLogRus.Add("Я - ТОП1 БЕРСЕРК НА СЕРВЕРЕ!!!");
        TigrSnipe.PassiveLogRus.Add("ЕБАНЫЕ БАНЫ НА 10 ЛЕТ");

        DarksciNotLucky.PassiveLogRus.Add("Сука, не везет с командой!");
        DarksciNotLucky.PassiveLogRus.Add("И вот так каждое промо...");
        DarksciNotLucky.PassiveLogRus.Add("Что они творят?");
        DarksciNotLucky.PassiveLogRus.Add("Я тилтед");
        DarksciLucky.PassiveLogRus.Add("Золотой");
        DarksciLucky.PassiveLogRus.Add("Наконец-то!");
        DarksciFuckThisGame.PassiveLogRus.Add("Нахуй эту игру");
        DarksciDysmoral.PassiveLogRus.Add("Всё, у меня горит!");

        DeepListMadnessPhrase.PassiveLogRus.Add("Хаха. Ха. АХАХАХАХАХАХАХ!");
        DeepListMadnessPhrase.PassiveLogRus.Add("Стоп, кто... я?");
        DeepListMadnessPhrase.PassiveLogRus.Add("Заткнитесь!");
        DeepListMadnessPhrase.PassiveLogRus.Add("Да бля бля Сука бля");
        DeepListMadnessPhrase.PassiveLogRus.Add("Какая полезная филактерия");
        DeepListMadnessPhrase.PassiveLogRus.Add("Невидимый волк. Он пришел за мной!!!");
        DeepListMadnessPhrase.PassiveLogRus.Add("Я самый хитрый таракан");
        DeepListMadnessPhrase.PassiveLogRus.Add("Покажите звук");
        DeepListMadnessPhrase.PassiveLogRus.Add("БАСЛОВУШКА");
        DeepListMadnessPhrase.PassiveLogRus.Add("Доктор, у меня проблема: Я слышу голоса, но сейчас их нет...");
        DeepListMadnessPhrase.PassiveLogRus.Add("Доктор Чо");
        DeepListMadnessPhrase.PassiveLogRus.Add("*Звуки губной гармошки*");
        DeepListMadnessPhrase.PassiveLogRus.Add("*Звуки расстроенной гитары*");
        

        

        DeepListPokePhrase.PassiveLogRus.Add("Хехе");
        DeepListPokePhrase.PassiveLogRus.Add("Чисто постЕбаться");
        DeepListPokePhrase.PassiveLogRus.Add("Я в деле!");
        DeepListPokePhrase.PassiveLogRus.Add("Два игнайта...");
        DeepListPokePhrase.PassiveLogRus.Add("*Хохочет*");
        DeepListPokePhrase.PassiveLogRus.Add("Ты кто такой?");
        DeepListPokePhrase.PassiveLogRus.Add("Скобочка");
        DeepListPokePhrase.PassiveLogRus.Add("It's a joke. You got it?");
        DeepListPokePhrase.PassiveLogRus.Add("Где импакт!?");
        DeepListPokePhrase.PassiveLogRus.Add("Вариан?");
        DeepListPokePhrase.PassiveLogRus.Add("Барон!");
        DeepListPokePhrase.PassiveLogRus.Add("Задень головой");
        DeepListPokePhrase.PassiveLogRus.Add("Хуле ты встал?");
        DeepListPokePhrase.PassiveLogRus.Add("Не руби сук");
        DeepListPokePhrase.PassiveLogRus.Add("Обмылки");
        DeepListPokePhrase.PassiveLogRus.Add("Ты жирный");
        DeepListPokePhrase.PassiveLogRus.Add("Ты тоже");
        DeepListPokePhrase.PassiveLogRus.Add("Как говорит мясник: Люди делятся");
        DeepListPokePhrase.PassiveLogRus.Add("#");
        DeepListPokePhrase.PassiveLogRus.Add("Прекрати пиздеть!");
        DeepListPokePhrase.PassiveLogRus.Add("Молоко в пакете");
        DeepListPokePhrase.PassiveLogRus.Add("Где рецензия на др. Стоуна?");
        DeepListPokePhrase.PassiveLogRus.Add("Ты змея очковая, и дело не в очках!");
        DeepListPokePhrase.PassiveLogRus.Add("Тыква. Выква. Мыква."); 
        DeepListPokePhrase.PassiveLogRus.Add("Крисп.");
        DeepListPokePhrase.PassiveLogRus.Add("сложно быть гением в дебилтернете"); 
        DeepListPokePhrase.PassiveLogRus.Add("Из Загары вышел хороший тиммейт");  
        DeepListPokePhrase.PassiveLogRus.Add("Я проиграл");  
        DeepListPokePhrase.PassiveLogRus.Add("Large wave of angry zergs");  
        DeepListPokePhrase.PassiveLogRus.Add("LeCringe");  
        DeepListPokePhrase.PassiveLogRus.Add("Кто **Там**?"); 
        DeepListPokePhrase.PassiveLogRus.Add("Дэусманс, придурки"); 
        DeepListPokePhrase.PassiveLogRus.Add("I hate Sirinokses!"); 
        DeepListPokePhrase.PassiveLogRus.Add("Надеваю кепку."); 
        DeepListPokePhrase.PassiveLogRus.Add("I DC'd again."); 
        DeepListPokePhrase.PassiveLogRus.Add("Злой Школьник"); 
        DeepListPokePhrase.PassiveLogRus.Add("Почему гиены смеются? Потому что смешно!"); 
        DeepListPokePhrase.PassiveLogRus.Add("**Лол** коронный, **Кек** похоронный"); 
        DeepListPokePhrase.PassiveLogRus.Add("Витамин C содержится в курице и __**БОБАХ**__"); 
        DeepListPokePhrase.PassiveLogRus.Add("Руки-щуки"); 

        

        DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Everything is going according to my plan");
        DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("My superior tactic will win");
        DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Я всё рассчитал, это работает.");
        DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Napoleon Wonnafcuk");
        DeepListDoubtfulTacticPhrase.PassiveLogRus.Add("Техника скрытого Листа - Гамбит");
        

        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Скоро...");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Тише едешь, дальше будешь");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Я называю это \"Гамбит\"");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Привилегия даумондов");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Это лишь часть моего плана");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("На нашей стороне ТЕОРИЯ НЕВЕРОЯТНОСТИ!");
        DeepListDoubtfulTacticFirstLostPhrase.PassiveLogRus.Add("Вижу хуету и я в деле!");
        


        DeepListSuperMindPhrase.PassiveLogRus.Add("Поделив энтропию на ноль, вы поняли, что ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Используя свою дедукцию, вы поняли, что ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Сложив 2+2, вы каким-то чудом догадались, что ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Всё ясно. Героин - Марихуана, а ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Враг спалился! Но я это понял еще давно. ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Пст... у меня есть информация, что ");
        DeepListSuperMindPhrase.PassiveLogRus.Add("Здравствуйте, я ваш заменитель мозга. И кстати, ");


        MylorikRevengeLostPhrase.PassiveLogRus.Add("Ах вы суки блять!");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("МММХ!");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("ПРРРРРРУ");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("Понерфайте!");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("РАААЗЪЫБУ!");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("Понерфайте!");
        MylorikRevengeLostPhrase.PassiveLogRus.Add("Я принял решение");

        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("ЭТО СПАРТА");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Показал как надо");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("ММ!");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Слабый персонаж!");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Лезу");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Го вирт");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Молоко в пакете");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Алексич... я твой фанат... Я Пантеон.");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Берём.");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Вьетнамская тристана!");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("Вьетнамская тристана!!!");
        MylorikRevengeVictoryPhrase.PassiveLogRus.Add("ВЬЕТНАМСКАЯ ТРИСТАНА!!!");

        
        
        MylorikSpanishPhrase.PassiveLogRus.Add("Ааарива!");
        MylorikSpanishPhrase.PassiveLogRus.Add("Буэнос ночес!");
        MylorikSpanishPhrase.PassiveLogRus.Add("Ale handro!");
        MylorikSpanishPhrase.PassiveLogRus.Add("noob team");
        MylorikBoolePhrase.PassiveLogRus.Add("Буль");
        MylorikBoolePhrase.PassiveLogRus.Add("Там... **Кенч**");
        MylorikBoolePhrase.PassiveLogRus.Add("А вы заметили? Айсика научилась играть...");


        GlebChallengerPhrase.PassiveLogRus.Add("А? БАРОН?!");
        GlebChallengerPhrase.PassiveLogRus.Add("Ща я покажу как надо");
        GlebChallengerPhrase.PassiveLogRus.Add("Глебка залетает!");
        GlebChallengerPhrase.PassiveLogRus.Add("В Претендентмобиль!");
        GlebChallengerPhrase.PassiveLogRus.Add("ЛИИИИРОЙ ДЖЕНКИНС");
        GlebChallengerPhrase.PassiveLogRus.Add("FLASH IN THE NIGhT! Я как вспышка в ночи!");


        GlebChallengerSeparatePhrase.PassiveLogRus.Add("FULL POWER!!!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("OVER 9000");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("JA - PRETENDENT!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("IT'S NASHORS TIME!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("NORMALNO. U MENJA EST HEAL!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("IGRAEM IGRAEM!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("DOMOJ GAMBIT!");
        GlebChallengerSeparatePhrase.PassiveLogRus.Add("POSTAVTE WARD!");
        

        GlebSleepyPhrase.PassiveLogRus.Add("Zzzz...");
        GlebSleepyPhrase.PassiveLogRus.Add("Я... Я тут...");
        GlebSleepyPhrase.PassiveLogRus.Add("Только не делайте ремейк *Зевнул*");
        GlebComeBackPhrase.PassiveLogRus.Add("5 минут");
        GlebComeBackPhrase.PassiveLogRus.Add("Я щас приду");
        GlebComeBackPhrase.PassiveLogRus.Add("Без меня не начина...");
        GlebTeaPhrase.PassiveLogRus.Add("Какао белого цвета");
        GlebTeaReadyPhrase.PassiveLogRus.Add("U menja est' lishnij chaj!__");
        GlebTeaReadyPhrase.PassiveLogRus.Add("Chaj zavarilsa!__");


        YongGlebIrelia.PassiveLogRus.Add("Blja opat' mou ireliu ponerfali. Nu ni4ego, glebka otbIgraetsa.");
        YongGlebIrelia.PassiveLogRus.Add("VsmbIsle? Skolko mozno nerfat' ee? Eto zhe celbIh");
        YongGlebIrelia.PassiveLogRus.Add("Rioti sovsem poputali y mojei malishki zabrali");
        YongGlebIrelia.PassiveLogRus.Add("Da kak tak 4alanger brat' kogday personazha");
        YongGlebCommunication.PassiveLogRus.Add("Otli4no, postavil ward. Glebka carry");
        YongGlebCommunicationReady.PassiveLogRus.Add("__Ja kupil communication__");
        YongGlebMeta.PassiveLogRus.Add("Delajem objactivs!");
        YongGlebMeta.PassiveLogRus.Add("Igraem ot objectov pasanbI");
        YongGlebMeta.PassiveLogRus.Add("Igraem igraem");
        YongGlebMeta.PassiveLogRus.Add("4alanger sam sebja ne apnet!!!!");
        YongGlebMeta.PassiveLogRus.Add("viigrivaetsa on objactivs");
        YongGlebMeta.PassiveLogRus.Add("vbI medlennie, nado srazy na objecti! po pervomy cally");
        YongGlebMeta.PassiveLogRus.Add("Ja kak Darien");
        YongGlebMeta.PassiveLogRus.Add("Ja kak __alexich__");
        YongGlebMeta.PassiveLogRus.Add("Ja kak **FLASH IN THE NIGHT**");
        YongGlebMeta.PassiveLogRus.Add("Gleb journey to Challanger");
        YongGlebTea.PassiveLogRus.Add("VbIpij 4au, yspokojsa");
        YongGlebTea.PassiveLogRus.Add("Derji 4aj ne ynbIvai XD XD");
        YongGlebTea.PassiveLogRus.Add("Poslushaj kak vodi4ka l'etsa");
        YongGlebTea.PassiveLogRus.Add("Eto moj lubimbIj 4aj is kitaja");
        YongGlebTea.PassiveLogRus.Add("4aj mne nepius privez");
        YongGlebTeaReady.PassiveLogRus.Add("Chaj zavarilsa!__");


        LeCrispJewPhrase.PassiveLogRus.Add("Я жру деньги!");
        LeCrispJewPhrase.PassiveLogRus.Add("Kill secure"); 
        LeCrispJewPhrase.PassiveLogRus.Add("ОЙ ВЕЙ!");
        LeCrispJewPhrase.PassiveLogRus.Add("Кошерно"); 
        LeCrispJewPhrase.PassiveLogRus.Add("ОЙ ВЕЙ!");     
        LeCrispJewPhrase.PassiveLogRus.Add("Хава Нагила"); 
        LeCrispJewPhrase.PassiveLogRus.Add("Призываю Иуду"); 
        LeCrispJewPhrase.PassiveLogRus.Add("Поднимаю кошелек на улице, а там нехватает"); 


        LeCrispBoolingPhrase.PassiveLogRus.Add("Why are you bulling me?");
        LeCrispBoolingPhrase.PassiveLogRus.Add("fuk u");
        LeCrispBoolingPhrase.PassiveLogRus.Add("У тебя учусь");
        LeCrispBoolingPhrase.PassiveLogRus.Add("DeepList - это заменитель мозга");
        LeCrispBoolingPhrase.PassiveLogRus.Add("Как Адепт");
        
        
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Гребанные ассассины");
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Эх, жизнь АДК");
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Моя жопа не смогла съесть змею. (с)");
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Женская собака приседает и писает");      
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Аль тигий ли ба делет"); 
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Малфит подкинул, Ясуо ультанул..."); 
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Голова арбуза"); 
        LeCrispAssassinsPhrase.PassiveLogRus.Add("Я трахался на летних каникулах. Никому это не понравилось "); 

        
        LeCrispImpactPhrase.PassiveLogRus.Add("Импакт!");
        LeCrispImpactPhrase.PassiveLogRus.Add("Это импакт?");
        LeCrispImpactPhrase.PassiveLogRus.Add("шпещьмен");
        LeCrispImpactPhrase.PassiveLogRus.Add("Забрал кэмп!");
        LeCrispImpactPhrase.PassiveLogRus.Add("Я MVP, Я MVP!");    
        LeCrispImpactPhrase.PassiveLogRus.Add("Хорошее позицианирование");       
        LeCrispImpactPhrase.PassiveLogRus.Add("Это просто навык");                                          
        LeCrispImpactPhrase.PassiveLogRus.Add("Хумус");                                                    
        LeCrispImpactPhrase.PassiveLogRus.Add("Мазальтов!");                                                      
        LeCrispImpactPhrase.PassiveLogRus.Add("Нимфоманка!"); 
        LeCrispImpactPhrase.PassiveLogRus.Add("Я прям вижу как он зонит");                                   
        LeCrispImpactPhrase.PassiveLogRus.Add("Импакт!");
        LeCrispImpactPhrase.PassiveLogRus.Add("Тихо, тихо... Без импакта не стреляй...");
        LeCrispImpactPhrase.PassiveLogRus.Add("Стреляй только когда точно добьешь!");
        LeCrispImpactPhrase.PassiveLogRus.Add("Я хочу чтобы моя девушка была немножко cringe");
        LeCrispImpactPhrase.PassiveLogRus.Add("Как Надо!");
        LeCrispImpactPhrase.PassiveLogRus.Add("OnlyFans... Почему здесь всё стоит денег?");
        LeCrispImpactPhrase.PassiveLogRus.Add("Стреляю редко, но метко");

        

        TolyaJewPhrase.PassiveLogRus.Add("Easy money");
        TolyaJewPhrase.PassiveLogRus.Add("Worth");
        TolyaJewPhrase.PassiveLogRus.Add("Таки кошерно");
        TolyaJewPhrase.PassiveLogRus.Add("Я пока еще не втянулся в эти ваши еврейские схемы");
        TolyaJewPhrase.PassiveLogRus.Add("Хехе, спиздил");
        TolyaJewPhrase.PassiveLogRus.Add("Одна медная!");
        TolyaJewPhrase.PassiveLogRus.Add("Ыыыы");
        TolyaJewPhrase.PassiveLogRus.Add("Настоящий еврей знает свой толк");        

        TolyaCountReadyPhrase.PassiveLogRus.Add("Я готов это просчитать__");
        TolyaCountPhrase.PassiveLogRus.Add("Ха! Подстчет!");
        TolyaCountPhrase.PassiveLogRus.Add("Так и знал!");
        TolyaCountPhrase.PassiveLogRus.Add("Предиктед");
        TolyaCountPhrase.PassiveLogRus.Add("Справедливо"); 
        TolyaCountPhrase.PassiveLogRus.Add("Для победы мне нужны: листочек, еврейсто и ручка"); 
        
        TolyaRammusPhrase.PassiveLogRus.Add("Okay.");
        TolyaRammusPhrase.PassiveLogRus.Add("Hm.");
        TolyaRammusPhrase.PassiveLogRus.Add("Должны, да не обязанны");
        TolyaRammus2Phrase.PassiveLogRus.Add("Я живу и горю");
        TolyaRammus3Phrase.PassiveLogRus.Add("Изи разбайтил");
        TolyaRammus4Phrase.PassiveLogRus.Add("Посмотри сколько я поднял за раз!");
        TolyaRammus5Phrase.PassiveLogRus.Add("Оу, ну всё. ГГ.");


        HardKittyLonelyPhrase.PassiveLogRus.Add("Привет");
        HardKittyLonelyPhrase.PassiveLogRus.Add("Мне сегодня снилось, что...");
        HardKittyLonelyPhrase.PassiveLogRus.Add("Что делаешь?");
        HardKittyLonelyPhrase.PassiveLogRus.Add("Как дела?");
        HardKittyDoebatsyaPhrase.PassiveLogRus.Add("У вас продают молоко в пакетах?");
        HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Какой у тебя Windows?");
        HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Что лучше взять на MF?");
        HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Эй, э-эй...");
        HardKittyDoebatsyaPhrase.PassiveLogRus.Add("Как работает твой бот?");
        HardKittyDoebatsyaLovePhrase.PassiveLogRus.Add("**ЭТО МОЕ ГОВНИЩЕ**, я доебывал его всю свою жизнь...");

        HardKittyDoebatsyaAnswerPhrase.PassiveLogRus.Add("Вам ответили на письмо!");
        HardKittyMutedPhrase.PassiveLogRus.Add("Muted");


        SirinoksFriendsPhrase.PassiveLogRus.Add("Го в пати");
        SirinoksFriendsPhrase.PassiveLogRus.Add("М/Ж?");
        SirinoksFriendsPhrase.PassiveLogRus.Add("Я проиграл");
        SirinoksFriendsPhrase.PassiveLogRus.Add("Пепега");
        SirinoksFriendsPhrase.PassiveLogRus.Add("Invite");


        SirinoksDragonPhrase.PassiveLogRus.Add("ROAR!");
        SirinoksDragonPhrase.PassiveLogRus.Add("А НУ, МОРДА. ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("НИКАКОГО NSFW НА СТРИМЕ! ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("ГДЕ БЛЯТЬ ГЛЕБ!?!?! ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("ОПЯТЬ ЛИСТ ХУЙНЮ ГОВОРИТ! ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("ЛОРИК НИЧЕГО НЕ ПОНИМАЕТ! ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("БЛЯТЬ КРИИИИСП! ROAR");
        SirinoksDragonPhrase.PassiveLogRus.Add("You can fly in a push of a button!");
        
        

        MitsukiCheekyBriki.PassiveLogRus.Add("Ну чё, ёпта, поиграем?");
        MitsukiTooMuchFucking.PassiveLogRus.Add("Алмаз!");
        MitsukiTooMuchFucking.PassiveLogRus.Add("Ну так-то всё правильно");
        MitsukiTooMuchFucking.PassiveLogRus.Add("Сука ня");
        MitsukiTooMuchFucking.PassiveLogRus.Add("Чё пацаны, аниме?");
        MitsukiTooMuchFucking.PassiveLogRus.Add("Наслаждайтесь жижей!");
        MitsukiTooMuchFuckingNoAttack.PassiveLogRus.Add("Ну че, зассали, суки?");
        MitsukiTooMuchFuckingNoAttack.PassiveLogRus.Add("Кто на меня?");
        MitsukiTooMuchFuckingNoAttack.PassiveLogRus.Add("Пвп или зассал?");
        MitsukiGarbageSmell.PassiveLogRus.Add("Во что это я вляпался?");
        MitsukiGarbageSmell.PassiveLogRus.Add("Запахло мусором...");
        MitsukiSchoolboy.PassiveLogRus.Add("Да блять, своего компьютера-то нету");


        AwdkaTeachToPlay.PassiveLogRus.Add("Научите играть");
        AwdkaTeachToPlay.PassiveLogRus.Add("Я ничего не умею");
        AwdkaTeachToPlay.PassiveLogRus.Add("Я бесполезный");
        AwdkaTeachToPlay.PassiveLogRus.Add("Кстати, я хач.");
        AwdkaTeachToPlay.PassiveLogRus.Add("Как это работает?");
        AwdkaTeachToPlay.PassiveLogRus.Add("Киберспорт!");
        AwdkaTeachToPlay.PassiveLogRus.Add("Что это?");
        AwdkaTeachToPlay.PassiveLogRus.Add("Как там нажимать?");
        AwdkaTeachToPlay.PassiveLogRus.Add("Два игнайта на вуконга!");
        AwdkaTrolling.PassiveLogRus.Add("Произошел троллинг");
        AwdkaTrolling.PassiveLogRus.Add("Ого... Это как?");
        AwdkaTrolling.PassiveLogRus.Add("Что я сделаль?");
        AwdkaTrying.PassiveLogRus.Add("Сапаги сораходы! Дёшево!");
        AwdkaTrying.PassiveLogRus.Add("Флеш-ульт в клона лебланки");
        AwdkaTrying.PassiveLogRus.Add("Что начинает получаться");
        AwdkaTrying.PassiveLogRus.Add("Почти...");
        AwdkaTrying.PassiveLogRus.Add("В киберспорт!");
        AwdkaAfk.PassiveLogRus.Add("AFKA");


        CraboRackSidewaysBoolePhrase.PassiveLogRus.Add("У них одни ренжевики!");
        CraboRackSidewaysBoolePhrase.PassiveLogRus.Add("На Полому!");
        CraboRackSidewaysBoolePhrase.PassiveLogRus.Add("*Кручусь на месте*");

        WeedwickRuthlessHunter.PassiveLogRus.Add("От меня не спрячешься");
        WeedwickRuthlessHunter.PassiveLogRus.Add("ВУФ!");
        WeedwickRuthlessHunter.PassiveLogRus.Add("Псевдособаааакааа");
        WeedwickRuthlessHunter.PassiveLogRus.Add("Что за запах..?");

        WeedwickValuablePreyPoints1.PassiveLogRus.Add("SHOTDDDDDDOWN");
        WeedwickValuablePreyPoints2.PassiveLogRus.Add("ГОВОРЯЩАЯ СОБАКА");
        WeedwickValuablePreyPoints3.PassiveLogRus.Add("ГАФ");
        WeedwickValuablePreyPoints4.PassiveLogRus.Add("ОМЕГА-ГАФ");
        WeedwickValuablePreyPoints5.PassiveLogRus.Add("АльфоВолк!");
        WeedwickValuablePreyPoints6.PassiveLogRus.Add("СААААМАЯ БОГАТАЯ ПСЕВДОСОБАКА");
        WeedwickValuablePreyPoints7.PassiveLogRus.Add("Не употребляйте наркотики");

        WeedwickValuablePreyDrop.PassiveLogRus.Add("БЛЯТЬ, СУКА, СОБАКА! АУУУУУУУУУФ! СОБАКАСОБАКАСОБАКА!!!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Я - Шиза воплоти!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("На нахуй!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Спускайся вниз!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("ГАФГАФГАФ РРРРРРРР");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Слезай с дерева!!!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Биологи, почему собаки смеются? Потому что смешно!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Я смеюсь над тобой!");
        WeedwickValuablePreyDrop.PassiveLogRus.Add("Покусаю, покусаю! Напрыгну, укушу!");



        WeedwickWeedYes1.PassiveLogRus.Add("Weed");
        WeedwickWeedYes2.PassiveLogRus.Add("Вуф");
        WeedwickWeedYes3.PassiveLogRus.Add("Авуууу!!!");
        WeedwickWeedYes4.PassiveLogRus.Add("СКУБИ-ДУБИ-ДУ-УУ!");
        WeedwickWeedYes5.PassiveLogRus.Add("WEED!");
        WeedwickWeedYes6.PassiveLogRus.Add("WEEEEEED!!!");
        WeedwickWeedYes7.PassiveLogRus.Add("SWA$$GG");
        WeedwickWeedYes8.PassiveLogRus.Add("D_O_double.G");
        WeedwickWeedYes9.PassiveLogRus.Add("420");
        WeedwickWeedYes10.PassiveLogRus.Add("81@23 !7");
        WeedwickWeedYes11.PassiveLogRus.Add("Я выбиваю из них ДУРЬ");

        WeedwickWeedNo.PassiveLogRus.Add("ГДЕ МОЙ WEED СУКИ?");
        WeedwickWeedNo.PassiveLogRus.Add("ГДЕ ШО");
        WeedwickWeedNo.PassiveLogRus.Add("228 папиросим, НО ГДЕ???");

        KratosEventYes.PassiveLogRus.Add("If all of Olympus will deny me my vengeance, then all of Olympus **will die**!");
        KratosEventNo.PassiveLogRus.Add("My vengeance ends now!");
        KratosEventFailed.PassiveLogRus.Add("AAAAAAAAAAAAAAAAAAAAAAAAAAA");

        KratosEventKill.PassiveLogRus.Add("Я вернулся из мертвых, чтобы подняться на олимп!");
        KratosEventKill.PassiveLogRus.Add("Зевс, твой сын идет за тобой!");
        KratosEventKill.PassiveLogRus.Add("Я не остановлюсь. Скоро атеисты окажутся правы!");
        KratosEventKill.PassiveLogRus.Add("Еще один....");
        KratosEventKill.PassiveLogRus.Add("Теперь останется лишь один бог! Вот и всё.");

        KratosTarget.PassiveLogRus.Add("О, ебать, лутбокс Пандоры!");
        KratosTarget.PassiveLogRus.Add("Гораздо круче Ареса");
        KratosTarget.PassiveLogRus.Add("Ненавижу богов!");
        KratosTarget.PassiveLogRus.Add("Сука, еще один бог");
        KratosTarget.PassiveLogRus.Add("Куда подевали мою семью? МММ????");
        KratosTarget.PassiveLogRus.Add("Я буду мстить");
        KratosTarget.PassiveLogRus.Add("Ща как покажу!");
        KratosTarget.PassiveLogRus.Add("The power to creater the power to creater the power to destroy!");
        KratosTarget.PassiveLogRus.Add("Призрак Спарты в заброшенном доме... Буууу!");


        // ── Сайтама ──

        SaitamaBroke.PassiveLogRus.Add("О, сегодня скидка на доширак!");
        SaitamaBroke.PassiveLogRus.Add("Суббота. Сегодня капуста на распродаже!");
        SaitamaBroke.PassiveLogRus.Add("Герой - это хобби, а хобби не приносит денег");

        SaitamaBrokeMonster.PassiveLogRus.Add("Если никто не видел, считается ли это за подвиг?");

        SaitamaUnnoticed.PassiveLogRus.Add("Этот монстр пал от руки Кинга! ...наверное.");

        SaitamaHoldsBack.PassiveLogRus.Add("Это... всё?");
        SaitamaHoldsBack.PassiveLogRus.Add("Скучно. Пойду домой.");

        SaitamaSerious.PassiveLogRus.Add("Серьёзная серия ударов.");

        // ── Рик Санчез ──

        RickGiantBeans.PassiveLogRus.Add("Вижу ингредиенты... Мне нужны эти бобы, Морти!");
        RickGiantBeans.PassiveLogRus.Add("Морти, иди сюда. Видишь эти бобы?");
        RickGiantBeans.PassiveLogRus.Add("Это не просто бобы, Морти. Это ГИГАНТСКИЕ бобы!");
        RickGiantBeans.PassiveLogRus.Add("Теперь-то я вижу у кого эти бобы! Не зря прокачался! Да! Science beatch!!");

        RickGiantBeansDrink.PassiveLogRus.Add("*бурп* Это... это наука, Морти!");
        RickGiantBeansDrink.PassiveLogRus.Add("Еще один ингредиент собран. *бурп*");
        RickGiantBeansDrink.PassiveLogRus.Add("Wubba Lubba Dub Dub! Ингредиент!");

        RickPickleTransform.PassiveLogRus.Add("Я ОГУРЧИК РИИИК!");
        RickPickleTransform.PassiveLogRus.Add("PICKLE RIIICK!");
        RickPickleTransform.PassiveLogRus.Add("Я превратил себя в огурец, Морти! Я ОГУРЧИК РИК!");

        RickPickleWin.PassiveLogRus.Add("Огурчик Рик побеждает!");
        RickPickleWin.PassiveLogRus.Add("Ты только что проиграл огурцу!");
        RickPickleWin.PassiveLogRus.Add("Solenya... Огурчик мести!");
        RickPickleWin.PassiveLogRus.Add("Ах, Морти, чувствую себя как огурчик!");

        RickPicklePenalty.PassiveLogRus.Add("Кто-нибудь... переверните огурец...");
        RickPicklePenalty.PassiveLogRus.Add("Ладно, может идея с огурцом была не лучшей...");

        RickPortalGunInvented.PassiveLogRus.Add("Портальная пушка изобретена!");
        RickPortalGunInvented.PassiveLogRus.Add("*щёлк* Теперь у меня есть портальная пушка!");
        RickPortalGunInvented.PassiveLogRus.Add("Я потерял всё, прежде чем создал эту пушку...");

        RickPortalGunFired.PassiveLogRus.Add("*щёлк* Портал открыт!");
        RickPortalGunFired.PassiveLogRus.Add("И мы в деле! Портал!");
        RickPortalGunFired.PassiveLogRus.Add("Заходи в портал, Морти! Быстро!");
        RickPortalGunFired.PassiveLogRus.Add("Я самый риканутый среди всех фальшивых Риков.");

        RickMostWanted.PassiveLogRus.Add("У Рика снова проблемы...");
        RickMostWanted.PassiveLogRus.Add("Рик привлекает слишком много внимания...");
        RickMostWanted.PassiveLogRus.Add("Галактическая федерация опять на хвосте!");

        RickMostWantedProc.PassiveLogRus.Add("Да чего этим федералам надо от меня?!");
        RickMostWantedProc.PassiveLogRus.Add("Вся вселенная гоняется за рецептом моего особого топлива...");
        RickMostWantedProc.PassiveLogRus.Add("Боже! Может умнейший человек во вселенной просто спокойно провести время с внуком?!");

        RickMostWantedPortalFollow.PassiveLogRus.Add("Jeez, вам настолько нечем заняться? Заведите себе хобби!");

        // -- Кира --

        KiraDeathNoteKill.PassiveLogRus.Add("Удалить... Удалить... Удалить!");
        KiraDeathNoteKill.PassiveLogRus.Add("Я - бог нового мира!");
        KiraDeathNoteKill.PassiveLogRus.Add("Правосудие свершилось.");
        KiraDeathNoteKill.PassiveLogRus.Add("Сакудзё... Сакудзё...");
        KiraDeathNoteKill.PassiveLogRus.Add("Всё по плану, Рюк.");

        KiraDeathNoteFailed.PassiveLogRus.Add("Это имя... неправильное?!");
        KiraDeathNoteFailed.PassiveLogRus.Add("Невозможно! Ошибка в расчетах!");
        KiraDeathNoteFailed.PassiveLogRus.Add("Тч... Рюк, ты мог бы предупредить.");

        KiraShinigamiEyes.PassiveLogRus.Add("Глаза бога смерти видят всё.");
        KiraShinigamiEyes.PassiveLogRus.Add("Теперь я вижу твоё настоящее имя...");
        KiraShinigamiEyes.PassiveLogRus.Add("Сделка с Рюком заключена.");

        KiraLNoFight.PassiveLogRus.Add("L не подозревает ничего...");
        KiraLNoFight.PassiveLogRus.Add("Пока L занят расследованием, я действую.");
        KiraLNoFight.PassiveLogRus.Add("L никогда не узнает правду.");

        KiraArrested.PassiveLogRus.Add("Нет... это невозможно! L... ты...");
        KiraArrested.PassiveLogRus.Add("Всё кончено. L победил.");

        // -- Итачи --

        ItachiCrows = new PhraseClass("Вороны");
        ItachiIzanagi = new PhraseClass("Изанаги");
        ItachiAmaterasu = new PhraseClass("Аматерасу");
        ItachiTsukuyomiCharge = new PhraseClass("Глаза Итачи");
        ItachiTsukuyomiActivate = new PhraseClass("Глаза Итачи");
        ItachiTsukuyomiEnd = new PhraseClass("Глаза Итачи");
        ItachiTsukuyomiSteal = new PhraseClass("Глаза Итачи");
        ItachiTsukuyomiReveal = new PhraseClass("Глаза Итачи");

        ItachiCrows.PassiveLogRus.Add("Черный ворон...");
        ItachiCrows.PassiveLogRus.Add("Лети, лети моя хуерга!");
        ItachiCrows.PassiveLogRus.Add("Курлык-курлык!");
        ItachiCrows.PassiveLogRus.Add("Загадочный ворон");
        ItachiCrows.PassiveLogRus.Add("Откуда у меня столько ворон?");
        ItachiCrows.PassiveLogRus.Add("Что есть ложь - Мои глаза или моя ворона?");
        ItachiCrows.PassiveLogRus.Add("\u30A4\u30BF\u30C1");

        ItachiIzanagi.PassiveLogRus.Add("О, ебать, а я не сдох");
        ItachiIzanagi.PassiveLogRus.Add("Это очередная запретная техника моего клана...");

        ItachiAmaterasu.PassiveLogRus.Add("Беги, глупый братец!");
        ItachiAmaterasu.PassiveLogRus.Add("Беги, братишка!");
        ItachiAmaterasu.PassiveLogRus.Add("Живи, но живи в страхе");
        ItachiAmaterasu.PassiveLogRus.Add("НЕНАВИДЬ МЕНЯ ВСЕМ СЕРДЦЕМ!");
        ItachiAmaterasu.PassiveLogRus.Add("Черное пламя в твоем сердце");
        ItachiAmaterasu.PassiveLogRus.Add("Наши глаза... погружаются во тьму");
        ItachiAmaterasu.PassiveLogRus.Add("Я жру тьму");
        ItachiAmaterasu.PassiveLogRus.Add("Беги и цепляйся за жизнь...");
        ItachiAmaterasu.PassiveLogRus.Add("Тебе нехватает... ненависти.");
        ItachiAmaterasu.PassiveLogRus.Add("Приходи, когда получишь такие же глаза... *_*");

        ItachiTsukuyomiCharge.PassiveLogRus.Add("Цукуеми готово...");

        ItachiTsukuyomiActivate.PassiveLogRus.Add("Ты УЖЕ внутри моей иллюзии. ");

        ItachiTsukuyomiEnd.PassiveLogRus.Add("У меня кончилась чакра");

        ItachiTsukuyomiSteal.PassiveLogRus.Add("Get confused");
        ItachiTsukuyomiSteal.PassiveLogRus.Add("My eyes! My eyes!");
        ItachiTsukuyomiSteal.PassiveLogRus.Add("У меня всегда есть ворона в рукаве");
        ItachiTsukuyomiSteal.PassiveLogRus.Add("Я планировал это с самого начала");
        ItachiTsukuyomiSteal.PassiveLogRus.Add("Все это ради деревни");

        ItachiTsukuyomiReveal.PassiveLogRus.Add("Всё это было в глазах у Итачи...");

        // Продавец
        SellerVparit = new PhraseClass("Впарить говна");
        SellerVparit.PassiveLogRus.Add("Пст... Попробуй это.");
        SellerVparit.PassiveLogRus.Add("Эй парень, сегодня твой счастливый день");
        SellerVparit.PassiveLogRus.Add("Только тсс...");
        SellerVparit.PassiveLogRus.Add("Это работает, мамой клянусь!");
        SellerVparit.PassiveLogRus.Add("Клиент всегда прав...");

        SellerVparitEnemy = new PhraseClass("Впарить говна");
        SellerVparitEnemy.PassiveLogRus.Add("Вам впарили говна");

        SellerZakup = new PhraseClass("Закуп");
        SellerZakup.PassiveLogRus.Add("Беру оптом!");
        SellerZakup.PassiveLogRus.Add("Сегодня низкие цены!");
        SellerZakup.PassiveLogRus.Add("Хороший товар!");
        SellerZakup.PassiveLogRus.Add("Настало МОЁ время!");

        SellerProfit = new PhraseClass("Выгодная сделка");
        SellerProfit.PassiveLogRus.Add("Подоходный налог");
        SellerProfit.PassiveLogRus.Add("Плата по счетам");
        SellerProfit.PassiveLogRus.Add("Всё по контракту");

        SellerProfitBig = new PhraseClass("Выгодная сделка");
        SellerProfitBig.PassiveLogRus.Add("Какой процент!");

        SellerBolshoiKushEnemy = new PhraseClass("Выгодная сделка");
        SellerBolshoiKushEnemy.PassiveLogRus.Add("Welcome, stranger.");
        SellerBolshoiKushEnemy.PassiveLogRus.Add("What are you buing and what are you selling?");
        SellerBolshoiKushEnemy.PassiveLogRus.Add("Good choice...");
        SellerBolshoiKushEnemy.PassiveLogRus.Add("ОЧЕНЬ дорого!");
        SellerBolshoiKushEnemy.PassiveLogRus.Add("Отличный рудник");

        SellerSecretBuild = new PhraseClass("Секретный билд");
        SellerSecretBuild.PassiveLogRus.Add("Пришло время играть по-настоящему.");

        // Dopa
        DopaVisionReady = new PhraseClass("Взгляд в будущее");
        DopaVisionReady.PassiveLogRus.Add("Взгляд в будущее готов: Кажется я знаю его следующий ход...");

        DopaVisionProc = new PhraseClass("Взгляд в будущее");
        DopaVisionProc.PassiveLogRus.Add("쉽고 간단합니다.");
        DopaVisionProc.PassiveLogRus.Add("Faker 그렇게 할 수 없었다.");
        DopaVisionProc.PassiveLogRus.Add("이것은 나의 재능으로서 명백하다.");
        DopaVisionProc.PassiveLogRus.Add("럭키. 골든.");
        DopaVisionProc.PassiveLogRus.Add("나는 미래를 본다. 사이트를 통해 나를 번역하지 마라.");

        DopaMetaChosen = new PhraseClass("Законодатель меты");
        DopaMetaChosen.PassiveLogRus.Add("잘 생긴");
        DopaMetaChosen.PassiveLogRus.Add("이제 내가 보여줄거야.");

        DopaImpact = new PhraseClass("Пассивный импакт");
        DopaImpact.PassiveLogRus.Add("Импакт.");

        DopaDomination = new PhraseClass("Доминация");
        DopaDomination.PassiveLogRus.Add("Доминирую.");

        DopaRoam = new PhraseClass("Роум");
        DopaRoam.PassiveLogRus.Add("Роумлю.");

        // Misc new phrases
        HighEloLoss = new PhraseClass("Хай эло");
        HighEloLoss.PassiveLogRus.Add("кажется у врага 2к эло!");

        SirinoksGeniusPhrase = new PhraseClass("Обучение");
        SirinoksGeniusPhrase.PassiveLogRus.Add("Интеллект **10** - ты ***Гений Говна*** (с) Sirinoks");

        SirinoksBlockNoPhrase = new PhraseClass("Блок");
        SirinoksBlockNoPhrase.PassiveLogRus.Add("НЕТ!");

        GlebDragonReaction = new PhraseClass("Спящее хуйло");
        GlebDragonReaction.PassiveLogRus.Add("Ogo, drakon, nihuya sebe");

        GlebWakeUpRoli = new PhraseClass("Спящее хуйло");
        GlebWakeUpRoli.PassiveLogRus.Add("POSTAV ROLI");

        GlebPsyche10 = new PhraseClass("Прокачка");
        GlebPsyche10.PassiveLogRus.Add("vobshe baldej");

        GlebComeBackEnemy = new PhraseClass("Я щас приду");
        GlebComeBackEnemy.PassiveLogRus.Add("ты ушел ждать глеба... НАВЕЧНО...");

        LeCrispStonks = new PhraseClass("Импакт");
        LeCrispStonks.PassiveLogRus.Add("Stonks");

        DeepListMadnessHardKittyMilk = new PhraseClass("Безумие");
        DeepListMadnessHardKittyMilk.PassiveLogRus.Add("БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!");

        DeepListMockeryHardKittyMilk = new PhraseClass("Стёб");
        DeepListMockeryHardKittyMilk.PassiveLogRus.Add("БОЛЬШЕ МОЛОКА ДЛЯ ХАРДКИТТИ!");

        // Салдорум
        SaldorumSurprise = new PhraseClass("Парень с сюрпризом");
        SaldorumSurprise.PassiveLogRus.Add("Хохол помечен! Сюрприз, сука!");
        SaldorumSurprise.PassiveLogRus.Add("Ещё один Хохол в коллекции...");

        SaldorumSalo = new PhraseClass("Сало");
        SaldorumSalo.PassiveLogRus.Add("Сало рулит! Двойная мораль!");
        SaldorumSalo.PassiveLogRus.Add("Хохлы получают по заслугам...");

        SaldorumNinja = new PhraseClass("Ниндзя");
        SaldorumNinja.PassiveLogRus.Add("Тихо пришёл, тихо ушёл...");
        SaldorumNinja.PassiveLogRus.Add("Никто не видел, никто не знает...");

        SaldorumChronicler = new PhraseClass("Великий летописец");
        SaldorumChronicler.PassiveLogRus.Add("История пишется победителями...");

        // Salldorum
        SalldorumShen = new PhraseClass("Шэн");
        SalldorumShen.PassiveLogRus.Add("Таунт!");
        SalldorumShen.PassiveLogRus.Add("Да я без палива.");
        SalldorumShen.PassiveLogRus.Add("Я просто встану тут и бэкнусь.");

        SalldorumOchko = new PhraseClass("Очко");
        SalldorumOchko.PassiveLogRus.Add("Люблю получать очко.");
        SalldorumOchko.PassiveLogRus.Add("Кто меня поймает?");
        SalldorumOchko.PassiveLogRus.Add("Я просто без палива стою здесь и бэкаюсь... Такой беззащитный...");

        SalldorumTimeCapsuleBury = new PhraseClass("Временная капсула");
        SalldorumTimeCapsuleBury.PassiveLogRus.Add("Кола закопана. Когда-нибудь вернусь за ней...");
        SalldorumTimeCapsuleBury.PassiveLogRus.Add("Зарыл колу на память.");

        SalldorumTimeCapsulePickup = new PhraseClass("Временная капсула");
        SalldorumTimeCapsulePickup.PassiveLogRus.Add("Кола найдена! Бодрость на максимум!");
        SalldorumTimeCapsulePickup.PassiveLogRus.Add("Вернулся за колой. Она ещё холодная!");

        SalldorumChroniclerTriple = new PhraseClass("Великий летописец");
        SalldorumChroniclerTriple.PassiveLogRus.Add("Летопись помнит всё. Тройной Скилл!");
        SalldorumChroniclerTriple.PassiveLogRus.Add("Победителей судят. Тройной Скилл активирован.");

        SalldorumChroniclerRewrite = new PhraseClass("Великий летописец");
        SalldorumChroniclerRewrite.PassiveLogRus.Add("История переписана!");
        SalldorumChroniclerRewrite.PassiveLogRus.Add("Кто контролирует прошлое — контролирует будущее.");

        SalldorumChroniclerRewriteGlobal = new PhraseClass("Великий летописец");
        SalldorumChroniclerRewriteGlobal.PassiveLogRus.Add("Salldorum переписал историю раунда");
        SalldorumChroniclerRewriteGlobal.PassiveLogRus.Add("Летописец изменил прошлое...");

        // Napoleon
        NapoleonAlliance = new PhraseClass("Вступить в союз");
        NapoleonAlliance.PassiveLogRus.Add("Napoleon Wonnafuck предлагает вам вступить в союз...");

        NapoleonConqueror = new PhraseClass("Завоеватель");
        NapoleonConqueror.PassiveLogRus.Add("Теперь это наша земля!");

        NapoleonPeaceTreaty = new PhraseClass("Мирный договор");
        NapoleonPeaceTreaty.PassiveLogRus.Add("Перемирие");

        NapoleonFace = new PhraseClass("Меня надо знать в лицо");
        NapoleonFace.PassiveLogRus.Add("Эффект неожиданности");
        NapoleonFace.PassiveLogRus.Add("Застал врасплох");
        NapoleonFace.PassiveLogRus.Add("Такова моя стратегия!");
        NapoleonFace.PassiveLogRus.Add("Аламут");
        NapoleonFace.PassiveLogRus.Add("Построение Черепахи");

        // Toxic Mate
        ToxicMateIntFirstLoss = new PhraseClass("INT");
        ToxicMateIntFirstLoss.PassiveLogRus.Add("Ok. I'm trolling.");

        ToxicMateCancerInfect = new PhraseClass("Get cancer");
        ToxicMateCancerInfect.PassiveLogRus.Add("{name} infected");
        ToxicMateCancerInfect.PassiveLogRus.Add("{name} is no0b");
        ToxicMateCancerInfect.PassiveLogRus.Add("{name} reported!");
        ToxicMateCancerInfect.PassiveLogRus.Add("{name} almost done");
        ToxicMateCancerInfect.PassiveLogRus.Add("{name} FEEDER!");

        ToxicMateCancerReturn = new PhraseClass("Get cancer");
        ToxicMateCancerReturn.PassiveLogRus.Add("Fuking idiots! All reported.");

        ToxicMateAggressPoint = new PhraseClass("Aggress");
        ToxicMateAggressPoint.PassiveLogRus.Add("LET'S TALK ABOUT IT");
        ToxicMateAggressPoint.PassiveLogRus.Add("SHUT UP I WON");
        ToxicMateAggressPoint.PassiveLogRus.Add("EZ");
        ToxicMateAggressPoint.PassiveLogRus.Add("Yeah stay afk!");

        ToxicMateAggressWontStop = new PhraseClass("Aggress");
        ToxicMateAggressWontStop.PassiveLogRus.Add("I. WONT. STOP.");

        ToxicMateTiltedReact = new PhraseClass("Tilted");
        ToxicMateTiltedReact.PassiveLogRus.Add("Hahaha look at this");
        ToxicMateTiltedReact.PassiveLogRus.Add("Are you mad?");
        ToxicMateTiltedReact.PassiveLogRus.Add("Go cry!");
        ToxicMateTiltedReact.PassiveLogRus.Add("Report this kid");
        ToxicMateTiltedReact.PassiveLogRus.Add("KYS");
        ToxicMateTiltedReact.PassiveLogRus.Add("Tilted");
        ToxicMateTiltedReact.PassiveLogRus.Add("OMG IDIOT");
        ToxicMateTiltedReact.PassiveLogRus.Add("Pepega");

        ToxicMateTiltedOpenMid = new PhraseClass("Tilted");
        ToxicMateTiltedOpenMid.PassiveLogRus.Add("OPEN MID!");
        //end Toxic Mate

        // Таинственный Суппорт
        SupportStakes = new PhraseClass("Stakes!");
        SupportStakes.PassiveLogRus.Add("СТАКИ!!!");
        SupportStakes.PassiveLogRus.Add("Пора дамажить!");

        SupportProtect = new PhraseClass("Protect");
        SupportProtect.PassiveLogRus.Add("Пепега");

        SupportPremadeMark = new PhraseClass("Premade");
        SupportPremadeMark.PassiveLogRus.Add("Ты теперь мой напарник...");

        SupportPremadeAntiSkip = new PhraseClass("Premade");
        SupportPremadeAntiSkip.PassiveLogRus.Add("QSS!");
        SupportPremadeAntiSkip.PassiveLogRus.Add("Cleance!");
        SupportPremadeAntiSkip.PassiveLogRus.Add("BKB!");
        //end Таинственный Суппорт

        // Стая Гоблинов
        GoblinTunnelEscape.PassiveLogRus.Add("Отлично! Сбежали.");
        GoblinTunnelEscape.PassiveLogRus.Add("Выкрутились из ситуации!");
        GoblinTunnelEscape.PassiveLogRus.Add("Хорошенько оторвались!!!");
        GoblinTunnelEscape.PassiveLogRus.Add("Теперь все подумают что мы Глеб, хехех");

        GoblinGrowthAttack.PassiveLogRus.Add("Дали поебаться");
        GoblinGrowthAttack.PassiveLogRus.Add("Гоблины трахают!");
        GoblinGrowthAttack.PassiveLogRus.Add("Гоблины.");
        GoblinGrowthAttack.PassiveLogRus.Add("Больше гоблинов богу гоблинов!");
        GoblinGrowthAttack.PassiveLogRus.Add("Я трахался на летних каникулах... Никому это не понравилось");
        GoblinGrowthAttack.PassiveLogRus.Add("Меня интересуют только сиськи и джем, это моя маленькая мечта");

        GoblinDeath.PassiveLogRus.Add("Тупое говно, тупого говна");
        GoblinDeath.PassiveLogRus.Add("Хайп. Мем. Флеш-моб.");
        GoblinDeath.PassiveLogRus.Add("Малолетние Дебилы.");
        GoblinDeath.PassiveLogRus.Add("Дебилы.");
        GoblinDeath.PassiveLogRus.Add("Пендальф серый");

        GoblinMine.PassiveLogRus.Add("Отличный рудник!");
        GoblinMine.PassiveLogRus.Add("Надежный как швейцарские часы!");

        GoblinZigguratBuild.PassiveLogRus.Add("Зиккурат построен!");
        GoblinZigguratBuild.PassiveLogRus.Add("Гоблины умеют строить!");
        GoblinZigguratBuild.PassiveLogRus.Add("Новая крепость!");

        GoblinZigguratNoMoney.PassiveLogRus.Add("Невозможно! Оочччччччччень дорого!");

        GoblinZigguratWorkerDeath.PassiveLogRus.Add("Трудяга умер - кранчил как в CDPR");
        GoblinZigguratWorkerDeath.PassiveLogRus.Add("Трудяга погиб - мы построили Зиккурат, но какой ценой..?");
        GoblinZigguratWorkerDeath.PassiveLogRus.Add("Трудяга погиб - его хватил сердечный приступ, когда он увидел дворец на ютубе");
        //end Стая Гоблинов

        // Котики
        KotikiMinka.PassiveLogRus.Add("Мяу~");
        KotikiMinka.PassiveLogRus.Add("Минька мурчит...");
        KotikiMinka.PassiveLogRus.Add("*мур-мур*");

        KotikiStormTaunt.PassiveLogRus.Add("Штормяк шипит!");
        KotikiStormTaunt.PassiveLogRus.Add("МЯЯЯЯУ!!!");

        KotikiStormWin.PassiveLogRus.Add("Штормяк доволен!");
        KotikiStormWin.PassiveLogRus.Add("Не злите кота.");

        KotikiCatDeploy.PassiveLogRus.Add("Кот остался на вражеской территории...");
        KotikiCatDeploy.PassiveLogRus.Add("Кот затаился...");

        KotikiCatReturn.PassiveLogRus.Add("Кот вернулся с добычей!");
        KotikiCatReturn.PassiveLogRus.Add("Кот принес подарок!");

        KotikiLevelUp.PassiveLogRus.Add("Мяу~ +1 Справедливость");
        KotikiLevelUp.PassiveLogRus.Add("*мур* Справедливость!");
        //end Котики

        // TheBoys
        TheBoysOrderNew.PassiveLogRus.Add("Француз: Новый заказ. Работаем.");
        TheBoysOrderNew.PassiveLogRus.Add("Француз: Цель определена. Приступаю.");
        TheBoysOrderNew.PassiveLogRus.Add("Француз: *загружает дробовик* Погнали.");

        TheBoysOrderComplete.PassiveLogRus.Add("Француз: Заказ выполнен. Чисто.");
        TheBoysOrderComplete.PassiveLogRus.Add("Француз: Готово. Следующий.");
        TheBoysOrderComplete.PassiveLogRus.Add("Француз: Ещё одна галочка в списке.");

        TheBoysOrderFailed.PassiveLogRus.Add("Француз: Заказ провален. Merde.");
        TheBoysOrderFailed.PassiveLogRus.Add("Француз: Не успел. Дерьмо.");
        TheBoysOrderFailed.PassiveLogRus.Add("Француз: Цель ушла. Бывает.");

        TheBoysChemWeapon.PassiveLogRus.Add("Француз: *распыляет* Вдохни поглубже.");
        TheBoysChemWeapon.PassiveLogRus.Add("Француз: Хим.оружие не выбирает жертв.");
        TheBoysChemWeapon.PassiveLogRus.Add("Француз: Маленький подарок от Франции.");

        TheBoysPoker.PassiveLogRus.Add("Бучер: *взмах кочергой* Diabolical.");
        TheBoysPoker.PassiveLogRus.Add("Бучер: Oi! Получи кочергой по ебалу!");
        TheBoysPoker.PassiveLogRus.Add("Бучер: Кочерга решает всё.");

        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: ... .., ..!");
        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: я. не. сдамся.");
        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: ...き...も...ち...=)");
        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: *молча сжимает кулаки*");
        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: боль — это слабость, покидающая тело");
        TheBoysKimikoRegen.PassiveLogRus.Add("Kimiko: ...");

        TheBoysKimikoDisabled.PassiveLogRus.Add("Kimiko ранена. Регенерация отключена.");
        TheBoysKimikoDisabled.PassiveLogRus.Add("Kimiko: *тяжело дышит* ...нужно время.");
        TheBoysKimikoDisabled.PassiveLogRus.Add("Kimiko выведена из строя.");

        TheBoysKimikoRecovered.PassiveLogRus.Add("Kimiko снова в строю!");
        TheBoysKimikoRecovered.PassiveLogRus.Add("Kimiko: *разминает кулаки* Я вернулась.");
        TheBoysKimikoRecovered.PassiveLogRus.Add("Kimiko восстановилась.");

        TheBoysKompromatGathered.PassiveLogRus.Add("М.М.: Досье собрано. Это пригодится.");
        TheBoysKompromatGathered.PassiveLogRus.Add("М.М.: *записывает в блокнот* Интересно...");
        TheBoysKompromatGathered.PassiveLogRus.Add("М.М.: Компромат получен. Хорошая работа.");

        TheBoysKompromatReward.PassiveLogRus.Add("М.М.: Все данные сходятся. Очки умножены.");
        TheBoysKompromatReward.PassiveLogRus.Add("М.М.: Компромат работает. Результат налицо.");
        //end TheBoys

        // Монстр без имени
        MonsterDeath.PassiveLogRus.Add("Все люди могут стать монстрами. Нужен лишь подходящий момент.");
        MonsterDeath.PassiveLogRus.Add("Йохан: Доктор Тэнма... вы снова подарили мне жизнь. А я подарю вам — смерть.");
        MonsterDeath.PassiveLogRus.Add("Посмотри на меня. Посмотри на монстра внутри себя.");
        MonsterDeath.PassiveLogRus.Add("Нет ничего страшнее, чем ребёнок без имени.");
        MonsterDeath.PassiveLogRus.Add("Единственное, что равно жизни — это смерть.");
        MonsterDeath.PassiveLogRus.Add("Мне не нужно оружие. Мне достаточно слов.");

        MonsterDrop.PassiveLogRus.Add("Они падают. Как дети из Кинденхайма.");
        MonsterDrop.PassiveLogRus.Add("Йохан: Каждое падение — это маленькая история, которую я написал.");
        MonsterDrop.PassiveLogRus.Add("Посмотри... мир рушится. Разве это не прекрасно?");
        MonsterDrop.PassiveLogRus.Add("Ещё один упал. Как в той книжке с картинками...");

        MonsterTwinSteal.PassiveLogRus.Add("Йохан: Я забираю всё, что тебе дорого. Начнём со справедливости.");
        MonsterTwinSteal.PassiveLogRus.Add("У Близнеца нет своего лица. Но у него есть твоё.");
        MonsterTwinSteal.PassiveLogRus.Add("Анна и Йохан — два имени, одна тьма. Твоя справедливость теперь моя.");
        MonsterTwinSteal.PassiveLogRus.Add("Ты пришёл убить монстра. Но монстр уже забрал всё, что у тебя было.");

        MonsterApocalypse.PassiveLogRus.Add("Йохан: Настоящий конец света — это когда некому вспомнить твоё имя.");
        MonsterApocalypse.PassiveLogRus.Add("Пейзаж конца света раскрылся. Идеальный суицид — забрать с собой весь мир.");
        MonsterApocalypse.PassiveLogRus.Add("Йохан: Последний, кто выживет, увидит настоящий ад.");
        //end Монстр без имени

        // Геральт
        GeraltContractSpawn = new PhraseClass("Ведьмачий Заказ");
        GeraltContractSpawn.PassiveLogRus.Add("На доске объявлений появился новый заказ...");
        GeraltContractSpawn.PassiveLogRus.Add("Люди снова просят о помощи. Надо разобраться.");
        GeraltContractSpawn.PassiveLogRus.Add("Сколько же вас тут развелось?");

        GeraltContractWin = new PhraseClass("Ведьмачий Заказ");
        GeraltContractWin.PassiveLogRus.Add("Контракт выполнен. Где мои деньги?");
        GeraltContractWin.PassiveLogRus.Add("Готово. Заплатите и я уйду.");
        GeraltContractWin.PassiveLogRus.Add("Монстра больше нет. Как и обещал.");

        GeraltContractLost = new PhraseClass("Ведьмачий Заказ");
        GeraltContractLost.PassiveLogRus.Add("Тварь оказалась сильнее, чем я думал.");
        GeraltContractLost.PassiveLogRus.Add("Нужно лучше подготовиться...");
        GeraltContractLost.PassiveLogRus.Add("Придётся вернуться позже.");

        GeraltMeditation = new PhraseClass("Медитация");
        GeraltMeditation.PassiveLogRus.Add("*медитирует у костра*");
        GeraltMeditation.PassiveLogRus.Add("Мне нужно сосредоточиться...");
        GeraltMeditation.PassiveLogRus.Add("Место Силы, должно быть...");

        GeraltMeditationInterrupted = new PhraseClass("Медитация");
        GeraltMeditationInterrupted.PassiveLogRus.Add("Никак они, блять, не научатся.");
        GeraltMeditationInterrupted.PassiveLogRus.Add("Прервали медитацию? Плохая идея.");
        GeraltMeditationInterrupted.PassiveLogRus.Add("Ну что ж, сами напросились.");

        GeraltOilActivate = new PhraseClass("Ведьмачое Масло");
        GeraltOilActivate.PassiveLogRus.Add("*наносит масло на меч*");
        GeraltOilActivate.PassiveLogRus.Add("Серебряный? Стальной? Оба.");
        GeraltOilActivate.PassiveLogRus.Add("Подготовка — половина победы.");

        GeraltOilUsed = new PhraseClass("Ведьмачое Масло");
        GeraltOilUsed.PassiveLogRus.Add("Масло истрачено. Нужна медитация.");
        GeraltOilUsed.PassiveLogRus.Add("Клинок нужно снова подготовить.");

        GeraltPlotva = new PhraseClass("Плотва");
        GeraltPlotva.PassiveLogRus.Add("Плотва! Давай, девочка!");
        GeraltPlotva.PassiveLogRus.Add("*свист* Плотва несётся галопом.");
        GeraltPlotva.PassiveLogRus.Add("Как Плотва туда забралась?!");

        GeraltLambertMixup = new PhraseClass("Медитация");
        GeraltLambertMixup.PassiveLogRus.Add("Ламберт, Ламберт — хрен моржовый.");
        GeraltLambertMixup.PassiveLogRus.Add("Ламберт с Эскелем подменили травы... опять.");
        GeraltLambertMixup.PassiveLogRus.Add("Весёлая ночь в Каэр Морхене...");

        GeraltBountyStolen = new PhraseClass("Ведьмачий Заказ");
        GeraltBountyStolen.PassiveLogRus.Add("Награда ушла вместе с жертвой...");
        GeraltBountyStolen.PassiveLogRus.Add("Мертвецы не платят. И за мертвецов тоже.");

        GeraltDetective = new PhraseClass("Детектив");
        GeraltDetective.PassiveLogRus.Add("Ведьмачье чутьё подсказывает...");

        GeraltMultiContract = new PhraseClass("Ведьмачий Заказ");
        GeraltMultiContract.PassiveLogRus.Add("Сколько же вас тут развелось?");
        GeraltMultiContract.PassiveLogRus.Add("Одним заказом тут не обойтись.");
        //end Геральт

        //not in the game

        //SaitamaWorthyFound.PassiveLogRus.Add("Наконец-то... Ты заставил меня стараться. Спасибо.");
        //SaitamaWorthyFound.PassiveLogRus.Add("Три года я ждал этого момента!");
        //SaitamaWorthyFound.PassiveLogRus.Add("**ONE PUUUUUUNCH!!!**");

        //end
    }


    //class needed to send unique logs.
    public class PhraseClass
    {
        public List<string> PassiveLogEng = new();
        public List<string> PassiveLogRus = new();
        public string PassiveNameEng;
        public string PassiveNameRus;

        private int Random(int minValue, int maxValue)
        {
            maxValue += 1;
            if (minValue == maxValue) return minValue;
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxValue)}");

            var diff = (long)maxValue - minValue;
            var upperBound = uint.MaxValue / diff * diff;

            uint ui;
            do
            {
                var randomBytes = RandomNumberGenerator.GetBytes(555);
                ui = BitConverter.ToUInt32(randomBytes, 0);
            } while (ui >= upperBound);

            var result = (int)(minValue + ui % diff);
            return result;
        }

        public PhraseClass(string passiveNameRus, string passiveNameEng = "")
        {
            PassiveNameRus = passiveNameRus;
            PassiveNameEng = passiveNameEng;
        }

        public void SendLog(GamePlayerBridgeClass player, bool delete, string prefix = "", bool isRandomOrder = true, string suffix = "")
        {
            var description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
            if (!isRandomOrder)
            {
                description = PassiveLogRus.First();
            }

            if (delete)
            {
                if (PassiveLogRus.Count > 1)
                    PassiveLogRus.Remove(description);
            }
            else
            {
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var i = 0;
                while (i < 20)
                {
                    i++;
                    if (!personalLogs.Contains(description))
                        break;
                    description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];

                    if (!isRandomOrder)
                    {
                        description = PassiveLogRus.First();
                    }
                }
            }

            player.Status.AddInGamePersonalLogs($"|>Phrase<|{PassiveNameRus}: {prefix}{description}{suffix}\n");
        }

        public void SendLog(GamePlayerBridgeClass player, GamePlayerBridgeClass player2, bool delete, bool isRandomOrder = true)
        {
            var description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
            if (!isRandomOrder)
            {
                description = PassiveLogRus.First();
            }
            if (delete)
            {
                if (PassiveLogRus.Count > 1)
                    PassiveLogRus.Remove(description);
            }
            else
            {
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var i = 0;
                while (i < 20)
                {
                    i++;
                    if (!personalLogs.Contains(description))
                        break;
                    description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
                    if (!isRandomOrder)
                    {
                        description = PassiveLogRus.First();
                    }
                }
            }

            description += $"{player2.DiscordUsername} - {player2.GameCharacter.Name}";


            player.Status.AddInGamePersonalLogs($"|>Phrase<|{PassiveNameRus}: {description}\n");
        }

        public async Task SendLogSeparate(GamePlayerBridgeClass player, bool delete, int delayMs, bool isRandomOrder = true)
        {
            if (player.IsBot()) return;

            var description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
            if (!isRandomOrder)
            {
                description = PassiveLogRus.First();
            }
            if (delete)
            {
                if (PassiveLogRus.Count > 1)
                    PassiveLogRus.Remove(description);
            }
            else
            {
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var i = 0;
                while (i < 20)
                {
                    i++;
                    if (!personalLogs.Contains(description))
                        break;
                    description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
                    if (!isRandomOrder)
                    {
                        description = PassiveLogRus.First();
                    }
                }
            }

            if (PassiveLogRus.Count > 1)
                PassiveLogRus.Remove(description);

            // Always store for web display
            player.WebMediaMessages.Add(new GamePlayerBridgeClass.WebMediaEntry
            {
                PassiveName = PassiveNameRus,
                Text = description,
                FileUrl = null,
                FileType = "text"
            });

            // Send to Discord if not web-only
            if (player.IsWebPlayer || player.PreferWeb) return;
            try
            {
                var mess2 = await player.DiscordStatus.SocketGameMessage.Channel.SendMessageAsync(description);
                player.DeleteMessages.Add(new GamePlayerBridgeClass.DeleteMessagesClass(mess2.Id, delayMs));
            }
            catch (Exception exception)
            {
                Console.Write(exception.Message);
                Console.Write(exception.StackTrace);
            }
        }
        public void SendLogSeparateWeb(GamePlayerBridgeClass player, bool delete, bool isRandomOrder = true, bool isEvent = true)
        {
            var description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
            if (!isRandomOrder)
            {
                description = PassiveLogRus.First();
            }
            if (delete)
            {
                if (PassiveLogRus.Count > 1)
                    PassiveLogRus.Remove(description);
            }
            else
            {
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var i = 0;
                while (i < 20)
                {
                    i++;
                    if (!personalLogs.Contains(description))
                        break;
                    description = PassiveLogRus[Random(0, PassiveLogRus.Count-1)];
                    if (!isRandomOrder)
                    {
                        description = PassiveLogRus.First();
                    }
                }
            }

            if (PassiveLogRus.Count > 1)
                PassiveLogRus.Remove(description);

            if (isEvent){
            player.WebMediaMessages.Add(new GamePlayerBridgeClass.WebMediaEntry
            {
                PassiveName = PassiveNameRus,
                Text = description,
                FileUrl = null,
                FileType = "text"
            });
            }
            else{
                player.WebMessages.Add($"{PassiveNameRus}: {description}");
            }
        }

        public async Task SendLogSeparateWithFile(GamePlayerBridgeClass player, bool delete, string filePath, bool clearNextRound, int delayMs, bool isRandomOrder = true, int roundsToPlay = 1)
        {
            if (player.IsBot()) return;

            var description = PassiveLogRus[Random(0, PassiveLogRus.Count - 1)];
            if (!isRandomOrder)
            {
                description = PassiveLogRus.First();
            }
            if (delete)
            {
                if (PassiveLogRus.Count > 1)
                    PassiveLogRus.Remove(description);
            }
            else
            {
                var personalLogs = player.Status.GetInGamePersonalLogs();
                var i = 0;
                while (i < 20)
                {
                    i++;
                    if (!personalLogs.Contains(description))
                        break;
                    description = PassiveLogRus[Random(0, PassiveLogRus.Count - 1)];
                    if (!isRandomOrder)
                    {
                        description = PassiveLogRus.First();
                    }
                }
            }

            if (PassiveLogRus.Count > 1)
                PassiveLogRus.Remove(description);

            // Always store for web display — convert DataBase path to web URL
            var webUrl = FilePathToWebUrl(filePath);
            var fileType = DetectFileType(filePath);
            player.WebMediaMessages.Add(new GamePlayerBridgeClass.WebMediaEntry
            {
                PassiveName = PassiveNameRus,
                Text = description,
                FileUrl = webUrl,
                FileType = fileType,
                RoundsToPlay = roundsToPlay,
            });

            // Send to Discord if not web-only
            if (player.IsWebPlayer || player.PreferWeb) return;
            try
            {
                var mess2 = await player.DiscordStatus.SocketGameMessage.Channel.SendFileAsync(filePath, description);
                if (clearNextRound)
                    player.DeleteMessages.Add(new GamePlayerBridgeClass.DeleteMessagesClass(mess2.Id, delayMs));
            }
            catch (Exception exception)
            {
                Console.Write(exception.Message);
                Console.Write(exception.StackTrace);
            }
        }

        /// <summary>Converts a DataBase file path to a web-accessible URL.</summary>
        private static string FilePathToWebUrl(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // Normalize separators
            var normalized = filePath.Replace('\\', '/');

            // "DataBase/art/events/kratos_death.jpg" → "/art/events/kratos_death.jpg"
            if (normalized.Contains("DataBase/art/"))
            {
                var idx = normalized.IndexOf("DataBase/art/", StringComparison.OrdinalIgnoreCase);
                return "/" + normalized.Substring(idx + "DataBase/".Length);
            }

            // "DataBase/sound/Kratos_PLAY_ME.mp3" → "/sound/Kratos_PLAY_ME.mp3"
            if (normalized.Contains("DataBase/sound/"))
            {
                var idx = normalized.IndexOf("DataBase/sound/", StringComparison.OrdinalIgnoreCase);
                return "/" + normalized.Substring(idx + "DataBase/".Length);
            }

            // Fallback: just use the filename
            return "/" + System.IO.Path.GetFileName(filePath);
        }

        /// <summary>Detect media type from file extension.</summary>
        private static string DetectFileType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "text";
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".mp3" or ".wav" or ".ogg" or ".flac" or ".aac" or ".m4a" => "audio",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" => "image",
                _ => "text"
            };
        }
    }
}
