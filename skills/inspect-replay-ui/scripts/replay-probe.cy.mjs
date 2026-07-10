const replayUrl = Cypress.env('replayUrl')
const outputDir = Cypress.env('outputDir')
const viewerId = String(Cypress.env('viewerId') || '1')
const includeReplayJson = String(Cypress.env('includeReplayJson')) === 'true'
const encodedActions = String(Cypress.env('actionsBase64') || '')
const decodeBase64Utf8 = value => decodeURIComponent(
  [...atob(value)].map(character => `%${character.charCodeAt(0).toString(16).padStart(2, '0')}`).join('')
)
const actions = encodedActions
  ? JSON.parse(decodeBase64Utf8(encodedActions))
  : []

const runtime = {
  console: [],
  uncaughtErrors: [],
  failedRequests: [],
  replayJson: null,
  executedActions: [],
}

function compactText(value, limit = 10000) {
  const text = String(value || '').replace(/\s+/g, ' ').trim()
  return text.length > limit ? `${text.slice(0, limit)}…` : text
}

function printable(value) {
  if (typeof value === 'string') return value
  if (value instanceof Error) return `${value.name}: ${value.message}`
  try {
    return JSON.stringify(value)
  } catch {
    return String(value)
  }
}

function actionParts(raw) {
  const separator = raw.indexOf(':')
  if (separator < 0) return { kind: raw.trim(), value: '' }
  return {
    kind: raw.slice(0, separator).trim(),
    value: raw.slice(separator + 1).trim(),
  }
}

function clickRound(target) {
  return cy.get('.round-badge').invoke('text').then(text => {
    const match = text.match(/Round\s+(\d+)/i)
    if (!match) throw new Error(`Cannot read current round from: ${text}`)
    const current = Number(match[1])
    if (current === target) return
    const buttonIndex = current < target ? 1 : 0
    cy.get('.round-nav button').eq(buttonIndex).should('not.be.disabled').click()
    cy.get('.round-badge').should('not.contain.text', `Round ${current} /`)
    return clickRound(target)
  })
}

function clickByText(text) {
  return cy.contains('button, [role="button"], .player-avatar-btn', text, { matchCase: false })
    .should('be.visible')
    .click()
}

function runAction(raw) {
  const { kind, value } = actionParts(raw)
  runtime.executedActions.push(raw)
  switch (kind) {
    case 'round': return clickRound(Number(value))
    case 'next-round': return cy.get('.round-nav button').eq(1).should('not.be.disabled').click()
    case 'prev-round': return cy.get('.round-nav button').eq(0).should('not.be.disabled').click()
    case 'player': return cy.get('.player-avatar-btn').eq(Number(value)).should('be.visible').click()
    case 'player-name': return cy.contains('.player-avatar-btn', value, { matchCase: false }).click()
    case 'tab': return cy.contains('.fa-tab', value, { matchCase: false }).click()
    case 'fight': return cy.get('.fa-thumb').eq(Number(value)).should('be.visible').click()
    case 'all-fight': return cy.get('.fa-all-row').eq(Number(value)).should('be.visible').click()
    case 'play': return cy.get('.fa-controls .fa-btn').eq(0).should('be.visible').click()
    case 'skip': return cy.get('.fa-btn[title="Skip to end"]').should('be.visible').click()
    case 'speed': return cy.contains('.fa-speed-btn', `${value}x`).click()
    case 'click': return clickByText(value)
    case 'selector': return cy.get(value).should('be.visible').click()
    case 'wait': return cy.wait(Number(value))
    case 'snapshot': return cy.screenshot(value || 'snapshot', { capture: 'fullPage' })
    default: throw new Error(`Unknown action: ${raw}`)
  }
}

function isVisible(element) {
  const style = element.ownerDocument.defaultView.getComputedStyle(element)
  const rect = element.getBoundingClientRect()
  return style.display !== 'none' && style.visibility !== 'hidden' && Number(style.opacity) !== 0 && rect.width > 0 && rect.height > 0
}

function elementSummary(element) {
  const rect = element.getBoundingClientRect()
  return {
    tag: element.tagName.toLowerCase(),
    text: compactText(element.innerText || element.getAttribute('aria-label') || element.getAttribute('title'), 300),
    className: typeof element.className === 'string' ? element.className : '',
    ariaLabel: element.getAttribute('aria-label'),
    title: element.getAttribute('title'),
    disabled: Boolean(element.disabled || element.getAttribute('aria-disabled') === 'true'),
    rect: {
      x: Math.round(rect.x),
      y: Math.round(rect.y),
      width: Math.round(rect.width),
      height: Math.round(rect.height),
    },
  }
}

function regionText(document, selector) {
  const element = document.querySelector(selector)
  return element && isVisible(element) ? compactText(element.innerText, 20000) : null
}

function collectSummary(document) {
  const interactive = [...document.querySelectorAll('button, a, select, input, [role="button"], .player-avatar-btn')]
    .filter(isVisible)
    .map(elementSummary)

  const overflows = [...document.querySelectorAll('body *')]
    .filter(element => isVisible(element) && (element.scrollWidth > element.clientWidth + 2 || element.scrollHeight > element.clientHeight + 2))
    .slice(0, 150)
    .map(element => ({
      ...elementSummary(element),
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
      clientHeight: element.clientHeight,
      scrollHeight: element.scrollHeight,
    }))

  const regions = {
    header: regionText(document, '.game-header'),
    round: regionText(document, '.round-nav'),
    playerSelector: regionText(document, '.player-selector'),
    selectedPlayer: regionText(document, '.player-avatar-btn.active'),
    selectedIdentity: regionText(document, '.gr-identity'),
    playerCard: regionText(document, '.game-left'),
    fightPanel: regionText(document, '.fight-panel'),
    activeFightTab: regionText(document, '.fa-tab.active'),
    leaderboard: regionText(document, '.leaderboard, .leaderboard-container'),
    skills: regionText(document, '.game-right'),
    currentLogs: regionText(document, '.events-panel'),
    directMessages: regionText(document, '.direct-messages'),
  }

  return {
    capturedAt: new Date().toISOString(),
    requestedUrl: replayUrl,
    finalUrl: document.location.href,
    title: document.title,
    viewport: { width: window.innerWidth, height: window.innerHeight },
    layoutReady: Boolean(document.querySelector('.game-layout')),
    terminalMessage: regionText(document, '.loading'),
    regions,
    playerButtons: [...document.querySelectorAll('.player-avatar-btn')].map(elementSummary),
    fightTabs: [...document.querySelectorAll('.fa-tab')].map(elementSummary),
    interactive,
    overflowCandidates: overflows,
    console: runtime.console,
    uncaughtErrors: runtime.uncaughtErrors,
    failedRequests: runtime.failedRequests,
    executedActions: runtime.executedActions,
    outputDir,
  }
}

Cypress.on('uncaught:exception', error => {
  runtime.uncaughtErrors.push({ message: error.message, stack: error.stack || '' })
  return false
})

describe('rendered replay UI probe', () => {
  it('drives gameplay elements and captures evidence', () => {
    cy.intercept('**', request => {
      request.on('response', response => {
        if (response.statusCode >= 400) {
          runtime.failedRequests.push({ method: request.method, url: request.url, status: response.statusCode })
        }
        if (includeReplayJson && /\/api\/game\/replay\//.test(request.url) && response.statusCode < 400) {
          runtime.replayJson = response.body
        }
      })
    })

    cy.visit(replayUrl, {
      failOnStatusCode: false,
      onBeforeLoad(window) {
        window.localStorage.setItem('discordId', viewerId)
        for (const level of ['error', 'warn']) {
          const original = window.console[level].bind(window.console)
          window.console[level] = (...args) => {
            runtime.console.push({ level, message: args.map(printable).join(' ') })
            original(...args)
          }
        }
        window.addEventListener('unhandledrejection', event => {
          runtime.uncaughtErrors.push({ message: printable(event.reason), type: 'unhandledrejection' })
        })
      },
    })

    cy.get('body', { timeout: 60000 }).should($body => {
      const ready = $body.find('.game-layout').length > 0
      const message = compactText($body.find('.loading').text())
      const terminalError = message && message !== 'Loading replay...'
      expect(ready || terminalError, 'replay layout or terminal replay error').to.eq(true)
    })

    cy.get('body').then($body => {
      if (!$body.find('.game-layout').length) return
      for (const action of actions) {
        cy.then(() => runAction(action))
      }
      cy.wait(500)
    })

    cy.screenshot('page-full', { capture: 'fullPage' })
    const componentShots = [
      ['player-card', '.game-left'],
      ['fight-panel', '.fight-panel'],
      ['leaderboard', '.leaderboard, .leaderboard-container'],
      ['skills', '.game-right'],
    ]
    for (const [name, selector] of componentShots) {
      cy.get('body').then($body => {
        if ($body.find(selector).length) cy.get(selector).first().screenshot(name)
      })
    }

    cy.document().then(document => {
      const summary = collectSummary(document)
      cy.task('writeEvidence', { filename: 'summary.json', content: JSON.stringify(summary, null, 2) })
      cy.task('writeEvidence', { filename: 'visible-text.txt', content: document.body.innerText || '' })
      cy.task('writeEvidence', { filename: 'page.html', content: document.documentElement.outerHTML })
      if (includeReplayJson && runtime.replayJson !== null) {
        cy.task('writeEvidence', { filename: 'replay.json', content: JSON.stringify(runtime.replayJson, null, 2) })
      }
    })
  })
})
