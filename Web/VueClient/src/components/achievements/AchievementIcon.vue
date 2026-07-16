<script setup lang="ts">
import { computed, type Component } from 'vue'
import {
  Award,
  BadgeCheck,
  Bird,
  Box,
  BriefcaseBusiness,
  Bug,
  Cat,
  Circle,
  Compass,
  Crown,
  Dices,
  Dog,
  Eye,
  Feather,
  Flame,
  Flower2,
  Gamepad2,
  Gem,
  Ghost,
  Globe,
  HandFist,
  Handshake,
  Heart,
  Hourglass,
  Medal,
  Mountain,
  NotebookPen,
  Orbit,
  Radiation,
  Rocket,
  Scale,
  Search,
  Shield,
  ShieldCheck,
  Skull,
  Sparkles,
  Sword,
  Swords,
  Target,
  Trophy,
  Turtle,
  VenetianMask,
  WandSparkles,
  Zap,
} from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  icon?: string
  size?: number
  strokeWidth?: number
}>(), {
  icon: 'trophy',
  size: 24,
  strokeWidth: 1.8,
})

const icons: Record<string, Component> = {
  arrows: Orbit,
  axe: Sword,
  badge: BadgeCheck,
  balance: Scale,
  bat: Feather,
  bird: Bird,
  bolt: Zap,
  box: Box,
  briefcase: BriefcaseBusiness,
  bug: Bug,
  cat: Cat,
  circle: Circle,
  compass: Compass,
  crown: Crown,
  crystal: Gem,
  detective: Search,
  dice: Dices,
  'dice-six': Dices,
  dragon: Flame,
  eye: Eye,
  'eye-glow': Eye,
  falling: Feather,
  fire: Flame,
  fist: HandFist,
  flame: Flame,
  games: Gamepad2,
  ghost: Ghost,
  globe: Globe,
  handshake: Handshake,
  heart: Heart,
  hourglass: Hourglass,
  jester: VenetianMask,
  lotus: Flower2,
  medal: Medal,
  medallion: Award,
  mountain: Mountain,
  notebook: NotebookPen,
  peace: Heart,
  phoenix: WandSparkles,
  pickle: Sparkles,
  portal: Orbit,
  puppet: Target,
  pyramid: Mountain,
  radiation: Radiation,
  rocket: Rocket,
  shield: Shield,
  'shield-cross': ShieldCheck,
  skull: Skull,
  'skull-crossbones': Skull,
  sparkle: Sparkles,
  sparkles: Sparkles,
  star: Sparkles,
  stars: Sparkles,
  sword: Sword,
  swords: Swords,
  target: Target,
  trophy: Trophy,
  'trophy-broken': Award,
  turtle: Turtle,
  wolf: Dog,
}

const resolvedIcon = computed(() => icons[props.icon.trim().toLowerCase()] ?? Trophy)
</script>

<template>
  <component
    :is="resolvedIcon"
    aria-hidden="true"
    :size="size"
    :stroke-width="strokeWidth"
  />
</template>
