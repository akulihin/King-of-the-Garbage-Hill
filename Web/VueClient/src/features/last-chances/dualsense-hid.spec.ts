import { describe, expect, it } from 'vitest'
import { BLUETOOTH_INPUT_REPORT_ID, type LastChancesHidInputReportEventLike } from './dualsense-input'
import type { LastChancesFeedbackEffect, LastChancesTriggerBaseline } from './feedback'
import {
  BLUETOOTH_INPUT_RETAINED_MESSAGE,
  DualSenseWebHidDriver,
  type DualSenseHidPacket,
  type DualSenseHidTransport,
  type DualSenseHidTransportSerializer,
  type LastChancesHidChooserLike,
  type LastChancesHidDeviceLike,
} from './dualsense-hid'

class FakeHidDevice implements LastChancesHidDeviceLike {
  opened = false
  readonly log: string[] = []
  readonly inputListeners = new Set<(event: LastChancesHidInputReportEventLike) => void>()
  failReportId: number | null = null
  private hasFailed = false

  constructor(
    readonly transport: DualSenseHidTransport,
    readonly allowed = true,
  ) {}

  async open(): Promise<void> {
    this.log.push('open')
    this.opened = true
  }

  async close(): Promise<void> {
    this.log.push('close')
    this.opened = false
  }

  async sendReport(reportId: number, data: Uint8Array): Promise<void> {
    this.log.push(`send:${reportId}:${[...data].join(',')}`)
    if (reportId === this.failReportId && !this.hasFailed) {
      this.hasFailed = true
      throw new Error(`write ${reportId} failed`)
    }
    await Promise.resolve()
  }

  addEventListener(
    _type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void {
    this.inputListeners.add(listener)
  }

  removeEventListener(
    _type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void {
    this.inputListeners.delete(listener)
  }

  emitInput(reportId: number, faceBits = 0): void {
    const payload = new Uint8Array(11)
    payload[1] = 128
    payload[2] = 128
    payload[3] = 128
    payload[4] = 128
    payload[8] = faceBits | 0x08
    const event = { reportId, data: new DataView(payload.buffer) }
    for (const listener of this.inputListeners) listener(event)
  }
}

class FakeChooser implements LastChancesHidChooserLike {
  requests = 0

  constructor(
    private readonly devices: readonly LastChancesHidDeviceLike[] = [],
    private readonly rejection: Error | null = null,
  ) {}

  async requestDevice(): Promise<readonly LastChancesHidDeviceLike[]> {
    this.requests += 1
    if (this.rejection) throw this.rejection
    return this.devices
  }
}

class FakeSerializer implements DualSenseHidTransportSerializer {
  constructor(readonly transport: DualSenseHidTransport) {}

  supports(device: LastChancesHidDeviceLike): boolean {
    return device instanceof FakeHidDevice && device.transport === this.transport
  }

  serialize(effect: LastChancesFeedbackEffect): readonly DualSenseHidPacket[] {
    const prefix = this.transport === 'usb' ? 0x02 : 0x32
    const hand = effect.hand === 'left' ? 1 : effect.hand === 'right' ? 2 : 3
    return [
      { reportId: prefix, data: new Uint8Array([hand, Math.round(effect.magnitude * 100)]) },
      { reportId: prefix + 1, data: new Uint8Array([effect.priority]) },
    ]
  }

  neutral(): readonly DualSenseHidPacket[] {
    const reportId = this.transport === 'usb' ? 0x01 : 0x31
    return [{ reportId, data: new Uint8Array([0, 0, 0]) }]
  }

  baseline(state: LastChancesTriggerBaseline): readonly DualSenseHidPacket[] {
    const reportId = this.transport === 'usb' ? 0x01 : 0x31
    const encode = (profile: LastChancesTriggerBaseline['left']) =>
      profile ? Math.round(profile.resistance * 100) : 0
    return [{ reportId, data: new Uint8Array([9, encode(state.left), encode(state.right)]) }]
  }
}

function effect(magnitude = 0.6): LastChancesFeedbackEffect {
  return {
    state: 'impact',
    profile: 'impact',
    hand: 'right',
    magnitude,
    durationMs: 100,
    priority: 100,
    adaptiveProfile: {
      startPosition: 0.2,
      endPosition: 0.8,
      resistance: 0.4,
      force: 0.5,
      transitionMs: 40,
      effectMs: 100,
      magnitude,
    },
  }
}

function driver(
  chooser: LastChancesHidChooserLike | null,
  serializers: readonly DualSenseHidTransportSerializer[] = [
    new FakeSerializer('usb'),
    new FakeSerializer('bluetooth'),
  ],
): DualSenseWebHidDriver {
  return new DualSenseWebHidDriver({
    hid: chooser,
    filters: [{ vendorId: 0x1234, productId: 0xabcd }],
    isAllowedDevice: candidate => candidate instanceof FakeHidDevice && candidate.allowed,
    serializers,
  })
}

describe('isolated DualSense WebHID driver', () => {
  it('never opens the chooser or advertises Tier 2 before the explicit enable action', () => {
    const chooser = new FakeChooser([new FakeHidDevice('usb')])
    const output = driver(chooser)
    expect(chooser.requests).toBe(0)
    expect(output.capability()).toMatchObject({
      tier: 0,
      status: 'controls-only',
      permission: 'not-requested',
    })
  })

  it('preserves exact USB packet ordering and sends neutral packets on both ends', async () => {
    const device = new FakeHidDevice('usb')
    const chooser = new FakeChooser([device])
    const output = driver(chooser)

    expect(await output.enableEnhancedFeatures()).toBe(true)
    expect(chooser.requests).toBe(1)
    expect(output.capability()).toMatchObject({ tier: 2, status: 'enhanced', permission: 'granted' })
    expect(output.activeTransport).toBe('usb')
    expect(await output.play(effect())).toBe(true)
    await output.disableEnhancedFeatures()

    expect(device.log).toEqual([
      'open',
      'send:1:0,0,0',
      'send:2:2,60',
      'send:3:100',
      'send:1:0,0,0',
      'close',
    ])
    expect(output.capability()).toMatchObject({ tier: 0, status: 'controls-only' })
  })

  it('keeps USB and Bluetooth serializers as distinct injected boundaries', async () => {
    const usb = new FakeHidDevice('usb')
    const bluetooth = new FakeHidDevice('bluetooth')
    const usbOutput = driver(new FakeChooser([usb]))
    const bluetoothOutput = driver(new FakeChooser([bluetooth]))

    await usbOutput.enableEnhancedFeatures()
    await bluetoothOutput.enableEnhancedFeatures()
    await Promise.all([usbOutput.play(effect(0.4)), bluetoothOutput.play(effect(0.4))])
    expect(usb.log.slice(1)).toEqual(['send:1:0,0,0', 'send:2:2,40', 'send:3:100'])
    expect(bluetooth.log.slice(1)).toEqual(['send:49:0,0,0', 'send:50:2,40', 'send:51:100'])
  })

  it('treats chooser denial as a controls-only fallback', async () => {
    const denied = new Error('No device selected')
    denied.name = 'NotFoundError'
    const chooser = new FakeChooser([], denied)
    const output = driver(chooser)
    expect(await output.enableEnhancedFeatures()).toBe(false)
    expect(output.capability()).toMatchObject({
      tier: 0,
      status: 'controls-only',
      permission: 'denied',
    })
  })

  it('does not prompt when no verified serializer is installed', async () => {
    const chooser = new FakeChooser([new FakeHidDevice('usb')])
    const output = driver(chooser, [])
    expect(output.capability()).toMatchObject({ tier: 0, status: 'unavailable' })
    expect(await output.enableEnhancedFeatures()).toBe(false)
    expect(chooser.requests).toBe(0)
  })

  it('rejects a selected device outside the injected allowlist without opening it', async () => {
    const device = new FakeHidDevice('usb', false)
    const output = driver(new FakeChooser([device]))
    expect(await output.enableEnhancedFeatures()).toBe(false)
    expect(device.log).toEqual([])
    expect(output.capability()).toMatchObject({
      tier: 0,
      status: 'unavailable',
      permission: 'unavailable',
    })
  })

  it('attempts a neutral stop and closes after a failed output write', async () => {
    const device = new FakeHidDevice('usb')
    device.failReportId = 0x02
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    expect(await output.play(effect())).toBe(false)
    expect(device.log).toEqual([
      'open',
      'send:1:0,0,0',
      'send:2:2,60',
      'send:1:0,0,0',
      'close',
    ])
    expect(output.capability()).toMatchObject({ tier: 0, status: 'error' })
    expect(output.capability().message).toContain('write 2 failed')
  })

  it('serializes concurrent writes without interleaving their packet sequences', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()
    await Promise.all([output.play(effect(0.2)), output.play(effect(0.7))])
    expect(device.log.slice(2)).toEqual([
      'send:2:2,20',
      'send:3:100',
      'send:2:2,70',
      'send:3:100',
    ])
  })
})

describe('resting trigger baseline', () => {
  it('writes the recorded baseline once and skips deep-equal duplicates', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    const profile = effect().adaptiveProfile
    await output.setBaseline({ left: profile, right: null })
    await output.setBaseline({ left: { ...profile }, right: null })
    expect(device.log).toEqual(['open', 'send:1:0,0,0', 'send:1:9,40,0'])
  })

  it('re-writes the baseline after an effect stomps the trigger state', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    const profile = effect().adaptiveProfile
    await output.setBaseline({ left: profile, right: null })
    await output.play(effect(0.5))
    await output.writeBaseline()
    expect(device.log.slice(2)).toEqual([
      'send:1:9,40,0',
      'send:2:2,50',
      'send:3:100',
      'send:1:9,40,0',
    ])
  })

  it('records a baseline set before enable and writes it once output activates', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))

    const profile = effect().adaptiveProfile
    await output.setBaseline({ left: null, right: profile })
    expect(device.log).toEqual([])
    await output.enableEnhancedFeatures()
    expect(device.log).toEqual(['open', 'send:1:0,0,0', 'send:1:9,0,40'])
  })

  it('restores the armed baseline when enhanced output re-activates on a retained pad', async () => {
    const device = new FakeHidDevice('bluetooth')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()
    const profile = effect().adaptiveProfile
    await output.setBaseline({ left: null, right: profile })
    await output.disableEnhancedFeatures()

    await output.enableEnhancedFeatures()
    expect(device.log[device.log.length - 1]).toBe('send:49:9,0,40')
    expect(output.capability()).toMatchObject({ tier: 2, status: 'enhanced' })
  })

  it('serializes baseline writes behind in-flight effect writes', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    const profile = effect().adaptiveProfile
    await Promise.all([
      output.play(effect(0.3)),
      output.setBaseline({ left: profile, right: null }),
    ])
    expect(device.log.slice(2)).toEqual([
      'send:2:2,30',
      'send:3:100',
      'send:1:9,40,0',
    ])
  })

  it('demotes like a failed play when the baseline write fails', async () => {
    const device = new FakeHidDevice('usb')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    device.failReportId = 0x01
    await output.setBaseline({ left: effect().adaptiveProfile, right: null })
    expect(output.capability()).toMatchObject({ tier: 0, status: 'error' })
    expect(output.capability().message).toContain('write 1 failed')
    expect(device.opened).toBe(false)
  })
})

describe('Bluetooth extended-mode input retention (M118)', () => {
  it('recovers controller input from 0x31 input reports over Bluetooth only', async () => {
    const bluetooth = new FakeHidDevice('bluetooth')
    const bluetoothOutput = driver(new FakeChooser([bluetooth]))
    await bluetoothOutput.enableEnhancedFeatures()
    expect(bluetoothOutput.hidInputSnapshot()).toBeNull()
    bluetooth.emitInput(BLUETOOTH_INPUT_REPORT_ID, 0x20)
    const snapshot = bluetoothOutput.hidInputSnapshot()
    expect(snapshot?.mapping).toBe('standard')
    expect(snapshot?.buttons[0].pressed).toBe(true)

    const usb = new FakeHidDevice('usb')
    const usbOutput = driver(new FakeChooser([usb]))
    await usbOutput.enableEnhancedFeatures()
    usb.emitInput(BLUETOOTH_INPUT_REPORT_ID, 0x20)
    expect(usbOutput.hidInputSnapshot()).toBeNull()
  })

  it('keeps the Bluetooth device open input-only when enhanced output is disabled', async () => {
    const device = new FakeHidDevice('bluetooth')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()
    await output.disableEnhancedFeatures()

    expect(device.log).toEqual(['open', 'send:49:0,0,0', 'send:49:0,0,0'])
    expect(device.opened).toBe(true)
    expect(output.capability()).toMatchObject({
      tier: 0,
      status: 'controls-only',
      permission: 'granted',
    })
    expect(output.capability().message).toBe(BLUETOOTH_INPUT_RETAINED_MESSAGE)
    expect(await output.play(effect())).toBe(false)
    device.emitInput(BLUETOOTH_INPUT_REPORT_ID, 0x20)
    expect(output.hidInputSnapshot()?.buttons[0].pressed).toBe(true)
  })

  it('re-activates enhanced output on a retained device without a second chooser prompt', async () => {
    const device = new FakeHidDevice('bluetooth')
    const chooser = new FakeChooser([device])
    const output = driver(chooser)
    await output.enableEnhancedFeatures()
    await output.disableEnhancedFeatures()

    expect(await output.enableEnhancedFeatures()).toBe(true)
    expect(chooser.requests).toBe(1)
    expect(output.capability()).toMatchObject({ tier: 2, status: 'enhanced' })
    expect(await output.play(effect(0.4))).toBe(true)
  })

  it('keeps Bluetooth input alive after a failed output write', async () => {
    const device = new FakeHidDevice('bluetooth')
    device.failReportId = 0x32
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()

    expect(await output.play(effect())).toBe(false)
    expect(device.opened).toBe(true)
    expect(output.capability()).toMatchObject({ tier: 0, status: 'error' })
    device.emitInput(BLUETOOTH_INPUT_REPORT_ID, 0x20)
    expect(output.hidInputSnapshot()?.buttons[0].pressed).toBe(true)
  })

  it('closes the retained Bluetooth device on dispose', async () => {
    const device = new FakeHidDevice('bluetooth')
    const output = driver(new FakeChooser([device]))
    await output.enableEnhancedFeatures()
    await output.disableEnhancedFeatures()
    await output.dispose()

    expect(device.opened).toBe(false)
    expect(device.log[device.log.length - 1]).toBe('close')
    expect(device.inputListeners.size).toBe(0)
    device.emitInput(BLUETOOTH_INPUT_REPORT_ID, 0x20)
    expect(output.hidInputSnapshot()).toBeNull()
  })
})
