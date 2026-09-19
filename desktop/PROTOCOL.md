# Smart Search desktop protocol v1

Both native clients implement this contract. The Python core owns configuration,
validation, routing and status. Do not open an HTTP listener or invoke a shell.

Launch bundled `backend/smart-search.exe --desktop-backend` on Windows;
`Contents/Resources/backend/smart-search --desktop-backend` in the macOS bundle.
For development an explicitly selected backend path may override this location.
Redirect UTF-8 stdin/stdout/stderr; keep the console hidden on Windows. Each stdin
and stdout line is one compact JSON object. stderr is diagnostic, never protocol.

Request: `{"id":1,"method":"initialize","params":{"protocol_version":1}}`.
Response: `{"id":1,"result":{...}}` or
`{"id":1,"error":{"code":"parameter_error","message":"..."}}`.
Events: `{"event":"activity","data":{...}}`,
`{"event":"run","data":{"run_id":"...","status":"finished","result":{...}}}`.
IDs are positive integers and each request receives one response. Each process has
a new `generation` UUID; discard data from a previous client/process generation.
Methods below return result objects. Ordinary business errors have `ok:false`.

| Method | Parameters | Result |
| --- | --- | --- |
| `ping` | `{}` | `protocol_version`, `version`, `generation` |
| `initialize` | `protocol_version:1`, optional absolute `config_dir` | full state below |
| `get_state` | `{}` | full state, local/read-only |
| `profile.select` | absolute `config_dir` | full state |
| `config.preview` | `set` object, `unset` key array | `ok`, `minimum_profile_ok`, `missing`, `capability_status` |
| `config.apply` | `set`, `unset`, `revision` from state | `ok`, `error`, `error_type`, refreshed `status` |
| `provider.test` | `provider`, `overrides` containing ONLY actual edited values (not masked placeholders) | `ok`, `run_id`; completion via run event/result |
| `run.start` | `command` catalog id, `arguments` string array (arguments following the command) | `ok`, `run_id` |
| `run.cancel` | `run_id` owned by this backend | `ok`, `status:"cancelling"`; wait for terminal event |
| `run.result` | `run_id` | `ok`, `run_id`, `status`, `result` or null |
| `providers.reset` | optional `providers` id array | `ok`, `cleared` |
| `skills.status` | optional `targets` id array | `ok`, `targets` array |
| `skills.install` | nonempty `targets` array | `ok`, `run_id` |
| `activity.list` | optional absolute `directories` array, `limit` 1..1000 | `ok`, `runs`, `errors`, `enabled` |
| `activity.clear` | `{}` | `ok`, clears completed metadata only |
| `activity.enabled` | `enabled` boolean | `ok`, `enabled` |
| `activity.details` | `run_id`, optional absolute `config_dir` | `ok`, `run`, metadata-only `events`, `events_truncated` |
| `cli.status` | `{}` | `bundled_path`, `external_path`, `version`, external version or null |
| `cli.enable` | `confirm:true`, sent only by explicit user action | `ok`, `path`, `message`; refuses command conflicts |
| `app.update-check` | `{}`; only user initiated | `ok`, `current_version`, `latest_version`, `url`, `error` |
| `shutdown` | `{}` | `ok`; cancels own work and exits |

`run.start` catalog identifiers can include subcommands, e.g.
`model/current`. `diagnose` selects its provider via its catalog fields. Arguments do not repeat command
tokens. A secret configuration mutation uses `config.apply`, not CLI arguments.
`provider.test` always tests a draft snapshot; no health changes are persisted.

Full state extends `smart_search.ui_api.state()`:

- `ok`, `values` (masked effective values), `saved_values` (masked file values),
  `sources` (`environment/config_file/default`), `revision`, `config_path`;
- `minimum_profile` (`ok/required/missing`), `capability_status`,
  `capability_chains`, `provider_health`, `provider_profiles`, `probe_kinds`;
- `provider_checks`: last in-memory test per provider for this App session, with
  `status/checked_at/source/scope/probe/message`. Scope is `draft`, and a draft
  test must never be described as a verified saved configuration. No entry means
  not tested during this session; cooldown `closed` alone is not a successful probe.
- `metadata.fields`: key, section, tier, kind, label_zh/en, help_zh/en, default,
  choices, provider, capabilities, key_url, docs_url; sections include
  getting_started, providers, routing, reliability, diagnostics;
- `skill_targets`: id, label, default;
- `protocol_version:1`, `version`, `generation`, `config_dir`, `cli`,
  `commands` (catalog below), `activity` (activity.list result).

Catalog entry: `id`, `label`, `description`, `experimental`, `fields`.
Field: `name` (argparse dest), `label`, `help`, `flags` (empty for positional),
`kind` (`text/int/float/bool/choice`), `choices`, `required`, `default`, `multiple`.
Send positionals in catalog order; optional values as flag then value; checked
boolean flags as a standalone flag; repeated values repeat the flag. Do not send
empty optional fields. Forms can show advanced parameters in an expander.
Commands already represented by config/skills/provider UI are not duplicated in
the ordinary tool catalog. CLI compatibility does not require arbitrary shell UI.

Activity run fields: `run_id`, `command`, `origin` (`app/cli`), `config_dir`,
`version`, `pid`, `status` (`running/finished/failed/cancelled/stale/interrupted`),
`phase`, `provider`, `model`, `started_at`, `updated_at`, `finished_at`,
`elapsed_ms`, `error_type`, `exit_code`, `sources_count`, `sequence`, `config_revision`.
Timestamps are Unix seconds. Live rows update at least every two seconds when
events arrive; duration may tick locally, but never fabricate percent complete.
No request arguments, query, content, headers or credentials are in the journal.
`config_revision` in an activity row is an opaque identifier for that invocation's
frozen snapshot, not a hash of secret values or the optimistic-save revision.

Editing rules: empty untouched or erased secret input means KEEP; an explicit
Clear control adds the key to `unset`; entering a replacement adds `set[key]`.
Read-only environment fields show the masked effective value and its source.
Save sends the displayed revision; conflicts keep the draft and offer refresh.

Native UI destinations: Overview, Providers, Search & Research, Activity,
AI integration, Settings & About. Use native controls/theme/keyboard/focus.
Current results render readable content and sources, with JSON in an advanced
expander and explicit copy/export. Never make raw JSON the primary UI.
Business `result.display_text` reuses the existing CLI Markdown formatter, keeping
plans, lists, diagnostics and sources readable without a second native formatter.
This additive desktop-only field is not added to public CLI JSON output.
Close with own active work offers background/stop-and-quit/return. Background
has a tray/menu-bar entry. Never terminate external CLI processes.
