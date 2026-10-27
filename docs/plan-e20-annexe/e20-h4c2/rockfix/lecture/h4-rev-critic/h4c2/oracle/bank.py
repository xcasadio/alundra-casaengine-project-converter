"""bank.py - D1 oracle: the hero's sprite record (bank alundra_0, Sector5Id 0 of data-extracted/data/map_alundra.json,
SpriteInfo.SpriteRecords[0], the record SpriteBankReader reads for `alundra_0`) laid out in RAM the way UpdateAnimation 0x80038AB4 reads it.

Layout read from the disassembly of 0x80038AB4 (docs/plan-e19-o9-annexe/dis_update_animation.txt, copied in ./dis_update_animation.txt) and
from the extractor's own readers (alundra-datas-analyser/AlundraTools/AlundraEngine/DatasBin: SpriteRecord, SpriteTableHeader, AnimationSet,
SiAnimation, SiFrame, FrameCollisionData, SiImageSet, SiImage):

  record R (entity+0x64 points at R):
     R+0x00  pointer to the animation-set table            (file: AnimationOffsetsPointer, R+0x20 in the file layout)
     R+0x04  pointer to the animation byte region          (file: AnimationsPointer)
     R+0x08  pointer to the frame-collision region         (file: FrameCollisionPointer)
     R+0x0C  pointer to the image-set region               (file: FramesPointer)
     R+0x10  16 header bytes (Ubuff): MoreFlags, CanPickup, FlagsPortraitShadowType, ProgramLoad/Tick/Touch/Deactivate/Interact,
             OffsetX/Y/Z (sbyte), SizeX/Y/Z, BreakEffect, Contents
  animation set i (14 bytes) at [R+0]+14*i: u16 offset down, up, left, right (0xFFFF = none), u16 speed, u8 Unknown, u8 Flags, u8 Sfx, u8 Acceleration
  animation (frame list) at [R+4]+offset: frames of 5 bytes (Delay with bit 7 set, CollisionOffset u16 (0xFFFF none), image-set index u16 = byte offset / 2),
     ended by a 1-byte terminator (Delay 1 = loop) or a 2-byte terminator (0, TransformIndexLow = chain / hold byte)
  frame collision at [R+8]+CollisionOffset: sbyte offX, offY, offZ, byte width, depth, height
  image set at [R+0xC]+2*index: byte DepthSortValue, byte NumberOfImages, then 14 bytes per image

The bytes are produced from the DECODED fields of the JSON (Delay, TransformIndexLow/High, SpriteIndexLow/High, CollisionData, Images, the animation-set
fields); each decoded frame also carries the exact RAM address the extractor read it from, which gives the relative offsets; the script checks that
two writers of the same byte agree.
"""
import json
import os

DATA = r"D:/development/repo/alundra-casaengine-project-converter/data-extracted/data/map_alundra.json"


def load_hero_record(path=DATA):
    d = json.load(open(path, encoding='utf-8-sig'))
    recs = d['SpriteInfo']['SpriteRecords']
    r = [x for x in recs if x and x['Header']['Sector5Id'] == 0][0]
    return r


def build_regions(rec):
    """-> dict of bytearrays: animsets, anims, coll, imgs (+ header16), computed from the decoded record only"""
    H = rec['Header']
    sets = rec['AnimSets']
    animsets = bytearray()
    for s in sets:
        for o in s['AnimationOffsets']:
            animsets += (o & 0xFFFF).to_bytes(2, 'little')
        animsets += (s['Speed'] & 0xFFFF).to_bytes(2, 'little')
        animsets += bytes([s['Unknown'] & 0xFF, s['Flags'] & 0xFF, s['Sfx'] & 0xFF, s['Acceleration'] & 0xFF])
    assert len(animsets) == 14 * len(sets)

    anim_bytes = {}                        # relative offset -> byte
    coll_bytes = {}
    img_bytes = {}
    clash = []

    def put(dic, off, b):
        if off in dic and dic[off] != b:
            clash.append((off, dic[off], b))
        dic[off] = b

    anim_base = H['SpriteInfoMemoryAddress'] + H['AnimationsPointer']
    for s in sets:
        for dr, an in enumerate(s['PreloadedAnims']):
            if an is None:
                continue
            base_off = s['AnimationOffsets'][dr]
            n = len(an['Frames'])
            for i, f in enumerate(an['Frames']):
                off = base_off + 5 * i
                assert f['MemoryAddress'] - anim_base == off, (f['MemoryAddress'], anim_base, off)
                if f['IsTransitionFrame']:
                    # SiAnimation: first byte `test` with bit 7 clear; if test == 0 a second byte (TransformIndexLow) follows
                    put(anim_bytes, off, f['Delay'])
                    if f['Delay'] == 0:
                        put(anim_bytes, off + 1, f['TransformIndexLow'])
                else:
                    bs = [f['Delay'], f['TransformIndexLow'], f['TransformIndexHigh'], f['SpriteIndexLow'], f['SpriteIndexHigh']]
                    for k, b in enumerate(bs):
                        put(anim_bytes, off + k, b)
                    cd = f.get('CollisionData')
                    if cd:
                        co = (f['TransformIndexHigh'] << 8) | f['TransformIndexLow']
                        assert co != 0xFFFF
                        vals = [cd['OffsetX'] & 0xFF, cd['OffsetY'] & 0xFF, cd['OffsetZ'] & 0xFF, cd['Width'], cd['Depth'], cd['Height']]
                        for k, b in enumerate(vals):
                            put(coll_bytes, co + k, b)
                    else:
                        assert ((f['TransformIndexHigh'] << 8) | f['TransformIndexLow']) == 0xFFFF
                    im = f.get('Images')
                    if im:
                        ip = ((f['SpriteIndexHigh'] << 8) | f['SpriteIndexLow']) * 2
                        assert ip == f['ImageSetPointer']
                        put(img_bytes, ip, im['DepthSortValue'])
                        put(img_bytes, ip + 1, im['NumberOfImages'])
                        for j, I in enumerate(im['Images']):
                            vals = [I['Spritesheet'], I['Palette'], I['Sx'], I['Sy'], I['Swidth'], I['Sheight'],
                                    I['X1'] & 0xFF, I['Y1'] & 0xFF, I['X2'] & 0xFF, I['Y2'] & 0xFF, I['X3'] & 0xFF, I['Y3'] & 0xFF, I['X4'] & 0xFF, I['Y4'] & 0xFF]
                            for k, b in enumerate(vals):
                                put(img_bytes, ip + 2 + 14 * j + k, b)
    assert not clash, clash[:5]

    def dense(dic):
        n = (max(dic) + 1) if dic else 0
        out = bytearray(n)
        for k, v in dic.items():
            out[k] = v & 0xFF
        return out, len(dic)
    anims, na = dense(anim_bytes)
    coll, nc = dense(coll_bytes)
    imgs, ni = dense(img_bytes)
    return dict(animsets=animsets, anims=anims, coll=coll, imgs=imgs, header16=bytes(H['Ubuff']),
                stats=dict(animsets=len(sets), anim_bytes=na, coll_bytes=nc, img_bytes=ni, clash=len(clash)))


def install(m, rec_addr, regions_base, rec=None):
    """write the record at rec_addr and its regions from regions_base; returns a dict of addresses"""
    rec = rec or load_hero_record()
    R = build_regions(rec)
    a_sets = rec_addr + 0x20
    pos = a_sets + len(R['animsets'])
    pos = (pos + 0xF) & ~0xF
    a_anims = pos
    pos += len(R['anims']); pos = (pos + 0xF) & ~0xF
    a_coll = pos
    pos += len(R['coll']); pos = (pos + 0xF) & ~0xF
    a_imgs = pos
    pos += len(R['imgs']); pos = (pos + 0xF) & ~0xF
    m.poke(rec_addr + 0, a_sets)
    m.poke(rec_addr + 4, a_anims)
    m.poke(rec_addr + 8, a_coll)
    m.poke(rec_addr + 0xC, a_imgs)
    m.poke_bytes(rec_addr + 0x10, R['header16'])
    m.poke_bytes(a_sets, R['animsets'])
    m.poke_bytes(a_anims, R['anims'])
    m.poke_bytes(a_coll, R['coll'])
    m.poke_bytes(a_imgs, R['imgs'])
    return dict(record=rec_addr, animsets=a_sets, anims=a_anims, coll=a_coll, imgs=a_imgs, end=pos, stats=R['stats'], header=rec['Header'])
