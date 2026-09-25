# IssueHarbor V2 / 1.0.0 — Scope 002 Implementation Brief

**Scope:** 002 — Repository Bootstrap / First Commit  
**Status:** APPROVED FOR IMPLEMENTATION  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** Not assigned  

## 1. Functional goal

Create the first public-safe IssueHarbor V2 repository state with the smallest useful .NET 10 foundation that later scopes can build on.

A developer or implementation agent with the documented development requirements must be able to restore, build, test, and run the Analyzer bootstrap without relying on private machine knowledge, historical Analyzer repositories, Visual Studio, Docker, or undocumented setup steps.

This scope establishes repository and toolchain foundations only. It does not implement IssueHarbor product workflows.

## 2. Authoritative inputs

Use these as the authoritative product/design baseline:

- `issueharbor-v2-1.0.0-design-plan.md`
- `issueharbor-v2-1.0.0-ideas-registry.md`
- `scope-001-technical-baseline-decision.md`

Do not reopen frozen architecture decisions unless implementation reveals a concrete conflict or blocker.

## 3. In scope

### Repository bootstrap

Create a new Git repository and the minimum tracked structure needed now.

Use:

- C#
- .NET 10 (`net10.0`)
- ASP.NET Core 10
- Razor Pages
- official MCP C# SDK
- MCP `stdio` transport
- MSTest + Microsoft.Testing.Platform
- `dotnet` CLI as the authoritative restore/build/test/publish interface

Create a `.slnx` solution.

Create `global.json` to keep development on the .NET 10 SDK family and select Microsoft.Testing.Platform for `dotnet test`.

### Analyzer bootstrap

Create a minimal Analyzer ASP.NET Core / Razor Pages project.

It must:

- target `net10.0`;
- start successfully;
- expose `/`;
- return a minimal public-safe IssueHarbor page;
- contain no Dashboard, authentication, configuration, storage, provider, Redmine, or Analyzer business logic.

Do not keep unused default template sample pages, sample navigation, privacy pages, sample styling, or other boilerplate merely because a template generated them.

### RedmineMcp bootstrap

Create a minimal RedmineMcp .NET project using the official MCP C# SDK and `stdio`.

It must:

- target `net10.0`;
- build successfully;
- establish the stdio MCP server host foundation;
- keep protocol stdout clean from ordinary application logging.

Do not define placeholder product tools, echo tools, fake Redmine tools, or a speculative MCP manifest.

### Tests

Create one meaningful Analyzer bootstrap integration test.

The test must start the actual ASP.NET Core test host, request `/`, and verify a successful response.

Do not add meaningless placeholder tests such as `Assert.IsTrue(true)`.

RedmineMcp only needs build validation in this scope; MCP contract tests belong to later RedmineMcp scopes.

### Repository documentation

Create only documents that have a current purpose:

- `README.md`
- `DEVELOPMENT.md`
- `AGENTS.md`
- `CHANGELOG.md`
- `.work/README.md`

Include the two formal V2 baseline documents and the Scope 001 decision under `docs/`.

Document only actual development requirements. Do not record personal workstation inventory or a list of technologies that are not required.

Current development requirements:

- .NET 10 SDK
- Git

### Repository hygiene

Add:

- `.editorconfig`
- `.gitignore`

Use the maintained GitHub Visual Studio/.NET ignore template as the ecosystem baseline, plus IssueHarbor-specific repository rules such as the `.work/` contract.

`.work/README.md` is tracked. Everything else under `.work/` is disposable and ignored.

## 4. Public-safe first-commit requirement

The repository must be public-safe before the first commit is created.

Do not commit:

- personal names or personal email addresses;
- personal usernames or home-directory paths;
- private hostnames, NAS names, internal IP addresses, or private network details;
- real Redmine URLs, projects, Issues, journals, attachments, or API credentials;
- real private repository names, URLs, paths, commits, logs, screenshots, or customer/internal data;
- secrets, tokens, passwords, API keys, session data, or credential material.

Use generic or synthetic examples only.

Do not use a “commit now, sanitize later” workflow.

Use a repository-local generic Git identity for the first commit so that commit metadata is also public-safe.

Example:

```text
user.name = IssueHarbor
user.email = issueharbor@example.invalid
```

The local Git identity configuration belongs only in `.git/config` and must not be tracked.

## 5. Expected minimum repository shape

```text
IssueHarbor/
├── src/
│   ├── Analyzer/
│   │   ├── IssueHarbor.Analyzer.csproj
│   │   ├── Program.cs
│   │   └── Pages/
│   │       └── Index.cshtml
│   └── RedmineMcp/
│       ├── IssueHarbor.RedmineMcp.csproj
│       └── Program.cs
├── tests/
│   └── Analyzer.Tests/
│       ├── IssueHarbor.Analyzer.Tests.csproj
│       └── BootstrapSmokeTests.cs
├── docs/
│   ├── design/
│   │   ├── issueharbor-v2-1.0.0-design-plan.md
│   │   └── issueharbor-v2-1.0.0-ideas-registry.md
│   └── scopes/
│       └── scope-001-technical-baseline-decision.md
├── .work/
│   └── README.md
├── .editorconfig
├── .gitignore
├── AGENTS.md
├── CHANGELOG.md
├── DEVELOPMENT.md
├── global.json
├── IssueHarbor.slnx
└── README.md
```

Do not create empty directories or files merely because they may be useful later.

## 6. Explicitly out of scope

Do not implement or design beyond what is needed for bootstrap:

- Analyzer domain models
- Request / Request Item status model
- structured analysis result schema
- SQLite schema
- EF Core vs explicit SQL decision
- storage migrations
- artifact storage implementation
- Redmine API access
- RedmineMcp 1.0 tool manifest
- source repository mapping or Git context acquisition
- Snapshot acquisition
- AI/provider/profile adapters
- provider authentication
- Operator authentication
- secret storage or secret-management UI
- configuration model
- Dashboard or other product Web UI pages
- CLI command surface
- internal scheduler
- Dockerfile or container image
- deployment scripts
- QNAP integration
- backup / restore
- release automation
- `.github/` automation
- open-source license decision
- `SECURITY.md` workflow
- speculative `config/`, `deploy/`, `scripts/`, or `third_party/` structures

If implementation reveals an adjacent idea, report it separately. Do not implement it.

## 7. Git operation boundary

The Implementation Agent may:

- create and modify files required by this approved scope;
- initialize the local Git repository;
- run local inspection commands such as `git status`, `git diff`, and `git diff --cached`;
- stage the approved Scope 002 content;
- create the approved local first commit after all acceptance evidence passes.

The Implementation Agent must not:

- run `git push`;
- create or modify a remote repository;
- create or modify remote branches;
- create or push tags;
- create releases;
- alter remote settings.

`git push` is always a manual Product Owner action unless a future scope explicitly changes that rule.

## 8. Security impact

No product security feature is implemented in this scope.

The security objective is limited to:

- public-safe repository content and history from the first commit;
- no credentials or private data in source, tests, documentation, generated evidence, or Git metadata;
- clean MCP stdout behavior suitable for later stdio protocol use.

Do not add unrelated security frameworks or tooling.

## 9. AI/provider cost impact

Runtime AI/provider cost: **zero**.

This scope must not require paid AI calls, production Redmine access, private repositories, or provider credentials for build or tests.

## 10. Acceptance criteria

All of the following must pass before creating the local first commit.

### Toolchain

`dotnet --version` resolves to a .NET 10 SDK compatible with the repository `global.json`.

### Restore

```text
dotnet restore IssueHarbor.slnx
```

passes.

### Build

```text
dotnet build IssueHarbor.slnx --no-restore
```

passes.

### Tests

```text
dotnet test IssueHarbor.slnx --no-build
```

passes.

The Analyzer bootstrap test must actually start the ASP.NET Core test host and successfully request `/`.

### Analyzer bootstrap

The Analyzer project can be launched with `dotnet run` and serves its minimal root page.

### RedmineMcp bootstrap

The RedmineMcp project builds successfully and ordinary logging does not pollute MCP protocol stdout.

### Repository structure

- no speculative empty directories;
- no build output committed;
- `.work/README.md` is tracked;
- disposable `.work/` content is ignored;
- no Docker/deploy/config/release structures outside this scope.

### Public-safety review

Before commit:

- inspect `git status`;
- inspect the staged diff;
- check for accidental absolute personal paths, personal emails, private URLs/hosts/IPs, credentials, secrets, real Redmine data, real private repository data, and private logs;
- ensure commit author/committer identity is generic and repository-local;
- ensure generated artifacts and temporary files are not staged.

### Scope review

Verify that every tracked file has a current Scope 002 purpose.

If a file exists only because “projects usually have one” or “it may be useful later,” remove it unless the formal baseline specifically requires it now.

## 11. Local first commit

Only after all acceptance criteria pass, create the local first commit.

Suggested public-safe commit message:

```text
chore: bootstrap IssueHarbor repository
```

Do not push.

## 12. Implementation report required

When finished, report:

1. files/directories created;
2. package/tooling versions actually resolved;
3. exact validation commands run;
4. test results;
5. public-safety checks performed;
6. local commit hash, if the commit was created;
7. any deviations from this brief;
8. any discovered follow-up ideas or blockers, clearly separated from implemented scope.

Do not hide incomplete work, warnings, deviations, or failed checks.

## 13. Completion rule

Scope 002 is ready for Architecture / Scope Lead review only when:

- bootstrap structure is minimal and justified;
- restore/build/test evidence passes;
- Analyzer bootstrap is executable;
- RedmineMcp bootstrap builds cleanly;
- repository content and Git metadata are public-safe;
- no out-of-scope product functionality was introduced;
- no push was performed.
