# Own model of AudioWriter.WriteManifest (System.Text.Json, SnakeCaseLower, WriteIndented, CRLF) for bgm-manifest.json.
# Number tokens are copied verbatim from bgm.json (both sides are System.Text.Json), asset ids from today's manifest.
import hashlib, os, re, sys

S = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
REPO = r"D:\development\repo\alundra-casaengine-project-converter"
TODAY_JSON = os.path.join(REPO, 'data-extracted', 'sound', 'bgm.json')
ORA_JSON = os.path.join(S, 'loop-data', 'oracle', 'sound', 'bgm.json')
TODAY_MAN = os.path.join(REPO, 'alundra-project', 'Musics', 'bgm-manifest.json')
OUT = os.path.join(S, 'loop-data-verify')


def raw_entries(path):
    txt = open(path, 'rb').read().decode('utf-8')
    ents = []
    for block in re.findall(r'\{(.*?)\}', txt, re.S):
        kv = re.findall(r'"([^"]+)": ([^,\r\n]+)', block)
        ents.append(kv)
    return ents


def snake(name):
    # JsonNamingPolicy.SnakeCaseLower on PascalCase identifiers without digits/acronyms
    return re.sub(r'(?<!^)([A-Z])', r'_\1', name).lower()


ORDER = ['SoundIndex', 'File', 'Frames', 'DurationSeconds', 'LoopDetected', 'PeakLeft', 'PeakRight', 'RmsLeft', 'RmsRight', 'FirstAudibleFrame', 'AssetId', 'Looping', 'LoopStartFrame', 'LoopStartSample']


def build(entries, ids, with_new):
    out = ['[']
    blocks = []
    for kv in entries:
        d = dict(kv)
        idx = int(d['SoundIndex'])
        d['AssetId'] = '"%s"' % ids[idx]
        keys = ORDER if with_new else ORDER[:11]
        lines = []
        for k in keys:
            v = d.get(k, 'null')
            lines.append('    "%s": %s' % (snake(k), v))
        blocks.append('  {\r\n' + ',\r\n'.join(lines) + '\r\n  }')
    return ('[\r\n' + ',\r\n'.join(blocks) + '\r\n]').encode('utf-8')


def main():
    man = open(TODAY_MAN, 'rb').read().decode('utf-8')
    ids = {}
    for m in re.finditer(r'"sound_index": (\d+),.*?"asset_id": "([0-9a-f-]+)"', man, re.S):
        ids[int(m.group(1))] = m.group(2)
    today = raw_entries(TODAY_JSON)
    ora = raw_entries(ORA_JSON)
    t = build(today, ids, False)
    print('model(today) == today manifest:', t == open(TODAY_MAN, 'rb').read(), len(t))
    n = build(ora, ids, True)
    open(os.path.join(OUT, 'my-bgm-manifest.json'), 'wb').write(n)
    print('new manifest bytes', len(n), 'sha1', hashlib.sha1(n).hexdigest(), 'sha256', hashlib.sha256(n).hexdigest())
    pred = open(os.path.join(S, 'loop-data', 'predicted', 'bgm-manifest.json'), 'rb').read()
    print('equals predicted:', n == pred)
    old = build(today, ids, True)
    pred_old = open(os.path.join(S, 'loop-data', 'predicted', 'bgm-manifest.from-old-bgm-json.json'), 'rb').read()
    print('from-old equals predicted from-old:', old == pred_old, len(old))
    # field-level diff today vs oracle bgm.json
    td = {int(dict(kv)['SoundIndex']): dict(kv) for kv in today}
    od = {int(dict(kv)['SoundIndex']): dict(kv) for kv in ora}
    print('keys oracle', [k for k, _ in ora[0]])
    for k in ORDER[:10]:
        ch = [i for i in td if td[i][k] != od[i][k]]
        print('changed %-18s %2d %s' % (k, len(ch), ch if len(ch) < 20 else ''))
    lo = [i for i in od if od[i].get('Looping') == 'true']
    ln = [i for i in od if od[i].get('Looping') == 'false']
    print('Looping true', len(lo), 'false', ln)
    bad = [i for i in lo if int(od[i]['LoopStartSample']) != int(od[i]['LoopStartFrame']) * 735]
    print('LSS != LSF*735:', bad)
    print('one-shot nulls:', all(od[i]['LoopStartFrame'] == 'null' and od[i]['LoopStartSample'] == 'null' for i in ln))


if __name__ == '__main__':
    main()
