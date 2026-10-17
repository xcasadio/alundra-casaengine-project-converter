#!/bin/bash
# usage: ln.sh <oldpath> <newpath> <regex>   -> shows matches at 9ecc7d5d, 2010ec38 and HEAD
old="$1"; new="$2"; re="$3"
echo "== $re"
for rev in 9ecc7d5d 2010ec38; do
  echo "-- $rev:$old"; git show $rev:"$old" 2>/dev/null | grep -nE "$re" | head -${N:-8} | cut -c1-200
done
echo "-- HEAD:$new"; grep -nE "$re" "$new" | head -${N:-8} | cut -c1-200
