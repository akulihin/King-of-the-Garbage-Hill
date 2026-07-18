const byId = (id) => {
  const element = document.getElementById(id)
  if (!element) throw new Error(`Missing harness element: ${id}`)
  return element
}

const ui = {
  gamepadStatus: byId('gamepad-status'),
  transportStatus: byId('transport-status'),
  hidStatus: byId('hid-status'),
  neutralStatus: byId('neutral-status'),
  startCapture: byId('start-capture'),
  stopCapture: byId('stop-capture'),
  requestHid: byId('request-hid'),
  neutralize: byId('neutralize'),
  cleanup: byId('cleanup'),
  leftTriggerIndex: byId('left-trigger-index'),
  rightTriggerIndex: byId('right-trigger-index'),
  activationThreshold: byId('activation-threshold'),
  releaseThreshold: byId('release-threshold'),
  mediumGate: byId('medium-gate'),
  deepGate: byId('deep-gate'),
  finalGate: byId('final-gate'),
  leftTriggerValue: byId('left-trigger-value'),
  rightTriggerValue: byId('right-trigger-value'),
  feedbackHand: byId('feedback-hand'),
  feedbackStrength: byId('feedback-strength'),
  neutralConfirmation: byId('neutral-confirmation'),
  confirmNeutral: byId('confirm-neutral'),
  markSupported: byId('mark-supported'),
  exportLog: byId('export-log'),
  clearLog: byId('clear-log'),
  eventLog: byId('event-log'),
}

const thresholdInputs = [
  ui.leftTriggerIndex,
  ui.rightTriggerIndex,
  ui.activationThreshold,
  ui.releaseThreshold,
  ui.mediumGate,
  ui.deepGate,
  ui.finalGate,
]
const semanticButtons = Array.from(document.querySelectorAll('[data-state][data-profile]'))

const state = {
  entries: [],
  sequence: 0,
  captureActive: false,
  captureFrame: null,
  captureConfig: null,
  activePadPresent: false,
  triggerValues: { left: 0, right: 0 },
  transport: null,
  hidDevice: null,
  hidOpenedOnce: false,
  outputBusy: false,
  semanticSequence: 0,
  semanticReportAcks: 0,
  outputReportErrors: 0,
  otherErrors: 0,
  lastCleanupLatencyMs: null,
  lastCleanupSuccessful: false,
  hasTestActivity: false,
  neutralRequired: false,
  neutralPrepared: false,
  neutralConfirmed: false,
  supportMarked: false,
}

function rounded(value, precision = 3) {
  const factor = 10 ** precision
  return Math.round(value * factor) / factor
}

function safeError(error) {
  return {
    name: error && typeof error === 'object' && typeof error.name === 'string'
      ? error.name
      : 'Error',
  }
}

class OutputReportWriteError extends Error {
  constructor() {
    super('A sanitized output report write failed')
    this.name = 'OutputReportWriteError'
  }
}

function logEvent(type, message, details = {}) {
  const entry = {
    sequence: ++state.sequence,
    wallTime: new Date().toISOString(),
    monotonicMs: rounded(performance.now()),
    type,
    message,
    details,
  }
  state.entries.push(entry)
  const line = `${entry.wallTime} +${entry.monotonicMs.toFixed(3)}ms [${type}] ${message}`
    + (Object.keys(details).length ? ` ${JSON.stringify(details)}` : '')
  ui.eventLog.textContent += `${ui.eventLog.textContent ? '\n' : ''}${line}`
  ui.eventLog.scrollTop = ui.eventLog.scrollHeight
  return entry
}

function setStatus(element, text, tone = '') {
  element.textContent = text
  element.className = tone ? `is-${tone}` : ''
}

function markTestActivity(reason) {
  state.hasTestActivity = true
  state.neutralRequired = true
  state.neutralPrepared = false
  state.neutralConfirmed = false
  state.lastCleanupSuccessful = false
  if (state.supportMarked) {
    state.supportMarked = false
    logEvent('support-revoked', 'In-session support mark revoked by new test activity.', { reason })
  }
  ui.neutralConfirmation.checked = false
  setStatus(ui.neutralStatus, 'Neutral output/check required', 'warn')
  updateControls()
}

function canMarkSupported() {
  return Boolean(
    state.transport
    && state.hidOpenedOnce
    && state.semanticReportAcks > 0
    && state.lastCleanupSuccessful
    && state.neutralConfirmed
    && !state.neutralRequired,
  )
}

function updateControls() {
  const webHidReady = window.isSecureContext && 'hid' in navigator
  const hidOpen = Boolean(state.hidDevice?.opened)
  ui.startCapture.disabled = state.captureActive
  ui.stopCapture.disabled = !state.captureActive
  ui.requestHid.disabled = !webHidReady || !state.transport || hidOpen || state.outputBusy
  ui.neutralize.disabled = !state.neutralRequired || state.outputBusy
  ui.cleanup.disabled = !hidOpen || state.outputBusy
  ui.neutralConfirmation.disabled = !state.neutralRequired || !state.neutralPrepared
  ui.confirmNeutral.disabled = ui.neutralConfirmation.disabled || !ui.neutralConfirmation.checked
  ui.exportLog.disabled = !state.hasTestActivity || state.neutralRequired || !state.neutralConfirmed
  ui.markSupported.disabled = !canMarkSupported() || state.supportMarked
  semanticButtons.forEach((button) => { button.disabled = state.outputBusy })
  thresholdInputs.forEach((input) => { input.disabled = state.captureActive })
}

function numberValue(element, name) {
  const value = Number(element.value)
  if (!Number.isFinite(value)) throw new Error(`${name} must be a finite number`)
  return value
}

function readCaptureConfig() {
  const config = {
    leftIndex: numberValue(ui.leftTriggerIndex, 'L2 index'),
    rightIndex: numberValue(ui.rightTriggerIndex, 'R2 index'),
    release: numberValue(ui.releaseThreshold, 'Release threshold'),
    activation: numberValue(ui.activationThreshold, 'Activation threshold'),
    medium: numberValue(ui.mediumGate, 'Medium gate'),
    deep: numberValue(ui.deepGate, 'Deep gate'),
    final: numberValue(ui.finalGate, 'Final gate'),
  }
  if (!Number.isInteger(config.leftIndex) || !Number.isInteger(config.rightIndex)
    || config.leftIndex < 0 || config.rightIndex < 0 || config.leftIndex === config.rightIndex) {
    throw new Error('Trigger indices must be distinct non-negative integers')
  }
  if (!(config.release >= 0
    && config.release < config.activation
    && config.activation <= config.medium
    && config.medium < config.deep
    && config.deep < config.final
    && config.final <= 1)) {
    throw new Error('Expected 0 ≤ release < activation ≤ medium < deep < final ≤ 1')
  }
  return config
}

function clampUnit(value) {
  return Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0))
}

function thresholdTransitions(hand, previous, current, config) {
  const thresholds = [
    { id: 'activation', value: config.activation },
    { id: 'medium', value: config.medium },
    { id: 'deep', value: config.deep },
    { id: 'final', value: config.final },
  ]
  const rising = thresholds
    .filter(gate => previous < gate.value && current >= gate.value)
    .sort((left, right) => left.value - right.value)
  const falling = thresholds
    .filter(gate => previous >= gate.value && current < gate.value)
    .sort((left, right) => right.value - left.value)
  rising.forEach((gate) => {
    markTestActivity('analog-threshold')
    logEvent('gamepad-threshold', `${hand.toUpperCase()} crossed ${gate.id} upward.`, {
      hand,
      threshold: gate.id,
      direction: 'up',
      configuredValue: gate.value,
      observedValue: rounded(current),
    })
  })
  falling.forEach((gate) => {
    markTestActivity('analog-threshold')
    logEvent('gamepad-threshold', `${hand.toUpperCase()} crossed ${gate.id} downward.`, {
      hand,
      threshold: gate.id,
      direction: 'down',
      configuredValue: gate.value,
      observedValue: rounded(current),
    })
  })
  if (previous > config.release && current <= config.release) {
    markTestActivity('analog-release')
    logEvent('gamepad-release', `${hand.toUpperCase()} crossed the release threshold.`, {
      hand,
      threshold: 'release',
      direction: 'down',
      configuredValue: config.release,
      observedValue: rounded(current),
    })
  }
}

function pollGamepad() {
  if (!state.captureActive || !state.captureConfig) return
  const pads = typeof navigator.getGamepads === 'function'
    ? Array.from(navigator.getGamepads()).filter(Boolean)
    : []
  const requiredIndex = Math.max(state.captureConfig.leftIndex, state.captureConfig.rightIndex)
  const pad = pads.find(candidate => candidate.connected && candidate.buttons.length > requiredIndex)
  if (!pad) {
    if (state.activePadPresent) logEvent('gamepad-disconnected', 'Active Gamepad input became unavailable.')
    state.activePadPresent = false
    state.triggerValues = { left: 0, right: 0 }
    ui.leftTriggerValue.textContent = '0.000'
    ui.rightTriggerValue.textContent = '0.000'
    setStatus(ui.gamepadStatus, 'Waiting for anonymous pad', 'warn')
    state.captureFrame = requestAnimationFrame(pollGamepad)
    return
  }
  if (!state.activePadPresent) {
    logEvent('gamepad-connected', 'An anonymous Gamepad input became active.')
    setStatus(ui.gamepadStatus, 'Capturing anonymous pad', 'good')
  }
  state.activePadPresent = true
  const next = {
    left: clampUnit(pad.buttons[state.captureConfig.leftIndex]?.value ?? 0),
    right: clampUnit(pad.buttons[state.captureConfig.rightIndex]?.value ?? 0),
  }
  thresholdTransitions('left', state.triggerValues.left, next.left, state.captureConfig)
  thresholdTransitions('right', state.triggerValues.right, next.right, state.captureConfig)
  state.triggerValues = next
  ui.leftTriggerValue.textContent = next.left.toFixed(3)
  ui.rightTriggerValue.textContent = next.right.toFixed(3)
  state.captureFrame = requestAnimationFrame(pollGamepad)
}

function startCapture() {
  if (state.captureActive) return
  try {
    state.captureConfig = readCaptureConfig()
  } catch (error) {
    state.otherErrors += 1
    logEvent('capture-error', 'Gamepad capture configuration was rejected.', safeError(error))
    setStatus(ui.gamepadStatus, 'Invalid thresholds', 'warn')
    return
  }
  if (typeof navigator.getGamepads !== 'function') {
    logEvent('capture-error', 'This browser does not expose the Gamepad API.')
    setStatus(ui.gamepadStatus, 'Gamepad API unavailable', 'warn')
    return
  }
  state.captureActive = true
  state.activePadPresent = false
  state.triggerValues = { left: 0, right: 0 }
  markTestActivity('capture-start')
  logEvent('capture-start', 'Gamepad analog threshold capture started.', {
    thresholds: {
      release: state.captureConfig.release,
      activation: state.captureConfig.activation,
      medium: state.captureConfig.medium,
      deep: state.captureConfig.deep,
      final: state.captureConfig.final,
    },
  })
  setStatus(ui.gamepadStatus, 'Waiting for anonymous pad', 'warn')
  updateControls()
  state.captureFrame = requestAnimationFrame(pollGamepad)
}

function stopCapture() {
  if (!state.captureActive) return
  state.captureActive = false
  if (state.captureFrame !== null) cancelAnimationFrame(state.captureFrame)
  state.captureFrame = null
  state.activePadPresent = false
  logEvent('capture-stop', 'Gamepad analog threshold capture stopped.')
  setStatus(ui.gamepadStatus, 'Stopped')
  updateControls()
}

function validFilter(filter) {
  return filter
    && Number.isInteger(filter.vendorId)
    && filter.vendorId > 0
    && filter.vendorId <= 0xffff
    && Number.isInteger(filter.productId)
    && filter.productId > 0
    && filter.productId <= 0xffff
}

function installVerifiedTransport(candidate) {
  if (state.hidDevice?.opened) throw new Error('Close the current HID session before replacing the transport')
  if (!candidate || typeof candidate !== 'object') throw new Error('Transport must be an object')
  if (candidate.transport !== 'usb' && candidate.transport !== 'bluetooth') {
    throw new Error('transport must be usb or bluetooth')
  }
  if (!Array.isArray(candidate.filters) || candidate.filters.length === 0
    || !candidate.filters.every(validFilter)) {
    throw new Error('filters must contain physically verified vendorId/productId pairs')
  }
  if (typeof candidate.supports !== 'function'
    || typeof candidate.serialize !== 'function'
    || typeof candidate.neutral !== 'function') {
    throw new Error('supports, serialize and neutral must be functions')
  }
  state.transport = {
    transport: candidate.transport,
    filters: candidate.filters.map(filter => ({
      vendorId: filter.vendorId,
      productId: filter.productId,
    })),
    supports: candidate.supports,
    serialize: candidate.serialize,
    neutral: candidate.neutral,
  }
  setStatus(ui.transportStatus, `Verified ${candidate.transport.toUpperCase()} hook`, 'good')
  logEvent('transport-installed', 'A verified in-memory transport hook was installed.', {
    transport: candidate.transport,
  })
  updateControls()
  return true
}

Object.defineProperty(window, 'install99LcVerifiedDualSenseHarnessTransport', {
  configurable: false,
  enumerable: true,
  writable: false,
  value: installVerifiedTransport,
})

function normalizedPackets(value) {
  if (!Array.isArray(value) || value.length === 0) {
    throw new Error('Verified serializer returned no packets')
  }
  return value.map((packet) => {
    if (!packet || !Number.isInteger(packet.reportId) || packet.reportId < 0 || packet.reportId > 0xff) {
      throw new Error('Verified serializer returned an invalid report ID')
    }
    let data
    if (packet.data instanceof Uint8Array) data = packet.data
    else if (packet.data instanceof ArrayBuffer) data = new Uint8Array(packet.data)
    else if (ArrayBuffer.isView(packet.data)) {
      data = new Uint8Array(packet.data.buffer, packet.data.byteOffset, packet.data.byteLength)
    } else {
      throw new Error('Verified serializer returned non-binary report data')
    }
    if (data.byteLength === 0 || data.byteLength > 1024) {
      throw new Error('Verified serializer returned an unsafe report length')
    }
    return { reportId: packet.reportId, data }
  })
}

async function writePackets(packets, purpose) {
  const device = state.hidDevice
  if (!device?.opened) throw new Error('No HID device is open')
  for (let index = 0; index < packets.length; index += 1) {
    const packet = packets[index]
    const startedAt = performance.now()
    try {
      await device.sendReport(packet.reportId, packet.data)
      const latencyMs = performance.now() - startedAt
      if (purpose === 'semantic') state.semanticReportAcks += 1
      logEvent('output-report-ack', 'The browser acknowledged an output report write.', {
        purpose,
        packetOrdinal: index + 1,
        packetCount: packets.length,
        byteLength: packet.data.byteLength,
        latencyMs: rounded(latencyMs),
      })
    } catch (error) {
      state.outputReportErrors += 1
      logEvent('output-report-error', 'An output report write failed.', {
        purpose,
        packetOrdinal: index + 1,
        ...safeError(error),
      })
      throw new OutputReportWriteError()
    }
  }
}

async function requestAndOpenHid() {
  if (state.outputBusy || !state.transport) return
  if (!window.isSecureContext || !('hid' in navigator)) {
    logEvent('hid-unavailable', 'WebHID is unavailable outside a supported secure context.')
    return
  }
  state.outputBusy = true
  updateControls()
  const requestedAt = performance.now()
  logEvent('hid-permission-request', 'The tester explicitly opened the WebHID chooser.', {
    transport: state.transport.transport,
  })
  try {
    const devices = await navigator.hid.requestDevice({ filters: state.transport.filters })
    logEvent('hid-permission-result', 'The WebHID chooser returned.', {
      selected: devices.length > 0,
      latencyMs: rounded(performance.now() - requestedAt),
    })
    let device = null
    for (const candidate of devices) {
      try {
        if (state.transport.supports(candidate)) {
          device = candidate
          break
        }
      } catch (error) {
        state.otherErrors += 1
        logEvent('hid-support-check-error', 'The injected support check failed.', safeError(error))
      }
    }
    if (!device) {
      setStatus(ui.hidStatus, devices.length ? 'No verified match' : 'Permission not granted', 'warn')
      logEvent('hid-open-skipped', 'No selected device matched the injected verified transport.')
      return
    }
    const openStartedAt = performance.now()
    await device.open()
    state.hidDevice = device
    state.hidOpenedOnce = true
    setStatus(ui.hidStatus, 'Anonymous verified HID open', 'good')
    logEvent('hid-open-ack', 'The verified HID session opened.', {
      transport: state.transport.transport,
      latencyMs: rounded(performance.now() - openStartedAt),
    })
  } catch (error) {
    state.otherErrors += 1
    setStatus(ui.hidStatus, 'Permission/open error', 'warn')
    logEvent('hid-permission-open-error', 'WebHID permission or open failed.', safeError(error))
  } finally {
    state.outputBusy = false
    updateControls()
  }
}

async function runSemanticTest(button) {
  if (state.outputBusy) return
  let strength
  try {
    strength = clampUnit(numberValue(ui.feedbackStrength, 'Feedback strength'))
  } catch (error) {
    state.otherErrors += 1
    logEvent('semantic-input-error', 'The semantic feedback strength was invalid.', safeError(error))
    return
  }
  const semanticEvent = Object.freeze({
    sequence: ++state.semanticSequence,
    state: button.dataset.state,
    profile: button.dataset.profile,
    hand: ui.feedbackHand.value,
    strength,
    atMonotonicMs: rounded(performance.now()),
  })
  markTestActivity('semantic-feedback')
  logEvent('semantic-feedback-event', 'A semantic feedback test event was requested.', semanticEvent)
  if (!state.transport || !state.hidDevice?.opened) {
    logEvent('semantic-output-skipped', 'No verified open output transport; semantic event remained controls-only.', {
      sequence: semanticEvent.sequence,
    })
    return
  }
  state.outputBusy = true
  updateControls()
  try {
    const packets = normalizedPackets(state.transport.serialize(semanticEvent))
    await writePackets(packets, 'semantic')
  } catch (error) {
    if (!(error instanceof OutputReportWriteError)) state.otherErrors += 1
    logEvent('semantic-output-error', 'The verified semantic serializer/output path failed.', safeError(error))
  } finally {
    state.outputBusy = false
    updateControls()
  }
}

async function prepareNeutralCheck(closeAfter = false, reason = 'manual') {
  if (state.outputBusy) return
  state.outputBusy = true
  state.neutralPrepared = false
  state.lastCleanupSuccessful = false
  updateControls()
  const startedAt = performance.now()
  let successful = true
  logEvent('cleanup-start', closeAfter ? 'Neutral output and close started.' : 'Neutral output started.', { reason })
  try {
    if (state.hidDevice?.opened && state.transport) {
      const packets = normalizedPackets(state.transport.neutral())
      await writePackets(packets, 'neutral')
    } else {
      logEvent('neutral-output-skipped', 'No verified HID output was open; proceeding to the physical neutral check.')
    }
  } catch (error) {
    successful = false
    if (!(error instanceof OutputReportWriteError)) state.otherErrors += 1
    logEvent('neutral-output-error', 'Neutral output failed; physically disconnect if needed.', safeError(error))
  }
  if (closeAfter && state.hidDevice?.opened) {
    try {
      const closeStartedAt = performance.now()
      await state.hidDevice.close()
      logEvent('hid-close-ack', 'The anonymous HID session closed.', {
        latencyMs: rounded(performance.now() - closeStartedAt),
      })
    } catch (error) {
      successful = false
      state.otherErrors += 1
      logEvent('hid-close-error', 'The HID close operation failed.', safeError(error))
    } finally {
      state.hidDevice = null
      setStatus(ui.hidStatus, 'Closed')
    }
  }
  state.lastCleanupLatencyMs = performance.now() - startedAt
  state.lastCleanupSuccessful = successful
  state.neutralPrepared = true
  setStatus(
    ui.neutralStatus,
    successful ? 'Awaiting physical confirmation' : 'Cleanup error; inspect physically',
    'warn',
  )
  logEvent('cleanup-finished', 'Cleanup attempt finished; human trigger inspection is still required.', {
    closeAfter,
    successful,
    latencyMs: rounded(state.lastCleanupLatencyMs),
  })
  state.outputBusy = false
  updateControls()
}

function confirmNeutral() {
  if (!state.neutralRequired || !state.neutralPrepared || !ui.neutralConfirmation.checked) return
  state.neutralRequired = false
  state.neutralConfirmed = true
  setStatus(ui.neutralStatus, 'Human-confirmed neutral', 'good')
  logEvent('human-neutral-confirmation', 'Tester confirmed both physical triggers are neutral.', {
    cleanupSuccessful: state.lastCleanupSuccessful,
    cleanupLatencyMs: state.lastCleanupLatencyMs === null ? null : rounded(state.lastCleanupLatencyMs),
  })
  updateControls()
}

function markSupported() {
  if (!canMarkSupported()) return
  state.supportMarked = true
  logEvent('transport-support-mark', 'Tester marked this verified transport supported for this export.', {
    transport: state.transport.transport,
    semanticReportAcknowledgements: state.semanticReportAcks,
  })
  updateControls()
}

function exportLog() {
  if (state.neutralRequired || !state.neutralConfirmed || !state.hasTestActivity) return
  const payload = {
    schemaVersion: 1,
    harness: '99lc-dualsense-real-device-qa',
    exportedAt: new Date().toISOString(),
    supportMarked: state.supportMarked,
    transport: state.transport?.transport ?? null,
    summary: {
      semanticReportAcknowledgements: state.semanticReportAcks,
      outputReportErrors: state.outputReportErrors,
      otherErrors: state.otherErrors,
      lastCleanupSuccessful: state.lastCleanupSuccessful,
      lastCleanupLatencyMs: state.lastCleanupLatencyMs === null
        ? null
        : rounded(state.lastCleanupLatencyMs),
      humanNeutralConfirmed: true,
    },
    capture: state.captureConfig
      ? {
          triggerButtonIndices: {
            left: state.captureConfig.leftIndex,
            right: state.captureConfig.rightIndex,
          },
          thresholds: {
            release: state.captureConfig.release,
            activation: state.captureConfig.activation,
            medium: state.captureConfig.medium,
            deep: state.captureConfig.deep,
            final: state.captureConfig.final,
          },
        }
      : null,
    events: state.entries,
  }
  const blob = new Blob([`${JSON.stringify(payload, null, 2)}\n`], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `99lc-dualsense-qa-${new Date().toISOString().slice(0, 10)}.json`
  link.click()
  URL.revokeObjectURL(url)
  logEvent('export-complete', 'A sanitized local JSON log was exported.', {
    supportMarked: state.supportMarked,
  })
}

function clearLog() {
  state.entries.splice(0, state.entries.length)
  state.sequence = 0
  ui.eventLog.textContent = ''
  logEvent('log-cleared', 'The in-memory event log was cleared by the tester.')
}

ui.startCapture.addEventListener('click', startCapture)
ui.stopCapture.addEventListener('click', stopCapture)
ui.requestHid.addEventListener('click', requestAndOpenHid)
ui.neutralize.addEventListener('click', () => prepareNeutralCheck(false, 'manual-neutral'))
ui.cleanup.addEventListener('click', () => prepareNeutralCheck(true, 'manual-close'))
ui.neutralConfirmation.addEventListener('change', updateControls)
ui.confirmNeutral.addEventListener('click', confirmNeutral)
ui.markSupported.addEventListener('click', markSupported)
ui.exportLog.addEventListener('click', exportLog)
ui.clearLog.addEventListener('click', clearLog)
semanticButtons.forEach((button) => {
  button.addEventListener('click', () => runSemanticTest(button))
})

if ('hid' in navigator) {
  navigator.hid.addEventListener('disconnect', (event) => {
    if (event.device !== state.hidDevice) return
    state.hidDevice = null
    state.lastCleanupSuccessful = false
    markTestActivity('hid-disconnect')
    setStatus(ui.hidStatus, 'Disconnected', 'warn')
    logEvent('hid-disconnect', 'The anonymous verified HID disconnected; physical neutral inspection is required.')
  })
}

window.addEventListener('pagehide', () => {
  stopCapture()
  const device = state.hidDevice
  const transport = state.transport
  if (!device?.opened || !transport) return
  try {
    const packets = normalizedPackets(transport.neutral())
    packets.forEach((packet) => { void device.sendReport(packet.reportId, packet.data) })
  } catch {
    // Best-effort unload cleanup cannot replace the required physical confirmation.
  }
  void device.close()
})

setStatus(
  ui.hidStatus,
  !window.isSecureContext
    ? 'Secure context required'
    : 'hid' in navigator ? 'Ready; no request made' : 'WebHID unavailable',
  window.isSecureContext && 'hid' in navigator ? '' : 'warn',
)
logEvent('harness-ready', 'The isolated harness loaded without requesting HID permission.', {
  secureContext: window.isSecureContext,
  gamepadApi: typeof navigator.getGamepads === 'function',
  webHid: 'hid' in navigator,
})
updateControls()
