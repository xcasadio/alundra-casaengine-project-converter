"""chk_dll.py - checker's own transcription of the DLL logic (AlundraEffectStage.RunFrame + AlundraEffectReservoir.CreateCore/Tick/Animate, read from the C# in this checkout; the C# was NOT run)
compared tick by tick with the binary (chk_bin.run).  Written independently of the probe's dll_model.py.
"""
import sys
import json
sys.dont_write_bytecode = True
HERE = r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/o-f3-6/checker"
sys.path.insert(0, HERE)
import chk_bin as B     # noqa: E402

G = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/effects/effects-global.json", encoding='utf-8-sig'))['Effects']


class Slot:
    def __init__(self):
        self.active = False


class Reservoir:
    def __init__(self):
        self.slots = [Slot() for _ in range(128)]

    def create_attached(self, effect, anim=0):
        for s in self.slots:
            if not s.active:
                s.active = True
                s.t_eff = effect & 0xFF
                s.t_anim = anim & 0xFF
                s.c_eff = ~s.t_eff & 0xFF
                s.c_anim = ~s.t_anim & 0xFF
                s.destroying = False
                s.fi = 0
                s.counter = 0
                s.first_update_tick = 0
                return s
        return None

    def tick(self, frozen, tick):
        if frozen:
            return
        for s in self.slots:
            if not s.active or s.first_update_tick > tick:
                continue
            if s.destroying:
                s.active = False
                continue
            self.animate(s)

    def animate(self, s):
        if s.t_eff != s.c_eff:
            s.table = G[s.t_eff]
            s.c_eff = s.t_eff
            s.c_anim = ~s.t_anim & 0xFF
        if s.t_anim != s.c_anim:
            s.anim = s.table['Animations'][s.t_anim]
            s.c_anim = s.t_anim
            s.fi = 0
            s.counter = 0
            s.destroying = False
        else:
            s.counter = (s.counter - 1) & 0xFF
            if s.counter != 0:
                return
        frames = s.anim['Frames']
        if s.fi < len(frames):
            fr = frames[s.fi]
            s.fi += 1
            s.counter = fr[0] & 0xFF
            return
        assert s.anim['End'] == 'Destroy'
        s.counter = 0xFF
        s.destroying = True


def run_dll(effect, T, N, ticks_per_frame=1):
    """RunFrame: for each tick k of a frame: apply the queued ops marked <= k, then reservoir.Tick(frozen=False, k).  The hit pass queues (Emit) its op at tick T (rank 1)."""
    r = Reservoir()
    rows = []
    created = None
    tick_no = 0
    while tick_no < T + N:
        frame_ticks = ticks_per_frame
        # queue of this frame: the op emitted by the entity block of logic tick T (1-based global tick number)
        pending = []
        for k in range(frame_ticks):
            if tick_no + k + 1 == T:
                pending.append((k, 'hit'))
        nxt = 0
        for k in range(frame_ticks):
            while nxt < len(pending) and pending[nxt][0] <= k:
                created = r.create_attached(effect)
                nxt += 1
            r.tick(False, k)
            rows.append(dict(t=tick_no + k + 1, active=created.active if created else False,
                             counter=created.counter if created else None, destroying=created.destroying if created else None, fi=(created.fi - 1) if created else None))
        while nxt < len(pending):
            created = r.create_attached(effect)
            nxt += 1
        tick_no += frame_ticks
    return rows


if __name__ == '__main__':
    bad = 0
    for who in ('hero', 'bean'):
        for nib, eff in ((6, 4), (10, 4), (7, 5), (9, 5)):
            T = 8
            brow, tracked, born, sparks = B.run(who, nib, T, 110)
            drow = run_dll(eff, T, 110)
            nd = 0
            raw = open(B.DATAS, 'rb').read(0x9000)
            for b, d in zip(brow, drow):
                if b['t'] < T:
                    assert 's' not in b and not d['active']
                    continue
                s = b['s']
                active_b = s['status'] == 2
                ordn = None
                if s['bptr']:
                    off = s['bptr'] - B.BANK_RAM
                    first_hdr = s['bptr'] + (raw[off] | (raw[off + 1] << 8))
                    ordn = (s['sptr'] - first_hdr) // 3 - 1
                ok = (active_b == d['active'])
                if active_b:
                    ok = ok and s['cnt'] == d['counter'] and (s['done'] == 1) == d['destroying'] and (s['done'] == 1 or ordn == d['fi'])
                if not ok:
                    nd += 1
                    print('DIFF', who, nib, 'tick', b['t'], 'd=', b['d'], 'binary', (s['status'], s['cnt'], s['done'], ordn), 'dll', d)
            bad += nd
            print('%s hits, nib=%d (effect %d), T=%d: %d tick differences (active, counter, destroy flag) over %d ticks' % (who, nib, eff, T, nd, len(brow)))
    print('TOTAL DIFFS', bad)
    # grouping 2 and 3 (DLL only): first absent tick (end-of-frame samples) relative to the hit tick, to compare with the (w1)/(w3) convention
    for g in (1, 2, 3):
        for eff, S in ((4, 40), (5, 95), (9, 25)):
            T = 7
            rows = run_dll(eff, T, 130, g)
            frames_end = [r for r in rows if r['t'] % g == 0]
            seen = [r['t'] for r in frames_end if r['active']]
            absent = [r['t'] for r in frames_end if r['t'] > seen[0] and not r['active']]
            print('DLL grouping %d effect %d (S=%d): first seen end-of-frame tick %d, first absent %d (hit tick %d, d=%d)' % (g, eff, S, seen[0], absent[0], T, absent[0] - T))
