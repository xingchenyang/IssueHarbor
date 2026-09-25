# IssueHarbor V2 / 1.0.0 — Scope 003 Review Decision

**Scope:** 003 — Core Domain & Status Contracts  
**Status:** PASS / CLOSED  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## 1. Review result

Scope 003 is accepted as complete.

The approved Core Domain & Status Contract was documented without expanding into persistence, schema, provider, execution-engine, CLI, or Web UI implementation.

The formal Scope 003 Implementation Brief was also archived after review feedback, restoring complete scope provenance.

## 2. Accepted implementation evidence

Primary Scope 003 contract commit:

`378efd3543139fa81f72bba70dcc8946be21ccca`

Commit message:

`docs: freeze IssueHarbor core domain statuses`

Scope 003 Implementation Brief archival commit:

`e5b0542d5d7c674d2ab5ac8da4932e10bcdf8407`

Commit message:

`docs: archive scope 003 implementation brief`

Reported repository state after the second commit:

- working tree clean;
- local branch two commits ahead of `origin/main`;
- no push performed.

## 3. Frozen contract outcome

The accepted contract freezes:

### Request statuses

- `running`
- `completed`
- `stopped`

### Request-wide stop reasons

- `operator_stopped`
- `execution_deadline`
- `provider_authentication_required`
- `provider_quota_exhausted`

### Request Item statuses

- `planned`
- `running`
- `completed`
- `failed`
- `blocked`
- `not_started`

### Admission-only result

- `analyzer_busy`

`analyzer_busy` remains an invocation/admission result and does not create a durable Request.

## 4. Accepted domain semantics

The review accepts the documented separation between:

- Issue identity;
- immutable Snapshot input;
- actual Run execution attempts;
- accepted durable Requests;
- internal `Issue × Profile` Request Items;
- lifecycle status;
- stop/block/not-started reasons.

Blocked or not-started Items do not require fake Runs.

A completed Request may contain failed or blocked Items; Request completion means planned execution reached its end without a request-wide stop condition.

## 5. Scope discipline

Scope 003 correctly leaves the following deferred:

- C# domain type shapes;
- identifier types;
- SQLite tables and indexes;
- ORM / SQL choice;
- migration mechanism;
- serialization shape;
- exact structured result schema;
- provider implementation;
- retry behavior;
- crash recovery / resume;
- execution-engine implementation;
- CLI implementation;
- Web UI implementation.

No scope expansion is accepted by implication.

## 6. Documentation provenance

The following Scope 003 records should remain under `docs/scopes/`:

- `scope-003-core-domain-status-contract.md`
- `scope-003-implementation-brief.md`
- `scope-003-review-decision.md`

Markdown trailing spaces intentionally used for hard line breaks are not considered a defect.

## 7. Git boundary

No remote write was performed by the Implementation Agent.

Remote push remains a manual Product Owner action.

## 8. Final decision

**PASS / CLOSED**

Scope 003 is now a frozen semantic baseline for later structured-result, storage, execution, CLI, and Web UI scopes.

Any future change to these lifecycle meanings requires an explicit approved scope decision.
