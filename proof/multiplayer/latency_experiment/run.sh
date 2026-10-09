#!/usr/bin/env bash
# Replays a perfectly timed tennis return through the REAL host rules (NetworkTennisMatch, TennisRules) with
# simulated screen delay, network delay and clock error. Needs only the .NET 8 SDK: no Unity, no phone. The few
# UnityEngine types the rules use come from Tools/netsim/Shim/UnityShim.cs.
#
#   proof/multiplayer/latency_experiment/run.sh                 # BEFORE (old commit) vs AFTER (working tree)
#   BEFORE_REF=<commit> proof/multiplayer/latency_experiment/run.sh
#
# BEFORE is built from `git show` of BEFORE_REF (default: the commit just before the screen-delay credit and the
# point-rule fixes), so the repository is never modified.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
SRC="$REPO/Unity/Assets/Scripts"
BEFORE_REF="${BEFORE_REF:-9a305ef7}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
FILES="Multiplayer/MultiplayerProtocol.cs Multiplayer/NetworkTennisMatch.cs Tennis/TennisRules.cs Tennis/TennisMatch.cs Tennis/TennisEmotes.cs Tennis/TennisBall.cs Tennis/TennisTossCurve.cs"

build() { # $1 = name, $2 = git ref or WORKTREE, $3 = extra defines
  local d="$WORK/$1"; mkdir -p "$d/src"
  for f in $FILES; do
    if [ "$2" = WORKTREE ]; then cp "$SRC/$f" "$d/src/"; else git -C "$REPO" show "$2:Unity/Assets/Scripts/$f" > "$d/src/$(basename "$f")"; fi
  done
  if [ "$2" = WORKTREE ]; then cp "$SRC/Multiplayer/NetworkTuning.cs" "$d/src/"; fi
  cp "$REPO/Tools/netsim/Shim/UnityShim.cs" "$d/Shim.cs"; cp "$HERE/Program.cs" "$d/"
  cat > "$d/Lat.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable><ImplicitUsings>disable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <DefineConstants>$3</DefineConstants>
    <NoWarn>CS0162;CS0168;CS0219;CS0649;CS0414;CS8632;CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup><Compile Include="Shim.cs" /><Compile Include="Program.cs" /><Compile Include="src/*.cs" /></ItemGroup>
</Project>
XML
  dotnet build "$d/Lat.csproj" -nologo -v q -o "$d/out" > "$d/build.log" 2>&1 || { cat "$d/build.log" >&2; exit 1; }
}

build before "$BEFORE_REF" ""
build after WORKTREE "AFTER"
echo "=== BEFORE: commit $BEFORE_REF (the phone credits no screen delay) ==="
dotnet "$WORK/before/out/Lat.dll" screen
echo "=== AFTER: working tree (the phone credits its screen delay; the host waits for a started swing) ==="
dotnet "$WORK/after/out/Lat.dll" screen
echo "=== CLOCK ERROR: working tree ==="
dotnet "$WORK/after/out/Lat.dll" clock
