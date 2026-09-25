# IssueHarbor

IssueHarbor is a self-hosted service for reproducible AI analyses of Redmine issues, with preserved provenance and model-result comparison.

IssueHarbor 1.0 targets Redmine. RedmineMcp provides deliberately read-only Redmine context access and can also be used independently from Analyzer. This repository currently contains the minimal .NET bootstrap; product workflows are added only by approved scopes.

## Development

Requirements: .NET 10 SDK and Git.

From the repository root:

```sh
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
dotnet run --project src/Analyzer/IssueHarbor.Analyzer.csproj
```

See [DEVELOPMENT.md](DEVELOPMENT.md) for repository structure and development boundaries.
