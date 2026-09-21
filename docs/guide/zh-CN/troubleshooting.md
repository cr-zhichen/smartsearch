[手册目录](../README.md) · [English](../en/troubleshooting.md)

# 排障

## 排障

如果 `doctor` 返回 `config_error`：

```powershell
smart-search setup
smart-search config list --format json
smart-search doctor --format markdown
```

如果搜索慢：

- 降低 `--extra-sources`；
- 把大问题拆成多个小问题；
- 先用 `exa-search` 或 `zhipu-search` 找来源，再 `fetch` 关键网页。

如果想确认安装是否正常：

```powershell
smart-search --help
smart-search --version
smart-search regression
smart-search smoke --mock --format json
```

Windows npm/mise 安装后建议验证中文 JSON 管道：

```powershell
smart-search deep "深度搜索一下最近的比特币行情" --format json | ConvertFrom-Json
```

## CLI 包已升级，但提示运行环境未就绪

mise 等管理器可能跳过 npm 包的安装脚本，因此包版本已更新并不代表 Python 环境已准备好。App 的“更新 CLI”会在核对安装来源后补齐该包的私有 Python 环境，并验证实际运行结果；失败时保留日志，允许重新检查后重试同一版本。普通刷新仍只读。若缺少可用的独立 Python，请先在“共用独立 CLI 环境”完成准备。终端中的 `smart-search --version` 也可能触发首次环境修复，因此第一次耗时更长。

## App 更新失败或不可用

恢复网络后在设置页重新检查 App 更新。已下载仍需安装和重启；先处理 App 任务、受保护写入与未保存草稿。Windows 开发散包和旧 Inno 安装需先运行新的完整安装器；没有更新密钥的 macOS 测试候选不能更新。只使用官方且架构匹配的版本；签名或完整性失败不能绕过，保留当前安装并报告错误。App 更新不会自动修复或升级独立 CLI/Skills。

## 界面语言没有变化

App 在“设置与关于”选择语言，独立 CLI 用 `smart-search config set SMART_SEARCH_LANGUAGE zh` 保存偏好。如果 CLI 仍是另一种语言，检查单次 `--lang` 和 `SMART_SEARCH_LANGUAGE` 环境变量覆盖。`auto` 跟随 CLI 的 locale，可能与图形会话不同。可用 `smart-search --lang zh --help` 检查，不会改变设置。偏好无法读取时会提示并回退；修复该配置文件时保留服务商 Key。网页原文和第三方日志不随界面翻译。

## AI 接入仍显示待验证

在“更新 Skills”检查正式版并选择有差异的 Agent；仅软件版本变化不会要求同步 Skills。CLI 未就绪不阻止正文同步，但实际调用前仍需在“共用独立 CLI 环境”准备。设置页识别失败时，查看具体原因并点击“刷新已安装版本”，再检查更新。Skill 文件一致不代表 Agent 已加载，重新打开会话或使用 Gemini `/skills reload` 后验证实际版本。内容不同会先备份，结果显示恢复副本路径。详见 [App 配置](app.md)。
