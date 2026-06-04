# ADR-001: Use SQLite instead of PostgreSQL for local config storage

**Date:** 2026-06-04
**Status:** Accepted

## Context

Dashy is a single-user personal tool running locally (Docker Compose). It stores configuration data only: log sources, tags, saved searches, and alert definitions. The total row count at steady state is expected to be in the tens to low hundreds. The original design specified PostgreSQL in a Docker container.

## Decision

We will use SQLite (via EF Core) as the database for Dashy v1. The Docker Compose stack drops the `postgres` container entirely; the database is a single `.db` file on the host filesystem, mounted into the `.NET` container.

## Alternatives considered

### PostgreSQL
Dropped. Provides no capability advantage for this workload — JSON operators, full-text search, and write concurrency are all irrelevant at this scale and user count. Requires a running Docker container, TCP port mapping, and connection pool management. Backup requires `pg_dump`; SQLite backup is a file copy. The only legitimate future-proofing argument is multi-user concurrency (SQLite allows one writer at a time), but that is a v3 concern at the earliest.

### In-memory / no persistence
Rejected. Configuration must survive process restarts.

## Consequences

### Positive
- Docker Compose drops to two services (`dashy-api`, `dashy-web`) instead of three
- Zero connection management overhead; EF Core opens the file directly
- Full backup is a single `cp dashy.db dashy.db.bak`; trivial to restore
- EF Core migrations work identically — switching to PostgreSQL later requires only a provider swap and a `dotnet ef migrations add` pass

### Negative / accepted tradeoffs
- SQLite allows only one write connection at a time; concurrent writes would queue. Acceptable for a single-user tool; becomes a bottleneck if multi-user is ever added
- No native `JSONB` indexing; JSON columns are stored as text and deserialized in application code. Acceptable because we never query inside the JSON blobs — they are always fetched whole

## Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| SQLite file corruption on unclean shutdown | Low | EF Core uses WAL mode by default; corruption risk is minimal for low-write workloads |
| EF Core migration incompatibility between SQLite and PostgreSQL if we ever migrate | Medium | Keep migrations clean (no PostgreSQL-specific features); document the provider-swap procedure |
| Accidental deletion of the `.db` file | Low | Mount to a named Docker volume, not a blind bind mount; document backup in README |

## Open questions
- None.
