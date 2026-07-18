import { afterEach, describe, expect, it, vi } from 'vitest'
import { DualSenseWebHidDriver, type LastChancesHidDeviceLike } from './dualsense-hid'
import {
  BLUETOOTH_OUTPUT_REPORT_ID,
  BluetoothDualSenseSerializer,
  createLastChancesDualSenseEnhancedOutput,
  DUALSENSE_EDGE_PRODUCT_ID,
  DUALSENSE_PRODUCT_ID,
  isAllowedDualSenseDevice,
  SONY_VENDOR_ID,
  USB_OUTPUT_REPORT_ID,
  UsbDualSenseSerializer,
} from './dualsense-serializers'
import type { LastChancesFeedbackEffect } from './feedback'

function effect(overrides: Partial<LastChancesFeedbackEffect> = {}): LastChancesFeedbackEffect {
  return {
    state: 'impact',
    profile: 'impact',
    hand: 'right',
    magnitude: 0.6,
    durationMs: 100,
    priority: 100,
    adaptiveProfile: {
      startPosition: 0.2,
      endPosition: 0.8,
      resistance: 0.4,
      force: 0.5,
      transitionMs: 40,
      effectMs: 100,
      magnitude: 0.6,
    },
    ...overrides,
  }
}

function deviceWithReports(
  outputReportIds: readonly number[],
  extra: Partial<LastChancesHidDeviceLike> = {},
): LastChancesHidDeviceLike {
  return {
    opened: false,
    collections: [{ outputReports: outputReportIds.map(reportId => ({ reportId })) }],
    async open() {},
    async close() {},
    async sendReport() {},
    ...extra,
  }
}

function referenceCrc32(bytes: readonly number[]): number {
  let crc = 0xffffffff
  for (const byte of bytes) {
    crc ^= byte
    for (let bit = 0; bit < 8; bit += 1) {
      crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1
    }
  }
  return (crc ^ 0xffffffff) >>> 0
}

describe('DualSense USB serializer', () => {
  it('emits one 47-byte 0x02 report with section-mode trigger bytes for the active hand', () => {
    const packets = new UsbDualSenseSerializer().serialize(effect())
    expect(packets).toHaveLength(1)
    expect(packets[0].reportId).toBe(USB_OUTPUT_REPORT_ID)
    const data = packets[0].data
    expect(data).toHaveLength(47)
    expect(data[0]).toBe(0x07)
    expect(data[2]).toBe(153)
    expect(data[3]).toBe(0)
    expect([...data.subarray(10, 14)]).toEqual([0x02, 51, 204, 128])
    expect([...data.subarray(14, 21)]).toEqual([0, 0, 0, 0, 0, 0, 0])
    expect([...data.subarray(21, 32)]).toEqual(new Array(11).fill(0))
    expect(data[38]).toBe(0x04)
  })

  it('uses continuous-resistance mode when the profile has no travel section', () => {
    const packets = new UsbDualSenseSerializer().serialize(effect({
      hand: 'left',
      adaptiveProfile: {
        startPosition: 0.5,
        endPosition: 0,
        resistance: 0.4,
        force: 0.9,
        transitionMs: 40,
        effectMs: 100,
        magnitude: 0.6,
      },
    }))
    const data = packets[0].data
    expect(data[0]).toBe(0x0b)
    expect(data[2]).toBe(0)
    expect(data[3]).toBe(153)
    expect([...data.subarray(10, 21)]).toEqual(new Array(11).fill(0))
    expect([...data.subarray(21, 25)]).toEqual([0x01, 128, 102, 0])
  })

  it('drives both motors and both triggers for a both-hands effect', () => {
    const data = new UsbDualSenseSerializer().serialize(effect({ hand: 'both' }))[0].data
    expect(data[0]).toBe(0x0f)
    expect(data[3]).toBe(153)
    expect(data[2]).toBe(99)
    expect([...data.subarray(10, 21)]).toEqual([...data.subarray(21, 32)])
  })

  it('neutral resets both triggers to full-off and both motors to zero', () => {
    const data = new UsbDualSenseSerializer().neutral()[0].data
    expect(data[0]).toBe(0x0f)
    expect(data[2]).toBe(0)
    expect(data[3]).toBe(0)
    expect(data[10]).toBe(0x05)
    expect(data[21]).toBe(0x05)
    expect([...data.subarray(11, 21)]).toEqual(new Array(10).fill(0))
    expect([...data.subarray(22, 32)]).toEqual(new Array(10).fill(0))
  })

  it('supports only devices whose descriptor exposes output report 0x02', () => {
    const serializer = new UsbDualSenseSerializer()
    expect(serializer.supports(deviceWithReports([USB_OUTPUT_REPORT_ID]))).toBe(true)
    expect(serializer.supports(deviceWithReports([BLUETOOTH_OUTPUT_REPORT_ID]))).toBe(false)
    expect(serializer.supports(deviceWithReports([]))).toBe(false)
    expect(serializer.supports({ ...deviceWithReports([]), collections: undefined })).toBe(false)
  })
})

describe('DualSense Bluetooth serializer', () => {
  it('wraps the common payload in a 77-byte 0x31 report with tag, sequence and CRC-32', () => {
    const serializer = new BluetoothDualSenseSerializer()
    const first = serializer.serialize(effect())[0]
    const second = serializer.serialize(effect())[0]
    expect(first.reportId).toBe(BLUETOOTH_OUTPUT_REPORT_ID)
    expect(first.data).toHaveLength(77)
    expect(first.data[0]).toBe(0x00)
    expect(second.data[0]).toBe(0x10)
    expect(first.data[1]).toBe(0x10)
    const usbPayload = new UsbDualSenseSerializer().serialize(effect())[0].data
    expect([...first.data.subarray(2, 49)]).toEqual([...usbPayload])
    const crc = referenceCrc32([0xa2, BLUETOOTH_OUTPUT_REPORT_ID, ...first.data.subarray(0, 73)])
    expect([...first.data.subarray(73, 77)]).toEqual([
      crc & 0xff,
      (crc >>> 8) & 0xff,
      (crc >>> 16) & 0xff,
      (crc >>> 24) & 0xff,
    ])
  })

  it('wraps the packet sequence nibble after sixteen writes', () => {
    const serializer = new BluetoothDualSenseSerializer()
    const sequenceBytes = Array.from({ length: 17 }, () => serializer.neutral()[0].data[0])
    expect(sequenceBytes[15]).toBe(0xf0)
    expect(sequenceBytes[16]).toBe(0x00)
  })

  it('supports devices exposing output report 0x31, including nested collections', () => {
    const serializer = new BluetoothDualSenseSerializer()
    expect(serializer.supports(deviceWithReports([BLUETOOTH_OUTPUT_REPORT_ID]))).toBe(true)
    expect(serializer.supports(deviceWithReports([USB_OUTPUT_REPORT_ID]))).toBe(false)
    const nested: LastChancesHidDeviceLike = {
      ...deviceWithReports([]),
      collections: [{ children: [{ outputReports: [{ reportId: BLUETOOTH_OUTPUT_REPORT_ID }] }] }],
    }
    expect(serializer.supports(nested)).toBe(true)
  })

})

describe('DualSense device allowlist and factory', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('allows only Sony DualSense and DualSense Edge product IDs', () => {
    const sony = (productId: number) => deviceWithReports([], { vendorId: SONY_VENDOR_ID, productId })
    expect(isAllowedDualSenseDevice(sony(DUALSENSE_PRODUCT_ID))).toBe(true)
    expect(isAllowedDualSenseDevice(sony(DUALSENSE_EDGE_PRODUCT_ID))).toBe(true)
    expect(isAllowedDualSenseDevice(sony(0x05c4))).toBe(false)
    expect(isAllowedDualSenseDevice(deviceWithReports([], { vendorId: 0x1234, productId: DUALSENSE_PRODUCT_ID }))).toBe(false)
    expect(isAllowedDualSenseDevice(deviceWithReports([]))).toBe(false)
  })

  it('returns null without WebHID support or outside a secure context', () => {
    vi.stubGlobal('window', { isSecureContext: true })
    vi.stubGlobal('navigator', {})
    expect(createLastChancesDualSenseEnhancedOutput()).toBeNull()
    vi.stubGlobal('navigator', { hid: { requestDevice: async () => [] } })
    vi.stubGlobal('window', { isSecureContext: false })
    expect(createLastChancesDualSenseEnhancedOutput()).toBeNull()
  })

  it('builds a not-yet-requested driver bound to navigator.hid in a secure context', () => {
    vi.stubGlobal('window', { isSecureContext: true })
    vi.stubGlobal('navigator', { hid: { requestDevice: async () => [] } })
    const output = createLastChancesDualSenseEnhancedOutput()
    expect(output).toBeInstanceOf(DualSenseWebHidDriver)
    expect(output?.capability()).toMatchObject({
      tier: 0,
      status: 'controls-only',
      permission: 'not-requested',
    })
  })
})
