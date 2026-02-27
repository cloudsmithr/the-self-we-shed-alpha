# the-self-we-shed-alpha
The alpha of an online mmo strategy/incremental game called The Self We Shed.

---

A massively multiplayer, browser-based cooperative strategy/incremental game. Players work together on a single server to push back an AI-controlled enemy faction across a persistent map of fortresses. Players contribute units and buffs on cooldown timers. Armies advance automatically. The campaign unfolds over weeks.
Think r/place meets an incremental grand strategy game.

## What it is

- Fully cooperative — all human players vs. AI enemies, no PvP
- Persistent — a single campaign lasts weeks to months
- Idle/incremental aesthetic — no twitch gameplay, meaningful decisions on long cooldowns
- Real-time — troop counts, army movement, and battle results update live for all players
- Enemy AI that pushes back — the enemy counterattacks, retakes fortresses, and scales difficulty based on player activity

---

## Stack

| Layer | Choice |
|-------|--------|
| Backend API | Node.js / TypeScript / NestJS |
| Real-time | Socket.IO (server push only) |
| Game loop | Standalone Node.js worker process |
| Database | PostgreSQL + Prisma |
| Cache / messaging | Redis (hot state + pub/sub + Socket.IO adapter) |
| Frontend | React + TypeScript |
| Auth | Passport.js OAuth2 (Google + Discord) + JWT |
| Hosting (Phase 1) | Hetzner VPS + Docker Compose + Caddy |
| Hosting (Phase 2) | AWS ECS Fargate + RDS + ElastiCache + CDK |

---

## Architecture overview

The system runs as two separate processes:

**API server** — handles auth, REST endpoints, and Socket.IO connections. All player commands go through REST. Socket.IO is push-only: the server broadcasts state, clients never send game commands over the socket.

**Worker** — runs the tick loop on a configurable interval. Handles army movement, combat resolution, enemy AI, and periodic persistence from Redis to PostgreSQL. Communicates results to the API server via Redis pub/sub.

**Redis** sits between them as the authoritative runtime game state store and message bus. PostgreSQL holds persistent state: accounts, world definition, action history, battle log.

---

## Status

Currently in active development. Phase 1 target: publicly playable on a real domain.
