import collections
import os
import pickle
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = os.path.dirname(os.path.abspath(__file__))
rows = pickle.load(open(HERE + "/census_v2.pkl", "rb"))
STORY = set(range(162, 183)) | {44, 10}
CLS = {"wall": "wall found", "zero": "zero speed", "nowall": "no wall", "indeterminate": "indeterminate",
       "0x0B": "not reached (0x0B blocked)", "flag": "not reached (flag never set)", "entry": "not reached (entry)"}
RISKY = ("zero", "nowall", "indeterminate")


def dist(r):
    d = r.get("dist")
    if not d:
        return ""
    lo, hi = d
    return "%d" % lo if lo == hi else "%d-%d" % (lo, hi)


def held(r):
    return "held" if r.get("lock") else "free"


def cause(r):
    d = r["detail"]
    return (d.get("why") or "").replace("|", "/")


def row_text(r):
    if r["reach"] == "dormant":
        return "| %d | %d | - | dormant | - | - | - | - | - | - | - |" % (r["map"], r["pc"])
    d = r["detail"]
    slots = sorted({det.get("slot") for _, det in r["all"]}) if r["all"] else [d.get("slot")]
    if r["reached"]:
        last = dist(r) or cause(r)
    else:
        last = cause(r)
    return "| %d | %d | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        r["map"], r["pc"], "/".join(s for s in slots if s), r["reach"], d.get("actor", "") if r["reached"] else "-",
        d.get("anim", "") if r["reached"] else "", d.get("speed", "") if r["reached"] else "", d.get("dir", "") if r["reached"] else "",
        CLS[r["class"]], last, held(r) if r["reached"] else "-")


live = [r for r in rows if r["reach"] != "dormant"]
reach = collections.Counter(r["reach"] for r in rows)
maps_all = sorted({r["map"] for r in rows})
maps_reach = sorted({r["map"] for r in live})
story_rows = [r for r in live if r["map"] in STORY]
off_rows = [r for r in live if r["map"] not in STORY]
cls_story = collections.Counter(r["class"] for r in story_rows)
cls_all = collections.Counter(r["class"] for r in live)
cls_off = collections.Counter(r["class"] for r in off_rows)
unreached = [r for r in live if not r["reached"]]
unreached_story = [r for r in unreached if r["map"] in STORY]
risky_off = [r for r in off_rows if r["class"] in RISKY]
risky_story = [r for r in story_rows if r["class"] in RISKY]
held_n = sum(1 for r in live if r["reached"] and r["lock"])
reached_n = sum(1 for r in live if r["reached"])

out = []
w = out.append
w("# Census of the `0x24` (Wait force adjusted) sites")
w("")
w("E19.d D5 (docs/plan-e19-opcodes.md §1.2g, D-E19-21, corrected by D-E19-24). Static census of every `0x24` of the exported corpus: "
  "what the actor waits on, and whether the wait can end on a wall in the port. `0x24` returns 1 when the logic entity's `ForceAdjusted` is "
  "nonzero (E19.d D4); in the port that flag is raised by a blocked step on the cell field (E19.a2/E19.a3), not yet by contacts between "
  "entities (E19.h), by the screen-border clamp (O-E19-17) or by native AI (E14).")
w("")
w("**Verdict on the story path: %d of the %d reachable sites of the story-path maps end on a cell (\"wall found\"); %d are not reached "
  "in the model (separate risks, listed below); %d are \"no wall\" and %d are \"indeterminate\", so the stop rule of D5 does not trigger.**"
  % (cls_story["wall"], len(story_rows), len(unreached_story), cls_story["nowall"], cls_story["indeterminate"]))
w("")
w("## Why this is the second version")
w("")
w("The first run of this census (stopped, O-E19-16) found 26 of the 59 reachable story-path sites outside \"wall found\" (17 \"no wall\", 9 "
  "\"indeterminate\"). A read-only follow-up, checked by two independent passes, showed that those sites came from wrong start positions in the "
  "model, not from the port: the villagers of map 10 and Nestus of map 165 were started at their record position although the map-load pass "
  "never spawns them there (`SpriteDirection` bit 0x40 clear, binary gate `0x8003A268`; they appear through `0x8A`, or `0x2D` then `0x64`), "
  "and the hero was cast from every free cell of the map event's zone. D-E19-24 corrects the model as below and reruns it; the screen-border "
  "clamp (O-E19-17) is not needed to decide any story-path site.")
w("")
w("## Method")
w("")
w("1. **Population** (unchanged): every `0x24` at an instruction boundary of the linear decode of the code region (after the six entry tables) "
  "of every map: %d sites in %d maps." % (len(rows), len(maps_all)))
w("2. **Reachability** (unchanged, mechanical rule of the plan). Roots: the B entry of every map event; for every record, the entry designated "
  "by each of its A, C, D, E and F slot indexes when bit 0x80 is set (`index & 0x7F`). *Reachable*: every instruction reached from a root by "
  "following every jump, branch and call (all branches open; `0x7D` returns to every call site seen); `0x40` (always `v2 = 0` in the corpus) "
  "and `0x41` open no script entry. *DLL only*: reached only from slot entries whose index has bit 0x80 clear. *Dormant*: neither. All 1714 "
  "map events have bit 0x80 set.")
w("3. **Actor and state** (unchanged walk): a path-sensitive forward walk of every root program, all branches open, tracks the logic entity "
  "(`0x42`; `0x43 [n]` takes record `n`, taken as found; `0x43 [0x80]` unchanged; `0x43 [0x81]` the hero), its animation (`0x1A`, `0x59`, "
  "`0x5B`, and `0x0B`, whose first operand is the animation), its direction (`0x09`, `0x08`, `0x0A`, `0x3A`, `0x0C` as the four cardinal "
  "values, `0x5A`, `0x5B`), the hero lock (`0x10`/`0x11`) and the position when it can be pinned, moved along the direction by the known waits "
  "(`0x37 Wait n`: `n` ticks at the animation speed; `0x0B`/`0x1E`/`0x1F`: the operand radius or distance; `0x24`: up to the wall).")
w("4. **Speed**: `Speed` of the animation in the actor's sprite header (`Data/sprite-records.json` `AnimSets`; the hero is the prefab "
  "`Alundra`, id `192c2eeb-...`).")
w("5. **Ray** (unchanged): the footprint of the actor (`OffsetX/Y`, `SizeX/Y` of its header) is moved along its direction (the major axis one "
  "pixel at a time) on the cell grid of the map: the first cell whose `walkability | ground_property << 8` hits the actor's mask (`0x40`, "
  "`|= 0x01` with ClassB, `|= 0x1000` with ClassA, as `AlundraCellsCollisionField.WalkabilityMaskFor`), or whose ground height (slopes as "
  "`ComputeGroundHeight`) is higher than the current one by more than the controller's `step_height`, ends the ray (a wall). The ray ends "
  "with *no wall* when it leaves the map (the DLL does not port the screen clip of `ApplyEntityForces`, O-E19-17).")
w("6. **Classes**: *wall found* (the wait ends on a cell), *zero speed* (the animation does not move the actor: it waits for an outside contact, "
  "E19.h or E14), *no wall* (the ray leaves the map), *indeterminate* (logic entity, animation, direction or position not deducible), and, "
  "new, *not reached* (no state of the walk arrives at the site, see 9). The class of a site is the worst of the states that reach it "
  "(indeterminate > no wall > zero speed > wall found).")
w("")
w("**Corrections of D-E19-24 (the corrected model):**")
w("")
w("7. **Where each actor starts.** A record starts at its record position only if the map-load pass spawns it (`IsEnabled` nonzero and "
  "`SpriteDirection` bit 0x40 set). Otherwise its start positions are the positions written by *any* program of the map: `0x8A` and `0x64` "
  "(literal record selector, or `0x80` resolved to the logic entity, or any selector resolved in the walk), collected to a fixpoint; a record "
  "that only a `0x2D` activates appears at its record position unless such a write exists; a record with no position source is unpinned "
  "(then *indeterminate* if it reaches a `0x24`). `0x2D` inside a walk pins the activated record at its record position. An actor that walks "
  "during a wait of unknown length and then turns is no longer unpinned: both ends of its possible position (where it was, the wall it walks "
  "to) become states (an actor that could leave the map stays unpinned).")
w("8. **Where the hero starts.** From its real arrivals, never from every free cell of the zone: (a) the story-chain entry of five programs "
  "whose entry is fixed by a flag gate that a static walk cannot see (table below), read from the operands of the `0x53` or from the portal "
  "rectangle, never typed, and chained to the walk of the source map (an entry whose source program is not reached is itself *not reached*); "
  "(b) otherwise every `0x53` and every portal of the corpus that lands in the map, kept when its tile is inside the zone of the map event, "
  "with the portal direction; (c) at a `0x3B` the free sample positions of its box (stride 4, at most 60 per box), carried through a `T` "
  "flag to the programs that wait for it; (d) the contact positions around an interaction record (one program).")
w("9. **Blocked walks and flags: *not reached*.** A `0x0B` whose wall comes before its radius in the cell model (more than 1 px short) is "
  "not clipped and continued: it has no `ForceAdjusted` exit (`0x8003D468`), so the rest of that program is *not reached (0x0B blocked)* "
  "for the states that blocked. A wait (`0x36`) or a \"flag on\" branch on a flag that has setters but none reached is cut: *not reached "
  "(flag never set)*. `T` flags: setters of the same map, to a fixpoint over all programs of the map (setters in dormant code do not count). "
  "`G` flags: setters of the whole corpus, evaluated on demand through the walk of the map that holds each setter; a flag whose only setters "
  "are dormant, or that has none, is taken as set outside the scripts. Between maps the walk is iterated until the table no longer changes "
  "(4 passes).")
w("10. **Hero lock** (the \"hero control\" column): the program's own `0x10`/`0x11`, plus the lock of the program that raised the flag the "
  "site's program waited for, plus the interaction contact.")
w("11. **Check of the model**: on map 163 (`0x24 @183`, Jess) the model gives the actor `rec0 Jess`, animation 1, speed 128, direction 0, "
  "pinned at (924, 216) after her two walks, wall at 81 px, i.e. `PosY` 297.0 px, the contact measured by arc A8. On map 178 the hero sites "
  "give 177 px, 57-133, 90-114 and 0 px (the follow-up found 177, 57-136, 89-116, 0), on map 165 Nestus 0-48 and 0-209 px (26 and 209).")
w("")
w("Limits of the model (the census is static): contacts between entities (E19.h) are not modelled, they change distances and can stop a "
  "`0x0B` earlier than a wall (example: the walk of Septimus `rec39` on map 10 is stopped by the activated block `rec41`, not by the wall at "
  "482 px, so the `0x0B @3673` that follows is *not reached* only in the cell model); the animation or direction inherited from another "
  "program is not tracked; every branch is taken as possible (infeasible paths can add states); the footprint is the sprite header's, not the "
  "exact controller capsule; the hero jump and `0x25` are not ported, so a walk that needs them blocks (O-E19-19); `0x45`/`0x46` (NoObstacleSlide) "
  "decide whether a wall contact raises the flag at once (O-E19-20) and are not modelled; a `G` flag set by a dialogue (Yarn) in addition "
  "to a script setter could be taken as never set; the walks of the unreached maps use every arrival of the corpus.")
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
w("| class (reachable sites) | all maps | story-path maps | other maps |")
w("|---|---|---|---|")
for k in ("wall", "zero", "nowall", "indeterminate", "0x0B", "flag", "entry"):
    w("| %s | %d | %d | %d |" % (CLS[k], cls_all[k], cls_story[k], cls_off[k]))
w("| total | %d | %d | %d |" % (len(live), len(story_rows), len(off_rows)))
w("")
w("Sites reached in the model: %d of %d; of these, %d can run under the hero's held control (the discovery's linear count of 160 had no model "
  "of reach)." % (reached_n, len(live), held_n))
w("")
w("First model for comparison (O-E19-16): wall found 269, zero speed 1, no wall 83, indeterminate 42 (all 395 reached by construction); "
  "story path 33 / 0 / 17 / 9.")
w("")
w("## Story path")
w("")
w("Criterion (plan D5): every map of the Inoa zone (maps 162 to 182, 172 included), map 44 (Wendels Nightmare, reached from map 179) and "
  "map 10 (Overworld 2,1, next to Inoa), from map 163 to the first dream. Maps of the criterion that have a `0x24`: %s. Map 172: its six sites "
  "(`@372`, `@383`, `@839`, `@1004`, `@1342`, `@1666`) are **dormant**, as expected." % ", ".join(str(m) for m in sorted(STORY & set(maps_all))))
w("")
w("**Verdict**: %d reachable sites; %d \"wall found\"; %d \"zero speed\"; **%d \"no wall\"; %d \"indeterminate\"**; %d not reached. The stop "
  "rule (a story-path site still \"no wall\" or \"indeterminate\") does not apply; the %d not-reached sites are separate risks, they do not stop "
  "the commit of `0x24` (D-E19-24)." % (
      len(story_rows), cls_story["wall"], cls_story["zero"], cls_story["nowall"], cls_story["indeterminate"], len(unreached_story),
      len(unreached_story)))
w("")
w("### Hero entries used for the programs whose entry a flag gate fixes")
w("")
w("| program | entry used | why (flag gate, from the data) |")
w("|---|---|---|")
w("| 10 mapevent19 `B[20]` (`@2462`) | `0x53` of 176 `@600` -> (10, 58) | gate G1654 on, G1655 off: only this chain is inside the window |")
w("| 176 mapevent6 `B[7]` (`@599`) | `0x53` of 179 `@568` -> (5, 33) | gate G1654 on, G1655 off; G1654 is set by 179 `@565` just before |")
w("| 179 mapevent2 `B[3]` (`@564`) | `0x53` of 176 `@533` or 44 `@985` -> (17, 7) | gate G1653 on, G1654 off; G1653 is set by 176 `@530` right before that `0x53` |")
w("| 178 mapevent0 `B[1]` (`@123`-`@151`) | portal 7 of 176 -> (38, 25), needs G1655 | the only entry inside the zone; 176 `B[8]` `@617` makes its source tile unwalkable while G1655 is off; G1655 is set only by 135 `@953` |")
w("| 135 mapevent13 `B[14]` (`@927`, `@942`) | `0x53` of 10 `@2463` -> (19, 51) | the church door portal is made unwalkable by 10 `B[1]` while G1655 is off |")
w("| 10 mapevent7 `B[8]` (`@1047`) | contact with rec34 Septimus when T300 is raised | T300 is set only by his `C[18]` `@3210`, after T3 raised by his interaction `F[5]` |")
w("")
w("Everything else on the story path uses (b) or (c) of the method; their starting points are in the table of every site. On map 10, "
  "`B[9]` (`@1212`) starts at the tile of its `0x3B` (37, 46).")
w("")
w("### Story-path sites that are not \"wall found\"")
w("")
w("| map | pc | class | cause |")
w("|---|---|---|---|")
for r in sorted(unreached_story + risky_story, key=lambda x: (x["map"], x["pc"])):
    w("| %d | %d | %s | %s |" % (r["map"], r["pc"], CLS[r["class"]], cause(r)))
w("")
w("Reading (O-E19-18, O-E19-19): in the cell model the villager scene of map 10 (G218) stops at the `0x0B @5172` of Nestus (up 28 px, blocked "
  "at the first step by the wall that ended `@5163`): T666 is never set, B[14] never ends, the hero stays locked (`0x10 @1826`), so the "
  "villagers' following sites and the scene of Ronan (`@5819`, T690, set by B[15] after G457) are not reached; `@3731`, `@3757`, `@3793` and "
  "`@3862` follow the `0x0B @3673` of Septimus (blocked in the cell model, probably stopped earlier by a block in the original). `10 @2462` and "
  "`10 @6400` (Giles) need the scripted jump with `0x25` (a 16 px ledge at x = 517, not ported): the chain 179 -> 176 -> 10 -> 135 stops there, "
  "so the four sites of map 178 (G1655, set only by 135 `@953`) and `178 @694` (T0, raised by B[1]) are not reached either. Map 135 and "
  "map 331 (the mirror of map 10 after the id remap) show the same blocks. In the original these scenes finish (contacts between entities, "
  "or the jump): to be settled by E19.d2 (D-E19-26) and E19.h; the screen clip (O-E19-17) decides none of them.")
w("")
w("## Not reached in the model (risks, whole corpus)")
w("")
w("%d sites (%d on the story path). A site here is not a defect of `0x24`: the program stops earlier, on a blocked `0x0B` or on a flag no "
  "reached program raises. They stay risks until the cell model, contacts or the jump explain the stop." % (len(unreached), len(unreached_story)))
w("")
w("| map | pc | class | cause | story path |")
w("|---|---|---|---|---|")
for r in sorted(unreached, key=lambda x: (x["map"], x["pc"])):
    w("| %d | %d | %s | %s | %s |" % (r["map"], r["pc"], CLS[r["class"]], cause(r), "yes" if r["map"] in STORY else ""))
w("")
w("## Off the story path: risky sites")
w("")
w("Reachable sites off the story path: %d (wall found %d, zero speed %d, no wall %d, indeterminate %d, not reached %d). The sites in a risky "
  "class (zero speed, no wall, indeterminate) are listed below with the hero control of their program; they are reported to the author, with "
  "no stop." % (len(off_rows), cls_off["wall"], cls_off["zero"], cls_off["nowall"], cls_off["indeterminate"],
                cls_off["0x0B"] + cls_off["flag"] + cls_off["entry"]))
w("")
w("| map | pc | actor | anim | speed | dir | class | detail | hero control |")
w("|---|---|---|---|---|---|---|---|---|")
for r in sorted(risky_off, key=lambda x: (x["map"], x["pc"])):
    d = r["detail"]
    w("| %d | %d | %s | %s | %s | %s | %s | %s | %s |" % (
        r["map"], r["pc"], d.get("actor"), d.get("anim", ""), d.get("speed", ""), d.get("dir", ""), CLS[r["class"]],
        (("%s px" % dist(r)) if r["class"] == "nowall" and r.get("detail", {}).get("res") else cause(r)) or "-", held(r)))
w("")
w("Held control among them: %d of %d. The \"no wall\" sites end in the original on the screen clip (O-E19-17), the \"indeterminate\" ones on "
  "an actor whose position the program does not fix, the \"zero speed\" one on a contact." % (
      sum(1 for r in risky_off if r["lock"]), len(risky_off)))
w("")
w("## Table of every site")
w("")
w("| map | pc | slot | reachability | actor | anim | speed | dir | class | distance to the wall (px) or reason | hero control |")
w("|---|---|---|---|---|---|---|---|---|---|---|")
for r in rows:
    w(row_text(r))
w("")
open("D:/development/repo/alundra-casaengine-project-converter/docs/census-0x24-waits.md", "w", encoding="utf-8", newline="\n").write("\n".join(out))
print("written", len(out), "lines")
print("story", dict(cls_story), len(story_rows), "unreached story", len(unreached_story))
print("all", dict(cls_all), "off", dict(cls_off))
print("held", held_n, "reached", reached_n, "risky_off", len(risky_off), "risky_off held", sum(1 for r in risky_off if r['lock']))
