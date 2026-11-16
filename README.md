# Conway's Game of Life API

A production-ready RESTful API for [Conway's Game of Life](https://en.wikipedia.org/wiki/Conway%27s_Game_of_Life) built with C# / .NET 8.0.

Upload a board once and it becomes an **immutable generation-0 baseline** persisted to SQLite (crash/restart safe, zero external setup). Read endpoints then compute generation 1, generation *N*, or the board's **final settled state** — distinguishing still lifes from oscillators via canonical SHA-256 state hashing — without ever mutating what you uploaded.

> **Reviewing this submission?** Start with [`DOCUMENTATION.md`](DOCUMENTATION.md) — requirement-by-requirement traceability, architecture, design rationale, quality evidence and known limitations.
>
> **Further detail:** [`docs/PROJECT-DETAILS.md`](docs/PROJECT-DETAILS.md) — every folder, file and method explained, with the alternatives weighed for each decision. [`docs/WALKTHROUGH-TRANSCRIPT.md`](docs/WALKTHROUGH-TRANSCRIPT.md) — a narrated tour with real captured request/response output, a guided code tour and technology explainers.

**Features**

- SQLite + EF Core persistence that survives application restarts and crashes
- Cycle detection: `Stable` (still lifes, extinction) vs `Oscillating` (with `cyclePeriod`)
- RFC 7807 `ProblemDetails` for every error (validation, 404, 422, unhandled 500)
- Strongly-typed, startup-validated configuration (`IOptions<GameSettings>`)
- Swagger UI, 95 xUnit unit + integration tests, zero-warning build (`TreatWarningsAsErrors`)

## Prerequisites & Quickstart

Requires the [.NET SDK 8.0.x](https://dotnet.microsoft.com/download/dotnet/8.0). No database install needed — SQLite creates `gameoflife.db` next to the API on first run.

```powershell
dotnet test                              # run all 95 unit + integration tests
dotnet run --project GameOfLife.Api     # start the API (http://localhost:5117)
```

Swagger UI: <http://localhost:5117/swagger>

> **PowerShell note:** `curl` is an alias for `Invoke-WebRequest` in Windows PowerShell. The examples below use `curl.exe` explicitly.

## API Reference

All endpoints live under `/api/boards`. All error responses are RFC 7807 `application/problem+json`.

| Method | Route | Success | Errors |
|---|---|---|---|
| `POST` | `/api/boards` | `201 Created` + `Location` | `400` invalid grid |
| `GET` | `/api/boards/{id}` | `200` generation-0 baseline | `404` unknown id |
| `GET` | `/api/boards/{id}/next` | `200` generation 1 | `404` |
| `GET` | `/api/boards/{id}/states/{steps}` | `200` generation `steps` | `400` steps out of range, `404` |
| `GET` | `/api/boards/{id}/final` | `200` settled state | `404`, `422` iteration limit reached |

### Upload a board — `POST /api/boards`

The body is `{ "state": [[...]] }` where cells are `0`/`1` integers **or** booleans (`true`/`false` are normalized to `1`/`0`). Grids must be non-empty, rectangular (no jagged rows), contain only 0/1 values, and fit within `MaxGridDimension` per side.

```bash
curl.exe -i -X POST http://localhost:5117/api/boards ^
  -H "Content-Type: application/json" ^
  -d "{\"state\":[[0,1,0],[0,1,0],[0,1,0]]}"
```

```http
HTTP/1.1 201 Created
Location: http://localhost:5117/api/boards/0d7f3c3e-...

{"id":"0d7f3c3e-...","rows":3,"cols":3,"generation":0,"state":[[0,1,0],[0,1,0],[0,1,0]]}
```

A `400` example (jagged rows):

```bash
curl.exe -s -X POST http://localhost:5117/api/boards -H "Content-Type: application/json" -d "{\"state\":[[0,1],[0]]}"
```

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Invalid board state.","status":400,"detail":"All rows must have the same length: row 0 has 2 cells but row 1 has 1."}
```

### Read computed generations

```bash
curl.exe -s http://localhost:5117/api/boards/{id}/next        # generation 1
curl.exe -s http://localhost:5117/api/boards/{id}/states/4    # generation 4
curl.exe -s http://localhost:5117/api/boards/{id}/final       # settled state
```

`/final` for the blinker uploaded above (it repeats generation 0 after two steps):

```json
{"id":"0d7f3c3e-...","status":"Oscillating","generation":0,"cyclePeriod":2,"state":[[0,1,0],[0,1,0],[0,1,0]]}
```

`/final` for a still life (note `cyclePeriod` is omitted entirely):

```json
{"id":"...","status":"Stable","generation":0,"state":[[1,1],[1,1]]}
```

`/final` when the board doesn't settle within `MaxFinalIterations` (e.g. a glider mid-flight with a tiny limit) returns `422 Unprocessable Entity`:

```json
{"title":"Board did not reach a final state.","status":422,"detail":"No stable or oscillating state was found within 1000 iterations. Increase Game:MaxFinalIterations or inspect the board pattern."}
```

## Configuration

Simulation limits are bound to `GameSettings` from the `Game` section of `appsettings.json`, validated with data annotations **at startup** (`ValidateOnStart`) — invalid configuration fails fast instead of surfacing mid-request.

| Key | Default | Meaning |
|---|---|---|
| `Game:MaxGridDimension` | `500` | Max rows and max columns of an uploaded board |
| `Game:MaxAllowedSteps` | `10000` | Max `steps` for `/states/{steps}` |
| `Game:MaxFinalIterations` | `1000` | Iteration budget for `/final` |
| `ConnectionStrings:GameOfLife` | `Data Source=gameoflife.db` | SQLite database file |

## Design & Trade-offs

### Architecture

```
GameOfLife.Api  ──►  GameOfLife.Infrastructure  ──►  GameOfLife.Core
(controllers,        (EF Core, SQLite,               (pure domain: engine,
 DTOs, ProblemDetails) repository, serialization)     simulation, validation, hashing)
```

`GameOfLife.Core` has **zero infrastructure dependencies**. Two seams keep the system extensible:

- **`IGameOfLifeEngine`** — the grid topology lives behind this interface. Today's implementation is `FiniteGridEngine`; a toroidal (wrap-around) engine would be a new implementation registered in DI, with no controller or service changes.
- **`IBoardRepository`** — defined in Core, implemented by SQLite/EF Core in Infrastructure; an alternative store swaps in without touching domain or API code.

### Grid topology: finite with a dead border

The board is a finite, bounded grid; cells outside the boundary are permanently dead, so edge/corner cells simply have fewer live-capable neighbors. This is the least surprising choice for an API whose input is a finite matrix: patterns like gliders eventually collide with the wall and decay into still lifes or oscillators (which is also what makes `/final` usually terminate).

### Immutability & idempotency

`POST` stores generation 0 and nothing ever updates it. Every `GET` recomputes deterministically from that baseline, so the same request always returns the same response, reads can be repeated/cached freely, and no read endpoint has side effects. Trade-off: generation *N* costs *N* engine steps on each request rather than being incrementally persisted — acceptable at these grid sizes, and cacheable later (see Possible Improvements).

### Cycle detection

`/final` hashes every visited generation — SHA-256 over `"{rows}x{cols}:"` plus one `'0'/'1'` byte per cell (the dimension prefix prevents 1×4 vs 2×2 collisions) — into a dictionary of hash → first generation seen. When the current hash is already present with period `p = currentGen − firstSeen`:

- `p == 1` → **`Stable`** (still life; extinction is the all-dead special case)
- `p ≥ 2` → **`Oscillating`** with `cyclePeriod: p`

The reported `generation` is when the returned state **first occurred** (cycle entry), and `state` is that state. A full SHA-256 collision between different grids is cryptographically negligible. If nothing repeats within `MaxFinalIterations`, the API returns `422` with a diagnostic rather than pretending the board settled.

`/states/{steps}` deliberately does **not** track hashes: it only needs the engine's "nothing changed" signal to short-circuit still lifes, keeping it O(1) in extra memory.

### Persistence

SQLite via EF Core gives durable, transactional, file-backed storage with zero setup, surviving crashes and restarts. The schema is created with `EnsureCreated()` on startup — right-sized for a single-table schema; a production system with an evolving schema would use EF migrations (`Database.Migrate()`). Cells are stored as a compact `'0'/'1'` text column (human-debuggable when inspecting the DB); bit-packing into a BLOB would be ~8× smaller but opaque.

### Complexity

For an *R*×*C* board: one engine step is **O(R·C)** time. `/states/{steps}` is **O(min(steps, settle) · R·C)** time and **O(R·C)** memory — the simulation double-buffers two flat `bool[]` arrays per request, allocating nothing further per generation. `/final` adds one 64-char hash per visited generation: **O(iterations)** extra memory (≤ 1000 by default).

### Error model

| Condition | Status |
|---|---|
| Malformed JSON, non-0/1/boolean cells, non-array rows | `400` (binder-level `ProblemDetails`) |
| Null/empty/jagged grid, values ∉ {0,1}, oversize grid | `400` (validator `ProblemDetails`) |
| Unknown board id | `404` |
| `steps < 1` or `steps > MaxAllowedSteps` | `400` |
| Non-integer `steps` (e.g. `/states/abc`) | `404` — the `{steps:int}` route constraint doesn't match, so the path denotes no resource (documented decision) |
| `/final` exceeds `MaxFinalIterations` | `422` |
| Unhandled exception | `500` via `IExceptionHandler`, details logged server-side only |

`GET /api/boards/{id}` (the generation-0 baseline) is a deliberate addition beyond the assignment's four endpoints: it gives the `201` `Location` header a dereferenceable target and makes restart-persistence directly observable.

## Testing

```powershell
dotnet test                                        # everything
dotnet test --collect:"XPlat Code Coverage"        # with coverlet coverage
```

- **Unit tests** (Core/Infrastructure/Api contracts): each of Conway's four rules in isolation; still lifes (block), oscillators (blinker, toad), glider translation; finite-boundary semantics (corner/edge, proving no wrap-around); simulation short-circuit proven with a counting engine decorator; stable/oscillating/not-reached semantics including extinction; validator, hasher (incl. canonical SHA-256 vector), cell serializer, JSON converter, and options validation.
- **Integration tests** (`WebApplicationFactory` over a real temp SQLite file per test class): full HTTP contract — status codes, `Location` header, `application/problem+json`, 0/1 numeric echo — plus **restart persistence**: a board uploaded under one host is served correctly by a second host booted over the same database file. The unhandled-exception path is also covered: a throwing repository proves `500` + `ProblemDetails` without leaking exception details. Temp databases are cleaned up with pool-clearing and retries (SQLite holds file handles on Windows).

## Possible Improvements

- EF Core migrations instead of `EnsureCreated()` for schema evolution
- Toroidal/infinite topologies as additional `IGameOfLifeEngine` implementations
- Bit-packed `ulong[]` grid with popcount neighbor counting for large boards
- ETag/response caching: results are deterministic, so generation *N* is infinitely cacheable
- Board listing/deletion endpoints, authentication, and rate limiting
