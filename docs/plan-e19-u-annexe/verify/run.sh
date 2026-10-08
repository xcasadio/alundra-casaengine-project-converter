#!/bin/sh
# usage: run.sh head|cand probes|suite   (PHYSICAL COPY ONLY)
#  probes: variant + seams + copy-only counters, the discovery's red-first tests and my VerifyO78Probes, filtered run
#  suite : the exact variant (no seam, no added test), full Alundra.Tests run
set -e
V="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78/verify"
D="$(dirname "$V")"
VAR="$1"; MODE="$2"
T="$V/repo/Alundra.Tests"
if [ "$MODE" = probes ]; then
  python "$V/apply_mine.py" "$VAR" --seams --probe
  cp "$D/AlundraInventoryTriggerFrameOrderTests.cs.txt" "$T/AlundraInventoryTriggerFrameOrderTests.cs"
  cp "$V/VerifyO78Probes.cs.txt" "$T/VerifyO78Probes.cs"
else
  python "$V/apply_mine.py" "$VAR"
  rm -f "$T/AlundraInventoryTriggerFrameOrderTests.cs" "$T/VerifyO78Probes.cs"
fi
cd "$V/repo"
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug --disable-build-servers -nodeReuse:false -p:UseSharedCompilation=false -v:q -nologo > "$V/build-$VAR-$MODE.log" 2>&1 || { echo BUILD FAILED; grep -m8 "error" "$V/build-$VAR-$MODE.log"; exit 1; }
if [ "$MODE" = probes ]; then
  mkdir -p "$V/out/$VAR"
  VERIFY_OUT="$V/out/$VAR" dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --blame-hang-timeout 5m --filter "FullyQualifiedName~AlundraInventoryTriggerFrameOrderTests|FullyQualifiedName~VerifyO78Probes" --logger "trx;LogFileName=$VAR-probes.trx" --results-directory "$V/results" -nologo > "$V/test-$VAR-probes.log" 2>&1 || true
else
  dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --blame-hang-timeout 5m --logger "trx;LogFileName=$VAR-suite.trx" --results-directory "$V/results" -nologo > "$V/test-$VAR-suite.log" 2>&1 || true
fi
tail -1 "$V/test-$VAR-$MODE.log"
