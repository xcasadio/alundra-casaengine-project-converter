# Read-only state check for L-b (2026-10-08).
# 1. oracle WAVs + bgm.json vs the versioned table docs/plan-e19-loop-annexe/data/oracle_sha256.tsv
# 2. today's data-extracted/sound/bgm, remaster copy and alundra-project/Musics vs the table's TodaySHA256
# 3. font3.png in the three places (sha256)
import csv, hashlib, os, sys

REPO = r'D:\development\repo\alundra-casaengine-project-converter'
REM = r'D:\development\repo\Alundra Remake\remaster-data-extracted'
ORACLE = r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\loop-data\oracle\sound'
TABLE = os.path.join(REPO, 'docs', 'plan-e19-loop-annexe', 'data', 'oracle_sha256.tsv')


def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for c in iter(lambda: f.read(1 << 22), b''):
            h.update(c)
    return h.hexdigest()


rows = list(csv.DictReader(open(TABLE, newline=''), delimiter='\t'))
bad = []
osum = 0
tsum = 0
for r in rows:
    f = r['File']
    o = os.path.join(ORACLE, 'bgm', f)
    so = sha(o)
    osum += os.path.getsize(o)
    if so != r['SHA256'] or os.path.getsize(o) != int(r['Bytes']):
        bad.append(('oracle', f))
    for label, base in (('data-extracted', os.path.join(REPO, 'data-extracted', 'sound', 'bgm')),
                        ('remaster', os.path.join(REM, 'sound', 'bgm')),
                        ('export', os.path.join(REPO, 'alundra-project', 'Musics'))):
        p = os.path.join(base, f)
        s = sha(p)
        if label == 'data-extracted':
            tsum += os.path.getsize(p)
        if s != r['TodaySHA256']:
            bad.append((label, f, s))
print('rows', len(rows), 'mismatches', bad)
print('oracle wav bytes', osum, 'today wav bytes', tsum)
print('oracle bgm.json', sha(os.path.join(ORACLE, 'bgm.json')), os.path.getsize(os.path.join(ORACLE, 'bgm.json')))
for label, p in (('data-extracted', os.path.join(REPO, 'data-extracted', 'sound', 'bgm.json')),
                 ('remaster', os.path.join(REM, 'sound', 'bgm.json'))):
    print(label, 'bgm.json', sha(p), os.path.getsize(p))
for label, p in (('data-extracted', os.path.join(REPO, 'data-extracted', 'ui', 'font3.png')),
                 ('remaster', os.path.join(REM, 'ui', 'font3.png')),
                 ('export', os.path.join(REPO, 'alundra-project', 'UI', 'Textures', 'font3.png'))):
    print(label, 'font3.png', sha(p), os.path.getsize(p))
p = os.path.join(REPO, 'alundra-project', 'Musics', 'bgm-manifest.json')
print('export bgm-manifest.json sha256', sha(p), os.path.getsize(p))
print('export bgm_044.wav exists', os.path.exists(os.path.join(REPO, 'alundra-project', 'Musics', 'bgm_044.wav')))
