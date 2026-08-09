<script setup lang="ts">
defineProps<{ title: string; phrase: string }>()
</script>

<template>
  <div class="underground-train" role="status" aria-live="assertive">
    <div class="halftone" aria-hidden="true" />
    <div class="tunnel-pillars" aria-hidden="true" />
    <div class="speed-field" aria-hidden="true">
      <i v-for="line in 9" :key="line" />
    </div>

    <div class="track-field" aria-hidden="true">
      <div class="sleepers" />
      <div class="rail rail-far" />
      <div class="rail rail-near" />
    </div>

    <div class="train-runner" aria-hidden="true">
      <div class="train-wake" />
      <div class="pantograph pantograph-front"><i /><i /></div>
      <div class="pantograph pantograph-back"><i /><i /></div>

      <div class="train-set">
        <section class="train-car train-lead">
          <div class="cab-window"><i /></div>
          <div class="headlamp" />
          <div class="passenger-windows">
            <i /><i /><i />
          </div>
          <div class="train-door"><i /><i /></div>
        </section>

        <div class="car-connector" />

        <section class="train-car train-middle">
          <div class="passenger-windows">
            <i /><i /><i /><i /><i />
          </div>
          <div class="train-door train-door-middle"><i /><i /></div>
        </section>

        <div class="car-connector" />

        <section class="train-car train-tail">
          <div class="passenger-windows">
            <i /><i /><i /><i />
          </div>
          <div class="train-door"><i /><i /></div>
        </section>
      </div>

      <div class="train-undercarriage">
        <span class="bogie bogie-one"><i /><i /></span>
        <span class="bogie bogie-two"><i /><i /></span>
        <span class="bogie bogie-three"><i /><i /></span>
        <span class="bogie bogie-four"><i /><i /></span>
      </div>

      <div class="rail-sparks">
        <i v-for="spark in 7" :key="spark" />
      </div>
    </div>

    <div class="comic-caption">
      <p class="comic-title" :data-text="title">{{ title }}</p>
      <p v-if="phrase" class="comic-phrase">{{ phrase }}</p>
    </div>
    <div class="foreground-slash" aria-hidden="true" />
  </div>
</template>

<style scoped>
.underground-train {
  position: fixed;
  z-index: 4100;
  inset: 0;
  overflow: hidden;
  isolation: isolate;
  pointer-events: none;
  background:
    radial-gradient(ellipse at 50% 52%, rgba(34, 86, 150, 0.68) 0 8%, transparent 44%),
    linear-gradient(178deg, #070f1f 0 48%, #02050b 70%, #000 100%);
  animation: scene-cut 5.4s cubic-bezier(.16, .72, .18, 1) both;
}

.underground-train::before {
  content: '';
  position: absolute;
  z-index: -3;
  inset: -30%;
  background:
    repeating-conic-gradient(
      from 269deg at 50% 49%,
      rgba(255, 226, 36, 0.2) 0deg 0.65deg,
      transparent 0.75deg 5.8deg
    );
  animation: tunnel-rush 720ms linear infinite;
}

.underground-train::after {
  content: '';
  position: absolute;
  z-index: 12;
  inset: 0;
  background:
    linear-gradient(104deg, transparent 0 44%, rgba(255, 233, 56, 0.08) 47%, transparent 51%),
    radial-gradient(ellipse at center, transparent 34%, rgba(0, 0, 0, 0.88) 100%);
}

.halftone {
  position: absolute;
  z-index: -1;
  inset: 0;
  opacity: 0.24;
  background-image: radial-gradient(circle, rgba(250, 225, 35, 0.8) 0 1.1px, transparent 1.4px);
  background-size: 8px 8px;
  -webkit-mask-image: linear-gradient(to bottom, black, transparent 58%);
  mask-image: linear-gradient(to bottom, black, transparent 58%);
  animation: halftone-jolt 220ms steps(2, end) infinite;
}

.tunnel-pillars {
  position: absolute;
  z-index: -2;
  inset: 0 -24%;
  opacity: 0.56;
  background:
    linear-gradient(to bottom, transparent 0 14%, rgba(17, 45, 75, 0.75) 15% 18%, transparent 19%),
    repeating-linear-gradient(
      90deg,
      transparent 0 12vw,
      rgba(4, 8, 14, 0.92) 12.2vw 15.4vw,
      rgba(186, 30, 37, 0.2) 15.6vw 16vw
    );
  transform: skewX(-7deg);
  animation: pillars-pass 460ms linear infinite;
}

.speed-field {
  position: absolute;
  z-index: 1;
  inset: 0;
  filter: drop-shadow(0 0 5px rgba(255, 229, 47, 0.8));
}

.speed-field i {
  position: absolute;
  right: -18vw;
  width: var(--line-width, 42vw);
  height: clamp(2px, 0.38vw, 7px);
  border-radius: 999px;
  opacity: 0;
  background: linear-gradient(90deg, transparent, #ffe82c 36%, #fff7a0);
  transform: skewX(-24deg);
  animation: speed-slash 530ms linear infinite;
}

.speed-field i:nth-child(1) { top: 11%; --line-width: 34vw; animation-delay: -80ms; }
.speed-field i:nth-child(2) { top: 20%; --line-width: 56vw; animation-delay: -390ms; }
.speed-field i:nth-child(3) { top: 31%; --line-width: 25vw; animation-delay: -210ms; }
.speed-field i:nth-child(4) { top: 43%; --line-width: 46vw; animation-delay: -470ms; }
.speed-field i:nth-child(5) { top: 52%; --line-width: 68vw; animation-delay: -140ms; }
.speed-field i:nth-child(6) { top: 63%; --line-width: 38vw; animation-delay: -320ms; }
.speed-field i:nth-child(7) { top: 71%; --line-width: 53vw; animation-delay: -40ms; }
.speed-field i:nth-child(8) { top: 82%; --line-width: 31vw; animation-delay: -430ms; }
.speed-field i:nth-child(9) { top: 91%; --line-width: 63vw; animation-delay: -250ms; }

.track-field {
  position: absolute;
  z-index: 2;
  right: -10%;
  bottom: -5%;
  left: -10%;
  height: 33%;
  overflow: hidden;
  background:
    linear-gradient(to bottom, transparent, rgba(3, 7, 14, 0.92) 45%),
    repeating-linear-gradient(102deg, #101722 0 3vw, #05080d 3.2vw 6vw);
  border-top: 4px solid rgba(255, 221, 35, 0.22);
  transform: perspective(720px) rotateX(58deg) scaleX(1.15);
  transform-origin: bottom center;
}

.sleepers {
  position: absolute;
  inset: 22% -20% 0;
  background: repeating-linear-gradient(
    90deg,
    transparent 0 4.4vw,
    #27303a 4.6vw 6.5vw,
    #07090d 6.7vw 8.2vw
  );
  transform: skewX(-18deg);
  animation: sleepers-pass 280ms linear infinite;
}

.rail {
  position: absolute;
  right: -5%;
  left: -5%;
  height: clamp(7px, 0.8vw, 15px);
  border: 2px solid #07090d;
  border-radius: 999px;
  background: linear-gradient(to bottom, #fff2a1, #7b8792 32%, #1b222a 62%, #000);
  box-shadow: 0 3px 0 #07090d, 0 0 15px rgba(250, 220, 42, 0.34);
}

.rail-far { top: 28%; }
.rail-near { bottom: 19%; }

.train-runner {
  position: absolute;
  z-index: 5;
  top: 42%;
  left: 0;
  width: max(760px, 138vw);
  height: clamp(205px, 30vw, 390px);
  opacity: 0;
  transform-origin: center;
  animation: train-cross 3.6s 180ms cubic-bezier(.12, .69, .16, 1) both;
  will-change: transform, opacity;
  filter:
    drop-shadow(0 12px 0 rgba(3, 5, 10, 0.95))
    drop-shadow(0 23px 28px rgba(0, 0, 0, 0.95));
}

.train-wake {
  position: absolute;
  z-index: -2;
  top: 10%;
  left: 55%;
  width: 84vw;
  height: 72%;
  opacity: 0.74;
  background:
    linear-gradient(90deg, rgba(234, 31, 37, 0.54), transparent 70%),
    repeating-linear-gradient(
      174deg,
      rgba(255, 227, 44, 0.82) 0 4px,
      transparent 5px 24px
    );
  clip-path: polygon(0 22%, 100% 0, 100% 100%, 0 78%);
  filter: blur(5px);
  transform: translateX(5%);
  animation: wake-throb 150ms steps(2, end) infinite;
}

.train-set {
  position: absolute;
  top: 17%;
  right: 0;
  left: 0;
  height: 64%;
  display: grid;
  grid-template-columns: 1.03fr minmax(16px, 2.2vw) 1.05fr minmax(16px, 2.2vw) 0.92fr;
  align-items: stretch;
}

.train-car {
  position: relative;
  overflow: hidden;
  border: clamp(4px, 0.45vw, 8px) solid #07101c;
  background:
    linear-gradient(
      to bottom,
      #f5f7f6 0 9%,
      #8098aa 10% 16%,
      #dce6e9 17% 66%,
      #90a2ac 67% 73%,
      #174c9a 74% 88%,
      #d8242d 89% 96%,
      #111b27 97%
    );
  box-shadow:
    inset 0 8px 0 rgba(255, 255, 255, 0.6),
    inset 0 -13px 0 rgba(0, 0, 0, 0.35),
    0 0 0 3px #e6c826;
}

.train-car::after {
  content: '';
  position: absolute;
  inset: 0;
  background:
    linear-gradient(103deg, transparent 18%, rgba(255, 255, 255, 0.72) 25%, transparent 32%),
    repeating-linear-gradient(90deg, transparent 0 8%, rgba(7, 16, 28, 0.15) 8.3% 8.7%);
}

.train-lead {
  clip-path: polygon(9% 0, 100% 0, 100% 100%, 6% 100%, 0 78%, 0 29%);
}

.train-lead::before {
  content: '';
  position: absolute;
  z-index: 4;
  top: 16%;
  bottom: 8%;
  left: 0;
  width: 9%;
  background:
    linear-gradient(94deg, #0b1727, #254e72 54%, #07101c 58%),
    #0b1727;
  border-right: 4px solid #edcf2c;
  transform: skewY(-13deg);
}

.car-connector {
  position: relative;
  align-self: center;
  height: 74%;
  background:
    repeating-linear-gradient(to right, #0b111b 0 4px, #35414b 5px 8px);
  border-block: 5px solid #05080d;
  box-shadow: inset 0 0 0 3px #09111d;
}

.passenger-windows {
  position: absolute;
  z-index: 2;
  top: 24%;
  right: 6%;
  left: 12%;
  height: 31%;
  display: flex;
  gap: clamp(5px, 0.75vw, 14px);
}

.passenger-windows i,
.cab-window {
  flex: 1;
  overflow: hidden;
  border: clamp(3px, 0.35vw, 6px) solid #0c1722;
  border-radius: 5px 5px 2px 2px;
  background:
    linear-gradient(118deg, rgba(255, 241, 148, 0.9) 0 7%, transparent 8% 47%, rgba(88, 172, 226, 0.34) 48% 60%, transparent 61%),
    linear-gradient(to bottom, #08111f, #153f67 56%, #050a11);
  box-shadow: inset 0 0 18px rgba(75, 154, 218, 0.38);
}

.cab-window {
  position: absolute;
  z-index: 5;
  top: 21%;
  left: 5%;
  width: 16%;
  height: 39%;
  transform: skewY(-9deg);
}

.cab-window i {
  position: absolute;
  right: 20%;
  bottom: -8%;
  width: 44%;
  height: 58%;
  border-radius: 50% 50% 18% 18%;
  background: rgba(2, 5, 10, 0.78);
  filter: blur(1px);
}

.train-lead .passenger-windows {
  left: 24%;
  right: 27%;
}

.train-door {
  position: absolute;
  z-index: 3;
  top: 17%;
  right: 5%;
  bottom: 12%;
  width: 18%;
  display: flex;
  border: 4px solid #263947;
  background: linear-gradient(to bottom, #cbd7da, #718793);
  box-shadow: inset 0 0 0 3px rgba(239, 245, 245, 0.48);
}

.train-door i {
  flex: 1;
  margin: 10% 7% 48%;
  border: 3px solid #111c26;
  background: linear-gradient(135deg, #10263d, #2f668c 52%, #08111d 54%);
}

.train-door i + i {
  margin-left: 0;
}

.train-door-middle {
  right: 41%;
  width: 18%;
}

.train-middle .passenger-windows {
  right: 4%;
  left: 4%;
}

.train-middle .passenger-windows i:nth-child(3) {
  visibility: hidden;
}

.train-tail .passenger-windows {
  right: 25%;
  left: 5%;
}

.headlamp {
  position: absolute;
  z-index: 7;
  bottom: 18%;
  left: 1.6%;
  width: clamp(12px, 1.6vw, 28px);
  aspect-ratio: 1;
  border: 3px solid #111925;
  border-radius: 50%;
  background: #fff8b0;
  box-shadow:
    0 0 10px #fff,
    0 0 28px #ffe839,
    -24vw 0 55px 12px rgba(255, 234, 74, 0.42);
}

.pantograph {
  position: absolute;
  z-index: -1;
  top: 0;
  width: clamp(90px, 10vw, 180px);
  height: 23%;
  border-top: clamp(4px, 0.4vw, 7px) solid #efcf2a;
}

.pantograph i {
  position: absolute;
  bottom: 0;
  width: 58%;
  height: clamp(5px, 0.5vw, 9px);
  border: 2px solid #070c13;
  background: #c92730;
  transform-origin: bottom;
}

.pantograph i:first-child {
  left: 5%;
  transform: rotate(-53deg);
}

.pantograph i:last-child {
  right: 5%;
  transform: rotate(53deg);
}

.pantograph-front { left: 29%; }
.pantograph-back { right: 24%; }

.train-undercarriage {
  position: absolute;
  z-index: -1;
  right: 2%;
  bottom: 0;
  left: 2%;
  height: 28%;
  border-top: clamp(10px, 1vw, 18px) solid #121a22;
  background: linear-gradient(to bottom, #24303a 0 28%, transparent 29%);
}

.bogie {
  position: absolute;
  bottom: 1%;
  width: 10%;
  height: 68%;
  display: flex;
  justify-content: space-between;
  align-items: end;
  padding: 0 5%;
  border: clamp(5px, 0.55vw, 10px) solid #111820;
  border-radius: 10px 10px 20px 20px;
  background: #2b3540;
}

.bogie i {
  width: 42%;
  aspect-ratio: 1;
  border: clamp(5px, 0.55vw, 10px) solid #080c11;
  border-radius: 50%;
  background:
    radial-gradient(circle, #d5c22b 0 11%, #4c5962 12% 30%, #141a21 31% 63%, #05070a 64%);
  animation: wheel-strobe 110ms steps(2, end) infinite;
}

.bogie-one { left: 9%; }
.bogie-two { left: 36%; }
.bogie-three { right: 31%; }
.bogie-four { right: 7%; }

.rail-sparks {
  position: absolute;
  z-index: 8;
  right: 0;
  bottom: 2%;
  left: 0;
}

.rail-sparks i {
  position: absolute;
  left: var(--spark-left, 14%);
  bottom: 0;
  width: clamp(24px, 3vw, 54px);
  height: clamp(3px, 0.3vw, 6px);
  border-radius: 999px;
  opacity: 0;
  background: linear-gradient(90deg, #fff, #ffe637 25%, #e1262d 72%, transparent);
  box-shadow: 0 0 9px #ffe637;
  transform-origin: left;
  animation: spark-flight 360ms linear infinite;
}

.rail-sparks i:nth-child(1) { --spark-left: 13%; animation-delay: -40ms; }
.rail-sparks i:nth-child(2) { --spark-left: 24%; animation-delay: -230ms; }
.rail-sparks i:nth-child(3) { --spark-left: 39%; animation-delay: -120ms; }
.rail-sparks i:nth-child(4) { --spark-left: 52%; animation-delay: -310ms; }
.rail-sparks i:nth-child(5) { --spark-left: 67%; animation-delay: -80ms; }
.rail-sparks i:nth-child(6) { --spark-left: 79%; animation-delay: -270ms; }
.rail-sparks i:nth-child(7) { --spark-left: 91%; animation-delay: -160ms; }

.comic-caption {
  position: absolute;
  z-index: 10;
  top: clamp(28px, 5vh, 70px);
  left: 50%;
  width: min(92vw, 1500px);
  transform: translateX(-50%);
  animation: caption-cut 5.4s cubic-bezier(.14, .78, .18, 1) both;
}

.comic-caption::before {
  content: '';
  position: absolute;
  z-index: -1;
  inset: -28% -5%;
  background: #f2d925;
  clip-path: polygon(0 34%, 8% 27%, 3% 11%, 25% 22%, 34% 0, 47% 18%, 63% 4%, 71% 23%, 100% 14%, 92% 39%, 100% 58%, 77% 61%, 86% 91%, 62% 76%, 48% 100%, 35% 77%, 12% 91%, 17% 65%, 0 58%);
  opacity: 0.9;
  filter: drop-shadow(10px 12px 0 #bc2027) drop-shadow(-8px -7px 0 #174c98);
}

.comic-title {
  position: relative;
  margin: 0;
  color: #fff1ab;
  font-family: Impact, Haettenschweiler, 'Arial Black', sans-serif;
  font-size: clamp(38px, 6.8vw, 118px);
  font-style: italic;
  font-weight: 900;
  line-height: 0.86;
  text-align: center;
  letter-spacing: -0.04em;
  text-transform: uppercase;
  transform: skew(-8deg) rotate(-1.5deg);
  -webkit-text-stroke: clamp(2px, 0.24vw, 5px) #081426;
  text-shadow:
    5px 6px 0 #df272d,
    10px 12px 0 #164a99,
    16px 20px 28px rgba(0, 0, 0, 0.82);
}

.comic-title::after {
  content: attr(data-text);
  position: absolute;
  inset: 0;
  color: transparent;
  opacity: 0.82;
  clip-path: polygon(0 55%, 100% 43%, 100% 64%, 0 74%);
  transform: translate(4px, 4px);
  -webkit-text-stroke: clamp(2px, 0.25vw, 5px) #8d0911;
}

.comic-phrase {
  width: fit-content;
  max-width: min(84vw, 980px);
  margin: clamp(24px, 4vh, 48px) auto 0;
  padding: 0.35em 0.7em 0.42em;
  border: clamp(2px, 0.2vw, 4px) solid #07101c;
  color: #07101c;
  background: rgba(255, 242, 164, 0.94);
  box-shadow: 7px 8px 0 #d4212b, 12px 14px 0 #164a99;
  font-family: Impact, Haettenschweiler, 'Arial Black', sans-serif;
  font-size: clamp(17px, 2vw, 34px);
  font-style: italic;
  font-weight: 900;
  line-height: 1.05;
  text-align: center;
  text-transform: uppercase;
  transform: rotate(-1deg);
}

.foreground-slash {
  position: absolute;
  z-index: 11;
  right: -25vw;
  bottom: -24vh;
  width: 82vw;
  height: 36vh;
  opacity: 0.72;
  background:
    repeating-linear-gradient(
      166deg,
      rgba(231, 31, 39, 0.92) 0 8px,
      transparent 9px 35px
    );
  transform: rotate(-4deg);
  animation: foreground-swipe 5.4s ease-out both;
}

@keyframes scene-cut {
  0% { opacity: 0; filter: brightness(3.2) contrast(1.8) saturate(0.7); }
  5%, 89% { opacity: 1; filter: brightness(1) contrast(1.08) saturate(1.12); }
  91% { filter: brightness(2.3) contrast(1.5) saturate(0.8); }
  100% { opacity: 0; filter: brightness(0.45) contrast(1.8) saturate(0.6); }
}

@keyframes train-cross {
  0%, 3% {
    opacity: 0;
    transform: translate3d(132vw, 6vh, 0) skewX(-2deg) scale(0.94);
  }
  8% { opacity: 1; }
  18% {
    opacity: 1;
    transform: translate3d(62vw, 1vh, 0) skewX(-1.2deg) scale(1);
  }
  54% {
    opacity: 1;
    transform: translate3d(-20vw, -1vh, 0) skewX(0) scale(1.035);
  }
  87% {
    opacity: 1;
    transform: translate3d(-116vw, 1vh, 0) skewX(1.4deg) scale(1.02);
  }
  100% {
    opacity: 0;
    transform: translate3d(-192vw, -5vh, 0) skewX(3deg) scale(1.08);
  }
}

@keyframes caption-cut {
  0%, 5% { opacity: 0; transform: translate3d(68vw, -18vh, 0) skew(-8deg) rotate(-7deg) scale(0.42); }
  12% { opacity: 1; transform: translate3d(-50%, 0, 0) skew(-2deg) rotate(0.8deg) scale(1.1); }
  17%, 70% { opacity: 1; transform: translate3d(-50%, 0, 0) scale(1); }
  78%, 100% { opacity: 0; transform: translate3d(-112vw, 9vh, 0) skew(7deg) rotate(-5deg) scale(1.22); }
}

@keyframes speed-slash {
  0% { opacity: 0; transform: translateX(0) skewX(-24deg) scaleX(0.25); }
  18% { opacity: 0.9; }
  100% { opacity: 0; transform: translateX(-150vw) skewX(-24deg) scaleX(1.4); }
}

@keyframes spark-flight {
  0% { opacity: 1; transform: translate3d(0, 0, 0) rotate(15deg) scaleX(0.2); }
  100% { opacity: 0; transform: translate3d(18vw, 9vh, 0) rotate(27deg) scaleX(1.5); }
}

@keyframes tunnel-rush {
  to { transform: rotate(5deg) scale(1.08); }
}

@keyframes pillars-pass {
  to { transform: translateX(-16vw) skewX(-7deg); }
}

@keyframes sleepers-pass {
  to { transform: translateX(-8.2vw) skewX(-18deg); }
}

@keyframes halftone-jolt {
  50% { transform: translate(2px, -1px); }
}

@keyframes wheel-strobe {
  50% { transform: rotate(45deg); filter: brightness(1.6); }
}

@keyframes wake-throb {
  50% { opacity: 0.42; transform: translateX(7%) scaleY(1.07); }
}

@keyframes foreground-swipe {
  0%, 70% { opacity: 0; transform: translateX(50vw) rotate(-4deg); }
  77% { opacity: 0.72; }
  100% { opacity: 0; transform: translateX(-130vw) rotate(-4deg); }
}

@media (max-width: 640px) {
  .train-runner {
    top: 44%;
    width: 920px;
    height: 230px;
  }

  .comic-caption {
    top: 8vh;
  }

  .comic-title {
    font-size: clamp(38px, 11vw, 62px);
  }

  .comic-phrase {
    max-width: 82vw;
    margin-top: 22px;
    font-size: clamp(15px, 4vw, 22px);
  }

  .track-field {
    height: 38%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .underground-train,
  .underground-train::before,
  .halftone,
  .tunnel-pillars,
  .speed-field i,
  .sleepers,
  .train-runner,
  .train-wake,
  .bogie i,
  .rail-sparks i,
  .comic-caption,
  .foreground-slash {
    animation: none;
  }

  .underground-train {
    opacity: 1;
    filter: none;
  }

  .train-runner {
    opacity: 1;
    transform: translate3d(-18vw, 0, 0);
  }

  .comic-caption {
    opacity: 1;
    transform: translateX(-50%);
  }

  .speed-field,
  .rail-sparks,
  .foreground-slash {
    display: none;
  }
}
</style>
