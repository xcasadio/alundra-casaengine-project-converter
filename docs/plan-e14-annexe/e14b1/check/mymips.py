"""mymips.py - an independent minimal MIPS-I interpreter written from scratch for the critic pass (does not use the e14b1 emulator).
Loads the PS-X EXE text segment at 0x80020000, sparse RAM, delay slots for branches/jumps, hooks by PC (stubs).
Load-delay slots are NOT modelled (the compiled code was checked to have a nop / independent instruction after every load)."""
import struct
import exelib as X

M32 = 0xFFFFFFFF


def s32(x):
    x &= M32
    return x - (1 << 32) if x & 0x80000000 else x


class Halt(Exception):
    pass


class Mach:
    def __init__(self):
        self.pages = {}
        self.r = [0] * 32
        self.hi = 0
        self.lo = 0
        self.hooks = {}
        self.icount = 0
        self.cov = set()
        base = 0x80020000
        data = X.DATA[0x800:0x800 + X.T_SIZE]
        for i in range(0, len(data), 4096):
            self.pages[(base + i) >> 12] = bytearray(data[i:i + 4096].ljust(4096, b'\0'))
        # pre-decode text for speed
        self.text_base = base
        self.text_words = struct.unpack('<%dI' % (len(data) // 4), data[: (len(data) // 4) * 4])

    # --- memory
    def _pg(self, a):
        k = (a & M32) >> 12
        p = self.pages.get(k)
        if p is None:
            p = bytearray(4096)
            self.pages[k] = p
        return p

    def rb(self, a):
        return self._pg(a)[a & 0xFFF]

    def wb(self, a, v):
        self._pg(a)[a & 0xFFF] = v & 0xFF

    def rh(self, a):
        return self.rb(a) | (self.rb(a + 1) << 8)

    def wh(self, a, v):
        self.wb(a, v)
        self.wb(a + 1, v >> 8)

    def rw(self, a):
        p = self._pg(a)
        o = a & 0xFFF
        if o <= 4092:
            return p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)
        return self.rb(a) | (self.rb(a + 1) << 8) | (self.rb(a + 2) << 16) | (self.rb(a + 3) << 24)

    def ww(self, a, v):
        for i in range(4):
            self.wb(a + i, v >> (8 * i))

    def fetch(self, pc):
        i = (pc - self.text_base) >> 2
        if 0 <= i < len(self.text_words):
            return self.text_words[i]
        return self.rw(pc)

    # --- execution
    def call(self, addr, *args, sp=0x801FFF00, maxins=200000):
        r = self.r
        for i in range(32):
            r[i] = 0
        for i, a in enumerate(args[:4]):
            r[4 + i] = a & M32
        r[29] = sp
        SENT = 0xDEAD0000
        r[31] = SENT
        pc = addr
        n = 0
        while pc != SENT:
            n += 1
            if n > maxins:
                raise Halt('too many instructions at %x' % pc)
            if pc in self.hooks:
                self.hooks[pc](self)
                pc = r[31]
                continue
            pc = self._step(pc)
        return r[2] & M32

    def _step(self, pc):
        """execute the instruction at pc and its delay slot if any; return the next pc"""
        self.cov.add(pc)
        w = self.fetch(pc)
        op = w >> 26
        npc = pc + 4
        # branch/jump instructions
        if op in (0x04, 0x05, 0x06, 0x07, 0x01, 0x02, 0x03) or (op == 0 and (w & 0x3F) in (0x08, 0x09)):
            r = self.r
            rs = (w >> 21) & 31
            rt = (w >> 16) & 31
            imm = w & 0xFFFF
            if imm & 0x8000:
                imm -= 0x10000
            target = None
            link = None
            if op == 0x04:
                if s32(r[rs]) == s32(r[rt]):
                    target = pc + 4 + (imm << 2)
            elif op == 0x05:
                if s32(r[rs]) != s32(r[rt]):
                    target = pc + 4 + (imm << 2)
            elif op == 0x06:
                if s32(r[rs]) <= 0:
                    target = pc + 4 + (imm << 2)
            elif op == 0x07:
                if s32(r[rs]) > 0:
                    target = pc + 4 + (imm << 2)
            elif op == 0x01:
                if rt == 0x00:      # bltz
                    if s32(r[rs]) < 0:
                        target = pc + 4 + (imm << 2)
                elif rt == 0x01:    # bgez
                    if s32(r[rs]) >= 0:
                        target = pc + 4 + (imm << 2)
                else:
                    raise Halt('regimm %x at %x' % (rt, pc))
            elif op == 0x02:
                target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
            elif op == 0x03:
                target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
                link = pc + 8
            elif op == 0 and (w & 0x3F) == 0x08:   # jr
                target = r[rs] & M32
            elif op == 0 and (w & 0x3F) == 0x09:   # jalr
                target = r[rs] & M32
                link = pc + 8
            # delay slot executes before the link? (jal sets ra at the jump; the slot may read ra only in weird code)
            if link is not None:
                saved_target = target
                rd = (w >> 11) & 31 if op == 0 else 31
                self.r[rd] = link & M32
            self._exec_slot(pc + 4)
            if target is not None:
                # a hooked call target: run the hook right after the slot, as a call
                return target & M32
            return pc + 8
        self._exec(w, pc)
        return npc

    def _exec_slot(self, pc):
        self.cov.add(pc)
        w = self.fetch(pc)
        self._exec(w, pc)

    def _exec(self, w, pc):
        r = self.r
        op = w >> 26
        rs = (w >> 21) & 31
        rt = (w >> 16) & 31
        rd = (w >> 11) & 31
        sh = (w >> 6) & 31
        fn = w & 0x3F
        imm = w & 0xFFFF
        simm = imm - 0x10000 if imm & 0x8000 else imm
        if op == 0:
            a = r[rs] & M32
            b = r[rt] & M32
            if fn == 0x00:
                v = (b << sh) & M32
            elif fn == 0x02:
                v = b >> sh
            elif fn == 0x03:
                v = (s32(b) >> sh) & M32
            elif fn == 0x04:
                v = (b << (a & 31)) & M32
            elif fn == 0x06:
                v = b >> (a & 31)
            elif fn == 0x07:
                v = (s32(b) >> (a & 31)) & M32
            elif fn in (0x20, 0x21):
                v = (a + b) & M32
            elif fn in (0x22, 0x23):
                v = (a - b) & M32
            elif fn == 0x24:
                v = a & b
            elif fn == 0x25:
                v = a | b
            elif fn == 0x26:
                v = a ^ b
            elif fn == 0x27:
                v = ~(a | b) & M32
            elif fn == 0x2A:
                v = 1 if s32(a) < s32(b) else 0
            elif fn == 0x2B:
                v = 1 if a < b else 0
            elif fn == 0x10:
                v = self.hi
            elif fn == 0x12:
                v = self.lo
            elif fn == 0x18:     # mult
                p = s32(a) * s32(b)
                self.lo = p & M32
                self.hi = (p >> 32) & M32
                return
            elif fn == 0x19:     # multu
                p = a * b
                self.lo = p & M32
                self.hi = (p >> 32) & M32
                return
            else:
                raise Halt('special fn %x at %x' % (fn, pc))
            if rd:
                r[rd] = v & M32
            return
        if op == 0x09 or op == 0x08:
            v = (r[rs] + simm) & M32
        elif op == 0x0A:
            v = 1 if s32(r[rs]) < simm else 0
        elif op == 0x0B:
            v = 1 if (r[rs] & M32) < (simm & M32) else 0
        elif op == 0x0C:
            v = r[rs] & imm
        elif op == 0x0D:
            v = r[rs] | imm
        elif op == 0x0E:
            v = r[rs] ^ imm
        elif op == 0x0F:
            v = (imm << 16) & M32
        elif op == 0x23:
            v = self.rw((r[rs] + simm) & M32)
        elif op == 0x24:
            v = self.rb((r[rs] + simm) & M32)
        elif op == 0x20:
            v = self.rb((r[rs] + simm) & M32)
            if v & 0x80:
                v |= 0xFFFFFF00
        elif op == 0x25:
            v = self.rh((r[rs] + simm) & M32)
        elif op == 0x21:
            v = self.rh((r[rs] + simm) & M32)
            if v & 0x8000:
                v |= 0xFFFF0000
        elif op == 0x2B:
            self.ww((r[rs] + simm) & M32, r[rt])
            return
        elif op == 0x28:
            self.wb((r[rs] + simm) & M32, r[rt])
            return
        elif op == 0x29:
            self.wh((r[rs] + simm) & M32, r[rt])
            return
        else:
            raise Halt('opcode %x at %x' % (op, pc))
        if rt:
            r[rt] = v & M32
