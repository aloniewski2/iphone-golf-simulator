#!/bin/zsh
# builds the macOS render harness from the app's own MatchHero* files; usage: ./build.sh [outputBinary]
cd "$(dirname "$0")"
SRC=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/GolfArcade/Unity
OUT=${1:-/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/b8c5974c-0658-48af-9507-14588d72a05b/scratchpad/proto/harness}
swiftc -swift-version 6 -O main.swift $SRC/MatchHeroData.swift $SRC/MatchHeroSurfaces.swift $SRC/MatchHeroMotion.swift -framework SceneKit -framework AppKit -framework Metal -o "$OUT"
