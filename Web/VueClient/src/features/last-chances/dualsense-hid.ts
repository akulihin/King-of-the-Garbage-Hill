import type {
  LastChancesEnhancedFeedbackOutput,
  LastChancesFeedbackEffect,
  LastChancesFeedbackOutputCapability,
} from './feedback'

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
}

export interface DualSenseWebHidDriverOptions {
  hid: LastChancesHidChooserLike | null
  filters: readonly LastChancesHidDeviceFilter[]
  isAllowedDevice: (device: LastChancesHidDeviceLike) => boolean
  serializers: readonly DualSenseHidTransportSerializer[]
  /**
   * Classifies an allowlisted device that no installed serializer supports, so the
   * capability can explain the real constraint (e.g. a Bluetooth pad that must be
   * re-connected over USB, finding M117). Returning null/undefined keeps the
   * generic no-verified-transport message.
   */
  unsupportedDeviceCapability?: (device: LastChancesHidDeviceLike) => {
    permission: PermissionState
    message: string
  } | null
}

type PermissionState = LastChancesFeedbackOutputCapability['permission']

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

function isPermissionDenial(error: unknown): boolean {
  if (!(error instanceof Error)) return false
  return error.name === 'NotFoundError'
    || error.name === 'NotAllowedError'
    || error.name === 'SecurityError'
}

export class DualSenseWebHidDriver implements LastChancesEnhancedFeedbackOutput {
  private device: LastChancesHidDeviceLike | null = null
  private serializer: DualSenseHidTransportSerializer | null = null
  private permission: PermissionState = 'not-requested'
  private status: LastChancesFeedbackOutputCapability['status'] = 'controls-only'
  private message: string | null = null
  private writer: Promise<void> = Promise.resolve()
  private disposed = false

  constructor(private readonly options: DualSenseWebHidDriverOptions) {
    if (!options.hid || options.filters.length === 0 || options.serializers.length === 0) {
      this.permission = 'unavailable'
      this.status = 'unavailable'
      this.message = 'Enhanced DualSense output has no verified HID transport.'
    }
  }

  capability(): LastChancesFeedbackOutputCapability {
    return {
      tier: this.device?.opened && this.serializer ? 2 : 0,
      status: this.device?.opened && this.serializer ? 'enhanced' : this.status,
      permission: this.permission,
      message: this.message,
    }
  }

  get activeTransport(): DualSenseHidTransport | null {
    return this.device?.opened ? this.serializer?.transport ?? null : null
  }

  /** The only method that may invoke the browser chooser. Call it from an explicit UI action. */
  async enableEnhancedFeatures(): Promise<boolean> {
    if (this.disposed || this.device?.opened) return Boolean(this.device?.opened)
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
      const allowed = devices.find(device => this.options.isAllowedDevice(device))
      const classified = allowed
        ? this.options.unsupportedDeviceCapability?.(allowed) ?? null
        : null
      this.permission = classified?.permission ?? 'unavailable'
      this.status = 'unavailable'
      this.message = classified?.message
        ?? 'The selected HID device has no physically verified transport serializer.'
      return false
    }

    this.device = match.device
    this.serializer = match.serializer
    this.permission = 'granted'
    try {
      if (!this.device.opened) await this.device.open()
      await this.writePackets(this.serializer.neutral())
      this.status = 'enhanced'
      this.message = null
      return true
    } catch (error) {
      await this.failAndClose(`DualSense open failed: ${errorMessage(error)}`)
      return false
    }
  }

  async play(effect: LastChancesFeedbackEffect): Promise<boolean> {
    const device = this.device
    const serializer = this.serializer
    if (this.disposed || !device?.opened || !serializer) return false

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
      return true
    } catch (error) {
      await this.failAndClose(`DualSense output failed: ${errorMessage(error)}`)
      return false
    }
  }

  async neutralize(): Promise<void> {
    const device = this.device
    const serializer = this.serializer
    if (!device?.opened || !serializer) return
    await this.writer
    try {
      await this.writePackets(serializer.neutral())
    } catch (error) {
      await this.failAndClose(`DualSense cleanup failed: ${errorMessage(error)}`)
    }
  }

  async disableEnhancedFeatures(): Promise<void> {
    await this.writer
    const device = this.device
    const serializer = this.serializer
    if (device?.opened && serializer) {
      try {
        await this.writePackets(serializer.neutral())
      } catch {
        // Closing is still mandatory after a failed stop packet.
      }
    }
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

  async dispose(): Promise<void> {
    if (this.disposed) return
    await this.disableEnhancedFeatures()
    this.disposed = true
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
