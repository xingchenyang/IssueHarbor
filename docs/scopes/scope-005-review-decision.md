# IssueHarbor V2 / 1.0.0 — Scope 005 Final Review Decision

**Scope:** 005 — Storage Foundation  
**Status:** PASS / CLOSED  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  

## 1. Final decision

**PASS / CLOSED**

Scope 005 is accepted as the frozen storage-foundation baseline for later IssueHarbor scopes.

The original implementation passed independent review with non-blocking findings. The bounded hardening revision required by the Architecture / Scope Lead has now been implemented and validated without changing approved schema, migration, domain, or artifact semantics.

No second full independent audit is required.

## 2. Accepted implementation commits

Primary storage implementation:

`4d65c14d3a2154e76562ab823dbff307dfad0258`

Commit message:

`feat: implement storage foundation`

Bounded hardening revision:

`36349599630a0319ebd73eefb815526d59eceb59`

Commit message:

`fix: harden Scope 005 artifact storage`

## 3. Independent review

Independent reviewer:

`DeepSeek-V41-Flash`

Independent review result:

`PASS WITH NON-BLOCKING FINDINGS`

The independent review verified the core storage and migration behavior, including:

- deterministic migration discovery and checksum verification;
- transactional migration application and ledger update;
- no silent migration of an existing durable database;
- foreign-key enforcement and foreign-key verification;
- Scope 003 lifecycle constraints;
- Scope 004 structured-result constraints;
- DB-last immutable artifact publication;
- relative-path catalog storage;
- absence of speculative storage abstractions or tables.

No blocking defect was found.

## 4. Reconciliation requirements and resolution

### R1 — durable-root containment guard

Resolved.

The artifact path implementation now:

- rejects rooted paths;
- rejects leading slash/backslash paths;
- rejects drive-prefixed and drive-relative forms;
- rejects `..` traversal segments across both separator styles;
- normalizes path separators;
- resolves with `Path.GetFullPath`;
- verifies the final path remains beneath the durable-data root using a directory-boundary-safe comparison;
- rejects resolution to the durable root itself.

This is accepted as enforcement of the already-frozen relative-path contract.

### R2 — verify actual published bytes

Resolved.

Artifact tests now reopen the final published files and independently recompute:

- byte length;
- SHA-256.

Those values are compared against:

- publication metadata;
- SQLite catalog metadata.

Snapshot and Run artifact publication are covered.

### R3 — direct C# path-guard coverage

Resolved.

The test suite now directly exercises the C# path guard with synthetic rooted and escaping paths rather than relying only on database CHECK constraints.

### R4 — disposable-test cleanup

Resolved.

Storage-test cleanup now clears SQLite pools and uses bounded retry behavior for temporary-directory deletion.

Reported full-suite and storage-focused runs left zero new storage-test directories.

## 5. Migration immutability

`001_initial.sql` was not modified by the hardening revision.

Reported Git blob identity remains:

`91963ddda1b4d31991205352af5af42fcfba8bde`

No migration `002` was added.

This preserves the approved migration-immutability and checksum model.

The independent-review observation that two composite UNIQUE constraints may appear redundant remains documented. They are required as parent keys for composite foreign keys and must not be removed casually.

## 6. Validation evidence

Reported bounded-revision validation:

- restore completed;
- build completed with 0 errors;
- full test suite: **43 / 43 passed**;
- storage-focused suite: **26 / 26 passed**;
- no new temporary storage-test roots remained after the verified runs;
- no schema, migration, contract, or brief semantics changed;
- worktree clean;
- no push performed.

### NU1900 warnings

The build reported three `NU1900` warnings because NuGet vulnerability metadata was unreachable in the execution environment.

These warnings are accepted as non-blocking for Scope 005 because the reported condition is external metadata availability rather than a code, schema, migration, or test failure.

They do not waive future dependency/security verification when network access is available.

## 7. Frozen Scope 005 outcome

The accepted storage baseline is:

- `Microsoft.Data.Sqlite`;
- explicit SQL;
- storage schema version 1;
- seven approved initial tables;
- Redmine `issue_id` stored as SQLite `INTEGER`;
- IssueHarbor durable IDs as UUID v7;
- UTC durable timestamps;
- foreign keys enabled;
- deterministic immutable migrations with checksum verification;
- no silent migration of existing durable storage at normal startup;
- relative artifact paths only;
- SHA-256 and byte-size artifact metadata;
- immutable Snapshot and Run artifacts;
- same-filesystem staging/publication;
- artifact publication before database registration;
- SQLite transaction rollback on registration failure;
- orphan artifact permitted after post-publication registration failure;
- no artifact overwrite;
- no generic repository/storage-provider abstraction.

## 8. Deferred work remains deferred

Scope 005 does not define or implement:

- failed-Run error-evidence shape;
- provider/model provenance;
- provider/profile persistence beyond approved references;
- Snapshot acquisition;
- RedmineMcp behavior;
- source-repository mapping;
- retry/resume;
- crash-restart recovery;
- backup/restore;
- orphan cleanup;
- Web UI;
- CLI;
- WAL tuning;
- encryption;
- multi-database portability.

The need to define failed-Run error evidence later is not a Scope 005 defect.

## 9. Documentation record

The Scope 005 record should include:

- `scope-005-storage-foundation-contract.md`
- `scope-005-implementation-brief.md`
- `scope-005-independent-review-brief.md`
- `scope-005-independent-review-decision.md`
- `scope-005-review-reconciliation.md`
- `scope-005-review-decision.md`

## 10. Git boundary

No agent push was performed.

Remote push remains a manual Product Owner action.

## 11. Final status

**PASS / CLOSED**

Later scopes may now rely on the Scope 005 storage foundation.

Any change to the frozen storage schema semantics, migration immutability model, artifact publication ordering, durable path rules, or storage authority split requires an explicit approved scope decision.
