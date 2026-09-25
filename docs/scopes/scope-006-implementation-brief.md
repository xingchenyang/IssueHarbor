# IssueHarbor V2 / 1.0.0 — Scope 006 Implementation Brief

**Scope:** 006 — RedmineMcp 1.0 Manifest
**Status:** APPROVED FOR IMPLEMENTATION
**Architecture / Scope Lead:** GPT-5.6 Sol
**Implementation Agent:** GPT-6 Luna
**Independent Reviewer:** Not assigned

## 1. Functional goal

Implement the approved RedmineMcp 1.0 manifest as deterministic MCP tool declarations/contracts and tests.

Do not implement real Redmine HTTP access.

## 2. Authoritative contract

Use:

```text
docs/scopes/scope-006-redmine-mcp-manifest-contract.md
```

as the semantic authority.

Also preserve the frozen V2 design plan and relevant prior scope contracts.

Do not add tools or widen tool semantics.

## 3. Required 1.0 tool set

The exposed RedmineMcp tool manifest must contain exactly:

```text
redmine_issues_list
redmine_issue_get
redmine_queries_list
redmine_attachment_get
```

No additional Redmine tool is authorized.

## 4. In-scope implementation

Implement the smallest concrete code needed to declare and machine-test:

- tool names;
- tool descriptions/purpose;
- input schemas/contracts;
- high-level structured result declarations where supported;
- read-only/non-destructive annotations where supported;
- deterministic selector validation for `redmine_issues_list`;
- error-contract/category declarations if represented in the current MCP layer;
- stdio-safe tool registration behavior.

Placeholder/synthetic handler behavior may be used only if needed to make manifest registration testable.

Do not simulate a fake product feature beyond manifest verification.

## 5. `redmine_issues_list` input contract

Support exactly one selector per call:

- `query_id`;
- `issue_ids`;
- `status`.

Allowed `status` values:

```text
open
closed
all
```

Also support:

```text
offset
limit
```

Rules:

- exactly one selector is required;
- multiple selectors are invalid;
- empty explicit `issue_ids` is invalid;
- `offset` must be non-negative;
- `limit` must be positive and must not exceed the Redmine single-page maximum used by this contract.

Do not implement a custom filter DSL.

## 6. Other input contracts

### `redmine_issue_get`

Required:

```text
issue_id
```

### `redmine_queries_list`

No business input.

### `redmine_attachment_get`

Required:

```text
attachment_id
```

IDs must use integer-compatible input matching Redmine identities.

## 7. Output contract scope

Scope 006 should declare or document the approved high-level output shapes but must not prematurely implement full Redmine DTO mapping.

At minimum preserve the contract distinction:

### `redmine_issues_list`

Lightweight Issue summaries only.

### `redmine_issue_get`

Full Issue factual context including:

- core fields;
- custom fields;
- journals;
- attachment metadata;
- relations;
- children.

### `redmine_queries_list`

Visible saved-query summaries.

### `redmine_attachment_get`

Attachment metadata plus MCP-supported binary/blob content.

If the current SDK requires concrete result types for registration, keep them minimal and aligned with the approved contract without adding speculative fields.

## 8. Read-only enforcement

Use the MCP SDK's read-only/non-destructive annotations if supported by the installed SDK.

Regardless of annotation support:

- do not add any mutation handlers;
- do not add arbitrary REST helper tools;
- do not add generic method/path passthrough;
- do not accept credentials in tool arguments.

## 9. No Redmine network implementation

Do not add:

- HttpClient Redmine calls;
- Redmine base URL settings;
- API key settings;
- authentication handlers;
- live Redmine probes;
- retry logic;
- attachment HTTP downloads.

Those belong to Scope 007.

No network access should be required by Scope 006 tests.

## 10. Stdio discipline

Preserve:

```text
stdout = MCP protocol only
stderr = diagnostics/logging
```

Add or retain deterministic tests where practical to ensure tool registration/startup does not emit ordinary diagnostics to stdout.

Do not add console banners.

## 11. Required deterministic tests

At minimum prove:

1. exactly four approved tools are registered;
2. the four tool names match the approved names exactly;
3. no mutation/generic REST tool is registered;
4. all tools are declared read-only/non-destructive when the SDK exposes such annotations;
5. `redmine_issues_list` accepts `query_id` alone;
6. it accepts explicit `issue_ids` alone;
7. it accepts each allowed status value;
8. it rejects no selector;
9. it rejects multiple selectors;
10. it rejects invalid status;
11. it rejects invalid pagination;
12. `redmine_issue_get` requires a valid integer Issue ID;
13. `redmine_attachment_get` requires a valid integer attachment ID;
14. `redmine_queries_list` requires no business arguments;
15. no tool input schema exposes credential/base-URL fields;
16. manifest/registration tests require no Redmine/network access.

Where SDK-level schema inspection is available, assert against the actual registered MCP tool schemas rather than only helper methods.

## 12. Documentation updates

Archive:

```text
docs/scopes/scope-006-redmine-mcp-manifest-contract.md
docs/scopes/scope-006-implementation-brief.md
```

Update the design plan and Ideas Registry only where necessary to record the MCP 1.0 manifest as frozen.

Do not rewrite unrelated baseline sections.

## 13. Explicitly out of scope

Do not implement:

- Redmine HTTP access;
- authentication;
- secret configuration;
- exact Redmine response DTO mapping;
- attachment download implementation;
- timeout/retry behavior;
- Snapshot acquisition;
- Analyzer Request selection orchestration;
- source-repository context;
- mutation tools;
- raw REST passthrough;
- additional discovery/catalog tools;
- HTTP MCP transport.

## 14. Validation

Run:

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

- all manifest tests pass;
- no Redmine/network access is required;
- no credential field appears in public tool input contracts;
- exactly four approved tools are exposed;
- no mutation/generic passthrough tool exists;
- no private/personal content was added;
- no unrelated files changed.

## 15. Git boundary

The Implementation Agent may inspect, stage, and create the approved local commit.

The Implementation Agent must never:

- run `git push`;
- write to a Git remote;
- create/push tags;
- create releases;
- modify remote settings.

Remote push remains a manual Product Owner action.

## 16. Implementation report

Report:

1. files changed;
2. exact tool-registration structure;
3. MCP SDK capabilities/annotations used;
4. input-schema/validation approach;
5. exact deterministic tests added;
6. confirmation that no HTTP/Redmine network implementation was introduced;
7. restore/build/test results;
8. public-safety checks;
9. local commit hash;
10. deviations/blockers/follow-up ideas;
11. confirmation that no push occurred.

## 17. Review strategy

Scope 006 does not require an Independent Reviewer unless implementation reveals a material contract/security ambiguity.

GPT-5.6 Sol performs the normal post-implementation review.

Credentials, HTTP behavior, attachment bytes, and upstream failure handling will be assessed in Scope 007.

## 18. Completion rule

Scope 006 is ready for review only when:

- the approved contract is archived;
- exactly four approved tools are machine-testably registered;
- selector validation is deterministic;
- read-only semantics are represented where supported;
- no credentials appear in tool inputs;
- no mutation or generic REST passthrough exists;
- tests require no Redmine/network access;
- restore/build/test pass;
- no push was performed.
