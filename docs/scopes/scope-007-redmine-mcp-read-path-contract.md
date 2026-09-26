# IssueHarbor V2 / 1.0.0 — Scope 007 RedmineMcp Read Path Contract

**Scope:** 007 — RedmineMcp Read Path\
**Status:** APPROVED / READY FOR IMPLEMENTATION\
**Architecture / Scope Lead:** GPT-5.6 Sol\
**Implementation Agent:** GPT-6 Luna\
**Independent Reviewer:** DeepSeek-V41-Flash\

## 1. Purpose

Implement the approved Scope 006 RedmineMcp 1.0 manifest as a real read-only Redmine HTTP integration.

Scope 007 freezes:

- Redmine API-key authentication;
- GET-only HTTP behavior;
- connection configuration;
- exact normalized output contracts;
- attachment download safety;
- deterministic upstream error mapping;
- timeout behavior;
- read-only implementation boundaries;
- synthetic HTTP integration tests.

Scope 007 does not implement Analyzer Snapshot acquisition, attachment-retention policy, Request orchestration, source-repository context, retry, mutation, or generic Redmine REST passthrough.

## 2. Approved tool surface

The implemented read path serves exactly the four tools frozen by Scope 006:

```text
redmine_issues_list
redmine_issue_get
redmine_queries_list
redmine_attachment_get
```

No additional tool is authorized.

## 3. Redmine authentication

Use Redmine API-key header authentication only:

```text
X-Redmine-API-Key: <secret>
```

Do not use:

```text
Basic username/password
query-string API keys
cookies as configured authentication
X-Redmine-Switch-User
impersonation
```

The API key is a secret and must never appear in:

```text
tool input
tool output
logs
exception messages
diagnostic stdout
HTTP query strings
```

## 4. Server-side configuration

Use server-side configuration for the Redmine connection.

Approved configuration contract:

```text
ISSUEHARBOR_REDMINE_BASE_URL
ISSUEHARBOR_REDMINE_API_KEY
ISSUEHARBOR_REDMINE_TIMEOUT_SECONDS
ISSUEHARBOR_REDMINE_MAX_ATTACHMENT_BYTES
```

Defaults:

```text
ISSUEHARBOR_REDMINE_TIMEOUT_SECONDS = 30
ISSUEHARBOR_REDMINE_MAX_ATTACHMENT_BYTES = 20971520
```

The attachment default is 20 MiB.

The exact .NET configuration binding mechanism is an implementation detail, but public tool inputs must never accept these settings.

## 5. Base URL validation

`ISSUEHARBOR_REDMINE_BASE_URL` must:

- be an absolute URL;
- use `http` or `https`;
- allow a Redmine deployment subpath;
- reject userinfo;
- reject query;
- reject fragment.

Valid conceptual examples:

```text
https://redmine.example.invalid/
https://redmine.example.invalid/redmine/
```

Do not provide any option that disables TLS certificate validation.

## 6. HTTP baseline

All Redmine requests are:

```text
GET only
Accept: application/json
X-Redmine-API-Key: <secret>
```

No mutation HTTP method is used.

Automatic redirect following is disabled.

Scope 007 introduces no automatic retry.

Caller cancellation propagates as cancellation and must not be relabeled as an upstream failure.

Internal timeout and transport failures are mapped according to the frozen error contract.

## 7. Timeout behavior

Default request timeout:

```text
30 seconds
```

A positive configured timeout may override the default.

Internal timeout is mapped to:

```text
upstream_unavailable
```

Caller-triggered cancellation remains cancellation.

## 8. `redmine_issues_list`

### 8.1 Query mapping

#### Saved query selector

```text
query_id = N
```

maps to:

```text
GET issues.json?query_id=N&offset=<offset>&limit=<limit>
```

#### Explicit Issue IDs

```text
issue_ids = [a,b,...]
```

maps to:

```text
GET issues.json?issue_id=a,b,...&status_id=*&offset=<offset>&limit=<limit>
```

`status_id=*` is required so explicit IDs can resolve closed as well as open Issues.

#### Status selector

```text
status = open
```

maps to:

```text
status_id=open
```

```text
status = closed
```

maps to:

```text
status_id=closed
```

```text
status = all
```

maps to:

```text
status_id=*
```

### 8.2 Limits

Enforce:

```text
1 <= issue_ids.Count <= 100
offset >= 0
1 <= limit <= 100
```

Scope 007 returns one Redmine page for `redmine_issues_list`.

Complete multi-page Request selection orchestration belongs to a later Analyzer scope.

### 8.3 Normalized output

The normalized response contains:

```text
total_count
offset
limit
issues[]
```

Each Issue summary contains:

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

Structured named identities use stable nested objects where applicable:

```text
id
name
```

`assigned_to` is null when absent.

Do not include journals or attachment content in this tool.

## 9. `redmine_issue_get`

### 9.1 Request mapping

Use:

```text
GET issues/<issue_id>.json?include=children,attachments,relations,journals
```

Do not request:

```text
changesets
watchers
allowed_statuses
reactions
```

unless a later approved scope changes the contract.

### 9.2 Normalized Issue output

The exact normalized output contains the following factual fields where applicable:

```text
issue_id

project
tracker
status
priority
author
assigned_to
category
fixed_version
parent_issue_id

subject
description
start_date
due_date
done_ratio
is_private
estimated_hours

created_on
updated_on
closed_on

custom_fields[]
journals[]
attachments[]
relations[]
children[]
```

Optional scalar/object facts are null when legitimately absent.

Collections are arrays and are empty when legitimately absent.

## 10. Named-reference shape

Where Redmine represents a referenced object by identity and label, normalize to:

```json
{
  "id": 1,
  "name": "Synthetic"
}
```

Do not add inferred fields.

## 11. Custom fields normalization

Normalize Redmine custom fields to:

```text
id
name
multiple
values[]
```

Single-valued custom fields are normalized to a one-element `values[]` array when a value exists.

Multi-valued custom fields remain ordered arrays.

Absent/no-value custom fields use an empty array.

Do not interpret, translate, or classify custom-field values.

## 12. Journals normalization

Each journal contains:

```text
id
user
notes
created_on
updated_on
updated_by
private_notes
details[]
```

Optional fields are null where legitimately absent.

Each journal detail contains:

```text
property
name
old_value
new_value
```

`old_value` and `new_value` may normalize as:

```text
string
string[]
null
```

Preserve factual source content. Do not summarize or rewrite notes.

## 13. Attachments metadata normalization

Attachment metadata contains:

```text
id
filename
filesize
content_type
description
author
created_on
```

Do not expose Redmine credentials.

The normalized output of `redmine_issue_get` contains attachment metadata only, not attachment bytes.

## 14. Relations normalization

Normalize Issue relations as factual structured records containing, at minimum, the Redmine-provided relationship identity and relation type plus related Issue identities needed to preserve the source relationship.

Do not infer relationship semantics beyond Redmine's provided values.

Exact names must remain stable in the implementation contract/tests.

## 15. Children normalization

Children are normalized recursively as:

```text
issue_id
tracker
subject
children[]
```

Do not enrich children with speculative fields.

## 16. `redmine_queries_list`

### 16.1 Request behavior

Read visible saved queries from Redmine.

The MCP tool has no business pagination input.

RedmineMcp internally follows Redmine query-list pagination until the complete visible query set has been retrieved.

Use page sizes no greater than Redmine's supported maximum.

### 16.2 Pagination safety

Pagination must terminate deterministically.

If upstream pagination metadata:

- does not progress;
- contradicts itself;
- is malformed;
- would cause an infinite loop;

return:

```text
invalid_upstream_response
```

### 16.3 Normalized output

Return:

```text
queries[]
```

Each entry contains:

```text
id
name
is_public
project_id
```

`project_id` may be null for global queries.

## 17. `redmine_attachment_get`

### 17.1 Metadata request

First request:

```text
GET attachments/<attachment_id>.json
```

Normalize metadata to:

```text
id
filename
filesize
content_type
description
author
created_on
```

### 17.2 Content URL trust rule

The upstream `content_url` must be validated before download.

Its effective origin must match the configured Redmine base origin:

```text
scheme
host
effective port
```

must all match.

If `content_url` is cross-origin:

```text
invalid_upstream_response
```

Do not issue the second request.

Do not send the API key to another origin.

### 17.3 Redirects

Automatic redirects are disabled.

Unexpected redirect responses are not followed and are mapped to:

```text
invalid_upstream_response
```

### 17.4 Binary content

After same-origin validation, download the attachment bytes.

Return:

```text
structured attachment metadata
+
MCP-supported binary/blob content
```

Do not expose:

```text
content_url
local temp-file path
Redmine credentials
```

## 18. Attachment safety ceiling

Default maximum accepted attachment size:

```text
20971520 bytes
```

or 20 MiB.

If metadata `filesize` exceeds the configured maximum:

```text
invalid_upstream_response
```

The actual download must also be bounded independently of metadata.

The implementation must not trust metadata size alone.

After download, verify:

```text
actual byte count == metadata filesize
```

If `Content-Length` exists, it must not contradict the safe accepted size contract.

Any unsafe/mismatched attachment response is:

```text
invalid_upstream_response
```

Scope 007 does not add a new attachment-specific error enum.

## 19. Redmine JSON normalization

Use JSON responses.

Unknown additional upstream JSON fields are ignored.

This allows Redmine to add fields without breaking the frozen 1.0 normalized output contract.

However, if a required fact for a frozen normalized output is:

- missing;
- the wrong JSON type;
- malformed;
- an invalid timestamp;

return:

```text
invalid_upstream_response
```

Do not silently fabricate required values.

## 20. Time normalization

Timestamp values are parsed as timestamps and emitted in deterministic UTC `Z` form.

Date-only values remain date-only:

```text
YYYY-MM-DD
```

Do not apply local timezone conversion in RedmineMcp.

## 21. Source fidelity

Source text remains source text.

Do not:

- translate;
- summarize;
- rewrite;
- classify;
- infer priority;
- render HTML as a substitute for source text;
- perform AI transformation.

This applies to:

```text
subject
description
journal notes
custom-field values
attachment description
```

## 22. Error categories

The frozen Scope 006 categories remain:

```text
not_found
authentication_required
forbidden
upstream_unavailable
invalid_upstream_response
invalid_argument
```

No new error category is added in Scope 007.

## 23. Error mapping

Map deterministically:

```text
local input/schema violation
-> invalid_argument

HTTP 401
-> authentication_required

HTTP 403
-> forbidden

HTTP 404
-> not_found

HTTP 408
HTTP 429
HTTP 5xx
DNS/connect failure
TLS failure
internal timeout
-> upstream_unavailable

malformed JSON
unexpected JSON shape
unsafe/cross-origin attachment content_url
redirect response
attachment size violation
attachment size mismatch
unexpected upstream 3xx
other unexpected 4xx
-> invalid_upstream_response
```

Caller cancellation is propagated as cancellation.

## 24. Error privacy

Errors must not expose:

```text
API key
authorization headers
cookies
raw HTML error pages
raw upstream response bodies
private stack traces
attachment bytes
```

Tool errors return the normalized error contract only.

## 25. Logging boundary

Logging may include:

```text
operation name
HTTP status code
duration
normalized error category
```

Logging must not include:

```text
API key
full request headers
full response headers when secret-bearing
raw response body
attachment bytes
Issue description
journal notes
custom-field source text
raw content_url
```

Avoid unnecessary logging of full private Redmine hostnames.

## 26. GET-only implementation structure

The Redmine HTTP implementation must expose only concrete approved read operations, such as:

```text
ListIssuesAsync
GetIssueAsync
ListQueriesAsync
GetAttachmentMetadataAsync
GetAttachmentContentAsync
```

A private shared GET helper is acceptable.

Do not expose a general-purpose public/internal abstraction that allows arbitrary caller-controlled:

```text
HTTP method
path
URL
body
```

in a way that becomes a raw Redmine REST escape hatch.

No POST/PUT/PATCH/DELETE behavior is implemented.

## 27. MCP tool integration

The four Scope 006 tools are wired to the real Scope 007 read path.

Keep:

```text
stdio reference transport
stdout = MCP protocol only
stderr = diagnostics
```

The tools remain read-only/non-destructive.

## 28. Testing baseline

Use synthetic HTTP handlers/fakes.

Scope 007 tests must not require:

```text
real Redmine
internet access
production credentials
private hostnames
private Issue content
```

## 29. Minimum deterministic test matrix

At minimum verify:

### Authentication and requests

- API key is sent only as `X-Redmine-API-Key`;
- no Basic authorization header;
- no query-string API key;
- no impersonation header;
- Redmine base URL subpath is preserved;
- only GET is used.

### Issue listing

- saved-query selector maps correctly;
- explicit Issue IDs add `status_id=*`;
- `open` maps correctly;
- `closed` maps correctly;
- `all` maps to `*`;
- offset/limit map correctly;
- limit does not exceed 100.

### Issue detail

- only approved includes are requested;
- no excluded include is requested;
- null optional facts normalize correctly;
- empty collections normalize to arrays;
- multivalue custom fields normalize to `values[]`;
- journal details preserve factual old/new values;
- child recursion is normalized deterministically.

### Query pagination

- visible queries paginate to completion;
- malformed/non-progressing pagination is rejected.

### Errors

- 401 -> `authentication_required`;
- 403 -> `forbidden`;
- 404 -> `not_found`;
- 408/429/5xx -> `upstream_unavailable`;
- transport failure -> `upstream_unavailable`;
- internal timeout -> `upstream_unavailable`;
- malformed JSON -> `invalid_upstream_response`;
- invalid required shape -> `invalid_upstream_response`;
- unexpected redirect -> `invalid_upstream_response`;
- caller cancellation remains cancellation.

### Attachment safety

- metadata then binary content retrieval succeeds;
- same-origin content URL succeeds;
- cross-origin content URL is rejected before second request;
- redirect is not followed;
- oversized metadata is rejected;
- actual streamed/downloaded bytes are bounded;
- actual-size mismatch is rejected;
- returned binary bytes exactly equal upstream bytes;
- `content_url` is not surfaced;
- no temporary local path is surfaced.

### Privacy

- secret API key does not appear in error output;
- secret API key does not appear in captured logs;
- raw upstream body/private source text is not emitted through errors;
- stdout remains MCP-only.

## 30. Explicitly deferred

Scope 007 does not implement:

- Analyzer Snapshot JSON;
- Snapshot acquisition sequencing;
- attachment retention/selection policy;
- source-repository context;
- Request orchestration;
- Analyzer execution;
- provider integration;
- retry;
- credential-management UI;
- secret retrieval API;
- backup/restore;
- mutation;
- generic Redmine REST access;
- live production Redmine integration tests.

## 31. Security impact

Scope 007 introduces a real secret and upstream trust boundary.

The approved controls are:

- API key server-side only;
- header authentication only;
- no impersonation;
- GET-only;
- redirects disabled;
- same-origin attachment validation;
- attachment byte ceiling;
- no raw REST escape hatch;
- no secret/body logging;
- deterministic error normalization.

## 32. Acceptance rule

The implementation must make it deterministic to answer:

- Which Redmine origin is trusted?
- How is the API key transmitted?
- Can a tool mutate Redmine? (No.)
- Can a tool send the API key to another origin? (No.)
- Can redirects silently move a credentialed request? (No.)
- Can attachment bytes exceed the configured safety ceiling? (No.)
- Are Redmine facts normalized without AI transformation? (Yes.)
- Are upstream failures mapped to the frozen error categories? (Yes.)
- Can malformed upstream data become a successful tool result? (No.)

Any change to these trust, authentication, read-only, attachment, or error semantics requires an explicit approved scope decision.
