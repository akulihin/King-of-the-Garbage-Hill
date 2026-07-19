import {
  DualSenseHidInputReader,
  type LastChancesHidInputReportEventLike,
} from './dualsense-input'
import type {
  LastChancesEnhancedFeedbackOutput,
  LastChancesFeedbackEffect,
  LastChancesFeedbackOutputCapability,
  LastChancesTriggerBaseline,
} from './feedback'
import type { LastChancesGamepadLike } from './gamepad'

export type DualSenseHidTransport = 'usb' | 'bluetooth'

export interface LastChancesHidDeviceFilter {
  vendorId: number
  productId?: number
  usagePage?: number
  usage?: number
}

export interface LastChancesHidReportLike {
  readonly reportId?: number
}

export interface LastChancesHidCollectionLike {
  readonly outputReports?: readonly LastChancesHidReportLike[]
  readonly children?: readonly LastChancesHidCollectionLike[]
}

export interface LastChancesHidDeviceLike {
  readonly opened: boolean
  readonly vendorId?: number
  readonly productId?: number
  readonly collections?: readonly LastChancesHidCollectionLike[]
  open(): Promise<void>
  close(): Promise<void>
  sendReport(reportId: number, data: Uint8Array): Promise<void>
  addEventListener?(
    type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void
  removeEventListener?(
    type: 'inputreport',
    listener: (event: LastChancesHidInputReportEventLike) => void,
  ): void
}

export interface LastChancesHidChooserLike {
  requestDevice(options: {
    filters: readonly LastChancesHidDeviceFilter[]
  }): Promise<readonly LastChancesHidDeviceLike[]>
}

export interface DualSenseHidPacket {
  reportId: number
  data: Uint8Array
}

/**
 * Raw report knowledge is deliberately injected. This repository does not advertise a
 * production USB or Bluetooth packet layout until real hardware has verified it.
 */
export interface DualSenseHidTransportSerializer {
  readonly transport: DualSenseHidTransport
  supports(device: LastChancesHidDeviceLike): boolean
  serialize(effect: LastChancesFeedbackEffect): readonly DualSenseHidPacket[]
  neutral(): readonly DualSenseHidPacket[]
  /** Motors off while each hand keeps (or relaxes) its resting trigger block. */
  baseline(state: LastChancesTriggerBaseline): readonly DualSenseHidPacket[]
}

export interface DualSenseWebHidDriverOptions {
  hid: LastChancesHidChooserLike | null
  filters: readonly LastChancesHidDeviceFilter[]
  isAllowedDevice: (device: LastChancesHidDeviceLike) => boolean
  serializers: readonly DualSenseHidTransportSerializer[]
}

type PermissionState = LastChancesFeedbackOutputCapability['permission']

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

function cloneBaseline(baseline: LastChancesTriggerBaseline): LastChancesTriggerBaseline {
  return {
    left: baseline.left ? { ...baseline.left } : null,
    right: baseline.right ? { ...baseline.right } : null,
  }
}

function baselineEquals(
  left: LastChancesTriggerBaseline,
  right: LastChancesTriggerBaseline | null,
): boolean {
  if (!right) return false
  const handEquals = (a: LastChancesTriggerBaseline['left'], b: LastChancesTriggerBaseline['left']) => {
    if (!a || !b) return a === b
    return a.startPosition === b.startPosition
      && a.endPosition === b.endPosition
      && a.resistance === b.resistance
      && a.force === b.force
      && a.transitionMs === b.transitionMs
      && a.effectMs === b.effectMs
      && a.magnitude === b.magnitude
  }
  return handEquals(left.left, right.left) && handEquals(left.right, right.right)
}

function isPermissionDenial(error: unknown): boolean {
  if (!(error instanceof Error)) return false
  return error.name === 'NotFoundError'
    || error.name === 'NotAllowedError'
    || error.name === 'SecurityError'
}

/**
 * Shown after enhanced output is turned off over Bluetooth: the pad is stuck in
 * extended report mode until it power-cycles (M118), so the device stays open
 * and keeps feeding controller input through the WebHID input reader.
 */
export const BLUETOOTH_INPUT_RETAINED_MESSAGE =
  'Enhanced DualSense output is off; controller input continues over WebHID until the pad is restarted.'

export class DualSenseWebHidDriver implements LastChancesEnhancedFeedbackOutput {
  private device: LastChancesHidDeviceLike | null = null
  private serializer: DualSenseHidTransportSerializer | null = null
  private inputReader: DualSenseHidInputReader | null = null
  private outputActive = false
  private permission: PermissionState = 'not-requested'
  private status: LastChancesFeedbackOutputCapability['status'] = 'controls-only'
  private message: string | null = null
  private writer: Promise<void> = Promise.resolve()
  private disposed = false
  private baseline: LastChancesTriggerBaseline = { left: null, right: null }
  /** Null when the pad's trigger state no longer matches the baseline (after any effect write). */
  private lastWrittenBaseline: LastChancesTriggerBaseline | null = null

  constructor(private readonly options: DualSenseWebHidDriverOptions) {
    if (!options.hid || options.filters.length === 0 || options.serializers.length === 0) {
      this.permission = 'unavailable'
      this.status = 'unavailable'
      this.message = 'Enhanced DualSense output has no verified HID transport.'
    }
  }

  capability(): LastChancesFeedbackOutputCapability {
    const enhanced = this.outputActive && Boolean(this.device?.opened) && Boolean(this.serializer)
    return {
      tier: enhanced ? 2 : 0,
      status: enhanced ? 'enhanced' : this.status,
      permission: this.permission,
      message: this.message,
    }
  }

  /**
   * Latest controller reading parsed from the pad's Bluetooth 0x31 input
   * reports, as a synthetic standard-mapping gamepad (finding M118). Null on
   * USB, before enable, and whenever the report stream goes stale.
   */
  hidInputSnapshot(): LastChancesGamepadLike | null {
    return this.inputReader?.snapshot() ?? null
  }

  get activeTransport(): DualSenseHidTransport | null {
    return this.device?.opened ? this.serializer?.transport ?? null : null
  }

  /** The only method that may invoke the browser chooser. Call it from an explicit UI action. */
  async enableEnhancedFeatures(): Promise<boolean> {
    if (this.disposed) return false
    if (this.device?.opened && this.serializer) {
      // A Bluetooth device retained for input after disable (M118) re-activates
      // output without a second chooser prompt.
      if (!this.outputActive) {
        try {
          await this.writePackets(this.serializer.neutral())
          this.outputActive = true
          this.status = 'enhanced'
          this.message = null
          this.lastWrittenBaseline = { left: null, right: null }
          await this.writeBaselineIfPending()
        } catch (error) {
          await this.failAndClose(`DualSense open failed: ${errorMessage(error)}`)
          return false
        }
      }
      return true
    }
    if (!this.options.hid || this.options.filters.length === 0 || this.options.serializers.length === 0) {
      return false
    }

    let devices: readonly LastChancesHidDeviceLike[]
    try {
      devices = await this.options.hid.requestDevice({ filters: [...this.options.filters] })
    } catch (error) {
      this.permission = isPermissionDenial(error) ? 'denied' : 'not-requested'
      this.status = isPermissionDenial(error) ? 'controls-only' : 'error'
      this.message = isPermissionDenial(error)
        ? 'DualSense permission was not granted.'
        : `DualSense chooser failed: ${errorMessage(error)}`
      return false
    }

    if (devices.length === 0) {
      this.permission = 'denied'
      this.status = 'controls-only'
      this.message = 'DualSense permission was not granted.'
      return false
    }

    const match = devices
      .filter(device => this.options.isAllowedDevice(device))
      .map(device => ({
        device,
        serializer: this.options.serializers.find(candidate => candidate.supports(device)),
      }))
      .find(candidate => candidate.serializer)
    if (!match?.serializer) {
      this.permission = 'unavailable'
      this.status = 'unavailable'
      this.message = 'The selected HID device has no physically verified transport serializer.'
      return false
    }

    this.device = match.device
    this.serializer = match.serializer
    this.permission = 'granted'
    try {
      if (!this.device.opened) await this.device.open()
      if (this.serializer.transport === 'bluetooth') {
        // Output over BT flips the pad into extended report mode (M118); the
        // reader recovers controller input from the resulting 0x31 reports.
        this.inputReader = new DualSenseHidInputReader(this.device)
      }
      await this.writePackets(this.serializer.neutral())
      this.outputActive = true
      this.status = 'enhanced'
      this.message = null
      this.lastWrittenBaseline = { left: null, right: null }
      await this.writeBaselineIfPending()
      return true
    } catch (error) {
      await this.failAndClose(`DualSense open failed: ${errorMessage(error)}`)
      return false
    }
  }

  async play(effect: LastChancesFeedbackEffect): Promise<boolean> {
    const device = this.device
    const serializer = this.serializer
    if (this.disposed || !device?.opened || !serializer || !this.outputActive) return false

    let packets: readonly DualSenseHidPacket[]
    try {
      packets = serializer.serialize(effect)
    } catch (error) {
      await this.failAndClose(`DualSense serialization failed: ${errorMessage(error)}`)
      return false
    }

    const operation = this.writer.then(() => this.writePackets(packets))
    this.writer = operation.catch(() => undefined)
    try {
      await operation
      // The effect payload stomps the resting trigger blocks and leaves the
      // motors energized; only a later baseline/neutral write restores them.
      this.lastWrittenBaseline = null
      return true
    } catch (error) {
      await this.failAndClose(`DualSense output failed: ${errorMessage(error)}`)
      return false
    }
  }

  /** Records the resting trigger state and writes it through when it changed. */
  async setBaseline(baseline: LastChancesTriggerBaseline): Promise<void> {
    this.baseline = cloneBaseline(baseline)
    if (this.disposed || !this.outputActive) return
    if (baselineEquals(this.baseline, this.lastWrittenBaseline)) return
    await this.writeBaseline()
  }

  /** Re-writes the recorded baseline: motors off, resting trigger resistance kept. */
  async writeBaseline(): Promise<void> {
    const device = this.device
    const serializer = this.serializer
    if (this.disposed || !device?.opened || !serializer || !this.outputActive) return

    let packets: readonly DualSenseHidPacket[]
    try {
      packets = serializer.baseline(this.baseline)
    } catch (error) {
      await this.failAndClose(`DualSense baseline serialization failed: ${errorMessage(error)}`)
      return
    }

    const written = cloneBaseline(this.baseline)
    const operation = this.writer.then(() => this.writePackets(packets))
    this.writer = operation.catch(() => undefined)
    try {
      await operation
      this.lastWrittenBaseline = written
    } catch (error) {
      await this.failAndClose(`DualSense baseline failed: ${errorMessage(error)}`)
    }
  }

  private async writeBaselineIfPending(): Promise<void> {
    if (this.baseline.left || this.baseline.right) await this.writeBaseline()
  }

  async neutralize(): Promise<void> {
    const device = this.device
    const serializer = this.serializer
    if (!device?.opened || !serializer || !this.outputActive) return
    await this.writer
    try {
      await this.writePackets(serializer.neutral())
      this.lastWrittenBaseline = { left: null, right: null }
    } catch (error) {
      await this.failAndClose(`DualSense cleanup failed: ${errorMessage(error)}`)
    }
  }

  async disableEnhancedFeatures(): Promise<void> {
    await this.releaseDevice({ keepBluetoothInput: true })
  }

  async dispose(): Promise<void> {
    if (this.disposed) return
    await this.releaseDevice({ keepBluetoothInput: false })
    this.disposed = true
  }

  private async releaseDevice(options: { keepBluetoothInput: boolean }): Promise<void> {
    await this.writer
    const device = this.device
    const serializer = this.serializer
    if (device?.opened && serializer && this.outputActive) {
      try {
        await this.writePackets(serializer.neutral())
      } catch {
        // Demoting/closing is still mandatory after a failed stop packet.
      }
    }
    this.outputActive = false
    this.lastWrittenBaseline = null
    if (options.keepBluetoothInput && device?.opened && this.inputReader) {
      // The BT pad is stuck in extended report mode until it power-cycles
      // (M118): closing the device would kill all controller input, so it
      // stays open input-only with the output capability demoted.
      this.permission = 'granted'
      this.status = 'controls-only'
      this.message = BLUETOOTH_INPUT_RETAINED_MESSAGE
      return
    }
    this.inputReader?.detach()
    this.inputReader = null
    if (device?.opened) {
      try {
        await device.close()
      } catch {
        // The capability is demoted even when the host rejects close().
      }
    }
    this.device = null
    this.serializer = null
    this.permission = this.options.hid ? 'not-requested' : 'unavailable'
    this.status = this.options.hid ? 'controls-only' : 'unavailable'
    this.message = null
  }

  private async writePackets(packets: readonly DualSenseHidPacket[]): Promise<void> {
    const device = this.device
    if (!device?.opened) throw new Error('HID device is not open')
    for (const packet of packets) {
      await device.sendReport(packet.reportId, new Uint8Array(packet.data))
    }
  }

  private async failAndClose(message: string): Promise<void> {
    const device = this.device
    const serializer = this.serializer
    if (device?.opened && serializer) {
      try {
        await this.writePackets(serializer.neutral())
      } catch {
        // A failed transport may reject the stop packet; close remains the final safety step.
      }
    }
    this.outputActive = false
    this.lastWrittenBaseline = null
    if (device?.opened && this.inputReader) {
      // Keep the retained BT input path alive (M118): only output is demoted.
      // If the failure was a real disconnect, the report stream goes stale and
      // the snapshot turns null on its own.
      this.status = 'error'
      this.message = message
      return
    }
    this.inputReader?.detach()
    this.inputReader = null
    if (device?.opened) {
      try {
        await device.close()
      } catch {
        // Preserve the original transport failure as the public reason.
      }
    }
    this.device = null
    this.serializer = null
    this.status = 'error'
    this.message = message
  }
}
