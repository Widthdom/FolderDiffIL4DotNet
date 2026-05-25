from __future__ import annotations

from dataclasses import dataclass
from shlex import split as shlex_split
from typing import Sequence


DISALLOWED_SEARCH_COMMANDS = (
    "grep",
    "rg",
    "find",
    "fd",
    "locate",
    "git grep",
    "ls -R",
    "Get-ChildItem -Recurse",
    "Select-String",
)

DISALLOWED_CDEXECUTABLES = (
    "dotnet ./src/CodeIndex/bin/Debug/net8.0/cdidx.dll",
    "dotnet path/to/cdidx.dll",
)


@dataclass(frozen=True)
class CommandGuardDecision:
    allowed: bool
    reason: str = ""


def guard_command_text(command_text: str) -> CommandGuardDecision:
    normalized = " ".join(command_text.strip().split())
    if not normalized:
        return CommandGuardDecision(False, "empty command")

    for blocked in DISALLOWED_CDEXECUTABLES:
        if normalized.startswith(blocked):
            return CommandGuardDecision(False, "repository-local cdidx.dll execution is blocked")

    if normalized.startswith("dotnet ") and "cdidx.dll" in normalized:
        return CommandGuardDecision(False, "repository-local cdidx.dll execution is blocked")

    if _contains_blocked_search(normalized):
        return CommandGuardDecision(False, "shell search and discovery commands are blocked")

    return CommandGuardDecision(True)


def guard_argv(argv: Sequence[str]) -> CommandGuardDecision:
    if not argv:
        return CommandGuardDecision(False, "empty argv")

    command_text = " ".join(argv)
    return guard_command_text(command_text)


def parse_command_line(command_line: str) -> CommandGuardDecision:
    return guard_argv(shlex_split(command_line))


def _contains_blocked_search(command_text: str) -> bool:
    return any(blocked in command_text for blocked in DISALLOWED_SEARCH_COMMANDS)
