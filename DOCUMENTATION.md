# Conway's Game of Life API — Technical Documentation

**Submission documentation for review.** This document describes what was built, how to run and verify it, how each requirement in the brief is satisfied, and the engineering decisions behind the implementation — including the trade-offs considered and the limitations I would address before production.

| | |
|---|---|
| **Language / Runtime** | C# 12, .NET 8.0 (`net8.0`), built with SDK 8.0.424 |
| **Solution** | 4 projects — `Api`, `Core`, `Infrastructure`, `Tests` |
| **Persistence** | SQLite via Entity Framework Core 8.0.11 — no external setup required |
| **API surface** | 5 REST endpoints under `/api/boards`, documented with Swagger/OpenAPI |
| **Tests** | 95 xUnit tests (64 unit, 31 integration) — all passing in ~1 second |
| **Build quality** | 0 warnings; `TreatWarningsAsErrors` and nullable reference types enabled on all 4 projects |
| **Stubs / TODOs** | None — verified by search across the solution |

---

## Contents

1. [Running and verifying the solution](#1-running-and-verifying-the-solution)
2. [Requirements traceability matrix](#2-requirements-traceability-matrix)
3. [Architecture](#3-architecture)
4. [Key design decisions and rationale](#4-key-design-decisions-and-rationale)
5. [API specification](#5-api-specification)
6. [Quality evidence](#6-quality-evidence)
7. [Declared deviations and additions](#7-declared-deviations-and-additions)
8. [Known limitations and production roadmap](#8-known-limitations-and-production-roadmap)
9. [Suggested review path](#9-suggested-review-path)

---

## 1. Running and verifying the solution

**Prerequisite:** .NET SDK 8.0.x. Nothing else — no database server, no Docker, no configuration edits.

```powershell
# 1. Run the full test suite (expect: Passed! 95 tests)
dotnet test

# 2. Start the API
dotnet run --project GameOfLife.Api
```

The API listens on `http://localhost:5117`, with Swagger UI at `http://localhost:5117/swagger`. A SQLite file (`gameoflife.db`) is created automatically on first start.

### A two-minute functional verification

The following sequence exercises every significant behaviour. On Windows PowerShell, `curl` is an alias for `Invoke-WebRequest`, so `curl.exe` is used explicitly.

```powershell
# Upload a blinker (a period-2 oscillator) → expect 201 Created with a Location header
curl.exe -i -X POST http://localhost:5117/api/boards `
  -H "Content-Type: application/json" `
  -d '{\"state\":[[0,1,0],[0,1,0],[0,1,0]]}'

# Substitute the returned id below.
curl.exe -s http://localhost:5117/api/boards/{id}/next        # → generation 1, horizontal
curl.exe -s http://localhost:5117/api/boards/{id}/states/4     # → generation 4, back to vertical
curl.exe -s http://localhost:5117/api/boards/{id}/final        # → Oscillating, cyclePeriod 2

# Validation and error contract
curl.exe -s -X POST http://localhost:5117/api/boards -H "Content-Type: application/json" -d '{\"state\":[[0,1],[0]]}'
curl.exe -s http://localhost:5117/api/boards/{id}/states/0
curl.exe -s http://localhost:5117/api/boards/00000000-0000-0000-0000-000000000001
```

**To verify crash/restart safety manually:** upload a board, note its id, stop the process (Ctrl+C or kill it), start it again, then `GET /api/boards/{id}`. The board is returned unchanged. This is also covered by an automated test (§6.3).

A complete transcript of these calls with actual captured responses is in [`docs/WALKTHROUGH-TRANSCRIPT.md`](docs/WALKTHROUGH-TRANSCRIPT.md) §5.

---

## 2. Requirements traceability matrix

Each requirement from the brief, mapped to its implementation and the test that verifies it.

### 2.1 Core architectural requirements

| Requirement | Implementation | Verified by |
|---|---|---|
| Finite, bounded grid; cells outside the boundary treated as dead | `GameOfLife.Core/Engine/FiniteGridEngine.cs` — neighbour loop bounds are clamped to the grid, so off-grid cells are never counted | `FiniteGridEngineTests`: `Step_CornerPattern_TreatsOutOfBoundsNeighborsAsDead`, `Step_BlinkerAgainstLeftEdge_DoesNotWrapAround` |
| Rules encapsulated behind `IGameOfLifeEngine` so alternative topologies need no controller changes | `GameOfLife.Core/Abstractions/IGameOfLifeEngine.cs`; the concrete engine is resolved only in `Program.cs` DI registration | The test suite substitutes its own `IGameOfLifeEngine` implementation (`CountingEngine`) without touching any other code |
| SQLite + EF Core persistence, reliable across restarts/crashes, zero external setup | `GameOfLife.Infrastructure/` — `GameOfLifeDbContext`, `SqliteBoardRepository`; connection string in `appsettings.json` | `PersistenceTests.Board_SurvivesApplicationRestart` — two sequential hosts over one database file |
| Schema applied cleanly on startup | `Program.cs` — `await dbContext.Database.EnsureCreatedAsync()` inside a startup scope, before the server accepts requests | The restart test boots a second host against an existing database, proving idempotence |
| Uploaded boards immutable; GETs compute generations without mutating the baseline | `SimulationService` clones `board.Cells` before simulating; no write path exists outside `POST` | `SimulationServiceTests.ComputeGeneration_DoesNotMutateTheBoardBaseline`; `PersistenceTests.RepeatedReads_AreDeterministicAndNeverMutateTheBaseline` |
| Cycle detection via a canonical hash of dimensions + serialized cells | `GameOfLife.Core/Hashing/GridHasher.cs` — SHA-256 of `"{rows}x{cols}:"` + one `'0'`/`'1'` byte per cell | `GridHasherTests` — 5 tests including a known-answer SHA-256 vector and a dimension-collision test |
| Detect both static stability and oscillating cycles | `SimulationService.ComputeFinalState` — one hash dictionary detects both; period 1 = `Stable`, period ≥ 2 = `Oscillating` | `SimulationServiceTests` (block, extinction, blinker, toad); `FinalEndpointTests` over HTTP |

### 2.2 Project structure and deliverables

| Requirement | Delivered |
|---|---|
| `GameOfLife.Api` — ASP.NET Core Web API (`net8.0`), controllers, Swagger/OpenAPI, `IExceptionHandler` returning RFC 7807, `appsettings.json` | ✅ `Controllers/BoardsController.cs`, `ErrorHandling/GlobalExceptionHandler.cs`, Swashbuckle 6.9.0, full `appsettings.json` |
| `GameOfLife.Core` — pure domain: models, validation, canonical hashing, `IGameOfLifeEngine`; **zero infrastructure dependencies** | ✅ The project file contains **no `PackageReference` elements at all** — the dependency rule is enforced by the compiler, not convention |
| `GameOfLife.Infrastructure` — EF Core `DbContext`, entity configuration, SQLite repository, grid serialization | ✅ `Persistence/` (context, entity, `IEntityTypeConfiguration`, `CellSerializer`), `Repositories/SqliteBoardRepository.cs`, `DependencyInjection.cs` |
| `GameOfLife.Tests` — xUnit unit tests (4 rules, still lifes, oscillators, max-iteration bounds, validation) **and** integration tests (`WebApplicationFactory` verifying SQLite persistence across requests) | ✅ 64 unit + 31 integration tests; the integration suite runs the real application against a real SQLite file |
| `README.md` — setup, example `curl` requests, Design & Trade-offs | ✅ [`README.md`](README.md), plus two deep-dive documents in [`docs/`](docs/) |

### 2.3 Endpoint specifications

| Requirement | Implementation | Verified by |
|---|---|---|
| `POST /api/boards` accepts a 2D integer array **or boolean matrix** | `Contracts/FlexibleCellArrayConverter.cs` accepts both token types; booleans normalise to `1`/`0` | `FlexibleCellArrayConverterTests` (9 tests); `BoardsApiTests.PostBoard_BooleanMatrix_IsAcceptedAndNormalizedToIntegers` |
| Reject null/empty grids, jagged arrays, values other than 0/1, and grids exceeding configurable max dimensions — with `400` + `ProblemDetails` | `Core/Validation/BoardStateValidator.cs`; limit from `GameSettings.MaxGridDimension` (default 500) | `BoardStateValidatorTests` (11 tests); `BoardsApiTests.PostBoard_InvalidPayload_Returns400ProblemDetails` (8 cases) + oversize test |
| `201 Created` with `Location` header and `{ id, rows, cols, generation: 0, state }` | `BoardsController.Create` → `CreatedAtAction(nameof(GetById), …)` | `BoardsApiTests.PostBoard_ValidGrid_Returns201WithLocationAndEchoedState` |
| `GET /{id:guid}/next` → generation 1; `404` if absent | `BoardsController.GetNext` → `ComputeGeneration(board, 1)` | `GetNext_Blinker_ReturnsHorizontalPhaseAsGenerationOne`, `GetNext_UnknownBoard_Returns404ProblemDetails` |
| `GET /{id:guid}/states/{steps:int}` rejects `steps < 1` or `> MaxAllowedSteps` with `400` | Range check inside the action (not a route constraint — see §7) | `GetStates_OutOfRangeSteps_Returns400ProblemDetails` — `[Theory]` over 0, −1, 10001 |
| Short-circuit remaining iterations if a still life is reached early | `SimulationService.ComputeGeneration` breaks when the engine reports no change | `ComputeGeneration_StillLife_WithHugeStepCount_InvokesEngineExactlyOnce` — asserts the engine ran **once** for 10,000 requested steps |
| `200 OK` with `{ id, generation: steps, state }`; `404` if absent | `BoardsController.GetStateAfterSteps` | `GetStates_BlinkerTwoSteps_…`, `GetStates_StillLifeAtMaxAllowedSteps_…`, `GetStates_UnknownBoard_Returns404` |
| `GET /{id:guid}/final` iterates up to configurable `MaxFinalIterations` (default 1000) | `GameSettings.MaxFinalIterations`, passed into `ComputeFinalState` | `FinalEndpointTests`, `GameSettingsValidationTests` |
| Still life / extinction → `status: "Stable"` | Period-1 detection; extinction is the all-dead special case of the same rule | `GetFinal_Block_ReturnsStableAtGenerationZero_…`, `GetFinal_SingleCell_ExtinctionReportsStableAtGenerationOne` |
| Oscillator → `status: "Oscillating"` including `cyclePeriod` | Period = current generation − first sighting | `GetFinal_Blinker_ReturnsOscillatingWithPeriodTwo_AtCycleEntryGeneration` |
| Exceeding the limit → `422 Unprocessable Entity` + `ProblemDetails` stating the iteration limit | `BoardsController.GetFinalState` maps `NotReached` → 422 with the limit and the setting name in `detail` | `GetFinal_GliderWithTinyIterationBudget_Returns422ProblemDetails` |

### 2.4 Code quality and non-functional requirements

| Requirement | How it is satisfied | Evidence |
|---|---|---|
| **No stubs** — complete, compilable, production-ready code | No `TODO`, `FIXME`, `HACK`, or `NotImplementedException` anywhere in the solution | Verified by search across all `.cs` files |
| **Performance** — avoid excessive intermediate allocations per generation | Double buffering: two `bool[]` arrays are allocated per request and swapped, regardless of step count. Grids are flat, row-major primitive arrays. `GridHasher` rents its working buffer from `ArrayPool<byte>` | Measured: a 500×500 board responds in ~60 ms for both 10 and 10,000 requested steps (§6.4) |
| **`CancellationToken` on all async I/O** | Every controller action takes a `CancellationToken` (bound to `HttpContext.RequestAborted`) and forwards it to `IBoardRepository`, which passes it to `SaveChangesAsync(ct)` and `FirstOrDefaultAsync(…, ct)` | Verified by inspection: all 5 actions, both repository methods, and both EF Core calls. The only async call without a token is startup `EnsureCreatedAsync()`, where no request context exists |
| **Configuration** — `IOptions<GameSettings>` with data annotation validation on startup | `AddOptions<GameSettings>().Bind(…).ValidateDataAnnotations().ValidateOnStart()` in `Program.cs`; `[Range]` attributes on all three settings | `GameSettingsValidationTests` — 4 tests covering valid binding and out-of-range rejection |

---

## 3. Architecture

```
GameOfLife.Api ──────────► GameOfLife.Infrastructure ──────────► GameOfLife.Core
(HTTP, JSON, Swagger,      (EF Core, SQLite, persistence          (domain model, game rules,
 ProblemDetails, DI)        mapping and serialization)             validation, hashing — 0 packages)

GameOfLife.Tests ────────► all three
```

Dependencies point inward only. `GameOfLife.Core` references **no NuGet packages**, which means the game rules cannot reach a database or an HTTP context even accidentally — those types are not available in that project. The architecture is therefore enforced at compile time rather than by review convention.

Two interfaces form the extension seams:

- **`IGameOfLifeEngine`** isolates grid topology. Adding a toroidal (wrap-around) engine means writing one new class and changing one line of DI registration — no controller, service, repository or contract changes.
- **`IBoardRepository`** is declared in `Core` using domain types and implemented in `Infrastructure` (dependency inversion). Replacing SQLite with another store is confined to one project.

### Request flow

```
HTTP GET /api/boards/{id}/final
  → BoardsController.GetFinalState(id, cancellationToken)
  → IBoardRepository.GetAsync(id, ct)           → SELECT … WHERE Id = @id  (AsNoTracking)
                                                 → CellSerializer.Deserialize → domain Board
  → ISimulationService.ComputeFinalState(board, maxIterations)
      clone baseline → loop: engine.Step + buffer swap + SHA-256 hash → dictionary lookup
  → GridMapper.ToJagged(bool[] → int[][])
  → 200 OK  { id, status, generation, cyclePeriod?, state }
```

No write occurs on any read path.

A complete file-by-file reference, including every method and the alternatives weighed for each decision, is in [`docs/PROJECT-DETAILS.md`](docs/PROJECT-DETAILS.md).

---

## 4. Key design decisions and rationale

The brief identified three standard ambiguities. Each was resolved explicitly rather than implicitly.

### 4.1 Grid topology — finite with a dead border

| Option | Assessment |
|---|---|
| **Finite, dead border (chosen)** | A finite matrix in, a finite matrix out — the least surprising contract for an API whose input is a fixed-size grid. Also means patterns reliably settle, so `/final` terminates in realistic cases. |
| Toroidal (wrap-around) | Valid, but a glider would orbit forever and never settle, making `/final` return 422 for common inputs. |
| Infinite / expanding | Requires sparse storage and has no natural fixed-size JSON representation for responses. |

The decision is implemented declaratively — the neighbour scan clamps its loop bounds to the grid, so off-grid cells are never visited, which is exactly equivalent to treating them as dead while avoiding eight bounds checks per cell. Two tests use patterns that behave *differently* under wrap-around, so the choice cannot silently change.

### 4.2 Cycle detection — one mechanism for two outcomes

The Game of Life is deterministic: a given state always yields the same successor. Therefore **a repeated state proves the board is in a permanent loop**. The algorithm hashes each visited generation into a `Dictionary<hash, generationFirstSeen>`:

```
period = currentGeneration − firstSeenGeneration
period == 1  → the state produced itself         → Stable
period >= 2  → the board repeats                 → Oscillating, cyclePeriod = period
budget spent without a repeat                    → NotReached → HTTP 422
```

A still life is simply an oscillator of period 1, and extinction is a still life with no live cells — so one code path covers all three cases, with no risk of two separate detectors disagreeing.

**Why SHA-256 rather than a cheaper hash:** a collision would not make the API slow, it would make it *incorrect* — reporting a board as oscillating when it is not. SHA-256 is collision-safe for this purpose and stable across processes and runtime versions, unlike `object.GetHashCode()`. **Why hashes rather than stored grids:** retaining 1,000 generations of a 500×500 board would consume roughly 250 MB; the hashes are 64 characters each regardless of board size.

**Reported `generation` semantics:** the generation at which the returned state *first occurred*. For a blinker this is 0 (the vertical phase exists at generations 0, 2, 4, …); for a dying single cell it is 1 (the first all-dead generation). These semantics are documented in the README and pinned by tests so the contract is unambiguous.

### 4.3 Immutability and idempotency

`POST` stores generation 0; no code path ever updates it. Each read clones the baseline before simulating, so:

- the same request always returns the same response (safe to cache or retry),
- `GET` has no side effects, as HTTP requires,
- concurrent reads of one board cannot interfere.

The trade-off is that generation N is recomputed per request rather than incrementally persisted. At the configured limits this is inexpensive (§6.4), and because results are deterministic, response caching is the natural next optimisation.

### 4.4 Persistence — SQLite with `EnsureCreated()`

SQLite satisfies "reliable persistence with zero external setup": it is a single file, fully transactional (a crash mid-write cannot leave a partially written board), and requires no server. The brief permitted either migrations or `EnsureCreated()`; I chose the latter because the schema is a single table that never evolves, and it allows a reviewer to clone and run with no tooling. This is recorded in the README as a deliberate trade-off — a production system with an evolving schema should use EF migrations, which is a one-line change plus generated migration files.

Cells are persisted as a compact `'0'`/`'1'` text column. This is roughly eight times larger than bit-packing but remains human-readable in any SQLite browser, which materially helps debugging; at the 500×500 cap the worst case is 250 KB per board. The format is isolated in `CellSerializer`, so switching to a packed `BLOB` would touch one file.

### 4.5 Error handling — RFC 7807 throughout

Every error — `400`, `404`, `422`, and `500` — returns `application/problem+json` with the standard `type`, `title`, `status`, `detail` fields, so a client implements one error handler rather than several. Validation messages identify the precise problem, for example:

```
"All rows must have the same length: row 0 has 2 cells but row 1 has 1."
"Cell values must be 0 or 1, but row 0, column 1 contains 2."
"steps must be between 1 and 10000, but was -5."
```

Unhandled exceptions are caught by `GlobalExceptionHandler` (`IExceptionHandler`), which logs the full exception server-side and returns a generic problem document containing no exception message, stack trace, or internal paths. An integration test asserts that the thrown message does not appear anywhere in the response body.

---

## 5. API specification

Base path `/api/boards`. All error responses are RFC 7807 `application/problem+json`.

| # | Method | Route | Success | Error responses |
|---|---|---|---|---|
| 1 | `POST` | `/api/boards` | `201 Created` + `Location` + `{id, rows, cols, generation: 0, state}` | `400` — null/empty/jagged grid, values ∉ {0,1}, exceeds `MaxGridDimension`, malformed JSON |
| 2 | `GET` | `/api/boards/{id:guid}` | `200 OK` + generation-0 baseline | `404` — unknown id |
| 3 | `GET` | `/api/boards/{id:guid}/next` | `200 OK` + `{id, generation: 1, state}` | `404` |
| 4 | `GET` | `/api/boards/{id:guid}/states/{steps:int}` | `200 OK` + `{id, generation: steps, state}` | `400` — `steps < 1` or `> MaxAllowedSteps`; `404` |
| 5 | `GET` | `/api/boards/{id:guid}/final` | `200 OK` + `{id, status, generation, cyclePeriod?, state}` | `404`; `422` — no final state within `MaxFinalIterations` |

### Request and response examples

**Upload** — cells may be integers or booleans:

```json
POST /api/boards
{ "state": [[0,1,0],[0,1,0],[0,1,0]] }

201 Created
Location: http://localhost:5117/api/boards/92c08c49-dc87-4db6-94dd-b6a50197e765
{"id":"92c08c49-…","rows":3,"cols":3,"generation":0,"state":[[0,1,0],[0,1,0],[0,1,0]]}
```

**Final state — oscillator:**

```json
{"id":"92c08c49-…","status":"Oscillating","generation":0,"cyclePeriod":2,"state":[[0,1,0],[0,1,0],[0,1,0]]}
```

**Final state — still life.** `cyclePeriod` is omitted entirely rather than serialized as `null`, and a test asserts the property is absent:

```json
{"id":"f4b6508d-…","status":"Stable","generation":0,"state":[[1,1],[1,1]]}
```

**Unsettled board — 422:**

```json
{"type":"https://tools.ietf.org/html/rfc4918#section-11.2",
 "title":"Board did not reach a final state.",
 "status":422,
 "detail":"No stable or oscillating state was found within 1000 iterations. Increase Game:MaxFinalIterations or inspect the board pattern."}
```

**Responses always use integer cells.** Regardless of whether the upload used integers or booleans, and regardless of the internal `bool[]` representation, every response renders cells as numeric `0`/`1`. This is guaranteed by routing all five actions through a single `GridMapper.ToJagged` method, and verified by a test that parses the raw JSON and asserts every cell is a `JsonValueKind.Number`.

### Configuration

Bound to `GameSettings` from the `Game` section of `appsettings.json` and validated at startup:

| Setting | Default | Purpose |
|---|---|---|
| `Game:MaxGridDimension` | 500 | Maximum rows and maximum columns per uploaded board |
| `Game:MaxAllowedSteps` | 10000 | Maximum `steps` accepted by endpoint 4 |
| `Game:MaxFinalIterations` | 1000 | Iteration budget for endpoint 5 before returning 422 |
| `ConnectionStrings:GameOfLife` | `Data Source=gameoflife.db` | SQLite database file |

Invalid configuration (for example `MaxGridDimension: 0`) causes the application to **fail at startup** with a clear message, rather than starting and failing every request — `ValidateOnStart()` makes the failure a deployment-time event rather than a runtime one. All values are overridable by environment variable (`Game__MaxFinalIterations=50`), per-environment file, or command line, using standard ASP.NET Core configuration precedence.

---

## 6. Quality evidence

### 6.1 Build gates

All four projects enable nullable reference types and `TreatWarningsAsErrors`. The solution builds with **0 warnings and 0 errors**. This is not cosmetic: it converts "possible null reference" from a suggestion into a build failure, and it caught a real null-handling gap during development.

### 6.2 Test suite — 95 tests

| Area | Class | Tests | Coverage |
|---|---|---|---|
| Rules engine | `FiniteGridEngineTests` | 18 | All four Conway rules **individually**; block, blinker, toad, glider translation; corner and edge boundary behaviour; empty-grid and extinction fixed points; buffer-length validation |
| Simulation | `SimulationServiceTests` | 13 | N-step correctness, zero/negative steps, short-circuit proof, baseline immutability, Stable/Oscillating/NotReached semantics, iteration-budget validation |
| Validation | `BoardStateValidatorTests` | 11 | Null, empty, empty row, null row, jagged, value 2, value −1, oversize rows/columns, at-exact-limit acceptance, row-major mapping |
| JSON contract | `FlexibleCellArrayConverterTests` | 9 | Integer, boolean and mixed matrices; five malformed-input shapes |
| Hashing | `GridHasherTests` | 5 | Determinism, sensitivity to cell changes, dimension-prefix collision avoidance, known-answer SHA-256 vector, length validation |
| Serialization | `CellSerializerTests` | 4 | Round-trip fidelity; corrupt-length and corrupt-character rejection |
| Configuration | `GameSettingsValidationTests` | 4 | Correct binding; out-of-range rejection for each setting |
| HTTP contract | `BoardsApiTests` | 23 | 201 + `Location`, numeric-cell invariant, boolean upload, 8 invalid payloads, oversize grid, `/next`, `/states`, step bounds, route-constraint behaviour, 404s |
| Final state | `FinalEndpointTests` | 5 | Stable, Oscillating with period, extinction, 422 budget exhaustion, 404 |
| Persistence | `PersistenceTests` | 2 | Survival across an application restart; read determinism and baseline integrity |
| Error contract | `ExceptionHandlingTests` | 1 | Unhandled exception → 500 ProblemDetails with no internal details leaked |

### 6.3 Noteworthy test design

**Restart persistence is tested honestly.** The test starts one host against a specific SQLite file, uploads a board, and disposes that host — a genuine shutdown. It then starts a **second** host pointed at the same file and verifies the board is still served correctly, including a computed generation. SQLite's `:memory:` mode was deliberately *not* used, because an in-memory database is private to a single connection and disappears when it closes — it would prove the opposite of the requirement. Using real files required handling Windows file locking in cleanup (clearing connection pools and removing WAL side files with retries).

**The optimisation is tested behaviourally, not just by output.** A still-life short-circuit does not change the answer — if it silently stopped working, every output assertion would still pass and the API would simply become slower. The suite therefore wraps the engine in a counting decorator, requests 10,000 generations of a still life, and asserts the engine was invoked **exactly once**. This is possible precisely because the engine is an interface.

**The boundary decision is pinned by tests that would fail under a different topology.** Two tests use patterns whose behaviour differs between a finite and a toroidal grid, so the architectural choice cannot be changed accidentally without a test failing.

### 6.4 Measured performance

Measured end-to-end over HTTP (Debug build, including JSON serialization), on a 500×500 board — the maximum permitted size — that stabilises at generation 4:

| Requested steps | 1 | 10 | 100 | 1,000 | 10,000 |
|---|---|---|---|---|---|
| Response time | 30 ms | 62 ms | 60 ms | 61 ms | 62 ms |

Response time is flat because the simulation detects the fixed point and stops. For comparison, a 100×100 board seeded with an R-pentomino — which remains chaotic, so no early exit is valid — scales linearly, exactly as the complexity analysis predicts:

| Requested steps | 1 | 10 | 100 | 1,000 |
|---|---|---|---|---|
| Response time | 17 ms | 19 ms | 48 ms | 350 ms |

Together these demonstrate that the optimisation applies when it is mathematically valid and never when it is not. Uploading a 500×500 board (a 489 KB payload of 250,000 cells) completes in 47 ms.

**Complexity:** one generation is `O(R × C)`. `GET /states/{n}` is `O(min(n, settle) × R × C)` time and `O(R × C)` memory — two reused buffers. `GET /final` adds one 64-character hash per visited generation, so `O(iterations)` additional memory, bounded at 1,000 by default.

---

## 7. Declared deviations and additions

Every point below is a conscious decision rather than an oversight, and each is documented in the code and the README.

| # | Item | Rationale |
|---|---|---|
| 1 | **`Dictionary<string,int>` instead of the suggested `HashSet<string>`** for visited states | A set answers *whether* a state was seen; a dictionary also answers *when*. The difference between the two sightings **is** the cycle period, so the brief's required `cyclePeriod` output falls out of a single subtraction. Same asymptotic cost, strictly more information. |
| 2 | **One additional endpoint: `GET /api/boards/{id}`** | The specification requires a `Location` header on `201`, and a `Location` must reference a retrievable resource. Without this endpoint the header would be a broken link. It also makes restart persistence directly observable. |
| 3 | **`EnsureCreated()` rather than EF migrations** | Explicitly permitted by the brief ("Auto-apply migrations or `EnsureCreated()`"). Chosen for zero-tooling setup against a single immutable table; noted as a production gap in the README. |
| 4 | **Non-integer `steps` (e.g. `/states/abc`) returns a bodiless `404`** | The `{steps:int}` route constraint does not match, so no endpoint exists at that URL — the same outcome as any unrouted path. Out-of-range *integers* (`0`, `-5`, `10001`) do match the route, reach the controller, and correctly return `400` with a descriptive message. A route constraint such as `{steps:min(1)}` was rejected precisely because a constraint failure yields `404` where the brief requires `400`. Both behaviours have tests. |
| 5 | **Boolean matrices accepted via a custom `JsonConverter`** | The brief specifies "a 2D integer array (`int[][]`, values `0` or `1`) **or boolean matrix**". Flexibility is confined to the boundary; booleans normalise to `0`/`1` immediately, so one representation flows through the system and all responses remain integer matrices. |
| 6 | **Simulation methods are synchronous and take no `CancellationToken`** | The brief requires cancellation support on async I/O, which is fully implemented end-to-end. The simulation is pure CPU work whose worst case is bounded *before* it starts by the three configured limits, so it cannot run away. Making bounded CPU work `async` would add overhead without benefit. Adding per-generation cancellation is noted in §8 as a hardening step. |

---

## 8. Known limitations and production roadmap

Stated plainly, because knowing where a system's edges are is part of delivering it.

| Limitation | Impact today | Remediation |
|---|---|---|
| SQLite is single-writer | None at this scale; reads are concurrent and writes are small and infrequent | Switch provider to PostgreSQL/SQL Server — contained within `GameOfLife.Infrastructure` (package + connection string) |
| `EnsureCreated()` cannot evolve a schema | None — one immutable table | Adopt EF migrations before the first schema change |
| Generation N is recomputed per request | Acceptable within configured limits (§6.4) | Results are deterministic, so ETag/response caching is the natural first optimisation |
| Simulation does not poll for cancellation | Bounded work only, so no runaway risk; a disconnected client's request still completes | Add `ThrowIfCancellationRequested()` per generation |
| `Board.Cells` is a `bool[]`, so immutability is behavioural rather than structural | Protected in practice — the service clones before simulating, with a test asserting the baseline is unchanged | Expose `ReadOnlyMemory<bool>` and keep the array private |
| Test runs leave an empty 12 KB `gameoflife.db` in the test output folder | None — it is created and never used; every test runs against its own temporary database, and the file is gitignored | Caused by `WebApplicationFactory` executing the entry point to discover the host before test overrides apply. Moving schema creation into an `IHostedService` would remove it; I kept the explicit startup block because it guarantees the schema exists before the first request is served |
| No authentication, authorisation, or rate limiting | Out of scope for the brief | Standard ASP.NET Core middleware when required |

Each of these was identified during an internal review pass of the finished solution rather than left to be discovered.

---

## 9. Suggested review path

For a reviewer working to a time budget, these are the files that best demonstrate the engineering.

**If you have 5 minutes**

1. `dotnet test` — 95 tests, approximately 1 second.
2. [`GameOfLife.Core/Simulation/SimulationService.cs`](GameOfLife.Core/Simulation/SimulationService.cs) — ~60 lines containing both core algorithms: double-buffered iteration with the still-life short-circuit, and hash-dictionary cycle detection.
3. [`GameOfLife.Api/Controllers/BoardsController.cs`](GameOfLife.Api/Controllers/BoardsController.cs) — the complete HTTP contract.

**If you have 15 minutes — add**

4. [`GameOfLife.Core/Engine/FiniteGridEngine.cs`](GameOfLife.Core/Engine/FiniteGridEngine.cs) — Conway's rules and the clamped-bounds implementation of the finite-grid decision.
5. [`GameOfLife.Tests/Unit/SimulationServiceTests.cs`](GameOfLife.Tests/Unit/SimulationServiceTests.cs) — in particular the counting-decorator test proving the short-circuit fires.
6. [`GameOfLife.Tests/Integration/PersistenceTests.cs`](GameOfLife.Tests/Integration/PersistenceTests.cs) — restart survival across two hosts sharing one database file.
7. `dotnet run --project GameOfLife.Api` and the two-minute verification in §1.

**If you have 30 minutes — add**

8. [`GameOfLife.Api/Program.cs`](GameOfLife.Api/Program.cs) — composition root, options validation, middleware ordering.
9. [`GameOfLife.Tests/Integration/GameOfLifeApiFactory.cs`](GameOfLife.Tests/Integration/GameOfLifeApiFactory.cs) — how the integration tests achieve real isolation against real SQLite files.
10. [`docs/PROJECT-DETAILS.md`](docs/PROJECT-DETAILS.md) — the full file-by-file rationale, including alternatives considered for every significant decision.

### Document map

| Document | Audience and purpose |
|---|---|
| [`README.md`](README.md) | Quick start, curl examples, configuration, condensed design and trade-offs |
| **`DOCUMENTATION.md`** (this file) | Reviewer-facing: requirement traceability, architecture, decisions, quality evidence, limitations |
| [`docs/PROJECT-DETAILS.md`](docs/PROJECT-DETAILS.md) | Complete technical reference — every folder, file and method, with the reasoning and rejected alternatives for each decision |
| [`docs/WALKTHROUGH-TRANSCRIPT.md`](docs/WALKTHROUGH-TRANSCRIPT.md) | Narrated walkthrough with real captured request/response transcripts, a guided code tour, and technology explainers |

---

**Summary.** All functional and non-functional requirements in the brief are implemented and covered by automated tests. The three classic ambiguities — boundary behaviour, final-state detection, and the meaning of generation N after stabilisation — are resolved explicitly, documented, and pinned by tests that would fail if the behaviour changed. Deviations from the letter of the brief are limited to the six items in §7, each of which is a deliberate engineering decision with a stated rationale, and the known limitations in §8 are documented rather than hidden.
