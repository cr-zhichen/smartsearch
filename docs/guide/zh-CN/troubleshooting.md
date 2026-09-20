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

## 界面语言没有变化

App 在“设置与关于”选择语言，独立 CLI 用 `smart-search config set SMART_SEARCH_LANGUAGE zh` 保存偏好。如果 CLI 仍是另一种语言，检查单次 `--lang` 和 `SMART_SEARCH_LANGUAGE` 环境变量覆盖。`auto` 跟随 CLI 的 locale，可能与图形会话不同。可用 `smart-search --lang zh --help` 检查，不会改变设置。偏好无法读取时会提示并回退；修复该配置文件时保留服务商 Key。网页原文和第三方日志不随界面翻译。

## AI 接入仍显示待验证

在“AI 接入”依次检测、补齐缺项，再验证本地启动。CLI 可运行、文件一致和 AI 实际调用是不同状态。按提示重新打开 AI，复制测试指引，关闭 Smart Search，让 AI 执行独立 CLI 的版本命令。缺少 Key 应去配置服务商；Skill 内容不同则先决定是否替换，详见 [App 配置](app.md)。
