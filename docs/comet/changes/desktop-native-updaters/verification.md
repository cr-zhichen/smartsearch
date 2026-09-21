---
generated_from_state_version: 18
---

# 验证

## 当前结果

- 结果: **已阻塞**
- 验证情况: **解决报告中的阻塞项后恢复验证**
- 目标周期: 3
- 迭代: 1
- 验证器尝试次数: 1
- 完成时间: 2026-09-21T13:17:37.415Z
- 摘要: Blocked overall: the immutable CI snapshot is bound to current `77a225cc3529e33a8786cdeb9fc5225eac4c6dcc` and establishes signed Windows x64/arm64 build, negative signing checks, SDK delta/fallback/install-signature checks, and runner-key cleanup. A9-A15 pass; A1-A8 remain blocked where the full accepted Scenario requires macOS, GUI, real migration, or complete release evidence that this authorized Windows-only run explicitly did not execute.

## 验收

| 编号 | 结果 | 来源 | 验收项 | 原因 |
| --- | --- | --- | --- | --- |
| A1 | blocked | specs/desktop-native-updaters/spec.md | Native frameworks own App updates WHEN 安装当前候选并检查 App 更新，THEN Windows 实际进入 Velopack、macOS 实际进入 Sparkle，SDK 使用正确平台架构和稳定源；App 与内置后端版本一致，旧 Python App 下载/安装动作不再与框架同时执行，开发散包明确报告未安装或测试模式。 | `.desktop-artifacts/check_windows_ci.py:10-21` binds successful Windows x64/arm64 CI to HEAD `77a225c`, but macOS is explicitly skipped; full Scenario requires actual Sparkle too (`specs/desktop-native-updaters/spec.md:9-10`). |
| A2 | blocked | specs/desktop-native-updaters/spec.md | Background checks and explicit consent WHEN 自动检查到期、用户关闭自动检查、手动重试或发现新版，THEN 检查频率和提示正确；未点击更新不下载或安装，稍后不会循环弹窗，关闭开关及退出 App 能停止后续自动检查，检查失败不显示已最新。 | CI lifecycle passed, but its documented boundary is no GUI (`desktop/README.md:43`); no actual 24-hour check, prompt/later, toggle, retry, or exit behavior evidence for `spec.md:18-19`. |
| A3 | blocked | specs/desktop-native-updaters/spec.md | Update and restart preserve active work WHEN 存在草稿、自有任务或不可取消写入时请求安装，THEN 门禁生效且业务工作保留；解除门禁后 SDK 安装并重启，实际 App 和引擎均为目标版本；失败/取消不被记成成功，不终止外部 CLI。 | Static race guard is present: prepare then draft recheck (`desktop/windows/MainWindow.xaml.cs:1353-1378`) and navigation disable (`:1908-1911`); CI does not run GUI, so no real draft/restart recovery evidence. |
| A4 | blocked | specs/desktop-native-updaters/spec.md | Real delta update and full fallback WHEN 为隔离的 A/B 两个版本打包并更新，THEN 两个平台各有真实差分包产出和 SDK 升级证据；结果版本/内容正确。移除或破坏差分后的完整包回退仍通过同等校验；缺失首版基线与基线下载失败分别记录，不把完整下载称为差分成功。 | Both Windows CI jobs successfully ran real delta/full-fallback/install-signature step (`windows-ci-35602415331-x64.log:538-554`, `-arm64.log:529-545`; `test_windows_updates.py:68-115`), but required macOS delta evidence is skipped (`check_windows_ci.py:21`; `spec.md:36-37`). |
| A5 | blocked | specs/desktop-native-updaters/spec.md | First migration preserves user data WHEN 从旧 Inno 安装或旧 macOS App 转入第一版框架安装，THEN 操作路径清楚、安装身份和启动入口准确，能继续使用原配置和结果；CLI/Skills/SmartSearchTools 保留，未确认时不卸载旧程序；完成一次完整安装后下一版可走框架更新。 | No real legacy Inno or macOS installation migration was executed. The isolated test explicitly uses a unique test identity and records no production-install modification (`test_windows_updates.py:46-50,119`); Scenario requires both migration paths (`spec.md:41-46`). |
| A6 | blocked | specs/desktop-native-updaters/spec.md | Signatures and trust fail closed WHEN 公钥不匹配、缺少正式密钥、下载包篡改或签名失败，THEN 正式发布或安装被阻止，不能降级到未签名正式更新。测试身份不能进入正式 feed，Secrets 不泄露，报告区分更新签名、Windows 自签名、Apple 代码签名、公证和系统信任。 | Windows signing/tamper failure coverage ran, but formal Sparkle/macOS signing identity and macOS execution remain absent (`brief.md:43-44`; `check_windows_ci.py:21`), so the cross-platform trust Scenario is incomplete. |
| A7 | blocked | specs/desktop-native-updaters/spec.md | Architecture and bundle integrity WHEN 构建和选择任一受支持架构的更新，THEN 包内前后端/helper/资源完整且架构匹配，错误架构、旧版本或不适用的更新不允许安装；真实原生构建和协议 smoke 分平台记录，不能把配置矩阵存在当作已经运行通过。 | Windows x64 and arm64 are independently bound successful jobs (`check_windows_ci.py:16-20`), while macOS architectures are skipped (`:21`); Scenario requires all supported platform/architecture native evidence (`spec.md:57-62`). |
| A8 | blocked | specs/desktop-native-updaters/spec.md | Release pipeline publishes complete updates WHEN 执行测试或受信发布流程，THEN PR 无发布 Secrets；必要检查失败时停止发布；成功候选包含所需安装器、完整包、适用差分、feed 及校验信息，feed 引用均可解析且不跨架构。只改 workflow 未运行时报告为未运行。 | This is an authorized Windows-only candidate: workflow input explicitly skips macOS and release uploads (`.github/workflows/desktop-build.yml:14-20`), and snapshot confirms macOS/release-assets skipped (`check_windows_ci.py:21`). No complete cross-platform release/feed execution. |
| A9 | passed | specs/desktop-native-updaters/spec.md | Independent CLI and Skills stay healthy WHEN 带入修复并完成 App 框架升级，THEN 原 CLI/Skills 回归继续通过，实际隔离 mise 升级无需用户手动运行版本命令触发修复；App 关闭后独立 CLI 可运行，测试不改真实全局安装或个人 Skills。 | Isolated actual mise upgrade receipt records `result=passed`, `0.1.22→0.1.23`, private Python initialization, Skills sync, `personal_installs_modified=false`, and `provider_calls=0` (`.desktop-artifacts/isolated-cli-upgrade-0dcb27fdac004bb0bcd2402028b6b3b9/receipt.json`); current bound Skill parity check passed. |
| A10 | passed | specs/desktop-native-updaters/spec.md | Delivery claims match actual evidence WHEN 交付本次候选，THEN 当前代码、打包程序、测试日志和新只读复核可对应；报告单列本机 Windows、macOS/ARM64、GUI、真实用户安装、CI 与发布状态，任何未运行项保留未运行/受阻，不宣称正式已上线；用户数据和原工作区保持不变。 | Current CI readback is bound to HEAD `77a225c` (`check_windows_ci.py:10-25`), and current documentation explicitly distinguishes Windows-only CI, skipped macOS/release, GUI limits, and no publication (`desktop/README.md:39-45`; `docs/windows-signing.md:68-70`). |
| A11 | passed | specs/desktop-windows-signing/spec.md | A1 持续身份与秘密隔离 首次准备产生可复用的 Windows 签名身份，公开证书确认没有私钥，PFX 受随机密码保护并置于仓库外的受限目录；仓库只出现公开证书和其公开元数据。GitHub Secrets 的设置只打印名称/操作状态。后续构建核对并复用该身份；证书指纹不匹配、用途错误、到期、缺少私钥或密码错误均不能用于发布。 | Public identity metadata is repository-only (`desktop/packaging/windows/smart-search.json`); documentation places encrypted PFX outside repo (`docs/windows-signing.md:36-44`), import validates pinned identity and clears secret env (`Import-WindowsSigningIdentity.ps1:7-43`), and both CI jobs passed 14 signing checks (`x64.log:394`, `arm64.log:386`). |
| A12 | passed | specs/desktop-windows-signing/spec.md | A2 Windows 发布物完整签名 为 x64 和 ARM64 生成签名发布物后，App EXE/DLL、冻结后端和自有安装器均带期望证书的 Authenticode 签名和时间戳，且内容完整性检查通过；框架更新/卸载 helper 的来源和签名按打包来源独立核对。产品名称和版本资源与构建版本一致。第三方库的签名前后摘要相同。证据必须区分本机实际构建、CI 实际构建、静态审查和未运行项目，不能以一种架构代替另一种架构。 | Bound x64/arm64 CI ran Required signing builds and installed-signature verification (`.github/workflows/desktop-build.yml:110-131`; `check_windows_ci.py:13-20`). Packaging validates signed Setup/owned files (`Package-Windows.ps1:65-112`) and both jobs completed the real upgrade/signature step (`x64.log:538-554`, `arm64.log:529-545`). |
| A13 | passed | specs/desktop-windows-signing/spec.md | A3 异常和不受信任状态不会冒充成功 可运行检查覆盖：缺失签名秘密、错误密码、错误证书、内容被篡改、缺失时间戳、签名工具失败。对应发布操作失败并阻止上传；秘密不出现在错误信息中。正确自签名文件在未导入信任的环境仍明确报告不受系统默认信任，不能将其写成公开可信。构建与验收不永久修改本机信任库。 | Both CI jobs report `Signing checks passed (14)` (`x64.log:394`, `arm64.log:386`). The executed checker covers missing secrets, wrong password/certificate, no private key, content/CMS/timestamp tampering, signing failure, self-signed trust classification, and trust-store invariance (`Test-WindowsSigning.ps1:37-111`). |
| A14 | passed | specs/desktop-windows-signing/spec.md | A4 流水线失败阻断且更新器能识别签名资产 现有 release 文件名校验接受两种 Windows signed 安装器，并继续校验现有 macOS 资产与版本。旧版迁移入口能识别新 Windows 安装器；Velopack feed 与包校验拒绝平台不符、身份不符或冲突资产，不以历史未签名资产作为正式更新回退。PR 测试不访问发布 Secrets。至少以真实 Windows 签名构建和可运行的发布/资产检查验证；只有实际成功运行 GitHub Actions 后，才报告线上 CI 已通过。 | Actual GitHub Actions success is bound to HEAD and requires both signed Windows jobs plus import/failure/build/delta/install/cleanup steps (`check_windows_ci.py:10-20`). Runtime `release-contracts` passed 24 tests, while workflow prevents a Windows-only candidate from being treated as a release (`desktop-build.yml:65-76,140-160`). |
| A15 | passed | specs/desktop-windows-signing/spec.md | A5 说明与证据保持一致 中英文下载/发布文档明确自签名、Windows 默认不信任、可能出现的首次运行提示、官方来源和证书指纹；不出现已获得 CA/SignPath 信任或 macOS 已签名等错误声明。报告列出实际验签文件、测试结果、CI 状态以及 GUI/干净机器/ARM64 实机等未运行边界。生成候选产物不自动发布新版本、覆盖旧发行附件或替换本机正式 App。 | Current bilingual documentation states self-signed/default-untrusted behavior, official source/fingerprint, SmartScreen boundary, macOS non-claim, CI/GUI limits, and no release (`docs/windows-signing.md:5-11,30-32,64-70`; `desktop/README.md:39-45`). |

## 检查

| 检查 | 命令 | 工作目录 | 状态 | 退出码 | 耗时 |
| --- | --- | --- | --- | ---: | ---: |
| Read back exact committed x64 ARM64 signed build and real upgrade CI | .desktop-artifacts/check_windows_ci.py | . | passed | 0 | 2632 ms |
| Windows-only candidate guard and complete release artifact checks | -m pytest -q tests/test_release_workflow.py tests/test_native_update_artifacts.py | . | passed | 0 | 1110 ms |
| Public and packaged Skill parity | npm/scripts/check-skill-parity.js | . | passed | 0 | 54 ms |
| Committed candidate whitespace | diff --check HEAD^ HEAD | . | passed | 0 | 62 ms |

### Builder 报告的证据

以下为 Builder 报告，不等同于 Runtime 检查凭据或独立验收结果。

- Windows signed CI x64 and ARM64: passed — https://github.com/konbakuyomu/smartsearch/actions/runs/35602415331; exact tested implementation HEAD77a225c; .desktop-artifacts/windows-ci-35602415331.json and per-architecture logs.
- Workflow and release artifact regressions: passed — 24 passed locally after Windows-only switch.
- 已知限制: macOS由另一开发者推进，本轮明确跳过；完整跨平台验收仍缺macOS证据。
- 已知限制: GUI/真实旧安装迁移未执行；Windows SDK差分夹具为当前payload与synthetic0.0.1版本。
- 已知限制: 没有正式合并、Tag/Release、正式Sparkle密钥配置或用户安装修改。候选提交授权不等于接受完整15项验收。

## 阻塞项

- **user**: Blocked overall: the immutable CI snapshot is bound to current `77a225cc3529e33a8786cdeb9fc5225eac4c6dcc` and establishes signed Windows x64/arm64 build, negative signing checks, SDK delta/fallback/install-signature checks, and runner-key cleanup. A9-A15 pass; A1-A8 remain blocked where the full accepted Scenario requires macOS, GUI, real migration, or complete release evidence that this authorized Windows-only run explicitly did not execute. (acceptance: A1, A2, A3, A4, A5, A6, A7, A8) — next: `resolve-verifier-blocker`

## 风险与跳过的工作

- macOS Sparkle, macOS arm64/x86_64, formal Sparkle keys, and release-assets are intentionally skipped in this Windows-only CI.
- GUI interaction, real legacy Inno/user migration, and clean-machine/SmartScreen acceptance were not executed.
- Working tree has Runtime state/handoff metadata changes; parent reports product-code paths have no current diff. No artifact downloads or reruns were performed.

## 之前的迭代

| 目标周期 | 迭代 | 尝试 | 结果 | 未解决项 | 摘要 | 完成时间 |
| ---: | ---: | ---: | --- | --- | --- | --- |
| 1 | 0 | 0 | recovery | — | Native Shape artifacts changed | 2026-09-21T10:46:39.126Z |
| 2 | 1 | 1 | fail | A1, A2, A3, A4, A5, A6, A7, A8, A11, A12, A13, A14 | 当前候选不通过：A3 已确认存在 Windows 安装准备 await 期间的新草稿可被重启丢失的缺陷。A9、A10、A15 有当前绑定证据；其余需要未授权或未具备的 macOS/ARM64/签名/CI/GUI/真实迁移证据，保留 blocked。 | 2026-09-21T12:25:40.665Z |
| 2 | 2 | 1 | blocked | A1, A2, A3, A4, A5, A6, A7, A8, A11, A12, A13, A14 | 当前候选为 blocked：A3 不再有上轮已确认的静态草稿丢失竞态，但 GUI 未实跑；A9、A10、A15 有当前绑定证据而通过。其余 12 项缺少规格明确要求的 macOS、ARM64、正式签名、GUI、真实迁移或远程 CI 证据，保持 blocked。 | 2026-09-21T12:43:07.689Z |
| 2 | 2 | 1 | recovery | — | 用户已确认提交、推送并运行候选CI；macOS由另一开发者推进，正式版本后续统一合并。本轮补充Windows-only候选开关后验证Windows x64/ARM64签名与升级，不发布。 | 2026-09-21T12:53:19.145Z |
| 2 | 3 | 0 | recovery | — | Native Shape artifacts changed | 2026-09-21T12:53:54.680Z |
| 3 | 1 | 1 | blocked | A1, A2, A3, A4, A5, A6, A7, A8 | Blocked overall: the immutable CI snapshot is bound to current `77a225cc3529e33a8786cdeb9fc5225eac4c6dcc` and establishes signed Windows x64/arm64 build, negative signing checks, SDK delta/fallback/install-signature checks, and runner-key cleanup. A9-A15 pass; A1-A8 remain blocked where the full accepted Scenario requires macOS, GUI, real migration, or complete release evidence that this authorized Windows-only run explicitly did not execute. | 2026-09-21T13:17:37.415Z |



## 结论

Blocked overall: the immutable CI snapshot is bound to current `77a225cc3529e33a8786cdeb9fc5225eac4c6dcc` and establishes signed Windows x64/arm64 build, negative signing checks, SDK delta/fallback/install-signature checks, and runner-key cleanup. A9-A15 pass; A1-A8 remain blocked where the full accepted Scenario requires macOS, GUI, real migration, or complete release evidence that this authorized Windows-only run explicitly did not execute.
