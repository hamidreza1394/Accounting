#!/usr/bin/env python3
"""Fails when a workflow runs a repository script directly but Git does not store it as 100755.

A script that a workflow starts as `scripts/x.sh` (not `bash scripts/x.sh`) needs the
executable bit in the Git index; without it the job dies with "Permission denied" (exit 126).
That happened to scripts/publish.sh in commit cc7176d and stayed hidden because the jobs that
run it (`publish`, `e2e`) are skipped whenever `build-and-test` fails.

Usage: python3 scripts/tests/check_workflow_script_modes.py [--repo-root .]
"""
import argparse
import glob
import os
import re
import subprocess
import sys

SCRIPT_TOKEN = re.compile(r"^(?:\./)?(scripts/[A-Za-z0-9_./-]+\.(?:sh|py))$")
SPLITTER = re.compile(r"&&|\|\||;|\||\$\(|`")


def direct_invocations(text):
    """Yields (line number, script path) for scripts started as the command itself."""
    for number, raw in enumerate(text.splitlines(), start=1):
        line = raw.strip()
        if line.startswith("#"):
            continue
        line = re.sub(r"^-\s+", "", line)
        line = re.sub(r"^(?:run:\s*)(?:[|>][-+]?\s*)?", "", line)
        for part in SPLITTER.split(line):
            words = part.strip().split()
            # skip leading variable assignments such as FOO=bar
            while words and re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", words[0]):
                words = words[1:]
            if words:
                match = SCRIPT_TOKEN.match(words[0])
                if match:
                    yield number, match.group(1)


def index_mode(repo_root, path):
    result = subprocess.run(
        ["git", "-C", repo_root, "ls-files", "-s", "--", path],
        capture_output=True,
        text=True,
    )
    fields = result.stdout.split()
    return fields[0] if fields else None


def check(repo_root):
    problems = []
    for workflow in sorted(glob.glob(os.path.join(repo_root, ".github", "workflows", "*.yml"))):
        with open(workflow, encoding="utf-8") as file:
            text = file.read()
        for number, script in direct_invocations(text):
            mode = index_mode(repo_root, script)
            name = os.path.relpath(workflow, repo_root)
            if mode is None:
                problems.append(f"{name}:{number}: '{script}' is started directly but is not tracked by Git.")
            elif mode != "100755":
                problems.append(
                    f"{name}:{number}: '{script}' is started directly but is stored as {mode}, "
                    "not 100755 (run: git update-index --chmod=+x)."
                )
    return problems


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    arguments = parser.parse_args(argv)
    problems = check(arguments.repo_root)
    for problem in problems:
        print(f"error: {problem}", file=sys.stderr)
    if problems:
        return 1
    print("Every script that a workflow starts directly is stored as 100755.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
