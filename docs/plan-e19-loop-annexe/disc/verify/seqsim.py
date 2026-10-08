"""Independent re-implementation (read-only) of the ALUN_CD.EXE (France) SEQ driver, written from the disassembly:
  0x8008BCC4  per-frame tick (variant 'bin': exact binary loop; variant 'ana': analyser SoundManager.cs:1321-1384,
              which re-checks the STALE tempo 'tempo < delay' at the top of each inner iteration)
  0x8008BDD0  event reader (status in +0x11, channel in +0x12; 0xF0 -> status 0xFF, channel 0x0F; 0x80 status ignored)
  0x8008C1B8  CC dispatcher (table 0x8002A83C): 6 -> 0x8008CC70, 98 -> 0x8008C918, 99 -> 0x8008CA40, 100 -> 0x8008CB88
  0x8008CA40  CC 99: 0x14 loop start / 0x1E loop end / other (+0x16 = v, +0x2A++)
  0x8008C96C / 0x8008CD08  loop-count latch (CC 98 / CC 6) when +0x27 == 1 and +0x10 == 0
  0x8008D568  meta: 0x51 tempo, 0x2F end of track (l_count = 1 from PlaySeq(seq,1,1) 0x8008F10C -> stop)
  0x8008D8D0  delta = 10 * VLQ
Does NOT import v_seq / loopmodel.
"""
import struct, json, sys

EXE = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE"
SND = r"D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\DATA\SOUND.BIN"
BGMJSON = r"D:\development\repo\alundra-casaengine-project-converter\data-extracted\sound\bgm.json"
RATE = 0x3C

_exe = open(EXE, "rb").read()
_snd = open(SND, "rb").read()
_taddr = struct.unpack_from("<I", _exe, 0x18)[0]


def u32(a):
    return struct.unpack_from("<I", _exe, 0x800 + a - _taddr)[0]


def seq_data(n):
    a = u32(0x800A7F90 + 12 * n)
    b = u32(0x800A7F90 + 12 * n + 4)
    return a, _snd[a:b]


class Stop(Exception):
    pass


class Sim:
    def __init__(self, data, variant="bin", lcount=1):
        self.d = data
        self.variant = variant
        assert data[:4] in (b"pQES", b"SEQp")
        self.res = (data[8] << 8) | data[9]
        tv = (data[10] << 16) | (data[11] << 8) | data[12]
        bpm = 0x03938700 // tv
        if 0x03938700 % tv > (tv >> 1):
            bpm += 1
        self.p = 15
        self.D = self.vlq()
        self.start = self.p
        self.loop = self.p
        prod = self.res * bpm * 10
        if prod < RATE * 60:
            raise RuntimeError("predelay")
        self.T = prod // (RATE * 60) + (1 if prod % (RATE * 60) > RATE * 30 else 0)
        self.st = 0
        self.ch = 0
        self.f16 = 0
        self.f27 = 0
        self.f10 = 0
        self.cnt = 0
        self.lcount = lcount
        self.played = 0
        self.alive = True
        self.frame = 0
        # musical bookkeeping
        self.cs = {}            # (ch, name) -> value
        self.on = {}            # (ch, key) -> (frame, pos, vel)
        self.ev = []            # (frame, pos, kind, payload)
        self.jumps = []         # frames of loop jumps
        self.jump_snap = []     # (cs copy, on copy, T)
        self.ls_marker = None   # (frame, pos)
        self.loop_first = None  # frame where the loop-position event first runs
        self.entry_snap = None
        self.count_ctl = None
        self.end_frame = None
        self.tempos = []

    def vlq(self):
        b = self.d[self.p]; self.p += 1
        if b == 0:
            return 0
        v = b
        if b & 0x80:
            v &= 0x7F
            while True:
                b = self.d[self.p]; self.p += 1
                v = v * 128 + (b & 0x7F)
                if not b & 0x80:
                    break
        return v * 10

    def snap(self):
        return (dict(self.cs), dict(self.on), self.T)

    # 0x8008BDD0
    def event(self):
        d = self.d
        start = self.p
        if self.loop_first is None and self.ls_marker is not None and start == self.loop:
            self.loop_first = self.frame
            self.entry_snap = self.snap()
            self.entry_evi = len(self.ev)
        b = d[self.p]; self.p += 1
        if b & 0x80:
            self.ch = b & 0x0F
            t = b & 0xF0
            if t == 0x90:
                self.st = 0x90
                k = d[self.p]; v = d[self.p + 1]; self.p += 2
                self.note(start, k, v)
                return
            if t == 0xB0:
                self.st = 0xB0
                c = d[self.p]; self.p += 1
                self.cc(start, c)
                return
            if t == 0xC0:
                self.st = 0xC0
                v = d[self.p]; self.p += 1
                self.cs[(self.ch, "prog")] = v
                self.ev.append((self.frame, start, "prog", (self.ch, v)))
                self.D = self.vlq()
                return
            if t == 0xE0:
                self.st = 0xE0
                self.p += 1                      # 0x8008BF34: skip LSB
                v = d[self.p]; self.p += 1       # 0x8008D528
                self.cs[(self.ch, "bend")] = v
                self.ev.append((self.frame, start, "bend", (self.ch, v)))
                self.D = self.vlq()
                return
            if t == 0xF0:
                self.st = 0xFF                   # 0x8008BF4C
                mt = d[self.p]; self.p += 1
                self.meta(start, mt)
                return
            raise RuntimeError("status %02x at %d (binary would return without delta)" % (b, start))
        # running status 0x8008BF74
        if self.st == 0x90:
            k = b; v = d[self.p]; self.p += 1
            self.note(start, k, v)
            return
        if self.st == 0xB0:
            self.cc(start, b)
            return
        if self.st == 0xC0:
            self.cs[(self.ch, "prog")] = b
            self.ev.append((self.frame, start, "prog", (self.ch, b)))
            self.D = self.vlq()
            return
        if self.st == 0xE0:
            v = d[self.p]; self.p += 1
            self.cs[(self.ch, "bend")] = v
            self.ev.append((self.frame, start, "bend", (self.ch, v)))
            self.D = self.vlq()
            return
        if self.st == 0xFF:
            self.meta(start, b)
            return
        raise RuntimeError("running status %02x" % self.st)

    def note(self, start, k, v):
        self.D = self.vlq()                      # 0x8008BFD0 delta read before the key event
        k &= 0x7F; v &= 0x7F
        ch = self.ch
        if v:
            self.on[(ch, k)] = (self.frame, start, v)
            self.ev.append((self.frame, start, "on", (ch, k, v, self.cs.get((ch, 7)), self.cs.get((ch, 10)),
                                                      self.cs.get((ch, "prog")), self.cs.get((ch, "bend")))))
        else:
            self.on.pop((ch, k), None)
            self.ev.append((self.frame, start, "off", (ch, k)))

    def cc(self, start, c):
        d = self.d
        v = d[self.p]; self.p += 1               # 0x8008C22C
        ch = self.ch
        if c == 99:
            if v == 0x14:
                self.f16 = 0x14; self.f27 = 1
                self.D = self.vlq()
                self.loop = self.p
                self.ls_marker = (self.frame, start)
                self.ev.append((self.frame, start, "LS", ch))
                return
            if v == 0x1E:
                self.f16 = 0x1E
                self.ev.append((self.frame, start, "LE", (ch, self.cnt)))
                if self.cnt == 0:
                    self.f10 = 0
                    self.D = self.vlq()
                    return
                if self.cnt < 0x7F:
                    self.cnt -= 1
                    self.D = self.vlq()
                    if self.cnt:
                        self.do_jump()
                    else:
                        self.f10 = 0
                    return
                self.vlq()
                self.D = 0
                self.do_jump()
                return
            self.f16 = v
            self.cs[(ch, "nrpn99")] = v
            self.D = self.vlq()
            return
        if c in (98, 6):
            if self.f27 == 1 and self.f10 == 0:
                self.cnt = v; self.f10 = 1
                self.count_ctl = (c, v, self.frame)
                self.ev.append((self.frame, start, "CNT", (c, v)))
                self.D = self.vlq()
                return
            self.cs[(ch, c)] = v
            self.ev.append((self.frame, start, "cc", (ch, c, v)))
            self.D = self.vlq()
            return
        self.cs[(ch, c)] = v
        self.ev.append((self.frame, start, "cc", (ch, c, v)))
        self.D = self.vlq()

    def do_jump(self):
        self.jumps.append(self.frame)
        self.jump_snap.append(self.snap())
        self.jump_evi = getattr(self, "jump_evi", []) + [len(self.ev)]
        self.p = self.loop

    def meta(self, start, mt):
        d = self.d
        if mt == 0x51:
            tv = (d[self.p] << 16) | (d[self.p + 1] << 8) | d[self.p + 2]; self.p += 3
            bpm = 60000000 // tv
            prod = self.res * bpm * 10
            if prod < RATE * 60:
                raise RuntimeError("predelay tempo")
            T = prod // (RATE * 60)
            if prod % (RATE * 60) > RATE * 30:
                T += 1
            self.T = T
            self.cs[("tempo",)] = T
            self.tempos.append((self.frame, start, T))
            self.D = self.vlq()
            return
        if mt == 0x2F:
            self.played += 1
            if self.lcount == 0 or self.played < self.lcount:
                raise RuntimeError("restart path not modelled")
            self.alive = False
            self.end_frame = self.frame
            self.ev.append((self.frame, start, "EOT", None))
            self.D = self.T                      # 0x8008D8AC
            return
        raise RuntimeError("meta %02x (binary returns without delta)" % mt)

    # 0x8008BCC4
    def tick(self):
        if not self.alive:
            return
        T = self.T
        delay = self.D
        if delay - T > 0:
            self.D = delay - T
            return
        s0 = delay
        if self.variant == "bin":
            while True:
                self.event()
                while self.D == 0:
                    self.event()
                s0 += self.D
                if s0 < self.T:
                    continue
                break
            self.D = s0 - self.T
        else:  # analyser
            while True:
                if T < s0:
                    return
                self.event()
                while self.alive and self.D == 0:
                    self.event()
                if not self.alive:
                    return
                s0 += self.D
                if not (s0 < self.T):
                    break
            self.D = s0 - self.T


def run(n, variant="bin", njumps=3, max_frames=60 * 60 * 40):
    off, data = seq_data(n)
    s = Sim(data, variant)
    for f in range(max_frames):
        s.frame = f
        s.tick()
        if len(s.jumps) >= njumps or not s.alive:
            break
    return off, s


if __name__ == "__main__":
    bj = {r["SoundIndex"]: r for r in json.load(open(BGMJSON))}
    idxs = [int(a) for a in sys.argv[1:]] or list(range(1, 47))
    for n in idxs:
        off, s = run(n)
        off2, a = run(n, "ana")
        fr = bj.get(n, {}).get("Frames")
        print(n, hex(off), "T0", s.T, "LS", s.loop_first, "J", s.jumps, "end", s.end_frame, "cnt", s.count_ctl,
              "| ana J", a.jumps, "end", a.end_frame, "| bgm.json Frames", fr, bj.get(n, {}).get("LoopDetected"))
