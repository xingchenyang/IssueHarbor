# IssueHarbor V2 / 1.0.0 — Formal Design Plan

Status: **FROZEN BASELINE FOR NEW CONVERSATION**  
Purpose: Authoritative starting point for IssueHarbor V2 / first stable public `1.0.0`.

## 1. Reset decision

Analyzer V2 is a clean product generation.

- Historical Analyzer `0.x` remains in the current **private** repository as engineering history.
- The historical Analyzer `0.x` project has now been formally closed/archived and is reference-only.
- The private repository ends with one final documentation/archive commit.
- The last real software tag remains the last actual Analyzer `0.x` release, currently expected to be `analyzer-0.4.8`.
- No extra archival release tag is required.
- The old repository stays private and may retain real internal names, paths, logs, historical evidence, and mixed-language material.
- Public V2 starts in a **new repository with new Git history**.
- No Git history sanitization or transplant from the private repository.
- No V1 -> V2 migration, artifact migration, backup restore compatibility, dual-state compatibility, or in-place upgrade.
- Existing `0.x` production can be stopped/archived and later deleted after V2 proves itself.

> **V2 inherits lessons, not V1 state.**

## 2. Product identity

The public project name is **IssueHarbor**.

The name is intentionally independent from Redmine, but the product implementation is not pretending to be universal:

- IssueHarbor V2 / 1.0 supports **Redmine only**.
- Redmine concepts may be used directly where they make the product clearer and simpler.
- Do not introduce a generic issue-source abstraction, plugin framework, or speculative adapter hierarchy for hypothetical future platforms.
- If Redmine is eventually replaced in the real environment, treat that as a new product-generation design problem: archive the Redmine generation when appropriate, distill the useful concepts/lessons/tooling, and design against the new platform's real semantics.
- Future portability is not a reason to make today's Redmine implementation harder to understand or maintain.

> **The project name leaves room for the future; the implementation serves the real Redmine need of today.**

### 2.1 RedmineMcp

A deliberately read-only MCP server for bounded Redmine context access.

- Useful independently from Analyzer.
- Reference transport for 1.0: `stdio`.
- Not intended to expose the full Redmine REST API.
- Tool selection is driven by real AI analysis workflows, not endpoint completeness.
- Redmine mutation capability is absent from the 1.x contract.

> **RedmineMcp 1.x is read-only by design. Any future Redmine mutation capability would require a new explicit product and security decision; it must not appear as a hidden switch or configuration mode inside the 1.x contract.**

### 2.2 Analyzer

> **A self-hosted analysis service that runs reproducible AI analyses over Redmine Issues, preserves provenance, and lets operators compare results across models.**

Analyzer is an analysis and recommendation system, not a Redmine operator, Git operator, or autonomous coding agent.

## 3. Functional-first design rule

All future development follows this order:

1. Identify the real user workflow.
2. Identify what Redmine / Git / the provider already does.
3. Define the minimum useful product contract.
4. Evaluate complexity, security, AI cost, maintenance cost, and compatibility impact.
5. Classify the idea as Current Scope, Roadmap, Ideas Registry, or Rejected.
6. Only then design and implement.

> **Start from functional user workflows, then design the simplest technical model that expresses them cleanly. Do not invent user workflows to justify a technically elegant abstraction.**

Any “while we are here, we could also…” idea is out of the current scope by default until separately evaluated.

Implementation agents such as Codex/Luna execute an approved scope; they do not define product scope.

## 4. Main workflows

### 4.1 Interactive

```text
Codex / DSH Web
    +
RedmineMcp
```

- one Issue or a small related set;
- live human + AI work;
- no Analyzer state required;
- suited to urgent investigation or coding work.

### 4.2 Durable / batch

```text
Analyzer
    +
RedmineMcp
    +
AI provider/profile
```

- many Issues;
- unattended work;
- NAS-hosted;
- persisted results/history/provenance;
- model comparison;
- Web UI;
- externally scheduled nightly execution.

RedmineMcp remains independently useful. Analyzer must not interpose in the interactive MCP workflow.

## 5. Redmine selection model

Analyzer supports two distinct selection sources:

1. Redmine Saved Query.
2. Analyzer-local filter.

Do not freely overlay competing filter authorities.

### 5.1 Local filter — 1.0 baseline

- `project_id`
- `include_subprojects`
- explicit `status`
- additional fields only when justified by a real use case

Do not introduce speculative filter algebra such as `subproject_depth`, arbitrary subtree exclusions, or generic query-expression engines.

At Request creation, selection resolves to an ordered Issue ID set and that membership is frozen.

## 6. Core domain model

### 6.1 Issue

Stable identity: Redmine `issue_id` only.

### 6.2 Snapshot

Immutable factual analysis input captured from a point in time. It may include:

- Redmine Issue content and relevant metadata;
- journals/history;
- selected retained attachment context;
- source repository revision/context when applicable;
- hashes/provenance needed to reconstruct exact input.

### 6.3 Analysis Contract

Defines what analysis should produce, including:

- analysis instructions;
- structured result schema;
- report language.

`report_language` is functionally part of execution but is **not** part of the factual Snapshot.

### 6.4 Execution Profile

Defines how the Analysis Contract is executed, for example:

- Luna High
- Sol High
- DeepSeek Flash
- DeepSeek Pro

May define provider, model, reasoning mode, timeout, and other execution settings.

Profiles are configuration; the resolved profile/provenance is recorded on the Run.

### 6.5 Run

One execution of:

```text
Snapshot + Analysis Contract + Execution Profile
```

Each successful Run retains:

- structured JSON result;
- human-readable Markdown report.

### 6.6 Request

One manual or scheduled batch invocation.

At creation:

- selection resolves;
- ordered Issue membership is frozen.

When an Issue begins processing:

- create/freeze its Snapshot;
- reuse it across profiles intended for strict comparison.

### 6.7 Request Item

Internal durable execution unit:

```text
Issue × Profile
```

It is not the primary Web UI model.

UI hierarchy:

```text
Request
  -> Issue
      -> Snapshot
          -> Runs / Models
```

## 7. Re-analysis and comparison

### 7.1 Automatic batch behavior

- Issue ID never analyzed -> create Snapshot -> analyze.
- Issue ID already analyzed -> do **not** automatically re-analyze, regardless of later Redmine changes.

No fingerprint-driven automatic re-analysis in 1.0.

### 7.2 Re-analyze

```text
Re-analyze
    -> fetch current Redmine state
    -> create new Snapshot
    -> run selected profile(s)
```

### 7.3 Compare with another model

Reuse the existing Snapshot and add requested Run(s). Do not re-fetch Redmine.

### 7.4 Strict comparison

Requires:

- same Snapshot;
- same Analysis Contract;
- same report language;
- same relevant source revision/input provenance;
- differing model/profile only as intended.

Different Snapshot comparison may be shown historically, but must be clearly marked `inputs differ / not strictly comparable`.

Different report languages are not strict-comparison candidates; rerun against the same Snapshot using one chosen language.

No automatic report-translation subsystem in 1.0.

## 8. Report language

Report language is explicit analysis configuration.

The model generates directly in the selected language in the original analysis call.

Changing language means a new Run against the same Snapshot; no Redmine refetch is required.

Web UI language and report language are independent.

## 9. Structured assessment

Redmine Tracker remains source metadata, not Analyzer assessment authority.

Agents fill a stable Analyzer schema; they do not invent arbitrary fields per Run.

Recommended concepts:

- `change_type`: `bug_fix`, `enhancement`, `configuration_or_usage`, `clarification_needed`, `no_change`, `unknown`
- `implementation_complexity`: `none`, `low`, `medium`, `high`, `unknown`
- `requires_code_change`: `yes`, `no`, `uncertain`
- `confidence`: `low`, `medium`, `high`
- short `complexity_rationale`

Avoid fake precision such as numeric complexity scores.

Exact JSON schema remains an implementation-planning item.

## 10. Execution model

### 10.1 Single execution slot

One Analyzer instance runs one AI analysis execution at a time.

No queue, priority, or preemption.

If busy: `analyzer_busy`.

- manual request -> rejected/not started;
- nightly request -> skipped/not started;
- no automatic retry.

### 10.2 Stop

No hard kill / `Cancel Now` in 1.0.

Use **Stop after current Item**:

- current Item finishes;
- later Items become `not_started`;
- reason: `operator_stopped`.

### 10.3 Deadline

After configured deadline:

- do not start another Item;
- current Item finishes;
- remaining Items become `not_started`;
- reason: `execution_deadline`.

Deadline is configuration, not hard-coded schedule logic.

### 10.4 Failure semantics

Ordinary issue-specific failure:

- affected Issue/item fails or blocks;
- continue to next Issue.

Shared Snapshot acquisition failure:

- related model Items for that Issue cannot run;
- continue to next Issue.

Provider-wide deterministic auth/quota failure:

- stop later work;
- remaining Items are `not_started`, not fake failures.

## 11. Scheduling

> **NAS/QNAP scheduler owns when. Analyzer owns what.**

Manual and unattended runs use the same Analyzer request/CLI entry point.

CLI requirements:

- no TTY dependency;
- no prompts;
- deterministic exit status;
- machine-readable summary;
- clear BUSY/operator-action result.

Do not implement in 1.0:

- internal scheduler daemon;
- schedule DB;
- calendar UI;
- missed-run engine;
- scheduling state machine.

## 12. AI usage, cost, quota

Only record real data.

- tokens: record if provider/harness reports them; otherwise unavailable.
- cost: record only if provider reports it or it is deterministically known.
- quota: record only from reliable official/machine-readable source.
- never scrape/guess remaining quota from UI.

Do not invoke AI for deterministic filtering, aggregation, hashing, diffs, indexing, or backup verification.

> **Prefer useful AI work avoided over merely making AI work faster.**

## 13. Web UI

Web UI is core 1.0 capability.

### Home / Dashboard

Focus: **What happened overnight?**

Show last Request summary, recent Requests, counts, and stop reason.

### Request page

Show selection, profiles, timings, counts, stop reason, and Issues grouped by Issue rather than flat Request Items.

### Issue page

Show source metadata, structured assessment, latest analysis, and history grouped by Snapshot.

### Snapshot / Comparison page

Show input provenance, Runs, strict comparability state, structured side-by-side, Markdown side-by-side, and deterministic text diff.

No AI-generated comparison summary in 1.0.

### Export

Web UI is the primary browse/filter/sort surface.

CSV/XLSX are on-demand derived exports, never canonical storage.

## 14. Runtime topology

1.0 reference topology:

- one long-running Analyzer container;
- Web/API resident;
- AI execution active only while a Request runs;
- RedmineMcp launched locally/co-located as needed;
- no premature web/worker/scheduler container split.

Architecture is generic OCI/Docker-compatible Linux. QNAP is a validated target, not an architecture abstraction.

## 15. Operational lifecycle modules

Operational complexity must be encapsulated into repeatable lifecycle operations rather than remembered sequences of SSH / `sudo` commands.

The lifecycle model includes:

- `bootstrap` — prepare a fresh host/runtime, required directories, permissions, and prerequisites;
- `doctor` / `verify` — check whether the current environment is ready and internally consistent;
- `test` — run independently selectable test/validation layers;
- `deploy` — deploy a specific IssueHarbor release from a known starting state;
- `upgrade` — verify current state, create/verify backup, migrate if needed, deploy the target release, and validate;
- `backup` — create a consistent durable-data backup;
- `restore` — restore durable data and verify it without Redmine/provider access;
- `rollback` — restore the previously verified state when an upgrade fails.

These are operational contracts, not a requirement that every operation live in a separate script or service.

Rules:

- Routine operation must not depend on manually remembering host commands.
- If elevated host privileges are genuinely required, the official lifecycle entry point should encapsulate or clearly request them rather than scattering `sudo` instructions throughout documentation.
- Each lifecycle operation should have explicit preconditions, deterministic outcomes, and clear failure reporting.
- Operations should be repeatable/idempotent where practical.
- Test layers must remain independently runnable; the release gate composes those modules rather than hiding everything behind one opaque mega-script.
- Temporary lifecycle state belongs under `.work/`; production durable data/config/secrets remain outside the source repository according to their defined boundaries.
- Operational modularity exists to make testing, deployment, upgrade, backup, restore, and recovery easier. It does **not** imply splitting IssueHarbor into unnecessary services or containers.

> **Modularity serves operability and recoverability; it does not imply microservices.**


## 16. Backup and restore

### 16.1 Concurrency

Backup and analysis are mutually exclusive; Web reading remains available.

If analysis runs:

- manual backup -> BUSY;
- automatic backup -> skipped with `analyzer_busy`.

If backup runs:

- analysis -> BUSY;
- second backup -> BUSY.

No queue/catch-up framework.

### 16.2 Backup scope

Include durable data only:

- Analyzer DB;
- Snapshots and retained input material;
- retained attachment material;
- structured JSON results;
- Markdown reports;
- Run/Snapshot provenance/hashes/timestamps/source revisions;
- real usage/cost data;
- non-secret durable Analyzer configuration;
- schema/storage metadata.

Exclude:

- Docker image/executable/source;
- logs/temp/caches;
- MCP download cache;
- generated CSV/XLSX;
- provider temporary HOME/config;
- build artifacts;
- rebuildable Git clones/cache;
- secrets/tokens/API keys/passwords/session auth.

### 16.3 Restore verification

Must be completely provider-free and Redmine-free.

Verify local metadata, artifacts, sizes/hashes, referential integrity, structured JSON schema, and missing-reference conditions.

Restore validation means the historical backup is internally intact; it does not mean it still matches current Redmine.

## 17. Storage model

Use hybrid SQLite + immutable artifact files.

SQLite is authority for state/catalog/relations/filterable fields.

Artifact storage is authority for larger immutable content such as Snapshot JSON/input, retained attachments, `result.json`, and `report.md`.

Conceptual layout:

```text
/data/
    analyzer.db
    snapshots/<snapshot-id>/...
    runs/<run-id>/result.json
    runs/<run-id>/report.md
```

> **SQLite is catalog/query authority; artifact storage is immutable content authority.**

Artifact publication:

1. generate temp output;
2. validate;
3. hash;
4. atomic rename;
5. register metadata in SQLite transaction.

Do not create a generic artifact framework without a real need.

## 18. Compatibility after 1.0

The 0.x -> 1.0 reset is exceptional.

After public 1.0, durable user data is a compatibility responsibility.

Normal 1.x evolution should support deterministic migration:

```text
verify current storage
 -> verified backup
 -> deterministic migration
 -> validate new schema
 -> start new release
```

Never continue from a half-migrated state.

## 19. Security and trust boundaries

### 18.1 Deployment

Reference model:

- private/trusted network;
- direct public Internet exposure is not a validated 1.0 deployment model.

Analyzer 1.0 does not provide built-in HTTPS/TLS termination.

HTTP is supported with transparent documentation:

> Plain HTTP does not provide transport encryption and assumes the network path is trusted.

Operators requiring HTTPS terminate TLS externally using NAS/reverse proxy/ingress.

Analyzer does not manage domains, certificates, ACME, or renewal.

### 18.2 Web authentication

1.0 uses:

- one Operator;
- password authentication;
- server-managed browser session;
- no multi-user system;
- no RBAC;
- no persistent user-facing access-token scheme without a future machine-to-machine use case.

Password is hashed, changeable, not readable back, and reset rather than recovered if lost.

### 18.3 Secrets

Secrets may be configured through authenticated Web UI where appropriate.

Examples: Redmine API key, DeepSeek API key, provider credentials.

> **Secrets are settable and replaceable, but not retrievable.**

UI may show configured/missing, last updated, optional fingerprint/last characters, test connection, replace/re-authenticate.

Secrets must never appear in model context, tool results, Snapshot, report, log, backup, or normal API response.

Provider adapters may mechanically use credentials; the model must not be able to inspect them.

### 18.4 Redmine and Git

Analyzer capability:

```text
Redmine: read-only
Git/source repositories: read-only
```

Analyzer may analyze, classify, estimate, recommend, and identify suggested code changes.

Analyzer must not edit/comment/change Redmine state, modify source, commit, or push.

Actual coding belongs to the separate interactive human + coding-agent workflow.

### 18.5 Prompt injection

Do not claim prompt injection is solved.

> **Security must not depend on the model behaving correctly. Capabilities and credentials must be constrained outside the model boundary.**

## 20. Configuration model

Separate four concerns:

1. Normal Analyzer configuration — Web-manageable where practical.
2. Secrets — Web-manageable with write-only semantics.
3. Provider-specific authentication — provider adapter decides safe UX.
4. Host/container configuration — deployment-managed.

Host/container examples: volume mounts, port binding, UID/GID, reverse proxy, container lifecycle.

Normal key/config changes should not require SSH where avoidable. Host access is acceptable for exceptional recovery such as a completely lost Operator password.

## 21. Language policy

### Engineering

English only for code, identifiers, comments/docstrings, tests, CLI/API/schema/config keys, logs, commits, changelog/release notes, architecture/security/developer docs.

English documentation is authoritative.

Chinese `.zh.md` companion docs are optional and synchronized at milestone boundaries, not every edit.

### Web UI

English canonical wording; i18n-ready. French/Chinese may be added when justified.

### Analysis reports

Report language is configured per Analysis Contract and generated directly in that language.

## 22. Repository structure

Allowed product-level structure:

```text
repo/
├── src/
│   ├── Analyzer/
│   └── RedmineMcp/
├── tests/
├── docs/
├── config/
├── deploy/
├── scripts/
├── third_party/          # only when genuinely required
├── .work/
│   └── README.md
├── .github/              # only when actual GitHub automation exists
├── .dockerignore
├── .editorconfig
├── .gitignore
├── AGENTS.md
├── CHANGELOG.md
├── DEVELOPMENT.md
├── LICENSE
├── README.md
├── SECURITY.md
└── solution/build files actually required
```

Do not pre-create empty files/directories because they are common elsewhere.

> **A repository file or directory exists because the project currently has content or a workflow that requires it.**

## 23. `.work/` contract

`.work/` is the single main repository-local disposable workspace.

Tracked:

```text
.work/README.md
```

Ignored:

```gitignore
.work/*
!.work/README.md
```

Runtime-created categories may include:

```text
.work/audit/
.work/tests/
.work/harness/
.work/build/
.work/tools/
.work/cache/
.work/scratch/
```

Do not pre-create empty categories.

Rules:

1. `.work/` is fully disposable.
2. No build/test/audit/dev script may create random directories directly under repo root.
3. Required inputs must be tracked or reproducibly acquired.
4. `.work/` is excluded from Git, Docker build context, production backup, and release packaging.

> **The repository root is a curated interface, not a workspace.**

Standard .NET `bin/obj` behavior does not need artificial redirection unless a real need appears.

## 24. Ignore and standards policy

Use maintained ecosystem templates only when that technology is actually present.

Current useful baseline:

- Visual Studio/.NET: `https://github.com/github/gitignore/blob/main/VisualStudio.gitignore`
- GitHub template collection: `https://github.com/github/gitignore`

Root `.gitignore` handles repo-wide rules. Technology-specific `.gitignore` may live at the smallest common directory covering that technology.

Do not add Python/Node/etc. templates before those technologies enter scope.

Docker `.dockerignore` follows the actual build context and Docker semantics.

## 25. Useful References

Not a bookmark collection.

A reference belongs here only if:

1. this project actually uses it;
2. it materially informs implementation/maintenance;
3. it is likely to be consulted again.

Current reference:

- GitHub maintained `.gitignore` templates: `https://github.com/github/gitignore`

## 26. Public repository policy

The public V2 repository is public-safe from its first byte.

Use generic/synthetic names, Issues, attachments, journals, URLs, repos, network examples, screenshots, logs, fixtures, comments, and commit messages.

Do not copy real private data and “sanitize later”.

> **Public-safe by construction, not sanitized at the end.**

## 27. Unified versioning

The public product uses one unified release version across all shipped components.

Example:

```text
issueharbor-v1.2.0
```

That release version applies to Analyzer, RedmineMcp, Web UI, CLI, Docker image, and other shipped components.

Do not maintain independent product versions per component.

Internal compatibility identifiers may remain independent: storage schema, result schema, MCP contract, HTTP API version.

## 28. Testing strategy

Layers:

- Unit — deterministic logic.
- Contract — MCP/API/schema/storage contracts.
- Integration — synthetic/fake Redmine, providers, repositories.
- Artifact — real built EXE/container/package.
- Restore — backup -> restore -> provider-free integrity verification.
- Release validation — real validated platform/provider/client where required.

> **Source tests passing do not prove the release artifact works.**

The exact artifact intended for release should be the artifact that passed final release validation; do not silently rebuild a different artifact afterward.

## 29. CI and AI usage

Normal CI must not depend on production Redmine, private repositories, customer data, or repeated paid AI calls.

Use synthetic fixtures, fake Redmine, fake providers, and synthetic repositories.

Real provider/NAS/client validation belongs to release validation.

## 30. Release discipline

No release by intuition.

Conceptual gate:

```text
approved scope
 -> source checks
 -> unit/contract tests
 -> integration tests
 -> build actual release artifact
 -> artifact smoke tests
 -> backup/restore verification
 -> secret/private-data checks
 -> required real-environment validation
 -> release candidate accepted
 -> tag/publish
```

Release identity should make exact product version, Git SHA, image digest, binary checksum, storage schema, relevant contract/schema versions, and validated platforms identifiable.

Do not silently replace an already-published release artifact; publish a new version.

## 31. Security without security theater

Security controls must address concrete threats/trust boundaries.

Priorities:

- Redmine read-only;
- Git read-only;
- model cannot inspect secrets;
- single-Operator authentication;
- write-only secret management;
- no secrets in artifacts/logs/backups;
- public repo free of private data;
- HTTP limitation documented honestly;
- actual release artifact tested;
- sensible dependency/secret checks.

Do not add enterprise-style complexity without real need: multi-user RBAC, SSO, built-in TLS/certificate manager, custom Vault/KMS, mTLS everywhere, or large security-tool stacks.

> **Security complexity is not a goal by itself.**

## 32. Scope and change discipline

Each implementation chunk should have a small Scope Note:

- Functional goal
- User workflow
- In scope
- Out of scope
- Security impact
- AI/provider cost impact
- Acceptance criteria

New ideas found during implementation are recorded/classified; they do not silently expand current scope.

## 33. Historical private-repository closure

Before archive, create/update a rich private history covering:

- original copy/paste pain;
- RedmineReadonlyMcp;
- rename/security-boundary evolution;
- Analyzer emergence;
- production 0.x;
- Job/storage/scheduler/projection complexity;
- packaging/runtime lessons;
- 0.5/V2 experiments/audits;
- lessons/pitfalls;
- why V2 abandons compatibility;
- why the public project restarts cleanly at 1.0.

Then make one final archival documentation commit. No unnecessary release tag.

## 34. Open items before the relevant implementation step

Not architecture blockers, but must be resolved before needed:

- open-source license;
- exact structured result schema;
- Request/Item lifecycle semantics (frozen by [Scope 003](../scopes/scope-003-core-domain-status-contract.md)); persistence shape and implementation details remain deferred;
- exact RedmineMcp 1.0 tool manifest;
- exact provider/profile adapter design;
- exact source repository mapping/revision capture;
- exact Web UI localization scope;
- exact backup retention/destination policy;
- exact release automation near first release;
- exact validated platform matrix based on evidence.

## 35. Final architecture summary

```text
Interactive:
Codex / DSH Web
    -> RedmineMcp
    -> Redmine
```

```text
Durable analysis:
NAS scheduler / Operator
    -> Analyzer
        -> RedmineMcp
            -> Redmine (read-only)
        -> Source repository (read-only)
        -> Provider adapter
            -> AI model
        -> SQLite + immutable artifacts
        -> Web UI
```

IssueHarbor exists to make durable Redmine analysis easy, reproducible, inspectable, comparable, and safe without becoming a generic workflow platform, generic issue-source framework, or autonomous operator.
