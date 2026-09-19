"""Expose the existing argparse contract to both native frontends."""
from __future__ import annotations

import argparse
from functools import lru_cache

from .cli import build_parser

_MANAGED = {"setup", "config", "skills", "providers", "ui"}
_LABELS = {"search": "搜索", "route": "查看路由", "deep": "离线研究计划", "research": "在线研究",
           "fetch": "读取网页", "map": "站点地图", "doctor": "服务商全面诊断（在线）",
           "smoke": "冒烟检查", "regression": "离线回归检查", "model/current": "当前模型",
           "route-calibrate": "校准意图路由", "exa-search": "Exa 搜索", "exa-similar": "Exa 相似网页",
           "zhipu-search": "智谱搜索", "zhipu-mcp-search": "智谱 MCP 搜索", "zhipu-mcp-reader": "智谱 MCP 网页阅读",
           "zhipu-mcp-search-doc": "智谱 仓库文档检索", "zhipu-mcp-repo-structure": "智谱 仓库目录",
           "zhipu-mcp-read-file": "智谱 仓库文件阅读", "context7-library": "Context7 查找库", "context7-docs": "Context7 查文档",
           "anysearch-domains": "AnySearch 查询领域", "anysearch-search": "AnySearch 搜索", "anysearch-extract": "AnySearch 读取网页",
           "anysearch-batch": "AnySearch 批量搜索", "sciverse-catalog": "SciVerse 目录", "sciverse-search": "SciVerse 搜索",
           "sciverse-semantic": "SciVerse 语义搜索", "sciverse-read": "SciVerse 文献阅读", "sciverse-relations": "SciVerse 关联检索",
           "diagnose": "诊断 OpenAI 兼容接口"}
_FIELD_LABELS = {"query": "问题或关键词", "queries": "问题列表（每行一项）", "url": "网页地址", "urls": "网页地址（每行一项）",
                 "provider": "服务商", "repo": "代码仓库", "path": "文件路径", "library_id": "库标识", "name": "名称",
                 "budget": "研究深度", "timeout": "超时（秒）", "model": "模型", "output": "输出文件（可选）",
                 "evidence_dir": "研究证据目录", "ref": "分支或版本", "count": "结果数量", "num_results": "结果数量",
                 "router_mode": "路由方式", "validation": "验证程度", "fallback": "回退策略", "providers": "服务商筛选",
                 "extra_sources": "额外来源数量", "stream": "启用流式请求", "no_stream": "禁用流式请求"}


@lru_cache(maxsize=1)
def command_catalog() -> list[dict]:
    entries = []

    def visit(parser, tokens=(), help_text=""):
        sub = next((a for a in parser._actions if isinstance(a, argparse._SubParsersAction)), None)
        if sub is not None:
            seen = set()
            for name, child in sub.choices.items():
                if id(child) in seen:
                    continue
                seen.add(id(child))
                if not tokens and name in _MANAGED:
                    continue
                if tokens == ("model",) and name != "current":
                    continue
                description = next((a.help for a in sub._choices_actions if a.dest == name), "")
                visit(child, (*tokens, name), description)
            return
        fields = []
        for arg in parser._actions:
            if arg.dest in {"help", "format"} or arg.help == argparse.SUPPRESS:
                continue
            flags = [s for s in arg.option_strings if s.startswith("--")]
            if isinstance(arg, (argparse._StoreTrueAction, argparse._StoreFalseAction)):
                kind, default = "bool", False
            else:
                kind = "choice" if arg.choices else "int" if arg.type is int else "float" if arg.type is float else "text"
                default = None if arg.default == argparse.SUPPRESS else arg.default
            field_name = flags[0][2:].replace("-", "_") if flags else arg.dest
            fields.append({"name": field_name,
                           "label": _FIELD_LABELS.get(field_name, flags[0] if flags else arg.dest), "help": arg.help or "", "flags": flags[:1],
                           "kind": kind, "choices": list(arg.choices or []), "required": arg.required,
                           "default": default, "multiple": isinstance(arg, argparse._AppendAction) or arg.nargs in {"+", "*"},
                           "nargs": arg.nargs, "advanced": bool(flags) and arg.dest not in {"budget", "evidence_dir"}})
        identifier = "/".join(tokens)
        entries.append({"id": identifier, "label": _LABELS.get(identifier, identifier),
                        "description": help_text or parser.description or "", "fields": fields,
                        "experimental": tokens[0].startswith(("anysearch-", "sciverse-"))})

    visit(build_parser())
    return entries


def command_arguments(command: str, arguments: list[str]) -> list[str]:
    if command not in {item["id"] for item in command_catalog()}:
        raise ValueError("未知或不可从工具页调用的命令。")
    if not isinstance(arguments, list) or any(not isinstance(arg, str) for arg in arguments):
        raise ValueError("arguments 必须是字符串数组。")
    if len(arguments) > 256 or sum(len(arg) for arg in arguments) > 256 * 1024:
        raise ValueError("命令参数过长。")
    if any(arg in {"--format", "-h", "--help"} or arg.startswith("--format=") for arg in arguments):
        raise ValueError("桌面结果格式由 App 管理。")
    return [*command.split("/"), *arguments]
