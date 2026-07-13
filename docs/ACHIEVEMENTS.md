# Achievements & Loot Boxes V2

> Code-verified reference for the account-level reward systems introduced in V2. Gameplay terms and Russian character/passive names are canonical identifiers; the English and Russian achievement copy is presentation metadata, not dispatch state.

## 1. Achievement model

The live catalog contains exactly **105** achievements: **12 Global**, **80 Character**, and **13 Interaction**. `AchievementDefinition` owns paired EN/RU names, descriptions and secret hints plus category, icon, rarity, target, related characters and rewards (`AchievementClass.cs` `AchievementDefinition`/`AllAchievements`). `tools/audit-achievements.sh` hard-fails unless all expected IDs are unique, present and evaluated.

The 80 character cards are deliberately paired: every one of the 40 character definitions/forms has one **Normal** requirement and one **Hard** requirement. Difficulty is not the reward rarity. In particular, the pre-existing hard cards `c_rick_portals`, `c_itachi_tax`, and `c_kotiki_reunion` remain Rare so their historic rewards stay unchanged; all other rows likewise show their live rarity explicitly.

Progress has two layers:

- `InGameAchievementTracker` records match-local observations only; its sets use player/character IDs where uniqueness matters (`AchievementClass.cs` `InGameAchievementTracker`).
- `AchievementProgress.Current` is the **best result achieved in one match**, not a cumulative total. `SetBestProgress` compares the current attempt with the stored best for display, but tests the **current attempt** against the target before unlocking; two partial matches can never combine (`AchievementClass.cs` `SetBestProgress`).
- An unlock is permanent and exactly-once. Under the account monitor it stamps `UnlockedAt`, queues the ID in `NewlyUnlocked`, credits the rarity reward immediately, and refuses later credits once `IsUnlocked` is true (`AchievementClass.cs` `SetBestProgress`).
- Game-end evaluation runs after final place/reward-place and match facts are known, in the same account-locked settlement that pays ZBS, Daily Quests and top-two loot boxes (`CheckIfReady.cs` game-end reward settlement; `AchievementClass.cs` `TrackGameEnd`).

Unless a row says otherwise, a target greater than 1 means “within one match.” Solo gates are explicit: team mode cannot unlock `g_clean_sweep`, `g_round10_comeback`, or `g_untouchable` (`AchievementClass.cs` `TrackGameEnd`).

## 2. Global achievements (12)

| ID | Achievement (EN / RU) | Single-match requirement | Rarity / reward |
|---|---|---|---|
| `g_bottom_feeder` | Bottom Feeder / Со дна | Attack from current place 6 and defeat the current place-1 player. | Uncommon · 25 ZBS |
| `g_class_advantage` | Rock, Paper, Crown / Камень, ножницы, корона | Win 3 resolved fights while holding the Nemesis class advantage; attack and defence both count. | Rare · 50 ZBS |
| `g_target_routine` | Bullseye / В яблочко | Gain Main Skill from Мишень in 3 different rounds; repeats within one round count once. | Common · 10 ZBS |
| `g_maximum_sentence` | Maximum Sentence / Высшая мера | Win a resolved fight while holding 5 live Justice. | Uncommon · 25 ZBS |
| `g_three_drops` | Down the Chute / Вниз по жёлобу | Personally cause 3 Drops; multi-Drop effects count the actual number caused. | Rare · 50 ZBS |
| `g_twenty_moral` | Moral Bankruptcy / Моральное банкротство | In one exchange, convert 20 Moral into 10 bonus points. | Uncommon · 25 ZBS |
| `g_open_book` | Open Book / Открытая книга | Correctly predict every eligible opponent, with at least 3 eligible targets. Admin, Кира, Мадара and Let's Roll attempts are ineligible; fictional/admin targets are excluded. | Rare · 50 ZBS |
| `g_clean_sweep` | Garbage Collector / Сборщик мусора | In solo mode, defeat all 5 distinct opponents at least once. | Rare · 50 ZBS |
| `g_round10_comeback` | From Sixth to King / Из шестого — в короли | In solo mode, open round 10 in place 6, then finish alive at actual place 1. | Epic · 100 ZBS + 1 box |
| `g_untouchable` | Untouchable / Неприкасаемый | Win a solo match alive with at least 5 resolved fight wins and no resolved fight losses. | Epic · 100 ZBS + 1 box |
| `g_quad_damage` | Quad Damage / Четверной урон | Receive at least 20 net regular points from round 10 after its real multiplier. | Rare · 50 ZBS |
| `g_auto_pilot` | Definitely Not a Bot / Точно не бот | **Secret.** Explicitly use Auto Move in every standard action round: 1–10 normally, 1–9 for Тигр (round-10 ban), or 1–7 plus 9–10 for Мадара (round 8 is locked). Other forced skips do not create exemptions. Reopening the turn removes that round until Auto Move is chosen again. | Epic · 100 ZBS + 1 box |

Definitions and game-end gates are centralized in `AchievementClass.cs` `AllAchievements`/`TrackGameEnd`; Auto Move's exact required-round sets are `HasUsedAutoMoveAllGame`. Fight facts come from the resolved-fight observation block in `DoomsdayMachine.cs`, while explicit Auto Move/Change Mind updates come from `GameReactions.cs:169-205` and `WebGameService.AutoMove`/`ChangeMind`.

## 3. Character achievements (80: 40 Normal + Hard pairs)

Character achievements require the named character/form even if another holder copies a related passive. Exceptions are deliberate identity handling: Молодой Глеб is detected by `Main Ирелия` on runtime `Глеб`; mylorik keeps its story after transforming; Братишка cards require the native form; and Наруто cards require the original, not either clone (`AchievementClass.cs` `TrackGameEnd`). “Normal” and “Hard” below describe requirement difficulty, while rarity independently determines the reward.

| Character / form | Normal card (ID · rarity / reward) | Normal requirement | Hard card (ID · rarity / reward) | Hard requirement |
|---|---|---|---|---|
| TheBoys | French Connection / Французская связь<br>`c_boys_orders` · Uncommon / 25 ZBS | Complete all 3 Francie orders. | The Boys Are Back / Пацаны снова в деле<br>`c_boys_ultimate` · Epic / 100 ZBS + 1 box | Prove one ultimate: infect 3 players from this TheBoys; actually cause 5 Drops while СуперМудень is active; actually steal 5 Justice with Живое Оружие; or finish with M.M. fully upgraded and 3 distinct компромат dossiers. |
| Стая Гоблинов | Location, Location, Ziggurat / Место, место и Зиккурат<br>`c_goblin_architect` · Common / 10 ZBS | Build the first Ziggurat. | Built Different / Особая постройка<br>`c_goblin_summit` · Legendary / 228 ZBS + 2 boxes | Build a Ziggurat at place 1, finish alive at actual place 1, and receive its enforced win. |
| Рик Санчез | Bean There, Done That / Боб был — интеллект вырос<br>`c_rick_beans` · Uncommon / 25 ZBS | Reach 3 Гигантские бобы stacks. | Portal Authority / Портальная власть<br>`c_rick_portals` · Rare / 50 ZBS | Successfully fire Портальная пушка twice. |
| Сайтама | A Worthy Warm-Up / Достойная разминка<br>`c_saitama_serious` · Common / 10 ZBS | Bank at least 5 deferred points through Неприметность. | One Punch / Один удар<br>`c_saitama_one_punch` · Epic / 100 ZBS + 1 box | Reclaim at least 20 deferred points through Ищет достойного противника. |
| Мадара | One Versus an Army / Один против армии<br>`c_madara_round_eight` · Rare / 50 ZBS | Win 3 fights during round 8. | Wake Up to Reality / Очнись и вернись в реальность<br>`c_madara_tsukuyomi` · Legendary / 228 ZBS + 2 boxes · **secret** | Finish with Вечное Цукуеми active and without being sealed. |
| Тигр | Clean Sheet / Сухая победа<br>`c_tigr_three_zero` · Uncommon / 25 ZBS | Complete one 3-0 обоссан. | Six–Zero / Шесть — ноль<br>`c_tigr_six_zero` · Epic / 100 ZBS + 1 box | Complete 3-0 обоссан against 2 different enemies. |
| Итачи | Murder of Crows / Воронья сходка<br>`c_itachi_crows` · Uncommon / 25 ZBS | Have positive Crow counts on 3 different opponents at once. | Tax Collector / Сборщик налогов<br>`c_itachi_tax` · Rare / 50 ZBS | Copy at least 20 total points through Глаза Итачи. |
| Кратос | First Blood of Olympus / Первая кровь Олимпа<br>`c_kratos_rampage` · Common / 10 ZBS | Personally kill one other player during Возвращение из мертвых. | Ghost of Sparta / Призрак Спарты<br>`c_kratos_olympus` · Legendary / 228 ZBS + 2 boxes | Personally kill all 5 other players during Возвращение из мертвых. |
| Кира | First Name Basis / По имени и насмерть<br>`c_kira_first_name` · Common / 10 ZBS | Write one correct victim into Тетрадь смерти. | Perfect Crime / Идеальное преступление<br>`c_kira_perfect_crime` · Epic / 100 ZBS + 1 box | Record successful Тетрадь смерти kills against 3 distinct victims. |
| Монстр без имени | No One Escapes the Story / Из этой истории не уйти<br>`c_monster_no_escape` · Common / 10 ZBS | Execute one pawn through Пейзаж конца света. | Beautiful Apocalypse / Прекрасный апокалипсис<br>`c_monster_apocalypse` · Epic / 100 ZBS + 1 box | Execute at least 2 pawns through Пейзаж конца света. |
| Продавец Сомнительных Тактик | Three Easy Payments / Три выгодных платежа<br>`c_seller_marks` · Uncommon / 25 ZBS | Mark 3 distinct players. | Market Manipulator / Повелитель рынка<br>`c_seller_market` · Epic / 100 ZBS + 1 box | Mark all 5 opponents and accumulate at least 100 Skill in Секретный билд. |
| Dopa | Ward Diff / Разница в вардах<br>`c_dopa_foresight` · Common / 10 ZBS | Trigger Взгляд в будущее once. | Three Steps Ahead / На три шага впереди<br>`c_dopa_big_brain` · Epic / 100 ZBS + 1 box | Trigger Взгляд в будущее 3 times. |
| Salldorum | Open Happiness / Открой счастье<br>`c_salldorum_cola` · Uncommon / 25 ZBS | Drink the Time Capsule cola once. | History Repeats Itself / История повторяется<br>`c_salldorum_double_cola` · Legendary / 228 ZBS + 2 boxes | Drink it twice. The card deliberately hints: «Колу можно выпить дважды, если знать историю.» |
| Геральт | Witcher’s Payday / Ведьмачья получка<br>`c_geralt_contracts` · Uncommon / 25 ZBS | Resolve 3 contract fights. | All Signs Point to Trouble / Все Знаки ведут к беде<br>`c_geralt_path` · Epic / 100 ZBS + 1 box | Reveal all 4 contract targets through Медитация. |
| Котики | Cat Came Back / Один котик вернулся<br>`c_kotiki_one_back` · Common / 10 ZBS | Reclaim either Минька or Штормяк. | The Cats Came Back / Котики вернулись<br>`c_kotiki_reunion` · Rare / 50 ZBS | Reclaim both cats by winning their return attacks. |
| Toxic Mate | Return to Sender / Вернуть отправителю<br>`c_toxic_chain` · Uncommon / 25 ZBS | Have Get cancer return to Toxic Mate after a completed chain of at least 2 transfers. | Full Circle of Toxicity / Полный круг токсичности<br>`c_toxic_return` · Epic / 100 ZBS + 1 box | Have Get cancer return after a completed chain of at least 5 transfers. Only chains that actually return update the tracked maximum. |
| Napoleon Wonnafcuk | First-Fight Diplomacy / Дипломатия первого боя<br>`c_napoleon_alliance` · Uncommon / 25 ZBS | Resolve first-fight business with 3 distinct players. | There Can Be Only Two / Останутся только двое<br>`c_napoleon_treaties` · Epic / 100 ZBS + 1 box | In team mode, form an alliance and defeat all 4 non-team enemies at least once. |
| Таинственный Суппорт | We Scale Together / Скейлимся вместе<br>`c_support_buff` · Rare / 50 ZBS | Finish alive in the top 3 while the marked Carry is also alive in the top 3. | Duo Queue Takeover / Захват дуо-очереди<br>`c_support_premade` · Legendary / 228 ZBS + 2 boxes | Finish alive at actual place 1 while the marked Carry finishes alive at actual place 2. |
| Осьминожка | Three Stops, Eight Arms / Три остановки, восемь щупалец<br>`c_octopus_tour` · Uncommon / 25 ZBS | Record 3 distinct leaderboard places through Раскинуть щупальца. | Every Seat Is Reserved / Забронированы все места<br>`c_octopus_ink` · Epic / 100 ZBS + 1 box | Record all 6 leaderboard places through Раскинуть щупальца. |
| DeepList | Read and Roasted / Прочитал и подколол<br>`c_deeplist_mockery` · Uncommon / 25 ZBS | Reach triggered Стёб entries against 2 distinct players. | Everybody Gets the Joke / Шутку поняли все<br>`c_deeplist_roast` · Epic / 100 ZBS + 1 box | Trigger Стёб against all 5 opponents. |
| mylorik | Two Names on the List / Два имени в списке<br>`c_mylorik_revenge` · Uncommon / 25 ZBS | Complete Месть against 2 distinct players; progress still belongs to mylorik after transformation. | No Grudge Left Behind / Ни одной незакрытой обиды<br>`c_mylorik_grudges` · Epic / 100 ZBS + 1 box | Complete Месть against all 5 opponents. |
| Глеб (classic) | The Tea Can Wait / Чай подождёт<br>`c_gleb_return` · Common / 10 ZBS | Return and meet at least one player who previously caught Глеб away. | Russian Server Final Boss / Финальный босс русского сервера<br>`c_gleb_challenger` · Epic / 100 ZBS + 1 box | Have Претендент русского сервера scheduled for round 10 and earn at least 24 net regular points that round. |
| LeCrisp | Make an Impact / Произвести впечатление<br>`c_lecrisp_impact` · Uncommon / 25 ZBS | Trigger Импакт 4 times. | Eightfold Impact / Восьмикратный импакт<br>`c_lecrisp_legend` · Epic / 100 ZBS + 1 box | Trigger Импакт 8 times and win at least 3 resolved fights. |
| Толя | Count on Me / Можешь на меня подсчитать<br>`c_tolya_rammus` · Uncommon / 25 ZBS | Use Подсчет on another player. | King of Accounting / Король бухгалтерии<br>`c_tolya_accounting` · Legendary / 228 ZBS + 2 boxes | Use Подсчет on 2 distinct players and finish alive at actual place 1. |
| HardKitty | Dear Everybody / Дорогие все<br>`c_hardkitty_letters` · Rare / 50 ZBS | Record attacks from 3 distinct players in Одиночество. | Twenty Love Letters / Двадцать писем любви<br>`c_hardkitty_love` · Epic / 100 ZBS + 1 box | Record all 5 opponents and collect at least 20 total weighted letter points (`AttackHistory.Times`). |
| Sirinoks | Friend Request Accepted / Заявка в друзья принята<br>`c_sirinoks_friends` · Uncommon / 25 ZBS | Befriend 3 distinct players. | Queen of Friends and Dragons / Королева друзей и драконов<br>`c_sirinoks_dragon` · Legendary / 228 ZBS + 2 boxes | Befriend all 5 opponents and finish alive at actual place 1. |
| Злой Школьник | Schoolyard Regular / Завсегдатай школьного двора<br>`c_mitsuki_loud` · Uncommon / 25 ZBS | Record 3 distinct opponents through Запах мусора. | Everyone Gets Detention / Всем остаться после уроков<br>`c_mitsuki_garbage` · Epic / 100 ZBS + 1 box | Record all 5 opponents through Запах мусора at least twice each. |
| AWDKA | Actually Trying / Он действительно пытается<br>`c_awdka_trying` · Uncommon / 25 ZBS | Reach 2 attempts against each of 2 distinct players. | It Finally Worked / Наконец-то получилось<br>`c_awdka_mastery` · Epic / 100 ZBS + 1 box | Reach 2 attempts against all 5 opponents. |
| Darksci | Any Odds Will Do / Подойдут любые шансы<br>`c_darksci_stable` · Uncommon / 25 ZBS | Trigger Повезло with either chosen type. | Against All Odds / Вопреки всему<br>`c_darksci_unstable` · Epic / 100 ZBS + 1 box | Choose unstable, trigger Повезло, and finish alive at actual place 1. |
| Братишка (native) | Three Rows of Teeth / Три ряда зубов<br>`c_shark_teeth` · Uncommon / 25 ZBS | Win through Челюсти against 3 distinct players. | Apex Accountant / Главный по зубам и местам<br>`c_shark_apex` · Epic / 100 ZBS + 1 box | Make distinct Челюсти victims plus distinct tracked leaderboard places total at least 10. A transformed mylorik is excluded. |
| Загадочный Спартанец в маске | Shame Travels Fast / Стыд быстро разносится<br>`c_spartan_shame` · Uncommon / 25 ZBS | Mark 3 distinct players through Они позорят военное искусство. | No One Likes the Warrior / Воина не любит никто<br>`c_spartan_warrior` · Legendary / 228 ZBS + 2 boxes | Mark all 5 opponents and defeat all 5 at least once. |
| Вампур | Three-Course Meal / Обед из трёх блюд<br>`c_vampyr_bites` · Common / 10 ZBS | Maintain 3 distinct active Гематофагия bites at game end. | All-You-Can-Bite / Кусай сколько влезет<br>`c_vampyr_feast` · Epic / 100 ZBS + 1 box | Maintain 5 distinct active bites and finish alive. |
| Краборак | Shell Company / Панцирная компания<br>`c_crab_shell` · Uncommon / 25 ZBS | Welcome 3 distinct players into Панцирь. | Shell Game Champion / Чемпион панцирной игры<br>`c_crab_fortress` · Epic / 100 ZBS + 1 box | Fill Панцирь with all 5 opponents and defeat at least 3 distinct players. |
| Weedwick | A Modest Harvest / Скромный урожай<br>`c_weedwick_smoke` · Common / 10 ZBS | Harvest 5 total Weed from defeated carriers. | Industrial Agriculture / Промышленное земледелие<br>`c_weedwick_harvest` · Epic / 100 ZBS + 1 box | Harvest 20 total Weed. |
| Молодой Глеб (`Глеб` + `Main Ирелия`) | Pink Is the New Meta / Розовый — новая мета<br>`c_young_gleb_meta` · Rare / 50 ZBS | Successfully use the round-6 Pink Ward from Коммуникация. | Top Gap / Разрыв на топе<br>`c_young_gleb_ward` · Legendary / 228 ZBS + 2 boxes | Finish alive at actual place 1 with final Intelligence, Strength, Speed and Psyche each at least 7. |
| Sakura | Still in the Story / Всё ещё в сюжете<br>`c_sakura_three` · Rare / 50 ZBS · **secret** | Finish alive in the actual top 3. | Useful After All / Всё-таки полезна<br>`c_sakura_first` · Legendary / 228 ZBS + 2 boxes · **secret** | Finish alive at actual place 1 after at least 5 resolved fight wins. |
| Баг | Hotfix Deployed / Хотфикс установлен<br>`c_bug_patch` · Rare / 50 ZBS · **secret** | Finish with 2 other players having their exploit fixed. | Works on My Machine / На моей машине работает<br>`c_bug_release` · Legendary / 228 ZBS + 2 boxes · **secret** | Finish with all 5 opponents having their exploit fixed. |
| DooM Guy | Rip, Tear, Roll / Рви, кромсай, ролль<br>`c_doom_loadout` · Rare / 50 ZBS | Enter Roll Mode with active modules in all 4 stages. | BFG Division / Дивизия BFG<br>`c_doom_bfg` · Epic / 100 ZBS + 1 box | Defeat at least 3 distinct players in one BFG wave, including its primary target. |
| Эрен Йегер | Tatake! Tatake! / Татакай! Татакай!<br>`c_eren_tatake` · Uncommon / 25 ZBS | Trigger the Tatake sound twice in total. | The Rumbling / Гул Земли<br>`c_eren_rumbling` · Epic / 100 ZBS + 1 box | Kill at least 2 distinct players with Rumbling. |
| Наруто (original) | Believe in the Harem / Поверь в гарем<br>`c_naruto_harem` · Rare / 50 ZBS | Cancel 3 fights with Гарем но джутсу. Clone actions do not count. | Shadow Hokage Dividend / Дивиденды теневого Хокаге<br>`c_naruto_rasengan` · Legendary / 228 ZBS + 2 boxes | Receive at least 30 points from Теневые and finish alive at actual place 1. |

The live definitions and every paired condition are in `AchievementClass.cs` `AllAchievements`/`TrackGameEnd`; the relevant passive state types and observation hooks are catalogued character-by-character in [CHARACTERS.md](CHARACTERS.md).

## 4. Interaction achievements (13, all secret)

Locked interaction cards expose only a deliberately vague hint. The exact rules below are implementation documentation, not player-facing card copy.

| ID | Achievement (EN / RU) | Single-match interaction and recipient | Rarity / reward |
|---|---|---|---|
| `x_spartan_dragon` | Dragon Slayer / Убийца драконов | Загадочный Спартанец в маске triggers DragonSlayer against round-10 Sirinoks/Дракон and actually wins that fight. Spartan earns it. | Epic · 100 ZBS + 1 box |
| `x_kira_kratos` | Gods Don’t Tell Me What to Do / Боги мне не указ | Кратос dies to Kira's Тетрадь смерти and revives through Боги мне не указ. Kratos earns it. | Legendary · 228 ZBS + 2 boxes |
| `x_itachi_madara` | Eyes Meet Eyes / Глаза встретились | Итачи correctly locks Мадара in round 8 and receives the extra Клоны Сусано attack. Itachi earns it. | Rare · 50 ZBS |
| `x_deeplist_weedwick` | Pet Project / Любимый проект | DeepList and Weedwick both finish alive in the actual top 3. Both accounts earn it. | Epic · 100 ZBS + 1 box |
| `x_spartan_mylorik` | Mutual Respect / Взаимное уважение | Загадочный Спартанец в маске triggers the special mutual-Psyche first contact with mylorik, then defeats him in a later resolved fight. Spartan earns it. | Rare · 50 ZBS |
| `x_boys_madara` | Nothing Is Immune / Нет неприкасаемых | TheBoys with СуперМудень successfully applies Harm through Мадара's Воскрешенное тело immunity. TheBoys earns it. | Legendary · 228 ZBS + 2 boxes |
| `x_monster_witness` | I Saw the Beast / Я видел Зверя | A non-pawn attacks Монстр без имени in round 10 and receives the Пейзаж конца света payout. That attacker earns it. | Rare · 50 ZBS |
| `x_doom_dragon` | How to Tame Your Dragon / Как приручить дракона | DooM Guy defeats Sirinoks after she becomes Дракон. DooM Guy earns it; this is the same win that can unlock Приручить дракона. | Legendary · 228 ZBS + 2 boxes |
| `x_rick_most_wanted` | Interdimensional Most Wanted / Межпространственный розыск | Рик Санчез participates in resolved fights against Кира, Загадочный Спартанец в маске, and Weedwick in one match; attack or defence counts. Rick earns it. | Epic · 100 ZBS + 1 box |
| `x_spartan_kratos` | Spartans Need No Introduction / Спартанцам не нужны представления | The special first-contact Psyche interaction occurs when Загадочный Спартанец в маске meets Кратос through Они позорят военное искусство. Both accounts earn it. | Rare · 50 ZBS |
| `x_deeplist_octopus` | Eight Arms in the Plan / Восемь щупалец по плану | DeepList gets Осьминожка into Сомнительная тактика's friend list and later reaches a triggered Стёб entry against her. DeepList earns it. | Epic · 100 ZBS + 1 box |
| `x_goblin_bad_architecture` | Building Code Violation / Нарушение строительных норм | Стая Гоблинов's Ziggurat learns Булькает from Братишка (or transformed mylorik) and the Goblin player finishes alive. Goblins earn it. | Rare · 50 ZBS |
| `x_eren_goblins` | Tiny Titans / Крошечные титаны | Эрен Йегер kills Стая Гоблинов with Rumbling. Eren earns it. | Epic · 100 ZBS + 1 box |

All interaction evaluation is in `AchievementClass.cs` `TrackGameEnd`; the observation point and recipient for each relationship are indexed by subsystem in [INTERACTION-MATRIX.md](INTERACTION-MATRIX.md) §8.

## 5. Rarity rewards and catalog totals

| Rarity | Unlock reward | Catalog use |
|---|---:|---:|
| Common | 10 ZBS | 11 |
| Uncommon | 25 ZBS | 25 |
| Rare | 50 ZBS | 21 |
| Epic | 100 ZBS + 1 loot box | 33 |
| Legendary | 228 ZBS + 2 loot boxes | 15 |

The reward switch is centralized in `AchievementClass.cs` `AchievementDefinition`. Completing the current catalog awards **8,505 ZBS and 63 loot boxes** in total. `AchievementBoard` reports earned/current-catalog totals by summing live unlocked definitions; these numbers are a catalog summary, not a historical transaction ledger (`GameHub.cs` `RequestAchievements`).

## 6. Secrets, queues, and Вечное Цукуеми

- A locked secret's exact name, descriptions and character list are not sent to the client; only its hint, category, rarity, target/reward framing and a stable SHA-256-derived opaque public ID are exposed. Once unlocked, the real ID and full paired metadata are returned (`GameHub.cs` `RequestAchievements`/`MapAchievementEntry`; DTO `AchievementEntryDto`).
- `NewlyUnlocked` is an acknowledgement queue, not a match-local toast flag. Match completion does not clear it. `AcknowledgeAchievements` removes only the live IDs actually shown; both it and legacy `ClearNewAchievements` snapshot/restore the queue and reject retryably if saving fails. This selective acknowledgement prevents a concurrent unlock from being erased with an older popup (`AchievementClass.cs` `AchievementData`; `GameHub.cs` acknowledgement methods).
- The finished personalized game-state DTO carries full entries for that player's queued live IDs so the in-game UI can celebrate them; spectators and other players receive none. The finish path attaches a detached progress/queue snapshot, so a second tab acknowledging the persistent queue cannot invalidate enumeration during the final broadcast (`CheckIfReady.cs` game-end reward settlement; `AchievementClass.cs` `CreateSnapshot`; `GameStateMapper.cs` `ToDto`).
- While Вечное Цукуеми is active, evaluating real-result achievements for non-Madara accounts would reveal or contradict their private projected ending. Achievement V2 therefore evaluates only Madara's own hidden-ending achievement and returns without evaluating anything else for any player (`AchievementClass.cs` `TrackGameEnd`).

## 7. V1 migration and compatibility

V2 was introduced as an intentional fresh catalog. Its current 105 `g_…` / `c_…` / `x_…` IDs remain disjoint from the older V1 achievement IDs, so V1 unlocks do **not** grant V2 rewards or appear as V2 completions. This expansion is **not another reset**: all original 34 V2 IDs, progress rows, unlocks and issued rewards remain intact (including the original Rare rewards on Rick, Itachi and Cats), while only the 71 new IDs begin tracking after this deployment. Existing account JSON remains readable:

- `EnsureInitialized` null-fills the account containers without deleting unknown legacy progress rows (`AchievementClass.cs` `EnsureInitialized`).
- Board totals and detached entries are built only from `AllAchievements`, under the account monitor, and queued IDs are filtered to the live set (`GameHub.cs` `RequestAchievements`).
- Legacy match-tracker fields remain deserializable for old snapshots/hooks but are explicitly not evaluated by V2 (`AchievementClass.cs` `InGameAchievementTracker`).

There is no retroactive reconstruction for the 71 added cards because most requirements need per-fight/per-passive facts that history never stored. Their bests and unlocks therefore begin with matches completed after deployment; the original 34 cards continue from their existing V2 state.

## 8. Loot boxes V2

### Base odds and rewards

| Rarity | Base chance | ZBS reward (inclusive) |
|---|---:|---:|
| Common | 60% | 15–30 |
| Uncommon | 25% | 40–75 |
| Rare | 12% | 100–175 |
| Epic | 2.5% | 300–450 |
| Legendary | 0.5% | 750 |

The server owns both the table and roll; the client receives the table only for explanation. Rarity uses the shared cryptographic RNG over 10,000 equally likely outcomes with exact cumulative thresholds, and variable rewards use its inclusive bounds (`QuestClass.cs:268-274,873-912`). A box is earned by finishing alive in the reward top 2, including Sakura's first-place reward treatment; Epic/Legendary achievements and Daily Quest 3/3 mastery add boxes to the same inventory (`CheckIfReady.cs:659-662,736-743`; quest mastery `QuestClass.cs:663-668`). Existing pending inventory is preserved and uses the V2 table when opened.

### Rare+ pity

The loot-box pity counter records consecutive final Common/Uncommon results. After 9 such boxes, the 10th opening is guaranteed Rare+: a natural Rare/Epic/Legendary is preserved, while a natural Common/Uncommon is upgraded to Rare. Any Rare+ result resets the counter to 0 (`QuestClass.cs:799-801,873-881,915-922`). `GuaranteedRareIn` is server-derived and both the lobby and reveal screen visualize the remaining distance.

The displayed chances are **base odds**. On the guaranteed opening, pity changes only the below-Rare outcomes as described above; it does not reroll or downgrade a natural Rare+.

### Atomic open, acknowledgement, reconnect

- Opening is serialized on the account monitor. If a prior result has not been acknowledged, repeated `OpenLootBoxV2` calls return that same `OpeningId` and do not consume another box or grant ZBS again (`QuestClass.cs:804-852`; hub transaction `GameHub.cs:811-869`).
- A new open decrements exactly one pending box, rolls and credits the result, and stores a result snapshot containing rarity, amount, balance, remaining inventory, pity state, timestamp and `WasPityUpgrade` (`QuestClass.cs:844-901`).
- `AcknowledgeLootBox(openingId)` marks only the matching current result; stale/mismatched IDs are safe no-ops (`QuestClass.cs:855-871`).
- `RequestQuests` snapshots under the account monitor and includes `LastUnacknowledgedLootBox`, odds and pity fields alongside Daily Quest V2. After a disconnect/reload, the client resumes that already-determined reveal, with current account balance/inventory mapped over the historical result snapshot, rather than rolling again (`GameHub.cs:649-803`; `GameStateDto.cs:1035-1106`).
- V2 opens and loot/achievement acknowledgements save before confirming; write failure restores the exact affected account snapshot and returns a retryable hub error. Cached pre-V2 clients still call legacy `OpenLootBox`, which acknowledges inside the same durable transaction so they can advance without the new acknowledgement call (`GameHub.cs:811-1018,1599-1643`). Paid draft/shop operations share the account monitor too. `SaveAccount` reports atomic-replace success; game-end save failure is critical-logged with its settled in-memory state retained for the periodic retry (`UserAccounts.cs:113-139`; `UsersDataStorage.cs:28-80`; `CheckIfReady.cs:790-796`).

## 9. Web experience and accessibility

The reward experience shares the lobby with Daily Quest V2, whose full contract is [DAILY-QUESTS.md](DAILY-QUESTS.md). Achievement/Loot has three coordinated surfaces:

1. **Rewards hub in `/games`** — always-visible ZBS balance, pending-box inventory, Rare+ distance/base legendary chance, achievement completion ring, new badge and earned/current-catalog reward totals. Authentication/reconnect and lobby mount hydrate both reward families; an unacknowledged reveal is restored (`router.ts:24-28`; `game.ts:313-335`; `Lobby.vue:41-177,248-358`).
2. **Dedicated `/achievements` page** — an achievement center with overall completion/reward summaries, nearest visible completions, category/status/rarity filters, bilingual search, character portraits, progress bars, secret hints and responsive rarity treatments (`router.ts:49-57`; `Achievements.vue:1-22`; `AchievementBoard.vue:22-193,196-430`). Icons are real Lucide components selected through one controlled mapping (`AchievementIcon.vue:1-55`).
3. **Celebrations** — unlocks appear sequentially after the game-over podium, with rarity color/audio, particles, character portraits and explicit ZBS/box rewards. The focus-isolated modal supports Escape/Skip all and restores focus; final dismissal stays busy and visible until acknowledgement succeeds, with inline retry on failure (`AchievementPopup.vue:20-166,195-301`; `game.ts:788-820`). Ordinary motion gives the podium priority, while reduced-motion users bypass its five-second gate and animations (`Game.vue:609-632,1491-1497,3122-3132`). `App.vue` globally recovers the queue on non-game routes unless the loot flow currently owns modal priority (`App.vue:13-17,249-255`).

The loot-box dialog is staged: anticipation/opening first, then the already server-determined result; rarity-specific treatment, current balance/inventory/pity, odds disclosure, pity-upgrade badge and “Open another” share one modal (`LootBox.vue:35-210,225-387`). The store owns reward-modal priority: Lobby asserts it, waits one render tick for any achievement focus trap to unmount, then shows loot; handoff back also waits a tick (`Lobby.vue:41-144`). A bounded server/reveal wait becomes bilingual Retry reveal / Return to lobby UI, and a late result safely reopens; acknowledgement errors likewise remain inline and retryable (`game.ts:719-757`; `LootBox.vue:129-201,267-387`). Keyboard isolation/restoration, Escape behavior, ARIA semantics, compact mobile layout and reduced-motion fallbacks remain part of the contract. Daily Quest completion stays inline and therefore never adds another focus trap; reroll explicitly announces and focuses its replacement (`DailyQuestBoard.vue:227-303,515-604`).

## 10. Verification

Run all of the following for changes to this system:

```bash
bash tools/audit-achievements.sh
bash tools/audit-quests.sh
bash tools/verify-docs.sh --changed
dotnet build King-of-the-Garbage-Hill/King-of-the-Garbage-Hill.csproj
pnpm --dir Web/VueClient build
bash tools/simulate.sh
```

`audit-achievements.sh` verifies the exact IDs/category counts, duplicate absence, evaluator references, required bilingual/reward metadata, and a Normal/Hard pair for every live `characters.json` roster name. The build checks the SignalR DTO/TypeScript mirrors; the simulation protects the gameplay hooks used as observations.
