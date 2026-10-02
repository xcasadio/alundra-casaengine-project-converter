"""show.py MAP PC [MAP PC ...] - the states of a site in every run, plus the provenance chain of each start pin."""
import os
import pickle
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.stdout.reconfigure(encoding="utf-8")
RUNS = ("DLL", "FIX", "ORIG0", "ORIG")
rows, prov = {}, {}
for run in RUNS:
    p = HERE + "/rows_%s.pkl" % run
    if os.path.exists(p):
        rows[run] = {(r["map"], r["pc"]): r for r in pickle.load(open(p, "rb"))}
    q = HERE + "/prov_%s.pkl" % run
    if os.path.exists(q):
        prov[run] = pickle.load(open(q, "rb"))


def chain(run, mid, key, pin, depth=8):
    out = []
    P = prov.get(run, {})
    seen = set()
    while pin is not None and depth > 0:
        k = (mid, key, tuple(pin))
        if k not in P or k in seen:
            break
        seen.add(k)
        pc, op, frm, kind, reason, ob = P[k]
        out.append("@%s op%s %s<-%s %s%s" % (pc, ("%02X" % op) if op is not None else "?", tuple(pin), frm, reason or kind,
                                              (" by %s" % (ob,)) if ob else ""))
        pin = frm
        depth -= 1
    return out


args = [int(a) for a in sys.argv[1:]]
for i in range(0, len(args), 2):
    key = (args[i], args[i + 1])
    print("=" * 30, key)
    for run in RUNS:
        r = rows.get(run, {}).get(key)
        if r is None:
            continue
        print("--", run, r["reach"], "lock=%s" % r.get("lock"), r.get("classes_seen"), r.get("unreached"))
        seen = set()
        for c, d in r.get("all", []):
            sig = (c, d.get("pin"), d.get("dir"), d.get("end"), d.get("reason"))
            if sig in seen:
                continue
            seen.add(sig)
            print("   %-7s %s anim=%s dir=%s pin=%s rad=%s -> %s n=%s %s %s obst=%s slid=%s root=%s" % (
                c, d.get("actor"), d.get("anim"), d.get("dir"), d.get("pin"), d.get("rad"), d.get("end"), d.get("n"), d.get("reason"),
                d.get("cell"), d.get("obst"), d.get("slid"), d.get("root")))
            if d.get("pin") is not None and d.get("key") is not None:
                for line in chain(run, key[0], d["key"], d["pin"]):
                    print("        ", line)
