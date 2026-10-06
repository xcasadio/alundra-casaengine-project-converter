"""Independent prediction of the G1 export numbers from data-extracted JSON (no converter code involved)."""
import json, glob, os, re, collections, hashlib
ROOT = r'D:\development\repo\alundra-casaengine-project-converter\data-extracted\data'
files = sorted(glob.glob(os.path.join(ROOT, 'map_*.json')))
T = collections.Counter()
per_map = {}
hole_after_nonzero = 0
anim_slot_idx_gaps = 0
frames_with_noimg = 0
sets_shared = 0
for p in files:
    name = os.path.basename(p)[4:-5]
    d = json.load(open(p))
    si = d['SpriteInfo']
    recs = si['MapEffectRecords'] or []
    effs = si['SpriteEffectRecords'] or []
    if not recs and not effs: continue
    T['companions' if name != 'alundra' else 'global_companion'] += 1
    T['records'] += len(recs)
    for r in recs:
        T['load_spawn'] += 1 if r['Flags'] & 0x40 else 0
    for ei, e in enumerate(effs):
        T['tables'] += 1
        offs = e['AnimationOffsets'][:e['AnimationCount']]
        # trailing-zero check
        nzs = [i for i, o in enumerate(offs) if o != 0]
        if nzs and (len(nzs) != nzs[-1] + 1): hole_after_nonzero += 1
        seen_sets = {}
        for ai, a in enumerate(e['PreloadedAnims']):
            if offs[ai] == 0:
                T['anim_slots_dropped'] += 1
                continue
            T['animations'] += 1
            nfr = 0
            for fr in a['Frames']:
                raw = fr['Delay']
                if raw & 0x80:
                    nfr += 1; T['frames'] += 1
                    if not fr['Images'] or fr['Images']['NumberOfImages'] == 0: frames_with_noimg += 1
                    ptr = fr['ImageSetPointer']
                    if ptr in seen_sets: sets_shared += 1
                    else:
                        seen_sets[ptr] = fr['Images']
                else:
                    T['end_destroy' if raw == 0 else 'end_loop'] += 1
        T['image_sets'] += len(seen_sets)
        for ptr, im in seen_sets.items():
            for q in (im or {}).get('Images', []):
                if q['Swidth'] == 0 or q['Sheight'] == 0:
                    T['images_degenerate'] += 1
                else:
                    T['images'] += 1
print(dict(T))
print('holes (zero offset before a non-zero one):', hole_after_nonzero, 'frames with no image', frames_with_noimg, 'shared set refs', sets_shared)
