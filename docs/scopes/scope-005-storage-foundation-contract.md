# IssueHarbor V2 / 1.0.0 — Scope 005 Storage Foundation Contract

**Scope:** 005 — Storage Foundation  
**Status:** APPROVED / READY FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  

## 1. Purpose

Freeze the minimum durable storage foundation for IssueHarbor V2 / 1.0.0.

The storage model is intentionally SQLite-specific and combines:

- SQLite as the authority for durable state, catalog, relations, and filterable fields;
- immutable artifact files as the authority for larger immutable content such as Snapshot JSON, structured result JSON, and Markdown reports.

This scope establishes storage, migration, artifact-publication, and validation rules only. It does not implement Analyzer execution workflows, provider behavior, backup/restore, or Web UI features.

## 2. Storage technology baseline

Use:

- `Microsoft.Data.Sqlite`
- explicit SQL
- linear versioned migrations
- deterministic migration verification
- SQLite foreign-key enforcement

Do not introduce EF Core, Dapper, a generic repository framework, a generic storage-provider abstraction, or a database-portability layer.

IssueHarbor 1.0 is intentionally SQLite-specific.

## 3. SQLite connection rules

Every application-controlled SQLite connection must enforce foreign keys.

Use either the equivalent of:

```text
Foreign Keys=True
```

or:

```sql
PRAGMA foreign_keys = ON;
```

Do not assume foreign-key enforcement is enabled by default.

Scope 005 does not enable WAL mode. Use the default SQLite journal behavior unless a later real workload demonstrates a concrete need to change it.

## 4. Identifier rules

### Redmine Issue identity

Redmine `issue_id` is source-owned identity and is stored directly as SQLite `INTEGER`.

Do not wrap or replace it with an IssueHarbor-generated UUID.

Observed real-world Redmine IDs are currently in the low tens of thousands, but storage must not encode assumptions about a small numeric ceiling. SQLite `INTEGER` provides ample range.

### IssueHarbor durable identities

Use UUID version 7 for IssueHarbor-owned durable identities:

- `request_id`
- `request_item_id`
- `snapshot_id`
- `run_id`

Use .NET 10 native UUID v7 generation.

Persist canonical lowercase textual UUID values.

## 5. Time representation

Durable timestamps use UTC.

Persist timestamps as deterministic RFC 3339 / ISO-8601 text values ending in `Z`.

Do not persist local-time-dependent timestamps.

Presentation-layer timezone conversion is outside this scope.

## 6. Storage schema version

Initial storage schema version:

```text
1
```

Storage schema versioning is independent from:

- IssueHarbor product version;
- structured result schema version;
- MCP contract version.

## 7. Migration model

Migrations are linear, ordered, deterministic, and immutable once applied.

Use file names such as:

```text
001_initial.sql
002_example_future_change.sql
```

The database migration ledger must record, at minimum:

- migration version;
- migration name;
- migration SHA-256;
- applied UTC timestamp.

Historical migration files must not be silently edited after application.

Startup or verification must detect:

- missing migration versions;
- duplicate versions;
- checksum mismatch between applied migration and current migration file;
- database schema version newer than supported software.

## 8. Migration execution boundary

Normal Analyzer startup must not silently migrate an existing durable database.

Normal startup:

```text
read storage version
-> verify supported
-> start or fail clearly
```

Migration belongs to an explicit lifecycle/upgrade path:

```text
verify current storage
-> verified backup
-> apply deterministic migrations
-> validate resulting storage
-> start target release
```

Fresh empty storage may be explicitly initialized to the current schema version.

Exact lifecycle command names are deferred.

## 9. Migration atomicity

Each migration must execute in one SQLite transaction where practical.

If a future migration requires SQLite table-rebuild semantics, that migration must implement the rebuild explicitly and deterministically.

The system must never continue from a known half-applied migration state.

## 10. Initial schema

The initial storage schema contains only tables required by already-frozen domain semantics:

```text
schema_migrations
issues
requests
request_issues
request_items
snapshots
runs
```

Do not create generic tables such as `entities`, `objects`, `properties`, or `artifacts` merely for abstraction.

## 11. `schema_migrations`

Minimum columns:

```text
version
name
sha256
applied_at_utc
```

Requirements:

- `version` is a monotonically increasing integer primary key;
- migration versions are contiguous from the initial version;
- `sha256` records the applied migration content;
- an applied migration checksum mismatch is a storage verification failure.

## 12. `issues`

Minimum durable fields:

```text
issue_id
first_seen_at_utc
```

Requirements:

- `issue_id` is the Redmine integer identity and primary key;
- mutable Redmine facts such as subject, status, tracker, assignee, and description do not belong in this identity table;
- mutable analysis input belongs in Snapshots.

## 13. `requests`

Minimum durable fields:

```text
request_id
status
stop_reason
created_at_utc
finished_at_utc
```

Allowed `status` values:

```text
running
completed
stopped
```

Allowed non-null `stop_reason` values:

```text
operator_stopped
execution_deadline
provider_authentication_required
provider_quota_exhausted
```

A `completed` or `running` Request must not carry a request-wide stop reason.

Detailed selection, Analysis Contract, and profile configuration fields are deferred to the scopes that freeze those contracts.

## 14. `request_issues`

Represents frozen ordered Issue membership for a Request.

Minimum fields:

```text
request_id
ordinal
issue_id
```

Storage-level invariants:

- `(request_id, issue_id)` is unique;
- `(request_id, ordinal)` is unique;
- `request_id` references `requests`;
- `issue_id` references `issues`.

The table preserves both membership and order.

## 15. `request_items`

Minimum fields:

```text
request_item_id
request_id
issue_id
profile_key
profile_ordinal
status
reason_code
snapshot_id
```

Allowed `status` values:

```text
planned
running
completed
failed
blocked
not_started
```

`profile_key` is a stable reference to profile configuration. Scope 005 does not define profile contents.

`reason_code` is nullable machine-readable storage for later-approved reason taxonomy. Scope 005 does not invent additional reason enums beyond already-frozen Request-wide stop reasons.

`snapshot_id` is nullable because planned/not-started/blocked work may not have a Snapshot.

## 16. `snapshots`

Minimum fields:

```text
snapshot_id
issue_id
created_at_utc
artifact_relative_path
artifact_sha256
artifact_size_bytes
```

Requirements:

- Snapshot content itself is not stored as a SQLite text/blob field;
- the SQLite row catalogs the immutable artifact;
- paths are repository-independent relative durable-data paths;
- artifact SHA-256 and size are required after successful publication.

Source repository revision, attachment details, journal provenance, and richer Snapshot metadata remain deferred.

## 17. `runs`

Minimum fields:

```text
run_id
request_item_id
snapshot_id
started_at_utc
finished_at_utc

result_schema_version
change_type
implementation_complexity
requires_code_change
confidence
complexity_rationale

result_relative_path
result_sha256
result_size_bytes

report_relative_path
report_sha256
report_size_bytes
```

Requirements:

- `request_item_id` references the durable Item;
- `snapshot_id` references the actual Snapshot used;
- structured result fields mirror the frozen Scope 004 filterable result contract;
- `result.json` remains artifact authority for the complete structured result;
- `report.md` remains artifact authority for the human-readable report;
- provider/model provenance, usage, tokens, and cost are deferred to the provider/execution scope.

## 18. Artifact filesystem layout

Conceptual durable layout:

```text
/data/
├── analyzer.db
├── snapshots/
│   └── <snapshot-id>/
│       └── snapshot.json
└── runs/
    └── <run-id>/
        ├── result.json
        └── report.md
```

Future retained attachment material may live under the corresponding Snapshot directory when that feature is implemented.

Do not pre-create empty directories that have no current artifact.

## 19. Relative paths only

SQLite stores artifact paths relative to the durable data root.

Valid conceptual example:

```text
snapshots/<snapshot-id>/snapshot.json
```

Do not store host-specific absolute paths such as:

```text
C:\...
/home/...
/share/...
/data/...
```

The durable data root is deployment configuration, not stored artifact identity.

## 20. Immutable artifact publication

Artifact publication follows this order:

```text
1. generate content in staging
2. validate content
3. compute byte size
4. compute SHA-256
5. publish immutable artifact by same-filesystem atomic rename
6. begin SQLite transaction
7. register artifact/catalog metadata
8. commit SQLite transaction
```

The database must never commit a reference to an artifact that has not been successfully published.

## 21. Crash residue semantics

The preferred failure mode is:

```text
artifact exists
database row absent
```

This is an orphan artifact and may be detected by later doctor/cleanup/restore tooling.

The forbidden committed state is:

```text
database row exists
artifact was never successfully published
```

Scope 005 does not implement orphan cleanup.

## 22. Artifact immutability

Once a Snapshot or Run artifact identity has been successfully published:

- do not overwrite it;
- do not merge into it;
- do not replace it in place.

Publishing an already-existing artifact identity must fail.

A changed analysis result requires a new Run and new immutable artifacts.

## 23. SQLite content boundary

Do not store the full immutable artifact bodies as SQLite fields such as:

```text
snapshot_json
result_json
report_markdown
```

SQLite stores:

- state;
- relations;
- filterable structured fields;
- relative artifact paths;
- artifact hashes;
- artifact sizes;
- timestamps;
- schema/migration metadata.

Artifact files remain the immutable content authority.

## 24. Concrete implementation components

Small concrete components are allowed when directly justified, for example:

- storage connection factory;
- migration loader/verifier;
- storage initializer;
- artifact publisher;
- storage verifier used by tests.

Do not introduce:

- `IRepository<T>`;
- generic repository implementations;
- generic Unit of Work abstractions;
- generic storage providers;
- SQL dialect abstractions;
- generic artifact frameworks.

## 25. Minimum deterministic validation matrix

The implementation must prove at least:

### Initialization and migration

- fresh storage initializes to schema version 1;
- initializing already-current storage is deterministic and does not duplicate migration application;
- migration checksum mismatch is rejected;
- missing/non-contiguous migration history is rejected where applicable.

### Relational integrity

- foreign-key violation is rejected;
- invalid Request status is rejected;
- invalid Request stop-reason combination is rejected;
- duplicate Request Issue membership is rejected;
- duplicate Request Issue ordinal is rejected;
- invalid Request Item status is rejected.

### Artifact publication

- Snapshot artifact publication produces expected relative path, SHA-256, and byte size;
- Run result/report publication produces expected relative paths, hashes, and sizes;
- publishing the same immutable artifact identity again is rejected;
- database paths remain relative;
- DB registration failure after file publication does not commit the DB row.

### Scope 004 integration

- stored structured-result fields accept valid schema-v1 values;
- invalid frozen enum values are rejected at the storage boundary.

## 26. Explicitly deferred

Scope 005 does not implement or decide:

- Analyzer Request execution engine;
- provider/profile schema contents;
- provider/model provenance;
- AI credentials or authentication;
- Snapshot acquisition;
- RedmineMcp tool behavior;
- source repository mapping;
- attachment retention implementation;
- retry/resume/crash recovery workflows;
- backup implementation;
- restore implementation;
- backup retention/pruning;
- orphan cleanup;
- Web UI;
- CLI command surface;
- Docker/QNAP tuning;
- WAL tuning;
- database encryption;
- generic multi-database support.

## 27. Security and privacy

Storage code and fixtures must remain public-safe.

Do not commit:

- secrets;
- credentials;
- production paths;
- private hostnames/IPs;
- real Redmine content;
- private repository content.

Storage fixtures and tests use synthetic values only.

## 28. AI/provider cost

Scope 005 uses no runtime AI/provider calls.

Hashing, migration, schema validation, integrity checking, and artifact verification are deterministic work.

## 29. Acceptance rule

The implemented storage foundation must deterministically answer:

- What storage schema version is installed?
- Are applied migrations exactly the expected immutable migrations?
- Are foreign-key and domain constraints enforced?
- Is an artifact fully published before catalog registration?
- Does the catalog store only relative artifact paths?
- Do stored size/hash values match published content?
- Can an existing immutable artifact be overwritten? (No.)
- Can normal startup silently migrate an existing database? (No.)
- Can the system detect a migration checksum mismatch? (Yes.)

Any future change to these durability semantics requires an explicit approved scope decision.
