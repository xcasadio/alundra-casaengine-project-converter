#!/bin/bash
# usage: runpart.sh <outdir> <label> <p1|p2|p3> [Release|Debug]   (no rebuild: --no-build; Release carries the neutralised copy)
REPO=D:/development/repo/alundra-casaengine-project-converter
SK=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets
OUT="$1"; NAME=$2; MODE=$3; CONF=${4:-Release}
mkdir -p "$OUT"
case $MODE in
p1) F="FullyQualifiedName~Alundra.Tests.Arcs.|FullyQualifiedName~Alundra.Tests.Ai.|FullyQualifiedName~Alundra.Tests.Audio.|FullyQualifiedName~Alundra.Tests.Cameras.|FullyQualifiedName~Alundra.Tests.Cells.|FullyQualifiedName~Alundra.Tests.Combat.|FullyQualifiedName~Alundra.Tests.Dialogues.";;
p2) F="FullyQualifiedName~Alundra.Tests.Entities.|FullyQualifiedName~Alundra.Tests.Events.|FullyQualifiedName~Alundra.Tests.ExportGuards.|FullyQualifiedName~Alundra.Tests.Hero.|FullyQualifiedName~Alundra.Tests.Items.";;
p3) F=""; for ns in Arcs Ai Audio Cameras Cells Combat Dialogues Entities Events ExportGuards Hero Items; do F="$F&FullyQualifiedName!~Alundra.Tests.$ns."; done; F=${F#&};;
esac
cd "$REPO"
if [ "$CONF" = Release ]; then
  dotnet test Alundra.Tests -c Release --no-build -p:CustomAfterMicrosoftCommonTargets=$SK --filter "$F" --blame-hang-timeout 60s > "$OUT/$NAME.$MODE.full" 2>&1
else
  dotnet test Alundra.Tests -c Debug --no-build --filter "$F" --blame-hang-timeout 60s > "$OUT/$NAME.$MODE.full" 2>&1
fi
grep -E "^  (Échoué|Failed) " "$OUT/$NAME.$MODE.full" | sed 's/ \[[0-9 a-z]*\]$//' | sort > "$OUT/$NAME.$MODE.red"
tail -4 "$OUT/$NAME.$MODE.full" > "$OUT/$NAME.$MODE.summary"
echo "$NAME $MODE: $(wc -l < "$OUT/$NAME.$MODE.red") red"; cat "$OUT/$NAME.$MODE.summary"
