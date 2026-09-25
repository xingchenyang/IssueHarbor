# IssueHarbor V2 / 1.0.0 — Scope 004 Structured Result Contract

**Scope:** 004 — Structured Result Contract  
**Status:** APPROVED / READY FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## 1. Purpose

Freeze the IssueHarbor 1.0 structured analysis result contract used by every successful Run.

The structured result exists for deterministic validation, filtering, sorting, comparison, and rendering. Detailed human-readable analysis remains in the Markdown report.

This scope does not define prompts, provider APIs, storage schema, or Web UI implementation.

## 2. Result shape

A valid v1 structured result contains exactly these six required fields:

```json
{
  "schema_version": 1,
  "change_type": "bug_fix",
  "implementation_complexity": "medium",
  "requires_code_change": "yes",
  "confidence": "high",
  "complexity_rationale": "The change affects several related components but does not require a major architectural redesign."
}
```

Additional structured fields are not allowed in schema v1.

## 3. Schema version

`schema_version` is an independent compatibility identifier for the structured result contract.

For v1:

```json
"schema_version": 1
```

It is not the IssueHarbor product release version.

A new schema version is required when any of the following changes materially:

- field set;
- requiredness;
- enum values;
- machine-readable shape;
- field semantics;
- cross-field validity rules.

Pure wording clarification that does not change semantics does not require a new schema version.

## 4. Required fields

All six fields are required:

- `schema_version`
- `change_type`
- `implementation_complexity`
- `requires_code_change`
- `confidence`
- `complexity_rationale`

Successful structured results must not omit fields.

Uncertainty is represented using the defined enum values rather than missing properties.

## 5. `change_type`

Allowed values:

- `bug_fix`
- `enhancement`
- `configuration_or_usage`
- `clarification_needed`
- `no_change`
- `unknown`

Semantics:

### `bug_fix`

The issue requires correction of existing behavior that does not match expected behavior.

### `enhancement`

The issue requires an intentional addition, extension, or change in product behavior or capability.

### `configuration_or_usage`

The issue is best resolved through configuration, usage, or operational procedure rather than a source-code change.

### `clarification_needed`

The available issue/context is insufficient to establish a reliable actionable conclusion and further clarification is needed.

### `no_change`

The analysis finds no product change is currently necessary.

### `unknown`

Available evidence is insufficient to classify reliably into another category and does not support a more specific `clarification_needed` conclusion.

`unknown` is a fallback for real uncertainty, not a substitute for analysis.

## 6. `requires_code_change`

Allowed values:

- `yes`
- `no`
- `uncertain`

Semantics:

### `yes`

Current evidence supports the conclusion that resolution requires a source-code change.

### `no`

Current evidence supports the conclusion that resolution does not require a source-code change.

### `uncertain`

Current evidence is insufficient to determine reliably whether a source-code change is required.

The field refers to source-code changes in the analyzed product/repository context, not Redmine issue editing.

## 7. `implementation_complexity`

Allowed values:

- `none`
- `low`
- `medium`
- `high`
- `unknown`

This field represents relative implementation complexity of the required code change. It is not severity, business priority, elapsed time, or a numeric risk score.

Semantics:

### `none`

No source-code implementation is required.

### `low`

The required code change is local, limited, and has a clearly bounded implementation/testing surface.

### `medium`

The required code change is non-trivial and spans multiple related components, behaviors, or test surfaces, but remains within a controlled scope.

### `high`

The required code change has broad behavioral impact, crosses significant boundaries, has material architectural implications, or requires a large implementation/validation surface.

### `unknown`

The available Snapshot does not support a reliable implementation-complexity assessment.

No numeric complexity score is used.

## 8. `confidence`

Allowed values:

- `low`
- `medium`
- `high`

This field represents confidence in the structured assessment as a whole.

Semantics:

### `high`

The Snapshot contains sufficient and consistent evidence supporting the main conclusions.

### `medium`

The main conclusions are reasonable, but meaningful uncertainty or missing context remains.

### `low`

The available evidence is limited, conflicting, or heavily dependent on inference.

No percentage or numeric confidence score is used.

## 9. `complexity_rationale`

`complexity_rationale` is the only free-text field in structured result v1.

Requirements:

- required;
- non-empty;
- concise;
- plain text;
- explains the assigned `implementation_complexity`;
- when complexity is `none` or `unknown`, explains why;
- must not contain hidden chain-of-thought or private reasoning traces.

The text is generated directly in the Analysis Contract report language.

Machine-readable enum values are never translated.

## 10. Cross-field invariants

The following deterministic invariants are part of schema v1:

### No code change

If:

```text
requires_code_change = no
```

then:

```text
implementation_complexity = none
```

### Code change required

If:

```text
requires_code_change = yes
```

then `implementation_complexity` must be one of:

- `low`
- `medium`
- `high`
- `unknown`

It must not be `none`.

### Code-change uncertainty

If:

```text
requires_code_change = uncertain
```

then:

```text
implementation_complexity = unknown
```

### Configuration/usage resolution

If:

```text
change_type = configuration_or_usage
```

then:

```text
requires_code_change = no
implementation_complexity = none
```

### No change required

If:

```text
change_type = no_change
```

then:

```text
requires_code_change = no
implementation_complexity = none
```

These checks are deterministic and must not invoke AI.

## 11. Language behavior

Machine-readable keys and enum values remain stable regardless of report language.

Example:

```json
{
  "schema_version": 1,
  "change_type": "bug_fix",
  "implementation_complexity": "low",
  "requires_code_change": "yes",
  "confidence": "high",
  "complexity_rationale": "La modification semble limitée à un composant bien identifié."
}
```

Only `complexity_rationale` follows the Analysis Contract report language.

## 12. Schema drift prevention

The machine-readable JSON Schema for v1 must define:

```json
"additionalProperties": false
```

Agents/providers must not add arbitrary structured fields.

A new useful structured field requires an explicit future schema decision and version change.

## 13. Validation and successful Run publication

Structured result validation is deterministic:

```text
provider/model output
  -> parse JSON
  -> validate against result schema
  -> validate cross-field invariants
  -> valid
       -> eligible for successful Run publication
     invalid
       -> cannot be published as a successful Run
```

A successful Run must have a valid structured result.

A valid Markdown report does not compensate for an invalid structured result.

Scope 004 does not introduce a second AI call to repair malformed or invalid structured output.

Failure categorization and provider retry/repair behavior are deferred.

## 14. Rendering semantics

Stored/compared values remain canonical machine values:

- `bug_fix`
- `medium`
- `yes`
- `high`

Presentation layers may map them to canonical human labels such as:

- Bug fix
- Medium
- Yes
- High

Future UI localization may translate presentation labels without changing stored values.

Structured comparison remains deterministic and field-by-field.

## 15. Machine-readable schema artifact

The canonical v1 machine-readable schema should be stored as:

```text
analysis-result-v1.schema.json
```

It must represent the approved field set, requiredness, enum values, `additionalProperties: false`, and cross-field invariants.

## 16. Explicitly deferred

Scope 004 does not decide or implement:

- prompt wording;
- system prompts;
- Markdown report structure;
- provider structured-output APIs;
- provider JSON modes;
- retry behavior;
- malformed-output repair;
- C# DTO/class shapes;
- JSON serialization library;
- JSON Schema validation library choice beyond what implementation needs for deterministic tests;
- SQLite storage;
- Run tables;
- artifact paths;
- Web UI implementation;
- numeric scoring;
- priority;
- estimated hours;
- generic risk scoring;
- affected-files lists.

## 17. Acceptance rule

For every proposed successful Run, the contract must make it possible to determine without AI:

- which result schema version applies;
- whether all required fields are present;
- whether any extra field is present;
- whether enum values are valid;
- whether cross-field values are consistent;
- whether the result is eligible for successful Run publication;
- how two valid structured results can be compared deterministically.

Any future change to these machine semantics requires an explicit approved scope decision.
