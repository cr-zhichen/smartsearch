# Smart Search 桌面测试包

这些脚本构建的是未签名的测试产物，不创建 GitHub Release、不修改 PATH，也不会读取或删除共享配置、用户结果或外部 npm CLI。每次执行都会在 `.desktop-artifacts/` 新建独立目录；失败现场保留供排查。

Python 后端固定为 PyInstaller `onedir`：`smart-search.exe`（Windows）或 `smart-search`（macOS），并验证 `smart_search/assets` 全量存在、`smart-search` 包元数据存在。`--smoke` 仅发送本机 `initialize` 与 `shutdown` 协议消息，使用本次运行目录中的空配置目录，不发真实服务商请求。

## Windows

在仓库根目录执行：

```powershell
.\desktop\scripts\Build-Windows.ps1 -PythonPath .\.venv\Scripts\python.exe -Architecture x64
```

脚本先构建并验证后端，再把 `desktop/windows` 的源文件阶段化到本次 artifact 目录中执行 `dotnet publish --self-contained true`，最后把完整 onedir 后端复制到 `publish\backend\smart-search.exe`。阶段化会跳过工作树已有的 `bin`、`obj` 和 `.desktop-artifacts`，因此每次构建不依赖或清空旧中间文件。Windows x64 与 ARM64 必须在对应架构的 Windows 上分别构建和运行；脚本会拒绝 Python 架构与目标不一致的 PyInstaller 交叉构建。本机 x64 的成功不能代表 ARM64 已验证。

脚本会查找本机已存在的 Inno Setup 6 `ISCC.exe`，但绝不安装它。找到后会额外生成仅当前用户的未签名安装包，安装目录为 `%LOCALAPPDATA%\Programs\Smart Search`；卸载只处理该应用目录，不移除 `%LOCALAPPDATA%\smart-search` 的共享配置或用户结果，也不改 PATH。未找到时 `result.json` 会标为 `not-built`，可先使用 `publish` 测试包；需要强制生成安装包时加 `-InstallerMode Required`，或用 `-InnoSetupPath` 指定已安装的编译器。

若本机已有 `innounp`，可显式加 `-BootstrapInnoSetup`。它只把固定版本的官方 Inno Setup 6.7.3 下载到本次 `.desktop-artifacts` 构建目录，核对固定 SHA-256、Pyrsys B.V. 的 Authenticode 签名与安装归档完整性，再本地解压 `ISCC.exe`；不会运行安装器、写注册表或改变 PATH。缺少 `innounp`、下载/签名/哈希/解压任一失败都会停止并保留该次目录。

更新前需要用户先处理 Smart Search 自有任务并退出 App。安装器与 App 共享 `Local\SmartSearch.Desktop` mutex，且显式禁用自动关闭、自动重启；检测到正在运行的 App 时不能原地覆盖其后端或资源。

## macOS

在目标架构的 macOS 13+ 机器上执行：

```bash
bash desktop/scripts/build-macos.sh --architecture arm64 --python python3
```

脚本要求 Python、宿主机和目标架构一致，避免把 PyInstaller 的原生二进制误当成交叉编译产物。它用独立 SwiftPM scratch 目录构建 `desktop/macos` 的 `SmartSearchDesktop`，将后端放入 `Smart Search.app/Contents/Resources/backend/smart-search`，并生成同目录的未签名、未公证 DMG。Intel 构建使用 `--architecture x86_64`。

## CI 与发布边界

`.github/workflows/desktop-build.yml` 在 pull request 或普通手动触发时构建 Windows x64/ARM64 与 macOS x86_64/arm64，并只上传短期测试产物。手动提供已有稳定 `release_tag` 时会检出该 tag，在临时 Windows runner 安装 Inno Setup，生成两种 Windows 安装器和两种 DMG；全部构建成功并核对 tag/版本/四个文件名后，才向已有 Release 上传包和 `SHA256SUMS.txt`。不创建 Release/Tag、不推送提交、不覆盖已上传附件、不签名或公证。CI 的结构与协议 smoke 通过只是对应架构的构建证据；干净机器安装、启动、卸载、键盘/缩放/主题、签名和 macOS 公证仍须分别验收。

普通 Windows PR CI 仍只上传 self-contained `publish` 测试包。发行模式与本地安装器均清楚标为未签名测试包。

## App 和 CLI 更新

设置中的“版本与更新”分别展示 App/内置引擎与实际生效的独立 CLI。生产 App 默认启动后检查，之后每 24 小时最多检查一次，可关闭；没有后台服务。检查不会下载安装，只在点击后下载匹配平台架构的官方稳定包；新 npm 版本没有对应桌面附件时不会误报 App 可安装。

下载显示实际字节进度，可取消重试，写临时文件并验证大小和 SHA256 后才可打开。Windows 会先要求处理草稿和 App 自有任务，再退出并打开当前用户安装器；macOS 打开 DMG，由用户正常安装。下载完成和安装器启动都不等于安装完成，重新启动后核对实际版本。SHA256 不等同系统代码签名。

独立 CLI 只在确认属于普通全局 npm 或全局 mise npm 时可更新。点击前展示当前来源、路径和确切目标版本；执行仅针对 Smart Search，保留原管理器，并读回实际版本。复杂 mise 工具选项、项目范围、版本约束、未知或冲突来源保留手动说明，不改 PATH。管理器运行期间保持 App 打开，不强制取消外部任务。CLI 状态刷新会重新读取路径和版本，不执行可能自动修复运行环境的公开 wrapper。
