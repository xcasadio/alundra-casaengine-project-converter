#!/bin/bash
# runmut.sh <name>...: for each mutant, apply, build, run the pickup tests, restore, compare to the pristine copy. Prints "name: failed=N killed|SURVIVED".
S=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad
K=-p:CustomAfterMicrosoftCommonTargets=$S/e20e0a3b-exec/skipcopy.targets
cd /d/development/repo/alundra-casaengine-project-converter || exit 1
FILTER="FullyQualifiedName~PickupAiDropWorldTests|FullyQualifiedName~PickupAiDropVectorTests|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~PickupAiTests"
for name in "$@"; do
  python -B $S/mut.py apply $name || { echo "$name: APPLY FAILED"; continue; }
  if dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release $K 2>&1 | grep -q " error "; then
    echo "$name: BUILD ERROR"
  else
    dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build $K --blame-hang-timeout 60s --filter "$FILTER" > $S/mut_$name.log 2>&1
    line=$(grep -E "^(Échoué|Réussi)!" $S/mut_$name.log | tail -1)
    n=$(echo "$line" | grep -oE 'ch[^0-9]*[0-9]+' | head -1 | grep -oE '[0-9]+$')
    if [ "$n" -gt 0 ] 2>/dev/null; then echo "$name: failed=$n killed"; else echo "$name: SURVIVED ($line)"; fi
  fi
  python -B $S/mut.py restore $name
done
