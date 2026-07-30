import type {
  BattleshipSummon,
  BattleshipSummonMarker,
} from 'src/services/signalr'

const summonNames: Record<string, string> = {
  Ram: 'Таран',
  Scout: 'Разведчик',
  Brander: 'Брандер',
  CursedBoat: 'Проклятый корабль',
  PirateBoat: 'Пиратская лодка',
}

const summonIcons: Record<string, string> = {
  Ram: 'ram',
  Scout: 'scout',
  Brander: 'brander',
  CursedBoat: 'cursedBoat',
  PirateBoat: 'pirateBoat',
}

export function summonTypeName(type: string): string {
  return summonNames[type] ?? type
}

export function boardingShipName(sourceShipName?: string | null): string {
  return sourceShipName
    ? `Абордажный корабль (${sourceShipName})`
    : 'Абордажный корабль'
}

export function activeSummonName(summon: BattleshipSummon): string {
  if (summon.isBoardingShip)
    return summon.sourceShipName || 'Абордажный корабль'
  return summonTypeName(summon.type)
}

export function summonMarkerName(marker: BattleshipSummonMarker): string {
  return marker.isBoardingShip
    ? boardingShipName(marker.sourceShipName)
    : summonTypeName(marker.type)
}

export function summonIconKey(type: string, isBoardingShip = false): string {
  return isBoardingShip ? 'ship1' : (summonIcons[type] ?? 'anchor')
}

export function summonMarkerClass(marker: BattleshipSummonMarker): string {
  return marker.isBoardingShip ? 'boarding' : marker.type.toLowerCase()
}
