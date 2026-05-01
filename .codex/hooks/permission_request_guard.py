#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
HARNES_DIR = ROOT / ".agent_harness"
if str(HARNES_DIR) not in sys.path:
    sys.path.insert(0, str(HARNES_DIR))

from command_guard_core import guard_command_text


def main() -> int:
    command_text = " ".join(sys.argv[1:]).strip()
    decision = guard_command_text(command_text)
    if decision.allowed:
        return 0
    sys.stderr.write(decision.reason + "\n")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
