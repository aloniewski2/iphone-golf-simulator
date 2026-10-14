#!/bin/zsh
# Compile every GolfArcade assembly and run the EditMode tests WITHOUT opening the project in
# Unity, using the compiler and runtime the editor ships. Handy while the editor GUI has the
# project open (headless Unity then refuses to start). It needs the editor to have imported the
# project once (Library/ScriptAssemblies and Library/PackageCache must exist).
#
#   Unity/Tools/check.sh            # compile runtime + tests, run EditMode tests
#
# PlayMode tests still need the editor: Window → General → Test Runner, or -runTests.
set -e
cd "$(dirname "$0")/.."
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/$(cat ProjectSettings/ProjectVersion.txt | sed -n 's/^m_EditorVersion: //p')/Unity.app}
S=$UNITY/Contents/Resources/Scripting
DOTNET=$S/NetCoreRuntime/dotnet
CSC="$DOTNET $S/DotNetSdkRoslyn/csc.dll -nologo -nullable:disable -langversion:9 -nowarn:1701,1702,0436"
RT=$(ls -d $S/NetCoreRuntime/shared/Microsoft.NETCore.App/* | tail -1)
OUT=${OUT:-Library/Check}
mkdir -p "$OUT"

refs=(-r:$S/NetStandard/ref/2.1.0/netstandard.dll)
for d in $S/NetStandard/compat/2.1.0/shims/netstandard/*.dll $S/Managed/UnityEngine/UnityEngine.*Module.dll $S/Managed/UnityEngine/UnityEngine.dll Library/ScriptAssemblies/UnityEngine.UI.dll; do refs+=("-r:$d"); done
eval $CSC -deterministic -target:library -out:"$OUT/GolfArcade.dll" "${refs[@]}" 'Assets/Scripts/**/*.cs'
echo "GolfArcade compiled"

NUNIT=$(ls Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom/nunit.framework.dll | head -1)
trefs=("${refs[@]}" -r:$S/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll -r:"$OUT/GolfArcade.dll" -r:"$NUNIT" -r:Library/ScriptAssemblies/UnityEngine.TestRunner.dll -r:Library/ScriptAssemblies/UnityEditor.TestRunner.dll -r:$S/Managed/UnityEditor.dll)
eval $CSC -target:library -out:"$OUT/GolfArcade.Tests.dll" "${trefs[@]}" Assets/Tests/EditMode/*.cs
eval $CSC -target:library -out:"$OUT/GolfArcade.PlayTests.dll" "${trefs[@]}" Assets/Tests/PlayMode/*.cs
echo "tests compiled"

# Run the EditMode tests: they only touch pure C#, so a reflection runner on the bare runtime is enough.
cp "$NUNIT" "$OUT/"
eval $CSC -target:exe -out:"$OUT/TestRunner.dll" -r:$RT/System.Runtime.dll -r:$RT/System.Private.CoreLib.dll -r:$RT/System.Console.dll -r:$RT/System.Linq.dll -r:$RT/System.Collections.dll -r:$RT/System.Runtime.Loader.dll -r:$RT/System.Runtime.Extensions.dll Tools/TestRunner.cs
printf '{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$(basename $RT)" > "$OUT/TestRunner.runtimeconfig.json"
$DOTNET "$OUT/TestRunner.dll" "$OUT"
