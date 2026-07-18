import { fireEvent, render, waitFor } from '@testing-library/vue'
import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import BuilderDrawer from '../../components/empires-endgame/BuilderDrawer.vue'
import { cloneEmpiresConfig, parseEmpiresConfig } from './config'
import type { EmpiresEndgameConfig } from './types'

describe('Empire\'s Endgame Builder schema-v12 JSON boundary', () => {
  it('round-trips economy content, domestic and external economy, combat, and lifecycle rules through every JSON control', async () => {
    const config = cloneEmpiresConfig(defaultConfigJson)
    const updates: EmpiresEndgameConfig[] = []
    const onExport = vi.fn()
    const { container, getByRole } = render(BuilderDrawer, {
      props: {
        config,
        'onUpdate:config': (next: EmpiresEndgameConfig) => updates.push(next),
        onExport,
      },
    })

    await fireEvent.click(getByRole('button', { name: /Весь JSON/ }))
    const editor = container.querySelector<HTMLTextAreaElement>('.json-editor')
    if (!editor) throw new Error('Builder JSON editor did not render')
    const exportedText = editor.value
    const exported = parseEmpiresConfig(exportedText)

    expect(exported.combat).toEqual(config.combat)
    expect(exported.combat.enabled).toBe(true)
    expect(exported.combat.equipment).toHaveLength(config.combat.equipment.length)
    expect(exported.schemaVersion).toBe(12)
    expect(exported.quests).toEqual(config.quests)
    expect(exported.quests.definitions.find(quest => quest.id === 'quest-palach')
      ?.stages.flatMap(stage => stage.nodes)).toHaveLength(43)
    expect(exported.empire.domesticEconomy).toEqual(config.empire.domesticEconomy)
    expect(exported.empire.externalEconomy).toEqual(config.empire.externalEconomy)
    expect(exported.empire.economyContent).toEqual(config.empire.economyContent)
    expect(exported.empire.seasons).toEqual(config.empire.seasons)
    expect(exported.empire.hiddenCombinations).toEqual(config.empire.hiddenCombinations)
    expect(exported.empire.epidemics).toEqual(config.empire.epidemics)
    expect(exported.empire.medical).toEqual(config.empire.medical)
    expect(exported.empire.technologies.map(technology => technology.sides))
      .toEqual(config.empire.technologies.map(technology => technology.sides))
    expect(exported.empire.steelResearch).toEqual(config.empire.steelResearch)
    expect(exported.empire.technologies.map(technology => technology.steel))
      .toEqual(config.empire.technologies.map(technology => technology.steel))
    expect(exported.empire.units?.map(unit => unit.loadouts))
      .toEqual(config.empire.units?.map(unit => unit.loadouts))
    expect(exported.td.equipmentProductionLines).toEqual(config.td.equipmentProductionLines)
    expect(exported.td.towerBases?.map(base => base.loadouts))
      .toEqual(config.td.towerBases?.map(base => base.loadouts))

    await fireEvent.update(editor, exportedText)
    await fireEvent.click(getByRole('button', { name: /Проверить и применить/ }))
    expect(updates.at(-1)).toEqual(config)

    await fireEvent.click(getByRole('button', { name: /Экспорт/ }))
    expect(onExport).toHaveBeenCalledTimes(1)

    const input = container.querySelector<HTMLInputElement>('input[type="file"]')
    if (!input) throw new Error('Builder JSON import input did not render')
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: [new File([`${exportedText}\n`], 'empires-combat.json', { type: 'application/json' })],
    })
    await fireEvent.update(input)
    await waitFor(() => expect(updates).toHaveLength(2))
    expect(updates.at(-1)?.combat).toEqual(config.combat)

    const malformed = structuredClone(config)
    malformed.empire.epidemics.definitions[0].stages[0].spreadChance = 2
    await fireEvent.update(editor, JSON.stringify(malformed))
    await fireEvent.click(getByRole('button', { name: /Проверить и применить/ }))
    expect(container.querySelector('.json-error')?.textContent).toMatch(/epidemic.*stage.*invalid/i)
    expect(updates).toHaveLength(2)

    const malformedEconomy = structuredClone(config)
    malformedEconomy.empire.domesticEconomy.loan.termCons = 0
    await fireEvent.update(editor, JSON.stringify(malformedEconomy))
    await fireEvent.click(getByRole('button', { name: /Проверить и применить/ }))
    expect(container.querySelector('.json-error')?.textContent).toMatch(/loan.*schedule/i)
    expect(updates).toHaveLength(2)

    const malformedContent = structuredClone(config)
    malformedContent.empire.economyContent.smuggling.taxChoiceId = 'missing-choice'
    await fireEvent.update(editor, JSON.stringify(malformedContent))
    await fireEvent.click(getByRole('button', { name: /Проверить и применить/ }))
    expect(container.querySelector('.json-error')?.textContent).toMatch(/smuggling\.taxChoiceId.*live event choice/i)
    expect(updates).toHaveLength(2)
  })
})
