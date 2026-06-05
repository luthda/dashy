# ADR-004: Pragmatic layered architecture with provider seam as the extension point

**Date:** 2026-06-04
**Status:** Accepted (amended 2026-06-05)

## Amendment (2026-06-05) — explicit onion folder layout

PR review (#9) asked that the project structure visibly reflect the onion/clean
architecture layers rather than the original mixed folder names. In response, the four
conceptual layers below were promoted to **explicit top-level folders and namespaces**
inside the single `Dashy.Api` project:

| Layer | Folder / namespace |
|---|---|
| Domain | `Domain/Entities`, `Domain/Models` (`Dashy.Api.Domain.*`) |
| Application | `Application/Services`, `Application/Abstractions`, `Application/Exceptions` (`Dashy.Api.Application.*`) |
| Infrastructure | `Infrastructure/Persistence` (+ `Configurations`, `Migrations`), `Infrastructure/Encryption`, `Infrastructure/LogSources` (`Dashy.Api.Infrastructure.*`) |
| Presentation | `Controllers` (`Dashy.Api.Controllers`) — minimal-API endpoint groups |

The interfaces Infrastructure implements (`ILogSourceAdapter`, `ILogSourceAdapterFactory`,
`IEncryptionService`) live in `Application/Abstractions`. `Options/` remains a top-level
cross-cutting folder for configuration POCOs (it is not a layer).

**This amendment does NOT change the core decisions below:** still a single project, still
**no repository/unit-of-work abstraction** — Application services continue to use EF Core's
`DashyDbContext` directly. The reorganisation is folder/namespace-only; the dependency rules
are unchanged.

Note for future migrations: the EF migrations now live under
`Infrastructure/Persistence/Migrations`, so generate new ones with
`dotnet ef migrations add <Name> -o Infrastructure/Persistence/Migrations`.

## Context

Dashy is a single-developer log dashboard (replacing Azure Portal / Grafana) built on
.NET 10 + EF Core + SQLite. The backend currently lives in one project, `Dashy.Api`,
organised into folders: `Endpoints/`, `Services/`, `Infrastructure/`, `Data/`,
`Models/`, `Options/`. Services depend directly on the EF Core `DashyDbContext` and on
concrete adapter classes for log sources.

The proposal that triggered this ADR was to "adopt Clean Architecture" — a four-project
split (Domain / Application / Infrastructure / Api) with dependency inversion so the
domain is insulated from infrastructure.

Stress-testing that proposal surfaced a contradiction. The stated driver was
**swappable infrastructure**, but ADR-001 already locks in SQLite + EF Core as a
deliberate, long-term choice with no plan to swap the database. Building a repository /
persistence-abstraction layer to insulate the domain from EF Core therefore buys
optionality the project has explicitly decided never to exercise — pure ceremony for a
solo maintainer.

The infrastructure that *is* genuinely swappable and growing is the **log source
providers**: Azure App Insights and Grafana Loki today, plausibly CloudWatch / Datadog /
GCP later. That seam already exists and is correct — `ILogSourceAdapter` +
`ILogSourceAdapterFactory` (see ADR-002). The decision needed is how much additional
structure to impose, given a solo developer, a locked database, and one already-correct
abstraction.

## Decision

We will adopt a **pragmatic layered architecture inside the single `Dashy.Api`
project**, enforced by folder conventions and dependency direction rather than a
multi-project split:

- **Domain** (`Domain/Entities`, `Domain/Models`): entities and value types. No dependency
  on EF Core, ASP.NET, or HTTP clients.
- **Application** (`Application/Services`, `Application/Abstractions`): use-case
  orchestration (`SourceService`, `LogQueryService`). Depends on Domain and on
  **abstractions** for outbound infrastructure (`ILogSourceAdapter`, `IEncryptionService`).
- **Infrastructure** (`Infrastructure/Persistence`, `Infrastructure/Encryption`,
  `Infrastructure/LogSources`): EF Core `DashyDbContext`, the App Insights adapter,
  encryption. Implements the abstractions the Application layer depends on.
- **Presentation** (`Controllers`, `Program.cs`): minimal-API endpoint groups, DI wiring,
  JSON config.

> The folder names above reflect the 2026-06-05 amendment. See the amendment note at the
> top of this ADR for the original-to-onion mapping.

The **log-source provider abstraction (`ILogSourceAdapter`) is the primary architectural
extension point** — adding a new source means adding one adapter and registering it, with
no changes to the Application or Api layers.

**Data access stays directly on EF Core `DbContext`** in the Application services. We will
**not** introduce a repository/unit-of-work abstraction over EF Core. This is consistent
with ADR-001 (database locked in): there is no DB-swap scenario to insulate against, and
`DbContext` is already a unit-of-work + repository.

The boundary rule we enforce: **Endpoints never touch `DbContext` directly** — they go
through Application services. Application services depend on infrastructure only via
interfaces.

## Alternatives considered

### Full four-project Clean Architecture (Domain / Application / Infrastructure / Api)
Rejected. Compile-time project boundaries are how Clean Architecture *enforces* that the
domain has no infrastructure dependencies — valuable when many contributors might
otherwise reach across layers. For a single developer with ~5 entities and a handful of
services, the four `.csproj` files, the cross-project interface plumbing, and the mapping
boilerplate add friction (more build targets, more `using`s, more indirection) without a
proportional payoff. It can be promoted to multi-project later if the team grows — the
folder layout chosen here maps 1:1 onto projects, making that migration mechanical.

### Repository / unit-of-work abstraction over EF Core
Rejected. Its only real benefit here would be DB-swap insulation, which ADR-001 rules
out, and "mockable data access" for unit tests. In practice a repository over EF Core
tends to become a leaky, anemic pass-through that fights `IQueryable` composition and
change tracking, and the project already tests against **in-memory SQLite via
`WebApplicationFactory`** (a real database, higher-fidelity than a mocked repository). The
boilerplate cost outweighs the testability gain.

### Status quo (leave it as undocumented folders)
Rejected. The current structure is *already* close to the target, but the layering rules
(who may depend on what, who may touch `DbContext`) are implicit. Without a recorded
boundary contract, the layers erode — e.g. an endpoint querying `DbContext` directly for a
"quick fix." This ADR exists to make the contract explicit and cheap to enforce.

### Strict Clean Architecture anyway (abstract the DB regardless)
Rejected explicitly. Recording a decision to insulate from EF Core while ADR-001 locks the
database in would be internally inconsistent and would institutionalise unused
optionality.

## Consequences

### Positive
- The genuinely volatile part of the system (log sources) has a clean, documented
  extension seam; adding a provider is a localised change.
- The folder layout maps directly onto a future multi-project split, so the "promote to
  full Clean Architecture" path stays open and mechanical if the team grows.
- No repository boilerplate; services use EF Core's full query surface and the test suite
  keeps using real (in-memory) SQLite.
- The "endpoints never touch `DbContext`" rule is simple enough to enforce in review and
  eventually with an analyzer/architecture test.

### Negative / accepted tradeoffs
- Layer boundaries are enforced by convention, not the compiler. A determined shortcut
  (endpoint → `DbContext`) compiles today; we accept this and rely on review + an optional
  architecture test.
- Application services remain coupled to EF Core. If ADR-001 is ever reversed, the
  data-access code in services must change. We accept this because reversing ADR-001 is
  considered out of scope.
- "Clean Architecture" in the strict, four-project sense is *not* what we built; anyone
  expecting that label literally will be surprised. This ADR is the disambiguation.

## Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Layer boundaries erode over time (e.g. an endpoint calls `DbContext` directly) | Medium | Document the rule here; add a NetArchTest/`ArchUnitNET` architecture test asserting `Endpoints` has no `DashyDbContext` reference |
| Coupling Application services to EF Core becomes painful if ADR-001 is reversed | Low | ADR-001 is a deliberate lock-in; revisit this ADR only if that one is superseded |
| The "pragmatic" middle ground is read as an excuse to skip structure entirely | Low | The boundary rules and folder→layer mapping are explicit above; review against them |
| Team grows and convention-based boundaries no longer scale | Low | Folder layout was chosen to map 1:1 onto a four-project split; promote at that point |

## Open questions
- Should we add an automated architecture test (NetArchTest / ArchUnitNET) now, or defer
  until a boundary is actually violated? Leaning defer for a solo project, but it is the
  natural enforcement mechanism if discipline slips.
