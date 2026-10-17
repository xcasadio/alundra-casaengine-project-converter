"""vec_p.py - compares pmodel.py (python model of the binary's carrier/passenger update) with the REAL physics pass 0x80038364 of ALUN_CD.EXE on random vectors.

Usage:  python -B vec_p.py [n_vectors] [seed]
For each vector: 2..4 entities (hero in slot 0 + 1..3 others), random flags / boxes / positions (many of them built to REST on another entity, so that the rider search fires),
terrain, map gravity, scripted writes of ForceZ / impulse between ticks, 1..4 ticks.  After every tick all fields of all entities are compared.
Also: instruction coverage of the real code over the functions under test, and the kill of model mutants (each mutant is run on the SAME recorded real outputs).
Writes only the files named on the command line / in this folder.
"""
import sys, os, random, json, time, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "h2tools"))
import mips
import oracle_p as OP
from oracle_p import P, U, M32, s32, ent
import pmodel as PM

COV = set()
_orig_step = mips.Cpu.step


def _step(self):
    COV.add(self.pc)
    _orig_step(self)


mips.Cpu.step = _step

FLAG_CHOICES = [0x80, 0x180, 0x4180, 0x100, 0x00, 0x80, 0x180, 0x1C0, 0x0560C0, 0x0360C0]
BOXES = [((0, 0, 0), (0, 0, 0)), ((-12, -8, 0), (24, 16, 16)), ((-24, -16, 0), (48, 32, 8)), ((-10, -7, 0), (21, 15, 32)), ((-1, -1, 0), (1, 1, 1)),
         ((-12, -8, 4), (24, 16, 16)), ((-9, -6, 0), (18, 12, 32))]
HERO_BOXES = [((-10, -7, 0), (21, 15, 32)), ((-10, -7, 0), (21, 15, 32)), ((-8, -6, 2), (16, 12, 24))]


def gen_vector(rnd: random.Random):
    n = rnd.randint(2, 4)
    T_hb = rnd.choice([0, 0, 1, 2])
    G, V = rnd.choice([(128, 4096), (128, 4096), (3, 256), (64, 1024)])
    ents = []
    basex, basey = 100 << 16, 100 << 16
    for i in range(n):
        if i == 0:
            flags = rnd.choice([0x03118C, 0x03118C, 0x03118C, 0x03108C, 0x03518C])
            off, size = rnd.choice(HERO_BOXES)
        else:
            flags = rnd.choice(FLAG_CHOICES)
            off, size = rnd.choice(BOXES)
        animflags = 0x80 if rnd.random() < 0.06 else 0
        status = rnd.choice([2, 2, 2, 2, 2, 3, 4]) if i else 2
        blocked = 1 if (i and rnd.random() < 0.05) else 0
        ents.append(dict(flags=flags, off=off, size=size, animflags=animflags, status=status, blocked=blocked))
    # positions: chain / stack building
    T = T_hb << 20
    for i, e in enumerate(ents):
        mode = rnd.random()
        sx, sy, sz = e["size"]
        ox, oy, oz = e["off"]
        depth = ((sz << 16) - 1) if sz else 0
        if i == 0 or mode < 0.25:
            # free position over the terrain
            px = basex + rnd.randint(-30, 30) * 65536 + rnd.choice([0, 0, 1, 32768])
            py = basey + rnd.randint(-20, 20) * 65536
            pz = T + 1 + rnd.choice([0, 0, 0, 1, 65536, 3 * 65536, 20 * 65536, -1, -65536]) + (0 if rnd.random() < 0.7 else rnd.randint(-40000, 400000))
        else:
            # rest on (or near) another entity j < i or j > i
            j = rnd.choice([k for k in range(len(ents)) if k != i and "pos" in ents[k]] or [None]) if any("pos" in ents[k] for k in range(len(ents)) if k != i) else None
            if j is None:
                px, py, pz = basex, basey, T + 1 + 65536 * rnd.randint(0, 10)
            else:
                o = ents[j]
                oox, ooy, ooz = o["off"]
                osx, osy, osz = o["size"]
                odepth = ((osz << 16) - 1) if osz else 0
                ptop1 = o["pos"][2] + (ooz << 16) + odepth + 1          # other.top + 1  (ModdedZ domain)
                if rnd.random() < 0.75:
                    # i rests on j  (i.mz == j.top + 1)  => i.posz = ptop1 - oz
                    pz = ptop1 - (oz << 16) + rnd.choice([0, 0, 0, 0, 1, -1, 65536, -65536, 2])
                else:
                    # j rests on i? approximate: place i under j
                    pz = o["pos"][2] + (ooz << 16) - depth - 1 - (oz << 16) + rnd.choice([0, 0, 1, -1, -3 * 65536])
                px = o["pos"][0] + rnd.choice([0, 0, 0, 5, -5, 11, 23, 24, 25, -23, -24, 47, 48]) * 65536 + rnd.choice([0, 0, 1, 65535])
                py = o["pos"][1] + rnd.choice([0, 0, 0, 3, -3, 7, 15, 16, -15, -16, 31, 32]) * 65536 + rnd.choice([0, 0, 1])
        e["pos"] = (px, py, pz)
    if rnd.random() < 0.06:
        # near the absolute ceiling (0x7800000 = 1920 px): shift the whole scene up
        shift = 0x7800000 - 0x0100000 * rnd.randint(1, 3) - rnd.choice([0, 65536 * 20, 3000])
        for e in ents:
            p0 = e["pos"]
            e["pos"] = (p0[0], p0[1], p0[2] + shift)
    # per-tick script writes
    nt = rnd.randint(1, 4)
    forces = [0, 0, 0, 32768, -32768, 65536, -65536, 131072, -131072, 5 * 65536, -5 * 65536, 1, -1, 12345, -98765]
    script = []
    for t in range(nt):
        w = []
        for i in range(n):
            r = rnd.random()
            if r < 0.30:
                w.append((i, "forcez", rnd.choice(forces)))
            elif r < 0.33:
                w.append((i, "izf", rnd.choice([0, 0, 1280, -1280, 768, 0x8000, -0x8000 + 0x10000 * 0, 1360, 5])))
        script.append(w)
    return dict(n=n, T_hb=T_hb, G=G, V=V, ents=ents, script=script, init_force=[rnd.choice([0, 0, 0, 32768, -32768, 65536, -65536, -131072]) for _ in range(n)])


def build_real(v):
    p = P(gravity=v["G"], visc=v["V"], cells=OP.flat(v["T_hb"]))
    for i, e in enumerate(v["ents"]):
        p.add(i, e["flags"], e["off"], e["size"], e["pos"], status=e["status"], init=False)
        p.s.mem.ww(ent(i) + 0xB4, e["animflags"])
        p.s.mem.ww(ent(i) + 0x20, e["blocked"])
    p.s.rebuild_lists()          # the lists must see AnimFlags / +0x20 written above (the first end pass of InitializeEntity reads them)
    for i, e in enumerate(v["ents"]):
        p.s.init_state(i, clamp=False)
        p.put(i, "forcez", v["init_force"][i])
    return p


def build_model(v):
    ents = []
    for i, e in enumerate(v["ents"]):
        ox, oy, oz = e["off"]
        sx, sy, sz = e["size"]
        ents.append(PM.E(idx=i, flags=e["flags"], animflags=e["animflags"], status=e["status"], blocked=e["blocked"], posx=e["pos"][0], posy=e["pos"][1], posz=e["pos"][2],
                         modx=ox << 16, mody=oy << 16, modz=oz << 16, w=((sx << 16) - 1) if sx else 0, h=((sy << 16) - 1) if sy else 0, d=((sz << 16) - 1) if sz else 0,
                         forcez=v["init_force"][i]))
    w = PM.World(ents, T=v["T_hb"] << 20, G=v["G"], V=v["V"])
    w.init_endpass()
    return w


def real_snap(p, n):
    out = []
    for i in range(n):
        s = p.snap(i)
        out.append(dict(z=s["z"], fz=s["fz"], ffz=s["ffz"], ride=s["ride"], coll=s["coll"], ground=s["ground"], floor=s["floor"], terr=s["terr"],
                        mz=s32(p.s.mem.rw(ent(i) + 0x1E0)), x=s["x"], y=s["y"], ffx=s["ffx"], ffy=s["ffy"]))
    return out


def run_vector(v, mut=frozenset(), record=None):
    """returns the list of (tick, entity, field, real, model) mismatches ; `record` (list) receives the real snapshots of the vector on the first call"""
    n = v["n"]
    mismatches = []
    w = build_model(v)
    w.mut = mut
    if record is not None and "real" not in record:
        p = build_real(v)
        reals = [real_snap(p, n)]
        for t, writes in enumerate(v["script"]):
            for (i, name, val) in writes:
                p.put(i, name, val)
            p.tick()
            reals.append(real_snap(p, n))
        record["real"] = reals
        record["init"] = reals[0]
    reals = record["real"]
    # initial state check
    for i in range(n):
        ms = w.snap(w.e[i])
        for k in ("z", "fz", "floor", "ground", "terr", "mz"):
            if ms[k] != reals[0][i][k]:
                mismatches.append((-1, i, k, reals[0][i][k], ms[k]))
    for t, writes in enumerate(v["script"]):
        for (i, name, val) in writes:
            setattr(w.e[i], name if name != "forcez" else "forcez", val)
        try:
            w.tick()
        except RuntimeError:
            mismatches.append((t, -1, "runaway", 0, 1))
            break
        for i in range(n):
            ms = w.snap(w.e[i])
            r = reals[t + 1][i]
            for k in ("z", "fz", "ffz", "ride", "coll", "ground", "floor", "terr", "mz"):
                if ms[k] != r[k]:
                    mismatches.append((t, i, k, r[k], ms[k]))
    return mismatches


MUTANTS = ["win_no_plus1", "ride_top_nopl1", "ride_mask_0x100", "land_t_ge", "land_cand_le", "land_below_le", "ceil_le", "no_ridercopy", "no_recursion", "no_move_first",
           "moved_ignored", "copy_ignores_impulse", "one_sided"]


def main():
    nvec = int(sys.argv[1]) if len(sys.argv) > 1 else 200
    seed = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    rnd = random.Random(seed)
    t0 = time.time()
    vectors, records = [], []
    bad = 0
    nticks = 0
    nrider = 0
    feats = collections.Counter()
    for k in range(nvec):
        v = gen_vector(rnd)
        rec = {}
        mm = run_vector(v, record=rec)
        vectors.append(v)
        records.append(rec)
        nticks += len(v["script"])
        for snap in rec["real"][1:]:
            for s in snap:
                if s["ride"] is not None:
                    nrider += 1
                    feats["ride"] += 1
                if s["coll"]:
                    feats["coll"] += 1
                if s["ground"]:
                    feats["ground"] += 1
        if mm:
            bad += 1
            if bad <= 5:
                print("MISMATCH vector", k, mm[:6])
                print(json.dumps(v)[:1500])
    dt = time.time() - t0
    print("vectors %d, ticks %d, vectors with a mismatch %d, entity-ticks with RidingEntity set %d, features %s, %.1fs" % (nvec, nticks, bad, nrider, dict(feats), dt))
    # coverage
    ranges = {
        "P 0x80038364": (0x80038364, 0x800384F4), "riders 0x800364C8": (0x800364C8, 0x8003660C), "forces 0x80036828": (0x80036828, 0x80036BD4),
        "ridercopy 0x80037364": (0x80037364, 0x800373E4), "ComputeZ 0x800375E0": (0x800375E0, 0x80037730), "landing 0x80036BFC": (0x80036BFC, 0x80036D94),
        "ceiling 0x80036D94": (0x80036D94, 0x80036F34), "MoveEntity 0x80037E34": (0x80037E34, 0x80037F28), "FloorHeight 0x80037F28": (0x80037F28, 0x80038064),
    }
    cov = set(COV)
    import capstone
    md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
    out = []
    for name, (a, b) in ranges.items():
        code = mips.IMG[a - mips.BASE + mips.OFF:b - mips.BASE + mips.OFF]
        ins = list(md.disasm(code, a))
        addrs = [i.address for i in ins]
        covered = set()
        for i in ins:
            if i.address in cov:
                covered.add(i.address)
                if i.mnemonic in ("j", "jal", "jr", "jalr", "beq", "bne", "blez", "bgtz", "bltz", "bgez", "beqz", "bnez", "b", "bal"):
                    covered.add(i.address + 4)     # delay slot
        unc = [a2 for a2 in addrs if a2 not in covered]
        out.append((name, len(addrs), len(covered), unc))
        print("coverage %-24s %4d / %4d instructions  (%.1f%%)" % (name, len(covered), len(addrs), 100.0 * len(covered) / len(addrs)))
    json.dump({name: [hex(a) for a in unc] for name, tot, c, unc in out}, open(os.path.join(HERE, "vec_p_uncovered_%d_%d.json" % (nvec, seed)), "w"))
    # mutants
    print("mutants (each run on the same recorded real outputs):")
    killed = 0
    for mu in MUTANTS:
        diff = 0
        first = None
        for v, rec in zip(vectors, records):
            mm = run_vector(v, mut=frozenset([mu]), record=rec)
            if mm:
                diff += 1
                if first is None:
                    first = mm[0]
        status = "KILLED" if diff else "SURVIVED"
        killed += 1 if diff else 0
        print("  %-22s %-9s differing vectors %d  %s" % (mu, status, diff, first if first else ""))
    print("mutants killed %d / %d" % (killed, len(MUTANTS)))


if __name__ == "__main__":
    main()
