[Guide](../README.md) · [简体中文](../zh-CN/app.md)

# Set up and use the App

## Install and open

Download the package for your operating system and architecture from [Releases](https://github.com/konbakuyomu/smartsearch/releases/latest). Windows installs for the current user. The App includes its own runtime; using the App does not require a separate Python, Node.js, or CLI installation.

Environment preparation and full App/CLI language switching are available from v0.1.21; the Update Skills page is available from v0.1.22. Windows self-signing and the new transparent icon are available from v0.1.23. Windows `-signed.exe` packages use a self-signed certificate that Windows does not trust by default, so SmartScreen may still appear. Check the official release source and [public certificate fingerprint](../../windows-signing.md), then decide whether to use the options allowed by your system. This does not permanently trust the certificate or require disabling security protection. Older `-unsigned-test.exe` packages remain unsigned; macOS uses ad-hoc integrity signing without Developer ID signing or Apple notarization. Complete macOS/Windows ARM64 device use, clean-machine operation and the DPI matrix still require manual validation.

Version 0.1.24 introduces the first official Velopack/Sparkle update feeds and the refreshed native interface. Existing Inno installations need the one-time full migration described below. This first framework release provides complete update packages; later releases can add deltas against its verified baseline.

Open **Smart Search** from the Start menu or Applications. Existing configuration is reused. Overview shows whether you have providers for main search, documentation lookup, and page reading.

### macOS installation and first launch

1. Choose a Mac installer from the release download table. The universal version supports both Apple Silicon and Intel.
2. Open the DMG, drag **Smart Search** to **Applications**, and wait for the copy to finish.
3. Double-click **Smart Search** in Applications once to attempt the first launch.
4. If macOS cannot verify the developer or check the app, confirm it came from this project's release page. Open **Apple menu → System Settings → Privacy & Security**, scroll to Security, and click **Open Anyway** beside the Smart Search message.
5. Authenticate if requested, then click **Open** in the confirmation dialog. macOS remembers the exception for this app; future launches can use Applications directly.

If Open Anyway is missing, try launching the app again before returning to Settings. Organization-managed Macs may restrict this setting. See [Apple's first-launch instructions](https://support.apple.com/en-us/102445). For a damaged-app or will-damage-your-computer warning, first check the source, integrity and signature using the [macOS troubleshooting guide](troubleshooting.md#macos-says-the-app-is-damaged-or-the-developer-cannot-be-verified), rather than treating it as an unidentified-developer warning.

## Configure services

1. Open **Providers** and select a service for each missing capability. The page links to the provider's documentation and key registration page. See [Providers and configuration](configuration.md) for the choices.
2. Enter the address, API key and model that your provider supplies. Leave optional settings at their defaults unless your provider requires otherwise.
3. Test the draft if you want to check the connection, then preview and save the changes. Testing and searches can consume paid provider quota; simply opening the App does not run them.

Edits stay in a draft until saved. An empty key field keeps the saved key; select the explicit clear option to remove it. Fields overridden by environment variables show their effective source, and saving does not replace those environment values. Tasks already running keep their original configuration snapshot.

## Update Agent Skills

1. Open **Update Skills** and select **Check latest Skills**. The page downloads instruction files from the latest official stable npm package and shows the source version and check time. It does not run package code.
2. Review the Agents, target paths and changed filenames, then select your targets. Status describes the Smart Search Skill, not the Agent application's installation, version or successful invocation.
3. **Update selected Skills** is enabled only when a selected target has changed content, missing files or local invocation details to refresh. It is disabled when nothing is selected or all selected targets match. Review the source and paths, then confirm. Changed content is backed up first; the result shows backup paths. Extra files, unselected targets and legacy copies are kept.
4. Reopen the Agent session; Gemini can use `/skills reload`. Ask the Agent to run `smart-search --version` first, then test a search when needed.

Daily automatic checks are enabled by default and can be turned off. They notify without writing Agent directories. Offline or integrity failures show an error and label cached data; check successfully again before updating. App and CLI upgrades do not automatically sync Skills.

Codex, Claude Code, Cursor, Copilot, Gemini, OpenCode, Cline, Roo Code and the other listed targets use the same Skill. Codex uses `~/.agents/skills/smart-search-cli`; other compatible Agents may read that shared directory too. Historical `.codex/skills` copies are kept. Claude respects an absolute `CLAUDE_CONFIG_DIR`. OpenCode uses `~/.config/opencode/skills` and reports old `.opencode/skills` copies. WSL, remote hosts and Cloud Agents need their own setup; local files are not automatically synced there.

## Prepare the shared independent CLI

Under **Update Skills → Shared independent CLI environment**, detect the environment, review the plan, install missing components and verify availability. Healthy Node.js, Python and independent Smart Search CLI installations are reused. Missing components go into user-writable directories separate from the App. The App does not install or sign into Agent applications, and does not require mise.

Skills are compared by file content; a software version change does not imply changed Skills. An older or unverified CLI does not block instruction-file sync. Existing local invocation details are preserved when unverified, and new targets receive generic instructions. Changes to a verified invocation path are shown separately. Prepare the CLI before using it and verify real calls in the Agent. Configure missing provider keys rather than reinstalling the runtime.

Failed installation keeps completed components; detect again and retry the missing work. Wait for installation or Skills writes before quitting. Local checks and version verification do not make paid requests.

## Everyday pages

| Page | Use it to |
| --- | --- |
| Overview | Check missing capabilities and the next setup step |
| Providers | Edit, test, preview and save provider settings |
| Search & research | Search, read URLs, map sites, look up docs, plan offline research or run online research; copy/export results |
| Activity | See actual stages, providers, models and elapsed time; cancel tasks started by this App |
| Update Skills | Check stable Skills, back up and sync selected Agent targets; prepare the shared independent CLI |
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

In Settings & about, App updates use Velopack on Windows and Sparkle on macOS. Automatic checks run at most every 24 hours while the App is open; the switch stops future automatic checks and leaves manual checks available. A check downloads only metadata. Choose Update or Later when prompted; downloads and installation require your action. Errors remain errors, rather than being shown as “up to date”.

The frameworks download, verify and install the App and its private engine together, using a delta when applicable and a verified full package as fallback. Before restart, finish App tasks, CLI/environment/Skills writes and handle unsaved drafts. The App never cancels independent CLI tasks. A completed download is not a completed installation; check the actual version after restart.

The first move from an Inno Setup installation requires a full installation: finish writes, close the old App, uninstall its App entry in Windows Settings, run the new official Setup, then use its new shortcut. Detection and the included migration guide do not uninstall anything automatically. On macOS, close the old App and replace it once with the full download. Shared configuration, independent CLI, SmartSearchTools, Agent Skills, research evidence and exports stay in their original locations. Subsequent App updates use the framework.

Windows release signatures remain self-signed. Sparkle uses a separate EdDSA update signature, which is not Apple Developer ID signing or notarization. Local macOS candidates have ad-hoc signing only; a candidate without a configured update key disables updates. Unpacked Windows development builds likewise require a full framework install before updates work.

Build and protocol details: [desktop README](../../../desktop/README.md), [desktop protocol](../../../desktop/PROTOCOL.md). Problems: [Troubleshooting](troubleshooting.md).
