<script setup lang="ts">
defineProps<{ points: number }>()
</script>

<template>
  <Teleport to="body">
    <div class="terminal-commit-overlay" role="alert" aria-live="assertive">
      <div class="terminal-commit-noise" aria-hidden="true" />
      <div class="terminal-commit-window">
        <div class="terminal-commit-header">
          <span>root@runtime:~/score_buffer</span>
          <span>[OVERRIDE]</span>
        </div>
        <div class="terminal-commit-warning">CRITICAL COMMIT</div>
        <div class="terminal-commit-code">
          <span>&gt; integrity_check --skip</span>
          <span>&gt; rewrite_score --force</span>
          <strong>+{{ points }} PTS</strong>
          <span>&gt; trace erased<span class="terminal-cursor">_</span></span>
        </div>
        <div class="terminal-commit-stamp">MEMORY ACCEPTED</div>
      </div>
    </div>
  </Teleport>
</template>

<style>
.terminal-commit-overlay {
  position: fixed;
  z-index: 11000;
  inset: 0;
  display: grid;
  place-items: center;
  overflow: hidden;
  pointer-events: none;
  background:
    radial-gradient(circle at center, rgba(0, 255, 65, 0.2), rgba(0, 10, 2, 0.92) 58%, #000 100%);
  animation: terminal-commit-blackout 4.2s steps(1, end) both;
}
.terminal-commit-noise {
  position: absolute;
  inset: -20%;
  background:
    repeating-linear-gradient(0deg, transparent 0 4px, rgba(0, 255, 65, 0.13) 5px),
    repeating-linear-gradient(90deg, transparent 0 47px, rgba(0, 255, 65, 0.08) 48px 49px);
  animation: terminal-commit-noise 0.16s steps(2, end) infinite;
}
.terminal-commit-window {
  position: relative;
  width: min(680px, 88vw);
  overflow: hidden;
  border: 2px solid #56ff80;
  background: rgba(0, 8, 2, 0.96);
  box-shadow: 0 0 30px rgba(0, 255, 65, 0.9), 0 0 100px rgba(0, 255, 65, 0.35), inset 0 0 40px rgba(0, 255, 65, 0.09);
  color: #8bffa8;
  font-family: var(--font-mono, monospace);
  transform-origin: center;
  animation: terminal-commit-window 4.2s steps(1, end) both;
}
.terminal-commit-window::after {
  content: '';
  position: absolute;
  inset: 0;
  background: repeating-linear-gradient(0deg, transparent 0 2px, rgba(0, 0, 0, 0.26) 3px);
  pointer-events: none;
}
.terminal-commit-header {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid rgba(0, 255, 65, 0.45);
  background: rgba(0, 255, 65, 0.12);
  font-size: 10px;
  letter-spacing: 0.08em;
}
.terminal-commit-warning {
  padding: 22px 16px 8px;
  color: #d0ffda;
  font-size: clamp(29px, 7vw, 70px);
  font-weight: 950;
  letter-spacing: 0.08em;
  line-height: 0.95;
  text-align: center;
  text-shadow: 3px 0 #00ffd0, -3px 0 #00ff41, 0 0 18px #00ff41;
  animation: terminal-commit-title 0.48s steps(2, end) infinite;
}
.terminal-commit-code {
  display: grid;
  gap: 5px;
  padding: 20px clamp(18px, 7vw, 70px) 25px;
  color: rgba(139, 255, 168, 0.78);
  font-size: clamp(11px, 2vw, 16px);
}
.terminal-commit-code strong {
  margin: 8px 0;
  color: #fff;
  font-size: clamp(42px, 12vw, 112px);
  line-height: 0.95;
  text-align: center;
  text-shadow: 0 0 12px #00ff41, 0 0 38px rgba(0, 255, 65, 0.85);
}
.terminal-cursor { animation: terminal-cursor-blink 0.45s steps(1, end) infinite; }
.terminal-commit-stamp {
  position: absolute;
  right: 14px;
  bottom: 10px;
  padding: 4px 7px;
  border: 2px solid rgba(115, 255, 150, 0.55);
  color: rgba(115, 255, 150, 0.68);
  font-size: 9px;
  font-weight: 900;
  letter-spacing: 0.12em;
  transform: rotate(-4deg);
}
@keyframes terminal-commit-blackout {
  0% { opacity: 0; }
  3%, 90% { opacity: 1; }
  92% { opacity: 0.35; }
  94% { opacity: 1; }
  100% { opacity: 0; }
}
@keyframes terminal-commit-window {
  0% { transform: scaleX(0.01) scaleY(0.004); filter: brightness(5); }
  4% { transform: scaleX(1) scaleY(0.012); filter: brightness(3); }
  8%, 88% { transform: scale(1); filter: none; }
  90% { transform: translateX(-12px) skewX(3deg); }
  92% { transform: translateX(8px) skewX(-2deg); }
  94%, 100% { transform: scale(1); }
}
@keyframes terminal-commit-noise {
  0% { transform: translate(0); opacity: 0.55; }
  50% { transform: translate(5px, -3px); opacity: 0.9; }
  100% { transform: translate(-4px, 2px); opacity: 0.62; }
}
@keyframes terminal-commit-title {
  50% { transform: translateX(2px); filter: brightness(1.35); }
}
@keyframes terminal-cursor-blink { 50% { opacity: 0; } }

@media (prefers-reduced-motion: reduce) {
  .terminal-commit-overlay,
  .terminal-commit-noise,
  .terminal-commit-window,
  .terminal-commit-warning,
  .terminal-cursor { animation: none; }
}
</style>
