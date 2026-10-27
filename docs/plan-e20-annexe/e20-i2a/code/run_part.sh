#!/bin/bash
# usage: run_part.sh <name> <filter> [Release|Debug]
SP=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad
SK=$SP/e20e0a3b-exec/skipcopy.targets
CFG=${3:-Release}
EXTRA=""
[ "$CFG" = "Release" ] && EXTRA="-p:CustomAfterMicrosoftCommonTargets=$SK"
OUT=$SP/i2a-t0b/runs-$CFG
mkdir -p $OUT
cd /d/development/repo/alundra-casaengine-project-converter
dotnet test Alundra.Tests/Alundra.Tests.csproj -c $CFG --no-build $EXTRA --blame-hang-timeout 60s --filter "$2" --nologo --logger "trx;LogFileName=$OUT/$1.trx" > $OUT/$1.log 2>&1
grep -E "Échoué!|Réussi!|Passed!|Failed!" $OUT/$1.log
python -B - <<PY
import re,sys
s=open(r"$OUT/$1.trx",encoding='utf-8').read()
for m in re.finditer(r'<UnitTestResult [^>]*testName="([^"]*)"[^>]*outcome="Failed"',s): print("RED",m.group(1))
PY
