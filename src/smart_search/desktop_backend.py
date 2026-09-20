"""Private native-app stdio bridge. No listener, shell, or mandatory daemon."""
from __future__ import annotations

import asyncio
import contextlib
import json
import io
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import sys
import time
import uuid

import httpx

from . import cli, service, ui_api
from .activity import ActivityStore, RETENTION_SECONDS
from .config import config
from .desktop_catalog import command_arguments, command_catalog
from .provider_errors import sanitize_provider_error_message as sanitize_error_message

PROTOCOL_VERSION = 1
MAX_LINE = 2 * 1024 * 1024
RELEASE_URL = "https://github.com/konbakuyomu/smartsearch/releases"


def engine_command(*arguments):
    if getattr(sys, "frozen", False):
        return [sys.executable, *arguments]
    return [sys.executable, "-m", "smart_search.desktop_entry", *arguments]


def absolute_directory(value):
    if not isinstance(value, str) or not value.strip() or not Path(value).expanduser().is_absolute():
        raise ValueError("配置目录必须是绝对路径。")
    return str(Path(value).expanduser().resolve())


def child_environment(directory):
    # Provider credentials travel only in the private worker's stdin snapshot.
    return {**{key: value for key, value in os.environ.items() if key not in config._CONFIG_KEYS},
            "SMART_SEARCH_CONFIG_DIR": directory, "PYTHONUTF8": "1"}


class Backend:
    def __init__(self, emit):
        self.emit = emit
        self.generation = uuid.uuid4().hex
        self.directory = ""
        self.initialized = False
        self.stopping = False
        self.runs = {}
        self.cli_info = None

    def event(self, name, data):
        self.emit({"event": name, "generation": self.generation, "data": {**data, "generation": self.generation}})

    def activity(self, params=None):
        params = params or {}
        directories = params.get("directories", [self.directory])
        if not isinstance(directories, list) or len(directories) > 20:
            raise ValueError("最多可观察 20 个明确选择的配置目录。")
        limit = params.get("limit", 100)
        if type(limit) is not int or not 1 <= limit <= 1000:
            raise ValueError("limit 必须介于 1 与 1000。")
        runs, errors, enabled = [], [], True
        directories = list(dict.fromkeys(absolute_directory(value) for value in directories))
        for directory in directories:
            try:
                store = ActivityStore(directory)
                runs.extend(store.list_runs(limit))
                if directory == self.directory:
                    enabled = store.enabled()
            except (OSError, ValueError, sqlite3.Error):
                errors.append({"config_dir": directory, "error": "活动记录不可读，当前状态未知。"})
        by_id = {row["run_id"]: row for row in runs}
        for run in self.runs.values():
            if run["directory"] not in directories or run.get("history_cleared") or time.time() - run.get("finished_at", time.time()) > RETENTION_SECONDS:
                continue
            row = by_id.get(run["run_id"])
            if row is None:
                by_id[run["run_id"]] = self.run_metadata(run)
            elif run["status"] not in {"running", "cancelling"}:
                row["status"] = run["status"]
        runs = list(by_id.values())
        return {"ok": not errors, "runs": sorted(runs, key=lambda row: row["started_at"], reverse=True)[:limit],
                "errors": errors, "enabled": enabled}

    @staticmethod
    def run_metadata(run):
        now = time.time()
        terminal = run["status"] not in {"running", "cancelling"}
        return {"run_id": run["run_id"], "command": run["command"], "origin": "app", "config_dir": run["directory"],
                "version": cli._get_version(), "pid": run["process"].pid if run["process"] else None,
                "status": run["status"], "phase": "completed" if terminal else "running", "provider": "", "model": "",
                "started_at": run["started_at"], "updated_at": run.get("finished_at", now),
                "finished_at": run.get("finished_at"), "elapsed_ms": (run.get("finished_at", now) - run["started_at"]) * 1000,
                "sequence": 0, "error_type": (run["result"] or {}).get("error_type", ""), "config_revision": "",
                "recorded": False}

    def state(self):
        data = ui_api.state()
        data.update(protocol_version=PROTOCOL_VERSION, version=cli._get_version(), generation=self.generation,
                    config_dir=self.directory, commands=command_catalog(), activity=self.activity(), cli=self.cli_status())
        checks = {}
        for run in sorted(self.runs.values(), key=lambda item: item.get("finished_at", 0)):
            if run["command"] != "provider.test" or run["directory"] != self.directory or not run.get("finished_at"):
                continue
            result = run["result"] or {}
            checks[run["provider"]] = {"status": result.get("status", run["status"]), "checked_at": run["finished_at"],
                                       "source": "app", "scope": run["scope"], "probe": result.get("probe", "none"),
                                       "message": result.get("message", result.get("error", ""))}
        data["provider_checks"] = checks
        return data

    def cli_status(self):
        if self.cli_info is not None:
            return self.cli_info
        bundled = sys.executable if getattr(sys, "frozen", False) else " ".join(engine_command())
        external = shutil.which("smart-search")
        data = {"bundled_path": bundled, "external_path": external, "version": cli._get_version(),
                "external_version": None, "activity_protocol_version": 1, "external_activity_protocol_version": None,
                "external_status": "未发现外部 CLI" if not external else "版本及观测能力尚未验证"}
        if external:
            external_path = Path(external).resolve()
            probe_command = None
            if getattr(sys, "frozen", False) and external_path == Path(sys.executable).resolve():
                probe_command = [str(external_path)]
            else:
                # npm's public wrapper repairs a missing runtime on invocation. Discovery
                # must never trigger that installer; probe an existing private Python only.
                candidates = [external_path.parent / "node_modules/@konbakuyomu/smart-search",
                              external_path.parent.parent / "@konbakuyomu/smart-search",
                              external_path.parent.parent.parent]
                for root in candidates:
                    try:
                        package = json.loads((root / "package.json").read_text(encoding="utf-8"))
                    except (OSError, ValueError):
                        continue
                    if package.get("name") != "@konbakuyomu/smart-search":
                        continue
                    data["external_version"] = package.get("version")
                    python = root / ".smart-search-python" / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
                    if python.is_file():
                        probe_command = [str(python), "-m", "smart_search.cli"]
                    else:
                        data["external_status"] = "外部 npm CLI 的运行环境缺失；App 不会自动修复该安装。"
                    break
            if probe_command is None:
                self.cli_info = data
                return data
            env = {**child_environment(self.directory), "SMART_SEARCH_ACTIVITY_ENABLED": "false"}
            try:
                version = subprocess.run([*probe_command, "--version"], capture_output=True, text=True, encoding="utf-8", errors="replace",
                                         timeout=3, env=env, creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
                if version.returncode == 0 and version.stdout.strip().startswith("smart-search "):
                    data["external_version"] = version.stdout.strip().split()[-1]
                capabilities = subprocess.run([*probe_command, "--desktop-capabilities"], capture_output=True, text=True, encoding="utf-8", errors="replace",
                                              timeout=3, env=env, creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
                if capabilities.returncode == 0:
                    parsed = json.loads(capabilities.stdout)
                    data["external_activity_protocol_version"] = parsed.get("activity_protocol_version")
                data["external_status"] = "支持实时活动" if data["external_activity_protocol_version"] == 1 else "该 CLI 尚未接入活动观测；升级后可见新调用。"
            except (OSError, ValueError, subprocess.TimeoutExpired):
                data["external_status"] = "外部 CLI 检查未完成；不能判断是否支持活动观测。"
        self.cli_info = data
        return data

    def enable_cli(self, params):
        if params.get("confirm") is not True:
            raise ValueError("只有用户点击启用后才可配置内置 CLI。")
        if not getattr(sys, "frozen", False):
            raise ValueError("请从已打包的 App 启用 CLI；开发环境不更改 PATH。")
        existing = shutil.which("smart-search")
        executable = Path(sys.executable).resolve()
        if existing and Path(existing).resolve() != executable:
            raise ValueError("已存在同名 CLI。请保留原命令或直接复制内置路径，不会覆盖。")
        if os.name == "nt":
            import winreg
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
                try:
                    current, value_type = winreg.QueryValueEx(key, "Path")
                except FileNotFoundError:
                    current, value_type = "", winreg.REG_EXPAND_SZ
                parts = current.split(";") if current else []
                directory = str(executable.parent)
                if directory.casefold() not in {part.casefold() for part in parts}:
                    winreg.SetValueEx(key, "Path", 0, value_type, ";".join([*parts, directory]))
            self.cli_info = None
            return {"ok": True, "path": str(executable), "message": "已加入当前用户 PATH，请重新打开终端。"}
        target = Path.home() / ".local" / "bin" / "smart-search"
        if target.exists() or target.is_symlink():
            if target.resolve() != executable:
                raise ValueError("~/.local/bin/smart-search 已存在，不会覆盖。")
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.symlink_to(executable)
        self.cli_info = None
        return {"ok": True, "path": str(target), "message": "已创建用户 CLI 入口。若 ~/.local/bin 不在 PATH，请使用所示完整路径。"}

    def start(self, method, params):
        if sum(run["status"] in {"running", "cancelling"} for run in self.runs.values()) >= 8:
            raise ValueError("最多同时运行 8 个 App 任务。")
        command = params.get("command") if method == "run.start" else method
        if method == "run.start":
            argv = command_arguments(command, params.get("arguments", []))
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                try:
                    cli.build_parser().parse_args(argv)
                except SystemExit as error:
                    raise ValueError("参数无效，请检查必填项和参数范围。") from error
        elif method == "provider.test":
            if params.get("provider") not in service.PROBE_KIND:
                raise ValueError("未知服务商。")
            overrides = params.get("overrides", {})
            if not isinstance(overrides, dict) or any(not isinstance(v, str) for v in overrides.values()):
                raise ValueError("overrides 必须是字符串键值对象。")
            for key, value in overrides.items():
                if key not in config._CONFIG_KEYS:
                    raise ValueError("未知配置键。")
                if value:
                    config._validate_config_value(key, value)
        elif method == "skills.install":
            if not isinstance(params.get("targets"), list) or not params["targets"]:
                raise ValueError("请选择要安装的 Skills 目标。")
            ui_api._resolve_targets(params["targets"])
        values = config.effective_values(masked=False)
        if method == "provider.test":
            values.update(params.get("overrides", {}))
        run_id = uuid.uuid4().hex
        payload = {"method": method, "command": command, "params": params, "values": values,
                   "sources": config.get_config_sources(), "config_dir": self.directory, "run_id": run_id}
        run = {"run_id": run_id, "status": "running", "result": None, "process": None, "cancel_requested": False,
               "directory": self.directory, "command": command, "started_at": time.time(),
               "provider": params.get("provider", "") if method == "provider.test" else "",
               "scope": "draft" if params.get("overrides") else "current"}
        self.runs[run_id] = run
        run["task"] = asyncio.create_task(self.execute(run, payload))
        # Results contain user content only in memory; keep a bounded recent set.
        completed = [key for key, value in self.runs.items() if value["status"] not in {"running", "cancelling"}]
        for key in completed[:-100]:
            self.runs.pop(key)
        return {"ok": True, "run_id": run_id}

    async def execute(self, run, payload):
        try:
            process = await asyncio.create_subprocess_exec(*engine_command("--desktop-worker"),
                stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL,
                env=child_environment(payload["config_dir"]),
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
            run["process"] = process
            process.stdin.write((json.dumps(payload, ensure_ascii=False) + "\n").encode("utf-8"))
            await process.stdin.drain()
            if run["cancel_requested"]:
                process.stdin.write(b'{"cancel":true}\n')
                await process.stdin.drain()
            chunks, size = [], 0
            while chunk := await process.stdout.read(65536):
                size += len(chunk)
                if size > 32 * 1024 * 1024:
                    process.kill()
                    raise ValueError("结果超过 32 MiB；请缩小请求或选择输出文件。")
                chunks.append(chunk)
            output = b"".join(chunks)
            await process.wait()
            if not output or process.returncode:
                raise ValueError("任务进程已中断。")
            envelope = json.loads(output)
            run.update(status=envelope["status"], result=envelope["result"])
        except (Exception, asyncio.CancelledError):
            run.update(status="cancelled" if run["cancel_requested"] else "failed",
                       result={"ok": False, "error_type": "cancelled" if run["cancel_requested"] else "runtime_error",
                               "error": "任务已取消。" if run["cancel_requested"] else "任务进程中断或响应无效。"})
        finally:
            run["finished_at"] = time.time()
            process = run["process"]
            if process is not None and process.returncode is None:
                process.kill()
                await process.wait()
            with contextlib.suppress(Exception):
                ActivityStore(run["directory"]).finish(run["run_id"], 0 if run["status"] == "finished" else 5, run["status"],
                                                     error_type=run["result"].get("error_type", ""))
            self.event("run", self.run_result(run["run_id"]))

    def run_result(self, run_id):
        if run_id not in self.runs:
            raise ValueError("该任务不属于当前 App 后端；外部 CLI 只能观察。")
        run = self.runs[run_id]
        return {"ok": True, "run_id": run_id, "status": run["status"], "result": run["result"]}

    async def cancel(self, run_id):
        self.run_result(run_id)
        run = self.runs[run_id]
        if run["status"] not in {"running", "cancelling"}:
            return self.run_result(run_id)
        run["cancel_requested"], run["status"] = True, "cancelling"
        process = run["process"]
        if process is not None and process.returncode is None:
            with contextlib.suppress(BrokenPipeError, ConnectionResetError):
                process.stdin.write(b'{"cancel":true}\n')
                await process.stdin.drain()
        asyncio.create_task(self.cancel_deadline(run))
        return {"ok": True, "run_id": run_id, "status": "cancelling"}

    async def cancel_deadline(self, run):
        try:
            await asyncio.wait_for(asyncio.shield(run["task"]), 3)
        except asyncio.TimeoutError:
            process = run["process"]
            if process is not None and process.returncode is None:
                process.kill()

    async def close(self):
        self.stopping = True
        for run_id in list(self.runs):
            await self.cancel(run_id)
        await asyncio.gather(*(run["task"] for run in self.runs.values()), return_exceptions=True)

    async def handle(self, method, params):
        if method == "ping":
            return {"protocol_version": 1, "version": cli._get_version(), "generation": self.generation}
        if method == "initialize":
            if type(params.get("protocol_version")) is not int or params["protocol_version"] != PROTOCOL_VERSION:
                raise ValueError("App 与后端协议版本不匹配，请安装完整的同版本 App。")
            self.directory = absolute_directory(params["config_dir"]) if params.get("config_dir") else str(config.config_file.parent)
            self.initialized = True
        if not self.initialized:
            raise ValueError("请先完成协议版本握手。")
        if method == "profile.select":
            self.directory = absolute_directory(params.get("config_dir"))
            self.cli_info = None
        with config.snapshot(directory=self.directory):
            if method in {"initialize", "profile.select", "get_state"}:
                return self.state()
            if method == "config.preview":
                values = dict(params.get("set", {}))
                values.update({key: "" for key in params.get("unset", [])})
                return ui_api.preview({"values": values})
            if method == "config.apply":
                if not isinstance(params.get("revision"), str):
                    raise ValueError("保存必须带上当前配置 revision。")
                return ui_api.apply_config(params)
            if method in {"run.start", "provider.test", "skills.install"}:
                return self.start(method, params)
            if method == "run.result":
                return self.run_result(params.get("run_id"))
            if method == "run.cancel":
                return await self.cancel(params.get("run_id"))
            if method == "providers.reset":
                return ui_api.reset_health(params)
            if method == "skills.status":
                return ui_api.skills_status(params.get("targets"))
            if method == "activity.list":
                return self.activity(params)
            if method == "activity.details":
                directory = absolute_directory(params.get("config_dir", self.directory))
                run_id = str(params.get("run_id", ""))
                own_run = self.runs.get(run_id)
                try:
                    details = ActivityStore(directory).details(run_id)
                except (OSError, sqlite3.Error):
                    if own_run is None:
                        raise
                    details = {"ok": False}
                if not details["ok"] and own_run is not None and own_run["directory"] == directory:
                    return {"ok": True, "run": self.run_metadata(own_run), "events": [], "events_truncated": False,
                            "note": "这次任务的活动未持久记录，当前状态来自 App 自有进程。"}
                return details
            if method == "activity.clear":
                ActivityStore(self.directory).clear()
                for run in self.runs.values():
                    if run["directory"] == self.directory and run["status"] not in {"running", "cancelling"}:
                        run["history_cleared"] = True
                return {"ok": True}
            if method == "activity.enabled":
                if type(params.get("enabled")) is not bool:
                    raise ValueError("enabled 必须为布尔值。")
                ActivityStore(self.directory).set_enabled(params["enabled"])
                return {"ok": True, "enabled": params["enabled"]}
            if method == "cli.status":
                return self.cli_status()
            if method == "cli.enable":
                return self.enable_cli(params)
            if method == "app.update-check":
                try:
                    async with httpx.AsyncClient(timeout=10, follow_redirects=False) as client:
                        response = await client.get("https://api.github.com/repos/konbakuyomu/smartsearch/releases/latest")
                        response.raise_for_status()
                        release = response.json()
                    return {"ok": True, "current_version": cli._get_version(), "latest_version": release["tag_name"], "url": RELEASE_URL}
                except (httpx.HTTPError, KeyError, ValueError):
                    return {"ok": False, "current_version": cli._get_version(), "error": "暂无可读取的正式发行版，请查看官方发行页面。", "url": RELEASE_URL}
            if method == "shutdown":
                await self.close()
                return {"ok": True}
        raise ValueError("未知协议方法。")


async def serve():
    def emit(data):
        print(json.dumps(data, ensure_ascii=False, separators=(",", ":")), flush=True)

    backend = Backend(emit)

    async def poll():
        while not backend.stopping:
            await asyncio.sleep(1)
            if backend.initialized:
                backend.event("activity", backend.activity())

    poller = asyncio.create_task(poll())
    try:
        while not backend.stopping:
            line = await asyncio.to_thread(sys.stdin.buffer.readline, MAX_LINE + 1)
            if not line:
                break
            identifier = None
            try:
                if len(line) > MAX_LINE:
                    raise ValueError("请求超过允许长度。")
                request = json.loads(line)
                if not isinstance(request, dict) or type(request.get("id")) is not int or request["id"] <= 0:
                    raise ValueError("请求必须包含正整数 id。")
                identifier = request["id"]
                params = request.get("params", {})
                if not isinstance(params, dict):
                    raise ValueError("params 必须是对象。")
                result = await backend.handle(request.get("method"), params)
                emit({"id": identifier, "generation": backend.generation, "result": result})
            except Exception as error:
                message = sanitize_error_message(str(error)) if isinstance(error, ValueError) else "本地操作失败，请检查目录权限或重新启动 App。"
                emit({"id": identifier, "generation": backend.generation, "error": {"code": "parameter_error" if isinstance(error, ValueError) else "runtime_error", "message": message}})
                if len(line) > MAX_LINE:
                    break
    finally:
        poller.cancel()
        await backend.close()


def main():
    asyncio.run(serve())
    return 0
