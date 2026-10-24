"""vab_adsr.py - read the global SFX VAB header of SOUND.BIN (the first 'pBAV'), print for the sounds 25, 85, 98, 229 (and the pillar's 25) the tone attributes (prior, mode, vol, pan, ADSR1, ADSR2) and
the release (RR = ADSR2 & 0x1F, bit 5 = exponential): the release time of a key-off. Read-only. The tone number of the sfx record indexes the tones of the program.
Usage: python -B -X utf8 vab_adsr.py"""
import struct, json, sys
P = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\DATA\SOUND.BIN"
b = open(P, 'rb').read()
offs = []
i = b.find(b'pBAV')
while i >= 0:
    offs.append(i); i = b.find(b'pBAV', i + 4)
print('pBAV sections:', len(offs), offs[:6])
def vab(off):
    h = b[off:off + 32]
    magic, ver, vid, fsize, rsv, ps, ts, vs, mvol, pan, a1, a2 = struct.unpack_from('<4sIIIHHHHBBBB', h, 0)
    progs = []
    for p in range(128):
        t, mv, prior, mode, mpan, r0, attr, r1, r2 = struct.unpack_from('<BBBBBBHII', b, off + 32 + 16 * p)
        progs.append(dict(tones=t, mvol=mv, prior=prior, mode=mode, mpan=mpan))
    base = off + 32 + 128 * 16
    tones = {}
    k = 0
    for p in range(128):
        if progs[p]['tones'] > 0:
            for t in range(16):
                a = base + 32 * (k * 16 + t)
                (prior, mode, vol, pan, center, shift, mn, mx, vw, vt, pw, pt, pbn, pbx, r1_, r2_, ad1, ad2, prog, vag) = struct.unpack_from('<16BHHhh', b, a)
                tones[(p, t)] = dict(prior=prior, mode=mode, vol=vol, pan=pan, center=center, shift=shift, min=mn, max=mx, adsr1=ad1, adsr2=ad2, prog=prog, vag=vag)
            k += 1
    return dict(id=vid, ps=ps, ts=ts, vs=vs, progs=progs, tones=tones)
v = vab(offs[0])
print('first VAB: id %d programs %d tones %d vags %d' % (v['id'], v['ps'], v['ts'], v['vs']))
m = {r['id']: r for r in json.load(open(r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Sounds\sfx-manifest.json", encoding='utf-8'))}
def rel(ad2):
    rr = ad2 & 0x1F; exp = (ad2 >> 5) & 1
    n = 2 ** (rr + 1)             # samples at 44100 Hz for the full-scale linear release, or the time constant of the exponential one
    return rr, exp, n / 44100.0
for sid in (25, 85, 2, 55, 45, 46, 60, 316):
    r = m[sid]
    if r['vab_id'] != -1:
        print('sfx %d: vab %d (not the global VAB)' % (sid, r['vab_id'])); continue
    t0 = r['tone_number']
    for k in range(r['num_tones']):
        t = v['tones'].get((r['program_number'], t0 + k))
        if t is None: print('sfx', sid, 'tone missing'); continue
        rr, ex, sec = rel(t['adsr2'])
        print('sfx %3d prog %d tone %2d | manifest vol %s pan %s | VAB vol %3d pan %3d | ADSR1 %04X ADSR2 %04X | release rate %2d %s -> %.3f s (%s)' % (
            sid, r['program_number'], t0 + k, r['tones'][k]['volume'], r['tones'][k]['pan'], t['vol'], t['pan'], t['adsr1'], t['adsr2'], rr, 'exp' if ex else 'lin', sec,
            'time constant' if ex else 'full-scale linear fall'))
