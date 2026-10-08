#!/bin/sh
set -e
D="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78"
V="$1"
cp "$D/orig/AlundraWorldProxy.cs.orig" "$D/repo/Alundra/Scripts/AlundraWorldProxy.cs"
python "$D/apply_o78.py" "$V" --seams
rm -f "$D/repo/Alundra.Tests/O78ProbeTests.cs"
cp "$D/AlundraInventoryTriggerFrameOrderTests.cs.txt" "$D/repo/Alundra.Tests/AlundraInventoryTriggerFrameOrderTests.cs"
cd "$D/repo"
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug --disable-build-servers -nodeReuse:false -p:UseSharedCompilation=false -v:q -nologo > "$D/build-$V-redfirst.log" 2>&1 || { echo BUILD FAILED; grep -m5 "error" "$D/build-$V-redfirst.log"; exit 1; }
dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build --blame-hang-timeout 5m --filter "FullyQualifiedName~AlundraInventoryTriggerFrameOrderTests" --logger "trx;LogFileName=redfirst-$V.trx" --results-directory "$D/results-redfirst-$V" -nologo > "$D/test-$V-redfirst.log" 2>&1 || true
tail -1 "$D/test-$V-redfirst.log"
python "$D/trx.py" "$D/results-redfirst-$V/redfirst-$V.trx"
