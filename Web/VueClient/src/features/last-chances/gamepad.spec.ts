import { describe, expect, it } from 'vitest'
import {
  createLastChancesGamepadAdapter,
  readLastChancesGamepads,
} from './gamepad'
import type {
  LastChancesGamepadButtonLike,
  LastChancesGamepadLike,
} from './gamepad'

// Shipping bindings: left hand on L1 (4), right hand on R1 (5).
const config = {
  deadZone: 0.18,
  leftButton: 4,
  rightButton: 5,
}

function button(pressed = false, value = pressed ? 1 : 0): LastChancesGamepadButtonLike {
  return { pressed, value }
}

function gamepad(options: Partial<LastChancesGamepadLike> & { index: number }): LastChancesGamepadLike {
  return {
    axes: [0, 0, 0, 0],
    buttons: Array.from({ length: 16 }, () => button()),
    connected: true,
    id: `Test gamepad ${options.index}`,
    mapping: 'standard',
    ...options,
  }
}

describe('99LC gamepad adapter', () => {
  it('normalizes standard axes and the configured L1/R1 shoulder buttons', () => {
    const buttons = Array.from({ length: 16 }, () => button())
    buttons[0] = button(true)
    buttons[1] = button(true)
    buttons[4] = button(true)
    buttons[5] = button(false, 0.7)
    buttons[6] = button(false, 0.31)
    buttons[7] = button(false, 0.82)
    const reading = readLastChancesGamepads([
      gamepad({ index: 3, axes: [0.1, -0.5, 0.6, 0.2], buttons }),
    ], config)

    expect(reading).toMatchObject({
      status: 'active',
      activeIndex: 3,
      connectedCount: 1,
      profile: 'standard',
      axes: [0, -0.5, 0.6, 0.2],
      move: { x: 0, y: -0.5 },
      aim: { x: 0.6, y: 0.2 },
      buttons: { left: true, right: true },
      triggers: { left: 0.31, right: 0.82 },
      sourceButtonIndexes: { left: 4, right: 5 },
    })
    expect(reading.buttons).toMatchObject({
      l1: true,
      r1: true,
      cross: true,
      circle: true,
    })
  })

  it('maps a raw Bluetooth DualSense to canonical axes with shoulders untouched', () => {
    const buttons = Array.from({ length: 18 }, () => button())
    // Raw Sony face order is Square, Cross, Circle, Triangle.
    buttons[1] = button(true)
    buttons[2] = button(true)
    buttons[4] = button(true)
    buttons[5] = button(false, 0.65)
    buttons[6] = button(false, 0.27)
    buttons[7] = button(false, 0.74)
    const reading = readLastChancesGamepads([
      gamepad({
        index: 1,
        id: '054c-0ce6-Sony Interactive Entertainment Wireless Controller',
        mapping: '',
        axes: [0.5, -0.4, -0.75, -1, -1, 0.6],
        buttons,
      }),
    ], config)

    expect(reading).toMatchObject({
      profile: 'sony-raw',
      axes: [0.5, -0.4, -0.75, 0.6],
      move: { x: 0.5, y: -0.4 },
      aim: { x: -0.75, y: 0.6 },
      buttons: { left: true, right: true },
      triggers: { left: 0.27, right: 0.74 },
      sourceButtonIndexes: { left: 4, right: 5 },
    })
    expect(reading.buttons).toMatchObject({ cross: true, circle: true, l1: true, r1: true })
    expect(reading.canonicalButtons[0]).toMatchObject({ pressed: true, value: 1 })
    expect(reading.canonicalButtons[1]).toMatchObject({ pressed: true, value: 1 })
  })

  it('still remaps configured face buttons through the raw Sony profile', () => {
    const faceConfig = { deadZone: 0.18, leftButton: 2, rightButton: 0 }
    const buttons = Array.from({ length: 18 }, () => button())
    buttons[0] = button(true)
    buttons[1] = button(false, 0.65)
    const reading = readLastChancesGamepads([
      gamepad({
        index: 1,
        id: '054c-0ce6-Sony Interactive Entertainment Wireless Controller',
        mapping: '',
        axes: [0.5, -0.4, -0.75, -1, -1, 0.6],
        buttons,
      }),
    ], faceConfig)

    expect(reading).toMatchObject({
      profile: 'sony-raw',
      buttons: { left: true, right: true },
      sourceButtonIndexes: { left: 0, right: 1 },
    })
  })

  it('prefers meaningful input but preserves the current pad while it remains active', () => {
    const adapter = createLastChancesGamepadAdapter(config)
    const idleFirst = gamepad({ index: 0 })
    const idleSecond = gamepad({ index: 4 })

    expect(adapter.poll([idleFirst, idleSecond])).toMatchObject({
      status: 'idle',
      activeIndex: 0,
    })

    const activeSecond = gamepad({ index: 4, axes: [0.7, 0, 0, 0] })
    expect(adapter.poll([idleFirst, activeSecond]).activeIndex).toBe(4)

    const activeFirst = gamepad({ index: 0, axes: [-0.8, 0, 0, 0] })
    expect(adapter.poll([activeFirst, activeSecond]).activeIndex).toBe(4)
    expect(adapter.poll([activeFirst, idleSecond]).activeIndex).toBe(0)
  })

  it('treats a shallow analog trigger pull as meaningful for active-pad arbitration', () => {
    const buttons = Array.from({ length: 16 }, () => button())
    buttons[6] = button(false, 0.3)
    const reading = readLastChancesGamepads([
      gamepad({ index: 0 }),
      gamepad({ index: 2, buttons }),
    ], { ...config, analogTriggerThreshold: 0.22 })

    expect(reading).toMatchObject({ activeIndex: 2, status: 'active' })
    expect(reading.triggers.left).toBe(0.3)
  })

  it('recovers cleanly when the selected pad disconnects and later reconnects', () => {
    const adapter = createLastChancesGamepadAdapter(config)
    const selected = gamepad({ index: 2, axes: [0, -1, 0, 0] })
    const fallback = gamepad({ index: 7 })

    expect(adapter.poll([selected, fallback]).activeIndex).toBe(2)
    expect(adapter.poll([gamepad({ index: 2, connected: false }), fallback])).toMatchObject({
      status: 'idle',
      activeIndex: 7,
      connectedCount: 1,
    })
    expect(adapter.poll([null, gamepad({ index: 7, connected: false })])).toMatchObject({
      status: 'disconnected',
      activeIndex: null,
      connectedCount: 0,
      buttons: { left: false, right: false },
    })
    expect(adapter.poll([gamepad({ index: 2 })])).toMatchObject({
      status: 'idle',
      activeIndex: 2,
    })
  })
})
