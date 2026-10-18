#!/bin/bash
# usage: lines.sh OLDREV oldpath newpath regex
cd /d/development/repo/alundra-casaengine-project-converter
oldrev=$1; oldp=$2; newp=$3; re=$4
echo "### $re"
echo "  old($oldrev:$oldp): $(git show $oldrev:$oldp 2>/dev/null | grep -n -E "$re" | head -3 | cut -c1-110 | tr '\n' '|')"
echo "  HEAD($newp): $(grep -n -E "$re" $newp 2>/dev/null | head -3 | cut -c1-110 | tr '\n' '|')"
