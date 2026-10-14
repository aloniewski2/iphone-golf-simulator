#!/bin/zsh
# macOS SceneKit render harness for the lobby parity pass: compiles the app's own MatchHero* files. usage: ./build.sh <outputBinary>
cd "$(dirname "$0")"
SRC=/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/GolfArcade/Unity
swiftc -swift-version 6 -O main.swift $SRC/MatchHeroData.swift $SRC/MatchHeroSurfaces.swift $SRC/MatchHeroMotion.swift -framework SceneKit -framework AppKit -framework Metal -o "$1"
