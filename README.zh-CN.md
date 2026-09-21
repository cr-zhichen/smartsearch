<div align="center">

<img src="https://raw.githubusercontent.com/konbakuyomu/smartsearch/main/assets/branding/smart-search.png" alt="Smart Search" width="112">

# Smart Search

**让 AI 联网查资料、读网页，回答时有来源可查。**

简体中文 | [English](README.md)

[![npm](https://img.shields.io/npm/v/@konbakuyomu/smart-search)](https://www.npmjs.com/package/@konbakuyomu/smart-search)
[![CI](https://github.com/konbakuyomu/smartsearch/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/konbakuyomu/smartsearch/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/konbakuyomu/smartsearch/blob/main/LICENSE)

[下载 App](https://github.com/konbakuyomu/smartsearch/releases/latest) · [使用手册](https://github.com/konbakuyomu/smartsearch/tree/main/docs/guide) · [反馈问题](https://github.com/konbakuyomu/smartsearch/issues)

</div>

## 这个工具是做什么的？

Smart Search 把搜索和网页读取服务接到 Codex、Claude Code 等 AI 工具里。你可以在 App 中配置服务并搜索，也可以接入后直接让 AI 调用。

搜索服务商由你选择，API Key 由你提供。App 负责管理配置，AI 通过独立的命令行工具执行搜索，关闭 App 后仍能使用。

## 哪些时候用得上？

- **写代码、查 API**：找到当前文档，读取与你的问题有关的页面。
- **了解新变化**：查公告或新闻，补上模型记忆里还没有的信息。
- **核对一个说法**：找到出处，读原文，再与其他来源对照。
- **研究一个复杂问题**：收集资料，检查缺少的证据，整理带来源的回答。

这些事情需要联网获取资料，也需要能回到原文核对。Smart Search 把搜索、读网页和服务商配置放在一起。搜到一条结果后，重要结论仍要看来源正文，不能只凭搜索摘要下判断。

## 怎么在 App 里配置？

1. **下载并打开 Smart Search。** 在[发行页](https://github.com/konbakuyomu/smartsearch/releases/latest)选择适合系统的安装包。App 自带运行环境。
2. **打开“服务商”。** 按页面提示配齐主搜索、文档检索和网页读取三类能力。页面会列出缺项和获取 Key 的入口；检查或测试配置后保存。
3. **打开“更新 Skills”。** 先在“共用独立 CLI 环境”准备命令行工具，再检查最新正式版 Skills，选择 Agent 并确认备份更新。支持 Codex、Claude Code、Cursor 等 17 个目标；Agent 软件由你安装并登录。
4. **点击“复制 AI 测试指引”。** 粘贴到 AI 的新对话里，先确认命令能运行，再试一次搜索。

服务商测试和搜索可能消耗相应服务的付费额度。打开 App 或检查本机环境不会发起付费搜索。

v0.1.22 新增独立的 Skills 下载与更新页面。每天自动检查只提示，点击后才更新所选 Agent；App、CLI 和 Skills 分别维护。文件名以 `-signed.exe` 结尾的 Windows 包使用**自签名证书**，仍可能出现 SmartScreen 提示；旧 `-unsigned-test.exe` 包仍未签名，详见 [Windows 签名与首次启动](docs/windows-signing.md)。macOS 仍未签名、未公证；macOS 与 Windows ARM64 实机验证仍待完成。

[查看 App 配置步骤、支持平台和排障说明 →](https://github.com/konbakuyomu/smartsearch/blob/main/docs/guide/zh-CN/app.md)

## 平时怎么使用？

**在 App 里用**：打开“搜索与研究”，输入问题或网页地址，查看结果和来源。“活动”页能看到执行进度及使用的服务商，结果可以复制或导出。

**在 AI 里用**：直接告诉它要查什么，例如：

> 用 Smart Search 查 React 当前文档里 useEffect 的清理规则，读相关页面，并给出来源。

> 用 Smart Search 核对这篇文章里的说法：https://example.com/article

使用时可以关闭 Smart Search App，AI 会按接入文件中的说明调用独立命令行工具。

**切换语言**：App 默认跟随系统，可在“设置与关于 → 语言”中选择中文或 English。命令行工具单独保存语言偏好，也能用 `--lang zh` / `--lang en` 临时切换。这些设置只改变界面和提示，不翻译来源网页。

## 想直接用命令行？

```sh
npm install -g @konbakuyomu/smart-search@latest
smart-search setup
```

手动安装 CLI 需要 Node.js 18+ 和 Python 3.10+。安装方法、语言设置和命令示例见 [CLI 使用指南](https://github.com/konbakuyomu/smartsearch/blob/main/docs/guide/zh-CN/cli.md)。

## 需要详细说明？

[完整手册](https://github.com/konbakuyomu/smartsearch/tree/main/docs/guide)包含全部命令与配置项，也说明服务商选择、研究模式和排障方法。开发与发布步骤也在手册中。

感谢 [LINUX DO](https://linux.do/) 社区的反馈和讨论。

[![Star History Chart](https://api.star-history.com/svg?repos=konbakuyomu/smartsearch&type=Date)](https://www.star-history.com/#konbakuyomu/smartsearch&Date)

本项目采用 [MIT](https://github.com/konbakuyomu/smartsearch/blob/main/LICENSE) 许可。
