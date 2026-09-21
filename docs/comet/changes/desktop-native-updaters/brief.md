# Outcome

Windows 接入 Velopack、macOS 接入 Sparkle，由成熟框架完成 Smart Search App 与内置 Python 引擎的整体更新，提供后台检查、新版提示、用户点击后下载和安装重启，以及真实可用的差分更新。

# Scope

- 保留 WinUI 3、SwiftUI、Python sidecar 和现有页面结构；采用各平台 SDK，不改为跨平台 Web 壳。
- 迁移 App 自更新和打包发布链；独立 CLI 保留原 npm/mise 管理器，Skills 保留按内容比较和显式同步。
- 带入上一候选 `desktop-update-state-fix` 已验证的 CLI 初始化、Windows stdin、缓存状态与 Skills 修复；原候选及验收记录保留。
- 保留默认每天检查一次和关闭开关。发现新版提示，用户点击后下载、安装并重启；应用退出后不运行常驻服务。
- Windows 用 Velopack 按用户安装；处理从现有 Inno Setup 安装转入新布局的首次完整安装。
- macOS 引入 Sparkle，按现有 arm64/x86_64 两种原生包分别发布可信 feed，保留当前最低系统要求。
- GitHub Actions 生成安装包、完整更新包、差分包、更新清单和校验信息。上一版不可用时明确记录完整包发布；网络/签名等错误不冒充正常首次发布。
- 测试使用独立工作区、独立安装身份/目录和测试更新源；修改、验证流水线源码不等于线上已运行或正式已发布。

## Source coverage

| 来源 | 读取状态 | 内容及用途 | 对应规格 | 验收 | 状态 |
| --- | --- | --- | --- | --- | --- |
| 用户要求正式向 Velopack / Sparkle 接入推进 | complete | 本次实现目标 | desktop-native-updaters 全文 | 全部 Scenario | covered |
| 本轮前文：静默检查、更新提醒、增量更新和 CI/CD | complete | 用户可见功能 | 检查与提示、差分、构建发布 | 对应 Scenario | covered |
| `D:/Dev/30_第三方项目/30_AI与MCP工具/codex-tweaks` | complete for relevant reference scope | 实现参考：WinUI/SwiftUI SDK 接入、打包脚本、发布 workflow；不导入其产品需求、标识、密钥或完整仓库规则 | 平台集成、差分、身份 | 对应 Scenario | background |
| Smart Search 当前 desktop/README.md、PROTOCOL.md 与 Windows 签名 Spec | complete | 当前架构及需要替换的旧 App 更新合同 | desktop-native-updaters、desktop-windows-signing | 对应 Scenario | covered |
| `desktop-update-state-fix` v21 的当前候选和 15 项验收报告 | complete | 待带入的已有修复依赖；不因此归档、提交或发布原任务 | CLI 和 Skills 边界 | Independent CLI and Skills stay healthy | covered |

# Non-goals

- 不更换前端或搜索后端，不重做界面，不引入新的通用更新抽象或后台服务。
- 不为 CLI/Skills 自制二进制差分更新，不改变 Provider、密钥或搜索行为。
- 不创建正式 Release/Tag、合入正式分支、替换历史附件或改用户正式安装；这些发布动作另行授权。用户现已授权本候选分支的提交、推送及 Windows 候选 CI。
- 不购买 Apple Developer 资格或 Windows 商业证书，不自动改变本机证书信任库。
- 不自动删除旧工作区、用户数据、真实 CLI 或个人 Skills。

# Acceptance examples

以两份完整目标 Spec 的 Scenario 为准。必须覆盖真实框架安装与升级、差分和完整回退、后台检查和显式安装、安全退出、首次迁移、签名失败阻断、四架构构建配置、CLI/Skills 修复回归，以及明确的实际运行证据边界。

# Constraints and invariants

- 独立候选目录 `smartsearch-native-updaters`，分支 `codex/desktop-native-updaters`，基线 main `b5773df01b111bd107dbdeb5b73ff35ca0e72e17`；原目录与其他候选的未提交工作保留。
- 正式包仅使用 Smart Search 自己的应用标识、官方源和签名身份；不复用 Codex Tweaks 的 feed、key 或应用标识。
- Windows 继续复用现有自签名身份和内容/身份/时间戳验证，不能退回未签名正式发布。
- macOS 的 Sparkle EdDSA 更新签名与 Apple Developer ID/公证是不同能力。当前只查到 Windows 仓库 Secrets，没有 macOS/Sparkle 配置；缺少正式配置必须明确阻止对应正式发布，不能称为已签名、公证或生产验证。
- 测试密钥只能用于隔离测试产物，不进入正式 feed。秘密值不进入代码、日志、聊天或 artifact。
- 自有任务、未保存配置、CLI/环境/Skills 写入未处理前，不允许更新器强制退出 App；不终止外部 CLI。

# Decisions

- 2026-09-21 用户确认提交、推送并运行候选 CI，同时明确 macOS 由另一开发者推进，后续统一合并正式版本。本轮只执行 Windows x64/ARM64 签名及升级验证；保留现有 macOS 候选供整合参考，不继续修改其实现，也不把未运行项计为通过。正式合并、发布及正式 Sparkle 密钥配置仍待后续授权。

- 用户已明确确认上述完整范围及 15 项验收，授权进入实现；本轮新增提交推送授权见上条，正式发布、真实用户安装和新正式密钥配置仍需后续授权。

- 用户已明确要求正式接入 Velopack / Sparkle，并指定 Codex Tweaks 作为实现参考。
- App 与内置引擎整体更新；外部 CLI/Skills 保持独立。这与前文已讨论的接入方向一致。
- 沿用现有 24 小时检查频率、稳定渠道和用户点击安装的行为；不增加预发布渠道选择或自动安装设置。
- 本次保持单个 Native change。两个平台共用发布合同、旧路径迁移和 CLI/Skills 基线，集中协调和验收；只读调查可并行，不为两个平台额外引入 Supervisor 集成分支。
- 参考项目本地 Windows 脚本只收集完整包，macOS 明确 `--maximum-deltas 0`。本次使用框架自带工具补齐真实差分产出和回退测试，不照搬关闭差分的配置。
- 先完成并验证可审查候选、打包脚本和发布流水线。正式密钥配置、上传和正式环境迁移保持独立的发布步骤。
- 首次 Windows 迁移拟采用一次完整安装及清晰的旧版处理引导；后续版本交给 Velopack。具体引导与旧安装识别纳入验收，不静默删除用户原安装。

# Open questions

用户已明确回复“确认，按此范围实现”，确认已展示的完整行为、15 项验收和非目标；没有待决的实现范围问题。macOS 正式签名配置缺失是发布前置条件，开发可用隔离测试身份继续，不能把发布前置条件当作已满足。

# Verification expectations

复用已有测试体系及当前 Windows 构建工具；开发时仅执行适用检查。先验证当前已修复基线，再验证平台 SDK 接入、真实 A→B 差分升级和完整包回退。对网络失败、签名/内容篡改、运行中写入及首次迁移做有意义的回归。macOS 与 ARM64 在对应 runner 或实机上获取独立证据；未运行项必须保留未运行/受阻，不由 Windows x64 或源码审查代替。最终由新的只读 Verifier 核对全部 Scenario。
