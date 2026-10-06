# O-E19-77 discovery: age of the inventory and sub-inventory pad words (read-only)

Repository `chantier/e19-suite2`, HEAD `b73c928` (Alundra/ identical to `796285f` except one new converter test file). Nothing built, run,
exported, edited, staged or committed in the repository. Physical copy `repo/` made by `setup.sh` (`git archive` of HEAD for
`Alundra`, `Alundra.Tests`, `alundra-project` (tracked part), the engine submodule (`acbb1ae3`) and MGUI (`18b2c14a`); then the
export data of `alundra-project` copied with robocopy, Musics and `Alundra.dll/pdb` excluded, once `report.json` had been stable
for 13 minutes). Builds deploy `Alundra.dll` into the copy only.

Tags: **[bin]** disassembly of ALUN_CD.EXE (France) ; **[emu]** the REAL binary code run in the MIPS interpreter ; **[code]** file:line at HEAD ;
**[sim]** the real `AlundraWorldProxy.Update` in the copy ; **[hyp]** hypothesis.

## 0. Answer

**There is no sample gap on the inventory paths. No fix.** The premise of O-E19-77 (RenderScene read = one sample old, port reads the
tick's word = one sample too young) compares per-call ages without the port's tick mapping. The port folds the slot-6 setup into the
trigger's tick (`AlundraInventoryDirector.cs:24-41`: "one port tick as one original Update followed by the next frame's render"), so its
per-frame call n runs one tick earlier than the binary's and reads TickPad of its tick = the very sample the binary's call n reads one
frame later. Measured: 21 schedules, binary real code vs real proxy, keyed by the sample each pass consumed: **21/21 identical**
(`cmp-today.txt`). The E19.f3d-style change (per-frame reads the previous tick's word) makes 16/21 differ and reddens 3 existing pins.

## 1. Binary [bin]

Frame f = `RenderScene(f)` (dispatcher `0x80048054`: slots 0..12 ascending, then the post-process tail `0x80048178-0x800481D8`) then
`Update(0)(f)` (sampler `0x8002E38C` at `0x8002BB5C`; `UpdateWorld` `0x8002E058` at `0x8002BC4C` = map events `0x8003C67C`, entities
`0x8003B388`, effects `0x8003C410`; trigger block `0x8002BC54-0x8002BD00`). RenderScene reads sample f-1, Update(0) sample f.

| Reader | Address | Word / mask | Phase, age |
|---|---|---|---|
| Trigger | `0x8002BCA4` | pressed `0x80126F2A` & `0x803` (L2, R2, Start) | Update(0), 0 |
| Trigger guard | `0x8002BCC8` | held `0x80126F28` & `0x100` (Select refuses) | Update(0), 0 |
| DisplayInventory `0x80055570` debug branch | `0x800555B8` | held, directions -> `0x80051F1C`/`0x80051218`/`0x80052618`/`0x800506AC` | only when `g_cdIsReady` `0x800A82B0` == 0 (set to 1 at `0x8004E85C`): dead in play |
| Slot 6 per-frame `0x80056598` | `0x80056784` Down, `0x800567C8` Up, `0x8005680C` Right, `0x80056870` Left, `0x800568D4` Cross (equip `0x8005795C`/`0x80057854`), `0x8005691C` `0x813` close (`0x800556DC`), `0x80056948` `0xC` L1/R1 (post-process 1) | by-interval `0x80126F2E` | RenderScene, 1 |
| Slot 4 per-frame `0x80053328` | `0x8005354C` Right, `0x80053584` Left, `0x800535BC` Up, `0x800535F4` Down, `0x8005362C` `0x813` close (`0x800526CC`), `0x80053658` L1/R1 (post-process 2) | by-interval `0x80126F2E` | RenderScene, 1 |

`0x80056784` was missed by the earlier scans (its `lui` sits in the delay slot of `0x800565DC`); `scan2.py` finds all 7. No pad read in the
setup `0x80054F1C` (leaf; installs `0x80056598` at `0x80055554-0x80055564`), the slot-4 init `0x800537F0`, the close setups, `0x80052618`.
Second call site of `0x80055570`: `0x800481D4` (dispatcher tail, post-process 2, slot 4 free): head only, no pad read in play.
Close completions clear MenuOpen (bit 8 of `0x800DC4B8`) at `0x8005675C` (slot 6, unless post&1) and `0x8005351C` (slot 4, unless post&2).

## 2. Real code [emu] (`bin/inv_bin.py`, `inv_sched*.py`, output `bin-events.txt`)

Samples relative to the opening press P: head in Update(P); setup RS(P+1); first per-frame RS(P+2); opening slide done RS(P+19).
Down/Cross/Start at P+18 dropped, at P+19 taken (RS(P+20)). Close at P+19: completion RS(P+38), MenuOpen cleared; Start at P+37 refused,
P+38 reopens. L1 at P+19: sub opened at the tail of RS(P+38); Right P+55 dropped, P+56 taken; sub close P+56 -> completion RS(P+75);
L1 back at P+56 -> main head RS(P+75), setup RS(P+76), Down P+93 dropped, P+94 taken. Down held from P+10: moves at P+31, then every
sample. L2/R2 open; Start+Select and Triangle do not.

## 3. Port [code]

`AlundraWorldProxy.cs:2202-2229`: per tick `TickPad.Update(LastPadState.ButtonsHold)` (`:2204`), `_squareOfTick`/`_choicePadOfTick` (`:2205-2206`),
then `AlundraInventoryDirector.Tick` (`:2208`), `AlundraSubInventoryDirector.Tick` (`:2209`), `AlundraInventoryPostProcess.Run` (`:2210`),
portrait, presenters. Loop 2 (`:2264-2310`): dialogue `Pass` and save screen on the tick-1 words (`:2269`, `:2287`), then map events.
`_choicePadOfLastTick` saved at `:2318-2321`. Inventory reads: `AlundraInventoryDirector.cs:404` (pressed, trigger), `:432` (held Select),
`:629-692` (7 by-interval reads); `AlundraSubInventoryDirector.cs:502-548` (6). Head+setup in the trigger tick, per-frame from the next;
post-process head then setup next tick (`_setupPending`).

## 4. Measurements [sim]

`probe-tests.cs.txt` (copy only), 1 tick per 0.02 s frame, output `probe-out/port-events.txt`; `bin/cmp_port.py` keys binary RS(f) events
to sample f-1, Update(f) to f, port tick k to k: **21/21 equal**. Mutant M2 (`apply_m2.py`, `O77MUT=2`, per-frame reads the previous tick's
by-interval word): 16/21 differ (`cmp-m2.txt`: Down at P+18 accepted, every reaction one sample late, the P+38 reopen lost) and 3 existing
pins go red: `AlundraInventoryPortraitWiringTests.ToggleBackToTheMainInventory_StartsInTheHeadTickOfThePostProcess`,
`ToggleToTheSubInventory_ReturnsThenStartsAgain_InThePostProcessTick`, `Close_ReturnStepsInItsOwnTick_AndEndsBeforeTheScreenIsRemoved`.
Today: 183/183 inventory-class tests and 17/17 proxy-level tests green in the copy.

## 5. Proposal

Close O-E19-77 as "no gap" (plan row; one-line pointers in `plan-e19-pad-age-annexe/notes.md` section 10 and `verify/verify.md` item 2).
Optional guard test (green today, red under M2), proxy level, values measured = binary: Down P+18 -> selection 0, P+19 -> 6 at tick P+19;
close P+19 -> MenuOpen off at tick P+37, Start P+37 refused, P+38 reopens; sub Right P+55 -> 0, P+56 -> 2 at tick P+56; back to main
Down P+93 -> 0, P+94 -> 6.

## 6. Adjacent, not pad age [bin][code] + [hyp]

The binary's trigger (Update(k)) runs after RS(k) (text box close `0x8004501C`, called at `0x8004704C` from slot 0, frees slot 0 and clears
control bits `0x18`) and after the map events of Update(k). The port runs the trigger in the pad loop, before loop 2's `Pass`, save screen
and map events of the same tick. [hyp] One-sample consequences, unmeasured: a Start on the sample the box closes opens in the binary, not
in the port; a map event opening a box or posing a control bit on tick k wins in the binary, the inventory wins in the port. A frame-order
question, for the author to register or not.
