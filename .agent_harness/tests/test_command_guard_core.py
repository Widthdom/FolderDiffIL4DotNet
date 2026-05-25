import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
HARNES_DIR = ROOT / ".agent_harness"
if str(HARNES_DIR) not in sys.path:
    sys.path.insert(0, str(HARNES_DIR))

from command_guard_core import guard_argv, guard_command_text


class CommandGuardCoreTests(unittest.TestCase):
    def test_guard_command_text_allows_cdidx(self) -> None:
        decision = guard_command_text("cdidx search foo")
        self.assertTrue(decision.allowed)

    def test_guard_command_text_blocks_shell_search(self) -> None:
        decision = guard_command_text("grep foo README.md")
        self.assertFalse(decision.allowed)
        self.assertIn("blocked", decision.reason)

    def test_guard_command_text_blocks_repo_local_cdidx(self) -> None:
        decision = guard_command_text("dotnet ./src/CodeIndex/bin/Debug/net8.0/cdidx.dll search foo")
        self.assertFalse(decision.allowed)

    def test_guard_argv_blocks_ls_r(self) -> None:
        decision = guard_argv(["ls", "-R"])
        self.assertFalse(decision.allowed)


if __name__ == "__main__":
    unittest.main()
