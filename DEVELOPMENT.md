# Development Guide

## Requirements

- .NET 10 SDK
- Git

The repository selects the .NET 10 SDK family in `global.json` and configures `dotnet test` to use Microsoft.Testing.Platform. Use the `dotnet` CLI for restore, build, test, and publish operations.

## Repository structure

- `src/Analyzer/` — minimal ASP.NET Core 10 Razor Pages host.
- `src/RedmineMcp/` — minimal stdio host built with the official MCP C# SDK.
- `tests/Analyzer.Tests/` — Analyzer bootstrap integration test.
- `docs/` — frozen V2 design baselines and the technical baseline decision.
- `.work/` — disposable working material; only this directory's README is tracked.

The Analyzer bootstrap serves `/` and contains no product workflows. RedmineMcp establishes the MCP stdio host only; it has no placeholder tools. For stdio use, stdout is reserved for protocol messages and console logging is routed to stderr.

## Validation

Run these commands from the repository root:

```sh
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Run the Analyzer page with:

```sh
dotnet run --project src/Analyzer/IssueHarbor.Analyzer.csproj
```

## Scope and public safety

Implement only work approved for the current scope. The design and ideas documents describe the product baseline and future classifications; they do not authorize implementation of out-of-scope features by themselves.

Keep tracked content and Git metadata suitable for a public repository. Use synthetic examples only. Do not add personal or private environment details, credentials, real Redmine data, or private repository material. Do not import the historical Analyzer repository's Git history or runtime state.
