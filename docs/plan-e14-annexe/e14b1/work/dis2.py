"""Annotated MIPS disassembler for ALUN_CD.EXE (France). Read-only. Scratch tool of slice e14b1.
Usage: python -B dis2.py FUNC_START [--end ADDR]   : follows the control flow of one function (not jal), prints an annotated listing
       python -B dis2.py --range START END         : linear listing
Annotations: entity field names (offset), helper names (jal), lui/addiu table addresses, inline RNG draws (N), jump tables.
"""
import sys
import struct
import capstone
import exelib

md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True

FIELD = {
    0x0C: 'Parent', 0x10: 'Status', 0x14: 'Hp', 0x20: 'ctl20', 0x28: 'Platform', 0x3C: 'ContentsItemId', 0x48: 'ref48',
    0x4C: 'ProgIdxA', 0x50: 'ProgIdxB', 0x54: 'ProgIdxC', 0x58: 'ProgIdxD', 0x5C: 'ProgIdxE', 0x60: 'ProgIdxF', 0x64: 'SpriteRecord',
    0x68: 'ptr68', 0x6C: 'Flags', 0x70: 'SprIdxA', 0x74: 'SprIdxB', 0x78: 'SprIdxC', 0x7C: 'SprIdxD', 0x80: 'SprIdxE', 0x84: 'SprIdxF',
    0x88: 'TargetAnim', 0x8C: 'TargetDir', 0x90: 'CurAnim', 0x94: 'CurDir', 0x98: 'AnimDir', 0xA8: 'animA8', 0xAC: 'AnimEndFlag', 0xB0: 'animB0',
    0xB4: 'AnimFlagsB4', 0xB8: 'ForceZ', 0xBC: 'TForceX', 0xC0: 'TForceY', 0xC4: 'ForceX', 0xC8: 'ForceY', 0xD4: 'FStepX', 0xD8: 'FStepY',
    0x114: 'PosX', 0x118: 'PosY', 0x11C: 'PosZ', 0x120: 'TileX', 0x124: 'TileY', 0x128: 'TileZ', 0x134: 'FloorHeight', 0x138: 'TerrainHeight',
    0x13C: 'ForceAdjusted', 0x144: 'IsOnGround', 0x158: 'MapH0', 0x15C: 'MapH1', 0x160: 'MapH2', 0x164: 'MapH3',
    0x1B8: 'ActiveEffect', 0x1C8: 'CurAttack', 0x1CC: 'Blink1CC', 0x1D0: 'Lock1D0', 0x1D4: 'FrameBox', 0x220: 'HitCounter', 0x224: 'TouchingEntity',
    0x228: 'EventTrigger', 0x274: 'Bytes0', 0x275: 'Bytes1', 0x276: 'Bytes2', 0x277: 'Bytes3', 0x278: 'Bytes4', 0x279: 'Bytes5',
    0x27A: 'Bytes6', 0x27B: 'Bytes7', 0x280: 'AIValues0', 0x282: 'AIValues1', 0x284: 'w284', 0x288: 'w288', 0x28C: 'w28C',
}

# helper names (jal targets), grown while the slice proceeds
HELP = {
    0x8007FE20: 'CalculateEntityRelativePosition', 0x8002E814: 'GetDirectionToTarget', 0x800805C8: 'TryAttackPlayerFront',
    0x8008048C: 'TryAttackPlayer2', 0x80080144: 'HandleAnimationDirection', 0x800490FC: 'PlaySoundEffect', 0x8003A374: 'ComputeNewHp',
    0x8003A59C: 'DestroyEntity', 0x8003BDD8: 'CreateEffectEntity', 0x8003BE74: 'CreateAttachedEffectCore', 0x8003A614: 'CreateAttachedEffect',
    0x80080260: 'CanMoveForward', 0x800808DC: 'FUN_800808DC', 0x80080030: 'SetEntityRandomDirection', 0x80080704: 'TryAttackPlayer',
    0x8003AB54: 'GetEntityTileHeight', 0x8007FEF8: 'StartFlying', 0x8007FE8C: 'FUN_8007FE8C', 0x80039F58: 'SpawnWarpEntity',
    0x80032B90: 'SpawnEntityContents', 0x80032A40: 'ChooseRandomlyAnItemInit',
}
TABLES = {
    0x80098708: 'RNG_state', 0x80127D30: 'EntityTable(hero slot0)', 0x80028B54: 'CardinalDirTable', 0x80028B34: 'QuarterTurnTable',
    0x80022488: 'DirQuadTable', 0x80023654: 'DirForceTableX?', 0x80023694: 'DirForceTableY?', 0x800DC5A0: 'MapId',
}
SPECIAL_REG = {16: 's0', 17: 's1', 18: 's2', 19: 's3', 20: 's4', 21: 's5', 22: 's6', 23: 's7'}


def regnum(name):
    return capstone.mips.MIPS_REG_ZERO if name == 'zero' else None


def disasm_at(addr):
    o = exelib.off(addr)
    for ins in md.disasm(exelib.DATA[o:o + 4], addr):
        return ins
    return None


def w(addr):
    return exelib.u32(addr)


def branch_target(ins):
    """return (kind, target) for control-flow instructions; kind in 'b','j','jr','jal','jalr'"""
    m = ins.mnemonic
    ops = ins.op_str.split(',')
    if m in ('beq', 'bne', 'beqz', 'bnez', 'bgez', 'bgtz', 'blez', 'bltz', 'bc1f', 'bc1t', 'bgezal', 'bltzal', 'b'):
        return 'b', int(ops[-1].strip(), 16)
    if m == 'j':
        return 'j', int(ops[0].strip(), 16)
    if m == 'jal':
        return 'jal', int(ops[0].strip(), 16)
    if m == 'jr':
        return 'jr', ops[0].strip()
    if m == 'jalr':
        return 'jalr', ops
    return None, None


def find_jump_table(addr_jr, lo_limit):
    """scan back from a `jr $reg` for lui+addiu table base and sltiu bound; returns (base, n) or None"""
    n = None
    base = None
    luis = {}
    a = addr_jr - 4
    insns = []
    while a >= lo_limit and a >= addr_jr - 80:
        insns.append(disasm_at(a))
        a -= 4
    insns.reverse()
    for ins in insns:
        m, o = ins.mnemonic, [x.strip() for x in ins.op_str.split(',')]
        if m == 'sltiu':
            n = int(o[2], 0)
        if m == 'lui':
            luis[o[0]] = int(o[1], 0) << 16
        if m == 'addiu' and o[1] in luis:
            base = (luis[o[1]] + int(o[2], 0)) & 0xFFFFFFFF
    if base is not None and n is not None:
        return base, n
    return None


def discover(start, end_hint=None, tables=None):
    """control-flow walk of one function; returns sorted list of instruction addresses, labels, jump tables"""
    tables = dict(tables or {})
    seen = set()
    labels = set()
    jts = {}
    work = [start]
    maxa = start
    while work:
        a = work.pop()
        while True:
            if a in seen:
                break
            if end_hint is not None and a >= end_hint:
                break
            ins = disasm_at(a)
            if ins is None:
                break
            seen.add(a)
            maxa = max(maxa, a)
            kind, tgt = branch_target(ins)
            if kind is None:
                a += 4
                continue
            # delay slot
            seen.add(a + 4)
            maxa = max(maxa, a + 4)
            if kind == 'b':
                labels.add(tgt)
                work.append(tgt)
                a += 8
                continue
            if kind == 'j':
                labels.add(tgt)
                work.append(tgt)
                break
            if kind == 'jr':
                if tgt == 'ra':
                    break
                jt = tables.get(a) or find_jump_table(a, start)
                if jt:
                    base, n = jt
                    jts[a] = (base, n)
                    for i in range(n):
                        t = w(base + 4 * i)
                        labels.add(t)
                        work.append(t)
                break
            if kind in ('jal', 'jalr'):
                a += 8
                continue
    return sorted(seen), labels, jts


def fmt(ins, state):
    """text for one instruction + annotation; state = dict reg->lui value"""
    m = ins.mnemonic
    ops = [x.strip() for x in ins.op_str.split(',')] if ins.op_str else []
    note = ''
    # memory operand: off(reg)
    if m in ('lw', 'sw', 'lbu', 'lb', 'sb', 'lhu', 'lh', 'sh') and len(ops) == 2 and '(' in ops[1]:
        offs, rg = ops[1].split('(')
        rg = rg.rstrip(')').lstrip('$')
        offv = int(offs, 0) if offs else 0
        if rg == 's0' or rg in ('s1', 's2', 's3', 's4', 'v0', 'v1', 'a0', 'a1', 'a2', 'a3', 't0', 't1', 't2', 't3', 'fp'):
            if (offv & 0xFFFF) in FIELD or offv in FIELD:
                note = '%s.%s' % (rg, FIELD.get(offv, ''))
        if rg in state:
            ea = (state[rg] + offv) & 0xFFFFFFFF
            note = (note + ' ' if note else '') + '[0x%08X%s]' % (ea, ('=' + TABLES[ea]) if ea in TABLES else '')
        if rg == 'sp':
            note = 'sp+0x%X' % offv
    elif m == 'lui':
        state[ops[0].lstrip('$')] = int(ops[1], 0) << 16
    elif m in ('addiu', 'ori') and len(ops) == 3 and ops[1].lstrip('$') in state:
        v = (state[ops[1].lstrip('$')] + int(ops[2], 0)) & 0xFFFFFFFF if m == 'addiu' else state[ops[1].lstrip('$')] | int(ops[2], 0)
        state[ops[0].lstrip('$')] = v
        note = '=0x%08X%s' % (v, (' ' + TABLES[v]) if v in TABLES else '')
    elif m == 'jal':
        t = int(ops[0], 16)
        note = HELP.get(t, 'FUN_%08X' % t)
    return note


def listing(addrs, labels, jts, start=None):
    state = {}
    out = []
    ind = set(labels)
    for a in addrs:
        ins = disasm_at(a)
        if a in ind:
            out.append('L_%08X:' % a)
            state = {}
        note = fmt(ins, state)
        # RNG marker
        if ins.mnemonic == 'lui' and ins.op_str.endswith('0x7d2b'):
            note = (note + ' ' if note else '') + '<<RNG step begins>>'
        if ins.mnemonic == 'multu':
            note = (note + ' ' if note else '') + '<<multu: RNG scale (look at the addiu N above)>>'
        k, t = branch_target(ins)
        text = '%08X  %08x  %-8s %s' % (a, w(a), ins.mnemonic, ins.op_str)
        if k == 'b' or k == 'j':
            text += '   -> L_%08X' % t
        out.append('%-58s ; %s' % (text, note) if note else text)
        if a in jts:
            base, n = jts[a]
            out.append('    ; jump table @0x%08X (%d): %s' % (base, n, ' '.join('%X' % w(base + 4 * i) for i in range(n))))
    return out


def main():
    args = sys.argv[1:]
    if args[0] == '--range':
        s, e = int(args[1], 16), int(args[2], 16)
        addrs = list(range(s, e, 4))
        print('\n'.join(listing(addrs, set(), {})))
        return
    start = int(args[0], 16)
    end = None
    if '--end' in args:
        end = int(args[args.index('--end') + 1], 16)
    addrs, labels, jts = discover(start, end)
    print('; function 0x%08X..0x%08X (%d words)' % (addrs[0], addrs[-1] + 4, len(addrs)))
    print('\n'.join(listing(addrs, labels, jts)))


if __name__ == '__main__':
    main()
