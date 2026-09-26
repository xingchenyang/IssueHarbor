# IssueHarbor V2 / 1.0.0 — Scope 007 Independent Review Brief

**Scope:** 007 — RedmineMcp Read Path<br>
**Review stage:** Independent HTTP/auth/attachment security audit<br>
**Architecture / Scope Lead:** GPT-5.6 Sol<br>
**Implementation Agent:** GPT-6 Luna<br>
**Independent Reviewer:** DeepSeek-V41-Flash<br>
**Sol first-review result:** PASS TO INDEPENDENT REVIEW<br>

## 1. Review purpose

Independently audit the implemented Scope 007 RedmineMcp read path against the approved Scope 006 manifest and Scope 007 read-path contract.

Do not redesign the product, widen the MCP manifest, or introduce future features.

Report concrete correctness, security-boundary, privacy, upstream-trust, HTTP, normalization, cancellation/timeout, attachment, or contract-compliance issues only.

## 2. Implementation under review

Implementation commit:

`ee9c2672debfad1d30cf8dad4a893eac0c8b5ece`

Commit message:

`feat: implement RedmineMcp read path`

Reported validation:

- normal restore: blocked by sandbox denial to `api.nuget.org:443` with `NU1301`;
- cached/offline-compatible restore with `--ignore-failed-sources`: completed with visible `NU1801` / `NU1900` warnings;
- build: PASS, 0 errors;
- full test suite: **80 / 80 passed**;
- RedmineMcp-focused suite: **37 / 37 passed**;
- real stdio initialize/tools-list probe: exactly four tools, no extra stdout;
- no push performed.

## 3. Frozen capability surface

Exactly these four tools must remain exposed:

```text
redmine_issues_list
redmine_issue_get
redmine_queries_list
redmine_attachment_get
```

The reviewer must flag any mutation path, generic REST passthrough, arbitrary caller-controlled HTTP method/path/body capability, or credential-bearing tool input as a contract violation.

## 4. High-priority audit questions

### A. Secret/authentication boundary

Verify that:

1. the configured Redmine API key is transmitted only via `X-Redmine-API-Key`;
2. no Basic authentication is added;
3. no query-string API key path exists;
4. no cookie-based configured authentication is used;
5. no `X-Redmine-Switch-User` / impersonation path exists;
6. API keys cannot appear in MCP tool input/output;
7. API keys cannot appear in normalized errors;
8. API keys cannot appear in ordinary logs or exception messages;
9. configuration binding does not accidentally serialize secrets to diagnostic output.

Use sentinel secret values and adversarial upstream responses where helpful.

### B. Base-URL and request-target safety

Verify that configured base URL validation:

1. requires absolute `http` or `https`;
2. rejects userinfo;
3. rejects query;
4. rejects fragment;
5. preserves an allowed Redmine subpath such as `/redmine/`;
6. does not allow malformed URL composition to escape that base unintentionally.

Check URI composition behavior directly, especially when combining subpaths and relative endpoint paths.

### C. GET-only / no raw REST escape hatch

Inspect the actual HTTP-client structure.

Confirm:

1. approved operations issue GET only;
2. no POST/PUT/PATCH/DELETE path exists;
3. no general public/internal helper accepts arbitrary caller-controlled HTTP method + path/URL/body in a way that widens capability;
4. the four MCP tools cannot be repurposed to call arbitrary Redmine endpoints.

A private shared GET helper is allowed.

### D. Redirect behavior

Verify automatic redirects are truly disabled at the real handler level.

Test or inspect that:

1. 301/302/303/307/308 are not followed;
2. a redirect cannot cause the API key to be sent to another origin;
3. unexpected redirects map to `invalid_upstream_response`;
4. no special attachment code bypasses the no-redirect rule.

### E. Attachment trust boundary

Audit `redmine_attachment_get` closely.

Confirm:

1. attachment metadata is fetched first;
2. `content_url` is not exposed to MCP clients;
3. `content_url` is parsed safely;
4. same-origin comparison uses scheme, host, and effective port;
5. userinfo in attachment URLs is rejected;
6. cross-origin attachment URLs cause zero second download requests;
7. redirects are not followed;
8. no local temporary path is exposed;
9. no API key can be sent to a different origin.

Check edge cases such as:

```text
https://trusted.example.invalid.evil.invalid/
https://trusted.example.invalid@evil.invalid/
https://trusted.example.invalid:444/
http://trusted.example.invalid/
```

where relevant to the configured origin.

### F. Attachment size/buffering safety

Verify that the implementation enforces the configured maximum independently of Redmine metadata.

Check:

1. metadata size above limit is rejected before content retrieval;
2. actual streamed/downloaded bytes are bounded;
3. an absent or dishonest `Content-Length` cannot bypass the byte ceiling;
4. declared metadata size and actual downloaded byte count must match;
5. oversized content does not allocate an unbounded buffer before rejection;
6. byte-array representability checks happen safely before conversion/allocation;
7. returned MCP blob bytes exactly equal accepted upstream bytes.

Any integer conversion/overflow path should be examined explicitly.

### G. Timeout versus caller cancellation

Verify that:

1. caller cancellation remains cancellation;
2. internal timeout maps to `upstream_unavailable`;
3. linked cancellation-token handling can distinguish those two cases reliably;
4. timeout handling does not accidentally convert arbitrary caller cancellation into upstream failure;
5. transport failures map to `upstream_unavailable`.

### H. HTTP status/error mapping

Verify exactly:

```text
401 -> authentication_required
403 -> forbidden
404 -> not_found
408 -> upstream_unavailable
429 -> upstream_unavailable
5xx -> upstream_unavailable
unexpected 3xx -> invalid_upstream_response
other unexpected 4xx -> invalid_upstream_response
malformed JSON -> invalid_upstream_response
required-shape mismatch -> invalid_upstream_response
```

Check that upstream HTML/raw bodies are not copied into MCP errors.

### I. Issue-list request semantics

Verify:

1. explicit Issue IDs add `status_id=*`;
2. query selector maps correctly;
3. `open`, `closed`, and `all` map correctly;
4. offset/limit are encoded correctly;
5. `limit <= 100`;
6. issue-ID collections cannot exceed the approved limit;
7. no selector/filter DSL beyond Scope 006 was introduced.

### J. Issue-detail normalization

Verify the request uses only:

```text
include=children,attachments,relations,journals
```

and does not include:

```text
changesets
watchers
allowed_statuses
reactions
```

Audit normalized output for required versus optional fields, null handling, empty arrays, custom fields, journals/details, relations, recursive children, timestamp UTC normalization, date-only preservation, and source-text fidelity.

### K. Saved-query pagination

Verify complete pagination is bounded and deterministic.

Check:

1. total-count/offset/limit progression;
2. no infinite loop on repeated offset/page metadata;
3. malformed or contradictory pagination maps to `invalid_upstream_response`;
4. duplicate or regressive pages cannot cause endless retrieval.

### L. Unknown-field tolerance / required-field strictness

Verify:

- extra unknown Redmine JSON fields are ignored;
- required frozen fields with missing/wrong/malformed values are rejected;
- optional legitimately absent fields normalize consistently.

### M. Logging and stdout discipline

Confirm:

1. stdout contains MCP protocol only;
2. diagnostics use stderr/logging;
3. ordinary logs do not contain API keys, raw response bodies, Issue descriptions, journal notes, attachment bytes, or raw content URLs;
4. exception rendering cannot accidentally dump request headers or response bodies.

### N. MCP result semantics

Inspect success and failure tool results.

Confirm:

- successful tool results are not incorrectly marked `IsError`;
- normalized failures are consistently marked as errors;
- attachment success uses the intended MCP blob/binary representation;
- result shape does not expose transport/internal implementation details.

## 5. Tests to assess critically

The implementation reports 37 focused RedmineMcp tests.

Do not rely only on test names. Confirm important tests hit the real read-path code and synthetic HTTP handler rather than only helper methods.

Pay particular attention to whether tests genuinely prove:

- secret header placement;
- redirect disabling;
- cross-origin second-request suppression;
- byte ceiling during content read;
- size mismatch;
- timeout/caller-cancellation distinction;
- malformed pagination termination;
- raw-body/privacy suppression;
- exact four-tool stdio manifest.

Identify contract behavior that is implemented but insufficiently tested.

## 6. Known environment limitation

Normal NuGet restore was blocked by sandbox policy/network access and returned `NU1301`.

The implementation used cached packages with `--ignore-failed-sources` and reported `NU1801` / `NU1900` warnings.

Do not treat this network-source limitation as a Scope 007 implementation defect by itself.

Do flag any actual package/version inconsistency or build behavior that cannot be reproduced from the current repository state.

## 7. Explicitly out of scope

Do not require:

- Snapshot JSON or acquisition;
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
- live production Redmine tests.

## 8. Review output format

Return:

### Decision

Exactly one of:

- `PASS`
- `PASS WITH NON-BLOCKING FINDINGS`
- `NEEDS REVISION`
- `BLOCKED`

### Blocking findings

For each blocking finding include:

- exact file/component;
- observed behavior;
- violated approved contract rule;
- security/correctness impact;
- smallest compliant correction;
- deterministic test that should prove the correction.

### Non-blocking findings

Include only concrete maintainability, test-strength, privacy, or robustness findings worth recording.

Do not propose speculative architecture.

### Evidence reviewed

List implementation files, tool-registration files, test files, scope contracts, and commands/probes actually executed.

### Scope check

State whether the implementation introduced any out-of-scope behavior.

## 9. Independence rule

Review the approved contract and implementation evidence independently.

Do not widen the MCP manifest, add new error categories, introduce retry, redesign authentication, or require Analyzer/Snapshot behavior unless the current implementation violates an approved Scope 007 rule.
