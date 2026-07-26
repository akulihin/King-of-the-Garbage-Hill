<script setup lang="ts">
defineProps<{ phrase: string }>()
</script>

<template>
  <div class="underground-train" role="status" aria-live="assertive">
    <div class="tunnel-lines" />
    <div class="blood blood-left" />
    <div class="blood blood-right" />
    <p :data-text="phrase">{{ phrase }}</p>
  </div>
</template>

<style scoped>
.underground-train {
  position: fixed;
  z-index: 4100;
  inset: 0;
  display: grid;
  place-items: center;
  overflow: hidden;
  padding: clamp(24px, 7vw, 96px);
  isolation: isolate;
  background:
    radial-gradient(ellipse at center, rgba(21, 42, 72, 0.62) 0 15%, transparent 58%),
    linear-gradient(112deg, #02040a 0%, #0a182c 45%, #020307 100%);
  animation: train-impact 5.4s cubic-bezier(.12, .78, .18, 1) both;
}

.underground-train::before {
  content: '';
  position: absolute;
  z-index: -2;
  inset: -35%;
  background: repeating-conic-gradient(
    from 266deg at 50% 52%,
    rgba(255, 221, 34, 0.22) 0deg 1.2deg,
    transparent 1.2deg 7deg
  );
  animation: tunnel-rush 1.05s linear infinite;
}

.underground-train::after {
  content: '';
  position: absolute;
  z-index: 4;
  inset: 0;
  pointer-events: none;
  background:
    repeating-linear-gradient(176deg, transparent 0 12vh, rgba(110, 5, 10, 0.38) 12.4vh 12.9vh),
    radial-gradient(ellipse at center, transparent 35%, rgba(0, 0, 0, 0.86) 100%);
  mix-blend-mode: multiply;
}

.tunnel-lines {
  position: absolute;
  z-index: -1;
  inset: 8%;
  border: clamp(5px, 0.8vw, 13px) solid rgba(247, 211, 23, 0.48);
  transform: perspective(620px) rotateX(67deg) scaleX(1.65);
  box-shadow:
    0 0 0 7vw rgba(10, 51, 112, 0.2),
    0 0 80px 18px rgba(238, 31, 31, 0.2);
  animation: rail-pulse .42s linear infinite;
}

.blood {
  position: absolute;
  z-index: 2;
  width: min(34vw, 440px);
  aspect-ratio: 1.35;
  opacity: 0.9;
  filter: drop-shadow(0 5px 5px rgba(0, 0, 0, 0.7));
  background:
    radial-gradient(circle at 28% 42%, #8d0710 0 8%, transparent 9%),
    radial-gradient(circle at 47% 30%, #bd1018 0 5%, transparent 6%),
    radial-gradient(circle at 68% 61%, #72030a 0 12%, transparent 13%),
    radial-gradient(ellipse at center, #990912 0 35%, transparent 37%);
}

.blood-left {
  left: -10%;
  top: 5%;
  transform: rotate(24deg);
}

.blood-right {
  right: -9%;
  bottom: 1%;
  transform: rotate(-18deg) scale(.85);
}

p {
  position: relative;
  z-index: 3;
  max-width: 1320px;
  margin: 0;
  transform: skew(-9deg) rotate(-2deg);
  color: #ffe321;
  font-family: Impact, Haettenschweiler, 'Arial Black', sans-serif;
  font-size: clamp(54px, 10.4vw, 168px);
  font-style: italic;
  font-weight: 900;
  line-height: .84;
  text-align: center;
  letter-spacing: -.05em;
  text-transform: uppercase;
  -webkit-text-stroke: clamp(1px, .2vw, 4px) #121726;
  text-shadow:
    6px 7px 0 #db251d,
    12px 14px 0 #123f94,
    18px 22px 35px rgba(0, 0, 0, .9);
  animation: phrase-collision 5.4s cubic-bezier(.12, .76, .2, 1) both;
}

p::before {
  content: attr(data-text);
  position: absolute;
  z-index: 1;
  inset: 0;
  color: transparent;
  -webkit-text-stroke: clamp(2px, .34vw, 6px) rgba(118, 0, 8, .9);
  clip-path: polygon(0 14%, 100% 2%, 100% 28%, 0 44%);
  transform: translate(2px, -2px);
  filter: blur(.2px);
}

@keyframes train-impact {
  0% { opacity: 0; filter: brightness(4) contrast(1.8); }
  7%, 87% { opacity: 1; filter: brightness(1) contrast(1.1); }
  100% { opacity: 0; filter: brightness(.55) contrast(1.7); }
}

@keyframes phrase-collision {
  0% { opacity: 0; transform: translate3d(115vw, -18vh, 0) skew(-9deg) rotate(-9deg) scale(.38); }
  12% { opacity: 1; transform: translate3d(-2vw, 0, 0) skew(-9deg) rotate(-2deg) scale(1.08); }
  17%, 82% { opacity: 1; transform: skew(-9deg) rotate(-2deg) scale(1); }
  100% { opacity: 0; transform: translate3d(-65vw, 24vh, 0) skew(-9deg) rotate(-5deg) scale(1.35); }
}

@keyframes tunnel-rush {
  to { transform: rotate(8deg) scale(1.13); }
}

@keyframes rail-pulse {
  50% { opacity: .45; transform: perspective(620px) rotateX(67deg) scaleX(1.72) scaleY(1.08); }
}

@media (prefers-reduced-motion: reduce) {
  .underground-train,
  .underground-train::before,
  .tunnel-lines,
  p {
    animation: none;
  }
}
</style>
