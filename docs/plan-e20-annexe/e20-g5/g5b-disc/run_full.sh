#!/bin/bash
# run_full.sh - baseline (HEAD runner) then prototype full test runs in the probe copy; results in full_head.trx/log and full_proto.trx/log
G="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g5b-disc"
cd "$G/probe"
cp "$G/EventProgramRunner.cs.head" Alundra/Events/EventProgramRunner.cs
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug --nologo -v q > "$G/build_head.log" 2>&1
timeout 2400 dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build -c Debug --blame-hang-timeout 300s --logger "trx;LogFileName=$G/full_head.trx" -v q > "$G/full_head.log" 2>&1
echo "exit $?" >> "$G/full_head.log"
cp "$G/EventProgramRunner.cs.proto" Alundra/Events/EventProgramRunner.cs
dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug --nologo -v q > "$G/build_proto.log" 2>&1
timeout 2400 dotnet test Alundra.Tests/Alundra.Tests.csproj --no-build -c Debug --blame-hang-timeout 300s --logger "trx;LogFileName=$G/full_proto.trx" -v q > "$G/full_proto.log" 2>&1
echo "exit $?" >> "$G/full_proto.log"
