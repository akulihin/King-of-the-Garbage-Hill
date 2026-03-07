export interface WheelState {
  phase: 'hidden' | 'modifiers' | 'reveal' | 'spinning' | 'landed'
  spinDeg: number
  result: 'win' | 'loss'
}
