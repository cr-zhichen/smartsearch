[Guide](../README.md) · [简体中文](../zh-CN/troubleshooting.md)

# Troubleshooting

## Troubleshooting

If `doctor` reports `config_error`:

```powershell
smart-search setup
smart-search config list --format json
smart-search doctor --format markdown
```

If OpenAI-compatible `search` hangs or times out after `doctor` passes:

```powershell
smart-search doctor --format markdown
smart-search diagnose openai-compatible --format markdown
```

The diagnose report masks the API key and says whether the problem is missing config, the upstream/relay hanging on the real Smart Search prompt, or a stream/no-stream compatibility mismatch.

If search is slow:

- reduce `--extra-sources`;
- split broad questions into smaller queries;
- use `exa-search` or `zhipu-search` for source discovery, then `fetch` key pages.

If installed CLI health is uncertain:

```powershell
smart-search --help
smart-search --version
smart-search regression
smart-search smoke --mock --format json
```

On Windows npm/mise installs, verify non-ASCII JSON piping:

```powershell
smart-search deep "深度搜索一下最近的比特币行情" --format json | ConvertFrom-Json
```

## Interface language

Set the App language in Settings & about, and the independent CLI language with `smart-search config set SMART_SEARCH_LANGUAGE en`. If the CLI still uses another language, check a per-call `--lang` and the `SMART_SEARCH_LANGUAGE` environment override. `auto` follows the CLI locale, which can differ from the GUI session. Use `smart-search --lang en --help` to test without changing settings. Unreadable preferences fall back with a warning; repair that configuration file without deleting provider keys. Original page text and third-party logs do not change language.

## AI integration is still pending

Use AI integration to detect, install missing components, and verify local startup. A ready CLI and matching files are separate from actual AI invocation. Reopen the AI when asked, copy the test guide, close Smart Search, and let the AI execute the independent version command. A missing key requires provider configuration; a differing Skill file requires your decision before replacement. See [App setup](app.md).
