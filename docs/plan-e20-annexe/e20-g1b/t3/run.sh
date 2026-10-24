#!/bin/bash
OUT="$(pwd)/$2"
# usage: run.sh <partition 1|2|3> <outfile> [build]
cd /d/development/repo/alundra-casaengine-project-converter
SK=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets
case $1 in
1) F="FullyQualifiedName~Alundra.Tests.Arcs.|FullyQualifiedName~Alundra.Tests.Ai.|FullyQualifiedName~Alundra.Tests.Audio.|FullyQualifiedName~Alundra.Tests.Cameras.|FullyQualifiedName~Alundra.Tests.Cells.|FullyQualifiedName~Alundra.Tests.Combat.|FullyQualifiedName~Alundra.Tests.Dialogues.";;
2) F="FullyQualifiedName~Alundra.Tests.Entities.|FullyQualifiedName~Alundra.Tests.Events.|FullyQualifiedName~Alundra.Tests.ExportGuards.|FullyQualifiedName~Alundra.Tests.Hero.|FullyQualifiedName~Alundra.Tests.Items.";;
3) F=""; for ns in Arcs Ai Audio Cameras Cells Combat Dialogues Entities Events ExportGuards Hero Items; do F="$F&FullyQualifiedName!~Alundra.Tests.$ns."; done; F=${F#&};;
esac
B=""; [ "$3" != build ] && B="--no-build"
dotnet test Alundra.Tests -c Release -p:CustomAfterMicrosoftCommonTargets=$SK $B --filter "$F" --blame-hang-timeout 60s > "${OUT}.full" 2>&1
grep -E "^  (Échoué|Failed) " "${OUT}.full" | sed 's/ \[[0-9 a-z]*\]$//' | sort > "$OUT"
tail -1 "${OUT}.full" >> "$OUT.summary"
