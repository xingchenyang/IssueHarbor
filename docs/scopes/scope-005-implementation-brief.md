# IssueHarbor V2 / 1.0.0 — Scope 005 Implementation Brief

**Scope:** 005 — Storage Foundation  
**Status:** APPROVED FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  

## 1. Functional goal

Implement the approved SQLite + immutable-artifact storage foundation without expanding into Analyzer execution, provider, backup/restore, deployment, or Web UI behavior.

## 2. Authoritative inputs

Use:

- the frozen V2 design plan;
- the Ideas Registry;
- Scope 003 Core Domain & Status Contract;
- Scope 004 Structured Result Contract;
- `docs/scopes/scope-005-storage-foundation-contract.md`.

Do not reopen frozen semantics unless a concrete implementation conflict is discovered.

## 3. Required technology

Use:

- `Microsoft.Data.Sqlite`;
- explicit SQL;
- versioned migration files;
- SHA-256 hashing;
- UUID v7 from .NET 10;
- deterministic tests.

Do not introduce EF Core, Dapper, generic repository abstractions, storage-provider abstractions, or database portability layers.

## 4. Required storage artifacts

Add a minimal concrete storage implementation supporting:

- SQLite connection creation with foreign keys enabled;
- storage initialization;
- migration discovery/loading;
- migration ledger verification;
- schema version verification;
- schema version 1 initialization;
- artifact publication;
- artifact hash/size calculation;
- DB catalog registration.

Use the smallest concrete code structure that cleanly supports these requirements.

## 5. Initial migration

Add:

```text
001_initial.sql
```

It must create only:

```text
schema_migrations
issues
requests
request_issues
request_items
snapshots
runs
```

plus only indexes/constraints required by the approved contract.

Do not create speculative tables.

## 6. Schema constraints

Enforce the approved Scope 003 status values and Scope 004 structured-result enum values at the database boundary where practical.

At minimum enforce:

- Request status values;
- Request stop-reason values and valid null/non-null relationship;
- Request Item status values;
- Request Issue uniqueness by membership and ordinal;
- foreign keys;
- Scope 004 result enum values stored on Runs.

Do not invent new reason taxonomies.

## 7. IDs

Use:

- Redmine `issue_id` -> SQLite `INTEGER`;
- IssueHarbor-owned Request/Item/Snapshot/Run IDs -> UUID v7.

Persist UUIDs as canonical lowercase text.

Do not wrap Redmine Issue IDs in UUIDs.

## 8. Time

Persist durable timestamps as UTC RFC 3339 / ISO-8601 text with `Z`.

Tests must not depend on local timezone.

## 9. Artifact publication

Implement immutable publication using a staging location on the same filesystem as final durable data.

Required order:

```text
generate
-> validate
-> hash/size
-> atomic rename
-> SQLite transaction
-> catalog registration
-> commit
```

If catalog registration fails after file publication:

- do not commit the DB row;
- leave the already-published artifact as an orphan;
- do not delete or overwrite unrelated durable data.

Do not implement orphan cleanup in this scope.

## 10. Artifact paths

Persist only paths relative to the configured durable-data root.

Reject or prevent host-specific absolute paths from entering artifact catalog fields.

Do not encode private machine paths in tests or docs.

## 11. Immutability

Attempting to publish an already-existing Snapshot or Run artifact identity must fail.

Do not overwrite existing immutable artifact content.

## 12. Migration behavior

Normal application startup must verify storage compatibility but must not auto-migrate an existing durable database.

Fresh initialization may apply all migrations from schema version 0 to the current version.

The implementation must detect migration checksum drift.

Do not implement backup-before-upgrade yet; that belongs to the later lifecycle/backup scope.

## 13. Required deterministic tests

At minimum add tests for:

### Migration

1. fresh initialization reaches storage schema version 1;
2. repeat initialization is deterministic/idempotent;
3. migration checksum mismatch is rejected;
4. invalid/missing migration history is rejected where representable.

### Database constraints

5. foreign-key violations fail;
6. invalid Request status fails;
7. invalid Request stop-reason combination fails;
8. duplicate Request Issue membership fails;
9. duplicate Request Issue ordinal fails;
10. invalid Request Item status fails;
11. invalid stored structured-result enum value fails.

### Artifacts

12. Snapshot artifact publication records expected relative path/hash/size;
13. Run result/report publication records expected relative paths/hashes/sizes;
14. duplicate immutable publication fails;
15. persisted artifact path is relative;
16. forced DB registration failure after artifact publication commits no catalog row.

Tests must use temporary disposable storage and synthetic fixtures only.

No AI/provider/network access is allowed.

## 14. Documentation updates

Archive:

```text
docs/scopes/scope-005-storage-foundation-contract.md
docs/scopes/scope-005-implementation-brief.md
```

Update the design plan and Ideas Registry only as necessary to record that the relevant storage/migration foundation decisions are now frozen.

Do not rewrite unrelated baseline sections.

The Scope 005 review decision will be added after Sol review and independent review reconciliation.

## 15. Explicitly out of scope

Do not implement:

- provider/profile persistence beyond minimal reference fields already approved;
- Analyzer execution;
- Snapshot acquisition;
- Redmine access;
- source repository mapping;
- attachment retention;
- retry/resume;
- crash restart recovery;
- backup/restore;
- backup retention;
- orphan cleanup;
- Web UI;
- CLI command surface;
- Docker/deployment tuning;
- WAL tuning;
- database encryption;
- generic database abstractions.

## 16. Validation

Run:

```text
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Also inspect:

```text
git status
git diff
git diff --check
```

Confirm:

- all storage tests pass;
- no AI/provider/network access is required;
- no private/personal content is present;
- no absolute host path is persisted in test database/catalog output;
- only approved files changed.

## 17. Git boundary

The Implementation Agent may inspect, stage, and create the approved local commit.

The Implementation Agent must never:

- run `git push`;
- write to a Git remote;
- create/push tags;
- create releases;
- modify remote settings.

Remote push remains a manual Product Owner action.

## 18. Implementation report

Report:

1. files changed;
2. storage code structure;
3. migration file(s);
4. package versions added;
5. schema constraints implemented;
6. artifact publication behavior;
7. exact test matrix and results;
8. restore/build/test evidence;
9. public-safety checks;
10. local commit hash;
11. deviations, blockers, or follow-up ideas.

Do not push.

## 19. Review sequence

After implementation:

1. GPT-5.6 Sol performs architecture/scope review.
2. If Sol review finds implementation-level blockers, revise before independent review.
3. DeepSeek-V41-Flash performs independent storage/migration review against the approved contract and implementation evidence.
4. GPT-5.6 Sol reconciles findings.
5. Product Owner receives the final decision material.

The Independent Reviewer may identify concrete risks or conflicts but must not redefine product scope.

## 20. Completion rule

Scope 005 is ready for first review only when:

- schema version 1 is implemented;
- explicit SQL migrations work deterministically;
- migration drift is detected;
- foreign keys/domain constraints are enforced;
- artifact publication is immutable and DB-last;
- artifact paths are relative;
- required deterministic tests pass;
- no out-of-scope product functionality was introduced;
- no push was performed.
