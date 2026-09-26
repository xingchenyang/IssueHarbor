# IssueHarbor V2 / 1.0.0 — Scope 007 Implementation Brief

**Scope:** 007 — RedmineMcp Read Path\
**Status:** APPROVED FOR IMPLEMENTATION\
**Architecture / Scope Lead:** GPT-5.6 Sol\
**Implementation Agent:** GPT-6 Luna\
**Independent Reviewer:** DeepSeek-V41-Flash\

## 1. Functional goal

Implement the four approved RedmineMcp 1.0 tools against a real read-only Redmine HTTP client while preserving the Scope 006 manifest exactly.

The implementation must be independently usable as an MCP server without Analyzer.

Do not implement Snapshot acquisition or Analyzer orchestration.

## 2. Authoritative inputs

Use:

- frozen V2 design plan;
- Scope 006 RedmineMcp 1.0 Manifest Contract;
- `docs/scopes/scope-007-redmine-mcp-read-path-contract.md`.

Do not widen the four-tool manifest.

## 3. Required implementation

Implement the smallest concrete code needed for:

- Redmine base URL configuration;
- API-key configuration;
- timeout configuration;
- attachment-size ceiling configuration;
- validated GET-only Redmine client;
- four tool handlers;
- deterministic JSON normalization;
- attachment metadata and binary retrieval;
- normalized error mapping;
- synthetic HTTP integration tests.

## 4. Authentication

Send the API key only as:

```text
X-Redmine-API-Key
```

Do not implement:

```text
Basic auth
query-string API keys
cookies as auth
impersonation
```

Never expose the key in tool arguments, outputs, logs, or exception messages.

## 5. Base URL

Validate the configured Redmine base URL.

Requirements:

- absolute;
- `http` or `https`;
- optional Redmine subpath supported;
- no userinfo;
- no query;
- no fragment;
- normal platform TLS certificate validation only.

Do not add certificate-validation bypass settings.

## 6. HTTP behavior

Use GET only.

Set:

```text
Accept: application/json
X-Redmine-API-Key: <secret>
```

Disable automatic redirect following.

Do not add automatic retry.

Caller cancellation must remain caller cancellation.

## 7. Tool wiring

Wire the existing Scope 006 tools to the real read path:

```text
redmine_issues_list
redmine_issue_get
redmine_queries_list
redmine_attachment_get
```

Do not add or rename tools.

## 8. `redmine_issues_list`

Implement approved selector mappings.

Explicit `issue_ids` must add:

```text
status_id=*
```

Enforce:

```text
1 <= issue_ids.Count <= 100
offset >= 0
1 <= limit <= 100
```

Return one Redmine page only.

Normalize exactly to the approved summary output contract.

## 9. `redmine_issue_get`

Request exactly:

```text
include=children,attachments,relations,journals
```

Do not request excluded include sets.

Normalize:

- Issue facts;
- named references;
- optional nulls;
- custom fields;
- journals/details;
- attachment metadata;
- relations;
- recursive children.

Do not perform AI transformation.

## 10. `redmine_queries_list`

Implement complete visible-query pagination internally.

Detect and reject non-progressing or malformed pagination.

Return normalized query summaries.

## 11. `redmine_attachment_get`

Implement:

```text
attachment metadata GET
-> validate content_url origin
-> validate size ceiling
-> binary content GET
-> enforce actual byte ceiling
-> verify actual size
-> return metadata + MCP blob/binary content
```

Do not expose `content_url`.

Do not expose local paths.

Do not follow redirects.

## 12. Same-origin requirement

Attachment `content_url` must match configured Redmine base origin by:

```text
scheme
host
effective port
```

A cross-origin URL must fail before a second HTTP request is sent.

Add a deterministic test that asserts the fake cross-origin server/handler receives zero attachment-content requests.

## 13. Attachment size ceiling

Default:

```text
20971520 bytes
```

Reject metadata already above the configured ceiling.

Bound actual download independently.

Verify downloaded byte count matches Redmine metadata `filesize`.

Do not add a new error enum.

Use:

```text
invalid_upstream_response
```

for unsafe/mismatched attachment payloads.

## 14. Normalization

Unknown upstream JSON fields are ignored.

Required frozen facts with missing/wrong/malformed values fail as:

```text
invalid_upstream_response
```

Normalize timestamps to UTC `Z`.

Preserve date-only values as `YYYY-MM-DD`.

Preserve source text exactly.

## 15. Error mapping

Implement exactly:

```text
invalid local argument
-> invalid_argument

401
-> authentication_required

403
-> forbidden

404
-> not_found

408 / 429 / 5xx
DNS / connect / TLS
internal timeout
-> upstream_unavailable

malformed JSON
unexpected required shape
cross-origin attachment URL
redirect
attachment size violation
attachment size mismatch
unexpected 3xx / other unexpected 4xx
-> invalid_upstream_response
```

Caller cancellation propagates.

## 16. Logging/privacy

Add tests or deterministic capture proving:

- API key absent from logs;
- API key absent from error output;
- raw upstream body absent from normalized errors;
- private source text absent from ordinary error logs;
- attachment bytes absent from logs.

Logging may retain only minimal operational metadata.

## 17. Read-only structure

Prefer concrete methods such as:

```text
ListIssuesAsync
GetIssueAsync
ListQueriesAsync
GetAttachmentMetadataAsync
GetAttachmentContentAsync
```

A private shared GET helper is acceptable.

Do not add a general arbitrary method/path/body Redmine client interface that would create a raw REST escape hatch.

## 18. Required deterministic test matrix

At minimum add tests for:

### Auth / transport

1. API key appears only in `X-Redmine-API-Key`;
2. no Basic auth;
3. no API key in query;
4. no impersonation header;
5. only GET requests;
6. Redmine subpath resolution works;
7. redirect is not automatically followed.

### Issue listing

8. query selector mapping;
9. explicit IDs mapping includes `status_id=*`;
10. open status mapping;
11. closed status mapping;
12. all status mapping;
13. pagination mapping and max limit.

### Issue detail

14. approved include list only;
15. optional null normalization;
16. empty collection normalization;
17. single/multi custom-field normalization;
18. journal details normalization;
19. recursive child normalization.

### Queries

20. multi-page query retrieval;
21. non-progressing/malformed pagination rejected.

### Errors

22. 401 mapping;
23. 403 mapping;
24. 404 mapping;
25. 408 mapping;
26. 429 mapping;
27. 5xx mapping;
28. transport exception mapping;
29. timeout mapping;
30. caller cancellation propagation;
31. malformed JSON mapping;
32. malformed required shape mapping.

### Attachments

33. metadata + content success;
34. same-origin content URL success;
35. cross-origin content URL rejected with no second request;
36. redirect rejected;
37. metadata oversize rejected;
38. actual byte ceiling enforced;
39. size mismatch rejected;
40. returned bytes exactly match upstream bytes;
41. content URL not exposed;
42. local path not exposed.

### Privacy / stdio

43. API key absent from normalized error output;
44. API key absent from captured logs;
45. raw upstream body absent from errors;
46. attachment bytes absent from logs;
47. stdout remains MCP-protocol-only.

Equivalent test organization is acceptable if all behaviors are directly proven.

## 19. No live Redmine dependency

Tests must use synthetic `HttpMessageHandler` / fake upstream behavior.

Do not require:

```text
internet
real Redmine
real API key
production URL
private Issue content
```

## 20. Documentation updates

Archive:

```text
docs/scopes/scope-007-redmine-mcp-read-path-contract.md
docs/scopes/scope-007-implementation-brief.md
```

Update design/Ideas Registry only where needed to record Scope 007 as the read-path authority.

Do not rewrite unrelated baseline material.

## 21. Validation

Run:

```text
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Also run the RedmineMcp-focused test set if separately filterable.

Inspect:

```text
git status
git diff
git diff --check
```

Confirm:

- all tests pass;
- no real network dependency exists;
- no private/personal data exists;
- no mutation path exists;
- no generic REST tool/client escape hatch exists;
- no secret appears in fixtures/log outputs;
- only approved files changed.

## 22. Git boundary

The Implementation Agent may inspect, stage, and create an approved local commit.

The Implementation Agent must never:

- run `git push`;
- write to a Git remote;
- create/push tags;
- create releases;
- modify remote settings.

Remote push remains a manual Product Owner action.

## 23. Implementation report

Report:

1. files changed;
2. configuration structure;
3. HTTP client structure;
4. normalization DTO structure;
5. attachment trust/safety implementation;
6. exact error mapping;
7. test matrix and counts;
8. restore/build/test results;
9. RedmineMcp-focused test result;
10. public-safety/privacy checks;
11. local commit hash;
12. deviations/blockers/follow-up ideas;
13. confirmation that no push occurred.

## 24. Review sequence

After implementation:

1. GPT-5.6 Sol performs first architecture/scope/security review.
2. If blocking implementation issues exist, revise before independent review.
3. DeepSeek-V41-Flash performs independent HTTP/auth/attachment-security review.
4. GPT-5.6 Sol reconciles findings.
5. Product Owner receives final decision material.

The Independent Reviewer must audit the approved contract and implementation, not redesign the product.

## 25. Completion rule

Scope 007 is ready for first review only when:

- all four tools use the real GET-only read path;
- API-key header auth works without secret exposure;
- base URL validation works;
- redirects are disabled;
- same-origin attachment protection is enforced;
- attachment size is bounded;
- normalized DTOs are deterministic;
- error categories match the frozen contract;
- synthetic HTTP tests prove success and failure paths;
- no out-of-scope Analyzer/Snapshot/mutation functionality exists;
- restore/build/test pass;
- no push was performed.
