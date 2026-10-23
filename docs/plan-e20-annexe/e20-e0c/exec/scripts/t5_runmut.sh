#!/bin/bash
# t5_runmut.sh <name>...: for each mutant of t5_mutants.py, apply, build (Release, neutralised copy), run the tests of the slice, restore. Prints "name: failed=N killed|SURVIVED|BUILD ERROR".
# The restored file is compared with HEAD by the caller (git diff).
S=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad
HERE="$(cd "$(dirname "$0")" && pwd)"
K=-p:CustomAfterMicrosoftCommonTargets=$S/e20e0a3b-exec/skipcopy.targets
cd /d/development/repo/alundra-casaengine-project-converter || exit 1
FILTER="FullyQualifiedName~PickupAi|FullyQualifiedName~LootDrop|FullyQualifiedName~ItemEffects|FullyQualifiedName~HeroHeals|FullyQualifiedName~WorldProxyBlock|FullyQualifiedName~DestroyEntityBreakEffect|FullyQualifiedName~NativeSpriteRegistry|FullyQualifiedName~BeannoideLootArc|FullyQualifiedName~DialogueDirectorTests|FullyQualifiedName~ItemTablesTests|FullyQualifiedName~BeannoideOracleTests|FullyQualifiedName~MurutaOracleTests"
for name in "$@"; do
  python -B "$HERE/t5_mutants.py" apply $name || { echo "$name: APPLY FAILED"; continue; }
  if dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release $K 2>&1 | grep -q " error "; then
    echo "$name: BUILD ERROR"
  else
    dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build $K --blame-hang-timeout 60s --filter "$FILTER" --logger "console;verbosity=normal" > $S/e20e0c-t5/mut_$name.log 2>&1
    n=$(grep -cE "^\s+Échoué " $S/e20e0c-t5/mut_$name.log)
    first=$(grep -E "^\s+Échoué " $S/e20e0c-t5/mut_$name.log | head -2 | sed -E 's/^\s+Échoué Alundra.Tests.//;s/ \[.*//' | tr '\n' ';')
    if [ "$n" -gt 0 ] 2>/dev/null; then echo "$name: failed=$n killed  ($first)"; else echo "$name: SURVIVED"; fi
  fi
  python -B "$HERE/t5_mutants.py" restore $name
done
