# IssueHarbor V2 / 1.0.0 — Scope 008 Implementation Brief

**Scope:** 008 — Source Repository Context<br>
**Status:** APPROVED FOR IMPLEMENTATION<br>
**Architecture / Scope Lead:** GPT-5.6 Sol<br>
**Implementation Agent:** GPT-6 Luna<br>
**Independent Reviewer:** DeepSeek-V41-Flash<br>

## 1. Functional goal

Implement the approved Git-specific, local-only, read-only source repository context layer.

The implementation must support operator-configured repositories, immutable revision resolution, Git-object tree/blob reads, and commit-scoped Git search without requiring a working-tree checkout.

Do not implement Snapshot acquisition.

## 2. Authoritative inputs

Use:

- frozen V2 design plan;
- Scope 003 domain timing semantics;
- `docs/scopes/scope-008-source-repository-context-contract.md`.

Do not introduce generic SCM or multi-source abstractions.

## 3. Required implementation

Implement the smallest concrete code required for repository mapping configuration, mapping validation, Git executable process execution, read-only/local-only Git environment, exact revision resolution, repository object-format discovery, root-tree resolution, resolved source provenance, recursive tree enumeration, bounded blob reading, commit-scoped Git search, deterministic Git/process failures, and synthetic local Git integration tests.

## 4. Repository mapping

Implement:

```text
key
redmine_project_ids
path
revision
```

Rules:

- key unique;
- project IDs positive;
- project IDs globally unique across mappings;
- path absolute;
- revision defaults to `HEAD` if the approved configuration shape uses a default;
- no mapping is valid;
- one project maps to at most one repository.

Do not persist absolute repository paths as provenance.

## 5. Bare/mirror support

The implementation must work with a bare repository that has no working tree.

At least one integration test must use a real bare or mirror-style synthetic repository and prove revision resolution, tree listing, blob read, and commit-scoped search all work without checkout.

## 6. Git process execution

Use .NET process APIs with structured argument lists.

Do not invoke Git through shell command strings.

Apply consistently:

```text
GIT_NO_LAZY_FETCH=1
GIT_NO_REPLACE_OBJECTS=1
GIT_OPTIONAL_LOCKS=0
```

Capture stdout/stderr separately.

Do not forward ordinary Git stdout directly to application/MCP stdout.

## 7. Revision resolution

Resolve configured revision once to an exact commit.

Use safe option termination and commit verification.

After resolution, all tree/blob/search operations must use the exact resolved commit or object IDs derived from it.

Do not re-resolve the configured moving ref during the same resolved context.

## 8. Provenance

Produce a resolved source context containing at minimum:

```text
repository_key
revision_spec
object_format
commit_oid
root_tree_oid
```

Do not include absolute repository path, remote URL, or Git credentials in provenance-facing DTOs.

## 9. Tree enumeration

Use Git object/tree commands with machine-safe parsing.

Use NUL-terminated output where available.

Return at minimum:

```text
path
mode
object_type
object_oid
size
```

Support normal blobs, symlinks, and gitlinks without filesystem traversal.

## 10. Blob reading

Read committed blobs from the Git object database.

Do not read the working-tree file.

Required behavior:

```text
verify blob object type
inspect object size
enforce caller maxBytes
read exact bytes
enforce actual emitted byte count
```

Return bytes.

Do not decide text encoding or source relevance.

## 11. Commit-scoped search

Implement a deterministic search primitive using Git's commit/tree search capability.

Search takes at minimum:

```text
resolved source context
pattern
optional path filters
result limit
```

Requirements:

- searches the exact resolved commit;
- works in bare repositories;
- ignores working-tree modifications/untracked files;
- uses structured Git arguments;
- is bounded;
- returns factual matches only.

Do not add AI ranking or semantic search.

## 12. Symlinks and submodules

Do not follow symlinks into the host filesystem.

Do not initialize/traverse submodules.

Represent gitlink/submodule entries only as Git tree facts if encountered.

## 13. External synchronization boundary

Do not implement:

```text
clone
fetch
pull
push
remote update
```

The repository may be updated externally while IssueHarbor is running.

Tests must prove a previously resolved context remains frozen to its original commit after the configured ref is moved.

## 14. Git command deny check

Inspect production source to ensure no execution path invokes:

```text
clone
fetch
pull
push
checkout
switch
reset
clean
stash
commit
merge
rebase
submodule update
```

Do not build a generic command executor exposed to higher layers.

## 15. `safe.directory`

Do not execute wildcard `safe.directory=*` configuration or change global Git configuration.

Scope 008 does not change global Git config.

## 16. No SQLite migration

Do not add migration `002`.

Do not add source-provenance columns to Scope 005 storage yet.

Snapshot persistence belongs to Scope 009.

## 17. Required deterministic integration tests

Use synthetic local repositories and the actual Git executable.

At minimum cover:

### Mapping
- valid mapping;
- duplicate key;
- duplicate project ID;
- relative path rejection.

### Revision/provenance
- HEAD/ref/full commit resolution;
- non-commit failure;
- object format;
- root tree;
- no absolute path/remote URL in provenance.

### Moving ref
- resolve A;
- move ref to B;
- resolved context still reads/searches A.

### Working tree
- modified tracked file ignored;
- staged file ignored;
- untracked file ignored;
- dirty repo still readable.

### Bare repository
- resolve/list/read/search without checkout.

### Tree/blob
- recursive listing;
- unusual filenames;
- exact bytes;
- size/maximum enforcement;
- symlink not followed;
- gitlink not traversed.

### Search
- committed match;
- dirty/untracked content ignored;
- frozen commit after ref movement;
- result limit enforced.

### Git controls
- replacement objects disabled;
- lazy remote retrieval disabled;
- no mutation/update commands;
- cancellation/timeout kills child Git process.

If constructing a partial-clone network probe is not possible without network access, prove `GIT_NO_LAZY_FETCH=1` deterministically and add a local missing-object failure case.

## 18. Test data/public safety

Use synthetic repositories only.

Do not include real source files, private remote URLs, private project paths, Git credentials, or real Issue data.

Temporary test paths must not become tracked provenance/documentation.

## 19. Documentation updates

Archive:

```text
docs/scopes/scope-008-source-repository-context-contract.md
docs/scopes/scope-008-implementation-brief.md
```

Update the design plan and Ideas Registry only where necessary to mark repository mapping/source-context decisions as frozen by Scope 008.

Record that external automated repository synchronization is supported/expected for moving branches but operational implementation remains deferred.

## 20. Validation

Run:

```text
git --version
dotnet restore IssueHarbor.slnx
dotnet build IssueHarbor.slnx --no-restore
dotnet test IssueHarbor.slnx --no-build
```

Also run a source-context-focused test set if filterable.

Inspect:

```text
git status
git diff
git diff --check
```

Confirm synthetic Git tests pass, bare-repository tests pass, no real network access is required, no Git remote update command is executed, no working-tree source read is used, no private/personal source data appears, no migration `002` is added, and no unrelated files change.

## 21. Git boundary for IssueHarbor repository

The Implementation Agent may inspect, stage, and create the approved local IssueHarbor commit.

The Implementation Agent must never push IssueHarbor, write to the IssueHarbor remote, create/push IssueHarbor tags, create releases, or modify IssueHarbor remote settings.

Synthetic Git repositories used by tests may perform local-only commits/ref updates strictly inside temporary test directories.

## 22. Implementation report

Report:

1. files changed;
2. repository mapping/configuration structure;
3. Git process runner structure;
4. exact environment/read-only controls;
5. revision/provenance implementation;
6. tree/blob/search primitives;
7. bare repository validation;
8. moving-ref/frozen-context validation;
9. working-tree isolation validation;
10. Git mutation/network-boundary validation;
11. exact test counts/results;
12. restore/build/test evidence;
13. source-context-focused test result;
14. public-safety checks;
15. local IssueHarbor commit hash;
16. deviations/blockers/follow-up ideas;
17. confirmation that no push occurred.

## 23. Review sequence

After implementation:

1. GPT-5.6 Sol performs first architecture/scope review.
2. If blocking issues exist, revise before independent review.
3. DeepSeek-V41-Flash performs independent Git/path/process/read-only audit.
4. GPT-5.6 Sol reconciles findings.
5. Product Owner receives final decision material.

## 24. Completion rule

Scope 008 is ready for first review only when:

- mapping is deterministic;
- bare repositories work;
- revisions resolve once to immutable commits;
- provenance contains repository key/object format/commit/root tree and no absolute path;
- tree/blob/search operate on committed Git objects;
- dirty working-tree content cannot affect source context;
- lazy fetch and replacement objects are disabled;
- no Git update/mutation path exists;
- synthetic local Git integration tests pass;
- no SQLite migration is introduced;
- no push was performed.
