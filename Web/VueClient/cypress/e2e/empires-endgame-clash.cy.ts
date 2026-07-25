/// <reference types="cypress" />

describe('Empire\'s Endgame Clash vertical slice', () => {
  it('renders the lane battle and settles through deterministic QA replay', () => {
    const query = new URLSearchParams({
      qa: '1',
      scenario: 'battle-clash',
      seed: 'cypress-empires-clash',
    })
    cy.visit(`/empires-endgame?${query}`, {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid="qa-scenario"]').should('have.value', 'battle-clash')
    cy.get('[data-testid="clash-minigame"]')
      .should('be.visible')
      .and('contain.text', 'Клэш')
    cy.get('[data-testid="clash-morale-attacker"]').should('be.visible')
    cy.get('[data-testid="clash-morale-defender"]').should('be.visible')
    cy.get('[role="grid"][aria-label="Поле боя Клэша"]').should('be.visible')
    cy.get('[data-testid="qa-digest"]').should('contain.text', 'результаты 0')
    cy.get('[data-testid="clash-qa-policy"]').select('ranged-rear')
    cy.get('[data-testid="clash-qa-resolve"]').should('be.enabled').click()

    cy.get('[data-testid="clash-minigame"]').should('not.exist')
    cy.get('[data-testid="qa-digest"]')
      .should('contain.text', 'cards')
      .and('contain.text', 'результаты 1')
      .and(($digest) => expect($digest.text()).to.match(/r[1-9]\d*/))
  })

  it('opens Clash from its own public tab and runs without a campaign', () => {
    cy.visit('/clash?seed=cypress-standalone-clash&policy=ranged-rear', {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('nav[aria-label="Primary navigation / Основная навигация"]')
      .find('a[href="/clash"]')
      .should('have.class', 'router-link-exact-active')
      .and('contain.text', 'Clash')
    cy.get('[data-testid="clash-standalone"]').should('be.visible')
    cy.get('[data-testid="clash-standalone-seed"]').should('have.value', 'cypress-standalone-clash')
    cy.get('[data-testid="clash-minigame"]').should('be.visible')
    cy.get('[role="grid"][aria-label="Поле боя Клэша"]').should('be.visible')
    cy.get('[data-testid="clash-qa-policy"]').should('have.value', 'ranged-rear')
    cy.get('[data-testid="clash-qa-resolve"]').click()

    cy.get('[data-testid="clash-standalone-result"]')
      .should('be.visible')
      .and('contain.text', 'Команд')
    cy.get('[data-testid="clash-standalone-digest"]')
      .invoke('text')
      .should('match', /^[0-9a-f]{16}$/)
    cy.get('[data-testid="clash-standalone-restart"]').click()
    cy.get('[data-testid="clash-minigame"]').should('be.visible')
    cy.get('[data-testid="clash-text-state"]').should('contain.text', 'Ход 0')
    cy.window().then((window) => {
      expect(Object.keys(window.localStorage)
        .filter(key => key.startsWith('empires-endgame:campaign:'))).to.deep.equal([])
    })
  })
})
