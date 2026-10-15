"""kcheck.py - E20.c1b: checks of the oracle itself that are not mutation tests: the load-delay check and the instruction coverage of the functions under test."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import lib

# (name, start, end exclusive) - function bodies as delimited by their `jr ra` (explore6 listing of the disassembly)
RANGES = [
    ('ComputeNewHp 0x8003A374', 0x8003A374, 0x8003A59C),
    ('ResolveBalanceTarget 0x8004464C', 0x8004464C, 0x80044820),
    ('UpdateItemEffectState 0x800307E8 (records, sheet; debug printing from 0x80030D40)', 0x800307E8, 0x80030D40),
    ('UpdateItemEffectState 0x80030D40-0x80030FC4 (debug printing only, gated by [0x800DC058] < 0)', 0x80030D40, 0x80030FC4),
    ('0x800445C0 item record chooser', 0x800445C0, 0x80044650),
    ('0x8004E030 GetItemIdFromCurrentWeapon', 0x8004E030, 0x8004E0F8),
    ('0x8004E0F8 SetItemIdFromCurrentItemId', 0x8004E0F8, 0x8004E168),
    ('0x8004E18C GetItemIdFromSlotId', 0x8004E18C, 0x8004E260),
    ('0x8004E428 GetNumberOfItem', 0x8004E428, 0x8004E484),
    ('0x8004E484 SetPlayerWeaponId', 0x8004E484, 0x8004E4D8),
    ('0x8004E4D8 SetCurrentItemId', 0x8004E4D8, 0x8004E530),
    ('0x8004E530 AddItem', 0x8004E530, 0x8004E5BC),
    ('0x8004205C program runner (loop and dispatch)', 0x8004205C, 0x800423A8),
    ('0x8003C914 search entry', 0x8003C914, 0x8003C954),
    ('0x8003C954 search (the codes used: record id, 0x80, 0x81, 0x82)', 0x8003C954, 0x8003CF20),
    ('0x80039BD0 map record lookup', 0x80039BD0, 0x80039C38),
    ('0x71 handler 0x8003FA10', 0x8003FA10, 0x8003FA24),
    ('0x86 handler 0x8003FFD4', 0x8003FFD4, 0x80040048),
    ('0x87 handler 0x80040048', 0x80040048, 0x80040110),
    ('0x95 handler 0x800407C0', 0x800407C0, 0x800409A8),
]


def coverage(cov):
    lines = ['# executed instructions of the functions under test (union over every machine of the baseline runs, emu.py); an instruction is listed as not executed when no run reached it']
    ok = True
    for name, a, b in RANGES:
        total = (b - a) // 4
        miss = [pc for pc in range(a, b, 4) if pc not in cov]
        lines.append('%-92s %4d of %4d executed' % (name, total - len(miss), total))
        # merged ranges of unexecuted instructions
        i = 0
        out = []
        while i < len(miss):
            j = i
            while j + 1 < len(miss) and miss[j + 1] == miss[j] + 4:
                j += 1
            out.append('0x%08X' % miss[i] if i == j else '0x%08X-0x%08X' % (miss[i], miss[j]))
            i = j + 1
        if out:
            lines.append('      not executed: ' + ', '.join(out))
    return lines


def load_delay(cov):
    LOADS = (0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26)

    def reads(w):
        op = w >> 26
        rs, rt = (w >> 21) & 31, (w >> 16) & 31
        if op == 0:
            fn = w & 63
            if fn in (0x00, 0x02, 0x03):
                return {rt}
            if fn in (0x08, 0x09, 0x11, 0x13):
                return {rs}
            if fn in (0x10, 0x12, 0x0C, 0x0D):
                return set()
            return {rs, rt}
        if op == 1:
            return {rs}
        if op in (2, 3, 0xF):
            return set()
        if op in (4, 5):
            return {rs, rt}
        if op in (6, 7):
            return {rs}
        if op in (0x22, 0x26):
            return {rs, rt}
        if 0x20 <= op <= 0x26:
            return {rs}
        if 0x28 <= op <= 0x2E:
            return {rs, rt}
        return {rs}

    def is_branch(w):
        op = w >> 26
        return op in (1, 2, 3, 4, 5, 6, 7) or (op == 0 and (w & 63) in (8, 9))

    def target(a, w):
        op = w >> 26
        if op in (1, 4, 5, 6, 7):
            imm = w & 0xFFFF
            simm = imm - 0x10000 if imm & 0x8000 else imm
            return a + 4 + (simm << 2)
        if op in (2, 3):
            return ((a + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
        return None
    nloads = 0
    bad = []
    for pc in sorted(cov):
        if not (0x80020000 <= pc < 0x800CA800):
            continue
        w = lib.u32(pc)
        if (w >> 26) not in LOADS:
            continue
        dst = (w >> 16) & 31
        if dst == 0:
            continue
        nloads += 1
        succ = [pc + 4]
        pw = lib.u32(pc - 4)
        if is_branch(pw):
            t = target(pc - 4, pw)
            if t is not None:
                succ.append(t)
            elif (pw >> 26) == 0:
                succ.append(None)
        for sx in succ:
            if sx is None:
                bad.append((pc, 'load in a jr/jalr delay slot', w, 0, 0))
                continue
            nw = lib.u32(sx)
            pair = (w >> 26) == 0x22 and (nw >> 26) == 0x26 and ((nw >> 16) & 31) == dst
            if dst in reads(nw) and not pair:
                bad.append((pc, 'next uses the loaded register', w, sx, nw))
    L = ['executed distinct instructions: %d; executed loads checked: %d; loaded register read by the next instruction: %d' % (len(cov), nloads, len(bad))]
    for b in bad:
        L.append('  HAZARD load %08x: %s (load word %08x, next %08x word %08x)' % b)
    L.append('VERDICT: ' + ('no executed load is followed by a use of its register: ignoring the load delay slot does not change the result' if not bad else 'HAZARDS FOUND'))
    return L, not bad
