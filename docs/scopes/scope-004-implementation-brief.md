# IssueHarbor V2 / 1.0.0 — Scope 004 Implementation Brief

**Scope:** 004 — Structured Result Contract  
**Status:** APPROVED FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## 1. Functional goal

Implement the approved Scope 004 structured-result contract as repository documentation plus a canonical machine-readable JSON Schema and deterministic contract tests.

Do not implement provider, prompt, storage, execution-engine, or Web UI behavior.

## 2. Authoritative contract

Use the approved:

```text
docs/scopes/scope-004-structured-result-contract.md
```

as the semantic authority for the implementation.

Do not alter the approved field set, enum set, requiredness, or cross-field invariants.

## 3. In scope

Add the formal Scope 004 contract to `docs/scopes/`.

Add one canonical machine-readable JSON Schema for result schema v1.

Recommended location:

```text
src/Analyzer/Contracts/analysis-result-v1.schema.json
```

or another minimal location justified by the current repository structure.

Add deterministic tests that prove the schema accepts valid examples and rejects invalid examples required by this brief.

Update existing project documentation only where necessary to reference the new structured-result contract/schema.

Create one local commit after validation.

## 4. Canonical v1 field set

The schema must contain exactly these required properties:

- `schema_version`
- `change_type`
- `implementation_complexity`
- `requires_code_change`
- `confidence`
- `complexity_rationale`

`additionalProperties` must be `false`.

`schema_version` must equal integer `1`.

## 5. Enum values

### `change_type`

- `bug_fix`
- `enhancement`
- `configuration_or_usage`
- `clarification_needed`
- `no_change`
- `unknown`

### `implementation_complexity`

- `none`
- `low`
- `medium`
- `high`
- `unknown`

### `requires_code_change`

- `yes`
- `no`
- `uncertain`

### `confidence`

- `low`
- `medium`
- `high`

## 6. Cross-field invariants to enforce

The machine-readable contract/tests must enforce:

- `requires_code_change = no` -> `implementation_complexity = none`
- `requires_code_change = yes` -> complexity is `low|medium|high|unknown`, never `none`
- `requires_code_change = uncertain` -> `implementation_complexity = unknown`
- `change_type = configuration_or_usage` -> `requires_code_change = no` and complexity `none`
- `change_type = no_change` -> `requires_code_change = no` and complexity `none`

Implementation may express these directly in JSON Schema or through a deterministic validator layered on the schema, but the resulting contract behavior must be deterministic and covered by tests.

Do not call AI for validation.

## 7. Minimum deterministic test matrix

At minimum, add tests for:

### Valid

1. `bug_fix / yes / low`
2. `enhancement / yes / high`
3. `configuration_or_usage / no / none`
4. `no_change / no / none`
5. `clarification_needed / uncertain / unknown`
6. valid result with non-English `complexity_rationale`

### Invalid

1. missing required property
2. unknown extra property
3. invalid enum value
4. `requires_code_change = no` with complexity `medium`
5. `requires_code_change = yes` with complexity `none`
6. `requires_code_change = uncertain` with complexity other than `unknown`
7. `configuration_or_usage` with `requires_code_change = yes`
8. `no_change` with non-`none` complexity
9. empty `complexity_rationale`
10. wrong `schema_version`

Do not add provider mocks or AI calls.

## 8. Out of scope

Do not implement or design:

- C# product/domain DTOs beyond minimal test/support code required to validate the schema;
- provider integration;
- prompts;
- structured-output API selection;
- malformed-output repair;
- retries;
- Run execution logic;
- SQLite persistence;
- artifact publication;
- UI rendering code;
- report templates;
- new result fields;
- scoring systems.

Do not expand schema v1.

## 9. Documentation updates

If the design plan or ideas registry currently describes the exact structured-result schema as OPEN, update only the relevant wording to:

- record Scope 004 as the frozen authority;
- preserve still-deferred implementation details;
- avoid rewriting unrelated baseline material.

Archive:

```text
docs/scopes/scope-004-structured-result-contract.md
docs/scopes/scope-004-implementation-brief.md
```

The review decision will be added after Architecture / Scope Lead review.

## 10. Validation

Run all solution checks because this scope adds machine-readable contract tests:

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

- all approved valid examples pass;
- all approved invalid examples fail validation;
- no extra structured fields were introduced;
- no AI/network/provider dependency is needed by the tests;
- no private/personal data appears in tracked changes;
- no unrelated files changed.

## 11. Git boundary

The Implementation Agent may inspect, stage, and create the approved local commit.

The Implementation Agent must never:

- run `git push`;
- write to a Git remote;
- create/push tags;
- create releases;
- modify remote settings.

Remote push remains a manual Product Owner action.

## 12. Implementation report

Report:

1. files changed;
2. schema location;
3. validation mechanism/library used, if any;
4. exact tests added;
5. restore/build/test results;
6. public-safety checks;
7. local commit hash;
8. deviations, blockers, or follow-up ideas.

Do not push.

## 13. Completion rule

Scope 004 is ready for review only when:

- the approved contract is archived;
- a canonical v1 machine-readable schema exists;
- requiredness/enums/additionalProperties/cross-field invariants are enforced;
- deterministic valid/invalid tests pass;
- no provider/storage/UI implementation was introduced;
- repository public-safety rules remain satisfied;
- no push was performed.
