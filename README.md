<div align="center">

<img src="https://raw.githubusercontent.com/konbakuyomu/smartsearch/main/assets/branding/smart-search.png" alt="Smart Search" width="112">

# Smart Search

**Search the web, read sources, and bring current information into your AI conversations.**

[简体中文](README.zh-CN.md) | English

[![npm](https://img.shields.io/npm/v/@konbakuyomu/smart-search)](https://www.npmjs.com/package/@konbakuyomu/smart-search)
[![CI](https://github.com/konbakuyomu/smartsearch/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/konbakuyomu/smartsearch/actions/workflows/ci.yml)
[![MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/konbakuyomu/smartsearch/blob/main/LICENSE)

[Download the App](https://github.com/konbakuyomu/smartsearch/releases/latest) · [User guide](https://github.com/konbakuyomu/smartsearch/tree/main/docs/guide) · [Report a problem](https://github.com/konbakuyomu/smartsearch/issues)

</div>

## What is Smart Search?

Smart Search connects search and page-reading services to AI tools such as Codex and Claude Code. Use the desktop App to configure your services and run searches. Once connected, you can also ask your AI to use Smart Search directly.

You choose the providers and supply their API keys. The App manages configuration; the independent command-line tool runs AI requests even after you close the App.

## When is it useful?

- **Working with a library or API:** find current documentation and read the relevant pages.
- **Following recent changes:** look up announcements or news that may be newer than a model's knowledge.
- **Checking a claim:** find its source, read the page, and compare it with other evidence.
- **Researching a larger question:** collect sources, inspect gaps, and build a cited answer.

These tasks need information from the web and a way to trace it back to its source. Smart Search puts search, page reading, and provider configuration in one place. A search hit is a starting point; read the source before relying on an important claim.

## Get started in the App

1. **Download and open Smart Search.** Choose the package for your system from [Releases](https://github.com/konbakuyomu/smartsearch/releases/latest). The App includes its own runtime.
2. **Open Providers.** Add services for the three required jobs: answering searches, finding documentation, and reading pages. The page shows what is still missing and where to obtain each key. Check or test the settings, then save them.
3. **Open Update Skills.** Prepare the command-line tool under Shared independent CLI environment, then check the latest stable Skills, select your Agents, and confirm backup and sync. Codex, Claude Code, Cursor and 14 other targets are supported. Install and sign in to the Agent applications yourself.
4. **Copy the AI test instructions.** Paste them into a new AI conversation. First check that the command runs; then try a search.

Provider tests and searches may use your providers' paid quota. Opening the App or checking the local environment does not run a paid search.

Version 0.1.22 adds a dedicated Skills download and update page. Daily checks only notify; selected Agent files change after you click to update. The App, CLI and Skills are maintained separately. Windows packages ending in `-signed.exe` use a **self-signed certificate** and may still trigger SmartScreen; older `-unsigned-test.exe` packages are unsigned. See [Windows signatures and first launch](docs/windows-signing.md). macOS remains unsigned and unnotarized; macOS and Windows ARM64 device validation is still pending.

[App setup, supported platforms, and troubleshooting →](https://github.com/konbakuyomu/smartsearch/blob/main/docs/guide/en/app.md)

## Everyday use

**In the App:** open Search & research, enter a question or URL, and review the result and its sources. Activity shows what is running and which provider it used. You can copy or export the result.

**In your AI tool:** ask naturally, for example:

> Use Smart Search to find the current React documentation for useEffect cleanup. Read the relevant page and include the source.

> Use Smart Search to check the claims in this page: https://example.com/article

The App can stay closed. Your AI calls the independent CLI using the installed integration instructions.

**Language:** the App follows your system by default. Change it in Settings & about → Language. The CLI has its own saved preference and a one-command `--lang en` / `--lang zh` override. These settings change the tool's interface and messages, not the text of a source page.

## Prefer the terminal?

```sh
npm install -g @konbakuyomu/smart-search@latest
smart-search setup
```

Manual CLI installation needs Node.js 18+ and Python 3.10+. See the [CLI guide](https://github.com/konbakuyomu/smartsearch/blob/main/docs/guide/en/cli.md) for installation, language settings, commands, and examples.

## Need more detail?

The [complete guide](https://github.com/konbakuyomu/smartsearch/tree/main/docs/guide) covers all commands and configuration keys, provider choices, research, and troubleshooting. Development and release instructions are there too.

Thanks to the [LINUX DO](https://linux.do/) community for its feedback and discussion.

[![Star History Chart](https://api.star-history.com/svg?repos=konbakuyomu/smartsearch&type=Date)](https://www.star-history.com/#konbakuyomu/smartsearch&Date)

Licensed under [MIT](https://github.com/konbakuyomu/smartsearch/blob/main/LICENSE).
