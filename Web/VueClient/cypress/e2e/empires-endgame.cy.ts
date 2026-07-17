/// <reference types="cypress" />

import bundledConfigJson from '../../public/empires-endgame/game-config.json'
import { createEmpiresQaScenarios } from '../../src/features/empires-endgame/qa'
import type { EmpiresEndgameConfig } from '../../src/features/empires-endgame/types'

type QaScenario =
  | 'pending-take'
  | 'empty-hand-pending-finish'
  | 'divine-gift'
  | 'target-city-resources'
  | 'target-meteor-city'
  | 'empire-council-with-points'
  | 'destroyed-west'
  | 'relic-production-levels'
  | 'event'
  | 'victory'
  | 'defeat'

const QA_SEED = 'cypress-empires-endgame'
const CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'
const SAVE_STORAGE_KEY = 'empires-endgame:campaign:v1'
const bundledConfig = bundledConfigJson as unknown as EmpiresEndgameConfig

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

function expectStoredValue(key: string, value: string) {
  cy.window().then((window) => {
    expect(window.localStorage.getItem(key), key).to.equal(value)
  })
}

describe('Empire\'s Endgame deterministic browser scenarios', () => {
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

  it('labels deferred technology while exposing the newly live smithy carrier', () => {
    visitScenario('empire-council-with-points')
    cy.get('[data-testid="tab-technology"]').click()
    cy.get('[data-testid="technology-node-tech-fair"]')
      .scrollIntoView()
      .should('have.class', 'deferred')
      .click({ force: true })
    cy.get('.tech-detail .deferred-reason')
      .should('be.visible')
      .and('contain.text', 'Будущая механика')
    cy.get('.tech-detail .research-button')
      .should('be.disabled')
      .and('contain.text', 'Будущая механика')

    cy.get('[data-testid="tab-city"]').click()
    cy.get('[data-testid="city-building-building-smithy"]')
      .should('not.have.class', 'deferred')
      .click()
    cy.get('.improvement-drawer .deferred-note').should('not.exist')
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
      schemaVersion: 1,
      savedAt: '2026-07-15T00:00:00.000Z',
      state: importSnapshot,
    }

    visitScenario('empire-council-with-points', (window) => {
      window.localStorage.setItem(SAVE_STORAGE_KEY, productionCampaign)
      window.localStorage.setItem(CONFIG_STORAGE_KEY, productionConfig)
    })
    cy.window().then(window => cy.stub(window, 'confirm').returns(true))

    cy.get('[data-testid="new-campaign"]').click()
    cy.contains('.campaign-toast', 'только в QA').should('be.visible')
    expectStoredValue(SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="open-constructor"]').click()
    cy.get('[data-testid="constructor-save"]').click()
    cy.contains('.campaign-toast', 'Правила применены только к QA-стенду').should('be.visible')
    expectStoredValue(SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="constructor-reset"]').click()
    cy.contains('.campaign-toast', 'восстановлен только в QA').should('be.visible')
    expectStoredValue(SAVE_STORAGE_KEY, productionCampaign)
    expectStoredValue(CONFIG_STORAGE_KEY, productionConfig)

    cy.get('[data-testid="import-campaign"]').selectFile({
      contents: Cypress.Buffer.from(JSON.stringify(importEnvelope)),
      fileName: 'qa-import.json',
      mimeType: 'application/json',
      lastModified: Date.now(),
    }, { force: true })
    cy.contains('.campaign-toast', 'временный QA-стенд').should('be.visible')
    expectStoredValue(SAVE_STORAGE_KEY, productionCampaign)
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
    cy.get('[data-testid^="technology-node-"]').should('have.length', 62)
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

    cy.get('[data-testid^="technology-node-"]').should('have.length', 62).then(($nodes) => {
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

  it('runs the seeded campaign autoplay without a stalled player turn', () => {
    visitScenario('pending-take')
    cy.get('[data-testid="qa-autoplay"]').should('be.enabled').click()
    cy.get('[data-testid="qa-autoplay-result"]')
      .should('be.visible')
      .and('contain.text', 'OK')
      .and('contain.text', 'проверок хода')
  })
})
