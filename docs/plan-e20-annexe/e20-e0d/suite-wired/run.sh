#!/bin/bash
# usage: e0d-run.sh <outdir> <p1|p2|p3> [config Release|Debug]
REPO=D:/development/repo/alundra-casaengine-project-converter
SK=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets
OUT="$1"; MODE=$2; CONFIGNAME=${3:-Release}
if [ "$CONFIGNAME" = Release ]; then CFG="-c Release -p:CustomAfterMicrosoftCommonTargets=$SK"; else CFG="-c Debug"; fi
mkdir -p "$OUT"
case $MODE in
p1) F="FullyQualifiedName~Alundra.Tests.Arcs.|FullyQualifiedName~Alundra.Tests.Ai.|FullyQualifiedName~Alundra.Tests.Audio.|FullyQualifiedName~Alundra.Tests.Cameras.|FullyQualifiedName~Alundra.Tests.Cells.|FullyQualifiedName~Alundra.Tests.Combat.|FullyQualifiedName~Alundra.Tests.Dialogues.";;
p2) F="FullyQualifiedName~Alundra.Tests.Entities.|FullyQualifiedName~Alundra.Tests.Events.|FullyQualifiedName~Alundra.Tests.ExportGuards.|FullyQualifiedName~Alundra.Tests.Hero.|FullyQualifiedName~Alundra.Tests.Items.";;
p3) F=""; for ns in Arcs Ai Audio Cameras Cells Combat Dialogues Entities Events ExportGuards Hero Items ScratchE0d; do F="$F&FullyQualifiedName!~Alundra.Tests.$ns."; done; F=${F#&};;
esac
cd "$REPO"
dotnet test Alundra.Tests $CFG --filter "$F" --blame-hang-timeout 60s > "$OUT/$MODE.full" 2>&1
grep -E "^  (Échoué|Failed) " "$OUT/$MODE.full" | sed 's/ \[[0-9 a-z]*\]$//' | sort > "$OUT/$MODE.red"
tail -3 "$OUT/$MODE.full" > "$OUT/$MODE.summary"
echo "$MODE: $(wc -l < "$OUT/$MODE.red") red"; cat "$OUT/$MODE.summary"
