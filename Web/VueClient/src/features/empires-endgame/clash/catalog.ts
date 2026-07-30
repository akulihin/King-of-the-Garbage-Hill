import type {
  ClashAbilityDefinition,
  ClashPassiveDefinition,
  ClashUnitDefinition,
  EmpiresClashConfig,
} from './types'

const SOURCE_GAP = 'The pre-cutoff война source does not define a complete executable battle contract for this carrier.'
const CAMPAIGN_GAP = 'Combat text exists, but acquisition, persistence, or the campaign cohort-to-Clash mapping is not authored.'

function passive(
  id: string,
  name: string,
  description: string,
  kind: ClashPassiveDefinition['kind'],
  values: Partial<ClashPassiveDefinition> = {},
): ClashPassiveDefinition {
  return { id, name, description, kind, category: 'unit', ...values }
}

function ability(
  id: string,
  name: string,
  kind: ClashAbilityDefinition['kind'],
  values: Omit<Partial<ClashAbilityDefinition>, 'id' | 'name' | 'kind'> = {},
): ClashAbilityDefinition {
  return {
    id,
    name,
    kind,
    charges: 1,
    reloadTurns: 0,
    target: 'enemy',
    ...values,
  }
}

function unit(
  definition: Pick<ClashUnitDefinition, 'id' | 'name' | 'attack' | 'maxHp' | 'speed' | 'sourceMessageIds'>
    & Partial<Omit<ClashUnitDefinition, 'id' | 'name' | 'attack' | 'maxHp' | 'speed' | 'sourceMessageIds'>>,
): ClashUnitDefinition {
  return {
    faction: 'Нейтральный',
    regions: ['common'],
    ranks: [],
    cost: null,
    acquisitionTags: [],
    tags: [],
    passives: [],
    abilities: [],
    ...definition,
  }
}

const blockOnce = (id: string): ClashPassiveDefinition => passive(
  `${id}-block`,
  'Блок',
  'Блокирует любую атаку один раз за бой.',
  'shield',
  { category: 'shield', charges: 1, targetTag: 'ordinary-arrows' },
)

const ranged = (id: string, reloadTurns = 0): ClashPassiveDefinition => passive(
  `${id}-ranged`,
  'Лучник',
  'Добавляет свой урон к атаке в клэше, даже находясь сзади.',
  'ranged',
  { category: 'weapon', reloadTurns },
)

const legion = (id: string): ClashPassiveDefinition => passive(
  `${id}-legion`,
  'Легион!',
  'Полный ряд легионных бойцов получает +2 скорости.',
  'legion',
)

const bleed = (id: string, charges: number): ClashPassiveDefinition => passive(
  `${id}-bleed`,
  'Кровотечение',
  'Первая точная атака оставляет кровотечение.',
  'status-on-hit',
  { category: 'weapon', statusId: 'bleeding', charges },
)

const dodge = (id: string, charges: number): ClashPassiveDefinition => passive(
  `${id}-dodge`,
  'Увороты',
  'Точные атаки снимают лишь 1 ХП; любая АОЕ-атака убивает.',
  'dodge',
  { charges },
)

/**
 * The raw roster is intentionally complete even while the bundled section stays disabled.
 * A missing mechanic is carried by its own row instead of being silently approximated.
 */
export const CLASH_ROSTER: ClashUnitDefinition[] = [
  unit({
    id: 'shield-bearer', name: 'Щитарь', faction: 'Альянс', attack: 1, maxHp: 5, speed: 1,
    tags: ['shield', 'legion-candidate'], passives: [blockOnce('shield-bearer'), legion('shield-bearer')],
    sourceMessageIds: ['1359427160133341205'],
  }),
  unit({
    id: 'legionary', name: 'Легионер', faction: 'Империя', regions: ['tetrakor'],
    attack: 1, maxHp: 4, speed: 1, tags: ['shield', 'legion-candidate'],
    passives: [blockOnce('legionary'), legion('legionary')], sourceMessageIds: ['1359427160133341205'],
  }),
  unit({ id: 'anti-horse', name: 'Противоконь', faction: 'Империя', regions: ['tetrakor'], attack: 1, maxHp: 2, speed: 1, tags: ['spear', 'legion-candidate'], deferredReason: 'Its reach is sourced, but its enter-adjacency poke needs movement/cross-zone semantics.', sourceMessageIds: ['1359426528877744199', '1359427160133341205'] }),
  unit({ id: 'phalanxer', name: 'Фалангер', faction: 'Империя', regions: ['tetrakor'], cost: 'cheap', attack: 1, maxHp: 1, speed: 1, tags: ['spear', 'legion-candidate'], deferredReason: 'Attacking through three allies needs a sourced multi-depth targeting order.', sourceMessageIds: ['1359427160133341205'] }),
  unit({ id: 'halberdier', name: 'Алебардщик', cost: 'expensive', attack: 3, maxHp: 2, speed: 1, tags: ['spear', 'anti-cavalry'], passives: [passive('halberd-break', 'Алебарда', 'Первый удар уничтожает щит и наносит 1 урон.', 'shield', { category: 'weapon', targetTag: 'break-enemy', value: 1 }), passive('halberd-anti-cavalry', 'Уничтожает конницу', 'Скрытая пассивка уничтожает конницу.', 'anti-cavalry', { category: 'weapon', hidden: true })], deferredReason: 'The shield break and anti-cavalry are executable, but the entry-poke trigger requires cross-zone movement semantics.', sourceMessageIds: ['1359427160133341205'] }),
  unit({ id: 'lancer', name: 'Лансер', faction: 'Лес', regions: ['forest'], ranks: ['elite'], attack: 3, maxHp: 1, speed: 4, tags: ['spear'], deferredReason: 'The adjacency-poke trigger is not representable until cross-zone movement is decided.', sourceMessageIds: ['1359427160133341205'] }),
  unit({ id: 'mounted-anti-horse-ramrods', name: 'Противоконь на коне с железными шомполами', faction: 'Империя', regions: ['tetrakor'], attack: 3, maxHp: 2, speed: 8, tags: ['spear', 'cavalry'], deferredReason: CAMPAIGN_GAP, sourceMessageIds: ['1287827214313980057', '1359427160133341205', '1511963215616147456'] }),
  unit({ id: 'mounted-anti-horse-heavy', name: 'Противоконь на коне в тяжелой броне', faction: 'Империя', regions: ['tetrakor'], attack: 5, maxHp: 8, speed: 5, tags: ['spear', 'cavalry', 'heavy-armor'], deferredReason: 'Conflicting pre-cutoff anti-cavalry damage plus cavalry persistence are unresolved.', sourceMessageIds: ['1363872913278894080', '1363894353629548754', '1363912730788233246', '1511963215616147456'] }),
  unit({ id: 'trained-spearman', name: 'Обученный копейщик', faction: 'Альянс', cost: 'expensive', attack: 1, maxHp: 2, speed: 4, tags: ['spear'], deferredReason: 'Post-move and jump double-damage need a sourced base-armor model in Clash.', sourceMessageIds: ['1374750571000496219'] }),
  unit({ id: 'master-spearman', name: 'Мастер-копейщик', faction: 'Альянс', ranks: ['elite'], attack: 1, maxHp: 2, speed: 5, tags: ['spear'], deferredReason: 'Thrown-spear loss/retrieval and base-HP reduction are incomplete in the current engine.', sourceMessageIds: ['1374750571000496219'] }),
  unit({ id: 'guard-spearman', name: 'Страж-копейщик', faction: 'Альянс', ranks: ['elite'], attack: 1, maxHp: 4, speed: 1, tags: ['spear', 'shield'], deferredReason: 'Opposite-cell entry, shield discard, and current-armor checks need movement/armor semantics.', sourceMessageIds: ['1374750571000496219'] }),
  unit({ id: 'storm-spear', name: 'Грозовое копье', faction: 'Альянс', ranks: ['legend'], attack: 2, maxHp: 3, speed: 5, tags: ['spear'], deferredReason: 'The continuing post-move ram needs a cross-zone movement path.', sourceMessageIds: ['1374750571000496219'] }),

  unit({ id: 'warrior', name: 'Воин', cost: 'medium', attack: 2, maxHp: 3, speed: 2, tags: ['axe'], deferredReason: 'The half-damage premature counter needs an exact definition of premature attacks.', sourceMessageIds: ['1359471938258469086'] }),
  unit({ id: 'fool-long-stick', name: 'Дурачок с длинной палкой', cost: 'cheapest', attack: 1, maxHp: 1, speed: 1, tags: ['spear'], deferredReason: 'Flee timing and the city-defense replacement passive are cross-feature semantics.', sourceMessageIds: ['1359471938258469086', '1506555248074948622'] }),
  unit({ id: 'archer', name: 'Лучник', cost: 'cheap', attack: 1, maxHp: 1, speed: 3, tags: ['archer', 'bow', 'short-weapon'], passives: [ranged('archer')], sourceMessageIds: ['1359472078700413070'] }),
  unit({ id: 'slingers', name: 'Пращи', cost: 'very-cheap', attack: 1, maxHp: 1, speed: 1, tags: ['archer', 'sling'], deferredReason: 'The latest four-turn reload is exact, but 50%-current-HP damage and swamp/glacier availability need typed targeting validation.', sourceMessageIds: ['1442090262489989201'] }),
  unit({ id: 'hammerer', name: 'Молотобой', cost: 'expensive', attack: 1, maxHp: 6, speed: 1, tags: ['hammer'], deferredReason: 'Base-HP removal, armor-passive suppression, and the speed>3 miss need the deferred armor layer.', sourceMessageIds: ['1379525568227180666'] }),
  unit({ id: 'battle-hammer', name: 'Боевой молоток', ranks: ['elite'], attack: 1, maxHp: 5, speed: 2, tags: ['hammer', 'shield'], deferredReason: 'Забив recovery and plate-aware Клевок need healing/armor semantics.', sourceMessageIds: ['1389102465617625251'] }),
  unit({ id: 'heron', name: 'Цапля', ranks: ['elite'], cost: 'very-expensive', attack: 1, maxHp: 3, speed: 1, tags: ['polearm'], deferredReason: '“Попадание нельзя пережить” has no shield/immortality precedence.', sourceMessageIds: ['1442423291460452423'] }),
  unit({ id: 'ice-pick', name: 'Ледоруб', faction: 'Империя', regions: ['north'], ranks: ['legend'], attack: 1, maxHp: 4, speed: 1, tags: ['hammer', 'shield'], deferredReason: 'Забив recovery and Клевок armor precedence remain unresolved.', sourceMessageIds: ['1388056727936696330'] }),

  unit({ id: 'knight', name: 'Рыцарь', faction: 'Империя', regions: ['tetrakor'], cost: 'expensive', attack: 1, maxHp: 4, speed: 4, tags: ['cavalry'], deferredReason: 'Four strikes are sourced, but per-enemy first-parry state and post-battle horse return are not complete.', sourceMessageIds: ['1359475833948999750', '1511963215616147456'] }),
  unit({ id: 'white-stone-hammer', name: 'Молот из Белого Камня', faction: 'Империя', regions: ['tetrakor'], ranks: ['elite'], attack: 4, maxHp: 4, speed: 4, tags: ['hammer'], deferredReason: 'Armor-passive suppression and adjacent stun require the deferred armor/stun contracts.', sourceMessageIds: ['1379525568227180666'] }),
  unit({ id: 'monk', name: 'Монах', faction: 'Империя', regions: ['tetrakor'], cost: 'cheap', attack: 1, maxHp: 1, speed: 3, tags: ['shield', 'flail'], deferredReason: 'Theocracy availability, unique-unit stun, flank-only placement, and AOE flail targeting are bundled.', sourceMessageIds: ['1379541707921690707'] }),
  unit({ id: 'punisher', name: 'Каратель', faction: 'Империя', regions: ['tetrakor'], ranks: ['elite'], attack: 1, maxHp: 7, speed: 1, tags: ['shield', 'flail', 'heavy-armor'], deferredReason: 'Its sourced combat bundle depends on undefined morale magnitude and theocracy acquisition.', sourceMessageIds: ['1379541707921690707'] }),
  unit({ id: 'holy-paladin', name: 'Святой паладин', faction: 'Империя', regions: ['tetrakor'], ranks: ['elite'], attack: 1, maxHp: 7, speed: 1, tags: ['shield', 'heavy-armor'], deferredReason: CAMPAIGN_GAP, sourceMessageIds: ['1379541707921690707'] }),
  unit({ id: 'holy-cavalry', name: 'Святая конница', faction: 'Империя', regions: ['tetrakor'], acquisitionTags: ['free-church-aid'], attack: 7, maxHp: 7, speed: 7, tags: ['cavalry'], deferredReason: 'Free church-aid lifecycle and cavalry dismount/return are not represented.', sourceMessageIds: ['1379562532930326769', '1511963215616147456'] }),
  unit({ id: 'long-cross', name: 'Длинный крест', faction: 'Империя', regions: ['tetrakor'], ranks: ['elite'], attack: 2, maxHp: 6, speed: 3, tags: ['blade'], deferredReason: 'Reflection targeting and morale amount are missing; theocracy acquisition is external.', sourceMessageIds: ['1381230668423368706'] }),
  unit({ id: 'double-cross', name: 'Двойной крест', faction: 'Империя', regions: ['tetrakor'], ranks: ['elite'], attack: 3, maxHp: 6, speed: 2, tags: ['greatsword'], deferredReason: 'AOE spear stun and both morale magnitudes are missing.', sourceMessageIds: ['1381230668423368706'] }),
  unit({ id: 'champion', name: 'Чемпион', faction: 'Империя', regions: ['tetrakor'], ranks: ['limited'], limitPerGame: 4, attack: 2, maxHp: 4, speed: 4, tags: ['shield', 'heavy-armor'], deferredReason: 'Disc durability/throw state and stun semantics are not fully modeled.', sourceMessageIds: ['1381226429123137577'] }),
  unit({ id: 'nail', name: 'Гвоздь', faction: 'Империя', regions: ['tetrakor'], cost: 'expensive', attack: 1, maxHp: 2, speed: 3, tags: ['spear', 'ranged'], deferredReason: 'Arsenal, opening volley, corpse retrieval, and resurrection prevention need per-corpse inventory.', sourceMessageIds: ['1379687984907161638'] }),
  unit({ id: 'shield-bow', name: 'Щито-лук', faction: 'Империя', regions: ['forest'], cost: 'expensive', attack: 2, maxHp: 4, speed: 2, tags: ['archer', 'bow', 'shield'], passives: [ranged('shield-bow'), blockOnce('shield-bow')], sourceMessageIds: ['1381226429123137577'] }),
  unit({ id: 'medical-healer', name: 'Медицинский хиллер с легким арбалетом', faction: 'Империя', regions: ['tetrakor'], attack: 1, maxHp: 1, speed: 1, tags: ['healer', 'crossbow'], deferredReason: 'The source only tentatively says “apparently two” full-heal charges.', sourceMessageIds: ['1363851577764347955'] }),
  unit({ id: 'tenth-perst', name: 'Десятый', faction: 'Империя', regions: ['tetrakor'], ranks: ['perst'], hireOnce: true, attack: 3, maxHp: 9, speed: 9, tags: ['human'], deferredReason: 'Perst campaign lifecycle and the exact stamina pool are not authored for Clash.', sourceMessageIds: ['1421169045566263398'] }),
  unit({ id: 'abbot', name: 'Настоятель Святой Церкви', faction: 'Хаотичный', regions: ['tetrakor'], ranks: ['hero'], hireOnce: true, attack: 7, maxHp: 7, speed: 7, tags: ['boss'], reviewReason: 'End-boss sequence; do not implement until its seven-kill continuation and campaign trigger are authored.', sourceMessageIds: ['1382288986579664916'] }),
  unit({ id: 'drain-legionaries', name: 'сливные Легионеры', faction: 'Империя', regions: ['tetrakor'], attack: null, maxHp: null, speed: null, tags: ['sketch'], reviewReason: 'Equipment sketch has no unit triple or complete passives.', sourceMessageIds: ['1511409380367663114'] }),

  unit({ id: 'master-fencer', name: 'Мастер Фехтавания', faction: 'Альянс', ranks: ['elite'], attack: 1, maxHp: 1, speed: 5, tags: ['blade'], deferredReason: 'Five strikes are exact, but bypassing parries/reflections/disarm needs a shared defense-interception contract.', sourceMessageIds: ['1359477727530320015'] }),
  unit({ id: 'jugger', name: 'Джаггер', faction: 'Альянс', ranks: ['legend'], attack: 1, maxHp: 9, speed: 1, tags: ['shield', 'heavy-armor'], deferredReason: 'Damage floor, five-hit shield, and current-armor half-damage need typed armor state.', sourceMessageIds: ['1375139629233078445'] }),
  unit({ id: 'former-tetrakor-gladiator', name: 'Бывший Гладиатор Тетракора', faction: 'Альянс', regions: ['tetrakor'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: 9, speed: 1, tags: ['shield', 'heavy-armor'], deferredReason: 'Shield theft, armor retaliation, Empire hatred, and net state need a complete typed defense layer.', sourceMessageIds: ['1375139629233078445'] }),
  unit({ id: 'heretic', name: 'Еретик', faction: 'Альянс', regions: ['tetrakor'], ranks: ['hero'], hireOnce: true, attack: 3, maxHp: 7, speed: 3, tags: ['greatsword', 'heavy-armor'], deferredReason: 'Morale magnitudes, first-damage mode switch, resurrection, and acquisition are incomplete.', sourceMessageIds: ['1381230668423368706'] }),
  unit({ id: 'weapon-thrower', name: 'Метатель оружия', faction: 'Альянс', cost: 'expensive', attack: 2, maxHp: 4, speed: 3, tags: ['ranged', 'axe'], deferredReason: 'Four-cell range and northern weapon pickup need a cross-depth/campaign inventory rule.', sourceMessageIds: ['1442817717269823498'] }),
  unit({ id: 'tridenter', name: 'Трайдэнёр', faction: 'Альянс', regions: ['atlantis'], ranks: ['legend'], attack: 3, maxHp: 3, speed: 3, tags: ['spear'], deferredReason: 'Humidity degree, three-line disarm, flank-wide cavalry kill, and harpoon movement are incomplete.', sourceMessageIds: ['1365227194846154772'] }),
  unit({ id: 'scytheman', name: 'Косиньер', faction: 'Альянс', regions: ['village'], cost: 'very-cheap', attack: 1, maxHp: 1, speed: 2, tags: ['scythe'], deferredReason: 'This unit is explicitly city-defense-only and cannot enter the offensive Clash route.', sourceMessageIds: ['1385498640197488640'] }),
  unit({ id: 'reaper', name: 'Жнец', faction: 'Альянс', regions: ['village'], ranks: ['limited'], attack: 2, maxHp: 2, speed: 4, tags: ['scythe'], deferredReason: 'Morale magnitudes, Жатва depth targeting, fog swap, and settlement-count acquisition are incomplete.', sourceMessageIds: ['1385498640197488640', '1506559085473169480'] }),
  unit({ id: 'scarecrow', name: 'Пугало', faction: 'Альянс', regions: ['village'], cost: 'cheapest', attack: 2, maxHp: 2, speed: 4, tags: ['fake'], deferredReason: 'Displayed 2-2-4 conflicts with “deals no damage and dies to any hit”; fake-stat semantics require a verdict.', sourceMessageIds: ['1386189910884749343'] }),
  unit({ id: 'black-swordsman', name: 'Черный мечник', faction: 'Альянс', regions: ['black-island'], ranks: ['legend'], attack: 3, maxHp: 5, speed: 3, tags: ['blade', 'fire'], deferredReason: 'Third-burn incineration, weapon degradation, and morale amount need explicit precedence/state.', sourceMessageIds: ['1380082231610966137'] }),
  unit({ id: 'falling-star', name: 'Падающая Звезда', faction: 'Альянс', regions: ['black-island'], ranks: ['legend'], attack: 2, maxHp: 2, speed: 2, tags: ['archer', 'fire'], deferredReason: 'Permanent burning-cell lifetime and column-AOE ordering are not authored.', sourceMessageIds: ['1380090562178449428'] }),
  unit({ id: 'hell-salamander', name: 'Адская Саламандра', faction: 'Альянс', regions: ['black-island'], ranks: ['elite'], attack: 1, maxHp: 2, speed: 4, tags: ['creature'], deferredReason: 'The source passive is literally incomplete: “Выкапывается …”.', sourceMessageIds: ['1430596596646809762'] }),

  unit({ id: 'berserker', name: 'Берсеркер', faction: 'Север', regions: ['north'], ranks: ['elite'], attack: 3, maxHp: 4, speed: 3, tags: ['axe'], deferredReason: 'Zero-HP finishing, standing corpse delay, home-territory mutual kill, and wooden-shield typing need complete precedence.', sourceMessageIds: ['1359445308005683211'] }),
  unit({ id: 'cold-hand-knight', name: 'Рыцарь Хладной Руки', faction: 'Север', regions: ['north'], ranks: ['legend'], attack: 3, maxHp: 9, speed: 1, tags: ['heavy-armor'], deferredReason: 'Climate degree, three-unit freeze targeting, standing death, marauding, and complaints are bundled.', sourceMessageIds: ['1359445308005683211'] }),
  unit({ id: 'barbarian-raider', name: 'Варвар, мародер, налетчик', faction: 'Север', regions: ['north'], acquisitionTags: ['hired-and-placed-as-three'], attack: 1, maxHp: 2, speed: 4, tags: ['marauder'], deferredReason: 'Global corpse-maraud stacks, triple placement, hostile-ground speed, and campaign plunder need shared state.', sourceMessageIds: ['1363837435292942346'] }),
  unit({ id: 'zweihander-warrior', name: 'Воин - Цвайхандер', faction: 'Север', regions: ['north'], attack: 2, maxHp: 6, speed: 2, tags: ['greatsword', 'heavy-armor'], deferredReason: 'Its shield/unarmored/armored damage table needs typed current armor.', sourceMessageIds: ['1363857113805357146'] }),
  unit({ id: 'hammer-warrior', name: 'Воин - Молот', faction: 'Север', regions: ['north'], attack: 3, maxHp: 6, speed: 2, tags: ['hammer', 'heavy-armor'], deferredReason: 'Its shield/armor damage table needs typed current armor.', sourceMessageIds: ['1363857113805357146'] }),
  unit({ id: 'eternal-cold', name: 'Вечный холод', faction: 'Хаотичный', regions: ['north'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: null, speed: 1, tags: ['immortal'], deferredReason: 'Infinite HP versus иссушение, morale removal, forced travel, and heir-only command are unresolved.', sourceMessageIds: ['1375145047531720816', '1375150503733760051'] }),

  unit({ id: 'dancer', name: 'Танцор', faction: 'Лес', regions: ['forest'], cost: 'expensive', attack: 1, maxHp: 4, speed: 5, tags: ['blade', 'agile'], passives: [bleed('dancer', 1), dodge('dancer', 4)], sourceMessageIds: ['1359468214072905859'] }),
  unit({ id: 'forest-guard', name: 'Страж', faction: 'Лес', regions: ['forest'], attack: 1, maxHp: 6, speed: 4, tags: ['shield', 'axe'], deferredReason: 'Neighbor interception, half counter, spear destruction, and acquisition condition need complete targeting.', sourceMessageIds: ['1359531891417415763'] }),
  unit({ id: 'downpour', name: 'Ливень', faction: 'Лес', regions: ['forest'], cost: 'expensive', attack: 2, maxHp: 1, speed: 6, tags: ['archer', 'bow'], deferredReason: 'Back-row damage is executable, but the three-arrow back-cell active needs exact three-target geometry.', sourceMessageIds: ['1359535707462565950'] }),
  unit({ id: 'eagle', name: 'Орёл', faction: 'Нейтральный', regions: ['forest'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: 4, speed: 6, tags: ['archer', 'bow', 'agile'], deferredReason: 'Active/ambush/cavalry evasion and targeted armor<5 execution need shared active/armor precedence.', sourceMessageIds: ['1379687984907161638'] }),
  unit({ id: 'poacher', name: 'Браконьер', faction: 'Нейтральный', regions: ['forest'], ranks: ['convict'], cost: 'medium', attack: 1, maxHp: 1, speed: 3, tags: ['trap'], deferredReason: 'Corpse armor theft and post-death trap need typed corpse armor/current-HP behavior.', sourceMessageIds: ['1383062550937211001'] }),
  unit({ id: 'sadist', name: 'Садист', faction: 'Хаотичный', regions: ['forest'], ranks: ['convict'], attack: 1, maxHp: 5, speed: 5, tags: ['blade', 'agile'], deferredReason: 'Bleed-death global dodge gain, missing morale amount, mercy targeting, and acquisition are incomplete.', sourceMessageIds: ['1381896138252222515'] }),
  unit({ id: 'bear', name: 'Медведь', faction: 'Хаотичный', regions: ['forest'], ranks: ['creature'], attack: 6, maxHp: 8, speed: 4, tags: ['creature'], deferredReason: 'Double cutting/chopping/bleed damage and melee-only retaliation require the deferred damage-type layer.', sourceMessageIds: ['1383301516684689420'] }),
  unit({ id: 'cur', name: 'Шавка', faction: 'Хаотичный', regions: ['forest'], ranks: ['creature'], attack: 1, maxHp: 2, speed: 7, tags: ['creature'], deferredReason: 'Three-on-row bark stun uses undefined creature stun semantics.', sourceMessageIds: ['1383301516684689420'] }),
  unit({ id: 'vampire-lord', name: 'Вампир-Лорд', faction: 'Империя', regions: ['forest'], ranks: ['one-of-kind'], attack: 1, maxHp: 8, speed: 5, tags: ['vampire'], deferredReason: 'Epidemic acquisition, full-heal bite timing, and permanent stat growth persistence are external.', sourceMessageIds: ['1374954114001408021'] }),
  unit({ id: 'vampire', name: 'Вампир', faction: 'Империя', regions: ['forest'], attack: 1, maxHp: 4, speed: 3, tags: ['vampire'], deferredReason: 'Epidemic acquisition and global bleeding-madness observation are external.', sourceMessageIds: ['1374954114001408021'] }),
  unit({ id: 'ranger', name: 'Рейнджер', faction: 'Нейтральный', regions: ['forest'], ranks: ['limited'], attack: null, maxHp: null, speed: null, tags: ['expedition'], deferredReason: 'Expedition unit has no triple, cap, or battle passive values.', sourceMessageIds: ['1381583687946080256'] }),
  unit({ id: 'mounted-rangers', name: 'Отряд Рейнджеров на беговых конях', faction: 'Нейтральный', regions: ['forest'], attack: null, maxHp: null, speed: null, tags: ['expedition', 'cavalry'], deferredReason: 'No triple, battle passives, count, or cavalry persistence are authored.', sourceMessageIds: ['1381583687946080256'] }),

  unit({ id: 'assassin', name: 'Ассассин', faction: 'Пустыня', regions: ['desert'], ranks: ['elite'], attack: 1, maxHp: 1, speed: 8, tags: ['agile'], deferredReason: 'Humidity degree and иссушение resurrection charges need exact status/lifecycle semantics.', sourceMessageIds: ['1359445308005683211'] }),
  unit({ id: 'scorpion', name: 'Скорпион', faction: 'Пустыня', regions: ['desert'], attack: 1, maxHp: 1, speed: 6, tags: ['creature'], deferredReason: '“Only in desert / one-use” and hidden enemy-cell placement are ambiguous.', sourceMessageIds: ['1359445308005683211'] }),
  unit({ id: 'sand-snake', name: 'Песчаный змей', faction: 'Нейтральный', regions: ['desert'], cost: 'expensive', attack: 5, maxHp: 1, speed: 2, tags: ['ambush'], deferredReason: 'Replacement emergence and next-turn initiative require a sourced movement/replacement order.', sourceMessageIds: ['1359541662241783949'] }),
  unit({ id: 'desert-demon', name: 'Пустынный демон', faction: 'Хаотичный', regions: ['desert'], ranks: ['legend'], attack: 1, maxHp: 1, speed: 1, tags: ['creature'], deferredReason: 'Dryness/humidity degrees are absent, so its scaled иссушение/regeneration cannot be enabled.', sourceMessageIds: ['1359541662241783949'] }),
  unit({ id: 'many-faced-killer', name: 'Многоликий убийца', faction: 'Нейтральный', regions: ['desert'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: 4, speed: 8, tags: ['agile'], deferredReason: 'Mask selection/timing, base-armor checks, and campaign hero lifecycle are missing.', sourceMessageIds: ['1374873105218015302'] }),
  unit({ id: 'widow', name: 'Вдова', faction: 'Хаотичный', regions: ['desert'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: 5, speed: 6, tags: ['archer'], deferredReason: 'Movement-path slowdown, hostage/meat-shield state, betrayal, and hero lifecycle are incomplete.', sourceMessageIds: ['1381491191610081332'] }),
  unit({ id: 'death-chaldean', name: 'Халдей смерти', faction: 'Нейтральный', regions: ['desert'], cost: 'cheap', attack: 1, maxHp: 1, speed: 2, tags: ['fake-corpse'], deferredReason: 'The 50/50 fake corpse is deterministic-RNG ready, but enemy-step timing/corpse exclusions need cross-zone movement.', sourceMessageIds: ['1379693248704151643'] }),
  unit({ id: 'gek-tamer', name: 'Укротитель Геков', faction: 'Нейтральный', regions: ['desert'], ranks: ['hero'], hireOnce: true, attack: 1, maxHp: 6, speed: 2, tags: ['gek'], deferredReason: 'Gek reserve count, corpse spawns, provision cost, neutral conversion, and hero lifecycle are incomplete.', sourceMessageIds: ['1365283831325921351'] }),
  unit({ id: 'gek-corpse-eater', name: 'Гек-трупоед', faction: 'Нейтральный', regions: ['desert'], attack: 1, maxHp: 1, speed: 4, tags: ['gek', 'creature'], deferredReason: 'Corpse-cell spawn and corpse consumption order are unresolved.', sourceMessageIds: ['1365283831325921351'] }),
  unit({ id: 'loud-gek', name: 'Громогласный Гек', faction: 'Нейтральный', regions: ['desert'], attack: 2, maxHp: 1, speed: 4, tags: ['gek', 'creature'], deferredReason: 'One-turn stun has no universal action contract.', sourceMessageIds: ['1365283831325921351'] }),
  unit({ id: 'nimble-gek', name: 'Проворный Гек', faction: 'Нейтральный', regions: ['desert'], attack: 1, maxHp: 3, speed: 7, tags: ['gek', 'creature', 'agile'], passives: [dodge('nimble-gek', 3)], sourceMessageIds: ['1365283831325921351'] }),
  unit({ id: 'hexagek', name: 'ГексаГек', faction: 'Нейтральный', regions: ['desert'], attack: 6, maxHp: 6, speed: 6, tags: ['gek', 'creature'], deferredReason: 'Full regeneration exactly three turns after first damage needs a persistent damage trigger.', sourceMessageIds: ['1365283831325921351'] }),
  unit({ id: 'gek-crowd', name: 'Толпа маленьких геков', faction: 'Нейтральный', regions: ['desert'], attack: 1, maxHp: 1, speed: 3, tags: ['gek', 'creature'], deferredReason: 'Replacement stock size is not authored.', sourceMessageIds: ['1366117710311981238'] }),
  unit({ id: 'cobra', name: 'Кобра', faction: 'Нейтральный', regions: ['desert'], attack: null, maxHp: null, speed: null, tags: ['creature'], deferredReason: 'No triple or cost exists in the pre-cutoff source; only the ambush analogy and next-turn poison death are defined.', sourceMessageIds: ['1363942979093532672', '1381503271969095740', '1386232774247055501'] }),

  unit({ id: 'utilizer', name: 'Утилизатор', faction: 'Болото', regions: ['swamp'], ranks: ['elite'], attack: 3, maxHp: 2, speed: 3, tags: ['alchemist'], deferredReason: 'Enemy speed aura, delayed adjacent fire-water damage, death poison AOE, and terrain immunity need full ordering.', sourceMessageIds: ['1359464928330776758'] }),
  unit({ id: 'healer', name: 'Врачиватель', faction: 'Болото', regions: ['swamp'], cost: 'cheap', attack: 1, maxHp: 2, speed: 1, tags: ['healer'], deferredReason: 'Heal-or-cleanse choice timing and toxin/poison immunity duration are not specified.', sourceMessageIds: ['1359464928330776758'] }),
  unit({ id: 'regenerator', name: 'Регенератор', faction: 'Болото', regions: ['swamp'], cost: 'very-expensive', attack: 1, maxHp: 6, speed: 1, tags: ['healer'], deferredReason: 'Quest acquisition, heal/cleanse ordering, and neurotoxin rage semantics are incomplete.', sourceMessageIds: ['1359464928330776758'] }),
  unit({ id: 'cultist', name: 'Культист', faction: 'Болото', regions: ['swamp'], attack: 1, maxHp: 2, speed: 1, tags: ['cult'], deferredReason: 'The row damage-sharing examples do not define a coherent general remainder algorithm.', sourceMessageIds: ['1359543786719543577'] }),
  unit({ id: 'hirudian', name: 'Хирудианец', faction: 'Хаотичный', regions: ['swamp'], ranks: ['convict'], attack: 1, maxHp: 6, speed: 2, tags: ['cult'], deferredReason: '“All bonuses intensify” magnitude, one-HP mutation target, and campaign gate are incomplete.', sourceMessageIds: ['1381910874721095832'] }),
  unit({ id: 'hirudian-cult-father', name: 'Отец культа Хирудиана', faction: 'Хаотичный', regions: ['swamp'], ranks: ['convict'], attack: 1, maxHp: 4, speed: 3, tags: ['cult'], deferredReason: 'Global bleeding madness, cult-count HP, and campaign recruitment need shared lifecycle state.', sourceMessageIds: ['1381910874721095832'] }),
  unit({ id: 'zero', name: 'Нулевой', faction: 'Хаотичный', regions: ['swamp'], ranks: ['convict', 'hero'], hireOnce: true, attack: 1, maxHp: 8, speed: 5, tags: ['cult'], deferredReason: 'Хируграммы, full-heal observation, unique-target typing, and hero/campaign lifecycle are incomplete.', sourceMessageIds: ['1381910874721095832'] }),
  unit({ id: 'hirud-child', name: 'Дитя Хируда', faction: 'Хаотичный', regions: ['swamp'], ranks: ['convict'], attack: 1, maxHp: 1, speed: 1, tags: ['cult'], deferredReason: 'Carrier death, parasite transfer target/timing, and host replacement are unresolved.', sourceMessageIds: ['1382246650453692427'] }),
  unit({ id: 'bracovermin', name: 'Браковермин', faction: 'Нейтральный', regions: ['swamp'], ranks: ['convict'], cost: 'expensive', attack: 2, maxHp: 1, speed: 4, tags: ['hunter'], deferredReason: 'Corpse armor theft and creature damage reflection require typed armor/corpse state.', sourceMessageIds: ['1383062550937211001'] }),
  unit({ id: 'stalker', name: 'Сталкер', faction: 'Нейтральный', regions: ['swamp', 'desert'], cost: 'very-expensive', attack: 1, maxHp: 2, speed: 4, tags: ['guide'], deferredReason: 'Its battle effect disables all traps, but expedition mortality reduction has no magnitude.', sourceMessageIds: ['1383062550937211001'] }),
  unit({ id: 'uncle-vova', name: 'Дядя Вова', faction: 'Нейтральный', regions: ['swamp'], ranks: ['incredible'], attack: 2, maxHp: 2, speed: 3, tags: ['agile'], deferredReason: 'Alcohol inventory, “someone to talk to,” creature immunity, and quest acquisition are incomplete.', sourceMessageIds: ['1384398300190605344'] }),
  unit({ id: 'moonshine-grandmother', name: 'Бабка Самогонщица', faction: 'Нейтральный', regions: ['swamp'], cost: 'very-cheap', attack: 0, maxHp: 1, speed: 1, tags: ['brewer'], deferredReason: '“Варит самогон” has no battle action, quantity, timing, or campaign inventory contract.', sourceMessageIds: ['1384457557896069210'] }),
  unit({ id: 'mechanical-crossbow-08', name: 'Мехакинетический Самострел 0.8', faction: 'Империя', regions: ['swamp', 'tetrakor'], cost: 'expensive', attack: 2, maxHp: 3, speed: 2, tags: ['archer', 'crossbow'], passives: [ranged('mechanical-crossbow-08', 2)], sourceMessageIds: ['1442090262489989201'] }),
  unit({ id: 'mechanical-bolt-thrower-10', name: 'Мехакинетический Стреломёт 1.0', faction: 'Империя', regions: ['swamp', 'tetrakor'], ranks: ['elite'], attack: 2, maxHp: 2, speed: 5, tags: ['archer', 'crossbow', 'automatic'], passives: [ranged('mechanical-bolt-thrower-10')], sourceMessageIds: ['1442090262489989201'] }),
  unit({ id: 'mechanical-crossbow-20', name: 'Мехакинетический Самострел 2.0', faction: 'Империя', regions: ['swamp', 'tetrakor'], ranks: ['elite'], cost: 'very-expensive', attack: 4, maxHp: 2, speed: 1, tags: ['archer', 'crossbow'], deferredReason: 'Column penetration, non-decrement on kills, shield bypass, and once/game sniper targeting need exact ordering.', sourceMessageIds: ['1442090262489989201'] }),
  unit({ id: 'fire-snow', name: 'Огненный снег', faction: 'Империя', regions: ['swamp', 'tetrakor'], cost: 'expensive', attack: 1, maxHp: 1, speed: 4, tags: ['archer', 'fire'], deferredReason: 'Explosion-adjacent targeting needs exact cell geometry.', sourceMessageIds: ['1442090262489989201'] }),
  unit({ id: 'priest-buffer', name: 'Жрец - Баффер', faction: 'Болото', regions: ['swamp'], attack: 0, maxHp: 1, speed: 1, tags: ['support'], deferredReason: 'Four diagonal allies need an exact side-relative diagonal definition at edges.', sourceMessageIds: ['1368926622119362580'] }),

  unit({ id: 'executioner', name: 'Палач', faction: 'Нейтральный', regions: ['forest'], ranks: ['hero'], hireOnce: true, attack: 2, maxHp: 8, speed: 4, tags: ['shield', 'axe'], deferredReason: 'Quest recruitment plus post-shield mode, limb sever, AOE, execution, and morale amount need complete shared contracts.', sourceMessageIds: ['1380078579861422110'] }),
  unit({ id: 'he', name: 'Он', faction: 'Хаотичный', regions: ['common'], attack: 6, maxHp: 6, speed: 6, tags: ['boss'], reviewReason: 'End-boss sequence with resurrection, global attacks, corpse army, and campaign persistence.', sourceMessageIds: ['1381910685776089160'] }),
  unit({ id: 'white-stone-golem', name: 'голем из белого камня', faction: 'Хаотичный', regions: ['tetrakor'], attack: null, maxHp: null, speed: null, tags: ['boss'], reviewReason: 'End-boss sketch has no triple or complete encounter/settlement.', sourceMessageIds: ['1485189779388891236'] }),
  unit({ id: 'slave-owner', name: 'Рабовладелец', faction: 'Хаотичный', regions: ['common'], attack: null, maxHp: null, speed: null, tags: ['boss'], reviewReason: 'Slave lifecycle, abolition cleanup, stats, targeting, and rewards are incomplete.', sourceMessageIds: ['1392420995775598785'] }),
  unit({ id: 'organ', name: 'ОргАн', faction: 'Хаотичный', regions: ['common'], attack: null, maxHp: null, speed: null, tags: ['boss'], reviewReason: 'Named boss carrier has no complete pre-cutoff battle contract.', sourceMessageIds: ['1369418274797781012'] }),
  unit({ id: 'shield-archer-sketch', name: 'щито-лучники / метатели дисков', faction: 'Нейтральный', regions: ['common'], attack: null, maxHp: null, speed: null, tags: ['sketch'], reviewReason: 'Sketch lacks stable unit identity, triple, acquisition, and complete mechanics.', sourceMessageIds: ['1442819486754410629'] }),
  unit({ id: 'alliance-charger-sketch', name: 'альянсовый «заряжающий» юнит', faction: 'Альянс', regions: ['common'], attack: null, maxHp: null, speed: null, tags: ['sketch'], reviewReason: 'Only the interruption premise exists; no triple, charge timing, target, or consequence.', sourceMessageIds: ['1386585709833617468'] }),
  unit({ id: 'eastern-guard-sketch', name: 'восточный охранник', faction: 'Нейтральный', regions: ['swamp'], attack: null, maxHp: null, speed: null, tags: ['sketch'], reviewReason: 'Visual/equipment sketch lacks triple, acquisition, and executable values.', sourceMessageIds: ['1481555236354129930'] }),
  unit({ id: 'companion', name: 'Соратник', faction: 'Нейтральный', regions: ['common'], attack: null, maxHp: null, speed: null, tags: ['cavalry-support'], deferredReason: 'No triple/acquisition; its once-per-turn follow-up depends on the conditional cavalry collision path.', sourceMessageIds: ['1511972539377647649'] }),
]

export const CLASH_SCAFFOLD: EmpiresClashConfig = {
  enabled: false,
  resultLogLimit: 32,
  maxTurns: 160,
  maxCommands: 256,
  defaultFieldVariantId: 'settlement-3x4',
  placementFirstSide: 'attacker',
  betweenClashesFirstSide: 'attacker',
  speedTieRule: 'defender-first',
  turnCapTieWinner: 'defender',
  victoryRule: 'elimination',
  corpseBlocksAdvance: false,
  onePlacementPerSideBetweenClashes: true,
  fieldVariants: [
    { id: 'tetrakor-streets-4x4', name: 'Улицы Тетракора', columns: 4, rowsPerSide: 4, reinforcementRows: 1, unitCountMultiplier: 1, terrainCellIds: [] },
    { id: 'narrow-3x5', name: 'Узкое место', columns: 3, rowsPerSide: 5, reinforcementRows: 1, unitCountMultiplier: 1, terrainCellIds: [] },
    { id: 'wide-5x5', name: 'Широкое поле', columns: 5, rowsPerSide: 5, reinforcementRows: 1, unitCountMultiplier: 1, terrainCellIds: [] },
    { id: 'settlement-3x4', name: 'Улицы поселения', columns: 3, rowsPerSide: 4, reinforcementRows: 1, unitCountMultiplier: 1, terrainCellIds: [] },
    { id: 'skirmish-3x3', name: 'Малое поле', columns: 3, rowsPerSide: 3, reinforcementRows: 1, unitCountMultiplier: 1, terrainCellIds: [], deferredReason: 'The “about 13 units” note does not define distribution or exact capacity.' },
    { id: 'slaughter-10x5', name: 'Бойня', columns: 10, rowsPerSide: 5, reinforcementRows: 1, unitCountMultiplier: 20, terrainCellIds: [], deferredReason: 'The ×20 отряд note does not define whether it changes counts, casualties, or stats.' },
  ],
  statuses: [
    { id: 'bleeding', name: 'кровотечение', kind: 'bleeding', damagePerTurn: 1, durationTurns: null, stacks: true },
    { id: 'heavy-bleeding', name: 'обильное кровотечение', kind: 'bleeding', damagePerTurn: 2, durationTurns: null, stacks: true },
    { id: 'ignite', name: 'поджог', kind: 'ignite', damagePerTurn: 1, durationTurns: 1, stacks: false, clearsShields: true },
    { id: 'scorpion-poison', name: 'Яд Скорпиона', kind: 'scorpion-poison', durationTurns: null, stacks: false, thresholdHpExclusive: 5 },
    { id: 'cobra-poison', name: 'Яд Кобры', kind: 'cobra-poison', durationTurns: 1, stacks: false, delayedDeathTurns: 1 },
    { id: 'centipede-poison', name: 'Яд Сколопендры', kind: 'centipede-poison', durationTurns: null, stacks: false, attackDivisor: 2, speedDivisor: 2 },
    { id: 'karakurt-poison', name: 'Яд Харакурта', kind: 'karakurt-poison', durationTurns: 3, stacks: false, delayedDeathTurns: 3 },
    { id: 'corpse-centipede-poison', name: 'Яд Трупной Сколопендры', kind: 'corpse-centipede-poison', durationTurns: null, stacks: false },
    { id: 'lhp-toxin', name: 'Токсин L.H.P.', kind: 'lhp-toxin', durationTurns: 1, stacks: false, bypassesShields: true },
    { id: 'neuro-toxin', name: 'Нейро-Токсин', kind: 'neuro-toxin', durationTurns: 2, stacks: false, wakesOnDamage: true, clearsRage: true },
    { id: 'wither', name: 'иссушение', kind: 'wither', durationTurns: 1, stacks: false, bypassesShields: true, deferredReason: 'The source says “bonus damage” but gives no numeric bonus.' },
    { id: 'freeze', name: 'заморозка', kind: 'freeze', durationTurns: 2, stacks: false, deferredReason: 'Climate-scaled duration lacks a numeric climate scale.' },
    { id: 'stun', name: 'стан/оглушение', kind: 'stun', durationTurns: 1, stacks: false, deferredReason: 'The source does not define one universal action/activation consequence.' },
    { id: 'dodge', name: 'увороты', kind: 'dodge', durationTurns: null, stacks: true },
    { id: 'paralysis', name: 'паралич', kind: 'paralysis', durationTurns: null, stacks: false },
    { id: 'disarm', name: 'Обезоруживание', kind: 'disarm', durationTurns: null, stacks: false },
    { id: 'rage', name: 'ярость/безумие', kind: 'rage', durationTurns: null, stacks: true, deferredReason: 'Base rage/madness accumulation and bonuses are not globally authored.' },
  ],
  terrain: [
    { id: 'high-ground', name: 'Хайграунд', kind: 'high-ground', speedDelta: 1, archerCapacity: 2, duplicateActivations: true },
    { id: 'swamp-mushrooms', name: 'болотные грибы', kind: 'healing-mushrooms', durationTurns: 3, healingPerTurn: 0, deferredReason: 'Three turns are sourced; healing amount is not.' },
    { id: 'acid', name: 'кислота', kind: 'acid', maxHpMultiplier: 0.5, deferredReason: 'Leaving/restoration and current-HP behavior are not authored.' },
    { id: 'cordyceps', name: 'гриб-кордицепс', kind: 'cordyceps', deferredReason: 'Ally-target selection and timing are not authored.' },
    { id: 'trap', name: 'ловушки/капканы', kind: 'trap', damage: 0, deferredReason: 'Trap catalog, count, trigger, and values are absent.' },
    { id: 'fog', name: 'Туман', kind: 'fog', hidesEnemyCell: true, deferredReason: 'Random placement/count and Жнец↔Пугало swap command/privacy are incomplete.' },
  ],
  regions: [
    { id: 'swamp', name: 'Болото', speedDelta: -1, supplyMultiplier: 1, temperature: 'warm', humidity: 'humid', imperialCountBonus: 0, heatingRequired: false },
    { id: 'desert', name: 'Пустыня', speedDelta: 0, supplyMultiplier: 2, temperature: 'hot', humidity: 'dry', imperialCountBonus: 0, heatingRequired: false },
    { id: 'north', name: 'Север', speedDelta: 0, supplyMultiplier: 1, temperature: 'cold', humidity: 'neutral', imperialCountBonus: 0, heatingRequired: true },
    { id: 'forest', name: 'Лес', speedDelta: 0, supplyMultiplier: 0.5, temperature: 'warm', humidity: 'neutral', imperialCountBonus: 0, heatingRequired: false },
    { id: 'tetrakor', name: 'Тетракор', speedDelta: 0, supplyMultiplier: 0, temperature: 'warm', humidity: 'neutral', imperialCountBonus: 1, heatingRequired: false },
    { id: 'common', name: 'Обычное поле', speedDelta: 0, supplyMultiplier: 1, temperature: 'temperate', humidity: 'neutral', imperialCountBonus: 0, heatingRequired: false },
  ],
  morale: {
    positiveThresholdExclusive: 0,
    negativeThresholdExclusive: 0,
    positiveActivationCharges: 2,
    neutralActivationCharges: 1,
    negativeActivationCooldownTurns: 2,
    minimum: -9,
    maximum: 9,
  },
  settlement: {
    victoryMoraleDelta: 1,
    defeatMoraleDelta: -1,
    abortMoraleDelta: -2,
    abortAllianceThreatDelta: 2,
    recruitmentPenaltyPerLoss: 1,
    growthPenaltyPerLoss: 1,
  },
  assaultRoutes: [
    {
      id: 'campaign-central-assault',
      sourceKind: 'campaign',
      sourceId: 'central-fort-assault',
      battleMode: 'td',
      tdVariantId: 'central-fort-assault',
      clashVariantId: 'tetrakor-streets-4x4',
      deferredReason: 'Named campaign cohorts are not mapped to the война roster, so the existing TD route remains active.',
    },
    {
      id: 'expedition-south-fortress-assault',
      sourceKind: 'expedition',
      sourceId: 'expedition-south-fortress',
      battleMode: 'td',
      tdVariantId: 'desert-fort-expedition-assault',
      clashVariantId: 'narrow-3x5',
      deferredReason: 'P11A roster instances have generic campaign unit IDs and no authored named-Clash mapping.',
    },
  ],
  roster: CLASH_ROSTER,
  deferredSubfeatures: [
    { id: 'campaign-roster-mapping', reason: 'Generic campaign cohorts are not mapped to the named война roster.' },
    { id: 'campaign-field-selection', reason: 'No author/attacker/terrain selection rule chooses a field variant.' },
    { id: 'campaign-morale-seed', reason: 'No scale maps campaign morale into side morale.' },
    { id: 'high-ground-archer-stacking', reason: 'Two archers per high-ground cell are sourced, but co-occupant clash, casualty, and advance ordering are not authored.' },
    { id: 'corpse-passability', reason: 'Default blocking duration, marauding, and removal order are incomplete.' },
    { id: 'cavalry', reason: 'Charge collision is sourced, but it conflicts with the older ram drain and lacks campaign return integration.' },
    { id: 'captains', reason: 'Constructor slot 6 «Специализация» is blank.' },
    { id: 'armor-typing', reason: 'The pre-cutoff note explicitly defers per-unit armor/weapon typing.' },
    { id: 'post-battle-healing', reason: 'Healing rate and exact ≤50% next-battle stat semantics are incomplete.' },
    { id: 'pvp', reason: 'Opponent ownership, secrecy, disconnect, and settlement are not authored.' },
    { id: 'mutations', reason: 'Cross-feature P5-adjacent review; no Clash implementation in this phase.' },
    { id: 'mercenaries-villains', reason: '«Мерзавцы» camp/AI/dialogue/acquisition remain review-only.' },
    { id: 'military-discipline', reason: 'Draw-luck meta has no progression curve or card ownership.' },
    { id: 'fools-city-defense', reason: 'Cross-feature random TD/Clash fill has no count, side, or passive override order.' },
    { id: 'boss-sequences', reason: '«Он»/Настоятель/Голем/Рабовладелец/ОргАн are review-only.' },
    { id: 'absolute-sword', reason: 'Cross-region design/forge/equip chain has no campaign action contract.' },
    { id: 'weapon-ideas', reason: 'Named weapon sketches are ledger items, not unit definitions.' },
  ],
}

export const CLASH_CORE_LIVE_UNIT_IDS = [
  'shield-bearer',
  'legionary',
  'archer',
  'shield-bow',
  'dancer',
  'nimble-gek',
  'mechanical-crossbow-08',
  'mechanical-bolt-thrower-10',
] as const

export const CLASH_REVIEW_SOURCE_GAP = SOURCE_GAP

export const CLASH_QA_ABILITIES = {
  ignite: ability('qa-ignite', 'Поджог', 'status', { statusId: 'ignite', target: 'enemy' }),
  morale: ability('qa-morale', 'Боевой дух', 'morale', { value: 1, target: 'self' }),
}
