#!/bin/zsh
# Stamp the build: Assets/Resources/build_id.txt carries the short commit hash (with a + when the
# tree is dirty) and the time, and the menu shows it, so a phone can be checked against the code.
cd "$(dirname "$0")/.."
hash=$(git rev-parse --short HEAD 2>/dev/null || echo local)
dirty=$(git status --porcelain --untracked-files=no 2>/dev/null | grep -q . && echo "+" || echo "")
echo "${hash}${dirty} $(date +%m-%d\ %H:%M)" > Assets/Resources/build_id.txt
cat Assets/Resources/build_id.txt
