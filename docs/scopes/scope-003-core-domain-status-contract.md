# IssueHarbor V2 / 1.0.0 — Scope 003 Core Domain & Status Contract

**Scope:** 003 — Core Domain & Status Contracts  
**Status:** APPROVED / READY FOR DOCUMENTATION IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## 1. Purpose

Freeze the minimum durable domain semantics needed by later storage, execution, CLI, and Web UI work.

This scope defines meaning and lifecycle only. It does not define database schema, ORM mappings, serialization shapes, or execution code.

## 2. Frozen domain relationships

- `Issue` identity is the Redmine `issue_id`.
- `Snapshot` is immutable factual analysis input for one Issue.
- `Run` represents one actual analysis execution attempt against one Snapshot, one Analysis Contract, and one resolved Execution Profile.
- `Request` is one accepted manual or scheduled batch execution.
- `Request Item` is the internal durable execution unit `Issue × Profile`.

A Request freezes ordered Issue membership when accepted. A Snapshot is created only when processing for that Issue actually begins. Profiles intended for strict comparison reuse the same Snapshot.

## 3. Domain invariants

### Issue

- Stable identity is Redmine `issue_id` only.
- Current mutable Redmine content is not Issue identity.
- Analysis input facts belong in Snapshots.

### Snapshot

- Immutable once successfully published.
- Belongs to one Issue.
- Created when that Issue begins processing.
- Shared across profiles intended for strict comparison.
- A failed acquisition must not publish a partial Snapshot.

### Analysis Contract

- Defines analysis instructions, structured result contract, and report language.
- Report language is execution configuration, not factual Snapshot content.
- Exact result schema is deferred to Scope 004.

### Execution Profile

- Defines how the Analysis Contract is executed.
- Profiles are configuration.
- Resolved execution provenance is recorded on the Run.
- Provider adapter/authentication details remain deferred.

### Request

- Exists only after execution admission succeeds.
- Freezes selection definition, ordered Issue membership, Analysis Contract, and selected profiles.
- Membership does not change while the Request runs.
- Does not represent a queue, retry framework, or scheduler state machine.

### Request Item

- Represents one `Issue × Profile` within a Request.
- May execute at most once within that Request.
- May have no Run if execution never meaningfully starts.

### Run

- Represents an actual started analysis execution attempt.
- A successful Run retains structured JSON and Markdown report artifacts.
- A failed execution may retain a Run for provenance/error evidence.
- Blocked or not-started Items must not require fake Runs.

## 4. Request lifecycle

Frozen Request statuses:

- `running`
- `completed`
- `stopped`

`completed` means the Request reached the end of its planned work without a request-wide stop condition. It does not mean all Items succeeded.

Frozen Request stop reasons:

- `operator_stopped`
- `execution_deadline`
- `provider_authentication_required`
- `provider_quota_exhausted`

Do not add speculative request-wide reasons without a real workflow.

## 5. Request Item lifecycle

Frozen Item statuses:

- `planned`
- `running`
- `completed`
- `failed`
- `blocked`
- `not_started`

Definitions:

- `planned` — belongs to the frozen Request plan and is expected to execute later.
- `running` — execution has started.
- `completed` — execution completed successfully.
- `failed` — execution was actually attempted and failed because of an Item-specific failure.
- `blocked` — execution could not meaningfully proceed because a prerequisite/shared precondition was unavailable.
- `not_started` — terminal state for planned work that will not execute because the Request stopped early.

## 6. Status/reason separation

Lifecycle state and cause are separate concepts.

Do not encode stop reasons or external error categories directly as lifecycle statuses.

Examples:

- Item status: `not_started`; reason: `execution_deadline`
- Item status: `blocked`; reason: `provider_authentication_required`

## 7. Busy admission semantics

`analyzer_busy` is an admission result, not a durable Request status.

When the single execution slot is occupied:

- the attempted invocation is not accepted;
- no durable Request is created;
- no selection is resolved;
- no Request Items are created;
- the caller receives a machine-readable `not_started / analyzer_busy` result.

If a future real workflow requires durable history of skipped invocations, design that separately instead of weakening Request invariants.

## 8. Failure mapping

### Ordinary Item-specific failure

- current Item -> `failed`
- continue with later Items

### Shared Snapshot acquisition failure

- affected Issue/profile Items -> `blocked`
- continue with the next Issue

### Provider-wide deterministic authentication failure

- triggering Item -> `blocked`
- reason -> `provider_authentication_required`
- Request -> `stopped`
- later planned Items -> `not_started`

### Provider-wide deterministic quota exhaustion

- triggering Item -> `blocked`
- reason -> `provider_quota_exhausted`
- Request -> `stopped`
- later planned Items -> `not_started`

### Operator stop

- current Item finishes
- Request -> `stopped`
- reason -> `operator_stopped`
- later planned Items -> `not_started`

### Execution deadline

- current Item finishes
- Request -> `stopped`
- reason -> `execution_deadline`
- later planned Items -> `not_started`

## 9. Explicitly deferred

Scope 003 does not decide:

- C# class/record/value-object shapes
- identifier type (`Guid`, `Ulid`, integer, etc.)
- SQLite tables, columns, foreign keys, or indexes
- EF Core vs explicit SQL
- migration mechanism
- JSON serialization shape
- exact structured result schema
- provider error taxonomy beyond the frozen request-wide stop cases
- retry behavior
- crash recovery / resume semantics
- UI wording
- exact execution sequencing implementation

## 10. Acceptance rule

After this contract is documented, later scopes must be able to answer unambiguously:

- Is a durable Request created?
- Is a Snapshot created?
- Is a Run created?
- What is the Request status?
- What is the Item status?
- What reason explains a stopped/blocked/not-started outcome?
- Does execution continue to later Items?

Any later need to change these meanings requires an explicit scope decision.
