import collections
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
rows, tot = pickle.load(open("census.pkl", "rb"))
STORY = set(range(162, 183)) | {44, 10}
CLS = {"wall": "wall found", "zero": "zero speed", "nowall": "no wall", "indeterminate": "indeterminate", None: "-"}


def dist(r):
    d = r.get("detail", {})
    res = d.get("res")
    if not res:
        return ""
    if res[3] == "zone":
        lo, hi = res[1]
        return "%d-%d (zone)" % (lo, hi) if hi >= 0 else ""
    return "%d" % res[1] if res[0] == "wall" else ("none (%s)" % res[3])


def actor(r):
    d = r.get("detail", {})
    return d.get("actor", "")


def row_text(r):
    d = r.get("detail", {})
    if r["reach"] == "dormant":
        return "| %d | %d | - | dormant | - | - | - | - | - | - | - |" % (r["map"], r["pc"])
    slots = sorted({det.get("slot") for _, det in r["all"]})
    return "| %d | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        r["map"], r["pc"], "/".join(s for s in slots if s), r["reach"], actor(r),
        d.get("anim", ""), d.get("speed", ""), d.get("dir", ""), CLS[r["class"]], dist(r) or d.get("why", ""),
        "held" if r["lock"] else "free")


reach = collections.Counter(r["reach"] for r in rows)
cls = collections.Counter((r["reach"], r["class"]) for r in rows if r["reach"] != "dormant")
story_rows = [r for r in rows if r["map"] in STORY and r["reach"] != "dormant"]
offenders = [r for r in story_rows if r["class"] != "wall"]
maps_all = sorted({r["map"] for r in rows})
maps_reach = sorted({r["map"] for r in rows if r["reach"] != "dormant"})
names = {r["map"]: (r["zone"], r["name"]) for r in rows}

out = []
w = out.append
w("# Census of the `0x24` (Wait force adjusted) sites")
w("")
w("E19.d D5 (docs/plan-e19-opcodes.md §1.2g, D-E19-21). Static census of every `0x24` of the exported corpus: what the actor waits on, "
  "and whether the wait can end on a wall in the port. `0x24` returns 1 when the logic entity's `ForceAdjusted` is nonzero (E19.d D4); in "
  "the port that flag is raised by a blocked step on the cell field (E19.a2/E19.a3), not yet by contacts between entities (E19.h) or by "
  "native AI (E14).")
w("")
w("**Verdict on the story path: NOT all sites are \"wall found\". %d of the %d reachable sites of the story-path maps are outside that "
  "class (details below). Per the plan (D5, stop rule) the execution stopped before committing D4 and D5.**" % (len(offenders), len(story_rows)))
w("")
w("## Method")
w("")
w("1. **Population**: every `0x24` at an instruction boundary of the linear decode of the code region (after the six entry tables) of every "
  "map: %d sites in %d maps." % (len(rows), len(maps_all)))
w("2. **Reachability** (mechanical rule of the plan). Roots: the B entry of every map event; for every record, the entry designated by each of "
  "its A, C, D, E and F slot indexes when bit 0x80 is set (`index & 0x7F`). *Reachable*: every instruction reached from a root by following "
  "every jump, branch and call (all branches open; `0x7D` returns to every call site seen); `0x40` (always `v2 = 0` in the corpus) and `0x41` "
  "open no script entry. *DLL only*: reached only from slot entries whose index has bit 0x80 clear (the DLL falls back on the map's own "
  "table). *Dormant*: neither. All 1714 map events have bit 0x80 set.")
w("3. **Actor and state** (reachable and DLL-only sites): a path-sensitive forward walk of every root program, all branches open, tracks the "
  "logic entity (`0x42`; `0x43 [n]` takes record `n`, taken as found; `0x43 [0x80]` unchanged; `0x43 [0x81]` the hero), its animation "
  "(`0x1A`, `0x59`, `0x5A`/`0x5B` for the direction, `0x5B`, and `0x0B`, whose first operand is the animation), its direction (`0x09`, `0x08`, "
  "`0x0A`, `0x3A`, `0x0C` as the four cardinal values, `0x5A`, `0x5B`), the hero lock (`0x10`/`0x11`) and the position when it can be "
  "pinned: the spawn position of the record (`XPos * 12 + 12`, `YPos * 8 + 8`), `0x64`/`0x65`/`0x8A`, then moved along the direction by the "
  "known waits (`0x37 Wait n`: `n` ticks at the animation speed; `0x0B`/`0x1E`/`0x1F`: the operand radius or distance; `0x24`: up to the "
  "wall), clipped at the first wall. Any wait of unknown duration while the actor walks, followed by a change of direction, loses the pin.")
w("4. **Speed**: `Speed` of the animation in the actor's sprite header (`Data/sprite-records.json` `AnimSets`; the hero is the prefab "
  "`Alundra`, id `192c2eeb-...`).")
w("5. **Ray**: the footprint of the actor (`OffsetX/Y`, `SizeX/Y` of its header) is moved along its direction (the major axis one pixel at a "
  "time) on the cell grid of the map: the first cell whose `walkability | ground_property << 8` hits the actor's mask (`0x40`, `|= 0x01` "
  "with ClassB, `|= 0x1000` with ClassA, as `AlundraCellsCollisionField.WalkabilityMaskFor`), or whose ground height (slopes as "
  "`ComputeGroundHeight`) is higher than the current one by more than the controller's `step_height` (prefab `CharacterControllerComponent`), "
  "ends the ray (a wall). The ray stops with *no wall* when it leaves the map (the DLL does not port the screen clip that the original "
  "applies at the map border, `ApplyEntityForces`). When the position cannot be pinned for the hero of a B program, the ray is cast from "
  "the centre of every free cell of the map event's zone: *wall found* only if every cast ends on a wall (distance range given).")
w("6. **Classes**: *wall found* (the wait ends on a cell), *zero speed* (the animation does not move the actor: it waits for an outside contact, "
  "E19.h or E14), *no wall* (the ray leaves the map), *indeterminate* (logic entity, animation, direction or position not deducible). The "
  "class of a site is the worst of the states that reach it (indeterminate > no wall > zero speed > wall found).")
w("7. **Check of the model**: on map 163 (`0x24 @183`, Jess) the model gives the actor `rec0 Jess`, animation 1, speed 128, direction 0, "
  "pinned at (924, 216) after her two walks, wall at 81 px, i.e. `PosY` 297.0 px, the contact measured by arc A8.")
w("")
w("Limits of the model (the census is static): the animation or direction inherited from another program (an A Load program, an earlier "
  "tick) is not tracked across programs; positions moved by other entities or by flags are not modelled; every branch is taken as possible "
  "(infeasible paths can add states); the footprint is the sprite header's, not the exact controller capsule.")
w("")
w("## Totals")
w("")
w("| | sites | maps |")
w("|---|---|---|")
w("| population (linear decode) | %d | %d |" % (len(rows), len(maps_all)))
w("| reachable | %d | %d |" % (reach["reachable"], len(maps_reach)))
w("| DLL only | %d | - |" % reach["DLL only"])
w("| dormant | %d | - |" % reach["dormant"])
w("")
w("Expected by the plan: 395 reachable sites in 76 maps and 34 dormant or DLL-only: **matched** (no site is DLL-only: no record has a slot "
  "index with bit 0x80 clear that reaches a `0x24`).")
w("")
w("| class (reachable sites) | all maps | story-path maps |")
w("|---|---|---|")
for k in ("wall", "zero", "nowall", "indeterminate"):
    allc = cls[("reachable", k)] + cls[("DLL only", k)]
    sc = sum(1 for r in story_rows if r["class"] == k)
    w("| %s | %d | %d |" % (CLS[k], allc, sc))
w("| total | %d | %d |" % (reach["reachable"] + reach["DLL only"], len(story_rows)))
w("")
held = sum(1 for r in rows if r["reach"] != "dormant" and r["lock"])
w("Sites that can run under the hero's held control (`0x10` without a following `0x11` on some path): %d of %d (the earlier linear census "
  "of the discovery counted 160)." % (held, reach["reachable"]))
w("")
w("## Story path")
w("")
w("Criterion (plan D5): every map of the Inoa zone (maps 162 to 182, 172 included), map 44 (Wendels Nightmare, reached from map 179) and "
  "map 10 (Overworld 2,1, next to Inoa), from map 163 to the first dream. Maps of the criterion that have a `0x24`: %s. Map 172: its six sites "
  "(`@372`, `@383`, `@839`, `@1004`, `@1342`, `@1666`) are **dormant**, as expected." % ", ".join(str(m) for m in sorted(STORY & set(maps_all))))
w("")
w("Sites of the story-path maps that are reachable: %d, of which **%d are outside \"wall found\"**:" % (len(story_rows), len(offenders)))
w("")
w("| map | pc | slot | root | actor | anim | speed | dir | class | detail | hero lock |")
w("|---|---|---|---|---|---|---|---|---|---|---|")
for r in offenders:
    d = r["detail"]
    w("| %d | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        r["map"], r["pc"], d.get("slot"), d.get("root"), d.get("actor"), d.get("anim", ""), d.get("speed", ""), d.get("dir", ""),
        CLS[r["class"]], dist(r) or d.get("why", ""), "held" if r["lock"] else "free"))
w("")
w("Reading: the 17 *no wall* and 9 *indeterminate* sites split in three groups. (a) Sites where the hero walks until a wall from a position "
  "the program does not pin (maps 10, 176, 178 and 179: the position depends on how the hero arrived on the map): from some free cells "
  "of the zone the ray leaves the map, from others it hits a wall. (b) Villagers of map 10 (Meade, Bergus, Nestus, Kisha, Lutas, Fein, Thyea, "
  "Meia) that walk away until the map border: in the original the screen clip ends the walk, not ported in the DLL; their following sites "
  "are then indeterminate. (c) Nestus on map 165, whose position is not deducible (T103 commands his walks).")
w("")
w("## Off the story path")
w("")
off = [r for r in rows if r["reach"] != "dormant" and r["map"] not in STORY]
oc = collections.Counter(r["class"] for r in off)
w("Reachable sites off the story path: %d (wall found %d, zero speed %d, no wall %d, indeterminate %d). The sites in a risky class are in the table "
  "below (rows other than \"wall found\"); they are reported to the author, with no stop." % (
      len(off), oc["wall"], oc["zero"], oc["nowall"], oc["indeterminate"]))
w("")
w("## Table of every site")
w("")
w("| map | pc | slot | reachability | actor | anim | speed | dir | class | distance to the wall (px) or reason | hero control |")
w("|---|---|---|---|---|---|---|---|---|---|---|")
for r in rows:
    w(row_text(r))
w("")
open("D:/development/repo/alundra-casaengine-project-converter/docs/census-0x24-waits.md", "w", encoding="utf-8", newline="\n").write("\n".join(out))
print("written", len(out), "lines;", "offenders", len(offenders), "of", len(story_rows))
for r in offenders:
    d = r["detail"]
    print((r["map"], r["pc"], d.get("slot"), d.get("root"), d.get("actor"), d.get("anim"), d.get("speed"), d.get("dir"), CLS[r["class"]], "held" if r["lock"] else "free", dist(r) or d.get("why", "")))
