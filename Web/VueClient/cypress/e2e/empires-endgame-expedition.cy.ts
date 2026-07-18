/// <reference types="cypress" />

describe('Empire\'s Endgame expedition browser flow', () => {
  it('opens the typed fortress, provisions a roster, assaults it, and exposes the opened zone', () => {
    const query = new URLSearchParams({
      qa: '1',
      scenario: 'expedition-planning',
      seed: 'cypress-empires-expedition',
      tab: 'map',
    })
    cy.visit(`/empires-endgame?${query}`, {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid="qa-scenario"]').should('have.value', 'expedition-planning')
    cy.get('[data-testid="map-region-south"]').click()
    cy.get('[data-testid="map-fortress-expedition-south-fortress"]').should('be.visible').click()
    cy.get('[data-testid="expedition-planning"]')
      .should('contain.text', 'Экспедиция к Пустынной крепости')
      .and('contain.text', 'Юг: голые враги и крокодилья кожа')
    cy.get('[data-testid^="expedition-unit-"]:checked').should('have.length', 2)
    cy.get('[data-testid="expedition-launch"]').should('be.enabled').click()
    cy.get('[data-testid="expedition-assault"]').should('be.visible').click()

    cy.get('[data-testid="td-battle"]').should('be.visible')
    cy.get('[data-testid="td-hud"]').should('contain.text', 'Штурм')
    cy.get('[data-testid="td-fast-resolve"]').should('be.enabled').click()

    cy.get('[data-testid="td-battle"]').should('not.exist')
    cy.get('[data-testid="expedition-planning"]').should('contain.text', 'Зона открыта')
    cy.get('[data-testid="expedition-close"]').click()
    cy.get('[data-testid="map-fortress-expedition-south-fortress"]').should('have.class', 'opened')
  })
})
