# IssueHarbor V2 / 1.0.0 — Scope 008 Review Reconciliation

**Scope:** 008 — Source Repository Context\
**Status:** NEEDS REVISION — test-hardening only\
**Architecture / Scope Lead:** GPT-5.6 Sol\
**Implementation Agent:** GPT-6 Luna\
**Independent Reviewer:** DeepSeek-V41-Flash\
**Independent review result:** PASS WITH NON-BLOCKING FINDINGS\

## 1. Reconciliation result

The independent review found no blocking implementation defect in Scope 008.

The approved source architecture, read-only/local-only Git boundary, immutable revision semantics, bare-repository support, working-tree isolation, object parsing, and process controls are accepted.

Scope 008 is not reopened for architecture or product redesign.

Before final `PASS / CLOSED`, two narrowly bounded regression tests are required because they protect core frozen guarantees that the current repository tests do not prove strongly enough:

- real lazy-fetch suppression against a configured promisor remote;
- NUL-safe parsing of Git tree/search paths containing control characters injected at the object level.

No production semantic change is required for these two items unless the new tests expose a defect.

## 2. Required revision R1 — real lazy-fetch suppression regression test

Add a deterministic integration test that proves `GIT_NO_LAZY_FETCH=1` prevents Git from attempting to retrieve a missing object from a configured promisor remote.

The test must use only local/synthetic resources and no real network.

Recommended structure:

1. create a synthetic repository;
2. create or identify a committed blob;
3. configure a synthetic `promisor` remote;
4. point that remote at a local fake/recording transport mechanism;
5. make the target blob locally unavailable;
6. call the real IssueHarbor blob-read path;
7. assert that:
   - the call fails deterministically as a missing/local-object failure;
   - the recording transport is not invoked.

Where practical, include a control step outside the IssueHarbor call showing that Git without `GIT_NO_LAZY_FETCH=1` would attempt remote retrieval. This proves the test is discriminating rather than vacuously passing.

Do not add any production remote-fetch capability.

## 3. Required revision R2 — NUL/control-character path regression test

Add a deterministic real-Git integration test for paths that cannot be created normally on the host filesystem but are legal Git tree paths.

Construct the tree at the Git object level using an equivalent of:

```text
git hash-object
git mktree -z
```

Include at minimum paths containing:

```text
newline
tab
```

Then prove through the real production code that:

- `ListTreeAsync` returns the exact original paths;
- `SearchAsync` returns the exact original paths;
- entries do not split, shift, or corrupt;
- no line-delimited parsing assumption is introduced.

The test must rely on the production `-z` / NUL-safe parsing behavior.

## 4. Finding N1 — `git grep -m` per-file semantics

No Scope 008 production change is required.

The independent reviewer correctly confirmed that `git grep -m N` limits matches per file rather than globally.

The IssueHarbor parser still enforces the returned `resultLimit`, and process output remains bounded by the existing maximum-output ceiling.

This is a robustness/usability observation rather than a correctness or scope violation.

Record the behavior for later Snapshot/source-selection work. If real workloads show that large repositories frequently hit the Git-output ceiling before IssueHarbor collects the requested global result count, a later approved scope may refine search execution.

Do not change the frozen source-search semantics in this bounded revision.

## 5. Finding N4 — missing repository path diagnostic

No closure-blocking code change is required.

A nonexistent configured repository path and an existing non-Git directory both fail deterministically, but the former currently produces a misleading message indicating that the Git executable could not be started.

This is a diagnostic-quality issue only.

It may be improved in later maintenance or observability work without changing source-context semantics.

Do not introduce a new failure taxonomy in this revision.

## 6. Finding N5 — empty literal search pattern

No change required.

An empty fixed-string search pattern is accepted and matches lines according to Git's literal-search behavior.

The approved Scope 008 contract does not forbid an empty search pattern.

Later source-selection/Snapshot logic may choose to reject empty searches at its own input boundary if required.

## 7. No production redesign authorized

This revision must not:

- change repository mapping cardinality;
- add remote synchronization;
- add clone/fetch/pull/push behavior;
- add checkout/worktree-based reading;
- add submodule traversal;
- add a generic SCM abstraction;
- add SQLite migration `002`;
- change provenance fields;
- add Snapshot behavior;
- change the frozen Git error taxonomy merely for diagnostics;
- add AI/source-ranking behavior.

Production code should remain unchanged unless one of the two new regression tests exposes an actual defect.

## 8. Independent-review records to archive

Archive under `docs/scopes/`:

```text
scope-008-independent-review-brief.md
scope-008-independent-review-decision.md
scope-008-review-reconciliation.md
```

The final Scope 008 review decision will be added after the bounded test-hardening revision passes Sol review.

## 9. Validation required

Run:

```text
git --version
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Also run the SourceContext-focused tests.

Confirm:

- both new regression tests pass;
- all previous tests still pass;
- no production remote synchronization code is introduced;
- no migration `002` is added;
- no private/personal source data is added;
- `git diff --check` passes;
- no push is performed.

If normal restore is blocked by execution-environment NuGet configuration/permissions, report the exact limitation and use the previously accepted isolated configuration only as needed; do not hide warnings/errors.

## 10. Git boundary

The Implementation Agent may create one local correction/documentation commit.

The Implementation Agent must not push.

Remote push remains a manual Product Owner action.

## 11. Revision report required

Report:

1. files changed;
2. lazy-fetch regression-test construction;
3. proof that the fake transport was not invoked;
4. whether a no-`GIT_NO_LAZY_FETCH` control demonstrates an attempted retrieval;
5. control-character Git-tree construction;
6. exact `ListTreeAsync` and `SearchAsync` assertions;
7. whether any production code changed and why;
8. full and focused test counts;
9. restore/build/test evidence;
10. proof no migration `002` was added;
11. local commit hash;
12. deviations/blockers;
13. confirmation no push occurred.

## 12. Review sequence after revision

After this bounded revision:

1. GPT-5.6 Sol performs a targeted review.
2. A second full independent audit is not required unless the tests expose a production defect or implementation changes source semantics/process behavior.
3. If compliant, Sol issues the final Scope 008 `PASS / CLOSED` decision.
