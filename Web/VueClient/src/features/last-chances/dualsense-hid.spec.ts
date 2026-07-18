import { describe, expect, it } from 'vitest'
import type { LastChancesFeedbackEffect } from './feedback'
import {
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
