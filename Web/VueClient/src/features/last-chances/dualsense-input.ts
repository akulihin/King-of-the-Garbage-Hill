import type { LastChancesGamepadButtonLike, LastChancesGamepadLike } from './gamepad'

/**
 * Bluetooth DualSense input driver for finding M118: sending any effects output
 * over Bluetooth flips the pad from simple mode (standard 0x01 input reports)
 * into extended mode (vendor-typed 0x31 input reports), which Chromium's
 * Gamepad API cannot map — the pad stays flipped until it power-cycles. The
 * page already holds the open WebHID device for Tier-2 output, so controller
 * input is recovered by parsing those 0x31 input reports directly into the
 * canonical standard-gamepad order the engine's adapter expects.
 */

export const BLUETOOTH_INPUT_REPORT_ID = 0x31

/** Synthetic index outside any realistic Gamepad-API slot. */
export const DUALSENSE_HID_GAMEPAD_INDEX = 99
export const DUALSENSE_HID_GAMEPAD_ID = 'DualSense (WebHID 0x31 input)'

/** A reading is discarded when the report stream stalls longer than this. */
export const DUALSENSE_HID_INPUT_STALE_MS = 500

/** BT report 0x31 carries one header byte before the common input payload. */
const BLUETOOTH_INPUT_PAYLOAD_OFFSET = 1
const OFFSET_LEFT_X = 0
const OFFSET_LEFT_Y = 1
const OFFSET_RIGHT_X = 2
const OFFSET_RIGHT_Y = 3
const OFFSET_LEFT_TRIGGER = 4
const OFFSET_RIGHT_TRIGGER = 5
const OFFSET_BUTTONS0 = 7
const OFFSET_BUTTONS1 = 8
const OFFSET_BUTTONS2 = 9
const MINIMUM_PAYLOAD_LENGTH = OFFSET_BUTTONS2 + 1

const BUTTONS0_SQUARE = 0x10
const BUTTONS0_CROSS = 0x20
const BUTTONS0_CIRCLE = 0x40
const BUTTONS0_TRIANGLE = 0x80
const BUTTONS1_L1 = 0x01
const BUTTONS1_R1 = 0x02
const BUTTONS1_CREATE = 0x10
const BUTTONS1_OPTIONS = 0x20
const BUTTONS1_L3 = 0x40
const BUTTONS1_R3 = 0x80
const BUTTONS2_PS = 0x01
const BUTTONS2_TOUCHPAD = 0x02

const HAT_NEUTRAL = 8

/** Canonical standard-gamepad button count (0 Cross … 17 touchpad). */
const CANONICAL_BUTTON_COUNT = 18

export interface DualSenseHidInputReading {
  axes: readonly [number, number, number, number]
  buttons: readonly LastChancesGamepadButtonLike[]
}

export interface LastChancesHidInputReportEventLike {
  readonly reportId: number
  readonly data: DataView
}

export interface LastChancesHidInputDeviceLike {
  readonly opened: boolean
  addEventListener?(
    type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void
  removeEventListener?(
    type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void
}

function stickAxis(byte: number): number {
  return Math.max(-1, Math.min(1, (byte * 2) / 255 - 1))
}

function digital(pressed: boolean): LastChancesGamepadButtonLike {
  return { pressed, value: pressed ? 1 : 0 }
}

function analog(byte: number, threshold = 0.5): LastChancesGamepadButtonLike {
  const value = Math.max(0, Math.min(1, byte / 255))
  return { pressed: value >= threshold, value }
}

/**
 * Parses a Bluetooth extended input report into canonical standard-gamepad
 * order (0 Cross, 1 Circle, 2 Square, 3 Triangle, 4 L1, 5 R1, 6 L2, 7 R2,
 * 8 Create, 9 Options, 10 L3, 11 R3, 12–15 d-pad, 16 PS, 17 touchpad).
 * Returns null for any other report id or a truncated payload.
 */
export function parseDualSenseBluetoothInputReport(
  reportId: number,
  data: DataView,
): DualSenseHidInputReading | null {
  if (reportId !== BLUETOOTH_INPUT_REPORT_ID) return null
  if (data.byteLength < BLUETOOTH_INPUT_PAYLOAD_OFFSET + MINIMUM_PAYLOAD_LENGTH) return null
  const payload = (offset: number): number =>
    data.getUint8(BLUETOOTH_INPUT_PAYLOAD_OFFSET + offset)

  const buttons0 = payload(OFFSET_BUTTONS0)
  const buttons1 = payload(OFFSET_BUTTONS1)
  const buttons2 = payload(OFFSET_BUTTONS2)
  const hat = buttons0 & 0x0f
  const hatActive = hat !== HAT_NEUTRAL && hat >= 0 && hat <= 7

  const buttons: LastChancesGamepadButtonLike[] = new Array(CANONICAL_BUTTON_COUNT)
  buttons[0] = digital((buttons0 & BUTTONS0_CROSS) !== 0)
  buttons[1] = digital((buttons0 & BUTTONS0_CIRCLE) !== 0)
  buttons[2] = digital((buttons0 & BUTTONS0_SQUARE) !== 0)
  buttons[3] = digital((buttons0 & BUTTONS0_TRIANGLE) !== 0)
  buttons[4] = digital((buttons1 & BUTTONS1_L1) !== 0)
  buttons[5] = digital((buttons1 & BUTTONS1_R1) !== 0)
  buttons[6] = analog(payload(OFFSET_LEFT_TRIGGER))
  buttons[7] = analog(payload(OFFSET_RIGHT_TRIGGER))
  buttons[8] = digital((buttons1 & BUTTONS1_CREATE) !== 0)
  buttons[9] = digital((buttons1 & BUTTONS1_OPTIONS) !== 0)
  buttons[10] = digital((buttons1 & BUTTONS1_L3) !== 0)
  buttons[11] = digital((buttons1 & BUTTONS1_R3) !== 0)
  buttons[12] = digital(hatActive && (hat === 7 || hat === 0 || hat === 1))
  buttons[13] = digital(hatActive && hat >= 3 && hat <= 5)
  buttons[14] = digital(hatActive && hat >= 5 && hat <= 7)
  buttons[15] = digital(hatActive && hat >= 1 && hat <= 3)
  buttons[16] = digital((buttons2 & BUTTONS2_PS) !== 0)
  buttons[17] = digital((buttons2 & BUTTONS2_TOUCHPAD) !== 0)

  return {
    axes: [
      stickAxis(payload(OFFSET_LEFT_X)),
      stickAxis(payload(OFFSET_LEFT_Y)),
      stickAxis(payload(OFFSET_RIGHT_X)),
      stickAxis(payload(OFFSET_RIGHT_Y)),
    ],
    buttons,
  }
}

/**
 * Subscribes to a device's inputreport stream and exposes the latest reading
 * as a synthetic standard-mapping gamepad. The snapshot goes null when the
 * stream stalls (pad disconnected/asleep) so callers fall back to the Gamepad
 * API instead of acting on frozen values.
 */
export class DualSenseHidInputReader {
  private reading: DualSenseHidInputReading | null = null
  private lastReportAt = Number.NEGATIVE_INFINITY
  private detached = false
  private readonly listener = (event: LastChancesHidInputReportEventLike): void => {
    const parsed = parseDualSenseBluetoothInputReport(event.reportId, event.data)
    if (!parsed) return
    this.reading = parsed
    this.lastReportAt = this.now()
  }

  constructor(
    private readonly device: LastChancesHidInputDeviceLike,
    private readonly now: () => number = () => performance.now(),
    private readonly staleAfterMs: number = DUALSENSE_HID_INPUT_STALE_MS,
  ) {
    device.addEventListener?.('inputreport', this.listener)
  }

  snapshot(): LastChancesGamepadLike | null {
    if (this.detached || !this.reading || !this.device.opened) return null
    if (this.now() - this.lastReportAt > this.staleAfterMs) return null
    return {
      axes: this.reading.axes,
      buttons: this.reading.buttons,
      connected: true,
      id: DUALSENSE_HID_GAMEPAD_ID,
      index: DUALSENSE_HID_GAMEPAD_INDEX,
      mapping: 'standard',
    }
  }

  detach(): void {
    if (this.detached) return
    this.detached = true
    this.reading = null
    this.device.removeEventListener?.('inputreport', this.listener)
  }
}
