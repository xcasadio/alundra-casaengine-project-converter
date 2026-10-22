"""Critic-own: recount of the sm0 list (docs/plan-e19-sm-annexe/skipped-sites.tsv, read-only) before / after the removal of the sm3 opcodes,
and comparison of the 42 sm3 keys with the preparation's corpus-lines-sm3.tsv. Scenario B: B2 stays (Q-sm-4 variant)."""
import csv
import collections

ROOT = 'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv'
PREP = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3/corpus-lines-sm3.tsv'
rows = list(csv.DictReader(open(ROOT, encoding='utf-8'), delimiter='\t'))
S3 = {'A9', 'AA', 'B2', 'B4', 'B5', 'B6', 'B7', '9E'}


def stats(rs):
    return dict(lines=len(rs), instructions=len({(r['map'], r['pc']) for r in rs}), maps=len({r['map'] for r in rs}), opcodes=len({r['opcode'] for r in rs}))


sm3 = [r for r in rows if r['opcode'].upper() in S3]
rest = [r for r in rows if r['opcode'].upper() not in S3]
print('before', stats(rows))
print('sm3   ', stats(sm3), dict(collections.Counter(r['opcode'] for r in sm3)))
print('after ', stats(rest))
varB = [r for r in rows if r['opcode'].upper() in S3 - {'B2'}]
restB = [r for r in rows if r['opcode'].upper() not in S3 - {'B2'}]
print('variant B2-stays: removed', stats(varB), 'after', stats(restB))
mine = list(csv.DictReader(open(PREP, encoding='utf-8'), delimiter='\t'))
key = lambda r: (r['map'], r['slot'], r['program'], r['pc'], r['opcode'].upper(), r['operands'].upper())
print('prep keys == census keys:', {key(r) for r in mine} == {key(r) for r in sm3}, len(mine))
print('maps vanishing:', sorted({r['map'] for r in sm3} - {r['map'] for r in rest}, key=int))
print('maps keeping other-owner lines:', sorted({r['map'] for r in sm3} & {r['map'] for r in rest}, key=int))
