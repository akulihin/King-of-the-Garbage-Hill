<template>
  <div class="deep-veil" role="presentation" aria-hidden="true">
    <div class="deep-veil__curtain" />
    <div class="deep-veil__water" />
  </div>
</template>

<style scoped>
.deep-veil {
  position: fixed;
  z-index: 12050;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
  background: transparent;
  animation: deep-veil-sink 6s cubic-bezier(.22, .72, .28, 1) both;
}

.deep-veil__curtain {
  position: absolute;
  inset: -15% 0 0;
  background:
    radial-gradient(ellipse at 50% 0%, rgba(5, 35, 48, 0.42), transparent 48%),
    linear-gradient(180deg, #000 0%, #01050a 65%, #020d17 100%);
  transform-origin: top;
  animation: deep-veil-fall 1.5s cubic-bezier(.7, 0, .3, 1) both;
}

.deep-veil__water {
  position: absolute;
  inset: 0;
  opacity: 0;
  background:
    repeating-radial-gradient(ellipse at 40% 0%, rgba(43, 190, 190, 0.09) 0 2px, transparent 4px 40px),
    linear-gradient(180deg, rgba(0, 0, 0, 0.05), rgba(0, 30, 48, 0.55));
  backdrop-filter: blur(0);
  animation: deep-water-settle 4.8s 1.2s ease-in both;
}

@keyframes deep-veil-fall {
  from { transform: translateY(-105%); }
  to { transform: translateY(0); }
}

@keyframes deep-water-settle {
  0% { opacity: 0; backdrop-filter: blur(0); transform: translateY(-2%); }
  35% { opacity: .78; backdrop-filter: blur(2px); }
  100% { opacity: 1; backdrop-filter: blur(8px); transform: translateY(5%); }
}

@keyframes deep-veil-sink {
  0%, 22% { filter: saturate(1); }
  100% { filter: saturate(.5) brightness(.34) hue-rotate(8deg); }
}

@media (prefers-reduced-motion: reduce) {
  .deep-veil,
  .deep-veil__curtain,
  .deep-veil__water {
    animation: none;
  }

  .deep-veil {
    background: rgba(0, 5, 10, .96);
  }
}
</style>
