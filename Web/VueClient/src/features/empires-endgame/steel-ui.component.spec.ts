import { cleanup, fireEvent, render } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import CityView from '../../components/empires-endgame/CityView.vue'
import TechTree from '../../components/empires-endgame/TechTree.vue'

afterEach(cleanup)

function steelNode(available: boolean) {
  return {
    id: 'steel-cross-spearhead',
    name: 'Крестовидное',
    description: 'Серийный выпуск крестовидного наконечника.',
    branch: 'steel',
    tier: 3,
    x: 100,
    y: 100,
    requires: ['steel-diamond-spearhead'],
    costKnowledge: 20,
    costGold: 10,
    costs: ['20 Знания', '10 Золото'],
    timeCost: 5,
    researched: false,
    available,
    blockedReason: available ? undefined : 'Нужно: Ромбовидное',
    steelBranch: 'steel-polearms',
    steelGeneration: 3,
    steelStage: 'minus' as const,
    steelElite: true,
    steelPayoff: 'equipment' as const,
    costMultiplier: 2,
    entryFromName: 'Крестовая булава',
    freeEligibleCon: 7,
    deferredSubfeatures: [{ id: 'test-part', reason: 'Часть механики отложена.' }],
  }
}

describe('Empire\'s Endgame steel UI', () => {
  it('shows authored steel metadata and treats authoritative availability as the research gate', async () => {
    const view = render(TechTree, {
      props: {
        nodes: [steelNode(false)],
        selectedId: 'steel-cross-spearhead',
        knowledge: 999,
        gold: 999,
        days: 999,
      },
    })

    expect(view.getByTestId('selected-steel-metadata').textContent).toContain('steel-polearms')
    expect(view.getByTestId('selected-steel-metadata').textContent).toContain('поколение 3−')
    expect(view.getByText('Цена покинутой ветви: ×2')).toBeTruthy()
    expect(view.getByText('Бесплатное открытие: кон 7')).toBeTruthy()
    expect(view.getByText('Часть механики отложена.')).toBeTruthy()
    expect((view.getByRole('button', { name: /Изучить/ }) as HTMLButtonElement).disabled).toBe(true)

    await view.rerender({
      nodes: [steelNode(true)],
      selectedId: 'steel-cross-spearhead',
      knowledge: 0,
      gold: 0,
      days: 0,
    })
    expect((view.getByRole('button', { name: /Изучить/ }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('renders equipment stock, morale bounds, and persistent cohort loadouts together', () => {
    const view = render(CityView, {
      props: {
        activeCityId: 'capital',
        gold: 40,
        cities: [{
          id: 'capital',
          name: 'Тетракор',
          population: 100,
          militaryPopulation: 25,
          foodProduced: 100,
          foodConsumed: 70,
          buildings: [],
          slots: [],
          armyMorale: { value: 2, minimum: 2, maximum: 5 },
          equipmentStock: [{ id: 'weapon-cross-spear', name: 'Крестовидное', value: 12 }],
          armyCohorts: [{
            id: 'capital:regular:cross',
            unitName: 'Регулярная армия',
            count: 8,
            loadoutId: 'cross-spear',
            weaponName: 'Крестовидное',
          }],
        }],
      },
    })

    expect(view.getByText(/Боевой дух 2/)).toBeTruthy()
    expect(view.getByText('Общий склад снаряжения')).toBeTruthy()
    expect(view.getAllByText(/Крестовидное/).length).toBeGreaterThanOrEqual(2)
    expect(view.getByText('Регулярная армия × 8')).toBeTruthy()
    expect(view.getByText(/cross-spear/)).toBeTruthy()
  })

  it('emits quantity changes so the parent can project the matching authoritative quote', async () => {
    const city = {
      id: 'capital',
      name: 'Тетракор',
      population: 100,
      militaryPopulation: 25,
      foodProduced: 100,
      foodConsumed: 70,
      slots: [{ id: 'slot-barracks', kind: 'barracks' as const }],
      buildings: [{
        id: 'barracks',
        slotId: 'slot-barracks',
        slot: 'barracks' as const,
        name: 'Казарма',
        level: 1,
        maxLevel: 1,
      }],
      recruitableUnits: [{
        id: 'regular',
        name: 'Регулярный отряд',
        count: 0,
        foodUpkeep: 1_000,
        populationCost: 1,
        timeCost: 0,
        quantity: 1,
        maxQuantity: 5,
        loadoutId: 'cross-spear',
        equipmentCosts: ['1 Крестовидное'],
      }],
    }
    const view = render(CityView, {
      props: {
        activeCityId: 'capital',
        selectedBuildingId: 'barracks',
        gold: 40,
        cities: [city],
      },
    })
    const input = view.getByRole('spinbutton', { name: /Количество Регулярный отряд/ })
    await fireEvent.update(input, '3')
    expect(view.emitted().recruitQuantity?.[0]).toEqual(['capital', 'regular', 3])

    city.recruitableUnits[0].quantity = 3
    city.recruitableUnits[0].equipmentCosts = ['3 Крестовидное']
    await view.rerender({ cities: [city] })
    expect(view.getByText('Снаряжение: 3 Крестовидное')).toBeTruthy()
    await fireEvent.click(view.getByRole('button', { name: 'Нанять' }))
    expect(view.emitted().recruit?.[0]).toEqual(['capital', 'regular', 3])
  })
})
