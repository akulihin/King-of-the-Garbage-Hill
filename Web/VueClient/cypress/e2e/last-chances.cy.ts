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

interface LastChancesTestWindow {
  __lastChancesGamepad: MutableGamepad
  __lastChancesHidRequests: number
}

interface DurableConfigFixture {
  loadout: {
    primaryWeaponId: string
    secondaryWeaponId: string | null
  }
  player: {
    baseStats: {
      maxHp: number
      maxMentalHealth: number
      maxStamina: number
    }
  }
  progression: {
    tiers: Array<{
      enemyPool: Array<{ enemyId: string, weight: number }>
      enemyCount: [number, number]
      roomTemplateIds: string[]
    }>
  }
  enemies: Array<{
    id: string
    maxHp: number
    moveSpeed: number
    dodge?: number
    idleTurnRadiansPerSecond?: number
    visionRange: number
    visionAngleDegrees: number
    noticeMs: number
    alertPauseMs: number
    attackRange: number
    attackDamage: number
    attackCooldownMs: number
    attackWindupMs: number
    leapDistance?: number
    leapDurationMs?: number
    targetLockMs?: number
    tuning?: Record<string, number>
  }>
  rooms: Array<{
    id: string
    obstacles: unknown[]
    hazards?: unknown[]
    spawnLayouts?: Array<{ enemySpawns: Array<{ x: number, y: number }> }>
  }>
}

function mutateGamepad(update: (gamepad: MutableGamepad) => void) {
  cy.window().then((window) => {
    update((window as unknown as { __lastChancesGamepad: MutableGamepad }).__lastChancesGamepad)
  })
}

function makeGamepad(): MutableGamepad {
  return {
    axes: [0, 0, 0, 0],
    buttons: Array.from({ length: 16 }, () => ({ pressed: false, value: 0 })),
    connected: true,
    id: 'DualSense Wireless Controller',
    index: 0,
    mapping: 'standard',
  }
}

function installGamepad(window: Window, gamepad: MutableGamepad) {
  const testWindow = window as unknown as LastChancesTestWindow
  testWindow.__lastChancesGamepad = JSON.parse(
    JSON.stringify(gamepad),
  ) as MutableGamepad
  testWindow.__lastChancesHidRequests = 0
  Object.defineProperty(window.navigator, 'getGamepads', {
    configurable: true,
    value: () => [testWindow.__lastChancesGamepad],
  })
  Object.defineProperty(window.navigator, 'hid', {
    configurable: true,
    value: {
      requestDevice: async () => {
        testWindow.__lastChancesHidRequests += 1
        return []
      },
    },
  })
}

function visitDurableGamepadFixture() {
  cy.intercept('GET', '**/99lc/game-config.json', (request) => {
    request.continue((response) => {
      const parsed = (typeof response.body === 'string'
        ? JSON.parse(response.body) as DurableConfigFixture
        : response.body as DurableConfigFixture)
      parsed.player.baseStats.maxHp = 99_999
      parsed.player.baseStats.maxMentalHealth = 99_999
      parsed.progression.tiers[0].enemyPool = [{ enemyId: 'servant', weight: 1 }]
      const servant = parsed.enemies.find(enemy => enemy.id === 'servant')
      if (servant) servant.maxHp = 99_999
      response.body = parsed
    })
  })
  cy.visit('/99lc?qa=1&fixture=controls', {
    onBeforeLoad(window) {
      window.localStorage.clear()
      installGamepad(window, makeGamepad())
    },
  })
}

function visitKnifeSpiderCaptureFixture() {
  cy.intercept('GET', '**/99lc/game-config.json', (request) => {
    request.continue((response) => {
      const parsed = (typeof response.body === 'string'
        ? JSON.parse(response.body) as DurableConfigFixture
        : response.body as DurableConfigFixture)
      parsed.player.baseStats.maxHp = 99_999
      parsed.player.baseStats.maxMentalHealth = 99_999
      parsed.progression.tiers[0].enemyCount = [1, 1]
      parsed.progression.tiers[0].enemyPool = [{ enemyId: 'spider-knife', weight: 1 }]
      parsed.progression.tiers[0].roomTemplateIds = ['false-apartment']
      parsed.loadout.primaryWeaponId = 'either-claws'
      parsed.loadout.secondaryWeaponId = null

      const room = parsed.rooms.find(candidate => candidate.id === 'false-apartment')
      if (room) {
        room.obstacles = []
        room.hazards = []
        room.spawnLayouts?.forEach((layout) => {
          layout.enemySpawns = [{ x: 200, y: 260 }]
        })
      }

      const spider = parsed.enemies.find(enemy => enemy.id === 'spider-knife')
      if (spider) {
        spider.maxHp = 99_999
        spider.moveSpeed = 1
        spider.dodge = 0
        spider.idleTurnRadiansPerSecond = 0
        spider.visionRange = 9_999
        spider.visionAngleDegrees = 360
        spider.noticeMs = 1
        spider.alertPauseMs = 1
        spider.attackRange = 9_999
        spider.attackDamage = 1
        spider.attackCooldownMs = 9_999
        spider.attackWindupMs = 1
        spider.targetLockMs = 1
        spider.leapDistance = 1
        spider.leapDurationMs = 1
        spider.tuning = {
          ...(spider.tuning ?? {}),
          captureWindowMs: 5_000,
          captureStunMs: 5_000,
          captureDistance: 9_999,
          captureRearDotMaximum: 1.1,
        }
      }
      response.body = parsed
    })
  })
  cy.visit('/99lc?qa=1&fixture=controls', {
    onBeforeLoad(window) {
      window.localStorage.clear()
      installGamepad(window, makeGamepad())
    },
  })
}

function setGamepadButton(index: number, pressed: boolean, value = pressed ? 1 : 0) {
  mutateGamepad((pad) => {
    pad.buttons[index].pressed = pressed
    pad.buttons[index].value = value
  })
}

function pulseGamepadButton(index: number, holdMs = 60) {
  setGamepadButton(index, true)
  cy.wait(holdMs)
  setGamepadButton(index, false)
  cy.wait(80)
}

function setAnalogTrigger(index: 6 | 7, value: number) {
  setGamepadButton(index, value >= 0.5, value)
  cy.wait(80)
}

function releaseAllGamepadInputs() {
  mutateGamepad((pad) => {
    pad.axes.fill(0)
    for (const button of pad.buttons) {
      button.pressed = false
      button.value = 0
    }
  })
  cy.wait(100)
}

function finishIntroStory() {
  cy.get('.lc-story-card footer span').should('be.visible').invoke('text').then((counter) => {
    const total = Number(counter.split('/').at(-1)?.trim() ?? 0)
    expect(total).to.be.greaterThan(0)
    for (let page = 0; page < total; page += 1) {
      cy.get('.lc-story-card button').then(($button) => {
        ($button[0] as HTMLButtonElement).click()
      })
      cy.wait(30)
    }
  })
}

describe('99 Last Chances controller flow', () => {
  it('enters the opening route on L1/R1 and gates the quest-locked hold follow-up', () => {
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

    finishIntroStory()
    cy.get('.lc-map-backdrop').should('be.visible')
    cy.get('.lc-control-grid article.is-connected')
      .should('have.attr', 'data-gamepad-id', 'DualSense Wireless Controller')
      .and('have.attr', 'data-gamepad-profile', 'standard')
      .and('contain.text', 'DualSense Wireless Controller')
    cy.get('.lc-route-node.is-available.is-gamepad-selected').then(($openingSelection) => {
      const firstNodeId = $openingSelection.attr('data-node-id')

      mutateGamepad((pad) => {
        pad.buttons[4].pressed = true
        pad.buttons[4].value = 1
      })
      cy.get('.lc-control-grid article.is-connected')
        .should('have.attr', 'data-gamepad-status', 'active')
      cy.get('.lc-route-node.is-available.is-gamepad-selected')
        .should(($cycledSelection) => {
          expect($cycledSelection.attr('data-node-id')).not.to.equal(firstNodeId)
        })
      mutateGamepad((pad) => {
        pad.buttons[4].pressed = false
        pad.buttons[4].value = 0
      })
      cy.wait(80)

      mutateGamepad((pad) => {
        pad.buttons[5].pressed = true
        pad.buttons[5].value = 1
      })
      cy.get('.lc-map-backdrop').should('not.exist')
      mutateGamepad((pad) => {
        pad.buttons[5].pressed = false
        pad.buttons[5].value = 0
      })
    })

    // The move-unlock quest chain starts every generation with only tap and hold.
    cy.get('.lc-quest-card').should('be.visible')
    cy.get('.lc-cooldowns').should('contain.text', 'Закрыто квестом')

    mutateGamepad((pad) => {
      pad.buttons[4].pressed = true
      pad.buttons[4].value = 1
    })
    cy.wait(1200)
    mutateGamepad((pad) => {
      pad.buttons[4].pressed = false
      pad.buttons[4].value = 0
    })
    cy.get('.lc-input-feedback').first().should('contain.text', 'Tap once now')
    mutateGamepad((pad) => {
      pad.buttons[4].pressed = true
      pad.buttons[4].value = 1
    })
    cy.wait(60)
    mutateGamepad((pad) => {
      pad.buttons[4].pressed = false
      pad.buttons[4].value = 0
    })

    // The follow-up resolves to the locked holdThenDoubleTap, so no attack fires…
    cy.wait(300)
    cy.get('.lc-gesture-toast').should('not.exist')

    // …while the always-unlocked tap still does.
    mutateGamepad((pad) => {
      pad.buttons[4].pressed = true
      pad.buttons[4].value = 1
    })
    cy.wait(60)
    mutateGamepad((pad) => {
      pad.buttons[4].pressed = false
      pad.buttons[4].value = 0
    })
    cy.wait(400)
    cy.get('.lc-gesture-toast').should('be.visible')
  })

  it('saves an override without disrupting the attempt and applies it only to a fresh generation', () => {
    cy.visit('/99lc', {
      onBeforeLoad(window) {
        window.localStorage.clear()
      },
    })

    finishIntroStory()
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
      finishIntroStory()
      cy.get('.lc-map-backdrop').should('be.visible')
      cy.get('.lc-chance-orb > span').should('have.text', '77')
      cy.contains('.lc-run-card dt', 'Generation').parent().find('dd').should('have.text', '#1')
    })
  })

  it('hot-switches mylorik, DualSense, and DeepList while preserving the live attempt', () => {
    visitDurableGamepadFixture()

    finishIntroStory()
    cy.get('.lc-map-backdrop').should('be.visible')
    cy.get('[data-testid="control-scheme-select"]')
      .find('option')
      .then(($options) => {
        expect([...$options].map(option => option.textContent?.trim())).to.deep.equal([
          'DeepList',
          'mylorik',
          'DualSense',
        ])
      })
    cy.get('[data-testid="control-scheme-select"]')
      .select('mylorik')
      .should('have.value', 'mylorik')
    cy.window().then((window) => {
      expect(window.localStorage.getItem('99lc:control-scheme')).to.equal('mylorik')
    })
    cy.get('[data-testid="control-guide"]').should('contain.text', 'mylorik')
    cy.get('[data-testid="qa-controls-fixture"]')
      .should('contain.text', 'all moves unlocked')

    // mylorik retains the existing planning controls: R1 confirms the highlighted route.
    setGamepadButton(5, true)
    cy.get('.lc-map-backdrop').should('not.exist')
    setGamepadButton(5, false)
    cy.wait(120)

    let roomName = ''
    let chances = ''
    let generation = ''
    cy.get('.lc-room-readout strong').invoke('text').then((value) => { roomName = value })
    cy.get('.lc-chance-orb > span').invoke('text').then((value) => { chances = value })
    cy.contains('.lc-run-card dt', 'Generation').parent().find('dd')
      .invoke('text').then((value) => { generation = value })

    // L1 is the immediate support-hand move: it resolves before button-up.
    setGamepadButton(4, true)
    cy.get('.lc-gesture-toast').should('contain.text', 'Быстрое парирование')
    setGamepadButton(4, false)
    cy.wait(280)

    // A short L2 support technique resolves on release with no classifier delay.
    setGamepadButton(6, true)
    cy.wait(60)
    setGamepadButton(6, false)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Широкий толчок древком')
      .and('not.have.class', 'is-blocked')
    cy.get('.lc-gesture-toast').should('contain.text', 'Широкий толчок древком')
    cy.wait(1_500)

    // Hold L2 to enter the authored support stance, then release to commit it.
    setGamepadButton(6, true)
    cy.wait(700)
    setGamepadButton(6, false)
    cy.get('.lc-gesture-toast').should('contain.text', 'Режущая стойка')
    cy.wait(700)

    // An armed primary technique makes Circle select that physical hand's Mobility route.
    setGamepadButton(7, true)
    cy.wait(1_200)
    setGamepadButton(1, true)
    cy.get('.lc-gesture-toast').should('contain.text', 'Раскрутка копья над головой')
    releaseAllGamepadInputs()

    // Cross is Interact only and cannot synthesize a weapon action without a capture prompt.
    cy.wait(950)
    cy.get('[data-testid="interaction-prompt"]').should('not.exist')
    cy.get('.lc-gesture-toast').should('not.exist')
    pulseGamepadButton(0)
    cy.get('.lc-gesture-toast').should('not.exist')

    cy.get('[data-testid="control-scheme-select"]')
      .select('DualSense')
      .should('have.value', 'dualsense')
    cy.get('[data-testid="dualsense-capability"]')
      .should('contain.text', 'Tier 0')
      .and('contain.text', 'Controls only')
    cy.get('[data-testid="enable-dualsense-features"]').should('be.visible')
    cy.window().then((window) => {
      const testWindow = window as unknown as LastChancesTestWindow
      expect(testWindow.__lastChancesHidRequests).to.equal(0)
      expect(window.localStorage.getItem('99lc:control-scheme')).to.equal('dualsense')
    })
    cy.get('.lc-room-readout strong').should('have.text', roomName)
    cy.get('.lc-chance-orb > span').should('have.text', chances)
    cy.contains('.lc-run-card dt', 'Generation').parent().find('dd').should('have.text', generation)

    // Let the prior stance finish, then prove R1 resolves before release.
    cy.wait(2_000)
    setGamepadButton(5, true)
    cy.get('.lc-gesture-toast').should('contain.text', 'Быстрый рассекающий удар')
    setGamepadButton(5, false)
    cy.wait(320)

    // R2 crosses the primary shallow/charge/ram gates independently.
    setAnalogTrigger(7, 0.3)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Тычок на дистанции')
    setAnalogTrigger(7, 0.55)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Замах и три исхода')
    setAnalogTrigger(7, 0.8)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Заряженный таран копьём')
    cy.wait(700)
    setAnalogTrigger(7, 0)
    cy.get('.lc-gesture-toast').should('contain.text', 'Заряженный таран копьём')

    // L2 enters the support stance and arms/releases its movement finisher at the final gate.
    cy.wait(1_900)
    setAnalogTrigger(6, 0.3)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Широкий толчок древком')
    setAnalogTrigger(6, 0.55)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Режущая стойка')
    setAnalogTrigger(6, 0.92)
    cy.get('[data-testid="semantic-control-cue"]')
      .should('contain.text', 'Олимпийский прыжок с шестом')
    setAnalogTrigger(6, 0)
    cy.get('.lc-gesture-toast').should('contain.text', 'Олимпийский прыжок с шестом')

    // Circle and Cross remain outside DualSense combat grammar.
    cy.wait(950)
    cy.get('.lc-gesture-toast').should('not.exist')
    pulseGamepadButton(1)
    cy.get('.lc-gesture-toast').should('not.exist')
    cy.get('[data-testid="interaction-prompt"]').should('not.exist')
    pulseGamepadButton(0)
    cy.get('.lc-gesture-toast').should('not.exist')

    cy.get('[data-testid="control-scheme-select"]')
      .select('DeepList')
      .should('have.value', 'legacy')
    cy.window().then((window) => {
      expect(window.localStorage.getItem('99lc:control-scheme')).to.equal('legacy')
    })
    cy.get('.lc-room-readout strong').should('have.text', roomName)
    cy.get('.lc-chance-orb > span').should('have.text', chances)
    cy.contains('.lc-run-card dt', 'Generation').parent().find('dd').should('have.text', generation)

    // The original DeepList hold-follow-up resolves from a real second tap.
    setGamepadButton(4, true)
    cy.wait(1_200)
    setGamepadButton(4, false)
    cy.wait(100)
    setGamepadButton(4, true)
    cy.wait(60)
    setGamepadButton(4, false)
    cy.get('.lc-gesture-toast').should('contain.text', 'Раскрутка копья над головой')
  })

  it('requires explicit DualSense focus, captures Knife-spider, and focuses a mandatory choice', () => {
    visitKnifeSpiderCaptureFixture()

    finishIntroStory()
    cy.get('.lc-map-backdrop').should('be.visible')
    cy.get('[data-testid="control-scheme-select"]')
      .select('DualSense')
      .should('have.value', 'dualsense')
    cy.get('.lc-route-node.is-available.is-gamepad-selected').should('not.exist')

    pulseGamepadButton(0)
    cy.get('.lc-map-backdrop').should('be.visible')
    cy.get('.lc-route-node.is-available.is-gamepad-selected').should('not.exist')

    pulseGamepadButton(15)
    cy.get('.lc-route-node.is-available.is-gamepad-selected').should('have.length', 1)
    pulseGamepadButton(0)
    cy.get('.lc-map-backdrop').should('not.exist')

    cy.get('[data-testid="interaction-prompt"]')
      .should('contain.text', 'схватить Нож-паука со спины')
    pulseGamepadButton(0)
    cy.get('.lc-gesture-toast').should('contain.text', 'Нож-паук схвачен со спины')

    cy.get('.lc-interaction-overlay').should('be.visible')
    cy.get('.lc-interaction-choices .is-controller-selected').should('not.exist')
    pulseGamepadButton(0)
    cy.get('.lc-interaction-overlay').should('be.visible')
    cy.get('.lc-interaction-choices .is-controller-selected').should('not.exist')

    pulseGamepadButton(15)
    cy.get('.lc-interaction-choices .is-controller-selected').should('have.length', 1)
    pulseGamepadButton(0)
    cy.get('.lc-interaction-overlay').should('not.exist')
    cy.get('.lc-map-backdrop').should('be.visible')
  })
})
