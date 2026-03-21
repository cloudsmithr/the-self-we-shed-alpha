# Architecture Document — Multiplayer PvE Fortress Strategy Game

**Working Title:** TBD
**Author:** [You]
**Date:** February 2026
**Status:** Draft

---

## 1. Project Overview

### Concept
A massively multiplayer, browser-based, cooperative PvE strategy game where thousands of players work together to conquer a tree of enemy-held fortresses radiating outward from a central objective. Players construct buildings inside captured fortresses that passively produce units — think r/place meets an incremental grand strategy game.

### Core Loop
1. Player's action charges over hours/days
2. Player spends action: constructs a building inside a captured fortress
3. Buildings passively produce units, accumulating at the fortress level
4. When a fortress's unit count reaches the capture threshold — and all of its child fortresses are already captured — the next fortress is taken. A percentage of units transfer up; production briefly pauses and then resumes
5. The campaign progresses as players collectively push inward along branches, converging on the final enemy fortress at the center of the tree

### Design Goals
- Cooperative: all players on the same human faction vs. AI-controlled enemies
- Persistent: a single game lasts weeks to months
- Scalable: designed to support large player populations, with a documented horizontal scaling path
- Simple UI: incremental/idle game aesthetic, not a full MMO client
- Real-time updates: all clients see changes (unit counts, fortress captures, buildings constructed) with minimal lag

---

## 2. Deployment Strategy

This project has two distinct phases with different deployment targets. **The application architecture (API, game loop, data model, SignalR) is identical in both phases.** Only the hosting layer changes.

| | Phase 1 — Ship It | Phase 2 — Cloud |
|---|---|---|
| **Goal** | Live, public, playable, has real users | Production-grade, cloud-native, scalable |
| **Hosting** | Hetzner VPS | AWS (ECS Fargate + managed services) |
| **Entry condition** | Project completion | Phase 1 is live and has active users |
| **IaC** | Docker Compose + shell scripts | AWS CDK (TypeScript) |
| **Focus** | Shipping and stability | Scalability and cloud signal |

The rationale: a live, running game with real players is a stronger portfolio asset than a sophisticated cloud architecture that isn't publicly accessible. Phase 1 proves the product. Phase 2 proves cloud engineering depth. Both phases are portfolio artifacts.

---

## 3. High-Level Architecture

The logical architecture is the same in both phases:

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENTS                              │
│           React SPA (served by Caddy / CDN)                 │
│         SignalR (WebSocket) + REST API calls                │
└──────────────────────────┬──────────────────────────────────┘
                           │
                     HTTPS / WSS
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                API SERVER (ASP.NET Core / C#)               │
│                                                             │
│  ┌──────────────────────┐   ┌──────────────────────────┐   │
│  │  REST API            │   │  SignalR Hub             │   │
│  │  Auth, player        │   │  Server push only        │   │
│  │  actions, admin      │   │  No client commands      │   │
│  └──────────────────────┘   └──────────────────────────┘   │
└───────────────────────────────────┬─────────────────────────┘
                                    │ reads/writes hot state
                                    │ subscribes to pub/sub
                          ┌─────────▼──────────┐
                          │       Redis         │
                          │  Hot game state     │
                          │  Pub/sub channel    │
                          │  SignalR backplane  │
                          └─────────┬──────────┘
                                    │ publishes events
┌───────────────────────────────────▼─────────────────────────┐
│                    WORKER (Go)                              │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │  Tick Loop   │  │  Production  │  │  Persistence      │ │
│  │  (scheduler) │  │  & Capture   │  │  Flush (Postgres) │ │
│  └──────────────┘  └──────────────┘  └───────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                                    │
                          ┌─────────▼──────────┐
                          │     PostgreSQL      │
                          │  Persistent state   │
                          └────────────────────┘
```

### Tech Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| **API Server language** | C# | ASP.NET Core Web API |
| **Worker language** | Go | Standalone binary. Ideal for tick loop, concurrent simulation, and Redis I/O. |
| **Backend framework** | ASP.NET Core | Fastest path for delivery given existing experience and prior implementation. |
| **Real-time** | SignalR | Native ASP.NET Core real-time stack. API Server only — Worker never speaks to clients. |
| **ORM (API Server)** | EF Core + Npgsql | Migration-based schema management and typed data access for PostgreSQL. |
| **DB driver (Worker)** | pgx | Raw SQL for narrow, explicit game-state flush writes. No ORM in the Worker. |
| **Redis client (API Server)** | StackExchange.Redis | Intent queue writes, hot state reads, pub/sub subscribe, SignalR backplane. |
| **Redis client (Worker)** | go-redis | Intent queue drain, hot state read/write, pub/sub publish. |
| **Auth** | ASP.NET Core authentication + external OAuth + JWT | Google and Discord providers. Auth lives entirely in the API Server — Worker has no auth concerns. |
| **Frontend** | React + TypeScript | Frontend stays TypeScript; HTTP DTOs can be documented via OpenAPI and mirrored or generated client-side. |
| **Container** | Docker | API Server and Worker are separate images. |
| **IaC (Phase 2)** | AWS CDK (TypeScript) | Strong CDK ecosystem and aligns with the frontend/infra TypeScript layer. |

### Process Boundaries

The system runs as **two separate processes**, each in its own Docker container. They do not share memory. Redis is the communication layer between them.

**API Server** (`src/server`) — C# · ASP.NET Core
- Auth (ASP.NET Core authentication + external OAuth + JWT)
- REST API — all player commands (build, buff), player profile, admin endpoints
- SignalR hub — server-to-client push only (see rationale below)
- For player actions: validates auth + basic eligibility (cooldown check via Redis read), then **enqueues a player intent** to the Redis intent queue — no direct game state mutation
- Subscribes to Redis pub/sub channel to receive worker-originated events and fan them out to connected clients

**Worker** (`src/worker`) — Go
- **Sole owner of all authoritative game state mutation.** Nothing else writes game state.
- Tick loop — runs on a configurable interval (e.g., every 5–10 seconds) via `time.Ticker`
- At the start of each tick: drains the Redis intent queue and applies all pending player actions before running simulation
- Unit production — calculates units generated by each fortress's buildings since the last tick
- Capture resolution — checks whether any fortress has crossed the capture threshold and all child fortresses are captured; if so, resolves the capture and transfers units to the parent
- Production pause/resume — halts production on fortresses that have hit threshold but whose parent is not yet capturable; resumes and enters feeder mode once the parent is taken
- Writes updated state to Redis after each tick
- Publishes tick result events to Redis pub/sub channel for the API server to broadcast
- Periodic persistence flush from Redis → PostgreSQL via `pgx`

**The routing rule:** The Worker is the only process that mutates game state. The API Server only enqueues player intent and fans out Worker-originated events to clients via pub/sub. All broadcasts originate from the Worker — there are no direct API-to-client state updates.

**What lives in Redis between them**
- **Intent queue** (`intents:pending`) — a Redis List. API Server `RPUSH`es player intents (build, buff) as JSON payloads. Worker `LRANGE`/`DEL`s the full queue at the start of each tick to drain and apply all pending actions before running simulation.
- Authoritative runtime game state (unit counts, fortress ownership, production states, cooldowns, active buffs, game phase) — written exclusively by the Worker
- A pub/sub channel the Worker publishes to and the API Server subscribes to
- The SignalR Redis backplane namespace (for multi-instance Phase 2)

**If the worker dies mid-tick:** The API server continues serving requests normally. Players can still act — their intents accumulate in the Redis intent queue. Hot game state in Redis is unchanged from before the failed tick. When the worker restarts, it drains the intent queue and resumes. At worst, one tick is delayed. No corrupted state.

**If the API server dies:** The worker continues ticking, draining any queued intents, and writing to Redis. No clients are connected, so pub/sub messages are dropped harmlessly. When the API server restarts, clients reconnect and receive a full state snapshot on the `game:snapshot` event.

### Component Responsibilities

| Component | Process | Language | Role |
|-----------|---------|----------|------|
| **React SPA** | Client | TypeScript | Tree visualization, player actions UI, real-time state display |
| **REST API** | API Server | C# | Authentication, player actions (build, buff) → enqueues intent to Redis, player profile/data, admin |
| **SignalR Hub** | API Server | C# | Server-to-client push only. Subscribes to worker events via Redis pub/sub and broadcasts to connected clients. |
| **Intent Queue** | Redis | — | Redis List (`intents:pending`). API Server pushes. Worker drains at tick start. Decouples player input from simulation timing. |
| **Tick Loop** | Worker | Go | `time.Ticker`-based scheduler. Drains intent queue, then runs: unit production, capture resolution, production state transitions. |
| **Persistence Flush** | Worker | Go | Periodic write of Redis hot state to PostgreSQL via `pgx` |
| **Redis** | Shared | — | Intent queue, hot game state, pub/sub channel, SignalR backplane |
| **PostgreSQL** | Shared | — | Persistent storage: accounts, world definition, action history, capture log |

> **Note on Redis:** Hot game state, the pub/sub channel, the intent queue, and the SignalR backplane all share a single Redis instance. Logical separation is handled by key prefixes and channel namespacing. Separate clusters are a Phase 2+ concern.

### State Ownership Rules

These rules are architectural invariants. Violating them reintroduces split-brain state mutation.

| Rule | Detail |
|------|--------|
| **Worker is the sole game state mutator** | Only the Worker writes unit counts, fortress ownership, production states, cooldowns, buffs, or game phase. The API Server never mutates these directly. |
| **API Server enqueues intent, never applies it** | A player build/buff action results in an `RPUSH` to the intent queue. The game state change happens when the Worker drains and processes that intent on the next tick. |
| **Redis pub/sub is for notifications only** | Pub/sub carries tick result events for the API Server to broadcast to clients. It is fire-and-forget. It never carries commands, player actions, or anything requiring durability or replay semantics. |
| **EF Core owns migrations** | All schema migrations are generated via EF Core. The Worker's `pgx` writes must stay manually aligned with the EF Core schema. Migrations run in one place only. |

### Intent Queue — Detailed Flow

```
Player POSTs /api/game/build
  → API Server validates JWT
  → API Server reads cooldown key from Redis (read-only)
  → If cooldown not expired: return 429
  → If valid: RPUSH intents:pending <json payload>
     {
       playerId, actionType, fortressId,
       buildingTypeId, idempotencyKey, enqueuedAt
     }
  → API Server writes new cooldown expiry to Redis (cooldown key only — not game state)
  → API Server returns 202 Accepted

  [next Worker tick fires]
  → Worker: LRANGE intents:pending 0 -1  (read full queue)
  → Worker: DEL intents:pending           (delete queue)
  → Worker: applies each intent to in-memory game state copy
  → Worker: runs simulation (production, capture resolution, state transitions)
  → Worker: writes updated game state to Redis
  → Worker: publishes game:tick event to pub/sub channel
  → API Server subscriber receives event → broadcasts to SignalR clients
```

> **On cooldowns:** The API Server writes the cooldown expiry key (`cooldown:{playerId}`) directly to Redis. This is not game state — it is a pre-filter guard layer that prevents spam before the next tick. The Worker double-validates the cooldown key during intent application and is the authoritative decision-maker on whether an intent is accepted. The API Server owns *anti-spam gating*; the Worker owns *authoritative acceptance*.

> **Queue delivery guarantee — at-most-once (Phase 1):** The `LRANGE` + `DEL` drain pattern is simple and inspectable, but not fully crash-safe. If the Worker reads the queue, deletes it, and then crashes before successfully applying the intents, those actions are lost — they will not be replayed on restart. For a browser game with cooldown-gated actions, rare lost intents are an acceptable MVP tradeoff. This is documented deliberately, not overlooked. Phase 2 may upgrade to a safer pattern — Redis Streams with consumer groups, or an `RPOPLPUSH` processing-list pattern — if durability guarantees become a requirement.

### Pub/Sub Contract

Redis pub/sub is **notifications only**. The API Server receiving a pub/sub message should never need to take a write action as a result — it reads the payload and broadcasts it to SignalR clients. That's the entire job.

| Allowed over pub/sub | Not allowed over pub/sub |
|---|---|
| Tick result summaries | Player commands or intents |
| Capture outcomes | State mutation instructions |
| Production state change notifications | Anything requiring guaranteed delivery |
| Buff/cooldown expiry events | Anything requiring replay on restart |

If a message would cause data loss when missed (e.g., API Server restarting mid-publish), it does not belong in pub/sub. It belongs in the intent queue (Redis List) or PostgreSQL.

### SignalR is Push-Only — Why This Matters

All player commands go through the REST API. SignalR is strictly server-to-client broadcast. This is an explicit design decision, not an oversight.

**What this buys:**
- **Auth and validation stay in one place.** Every action passes through HTTP middleware — JWT verification, cooldown checks, rate limiting — before anything touches game state. Nothing slips through via a hub method.
- **The server stays authoritative.** Clients display state; they do not drive it. There is no "client says build a barracks" message the server has to trust or sanitize.
- **Audit trail is clean.** Every player action is a REST call with a clear request/response lifecycle. The action history table is a natural byproduct.
- **Simpler real-time code.** The hub has one job: listen to Redis pub/sub and relay events to the right clients. No command parsing, no session state, no ack complexity.

---

## 4. Game State Architecture

### The Tick-Based Game Loop

The game loop runs in the **Worker process** as a `time.Ticker`-based scheduler (Go) on a configurable tick interval (e.g., every 5–10 seconds). It is isolated from the API server and has no knowledge of SignalR connections or HTTP requests.

Each tick:
1. **Drain intent queue** — `LRANGE intents:pending 0 -1` then `DEL intents:pending`. Apply all pending player actions (build, buff) to in-memory game state before running simulation.
2. **Read** full current game state from Redis (`go-redis`)
3. **Process** unit production — for each captured fortress, calculate units generated by its buildings since the last tick. Skip fortresses in paused or recovery state.
4. **Resolve** any captures — for each fortress that has crossed the capture threshold, check if all child fortresses are captured. If so, resolve the capture: transfer a percentage of units to the parent, set the originating fortress to recovery mode, set the parent fortress to human-owned.
5. **Update** production states — resume production on any fortress that has recovered past the transfer loss; enter feeder mode (units beyond threshold feed to parent) where applicable.
6. **Update** Redis with new state
7. **Publish** a tick result event to the Redis pub/sub channel (the API server picks this up and fans out to SignalR clients)
8. **Persist** to PostgreSQL on a slower interval (e.g., every 60 seconds or on significant events) via `pgx`

Player actions (build, buff) are enqueued by the API Server and **applied by the Worker at the start of the next tick**. The Worker is the only process that applies actions to game state — there are no direct API-to-game-state writes.

> **Leader election (Phase 2):** When running multiple API server instances behind a load balancer, only one worker should run at a time. The worker uses a Redis distributed lock (`SET NX` with TTL) to elect a leader. If the leader dies, another worker instance acquires the lock within one tick interval. The worker is already a separate process, so this is a clean add — no changes to the API server required.

### State Split: Redis vs PostgreSQL

| Redis (hot state) | PostgreSQL (cold/persistent state) |
|---|---|
| Current unit counts per fortress | Player accounts & OAuth provider IDs |
| Fortress ownership (human/enemy) | Tree definition (fortresses, parent/child relationships, thresholds) |
| Fortress production state (producing/paused/feeder/recovery) | Unlocked building types per fortress |
| Active buffs & expiration times | Action history / audit log |
| Player cooldown timers | Game configuration (world config JSONB) |
| Current game phase/progress | Historical capture results |
| Building counts per fortress (per type) | |

### Why This Split?
- Player actions and the game loop read/write hot state at high frequency — Redis handles this with sub-millisecond latency
- PostgreSQL writes happen on a controlled interval via EF Core-backed persistence and Worker flushes, making DB load predictable regardless of player count
- If Redis goes down, the system recovers from the most recent PostgreSQL snapshot. Transient runtime data since the last flush may be lost — this includes pending intents in the queue, ephemeral state changes not yet flushed, and any pub/sub notifications in flight. The game world rolls back to the last persisted snapshot. For a game where the flush interval is ~60 seconds, this is an acceptable durability window at Phase 1 scale.
- This pattern also makes horizontal scaling straightforward: multiple app instances share the same Redis

### Persistence Layer

**API Server** uses **EF Core + Npgsql** for PostgreSQL interaction. Schema is defined in C# entity configuration, migrations are managed via `dotnet ef`, and the API uses typed queries for normal application reads/writes. EF Core is the single source of truth for schema migrations.

**Worker** uses **`pgx`** (raw SQL) for its narrow, explicit game-state flush writes. No ORM. The Worker's SQL paths are minimal and must stay manually aligned with the EF Core schema. When migrations run, the Worker's queries must be reviewed for compatibility — this is a small, explicit maintenance surface, not an automatic one.

---

## 5. Data Model (PostgreSQL)

### Core Tables

```
players
├── id (UUID, PK)
├── display_name (VARCHAR)
├── oauth_provider (VARCHAR) — "google" | "discord"
├── oauth_id (VARCHAR)
├── created_at (TIMESTAMP)
└── last_active_at (TIMESTAMP)

game_worlds
├── id (UUID, PK)
├── name (VARCHAR)
├── status (VARCHAR) — "active" | "completed" | "paused"
├── config (JSONB) — world-level configuration: capture thresholds, transfer percentage,
│                    production rates, etc. Set at world creation, not modified at runtime.
├── created_at (TIMESTAMP)
└── completed_at (TIMESTAMP, nullable)

fortresses
├── id (UUID, PK)
├── game_world_id (FK → game_worlds)
├── name (VARCHAR)
├── position_x (FLOAT) — map coordinates
├── position_y (FLOAT)
├── owner (VARCHAR) — "human" | "enemy"
├── parent_id (FK → fortresses, nullable) — null for the final center fortress
├── capture_threshold (INT) — units required to trigger capture of this fortress
├── difficulty_tier (INT) — for building unlock scaling
└── is_final_objective (BOOLEAN)

NOTE: Children of a fortress are not stored explicitly. The full tree is assembled
at runtime from parent_id references (SELECT * FROM fortresses WHERE parent_id = :id).
The Worker loads the complete set of fortresses in a single query at startup
(SELECT * FROM fortresses WHERE game_world_id = :id), then builds the tree in memory
by iterating the results and linking nodes via parent_id. The tree topology never
changes during a campaign — only ownership and unit counts do.

building_types
├── id (UUID, PK)
├── game_world_id (FK → game_worlds)
├── name (VARCHAR)
├── production_rate (FLOAT) — units produced per second
├── production_rate (FLOAT) — units produced per second
├── unlock_tier (INT) — difficulty_tier at which this building type becomes available
└── description (VARCHAR)

buildings
├── id (UUID, PK)
├── fortress_id (FK → fortresses)
├── building_type_id (FK → building_types)
├── total_units (INT)
├── built_by (FK → players)
└── built_at (TIMESTAMP)

player_contributions
├── id (UUID, PK)
├── player_id (FK → players)
├── game_world_id (FK → game_worlds)
├── action_type (VARCHAR) — "build"
├── target_fortress_id (FK → fortresses)
├── building_type_id (FK → building_types)
└── created_at (TIMESTAMP)

capture_log
├── id (UUID, PK)
├── game_world_id (FK → game_worlds)
├── fortress_id (FK → fortresses)
├── units_at_capture (INT)
├── units_transferred (INT)
├── transferred_to_id (FK → fortresses, nullable) — null if final objective
└── captured_at (TIMESTAMP)
```

---

## 6. API Design

### REST Endpoints

```
Authentication
  GET  /api/auth/login/:provider      — Initiate OAuth flow (Google/Discord) via ASP.NET Core auth middleware
  GET  /api/auth/callback/:provider   — OAuth callback, issues JWT (httpOnly cookie or Bearer token)
  POST /api/auth/refresh              — Refresh JWT token

Player
  GET  /api/player/me                 — Current player profile
  GET  /api/player/me/cooldowns       — Current cooldown timers
  GET  /api/player/me/contributions   — Build history

Game State (read from Redis, fallback to DB)
  GET  /api/game/state                — Full current game state snapshot
  GET  /api/game/fortresses           — All fortresses with current unit counts, ownership, production state
  GET  /api/game/fortresses/:id       — Single fortress detail: unit count, buildings, production state, children/parent
  GET  /api/game/captures/recent      — Recent capture results

Player Actions
  POST /api/game/build                — Construct a building in a captured fortress
  POST /api/game/buff                 — Apply a production buff to a fortress
  GET  /api/game/buildings/available  — Building types available at a given fortress (based on unlock tier)

Admin (protected by API key or admin role)
  GET  /api/admin/stats               — Live game stats (player count, fortress status, activity)
  POST /api/admin/announce            — Push a message to all connected clients
  POST /api/admin/world/pause         — Pause the active game world
  POST /api/admin/world/resume        — Resume a paused game world
```

### SignalR Hub: `/hubs/game`

**Server → Client Events:**

| Event | Payload | Trigger |
|-------|---------|---------|
| `fortress:updated` | `{ fortressId, unitCount, owner, productionState }` | Unit count changes, ownership changes, production state changes |
| `fortress:captured` | `{ fortressId, unitsTransferred, parentId }` | Fortress capture threshold crossed and resolved |
| `building:constructed` | `{ fortressId, buildingType, builtBy }` | Player constructs a building |
| `buff:applied` | `{ fortressId, buffType, duration }` | Player applied a production buff |
| `game:snapshot` | Full state | On client connect (initial load) |
| `player:count` | `{ count }` | Periodic broadcast |

**Client → Server:** None. All actions go through the REST API. SignalR is push-only from the server.

> **SignalR backplane (Phase 2):** When running multiple API server instances behind a load balancer, Redis is used as the SignalR backplane so any API instance can publish to any connected client. Already accounted for in the Redis architecture.

---

## 7. Unit Production & Capture Resolution

### Unit Production

Units are produced passively by buildings inside captured fortresses. Each building type has a `production_rate` (units per second). The Worker calculates units generated since the last tick for each active fortress:

```
units_generated = sum(building.production_rate for each building in fortress) * seconds_since_last_tick
fortress.unit_count += units_generated
```

Production is skipped for fortresses in `paused` or `recovery` state.

### Production States

| State | Description |
|---|---|
| `producing` | Normal production. Units accumulate toward capture threshold. |
| `paused` | Threshold reached but parent fortress is not yet capturable (not all sibling branches cleared). No units produced. |
| `recovery` | Capture just resolved. Fortress lost a percentage of units to transfer. Producing until recovered to threshold. |
| `feeder` | Fortress is at threshold and parent is already captured. Any units produced beyond threshold feed directly into the parent's unit count. |

### Capture Resolution

At each tick, the Worker checks every human-held fortress for capture eligibility:

```
for each fortress in tree:
  if fortress.unit_count >= capture_threshold:
    if all children of fortress.parent are human-owned:
      resolve_capture(fortress.parent)
```

Capture resolution:
```
units_transferred = fortress.unit_count * transfer_percentage  // e.g. 5%
parent.owner = "human"
parent.unit_count += units_transferred
fortress.unit_count -= units_transferred
fortress.production_state = "recovery"
```

If the captured fortress is the final objective (`is_final_objective = true`), the game world is marked as completed.

### Building Unlocks

Building types have an `unlock_tier` that corresponds to the `difficulty_tier` of the fortress they are built in. As players push deeper into the tree and encounter higher-tier fortresses, more powerful building types become available. Early-game buildings are outpaced quickly — a single late-game building may produce more units than an entire outer fortress.

### Buffs

Buffs apply a temporary production multiplier to a target fortress:

```
effective_production_rate = base_production_rate * buff_multiplier
```

Buff duration and multiplier values are defined in the world config JSONB. Multiple buffs on the same fortress stack multiplicatively. Buff expiry is tracked in Redis and checked each tick.

---

## 8. Admin System

The enemy faction has been removed — fortresses are static obstacles, not active agents. The admin system is scoped to operational controls and observability only. There are no runtime balance tuning parameters; world balance is baked into the seed data and the world config JSONB at world creation.

**Admin API Endpoints:**

```
Admin (protected by API key or admin role)
  GET  /api/admin/stats               — Live game stats (player count, fortress status, activity)
  POST /api/admin/announce            — Push a message to all connected clients
  POST /api/admin/world/pause         — Pause the active game world
  POST /api/admin/world/resume        — Resume a paused game world
```

---

## 9. Phase 1 Infrastructure — Hetzner VPS

**Goal:** Get a stable, public, playable game in front of real users at minimal fixed cost.

### Server Spec

| Resource | Choice | Estimated Cost |
|---|---|---|
| VPS | Hetzner CX22 (2 vCPU, 4GB RAM) | ~€4/mo |
| Object Storage (frontend) | Hetzner Object Storage | ~€1/mo |
| Domain + TLS | Namecheap + Let's Encrypt (via Caddy) | ~$10/yr |
| **Total** | | **~€5/mo + domain** |

### Stack on the VPS

```
┌──────────────────────────────────┐
│           Hetzner VPS            │
│                                  │
│  Caddy (reverse proxy + TLS)     │
│         │          │             │
│   API Server     Static files    │
│    (Docker)      (React build)   │
│         │                        │
│   Docker Compose                 │
│    ├── api-server (ASP.NET Core) │
│    ├── worker (Go binary)        │
│    ├── postgres                  │
│    └── redis                     │
└──────────────────────────────────┘
```

- **Caddy** handles TLS automatically via Let's Encrypt and reverse proxies to the API server container. The worker has no public ingress — it communicates only through Redis.
- **Docker Compose** manages all four services: api-server, worker, PostgreSQL, Redis. Both app containers share the same internal Docker network and Redis instance.
- **React build** is served as static files by Caddy (no S3/CDN needed at this scale)
- **Backups:** Hetzner automated VPS snapshots (weekly) + `pg_dump` to object storage (daily via cron)

### Deployment

```bash
# On push to main (GitHub Actions):
1. Build & test API Server (`dotnet build`, `dotnet test`)
2. Build & test Worker (`go build ./...`, `go test ./...`)
3. Build api-server Docker image, push to GHCR
4. Build worker Docker image, push to GHCR
5. SSH into VPS
6. `docker compose pull && docker compose up -d`
7. Build React app, rsync static files to VPS
```

No managed services, no cloud-specific tooling. Straightforward and cheap.

### Ops Basics

- **TLS:** Automatic via Caddy + Let's Encrypt
- **Monitoring:** UptimeRobot (free tier) for uptime alerts; Caddy access logs
- **Backups:** Daily `pg_dump` to Hetzner Object Storage via cron job
- **Rollback:** Tag Docker images by git SHA; roll back by redeploying previous tag

### Phase 1 Exit Criteria

Phase 2 does not begin until all of the following are true:
- The game is publicly accessible at a real domain
- Auth, game loop, and real-time updates are all working in production
- The game has been played by users who are not the developer
- The system has been stable for a meaningful period (no critical outages)

---

## 10. Phase 2 Infrastructure — AWS Cloud

**Goal:** Demonstrate cloud engineering depth. Build a production-grade AWS deployment path as a portfolio artifact alongside the live VPS demo.

This phase begins only after Phase 1 is complete and stable. The VPS demo stays live throughout Phase 2 — it's the link you send people.

### AWS Stack

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENTS                              │
│              React SPA (S3 + CloudFront)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
                     HTTPS / WSS
                           │
┌──────────────────────────▼──────────────────────────────────┐
│          ECS Fargate — API Service                          │
│          ASP.NET Core / C#                                  │
│          REST API + Auth + SignalR Hub                      │
└───────┬──────────────────────────────────┬──────────────────┘
        │ enqueues intent / reads cooldowns │ subscribes to pub/sub
        │                                  │
┌───────▼──────────────────────────────────▼──────────────────┐
│                    Redis (ElastiCache)                      │
│   Intent queue · Hot game state · Pub/sub · SignalR backplane│
└───────┬──────────────────────────────────┬──────────────────┘
        │ publishes tick events            │ drains intent queue
        │                                  │ reads/writes hot state
┌───────▼─────────────────────────────┐   │
│     ECS Fargate — Worker Service    │◄──┘
│     Go                              │
│     Tick Loop · Production          │
│     Capture · Persistence Flush     │
└───────────────┬─────────────────────┘
                │ periodic flush
      ┌─────────▼──────────┐
      │   PostgreSQL (RDS) │
      │   Persistent state │
      └────────────────────┘
```

Two ECS services, both managed under the same ECS cluster. The Worker service runs a single task (desired count = 1) — the tick loop must not run in parallel. The API service can scale horizontally behind an ALB when multi-instance support is needed; the Redis backplane handles cross-instance broadcasting automatically.

**Worker single-instance strategy — two layers:**
- **Operational:** ECS Worker service desired count is set to 1. Under normal conditions, only one Worker task exists.
- **Safety net:** The Worker holds a Redis distributed lock (`SET NX` with TTL) on startup. This protects against duplicate tick ownership during deployment races or brief ECS overlap when a new task starts before the old one drains. If a second Worker task starts before the first has stopped, it will fail to acquire the lock and exit cleanly. The lock is not a substitute for desired count = 1; it is a guard against the edge cases desired count cannot prevent.

### AWS Resources

| Resource | Service | Estimated Cost |
|---|---|---|
| API service | ECS Fargate (0.25 vCPU, 0.5GB) | ~$9/mo |
| Worker service | ECS Fargate (0.25 vCPU, 0.5GB) | ~$9/mo |
| Database | RDS PostgreSQL (db.t3.micro, free tier yr 1) | $0–$15/mo |
| Cache | ElastiCache Redis (cache.t3.micro) | ~$12/mo |
| Frontend | S3 + CloudFront | ~$1/mo |
| DNS | Route 53 | ~$0.50/mo |
| **Total** | | **~$31–47/mo** |

> **No ALB at MVP.** The ALB (~$16/mo) is deferred for cost reasons. Phase 2 can begin as a short-lived cloud deployment artifact — CI/CD pipeline, ECS task/service, and internal validation — without a public-facing load balancer. Production-grade public ingress (ALB + TLS + stable routing) is added when demonstrating multi-instance support, at which point the ALB earns its cost.

> **No NAT Gateway.** Fargate tasks run in a public subnet for this portfolio deployment. NAT Gateways cost ~$32/mo just to exist. Public-subnet Fargate is a deliberate temporary simplification for the portfolio path; private subnets with NAT or VPC endpoints are the production-hardening step, added if this becomes a real production system.

> **Teardown strategy.** The CDK stack can be fully destroyed with `cdk destroy` when not actively needed. The code, the architecture diagram, and the deployment screenshots are the portfolio artifact — not a continuously running bill.

### CDK Stack Structure

```
/infra (AWS CDK TypeScript project)
├── bin/
│   └── app.ts
├── lib/
│   ├── network-stack.ts      — VPC, subnets, security groups
│   ├── database-stack.ts     — RDS PostgreSQL, ElastiCache Redis
│   ├── compute-stack.ts      — ECS cluster, API Fargate service, Worker Fargate service
│   ├── frontend-stack.ts     — S3 bucket, CloudFront distribution
│   └── pipeline-stack.ts     — GitHub Actions integration
```

### Scaling Path (Future)

- **API service scales horizontally** behind an ALB with sticky sessions (sticky sessions for long-lived real-time connections). The SignalR backplane handles cross-instance broadcasting automatically.
- **Worker service stays at one task** — the tick loop should not run in parallel. The worker is already a separate ECS service, so its desired count stays at 1 regardless of how many API instances are running.
- **Read replicas** for PostgreSQL if DB reads become a bottleneck

### Why Not Kubernetes?
ECS Fargate was chosen deliberately over EKS:
- EKS control plane costs ~$75/mo before running any workloads
- Two services (API + worker) is not a microservices mesh — Fargate handles this cleanly
- Fargate provides container orchestration, auto-scaling, and health checks — everything needed here
- Migration to EKS is straightforward if/when the complexity warrants it

---

## 11. CI/CD Pipeline

### Phase 1 (VPS)

```
On push to main:
  1. Build & test API Server (`dotnet build`, `dotnet test`)
  2. Build & test Worker (`go build ./...`, `go test ./...`)
  3. Build api-server Docker image, push to GHCR
  4. Build worker Docker image (Go binary, scratch/alpine base), push to GHCR
  5. SSH into Hetzner VPS
  6. docker compose pull && docker compose up -d --remove-orphans
  7. Build React app
  8. rsync build/ to VPS static files directory
```

### Phase 2 (AWS)

```
On push to main (backend changes):
  1. Build & test API Server (`dotnet build`, `dotnet test`)
  2. Build & test Worker (`go build`, `go test`)
  3. Build api-server image, push to Amazon ECR
  4. Build worker image (Go binary), push to Amazon ECR
  5. Deploy new task definition to ECS API service
  6. Deploy new task definition to ECS Worker service

On push to main (frontend changes):
  1. Build React app
  2. Sync to S3
  3. Invalidate CloudFront cache
```

Separate workflows for backend and frontend. Both triggered on push to `main`.

---

## 12. Project Structure

```
/
├── src/
│   ├── server/                              — API Server (ASP.NET Core)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs           — OAuth callback routes, token issuance
│   │   │   ├── PlayerController.cs         — Profile, cooldowns, contribution history
│   │   │   ├── GameController.cs           — Build, buff, game state reads
│   │   │   └── AdminController.cs          — Stats, announce, world pause/resume
│   │   ├── Hubs/
│   │   │   └── GameHub.cs                  — SignalR hub (push-only)
│   │   ├── Application/
│   │   │   ├── Auth/
│   │   │   ├── Players/
│   │   │   ├── Game/
│   │   │   └── Admin/
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/
│   │   │   │   ├── AppDbContext.cs
│   │   │   │   └── Configurations/
│   │   │   ├── Redis/
│   │   │   │   └── RedisConnectionFactory.cs
│   │   │   ├── Messaging/
│   │   │   │   └── WorkerEventSubscriber.cs
│   │   │   └── Auth/
│   │   │       └── ExternalProviders/
│   │   └── Contracts/
│   │       ├── Requests/
│   │       ├── Responses/
│   │       └── Events/
│   │
│   ├── worker/                              — Worker Process (Go)
│   │   ├── main.go                          — Entry point, loads fortress tree, starts tick loop
│   │   ├── tick/
│   │   │   └── loop.go                      — time.Ticker scheduler, tick orchestration, intent drain
│   │   ├── game/
│   │   │   ├── production.go                — Unit production calculation per fortress per tick
│   │   │   ├── capture.go                   — Capture threshold checks and resolution
│   │   │   └── transfer.go                  — Unit transfer to parent on capture
│   │   ├── intent/
│   │   │   └── queue.go                     — Redis List drain: LRANGE + DEL intents:pending
│   │   ├── state/
│   │   │   └── redis.go                     — go-redis client, hot state read/write
│   │   ├── persistence/
│   │   │   └── flush.go                     — Periodic flush to PostgreSQL via pgx
│   │   ├── publisher/
│   │   │   └── pubsub.go                    — Redis pub/sub: publishes tick events for API server
│   │   └── go.mod
│   │
│   └── client/                              — React App
│       ├── src/
│       │   ├── components/
│       │   │   ├── GameMap.tsx              — Tree visualization (Canvas or SVG)
│       │   │   ├── FortressPanel.tsx        — Unit count, buildings, production state, progress
│       │   │   ├── BuildingMenu.tsx         — Available building types, build action
│       │   │   └── ActionBar.tsx
│       │   ├── hooks/
│       │   │   └── useGameHub.ts            — SignalR connection + event handling
│       │   ├── services/
│       │   │   └── api.ts                   — REST API client
│       │   ├── types/
│       │   │   ├── game-state.ts
│       │   │   └── events.ts
│       │   └── App.tsx
│       └── package.json
│
├── src/server/Migrations/                  — EF Core migrations
│
├── infra/                                  — AWS CDK (TypeScript) — Phase 2
│   ├── bin/app.ts
│   └── lib/
│
├── deploy/
│   ├── docker-compose.prod.yml
│   ├── Caddyfile
│   └── backup.sh
│
├── tests/
│   ├── unit/
│   └── load/
│
├── .github/
│   └── workflows/
│       ├── backend.yml
│       └── frontend.yml
│
├── docker-compose.yml                      — Local dev: API server + worker + PostgreSQL + Redis
├── Dockerfile.server                       — API server image
├── Dockerfile.worker                       — Worker image
├── src/server/Privarta.Game.Api.csproj
├── README.md
└── .gitignore
```

> **Contract note:** There are no magical shared compile-time types between the C# API and the React client. HTTP DTOs are the contract boundary for REST, and Redis JSON payloads are the contract boundary between the API/Worker real-time path. Keep those DTOs explicit and versioned.

---

## 13. Milestone Plan

### Phase 1A: Foundation (Weeks 1–2)
- [ ] Initialize repository and baseline project structure
- [ ] Configure Docker Compose for local development
- [ ] Scaffold ASP.NET Core API Server
- [ ] Configure API Server integration with PostgreSQL and Redis
- [ ] Set up EF Core migrations and establish initial schema
- [ ] Implement external authentication providers (Google + Discord)
- [ ] Build initial REST endpoints for player profile and game state reads
- [ ] Scaffold Worker service in Go
- [ ] Establish Worker connectivity to Redis and PostgreSQL
- [ ] Implement initial tick-loop stub using `time.Ticker`
- [ ] Implement first-pass intent queue flow between API Server and Worker
- [ ] Configure Caddy and Docker Compose for VPS deployment
- [ ] Set up GitHub Actions for build, publish, and deploy workflows

### Phase 1B: Core Game Loop (Weeks 3–4)
- [ ] Seed game world: fortress tree definition, building types, world config JSONB
- [ ] Worker: load and cache fortress tree from PostgreSQL at startup
- [ ] Worker: unit production calculation per fortress per tick
- [ ] Worker: production state machine (producing → paused → recovery → feeder)
- [ ] Worker: capture resolution — threshold check, children check, unit transfer
- [ ] API: build action validates auth + cooldown + fortress ownership, enqueues intent
- [ ] Worker: drains intent queue, applies build actions to fortress building counts
- [ ] Worker: publishes state update to pub/sub after each tick
- [ ] API Server: broadcasts worker-originated update to SignalR clients

### Phase 1C: Real-Time & Frontend (Weeks 5–7)
*Goal: everything a player sees and interacts with in real-time. No infrastructure scaling work in this phase.*
- [ ] SignalR hub (single instance — no adapter needed at this scale)
- [ ] Real-time broadcasts: fortress updates, captures, buildings constructed
- [ ] React app: tree visualization (Canvas or SVG)
- [ ] Fortress detail panels: unit count, buildings, production state, progress to capture
- [ ] Action UI: build menu, available building types per fortress, cooldown timer
- [ ] Live player count display

### Phase 1D: Polish & Launch (Weeks 8–10)
- [ ] Buff system: temporary production multipliers
- [ ] Building unlock progression tied to fortress difficulty tier
- [ ] Capture log / activity feed
- [ ] Load testing (k6 scripts for API load + simulated connection patterns)
- [ ] Backups, monitoring (UptimeRobot), ops runbook
- [ ] README, architecture diagrams, portfolio write-up
- [ ] **Deploy to Hetzner VPS — publicly accessible at real domain**
- [ ] Share on Reddit (r/incremental_games, r/webgames, r/gamedev)
- [ ] Collect feedback, confirm real users playing

### Phase 2: Cloud Path (After Phase 1 is live and stable)
- [ ] AWS CDK stack (TypeScript): VPC, RDS, ElastiCache, ECS Fargate, ECR
- [ ] Enable Redis-backed SignalR scale-out for multi-instance ECS deployment
- [ ] GitHub Actions → ECR → ECS deployment pipeline
- [ ] S3 + CloudFront for frontend
- [ ] Multi-instance support: ALB + sticky sessions + leader election
- [ ] CloudWatch monitoring & alerting
- [ ] AWS Budget alerts (prevent surprise bills)
- [ ] Document Phase 2 in README alongside Phase 1 demo link
- [ ] (Optional) Deploy Phase 2 stack, screenshot, then tear down

---

## 14. Key Architectural Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| Phase 1 hosting | Hetzner VPS | Predictable fixed cost (~€5/mo), no surprise bills, fast to ship, stays public 24/7 |
| Phase 2 hosting | AWS ECS Fargate | Cloud engineering portfolio signal, maps to real employer stacks |
| Deployment sequencing | VPS first, cloud second | A live game with users is a stronger portfolio artifact than an undeployed cloud design |
| Cloud compute | ECS Fargate | Right-sized for two small services, no K8s overhead, within budget |
| No ALB at MVP | Defer until multi-instance | ALB costs ~$16/mo with no benefit at single-instance scale |
| No NAT Gateway | Public subnet for Fargate | NAT Gateways cost ~$32/mo just to exist; not justified at this scale |
| Real-time | SignalR + Redis backplane | Native ASP.NET Core stack, straightforward in C#, scales horizontally with Redis in Phase 2 |
| Real-time remains push-only | Deliberate | All commands go through REST; the hub is broadcast only — cleaner auth, validation, and audit trail |
| Worker / API split | Two separate processes | Fault isolation, cleaner mental model, independent scaling path |
| API framework | ASP.NET Core | Highest delivery velocity and lowest framework tax given existing experience and the prior implementation |
| Worker language | Go | Tick loop, concurrent simulation, and Redis I/O are ideal Go workloads. Keeps a meaningful second-backend signal without slowing the API layer. |
| Worker is sole state mutator | Deliberate | Eliminates split-brain write model. API Server enqueues intent only. Worker applies all game state changes. |
| Intent queue pattern | Redis List (`intents:pending`) | Decouples player input timing from simulation tick. API Server `RPUSH`es; Worker drains at tick start. Simple, inspectable, and acceptable as an at-most-once MVP tradeoff — not fully crash-safe. |
| Pub/sub for notifications only | Deliberate | Redis pub/sub is fire-and-forget. Commands and durable events belong in the intent queue or PostgreSQL. Pub/sub is only for tick result fan-out to connected clients. |
| State management | Redis (hot) + PostgreSQL (cold) | Predictable DB writes, sub-ms reads for game state |
| Worker ↔ API comms | Redis pub/sub (Worker→API) + Redis List (API→Worker) | Fully decoupled. Worker publishes events; API fans out to clients. API enqueues intents; Worker drains and applies. |
| Game loop | Standalone Go worker with `time.Ticker` | Isolated from HTTP/real-time concerns; independently restartable, goroutine-native concurrency |
| IaC (Phase 2) | AWS CDK (TypeScript) | Good CDK ecosystem, aligns with the frontend/infra TypeScript layer, and stays separate from API delivery concerns |
| ORM (API Server) | EF Core + Npgsql | Familiar, typed, migration-based, single source of schema truth |
| DB driver (Worker) | `pgx` | Narrow, explicit writes. No ORM needed for game state flush. |
| Auth | ASP.NET Core auth + external OAuth + JWT | Standard C# web stack; Google and Discord providers. Auth lives entirely in API Server. |
| Frontend (Phase 1) | Caddy static files | Free, zero-config, no CDN complexity at low traffic |
| Frontend (Phase 2) | React on S3 + CloudFront | Industry standard, cheap static hosting |
| No K8s | Deliberate | Cost ($75/mo control plane), complexity, overkill for two small services |
| Fortress tree topology | Parent/child references (`parent_id`) | Simple, queryable, assembles into full tree in a single DB read at Worker startup. Children derived at runtime — not stored explicitly. |
| World balance | Fixed config JSONB on `game_worlds` | Balance parameters set at world creation, not tuned at runtime. Player population determines pace — more players means faster completion, not a different game. |
| No enemy AI | Deliberate | Fortresses are static obstacles. Complexity budget goes into the production and progression systems, not active opposition. |
| Production state machine | `producing → paused → recovery → feeder` | Ensures unit counts are coherent across captures. Prevents runaway accumulation. Naturally funnels units inward as the campaign progresses. |

---

*This is a living document. Update as decisions evolve.*
