"""Exploration helper: one line per sprite type with the raw signals, to design the classification rules. Not an output."""
import pickle
import sys

import common
import handlers_base

sys.stdout.reconfigure(encoding="utf-8")
c = pickle.load(open("cache/census.pkl", "rb"))
T, K, H = c["types"], c["keys"], c["headers"]
names = common.load_entity_names()
bal = common.load_balance()
exe, addr_idx, methods, regs = handlers_base.load()
E = exe["entries"]
COMPUTE_HP = 0x8003A374


def calls_hp(slot, code):
    e = E.get((slot, code))
    return bool(e and COMPUTE_HP in e["jals"])


rows = []
for t in sorted(H):
    h = H[t]
    nm = names[t][0] if t < len(names) else "?"
    chain = common.balance_chain(bal, t)
    hp = chain[0]["hp"] if chain else None
    mp = max([r["maxPower"] for r in chain] or [0])
    T_ = T.get(t)
    recs = T_["recs"] if T_ else 0
    nm_maps = len(T_["maps"]) if T_ else 0
    flags = h["MoreFlags"]
    dhit = calls_hp(3, h["ProgramTouch"]) if h["ProgramTouch"] else False
    rows.append((t, nm, recs, nm_maps, (h["ProgramLoad"], h["ProgramTick"], h["ProgramTouch"], h["ProgramDeactivate"], h["ProgramInteract"]), hp, mp,
                 f"{flags:02x}", "P" if h["FlagsPortraitShadowType"] & 0x80 else "-", "DH" if dhit else "-", h["BreakEffect"], h["Contents"]))
lo, hi = int(sys.argv[1]), int(sys.argv[2])
for r in rows:
    if lo <= r[0] < hi:
        print(r)
