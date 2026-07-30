import type { FightEntry } from 'src/services/signalr'

type R3PresentationFight = Pick<
  FightEntry,
  | 'attackerName'
  | 'defenderName'
  | 'outcome'
  | 'usedRandomRoll'
  | 'randomNumber'
  | 'randomForPoint'
  | 'maxRandomNumber'
>

export function createR3PresentationKey(input: {
  roundKey: string | number | undefined
  fightIndex: number
  perspectiveUsername: string
  fight: R3PresentationFight | null
}): string {
  const { roundKey, fightIndex, perspectiveUsername, fight } = input
  return JSON.stringify([
    roundKey ?? '',
    fightIndex,
    perspectiveUsername,
    fight?.attackerName ?? '',
    fight?.defenderName ?? '',
    fight?.outcome ?? '',
    fight?.usedRandomRoll ?? false,
    fight?.randomNumber ?? null,
    fight?.randomForPoint ?? null,
    fight?.maxRandomNumber ?? null,
  ])
}
