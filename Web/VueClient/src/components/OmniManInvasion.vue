<template>
  <div class="viltrumite-invasion" role="status" aria-live="assertive">
    <div class="invasion-sun" />
    <div class="invasion-cloud cloud-one" />
    <div class="invasion-cloud cloud-two" />
    <div class="invasion-speed-lines" />
    <p>Земля единогласно присоединилась к Вилтрумайтской империи!</p>
  </div>
</template>

<style scoped>
.viltrumite-invasion {
  position: fixed;
  z-index: 4000;
  inset: 0;
  display: grid;
  place-items: center;
  overflow: hidden;
  padding: clamp(24px, 7vw, 96px);
  isolation: isolate;
  background:
    radial-gradient(circle at 76% 24%, rgba(255, 247, 166, 0.95) 0 4%, transparent 13%),
    linear-gradient(155deg, #75d9ff 0%, #168acb 47%, #073d79 100%);
  animation: invasion-arrival 6.5s cubic-bezier(.16, .8, .24, 1) both;
}

.viltrumite-invasion::before {
  content: '';
  position: absolute;
  z-index: -2;
  inset: -30%;
  opacity: 0.38;
  background: repeating-conic-gradient(
    from 250deg at 50% 50%,
    rgba(255, 255, 255, 0.72) 0deg 1.5deg,
    transparent 1.5deg 7deg
  );
  animation: invasion-rays 5.6s ease-out both;
}

.viltrumite-invasion::after {
  content: '';
  position: absolute;
  z-index: 4;
  inset: 0;
  pointer-events: none;
  background:
    linear-gradient(90deg, rgba(255, 216, 0, 0.16), transparent 10% 90%, rgba(255, 216, 0, 0.16)),
    radial-gradient(ellipse at center, transparent 40%, rgba(0, 18, 61, 0.58) 100%);
}

.invasion-sun {
  position: absolute;
  z-index: -1;
  top: 8%;
  right: 11%;
  width: min(26vw, 310px);
  aspect-ratio: 1;
  border-radius: 50%;
  background: #fff7a1;
  box-shadow:
    0 0 40px 18px rgba(255, 240, 87, 0.8),
    0 0 130px 62px rgba(255, 220, 50, 0.35);
}

.invasion-cloud {
  position: absolute;
  z-index: -1;
  width: 44vw;
  height: 10vw;
  border-radius: 50%;
  opacity: 0.48;
  filter: blur(13px);
  background: rgba(235, 250, 255, 0.92);
}

.cloud-one {
  top: 18%;
  left: -8%;
  transform: rotate(-8deg);
  animation: cloud-drift-one 6.5s ease-out both;
}

.cloud-two {
  right: -6%;
  bottom: 15%;
  transform: rotate(7deg);
  animation: cloud-drift-two 6.5s ease-out both;
}

.invasion-speed-lines {
  position: absolute;
  z-index: 1;
  inset: 0;
  opacity: 0.5;
  background: repeating-linear-gradient(
    164deg,
    transparent 0 45px,
    rgba(255, 255, 255, 0.74) 46px 48px,
    transparent 49px 96px
  );
  transform: translateX(-18%);
  animation: speed-lines 1s linear infinite;
}

p {
  position: relative;
  z-index: 3;
  max-width: 1180px;
  margin: 0;
  transform: skew(-8deg) rotate(-2deg);
  color: #ffe51f;
  font-family: Impact, Haettenschweiler, 'Arial Black', sans-serif;
  font-size: clamp(42px, 8.7vw, 132px);
  font-style: italic;
  font-weight: 900;
  line-height: 0.88;
  text-align: center;
  letter-spacing: -0.045em;
  -webkit-text-stroke: clamp(1px, 0.18vw, 3px) #171a28;
  text-shadow:
    5px 5px 0 #e2261d,
    10px 10px 0 #123f94,
    16px 18px 28px rgba(0, 20, 65, 0.72);
  animation: invasion-title 6.5s cubic-bezier(.12, .76, .2, 1) both;
}

@keyframes invasion-arrival {
  0% { opacity: 0; filter: brightness(4); }
  6%, 88% { opacity: 1; filter: brightness(1); }
  100% { opacity: 0; filter: brightness(1.3); }
}

@keyframes invasion-title {
  0% { opacity: 0; transform: translate3d(120vw, -40vh, 0) skew(-8deg) rotate(-8deg) scale(0.45); }
  12% { opacity: 1; transform: translate3d(-2vw, 0, 0) skew(-8deg) rotate(-2deg) scale(1.06); }
  17%, 82% { opacity: 1; transform: translate3d(0, 0, 0) skew(-8deg) rotate(-2deg) scale(1); }
  100% { opacity: 0; transform: translate3d(-55vw, 18vh, 0) skew(-8deg) rotate(-4deg) scale(1.3); }
}

@keyframes invasion-rays {
  from { transform: scale(0.2) rotate(-8deg); opacity: 0; }
  18% { opacity: 0.38; }
  to { transform: scale(1.15) rotate(2deg); opacity: 0.16; }
}

@keyframes speed-lines {
  to { transform: translateX(18%); }
}

@keyframes cloud-drift-one {
  from { transform: translateX(-24vw) rotate(-8deg); }
  to { transform: translateX(28vw) rotate(-8deg); }
}

@keyframes cloud-drift-two {
  from { transform: translateX(24vw) rotate(7deg); }
  to { transform: translateX(-20vw) rotate(7deg); }
}

@media (prefers-reduced-motion: reduce) {
  .viltrumite-invasion,
  .viltrumite-invasion::before,
  .invasion-cloud,
  .invasion-speed-lines,
  p {
    animation: none;
  }
}
</style>
