"""Replays the whole sm2 preparation from this folder (read-only on the repository and on the EXE; writes only under ../evidence, ../vectors, ../proposed).
python -B run_all.py            -> prints a summary, exits non-zero if two vector runs differ or a cross-check mismatches."""
import filecmp, os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, '..'))
PY = [sys.executable, '-B']


def run(script, *args, out=None):
    cmd = PY + [os.path.join(HERE, script)] + list(args)
    r = subprocess.run(cmd, capture_output=True, text=True, cwd=HERE)
    if r.returncode != 0:
        print('FAILED:', ' '.join(cmd)); print(r.stdout); print(r.stderr); sys.exit(1)
    if out:
        with open(os.path.join(OUT, out), 'w', encoding='utf-8', newline='\n') as f:
            f.write(r.stdout)
    return r.stdout


v1 = os.path.join(OUT, 'vectors', 'vectors-sm2.tsv')
v2 = os.path.join(OUT, 'vectors', 'vectors-sm2.run2.tsv')
print(run('vectors_sm2.py', v1).splitlines()[0])
print(run('vectors_sm2.py', v2).splitlines()[0])
same = filecmp.cmp(v1, v2, shallow=False)
os.remove(v2)
print('two runs identical:', same)
if not same:
    sys.exit(1)
run('mutants_vectors.py', out='evidence/mutants_vectors.txt')
run('mutants_rows.py', out='evidence/mutants_rows.txt')
run('killmatrix.py', out='evidence/killmatrix.txt')
run('handvalues.py', out='evidence/handvalues.txt')
run('scenarios.py', out='evidence/scenarios.txt')
run('realbytes_table.py', out='proposed/RealBytesExpected.txt')
run('memo_static.py', out='evidence/memo_static.txt')
run('memo_table.py', out='evidence/memo_reads_table.tsv')
run('memo_fabricated.py', out='evidence/memo_fabricated.txt')
run('listprog.py', out='evidence/programs_decoded.txt')
run('records.py', out='evidence/records_sm2.txt')
run('closure.py', out='evidence/entity_closure.txt')
run('reach_maps.py', out='evidence/reach_maps.txt')
run('remaps.py', out='evidence/remaps.txt')
run('corpus_counts.py', out='evidence/corpus_counts.txt')
run('export_hashes.py', out='evidence/export_hashes.txt')
print(run('crosscheck_annex.py', '1000', '11'))
print('done')
