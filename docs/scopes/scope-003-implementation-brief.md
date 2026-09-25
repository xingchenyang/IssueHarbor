# IssueHarbor V2 / 1.0.0 — Scope 003 Implementation Brief

**Scope:** 003 — Core Domain & Status Contracts  
**Status:** APPROVED FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## Functional goal

Add the approved Scope 003 domain/status contract to the repository as authoritative project documentation and verify that existing bootstrap documentation does not conflict with it.

## In scope

- Add `docs/scopes/scope-003-core-domain-status-contract.md` using the approved contract.
- Update existing repository documentation only where necessary to reference the new approved contract.
- Keep terminology aligned with the formal V2 design baseline.
- Run repository documentation/public-safety review.
- Create one local documentation commit after validation.

## Out of scope

Do not implement:

- C# domain classes or enums
- database schema or migrations
- EF Core or SQL persistence
- result JSON schema
- RedmineMcp tools
- provider code
- execution engine
- CLI behavior
- Web UI behavior
- retries or crash recovery

Do not expand the approved status/reason set.

## Validation

Before commit:

- inspect `git status`;
- inspect `git diff`;
- confirm no contradiction with the formal design baseline;
- confirm no speculative states/reasons were added;
- confirm no personal/private data entered the repository;
- confirm no unrelated files changed.

Because this scope is documentation-only, `dotnet restore/build/test` are not required unless implementation changes solution-affecting files.

## Git boundary

Local Git inspection, staging, and the approved local commit are allowed.

The Implementation Agent must never run `git push` or otherwise write to a Git remote.

Remote push remains a manual Product Owner action.

## Implementation report

Report:

1. files changed;
2. any wording reconciliation performed;
3. validation performed;
4. local commit hash;
5. deviations, blockers, or follow-up ideas.

Do not push.
