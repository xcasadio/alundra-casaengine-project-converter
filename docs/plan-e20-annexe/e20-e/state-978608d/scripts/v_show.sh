#!/bin/sh
# usage: v_show.sh OLDPATH A B NEWPATH C D
cd D:/development/repo/alundra-casaengine-project-converter || exit 1
echo "=== OLD 9502f75:$1 [$2-$3]"
git show "9502f75:$1" | awk -v a="$2" -v b="$3" 'NR>=a && NR<=b {printf "%5d| %s\n", NR, $0}'
echo "=== NEW HEAD:$4 [$5-$6]"
git show "HEAD:$4" | awk -v a="$5" -v b="$6" 'NR>=a && NR<=b {printf "%5d| %s\n", NR, $0}'
