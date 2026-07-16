/// <reference types="cypress" />

interface MutableButton {
  pressed: boolean
  value: number
}

interface MutableGamepad {
  axes: number[]
  buttons: MutableButton[]
  connected: boolean
  id: string
  index: number
  mapping: string
}

function mutateGamepad(update: (gamepad: MutableGamepad) => void) {
  cy.window().then((window) => {
    update((window as unknown as { __lastChancesGamepad: MutableGamepad }).__lastChancesGamepad)
  })
}

describe('99 Last Chances controller flow', () => {
  it('enters the opening route and performs the hold follow-up with a standard DualSense', () => {
    const axes = [0, 0, 0, 0]
    const buttons: MutableButton[] = Array.from({ length: 16 }, () => ({ pressed: false, value: 0 }))
    const gamepad: MutableGamepad = {
      axes,
      buttons,
      connected: true,
      id: 'DualSense Wireless Controller',
      index: 0,
      mapping: 'standard',
    }

    cy.visit('/99lc', {
      onBeforeLoad(window) {
        window.localStorage.clear()
        const testWindow = window as unknown as { __lastChancesGamepad: MutableGamepad }
        testWindow.__lastChancesGamepad = window.JSON.parse(JSON.stringify(gamepad)) as MutableGamepad
        Object.defineProperty(window.navigator, 'getGamepads', {
          configurable: true,
          value: () => [testWindow.__lastChancesGamepad],
        })
      },
    })

    cy.get('.lc-map-backdrop').should('be.visible')
    cy.get('.lc-control-grid article.is-connected')
      .should('have.attr', 'data-gamepad-id', 'DualSense Wireless Controller')
      .and('have.attr', 'data-gamepad-profile', 'standard')
      .and('contain.text', 'DualSense Wireless Controller')
    cy.get('.lc-route-node.is-available.is-gamepad-selected').then(($openingSelection) => {
      const firstNodeId = $openingSelection.attr('data-node-id')

      mutateGamepad((pad) => {
        pad.buttons[2].pressed = true
        pad.buttons[2].value = 1
      })
      cy.get('.lc-control-grid article.is-connected')
        .should('have.attr', 'data-gamepad-status', 'active')
      cy.get('.lc-route-node.is-available.is-gamepad-selected')
        .should(($cycledSelection) => {
          expect($cycledSelection.attr('data-node-id')).not.to.equal(firstNodeId)
        })
      mutateGamepad((pad) => {
        pad.buttons[2].pressed = false
        pad.buttons[2].value = 0
      })
      cy.wait(80)

      mutateGamepad((pad) => {
        pad.buttons[0].pressed = true
        pad.buttons[0].value = 1
      })
      cy.get('.lc-map-backdrop').should('not.exist')
      mutateGamepad((pad) => {
        pad.buttons[0].pressed = false
        pad.buttons[0].value = 0
      })
    })

    mutateGamepad((pad) => {
      pad.buttons[2].pressed = true
      pad.buttons[2].value = 1
    })
    cy.wait(700)
    mutateGamepad((pad) => {
      pad.buttons[2].pressed = false
      pad.buttons[2].value = 0
    })
    cy.get('.lc-input-feedback').first().should('contain.text', 'Tap once now')
    mutateGamepad((pad) => {
      pad.buttons[2].pressed = true
      pad.buttons[2].value = 1
    })
    cy.wait(60)
    mutateGamepad((pad) => {
      pad.buttons[2].pressed = false
      pad.buttons[2].value = 0
    })

    cy.get('.lc-gesture-toast').should('contain.text', 'Круг над головой')
  })

  it('saves an override without disrupting the attempt and applies it only to a fresh generation', () => {
    cy.visit('/99lc', {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    cy.get('.lc-route-node.is-available').first().click()
    cy.get('.lc-map-backdrop').should('not.exist')
    cy.get('.lc-room-readout strong').invoke('text').then((roomName) => {
      cy.get('.lc-header-actions .is-builder').click()
      cy.contains('.lc-fields-grid label', 'Starting Chances').find('input').clear().type('77')
      cy.contains('.lc-builder-apply-actions button', 'Save browser override').click()
      cy.window().then((window) => {
        const override = JSON.parse(window.localStorage.getItem('99lc:game-config') ?? '{}') as {
          chances?: number
        }
        expect(override.chances).to.equal(77)
      })

      cy.contains('.lc-builder-file-actions button', 'Clear override').click()
      cy.window().then((window) => {
        expect(window.localStorage.getItem('99lc:game-config')).to.equal(null)
      })

      cy.get('.lc-builder').should('be.visible')
      cy.get('.lc-builder-header button[aria-label="Close builder"]').click()
      cy.get('.lc-map-backdrop').should('not.exist')
      cy.get('.lc-room-readout strong').should('have.text', roomName)
      cy.get('.lc-chance-orb > span').should('have.text', '99')

      cy.get('.lc-header-actions .is-builder').click()
      cy.contains('.lc-builder-apply-actions button', 'Apply & start fresh generation').click()
      cy.get('.lc-map-backdrop').should('be.visible')
      cy.get('.lc-chance-orb > span').should('have.text', '77')
      cy.contains('.lc-run-card dt', 'Generation').parent().find('dd').should('have.text', '#1')
    })
  })
})
