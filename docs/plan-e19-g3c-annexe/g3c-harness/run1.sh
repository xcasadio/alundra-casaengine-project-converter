#!/bin/bash
# usage: run1.sh scene k
H=$(cd "$(dirname "$0")" && pwd)
R=/d/development/repo/alundra-casaengine-project-converter
export G3C_SCENE=$1 G3C_K=$2 G3C_OUT=$(cygpath -w $H/out)
export G3C_PROJECT=$(cygpath -w $R/alundra-project/AlundraGame.json)
export G3C_EXPECTED=$(cygpath -w $R/docs/plan-e19-g3c-annexe/g3c_expected.json)
cd $H/harness/bin/Debug/net9.0-windows
timeout 120 ./G3cHarness.exe
echo "exit=$? scene=$1 k=$2"
