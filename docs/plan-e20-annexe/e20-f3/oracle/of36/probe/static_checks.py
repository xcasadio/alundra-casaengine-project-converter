"""static_checks.py - O-F3-6: static facts about ALUN_CD.EXE (France) used by the report (disassembly dumps, call sites, order of the frame function, template writers, raw bank vs JSON).
usage: python -B static_checks.py     (writes out/disasm/*.txt, out/static_facts.txt, out/bank_check.txt)
"""
import os
import sys
import json

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r'D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-e0k/oracle')
import exe   # noqa: E402

OUT = os.path.join(HERE, 'out')
DIS = os.path.join(OUT, 'disasm')
os.makedirs(DIS, exist_ok=True)

FUNCS = [
    ('8003BE74_CreateAttachedEffect', 0x8003BE74, None),
    ('8003BDD8_CreateEffect_mode0', 0x8003BDD8, None),
    ('8003BFE8_CreateDetachedEffect_mode3', 0x8003BFE8, 0x8003C094),
    ('8003B9C4_GetFreeEffect', 0x8003B9C4, None),
    ('8003BB14_InitEffect', 0x8003BB14, 0x8003BBDC),
    ('8003B9FC_LookupEffectTable', 0x8003B9FC, None),
    ('8003BBDC_AnimateEffect', 0x8003BBDC, 0x8003BDD8),
    ('8003C284_MoveEffect', 0x8003C284, 0x8003C410),
    ('8003C410_UpdateEffects_slot_pass', 0x8003C410, 0x8003C508),
    ('8003C1A4_MapLoadEffectsClear', 0x8003C1A4, None),
    ('80038E84_entity_effects_pass', 0x80038E84, 0x80039188),
    ('80039300_hit_pass_gate_800395D4-80039668', 0x800395D4, 0x8003966C),
    ('8003B388_world_tick_passes', 0x8003B388, 0x8003B414),
    ('8002E058_frame_function_tail', 0x8002E0F0, 0x8002E130),
]


def main():
    for name, a, e in FUNCS:
        lines = exe.dis(a, e) if e else exe.fn(a)
        open(os.path.join(DIS, name + '.txt'), 'w').write('\n'.join(lines) + '\n')

    facts = []

    def jal_target(addr):
        w = exe.u32(addr)
        assert w >> 26 == 3, hex(addr)
        return ((w & 0x03FFFFFF) << 2) | (addr & 0xF0000000)

    facts.append('FACT frame function 0x8002E058 (called once by the main loop at 0x8002BC4C: jal %08X):' % jal_target(0x8002BC4C))
    for a, label in ((0x8002E100, 'jal 0x8003C67C map-record entity spawner'), (0x8002E108, 'jal world tick 0x8003B388'), (0x8002E110, 'jal slot pass 0x8003C410')):
        facts.append('   %08X: %s  -> %08X   (%s)' % (a, '%08X' % exe.u32(a), jal_target(a) if (exe.u32(a) >> 26) == 3 else 0, label))
    facts.append('FACT passes inside 0x8003B388 (pause flag [0x800DC4B8] & 0x48 == 0):')
    for a in (0x8003B3A0, 0x8003B3A8, 0x8003B3B0, 0x8003B3D0, 0x8003B3D8, 0x8003B3E0, 0x8003B3E8, 0x8003B3F0):
        facts.append('   %08X: jal %08X' % (a, jal_target(a)))
    facts.append('   -> 0x80038E84 (entity effects) at 0x8003B3E8, hit pass 0x80039300 at 0x8003B3F0, then 0x800399B8 (entity list/draw prep, no effect creator inside: see below), return; the slot pass 0x8003C410 follows in 0x8002E058.')

    # every call site of the creators and of the slot pass
    n = (len(exe.DATA) - 0x800) // 4
    sites = {}
    targets = {0x8003BE74: 'CreateAttached', 0x8003BDD8: 'CreateEffect', 0x8003BFE8: 'CreateDetached', 0x8003C410: 'UpdateEffects', 0x8003BBDC: 'AnimateEffect', 0x8003C284: 'MoveEffect',
               0x8003B9C4: 'GetFree', 0x8003BB14: 'InitEffect', 0x8003B388: 'WorldTick', 0x80038E84: 'EntityEffectsPass', 0x80039300: 'HitPass'}
    for i in range(n):
        a = exe.BASE + 4 * i
        w = exe.u32(a)
        if w >> 26 in (2, 3):
            t = ((w & 0x03FFFFFF) << 2) | (a & 0xF0000000)
            if t in targets:
                sites.setdefault(targets[t], []).append(a)
    for k in ('UpdateEffects', 'AnimateEffect', 'MoveEffect', 'WorldTick', 'EntityEffectsPass', 'HitPass', 'CreateAttached', 'CreateDetached'):
        facts.append('FACT call sites of %s: %s' % (k, ' '.join('%08X' % a for a in sites.get(k, []))))
    facts.append('FACT call sites of CreateEffect 0x8003BDD8: %d sites (list in out/disasm is not needed here)' % len(sites.get('CreateEffect', [])))
    facts.append('FACT AnimateEffect 0x8003BBDC is called ONLY from 0x8003C480 (inside the slot pass 0x8003C410); the slot pass is called ONLY from 0x8002E110.')

    # static writers of the template
    lo, hi = 0x8013C608, 0x8013C688
    words = [exe.u32(exe.BASE + 4 * i) for i in range(n)]

    def sx(v):
        return v - 0x10000 if v & 0x8000 else v
    writers = []
    for i, w in enumerate(words):
        if w >> 26 == 0x0F:
            rt = (w >> 16) & 31
            hiv = (w & 0xFFFF) << 16
            for j in range(i + 1, min(i + 40, n)):
                w2 = words[j]
                op2 = w2 >> 26
                rs2 = (w2 >> 21) & 31
                if op2 in (0x28, 0x29, 0x2B) and rs2 == rt and lo <= ((hiv + sx(w2 & 0xFFFF)) & 0xFFFFFFFF) < hi:
                    writers.append(exe.BASE + 4 * j)
                if op2 == 0x0F and ((w2 >> 16) & 31) == rt:
                    break
    facts.append('FACT static stores (lui + sb/sh/sw) into the template 0x8013C608..0x8013C688: %s (none = the template is only ever read by 0x8003BB14; INFERENCE: zero BSS)' % (writers or 'none'))

    # the effect 4 / 5 hit-pass call sites
    facts.append('FACT hit pass creator calls: 0x80039624 (nibble 6 or 10 -> BE74(0, 4, 0, target, 1, 0, 0, 0)) and 0x80039660 (nibble 7 or 9 -> BE74(0, 5, 0, target, 1, 0, 0, 0)); gate 0x800395E8: (response byte & 0xC0) == 0x80 skips both.')
    open(os.path.join(OUT, 'static_facts.txt'), 'w').write('\n'.join(facts) + '\n')
    print('\n'.join(facts))

    # ---- raw bank vs JSON
    raw = open(r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN", 'rb').read(0x9000)
    mj = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json", encoding='utf-8-sig'))['SpriteInfo']['SpriteEffectRecords']
    gj = json.load(open(r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/effects/effects-global.json", encoding='utf-8-sig'))['Effects']
    lines = []
    for idx in (4, 5, 9):
        base = mj[idx]['BinOffset']
        u16 = lambda o: raw[base + o] | (raw[base + o + 1] << 8)
        p = u16(0)                       # sequence 0 : u16 at bank + 0 (0x8003BC8C : lhu (a1 + 2*seq))
        frames = []
        while True:
            b = raw[base + p]
            if b & 0x80:
                frames.append((b & 0x7F, u16(p + 1)))
                p += 3
            else:
                end = b
                break
        total = sum(f[0] for f in frames)
        # JSON side
        jf = mj[idx]['PreloadedAnims'][0]['Frames']
        jframes = [(f['Delay'] & 0x7F, f['ImageSetPointer'] // 2) for f in jf[:-1]]
        seen = {}
        dd = []
        for _, w in frames:
            seen.setdefault(w, len(seen))
            dd.append(seen[w])
        g = gj[idx]['Animations'][0]['Frames']
        gf = [(x[0], x[1]) for x in g]
        ok_json = frames == jframes
        ok_exp = [(f[0], d) for f, d in zip(frames, dd)] == gf
        lines.append('effect %d: raw DATAS.BIN table @%d : %d frames, end byte %d (0 = Destroy), sum of delays S = %d ; frames == map_alundra.json PreloadedAnims: %s ; (delay, de-duplicated set) == effects-global.json Frames: %s ; first words %s' % (
            idx, base, len(frames), end, total, ok_json, ok_exp, [w for _, w in frames][:6]))
    open(os.path.join(OUT, 'bank_check.txt'), 'w').write('\n'.join(lines) + '\n')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
