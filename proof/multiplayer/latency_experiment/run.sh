#!/usr/bin/env bash
# Replays a perfectly timed tennis return through the REAL host rules (NetworkTennisMatch, TennisRules)
# with simulated screen delay, network delay and clock error. Needs only the .NET 8 SDK:
# no Unity, no phone. The few UnityEngine math types the rules use come from Shim.cs.
#
#   proof/multiplayer/latency_experiment/run.sh            # prints the three tables
#
# "proposed" = the fix described in PLAN_Multiplayer_OnlineLocal.md (A3): the guest adds its measured
# screen delay to the swing age, and the host's rewind limit goes from 0.15 s to 0.40 s. It is applied
# to a temporary COPY of the sources; the repository is never modified.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
SRC="$REPO/Unity/Assets/Scripts"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

build() { # $1 = folder name, $2 = 1 to apply the proposed constants
  local d="$WORK/$1"; mkdir -p "$d/src"
  cp "$SRC/Multiplayer/MultiplayerProtocol.cs" "$SRC/Multiplayer/NetworkTennisMatch.cs" \
     "$SRC/Tennis/TennisRules.cs" "$SRC/Tennis/TennisMatch.cs" "$SRC/Tennis/TennisEmotes.cs" "$SRC/Tennis/TennisBall.cs" "$d/src/"
  cp "$HERE/Shim.cs" "$HERE/Program.cs" "$d/"
  if [ "$2" = 1 ]; then
    sed -i 's/age>=0 \&\& age<=\.25/age>=0 \&\& age<=.5/' "$d/src/MultiplayerProtocol.cs"
    sed -i 's/MaximumRewind=\.15/MaximumRewind=.40/' "$d/src/NetworkTennisMatch.cs"
    grep -q 'age<=\.5' "$d/src/MultiplayerProtocol.cs" && grep -q 'MaximumRewind=\.40' "$d/src/NetworkTennisMatch.cs" \
      || { echo "The source no longer contains the constants this experiment patches; update run.sh." >&2; exit 2; }
  fi
  cat > "$d/Lat.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable><ImplicitUsings>disable</ImplicitUsings><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0162;CS0168;CS0219;CS0649;CS0414;CS8632;CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup><Compile Include="Shim.cs" /><Compile Include="Program.cs" /><Compile Include="src/*.cs" /></ItemGroup>
</Project>
XML
  dotnet build "$d/Lat.csproj" -nologo -v q -o "$d/out" > "$d/build.log" 2>&1 || { cat "$d/build.log" >&2; exit 1; }
}

build current 0
build proposed 1
dotnet "$WORK/current/out/Lat.dll" current
dotnet "$WORK/proposed/out/Lat.dll" proposed
dotnet "$WORK/current/out/Lat.dll" clock
