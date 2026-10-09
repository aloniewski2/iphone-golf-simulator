#!/usr/bin/env bash
# Tools/PostcardCheck/run.sh <command> [args...]   (works from any cwd)
#
# Builds the standalone C# harness into Tools/PostcardCheck/.build/ with the Roslyn (csc.dll) and the
# dotnet runtime that ship inside Unity (no Unity launch, no project edit), then runs it.
# The harness compiles the REAL game sources (Hole.cs, CourseShot.cs, Wind.cs, Scorecard.cs, BallFlight.cs,
# GolfClub.cs, MotionSwingDetector.cs) from Unity/Assets/Scripts together with src/*.cs.
#
#   run.sh report --course Postcards            per hole: par, length, shore, hazards, widths, water carries
#   run.sh lies   --course Cliffside --step 2   dump LieAt of every grid cell as letters (equivalence testing; see equiv.py)
#   run.sh probe  --course X --hole N --at x,d  every sub-expression of LieAt at a point
#   run.sh gates  --course Postcards            independent design gates, GATE: NAME PASS|FAIL - detail (brief limits built in)
#   run.sh plan   --course Postcards [--hole 9] shot planner over the real CourseShot: PAR_REACHABLE, min strokes, ridge route
#   run.sh plan-selfcheck --course X            brute-force proof that the planner's prunings lose no finish
#   run.sh tests  <file.cs> [more.cs]           compile an NUnit test file against the real sources + shim and run it
#   run.sh build | clean | help
# Python helpers (system python3): equiv.py (Python mirror vs real LieAt), gates_diff.py (Python gates vs C# gates).
# Env: PC_HOLE_CS=/abs/Hole.cs (build against a patched Hole.cs), PC_BUILD=/abs/dir (alternative build dir), PC_EXTRA_SOURCES="a.cs b.cs",
#      PC_UNITY_SCRIPTING=<Unity .../Contents/Resources/Scripting> (otherwise the newest Unity under /Applications/Unity/Hub/Editor).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
BUILD="${PC_BUILD:-$HERE/.build}"          # PC_BUILD: alternative output dir (used to build against a patched Hole.cs in scratch)
SCRIPTS="$REPO/Unity/Assets/Scripts"

# ---- locate Unity's bundled dotnet + Roslyn --------------------------------------------------
find_unity_scripting() {
  if [ -n "${PC_UNITY_SCRIPTING:-}" ] && [ -x "$PC_UNITY_SCRIPTING/NetCoreRuntime/dotnet" ]; then echo "$PC_UNITY_SCRIPTING"; return; fi
  local want="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Resources/Scripting"
  if [ -x "$want/NetCoreRuntime/dotnet" ]; then echo "$want"; return; fi
  local c
  for c in $(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting 2>/dev/null | sort -r); do
    if [ -x "$c/NetCoreRuntime/dotnet" ] && [ -f "$c/DotNetSdkRoslyn/csc.dll" ]; then echo "$c"; return; fi
  done
  return 1
}
UNITY_SCRIPTING="$(find_unity_scripting)" || { echo "run.sh: no Unity with NetCoreRuntime/dotnet + DotNetSdkRoslyn found under /Applications/Unity/Hub/Editor" >&2; exit 90; }
DOTNET="$UNITY_SCRIPTING/NetCoreRuntime/dotnet"
CSC="$UNITY_SCRIPTING/DotNetSdkRoslyn/csc.dll"
FXDIR="$(ls -d "$UNITY_SCRIPTING"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ | sort | tail -1)"
FXDIR="${FXDIR%/}"
FXVER="$(basename "$FXDIR")"
export PC_DOTNET="$DOTNET" PC_CSC="$CSC" PC_FXDIR="$FXDIR" PC_BUILD="$BUILD" PC_REPO="$REPO"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_gcServer=0

GAME_SOURCES=(
  "${PC_HOLE_CS:-$SCRIPTS/Course/Hole.cs}" "$SCRIPTS/Course/CourseShot.cs" "$SCRIPTS/Course/Wind.cs" "$SCRIPTS/Course/Scorecard.cs"
  "$SCRIPTS/Shot/BallFlight.cs" "$SCRIPTS/Shot/GolfClub.cs" "$SCRIPTS/Swing/MotionSwingDetector.cs"
)
# PC_HOLE_CS=/path/Hole.cs swaps in a patched Hole.cs; PC_EXTRA_SOURCES="a.cs b.cs" adds pure-C# game sources (e.g. Scripts/Net/ControllerProtocol.cs for MotionSwingDetectorTests)
if [ -n "${PC_EXTRA_SOURCES:-}" ]; then read -r -a EXTRA <<< "$PC_EXTRA_SOURCES"; GAME_SOURCES+=("${EXTRA[@]}"); fi
HARNESS_SOURCES=("$HERE"/src/*.cs)

say() { printf '+ %s\n' "$*" >&2; }

build() {
  mkdir -p "$BUILD"
  local sum
  sum="$(cat "${GAME_SOURCES[@]}" "${HARNESS_SOURCES[@]}" "$CSC" | shasum | cut -d' ' -f1)"
  if [ -f "$BUILD/PostcardCheck.dll" ] && [ "$(cat "$BUILD/inputs.sha" 2>/dev/null || true)" = "$sum" ]; then
    say "build: up to date (inputs $sum)"
    return 0
  fi
  # compile into a private staging dir and move the result in, so two concurrent runs never see a half-written dll
  local stage="$BUILD/stage.$$"
  mkdir -p "$stage"
  local rsp="$stage/csc.rsp"
  {
    echo "-nologo -target:exe -optimize+ -debug:portable -langversion:10 -nullable:disable -warn:3 -nowarn:CS0649,CS0169,CS0219,CS0414,CS8632"
    echo "-out:$stage/PostcardCheck.dll -main:GolfArcade.PostcardCheck.Program"
    local f
    for f in "$FXDIR"/*.dll; do
      case "$(basename "$f")" in
        *Native*|createdump*) continue ;;      # native shims, not managed references
      esac
      echo "-r:$f"
    done
    for f in "${GAME_SOURCES[@]}" "${HARNESS_SOURCES[@]}"; do echo "$f"; done
  } > "$rsp"
  say "$DOTNET $CSC @$rsp      # $(grep -c '^-r:' "$rsp") framework references from $FXDIR, ${#GAME_SOURCES[@]} real game sources, ${#HARNESS_SOURCES[@]} harness sources"
  if ! "$DOTNET" "$CSC" "@$rsp"; then rm -rf "$stage"; echo "run.sh: build FAILED" >&2; exit 91; fi
  cat > "$stage/PostcardCheck.runtimeconfig.json" <<JSON
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "$FXVER" },
    "rollForward": "LatestMinor",
    "configProperties": {
      "System.GC.Server": false,
      "System.GC.Concurrent": false,
      "System.Runtime.TieredPGO": false,
      "System.Runtime.TieredCompilation.QuickJitForLoops": false,
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false
    }
  }
}
JSON
  cp "$rsp" "$BUILD/csc.rsp"
  mv -f "$stage/PostcardCheck.pdb" "$BUILD/PostcardCheck.pdb" 2>/dev/null || true
  mv -f "$stage/PostcardCheck.runtimeconfig.json" "$BUILD/PostcardCheck.runtimeconfig.json"
  mv -f "$stage/PostcardCheck.dll" "$BUILD/PostcardCheck.dll"
  echo "$sum" > "$BUILD/inputs.sha"
  rm -rf "$stage"
  say "build: OK -> $BUILD/PostcardCheck.dll"
}

cmd="${1:-help}"
case "$cmd" in
  clean) if [ -f "$BUILD/PostcardCheck.dll" ]; then say "rm -rf $BUILD"; rm -rf "$BUILD"; else say "nothing to clean at $BUILD"; fi; exit 0 ;;
  build) build; exit 0 ;;
  help|-h|--help) awk 'NR>1 && /^#/ {print; next} NR>1 {exit}' "$0"; exit 0 ;;
esac
build
say "$DOTNET $BUILD/PostcardCheck.dll $*"
exec "$DOTNET" "$BUILD/PostcardCheck.dll" "$@"
