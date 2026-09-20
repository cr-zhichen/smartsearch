[Guide](../README.md) · [简体中文](../zh-CN/app.md)

# Set up and use the App

## Install and open

Download the package for your operating system and architecture from [Releases](https://github.com/konbakuyomu/smartsearch/releases/latest). Windows installs for the current user. The App includes its own runtime; using the App does not require a separate Python, Node.js, or CLI installation.

Environment preparation and full App/CLI language switching are available from v0.1.21. Desktop packages are unsigned; macOS, Windows ARM64, clean-machine installation, and the complete DPI matrix have not all been validated on devices.

Open **Smart Search** from the Start menu or Applications. Existing configuration is reused. Overview shows whether you have providers for main search, documentation lookup, and page reading.

## Configure services

1. Open **Providers** and select a service for each missing capability. The page links to the provider's documentation and key registration page. See [Providers and configuration](configuration.md) for the choices.
2. Enter the address, API key and model that your provider supplies. Leave optional settings at their defaults unless your provider requires otherwise.
3. Test the draft if you want to check the connection, then preview and save the changes. Testing and searches can consume paid provider quota; simply opening the App does not run them.

Edits stay in a draft until saved. An empty key field keeps the saved key; select the explicit clear option to remove it. Fields overridden by environment variables show their effective source, and saving does not replace those environment values. Tasks already running keep their original configuration snapshot.

## Connect an AI tool

1. Open **AI integration**. Select the installed Codex or Claude Code application you want to connect.
2. Select **Detect environment**. Detection reads local state without installing components. Review the plan before selecting **Install missing components**.
3. Follow the progress. Healthy existing Node.js, Python and independent Smart Search CLI installations are reused. Missing components are installed in user-writable directories separate from the App. You do not need to install mise. The App does not install your AI application or sign into it.
4. Select **Verify availability**. If the CLI starts but search configuration is missing, configure services; reinstalling the runtime will not supply an API key.
5. Copy the AI test instructions, close Smart Search, and paste the instructions into a new AI conversation. The first test only asks the AI to run the independent CLI's version command, without a network search. Then try a search if you want to test provider connectivity.

The page separately reports the runtime, CLI startup, integration files, restart requirements and actual AI invocation. Files being ready does not prove the AI loaded them. Reopen an existing AI or terminal when prompted; the App does not terminate those processes. WSL and remote hosts need their own setup.

If existing integration files differ, they are kept. Compare them before choosing replacement; an explicit replacement makes a recoverable backup and preserves extra files. Codex uses `~/.agents/skills/smart-search-cli`, while historical `.codex/skills` copies are reported and retained. Claude Code uses `~/.claude/skills/smart-search-cli`. Other existing CLI installation targets remain available through [the CLI](cli.md).

Installation failure shows the failed step and a recovery action. Detect again and retry the remaining work. During protected installation writes, wait for completion before updating, reconnecting or exiting. Checks and local version verification do not make paid AI requests.

## Everyday pages

| Page | Use it to |
| --- | --- |
| Overview | Check missing capabilities and the next setup step |
| Providers | Edit, test, preview and save provider settings |
| Search & research | Search, read URLs, map sites, look up docs, plan offline research or run online research; copy/export results |
| Activity | See actual stages, providers, models and elapsed time; cancel tasks started by this App |
| AI integration | Detect and prepare the independent CLI, install selected integration files and verify local readiness |
| Settings & about | Choose the configuration directory, observe additional activity directories, set theme/language, reset health status and check updates |

Search results need source checking. A hit or snippet is a candidate source, not proof that its page was read. See [Search, research and evidence](research.md).

## Language

Use **Settings & about → Language** to select automatic, 简体中文 or English. Automatic uses Chinese for a Chinese system language and English otherwise. The choice is saved for this App. Switching retains unsaved provider drafts and running searches; protected installation or update writes must finish first.

The independent CLI saves its own preference. Changing the App does not change that preference, provider configuration, AI language, queries, answers or source pages. Third-party installation logs may remain in their original language.

For a manual check, switch to English, visit each page, then switch to Chinese and restart the App. Confirm that the choice persists, the draft remains intact, buttons wrap at the minimum window size and keyboard navigation works. Check an invalid local setting and its error as well; no paid request is needed for these checks.

## App and CLI independence

The App's private engine serves the App. An independently installed npm CLI runs separately and remains available after the App closes or is uninstalled. App upgrades do not replace that CLI; its update uses the original installation manager.

Both can share provider settings by selecting the same configuration directory. Windows defaults to `%LOCALAPPDATA%\smart-search`, with the legacy home directory supported. `SMART_SEARCH_CONFIG_DIR` or the App's directory selector can isolate configurations. Different inherited environment variables can still produce different effective settings.

The private engine's absolute path also works while the App is closed, but uninstalling the App removes that engine. Use the independent CLI for lasting AI integration. External activity requires CLI 0.1.19 or newer; older CLI versions still work but do not emit the new activity events.

## Activity, updates and removal

Activity refreshes every two seconds and observes only the current or explicitly added directories. By default it records metadata rather than queries, answers, headers or keys. Completed entries are retained for at most seven days / 1,000 entries by default. Clearing activity does not delete configuration, research evidence or exports. An expired heartbeat means the status is stale, not that the task succeeded.

Closing the window with a running App task offers continuing in the background, cancelling App tasks and exiting, or returning. Restore the background App from its tray/menu icon; launching it again restores the same window. Independent CLI tasks started by a terminal or AI are not cancelled by the App.

Use the update page or download a new installer from Releases. Finish protected writes and close the App before replacing its files. The Windows installer refuses to overwrite private resources while the App is still running. Uninstalling preserves shared configuration, independent CLI data, research evidence and exported results.

Build and protocol details: [desktop README](../../../desktop/README.md), [desktop protocol](../../../desktop/PROTOCOL.md). Problems: [Troubleshooting](troubleshooting.md).
