"""Critic: an independent model (written from the critic's own listing reading) of the five starts + six heals,
applied to every row of the discovery's 30 keys.  Usage: python -B c_keycheck.py KEYDIR"""
import glob
import json
import os
import struct
import sys
from c_exe import load

pc0, gp0, tbase, tsize, img = load()
M = 0xFFFFFFFF


def hw(addr, i):
    return struct.unpack_from("<h", img, addr - tbase + 2 * i)[0]


TABX = [hw(0x80023654, i) for i in range(32)]
TABY = [hw(0x80023694, i) for i in range(32)]


def s32(x):
    x &= M
    return x - (1 << 32) if x & 0x80000000 else x


def sra_div(a, k):
    # exactly what the binary does: (a + (a < 0 ? 2^k - 1 : 0)) >> k, a signed 32
    if a < 0:
        a = a + (1 << k) - 1
    return a >> k


def step(s):
    return (s * 0x7D2B89DD + 0xE06A02E7) & M


def hi_mul(a, b):
    return ((a & M) * b) >> 32


class World:
    def __init__(self, free_slots):
        self.free = free_slots   # None = unlimited
        self.made = 0
        self.events = []
        self.effects = []
        self.rng = None

    def create(self, anim, x, y, z):
        args = [0, 14, anim, x, y, z]
        if self.free is not None and self.made >= self.free:
            self.events.append(["effect", args + ["refused"]])
            self.effects.append({"obtained": False, "args": args})
            self.made += 1
            return None
        self.made += 1
        e = {"obtained": True, "args": args, "x3c": x, "y40": y, "z44": z, "fx54": 0, "fy58": 0, "fz5c": 0}
        self.events.append(["effect", args])
        self.effects.append(e)
        return e

    def sound(self, n):
        self.events.append(["sound", n])


def herb_heal(w, st, pos):
    hp, hpmax = st["hp"], st["hpmax"]
    inc = max(5, sra_div(hpmax, 2))
    new = hp + inc
    if hpmax < new:
        new = hpmax
    st["hp"] = new
    e = w.create(0, pos[0], pos[1], pos[2] + 0x100000)
    if e:
        e["fz5c"] = 0x20000


def spark(w, st, pos, anim, fz):
    e = w.create(anim, pos[0], pos[1], pos[2] + 0x100000)
    if e:
        r1 = step(w.rng)
        r2 = step(r1)
        e["fz5c"] = fz
        w.rng = r2
        e["fx54"] = s32(hi_mul(r1, 0x20001) - 0x10000)
        e["fy58"] = s32(hi_mul(r2, 0x30001) - 0x18000)


def spotion_heal(w, st, pos):
    hp, hpmax = st["hp"], st["hpmax"]
    inc = max(10, sra_div(hpmax, 1))
    new = hp + inc
    if hpmax < new:
        new = hpmax
    st["hp"] = new
    for _ in range(4):
        spark(w, st, pos, 0, 0x20000)


def rings(w, pos, anims):
    for i in range(8):
        e = w.create(anims[i], pos[0], pos[1], pos[2] + 0x80000)
        if e:
            e["x3c"] = s32(e["x3c"] + (TABX[4 * i] << 11))
            e["y40"] = s32(e["y40"] + (TABY[4 * i] << 11))
            k = (8 + 4 * i) & 31
            e["fz5c"] = 0x30000
            e["fx54"] = TABX[k] * 448
            e["fy58"] = TABY[k] * 448


def lpotion_heal(w, st, pos):
    st["hp"] = st["hpmax"]
    rings(w, pos, [0] * 8)


def elixir_heal(w, st, pos):
    st["mp"] = st["mpmax"]
    rings(w, pos, [2] * 8)


def cureale_heal(w, st, pos):
    st["hp"] = st["hpmax"]
    st["mp"] = st["mpmax"]
    rings(w, pos, [2 if i % 2 == 0 else 0 for i in range(8)])
    for _ in range(4):
        spark(w, st, pos, 0, 0x40000)
        spark(w, st, pos, 2, 0x20000)


def use(item, w, st, pos):
    """the dispatcher + start of the item: returns True if used"""
    if item == 0x24:
        ok = st["hp"] < st["hpmax"]
        heal, snd = herb_heal, 0x31
    elif item == 0x25:
        ok = st["hp"] < st["hpmax"]
        heal, snd = lpotion_heal, 0x32
    elif item == 0x26:
        ok = st["mp"] < st["mpmax"]
        heal, snd = elixir_heal, 0x30
    elif item == 0x27:
        ok = st["hp"] < st["hpmax"] or st["mp"] < st["mpmax"]
        heal, snd = cureale_heal, 0x30
    elif item == 0x29:
        ok = st["hp"] < st["hpmax"]
        heal, snd = spotion_heal, 0x31
    if st["count"] == 0:
        w.sound(3)
        return False
    if not ok:
        w.sound(3)
        return False
    heal(w, st, pos)
    w.sound(snd)
    st["count"] -= 1
    return True


MAXCOUNT = {0x24: 9, 0x25: 1, 0x26: 1, 0x27: 1, 0x29: 1}


def check_key(fp):
    d = json.load(open(fp))
    item = d["item"]
    a = d["args"]
    rows = d["rows"]
    hpmax0 = rows[0]["hero_hpmax"]
    st = {"hp": a["hp"], "hpmax": hpmax0, "mp": a["mp"], "mpmax": a["mpmax"], "count": min(a["count"], MAXCOUNT[item])}
    free = a["free_slots"]
    made = 0
    prev_pressed = False
    problems = []
    uses = 0
    alarms = []
    for r in rows:
        t = r["t"]
        if a.get("relower") and str(t) in a["relower"]:
            st["hp"], st["mp"] = a["relower"][str(t)]
        pressed = t in a["presses"]
        front = pressed and not prev_pressed
        prev_pressed = pressed
        w = World(None if free is None else max(0, free - made))
        w.rng = r["rng0"]
        pos = r["pos"]
        if front:
            if use(item, w, st, pos):
                uses += 1
        made += w.made
        ev = r["events"]
        ev_cmp = ev
        if ev and ev[0] == ["sound", 400]:
            ev_cmp = ev[1:]
            alarms.append(t)
        if ev_cmp != w.events:
            problems.append((t, "events", ev_cmp, w.events))
        mine = w.effects
        theirs = r["effects"]
        if len(mine) != len(theirs):
            problems.append((t, "effect count", len(theirs), len(mine)))
        else:
            for m_, th in zip(mine, theirs):
                if m_["obtained"] != th["obtained"] or m_["args"] != th["args"][:6]:
                    problems.append((t, "effect args", th["args"], m_["args"]))
                elif m_["obtained"]:
                    for k in ("x3c", "y40", "z44", "fx54", "fy58", "fz5c"):
                        if th[k] != m_[k]:
                            problems.append((t, "effect " + k, th[k], m_[k]))
        if (st["hp"], st["mp"], st["count"]) != (r["hero_hp"], r["stats_mp"], r["count"]):
            problems.append((t, "state", (r["hero_hp"], r["stats_mp"], r["count"]), (st["hp"], st["mp"], st["count"])))
        if r["rng1"] != w.rng:
            problems.append((t, "rng1", r["rng1"], w.rng))
        if r["rng2"] != step(r["rng1"]):
            problems.append((t, "rng2", r["rng2"], step(r["rng1"])))
        if t < len(rows) and rows[t]["rng0"] != r["rng2"]:
            problems.append((t, "rng0(t+1)", rows[t]["rng0"], r["rng2"]))
        nd = 0
        s = r["rng0"]
        while s != r["rng1"] and nd < 40:
            s = step(s)
            nd += 1
        if nd != r["draws"]:
            problems.append((t, "draws", r["draws"], nd))
        if (r["in_use"], r["phase"], r["counter"]) != (0, 0, 0):
            problems.append((t, "item words", (r["in_use"], r["phase"], r["counter"])))
        if r["stats_hp"] != r["hero_hp"] or r["stats_hpmax"] != r["hero_hpmax"]:
            problems.append((t, "mirror", (r["stats_hp"], r["hero_hp"])))
    return d["label"], uses, alarms, problems


if __name__ == "__main__":
    tot = 0
    for fp in sorted(glob.glob(os.path.join(sys.argv[1], "key_*.json"))):
        label, uses, alarms, problems = check_key(fp)
        tot += len(problems)
        print("%-34s uses=%d alarm_ticks=%s problems=%d %s" % (label, uses, alarms, len(problems), problems[:2]))
    print("TOTAL problems", tot)
