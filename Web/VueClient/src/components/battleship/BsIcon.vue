<script setup lang="ts">
import { computed, type Component } from 'vue'
import {
  Anchor,
  ArrowLeft,
  Ban,
  Check,
  Coins,
  Compass,
  Crosshair,
  Eye,
  Flag,
  Flame,
  Gauge,
  Hourglass,
  Map,
  Plus,
  RotateCw,
  Sailboat,
  Ship,
  Skull,
  Sparkles,
  Swords,
  Target,
  Trophy,
  Waves,
  X,
  Zap,
} from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  icon?: string
  size?: number
  strokeWidth?: number
}>(), {
  icon: 'anchor',
  size: 16,
  strokeWidth: 1.8,
})

const icons: Record<string, Component> = {
  anchor: Anchor,
  back: ArrowLeft,
  ban: Ban,
  check: Check,
  coins: Coins,
  compass: Compass,
  crosshair: Crosshair,
  eye: Eye,
  flag: Flag,
  flame: Flame,
  gauge: Gauge,
  hourglass: Hourglass,
  map: Map,
  plus: Plus,
  rotate: RotateCw,
  sailboat: Sailboat,
  ship: Ship,
  skull: Skull,
  sparkles: Sparkles,
  swords: Swords,
  target: Target,
  trophy: Trophy,
  waves: Waves,
  x: X,
  zap: Zap,
}

const resolvedIcon = computed(() => icons[props.icon.trim().toLowerCase()] ?? Anchor)
</script>

<template>
  <component
    :is="resolvedIcon"
    aria-hidden="true"
    :size="size"
    :stroke-width="strokeWidth"
  />
</template>
