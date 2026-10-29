#!/bin/bash
# usage: part.sh <1..7> <Release|Debug>
S=/c/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad
SK=C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets
N=Alundra.Tests
case $1 in
  1) F="FullyQualifiedName~$N.Ai.Objects.";;
  2) F="FullyQualifiedName~$N.Ai.Projectiles.";;
  3) F="FullyQualifiedName~$N.Ai.&FullyQualifiedName!~$N.Ai.Objects.&FullyQualifiedName!~$N.Ai.Projectiles.";;
  4) F="FullyQualifiedName~$N.Hero.Hero";;
  5) F="FullyQualifiedName~$N.Hero.&FullyQualifiedName!~$N.Hero.Hero";;
  6) F="FullyQualifiedName~$N.Arcs.|FullyQualifiedName~$N.Audio.|FullyQualifiedName~$N.Cameras.|FullyQualifiedName~$N.Cells.|FullyQualifiedName~$N.Combat.|FullyQualifiedName~$N.Dialogues.|FullyQualifiedName~$N.Entities.|FullyQualifiedName~$N.Events.";;
  7) F="FullyQualifiedName!~$N.Ai.&FullyQualifiedName!~$N.Hero.&FullyQualifiedName!~$N.Arcs.&FullyQualifiedName!~$N.Audio.&FullyQualifiedName!~$N.Cameras.&FullyQualifiedName!~$N.Cells.&FullyQualifiedName!~$N.Combat.&FullyQualifiedName!~$N.Dialogues.&FullyQualifiedName!~$N.Entities.&FullyQualifiedName!~$N.Events.";;
esac
cd /d/development/repo/alundra-casaengine-project-converter/Alundra.Tests
if [ "$2" = "Release" ]; then
  EXTRA="-p:CustomAfterMicrosoftCommonTargets=$SK"
else
  EXTRA=""
fi
dotnet test -c $2 --no-build $EXTRA --filter "$F" --blame-hang-timeout 60s > $S/h4d2-t1/suite/part$1.$2.txt 2>&1
tail -1 $S/h4d2-t1/suite/part$1.$2.txt
