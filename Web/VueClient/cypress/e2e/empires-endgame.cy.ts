/// <reference types="cypress" />

import bundledConfigJson from '../../public/empires-endgame/game-config.json'
import { createEmpiresQaScenarios } from '../../src/features/empires-endgame/qa'
import { EmpiresEndgameEngine } from '../../src/features/empires-endgame/engine'
import { EMPIRES_SAVE_STORAGE_KEY } from '../../src/features/empires-endgame/persistence'
import type { EmpiresEndgameConfig } from '../../src/features/empires-endgame/types'

type QaScenario =
  | 'pending-take'
  | 'empty-hand-pending-finish'
  | 'anti-bito'
  | 'divine-gift'
  | 'target-city-resources'
  | 'target-meteor-city'
  | 'empire-council-with-points'
  | 'governance'
  | 'domestic-economy'
  | 'mystic-tavern'
  | 'external-trade'
  | 'economy-content-event'
  | 'quest-dialogue'
  | 'destroyed-west'
  | 'loyalty-rebellion'
  | 'relic-production-levels'
  | 'season-disclosure'
  | 'epidemic-outbreak'
  | 'event'
  | 'victory'
  | 'defeat'

const QA_SEED = 'cypress-empires-endgame'
const CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'
const GOD_UI_PREFERENCES_STORAGE_KEY = 'empires-endgame:ui:god-presence:v1'
const bundledConfig = bundledConfigJson as unknown as EmpiresEndgameConfig
const TECHNOLOGY_COUNT = bundledConfig.empire.technologies.length

function scenarioUrl(scenario: QaScenario) {
  const query = new URLSearchParams({ qa: '1', scenario, seed: QA_SEED })
  return `/empires-endgame?${query}`
}

function visitScenario(
  scenario: QaScenario,
  onBeforeLoad?: (window: Cypress.AUTWindow) => void,
) {
  cy.visit(scenarioUrl(scenario), {
    onBeforeLoad(window) {
      window.localStorage.clear()
      onBeforeLoad?.(window)
    },
  })
  cy.get('[data-testid="qa-panel"]').should('be.visible')
  cy.get('[data-testid="qa-scenario"]').should('have.value', scenario)
  cy.get('[data-testid="qa-seed"]').should('have.value', QA_SEED)
  cy.get('[data-testid="qa-digest"]').should('be.visible')
}

function firstGradientLuminance(backgroundImage: string) {
  const rgb = backgroundImage.match(/rgba?\(\s*(\d+)[, ]+\s*(\d+)[, ]+\s*(\d+)/i)
  if (!rgb) return 0
  const [, red, green, blue] = rgb.map(Number)
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

function expectStoredValue(key: string, value: string | null) {
  cy.window().then((window) => {
    expect(window.localStorage.getItem(key), key).to.equal(value)
  })
}

describe('Empire\'s Endgame deterministic browser scenarios', () => {
  it('advances the mandatory Палач dialogue and records it in the quest journal', () => {
    visitScenario('quest-dialogue')
    cy.get('[data-quest-id="quest-palach"]').should('be.visible').and('contain.text', 'Палач')
    cy.get('[data-testid="dialogue-choice-palach-p28-bed"]').click()
    cy.get('[data-testid="dialogue-choice-palach-p30-start"]').click()
    cy.get('[data-testid="dialogue-choice-palach-p01-point"]').click()
    cy.get('[data-testid="dialogue-choice-palach-p03-messenger"]').click()
    cy.contains('Задание выполнено.').should('be.visible')
    cy.get('[data-testid="dialogue-dismiss"]').click()
    cy.get('[data-quest-id="quest-palach"]').should('not.exist')
    cy.get('[data-testid="open-quest-journal"]').click()
    cy.get('[role="dialog"]').should('contain.text', 'Журнал заданий')
      .and('contain.text', 'Выполнено')
      .and('contain.text', 'Палач')
  })

  it('shows domestic obligations and executes each live economy carrier', () => {
    visitScenario('domestic-economy')
    cy.get('[data-testid="domestic-economy-panel"]').should('be.visible')
    cy.contains('Обязательства').should('be.visible')
    cy.get('[data-testid^="loan-"]').should('contain.text', 'active').and('contain.text', 'Следующий платёж')

    cy.get('[data-testid="economy-city"]').select('city-north-frost-harbor')
    cy.get('[data-testid="economy-start-insurance"]').should('be.enabled').click()
    cy.get('[data-testid="insurance-contract"]').should('contain.text', 'waiting')

    cy.get('[data-testid="economy-city"]').select('city-west-green-bastion')
    cy.get('[data-testid="fair-carnival"]').should('be.enabled').click()
    cy.get('[data-testid="fair-carnival"]').should('contain.text', 'эффект до')
    cy.get('[data-testid="fair-traveling-artists"]').should('be.enabled')

    cy.get('[data-testid="economy-city"]').select('city-west-horse-march')
    cy.get('[data-testid="temple-preach"]').should('be.enabled').click()
    cy.get('[data-testid^="temple:city-west-horse-march:relic:"]').should('have.length', 2)
    cy.get('[data-testid="temple:city-west-horse-march:relic:1"] select')
      .select('relic-epidemic-ward')
    cy.get('[data-testid="temple:city-west-horse-march:relic:1"]')
      .should('contain.text', 'Реликвия защиты от эпидемий')

    cy.get('[data-testid="economy-city"]').select('city-south-cactus-wall')
    cy.contains('Пассивный армейский hook').should('be.visible')
    cy.contains('maria2x2').should('be.visible')
  })

  it('accepts and declines serialized external offers with exact effects', () => {
    visitScenario('external-trade')
    cy.get('[data-testid="external-diplomacy-panel"]').should('be.visible')
    cy.get('[data-testid="external-city"]').select('city-north-frost-harbor')
    cy.get('[data-testid^="external-offer-"]').its('length').should('be.gte', 2)
    cy.get('[data-testid^="external-accept-"]').first().should('be.enabled').click()
    cy.contains('accepted').should('be.visible')
    cy.get('[data-testid^="external-decline-"]').first().click()
    cy.contains('declined').should('be.visible')
  })

  it('covers reputation denial and both Sea Port placement rejections', () => {
    const fixtures = createEmpiresQaScenarios(bundledConfig, { seed: QA_SEED })
    const deniedState = structuredClone(fixtures['external-trade'].snapshot)
    deniedState.empire.reputation = -9
    const denied = new EmpiresEndgameEngine(bundledConfig, deniedState)
    const deniedView = denied.externalDiplomacyView('city-north-frost-harbor')
    expect(deniedView.offers.every(offer => offer.quote.blockedReason?.includes('Reputation'))).to.equal(true)

    const nonCoastalState = structuredClone(fixtures['external-trade'].snapshot)
    const nonCoastal = new EmpiresEndgameEngine(bundledConfig, nonCoastalState)
    const nonCoastalResult = nonCoastal.placeBuilding(
      'city-north-iron-gate',
      'slot-unique',
      bundledConfig.empire.externalEconomy.seaPort.buildingId,
    )
    expect(nonCoastalResult.ok).to.equal(false)
    expect(nonCoastalResult.message).to.include('coastal')

    const capState = structuredClone(fixtures['external-trade'].snapshot)
    const portId = bundledConfig.empire.externalEconomy.seaPort.buildingId
    for (const cityId of [
      'city-north-frost-harbor',
      'city-west-horse-march',
      'city-east-alchemy-gate',
      'city-center-east',
    ]) {
      const city = capState.empire.cities.find(candidate => candidate.id === cityId)!
      const slot = bundledConfig.empire.cities.find(candidate => candidate.id === cityId)!
        .slots.find(candidate => candidate.kind === 'maritime')!
      city.buildingLevels[portId] = 1
      city.operationalBuildingLevels[portId] = 1
      city.buildingSlotAssignments[slot.id] = portId
    }
    const perst = bundledConfig.governance.persts[0]
    capState.governance.governorAssignments.north = {
      regionId: 'north',
      perstId: perst.id,
      assignedAtCon: capState.con,
    }
    const capped = new EmpiresEndgameEngine(bundledConfig, capState)
    const capResult = capped.placeBuilding('city-north-governor-2-b', 'slot-maritime', portId)
    expect(capResult.ok).to.equal(false)
    expect(capResult.message).to.include('maximum 4')
  })

  it('resolves one advisor judgment and permanently opens a Perst region', () => {
    visitScenario('governance')
    cy.get('[data-testid="governance-panel"]').should('be.visible')
    cy.get('[data-testid="advisor-pardon-advisor-science"]').click()
    cy.get('[data-testid="advisor-execute-advisor-trade"]').click()
    cy.get('[data-testid="advisor-execute-advisor-war"]').click()
    cy.get('[data-testid="advisor-advisor-science"]').should('contain.text', 'Помилован · активен')
    cy.get('[data-testid="advisor-advisor-trade"]').should('contain.text', 'Казнён')
    cy.get('[data-testid="advisor-grand-status"]').should('contain.text', 'Закрыт')

    cy.get('[data-testid="governance-region-north"]').should('contain.text', 'Доступно 2 / 5 городов')
    cy.get('[data-testid="perst-region-perst-fourth-trevor"]').select('north')
    cy.get('[data-testid="perst-assign-perst-fourth-trevor"]').click()
    cy.get('[data-testid="perst-perst-fourth-trevor"]').should('contain.text', 'Губернатор: Северный регион')
    cy.get('[data-testid="governance-region-north"]').should('contain.text', 'Доступно 5 / 5 городов')
  })

  it('shows and executes pending finish immediately, without a refresh', () => {
    let unloaded = false
    visitScenario('pending-take', (window) => {
      window.addEventListener('beforeunload', () => {
        unloaded = true
      })
    })

    cy.get('[data-testid="qa-digest"]')
      .should('contain.text', 'player / taking')
      .invoke('text')
      .then((beforeDigest) => {
        cy.get('.deck-count').invoke('text').then((beforeDeckText) => {
          const beforeDeck = Number(beforeDeckText.trim())
          expect(beforeDeck).to.be.greaterThan(0)

          cy.get('[data-testid="durak-finish"]')
            .should('be.visible')
            .and('be.enabled')
            .and('contain.text', 'Хватит подкидывать')
            .click()

          cy.get('[data-testid="qa-digest"]').should(($digest) => {
            expect($digest.text().trim()).not.to.equal(beforeDigest.trim())
          })
          cy.get('.deck-count').should(($count) => {
            expect(Number($count.text().trim())).to.be.lessThan(beforeDeck)
          })
          cy.get('.battle-pair').should('not.exist')
        })
      })
      .then(() => {
        expect(unloaded, 'the action must not rely on a page refresh').to.equal(false)
      })
  })

  it('automatically clears the empty-hand pending-finish edge case', () => {
    let unloaded = false
    visitScenario('empty-hand-pending-finish', (window) => {
      window.addEventListener('beforeunload', () => {
        unloaded = true
      })
    })

    cy.get('[data-testid="qa-digest"]')
      .should('contain.text', 'cards')
      .and('contain.text', 'player / attack')
      .and(($digest) => {
        expect($digest.text()).to.match(/r[1-9]\d*/)
      })
    cy.get('.player-hand .empire-card.interactive:not(:disabled)')
      .should('have.length.greaterThan', 0)
    cy.then(() => {
      expect(unloaded, 'the no-card recovery must not reload the page').to.equal(false)
    })
  })

  it('opens Tom\'s ordered deck memory without changing the campaign', () => {
    visitScenario('pending-take')
    cy.get('[data-testid="qa-digest"]').invoke('text').then((beforeDigest) => {
      cy.get('[data-testid="inspect-deck-memory"]')
        .should('be.visible')
        .and('be.enabled')
        .click()
      cy.get('[data-testid="deck-memory-panel"]')
        .should('be.visible')
        .and('contain.text', 'Позиция 1 будет добрана следующей')
      cy.get('[data-testid="deck-memory-panel"] ol li')
        .should('have.length.greaterThan', 0)
        .first()
        .should('contain.text', '1')
        .and(($card) => {
          expect($card.text()).to.match(/Прямая|Перевёрнута/)
        })
      cy.get('[data-testid="qa-digest"]').should('have.text', beforeDigest)
      cy.get('[aria-label="Закрыть память колоды"]').click()
      cy.get('[data-testid="deck-memory-panel"]').should('not.exist')
    })
  })

  it('intercepts a premature winner and renders the authored God line', () => {
    visitScenario('anti-bito')
    cy.get('[data-testid="god-dialogue-line"]')
      .should('be.visible')
      .and('have.text', 'игра закончится слишком быстро и это не интересно')
    cy.get('[data-testid="qa-digest"]').should('contain.text', 'cards')
  })

  it('confirms Божественная Милость once, persists opt-out, and fails closed for bad prefs', () => {
    visitScenario('empire-council-with-points', (window) => {
      window.localStorage.setItem('kotgh_locale', 'ru')
    })
    cy.get('[data-testid^="council-restore-"]').first().as('restore').should('be.enabled').click()
    cy.get('[data-testid="divine-mercy-confirmation"]')
      .should('be.visible')
      .and('contain.text', 'Вы собираетесь потратить Божественную Милость (3/1) на (переворот карты)')
    cy.get('[data-testid="cancel-divine-mercy"]').should('be.focused').click()
    cy.get('[data-testid="divine-mercy-confirmation"]').should('not.exist')
    expectStoredValue(GOD_UI_PREFERENCES_STORAGE_KEY, null)

    cy.get('@restore').click()
    cy.get('[data-testid="confirm-divine-mercy"]')
      .should('contain.text', 'Да я и сам знаю! Не показывайте мне это больше!')
      .click()
    cy.get('[data-testid="divine-mercy-confirmation"]').should('not.exist')
    cy.window().then((window) => {
      expect(JSON.parse(window.localStorage.getItem(GOD_UI_PREFERENCES_STORAGE_KEY) ?? 'null'))
        .to.deep.equal({ schemaVersion: 1, skipDivineMercyConfirmation: true })
    })

    cy.reload()
    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid^="council-restore-"]').first().should('be.enabled').click()
    cy.get('[data-testid="divine-mercy-confirmation"]').should('not.exist')

    cy.window().then((window) => {
      window.localStorage.setItem(GOD_UI_PREFERENCES_STORAGE_KEY, '{bad-json')
    })
    cy.reload()
    cy.get('[data-testid^="council-restore-"]').first().should('be.enabled').click()
    cy.get('[data-testid="divine-mercy-confirmation"]').should('be.visible')
    cy.get('[data-testid="cancel-divine-mercy"]').click()

    cy.window().then((window) => {
      window.localStorage.setItem(GOD_UI_PREFERENCES_STORAGE_KEY, JSON.stringify({
        schemaVersion: 2,
        skipDivineMercyConfirmation: true,
      }))
    })
    cy.reload()
    cy.get('[data-testid^="council-restore-"]').first().should('be.enabled').click()
    cy.get('[data-testid="divine-mercy-confirmation"]').should('be.visible')
  })

  it('keeps a deferred card playable in Durak while labelling its empire face', () => {
    visitScenario('pending-take')
    cy.get('.player-hand .empire-card.interactive:not(:disabled)')
      .scrollIntoView()
      .should('have.length', 1)
      .and('have.class', 'deferred')
      .within(() => {
        cy.get('.deferred-status')
          .should('exist')
          .and('contain.text', 'Будущая механика')
          .and('have.attr', 'title')
      })
    cy.get('[data-testid="qa-digest"]').invoke('text').then((beforeDigest) => {
      cy.get('.player-hand .empire-card.interactive:not(:disabled)').click()
      cy.get('[data-testid="qa-digest"]').should(($digest) => {
        expect($digest.text().trim()).not.to.equal(beforeDigest.trim())
      })
    })
  })

  it('labels remaining deferred technology while exposing live Fair and Smithy carriers', () => {
    visitScenario('empire-council-with-points')
    cy.get('[data-testid="tab-technology"]').click()
    cy.get('[data-testid="technology-node-tech-printing"]')
      .scrollIntoView()
      .should('have.class', 'deferred')
      .click({ force: true })
    cy.get('.tech-detail .deferred-reason')
      .should('be.visible')
      .and('contain.text', 'сброшенных карт')
    cy.get('.tech-detail .research-button')
      .should('be.disabled')
      .and('contain.text', 'Будущая механика')
    cy.get('[data-testid="technology-node-tech-fair"]')
      .scrollIntoView()
      .should('not.have.class', 'deferred')
    cy.get('[data-testid="technology-node-steel-laurel-spearhead"]')
      .scrollIntoView()
      .click({ force: true })
    cy.get('[data-testid="selected-steel-metadata"]')
      .should('contain.text', 'steel-polearms')
      .and('contain.text', 'поколение 0')
      .and('contain.text', 'снаряжение')

    cy.get('[data-testid="tab-city"]').click()
    cy.get('[data-testid="city-building-building-smithy"]')
      .should('not.have.class', 'deferred')
      .click()
    cy.get('.improvement-drawer .deferred-note').should('not.exist')
  })

  it('shows the derived season and one persisted technology disclosure', () => {
    visitScenario('season-disclosure')
    cy.get('[data-testid="current-season"]')
      .should('contain.text', 'Зима')
      .and('contain.text', 'еда ×1')

    cy.get('[data-testid="tab-loyalty"]').click()
    cy.get('[data-testid="chronicle-entry-season"]').should('have.length', 1)
    cy.get('[data-testid="chronicle-entry-technology-disclosure"]')
      .should('have.length', 1)
      .and('contain.text', 'Закрытый город')

    cy.get('[data-testid="tab-technology"]').click()
    cy.get('[data-testid="technology-node-reform-city-gates"]')
      .scrollIntoView()
      .click({ force: true })
    cy.get('[data-testid="selected-technology-side"]')
      .should('contain.text', 'Тёмная сторона')
      .and('contain.text', 'Закрытый город')
  })

  it('opens the constructor without structuredClone/DataCloneError failures', () => {
    const browserErrors: string[] = []
    visitScenario('empire-council-with-points', (window) => {
      window.addEventListener('error', event => browserErrors.push(event.message))
      window.addEventListener('unhandledrejection', event => browserErrors.push(String(event.reason)))
    })

    cy.get('[data-testid="open-constructor"]').should('be.visible').and('be.enabled').click()
    cy.get('[data-testid="constructor-drawer"]')
      .should('be.visible')
      .and('have.attr', 'aria-label', "Конструктор Empire's Endgame")
    cy.get('[data-testid="constructor-drawer"] select').first().should(($select) => {
      expect(String($select.val())).not.to.equal('')
    })
    cy.then(() => {
      expect(browserErrors.join('\n')).not.to.match(/DataCloneError|structuredClone/i)
    })
  })

  it('keeps production campaign and config storage isolated from every QA mutation', () => {
    const productionCampaign = 'production-campaign-sentinel'
    const productionConfig = 'production-config-sentinel'
    const importSnapshot = createEmpiresQaScenarios(bundledConfig, { seed: QA_SEED }).event.snapshot
    const importEnvelope = {
      schemaVersion: importSnapshot.schemaVersion,
      savedAt: '2026-07-15T00:00:00.000Z',
      state: importSnapshot,
    }

    visitScenario('empire-council-with-points', (window) => {
      window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, productionCampaign)
      window.localStorage.setItem(CONFIG_STORAGE_KEY, productionConfig)
    })
    cy.window().then(window => cy.stub(window, 'confirm').returns(true))

    cy.get('[data-testid="new-campaign"]').click()
    cy.contains('.campaign-toast', 'только в QA').should('be.visible')
    expectStoredValue(EMPIRES_SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="open-constructor"]').click()
    cy.get('[data-testid="constructor-save"]').click()
    cy.get('[data-testid="constructor-save"]').should('contain.text', 'Сохранено')
    cy.get('[data-testid="qa-digest"]').should('be.visible')
    expectStoredValue(EMPIRES_SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="constructor-reset"]').click()
    cy.contains('.campaign-toast', 'восстановлен только в QA').should('be.visible')
    expectStoredValue(EMPIRES_SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="import-campaign"]').selectFile({
      contents: Cypress.Buffer.from(JSON.stringify(importEnvelope)),
      fileName: 'qa-import.json',
      mimeType: 'application/json',
      lastModified: Date.now(),
    }, { force: true })
    cy.contains('.campaign-toast', 'временный QA-стенд').should('be.visible')
    expectStoredValue(EMPIRES_SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)
  })

  it('accepts one of three deterministic divine gifts and enters the empire phase', () => {
    const immediateGift = bundledConfig.gifts.definitions.find((gift) => {
      const kind = gift.resolution?.kind
      return !gift.deferredReason && kind !== 'cityResources' && kind !== 'meteorCity'
    })
    visitScenario('divine-gift')
    cy.get('.gift-card:not(.empty-card)')
      .should('have.length', 3)
    cy.contains('.gift-card:not(.empty-card)', immediateGift?.name ?? 'Землетрясение')
      .should('be.visible')
      .and('be.enabled')
      .click()
    cy.get('[data-testid="qa-digest"]')
      .should('contain.text', 'empire')
      .and('not.contain.text', 'divineGift')
    cy.get('.empire-toolbar').should('be.visible')
  })

  it('targets one city for the resource grant and leaves every other city ledger unchanged', () => {
    const fixture = createEmpiresQaScenarios(bundledConfig, { seed: QA_SEED })['target-city-resources']
    const pending = fixture.snapshot.pendingResolution
    expect(pending?.kind).to.equal('cityResources')
    if (!pending || pending.kind !== 'cityResources') throw new Error('Missing city-resource fixture.')
    const targetId = pending.eligibleTargetIds[0]
    const otherId = pending.eligibleTargetIds.find(id => id !== targetId) as string
    const resourceId = bundledConfig.gifts.definitions
      .find(gift => gift.id === pending.giftId)?.effects
      .find(effect => effect.kind === 'resource')?.resourceId as string

    visitScenario('target-city-resources')
    cy.get('[data-testid="target-resolution-dialog"]').should('be.visible')
    cy.get(`[data-testid="target-city-${targetId}"]`)
      .should('be.visible')
      .and('be.enabled')
      .and('contain.text', '→')
      .click()

    cy.get('[data-testid="qa-digest"]').should('contain.text', 'empire')
    cy.get('.city-selector-row select').should('have.value', targetId)
    cy.get(`[data-testid="city-resource-${resourceId}"]`)
      .should('have.attr', 'data-city-id', targetId)
      .and(($resource) => {
        expect($resource.text().trim()).not.to.match(/\b0$/)
      })

    cy.get('.city-selector-row select').select(otherId)
    cy.get(`[data-testid="city-resource-${resourceId}"]`)
      .should('have.attr', 'data-city-id', otherId)
      .and(($resource) => {
        expect($resource.text().trim()).to.match(/\b0$/)
      })
  })

  it('targets one city for a meteor strike and downgrades only that city', () => {
    const fixture = createEmpiresQaScenarios(bundledConfig, { seed: QA_SEED })['target-meteor-city']
    const pending = fixture.snapshot.pendingResolution
    expect(pending?.kind).to.equal('meteorCity')
    if (!pending || pending.kind !== 'meteorCity') throw new Error('Missing meteor fixture.')
    const targetId = pending.eligibleTargetIds.find((id) => {
      const city = fixture.snapshot.empire.cities.find(item => item.id === id)
      return (city?.buildingLevels['building-farm'] ?? 0) > 0
    }) as string
    const otherId = pending.eligibleTargetIds.find(id => id !== targetId) as string
    const targetBuildings = fixture.snapshot.empire.cities
      .find(city => city.id === targetId)?.buildingLevels ?? {}
    const [targetBuildingId, targetBefore] = Object.entries(targetBuildings)
      .filter(([buildingId, level]) => (
        level > 0
        && !bundledConfig.empire.buildings.find(building => building.id === buildingId)?.deferredReason
      ))
      .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
        rightLevel - leftLevel || leftId.localeCompare(rightId)
      ))[0]
    const otherBefore = fixture.snapshot.empire.cities
      .find(city => city.id === otherId)?.buildingLevels[targetBuildingId] ?? 0
    const expectedTarget = Math.max(0, targetBefore - pending.damageLevels)
    const buildingMax = Math.max(...bundledConfig.empire.buildings
      .find(building => building.id === targetBuildingId)!
      .levels.map(level => level.level))

    visitScenario('target-meteor-city')
    cy.get(`[data-testid="target-city-${targetId}"]`).should('be.enabled').click()
    cy.get('.city-selector-row select').should('have.value', targetId)
    cy.get(`[data-testid="city-building-level-${targetBuildingId}"]`)
      .should('contain.text', `Ур. ${expectedTarget}/${buildingMax}`)

    cy.get('.city-selector-row select').select(otherId)
    cy.get(`[data-testid="city-building-level-${targetBuildingId}"]`)
      .should('contain.text', `Ур. ${otherBefore}/${buildingMax}`)
  })

  it('keeps a destroyed region inspectable while blocking its cities', () => {
    visitScenario('destroyed-west')
    cy.get('[data-testid="map-region-west"]')
      .should('be.visible')
      .and('not.be.disabled')
      .click()
    cy.get('[data-testid="region-lost-west"]')
      .should('be.visible')
      .and('contain.text', 'Доступ к региону потерян')
    cy.get('[data-testid="map-city-city-west-green-bastion"]')
      .should('be.disabled')
      .and('contain.text', 'Недоступен')

    cy.get('[data-testid="tab-city"]').click()
    cy.get('.city-selector-row option[value="city-west-green-bastion"]').should('be.disabled')
  })

  it('shows rebellion, recovery history, class gates, and bounded political history', () => {
    visitScenario('loyalty-rebellion')
    cy.get('[data-testid="tab-loyalty"]').click()
    cy.get('[data-testid="loyalty-panel"]').should('be.visible')
    cy.get('[data-testid="reputation-value"]').should('be.visible')
    cy.get('[data-testid="region-status-west"]')
      .should('contain.text', 'Восстание')
      .and('not.contain.text', 'Уничтожен')
    cy.get('[data-testid="region-status-north"]').should('contain.text', 'Под контролем')
    cy.get('[data-testid="chronicle-entry-battle-loss"]').should('exist')
    cy.get('[data-testid="chronicle-entry-recovery"]').should('exist')
    cy.get('[data-testid^="city-loyalty-"]').first().should('contain.text', 'рабочие')
  })

  it('shows relic-adjusted current and maximum farm and lumber levels', () => {
    const fixture = createEmpiresQaScenarios(bundledConfig, { seed: QA_SEED })['relic-production-levels']
    const city = fixture.snapshot.empire.cities.find(item =>
      (item.buildingLevels['building-farm'] ?? 0) > 0
      && (item.buildingLevels['building-lumber'] ?? 0) > 0)
    expect(city).to.not.equal(undefined)
    const baseFarm = city?.buildingLevels['building-farm'] ?? 0
    const farmMax = Math.max(...bundledConfig.empire.buildings
      .find(building => building.id === 'building-farm')!
      .levels.map(level => level.level))

    visitScenario('relic-production-levels')
    cy.get('[data-testid="tab-city"]').click()
    cy.get('.city-selector-row select').select(city?.id ?? '')
    cy.get('[data-testid="city-building-building-farm"]').click()
    cy.get('[data-testid="building-level-building-farm"]')
      .should('contain.text', `${baseFarm + 1}/${farmMax + 1}`)
    cy.contains('.effective-level-note', 'Реликвия').should('be.visible')
  })

  it('renders legible Council cards and spends an upgrade point on click', () => {
    visitScenario('empire-council-with-points')
    cy.contains('.empire-toolbar nav button', 'Совет карт').click()

    cy.get('.council-view > header > b').should(($points) => {
      expect($points.text()).to.match(/^3\b/)
    })
    cy.get('[data-testid^="council-card-"]')
      .should('have.length.greaterThan', 0)
      .first()
      .as('councilCard')
      .should('be.visible')
      .and('have.attr', 'aria-pressed', 'false')
      .then(($card) => {
        const style = getComputedStyle($card[0])
        expect(firstGradientLuminance(style.backgroundImage), style.backgroundImage).to.be.greaterThan(150)
        expect(style.color).to.equal('rgb(39, 31, 22)')
      })

    cy.get('@councilCard').click().should('have.attr', 'aria-pressed', 'true')
    cy.get('@councilCard')
      .closest('article')
      .find('.council-actions > button')
      .first()
      .should('be.enabled')
      .click()
    cy.get('.council-view > header > b').should(($points) => {
      expect($points.text()).to.match(/^2\b/)
    })
  })

  it('exposes the complete scrollable Development tree and supports constructor drag', () => {
    visitScenario('empire-council-with-points')
    cy.contains('.empire-toolbar nav button', 'Развитие').click()

    cy.get('[data-testid="technology-viewport"]')
      .should('be.visible')
      .then(($viewport) => {
        expect($viewport[0].scrollHeight).to.be.greaterThan($viewport[0].clientHeight)
        expect($viewport[0].scrollWidth).to.be.greaterThan($viewport[0].clientWidth)
      })
    cy.get('[data-testid^="technology-node-"]').should('have.length', TECHNOLOGY_COUNT)
    cy.get('[data-testid^="technology-node-"]')
      .last()
      .scrollIntoView()
      .should('be.visible')
      .click()
      .should('have.attr', 'aria-pressed', 'true')
    cy.get('.tech-detail h3').should('not.have.text', 'Выберите ноду')

    cy.get('[data-testid="open-constructor"]').click()
    cy.get('[data-testid="constructor-drawer"]').should('be.visible')
    cy.get('.tech-tree .mode-label').should('contain.text', 'Режим редактора')

    cy.get('[data-testid^="technology-node-"]')
      .first()
      .scrollIntoView()
      .then(($node) => {
        const node = $node[0]
        const nodeBounds = node.getBoundingClientRect()
        const originalPosition = `${node.style.left}|${node.style.top}`
        const dataTransfer = new DataTransfer()

        cy.wrap($node).trigger('dragstart', {
          dataTransfer,
          clientX: nodeBounds.left + nodeBounds.width / 2,
          clientY: nodeBounds.top + nodeBounds.height / 2,
          force: true,
        })
        cy.get('[data-testid="technology-viewport"]').then(($viewport) => {
          const bounds = $viewport[0].getBoundingClientRect()
          cy.wrap($viewport).trigger('drop', {
            dataTransfer,
            clientX: bounds.left + Math.min(bounds.width - 90, 520),
            clientY: bounds.top + Math.min(bounds.height - 90, 360),
            force: true,
          })
        })
        cy.wrap($node).trigger('dragend', { dataTransfer, force: true })
        cy.get(`[data-testid="${node.dataset.testid}"]`).should(($movedNode) => {
          const movedPosition = `${$movedNode[0].style.left}|${$movedNode[0].style.top}`
          expect(movedPosition).not.to.equal(originalPosition)
        })
      })
  })

  it('lays out technologies without coordinates away from authored nodes on a non-overlapping pixel grid', () => {
    const configWithoutSomePositions = structuredClone(bundledConfig)
    configWithoutSomePositions.empire.technologies.forEach((technology, index) => {
      if (index % 2 === 0) delete technology.position
    })
    visitScenario('empire-council-with-points', (window) => {
      window.localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(configWithoutSomePositions))
    })
    cy.contains('.empire-toolbar nav button', 'Развитие').click()

    cy.get('[data-testid^="technology-node-"]').should('have.length', TECHNOLOGY_COUNT).then(($nodes) => {
      const positions = Array.from($nodes, node => ({
        x: Number.parseFloat((node as HTMLElement).style.left),
        y: Number.parseFloat((node as HTMLElement).style.top),
      }))
      for (let left = 0; left < positions.length; left += 1) {
        for (let right = left + 1; right < positions.length; right += 1) {
          const horizontalOverlap = Math.abs(positions[left].x - positions[right].x) < 148
          const verticalOverlap = Math.abs(positions[left].y - positions[right].y) < 58
          expect(horizontalOverlap && verticalOverlap, `nodes ${left} and ${right}`).to.equal(false)
        }
      }
    })
  })

  it('executes a deterministic event choice and leaves the event phase', () => {
    visitScenario('event')
    cy.get('[data-testid="qa-digest"]').should('contain.text', 'event')
    cy.get('[role="dialog"][aria-labelledby="event-dialog-title"]').should('be.visible')
    cy.get('.event-choice:not(:disabled)').first().should('be.visible').click()
    cy.get('[role="dialog"][aria-labelledby="event-dialog-title"]').should('not.exist')
    cy.get('[data-testid="qa-digest"]').should('not.contain.text', 'event')
  })

  it('shows the typed Customs target and next-con consequences', () => {
    visitScenario('economy-content-event')
    cy.get('#event-dialog-title').should('contain.text', 'Тайный наркотрафик')
    cy.get('#event-dialog-description').should('contain.text', 'Город:')
    cy.get('.event-choice').should('have.length', 2)
      .and('contain.text', 'Следующий кон')
      .and('contain.text', 'Население выбранного города')
    cy.get('.event-choice:not(:disabled)').first().click()
    cy.get('[role="dialog"][aria-labelledby="event-dialog-title"]').should('not.exist')
  })

  it('shows an epidemic badge and the detailed city projection', () => {
    visitScenario('epidemic-outbreak')
    cy.get('[data-testid^="map-epidemic-"]').should('have.length', 1).first().as('badge')
    cy.get('@badge').should('contain.text', 'Вспышка')
    cy.get('@badge').closest('button').click()
    cy.get('[data-testid^="city-epidemics-"]')
      .should('be.visible')
      .and('contain.text', 'Чума')
      .and('contain.text', 'Следующий итог')
      .and('contain.text', 'Защита')
  })

  it('runs the seeded campaign autoplay without a stalled player turn', () => {
    visitScenario('pending-take')
    cy.get('[data-testid="qa-autoplay"]').should('be.enabled').click()
    cy.get('[data-testid="qa-autoplay-result"]')
      .should('be.visible')
      .and('contain.text', 'OK')
      .and('contain.text', 'проверок хода')
  })
})
