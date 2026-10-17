#!/bin/bash
# usage: show.sh OLDPATH OLDFROM OLDTO NEWPATH NEWFROM NEWTO
cd /d/development/repo/alundra-casaengine-project-converter || exit 1
echo "=== OLD 2010ec3:$1 $2-$3"
git show "2010ec3:$1" | awk -v a="$2" -v b="$3" 'NR>=a && NR<=b {printf "%5d  %s\n", NR, $0}'
echo "=== HEAD:$4 $5-$6"
awk -v a="$5" -v b="$6" 'NR>=a && NR<=b {printf "%5d  %s\n", NR, $0}' "$4"
