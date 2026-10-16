"""dll_model.py - O-F3-6: a Python TRANSCRIPTION of the DLL's effect reservoir + stage ordering, fed by the exported alundra-project/Data/effects/effects-global.json, compared per tick
with the binary's per-tick state recorded by run_hit_effect.py (out/*.json).

Transcribed (not executed: the C# is not built here) from:
  Alundra/Scripts/AlundraEffectReservoir.cs : CreateCore, Animate, Tick (Destroying -> freed, else Animate then Move), CreateAttached
  Alundra/Scripts/AlundraEffectStage.cs     : RunFrame (at tick k: the operations queued with tick <= k are applied, THEN reservoir.Tick(k))
  Alundra/Scripts/AlundraWorldProxy.cs      : AttachHitEffect queues the creation with Emit(...) at rank 1 of the tick of the hit pass (BeginSource(tick, 1)); RunFrame is called once
                                              per rendered frame AFTER the whole tick block (the DLL test (w1) uses 1 tick per frame)
Every claim derived from this file is INFERENCE (code reading + transcription), not an execution of the C#.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FX = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/effects/effects-global.json"
OUT = os.path.join(HERE, 'out')


class Slot:
    def __init__(self):
        self.active = False
        self.destroying = False
        self.frame_index = 0
        self.counter = 0
        self.cur_set = -1
        self.target_anim = 0
        self.cur_anim = 0
        self.cur_effect = 0
        self.table = None
        self.animation = None


class Reservoir:
    def __init__(self, tables):
        self.tables = tables
        self.slots = [Slot() for _ in range(128)]

    def create(self, effect, animation):
        for s in self.slots:
            if not s.active:
                s.active = True
                s.destroying = False
                s.target_anim = animation & 0xFF
                s.cur_effect = ~effect & 0xFF
                s.effect = effect
                s.cur_anim = ~s.target_anim & 0xFF
                s.cur_set = -1
                s.table = None
                s.animation = None
                s.frame_index = 0
                s.counter = 0
                return s
        return None

    def tick(self, frozen=False):
        if frozen:
            return
        for s in self.slots:
            if not s.active:
                continue
            if s.destroying:
                s.active = False
                continue
            self.animate(s)

    def animate(self, s):
        if s.effect != s.cur_effect:
            s.table = self.tables[s.effect]
            s.cur_effect = s.effect
            s.cur_anim = ~s.target_anim & 0xFF
        if s.target_anim != s.cur_anim:
            s.animation = s.table['Animations'][s.target_anim]
            s.cur_anim = s.target_anim
            s.frame_index = 0
            s.counter = 0
            s.destroying = False
        else:
            s.counter = (s.counter - 1) & 0xFF
            if s.counter != 0:
                return
        a = s.animation
        wraps = 0
        while True:
            if s.frame_index < len(a['Frames']):
                fr = a['Frames'][s.frame_index]
                s.frame_index += 1
                s.counter = fr[0] & 0xFF
                s.cur_set = fr[1]
                return
            if a['End'] == 'Destroy':
                s.counter = 0xFF
                s.destroying = True
                return
            if a['End'] != 'Loop' or (wraps := wraps + 1) > 1:
                s.destroying = True
                return
            s.frame_index = 0


def simulate(effect, t_hit, total):
    """RunFrame with one tick per frame: the creation queued by the hit pass of tick t_hit is applied before reservoir.Tick(t_hit); state read at the end of each tick"""
    tables = json.load(open(FX, encoding='utf-8-sig'))['Effects']
    r = Reservoir(tables)
    rows = {}
    slot = None
    for t in range(1, total + 1):
        if t == t_hit:
            slot = r.create(effect, 0)
        r.tick()
        if slot is not None:
            rows[t] = dict(active=slot.active, counter=slot.counter, destroying=slot.destroying, frame_ordinal=slot.frame_index - 1, image_set=slot.cur_set)
    return rows


def dedup_sets(words_by_ordinal):
    """the exporter de-duplicates image sets per table in order of first use of the ImageSetPointer (docs/formats/effects.md)"""
    seen = {}
    out = {}
    for o in sorted(words_by_ordinal):
        w = words_by_ordinal[o]
        if w not in seen:
            seen[w] = len(seen)
        out[o] = seen[w]
    return out


def compare(binary_json, effect):
    d = json.load(open(binary_json))
    res = d['result']
    rows = {r['tick']: r for r in d['rows']}
    t_hit = res['T']
    total = max(rows)
    model = simulate(effect, t_hit, total)
    words = {}
    for t, r in rows.items():
        if r.get('status') and r.get('frame_ordinal') is not None:
            words[r['frame_ordinal']] = r['image_word']
    dd = dedup_sets(words)
    diffs = []
    n = 0
    for t in range(t_hit, total + 1):
        b = rows[t]
        m = model[t]
        n += 1
        b_active = b['status'] != 0
        # compare: active, counter (while active), destroying flag vs done flag, frame ordinal and image set (while active)
        if b_active != m['active']:
            diffs.append((t - t_hit, 'active', b_active, m['active']))
            continue
        if not b_active:
            continue
        if b['counter'] != m['counter']:
            diffs.append((t - t_hit, 'counter', b['counter'], m['counter']))
        if bool(b['done']) != m['destroying']:
            diffs.append((t - t_hit, 'destroying/done', b['done'], m['destroying']))
        if b['frame_ordinal'] != m['frame_ordinal']:
            diffs.append((t - t_hit, 'frame_ordinal', b['frame_ordinal'], m['frame_ordinal']))
        if b['frame_ordinal'] is None:
            diffs.append((t - t_hit, 'frame_ordinal(not yet stepped)', None, m['frame_ordinal']))
            continue
        if dd[b['frame_ordinal']] != m['image_set']:
            diffs.append((t - t_hit, 'image_set', dd[b['frame_ordinal']], m['image_set']))
    first_abs_model = next((t - t_hit for t in range(t_hit, total + 1) if not model[t]['active']), None)
    return dict(binary=os.path.basename(binary_json), effect=effect, ticks_compared=n, diffs=diffs, dll_first_absent_d=first_abs_model, binary_first_absent_d=res.get('first_absent_d'))


if __name__ == '__main__':
    lines = []
    for name, effect in (('hero_hits_bean_nib6_binary', 4), ('hero_hits_bean_nib10_binary', 4), ('bean_hits_hero_nib6_binary', 4),
                         ('hero_hits_bean_nib7_binary', 5), ('hero_hits_bean_nib9_binary', 5), ('bean_hits_hero_nib7_binary', 5),
                         ('hero_hits_bean_nib6_slotpass_first', 4), ('hero_hits_bean_nib7_slotpass_first', 5)):
        c = compare(os.path.join(OUT, name + '.json'), effect)
        lines.append('%-36s effect %d : %d ticks compared, differences: %d%s ; first tick absent at end of tick: binary T+%s, DLL model T+%s' % (
            name, effect, c['ticks_compared'], len(c['diffs']), ('' if not c['diffs'] else ' first=%s' % (c['diffs'][:3],)), c['binary_first_absent_d'], c['dll_first_absent_d']))
    open(os.path.join(OUT, 'dll_model_vs_binary.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))
