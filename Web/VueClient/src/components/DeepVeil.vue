<template>
  <div class="deep-veil" role="presentation" aria-hidden="true">
    <div class="deep-veil__curtain" />
    <div class="deep-veil__water" />

    <div class="deep-veil__bubbles">
      <i
        v-for="bubble in 22"
        :key="bubble"
        class="deep-veil__bubble"
        :style="{
          left: `${(bubble * 37) % 96}%`,
          width: `${5 + (bubble % 5) * 4}px`,
          height: `${5 + (bubble % 5) * 4}px`,
          animationDelay: `${0.7 + (bubble % 8) * 0.23}s`,
          animationDuration: `${2.7 + (bubble % 6) * 0.34}s`,
        }"
      />
    </div>

    <div class="deep-veil__seabed">
      <div class="deep-veil__coral deep-veil__coral--left">
        <i /><i /><i /><i />
      </div>
      <div class="deep-veil__coral deep-veil__coral--right">
        <i /><i /><i />
      </div>
      <i
        v-for="weed in 9"
        :key="weed"
        class="deep-veil__weed"
        :style="{
          left: `${3 + ((weed * 29) % 94)}%`,
          height: `${50 + (weed % 5) * 24}px`,
          animationDelay: `${(weed % 4) * -0.45}s`,
        }"
      />
    </div>

    <svg class="deep-veil__tentacle deep-veil__tentacle--tl" viewBox="0 0 260 260">
      <path d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
      <path class="deep-veil__suckers" d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
    </svg>
    <svg class="deep-veil__tentacle deep-veil__tentacle--tr" viewBox="0 0 260 260">
      <path d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
      <path class="deep-veil__suckers" d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
    </svg>
    <svg class="deep-veil__tentacle deep-veil__tentacle--bl" viewBox="0 0 260 260">
      <path d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
      <path class="deep-veil__suckers" d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
    </svg>
    <svg class="deep-veil__tentacle deep-veil__tentacle--br" viewBox="0 0 260 260">
      <path d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
      <path class="deep-veil__suckers" d="M-18 24 C82 26 32 148 132 130 C214 115 160 232 256 238" />
    </svg>
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
  z-index: 1;
  inset: -15% 0 0;
  background:
    radial-gradient(ellipse at 50% 0%, rgba(5, 35, 48, 0.42), transparent 48%),
    linear-gradient(180deg, #000 0%, #01050a 65%, #020d17 100%);
  transform-origin: top;
  animation: deep-veil-fall 1.5s cubic-bezier(.7, 0, .3, 1) both;
}

.deep-veil__water {
  position: absolute;
  z-index: 2;
  inset: 0;
  opacity: 0;
  background:
    radial-gradient(ellipse at 50% 105%, rgba(5, 71, 74, .48), transparent 50%),
    repeating-radial-gradient(ellipse at 40% 0%, rgba(43, 190, 190, 0.09) 0 2px, transparent 4px 40px),
    linear-gradient(180deg, rgba(0, 0, 0, 0.05), rgba(0, 30, 48, 0.62));
  backdrop-filter: blur(0);
  animation: deep-water-settle 4.8s 1.2s ease-in both;
}

.deep-veil__bubbles {
  position: absolute;
  z-index: 5;
  inset: 0;
}

.deep-veil__bubble {
  position: absolute;
  bottom: -28px;
  display: block;
  border: 1px solid rgba(154, 244, 239, .72);
  border-radius: 50%;
  background: radial-gradient(circle at 32% 28%, rgba(235, 255, 255, .7), rgba(34, 158, 165, .08) 28%, transparent 66%);
  box-shadow: inset -2px -2px 5px rgba(21, 105, 125, .25), 0 0 6px rgba(99, 232, 222, .22);
  opacity: 0;
  animation: deep-bubble-rise ease-in both;
}

.deep-veil__seabed {
  position: absolute;
  z-index: 4;
  inset: auto 0 0;
  height: 24%;
  opacity: 0;
  background:
    radial-gradient(ellipse at 18% 112%, #061f23 0 18%, transparent 19%),
    radial-gradient(ellipse at 74% 116%, #04191f 0 24%, transparent 25%),
    linear-gradient(0deg, rgba(1, 13, 17, .98), rgba(3, 36, 40, .12) 72%, transparent);
  animation: deep-seabed-emerge 2.1s 2.7s ease-out both;
}

.deep-veil__weed {
  position: absolute;
  bottom: 4%;
  width: 10px;
  border-radius: 80% 15% 80% 12%;
  background: linear-gradient(90deg, #06272b, #0b5550 52%, #07322f);
  box-shadow: inset 2px 0 rgba(75, 148, 124, .12);
  transform-origin: 50% 100%;
  animation: deep-weed-sway 2.4s ease-in-out infinite alternate;
}

.deep-veil__weed:nth-of-type(3n) {
  width: 7px;
  background: linear-gradient(90deg, #102e2a, #17604f, #092c29);
}

.deep-veil__coral {
  position: absolute;
  bottom: 7%;
  width: 150px;
  height: 130px;
  filter: drop-shadow(0 0 8px rgba(27, 92, 91, .24));
}

.deep-veil__coral--left { left: 7%; }
.deep-veil__coral--right { right: 8%; transform: scaleX(-1) scale(.82); }

.deep-veil__coral i {
  position: absolute;
  bottom: 0;
  left: 42%;
  width: 15px;
  height: 112px;
  border-radius: 60% 60% 20% 20%;
  background: linear-gradient(90deg, #18353a, #315957 46%, #142c31);
  transform-origin: 50% 100%;
}

.deep-veil__coral i:nth-child(2) { height: 80px; transform: rotate(-42deg); }
.deep-veil__coral i:nth-child(3) { height: 88px; transform: rotate(39deg); }
.deep-veil__coral i:nth-child(4) { left: 62%; height: 58px; transform: rotate(62deg); }

.deep-veil__tentacle {
  position: absolute;
  z-index: 6;
  width: min(34vw, 340px);
  height: min(34vw, 340px);
  overflow: visible;
  opacity: 0;
  filter: drop-shadow(0 8px 13px rgba(0, 0, 0, .7));
  animation: deep-tentacle-enter 2.6s 2.4s cubic-bezier(.2, .9, .2, 1) both;
}

.deep-veil__tentacle path {
  fill: none;
  stroke: #071b22;
  stroke-width: 27;
  stroke-linecap: round;
}

.deep-veil__tentacle .deep-veil__suckers {
  stroke: rgba(70, 128, 125, .52);
  stroke-width: 5;
  stroke-dasharray: 1 19;
}

.deep-veil__tentacle--tl { top: -5%; left: -4%; transform: rotate(4deg); }
.deep-veil__tentacle--tr { top: -5%; right: -4%; transform: scaleX(-1) rotate(4deg); }
.deep-veil__tentacle--bl { bottom: -6%; left: -4%; transform: scaleY(-1) rotate(4deg); }
.deep-veil__tentacle--br { right: -4%; bottom: -6%; transform: scale(-1) rotate(4deg); }

@keyframes deep-veil-fall {
  from { transform: translateY(-105%); }
  to { transform: translateY(0); }
}

@keyframes deep-water-settle {
  0% { opacity: 0; backdrop-filter: blur(0); transform: translateY(-2%); }
  35% { opacity: .78; backdrop-filter: blur(2px); }
  100% { opacity: 1; backdrop-filter: blur(8px); transform: translateY(5%); }
}

@keyframes deep-bubble-rise {
  0% { opacity: 0; transform: translate3d(0, 0, 0) scale(.55); }
  12% { opacity: .75; }
  70% { opacity: .55; }
  100% { opacity: 0; transform: translate3d(24px, -112vh, 0) scale(1.18); }
}

@keyframes deep-seabed-emerge {
  from { opacity: 0; transform: translateY(72%); }
  to { opacity: 1; transform: translateY(0); }
}

@keyframes deep-weed-sway {
  from { transform: rotate(-7deg) skewX(-2deg); }
  to { transform: rotate(9deg) skewX(3deg); }
}

@keyframes deep-tentacle-enter {
  from { opacity: 0; margin: -13%; }
  45% { opacity: .76; }
  to { opacity: .92; margin: 0; }
}

@keyframes deep-veil-sink {
  0%, 22% { filter: saturate(1); }
  100% { filter: saturate(.5) brightness(.34) hue-rotate(8deg); }
}

@media (prefers-reduced-motion: reduce) {
  .deep-veil,
  .deep-veil__curtain,
  .deep-veil__water,
  .deep-veil__bubble,
  .deep-veil__seabed,
  .deep-veil__weed,
  .deep-veil__tentacle {
    animation: none;
  }

  .deep-veil {
    background: rgba(0, 5, 10, .96);
  }

  .deep-veil__water,
  .deep-veil__seabed,
  .deep-veil__tentacle {
    opacity: .9;
  }
}
</style>
