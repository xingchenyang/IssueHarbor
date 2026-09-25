# IssueHarbor V2 / 1.0.0 — Ideas Registry and Deferred Decisions

Status: **FORMAL COMPANION TO THE V2 DESIGN PLAN**  
Purpose: Preserve useful ideas without allowing them to silently enter current scope.

Project name: **IssueHarbor**. V2 / 1.0 is intentionally Redmine-specific despite the source-neutral project name.

Presence in this file is **not** an implementation commitment.

Classification:

- `ROADMAP` — accepted as useful, but not current scope.
- `IDEA` — potentially useful, requires future functional analysis.
- `DEFERRED` — deliberately postponed because 1.0 has a simpler solution.
- `REJECTED` — explicitly not part of the current direction unless future evidence materially changes the decision.
- `OPEN` — must be resolved before a specific later step.

## 1. Operational analytics / lightweight BI

Classification: `ROADMAP`

Potential capability:

- Issue counts/trends by time, project, tracker, status, priority, author, assignee, version, category, and custom field;
- created/resolved volumes;
- backlog/status distribution;
- generic organizational/customer grouping;
- assignee throughput;
- average/median resolution time;
- backlog age;
- later Analyzer-result dimensions such as analysis coverage/classification.

Direction:

```text
Redmine
  -> RedmineMcp
      -> Analyzer
      -> Analytics / Insights
```

Rules:

- deterministic aggregation produces facts;
- AI may interpret facts but is not authoritative for counts/grouping/durations;
- do not create many report-specific MCP tools;
- if it grows, prefer a separate analytical read model.

Not 1.0 core.

## 2. Complex project-filter algebra

Classification: `DEFERRED`

Ideas considered:

- `subproject_depth`;
- root-minus-subtree exclusion;
- generic project-set expressions.

Current 1.0 solution remains simple:

- `project_id`;
- `include_subprojects`;
- explicit `status`;
- only proven additional fields.

Revisit only when a real workflow cannot be expressed cleanly.

## 3. Generic multi-source / source-adapter framework

Classification: `REJECTED`

IssueHarbor has a source-neutral project name, but V2 / 1.0 is intentionally Redmine-specific.

Do not add speculative abstractions such as:

- universal Issue/WorkItem models;
- generic source/plugin registries;
- dynamic MCP source discovery;
- `ISourceAdapter` hierarchies with only one real implementation;
- generic renaming of Redmine concepts merely to appear portable.

Current implementation may use Redmine concepts directly when they are useful and clear.

If Redmine is eventually replaced in the real environment, evaluate the new platform from its actual workflows and semantics. The Redmine generation may then be archived, and useful concepts, implementation patterns, operational tooling, and lessons may be distilled into a new generation.

> Future portability is not a current functional requirement.


## 4. Internal scheduler

Classification: `REJECTED for 1.0 / possible future IDEA`

Current solution:

> NAS/QNAP scheduler owns when. Analyzer owns what.

Do not add scheduler daemon, schedule DB, calendar UI, missed-run logic, or schedule state machine.

Revisit only if external scheduling becomes a real operational limitation.

## 5. Queue / priorities / preemption

Classification: `REJECTED for 1.0`

Current solution:

- one execution slot;
- active Request owns it;
- others receive/record `analyzer_busy`;
- no queue;
- no automatic retry.

Urgent interactive work belongs to Codex/DSH + RedmineMcp.

## 6. Hard cancel / process kill

Classification: `DEFERRED`

Current solution:

> Stop after current Item.

Per-run timeout handles genuinely stuck execution.

Hard cancel adds partial-Run semantics, process-tree cleanup, quota ambiguity, and artifact-publication ambiguity.

Revisit only if real use proves stop-after-current insufficient.

## 7. AI-generated comparison summary

Classification: `DEFERRED`

1.0 comparison is deterministic:

- structured field side-by-side;
- Markdown side-by-side;
- deterministic diff.

This consumes 0 extra AI.

If added later, it must be explicit and clearly consume AI/quota.

## 8. Report translation subsystem

Classification: `REJECTED`

Current design:

- report language is part of Analysis Contract;
- model directly generates the selected language;
- different language = new Run against the same Snapshot;
- user may use external translation tools independently.

Do not add translation artifacts, cache, provenance, or automatic comparison translation.

## 9. Automatic re-analysis from fingerprints

Classification: `REJECTED`

Current behavior:

- unseen Issue ID -> analyze;
- already analyzed Issue ID -> no automatic re-analysis;
- explicit Re-analyze creates a new Snapshot.

Reason:

- avoid change-monitoring complexity;
- avoid unnecessary AI consumption;
- Analyzer is not a real-time Issue-change monitor.

## 10. Built-in TLS / certificate management

Classification: `REJECTED`

Analyzer 1.0 provides HTTP only.

Reference deployment is private/trusted network, not direct public Internet.

Operators requiring HTTPS use external reverse proxy / NAS ingress.

Do not add domain management, certificates, ACME, or renewal automation.

## 11. Multi-user accounts / RBAC

Classification: `REJECTED for 1.0`

Current need:

- one Operator;
- password authentication;
- browser session.

Do not add users, roles, admin/viewer/operator separation, email flows, MFA, or SSO until a genuine multi-user workflow exists.

## 12. Machine API tokens

Classification: `DEFERRED`

Current human Web access uses Operator password + server-managed session.

NAS scheduler uses local CLI.

Do not introduce persistent machine access tokens until a real remote machine-to-machine integration exists.

## 13. External credential and authentication handling

Classification: `OPEN`

Need a clear authentication model that distinguishes AI execution providers from other external integrations.

Examples:

- DeepSeek: AI execution provider, likely API key;
- Codex: AI execution provider, may use a different authentication flow;
- Redmine: external source integration with its own API credential.

Required principles:

- Do not use `provider` as a catch-all term for every external credential.
- AI provider authentication and Redmine integration authentication may differ.
- Secret-handling guarantees remain consistent regardless of integration type.
- Do not force all authentication flows into a generic `API_KEY` abstraction.

Resolve when the relevant provider/integration authentication slice is designed.

## 14. Web UI localization scope

Classification: `OPEN`

Frozen:

- engineering language = English;
- English UI wording canonical;
- UI architecture i18n-ready;
- report language independent from UI language.

Still to decide:

- French in 1.0 or later;
- Chinese in 1.0 or later.

Decision should follow real maintenance cost, not symbolic completeness.

## 15. Backup retention / destination

Classification: `OPEN`

Frozen:

- automatic and manual backup are both useful;
- same implementation;
- analysis and backup mutually exclusive;
- secrets excluded;
- provider-free restore verification required.

Still to decide:

- default retention count;
- naming;
- on-NAS vs off-NAS reference recommendation;
- pruning behavior.

Do not create a complex backup orchestration framework.

## 16. Source repository mapping / revision capture

Classification: `OPEN`

Frozen:

- repositories are read-only;
- exact revision/provenance matters.

Still to define:

- mapping from Redmine context to repo;
- local checkout/cache strategy;
- revision capture;
- unavailable-repository behavior.

Keep it functionally simple.

## 17. Exact RedmineMcp 1.0 tool manifest

Classification: `OPEN`

Historical 12-tool surface is evidence, not a requirement.

Implementation planning should classify old tools one by one:

- KEEP
- RENAME
- MERGE
- DROP
- ADD

A tool belongs only if it materially helps discover/retrieve/understand Issues, history, relations, bounded attachment context, or necessary reference metadata.

Do not compete on maximum API coverage.

## 18. Exact structured result schema

Classification: `OPEN`

Frozen conceptual fields:

- change type;
- implementation complexity;
- requires code change;
- confidence;
- rationale.

Need exact JSON schema, required/optional rules, versioning, validation, and rendering behavior.

Must remain generic across Redmine installations.

## 19. Exact Request / Item status model

Classification: `OPEN`

Need a small durable status model supporting real states such as:

- completed;
- failed;
- blocked;
- not_started;
- analyzer_busy;
- execution_deadline;
- operator_stopped;
- provider-wide stop conditions.

Avoid recreating the old generic Job framework.


## 20. Open-source license

Classification: `OPEN`

Choose before public release.

Do not add a random license just because public repositories often have one.

Decision should reflect intended reuse/contribution model.

## 21. Release automation details

Classification: `DEFERRED / OPEN near release`

Frozen release principles:

- approved scope;
- source checks;
- artifact build;
- artifact smoke;
- backup/restore verification;
- secret/private-data checks;
- required real-environment validation;
- exact artifact provenance.

Do not prematurely build a large CI/release platform.

Define the actual automation when the first release is near.

## 22. Artifact attestation / SBOM

Classification: `IDEA`

Potential benefits:

- stronger artifact provenance;
- dependency inventory;
- supply-chain verification.

Not a 1.0 functional blocker.

First priorities remain exact Git SHA, image digest, checksums, tested artifact, and immutable release behavior where practical.

## 23. Additional security automation

Classification: `IDEA`

Possible future additions:

- more dependency scanning;
- supply-chain scoring;
- advanced static analysis;
- external secret-manager integration.

Do not add security tooling for appearance or scorecards.

> Security controls must address a concrete threat or trust boundary.

## 24. External secret managers / Docker secrets / KMS

Classification: `IDEA`

1.0 baseline:

- dedicated secret handling;
- Web-managed write-only secrets where appropriate;
- secrets excluded from backups/logs/models.

Future deployments may prefer Docker secrets, Kubernetes secrets, Vault/KMS, etc.

Do not make these baseline dependencies.

## 25. More deployment targets

Classification: `ROADMAP as evidence becomes available`

Current philosophy:

- generic Docker/Linux design;
- concrete validated targets only.

Do not claim support for Synology, ARM NAS, Kubernetes, macOS, or other platforms until validated.

## 26. Richer Web actions

Classification: `IDEA`

Potential examples:

- bulk re-analysis;
- bulk compare;
- saved UI views;
- richer export presets.

Evaluate each separately from real workflow.

Any AI-consuming action must remain explicit.

Do not create a generic control plane.

## 27. In-product schedule management

Classification: `IDEA`

Current external scheduler is intentionally sufficient.

Revisit only if users repeatedly need schedule visibility/editing/missed-run awareness inside Analyzer.

Functional analysis comes first.

## 28. Public contribution process

Classification: `DEFERRED`

Do not create empty open-source ceremony files until needed.

Possible future items:

- `CONTRIBUTING.md`;
- issue templates;
- PR templates;
- code of conduct;
- contributor workflow.

Add only when the corresponding real workflow exists.

## 29. Useful References

Classification: `ONGOING, strictly scoped`

Current accepted reference:

- GitHub maintained `.gitignore` templates: `https://github.com/github/gitignore`

Future references are added only if the project actually uses them.

This is not a bookmark collection.

## 30. Historical private-repository closure

Status update: the historical Analyzer 0.x project has been closed/archived and is now reference-only.

Classification: `PLANNED ACTION`

Before archive, create/update a rich history covering:

- original copy/paste pain;
- RedmineReadonlyMcp;
- rename/security-boundary evolution;
- Analyzer emergence;
- 0.4.x production;
- Job/storage/scheduler/projection complexity;
- packaging/runtime failures;
- 0.5/V2 experiments and audits;
- what worked and failed;
- why V2 abandons compatibility;
- why the public repo starts fresh at 1.0.

This private history may retain internal terminology because the repository remains private.

Then:

- one final archival documentation commit;
- no unnecessary release tag;
- repository becomes historical/archive state.

## 31. Ideas Registry discipline

When a new idea appears:

```text
idea
  -> functional analysis
  -> classify
```

Do not implement directly.

If it is not current scope:

- record it here;
- keep enough context to avoid rediscovering the same discussion;
- finish the current implementation chunk.

This registry preserves useful thinking **without allowing scope creep**.
