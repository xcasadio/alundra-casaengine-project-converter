#!/bin/bash
# usage: run.sh <mutant|base> <outdir> [narrow|p1|p2|p3]
# applies the mutant (or none for "base"), builds Release with the neutralised copy, runs the filter, undoes the mutation (git checkout)
REPO=D:/development/repo/alundra-casaengine-project-converter
HERE="$REPO/docs/plan-e20-annexe/e20-g2c1/t4"
SK=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets
CFG=${CFG:-"-c Release -p:CustomAfterMicrosoftCommonTargets=$SK"}
NAME=$1; OUT="$2"; MODE=${3:-narrow}
mkdir -p "$OUT"
case $MODE in
narrow) F="FullyQualifiedName~Alundra.Tests.Ai.Objects|FullyQualifiedName~Alundra.Tests.Hero.Lift|FullyQualifiedName~Alundra.Tests.Arcs.Lift|FullyQualifiedName~Alundra.Tests.Arcs.BarrelLift|FullyQualifiedName~TestSavesTests|FullyQualifiedName~NativeSpriteRegistryTests";;
c4) F="FullyQualifiedName~Alundra.Tests.Physics.CornerTileWordsTests|FullyQualifiedName~Alundra.Tests.Ai.Objects.C4|FullyQualifiedName~Alundra.Tests.Ai.Objects.LiftableC4|FullyQualifiedName~Alundra.Tests.Ai.Objects.LiftableShared|FullyQualifiedName~Alundra.Tests.Arcs.BarrelLift|FullyQualifiedName~NativeSpriteRegistryTests|FullyQualifiedName~LiftPureTests";;
p1) F="FullyQualifiedName~Alundra.Tests.Arcs.|FullyQualifiedName~Alundra.Tests.Ai.|FullyQualifiedName~Alundra.Tests.Audio.|FullyQualifiedName~Alundra.Tests.Cameras.|FullyQualifiedName~Alundra.Tests.Cells.|FullyQualifiedName~Alundra.Tests.Combat.|FullyQualifiedName~Alundra.Tests.Dialogues.";;
p2) F="FullyQualifiedName~Alundra.Tests.Entities.|FullyQualifiedName~Alundra.Tests.Events.|FullyQualifiedName~Alundra.Tests.ExportGuards.|FullyQualifiedName~Alundra.Tests.Hero.|FullyQualifiedName~Alundra.Tests.Items.";;
p3) F=""; for ns in Arcs Ai Audio Cameras Cells Combat Dialogues Entities Events ExportGuards Hero Items; do F="$F&FullyQualifiedName!~Alundra.Tests.$ns."; done; F=${F#&};;
esac
cd "$REPO"
if [ "$NAME" != base ]; then python -B "$HERE/mut.py" "$NAME" > "$OUT/$NAME.$MODE.apply" 2>&1 || { echo "APPLY FAILED"; cat "$OUT/$NAME.$MODE.apply"; git checkout -- Alundra/ ; exit 3; }; fi
dotnet test Alundra.Tests $CFG --filter "$F" --blame-hang-timeout 60s > "$OUT/$NAME.$MODE.full" 2>&1
grep -E "^  (Échoué|Failed) " "$OUT/$NAME.$MODE.full" | sed 's/ \[[0-9 a-z]*\]$//' | sort > "$OUT/$NAME.$MODE.red"
tail -3 "$OUT/$NAME.$MODE.full" > "$OUT/$NAME.$MODE.summary"
git checkout -- Alundra/
echo "$NAME $MODE: $(wc -l < "$OUT/$NAME.$MODE.red") red"; cat "$OUT/$NAME.$MODE.summary"
