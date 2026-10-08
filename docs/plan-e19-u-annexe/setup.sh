#!/bin/sh
# Physical copy of the repository for O-E19-78 (read-only use of the repository: git archive + tar copy of the export).
set -e
SRC="D:/development/repo/alundra-casaengine-project-converter"
DST="C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78/repo"
mkdir -p "$DST"
git -C "$SRC" archive --format=tar HEAD Alundra Alundra.Tests docs alundra-project | tar -C "$DST" -xf -
mkdir -p "$DST/CasaEngineMonogame"
git -C "$SRC/CasaEngineMonogame" archive --format=tar HEAD | tar -C "$DST/CasaEngineMonogame" -xf -
mkdir -p "$DST/CasaEngineMonogame/MGUI" "$DST/CasaEngineMonogame/NvgSharp"
git -C "$SRC/CasaEngineMonogame/MGUI" archive --format=tar HEAD | tar -C "$DST/CasaEngineMonogame/MGUI" -xf -
git -C "$SRC/CasaEngineMonogame/NvgSharp" archive --format=tar HEAD | tar -C "$DST/CasaEngineMonogame/NvgSharp" -xf -
# export data (untracked part included), without the music and the deployed DLL
tar -C "$SRC/alundra-project" --exclude=./Musics --exclude=./Alundra.dll --exclude=./Alundra.pdb -cf - . | tar -C "$DST/alundra-project" -xf -
echo "parent HEAD $(git -C "$SRC" rev-parse HEAD)"
echo "engine HEAD $(git -C "$SRC/CasaEngineMonogame" rev-parse HEAD)"
echo "MGUI HEAD $(git -C "$SRC/CasaEngineMonogame/MGUI" rev-parse HEAD)"
echo "NvgSharp HEAD $(git -C "$SRC/CasaEngineMonogame/NvgSharp" rev-parse HEAD)"
