#!/bin/bash
# Builds and runs the hit-synth gates on the Mac, then draws the charts. Output in work/hit-feel/out/.
set -e
cd "$(dirname "$0")"
mkdir -p out
clang++ -std=c++17 -O1 -Wall -Wextra -o out/test_hitfeel test_hitfeel.cpp
out/test_hitfeel out | tee out/gates.txt
python3 charts.py out/patterns.json out
