# IssueHarbor V2 / 1.0.0 — Research Scope 001 Technical Baseline Decision

**Scope:** Research Scope 001 — Implementation Stack Baseline  
**Status:** PASS / CLOSED  
**Purpose:** Freeze the minimum technical implementation baseline for IssueHarbor V2 / 1.0.0 before repository bootstrap.

## 1. Frozen decisions

- **Primary language:** C#
- **Runtime / target:** .NET 10 LTS (`net10.0`)
- **Analyzer Web/API:** ASP.NET Core 10
- **Web UI:** Razor Pages, server-rendered
- **RedmineMcp:** Official MCP C# SDK
- **MCP reference transport:** `stdio`
- **Storage baseline:** SQLite + immutable artifact files
- **SQLite .NET provider:** `Microsoft.Data.Sqlite` is an acceptable low-level baseline
- **Testing:** MSTest + Microsoft.Testing.Platform
- **Build / test / publish authority:** `dotnet` CLI
- **Deployment baseline:** OCI/Docker-compatible Linux container
- **Development requirements currently needed:** .NET 10 SDK and Git

## 2. Architecture intent

The implementation should use one primary application stack wherever practical.

The selected stack is intended to keep IssueHarbor V2 simple, lightweight, maintainable, and aligned with the frozen product workflows. Technical choices must continue to serve real functional needs rather than speculative portability or architectural elegance.

## 3. Explicitly not decided in Scope 001

The following remain for later, relevant scopes:

- Exact SQLite schema
- EF Core vs explicit SQL / migration mechanism
- Exact RedmineMcp 1.0 tool manifest
- Exact Request / Item status model
- Exact structured analysis result schema
- Provider/profile adapter design
- Provider authentication design
- Source repository mapping and revision capture
- Exact CLI command surface
- Docker base distribution / image details
- Exact QNAP CPU/RAM/storage minimums
- Backup implementation details
- Web UI localization scope
- Open-source license
- CI / release automation

These are deferred intentionally and must not be pulled into implementation merely because they are adjacent.

## 4. Public repository rule

All repository content must be public-safe from the first commit.

Use only generic or synthetic examples, paths, Issues, repositories, URLs, logs, fixtures, screenshots, credentials, and commit content. Personal or private environment details must not be committed.

## 5. Scope close condition

Research Scope 001 is complete.

Future scopes should treat this technical baseline as frozen unless a concrete implementation blocker or new real functional requirement justifies reopening it.

## 6. Baseline sources

- `issueharbor-v2-1.0.0-design-plan.md`
- `issueharbor-v2-1.0.0-ideas-registry.md`

This brief supplements those formal baselines. It does not replace them.
