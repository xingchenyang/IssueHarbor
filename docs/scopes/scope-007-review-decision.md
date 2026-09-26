# IssueHarbor V2 / 1.0.0 — Scope 007 Final Review Decision

**Scope:** 007 — RedmineMcp Read Path<br>
**Status:** PASS / CLOSED<br>
**Architecture / Scope Lead:** GPT-5.6 Sol<br>
**Implementation Agent:** GPT-6 Luna<br>
**Independent Reviewer:** DeepSeek-V41-Flash<br>

## 1. Final decision

**PASS / CLOSED**

Scope 007 is accepted as the frozen RedmineMcp read-path baseline.

The implementation passed Sol first review and an independent HTTP/authentication/attachment-security audit. The independent audit reported no blocking defect and verified the critical trust boundaries using both code inspection and adversarial/runtime probes.

No implementation revision is required before closure.

## 2. Accepted implementation commit

Implementation commit:

`ee9c2672debfad1d30cf8dad4a893eac0c8b5ece`

Commit message:

`feat: implement RedmineMcp read path`

## 3. Independent review outcome

Independent reviewer:

`DeepSeek-V41-Flash`

Decision:

`PASS WITH NON-BLOCKING FINDINGS`

The review independently confirmed:

- API-key header authentication only;
- GET-only implementation;
- no mutation or raw REST escape hatch;
- redirects disabled;
- cookies disabled;
- no TLS certificate-validation bypass;
- same-origin attachment enforcement;
- no second attachment-content request for rejected origins;
- bounded attachment reads;
- metadata/actual-size consistency checks;
- cancellation/timeout distinction;
- deterministic frozen error mapping;
- query-pagination termination checks;
- normalized DTO strictness;
- stdout protocol discipline;
- no secret/upstream-host leakage in the process probe;
- exactly four MCP tools.

## 4. Validation evidence

Accepted reported and independently reproduced evidence includes:

- restore completed in the independent-review environment;
- build completed with 0 warnings / 0 errors;
- full suite: **80 / 80 passed**;
- RedmineMcp-focused suite: **37 / 37 passed**;
- real stdio `initialize` + `tools/list` probe returned exactly four tools;
- no extra stdout;
- real failure-path process probe returned normalized errors without secret or upstream-host leakage.

The implementation agent's earlier NuGet source-access limitation is treated as an environment condition, not a product defect; the independent reviewer subsequently completed a normal restore successfully.

## 5. Reconciliation of non-blocking finding N1 — percent-encoded query values

No code change is required.

The implementation serializes some query values in percent-encoded wire form, for example:

```text
issue_id=12%2C19
status_id=%2A
include=children%2Cattachments%2Crelations%2Cjournals
```

The frozen contract specifies the semantic Redmine parameter values:

```text
issue_id=12,19
status_id=*
include=children,attachments,relations,journals
```

Scope 007 interprets those mappings as parameter semantics, not as a requirement for a particular raw query-string octet representation.

Percent-encoding does not change the intended decoded parameter values. No custom unescaped query serializer is introduced merely to make the raw URI visually match the contract examples.

This finding remains documented because raw-wire behavior was not directly asserted by the repository tests.

## 6. Reconciliation of non-blocking finding N2 — unexpected exception fallback

No Scope 007 revision is required.

The current handlers map unexpected internal exceptions to the existing frozen `invalid_upstream_response` category rather than exposing implementation exception details.

This is not ideal diagnostic classification, but it:

- does not leak secrets or stack traces;
- does not add an unauthorized error category;
- does not widen the MCP capability surface;
- does not break an approved success/failure path.

A later observability/error-diagnostics scope may refine internal defect handling if a concrete operational need is established.

Scope 007 does not add a new error enum for this purpose.

## 7. Reconciliation of non-blocking finding N3 — repository test-strength gaps

No closure blocker.

The reviewer identified three behaviors that are implemented but not each isolated by a dedicated repository test:

- non-zero total with an empty saved-query page;
- cancellation occurring during an in-flight transport operation;
- size mismatch without a `Content-Length` header.

These are worthwhile future test-strength improvements.

However, the independent review directly probed the relevant pagination and attachment boundaries and confirmed the implementation behavior. The code path distinguishing caller cancellation from internal timeout was also independently reviewed.

No product semantic change is required.

## 8. Reconciliation of non-blocking finding N4 — timestamp fractional precision

No change required.

RedmineMcp emits normalized UTC timestamps with seven fractional-second digits while the storage helper elsewhere may use millisecond precision.

Both are deterministic UTC `Z` representations.

Scope 007 requires deterministic UTC normalization, not identical string precision across unrelated component boundaries.

Future comparisons must compare timestamp values semantically rather than assuming all components serialize identical fractional precision unless a later contract explicitly freezes that requirement.

## 9. Frozen security/read-path outcome

The accepted Scope 007 baseline is:

- exactly four Scope 006 tools;
- server-side Redmine configuration;
- API key transmitted only through `X-Redmine-API-Key`;
- GET-only HTTP access;
- no Basic auth;
- no impersonation;
- no query-string credentials;
- redirects disabled;
- cookies disabled;
- normal platform TLS validation;
- no retry;
- deterministic timeout/cancellation behavior;
- same-origin attachment URL validation;
- bounded attachment retrieval;
- size consistency verification;
- normalized factual DTOs;
- strict required-field validation with unknown-field tolerance;
- six frozen error categories;
- sanitized logs/errors;
- stdio protocol-only stdout;
- no generic REST passthrough;
- no mutation path.

## 10. Scope discipline

Scope 007 correctly leaves deferred:

- Analyzer Snapshot JSON and acquisition;
- attachment retention policy;
- source-repository context;
- Request orchestration;
- Analyzer execution;
- provider integration;
- retry;
- credential-management UI;
- secret-retrieval API;
- backup/restore;
- Redmine mutation;
- generic REST access;
- live production Redmine fixtures.

No future-scope behavior is accepted by implication.

## 11. Documentation record

The final Scope 007 record should contain:

- `scope-007-redmine-mcp-read-path-contract.md`
- `scope-007-implementation-brief.md`
- `scope-007-independent-review-brief.md`
- `scope-007-independent-review-decision.md`
- `scope-007-review-decision.md`

## 12. Git boundary

No agent push was performed.

Remote push remains a manual Product Owner action.

## 13. Final status

**PASS / CLOSED**

Later scopes may rely on the Scope 007 RedmineMcp read path as the read-only Redmine integration baseline.

Any future widening of authentication methods, trusted origins, HTTP methods, tool surface, redirect behavior, attachment transport rules, or frozen error semantics requires an explicit approved scope decision.
