#!/bin/bash
# t4_runmut.sh <name>...: for each mutant of t4_mutants.py, apply, build, run the pickup tests, restore. Prints "name: failed=N killed|SURVIVED".
# The restored file is compared with the pristine copy by the caller (git diff of the two files).
S=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad
HERE="$(cd "$(dirname "$0")" && pwd)"
K=-p:CustomAfterMicrosoftCommonTargets=$S/e20e0a3b-exec/skipcopy.targets
cd /d/development/repo/alundra-casaengine-project-converter || exit 1
FILTER="FullyQualifiedName~PickupAiPlaced|FullyQualifiedName~PickupAiDrop|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~PickupAiTests|FullyQualifiedName~DestroyEntityBreakEffectTests"
for name in "$@"; do
  python -B "$HERE/t4_mutants.py" apply $name || { echo "$name: APPLY FAILED"; continue; }
  if dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release $K 2>&1 | grep -q " error "; then
    echo "$name: BUILD ERROR"
  else
    dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build $K --blame-hang-timeout 60s --filter "$FILTER" --logger "console;verbosity=normal" > $S/e20e0c-t4/mut_$name.log 2>&1
    n=$(grep -cE "^\s+Échoué " $S/e20e0c-t4/mut_$name.log)
    first=$(grep -E "^\s+Échoué " $S/e20e0c-t4/mut_$name.log | head -2 | sed -E 's/^\s+Échoué Alundra.Tests.//;s/ \[.*//' | tr '\n' ';')
    if [ "$n" -gt 0 ] 2>/dev/null; then echo "$name: failed=$n killed  ($first)"; else echo "$name: SURVIVED"; fi
  fi
  python -B "$HERE/t4_mutants.py" restore $name
done
