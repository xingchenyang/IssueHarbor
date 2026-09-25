# IssueHarbor V2 / 1.0.0 — Scope 005 Independent Review Decision

**Scope:** 005 — Storage Foundation  
**Status:** PASS WITH NON-BLOCKING FINDINGS  
**Review type:** Independent storage/migration audit  
**Architecture / Scope Lead:** GPT-5.6 Sol  
**Implementation Agent:** GPT-6 Luna  
**Independent Reviewer:** DeepSeek-V41-Flash  
**Review brief:** `docs/scopes/scope-005-independent-review-brief.md`

---

## 1. 决策 (Decision)

**PASS WITH NON-BLOCKING FINDINGS**

已实现提交 `4d65c14d3a2154e76562ab823dbff307dfad0258`（`feat: implement storage foundation`）经独立核查后未发现 **blocking** 缺陷。

审查确认通过的关键点：

- 迁移发现按版本号数值排序，版本必须从 `001` 起唯一且连续；重复版本会被连续性校验拒绝；
- 迁移文件加载时即计算规范化（LF）内容的 SHA-256，`applied` 账本的 `version / name / sha256` 逐条与当前迁移文件比对，历史迁移被编辑会被检出；
- `ApplyMigration` 将「执行迁移 SQL + 写入 `schema_migrations` + 写入 `PRAGMA user_version`」放在**同一个事务**内提交，不存在被接受为当前版本的半应用状态；
- `InitializeOrVerify()` 仅在用户表数为 0 时才应用迁移，已存在的存储库只会被校验，不会被静默迁移；
- 每个应用连接都通过 `ForeignKeys=True` 启用外键，且 `Verify` 断言 `PRAGMA foreign_keys = 1` 并执行 `PRAGMA foreign_key_check`；
- Scope 003 的 Request 状态、Request 停止原因及其空/非空关系、Request Item 状态，以及 Scope 004 的全部枚举与跨字段不变量在数据库边界均有约束；
- 发布顺序为「校验 → 体积/SHA-256 → 同目录 staging → 原子重命名 → SQLite 事务 → 目录登记 → 提交」，数据库永不提交尚未成功发布的工件引用；
- 清单只存相对路径，路径由规范化小写 UUID v7 推导，扫描不到任何主机绝对路径；
- 未引入 EF Core、Dapper、仓储抽象或存储提供程序抽象。

---

## 2. Blocking findings

无。

以下潜在阻断项经核查后**确认不成立**，记录以说明审查深度：

| 潜在阻断项 | 核查结论 |
|---|---|
| `request_items` 的复合外键 `(snapshot_id, issue_id)` 在 `snapshot_id IS NULL` 时被 SQLite 跳过，是否可绕过成员校验 | 不成立。`request_items → request_issues(request_id, issue_id)` 在任何情况下都会生效（该表无 NULL 列），成员与序号唯一性仍受约束 |
| `runs` 的复合外键 `(request_item_id, snapshot_id) → request_items` 是否允许同一 Item 出现多个 Run | 不成立。`runs.request_item_id` 本身带 `UNIQUE`，实测第二个 Run 被拒绝（外键 + 唯一约束双重生效） |
| 迁移加载失败时是否会留下半初始化数据库 | 不成立。`StorageMigrationLoader.Load` 在打开连接之前执行，`Non_contiguous_migration_files_are_rejected` 已验证数据库文件根本未被创建 |
| 已迁移数据库被静默升级 | 不成立。实测在迁移目录新增 `002_probe.sql` 后启动抛错，`user_version` 与账本行数保持为 1 |

---

## 3. Non-blocking findings

### N1. C# 侧没有显式的「相对路径不得逃逸持久化根」守卫

- **文件：** `src/Analyzer/Storage/ArtifactPublisher.cs:228`（`GetAbsolutePath`）
- **观察到的行为：** `Path.Combine(DurableDataRoot, relativePath.Replace('/', '\\'))` 对**根路径**第二参数会直接返回该绝对路径（实测 `Path.Combine("C:\data", "C:\abs\x.json")` → `C:\abs\x.json`，`/rooted/x.json` → `\rooted\x.json`）。数据库侧 CHECK 已能拒绝 `..`、前导分隔符与盘符，但 C# 端口没有任何等价的显式检查，当前安全性完全依赖调用方传参。
- **影响：** 目前不可利用（路径全部由 `StorageIds.ToCanonicalLowercase` 校验过的 UUID 与固定布局拼出，无外部输入），但未来任何新增的目录布局或调用者只要传入根路径，就会静默写到持久化根之外。评审简报 C.9 要求该类路径「cannot escape」。
- **最小合规修正：** 在 `GetAbsolutePath` 内计算完整路径后断言其仍位于 `DurableDataRoot` 之下（前缀比较 + 分隔符边界），否则抛 `InvalidOperationException`。
- **证明该修正的测试：** 一个直接针对该守卫的单元测试，断言根路径/盘符相对路径/UNC 路径被拒绝。

### N2. 冗余唯一索引缺少说明，易被误判为可删除

- **文件：** `src/Analyzer/Storage/Migrations/001_initial.sql`（`snapshots.UNIQUE (snapshot_id, issue_id)`、`request_items.UNIQUE (request_item_id, snapshot_id)`）
- **具体项：** 这两项在列集合上都以自身主键开头，看起来冗余，但它们是 `request_items → snapshots` 与 `runs → request_items` 复合外键所必需的父键索引；`runs.request_item_id UNIQUE` 则是「一个 Item 至多一个 Run」的实际执行者（实测重复 Run 报 `UNIQUE constraint failed: runs.request_item_id`）。
- **影响：** 不影响正确性。风险在于维护者按「冗余索引」删除 `snapshots` / `request_items` 的复合 UNIQUE 后，复合外键会在下一次写入时报 `foreign key mismatch`，属于后期的隐性破坏。
- **最小合规修正：** 在迁移中为这两项加注释，说明它们是复合外键目标而非普通冗余索引。

### N3. 工件测试未独立复核「落盘字节」与记录的哈希/体积一致

- **文件：** `tests/Analyzer.Tests/StorageFoundationTests.cs`（工件类测试）
- **观察到的行为：** 断言比较的是**传入的字节数组**哈希与目录行，并用 `File.Exists` 确认文件存在，但没有一处重新读取落盘文件并独立重算 SHA-256/字节数。
- **影响：** 契约第 29 节要求系统能回答「stored size/hash values match published content?」，当前证明强度依赖发布实现自身的正确性。属于测试强度缺口，非实现缺陷。
- **最小合规修正：** 新增断言：读取 `published.Artifact.RelativePath` 对应文件，重算 SHA-256 与长度并与返回值及目录行比较。

### N4. 相对路径测试未覆盖 C# 侧的路径拒绝行为

- **文件：** `tests/Analyzer.Tests/StorageFoundationTests.cs:436`（`Database_rejects_absolute_artifact_paths`）、`ArtifactPublisher.GetAbsolutePath`
- **观察到的行为：** 唯一与相对路径相关的测试是直接对数据库执行 `INSERT`（`C:\private\host\snapshot.json`），验证的是 SQL CHECK，而非发布代码。C# 侧没有任何拒绝/守卫行为被测试覆盖。
- **影响：** 明文记录的契约行为「relative-path enforcement」目前只有数据库一侧被证明。与 N1 配套修正后可一并补上。
- **最小合规修正：** 增加一个针对发布路径解析的单元测试，断言绝对/根路径/UNC 输入被拒绝。

### N5. 测试清理缺少容错，会遗留临时目录

- **文件：** `tests/Analyzer.Tests/StorageFoundationTests.cs:27`（`[TestCleanup] CleanUp`）
- **观察到的行为：** 上一次完整测试运行遗留在系统临时目录中的 `issueharbor-storage-tests-*` 目录，其中有 21 个仍残留 `analyzer.db`（这些目录在测试进程结束后可正常删除，说明是清理时的瞬时占用/竞争而非永久句柄泄漏）。
- **影响：** 不涉及产品持久化正确性，但违反「tests must use temporary disposable storage」的卫生要求，且残留 `analyzer.db` 可能干扰后续手工排查。
- **最小合规修正：** 在清理时捕获瞬时失败并重试/延迟一次，保持测试对临时目录无残留。

---

## 4. Evidence reviewed

**实现文件**

- `src/Analyzer/Storage/Migrations/001_initial.sql`（七张表全部约束，逐条比对）
- `src/Analyzer/Storage/StorageMigrationLoader.cs`
- `src/Analyzer/Storage/StorageInitializer.cs`
- `src/Analyzer/Storage/ArtifactPublisher.cs`
- `src/Analyzer/Storage/SqliteConnectionFactory.cs`
- `src/Analyzer/Storage/StorageIds.cs`
- `src/Analyzer/Storage/UtcTimestamp.cs`
- `src/Analyzer/Program.cs`（仅 `IssueHarbor:DataRoot` 已配置时执行 `InitializeOrVerify()`）
- `src/Analyzer/IssueHarbor.Analyzer.csproj`（`Microsoft.Data.Sqlite` 10.0.12、`Corvus.Text.Json.Validator` 5.2.12、迁移与 schema 内容复制）
- `src/Analyzer/Contracts/analysis-result-v1.schema.json`（与数据库枚举逐项比对）

**测试**

- `tests/Analyzer.Tests/StorageFoundationTests.cs`（25 个 `[TestMethod]`，按方法计数核对）
- `tests/Analyzer.Tests/AnalysisResultSchemaTests.cs`（17 个用例，含 6 个 `DataRow`）

**契约与文档**

- `docs/scopes/scope-005-storage-foundation-contract.md`（逐节比对第 3–25 节）
- `docs/scopes/scope-005-implementation-brief.md`
- `docs/scopes/scope-003-core-domain-status-contract.md`（状态/原因枚举比对）
- `docs/scopes/scope-004-structured-result-contract.md`（枚举与跨字段不变量比对）

**执行的验证**

| 命令 | 结果 |
|---|---|
| `dotnet restore IssueHarbor.slnx` | 成功（项目均为最新） |
| `dotnet build IssueHarbor.slnx --no-restore` | 成功，0 警告 / 0 错误 |
| `dotnet test IssueHarbor.slnx --no-build` | 42 / 42 通过（0 失败、0 跳过） |
| `dotnet test --filter FullyQualifiedName~StorageFoundationTests` | 25 / 25 通过，与「新增 25 个存储测试」一致 |
| `git status --porcelain` / `git log` | 工作区仅新增本审查简报文件；`HEAD = 4d65c14`，`main` 领先 `origin/main` 3 个提交；**未执行任何 push** |

**为核实约束语义而独立执行的探针（在仓库之外的临时工程中，对真实 `001_initial.sql` 执行，并直接调用编译后的 `IssueHarbor.Analyzer` 存储类型）**

- 由 SQLite 自身回读的 `snapshots` DDL 与 `PRAGMA foreign_key_list('snapshots')` 确认：`snapshots.issue_id` **确实**声明并对 `issues(issue_id)` 生效；
- `request_items.snapshot_id = NULL` 可插入（MATCH SIMPLE 语义，符合契约第 15 节「可空」），而任意非空且不存在的 `snapshot_id` 均被外键拒绝；
- 同一 Item 的第二个 Run 被拒绝，报 `UNIQUE constraint failed: runs.request_item_id`（1:1 由该唯一约束执行，与 `snapshot_id` 是否绑定无关）；
- `change_type = unknown + requires_code_change = no + complexity = medium` 被跨字段 CHECK 拒绝；`clarification_needed + requires_code_change = yes + complexity = low` 被接受，符合契约第 10 节（未对 `clarification_needed` 规定 `uncertain`）；
- `snapshots.artifact_relative_path` 对 `../evil.json`、`snapshots/../../../etc/passwd`、`/home/user/x.json`、`\rooted\x.json` 全部拒绝，普通嵌套相对路径正常接受；
- `PRAGMA foreign_key_check` 在全部探针写入后保持 clean；
- 直接调用 `ArtifactPublisher.PublishSnapshot(snapshotId, 9001, …)`（`issues` 中无 9001）复现既有测试：抛 `SqliteException: FOREIGN KEY constraint failed`、工件文件已落盘、`snapshots` 行数为 0——确认该测试**确实**走的是「登记失败但工件保留」路径；
- `Path.Combine` 对根路径第二参数返回绝对路径，确认 N1 所述行为。

> 审查过程记录：初版探针曾因 SQL 参数绑定错误得出「`snapshots.issue_id` 无外键」的错误中间结论，随后以 SQLite 回读 DDL 与真实实现调用两种独立方式复核并推翻该结论。上表为复核后的最终证据。

---

## 5. Scope check

**未发现越界行为。**

- 未实现备份/恢复、孤儿清理、WAL 调优、加密、崩溃恢复、重试/续跑、Web UI 命令面、Provider 或 profile 内容、Snapshot 采集、RedmineMcp 行为、源仓库映射（均在契约第 26 节明确延后）；
- 仅引入契约第 24 节明确允许的具体组件（连接工厂、迁移加载/校验、初始化器、工件发布器），未引入 `IRepository<T>`、通用工作单元、存储提供程序或 SQL 方言抽象；
- 迁移只创建契约第 10 节列出的七张表，无 `entities` / `objects` / `properties` / `artifacts` 之类的投机表；
- 未新增范围原因分类法：`request_items.reason_code` 保持无约束的可空文本，与契约第 15 节一致；
- 数据库未存储工件正文（无 `snapshot_json` / `result_json` / `report_markdown` 字段）；
- 测试数据全部为合成内容（`"Synthetic issue"`、`C:\private\host\...` 等明显虚构值），无凭据、无真实 Redmine 内容、无私有基础设施信息；
- 未执行 `git push` 或任何远端写入。

**契约要求的测试矩阵覆盖情况（第 13 节 1–16 项）：** 全部 16 项均有对应用例，且覆盖方式为数据库层真实约束与外键拒绝，而非仅调用辅助方法。已逐项复核并确认其中「forced DB registration failure after artifact publication」一项确实走通目标路径。测试强度缺口仅在 N1、N3、N4 中记录，均属加固性建议。

---

## 6. 后续处理建议

1. 本决策交由 GPT-5.6 Sol 与实现方按契约第 19 节流程归并；
2. N1–N4 属于加固项，不涉及已批准契约的语义变更，可作为存储基础的维护项处理；
3. 本审查**未发现需要修订实现才能通过的问题**，无需为 Scope 005 重新开启实现范围。

Scope 005 的存储基础可作为后续工作的基线提交：

`4d65c14d3a2154e76562ab823dbff307dfad0258`
