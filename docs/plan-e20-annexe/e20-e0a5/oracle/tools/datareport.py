"""datareport.py - E20.c1: what the DECODED DATA say (not the machine) about the animations and frame boxes used by the scenarios: the hero's basic-sword ground swing (anim 0x10) and hurt
state (0x31), and every animation of the Beannoid (map 14, SpriteRecords[79], balance index 335).  Data level: data-extracted/data/map_alundra.json and map_14.json, the same records
bank.py lays out in RAM for the emulated binary.  Box values are the raw sbyte offsets / byte sizes of the frame collision record (the binary converts them: offsets << 16, sizes (n << 16) - 1).

Animation-set bytes as the binary reads them (0x80038AB4): +0..+7 four u16 frame-list offsets (down, up, left, right), +0xA s16 -> entity +0xF8 (0x80038B5C), +0xC -> sound id (0x80038BB4),
+0xD -> entity +0xB4 = "AnimFlags" (0x80038B68 `9082000d`, 0x80038B70 `ae0200b4`).
Frame time: the delay byte & 0x7F is the number of ticks the frame is shown (INFERENCE from the traces: hero anim 0x10 frame 0 = byte 132 = ticks 1-4 in out/H1.txt).  In the decoded JSON the byte 0xD is named `Acceleration` and the u16 at 0xA `IsZForceApplied`.
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bank
import c1world as C

DIRS = ('down', 'up', 'left', 'right')


def frames_line(an):
    out = []
    for i, f in enumerate(an['Frames']):
        if f['IsTransitionFrame']:
            out.append('f%d: transition (delay byte %d, chain/hold byte %d)' % (i, f['Delay'], f['TransformIndexLow']))
            continue
        cd = f.get('CollisionData')
        box = 'box (%d,%d,%d) %dx%dx%d' % (cd['OffsetX'], cd['OffsetY'], cd['OffsetZ'], cd['Width'], cd['Depth'], cd['Height']) if cd else 'no box'
        out.append('f%d: %d ticks (byte %d), %s' % (i, f['Delay'] & 0x7F, f['Delay'], box))
    return '; '.join(out)


def anim_lines(label, rec, ai, dirs=DIRS):
    s = rec['AnimSets'][ai]
    L = ['%s anim 0x%X: set byte 0xD (AnimFlags, JSON "Acceleration") = 0x%02X%s, s16 at 0xA (+0xF8, JSON "IsZForceApplied") = %d, u16 at 8 (JSON "Speed") = %d, sfx byte 0xC = %d'
         % (label, ai, s['Acceleration'], ' (bit 0x40 set: the hit pass skips this entity as a target)' if s['Acceleration'] & 0x40 else '', s['IsZForceApplied'], s['Speed'], s['Sfx'])]
    for d in dirs:
        an = s['PreloadedAnims'][d]
        L.append('   %-5s %s' % (DIRS[d], 'absent' if an is None else frames_line(an)))
    return L


def lines():
    hero = bank.load_hero_record()
    bean = C.map14().record
    L = ['# decoded data behind the scenarios (see the module docstring); not computed by the emulator']
    L.append('')
    L.append('## hero (map_alundra.json, Sector5Id 0): header flags (Ubuff[0..2]) = 0x%06X; offsets (%d,%d,%d) sizes %dx%dx%d'
             % (hero['Header']['Ubuff'][0] | hero['Header']['Ubuff'][1] << 8 | hero['Header']['Ubuff'][2] << 16, hero['Header']['OffsetX'], hero['Header']['OffsetY'], hero['Header']['OffsetZ'],
                hero['Header']['SizeX'], hero['Header']['SizeY'], hero['Header']['SizeZ']))
    for ai in (0, 0x10, 0x31):
        L += anim_lines('hero', hero, ai, dirs=(3,) if ai == 0x10 else (3,))
    L.append('')
    h = bean['Header']
    L.append('## Beannoid (map_14.json SpriteRecords[79], table index 79, balance index 79 | 0x100 = 335): header flags (Ubuff[0..2]) = 0x%06X; handler indices A,C,D,E,F = %d,%d,%d,%d,%d; offsets (%d,%d,%d) sizes %dx%dx%d'
             % (h['Ubuff'][0] | h['Ubuff'][1] << 8 | h['Ubuff'][2] << 16, h['ProgramLoad'], h['ProgramTick'], h['ProgramTouch'], h['ProgramDeactivate'], h['ProgramInteract'],
                h['OffsetX'], h['OffsetY'], h['OffsetZ'], h['SizeX'], h['SizeY'], h['SizeZ']))
    flagged = [ai for ai, s in enumerate(bean['AnimSets']) if s['Acceleration'] & 0x40]
    L.append('animation sets whose byte 0xD has bit 0x40: %s; sets without it: %s' % (flagged, [ai for ai in range(len(bean['AnimSets'])) if ai not in flagged]))
    for ai in range(len(bean['AnimSets'])):
        L += anim_lines('beannoid', bean, ai, dirs=(0,) if ai not in (0, 1, 6) else (0, 1, 2, 3))
    return L


def write(outdir):
    open(os.path.join(outdir, 'data-boxes.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines()) + '\n')


if __name__ == '__main__':
    print('\n'.join(lines()))
