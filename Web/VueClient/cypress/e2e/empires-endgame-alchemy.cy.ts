/// <reference types="cypress" />

describe('Empire\'s Endgame Tetris-alchemy browser flow', () => {
  it('settles the deterministic explosion through the epidemic map projection', () => {
    const query = new URLSearchParams({
      qa: '1',
      scenario: 'alchemy-experiment',
      seed: 'cypress-empires-alchemy',
    })
    cy.visit(`/empires-endgame?${query}`, {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid="qa-scenario"]').should('have.value', 'alchemy-experiment')
    cy.get('[data-testid="alchemy-board"]')
      .should('be.visible')
      .and('contain.text', 'Сбор')
      .and('contain.text', 'Реагенты')
    cy.get('[data-testid="alchemy-pause"]').click()
    cy.get('[data-testid="alchemy-reagents"]').click()
    cy.get('[data-testid="alchemy-reagent-panel"]').should('be.visible')
    cy.get('[data-testid="alchemy-qa-explosion"]').click()

    cy.get('[data-testid="alchemy-board"]').should('not.exist')
    cy.get('[data-testid="qa-digest"]').should('contain.text', 'empire')
    cy.get('[data-testid="tab-map"]').click()
    cy.get('[data-testid^="map-epidemic-"]').should('have.length', 1).first().as('badge')
    cy.get('@badge').should('contain.text', 'Вспышка')
    cy.get('@badge').closest('button').click()
    cy.get('[data-testid^="city-epidemics-"]')
      .should('be.visible')
      .and('contain.text', 'Чума')
  })
})
