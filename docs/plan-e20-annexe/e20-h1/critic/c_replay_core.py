"""Critic: replay the discovery's vectors from an EMPTY memory on the critic's own interpreter."""
import struct
import json
import sys
import glob
import os
from c_cpu import Cpu, MemErr, M32, s32
from c_lst import func_end, dis, tbase


# arity of each hooked callee, from my own listings (a0..); the 8003bdd8 hook also logs the two stack args
ARITY = {0x800490FC: 1, 0x8003BDD8: 6, 0x8004E0F8: 0}


def make_hooks(stubs, arity_hint):
    state = {}
    hooks = {}
    for k, lst in stubs.items():
        a = int(k, 16)
        state[a] = 0

        def mk(a=a, lst=lst):
            def h(cpu):
                i = state[a]
                if i >= len(lst):
                    raise MemErr("hook %08X called more often (%d) than listed (%d)" % (a, i + 1, len(lst)))
                state[a] += 1
                n = arity_hint.get(a, 1)
                args = [cpu.r[4], cpu.r[5], cpu.r[6], cpu.r[7]]
                if n == 6:
                    args += [cpu.rd(cpu.r[29] + 0x10, 4), cpu.rd(cpu.r[29] + 0x14, 4)]
                cpu.calls.append(["0x%x" % a, args[:n]])
                return lst[i], None
            return h
        hooks[a] = mk()
    return hooks, state


def main(VEC):
    files = sorted(glob.glob(os.path.join(VEC, "*.json")))
    tot = ok = 0
    cover = {}
    hazards = 0
    for fp in files:
        d = json.load(open(fp))
        fn = int(d["function"], 16)
        n_ok = 0
        fails = []
        for v in d["vectors"]:
            tot += 1
            # arity per stubbed address from the journal
            ar = {}
            for c in v["calls"]:
                ar[int(c[0], 16)] = len(c[1])
            for k in v["stubs"]:
                ar.setdefault(int(k, 16), 1)
            hooks, state = make_hooks(v["stubs"], ar)
            cpu = Cpu(hooks)
            try:
                for a, sz, val in v["reads"]:
                    cpu.provide(a, sz, val)
                cpu.provided_all = set(cpu.mem.keys())
                v0, v1 = cpu.run(int(v["fn"], 16), v["args"])
            except MemErr as e:
                fails.append((v["id"], "ERR " + str(e)))
                continue
            problems = []
            if v0 != v["ret"]:
                problems.append("ret %x != %x" % (v0, v["ret"]))
            if v1 != v["ret_v1"]:
                problems.append("v1 %x != %x" % (v1, v["ret_v1"]))
            mine = {k: val for k, val in cpu.writes.items()}
            theirs = {(a, sz): val for a, sz, val in v["writes"]}
            if mine != theirs:
                problems.append("writes differ: only-mine=%s only-theirs=%s" % (
                    {k: mine[k] for k in mine if theirs.get(k) != mine[k]},
                    {k: theirs[k] for k in theirs if mine.get(k) != theirs[k]}))
            if cpu.calls != [[c[0], [x & M32 for x in c[1]]] for c in v["calls"]]:
                problems.append("calls differ %s vs %s" % (cpu.calls, v["calls"]))
            # all hooks consumed exactly
            for a, lst in ((int(k, 16), l) for k, l in v["stubs"].items()):
                if state[a] != len([c for c in v["calls"] if int(c[0], 16) == a]):
                    problems.append("hook count mismatch %08x" % a)
            # exact reads: every provided byte must have been touched (no phantom reads listed)
            untouched = cpu.provided_all - cpu.touched
            if untouched:
                problems.append("provided but never read: %d bytes e.g. %08X" % (len(untouched), min(untouched)))
            hazards += cpu.hazards
            cover.setdefault(fn, set()).update(cpu.covered)
            if problems:
                fails.append((v["id"], "; ".join(problems)))
            else:
                n_ok += 1
        ok += n_ok
        print("%-45s %4d/%4d %s" % (os.path.basename(fp), n_ok, len(d["vectors"]), "" if not fails else "FAILS %d: %s" % (len(fails), fails[:3])))
    print("TOTAL %d/%d  load-delay hazards seen: %d" % (ok, tot, hazards))
    # coverage per function body
    for fn, cov in sorted(cover.items()):
        n = func_end(fn, 0x400) if fn != 0x8002BAEC else 0
        if fn == 0x8002BAEC:
            continue
        body = {fn + 4 * i for i in range(n)}
        print("coverage %08X: %d/%d" % (fn, len(body & cov), len(body)))



def replay_file(fp, patches=None, stop_at_first=True):
    """Return number of vectors failing. patches: dict addr -> new word."""
    d = json.load(open(fp))
    bad = 0
    for v in d["vectors"]:
        ar = {}
        for c in v["calls"]:
            ar[int(c[0], 16)] = len(c[1])
        for k in v["stubs"]:
            ar.setdefault(int(k, 16), 1)
        hooks, state = make_hooks(v["stubs"], ar)
        cpu = Cpu(hooks)
        if patches:
            img = bytearray(cpu.img)
            for a, w in patches.items():
                struct.pack_into("<I", img, a - cpu.tbase, w)
            cpu.img = bytes(img)
        try:
            for a, sz, val in v["reads"]:
                cpu.provide(a, sz, val)
            v0, v1 = cpu.run(int(v["fn"], 16), v["args"])
        except MemErr:
            bad += 1
            if stop_at_first:
                return bad
            continue
        mine = dict(cpu.writes)
        theirs = {(a, sz): val for a, sz, val in v["writes"]}
        calls = [[c[0], [x & M32 for x in c[1]]] for c in v["calls"]]
        if v0 != v["ret"] or v1 != v["ret_v1"] or mine != theirs or cpu.calls != calls:
            bad += 1
            if stop_at_first:
                return bad
    return bad
