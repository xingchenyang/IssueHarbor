# IssueHarbor V2 / 1.0.0 — Scope 007 Independent Review Decision

**Scope:** 007 — RedmineMcp Read Path<br>
**Status:** PASS WITH NON-BLOCKING FINDINGS<br>
**Review type:** Independent HTTP / authentication / attachment security audit<br>
**Architecture / Scope Lead:** GPT-5.6 Sol<br>
**Implementation Agent:** GPT-6 Luna<br>
**Independent Reviewer:** DeepSeek-V41-Flash<br>
**Review brief:** `docs/scopes/scope-007-independent-review-brief.md`

---

## 1. 决策 (Decision)

**PASS WITH NON-BLOCKING FINDINGS**

`ee9c2672debfad1d30cf8dad4a893eac0c8b5ece`（`feat: implement RedmineMcp read path`）未发现阻断级缺陷。信任边界（密钥、来源、重定向、附件同源、字节上限）与只读结构均按已批准契约实现，且经**真实进程级探针**验证。

### 已确认通过的关键控制

**密钥/认证边界（A）** — 全仓库仅 `request.Headers.Add("X-Redmine-API-Key", options.ApiKey)` 一处凭据写入；grep 确认源码中不存在 `Authorization`、`Basic`、`Cookie`、`X-Redmine-Switch-User`、`Impersonation` 等构造。密钥仅来自服务端配置，未出现在任何工具输入 schema（`InputSchemasDoNotExposeCredentialsOrConnectionSettings` 断言禁止字段名）。

**HTTP 结构（C）** — 整个 `src/RedmineMcp` 中唯一的方法引用是 `HttpMethod.Get`；无 POST/PUT/PATCH/DELETE；共享 GET 助手为 `private`，不接受调用方控制的 method/URL/body，无原始 REST 逃逸口。

**重定向（D）** — `SocketsHttpHandler.AllowAutoRedirect = false`、`UseCookies = false`（实测断言），且 `SslOptions.RemoteCertificateValidationCallback` 为 `null`，即未加入任何 TLS 校验旁路。

**附件同源（E）** — 探针逐项验证（每次均只发出 1 次请求 = 零第二次下载）：

| `content_url` | 结果 |
|---|---|
| `https://redmine.example.invalid.evil.invalid/...`（后缀伪装） | 拒绝 `invalid_upstream_response`，1 次请求 |
| `https://redmine.example.invalid@evil.invalid/...`（userinfo 伪装） | 拒绝，1 次请求 |
| `https://redmine.example.invalid:444/...`（端口不同） | 拒绝，1 次请求 |
| `http://redmine.example.invalid/...`（降级） | 拒绝，1 次请求 |
| `//redmine.example.invalid/...`（协议相对） | 拒绝，1 次请求 |
| `/redmine/f`（相对路径） | 拒绝，1 次请求 |
| `https://REDMINE.example.invalid/...`（大小写差异） | 接受（`IdnHost` 忽略大小写，正确），2 次请求 |
| `https://redmine.example.invalid/redmine/f`（同源） | 接受，2 次请求 |

同源判定使用 `scheme` + `IdnHost` + `Port`，并对空 userinfo 语法（`https://@host/`）额外做了原文检查。

**附件字节上限（F）** — 三道独立防线：元数据 `filesize` 上限（含 `Array.MaxLength` 可表示性检查，`long.MaxValue` 元数据被拒且不发起第二次请求）→ `Content-Length` 上限与一致性检查 → 流式读取时的实数上限。读取循环把 `readCapacity` 限制为 `remainingCeiling + 1` 字节，因此**多出的 1 字节即可判定越界**：实测元数据声明 100、实际流 150 字节时，`BytesRead` 被限制在 101 以内（仓库测试断言 `<= 101`，我复核该上界正确）。字节数必须同时等于元数据 `filesize` 与 `Content-Length`。

**错误映射（H）** — `RedmineMcpFailure.FromStatus` 与契约逐项一致（401/403/404/408/429/5xx/3xx/其他 4xx）；传输异常、`IOException`、内部超时均映射 `upstream_unavailable`；`JsonException` 映射 `invalid_upstream_response`；调用方取消经 `when (cancellationToken.IsCancellationRequested)` 原样抛出。实测 4 个工具在真实传输失败下全部返回 `isError=true` + `upstream_unavailable`，无 `structuredContent`。

**日志/隐私（M）** — 唯一日志语句为 `RedmineMcpFailures.cs:60`，字段仅 `operation / status / error / duration_ms`。**真实二进制探针**（非测试替身）验证：stdout 仅 1 份 JSON-RPC 响应/请求，stderr 仅 4 行上述格式日志，stdout 与 stderr 均不含哨兵密钥 `synthetic-sentinel-secret-ZZ`、不含上游主机名，进程退出码 0。

**MCP 工具面与结果语义（N）** — 真实 stdio 服务器 `initialize` + `tools/list` 返回**恰好 4 个工具**（`redmine_issues_list`、`redmine_issue_get`、`redmine_queries_list`、`redmine_attachment_get`），无额外 stdout 输出。成功结果为 `IsError=false` 且携带 `StructuredContent`，失败为 `IsError=true` 且仅含类别字符串。

**分页安全（K）** — 探针验证：`total_count` 中途变化 → 拒绝；总数非零却返回空页 → 拒绝；正常 3 条查询分 3 页完整取回且确定性终止；`nextOffset <= offset` 与 `nextOffset > total_count` 双重防死循环。

**规范化（J/L）** — 仅请求 `include=children,attachments,relations,journals`；未知字段忽略（测试内嵌 `unknown_upstream_field`）；必需字段缺失/类型错误/时间戳非法均抛 `invalid_upstream_response`；时间戳用 `AssumeUniversal | AdjustToUniversal` 归一为 UTC `Z`，日期用 `TryParseExact("yyyy-MM-dd")` 保持 date-only，无本地时区依赖。

### 我执行的验证

| 项目 | 结果 |
|---|---|
| `dotnet restore IssueHarbor.slnx` | 成功（**未**出现简报所述 `NU1301`；缓存可用，退出码 0） |
| `dotnet build IssueHarbor.slnx --no-restore` | 成功，0 警告 / 0 错误 |
| `dotnet test IssueHarbor.slnx --no-build` | **80 / 80 通过** |
| `dotnet test --filter FullyQualifiedName~RedmineMcp` | **37 / 37 通过** |
| 真实二进制 stdio `initialize` + `tools/list` | 恰好 4 个工具，stdout 无杂项输出 |
| 真实二进制 4 工具失败路径 | 4 × `upstream_unavailable`，stderr 仅批准字段，无密钥/主机名泄漏 |
| 仓库外安全探针（同源、重定向、上限、分页、查询串原文） | 见下文发现 |

关于已知环境限制：本次审查中 `restore` 未复现网络拒绝，也没有出现包版本不一致或无法从仓库现状复现的构建行为。

---

## 2. Blocking findings

无。

以下曾被怀疑、经复核后**确认不成立**的阻断项，记录以说明审查深度：

| 疑似阻断项 | 复核结论 |
|---|---|
| 跨源附件是否会先发出带密钥的第二次请求 | 不成立。7 种非等价来源构造全部在 `ValidateAttachmentOrigin` 处失败，每次请求计数为 1 |
| 重定向是否可能把密钥带到其他来源 | 不成立。`AllowAutoRedirect=false`，且实测 302 附件响应被映射为 `invalid_upstream_response` |
| 无 `Content-Length` 时是否可绕过字节上限 | 不成立。流式循环按 `remainingCeiling + 1` 读取，越界 1 字节即拒绝 |
| `filesize` 极大值是否导致溢出/超大分配 | 不成立。`Array.MaxLength` 检查在分配前执行，实测 `long.MaxValue` 被拒且不发起第二次请求 |
| 内部超时是否会被误判为调用方取消 | 不成立。`when (cancellationToken.IsCancellationRequested)` 过滤器正确区分两者 |
| 是否存在原始 REST 逃逸口或写操作 | 不成立。全仓库唯一方法是 `HttpMethod.Get`，共享助手为 private |
| 密钥是否可能进入 stdout/stderr/工具输出 | 不成立。真实进程探针未发现密钥或主机名泄漏 |

---

## 3. Non-blocking findings

### N1. 查询串使用百分号编码，与冻结契约的字面映射存在差异（需维护者确认目标 Redmine 行为）

- **文件：** `src/RedmineMcp/RedmineMcpReadPath.cs:233-235`（`BuildQuery` 统一使用 `Uri.EscapeDataString`）
- **观察到的行为（探针实测原始请求行）：**

  ```text
  /redmine/issues.json?issue_id=12%2C19&status_id=%2A&offset=0&limit=100
  /redmine/issues/17.json?include=children%2Cattachments%2Crelations%2Cjournals
  ```

  契约第 8.1 节规定显式 ID 映射为 `issue_id=a,b,...&status_id=*`、状态选择器 `all` 映射为 `status_id=*`；实现把 `*` 编码为 `%2A`、`,` 编码为 `%2C`。服务端解码后语义相同（`%2A` → `*`、`%2C` → `,`），因此**预计功能等价**；但这意味着「全状态」选择器是否真正生效，取决于目标 Redmine 在应用过滤器前是否对查询参数做百分号解码。
- **契约/参考资料：** 契约第 8.1 节要求字面 `status_id=*` 与 `issue_id=a,b,...`。[Redmine REST Issues 文档](https://www.redmine.org/projects/redmine/wiki/REST_issues) 明确 `*` 取值，并说明「含 `> < =` 的运算符应十六进制编码，多数成熟客户端会默认这么做」——`*` 与 `,` 不在此列。
- **影响：** 若目标 Redmine 使用原始查询串而不解码，则状态选择器 `all` 与显式 ID 批量查询可能返回与预期不符的结果（属上游行为依赖，非密钥/信任边界问题）。当前仓库测试全部通过 `QueryMap(Uri)`（内含 `Uri.UnescapeDataString`）取值，**结构上无法发现该差异**。
- **最小合规修正：** 二选一并补测试：①在 `BuildQuery` 中允许 `*` 与 `,` 保持字面输出（对已批准的两个常量做白名单不转义）；②或在契约范围内明确记录「参数值统一百分号编码，服务端解码后等价」并保留现状。
- **证明修正的测试：** 断言**原始** `RequestUri.PathAndQuery`（不做 Unescape）包含 `status_id=*` / `issue_id=12,19`；若采纳方案②，则断言解码前为 `%2A`/`%2C` 且解码后等价。

### N2. 工具处理器把所有非预期异常都归类为 `invalid_upstream_response`

- **文件：** `src/RedmineMcp/RedmineMcpToolHandlers.cs:74-77` 与 `111-114`
- **观察到的行为：** `catch (Exception) { return ErrorResult(InvalidUpstreamResponse); }` 会把任何内部缺陷（例如读取循环中 `checked` 运算的 `OverflowException`、空引用等）报告为「上游响应非法」。探针实测：注入 `InvalidOperationException` 时工具返回 `isError=true` + `invalid_upstream_response`。
- **影响：** 不构成安全边界问题（无信息泄漏，类别为冻结枚举内值），但会把本地缺陷伪装成上游数据问题，降低可运维性与诊断准确性；契约第 23 节的映射表并未把「内部错误」归入 `invalid_upstream_response`。
- **最小合规修正：** 收窄兜底捕获范围（仅捕获已批准的上游/规范化失败），或明确记录该兜底语义并加测试固定该行为。
- **证明修正的测试：** 向处理器注入一个非 `RedmineMcpFailure` 的内部异常，断言其不会被标记为上游响应非法（例如改为显式重新抛出，或断言记录为内部错误）。

### N3. 部分契约行为的测试强度不足

以下均为**已实现但未被测试直接证明**的行为，不影响当前正确性：

1. **空页 + 非零总数**（`RedmineMcpReadPath.cs:105` / `RedmineMcpNormalizer.cs:56` 的 `elements.GetArrayLength() == 0 && offset < totalCount` 判定）——现有 `QueryListFollowsAllVisiblePagesAndRejectsNonProgressingOrContradictoryMetadata` 的三条无效样本都无法到达该判定（它们更早因 `total_count` 类型、offset 回显不符、`allQueries.Count` 断言而失败）。
2. **调用方在传输中途取消**——`TransportFailureAndInternalTimeoutAreNormalizedButCallerCancellationPropagates` 只使用**已取消**的令牌，因此「取消 vs 内部超时」的区分实际依赖的是前置取消，而非契约第 6/7 节的运行中区分。
3. **无 `Content-Length` 时的实际字节数不一致**——`AttachmentByteCountMustMatchMetadataAndUnexpectedRedirectIsNotFollowed` 使用 `ByteArrayContent`（带 `Content-Length`），因此 `Content-Length` 不匹配分支先命中，流式循环内的 `total != expectedSize` 分支未被触发。
4. **元数据不重置时的游标越界成本上界**——分页循环每次前进 1 条，理论上限约 `total_count` 次请求；契约未规定请求数上限，但值得记录。
- **最小合规修正：** 针对上述 1–3 各补一条最小用例。
- **证明修正的测试：** ①上游回显同 offset/limit 且 `queries: []`、`total_count > offset`；②处理器挂起后中途取消令牌，断言抛 `OperationCanceledException` 且**不**映射为 `upstream_unavailable`；③无 `Content-Length` 的自定义流返回不足 `filesize` 的字节。

### N4. 时间戳归一化会补零到 7 位小数（良性但值得记录）

- **文件：** `src/RedmineMcp/RedmineMcpNormalizer.cs:501`
- **观察到的行为：** 归一化输出固定为 `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`，因此上游 `"2024-01-02T03:04:05Z"` 会被输出为 `"2024-01-02T03:04:05.0000000Z"`。
- **影响：** 无。契约第 20 节只要求确定性 UTC `Z` 形式，`fffffff` 是合法且确定性的 RFC 3339 表示；但与 IssueHarbor 存储层 `UtcTimestamp` 使用的毫秒精度（`fff`）不同，两处对同一上游时间戳会产出不同字符串，未来做跨组件比对时需要留意。属记录项，不建议在本范围修改。

---

## 4. Evidence reviewed

**实现文件**

- `src/RedmineMcp/Program.cs` — 宿主、日志阈值（`LogToStandardErrorThreshold = Trace`）、stdio 传输、DI 注册
- `src/RedmineMcp/RedmineMcpOptions.cs` — 配置键与默认值、基址校验、`CreatePrimaryHandler`
- `src/RedmineMcp/RedmineMcpFailures.cs` — 错误类别、状态映射、诊断端口、`RedmineMcpOperation.RunAsync`
- `src/RedmineMcp/RedmineMcpReadPath.cs` — 四个读操作、GET 助手、同源校验、字节上限循环、查询构造
- `src/RedmineMcp/RedmineMcpNormalizer.cs` — 全部规范化与严格性检查
- `src/RedmineMcp/RedmineMcpContracts.cs` — 冻结的 DTO 形状与 JSON 名称
- `src/RedmineMcp/RedmineMcpManifest.cs` — 工具创建与输入校验、输出 schema
- `src/RedmineMcp/RedmineMcpToolHandlers.cs` — 工具处理器与结果语义
- `src/RedmineMcp/InternalsVisibleTo.cs`

**测试文件**

- `tests/RedmineMcp.Tests/RedmineMcpReadPathTests.cs`（20 个 `[TestMethod]`，含 1 个 8 行 `DataRow`）
- `tests/RedmineMcp.Tests/RedmineMcpManifestTests.cs`（9 个 `[TestMethod]`）

**契约与文档**

- `docs/scopes/scope-007-redmine-mcp-read-path-contract.md`（逐节比对第 2–29 节）
- `docs/scopes/scope-007-implementation-brief.md`
- `docs/scopes/scope-007-independent-review-brief.md`

**执行的命令与探针**

- `dotnet restore` / `dotnet build --no-restore` / `dotnet test --no-build`（80/80）/ `dotnet test --filter ~RedmineMcp`（37/37）
- 源码级 grep：HTTP 方法、凭据头、日志调用（确认无写方法、无 Basic/Cookie/Impersonation、唯一日志语句）
- 仓库外安全探针（引用真实 `IssueHarbor.RedmineMcp` 程序集，`InternalsVisibleTo` 对齐）：原始查询串、8 种 `content_url` 同源构造（6 拒绝 / 2 接受）、附件重定向、分页矛盾/空页/回归、声明小于实际的流、`filesize=0`、上限等于实际、上限超出、4 种基址组合的 URI 拼接
- 真实二进制进程探针：`initialize` + `tools/list`（4 工具、stdout 干净）；4 个工具的真实传输失败路径（类别、stdout/stderr 分离、哨兵密钥与主机名泄漏检查、退出码）
- 探针工程与临时目录已全部删除；`git status` 仅有未跟踪的审查简报与本文档，`HEAD` 仍为 `ee9c267`

---

## 5. Scope check

**未发现越界行为。**

- 工具面严格为批准的 4 个，未新增、未重命名，注解为只读/非破坏（实测 `tools/list`）；
- 无 POST/PUT/PATCH/DELETE、无通用 REST 直通、无调用方控制的 method/path/body 接口；
- 配置项仅 4 个批准键，工具输入不暴露连接或凭据设置；
- 未引入重试、缓存、连接池调优、凭据 UI、密钥检索 API、备份/恢复、变更操作、Analyzer/Snapshot 采集、Provider 集成（均在契约第 30 节延后）；
- 无新增错误类别（仍为冻结的 6 个）；
- 未对源文本做翻译/摘要/重写/分类/HTML 替换（规范化仅做类型校验与直通）；
- 未暴露 `content_url`、本地临时路径或凭据（测试断言 + 探针双重确认）；
- 测试全部使用合成 HTTP 处理器与合成数据（`redmine.example.invalid`、`Synthetic *`），无真实 Redmine、无网络依赖、无私有主机名或真实 Issue 内容；
- 未执行 `git push` 或任何远端写入。

---

## 6. 后续处理建议

1. 本决策交由 GPT-5.6 Sol 按契约第 24 节流程归并；
2. **N1 建议优先处理**：它是唯一可能影响真实 Redmine 行为语义的差异，且修正成本极小（查询构造 + 一条不使用 `UnescapeDataString` 的断言）；
3. N2–N4 属诊断质量与测试强度加固，可在后续维护中一并处理；
4. 不需要为本范围重新开启实现。

Scope 007 的读路径可作为后续工作的基线提交：

`ee9c2672debfad1d30cf8dad4a893eac0c8b5ece`
