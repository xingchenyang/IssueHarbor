# IssueHarbor V2 / 1.0.0 — Scope 005 Independent Review Brief

**Scope:** 005 — Storage Foundation  
**Review stage:** Independent storage/migration audit  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  
**Sol first-review result:** PASS TO INDEPENDENT REVIEW  

## 1. Review purpose

Independently audit the implemented Scope 005 storage foundation against the approved Storage Foundation Contract and Implementation Brief.

Do not redesign the product or expand scope. Report concrete correctness, durability, migration, integrity, or contract-compliance issues only.

## 2. Implementation under review

Implementation commit:

`4d65c14d3a2154e76562ab823dbff307dfad0258`

Commit message:

`feat: implement storage foundation`

Reported validation:

- `dotnet restore IssueHarbor.slnx` -> PASS
- `dotnet build IssueHarbor.slnx --no-restore` -> PASS, 0 warnings / 0 errors
- `dotnet test IssueHarbor.slnx --no-build` -> PASS, 42 / 42
- 25 storage tests added
- synthetic/public-safe test data only
- no push performed

## 3. Approved storage contract to audit

The implementation must preserve:

- SQLite as catalog/state/filter authority;
- immutable files as content authority;
- `Microsoft.Data.Sqlite` + explicit SQL;
- linear immutable migrations with checksum verification;
- foreign keys enabled;
- Redmine `issue_id` stored directly as SQLite `INTEGER`;
- IssueHarbor-owned durable IDs generated as UUID v7;
- UTC timestamp persistence;
- storage schema version 1;
- no silent migration on normal existing-storage startup;
- relative artifact paths only;
- SHA-256 + byte size catalog metadata;
- immutable artifact publication;
- artifact publication before SQLite catalog registration;
- DB transaction rollback if registration fails;
- orphan file allowed after DB registration failure;
- no overwrite of existing immutable artifact identity;
- no generic repository/storage abstraction.

## 4. High-priority audit questions

### A. Migration integrity

Check that:

1. migration versions are discovered deterministically;
2. version ordering is numeric rather than unsafe lexical ordering;
3. versions are contiguous as required;
4. duplicate versions are rejected;
5. applied migration name/hash/version are verified;
6. an edited historical migration is detected;
7. initialization cannot silently re-apply a migration;
8. an unsupported/newer DB schema is rejected clearly;
9. migration application and ledger registration are transactionally consistent;
10. SQL execution cannot leave a known half-applied migration accepted as current.

### B. SQLite integrity

Check that:

1. every application-controlled connection enables foreign keys;
2. Request status constraints exactly match Scope 003;
3. Request stop-reason constraints reject invalid status/reason combinations;
4. Request Item status constraints exactly match Scope 003;
5. Scope 004 stored result enums are constrained correctly;
6. request membership and ordinal uniqueness constraints are correct;
7. foreign-key relationships match the approved domain;
8. no speculative tables/columns or generic abstractions were introduced.

### C. Artifact publication

Check that:

1. staging and final destination are guaranteed to be on the same filesystem;
2. publication uses rename/move semantics that are atomic for the supported local filesystem assumption;
3. final artifact identity cannot be overwritten;
4. content is validated before publication;
5. hash and byte size are computed for the exact published bytes;
6. SQLite registration happens only after successful publication;
7. forced registration failure leaves no committed catalog row;
8. failure handling does not delete or corrupt unrelated durable artifacts;
9. relative paths cannot escape the configured durable root using `..`, rooted paths, drive-relative paths, or separator tricks;
10. Snapshot and Run artifact layouts remain deterministic.

### D. UUID/time correctness

Check that:

1. IssueHarbor durable IDs use .NET 10 UUID v7 generation;
2. persisted UUID text is canonical lowercase;
3. Redmine `issue_id` remains integer source identity;
4. timestamps are persisted in deterministic UTC `Z` form;
5. tests do not rely on local timezone.

### E. Tests

Check whether the 25 storage tests genuinely exercise the approved failure paths instead of merely testing helper methods.

Pay particular attention to:

- migration checksum mismatch;
- invalid/missing migration history;
- foreign-key failure;
- invalid Request state/reason;
- duplicate Request Issue membership;
- duplicate ordinal;
- invalid Item status;
- invalid Scope 004 stored enum;
- duplicate immutable publication;
- DB registration failure after artifact publication;
- relative-path enforcement.

Identify important approved contract behavior that is not actually tested.

## 5. Explicitly out of scope for the reviewer

Do not require implementation of:

- failed-Run error evidence shape;
- provider/model provenance;
- provider/profile schema;
- Snapshot acquisition;
- RedmineMcp behavior;
- source repository mapping;
- backup/restore;
- orphan cleanup;
- retry/resume;
- crash restart recovery;
- Web UI;
- CLI;
- WAL mode;
- database encryption;
- multi-database portability.

The reported need to define failed-Run error evidence later is not a Scope 005 defect by itself.

## 6. Review output format

Return:

### Decision

One of:

- `PASS`
- `PASS WITH NON-BLOCKING FINDINGS`
- `NEEDS REVISION`
- `BLOCKED`

### Blocking findings

For each blocking finding include:

- exact file/component;
- observed behavior;
- violated approved contract rule;
- why it matters;
- smallest compliant correction;
- test that should prove the correction.

### Non-blocking findings

List only concrete maintainability, durability, or clarity issues worth recording.

Do not suggest speculative architecture improvements.

### Evidence reviewed

List relevant implementation files, migrations, and tests actually inspected.

### Scope check

State explicitly whether the implementation introduced any out-of-scope behavior.

## 7. Independence rule

Review the approved contract and implementation evidence independently.

Do not change the product scope, add future-proofing abstractions, or substitute a preferred storage architecture unless the current implementation violates an approved requirement or creates a concrete correctness/durability risk.
