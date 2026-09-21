---
generated_from_state_version: 27
---

# 验证

## 当前结果

- 结果: **已阻塞**
- 验证情况: **解决报告中的阻塞项后恢复验证**
- 目标周期: 4
- 迭代: 2
- 验证器尝试次数: 1
- 完成时间: 2026-09-21T18:32:13.155Z
- 摘要: 无确定新增产品缺陷：当前735b48a保留PR51基线并以实际四架构CI、Windows签名负例/安装升级、Sparkle真实delta-fallback-wrong-key、冻结CLI/Skills回执证明核心实现。A2、A3、A5因当前整合GUI和真实旧安装迁移未实测而blocked，故总体blocked。

## 验收

| 编号 | 结果 | 来源 | 验收项 | 原因 |
| --- | --- | --- | --- | --- |
| A1 | passed | specs/desktop-native-updaters/spec.md | Native frameworks own App updates WHEN 安装当前候选并检查 App 更新，THEN Windows 实际进入 Velopack、macOS 实际进入 Sparkle，SDK 使用正确平台架构和稳定源；App 与内置后端版本一致，旧 Python App 下载/安装动作不再与框架同时执行，开发散包明确报告未安装或测试模式。 | 当前HEAD仍为735b48a23f06f62fc1375e60359340a6b991b277。Windows在早于单实例/后端启动处进入Velopack（desktop/windows/Program.cs:15-17），按进程架构选择稳定频道（desktop/windows/AppUpdater.cs:27-42）；macOS实际使用Sparkle且禁止自动下载（desktop/macos/Sources/SmartSearchDesktop/AppUpdater.swift:27-45）。远端CI35636177494的四个原生架构job均success且绑定该SHA（.desktop-artifacts/integrated-ci-35636177494.json:1）；Sparkle两架构实际SDK回执均升级至0.1.23（.desktop-artifacts/ci-35636177494-macos-arm64/sparkle-check-6bf8cf0bb85949de8926c3e0b8dc3815/receipt.json:1；ci-35636177494-macos-x86_64/sparkle-check-f969860ada2f4e3a87f746b331e5dbb1/receipt.json:1）。 |
| A2 | blocked | specs/desktop-native-updaters/spec.md | Background checks and explicit consent WHEN 自动检查到期、用户关闭自动检查、手动重试或发现新版，THEN 检查频率和提示正确；未点击更新不下载或安装，稍后不会循环弹窗，关闭开关及退出 App 能停止后续自动检查，检查失败不显示已最新。 | 静态实现具备24小时节流、关闭自动检查及“现在更新/稍后”分支（desktop/windows/MainWindow.xaml.cs:713-727；desktop/macos/Sources/SmartSearchDesktop/AppUpdater.swift:35-45,75-91），但当前两份实际Sparkle回执均明确gui_tested=false（对应receipt.json:1）。没有当前整合候选的真实GUI自动检查、关闭开关、稍后提示不循环和失败文案端到端证据。 |
| A3 | blocked | specs/desktop-native-updaters/spec.md | Update and restart preserve active work WHEN 存在草稿、自有任务或不可取消写入时请求安装，THEN 门禁生效且业务工作保留；解除门禁后 SDK 安装并重启，实际 App 和引擎均为目标版本；失败/取消不被记成成功，不终止外部 CLI。 | Windows在下载前后检查门禁、字段变更即时捕获并在准备后再次防护（desktop/windows/MainWindow.xaml.cs:786-831；desktop/windows/MainWindow.Providers.cs:305-325）；后端拒绝忙环境/任务/CLI并在准备后锁住其他写入（src/smart_search/desktop_backend.py:476-478,629-641）；macOS也有同等草稿/任务门禁和受控关闭（desktop/macos/Sources/SmartSearchDesktop/AppModel.swift:98-100,788-852）。未发现确定缺陷，但未实跑当前GUI中的草稿、自有任务、不可取消写入、取消/失败后的重启路径；SDK回执也标记gui_tested=false。 |
| A4 | passed | specs/desktop-native-updaters/spec.md | Real delta update and full fallback WHEN 为隔离的 A/B 两个版本打包并更新，THEN 两个平台各有真实差分包产出和 SDK 升级证据；结果版本/内容正确。移除或破坏差分后的完整包回退仍通过同等校验；缺失首版基线与基线下载失败分别记录，不把完整下载称为差分成功。 | Windows真实隔离Velopack检查要求先取delta、破坏delta后取full并校验落盘文件/后端版本（desktop/scripts/test_windows_updates.py:100-119）；Sparkle官方CLI对delta、full-fallback和错误公钥分别实跑（desktop/scripts/test_sparkle_updates.py:99-142）。macOS arm64/x86_64回执均记录delta仅请求.delta、fallback随后请求.zip且版本为0.1.23（两份sparkle receipt.json:1）；四个平台CI job均success。 |
| A5 | blocked | specs/desktop-native-updaters/spec.md | First migration preserves user data WHEN 从旧 Inno 安装或旧 macOS App 转入第一版框架安装，THEN 操作路径清楚、安装身份和启动入口准确，能继续使用原配置和结果；CLI/Skills/SmartSearchTools 保留，未确认时不卸载旧程序；完成一次完整安装后下一版可走框架更新。 | 当前实现提供旧Inno检测和非破坏性迁移说明（desktop/windows/MainWindow.xaml.cs:730-735；docs/guide/en/app.md:81），并明确共享数据保留；但没有从真实旧Inno安装或旧macOS App迁移当前候选、读回原配置/结果/快捷方式的证据。现有升级检查使用合成0.0.1框架基线，不能等同旧安装迁移。 |
| A6 | passed | specs/desktop-native-updaters/spec.md | Signatures and trust fail closed WHEN 公钥不匹配、缺少正式密钥、下载包篡改或签名失败，THEN 正式发布或安装被阻止，不能降级到未签名正式更新。测试身份不能进入正式 feed，Secrets 不泄露，报告区分更新签名、Windows 自签名、Apple 代码签名、公证和系统信任。 | Windows签名核验固定公开身份、PE内容、CMS、RFC3161时间戳和不受信任自签名根状态（desktop/scripts/Windows-Signing.ps1:79-142）；两Windows CI日志记录14项负例通过（.desktop-artifacts/integrated-windows-x64-35636177494.log:371-394）。Sparkle实际错误公钥保持0.0.1并非零退出，且断言固定Sparkle2.9.6的“improperly signed”（desktop/scripts/test_sparkle_updates.py:122-141；两份Sparkle receipt.json:1）。正式密钥缺失时构建脚本拒绝发布更新（desktop/scripts/build-macos.sh:60-69），测试身份仅用于隔离回执。 |
| A7 | passed | specs/desktop-native-updaters/spec.md | Architecture and bundle integrity WHEN 构建和选择任一受支持架构的更新，THEN 包内前后端/helper/资源完整且架构匹配，错误架构、旧版本或不适用的更新不允许安装；真实原生构建和协议 smoke 分平台记录，不能把配置矩阵存在当作已经运行通过。 | Windows按ProcessArchitecture分频道（desktop/windows/AppUpdater.cs:27-30），macOS构建拒绝host/Python与请求架构不匹配（desktop/scripts/build-macos.sh:99-103）。远端35636177494的Windows x64/arm64及macOS arm64/x86_64原生job均success；macOS日志确认安装布局、架构、SDK和已安装后端启动（.desktop-artifacts/integrated-macos-arm64-35636177494.log:818-821；integrated-macos-x86_64-35636177494.log:825-828）。 |
| A8 | passed | specs/desktop-native-updaters/spec.md | Release pipeline publishes complete updates WHEN 执行测试或受信发布流程，THEN PR 无发布 Secrets；必要检查失败时停止发布；成功候选包含所需安装器、完整包、适用差分、feed 及校验信息，feed 引用均可解析且不跨架构。只改 workflow 未运行时报告为未运行。 | PR触发路径只有contents:read（.github/workflows/desktop-build.yml:3-33），Windows私钥仅workflow_dispatch且显式签名/发布输入时读取（.github/workflows/desktop-build.yml:98-111），发布资产job仅非空release_tag才执行并先验证所有引用（.github/workflows/desktop-build.yml:279-323）。当前测试CI在四架构构建、签名/更新验证后成功，release-assets按预期skipped；未将其写成正式发布。 |
| A9 | passed | specs/desktop-native-updaters/spec.md | Independent CLI and Skills stay healthy WHEN 带入修复并完成 App 框架升级，THEN 原 CLI/Skills 回归继续通过，实际隔离 mise 升级无需用户手动运行版本命令触发修复；App 关闭后独立 CLI 可运行，测试不改真实全局安装或个人 Skills。 | Runtime回执核对当前六个后端源文件及MainWindow与真实冻结build逐字节一致（.desktop-artifacts/check_integrated_cli_receipt.py:5-14；.comet/runtime/native/changes/desktop-native-updaters/logs/checks/07e89c26-77c8-4cc4-9aa7-a270e4dad5a5-integrated-frozen-cli.log:1）。隔离实际mise从0.1.22升至0.1.23、自动初始化Python、同步旧CLI Skills，且personal_installs_modified=false、provider_calls=0（.desktop-artifacts/isolated-cli-upgrade-0a37539a1af14e9095f5497fd483daed/receipt.json:1）。 |
| A10 | passed | specs/desktop-native-updaters/spec.md | Delivery claims match actual evidence WHEN 交付本次候选，THEN 当前代码、打包程序、测试日志和新只读复核可对应；报告单列本机 Windows、macOS/ARM64、GUI、真实用户安装、CI 与发布状态，任何未运行项保留未运行/受阻，不宣称正式已上线；用户数据和原工作区保持不变。 | Runtime的当前绑定检查明确记录两次CI均为735b48a、stable_release=not-published、gui_acceptance=pending（.comet/runtime/native/changes/desktop-native-updaters/logs/checks/07e89c26-77c8-4cc4-9aa7-a270e4dad5a5-integrated-platform-ci.log:1）。交付文档也明确隔离安装/验签不能代替GUI、干净机器和SmartScreen验收（desktop/README.md:58-66）；未发现把测试或ad-hoc产物冒充正式上线的当前声明。 |
| A11 | passed | specs/desktop-windows-signing/spec.md | A1 持续身份与秘密隔离 首次准备产生可复用的 Windows 签名身份，公开证书确认没有私钥，PFX 受随机密码保护并置于仓库外的受限目录；仓库只出现公开证书和其公开元数据。GitHub Secrets 的设置只打印名称/操作状态。后续构建核对并复用该身份；证书指纹不匹配、用途错误、到期、缺少私钥或密码错误均不能用于发布。 | 仓库公钥加载后显式拒绝带私钥的证书，并核对指纹、有效期和代码签名EKU（desktop/scripts/Windows-Signing.ps1:4-28）；已追踪Windows packaging文件仅含smart-search.cer和公开元数据。两架构CI实际导入固定身份并通过缺密钥、错误密码、无私钥、错误证书、用途/到期负例（.desktop-artifacts/integrated-windows-x64-35636177494.log:371-394）。 |
| A12 | passed | specs/desktop-windows-signing/spec.md | A2 Windows 发布物完整签名 为 x64 和 ARM64 生成签名发布物后，App EXE/DLL、冻结后端和自有安装器均带期望证书的 Authenticode 签名和时间戳，且内容完整性检查通过；框架更新/卸载 helper 的来源和签名按打包来源独立核对。产品名称和版本资源与构建版本一致。第三方库的签名前后摘要相同。证据必须区分本机实际构建、CI 实际构建、静态审查和未运行项目，不能以一种架构代替另一种架构。 | 构建先签署App EXE/DLL和冻结后端，再打Velopack包；随后复验安装器和包内Update/helper（desktop/scripts/Build-Windows.ps1:163-210；desktop/scripts/Package-Windows.ps1:67-107；desktop/scripts/Test-WindowsUpdateInstall.ps1:8-12）。CI35636177494的Windows x64/arm64均完成“Build self-contained App and Velopack packages”及“Verify real delta update, full fallback and installed signatures”成功步骤。 |
| A13 | passed | specs/desktop-windows-signing/spec.md | A3 异常和不受信任状态不会冒充成功 可运行检查覆盖：缺失签名秘密、错误密码、错误证书、内容被篡改、缺失时间戳、签名工具失败。对应发布操作失败并阻止上传；秘密不出现在错误信息中。正确自签名文件在未导入信任的环境仍明确报告不受系统默认信任，不能将其写成公开可信。构建与验收不永久修改本机信任库。 | 签名负例覆盖缺Secrets、错误密码、无私钥、错误证书、用途/到期、PE/CMS/时间戳篡改和签名工具失败（desktop/scripts/Test-WindowsSigning.ps1:35-80；.desktop-artifacts/integrated-windows-x64-35636177494.log:371-394）。验证器把未导入根时的状态明确标为untrusted-self-signed-root而非公开可信（desktop/scripts/Windows-Signing.ps1:94-131），且CI随后删除runner身份（.github/workflows/desktop-build.yml:134-141）。 |
| A14 | passed | specs/desktop-windows-signing/spec.md | A4 流水线失败阻断且更新器能识别签名资产 现有 release 文件名校验接受两种 Windows signed 安装器，并继续校验现有 macOS 资产与版本。旧版迁移入口能识别新 Windows 安装器；Velopack feed 与包校验拒绝平台不符、身份不符或冲突资产，不以历史未签名资产作为正式更新回退。PR 测试不访问发布 Secrets。至少以真实 Windows 签名构建和可运行的发布/资产检查验证；只有实际成功运行 GitHub Actions 后，才报告线上 CI 已通过。 | 工作流对release_tag执行版本/提交一致性检查、真实Windows签名和升级验证，并只在所有平台完成后进入发布资产步骤（.github/workflows/desktop-build.yml:67-164,279-323）。资产验证测试对未签名、损坏、错架构、路径穿越、重复、缺full/签名和外部URL均失败关闭（tests/test_native_update_artifacts.py:80-118）；当前线上CI成功事实已由远端gh读取并与本地结构化回执一致。 |
| A15 | passed | specs/desktop-windows-signing/spec.md | A5 说明与证据保持一致 中英文下载/发布文档明确自签名、Windows 默认不信任、可能出现的首次运行提示、官方来源和证书指纹；不出现已获得 CA/SignPath 信任或 macOS 已签名等错误声明。报告列出实际验签文件、测试结果、CI 状态以及 GUI/干净机器/ARM64 实机等未运行边界。生成候选产物不自动发布新版本、覆盖旧发行附件或替换本机正式 App。 | 中英文文档明确Windows自签名默认不受信、macOS仅ad-hoc且未公证、正式Sparkle密钥未配置、GUI/干净机仍需人工验收（desktop/README.md:50-66；docs/guide/en/app.md:7-13,77-83；docs/windows-signing.md:5-11,64-70）。当前Runtime回执和CI日志也保留stable-release未发布与GUI pending边界，未见错误CA/SignPath/Developer-ID声明。 |

## 检查

| 检查 | 命令 | 工作目录 | 状态 | 退出码 | 耗时 |
| --- | --- | --- | --- | ---: | ---: |
| Exact candidate three-job CI and four-platform signed SDK upgrade checks | .desktop-artifacts/check_integrated_ci.py | . | passed | 0 | 146605 ms |
| Verify real isolated CLI upgrade and Skills receipt against current frozen source | .desktop-artifacts/check_integrated_cli_receipt.py | . | passed | 0 | 77 ms |
| Verify 34 engineer-tested interface branding and platform validation files are unchanged | .desktop-artifacts/check_pr51_preservation.py | . | passed | 0 | 2043 ms |
| Public and packaged Skills stay identical | npm/scripts/check-skill-parity.js | . | passed | 0 | 69 ms |
| Check full integrated committed diff whitespace | diff --check 65e7365 HEAD | . | passed | 0 | 76 ms |

### Builder 报告的证据

以下为 Builder 报告，不等同于 Runtime 检查凭据或独立验收结果。

- local-regression: passed — 整合后998通过2跳过；Mac包装相关26项通过。本机Windows完整构建、SDK差分/取消/完整回退通过。
- real-frozen-cli-and-skills: passed — 当前整合冻结后端实际隔离mise0.1.22→0.1.23升级、自动Python初始化、Skills备份和内容状态通过；receipt isolated-cli-upgrade-0a37539a1af14e9095f5497fd483daed，与当前六个后端文件/主窗口源码一致。
- preserved-engineer-baseline: passed — 34个关键文件与92224fb相同；先前整合生命周期只读预审无确定新增缺陷。
- current-ci: not-run — 当前735b48a的CI35636183383全部成功；Desktop35636177494两Windows及MacARM64成功，MacIntel进入最后SDK测试。正式Runtime检查将读取最终结果和SHA。
- 已知限制: PR51原Mac实机与用户Windows测试保留；新增整合GUI、真实旧Inno/旧Mac迁移及最终Mac实机仍待用户/工程师测试。
- 已知限制: Sparkle正式EdDSA身份未配置；CI仅隔离临时身份验证，MacApp仍adhoc，未公证。稳定发布和真实用户安装未执行。

## 阻塞项

- **user**: 无确定新增产品缺陷：当前735b48a保留PR51基线并以实际四架构CI、Windows签名负例/安装升级、Sparkle真实delta-fallback-wrong-key、冻结CLI/Skills回执证明核心实现。A2、A3、A5因当前整合GUI和真实旧安装迁移未实测而blocked，故总体blocked。 (acceptance: A2, A3, A5) — next: `resolve-verifier-blocker`

## 风险与跳过的工作

- A2、A3缺少当前整合候选的真实GUI端到端验收；这不是已发现产品缺陷，仍不能以CI/源码替代。
- A5缺少真实旧Inno与旧macOS App迁移、原配置/结果/入口保留的实测证据。
- 正式Sparkle EdDSA身份尚未配置；macOS当前仅ad-hoc、未Developer ID签名或公证。正式稳定发布、真实用户安装和干净机器信任验收均未执行，不能作为已上线或公开可信声明。
- 当前工作树除候选提交外有Runtime管理的comet-state.yaml修改和未跟踪handoff文件；最终核验时HEAD仍为735b48a，二者未改变候选代码提交。

## 之前的迭代

| 目标周期 | 迭代 | 尝试 | 结果 | 未解决项 | 摘要 | 完成时间 |
| ---: | ---: | ---: | --- | --- | --- | --- |
| 1 | 0 | 0 | recovery | — | Native Shape artifacts changed | 2026-09-21T10:46:39.126Z |
| 2 | 1 | 1 | fail | A1, A2, A3, A4, A5, A6, A7, A8, A11, A12, A13, A14 | 当前候选不通过：A3 已确认存在 Windows 安装准备 await 期间的新草稿可被重启丢失的缺陷。A9、A10、A15 有当前绑定证据；其余需要未授权或未具备的 macOS/ARM64/签名/CI/GUI/真实迁移证据，保留 blocked。 | 2026-09-21T12:25:40.665Z |
| 2 | 2 | 1 | blocked | A1, A2, A3, A4, A5, A6, A7, A8, A11, A12, A13, A14 | 当前候选为 blocked：A3 不再有上轮已确认的静态草稿丢失竞态，但 GUI 未实跑；A9、A10、A15 有当前绑定证据而通过。其余 12 项缺少规格明确要求的 macOS、ARM64、正式签名、GUI、真实迁移或远程 CI 证据，保持 blocked。 | 2026-09-21T12:43:07.689Z |
| 2 | 2 | 1 | recovery | — | 用户已确认提交、推送并运行候选CI；macOS由另一开发者推进，正式版本后续统一合并。本轮补充Windows-only候选开关后验证Windows x64/ARM64签名与升级，不发布。 | 2026-09-21T12:53:19.145Z |
| 2 | 3 | 0 | recovery | — | Native Shape artifacts changed | 2026-09-21T12:53:54.680Z |
| 3 | 1 | 1 | blocked | A1, A2, A3, A4, A5, A6, A7, A8 | Blocked overall: the immutable CI snapshot is bound to current `77a225cc3529e33a8786cdeb9fc5225eac4c6dcc` and establishes signed Windows x64/arm64 build, negative signing checks, SDK delta/fallback/install-signature checks, and runner-key cleanup. A9-A15 pass; A1-A8 remain blocked where the full accepted Scenario requires macOS, GUI, real migration, or complete release evidence that this authorized Windows-only run explicitly did not execute. | 2026-09-21T13:17:37.415Z |
| 3 | 1 | 1 | recovery | — | 用户已体验 PR51 Windows 软件并要求完成剩余全部合并。基于已测 main65e7365 保留工程师两端界面、Mac签名和打包修复，整合已有原生更新及CLI/Skills修复；执行适用本机与四架构CI后完成合并。正式版本发布仍待最终包测试确认。 | 2026-09-21T17:05:17.479Z |
| 3 | 2 | 0 | recovery | — | Native Shape artifacts changed | 2026-09-21T17:58:59.493Z |
| 4 | 1 | 0 | recovery | — | Mac实际SDK已正确拒绝错误公钥，保持旧版本。验证脚本却要求错误文本包含signatur，实际固定Sparkle2.9.6输出为improperly signed，需要修正这一错误文本断言后重验。产品签名验证不变。 | 2026-09-21T18:05:11.695Z |
| 4 | 2 | 1 | blocked | A2, A3, A5 | 无确定新增产品缺陷：当前735b48a保留PR51基线并以实际四架构CI、Windows签名负例/安装升级、Sparkle真实delta-fallback-wrong-key、冻结CLI/Skills回执证明核心实现。A2、A3、A5因当前整合GUI和真实旧安装迁移未实测而blocked，故总体blocked。 | 2026-09-21T18:32:13.155Z |



## 结论

无确定新增产品缺陷：当前735b48a保留PR51基线并以实际四架构CI、Windows签名负例/安装升级、Sparkle真实delta-fallback-wrong-key、冻结CLI/Skills回执证明核心实现。A2、A3、A5因当前整合GUI和真实旧安装迁移未实测而blocked，故总体blocked。
