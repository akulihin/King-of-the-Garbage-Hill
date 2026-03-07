import { ref, onUnmounted, type Ref } from 'vue'

// ── Particle types ──────────────────────────────────────

interface Particle {
  x: number
  y: number
  dx: number
  dy: number
  life: number
  maxLife: number
  color: string
  size: number
  gravity: number
  drag: number
}

interface Projectile {
  startX: number
  startY: number
  endX: number
  endY: number
  cpX: number
  cpY: number
  elapsed: number
  duration: number
  onImpact?: () => void
  trail: Array<{ x: number; y: number; alpha: number }>
}

type ImpactType = 'hit' | 'miss' | 'burn' | 'sunk' | 'destroy' | 'scratch' | 'freeze'

// ── Composable ──────────────────────────────────────────

export function useVfx() {
  const canvasRef: Ref<HTMLCanvasElement | null> = ref(null)

  let cellSize = 0
  let boardOffsetX = 0
  let boardOffsetY = 0
  let animFrameId = 0
  let running = false
  let lastTime = 0

  const particles: Particle[] = []
  const projectiles: Projectile[] = []

  // ── Coordinate helpers ──────────────────────────────────

  function cellCenterX(col: number): number {
    return boardOffsetX + col * cellSize + cellSize / 2
  }

  function cellCenterY(row: number): number {
    return boardOffsetY + row * cellSize + cellSize / 2
  }

  // ── Animation loop ──────────────────────────────────────

  function ensureRunning() {
    if (running) return
    running = true
    lastTime = performance.now()
    animFrameId = requestAnimationFrame(tick)
  }

  function tick(now: number) {
    const dt = Math.min((now - lastTime) / 1000, 0.05) // cap delta to avoid jumps
    lastTime = now

    const canvas = canvasRef.value
    if (!canvas) {
      running = false
      return
    }
    const ctx = canvas.getContext('2d')
    if (!ctx) {
      running = false
      return
    }

    ctx.clearRect(0, 0, canvas.width, canvas.height)

    // Update & draw projectiles
    updateProjectiles(ctx, dt)

    // Update & draw particles
    updateParticles(ctx, dt)

    if (particles.length === 0 && projectiles.length === 0) {
      running = false
      return
    }

    animFrameId = requestAnimationFrame(tick)
  }

  function updateParticles(ctx: CanvasRenderingContext2D, dt: number) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i]

      p.dy += p.gravity * dt
      p.dx *= p.drag
      p.dy *= p.drag
      p.x += p.dx * dt
      p.y += p.dy * dt
      p.life -= dt

      if (p.life <= 0) {
        particles.splice(i, 1)
        continue
      }

      const alpha = Math.max(0, p.life / p.maxLife)
      ctx.globalAlpha = alpha
      ctx.fillStyle = p.color
      ctx.beginPath()
      ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2)
      ctx.fill()
    }
    ctx.globalAlpha = 1
  }

  function updateProjectiles(ctx: CanvasRenderingContext2D, dt: number) {
    for (let i = projectiles.length - 1; i >= 0; i--) {
      const proj = projectiles[i]
      proj.elapsed += dt

      const t = Math.min(proj.elapsed / proj.duration, 1)

      // Quadratic bezier: B(t) = (1-t)^2 * P0 + 2(1-t)t * CP + t^2 * P1
      const oneMinusT = 1 - t
      const x = oneMinusT * oneMinusT * proj.startX + 2 * oneMinusT * t * proj.cpX + t * t * proj.endX
      const y = oneMinusT * oneMinusT * proj.startY + 2 * oneMinusT * t * proj.cpY + t * t * proj.endY

      // Add to trail
      proj.trail.push({ x, y, alpha: 1 })

      // Fade and trim trail
      for (let j = proj.trail.length - 1; j >= 0; j--) {
        proj.trail[j].alpha -= dt * 8
        if (proj.trail[j].alpha <= 0) {
          proj.trail.splice(j, 1)
        }
      }

      // Draw trail
      for (const tp of proj.trail) {
        ctx.globalAlpha = tp.alpha * 0.5
        ctx.fillStyle = '#555555'
        ctx.beginPath()
        ctx.arc(tp.x, tp.y, 2, 0, Math.PI * 2)
        ctx.fill()
      }

      // Draw cannonball
      ctx.globalAlpha = 1
      ctx.fillStyle = '#3a3a3a'
      ctx.beginPath()
      ctx.arc(x, y, 6, 0, Math.PI * 2)
      ctx.fill()

      // Shadow / outline
      ctx.strokeStyle = '#222222'
      ctx.lineWidth = 1
      ctx.beginPath()
      ctx.arc(x, y, 6, 0, Math.PI * 2)
      ctx.stroke()

      if (t >= 1) {
        if (proj.onImpact) proj.onImpact()
        projectiles.splice(i, 1)
      }
    }
    ctx.globalAlpha = 1
  }

  // ── Public API ──────────────────────────────────────────

  function init(size: number, offsetX: number, offsetY: number): void {
    cellSize = size
    boardOffsetX = offsetX
    boardOffsetY = offsetY
  }

  function spawnCannonball(targetRow: number, targetCol: number, onImpact?: () => void): void {
    const canvas = canvasRef.value
    if (!canvas) return

    const startX = canvas.width / 2
    const startY = canvas.height
    const endX = cellCenterX(targetCol)
    const endY = cellCenterY(targetRow)

    // Control point: midpoint horizontally, high above both endpoints
    const midX = (startX + endX) / 2
    const highestY = Math.min(startY, endY)
    const arcHeight = Math.max(150, Math.abs(endX - startX) * 0.4)
    const cpX = midX
    const cpY = highestY - arcHeight

    const proj: Projectile = {
      startX,
      startY,
      endX,
      endY,
      cpX,
      cpY,
      elapsed: 0,
      duration: 0.3,
      onImpact,
      trail: [],
    }

    projectiles.push(proj)
    ensureRunning()
  }

  function spawnImpact(row: number, col: number, type: ImpactType): void {
    const cx = cellCenterX(col)
    const cy = cellCenterY(row)

    switch (type) {
      case 'hit':
        spawnHit(cx, cy)
        break
      case 'miss':
        spawnMiss(cx, cy)
        break
      case 'burn':
        spawnBurn(cx, cy)
        break
      case 'sunk':
        spawnSunk(cx, cy)
        break
      case 'destroy':
        spawnDestroy(cx, cy)
        break
      case 'scratch':
        spawnScratch(cx, cy)
        break
      case 'freeze':
        spawnFreeze(cx, cy)
        break
    }

    ensureRunning()
  }

  function spawnWake(row: number, col: number, direction: 'up' | 'down' | 'left' | 'right'): void {
    const cx = cellCenterX(col)
    const cy = cellCenterY(row)

    const count = 3 + Math.floor(Math.random() * 3) // 3-5

    // Trail is opposite to movement direction
    const dirMap: Record<string, { dx: number; dy: number }> = {
      up: { dx: 0, dy: 1 },
      down: { dx: 0, dy: -1 },
      left: { dx: 1, dy: 0 },
      right: { dx: -1, dy: 0 },
    }
    const dir = dirMap[direction]

    for (let i = 0; i < count; i++) {
      const speed = 10 + Math.random() * 20
      const spread = (Math.random() - 0.5) * 15
      particles.push({
        x: cx + (Math.random() - 0.5) * 4,
        y: cy + (Math.random() - 0.5) * 4,
        dx: dir.dx * speed + (direction === 'up' || direction === 'down' ? spread : 0),
        dy: dir.dy * speed + (direction === 'left' || direction === 'right' ? spread : 0),
        life: 0.4,
        maxLife: 0.4,
        color: Math.random() > 0.5 ? '#c8e0f8' : '#ffffff',
        size: 1.5 + Math.random() * 1.5,
        gravity: 0,
        drag: 0.95,
      })
    }

    ensureRunning()
  }

  function spawnConfetti(count = 30): void {
    const canvas = canvasRef.value
    if (!canvas) return

    const colors = ['#ffd700', '#ff4444', '#4488ff', '#44bb44', '#ff8800', '#aa44ff']

    for (let i = 0; i < count; i++) {
      const x = Math.random() * canvas.width
      const horizontalWobble = (Math.random() - 0.5) * 60
      particles.push({
        x,
        y: -10 - Math.random() * 40,
        dx: horizontalWobble,
        dy: 30 + Math.random() * 50,
        life: 3,
        maxLife: 3,
        color: colors[Math.floor(Math.random() * colors.length)],
        size: 3 + Math.random() * 3,
        gravity: 40,
        drag: 0.99,
      })
    }

    ensureRunning()
  }

  function destroy(): void {
    if (animFrameId) {
      cancelAnimationFrame(animFrameId)
      animFrameId = 0
    }
    running = false
    particles.length = 0
    projectiles.length = 0
  }

  // ── Impact type spawners ────────────────────────────────

  function spawnHit(cx: number, cy: number): void {
    const count = 8 + Math.floor(Math.random() * 5) // 8-12
    const colors = ['#cc6633', '#ff8844', '#dd7733', '#ffaa55']
    for (let i = 0; i < count; i++) {
      const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.6
      const speed = 60 + Math.random() * 80
      particles.push({
        x: cx + (Math.random() - 0.5) * 4,
        y: cy + (Math.random() - 0.5) * 4,
        dx: Math.cos(angle) * speed,
        dy: Math.sin(angle) * speed,
        life: 0.5 + Math.random() * 0.3,
        maxLife: 0.8,
        color: colors[Math.floor(Math.random() * colors.length)],
        size: 2 + Math.random() * 2,
        gravity: 200,
        drag: 0.97,
      })
    }
  }

  function spawnMiss(cx: number, cy: number): void {
    const count = 6 + Math.floor(Math.random() * 3) // 6-8
    for (let i = 0; i < count; i++) {
      const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.4
      const speed = 30 + Math.random() * 40
      particles.push({
        x: cx,
        y: cy,
        dx: Math.cos(angle) * speed,
        dy: Math.sin(angle) * speed,
        life: 0.6 + Math.random() * 0.3,
        maxLife: 0.9,
        color: Math.random() > 0.4 ? '#aaccff' : '#ffffff',
        size: 2.5 + Math.random() * 2,
        gravity: 0,
        drag: 0.93,
      })
    }
  }

  function spawnBurn(cx: number, cy: number): void {
    const count = 10 + Math.floor(Math.random() * 6) // 10-15
    const colors = ['#ff4400', '#ff6600', '#ff8800', '#ffaa00', '#cc3300']
    for (let i = 0; i < count; i++) {
      const angle = (Math.random() - 0.5) * Math.PI * 0.8 // mostly upward spread
      const speed = 30 + Math.random() * 50
      particles.push({
        x: cx + (Math.random() - 0.5) * 10,
        y: cy + (Math.random() - 0.5) * 6,
        dx: Math.sin(angle) * speed + (Math.random() - 0.5) * 20, // spiral wobble
        dy: -(20 + Math.random() * 40), // upward
        life: 0.8 + Math.random() * 0.6,
        maxLife: 1.4,
        color: colors[Math.floor(Math.random() * colors.length)],
        size: 2 + Math.random() * 2.5,
        gravity: -60, // negative gravity: float upward
        drag: 0.98,
      })
    }
  }

  function spawnSunk(cx: number, cy: number): void {
    // Gray smoke particles (15-20)
    const smokeCount = 15 + Math.floor(Math.random() * 6)
    const smokeColors = ['#888888', '#999999', '#aaaaaa', '#777777', '#bbbbbb']
    for (let i = 0; i < smokeCount; i++) {
      const angle = (Math.PI * 2 * i) / smokeCount + (Math.random() - 0.5) * 0.5
      const speed = 20 + Math.random() * 50
      particles.push({
        x: cx + (Math.random() - 0.5) * 8,
        y: cy + (Math.random() - 0.5) * 8,
        dx: Math.cos(angle) * speed,
        dy: Math.sin(angle) * speed - 20,
        life: 1.0 + Math.random() * 0.5,
        maxLife: 1.5,
        color: smokeColors[Math.floor(Math.random() * smokeColors.length)],
        size: 3 + Math.random() * 3,
        gravity: -15,
        drag: 0.96,
      })
    }

    // Blue splash ring (8 particles in a ring expanding outward)
    const ringCount = 8
    for (let i = 0; i < ringCount; i++) {
      const angle = (Math.PI * 2 * i) / ringCount
      const speed = 70 + Math.random() * 30
      particles.push({
        x: cx,
        y: cy,
        dx: Math.cos(angle) * speed,
        dy: Math.sin(angle) * speed,
        life: 0.4 + Math.random() * 0.2,
        maxLife: 0.6,
        color: '#4488cc',
        size: 2.5 + Math.random() * 1.5,
        gravity: 0,
        drag: 0.94,
      })
    }
  }

  function spawnDestroy(cx: number, cy: number): void {
    const count = 10 + Math.floor(Math.random() * 3) // 10-12
    const colors = ['#ff2222', '#ff4444', '#ff6644', '#cc2222', '#aa4444']
    for (let i = 0; i < count; i++) {
      const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.7
      const speed = 50 + Math.random() * 80
      // Mix of sparks (small, fast) and debris (larger, slower)
      const isSpark = Math.random() > 0.4
      particles.push({
        x: cx + (Math.random() - 0.5) * 6,
        y: cy + (Math.random() - 0.5) * 6,
        dx: Math.cos(angle) * speed * (isSpark ? 1.3 : 0.7),
        dy: Math.sin(angle) * speed * (isSpark ? 1.3 : 0.7),
        life: isSpark ? 0.3 + Math.random() * 0.3 : 0.5 + Math.random() * 0.4,
        maxLife: isSpark ? 0.6 : 0.9,
        color: isSpark ? colors[Math.floor(Math.random() * colors.length)] : '#664433',
        size: isSpark ? 1.5 + Math.random() * 1.5 : 3 + Math.random() * 2,
        gravity: isSpark ? 100 : 250,
        drag: 0.97,
      })
    }
  }

  function spawnScratch(cx: number, cy: number): void {
    const count = 4 + Math.floor(Math.random() * 3) // 4-6
    for (let i = 0; i < count; i++) {
      const angle = (Math.PI * 2 * i) / count + (Math.random() - 0.5) * 0.8
      const speed = 80 + Math.random() * 60 // high horizontal speed
      particles.push({
        x: cx + (Math.random() - 0.5) * 4,
        y: cy + (Math.random() - 0.5) * 4,
        dx: Math.cos(angle) * speed,
        dy: Math.sin(angle) * speed - 40,
        life: 0.4 + Math.random() * 0.3,
        maxLife: 0.7,
        color: Math.random() > 0.3 ? '#ffffff' : '#ddddee',
        size: 1.5 + Math.random() * 1,
        gravity: 180, // medium gravity for bouncing feel
        drag: 0.96,
      })
    }
  }

  function spawnFreeze(cx: number, cy: number): void {
    const count = 6 + Math.floor(Math.random() * 3) // 6-8
    const colors = ['#88ccff', '#aaddff', '#66bbff', '#ccf0ff']
    for (let i = 0; i < count; i++) {
      const angle = (Math.random() - 0.5) * Math.PI // mostly upward spread
      const speed = 15 + Math.random() * 25
      particles.push({
        x: cx + (Math.random() - 0.5) * 12,
        y: cy + (Math.random() - 0.5) * 8,
        dx: Math.sin(angle) * speed * 0.5,
        dy: -(10 + Math.random() * 20), // slowly upward
        life: 0.8 + Math.random() * 0.5,
        maxLife: 1.3,
        color: colors[Math.floor(Math.random() * colors.length)],
        size: 2.5 + Math.random() * 2,
        gravity: -20, // gentle float upward
        drag: 0.97,
      })
    }
  }

  // ── Lifecycle ─────────────────────────────────────────

  onUnmounted(() => {
    destroy()
  })

  return {
    canvasRef,
    init,
    spawnCannonball,
    spawnImpact,
    spawnWake,
    spawnConfetti,
    destroy,
  }
}
