"""E19.g G3c (step G3c-0): prediction of the effect frames the DLL pushes, committed before any DLL code.

Plan: docs/plan-e19-opcodes.md, "E19.g G3c" (G3C-R1 to G3C-R5, G3c-0). Evidence: docs/plan-e19-g3c-annexe/ (README.md,
discovery/notes.md, discovery/verify/verify.md which wins).

INPUTS: the exported effect companions (alundra-project/Maps/*/*/effects/*.effects.json) and their sheet PNGs ONLY. Nothing of the
DLL (Alundra/) or of the engine is read. The pixel rasterizer and blend helpers are those of g2e_predict.py (rule B(k) of engine
ADR-0068, exact integer arithmetic, TOP-LEFT rule for exact coverage ties: verify.md R1). `tie_sign` and `bk` of
g2b1_predict.py are neither imported nor used.

Rules re-derived here from the plan (not from the DLL):
  G3C-R1 anchor     ax = x >> 16, ay = (y - z) >> 16 (32-bit arithmetic shifts); z of 0xA2/0x93 carries the +1.
  G3C-R2 cull       the whole slot is skipped unless -128 <= ax - scrollX <= 448 and -128 <= ay - scrollY <= 368.
  G3C-R3 quads      image k of the current image set: corners (ax + Xi, ay + Yi) - scroll, data order TL, TR, BL, BR; raw window
                    (U - mx, V - my, W, H), mx = C0 > C2, my = C1 > C5; mode Semi ? Abr + 1 : None; sheet 0 (map companion).
  G3C-R4 key        (YSortedWorld 300, layer 300, order 0, elevation 16 * min(DepthKey >> 20, 59) + 6, coordinate 100 * ay,
                    local offset N - 1 - k, stable id = slot index); DepthKey = (y & 0xFFFF0000) + (z >> 16) + (IDSV << 16).
  Ordering of the drawn quads: ascending key (the sprite pass sorts ascending).

Usage:
  python g3c_model.py               writes g3c_expected.json and ref_g3c_<scene>_k<k>.png next to this file, runs the cross-checks
  python g3c_model.py --check       computes, prints and cross-checks, writes nothing
Exit code 0 when every cross-check holds, 1 when one fails (a STOP: never adjust a reference to fit).
"""
import glob
import hashlib
import json
import os
import sys
from fractions import Fraction as F

import numpy as np
from PIL import Image

import g2e_predict as G          # rasterizer, exact B(k), blend: top-left rule only

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
ROOT = os.path.join(REPO, 'alundra-project')
G2B_ANNEX = os.path.join(REPO, 'docs', 'plan-e19-g2b-annexe')

BG = (24, 40, 56)
FACTORS = (1, 3)
PS1_W, PS1_H = 320, 240
PASS_YSORTED = 300
LAYER = 300
MODE_NAMES = {None: 'None', 0: 'Mode0', 1: 'Mode1', 2: 'Mode2', 3: 'Mode3'}
AURA_POS = (960 << 16, 160 << 16, (48 << 16) + 1)      # SpawnPlaced(0, 960, 160, 48): x, y, (z << 16) + 1
AURA_SCROLL = (800, -8)                                # screen anchor (160, 119)


def i32(v):
    v &= 0xFFFFFFFF
    return v - (1 << 32) if v & 0x80000000 else v


# ---------------------------------------------------------------------------------------------------------------------
# The export
# ---------------------------------------------------------------------------------------------------------------------
_CACHE = {}


def load_map(map_id):
    """(doc, sheet RGBA array, world name) of the map companion of map_id."""
    if map_id in _CACHE:
        return _CACHE[map_id]
    paths = [f for f in glob.glob(os.path.join(ROOT, 'Maps', '*', '*', 'effects', '*.effects.json')) if f.endswith('-%d.effects.json' % map_id)]
    assert len(paths) == 1, (map_id, paths)
    path = paths[0]
    with open(path, encoding='utf-8') as fh:
        doc = json.load(fh)
    folder = os.path.dirname(path)
    sheets = [f for f in os.listdir(folder) if f.endswith('_effectsheet.png')]
    assert len(sheets) == 1
    sheet = np.array(Image.open(os.path.join(folder, sheets[0])).convert('RGBA'))
    world = os.path.basename(path)[:-len('.effects.json')]
    _CACHE[map_id] = (doc, sheet, world)
    return _CACHE[map_id]


def pos_of_record(r):
    """SpawnMapEffect: (X * 12 + 12, Y * 8 + 8, Z * 8) pixels in 16.16."""
    return ((r['X'] * 12 + 12) << 16, (r['Y'] * 8 + 8) << 16, r['Z'] << 19)


# ---------------------------------------------------------------------------------------------------------------------
# Animation timeline (from the exported delays; a delay of 0 lasts 256 ticks), slot tick 0 = the first update of the slot
# ---------------------------------------------------------------------------------------------------------------------

def duration(frame):
    return (frame[0] & 0xFF) or 256


def frame_at_tick(anim, tick):
    t = 0
    idx = 0
    frames = anim['Frames']
    while True:
        d = duration(frames[idx])
        if tick < t + d:
            return idx
        t += d
        idx += 1
        if idx == len(frames):
            if anim['End'] != 'Loop':
                raise ValueError('the animation ends before tick %d' % tick)
            idx = 0


def first_tick_of_frame(anim, frame):
    return sum(duration(f) for f in anim['Frames'][:frame])


# ---------------------------------------------------------------------------------------------------------------------
# Scenes
# ---------------------------------------------------------------------------------------------------------------------

def slot_record(table, anim, tick, pos, slot_index):
    return dict(table=table, anim=anim, tick=tick, pos=pos, slot=slot_index)


def make_scene(name, map_id, description, scroll, slots, setup, slot_tick):
    """slots: list of dict(table, anim, tick, pos, slot) - the state of each slot after the frames of the scene. setup: the calls of the harness
    before the RunFrame calls; slot_tick: the slot tick of the wanted image, RunFrame(1, [false]) is called slot_tick + 1 times."""
    doc, _, world = load_map(map_id)
    return dict(name=name, map_id=map_id, world_name=world, sheet_asset_id=doc['SheetTextureAssetId'], description=description, scroll=list(scroll),
                slots=slots, setup=setup, slot_tick=slot_tick, run_frames=slot_tick + 1)


def aura_scene(tick):
    doc, _, _ = load_map(476)
    anim = doc['Effects'][0]['Animations'][1]
    return make_scene('aura_t%d' % tick, 476, '476 aura: SpawnPlaced(0, 960, 160, 48), record animation 1, slot tick %d (frame %d)' % (tick, frame_at_tick(anim, tick)),
                      AURA_SCROLL, [slot_record(0, 1, tick, AURA_POS, 0)],
                      [dict(call='Reservoir.SpawnPlaced', args=[0, 960, 160, 48])], tick)


def load_time_scene(name, map_id, scroll, description):
    doc, _, _ = load_map(map_id)
    slots = []
    for i, r in enumerate(doc['Records']):
        if r['Flags'] & 0x40:
            slots.append(slot_record(r['Effect'], r['Anim'], 0, pos_of_record(r), len(slots)))
    return make_scene(name, map_id, description, scroll, slots,
                      [dict(call='CaptureHeroTile', args=[0, 0]), dict(call='SpawnLoadEffects', args=[])], 0)


def mirror_476_scene():
    doc, _, _ = load_map(476)
    anim = doc['Effects'][0]['Animations'][3]
    tick = first_tick_of_frame(anim, 2)
    return make_scene('mirror_476', 476, '476 effect 0 animation 3 frame 2 at the aura placement (14 quads, 3 mirrored)', AURA_SCROLL,
                      [slot_record(0, 3, tick, AURA_POS, 0)],
                      [dict(call='Reservoir.SpawnPlaced', args=[0, 960, 160, 48]), dict(call='Reservoir.SetAnimation', args=[0, 3])], tick)


def mirror_327_scene():
    doc, _, _ = load_map(327)
    anim = doc['Effects'][0]['Animations'][3]
    tick = first_tick_of_frame(anim, 3)
    return make_scene('mirror_327', 327, '327 effect 0 animation 3 frame 3 at the aura placement (3 mirrored quads, two at a window origin of -1)', AURA_SCROLL,
                      [slot_record(0, 3, tick, AURA_POS, 0)],
                      [dict(call='Reservoir.Create', args=[True, 0, 3] + list(AURA_POS))], tick)


# ---------------------------------------------------------------------------------------------------------------------
# Quads (G3C-R1 to G3C-R4)
# ---------------------------------------------------------------------------------------------------------------------

def image_set_of(doc, slot):
    table = doc['Effects'][slot['table']]
    anim = table['Animations'][slot['anim']]
    frame = frame_at_tick(anim, slot['tick'])
    set_index = anim['Frames'][frame][1]
    return frame, set_index, table['ImageSets'][set_index]


def scene_quads(scene):
    """The quads the DLL pushes for the scene, with the sort key; culled slots are reported in scene['culled']."""
    doc, _, _ = load_map(scene['map_id'])
    sx, sy = scene['scroll']
    quads = []
    culled = []
    shown = []
    for slot in scene['slots']:
        frame, set_index, imset = image_set_of(doc, slot)
        x, y, z = slot['pos']
        ax = i32(x) >> 16
        ay = i32(y - z) >> 16
        key = i32((i32(y) & 0xFFFF0000) + (i32(z) >> 16) + ((imset['Idsv'] & 0xFFFF) << 16))
        elevation = 16 * min(key >> 20, 59) + 6
        info = dict(slot=slot['slot'], frame=frame, image_set=set_index, idsv=imset['Idsv'], anchor_world=[ax, ay], anchor_screen=[ax - sx, ay - sy],
                    depth_key=key, elevation=elevation, coordinate=100 * ay, images=len(imset['Images']))
        if not (-128 <= ax - sx <= 448 and -128 <= ay - sy <= 368):
            culled.append(info)
            continue
        shown.append(info)
        n = len(imset['Images'])
        for k, im in enumerate(imset['Images']):
            c = im['C']
            mx = int(c[0] > c[2])
            my = int(c[1] > c[5])
            wcorners = [(ax + c[2 * j], ay + c[2 * j + 1]) for j in range(4)]
            quads.append(dict(slot=slot['slot'], image=k, cell=[im['U'], im['V'], im['W'], im['H']], raw=[im['U'] - mx, im['V'] - my, im['W'], im['H']],
                              mirror=[mx, my], ps1_corners=[[p[0] - sx, p[1] - sy] for p in wcorners],
                              world_corners=[[p[0], -p[1]] for p in wcorners],
                              abr=(im['Abr'] if im['Semi'] else None), mode=MODE_NAMES[im['Abr'] if im['Semi'] else None],
                              key=[PASS_YSORTED, LAYER, 0, elevation, 100 * ay, n - 1 - k, slot['slot']]))
    quads.sort(key=lambda q: tuple(q['key']))
    scene['culled'] = culled
    scene['shown'] = shown
    return quads


# ---------------------------------------------------------------------------------------------------------------------
# Rule B(k) over a real sheet
# ---------------------------------------------------------------------------------------------------------------------

def render(quads, sheet, k, keep_cover=False):
    W, H = PS1_W * k, PS1_H * k
    sh, sw = sheet.shape[0], sheet.shape[1]
    img = np.empty((H, W, 3), dtype=np.int64)
    img[:, :] = BG
    tol = np.zeros((H, W), dtype=np.uint8)
    noise = np.zeros((H, W), dtype=bool)
    unsafe = np.zeros((H, W), dtype=bool)
    tie_vis = np.zeros((H, W), dtype=bool)
    tie_any = np.zeros((H, W), dtype=bool)
    cov_tie = np.zeros((H, W), dtype=bool)
    cov_tie_cov = np.zeros((H, W), dtype=bool)
    clamped = np.zeros((H, W), dtype=bool)
    drawn = np.zeros((H, W), dtype=bool)
    covers = []
    for q in quads:
        corners = q['ps1_corners']
        box = G.quad_box(corners, k, W, H)
        x0, y0, x1, y1 = box
        if x1 <= x0 or y1 <= y0:
            covers.append(None)
            continue
        f = G.quad_fields(corners, q['raw'], k, box)
        sl = (slice(y0, y1), slice(x0, x1))
        cx = np.clip(f.tx, 0, sw - 1)
        cy = np.clip(f.ty, 0, sh - 1)
        px = sheet[cy, cx].astype(np.int64)
        a = px[..., 3]
        semi = q['abr'] is not None
        assert not (semi and (f.covered & (a > 0) & (a != 255) & (a != 128)).any()), 'alpha other than 0, 128, 255 in a semi quad'
        draw = f.covered & (a > 0)
        replace = draw & ((a == 255) | (not semi))
        blended = draw & (a == 128) & semi
        reg = img[sl]
        src = px[..., :3]
        if semi:
            reg = np.where(blended[..., None], G.blend(q['abr'], src, reg), reg)
        reg = np.where(replace[..., None], src, reg)
        img[sl] = reg
        t = tol[sl]
        t = np.where(replace, 0, t)
        t = np.where(blended, 1, t)
        tol[sl] = t
        tie = (f.tie_u | f.tie_v) & ~f.noise
        tv = tie_vis[sl]
        tv = np.where(replace, tie, tv)
        tv = np.where(blended, tv | tie, tv)
        tie_vis[sl] = tv
        tie_any[sl] |= f.covered & (f.tie_u | f.tie_v)
        cov_tie[sl] |= f.cov_tie
        cov_tie_cov[sl] |= f.cov_tie & f.covered
        noise[sl] |= f.noise
        unsafe[sl] |= f.unsafe
        outside = (f.tx < 0) | (f.ty < 0) | (f.tx >= sw) | (f.ty >= sh)
        clamped[sl] |= f.covered & outside
        drawn[sl] |= draw
        if keep_cover:
            full = np.zeros((H, W), dtype=bool)
            full[sl] = f.covered
            covers.append(full)
    return dict(img=img, tol=tol, noise=noise, unsafe=unsafe, tie_vis=tie_vis, tie_any=tie_any, cov_tie=cov_tie, cov_tie_cov=cov_tie_cov,
                clamped=clamped, drawn=drawn, covers=covers, k=k, W=W, H=H)


def digest(img):
    return hashlib.sha1(img.astype(np.uint8).tobytes()).hexdigest()


def summarize(res):
    img, noise = res['img'], res['noise']
    lit = (img != np.array(BG)).any(axis=2)
    keep = ~noise
    return dict(
        width=res['W'], height=res['H'], k=res['k'],
        sha1=digest(img),
        lit_pixels=int(lit.sum()),
        drawn_pixels=int(res['drawn'].sum()),
        noise_pixels=int(noise.sum()),
        noise_sha1=hashlib.sha1(np.packbits(noise).tobytes()).hexdigest(),
        blended_pixels=int((res['tol'] == 1).sum()),
        exact_ties_kept=int((res['tie_any'] & keep).sum()),
        texel_ties_visible=int((res['tie_vis'] & keep).sum()),
        coverage_ties_on_boundary=int((res['cov_tie'] & keep).sum()),
        coverage_ties_covered=int((res['cov_tie_cov'] & keep).sum()),
        clamped_pixels=int((res['clamped'] & keep).sum()),
    )


def pick_probes(res):
    """Probes (as g2e_predict.pick_probes, with this background): pixels outside the noise zone, margins >= 1/16 texel and 1/16 px, a
    deterministic spread; plus clamped, visible texel tie and coverage tie pixels, and background pixels. Each: x, y, color, tol, kind."""
    img, tol = res['img'], res['tol']
    keep = ~res['noise']
    lit = (img != np.array(BG)).any(axis=2)
    out = []

    def add(mask, n, kind):
        pts = np.argwhere(mask)
        if len(pts) == 0:
            return
        step = max(1, len(pts) // n)
        for j, i in pts[::step][:n]:
            out.append(dict(x=int(i), y=int(j), color=[int(c) for c in img[j, i]], tol=int(tol[j, i]), kind=kind))

    add(lit & keep & ~res['unsafe'] & ~res['tie_any'] & (tol == 0), 5, 'margin-opaque')
    add(lit & keep & ~res['unsafe'] & ~res['tie_any'] & (tol == 1), 3, 'margin-blended')
    add(res['clamped'] & keep & ~res['tie_any'], 4, 'clamped')
    add(res['tie_vis'] & keep, 4, 'texel-tie')
    add(res['cov_tie_cov'] & keep, 3, 'coverage-tie')
    add(~lit & keep & ~res['unsafe'] & ~res['cov_tie'], 2, 'background')
    return out


def render_scene(scene, k, keep_cover=False):
    _, sheet, _ = load_map(scene['map_id'])
    return render(scene_quads(scene), sheet, k, keep_cover)


# ---------------------------------------------------------------------------------------------------------------------
# The ABR scene (verify M3): chosen by script among the frames of 378 effect 1 (ABR2) and 7 effect 1 (ABR0)
# ---------------------------------------------------------------------------------------------------------------------

def abr_candidates():
    """Every (map, effect 1, animation, frame) whose image set has 2 to 8 images, at least 2 mirrored and a semi mode ABR0 or ABR2, with one
    representative per image set. Created by Reservoir.Create(true, 1, anim, ...) at the aura placement."""
    out = []
    for map_id in (378, 7):
        doc, _, _ = load_map(map_id)
        table = doc['Effects'][1]
        seen = set()
        for ai, anim in enumerate(table['Animations']):
            for fi, (_, si) in enumerate(anim['Frames']):
                if si in seen:
                    continue
                imset = table['ImageSets'][si]
                ims = imset['Images']
                mirrored = sum(1 for i in ims if i['C'][0] > i['C'][2] or i['C'][1] > i['C'][5])
                abrs = {i['Abr'] for i in ims if i['Semi']}
                if 2 <= len(ims) <= 8 and mirrored >= 2 and abrs and abrs <= {0, 2}:
                    seen.add(si)
                    out.append((map_id, ai, fi, si))
    return out


def abr_scene_for(map_id, anim, frame):
    doc, _, _ = load_map(map_id)
    a = doc['Effects'][1]['Animations'][anim]
    tick = first_tick_of_frame(a, frame)
    return make_scene('abr_%d' % map_id, map_id,
                      '%d effect 1 animation %d frame %d at the aura placement (mirrored images, ABR %s)' % (
                          map_id, anim, frame, 'ABR2' if map_id == 378 else 'ABR0'),
                      AURA_SCROLL, [slot_record(1, anim, tick, AURA_POS, 0)],
                      [dict(call='Reservoir.Create', args=[True, 1, anim] + list(AURA_POS))], tick)


def choose_abr_scene():
    """Score of a candidate = number of blended pixels at k = 1 whose value differs from the background (a clamped blend tells nothing).
    The best score wins, the first candidate on a tie. Returns (scene, table of scores)."""
    best = None
    scores = []
    for map_id, ai, fi, si in abr_candidates():
        sc = abr_scene_for(map_id, ai, fi)
        res = render_scene(sc, 1)
        informative = int(((res['tol'] == 1) & (res['img'] != np.array(BG)).any(axis=2)).sum())
        mirrored_q = sum(1 for q in scene_quads(sc) if q['mirror'] != [0, 0])
        scores.append(dict(map=map_id, anim=ai, frame=fi, image_set=si, informative_blended=informative, blended=int((res['tol'] == 1).sum()), mirrored_quads=mirrored_q))
        if mirrored_q >= 2 and (best is None or informative > best[0]):
            best = (informative, sc)
            sc['pick'] = [map_id, ai, fi]
    return best[1], scores


def scene_by_name(name, abr_pick=None):
    """One scene by its name; abr_pick = [map, animation, frame] of the ABR scene (stored in g3c_expected.json, 'abr_choice.chosen')."""
    if name.startswith('aura_t'):
        return aura_scene(int(name[len('aura_t'):]))
    if name == 'rays_163':
        return load_time_scene('rays_163', 163, (220, 40), '163 rays: the 4 load-time records, one image for the slot ticks 0 to 10')
    if name == 'pieces_391':
        return load_time_scene('pieces_391', 391, (836, 640), '391 pieces: the 5 load-time records (slot 0 culled)')
    if name == 'mirror_476':
        return mirror_476_scene()
    if name == 'mirror_327':
        return mirror_327_scene()
    if name.startswith('abr_'):
        return abr_scene_for(*abr_pick)
    raise KeyError(name)


SCENE_NAMES = ['aura_t0', 'aura_t2', 'aura_t16', 'rays_163', 'pieces_391', 'mirror_476', 'mirror_327', 'abr_378']


def build_scenes():
    abr, _ = choose_abr_scene()
    return [scene_by_name(n, abr['pick']) for n in SCENE_NAMES[:-1]] + [abr]


# ---------------------------------------------------------------------------------------------------------------------
# Cross-checks
# ---------------------------------------------------------------------------------------------------------------------
# discovery notes.md section 4 (the 163 and 391 digests stand, verify.md section 2)
DISCOVERY = {('rays_163', 1): '8be82e40f923b8d1a9c06f5030faea08b6359e49', ('rays_163', 3): '2356a8852057d58d42d235df903b4e11e561631d',
             ('pieces_391', 1): '801a081a47c2a7091c5918124890d931de0d5872', ('pieces_391', 3): '23edcdbfea2215bdf5593d4e99e82fb451b27101'}
# verify.md section 2, top-left rule (aura)
VERIFY_AURA = {('aura_t0', 1): 'de9e3afd56e6b1b131a49328ab1140ca06dfc568', ('aura_t2', 1): '607a9e7d2191a26b79505160234876dce51a8609',
               ('aura_t16', 1): 'dba859ed08f61882f0531da37d6c34e1ad091dbc', ('aura_t0', 3): 'bda9e52c6a2ebbafd14de98d1be96a0433045cbb',
               ('aura_t2', 3): '44577bffda90382a6cf0300ab044a75ef7d6f3c8', ('aura_t16', 3): 'c440a862e56ae8369fc0c1e24fb033fd3a755dea'}
# verify.md section 3 (mirror scenes at the aura placement)
VERIFY_MIRROR = {('mirror_476', 1): '44417c5ee8e73983d9f531c55af1a58ac79f4cf9', ('mirror_476', 3): 'b9438a7e5a91a16d13f8d60fa5ff08e2db88363e',
                 ('mirror_327', 1): 'b39feb41c72bd285b4ec11c585dbc6438d7bfac2', ('mirror_327', 3): 'd5e110bfc89e3b53e787ba17934fadb82aeecc77'}
VERIFY_LIT = {('mirror_476', 1): 11787, ('mirror_476', 3): 106203, ('mirror_327', 1): 514, ('mirror_327', 3): 4626}


def load_walker():
    """The hardware walker redux.py of the G2b annex, loaded from its file with the 8-bit texel wrap (& 0xff) removed (the exported sheets are up
    to 512 texels wide; the wrap only touches the texel, never the covered pixels)."""
    src = open(os.path.join(G2B_ANNEX, 'redux.py'), encoding='utf-8').read()
    assert src.count('& 0xff') == 2
    ns = {}
    exec(compile(src.replace(' & 0xff', ''), 'redux_nowrap', 'exec'), ns)
    return ns


def walker_drawn(scene, quads, sheet, ns):
    """k = 1: drawn mask (covered pixel with alpha > 0) of the hardware walker, and the per-quad covered masks."""
    drawn = np.zeros((PS1_H, PS1_W), dtype=bool)
    covers = []
    sh, sw = sheet.shape[0], sheet.shape[1]
    for q in quads:
        u0, v0, w, h = q['raw']
        px = ns['quad'](q['ps1_corners'], [(0, 0), (w, 0), (0, h), (w, h)])
        cov = np.zeros((PS1_H, PS1_W), dtype=bool)
        for (x, y), (tu, tv) in px.items():
            if not (0 <= x < PS1_W and 0 <= y < PS1_H):
                continue
            cov[y, x] = True
            if sheet[min(max(v0 + tv, 0), sh - 1), min(max(u0 + tu, 0), sw - 1), 3] > 0:
                drawn[y, x] = True
        covers.append(cov)
    return drawn, covers


def cross_checks(results):
    """results[(name, k)] = (res, summary). Returns the list of (label, ok, detail)."""
    out = []

    def digest_check(label, table):
        for key, want in table.items():
            got = results[key][1]['sha1']
            out.append(('%s %s k=%d' % (label, key[0], key[1]), got == want, 'got %s want %s' % (got, want)))

    digest_check('discovery notes.md section 4 digest', DISCOVERY)
    digest_check('verify.md section 2 top-left digest', VERIFY_AURA)
    digest_check('verify.md section 3 mirror digest', VERIFY_MIRROR)
    for key, want in VERIFY_LIT.items():
        got = results[key][1]['lit_pixels']
        out.append(('verify.md section 3 lit pixels %s k=%d' % key, got == want, 'got %d want %d' % (got, want)))
    ns = load_walker()
    for name in ('aura_t0', 'aura_t2', 'aura_t16'):
        sc = [s for s in results['scenes'] if s['name'] == name][0]
        _, sheet, _ = load_map(sc['map_id'])
        quads = scene_quads(sc)
        wd, wc = walker_drawn(sc, quads, sheet, ns)
        res = render(quads, sheet, 1, keep_cover=True)
        diff_drawn = int((wd != res['drawn']).sum())
        diff_quad_cov = sum(int((a != b).sum()) for a, b in zip(wc, res['covers']))
        out.append(('aura coverage k=1 == hardware walker (drawn pixels) %s' % name, diff_drawn == 0,
                    '%d differing drawn pixels of %d; per-quad geometric coverage differs on %d pixel-quads (informational)' % (diff_drawn, int(wd.sum()), diff_quad_cov)))
    return out


# ---------------------------------------------------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------------------------------------------------

def harness_entry(sc):
    doc, _, _ = load_map(sc['map_id'])
    imsets = []
    for s in sc['slots']:
        frame, set_index, imset = image_set_of(doc, s)
        imsets.append(dict(slot=s['slot'], table=s['table'], animation=s['anim'], frame=frame, image_set=set_index, idsv=imset['Idsv'], images=len(imset['Images'])))
    return dict(
        world_name=sc['world_name'],
        bank='AlundraEffectBankLoader.Load(projectPath, "%s")' % sc['world_name'],
        sequence=['AttachService(service)', 'Load(bank)'] + ['%s(%s)' % (c['call'], ', '.join(json.dumps(a) for a in c['args'])) for c in sc['setup']]
                 + ['RunFrame(1, [false]) x %d' % sc['run_frames'], 'PushFrame(camera)', 'one engine Update and Draw', 'GetBackBufferData'],
        setup=sc['setup'],
        run_frame=dict(call='RunFrame', args=[1, [False]], count=sc['run_frames']),
        slot_tick=sc['slot_tick'],
        slots_after_run=imsets,
        slots_culled=[c['slot'] for c in sc['culled']])


def build_expected():
    images = {}
    scenes = build_scenes()
    abr_pick, abr_scores = choose_abr_scene()
    assert abr_pick['name'] == SCENE_NAMES[-1], abr_pick['name']
    out = dict(
        description='E19.g G3c-0 prediction of the effect frames the DLL pushes (rule B(k), top-left tie rule). Generated by g3c_model.py from the export only.',
        frame=dict(ps1_size=[PS1_W, PS1_H], factors=list(FACTORS), background=list(BG),
                   note=('image = the top-left 320 k x 240 k pixels of the back-buffer (w = 320 k, h = 240 k exactly); camera Zoom = k, Target = (scrollX + 160, '
                         '-(scrollY + 120)) with integer components; the PS1 point (x, y) is the screen point (k x, k y); the background is posed by the harness')),
        noise_rule=('texel within 2^-10 texel of a boundary (exact ties excluded, i.e. kept) or pixel top-left corner within 2^-10 px of an edge or the '
                    'TR-BL diagonal (exact distance 0 excluded, i.e. kept); exact ties are decided by the top-left rule'),
        tolerance_rule='opaque and background pixels exact; a pixel last written by a blended STP draw (modes Mode0 to Mode2) +-1 per channel',
        abr_choice=dict(rule='the candidate (378 effect 1 ABR2, 7 effect 1 ABR0) frame with the most blended pixels at k = 1 that differ from the background; 2 to 8 images, >= 2 mirrored',
                        chosen=abr_pick['pick'], candidates=abr_scores),
        scenes=[])
    for sc in scenes:
        quads = scene_quads(sc)
        entry = dict(name=sc['name'], description=sc['description'], map_id=sc['map_id'], sheet_asset_id=sc['sheet_asset_id'], scroll=sc['scroll'],
                     camera=dict(target=[sc['scroll'][0] + 160, -(sc['scroll'][1] + 120)], zoom_values=list(FACTORS)),
                     harness=harness_entry(sc), shown_slots=sc['shown'], culled_slots=sc['culled'], quads=quads, per_k={})
        for k in FACTORS:
            res = render_scene(sc, k)
            info = summarize(res)
            info['probes'] = pick_probes(res)
            entry['per_k'][str(k)] = info
            images[(sc['name'], k)] = res['img'].astype(np.uint8)
        out['scenes'].append(entry)
    return out, images


def main():
    np.seterr(invalid='ignore')
    check = '--check' in sys.argv
    out, images = build_expected()
    results = {'scenes': build_scenes()}
    for e in out['scenes']:
        h = e['harness']
        print('%s: map %d, scroll %s, %d quads (%d culled slots), slot_tick %d' % (e['name'], e['map_id'], e['scroll'], len(e['quads']), len(e['culled_slots']), h['slot_tick']))
        print('    sequence: ' + ' ; '.join(h['sequence']))
        for ks, i in e['per_k'].items():
            print('    k=%s %dx%d sha1=%s lit=%d drawn=%d noise=%d blended=%d ties_kept=%d (visible %d) cov_ties=%d (covered %d) clamped=%d probes=%d'
                  % (ks, i['width'], i['height'], i['sha1'], i['lit_pixels'], i['drawn_pixels'], i['noise_pixels'], i['blended_pixels'], i['exact_ties_kept'],
                     i['texel_ties_visible'], i['coverage_ties_on_boundary'], i['coverage_ties_covered'], i['clamped_pixels'], len(i['probes'])))
            results[(e['name'], int(ks))] = (None, i)
    print('ABR candidates:')
    for s in out['abr_choice']['candidates']:
        print('   ', s)
    bad = 0
    print('cross-checks:')
    for label, ok, detail in cross_checks(results):
        print('  [%s] %s: %s' % ('PASS' if ok else 'FAIL', label, detail))
        bad += 0 if ok else 1
    if bad:
        print('STOP: %d cross-check(s) failed; nothing written' % bad)
        sys.exit(1)
    if not check:
        for (name, k), img in images.items():
            Image.fromarray(img, 'RGB').save(os.path.join(HERE, 'ref_g3c_%s_k%d.png' % (name, k)), optimize=True)
        with open(os.path.join(HERE, 'g3c_expected.json'), 'w', newline='\n', encoding='utf-8') as fh:
            json.dump(out, fh, indent=1)
            fh.write('\n')


if __name__ == '__main__':
    main()
