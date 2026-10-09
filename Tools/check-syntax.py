#!/usr/bin/env python3
"""Syntax-only check (tree-sitter) for C# and Swift files that cannot be compiled outside Unity/Xcode.

    python3 Tools/check-syntax.py                 # every .cs/.swift file changed vs origin's branch + the working tree
    python3 Tools/check-syntax.py path/to/File.swift other.cs

This catches unbalanced braces, bad statements and typos in syntax. It does NOT type-check: a missing method or a
wrong argument still needs Xcode, Unity, or CI. Setup: pip install tree-sitter tree-sitter-c-sharp tree-sitter-swift
"""
import subprocess, sys

try:
    import tree_sitter_c_sharp, tree_sitter_swift
    from tree_sitter import Language, Parser
except ImportError:
    sys.exit("Install the parsers first: pip install tree-sitter tree-sitter-c-sharp tree-sitter-swift")

PARSERS = {".cs": Parser(Language(tree_sitter_c_sharp.language())), ".swift": Parser(Language(tree_sitter_swift.language()))}


def changed_files():
    out = set()
    for cmd in (["git", "diff", "--name-only", "HEAD"], ["git", "diff", "--name-only", "--cached"],
                ["git", "ls-files", "--others", "--exclude-standard"]):
        out.update(subprocess.run(cmd, capture_output=True, text=True).stdout.split())
    return sorted(f for f in out if f.endswith((".cs", ".swift")))


def first_errors(node, limit=5):
    found = []
    stack = [node]
    while stack and len(found) < limit:
        n = stack.pop()
        if n.type == "ERROR" or n.is_missing:
            found.append(n)
        else:
            stack.extend(reversed(n.children))
    return found


def check(path):
    try:
        data = open(path, "rb").read()
    except OSError as e:
        return [f"{path}: cannot read ({e})"]
    parser = PARSERS[path[path.rindex("."):]]
    tree = parser.parse(data)
    if not tree.root_node.has_error:
        return []
    problems = []
    for n in first_errors(tree.root_node):
        line = n.start_point[0] + 1
        text = data.splitlines()[line - 1].decode("utf8", "replace").strip()[:100]
        kind = "missing " + n.type if n.is_missing else "syntax error"
        problems.append(f"{path}:{line}: {kind}: {text}")
    return problems


def main():
    files = sys.argv[1:] or changed_files()
    bad = 0
    for f in files:
        problems = check(f)
        if problems:
            bad += 1
            print("\n".join(problems))
    print(f"GATE: syntax — {'FAIL' if bad else 'PASS'} ({len(files)} file(s) checked, {bad} with errors)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
