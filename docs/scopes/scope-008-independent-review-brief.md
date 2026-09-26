# IssueHarbor V2 / 1.0.0 — Scope 008 Independent Review Brief

**Scope:** 008 — Source Repository Context\
**Review stage:** Independent Git/path/process/read-only audit\
**Architecture / Scope Lead:** GPT-5.6 Sol\
**Implementation Agent:** GPT-6 Luna\
**Independent Reviewer:** DeepSeek-V41-Flash\
**Sol first-review result:** PASS TO INDEPENDENT REVIEW\

## 1. Review purpose

Independently audit the implemented Scope 008 source-repository context against the approved Source Repository Context Contract and Implementation Brief.

Do not redesign the source architecture, add remote synchronization, widen SCM support, or move Snapshot behavior into Scope 008.

Report concrete correctness, reproducibility, Git-object, filesystem/path, process-lifecycle, trust-boundary, local-only, or read-only issues only.

## 2. Implementation under review

Implementation commit:

`9b614ac4b604e1dc2982ff242ee498785f306a0a`

Commit message:

`feat: implement Git source repository context`

Reported validation:

- isolated restore: PASS;
- build: PASS, 0 warnings / 0 errors;
- full test suite: **102 / 102 passed**;
- SourceContext-focused suite: **22 / 22 passed**;
- no SQLite migration `002`;
- no push performed.

Known environment limitation:

- standard restore could not read a user-level NuGet configuration under the current execution account;
- isolated NuGet configuration/cache restored successfully;
- this should not be treated as a Scope 008 defect unless the repository itself depends on machine-specific NuGet configuration.

## 3. Frozen source model to audit

The implementation must preserve:

- one Redmine project -> zero or one configured Git repository;
- one repository may map to multiple Redmine projects;
- server-side absolute repository paths only;
- durable provenance uses repository key, not host path;
- Git CLI only;
- structured process arguments, no shell;
- no clone/fetch/pull/push;
- no checkout/switch/reset/clean/stash/commit/merge/rebase;
- no submodule traversal;
- no wildcard `safe.directory`;
- no SQLite migration;
- no Snapshot implementation;
- no generic SCM abstraction;
- working-tree content is never source authority;
- bare/mirror repositories work without checkout;
- revision resolves once to an exact commit;
- later reads/searches stay pinned to that commit;
- lazy fetch disabled;
- replacement objects disabled;
- optional locking disabled;
- committed-tree/blob/search primitives are bounded and deterministic.

## 4. High-priority audit questions

### A. Repository mapping and configuration

Verify:

1. repository keys are unique;
2. Redmine project IDs are positive;
3. one Redmine project cannot map to multiple repositories;
4. one repository may map to multiple project IDs;
5. paths must be absolute;
6. revision defaults/validation match the approved contract;
7. an unmapped project returns source-context absent rather than an error;
8. mapping DTOs/configuration cannot leak absolute paths into provenance-facing results.

Check whether configuration parsing has ambiguous duplicate-key/project behavior.

### B. Git process execution safety

Inspect the actual Git runner.

Confirm:

1. `ProcessStartInfo.ArgumentList` or equivalent structured arguments are used;
2. no `cmd /c`, PowerShell shell string, `sh -c`, or `bash -c`;
3. stdout/stderr are captured separately;
4. output limits are enforced;
5. timeout/cancellation terminates the child process tree;
6. no orphan Git process remains after timeout/cancellation;
7. production callers cannot supply arbitrary Git subcommands;
8. no shell metacharacter in revision/path/search input can escape argument boundaries.

### C. Local-only Git controls

Verify every source-context Git process receives:

```text
GIT_NO_LAZY_FETCH=1
GIT_NO_REPLACE_OBJECTS=1
GIT_OPTIONAL_LOCKS=0
```

Confirm:

1. missing local objects fail rather than contacting a remote;
2. replacement refs do not alter resolved/read content;
3. no code path selectively omits these environment controls;
4. no Git command used by the source layer can initiate remote access indirectly.

### D. Read-only guarantee

Search production source for Git commands and process construction.

Confirm there is no execution path for:

```text
clone
fetch
pull
push
remote update
checkout
switch
reset
clean
stash
commit
merge
rebase
branch mutation
tag mutation
config write
submodule update
```

Distinguish test-only synthetic repository construction from production behavior.

### E. `safe.directory` / repository trust

Verify:

1. no wildcard `safe.directory=*`;
2. no global Git configuration writes;
3. ownership/trust failures remain deterministic failures;
4. production code does not silently weaken Git trust checks.

### F. Revision resolution

Audit the exact revision-resolution command.

Confirm:

1. safe option termination is used;
2. target must resolve to a commit;
3. non-commit objects are rejected;
4. full OID is retained;
5. object format is retained;
6. root tree OID is derived from the resolved commit;
7. later tree/blob/search operations never re-resolve the moving revision spec.

### G. Moving-ref reproducibility

Verify with code/tests/probes:

1. resolve ref to commit A;
2. update ref to commit B;
3. existing resolved context still lists/reads/searches commit A;
4. a fresh resolution may obtain commit B.

This is a core reproducibility invariant.

### H. Bare/mirror repository support

Verify a real bare repository with no working tree supports:

- revision resolution;
- object-format discovery;
- root-tree resolution;
- tree listing;
- blob read;
- commit-scoped search.

Make sure no hidden working-tree assumption exists in path handling or Git command selection.

### I. Working-tree isolation

For a normal repository, verify:

1. modified tracked files do not affect tree/blob/search;
2. staged but uncommitted files do not affect source context;
3. untracked files do not appear;
4. deleted working-tree files do not remove committed content;
5. dirty working tree does not itself fail source access;
6. production source reads never fall back to host filesystem file reads.

Search for direct `File.ReadAllText`, `File.ReadAllBytes`, or equivalent against repository source paths.

### J. Tree enumeration / NUL parsing

Audit tree parsing carefully.

Verify:

1. recursive listing is from the exact resolved commit;
2. output is machine-safe/NUL-delimited where required;
3. filenames with spaces parse correctly;
4. filenames with tabs/newlines or Git-quoted characters cannot corrupt parsing;
5. mode, object type, OID, size, and path are associated correctly;
6. malformed Git output fails deterministically rather than silently producing corrupt entries.

### K. Symlink safety

Verify mode `120000` is treated as blob content only.

IssueHarbor must never dereference the symlink against the host filesystem.

Use a test/probe whose symlink target points outside the synthetic repository and confirm no host read occurs.

### L. Gitlink/submodule boundary

Verify mode `160000` is represented only as a Git tree fact.

Confirm:

- no `git submodule` command;
- no filesystem traversal into a checked-out submodule;
- no recursive source read of the gitlink target repository.

### M. Blob read correctness and ceilings

Verify:

1. requested object must be a blob;
2. object size is inspected before full read;
3. caller maxBytes must be positive;
4. oversize blobs are rejected before unsafe allocation where possible;
5. actual emitted bytes are also bounded;
6. returned bytes match committed blob content exactly;
7. integer conversions/size arithmetic cannot overflow;
8. missing/corrupt object produces deterministic failure.

### N. Commit-scoped search

Audit the Git search primitive.

Confirm:

1. search targets the exact resolved commit;
2. it works in bare repositories;
3. it does not search the working tree;
4. dirty/untracked content cannot appear;
5. moving refs cannot change an already-resolved search context;
6. search result limits are enforced deterministically;
7. optional path filters are passed as structured arguments;
8. pattern handling cannot become shell/Git option injection;
9. parser is machine-safe for filenames and result text;
10. result excerpts are bounded.

### O. Search semantics versus Git options

Check whether search is literal/fixed-string or regex-based.

The implementation report says literal search. Verify the actual Git options match that semantic contract and that special regex characters in the input pattern are treated as intended.

Flag any mismatch between declared search semantics and actual `git grep` behavior.

### P. Provenance privacy

Verify the resolved provenance DTO contains only approved source identity:

```text
repository_key
revision_spec
object_format
commit_oid
root_tree_oid
```

and does not include:

```text
absolute repository path
remote URL
Git credentials
working-tree path
```

Also inspect ordinary logs/errors for accidental absolute-path or remote-URL leakage.

### Q. Object-format handling

Verify SHA-1 and SHA-256 assumptions are not hard-coded into object-ID length validation or parsing.

If the test environment cannot easily create SHA-256 repositories, inspect the parsing/validation paths and add a deterministic probe where practical.

### R. Synthetic missing-object behavior

Confirm the missing-object test actually removes or makes unavailable the required local object and that the subsequent read fails locally.

The test must not succeed by reading a cached working-tree file or another repository copy.

### S. Output/process bounds

Review stdout/stderr ceilings for Git commands.

Confirm:

- normal large-tree/search output has deterministic limits;
- exceeding limits terminates the child process safely;
- error reporting does not echo unbounded Git output;
- cancellation/timeout/output-limit paths do not leak orphan processes.

## 5. Test strength assessment

The implementation reports 22 SourceContext integration tests.

Do not rely only on method names.

Verify important tests hit:

- the real installed `git` executable;
- real temporary Git repositories;
- real bare repository behavior;
- real ref movement;
- real staged/untracked/dirty worktree states;
- real symlink/gitlink modes;
- real replacement-ref behavior;
- real missing-object failure;
- real child-process timeout/cancellation.

Identify approved contract behaviors that are implemented but insufficiently tested.

## 6. External synchronization boundary

Scope 008 supports but does not implement:

```text
remote Git
→ external scheduled fetch/prune
→ local bare/mirror repository
→ IssueHarbor read-only
```

Do not require fetch automation, credentials, QNAP scheduling, or monitoring in this review.

Do flag any IssueHarbor production code that itself performs remote synchronization.

## 7. Explicitly out of scope

Do not require:

- Snapshot JSON;
- Snapshot acquisition;
- source relevance selection;
- AI source ranking;
- text encoding policy;
- binary/text classification;
- attachment/source combination;
- Analyzer execution;
- external fetch scripts/scheduling;
- Git credentials;
- submodule traversal;
- multiple repositories per Redmine project;
- SQLite migration;
- repository-management UI;
- GitHub/GitLab provider integrations.

## 8. Review output format

Return:

### Decision

Exactly one of:

- `PASS`
- `PASS WITH NON-BLOCKING FINDINGS`
- `NEEDS REVISION`
- `BLOCKED`

### Blocking findings

For each blocking finding include:

- exact file/component;
- observed behavior;
- violated approved contract rule;
- correctness/security/reproducibility impact;
- smallest compliant correction;
- deterministic test proving the correction.

### Non-blocking findings

Include only concrete maintainability, test-strength, privacy, or robustness findings worth recording.

Do not propose speculative architecture.

### Evidence reviewed

List source-context production files, configuration/binding files, test files, scope documents, and commands/probes actually executed.

### Scope check

State explicitly whether the implementation introduced any out-of-scope SCM, remote-sync, Snapshot, migration, or repository-management behavior.

## 9. Independence rule

Review the approved Scope 008 contract and implementation independently.

Do not introduce generic SCM abstractions, remote synchronization, checkout-based analysis, new storage schema, or Snapshot behavior unless the current implementation violates an approved Scope 008 requirement.
