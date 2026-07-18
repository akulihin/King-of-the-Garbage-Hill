/// <reference types="cypress" />

const QA_SEED = 'cypress-empires-tavern'

describe('Empire\'s Endgame Tavern browser flow', () => {
  it('uses both authored sections and QA fast resolve through the production replay path', () => {
    const query = new URLSearchParams({ qa: '1', scenario: 'mystic-tavern', seed: QA_SEED })
    cy.visit(`/empires-endgame?${query}`, {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid="qa-scenario"]').should('have.value', 'mystic-tavern')
    cy.get('[data-testid="tavern-minigame"]').should('be.visible')
      .and('contain.text', 'Таверна «У List\'a»')
    cy.get('[data-testid^="tavern-hire-"]').first().should('be.enabled').click()
    cy.contains('button', 'Барная стойка').click()
    cy.get('[data-testid="tavern-buy-rumor"]').should('be.enabled').click()
    cy.contains('Загадочная тройка').should('be.visible')
    cy.contains('неполные контракты нельзя нанять', { matchCase: false }).should('be.visible')

    cy.get('[data-testid="tavern-qa-resolve"]').click()
    cy.get('[data-testid="tavern-minigame"]').should('not.exist')
    cy.get('[data-testid="qa-digest"]').should('contain.text', 'empire')
    cy.get('[data-testid="tab-economy"]').click()
    cy.get('[data-testid="domestic-economy-panel"]').should('contain.text', 'Таверна')
      .and('contain.text', 'Последнее посещение')
  })
})
