import { describe, expect, it } from 'vitest'
import {
  BLUETOOTH_INPUT_REPORT_ID,
  DUALSENSE_HID_GAMEPAD_ID,
  DUALSENSE_HID_GAMEPAD_INDEX,
  DUALSENSE_HID_INPUT_STALE_MS,
  DualSenseHidInputReader,
  parseDualSenseBluetoothInputReport,
  type LastChancesHidInputDeviceLike,
  type LastChancesHidInputReportEventLike,
} from './dualsense-input'

interface ReportBytes {
  lx?: number
  ly?: number
  rx?: number
  ry?: number
  l2?: number
  r2?: number
  hat?: number
  face?: number
  buttons1?: number
  buttons2?: number
}

function reportData(bytes: ReportBytes = {}): DataView {
  const payload = new Uint8Array(11)
  payload[0] = 0xc1
  payload[1] = bytes.lx ?? 128
  payload[2] = bytes.ly ?? 128
  payload[3] = bytes.rx ?? 128
  payload[4] = bytes.ry ?? 128
  payload[5] = bytes.l2 ?? 0
  payload[6] = bytes.r2 ?? 0
  payload[7] = 0
  payload[8] = (bytes.face ?? 0) | (bytes.hat ?? 8)
  payload[9] = bytes.buttons1 ?? 0
  payload[10] = bytes.buttons2 ?? 0
  return new DataView(payload.buffer)
}

function parse(bytes: ReportBytes = {}) {
  const reading = parseDualSenseBluetoothInputReport(BLUETOOTH_INPUT_REPORT_ID, reportData(bytes))
  expect(reading).not.toBeNull()
  return reading!
}

class FakeInputDevice implements LastChancesHidInputDeviceLike {
  opened = true
  listeners = new Set<(event: LastChancesHidInputReportEventLike) => void>()

  addEventListener(
    _type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void {
    this.listeners.add(listener)
  }

  removeEventListener(
    _type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void {
    this.listeners.delete(listener)
  }

  emit(reportId: number, bytes: ReportBytes = {}): void {
    const event = { reportId, data: reportData(bytes) }
    for (const listener of this.listeners) listener(event)
  }
}

describe('DualSense Bluetooth 0x31 input parsing', () => {
  it('ignores other report ids and truncated payloads', () => {
    expect(parseDualSenseBluetoothInputReport(0x01, reportData())).toBeNull()
    const truncated = new DataView(new Uint8Array(6).buffer)
    expect(parseDualSenseBluetoothInputReport(BLUETOOTH_INPUT_REPORT_ID, truncated)).toBeNull()
  })

  it('parses a neutral report as centered sticks and released buttons', () => {
    const reading = parse()
    expect(reading.buttons).toHaveLength(18)
    for (const axis of reading.axes) expect(Math.abs(axis)).toBeLessThan(0.01)
    for (const button of reading.buttons) {
      expect(button.pressed).toBe(false)
      expect(button.value).toBe(0)
    }
  })

  it('maps stick extremes across the full axis range', () => {
    const reading = parse({ lx: 0, ly: 255, rx: 255, ry: 0 })
    expect(reading.axes[0]).toBe(-1)
    expect(reading.axes[1]).toBe(1)
    expect(reading.axes[2]).toBe(1)
    expect(reading.axes[3]).toBe(-1)
  })

  it('maps face buttons into canonical standard order', () => {
    expect(parse({ face: 0x20 }).buttons[0].pressed).toBe(true)
    expect(parse({ face: 0x40 }).buttons[1].pressed).toBe(true)
    expect(parse({ face: 0x10 }).buttons[2].pressed).toBe(true)
    expect(parse({ face: 0x80 }).buttons[3].pressed).toBe(true)
  })

  it('maps shoulder, stick-click and meta buttons', () => {
    const reading = parse({ buttons1: 0xff, buttons2: 0x03 })
    expect(reading.buttons[4].pressed).toBe(true)
    expect(reading.buttons[5].pressed).toBe(true)
    expect(reading.buttons[8].pressed).toBe(true)
    expect(reading.buttons[9].pressed).toBe(true)
    expect(reading.buttons[10].pressed).toBe(true)
    expect(reading.buttons[11].pressed).toBe(true)
    expect(reading.buttons[16].pressed).toBe(true)
    expect(reading.buttons[17].pressed).toBe(true)
  })

  it('keeps triggers analog with a pressed threshold', () => {
    const reading = parse({ l2: 255, r2: 64 })
    expect(reading.buttons[6].value).toBe(1)
    expect(reading.buttons[6].pressed).toBe(true)
    expect(reading.buttons[7].value).toBeCloseTo(64 / 255, 5)
    expect(reading.buttons[7].pressed).toBe(false)
  })

  it('decodes every hat direction including diagonals', () => {
    const directions: Array<[number, boolean, boolean, boolean, boolean]> = [
      [0, true, false, false, false],
      [1, true, false, false, true],
      [2, false, false, false, true],
      [3, false, true, false, true],
      [4, false, true, false, false],
      [5, false, true, true, false],
      [6, false, false, true, false],
      [7, true, false, true, false],
      [8, false, false, false, false],
    ]
    for (const [hat, up, down, left, right] of directions) {
      const reading = parse({ hat })
      expect(reading.buttons[12].pressed, `hat ${hat} up`).toBe(up)
      expect(reading.buttons[13].pressed, `hat ${hat} down`).toBe(down)
      expect(reading.buttons[14].pressed, `hat ${hat} left`).toBe(left)
      expect(reading.buttons[15].pressed, `hat ${hat} right`).toBe(right)
    }
  })
})

describe('DualSense HID input reader', () => {
  it('exposes the latest report as a synthetic standard-mapping gamepad', () => {
    const device = new FakeInputDevice()
    const reader = new DualSenseHidInputReader(device, () => 0)
    expect(reader.snapshot()).toBeNull()

    device.emit(BLUETOOTH_INPUT_REPORT_ID, { face: 0x20, l2: 255 })
    const snapshot = reader.snapshot()
    expect(snapshot).toMatchObject({
      connected: true,
      id: DUALSENSE_HID_GAMEPAD_ID,
      index: DUALSENSE_HID_GAMEPAD_INDEX,
      mapping: 'standard',
    })
    expect(snapshot?.buttons[0].pressed).toBe(true)
    expect(snapshot?.buttons[6].value).toBe(1)
  })

  it('ignores non-0x31 reports', () => {
    const device = new FakeInputDevice()
    const reader = new DualSenseHidInputReader(device, () => 0)
    device.emit(0x01, { face: 0x20 })
    expect(reader.snapshot()).toBeNull()
  })

  it('goes null when the report stream stalls and recovers on the next report', () => {
    let clock = 0
    const device = new FakeInputDevice()
    const reader = new DualSenseHidInputReader(device, () => clock)
    device.emit(BLUETOOTH_INPUT_REPORT_ID)
    expect(reader.snapshot()).not.toBeNull()

    clock += DUALSENSE_HID_INPUT_STALE_MS + 1
    expect(reader.snapshot()).toBeNull()

    device.emit(BLUETOOTH_INPUT_REPORT_ID)
    expect(reader.snapshot()).not.toBeNull()
  })

  it('goes null when the device closes and stops listening after detach', () => {
    const device = new FakeInputDevice()
    const reader = new DualSenseHidInputReader(device, () => 0)
    device.emit(BLUETOOTH_INPUT_REPORT_ID)
    device.opened = false
    expect(reader.snapshot()).toBeNull()

    device.opened = true
    reader.detach()
    expect(device.listeners.size).toBe(0)
    expect(reader.snapshot()).toBeNull()
  })
})
