<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  AlertTriangle,
  Castle,
  Landmark,
  MapPin,
  Mountain,
  Navigation,
  Plus,
  Sprout,
  Trash2,
  Trees,
  Waves,
  X,
} from 'lucide-vue-next'

type MapObjectKind = 'city' | 'capital' | 'fortress' | 'mountain' | 'river' | 'forest' | 'landmark'

interface EmpireMapObject {
  id: string
  kind: MapObjectKind
  label: string
  x: number
  y: number
  cityId?: string
  image?: string
  size?: EmpireMapPoint
  rotation?: number
  accessible?: boolean
  disabledReason?: string
  epidemicCount?: number
  epidemicStage?: string
  epidemicTurns?: number
}

interface EmpireMapPoint {
  x: number
  y: number
}

interface EmpireSubregionView {
  id: string
  name: string
  biome: string
  polygon: EmpireMapPoint[]
  regionId?: string
}

interface EmpireRegionView {
  id: string
  name: string
  shortName?: string
  biome: string
  accent?: string
  description?: string
  accessible?: boolean
  disabledReason?: string
  objects: EmpireMapObject[]
}

const props = withDefaults(defineProps<{
  regions: EmpireRegionView[]
  activeRegionId: string
  subregions?: EmpireSubregionView[]
  selectedSubregionId?: string | null
  selectedObjectId?: string | null
  editable?: boolean
  compact?: boolean
}>(), {
  subregions: () => [],
  editable: false,
  compact: false,
})

const emit = defineEmits<{
  selectRegion: [regionId: string]
  openCity: [cityId: string]
  moveObject: [regionId: string, objectId: string, x: number, y: number]
  addObject: [regionId: string, kind: MapObjectKind, x: number, y: number]
  selectObject: [regionId: string, objectId: string | null]
  updateObject: [regionId: string, objectId: string, patch: Partial<Pick<EmpireMapObject, 'label' | 'image' | 'size' | 'rotation'>>]
  removeObject: [regionId: string, objectId: string]
  selectSubregion: [regionId: string, subregionId: string | null]
  addSubregion: [regionId: string]
  updateSubregion: [regionId: string, subregionId: string, patch: Partial<Pick<EmpireSubregionView, 'name' | 'biome'>>]
  removeSubregion: [regionId: string, subregionId: string]
}>()

const localSelectedObjectId = ref<string | null>(null)
const localSelectedSubregionId = ref<string | null>(null)

const activeRegion = computed(() => props.regions.find(region => region.id === props.activeRegionId)
  ?? props.regions[0]
  ?? null)

const activeSubregions = computed(() => {
  if (!activeRegion.value) return []
  return props.subregions.filter(subregion => !subregion.regionId || subregion.regionId === activeRegion.value?.id)
})

const resolvedSelectedObjectId = computed(() => props.selectedObjectId !== undefined
  ? props.selectedObjectId
  : localSelectedObjectId.value)

const resolvedSelectedSubregionId = computed(() => props.selectedSubregionId !== undefined
  ? props.selectedSubregionId
  : localSelectedSubregionId.value)

const selectedObject = computed(() => activeRegion.value?.objects.find(object => object.id === resolvedSelectedObjectId.value)
  ?? null)

const selectedSubregion = computed(() => activeSubregions.value.find(subregion => subregion.id === resolvedSelectedSubregionId.value)
  ?? null)

const knownBiomes = ['central', 'ice', 'forest', 'desert', 'swamp', 'mountain', 'coast', 'steppe']

watch(() => props.activeRegionId, () => {
  localSelectedObjectId.value = null
  localSelectedSubregionId.value = null
})

const palette: Array<{ kind: MapObjectKind, label: string, icon: typeof Castle }> = [
  { kind: 'city', label: 'Город', icon: MapPin },
  { kind: 'fortress', label: 'Крепость', icon: Castle },
  { kind: 'mountain', label: 'Горы', icon: Mountain },
  { kind: 'river', label: 'Река', icon: Waves },
  { kind: 'forest', label: 'Лес', icon: Trees },
  { kind: 'landmark', label: 'Монумент', icon: Landmark },
]

function iconFor(kind: MapObjectKind) {
  if (kind === 'capital') return Landmark
  return palette.find(item => item.kind === kind)?.icon ?? Navigation
}

function beginPaletteDrag(event: DragEvent, kind: MapObjectKind) {
  if (!props.editable || !event.dataTransfer) return
  event.dataTransfer.effectAllowed = 'copy'
  event.dataTransfer.setData('application/x-ee-map-kind', kind)
}

function beginObjectDrag(event: DragEvent, objectId: string) {
  if (!props.editable || !event.dataTransfer) return
  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('application/x-ee-map-object', objectId)
}

function dropOnMap(event: DragEvent) {
  if (!props.editable || !activeRegion.value) return
  const target = event.currentTarget as HTMLElement
  const bounds = target.getBoundingClientRect()
  const x = Math.max(3, Math.min(97, (event.clientX - bounds.left) / Math.max(1, bounds.width) * 100))
  const y = Math.max(5, Math.min(94, (event.clientY - bounds.top) / Math.max(1, bounds.height) * 100))
  const objectId = event.dataTransfer?.getData('application/x-ee-map-object')
  const kind = event.dataTransfer?.getData('application/x-ee-map-kind') as MapObjectKind | undefined
  if (objectId) emit('moveObject', activeRegion.value.id, objectId, x, y)
  else if (kind) emit('addObject', activeRegion.value.id, kind, x, y)
}

function activateObject(object: EmpireMapObject) {
  if (props.editable && activeRegion.value) {
    localSelectedObjectId.value = object.id
    localSelectedSubregionId.value = null
    emit('selectObject', activeRegion.value.id, object.id)
    emit('selectSubregion', activeRegion.value.id, null)
    return
  }
  if (object.accessible === false) return
  if ((object.kind === 'city' || object.kind === 'capital') && object.cityId) {
    emit('openCity', object.cityId)
  }
}

function selectSubregion(subregionId: string) {
  if (!props.editable || !activeRegion.value) return
  localSelectedSubregionId.value = subregionId
  localSelectedObjectId.value = null
  emit('selectSubregion', activeRegion.value.id, subregionId)
  emit('selectObject', activeRegion.value.id, null)
}

function clearInspector() {
  if (!activeRegion.value) return
  localSelectedObjectId.value = null
  localSelectedSubregionId.value = null
  emit('selectObject', activeRegion.value.id, null)
  emit('selectSubregion', activeRegion.value.id, null)
}

function removeSelectedObject() {
  if (!activeRegion.value || !selectedObject.value) return
  emit('removeObject', activeRegion.value.id, selectedObject.value.id)
  clearInspector()
}

function removeSelectedSubregion() {
  if (!activeRegion.value || !selectedSubregion.value) return
  emit('removeSubregion', activeRegion.value.id, selectedSubregion.value.id)
  clearInspector()
}

function objectStyle(object: EmpireMapObject) {
  const style: Record<string, string> = {
    left: `${object.x}%`,
    top: `${object.y}%`,
    '--object-rotation': `${object.rotation ?? 0}deg`,
  }
  if (object.kind === 'river' && object.size) {
    style.width = `${Math.max(4, Math.min(100, object.size.x))}%`
    style.height = `${Math.max(3, Math.min(100, object.size.y))}%`
  }
  return style
}

function polygonPoints(subregion: EmpireSubregionView) {
  return subregion.polygon
    .map(point => `${Math.max(0, Math.min(100, point.x))},${Math.max(0, Math.min(100, point.y))}`)
    .join(' ')
}

function subregionCenter(subregion: EmpireSubregionView) {
  if (!subregion.polygon.length) return { x: 50, y: 50 }
  const total = subregion.polygon.reduce((sum, point) => ({ x: sum.x + point.x, y: sum.y + point.y }), { x: 0, y: 0 })
  return { x: total.x / subregion.polygon.length, y: total.y / subregion.polygon.length }
}

function biomeOptions(current: string) {
  return Array.from(new Set([current, ...knownBiomes]))
}

function inputValue(event: Event) {
  return (event.currentTarget as HTMLInputElement).value
}

function numericInputValue(event: Event) {
  const value = Number((event.currentTarget as HTMLInputElement).value)
  return Number.isFinite(value) ? value : 0
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}
</script>

<template>
  <section
    v-if="activeRegion"
    class="empire-map"
    :class="[`biome-${activeRegion.biome}`, { compact, editable }]"
    :style="{ '--region-accent': activeRegion.accent || '#c6a86b' }"
  >
    <header class="map-heading">
      <div>
        <span class="map-kicker">{{ editable ? 'Редактор ландшафта' : 'Карта империи' }}</span>
        <h2>{{ activeRegion.name }}</h2>
        <p v-if="activeRegion.description">{{ activeRegion.description }}</p>
      </div>
      <div v-if="editable" class="map-palette" aria-label="Палитра объектов карты">
        <button
          class="add-subregion-button"
          type="button"
          title="Добавить землю в активный регион"
          @click="emit('addSubregion', activeRegion.id)"
        >
          <Plus :size="16" />
          <span>Земля</span>
        </button>
        <button
          v-for="item in palette"
          :key="item.kind"
          draggable="true"
          type="button"
          :title="`Перетащить: ${item.label}`"
          @dragstart="beginPaletteDrag($event, item.kind)"
        >
          <component :is="item.icon" :size="16" />
          <span>{{ item.label }}</span>
        </button>
      </div>
    </header>

    <div class="map-stage" @dragover.prevent @drop.prevent="dropOnMap">
      <div class="terrain terrain-one" aria-hidden="true" />
      <div class="terrain terrain-two" aria-hidden="true" />
      <div class="terrain terrain-three" aria-hidden="true" />
      <div class="map-horizon" aria-hidden="true" />

      <svg
        v-if="activeSubregions.length"
        class="subregion-layer"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        aria-label="Земли активного региона"
      >
        <g
          v-for="subregion in activeSubregions"
          :key="subregion.id"
          class="subregion"
          :class="[`subregion-${subregion.biome}`, { selected: subregion.id === resolvedSelectedSubregionId, selectable: editable }]"
          :tabindex="editable ? 0 : undefined"
          :role="editable ? 'button' : undefined"
          :aria-label="editable ? `Выбрать землю ${subregion.name}` : subregion.name"
          @click.stop="selectSubregion(subregion.id)"
          @keydown.enter.prevent="selectSubregion(subregion.id)"
          @keydown.space.prevent="selectSubregion(subregion.id)"
        >
          <polygon :points="polygonPoints(subregion)" />
          <text
            v-if="subregion.polygon.length"
            :x="subregionCenter(subregion).x"
            :y="subregionCenter(subregion).y"
            text-anchor="middle"
            dominant-baseline="middle"
          >{{ subregion.name }}</text>
        </g>
      </svg>

      <button
        v-for="object in activeRegion.objects"
        :key="object.id"
        class="map-object"
        :class="[`kind-${object.kind}`, { actionable: Boolean(object.cityId) && object.accessible !== false, inaccessible: object.accessible === false, selected: object.id === resolvedSelectedObjectId, 'sized-river': object.kind === 'river' && object.size }]"
        :style="objectStyle(object)"
        :draggable="editable"
        type="button"
        :disabled="!editable && object.accessible === false"
        :data-testid="object.cityId ? `map-city-${object.cityId}` : undefined"
        :title="object.disabledReason"
        :aria-pressed="editable ? object.id === resolvedSelectedObjectId : undefined"
        @dragstart="beginObjectDrag($event, object.id)"
        @click="activateObject(object)"
      >
        <span v-if="object.kind === 'river' && object.size" class="river-line" aria-hidden="true">
          <Waves :size="18" />
        </span>
        <span v-else class="object-icon">
          <img v-if="object.image" :src="object.image" alt="" @error="hideBrokenImage" />
          <component :is="iconFor(object.kind)" v-else :size="20" />
        </span>
        <span class="object-label">{{ object.label }}</span>
        <span
          v-if="(object.epidemicCount ?? 0) > 0"
          class="object-epidemic"
          :data-testid="object.cityId ? `map-epidemic-${object.cityId}` : undefined"
        >☣ {{ object.epidemicStage }} · {{ object.epidemicTurns }}</span>
        <span v-if="object.accessible === false" class="object-state"><AlertTriangle :size="11" /> Недоступен</span>
      </button>

      <aside v-if="editable && (selectedSubregion || selectedObject)" class="map-inspector" aria-label="Редактор выбранного элемента карты">
        <header>
          <strong>{{ selectedSubregion ? 'Земля' : 'Объект карты' }}</strong>
          <button type="button" aria-label="Закрыть редактор" title="Закрыть" @click="clearInspector">
            <X :size="15" />
          </button>
        </header>

        <template v-if="selectedSubregion">
          <label>
            <span>Название</span>
            <input
              :value="selectedSubregion.name"
              type="text"
              autocomplete="off"
              @input="emit('updateSubregion', activeRegion.id, selectedSubregion.id, { name: inputValue($event) })"
            >
          </label>
          <label>
            <span>Биом</span>
            <select
              :value="selectedSubregion.biome"
              @change="emit('updateSubregion', activeRegion.id, selectedSubregion.id, { biome: inputValue($event) })"
            >
              <option v-for="biome in biomeOptions(selectedSubregion.biome)" :key="biome" :value="biome">{{ biome }}</option>
            </select>
          </label>
          <button class="delete-button" type="button" @click="removeSelectedSubregion">
            <Trash2 :size="14" /> Удалить землю
          </button>
        </template>

        <template v-else-if="selectedObject">
          <label>
            <span>Название</span>
            <input
              :value="selectedObject.label"
              type="text"
              autocomplete="off"
              @input="emit('updateObject', activeRegion.id, selectedObject.id, { label: inputValue($event) })"
            >
          </label>
          <label>
            <span>URL изображения</span>
            <input
              :value="selectedObject.image || ''"
              type="url"
              inputmode="url"
              autocomplete="off"
              placeholder="https://…"
              @input="emit('updateObject', activeRegion.id, selectedObject.id, { image: inputValue($event) || undefined })"
            >
          </label>
          <div v-if="selectedObject.size" class="inspector-row">
            <label>
              <span>Ширина, %</span>
              <input
                :value="selectedObject.size.x"
                type="number"
                min="1"
                max="100"
                step="1"
                @change="emit('updateObject', activeRegion.id, selectedObject.id, { size: { x: numericInputValue($event), y: selectedObject.size?.y ?? 1 } })"
              >
            </label>
            <label>
              <span>Высота, %</span>
              <input
                :value="selectedObject.size.y"
                type="number"
                min="1"
                max="100"
                step="1"
                @change="emit('updateObject', activeRegion.id, selectedObject.id, { size: { x: selectedObject.size?.x ?? 1, y: numericInputValue($event) } })"
              >
            </label>
          </div>
          <label v-if="selectedObject.rotation !== undefined">
            <span>Поворот, °</span>
            <input
              :value="selectedObject.rotation"
              type="number"
              min="-360"
              max="360"
              step="1"
              @change="emit('updateObject', activeRegion.id, selectedObject.id, { rotation: numericInputValue($event) })"
            >
          </label>
          <button class="delete-button" type="button" @click="removeSelectedObject">
            <Trash2 :size="14" /> Удалить объект
          </button>
        </template>
      </aside>

      <div v-if="!activeRegion.objects.length && !activeSubregions.length" class="map-empty">
        <Sprout :size="28" />
        <strong>Этот край ещё не нанесён на карту</strong>
        <span v-if="editable">Перетащите сюда объекты из палитры.</span>
      </div>

      <div
        v-if="!editable && activeRegion.accessible === false"
        class="region-lost"
        role="status"
        :data-testid="`region-lost-${activeRegion.id}`"
      >
        <AlertTriangle :size="31" />
        <strong>Доступ к региону потерян</strong>
        <span>{{ activeRegion.disabledReason || 'Эта земля больше не подчиняется империи.' }}</span>
      </div>
    </div>

    <nav class="region-minimap" aria-label="Регионы империи">
      <button
        v-for="region in regions"
        :key="region.id"
        type="button"
        :class="[`region-${region.id}`, { active: region.id === activeRegion.id, inaccessible: region.accessible === false }]"
        :style="{ '--mini-accent': region.accent || '#c6a86b' }"
        :data-testid="`map-region-${region.id}`"
        :title="region.disabledReason"
        :aria-pressed="region.id === activeRegion.id"
        @click="emit('selectRegion', region.id)"
      >
        <span>{{ region.shortName || region.name }}</span>
        <small>{{ region.accessible === false ? 'потерян' : region.biome }}</small>
      </button>
    </nav>
  </section>
</template>

<style scoped>
.empire-map {
  --map-sky: #6f8790;
  --map-ground: #7c7658;
  --map-ground-2: #525c4c;
  display: grid;
  min-height: 620px;
  grid-template-rows: auto minmax(400px, 1fr) auto;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--region-accent) 48%, #18150f);
  border-radius: 18px;
  color: #f6edd7;
  background: #11140f;
  box-shadow: 0 24px 70px rgba(0, 0, 0, 0.32);
}

.empire-map.compact { min-height: 480px; }
.biome-ice { --map-sky: #9fb7c5; --map-ground: #d9e3df; --map-ground-2: #758c91; }
.biome-forest { --map-sky: #647a6d; --map-ground: #647347; --map-ground-2: #263e2d; }
.biome-desert { --map-sky: #ac8565; --map-ground: #c69a5c; --map-ground-2: #80583d; }
.biome-swamp { --map-sky: #687770; --map-ground: #536044; --map-ground-2: #233934; }
.biome-tetrakor,
.biome-greece { --map-sky: #6f8995; --map-ground: #8a7c57; --map-ground-2: #445c48; }

.map-heading {
  display: flex;
  min-height: 92px;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  padding: 18px 22px;
  border-bottom: 1px solid rgba(224, 201, 151, 0.17);
  background: linear-gradient(100deg, rgba(31, 29, 21, 0.98), rgba(24, 31, 28, 0.94));
}

.map-kicker {
  color: var(--region-accent);
  font: 800 0.68rem/1 var(--font-mono, monospace);
  letter-spacing: 0.14em;
  text-transform: uppercase;
}

.map-heading h2 { margin: 5px 0 2px; font: 700 clamp(1.3rem, 2vw, 1.9rem)/1.05 Georgia, serif; }
.map-heading p { max-width: 700px; margin: 0; color: rgba(246, 237, 215, 0.62); font-size: 0.78rem; }
.map-palette { display: flex; max-width: 560px; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
.map-palette button {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 9px;
  border: 1px solid rgba(230, 210, 169, 0.18);
  border-radius: 8px;
  color: #e9ddc2;
  background: rgba(255, 255, 255, 0.045);
  cursor: grab;
  font-size: 0.69rem;
}
.map-palette button:hover { border-color: var(--region-accent); background: rgba(255, 255, 255, 0.09); }
.map-palette .add-subregion-button { cursor: pointer; }

.map-stage {
  position: relative;
  min-height: 400px;
  overflow: hidden;
  isolation: isolate;
  background:
    radial-gradient(circle at 48% 52%, rgba(255,255,255,0.09), transparent 23%),
    linear-gradient(178deg, var(--map-sky) 0 28%, var(--map-ground) 29% 70%, var(--map-ground-2) 100%);
}

.subregion-layer {
  position: absolute;
  z-index: 0;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
  pointer-events: none;
}
.subregion { color: rgba(218, 188, 124, 0.2); outline: none; }
.subregion polygon {
  fill: currentColor;
  stroke: rgba(250, 236, 202, 0.32);
  stroke-width: 0.35;
  vector-effect: non-scaling-stroke;
  transition: fill 140ms ease, stroke 140ms ease;
}
.subregion text {
  fill: rgba(252, 242, 218, 0.78);
  paint-order: stroke;
  stroke: rgba(17, 18, 14, 0.8);
  stroke-width: 0.5;
  font: 800 2.3px/1 Georgia, serif;
  letter-spacing: 0.04em;
  pointer-events: none;
}
.subregion.selectable { pointer-events: auto; cursor: pointer; }
.subregion.selectable:hover polygon,
.subregion.selectable:focus-visible polygon,
.subregion.selected polygon { fill-opacity: 0.84; stroke: #fff0c8; stroke-width: 1.4; }
.subregion-ice { color: rgba(194, 231, 242, 0.24); }
.subregion-forest { color: rgba(61, 108, 65, 0.32); }
.subregion-desert { color: rgba(217, 161, 79, 0.27); }
.subregion-swamp { color: rgba(48, 92, 78, 0.34); }
.subregion-mountain { color: rgba(103, 102, 99, 0.34); }
.subregion-coast { color: rgba(84, 146, 166, 0.28); }
.subregion-steppe { color: rgba(166, 156, 88, 0.28); }
.subregion-central { color: rgba(200, 174, 104, 0.24); }

.map-stage::before {
  content: '';
  position: absolute;
  z-index: -1;
  inset: 0;
  opacity: 0.25;
  background-image:
    repeating-linear-gradient(28deg, transparent 0 26px, rgba(255,255,255,0.055) 27px 28px),
    repeating-linear-gradient(-32deg, transparent 0 37px, rgba(0,0,0,0.08) 38px 39px);
}

.map-horizon {
  position: absolute;
  z-index: -1;
  top: 23%;
  left: -8%;
  width: 116%;
  height: 34%;
  border-radius: 50%;
  background: color-mix(in srgb, var(--map-ground) 77%, transparent);
  box-shadow: 0 -18px 50px rgba(255,255,255,0.07), 0 12px 40px rgba(0,0,0,0.14);
  transform: perspective(420px) rotateX(56deg);
}

.terrain { position: absolute; z-index: -1; border: 1px solid rgba(25, 25, 18, 0.18); opacity: 0.78; }
.terrain-one { top: 13%; left: 7%; width: 28%; height: 30%; border-radius: 62% 38% 48% 52%; background: color-mix(in srgb, var(--map-ground-2) 75%, #b6b086); transform: rotate(-8deg); }
.terrain-two { top: 20%; right: 3%; width: 32%; height: 35%; border-radius: 39% 61% 56% 44%; background: color-mix(in srgb, var(--map-ground-2) 72%, #848d72); transform: rotate(11deg); }
.terrain-three { right: 28%; bottom: -15%; width: 44%; height: 48%; border-radius: 50%; background: color-mix(in srgb, var(--map-ground) 72%, #2f4939); }

.map-object {
  position: absolute;
  z-index: 2;
  display: grid;
  width: min-content;
  min-width: 68px;
  place-items: center;
  gap: 4px;
  padding: 0;
  border: 0;
  color: #f8efd9;
  background: transparent;
  filter: drop-shadow(0 4px 7px rgba(0, 0, 0, 0.48));
  transform: translate(-50%, -50%) rotate(var(--object-rotation, 0deg));
  transition: transform 150ms ease, filter 150ms ease;
}
.map-object.actionable { cursor: pointer; }
.map-object.actionable:hover { z-index: 4; filter: drop-shadow(0 5px 10px rgba(233, 202, 132, 0.45)); transform: translate(-50%, -50%) rotate(var(--object-rotation, 0deg)) scale(1.08); }
.map-object.inaccessible {
  filter: grayscale(0.9) brightness(0.58) drop-shadow(0 3px 5px rgba(0, 0, 0, 0.58));
  cursor: not-allowed;
}
.map-object:disabled { opacity: 1; }
.editable .map-object { cursor: grab; }
.editable .map-object.selected { z-index: 4; filter: drop-shadow(0 0 8px #ffe9ad); }
.object-icon {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border: 1px solid rgba(252, 238, 204, 0.5);
  border-radius: 50% 50% 45% 45%;
  color: #201b12;
  background: linear-gradient(145deg, #e4cf9b, var(--region-accent));
  box-shadow: inset 0 0 0 3px rgba(39, 32, 20, 0.16);
}
.object-icon img { width: 100%; height: 100%; border-radius: inherit; object-fit: cover; }
.kind-river .object-icon { color: #e8f4f7; background: #527e91; }
.map-object.sized-river { min-width: 44px; min-height: 26px; }
.river-line {
  position: absolute;
  top: 50%;
  right: 0;
  left: 0;
  display: block;
  height: 10px;
  border-top: 4px solid #8bc7d9;
  border-radius: 55%;
  color: #d9f3fa;
  background: linear-gradient(180deg, rgba(188, 232, 242, 0.46), rgba(48, 111, 136, 0.72));
  box-shadow: 0 1px 0 rgba(225, 248, 251, 0.55), 0 4px 10px rgba(22, 59, 72, 0.42);
  transform: translateY(-50%);
}
.river-line svg { position: absolute; top: -11px; left: calc(50% - 9px); }
.sized-river .object-label { position: absolute; top: calc(50% + 9px); left: 50%; transform: translateX(-50%); }
.kind-mountain .object-icon { color: #e9e1d2; background: #655f58; }
.kind-forest .object-icon { color: #e4f2d8; background: #345d3f; }
.kind-fortress .object-icon { background: #9b8d75; }
.kind-capital .object-icon { width: 52px; height: 52px; color: #f8e4a6; background: #7e2822; box-shadow: 0 0 0 3px rgba(226, 186, 102, 0.3); }
.object-label {
  max-width: 112px;
  padding: 3px 6px;
  overflow: hidden;
  border-radius: 4px;
  background: rgba(16, 17, 13, 0.76);
  font-size: 0.65rem;
  font-weight: 800;
  text-overflow: ellipsis;
  text-shadow: 0 1px 2px #000;
  white-space: nowrap;
}
.object-state {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px 5px;
  border-radius: 4px;
  color: #f0b5a9;
  background: rgba(92, 30, 24, 0.88);
  font: 800 0.49rem/1 var(--font-mono, monospace);
  text-transform: uppercase;
}
.object-epidemic {
  display: inline-flex;
  padding: 3px 5px;
  border: 1px solid rgba(255, 196, 94, .38);
  border-radius: 4px;
  color: #ffe1a0;
  background: rgba(91, 47, 12, .92);
  font: 800 .48rem/1 var(--font-mono, monospace);
  white-space: nowrap;
}

.map-empty { position: absolute; inset: 0; display: grid; place-content: center; place-items: center; gap: 6px; color: rgba(248,239,217,0.68); text-align: center; }
.map-empty span { font-size: 0.75rem; }
.region-lost {
  position: absolute;
  z-index: 7;
  inset: 0;
  display: grid;
  place-content: center;
  place-items: center;
  gap: 7px;
  padding: 30px;
  color: #e5b1a5;
  text-align: center;
  background:
    repeating-linear-gradient(135deg, rgba(118, 43, 32, 0.08) 0 12px, transparent 13px 25px),
    rgba(21, 13, 11, 0.76);
  backdrop-filter: grayscale(1) blur(2px);
}
.region-lost strong { color: #f1c0b5; font: 700 1.25rem/1.1 Georgia, serif; }
.region-lost span { max-width: 430px; color: rgba(240, 193, 182, 0.72); font-size: 0.72rem; line-height: 1.45; }

.map-inspector {
  position: absolute;
  z-index: 8;
  top: 12px;
  right: 12px;
  display: grid;
  width: min(270px, calc(100% - 24px));
  gap: 9px;
  padding: 12px;
  border: 1px solid rgba(237, 211, 158, 0.34);
  border-radius: 12px;
  color: #f3e8d1;
  background: rgba(19, 21, 17, 0.94);
  box-shadow: 0 14px 36px rgba(0, 0, 0, 0.38);
  backdrop-filter: blur(10px);
}
.map-inspector header { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.map-inspector header strong { font: 800 0.78rem/1.1 Georgia, serif; }
.map-inspector header button {
  display: grid;
  width: 27px;
  height: 27px;
  place-items: center;
  padding: 0;
  border: 1px solid rgba(235, 215, 177, 0.18);
  border-radius: 6px;
  color: inherit;
  background: rgba(255, 255, 255, 0.04);
  cursor: pointer;
}
.map-inspector label { display: grid; gap: 4px; min-width: 0; }
.map-inspector label span { color: rgba(243, 232, 209, 0.68); font: 700 0.6rem/1 var(--font-mono, monospace); letter-spacing: 0.04em; text-transform: uppercase; }
.map-inspector input,
.map-inspector select {
  width: 100%;
  min-width: 0;
  padding: 7px 8px;
  border: 1px solid rgba(235, 215, 177, 0.2);
  border-radius: 6px;
  color: #f7ecd5;
  background: #25271f;
  font: 600 0.72rem/1.2 inherit;
}
.map-inspector input:focus-visible,
.map-inspector select:focus-visible,
.map-inspector button:focus-visible,
.map-palette button:focus-visible { outline: 2px solid var(--region-accent); outline-offset: 2px; }
.inspector-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 7px; }
.map-inspector .delete-button {
  display: inline-flex;
  min-height: 32px;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 6px 9px;
  border: 1px solid rgba(211, 97, 82, 0.38);
  border-radius: 7px;
  color: #f0b6ac;
  background: rgba(154, 47, 38, 0.13);
  cursor: pointer;
  font-size: 0.68rem;
}

.region-minimap {
  display: grid;
  grid-template-areas: '. north .' 'west center east' '. south .';
  grid-template-columns: repeat(3, minmax(100px, 1fr));
  gap: 5px;
  padding: 10px;
  background: #151711;
}
.region-minimap button {
  position: relative;
  min-height: 48px;
  padding: 8px 10px;
  overflow: hidden;
  border: 1px solid rgba(230, 211, 171, 0.13);
  border-radius: 7px;
  color: rgba(245, 235, 211, 0.7);
  background: rgba(255, 255, 255, 0.035);
  cursor: pointer;
}
.region-minimap button::after { content: ''; position: absolute; right: 0; bottom: 0; left: 0; height: 2px; background: var(--mini-accent); opacity: 0.45; }
.region-minimap button.active { border-color: var(--mini-accent); color: #fff4d8; background: color-mix(in srgb, var(--mini-accent) 18%, #171912); box-shadow: inset 0 0 22px rgba(255,255,255,0.035); }
.region-minimap button.inaccessible {
  border-color: rgba(174, 82, 67, 0.32);
  color: rgba(231, 175, 163, 0.56);
  background: rgba(91, 34, 27, 0.13);
  filter: grayscale(0.75);
}
.region-minimap button.inaccessible::after { background: #b35d4e; opacity: 0.7; }
.region-minimap span { display: block; font: 800 0.72rem/1.1 Georgia, serif; }
.region-minimap small { display: block; margin-top: 3px; color: currentColor; font: 600 0.55rem/1 var(--font-mono, monospace); opacity: 0.6; text-transform: uppercase; }
.region-north { grid-area: north; }
.region-west { grid-area: west; }
.region-center { grid-area: center; }
.region-east { grid-area: east; }
.region-south { grid-area: south; }

@media (max-width: 820px) {
  .empire-map { min-height: 520px; grid-template-rows: auto minmax(330px, 1fr) auto; }
  .map-heading { align-items: flex-start; flex-direction: column; padding: 14px; }
  .map-palette { justify-content: flex-start; }
  .map-palette button span { display: none; }
  .map-stage { min-height: 330px; }
  .region-minimap { grid-template-columns: repeat(3, minmax(76px, 1fr)); }
  .object-label { max-width: 78px; font-size: 0.58rem; }
  .subregion text { font-size: 2.8px; }
}

@media (max-width: 520px) {
  .map-inspector { top: 8px; right: 8px; width: calc(100% - 16px); max-height: calc(100% - 16px); overflow: auto; }
  .region-minimap { grid-template-columns: repeat(3, minmax(60px, 1fr)); padding: 7px; }
  .region-minimap button { min-height: 42px; padding: 6px; }
}
</style>
