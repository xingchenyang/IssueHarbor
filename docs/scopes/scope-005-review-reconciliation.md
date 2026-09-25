# IssueHarbor V2 / 1.0.0 — Scope 005 Review Reconciliation

**Scope:** 005 — Storage Foundation  
**Status:** NEEDS REVISION — bounded hardening only  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  
**Independent review result:** PASS WITH NON-BLOCKING FINDINGS  

## 1. Reconciliation result

The independent review found no blocking defect in the approved storage architecture or implemented schema/migration semantics.

Scope 005 is not reopened for redesign.

However, before final `PASS / CLOSED`, four small hardening/test-hygiene corrections are required because they strengthen already-approved invariants without changing product scope or storage semantics.

No change to the seven-table schema, status enums, result enums, ID model, migration model, or artifact layout is authorized.

## 2. Required revision R1 — enforce durable-root containment in C#

Add an explicit containment guard in the artifact path resolution/publishing code.

The implementation must reject any path that resolves outside the configured durable-data root, including at minimum:

- rooted Windows paths;
- rooted Unix-style paths;
- UNC paths;
- drive-relative paths;
- `..` traversal;
- separator/normalization variants that escape the root.

The guard must operate on normalized/full paths and enforce a directory-boundary-safe containment check.

This is a defense of the existing `relative paths only` contract, not a new security feature.

## 3. Required revision R2 — test published bytes, not only source bytes

Extend artifact publication tests so that they:

1. publish the artifact;
2. reopen/read the actual final file from disk;
3. recompute byte length;
4. recompute SHA-256;
5. compare both values with:
   - the returned publication metadata; and
   - the committed SQLite catalog metadata.

This must cover at least one Snapshot artifact and one Run artifact set.

## 4. Required revision R3 — exercise the C# path guard directly

Add deterministic tests proving the artifact publisher/path resolver rejects escape/rooted inputs.

The existing SQL CHECK test remains valuable but is not sufficient by itself because it verifies only the database boundary.

No host-private real path may appear in fixtures; use clearly synthetic paths.

## 5. Required revision R4 — improve disposable test cleanup

Harden storage-test cleanup so transient file/database handle timing does not routinely leave `issueharbor-storage-tests-*` directories behind.

Use a small bounded retry/delay strategy or equivalent deterministic cleanup approach.

Do not add a background cleanup service or production cleanup framework.

## 6. Finding intentionally not fixed by editing migration 001

The independent reviewer correctly observed that these constraints may look redundant:

- `snapshots.UNIQUE (snapshot_id, issue_id)`
- `request_items.UNIQUE (request_item_id, snapshot_id)`

They are required as parent-key uniqueness for composite foreign keys.

Do **not** edit `001_initial.sql` merely to add explanatory comments after the migration has been committed and is protected by migration checksums.

Instead, preserve this rationale in the Scope 005 review/reconciliation documentation and, if useful, in non-migration developer/storage documentation or tests without changing the historical migration file.

## 7. Independent review artifacts to archive

Archive the formal independent-review records under `docs/scopes/`:

- `scope-005-independent-review-brief.md`
- `scope-005-independent-review-decision.md`
- `scope-005-review-reconciliation.md`

The final Scope 005 review decision will be added after the bounded revision passes review.

## 8. Out of scope

The revision must not introduce:

- schema/table/column changes;
- migration 002;
- migration checksum bypasses;
- new status/reason/result enums;
- backup/restore;
- orphan cleanup;
- provider/execution behavior;
- failed-Run error evidence shape;
- retry/resume;
- Web UI or CLI features;
- generic storage abstractions.

## 9. Validation required

Run:

```text
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Also run the storage-focused test filter.

Confirm:

- all prior tests still pass;
- new containment tests pass;
- published-file reread hash/size tests pass;
- temporary test roots are cleaned successfully in a normal complete run;
- `001_initial.sql` is byte-for-byte unchanged from commit `4d65c14d3a2154e76562ab823dbff307dfad0258`;
- no private/personal content was added;
- no push was performed.

## 10. Git boundary

The Implementation Agent may create a local correction commit.

The Implementation Agent must not push.

Remote push remains a manual Product Owner action.

## 11. Implementation report required

Report:

1. files changed;
2. exact containment algorithm;
3. new/changed tests;
4. cleanup behavior;
5. proof that `001_initial.sql` was not modified;
6. restore/build/test results;
7. storage-focused test result;
8. local commit hash;
9. deviations or blockers;
10. confirmation that no push occurred.

## 12. Review sequence after revision

After the bounded correction:

1. GPT-5.6 Sol reviews the correction and test evidence.
2. A second full independent audit is not required unless the correction changes approved storage semantics, migration/schema content, or reveals a new durability issue.
3. If correction is compliant, Sol issues the final Scope 005 `PASS / CLOSED` decision.

