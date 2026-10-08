#!/bin/sh
# usage: run_variant.sh VARIANT probe|suite   (physical copy only)
set -e
D="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78"
V="$1"; MODE="$2"
cp "$D/orig/AlundraWorldProxy.cs.orig" "$D/repo/Alundra/Scripts/AlundraWorldProxy.cs"
if [ "$MODE" = probe ]; then
  python "$D/apply_o78.py" "$V" --instr
  cp "$D/O78ProbeTests.cs.txt" "$D/repo/Alundra.Tests/O78ProbeTests.cs"
else
  if [ "$V" != base ]; then python "$D/apply_o78.py" "$V"; fi
  rm -f "$D/repo/Alundra.Tests/O78ProbeTests.cs"
fi
cd "$D/repo"
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug --disable-build-servers -nodeReuse:false -p:UseSharedCompilation=false -v:q -nologo > "$D/build-$V-$MODE.log" 2>&1 || { echo BUILD FAILED; grep -m5 "error" "$D/build-$V-$MODE.log"; exit 1; }
if [ "$MODE" = probe ]; then
  mkdir -p "$D/probe-out/$V"
  O78OUT="$D/probe-out/$V" dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --blame-hang-timeout 5m --filter "FullyQualifiedName~O78ProbeTests" -nologo > "$D/test-$V-probe.log" 2>&1 || true
  tail -2 "$D/test-$V-probe.log"
else
  dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --blame-hang-timeout 5m --logger "trx;LogFileName=$V.trx" --results-directory "$D/results-$V" -nologo > "$D/test-$V-suite.log" 2>&1 || true
  tail -2 "$D/test-$V-suite.log"
fi
