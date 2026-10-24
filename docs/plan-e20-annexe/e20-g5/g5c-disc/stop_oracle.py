"""stop_oracle.py - StopSoundEffect (0x80049634, 36 words + the voice finder 0x80049714) AND the PlaySoundEffect front (0x800490FC, system-bank path) against Python models written from the disassembly, on the REAL code
(minimal MIPS interpreter of work/emu), with journalled libsnd stubs:
  0x80094f20 SsKeyOff(voice)           -> ('keyoff', voice)                       (the key-off of the SPU: a release, not a cut)
  0x80094660 SsUtKeyOn(vab, prog, tone, note, fine, voll, volr) -> ('keyon', vab, prog, tone, note) ; returns the lowest voice the python SPU has off, or -1
  0x8009860c SpuGetAllKeysStatus(buf)  -> 24 status bytes from the python SPU (0 off, 1 on, 3 release)
  0x80090370 tone attributes           -> 2 bytes written at buf+2, buf+3 (volume, pan) : deterministic filler
  0x80082918 sprintf / 0x800815ec print (warnings only) -> ignored
Scenarios: random sequences of ('play', id) ('stop', id) ('frame') ('end', voice) ('countdown', n) over a few sound ids of the REAL table (0x800A82E8 + 22*id, initialised in the EXE image).
After every operation the whole voice table (state bytes, ids +0x18, vab +0x78, tone +0xD8), the anti-duplicate table (0x80165028, 64 words) and the flags of the rows are compared with the model; the journals too.
Also: one-word mutants of the real Stop code (in the interpreter's RAM only) killed by the same vectors, and goldens (the classes of outcome).
Run: python -B -X utf8 stop_oracle.py [N] [mut] [gold]"""
import os, random, struct, sys, json, hashlib, collections
HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, 'work')
sys.path.insert(0, WORK)
sys.path.insert(0, os.path.join(WORK, 'emu'))
import capstone
from h import World, M32, s32
import emu as _emu

PLAY, PLAY_END = 0x800490FC, 0x80049634
STOP, STOP_END = 0x80049634, 0x80049714
FINDV, FINDV_END = 0x80049714, 0x8004974C
N = int(sys.argv[1]) if len(sys.argv) > 1 and sys.argv[1].isdigit() else 3000
DO_MUT = 'mut' in sys.argv
DO_GOLD = 'gold' in sys.argv

TABLE, ROW = 0x800A82E8, 22
VOICE = 0x80175858            # state bytes [0..23], then words id +0x18, vab +0x78, tone +0xD8 (4 bytes per voice)
ANTIDUP = 0x80165028          # 64 words
COUNTDOWN = 0x80175850        # g_soundEffectState
GROUP = 0x80173848
NV = 24
IDS = [25, 85, 2, 55, 24, 46, 98, 229, 1, 7]          # 25: max 1 loop; 85: 2 tones max 1; 2: max 2; 55: max 4; 98/229: group banks (Stop only); 1 and 7: Stop only

W = World()
m = W.m
J = []
spu = [0] * NV               # python SPU: 0 off, 1 on, 3 release


def keyoff(mm):
    v = s32(mm.r[4] << 16) >> 16
    J.append(('keyoff', v & 0xFFFF))
    if 0 <= v < NV and spu[v]:
        spu[v] = 3 if spu[v] == 1 else spu[v]
    mm.r[2] = 0


def keyon(mm):
    sp = mm.r[29]
    vab, prog, tone, note = s32(mm.r[4]) , s32(mm.r[5]), s32(mm.r[6]), s32(mm.r[7])
    J.append(('keyon', vab, prog, tone, note, mm.rw(sp + 0x10), mm.rw(sp + 0x14), mm.rw(sp + 0x18)))
    for i in range(NV):
        if spu[i] == 0:
            spu[i] = 1
            mm.r[2] = i
            return
    mm.r[2] = M32          # -1: no voice


def status(mm):
    buf = mm.r[4]
    for i in range(NV):
        mm.wb(buf + i, spu[i])


def toneattr(mm):
    buf = mm.r[7]
    mm.wb(buf + 2, 0x11)
    mm.wb(buf + 3, 0x22)
    mm.r[2] = 0


def ignore(mm):
    pass


m.hooks.pop(0x800490FC, None)       # World() stubs PlaySoundEffect for the other oracles: here the REAL Play runs
m.hooks.pop(0x80049634, None)
m.hooks[0x80094f20] = keyoff
m.hooks[0x80094660] = keyon
m.hooks[0x8009860c] = status
m.hooks[0x80090370] = toneattr
m.hooks[0x80082918] = ignore
m.hooks[0x800815ec] = ignore

SNAP = bytes(m.ram)
SYSVAB = m.rhs(0x800A8246)      # the SPU id of the system bank: the first argument of the key-on of the vab -1 path (lh -0x7dba($s5), 0x80049264)
PATCH = [None]            # a pristine image, to restore between vectors (the EXE image with the real sound table)


def row_of(i):
    a = TABLE + ROW * i
    return dict(vab=m.rhs(a), prog=m.rhs(a + 2), tone=m.rh(a + 4), note=m.rhs(a + 6), flags=m.rh(a + 8), bank=m.rhs(a + 10), ref=m.rhs(a + 12), mx=m.rhs(a + 16), nt=m.rhs(a + 20))


# ---- the python model --------------------------------------------------------------------------------------------------------------
class Model:
    def __init__(self):
        self.state = [0] * NV
        self.id = [0] * NV
        self.vab = [0] * NV
        self.tone = [0] * NV
        self.anti = [0] * 64
        self.flags = {i: row_of(i)['flags'] for i in IDS}
        self.countdown = 0
        self.rows = {i: row_of(i) for i in IDS}
        self.j = []
        self.spu = [0] * NV

    def find(self, sid):
        for i in range(NV):
            if self.id[i] == sid:
                return i
        return -1

    def stop(self, sid):
        """StopSoundEffect 0x80049634 (a0 = id, 32-bit signed word; ids here are small positive)"""
        if sid == 0:
            return
        if not (self.flags[sid] & 2):
            while True:
                v = self.find(sid)
                if v == -1:
                    break
                self.j.append(('keyoff', v))
                if self.spu[v]:
                    self.spu[v] = 3 if self.spu[v] == 1 else self.spu[v]
                self.vab[v] = -2          # sw $s2 (-2) , 0x78
                self.id[v] = 0            # sw $zero , 0x18
        self.flags[sid] = 0               # sh $zero, 8(row)

    def antidup(self, sid):
        for k in range(64):
            if self.anti[k] == sid:
                return 1
            if self.anti[k] == 0:
                self.anti[k] = sid
                return 0
        return 0

    def refresh(self):
        for i in range(NV):
            st = self.spu[i]
            if st != 0:
                self.state[i] = st
            else:
                if self.state[i] in (0x80, 0):
                    continue
                self.state[i] = 0
                self.id[i] = 0
                self.vab[i] = -2

    def count(self, sid):
        r = self.rows[sid]
        n = 0
        for i in range(NV):
            if self.state[i] != 0 and self.id[i] == sid and self.vab[i] == r['vab'] and self.tone[i] == r['tone']:
                n += 1
        return n

    def play(self, sid):
        """PlaySoundEffect 0x800490FC, system-bank path (row vab -1); a row of a foreign bank ends in the bank-load / redirect paths, which this model does not run: those ids are excluded by the caller"""
        if self.countdown != 0:
            return
        if sid <= 0 or sid > 0x3C2:
            return
        if self.antidup(sid):
            return
        r = self.rows[sid]
        if r['vab'] == -2:
            return
        self.refresh()
        assert r['vab'] == -1
        if self.count(sid) >= r['mx']:
            return
        if r['nt'] <= 0:
            return
        for k in range(r['nt']):
            self.j.append(('keyon', SYSVAB, r['prog'], r['tone'] + k, r['note'], 0, 0x7F, 0x7F))
            v = next((i for i in range(NV) if self.spu[i] == 0), -1)
            if v >= 0:
                self.spu[v] = 1
            if v >= 0:
                self.flags[sid] |= 1
                self.state[v] = 0x80
                self.id[v] = sid
                self.vab[v] = r['vab']
                self.tone[v] = r['tone'] + k


def read_state():
    st = [m.rb(VOICE + i) for i in range(NV)]
    ids = [m.rws(VOICE + 4 * i + 0x18) for i in range(NV)]
    vab = [m.rws(VOICE + 4 * i + 0x78) for i in range(NV)]
    tone = [m.rws(VOICE + 4 * i + 0xD8) for i in range(NV)]
    anti = [m.rw(ANTIDUP + 4 * k) for k in range(64)]
    fl = {i: m.rh(TABLE + ROW * i + 8) for i in IDS}
    return st, ids, vab, tone, anti, fl


def model_state(md):
    return md.state, md.id, md.vab, md.tone, md.anti, md.flags


COV = set()
_o = _emu.Emu.exec1


def _x(self, pc, wd):
    COV.add(pc)
    return _o(self, pc, wd)


_emu.Emu.exec1 = _x

rnd = random.Random(20261024)


def fresh():
    """restore the pristine image and a random initial voice table + flags; returns the model with the SAME state"""
    m.ram[:] = SNAP
    m.r[:] = [0] * 32          # a mutant that loops leaves the registers dirty: start every vector clean
    m.hi = m.lo = 0
    if PATCH[0] is not None:
        m.ww(*PATCH[0])
    J.clear()
    for i in range(NV):
        spu[i] = 0
    md = Model()
    # random initial voices (as if earlier plays had happened): up to 5 live voices with ids/vab/tone and SPU states
    for i in range(NV):
        if rnd.random() < 0.25:
            sid = rnd.choice(IDS[:6])
            r = md.rows[sid]
            s = rnd.choice([1, 1, 3, 0x80])
            md.state[i] = 0x80 if s == 0x80 else s
            md.spu[i] = 0 if s == 0x80 else s
            md.id[i] = sid
            md.vab[i] = r['vab']
            md.tone[i] = r['tone'] + rnd.choice([0, 0, 1])
    # a few stale entries
    for i in range(NV):
        if md.state[i] == 0 and rnd.random() < 0.1:
            md.id[i] = rnd.choice(IDS[:6])      # an id left in a free slot (not cleared) : Stop finds it by id alone
            md.vab[i] = rnd.choice([-1, -2])
    for sid in IDS:
        if rnd.random() < 0.15:
            md.flags[sid] |= 2                  # the 'bank pending' bit: Stop skips the key-off
        if rnd.random() < 0.15:
            md.flags[sid] |= 1
    if rnd.random() < 0.12:
        md.vab[0] = rnd.choice(IDS)            # the word right after the 24 id words is vab[0]: a finder that runs one slot too far would read it as the id of a 25th voice
    md.countdown = rnd.choice([0, 0, 0, 0, 3])
    md.anti = [0] * 64
    # write to the machine
    for i in range(NV):
        m.wb(VOICE + i, md.state[i])
        m.ww(VOICE + 4 * i + 0x18, md.id[i] & M32)
        m.ww(VOICE + 4 * i + 0x78, md.vab[i] & M32)
        m.ww(VOICE + 4 * i + 0xD8, md.tone[i] & M32)
        spu[i] = md.spu[i]
    for sid in IDS:
        m.wh(TABLE + ROW * sid + 8, md.flags[sid])
    m.ww(COUNTDOWN, md.countdown)
    m.ww(GROUP, M32)          # group -1
    return md


def run_op(md, op):
    kind = op[0]
    if kind == 'play':
        m.call(PLAY, op[1])
        md.play(op[1])
    elif kind == 'stop':
        m.call(STOP, op[1])
        md.stop(op[1])
    elif kind == 'frame':
        for k in range(64):
            m.ww(ANTIDUP + 4 * k, 0)
        md.anti = [0] * 64
    elif kind == 'end':
        v = op[1]
        spu[v] = 0
        md.spu[v] = 0
    elif kind == 'countdown':
        m.ww(COUNTDOWN, op[1])
        md.countdown = op[1]


def one_vector(length):
    md = fresh()
    ops = []
    for _ in range(length):
        k = rnd.random()
        if k < 0.34:
            ops.append(('play', rnd.choice(IDS[:6] + [25, 25, 85])))
        elif k < 0.7:
            ops.append(('stop', rnd.choice(IDS + [25, 25, 0, 1, 7])))
        elif k < 0.8:
            ops.append(('frame',))
        elif k < 0.9:
            ops.append(('end', rnd.randrange(NV)))
        else:
            ops.append(('countdown', rnd.choice([0, 0, 5])))
    trace = []
    for op in ops:
        run_op(md, op)
        a = read_state()
        b = model_state(md)
        ja, jb = list(J), list(md.j)
        trace.append((op, a, ja))
        if tuple(a[:5]) != tuple(b[:5]) or a[5] != b[5] or md.spu != spu:
            return False, (op, a, b)
        if ja != jb:
            return False, (op, 'journal', ja, jb)
    return True, ops


# ---- vectors -------------------------------------------------------------------------------------------------------------------------
bad = 0
classes = collections.Counter()
for k in range(N):
    ok, info = one_vector(rnd.randrange(1, 14))
    if not ok:
        bad += 1
        if bad <= 3:
            print('MISMATCH', info)
    else:
        for op in info:
            classes[op[0]] += 1
print('vectors %d  mismatches %d  ops %s' % (N, bad, dict(classes)))
cov_stop = sorted(a for a in COV if STOP <= a < STOP_END or FINDV <= a < FINDV_END)
all_stop = [a for a in range(STOP, STOP_END, 4)] + [a for a in range(FINDV, FINDV_END, 4)]
print('Stop+find instructions covered: %d / %d' % (len(cov_stop), len(all_stop)))
print('missing:', [hex(a) for a in all_stop if a not in COV])
play_cov = [a for a in range(PLAY, 0x80049320, 4) if a in COV]
print('Play front (0x800490FC..0x80049320) instructions covered: %d / %d' % (len(play_cov), len(range(PLAY, 0x80049320, 4))))

# ---- targeted scenarios (named), printed for the report ------------------------------------------------------------------------------
print('--- named scenarios on the REAL code')


GOLD = []


def scenario(title, ops):
    md = fresh()
    rec = []
    for sid in IDS:
        m.wh(TABLE + ROW * sid + 8, 0); md.flags[sid] = 0
    m.ww(COUNTDOWN, 0); md.countdown = 0
    for i in range(NV):
        m.wb(VOICE + i, 0); m.ww(VOICE + 4 * i + 0x18, 0); m.ww(VOICE + 4 * i + 0x78, M32); m.ww(VOICE + 4 * i + 0xD8, 0)
        spu[i] = 0; md.state[i] = 0; md.id[i] = 0; md.vab[i] = -2 if False else -1; md.tone[i] = 0
        md.vab[i] = s32(M32)
    md.spu = [0] * NV
    J.clear(); md.j.clear()
    print('# ' + title)
    for op in ops:
        before = len(J)
        run_op(md, op)
        a = read_state()
        live = [(i, a[1][i], a[0][i]) for i in range(NV) if a[1][i] != 0]
        print('  %-26s journal+ %-60s live voices (slot,id,state) %s' % (op, J[before:], live))
        rec.append([list(op), [list(x) for x in J[before:]], [list(x) for x in live]])
        assert a[0] == md.state and a[1] == md.id and a[2] == md.vab and a[3] == md.tone and a[4] == md.anti, 'model/real differ'
        assert list(J) == md.j
    GOLD.append({'title': title, 'steps': rec})


scenario('the pillar of g5a: Play(25) every tick of the push, then the end of the push (C76: Stop(25))',
         [('play', 25), ('frame',), ('play', 25), ('frame',), ('play', 25), ('frame',), ('stop', 25), ('frame',), ('play', 25)])
scenario('two objects playing 25 (cap 1): the second Play is refused, then ONE Stop(25) silences both',
         [('play', 25), ('frame',), ('play', 25), ('frame',), ('stop', 25), ('frame',), ('stop', 25)])
scenario('id 85 (two tones, cap 1): Stop keys off both tones',
         [('play', 85), ('frame',), ('play', 85), ('stop', 85)])
scenario('Stop of an id that is not playing, and Stop(0)',
         [('stop', 25), ('stop', 0), ('play', 25)])
scenario('Play then Stop in the SAME frame, then Play again in that frame: the anti-duplicate table refuses the second Play (Stop does not touch the table)',
         [('play', 25), ('stop', 25), ('play', 25), ('frame',), ('play', 25)])
scenario('Stop while the countdown (g_soundEffectState) is armed: the Stop is NOT gated (the Play is)',
         [('play', 25), ('frame',), ('countdown', 5), ('play', 85), ('stop', 25), ('play', 25)])
scenario('a voice that ended naturally (SPU off) but not yet refreshed: Stop still finds the id and keys it off',
         [('play', 25), ('end', 0), ('stop', 25)])

if DO_GOLD:
    blob = json.dumps(GOLD, indent=1, sort_keys=True)
    open(os.path.join(HERE, 'stop_goldens.json'), 'w', encoding='utf-8', newline=chr(10)).write(blob + chr(10))
    print('goldens: %d scenarios, sha256 %s' % (len(GOLD), hashlib.sha256((blob + chr(10)).encode()).hexdigest()))

# ---- mutants of the real Stop code ---------------------------------------------------------------------------------------------------
md_ = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
if DO_MUT:
    import exelib
    # a one-word mutant: replace the instruction at addr by nop (0), or flip a conditional branch (beq<->bne, beqz<->bnez) or change an immediate
    def mutants():
        out = []
        for addr in list(range(STOP, STOP_END, 4)) + list(range(FINDV, FINDV_END, 4)):
            w = exelib.u32(addr)
            ins = next(md_.disasm(struct.pack('<I', w), addr), None)
            mn = ins.mnemonic if ins else ''
            out.append((addr, 'nop', 0))
            if mn in ('beq', 'bne', 'beqz', 'bnez'):
                op = (w >> 26) & 0x3F
                out.append((addr, 'flip', (w & ~(0x3F << 26)) | ((5 if op == 4 else 4) << 26)))
            if mn in ('addiu', 'andi', 'slti', 'ori', 'sltiu'):
                out.append((addr, 'imm+1', (w & ~0xFFFF) | (((w & 0xFFFF) + 1) & 0xFFFF)))
        return out
    MUT = mutants()
    killed = 0
    survivors = []
    ms = 0
    for addr, kind, word in MUT:
        PATCH[0] = (addr, word)
        # a short run of vectors against the model
        rs = random.getstate()
        rnd.seed(77)
        dead = False
        for k in range(600):
            try:
                ok, _ = one_vector(rnd.randrange(1, 10))
            except Exception:
                ok = False
            if not ok:
                dead = True
                break
        random.setstate(rs)
        PATCH[0] = None
        if dead:
            killed += 1
        else:
            survivors.append((hex(addr), kind))
    print('mutants %d killed %d survivors %d' % (len(MUT), killed, len(survivors)))
    print('survivors:', survivors)
