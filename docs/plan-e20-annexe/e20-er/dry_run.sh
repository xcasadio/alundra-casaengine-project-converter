#!/bin/bash
# Dry run of the E20.eR re-extraction, entirely in the scratchpad (nothing is written in the repo,
# nothing in the remaster, nothing in data-extracted). Step 1: committed HEAD of the analyser exported
# with git archive (read-only). Step 2: apply the rename in the COPY only. Step 3: build Release.
# Step 4: extract into a scratch folder. Step 5: compare with data-extracted/.
set -u
S="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/prep-eR"
SRC="D:/development/repo/alundra-casaengine-project-converter/alundra-datas-analyser"
GAME="D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted"
OUT="$S/extract-new"
LOG="$S/dry-run.log"
: > "$LOG"
say() { echo "[$(date +%H:%M:%S)] $*" | tee -a "$LOG"; }

say "step 1: git archive HEAD"
rm -rf "$S/dry"
mkdir -p "$S/dry/src"
git -C "$SRC" archive HEAD AlundraTools Directory.Build.props | tar -x -C "$S/dry/src"
say "archived: $(ls "$S/dry/src")"

say "step 2: rename in the scratch copy"
F="$S/dry/src/AlundraTools/AlundraDataExtractor/TiledMapExporter.cs"
grep -n 'BalanceLevel' "$F" | tee -a "$LOG"
sed -i 's/TiledProperty.Int("BalanceLevel", gameMap.Info.BalanceLevel)/TiledProperty.Int("TileCutEffectId", gameMap.Info.BalanceLevel)/' "$F"
grep -n 'TileCutEffectId' "$F" | tee -a "$LOG"

say "step 3: build Release"
dotnet build "$S/dry/src/AlundraTools/AlundraDataExtractor/AlundraDataExtractor.csproj" -c Release -nologo -v q >> "$LOG" 2>&1
say "build exit: $?"

say "step 4: extract"
rm -rf "$OUT"
mkdir -p "$OUT"
cd "$S/dry/src/AlundraTools/AlundraTools" || exit 1
START=$(date +%s)
dotnet ../AlundraDataExtractor/bin/Release/net9.0-windows/AlundraDataExtractor.dll "$GAME" "$OUT" > "$S/extract-stdout.log" 2>&1
say "extract exit: $? after $(( $(date +%s) - START )) s"
say "files: $(find "$OUT" -type f | wc -l)"

say "step 5: diff -rq against data-extracted"
cd "D:/development/repo/alundra-casaengine-project-converter" || exit 1
diff -rq "$OUT" data-extracted > "$S/diff-new-vs-dataextracted.txt" 2>&1
say "diff lines: $(wc -l < "$S/diff-new-vs-dataextracted.txt")"
say "done"
