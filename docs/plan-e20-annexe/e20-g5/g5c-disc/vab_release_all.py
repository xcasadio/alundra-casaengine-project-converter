"""vab_release_all.py - for every sound id stopped by a 0xBE site of the exported maps (sites_stop.json), the release rate of its tone(s) in the VAB of its bank (SOUND.BIN, 'pBAV' sections; the section of a vab_id found by matching the
tone volume/pan of the manifest) : ADSR2 & 0x1F (release shift), bit 5 (exponential), and the time of a full-scale linear fall = 2^(RR+1) samples at 44100 Hz (for the exponential mode it is the time constant). Read-only.
Usage: python -B -X utf8 vab_release_all.py"""
import struct, json, collections
exec(open('vab_adsr.py', encoding='utf-8').read().split("v = vab(offs[0])")[0].replace("print('pBAV sections:', len(offs), offs[:6])", ""))
m = {r['id']: r for r in json.load(open(r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Sounds\sfx-manifest.json", encoding='utf-8'))}
sites = json.load(open('sites_stop.json'))
stopped = collections.Counter(x['id'] for x in sites if x['op'] == 0xBE)
vabs = [vab(o) for o in offs]
def section_for(vabid, rec):
    if vabid == -1: return 0
    # candidates: the sections whose tones match (volume, pan) of the record tones
    cands = []
    for si, v in enumerate(vabs):
        ok = True
        for k, t in enumerate(rec['tones']):
            tt = v['tones'].get((rec['program_number'], rec['tone_number'] + k))
            if tt is None or tt['vol'] != t['volume'] or tt['pan'] != t['pan']: ok = False; break
        if ok: cands.append(si)
    return cands
rows = []
for sid, n in sorted(stopped.items(), key=lambda kv: -kv[1]):
    r = m.get(sid)
    if r is None or not r['tones']:
        rows.append((sid, n, 'no tones / unknown id', None)); continue
    sec = section_for(r['vab_id'], r)
    if r['vab_id'] == -1:
        secs = [0]
    else:
        secs = [r['vab_id'] + 1]       # the rule seen on every record whose (volume, pan) match is unique: the section of vab_id k is the k+1-th 'pBAV' (the first one is the global bank)
        if secs[0] not in sec: secs = ['MISMATCH(vol/pan differ at section %d)' % secs[0]]
    rels = set()
    for si in secs[:6]:
        if isinstance(si, str): continue
        v = vabs[si]
        for k in range(r['num_tones']):
            t = v['tones'].get((r['program_number'], r['tone_number'] + k))
            if t: rels.add(((t['adsr2'] & 0x1F), (t['adsr2'] >> 5) & 1))
    rows.append((sid, n, 'vab %d repeat=%s secs=%s' % (r['vab_id'], [t['repeat'] for t in r['tones']], secs[:6]), sorted(rels)))
for sid, n, d, rels in rows:
    if rels:
        txt = '; '.join('RR %d %s -> %.3f s' % (rr, 'exp' if ex else 'lin', 2 ** (rr + 1) / 44100.0) for rr, ex in rels)
    else:
        txt = '-'
    print('id %3d  sites %3d  %-50s  release: %s' % (sid, n, d, txt))
