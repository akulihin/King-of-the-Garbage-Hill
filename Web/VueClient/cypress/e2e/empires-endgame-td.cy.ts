/// <reference types="cypress" />

describe('Empire\'s Endgame TD vertical slice', () => {
  it('renders the defense HUD and settles through the QA replay path', () => {
    const query = new URLSearchParams({
      qa: '1',
      scenario: 'battle-defense',
      seed: 'cypress-empires-td',
    })
    cy.visit(`/empires-endgame?${query}`, {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('[data-testid="qa-panel"]').should('be.visible')
    cy.get('[data-testid="qa-scenario"]').should('have.value', 'battle-defense')
    cy.get('[data-testid="td-battle"]').should('be.visible')
    cy.get('[data-testid="td-hud"]')
      .should('contain.text', 'Оборона')
      .and('contain.text', 'Замок')
      .and('contain.text', 'Ресурс')
    cy.get('[data-testid="td-deployment"]').should('contain.text', 'План развёртывания')
    cy.get('[data-testid="td-grade-drawer"]').should('be.visible')
    cy.get('[data-testid="td-qa-policy"]').select('balanced')
    cy.get('[data-testid="td-fast-resolve"]').should('be.enabled').click()

    cy.get('[data-testid="td-battle"]').should('not.exist')
    cy.get('[data-testid="qa-digest"]')
      .should('contain.text', 'cards')
      .and(($digest) => expect($digest.text()).to.match(/r[1-9]\d*/))
  })
})
