# IssueHarbor V2 / 1.0.0 — Scope 008 Source Repository Context Contract

**Scope:** 008 — Source Repository Context<br>
**Status:** APPROVED / READY FOR IMPLEMENTATION<br>
**Architecture / Scope Lead:** GPT-5.6 Sol<br>
**Implementation Agent:** GPT-6 Luna<br>
**Independent Reviewer:** DeepSeek-V41-Flash<br>

## 1. Purpose

Freeze IssueHarbor's read-only Git source-context model.

Scope 008 defines Redmine Project → Git repository mapping, local repository trust/configuration boundaries, immutable revision resolution, source provenance, tree enumeration, exact blob reading, commit-scoped source search, read-only/local-only Git process rules, and synthetic local Git integration tests.

Scope 008 does not create Analyzer Snapshots, select AI-relevant source files, update Git repositories, or implement deployment scheduling.

## 2. Reference deployment model

IssueHarbor is expected to run on NAS/server infrastructure independently from developer working copies.

The recommended reference model is:

```text
developer working copy on PC
        ↓ push
remote Git server
        ↓ external automated fetch
dedicated bare/mirror repository on NAS
        ↓ read-only Git object access
IssueHarbor
```

A working-tree checkout on the NAS is not required. IssueHarbor reads committed Git objects directly.

## 3. Repository synchronization boundary

IssueHarbor never performs remote synchronization.

IssueHarbor must not run:

```text
git clone
git fetch
git pull
git push
git remote update
```

Repository synchronization is external to IssueHarbor and may be automated.

External operator/deployment automation may create the source repository initially, periodically fetch/prune remote refs, and update local repository refs.

The automation, schedule, credentials, monitoring, and QNAP/NAS deployment of that synchronization are deferred to the deployment/operations scope.

`operator-managed` means an operational responsibility boundary; it does not require manual human fetch/pull commands.

## 4. Repository mapping cardinality

IssueHarbor 1.0 supports:

```text
one Redmine project
→ zero or one configured Git repository
```

No repository mapping is a valid state.

A single configured Git repository may serve multiple Redmine projects.

A Redmine project ID must not appear in more than one configured repository mapping.

Multiple repositories for one Redmine project are deferred.

## 5. Repository mapping configuration

Each configured repository has:

```text
key
redmine_project_ids
path
revision
```

Rules:

- `key` is required, unique, stable, and machine-friendly; recommended syntax is `[a-z0-9][a-z0-9._-]*`.
- `redmine_project_ids` contains one or more positive Redmine integer IDs and is globally unique across mappings.
- `path` is required, absolute, server-side/operator-controlled only, and may identify a normal Git repository or a bare/mirror Git repository.
- `revision` is required or defaults to `HEAD`, and may be a branch/ref/tag/full object name that resolves to a commit.

Repository paths must never come from Issue content, MCP input, AI output, or request parameters.

Invalid mapping configuration is a startup/configuration error.

## 6. Repository identity versus host path

Durable provenance uses `repository_key`.

The configured absolute repository path is deployment configuration only.

Do not persist or expose repository absolute paths in Snapshot artifacts, AI prompts, reports, durable source provenance, or comparison identity.

Do not persist remote Git URLs or Git credentials as source provenance.

## 7. Git CLI baseline

Use the system Git CLI through .NET process APIs.

Do not introduce LibGit2Sharp or a generic SCM abstraction.

All arguments must be passed through structured process argument APIs such as `ProcessStartInfo.ArgumentList`.

Do not invoke Git through shell command strings such as `cmd /c`, `powershell -Command`, `sh -c`, or `bash -c`.

Git becomes an IssueHarbor runtime dependency from Scope 008 onward.

## 8. Read-only Git boundary

IssueHarbor source access is read-only.

It must not run:

```text
checkout
switch
reset
clean
stash
commit
merge
rebase
branch creation/deletion
tag creation/deletion
git config writes
submodule update
```

IssueHarbor does not intentionally modify refs, the index, working tree, Git configuration, or remote state.

Future deployment should prefer OS/container read-only repository mounts where practical, but that behavior is deferred.

## 9. Working tree is not source authority

IssueHarbor analyzes committed Git objects, not the working directory.

Modified, staged, deleted, or untracked working-tree content must not affect source context.

A dirty working tree is not itself an error.

IssueHarbor must not use direct filesystem reads such as `File.ReadAllText(repositoryPath + sourcePath)` to obtain committed source content.

## 10. Bare/mirror repository support

Bare or mirror repositories are first-class supported source repositories.

IssueHarbor must not require a working tree, checkout, index population, or local branch checkout to enumerate, search, or read committed source.

This is the preferred reference pattern for NAS deployment.

## 11. Revision resolution timing

Source revision is resolved when the Issue begins source-context acquisition as part of later Snapshot acquisition.

It is not resolved when Analyzer starts, when a Request is accepted, or when repository synchronization occurs.

Flow:

```text
Issue begins processing
→ locate repository mapping by Redmine project_id
→ resolve configured revision once
→ freeze immutable Git provenance
→ all later reads use that exact resolved commit
```

## 12. Resolve once to immutable commit

The configured revision specification must resolve exactly once to a commit object, using an equivalent of:

```text
git rev-parse --verify --end-of-options <revision>^{commit}
```

After resolution, later operations must use the resolved full commit OID, not re-resolve the moving branch/ref.

If a branch moves during acquisition, an acquisition already frozen to commit A continues to read commit A.

## 13. Remote-tracking branch semantics

In an ordinary non-bare repository, external `git fetch origin` commonly updates remote-tracking refs such as:

```text
refs/remotes/origin/main
```

rather than the local checked-out branch.

Therefore repository configuration may intentionally use `refs/remotes/origin/main` for an externally fetched repository.

In a dedicated mirror/bare repository, the configured ref may follow that repository's mirror/fetch layout.

Scope 008 does not assume a working-tree local branch is current.

## 14. Frozen Git provenance

Resolved source provenance contains at minimum:

```text
repository_key
revision_spec
object_format
commit_oid
root_tree_oid
```

Identity authority for strict source comparison is:

```text
repository_key
object_format
commit_oid
```

`revision_spec` is useful provenance but is not immutable identity because several specs may resolve to the same commit.

`root_tree_oid` is retained for deterministic verification.

## 15. Git object format

Do not assume commit/tree/blob OIDs are always 40-character SHA-1 values.

Determine and retain the repository object format, supporting Git-reported formats such as `sha1` and `sha256`.

Object IDs are treated as full opaque Git object IDs paired with their object format.

## 16. Local-only Git object access

IssueHarbor must not trigger implicit remote object retrieval.

Every Git child process used for source context must disable lazy fetch behavior using the equivalent of:

```text
GIT_NO_LAZY_FETCH=1
```

If a required object is not locally available, source-context acquisition fails deterministically.

IssueHarbor does not contact the remote to repair it.

Shallow/partial repositories may be usable only when all required objects for the configured revision are locally present.

## 17. Disable replacement objects

Every source-context Git process must disable replacement-object behavior using the equivalent of:

```text
GIT_NO_REPLACE_OBJECTS=1
```

or Git's corresponding no-replace option.

The commit/tree/blob identity used by IssueHarbor must refer to the real local object database rather than locally configured replacement refs.

## 18. Reduce incidental Git writes

Source-context Git processes must disable optional locking behavior using the equivalent of:

```text
GIT_OPTIONAL_LOCKS=0
```

or the corresponding Git command option where appropriate.

This reinforces the read-only intent.

## 19. Repository trust and `safe.directory`

Configured repositories are operator-trusted repositories.

IssueHarbor must not automatically set `safe.directory=*` and must not globally disable Git ownership trust checks.

If deployment ownership requires `safe.directory`, later deployment configuration must trust only the specific configured repository paths.

## 20. Tree enumeration

IssueHarbor provides a deterministic committed-tree enumeration primitive.

Each file-like entry contains at minimum:

```text
path
mode
object_type
object_oid
size
```

Use Git object/tree operations equivalent to:

```text
git ls-tree -r -l -z --full-tree <resolved-commit>
```

Machine parsing must use NUL-safe output, not line-delimited filename parsing.

## 21. Path fidelity

Git paths are source-controlled Git tree paths, not host filesystem paths.

Preserve Git path content deterministically.

Do not resolve Git tree paths against the host filesystem to read committed content.

The parser must correctly handle filenames containing spaces or characters requiring ordinary Git quoting.

NUL-safe machine output is required where Git supports it.

## 22. Symlinks

Git symlink entries are not followed.

For mode `120000`, IssueHarbor treats the stored symlink target text as Git blob content only.

It must not dereference the target against the NAS/server filesystem.

## 23. Submodules / gitlinks

Gitlink entries such as mode `160000` may be represented as source tree entries with their path and object/commit identity.

IssueHarbor 1.0 does not run `git submodule update`, enter submodule repositories, or recursively analyze submodule content.

A submodule requiring first-class analysis must later be configured as its own approved source repository mapping.

## 24. Blob inspection and reading

Source blob reading is object-based.

Conceptual flow:

```text
tree entry
→ blob OID
→ verify object type
→ inspect object size
→ bounded read of exact blob bytes
```

The source reader returns bytes.

Scope 008 does not decide text encoding, binary classification, source relevance, Snapshot inclusion, or AI token selection.

The caller must provide a positive maximum byte limit.

Both reported object size and actual emitted bytes must respect the caller-provided limit.

## 25. Commit-scoped source search

Scope 008 includes a deterministic Git search primitive over a resolved exact commit.

Conceptually:

```text
Search(
    resolved_commit,
    pattern,
    optional_path_filters,
    result_limit
)
```

Implementation may use Git functionality equivalent to `git grep` against the resolved commit/tree.

Search must operate on committed Git objects, not require a working tree, not silently re-resolve a moving branch, not access Git remotes, use structured process arguments, use machine-safe output parsing, and be deterministically bounded.

Scope 008 does not define AI source relevance selection.

## 26. Search result contract

Each search match should retain enough factual source identity to allow later exact blob/path retrieval, including at minimum:

```text
path
line_number when available
matched source text or bounded match excerpt
```

Search output must not invent summaries or semantic classifications.

Exact Snapshot inclusion rules remain deferred.

## 27. No generic SCM abstraction

Concrete components are allowed, for example:

```text
GitRepositoryCatalog
GitRepositoryReader
GitProcessRunner
ResolvedGitRepository
GitTreeEntry
GitSearchMatch
```

Do not introduce `ISourceControlProvider`, `IScmAdapter`, repository-provider factories, Git-or-SVN abstractions, or a generic source-control plugin system.

IssueHarbor 1.0 source repository support is Git-specific.

## 28. Unmapped versus broken mapping

No repository mapping for the Issue's Redmine project means:

```text
source context absent
```

This is not automatically an error.

If a mapping exists but the path is missing, it is not a Git repository, Git rejects repository ownership/trust, the revision cannot resolve, a required Git object is missing, or a Git command fails, source-context acquisition fails deterministically.

Scope 008 does not invent a new Request Item reason enum.

Later Snapshot/execution scopes decide how this failure affects Issue processing.

## 29. Strict comparison provenance

Later strict comparison uses frozen source provenance.

At minimum, strict source equality requires:

```text
repository_key
object_format
commit_oid
```

to match.

The resolved exact commit, not the moving revision name, is the source identity.

## 30. No SQLite migration in Scope 008

Scope 008 does not add migration `002`.

Repository mappings are runtime/server configuration.

Resolved Git provenance belongs to the later Snapshot contract.

Scope 009 decides the exact Snapshot artifact shape and whether any additional catalog fields are required.

## 31. Security and privacy

Repository configuration and tests must remain public-safe.

Do not commit private repository URLs, Git credentials, real source content, real private host paths, SSH keys, access tokens, or real Issue data.

Use synthetic repositories and synthetic commit/file content in tests.

Absolute repository paths used at runtime must not leak into durable provenance, AI prompts, reports, or ordinary source-context output.

## 32. External synchronization deferred dependency

For moving branches, a useful deployment normally requires external repository synchronization.

Scope 008 explicitly supports this operational pattern:

```text
remote Git
→ external scheduled fetch/prune
→ local bare/mirror repository
→ IssueHarbor read-only source access
```

But Scope 008 does not implement fetch scripts, schedules, Git remote credentials, failure monitoring, QNAP cron configuration, or repository initialization orchestration.

These belong to the later deployment/operations scope and may be split into a dedicated source-synchronization scope if the operational work becomes substantial.

## 33. Minimum deterministic test matrix

Implementation must use real local synthetic Git repositories and the real installed Git executable for integration behavior.

At minimum prove:

### Mapping/configuration

1. valid repository mapping loads;
2. duplicate repository key is rejected;
3. duplicate Redmine project mapping is rejected;
4. relative repository path is rejected;
5. invalid/empty revision is rejected as configuration or resolution failure as appropriate.

### Resolution/provenance

6. `HEAD` resolves to an exact commit;
7. a ref resolves to an exact commit;
8. a full commit OID resolves;
9. a non-commit revision fails;
10. object format is recorded;
11. root tree OID is recorded;
12. provenance excludes absolute repository path;
13. provenance excludes remote URL.

### Moving refs

14. resolve branch/ref to commit A;
15. move/update the branch/ref to commit B;
16. continued operations on the previously resolved context still read commit A.

### Working-tree isolation

17. tracked working-tree modification does not change committed read;
18. staged uncommitted content does not change committed read;
19. untracked files do not appear in committed tree/search;
20. a dirty working tree does not itself fail source access.

### Tree/blob behavior

21. recursive tree listing works;
22. filenames with spaces/special characters parse correctly;
23. blob bytes match committed content;
24. blob size is enforced;
25. caller maximum byte ceiling is enforced;
26. symlink is not followed;
27. gitlink/submodule content is not traversed.

### Search

28. commit-scoped search finds committed matches;
29. search ignores dirty/untracked working-tree content;
30. search remains frozen to a previously resolved commit after ref movement;
31. search result count/output is deterministically bounded.

### Local-only/read-only controls

32. Git processes receive lazy-fetch disabling;
33. Git replacement objects do not alter resolved/read source;
34. optional-lock/read-only intent is applied;
35. missing required local object fails rather than silently contacting a remote;
36. no source code execution path invokes clone/fetch/pull/push/checkout/reset/clean/submodule update;
37. cancellation/timeout terminates Git child processes without leaving an orphan process.

Equivalent organization is acceptable if all approved behaviors are directly proven.

## 34. Explicitly deferred

Scope 008 does not implement or decide:

- Snapshot JSON;
- source relevance/file selection policy;
- text encoding policy;
- binary/text classification policy;
- AI source selection;
- attachment/source combination;
- Analyzer Request execution;
- repository clone/fetch/pull/update;
- external sync scheduling;
- Git remote credentials;
- submodule traversal;
- multiple repositories per Redmine project;
- SQLite migration;
- repository-management UI;
- GitHub/GitLab provider integration.

## 35. Acceptance rule

The implemented source-context layer must make it deterministic to answer:

- Which configured repository belongs to a Redmine project?
- Which exact commit was frozen for this source context?
- Which exact root tree belongs to that commit?
- Which Git object format is in use?
- Can moving branches change an already-resolved source context? (No.)
- Can dirty/untracked working-tree content affect source reads? (No.)
- Can IssueHarbor read/search source without checkout? (Yes.)
- Can IssueHarbor follow symlinks into the host filesystem? (No.)
- Can IssueHarbor silently fetch missing objects? (No.)
- Can Git replacement refs rewrite the analyzed provenance? (No.)
- Can IssueHarbor update a remote/source repository? (No.)
- Can absolute repository paths leak into durable source provenance? (No.)

Any future widening of source repository cardinality, SCM type, remote access, or synchronization responsibility requires an explicit approved scope decision.
