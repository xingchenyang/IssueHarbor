# IssueHarbor V2 / 1.0.0 — Scope 006 RedmineMcp 1.0 Manifest Contract

**Scope:** 006 — RedmineMcp 1.0 Manifest
**Status:** APPROVED / READY FOR IMPLEMENTATION
**Architecture / Scope Lead:** GPT-5.6 Sol
**Implementation Agent:** GPT-6 Luna
**Independent Reviewer:** Not assigned

## 1. Purpose

Freeze the public IssueHarbor RedmineMcp 1.0 read-only capability surface.

RedmineMcp is an independently useful read-only MCP server that exposes only the Redmine capabilities required by IssueHarbor workflows.

It is not a generic Redmine REST wrapper and does not expose mutation.

This scope freezes tool names, intent, high-level input/output contracts, permission boundaries, and error categories.

It does not implement Redmine HTTP access, authentication configuration, exact normalized DTO mapping, attachment size policy, or Snapshot acquisition.

## 2. RedmineMcp 1.0 tool manifest

The complete 1.0 manifest contains exactly these four tools:

```text
redmine_issues_list
redmine_issue_get
redmine_queries_list
redmine_attachment_get
```

No other Redmine tools are part of the 1.0 manifest.

## 3. `redmine_issues_list`

### Purpose

Discover ordered Issue membership candidates without freezing Snapshot content.

This tool supports Request selection/discovery only.

It returns lightweight Issue summaries and must not return journals or attachment content.

### Input

Exactly one selector mode is used per call:

#### Saved query mode

```json
{
  "query_id": 84,
  "offset": 0,
  "limit": 100
}
```

#### Explicit Issue ID mode

```json
{
  "issue_ids": [1605, 22382],
  "offset": 0,
  "limit": 100
}
```

#### Status mode

```json
{
  "status": "open",
  "offset": 0,
  "limit": 100
}
```

Allowed `status` values:

```text
open
closed
all
```

Selector rules:

- exactly one of `query_id`, `issue_ids`, or `status` must be supplied;
- selectors must not be combined;
- pagination uses `offset` and `limit`;
- `limit` must not exceed Redmine's supported single-page maximum.

Scope 006 does not introduce a custom IssueHarbor filter DSL.

### Output

The output is structured and stable.

Each Issue summary contains the normalized factual fields required for discovery, including at minimum:

```text
issue_id
project
tracker
status
priority
subject
author
assigned_to
created_on
updated_on
```

Nested identity/name objects may be used where appropriate.

Optional facts such as `assigned_to` are represented explicitly as null when absent.

No journals or attachment content are returned by this tool.

## 4. `redmine_issue_get`

### Purpose

Return the current factual state of one Redmine Issue for later Snapshot acquisition.

### Input

```json
{
  "issue_id": 22382
}
```

### Output

The normalized structured output includes:

```text
issue core fields
custom fields
journals
attachment metadata
relations
children
```

This tool does not include:

```text
allowed_statuses
watchers
reactions
changesets
```

unless a later approved scope changes the contract.

`changesets` are intentionally not used as a replacement for IssueHarbor source-repository provenance.

### Timing semantics

The intended higher-level workflow remains:

```text
Request accepted
-> issue selection resolved
-> ordered Issue membership frozen

later, when an Issue begins processing
-> redmine_issue_get
-> current factual state read
-> Snapshot frozen
```

The tool itself does not create Snapshots.

## 5. `redmine_queries_list`

### Purpose

Discover saved Redmine queries visible to the configured credential.

### Input

No business input.

### Output

Return structured entries containing at minimum:

```text
id
name
is_public
project_id
```

where facts may be null if Redmine legitimately omits them.

This tool exists to support discoverable use of `query_id` by `redmine_issues_list`.

## 6. `redmine_attachment_get`

### Purpose

Read one Redmine attachment using RedmineMcp-owned authentication.

### Input

```json
{
  "attachment_id": 6243
}
```

### Output

Return:

```text
structured attachment metadata
+
attachment binary content
```

Attachment metadata includes at minimum:

```text
id
filename
filesize
content_type
description
author
created_on
```

Binary content is returned through an MCP-supported embedded binary/blob content mechanism.

Do not return a local temporary-file path.

Do not require Analyzer or another MCP client to receive Redmine credentials or independently fetch a Redmine `content_url`.

Attachment-size limits and retention decisions are deferred.

## 7. Read-only boundary

All four tools are read-only and non-destructive.

RedmineMcp 1.0 must not expose tools for:

```text
issue creation
issue update
issue deletion
adding notes
attachment deletion
project mutation
user mutation
workflow mutation
```

Read-only MCP annotations should be applied where supported, but annotations are not the security boundary.

The implementation itself must not expose a mutation path reachable by the 1.0 tools.

## 8. No arbitrary REST escape hatch

Do not expose generic tools such as:

```text
redmine_raw_request
redmine_rest
redmine_get_url
redmine_http
```

No tool may accept arbitrary HTTP method/path/body inputs.

This prevents bypassing the approved read-only capability surface.

## 9. Stable structured output

Tools must return structured normalized content.

Do not return Redmine's raw JSON as an opaque string.

The mapping is deterministic:

```text
Redmine factual representation
-> deterministic normalization
-> RedmineMcp structured contract
```

RedmineMcp performs no AI interpretation.

It must not:

- summarize;
- classify;
- translate;
- infer priority;
- rewrite journal text;
- generate recommendations.

Source text is preserved as factual source content.

## 10. Null and collection behavior

Legitimately absent optional scalar/object facts are represented consistently as null.

Examples:

```text
assigned_to = null
fixed_version = null
parent = null
```

Collections are represented consistently as arrays, including empty arrays when no values exist:

```text
journals = []
attachments = []
relations = []
children = []
```

The exact normalized DTO field set is finalized in Scope 007.

## 11. Credential boundary

No tool input may contain:

```text
api_key
password
username
authorization header
cookie
base_url
```

No tool output may expose credentials.

Redmine connection details and authentication belong to server configuration.

Clients call tools with business identifiers only.

## 12. Permission boundary

RedmineMcp returns only data visible to the configured Redmine credential.

It does not elevate permission and does not impersonate another Redmine user.

User impersonation is not part of IssueHarbor 1.0.

If the configured credential cannot see data, RedmineMcp cannot return that data.

## 13. Error categories

Scope 006 freezes these business/upstream error categories:

```text
not_found
authentication_required
forbidden
upstream_unavailable
invalid_upstream_response
invalid_argument
```

`invalid_argument` covers tool/input contract violations.

Scope 007 will map concrete Redmine/HTTP conditions to these categories.

Errors must not expose:

- API keys;
- authorization headers;
- cookies;
- private credentials;
- raw server-side stack traces;
- unfiltered HTML error pages.

Scope 006 does not define retry behavior.

## 14. Transport boundary

The IssueHarbor 1.0 reference transport remains MCP over `stdio`.

Rules:

```text
stdout = MCP protocol only
stderr = diagnostics/logging
```

Ordinary diagnostic text must never be written to stdout.

Scope 006 does not add an HTTP MCP transport.

## 15. Independently useful server

RedmineMcp must remain usable by a compatible MCP client without Analyzer.

A compatible client may independently:

```text
list visible saved queries
list Issue summaries
read one complete Issue
read one attachment
```

Independent usefulness does not mean complete Redmine API coverage.

## 16. Selection versus Snapshot boundary

`redmine_issues_list` and `redmine_issue_get` have intentionally different responsibilities.

`redmine_issues_list`:

```text
selection/discovery
-> lightweight facts
-> membership candidates
```

`redmine_issue_get`:

```text
processing-time factual read
-> full Issue context required by later Snapshot acquisition
```

This separation preserves the frozen domain rule that Request membership is resolved/frozen when the Request is accepted while Snapshot content is frozen only when the Issue begins processing.

## 17. Machine-readable manifest authority

The implementation must expose a deterministic machine-testable manifest for the four approved tools.

The manifest/tests must prove:

- exactly four approved tool names;
- no mutation tool;
- no arbitrary REST passthrough tool;
- declared input shape for each tool;
- selector exclusivity for `redmine_issues_list`;
- declared read-only semantics where supported by the SDK;
- stdio protocol output is not polluted by diagnostic stdout.

Scope 006 does not freeze a specific MCP protocol revision number.

## 18. Explicitly deferred to Scope 007

Scope 006 does not decide or implement:

- Redmine HTTP client behavior;
- Redmine base URL configuration;
- authentication mechanism/configuration keys;
- concrete credential storage;
- exact normalized C# DTO schemas;
- exact Redmine JSON-to-contract mapping;
- HTTP status-to-error-category mapping;
- HTTP timeouts;
- retry behavior;
- attachment size limits;
- attachment streaming/buffering implementation;
- Redmine version compatibility probes;
- MCP protocol-version mechanics;
- live/synthetic Redmine HTTP integration tests.

## 19. Explicitly deferred to later Analyzer scopes

Scope 006 does not decide or implement:

- which attachments Analyzer retains;
- Snapshot JSON shape;
- Snapshot acquisition sequencing;
- source-repository provenance;
- Request selection UI/CLI;
- complete multi-page Request selection orchestration;
- Analyzer execution behavior.

## 20. Security and privacy

All documentation, fixtures, schemas, and tests must use synthetic/public-safe values.

Do not commit:

- real Redmine issue content;
- real credentials;
- production URLs;
- private hostnames;
- private attachment content.

## 21. Acceptance rule

The implemented manifest must make it deterministic to answer:

- What tools does RedmineMcp 1.0 expose?
- Is each tool read-only?
- Can a client perform arbitrary Redmine REST requests? (No.)
- Can a tool receive or return Redmine credentials? (No.)
- Which tool is used for Issue selection? (`redmine_issues_list`.)
- Which tool is used for full Issue factual retrieval? (`redmine_issue_get`.)
- How are visible saved queries discovered? (`redmine_queries_list`.)
- How is attachment content obtained without exposing credentials? (`redmine_attachment_get`.)
- Are tool outputs deterministic factual structures rather than AI transformations? (Yes.)

Any future expansion of the manifest requires an explicit approved scope decision.
