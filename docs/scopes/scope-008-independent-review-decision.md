# IssueHarbor V2 / 1.0.0 — Scope 008 Independent Review Decision

**Scope:** 008 — Source Repository Context\
**Status:** PASS WITH NON-BLOCKING FINDINGS\
**Review type:** Independent Git / path / process / read-only audit\
**Architecture / Scope Lead:** GPT-5.6 Sol\
**Implementation Agent:** GPT-6 Luna\
**Independent Reviewer:** DeepSeek-V41-Flash\
**Review brief:** `docs/scopes/scope-008-independent-review-brief.md`

---

## 1. Decision

**PASS WITH NON-BLOCKING FINDINGS**

`9b614ac4b604e1dc2982ff242ee498785f306a0a`（`feat: implement Git source repository context`）未发现阻断级缺陷。只读边界、本地化控制、不可变修订解析、裸仓库支持与机器安全解析均按已批准契约实现，并以真实 Git 2.53.0 + 真实临时仓库 + 仓库外探针验证。

### 已确认通过的关键控制

**只读 / 无逃逸口（D、S）** — 生产源码中出现的 Git 动词**穷举**为 `ls-tree`、`cat-file`、`rev-parse`、`grep`（`GitRepositoryReader.cs` 内 7 处参数表 + search 一处）。grep 全量扫描确认 `src/` 中**不存在** `clone`/`fetch`/`pull`/`push`/`remote`/`checkout`/`switch`/`reset`/`clean`/`stash`/`commit`/`merge`/`rebase`/`branch`/`tag`/`config`/`submodule`/`update-index`/`add`/`apply`/`gc`/`repack`/`prune`/`worktree`。唯一进程构造处为 `UseShellExecute = false`，无 `cmd`/`powershell`/`sh -c`/`bash -c`。

**工作树隔离（I）** — `src/Analyzer/SourceContext` 与 `Program.cs` 中**没有任何** `File.Read*`/`File.Write*`/`File.Open*` 调用（grep 为空）。全部内容来自 `cat-file`/`grep` 的对象访问。探针实测：删除工作树中已跟踪的 `a.txt` 后，`ListTreeAsync` 仍列出该条目且 blob 读取仍返回已提交内容。

**修订解析与可复现性（F、G）** — `rev-parse --verify --end-of-options <rev>^{commit}`，非 commit 规格在解析阶段失败；`HEAD`、`refs/heads/*`、完整 OID 三种规格均解析到同一 commit（既有测试）。探针复核：分支移动后，先前冻结的 context 继续读取旧 commit 的树、blob 与搜索结果，而新解析得到新 commit。

**本地化控制（C）** — `GitProcessRunner` 为**每一次**子进程设置 `GIT_NO_LAZY_FETCH=1`、`GIT_NO_REPLACE_OBJECTS=1`、`GIT_OPTIONAL_LOCKS=0`（另加 `GIT_TERMINAL_PROMPT=0`），并主动移除 `GIT_DIR`、`GIT_WORK_TREE`、`GIT_INDEX_FILE`、`GIT_OBJECT_DIRECTORY`、`GIT_ALTERNATE_OBJECT_DIRECTORIES`、`GIT_CONFIG_PARAMETERS`、`GIT_CONFIG_KEY_*`/`GIT_CONFIG_VALUE_*`、全部 `GIT_TRACE*`/`GIT_TRACE2*`、`GIT_SSH_COMMAND` 等。存在注入 `GIT_CONFIG_PARAMETERS` 的对抗性探针空间，实现已覆盖。

**惰性抓取抑制（实证）** — 我构造了一个**声明 promisor 远程**（`remote.origin.promisor=true`，URL 指向会写标记文件的 `ext::` 传输脚本）的仓库并删除目标 blob 的松散对象：

| 场景 | Git 行为 | 传输脚本是否被调用 |
|---|---|---|
| `GIT_NO_LAZY_FETCH` 未设置（对照） | 尝试抓取，报 `fatal: transport 'ext' not allowed` | 否（被 Git 策略阻挡，但**确实发起了抓取尝试**） |
| `GIT_NO_LAZY_FETCH=1`（实现所设） | 直接报 `bad file`，无任何抓取尝试 | 否 |
| 经 `GitRepositoryReader.ReadBlobAsync` | `ObjectMissing` | 否 |

即：实现确实切断了「缺失对象 → 联系远程」的路径，且以确定性错误收场。

**裸仓库 / 无工作树（H）** — 探针以真实 bare 仓库（`git init --bare` + 外部 `fetch`，无任何工作文件）完成解析、对象格式发现、根树解析、树列举（含换行路径条目）、blob 读取与搜索。无隐藏的工作树假设：树列举使用 `--full-tree`，blob/搜索均按 commit 或对象 OID 访问。

**NUL 安全 / 特殊文件名（J）** — 这是本次审查投入最多的一处。Windows 的文件系统**不允许**文件名包含换行或制表符，所以我绕过索引，用 `git hash-object -w` 与 `git mktree -z` 直接构造了包含 `"new\nline.txt"` 与 `"tab\tname.txt"` 的**真实树对象**：

- 原始 `git ls-tree -r -l -z --full-tree` 输出确实包含裸换行与裸制表符，未做引号转义；
- `ListTreeAsync` 逐条正确还原为 `path=new\nline.txt`（size 15）与 `path=tab\tname.txt`（size 11），未溃散、未错位；
- `SearchAsync` 同样正确还原两条路径；
- 该树的 blob 在 bare 仓库中也能正确读取。

这直接证明解析器是按字节/NUL 解析而非按行解析，`contract §21`（含空格或需 Git 引号的名称）与简报 J.4 得到满足。

**字面搜索语义（O）** — `-F` 使搜索为固定字符串。探针复核：`a.1` 在仅存在 `a1` 时返回 0（正则本应命中），在新增 `a 1` 后**在新 commit 上仍返回 0**（正则本应命中 1）。`n[ee]dle`、`needle$` 均返回 0。声明语义与 `git grep` 实际行为一致。

**提交作用域搜索（N）** — `git grep ... <commitOid> --`，输出解析强制路径前缀必须等于 `<commitOid>:`，否则 `InvalidGitOutput`。探针确认原始输出形如 `<oid>:<raw path>\0<line>\0<matched text>\n`，解析器与之一致；二进制文件因 `-I` 被跳过（`-I` 语义为「无匹配即视为二进制」，对文本搜索安全）。

**路径过滤安全（N.7/8）** — `IsSafePathFilter` 拒绝空/空白、NUL、反斜杠、前导 `/`、前导 `:`、`Path.IsPathRooted`、以及任何为 `.` 或 `..` 的路径段；参数以 `--` 终止选项解析。探针实测：`./a.txt`、`dir/../a.txt`、`:!a.txt` 被 `SearchInputInvalid` 拒绝；`a.txt`、`dir with spaces`、`a.txt/`、`--all` 作为普通 pathspec 被接受（无选项注入）。

**进程生命周期与边界（B、S）** — `stdout` 与 `stderr` 分别异步读取；stdout 有调用方上限、stderr 固定 64 KiB；超限、超时、取消三条路径都调用 `Kill(entireProcessTree: true)` 并 `WaitForExitAsync` 等待回收，随后 drain 读取任务，避免僵尸进程与管道阻塞。既有测试用**真实子进程**（`GitProcessStub` 写入自身 PID 后挂起）断言超时/取消/超限后该 PID 不再存活。

**配置与映射（A）** — key 正则 `^[a-z0-9][a-z0-9._-]*$`、key 唯一、路径必须 `Path.IsPathFullyQualified`、项目 ID 必须为正、同一映射内不得重复、跨映射重复项目 ID 被拒、`Revision` 缺省为 `HEAD`。未映射项目返回 `null`（source-context absent，非错误）。

**溯源隐私（P）** — 序列化结果为 `{"repository_key","revision_spec","object_format","commit_oid","root_tree_oid"}`，实测 `Contains(repoPath) == false`。`RepositoryPath` 仅作 `internal` 成员存在且无 `[JsonPropertyName]`，不参与序列化。

**对象格式（Q）** — `rev-parse --show-object-format=storage` 结果必须为 `sha1` 或 `sha256`；`IsObjectId` 只校验「非空且全为小写十六进制」，**不假设长度**。既有测试创建真实 SHA-256 仓库并断言 64 位 OID 被保留。

**无迁移（契约 §30）** — `src/Analyzer/Storage/Migrations/` 下仅有 `001_initial.sql`，无 `002`。

### 执行的验证

| 项目 | 结果 |
|---|---|
| `dotnet build IssueHarbor.slnx --no-restore` | 成功，0 警告 / 0 错误 |
| `dotnet test IssueHarbor.slnx --no-build` | **102 / 102 通过** |
| `dotnet test --filter FullyQualifiedName~SourceContext` | **22 / 22 通过** |
| 源码级 grep（Git 动词、File 读写、shell、环境变量） | 见上 |
| 仓库外探针（真实 Git 2.53.0 + 真实临时仓库） | NUL/特殊路径、字面语义、每文件匹配上限、裸仓库、工作树隔离、缺失路径、路径过滤边界、惰性抓取抑制 |

关于已知环境限制：本次会话中常规 `restore` 与构建均成功，未复现任何 NuGet 配置/网络问题，也未发现包版本不一致或依赖机器特定配置的情况。

`GitRepositoryReaderTests` 确为**真实集成测试**：使用真实 `git` 可执行文件、真实临时仓库（`.work/` 下，已 gitignore）、真实裸仓库、真实 ref 移动、真实 staged/untracked/dirty 状态、真实 `120000`/`160000` 模式、真实 replace ref、真实松散对象删除、真实子进程超时/取消/超限。

---

## 2. Blocking findings

无。

以下曾被怀疑、经复核后**确认不成立**的阻断项，记录以说明审查深度：

| 疑似阻断项 | 复核结论 |
|---|---|
| 含换行/制表符的 Git 路径是否会破坏 NUL 解析 | 不成立。用 `mktree -z` 注入真实异常路径后，树与搜索均逐条正确还原 |
| 缺失对象时是否可能静默联系远程 | 不成立。设置 promisor 远程与可记录传输的对照实验证明 `GIT_NO_LAZY_FETCH=1` 切断了抓取尝试 |
| 搜索是否可能搜索工作树而非提交 | 不成立。搜索以 `<commitOid>` 为目标，且解析器强制输出路径前缀为该 OID |
| 是否可能通过 revision 或路径过滤注入 Git 选项 | 不成立。revision 使用 `--end-of-options`，路径过滤走 `--` 且拒绝前导 `-`/`:`/`/`/`..` |
| 是否可能通过环境变量注入 Git 配置（如 `safe.directory=*`） | 不成立。`GIT_CONFIG_PARAMETERS`、`GIT_CONFIG_COUNT`、`GIT_CONFIG_KEY_*`、`GIT_CONFIG_VALUE_*` 均被移除，且未写入任何 Git 配置 |
| 是否有直接文件系统读取已提交内容 | 不成立。SourceContext 生产代码无任何 `File.Read*` |
| 裸仓库是否需要工作树 | 不成立。真实 bare 仓库完整通过解析、列举、读取、搜索 |
| 是否新增 SQLite 迁移 002 | 不成立。迁移目录仅含 `001_initial.sql` |

---

## 3. Non-blocking findings

### N1. `git grep -m <resultLimit>` 是「每文件上限」，不是「全局结果上限」

- **文件：** `src/Analyzer/SourceContext/GitRepositoryReader.cs:227`
- **观察到的行为：** 参数表传入 `"-m", resultLimit`。实测 `git grep -m 1` 在 3 个匹配文件上产出 **3 条**记录（每文件各 1 条），而非 1 条；实现随后在解析循环中按 `matches.Count < resultLimit` 截断，因此**返回值**确实受限于 `resultLimit`，但**子进程实际产出的数据量是「每文件至多 resultLimit 条」的全仓库总量**。
- **影响：** 不破坏确定性，也不越权（`MaximumSearchOutputBytes = 16 MiB` 上限仍然生效）。但在大型仓库上，一次 `resultLimit=1000` 的常见搜索可能产出远超预期的数据量、触及 16 MiB 上限而被判为 `InvalidGitOutput`（整个工具调用失败），而不是安静地返回较少结果。属可用性/健壮性问题。
- **最小合规修正：** 将 `-m` 固定为较小值（例如 `-m 1` 或 `-m 2`）并在客户端累计到 `resultLimit` 后停止消费，或显式记录该语义。
- **证明该行为的测试：** 构造每文件多行匹配、且匹配文件数远大于 `resultLimit` 的仓库，断言（a）返回条数等于 `resultLimit`，（b）子进程不会被 16 MiB 上限杀掉。

### N2. 「缺失对象不联系远程」的既有测试未真正证明惰性抓取被抑制

- **文件：** `tests/Analyzer.Tests/SourceContext/GitRepositoryReaderTests.cs:380`（`Missing_local_blob_fails_without_remote_retrieval`）
- **观察到的行为：** 该测试删除松散对象后断言得到 `ObjectMissing`，但合成仓库**没有配置任何远程**。因此它证明的是「本地缺失即确定性失败」，而非「不会去远程取回」——后者是契约 §16 与简报 C.1 的核心要求。
- **影响：** 契约中最关键的一条本地化保证缺乏回归保护。我用 promisor 远程 + 记录型 `ext::` 传输的对照实验证明了**当前实现是对的**（见 §1），但该证明不在仓库测试中，未来重构 `GitProcessRunner` 的环境变量处理时不会被现有测试拦住。
- **最小合规修正：** 新增一条集成测试：为合成仓库配置 `remote.origin.promisor=true` 与指向可记录调用的假传输脚本，删除目标松散对象，断言（a）`ReadBlobAsync` 抛 `ObjectMissing`，（b）传输脚本从未被调用。
- **证明该修正的测试：** 即上述测试本身；其对照分支（临时取消 `GIT_NO_LAZY_FETCH`）应显示 Git 会尝试抓取，以证明该断言具备判别力。

### N3. 针对「需 Git 引号的路径」缺少确定性回归测试

- **文件：** `tests/Analyzer.Tests/SourceContext/GitRepositoryReaderTests.cs:176`
- **观察到的行为：** 现有测试覆盖的异常名称为 `"directory with spaces/quote ' café [one].txt"`（空格、单引号、非 ASCII）。但 Git 只在路径含**双引号、反斜杠、控制字符**（换行、制表符）时才启用 `core.quotePath` 引号包装；空格与非 ASCII 并不触发。因此「路径被 Git 引号包装」这一分支实际上从未被覆盖，其中换行/制表符路径在当前平台上**无法通过文件系统创建**（NTFS 禁止控制字符；`"` 与 `\` 则可以创建但未被现有测试使用）。
- **影响：** 契约 §21 与简报 J.4 明确要求该类名称不得破坏解析。当前实现是正确的：`-z` 使 `ls-tree`/`grep` 输出**不做**引号包装而直接给出原始路径字节，我用 `hash-object` + `mktree -z` 注入真实换行/制表符路径验证解析正确。但该正确性缺乏回归保护——一旦有人移除 `-z` 或改用 `core.quotePath=true` 的假设，解析会静默错位，而现有测试不会失败。
- **最小合规修正：** 在测试中绕过文件系统，用 `git hash-object -w --stdin` + `git mktree -z` 构造含 `\n`/`\t` 路径的树并提交，断言 `ListTreeAsync` 与 `SearchAsync` 逐条还原原始路径字节。可另加一条低成本用例，用文件系统创建含 `"` 与 `\` 的常规文件以覆盖引号相关分支。
- **证明该修正的测试：** 断言路径字符串等于含真实 `\n`/`\t`（以及 `"`/`\`）的期望值，而非抛 `InvalidGitOutput` 或返回被引号包装的路径。

### N4. 路径不存在与路径不是 Git 仓库的失败码相同且诊断信息不准确

- **文件：** `src/Analyzer/SourceContext/GitProcessRunner.cs:280`、`GitRepositoryReader.cs:312`
- **观察到的行为（探针实测）：**

  | 配置情形 | 结果 |
  |---|---|
  | 路径不存在 | `GitCommandFailed: The Git executable could not be started.` |
  | 路径存在但不是 Git 仓库 | `GitCommandFailed: Git object format discovery failed.` |

  第一种情形的真实原因是**工作目录不存在**导致 `Process.Start` 抛出 `Win32Exception`，但消息声称「Git 可执行文件无法启动」，会误导排障方向（契约 §28 把「路径缺失」与「不是 Git 仓库」并列为不同的失败原因）。
- **影响：** 失败是确定性的、无信息泄漏，仅诊断准确性问题。
- **最小合规修正：** 在启动失败分支中区分「工作目录不存在」并给出对应消息，或让 `GitRepositoryCatalog`/`ResolveMappingAsync` 在启动前单独校验目录存在性。
- **证明该修正的测试：** 断言两种情形各自产生可区分的错误消息（或专有失败码），且都不含绝对路径。

### N5. 记录项：空搜索模式被接受并匹配每一行

- **文件：** `src/Analyzer/SourceContext/GitRepositoryReader.cs:209`
- **观察到的行为：** `pattern` 仅校验非 `null` 且不含 NUL，因此空字符串被接受；探针实测 `SearchAsync(context, "", null, 10)` 返回 10 条匹配（即每一行）。
- **影响：** 无语义破坏（固定字符串语义下空模式匹配一切是 Git 的既定行为），但空模式在 `redmine_*`/后续 Snapshot 场景中很可能是调用方错误而非意图。契约未要求拒绝，故仅作记录，不建议在本范围修改。

---

## 4. Evidence reviewed

**生产文件**

- `src/Analyzer/SourceContext/GitProcessRunner.cs` — 进程构造、环境变量策略、边界读取、超时/取消/超限终止
- `src/Analyzer/SourceContext/GitRepositoryReader.cs` — 解析、树列举、blob 读取、搜索、路径过滤校验
- `src/Analyzer/SourceContext/GitRepositoryCatalog.cs` — 映射校验与索引
- `src/Analyzer/SourceContext/SourceContextContracts.cs` — 失败码、`ResolvedSourceContext`、`GitTreeEntry`、`GitSearchMatch`
- `src/Analyzer/SourceContext/InternalsVisibleTo.cs`、`src/Analyzer/Storage/StorageInternals.cs`
- `src/Analyzer/Program.cs` — 启动期映射绑定与 DI 注册
- `src/Analyzer/Storage/Migrations/` — 确认不存在 `002`

**测试文件**

- `tests/Analyzer.Tests/SourceContext/GitRepositoryReaderTests.cs`（22 个 `[TestMethod]`，含清理越界防护与 5 次重试）
- `tests/Analyzer.Tests/SourceContext/GitProcessStub/Program.cs`（`hold` / `environment` / `overflow` 三种真实子进程行为）

**契约与文档**

- `docs/scopes/scope-008-source-repository-context-contract.md`（逐节比对 §2–§33）
- `docs/scopes/scope-008-implementation-brief.md`
- `docs/scopes/scope-008-independent-review-brief.md`

**执行的命令与探针**

- `dotnet build`（0/0）、`dotnet test`（102/102）、`dotnet test --filter ~SourceContext`（22/22）
- 源码级 grep：Git 动词白名单、`File.*` 读写、shell 调用、`UseShellExecute`、环境变量移除清单
- 探针 A（真实 Git 2.53.0）：`hash-object` + `mktree -z` 注入换行/制表符路径 → 原始 `ls-tree -z` / `grep -z` 输出对照实现解析；字面 vs 正则语义；`-m` 每文件语义；裸仓库（外部 fetch）解析/列举/读取/搜索；工作树删除后仍读取已提交内容；不存在路径与空目录的失败行为；路径过滤与模式边界
- 探针 B：promisor 远程 + 记录型 `ext::` 传输的惰性抓取对照实验（未设置 vs 设置 `GIT_NO_LAZY_FETCH` vs 经实现调用）
- 探针工程与临时仓库已全部删除；`git status` 仅剩未跟踪的审查简报，`HEAD` 仍为 `9b614ac`；`.work/` 仅含被跟踪的 `README.md`

---

## 5. Scope check

**未发现越界行为。**

- 无 `clone`/`fetch`/`pull`/`push`/`remote update`：生产源码不含任何远程同步路径（简报 §6 要求上报的行为不存在）；
- 无 `checkout`/`switch`/`reset`/`clean`/`stash`/`commit`/`merge`/`rebase`/分支或标签变更/`config` 写入/`submodule update`；
- 无 Snapshot 实现、无 Snapshot 采集时序、无源文件相关性选择或 AI 排序；
- 无文本编码策略、无二进制/文本分类策略（`-I` 仅为搜索跳过二进制文件的 Git 行为，不构成分类策略）；
- 未新增 SQLite 迁移（无 `002`），未新增目录字段；
- 未引入 `ISourceControlProvider`/`IScmAdapter`/仓储工厂/多 SCM 抽象，仅四个契约允许的具体组件；
- 未引入 LibGit2Sharp 或任何 SCM 库，仅系统 Git CLI；
- 未实现远程凭据、外部同步脚本、调度或监控；
- 未削弱 Git 信任检查：无 `safe.directory=*`、无全局 Git 配置写入、无 TLS/校验旁路；
- 测试数据全部为合成内容（`synthetic@example.invalid`、`Synthetic *`、`example.invalid`），无私有仓库 URL、凭据、真实源码或私有主机路径；绝对路径仅存在于运行时配置与 `.work/` 测试夹具，不进入溯源输出；
- 未执行 `git push` 或任何远端写入。

**契约 §33 测试矩阵覆盖情况：** 36 项要求全部有对应测试。N2 与 N3 指出了其中两项**证明强度不足**（而非缺失）：本地化抑制缺乏判别性对照，需 Git 引号的路径缺乏真实注入覆盖。二者均已由本次探针独立证明当前实现正确。

---

## 6. 后续处理建议

1. 本决策交由 GPT-5.6 Sol 按契约 §24 流程归并；
2. **N2 与 N3 建议优先补齐**：二者都是「实现正确但回归保护缺失」的类型，且我已在本次审查中给出可直接复用的构造方法（promisor 远程对照、`mktree -z` 路径注入）；
3. N1 涉及搜索上限语义，建议与后续 Snapshot/执行范围一并决定是收窄 `-m` 还是保留现状；
4. N4、N5 为诊断准确性与记录项，可在维护中处理；
5. 不需要为本范围重新开启实现。

Scope 008 的源仓库上下文可作为后续工作的基线提交：

`9b614ac4b604e1dc2982ff242ee498785f306a0a`
