import {
  DualSenseWebHidDriver,
  type DualSenseHidPacket,
  type DualSenseHidTransportSerializer,
  type LastChancesHidChooserLike,
  type LastChancesHidDeviceFilter,
  type LastChancesHidDeviceLike,
} from './dualsense-hid'
import type { LastChancesEnhancedFeedbackOutput, LastChancesFeedbackEffect } from './feedback'
import type { LastChancesAdaptiveTriggerProfileDefinition } from './types'

export const SONY_VENDOR_ID = 0x054c
export const DUALSENSE_PRODUCT_ID = 0x0ce6
export const DUALSENSE_EDGE_PRODUCT_ID = 0x0df2

export const DUALSENSE_HID_FILTERS: readonly LastChancesHidDeviceFilter[] = [
  { vendorId: SONY_VENDOR_ID, productId: DUALSENSE_PRODUCT_ID },
  { vendorId: SONY_VENDOR_ID, productId: DUALSENSE_EDGE_PRODUCT_ID },
]

export const USB_OUTPUT_REPORT_ID = 0x02
export const BLUETOOTH_OUTPUT_REPORT_ID = 0x31

const COMMON_PAYLOAD_LENGTH = 47
const OFFSET_VALID_FLAG0 = 0
const OFFSET_MOTOR_RIGHT = 2
const OFFSET_MOTOR_LEFT = 3
const OFFSET_RIGHT_TRIGGER = 10
const OFFSET_LEFT_TRIGGER = 21
const OFFSET_VALID_FLAG2 = 38
const TRIGGER_BLOCK_LENGTH = 11

const VALID_FLAG0_COMPATIBLE_VIBRATION = 0x01
const VALID_FLAG0_HAPTICS_SELECT = 0x02
const VALID_FLAG0_RIGHT_TRIGGER = 0x04
const VALID_FLAG0_LEFT_TRIGGER = 0x08
/** Post-0x0224 firmware reads rumble through this flag; older firmware uses valid_flag0 bit 0. */
const VALID_FLAG2_COMPATIBLE_VIBRATION2 = 0x04

const TRIGGER_MODE_RESISTANCE = 0x01
const TRIGGER_MODE_SECTION = 0x02
const TRIGGER_MODE_OFF = 0x05

const BLUETOOTH_PAYLOAD_LENGTH = 77
const BLUETOOTH_CRC_OFFSET = 73
const BLUETOOTH_OUTPUT_SALT = 0xa2
const BLUETOOTH_TAG = 0x10

/** The weaker high-frequency motor mirrors the Tier 1 Gamepad weak/strong split. */
const WEAK_MOTOR_RATIO = 0.65

function byteScale(value: number): number {
  if (!Number.isFinite(value)) return 0
  return Math.round(Math.max(0, Math.min(1, value)) * 255)
}

function triggerBlock(profile: LastChancesAdaptiveTriggerProfileDefinition): Uint8Array {
  const block = new Uint8Array(TRIGGER_BLOCK_LENGTH)
  if (profile.endPosition > profile.startPosition) {
    block[0] = TRIGGER_MODE_SECTION
    block[1] = byteScale(profile.startPosition)
    block[2] = byteScale(profile.endPosition)
    block[3] = byteScale(profile.force)
  } else {
    block[0] = TRIGGER_MODE_RESISTANCE
    block[1] = byteScale(profile.startPosition)
    block[2] = byteScale(profile.resistance)
  }
  return block
}

function neutralTriggerBlock(): Uint8Array {
  const block = new Uint8Array(TRIGGER_BLOCK_LENGTH)
  block[0] = TRIGGER_MODE_OFF
  return block
}

export function buildDualSenseEffectPayload(effect: LastChancesFeedbackEffect): Uint8Array {
  const payload = new Uint8Array(COMMON_PAYLOAD_LENGTH)
  const leftHand = effect.hand !== 'right'
  const rightHand = effect.hand !== 'left'
  payload[OFFSET_VALID_FLAG0] = VALID_FLAG0_COMPATIBLE_VIBRATION
    | VALID_FLAG0_HAPTICS_SELECT
    | (rightHand ? VALID_FLAG0_RIGHT_TRIGGER : 0)
    | (leftHand ? VALID_FLAG0_LEFT_TRIGGER : 0)
  payload[OFFSET_VALID_FLAG2] = VALID_FLAG2_COMPATIBLE_VIBRATION2
  const strong = byteScale(effect.magnitude)
  const weak = byteScale(effect.magnitude * WEAK_MOTOR_RATIO)
  payload[OFFSET_MOTOR_LEFT] = leftHand ? strong : 0
  payload[OFFSET_MOTOR_RIGHT] = rightHand ? (leftHand ? weak : strong) : 0
  const trigger = triggerBlock(effect.adaptiveProfile)
  if (rightHand) payload.set(trigger, OFFSET_RIGHT_TRIGGER)
  if (leftHand) payload.set(trigger, OFFSET_LEFT_TRIGGER)
  return payload
}

export function buildDualSenseNeutralPayload(): Uint8Array {
  const payload = new Uint8Array(COMMON_PAYLOAD_LENGTH)
  payload[OFFSET_VALID_FLAG0] = VALID_FLAG0_COMPATIBLE_VIBRATION
    | VALID_FLAG0_HAPTICS_SELECT
    | VALID_FLAG0_RIGHT_TRIGGER
    | VALID_FLAG0_LEFT_TRIGGER
  payload[OFFSET_VALID_FLAG2] = VALID_FLAG2_COMPATIBLE_VIBRATION2
  payload.set(neutralTriggerBlock(), OFFSET_RIGHT_TRIGGER)
  payload.set(neutralTriggerBlock(), OFFSET_LEFT_TRIGGER)
  return payload
}

function exposesOutputReport(device: LastChancesHidDeviceLike, reportId: number): boolean {
  const walk = (collections: NonNullable<LastChancesHidDeviceLike['collections']>): boolean =>
    collections.some(collection => (
      (collection.outputReports ?? []).some(report => report.reportId === reportId)
      || walk(collection.children ?? [])
    ))
  return walk(device.collections ?? [])
}

export class UsbDualSenseSerializer implements DualSenseHidTransportSerializer {
  readonly transport = 'usb' as const

  supports(device: LastChancesHidDeviceLike): boolean {
    return exposesOutputReport(device, USB_OUTPUT_REPORT_ID)
  }

  serialize(effect: LastChancesFeedbackEffect): readonly DualSenseHidPacket[] {
    return [{ reportId: USB_OUTPUT_REPORT_ID, data: buildDualSenseEffectPayload(effect) }]
  }

  neutral(): readonly DualSenseHidPacket[] {
    return [{ reportId: USB_OUTPUT_REPORT_ID, data: buildDualSenseNeutralPayload() }]
  }
}

const CRC_TABLE = (() => {
  const table = new Uint32Array(256)
  for (let index = 0; index < 256; index += 1) {
    let value = index
    for (let bit = 0; bit < 8; bit += 1) {
      value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1
    }
    table[index] = value >>> 0
  }
  return table
})()

function crc32(bytes: Iterable<number>): number {
  let crc = 0xffffffff
  for (const byte of bytes) {
    crc = CRC_TABLE[(crc ^ byte) & 0xff] ^ (crc >>> 8)
  }
  return (crc ^ 0xffffffff) >>> 0
}

/**
 * HARNESS-ONLY — excluded from `createLastChancesDualSenseEnhancedOutput`.
 * M116 assumed only a feature-report read (calibration 0x05) flips a Bluetooth
 * pad into extended report mode. Real hardware disproved that on 2026-07-18:
 * the pad also switches as soon as it receives ANY 0x31 output packet — SDL's
 * PS5 driver documents the same ("We can't even send an invalid effects packet,
 * or it will put the controller in enhanced mode"). Extended mode makes the
 * input reports vendor-typed (usage page 0xFF00), Chromium's Gamepad API can no
 * longer map them, and controller input freezes until the pad power-cycles
 * (finding M117). Production Tier 2 is therefore USB-only; this serializer is
 * retained for the `/99lc/dualsense-harness.html` diagnostic page and for a
 * possible future WebHID-input driver that would parse 0x31 input reports itself.
 */
export class BluetoothDualSenseSerializer implements DualSenseHidTransportSerializer {
  readonly transport = 'bluetooth' as const
  private sequence = 0

  supports(device: LastChancesHidDeviceLike): boolean {
    return exposesOutputReport(device, BLUETOOTH_OUTPUT_REPORT_ID)
  }

  serialize(effect: LastChancesFeedbackEffect): readonly DualSenseHidPacket[] {
    return [this.packet(buildDualSenseEffectPayload(effect))]
  }

  neutral(): readonly DualSenseHidPacket[] {
    return [this.packet(buildDualSenseNeutralPayload())]
  }

  private packet(common: Uint8Array): DualSenseHidPacket {
    const data = new Uint8Array(BLUETOOTH_PAYLOAD_LENGTH)
    data[0] = (this.sequence << 4) & 0xf0
    this.sequence = (this.sequence + 1) & 0x0f
    data[1] = BLUETOOTH_TAG
    data.set(common, 2)
    const crc = crc32([
      BLUETOOTH_OUTPUT_SALT,
      BLUETOOTH_OUTPUT_REPORT_ID,
      ...data.subarray(0, BLUETOOTH_CRC_OFFSET),
    ])
    data[BLUETOOTH_CRC_OFFSET] = crc & 0xff
    data[BLUETOOTH_CRC_OFFSET + 1] = (crc >>> 8) & 0xff
    data[BLUETOOTH_CRC_OFFSET + 2] = (crc >>> 16) & 0xff
    data[BLUETOOTH_CRC_OFFSET + 3] = (crc >>> 24) & 0xff
    return { reportId: BLUETOOTH_OUTPUT_REPORT_ID, data }
  }
}

export function isAllowedDualSenseDevice(device: LastChancesHidDeviceLike): boolean {
  return device.vendorId === SONY_VENDOR_ID
    && (device.productId === DUALSENSE_PRODUCT_ID || device.productId === DUALSENSE_EDGE_PRODUCT_ID)
}

interface NavigatorWithHid {
  hid?: LastChancesHidChooserLike
}

/**
 * Returns the production WebHID enhanced output, or null when the context cannot
 * host one (non-Chromium browser, insecure context, SSR). Null keeps the page at
 * Tier 0/1 with the same graceful fallback as before the transport shipped.
 * Production Tier 2 is USB-only (finding M117): a granted Bluetooth pad is
 * classified as `usb-required` instead of receiving output that would freeze its
 * Gamepad API input.
 */
export function createLastChancesDualSenseEnhancedOutput(): LastChancesEnhancedFeedbackOutput | null {
  if (typeof navigator === 'undefined' || typeof window === 'undefined' || !window.isSecureContext) {
    return null
  }
  const hid = (navigator as unknown as NavigatorWithHid).hid ?? null
  if (!hid || typeof hid.requestDevice !== 'function') return null
  return new DualSenseWebHidDriver({
    hid,
    filters: DUALSENSE_HID_FILTERS,
    isAllowedDevice: isAllowedDualSenseDevice,
    serializers: [new UsbDualSenseSerializer()],
    unsupportedDeviceCapability: device => (
      exposesOutputReport(device, BLUETOOTH_OUTPUT_REPORT_ID)
        ? {
            permission: 'usb-required',
            message: 'Adaptive triggers require a USB connection: any Bluetooth trigger/rumble packet '
              + 'switches the pad into a report mode the browser cannot read, freezing controller input.',
          }
        : null
    ),
  })
}
