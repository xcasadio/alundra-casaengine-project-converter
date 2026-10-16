#!/usr/bin/env bash
# run_variant.sh <BASE|PA|PAB|PANOAF|PABEARLY> <build|test|both>
set -u
V="$1"; STEP="${2:-both}"
REPO="D:/development/repo/alundra-casaengine-project-converter"
SP="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad"
OUT="$SP/e20e0a4-exec/measure"
TARGETS="$SP/e20e0a3b-exec/skipcopy.targets"
cd "$REPO" || exit 90
FILES="Alundra/Scripts/AlundraWorldProxy.cs Alundra/Scripts/AlundraEventProgramRunner.cs"
echo "== [$V/$STEP] HEAD $(git rev-parse HEAD)"
if [ "$STEP" != "test" ]; then
  if [ "$V" != "BASE" ]; then
    pre=$(git status --porcelain -- $FILES); if [ -n "$pre" ]; then echo "PRECONDITION FAILED $pre"; exit 91; fi
    python -B "$OUT/patch.py" "$V" || { git checkout -- $FILES; exit 92; }
    git diff -- $FILES > "$OUT/$V.patch.diff"
  fi
  echo "== build $(date +%T)"
  dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release -v q -nologo -p:CustomAfterMicrosoftCommonTargets="$TARGETS" > "$OUT/$V.build.log" 2>&1
  rc=$?; tail -3 "$OUT/$V.build.log"; echo "== build rc=$rc $(date +%T)"
  if [ $rc -ne 0 ]; then git checkout -- $FILES; exit 93; fi
fi
if [ "$STEP" != "build" ]; then
  echo "== test $(date +%T)"
  rm -f "$OUT/$V.trx"
  dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --blame-hang-timeout 300s --logger "trx;LogFileName=$OUT/$V.trx" > "$OUT/$V.test.log" 2>&1
  rc=$?; tail -6 "$OUT/$V.test.log"; echo "== test rc=$rc $(date +%T)"
  git checkout -- $FILES
  echo "== restored, status of touched files: '$(git status --porcelain -- $FILES)'"
  python -B "$OUT/trx2list.py" "$OUT/$V.trx" "$OUT/$V.failed.txt"; head -3 "$OUT/$V.failed.txt" | cut -c1-200
  echo "== md5 alundra-project/Alundra.dll $(md5sum alundra-project/Alundra.dll | cut -d' ' -f1)"
fi
