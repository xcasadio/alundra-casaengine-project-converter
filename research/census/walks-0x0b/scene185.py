"""scene185.py - map 185, day-4 meeting (G120..G123): the hero's B[1] from @81 (hero inside its 0x3B box (4..5,18)) with
Septimus rec6 activated by B[1] @84 (C[5] @452) and his transparent block rec7 (0x2D @454), run with the checker's emulator
of the binary loop (collision-check/emu.py, copied; 0x08, 0x45 and 0x46 added here as the binary does them:
0x08 TargetDirection += v1 mod 32; 0x45 / 0x46 clear / set Flags bit 0x2000, chain.md F12).
The other load-spawned records stand still at their record positions (their C programs are not run: they only face and
talk, chain.md F9); variant 'withnpc' keeps them, 'alone' drops them.
Variants: binary (entity collision, native E destroy, slot recycling, border clamp) | noent (the DLL today).
Usage: scene185.py [binary|noent] [withnpc|alone] [x0 y0]"""
import sys

import emu

sys.stdout.reconfigure(encoding="utf-8")

_orig_run = emu.World.run_program


def run_program(self, e, st, logic):
    C = self.mp.C
    while True:
        pc = st.get("pc")
        if pc is None:
            return
        op = C[pc]
        L = st.get("logic", logic)
        if op == 0x08:
            L.TargetDir = (L.TargetDir + C[pc + 1]) & 0x1F
            st["pc"] = pc + 2
            continue
        if op in (0x45, 0x46):
            if op == 0x46:
                L.Flags |= 0x2000
            else:
                L.Flags &= ~0x2000
            st["pc"] = pc + 1
            continue
        before = pc
        _orig_run(self, e, st, logic)
        # _orig_run returns on a suspension, an end, or after raising; if it stopped on one of our ops, loop again
        npc = st.get("pc")
        if npc is None or npc == before or C[npc] not in (0x08, 0x45, 0x46):
            return


emu.World.run_program = run_program


def patched_unknown_guard():
    # make the original interpreter stop (return) instead of raising on 0x08 / 0x45 / 0x46, so the wrapper handles them
    src_run = _orig_run

    def wrapped(self, e, st, logic):
        C = self.mp.C
        try:
            return src_run(self, e, st, logic)
        except NotImplementedError as ex:
            pc = st.get("pc")
            if pc is not None and C[pc] in (0x08, 0x45, 0x46):
                return
            raise
    return wrapped


_orig_run = patched_unknown_guard()


def build(variant="binary", npc="withnpc", x0=132, y0=296, dlg=60):
    kw = dict(entity_collision=True, destroy_on_deact=True, recycle_destroyed=True, border_clamp=True)
    if variant == "noent":
        kw.update(entity_collision=False, destroy_on_deact=False, recycle_destroyed=False, border_clamp=False)
    w = emu.World(185, dialog_ticks=dlg, **kw)
    hero = emu.Ent("hero", x0, y0, 0, emu.hero_hdr(), player=True)
    hero.refresh()
    hero.PosZ = w.ground(hero) + 1
    hero.refresh()
    hero.status = 2
    hero.progA = hero.progC = None
    hero.TargetDir = 16
    w.ents.append(hero)
    w.hero = hero
    if npc == "withnpc":
        for rec in (0, 1, 2, 3, 4, 5, 9, 10):
            r = w.recs[rec]
            e = w.spawn(rec, int(r["XPos"]) * 12 + 12, int(r["YPos"]) * 8 + 8, int(r["Height"]) * 8)
            e.progA = e.progC = None
    w.flags |= {"G120", "G121", "G122", "G123"}
    w.map_prog = {"pc": 81}
    return w


def run(variant="binary", npc="withnpc", x0=132, y0=296, ticks=3000):
    w = build(variant, npc, x0, y0)
    end = None
    for _ in range(ticks):
        w.step()
        sep = w.find(6)
        if sep is not None and getattr(sep, "cstate", None) and sep.cstate.get("pc") is not None and sep.cstate["pc"] > 510:
            end = w.tick
            break
    print("variant=%s npc=%s hero=(%s,%s) ticks=%d reached>@510=%s" % (variant, npc, x0, y0, w.tick, end))
    for t, who, what, p in w.events:
        if any(k in what for k in ("0x24", "0x0B", "activate", "destroy", "flag")):
            print("  t=%5d %-24s %-44s (%.6f, %.6f)" % (t, who[:24], what, p[0], p[1]))
    sep = w.find(6)
    if sep:
        print("  Septimus pos (%.6f, %.6f) PosX=%d PosY=%d pc=%s" % (sep.PosX / 65536, sep.PosY / 65536, sep.PosX, sep.PosY,
                                                                   sep.cstate["pc"] if getattr(sep, "cstate", None) else None))
    print("  hero pos (%.6f, %.6f)" % (w.hero.PosX / 65536, w.hero.PosY / 65536))
    return w, end


if __name__ == "__main__":
    a = sys.argv[1:]
    variant = a[0] if a else "binary"
    npc = a[1] if len(a) > 1 else "withnpc"
    x0 = int(a[2]) if len(a) > 2 else 132
    y0 = int(a[3]) if len(a) > 3 else 296
    run(variant, npc, x0, y0)
