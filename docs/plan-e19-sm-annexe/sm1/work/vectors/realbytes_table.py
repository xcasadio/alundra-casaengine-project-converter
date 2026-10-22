"""Flattens real-bytes-scenarios.json into tests/real-bytes.tsv and marks each row red (the skipped opcode gives another end state) or green-in-advance (a guard).
Usage: python -B realbytes_table.py IN.json OUT.tsv"""
import json
import sys

R = json.load(open(sys.argv[1], encoding='utf-8'))
rows = []
nred = ngreen = 0
for s in R:
    exp = [(p['end_pc'], p['result'], p['logic']) for p in s['expected']]
    red = [(p['end_pc'], p['result'], p['logic']) for p in s['red']]
    verdict = 'RED today' if exp != red else 'green in advance (guard)'
    if exp != red:
        nred += 1
    else:
        ngreen += 1
    rows.append([s['id'], s['title'], s['world'], s['program'], str(s['entry']), str(s['start']), str(s['cut']), s['bytes_hex'], str(s['result0']), ','.join(map(str, s['flags_at_start'])) or '-',
                 str(s['calls']), ' '.join('%d/R%d/L%d' % e for e in exp), ' '.join('%d/R%d/L%d' % e for e in red), ','.join('0x%X' % x for x in s['expected'][-1]['flags']) or '-', verdict])
with open(sys.argv[2], 'w', encoding='utf-8', newline='\n') as f:
    f.write('id\ttitle\tworld\tprogram\tentry_P0\tstart_pc\tcut_pc\treal_bytes_from_start_to_cut\tResult_before\tflags_set\tcalls\texpected_per_call(end_pc/Result/logic_slot)\tred_today_per_call\tflags_set_at_the_end_of_the_expected_run\tverdict\n')
    for r in rows:
        f.write('\t'.join(r) + '\n')
print(len(rows), 'rows;', nred, 'red today;', ngreen, 'green in advance')
