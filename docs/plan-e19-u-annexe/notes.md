# O-E19-78 discovery: frame order of the inventory trigger (D-E19-103) - read-only

Repository `chantier/e19-suite2` HEAD `486fdf5`, engine checkout `f86a981b` (MGUI `18b2c14a`, NvgSharp `9c0da031`). Nothing built, run, edited,
staged or committed in the repository. Physical copy `repo/` made by `setup.sh` (git archive of HEAD for Alundra, Alundra.Tests, docs and the tracked
alundra-project; git archive of the three submodule HEADs; the export of alundra-project copied with tar, Musics added afterwards with cp).
All builds and runs happened in `repo/` only. Tags: **F** fact (file:line at HEAD, address, or measurement), **H** hypothesis.

## 0. Answer

Move the whole inventory block (main director Tick = warp-delay decrement + trigger/head/setup or per-frame, sub-inventory Tick, post-process,
portrait Step, the two presenters) and the pad pass into the loop of the passes, the block running AFTER the map events and the effect gate of
the tick; read the pending-trigger gate after the map events of the last tick, before its inventory block. Measured in the copy:
map events, effects and pending triggers freeze on the binary's ticks for the three schedules (main close, sub-inventory, back to main);
Start on the tick a box is released opens the inventory; a map-event box on the trigger's tick wins. **No existing test moves (3214/3214 green on
the candidate), the six trace files are byte-identical (EOL aside), the inventory's own per-tick state is identical on the ten O-E19-77
schedules at 1 and 2 ticks per frame.** 7 proposed red-first cases: 7/7 red at HEAD, 7/7 green on the candidate.

## 1. Binary per-frame order (F, ALUN_CD.EXE France, `dis-*.txt`)

Main loop `0x8002C3F4`: `0x8002AC2C`; **RenderScene** `0x8002BD60` at `0x8002C3FC`; **Update(0)** `0x8002BAEC` at `0x8002C404` (a0=0);
`0x80083C40` at `0x8002C414`; `0x80042798` at `0x8002C444` (end-of-frame counter); `0x800815E4`; loop at `0x8002C45C`.

RenderScene(f): `0x8002CDA0`, `0x8002E130`, `0x8005B670`, `0x8003B51C`, `0x80042CCC`, `0x8005EC98` (memory-card process, per the f3d annex),
dispatcher `0x80048054` at `0x8002BE5C` (slots 0..12 ascending, then the post-process tail `0x80052618`/`0x80055570` at `0x800481D4`), then
`0x80044C5C` at `0x8002BE64` (DisplayUserInterface: HUD, portrait). Slot table `0x800A731C` (`slots.py`): 0 = text box (render `0x80046EF0`),
3 = choice `0x800501FC`, 4 = sub-inventory `0x80053328`, 6 = inventory `0x80054F1C` (installs `0x80056598`), 10 = file menu `0x800583EC`,
12 = name `0x8005A3E0`. Slot 0's release: `0x80046EF0` -> slide done & closing -> `jal 0x8004501C` at `0x8004704C`, which frees the slot
(`0x80047CB0`) and does `0x800DC4B8 &= ~0x18` (`0x8004503C-0x80045048`).

Update(0)(f): band counters `0x8002BB0C-0x8002BB58`; pad sampler `0x8002E38C` at `0x8002BB5C` (sample f); soft-reset block
`0x8002BB64-0x8002BC48`; **UpdateWorld** `0x8002E058` at `0x8002BC4C` = RunMapEvents `0x8003C67C` (`0x8002E100`), UpdateEntities `0x8003B388`
(`0x8002E108`, entities + pending triggers + camera look-at per the port's C1 notes), effects `0x8003C410` (`0x8002E110`); warp-delay decrement
`0x8002BC54-0x8002BC68`; **trigger** `0x8002BC6C-0x8002BD00`: `0x800DC4B8 == 0` (whole word, `0x8002BC70`), `0x80127D50`, `0x80127164`, pressed
`& 0x803` (`0x8002BCA4`), delay `0x800DC4B4 == 0` (`0x8002BCB8`), held `& 0x100` (`0x8002BCC8`), `0x800C4980 == 0` (`0x8002BCDC`),
DisplayInventory `0x80055570` (`0x8002BCEC`); `0x8004B1D4` at `0x8002BD04` (sound frame); RNG `0x8002BD0C-0x8002BD34`.

## 2. Port per-tick order at HEAD (F, `Alundra/Scripts/AlundraWorldProxy.cs`)

Engine first: every entity's `AlundraEntityScriptProxy.Update` (gate read at its own head, `AlundraEntityScriptProxy.cs:1193`), World.cs:443-491.
Proxy `Update` (`:2133`): game time `:2139`; recipe keys `:2146`; ticks `:2160`; load effects `:2164`; frame gate `:2177-2178` (`0x48` or warp).
**Loop 1** `:2206-2237`, per tick k: `TickPad.Update` `:2208`, square/choice words of k `:2209-2210`, portrait head point `:2211`,
**inventory Tick** `:2212` (warp-delay decrement `AlundraInventoryDirector.cs:354`, trigger `:366-371` with guards flags==0 `:385`,
Start/L2/R2 edge `:404`, warp `:419`, delay `:426`, Select `:432`, save screen `:441`; head guard dialogue open `:485`; setup poses MenuOpen
`:552`; or per-frame `:376`), sub-inventory Tick `:2213`, post-process `:2214`, portrait Step `:2222`, presenters `:2231`, `:2236`.
TryWire x7 `:2241-2247`. **Loop 2** `:2268-2318`, per tick k: effect source `:2270`; dialogue Pass on k-1 words `:2271-2274`; text/choice
presenters `:2278`, `:2282`; save screen on k-1 words `:2291-2293`; gate `:2295-2296`; no hero or blocked -> effect gate + `continue`
`:2297-2301`; hold flags `:2306-2309`; **map events** `:2311`; recycling `:2315`; effect gate `:2317`. After: last-tick words `:2322-2326`;
**pending gate re-read** `:2329-2330`; overlay flush `:2337`; refresh `:2344`; **pending triggers** (gated) `:2355-2364`; wall interleave
`:2371-2374`; **effects RunFrame** `:2381`; **camera** `:2404-2410`; backdrops `:2427-2430`; effect quads `:2434`; fade `:2443-2444`;
BGM fade `:2449`; warp `:2455`; **HUD loop** `:2466-2477`; freeze apply `:2490-2497`; CloseFrame `:2505`; music flag `:2517`; sound flush
`:2525`; warp departure `:2534`.

## 3. Mapping convention and where the block goes

Port tick k = binary frame k for the dialogue/save side (Pass(k) = RS(k) slots 0/3 on sample k-1; save screen = RS(k) slot 10) and for the world
(map events, entities, effects = Update(k)). For the inventory, tick k = Update(k)'s trigger + RS(k+1)'s slots 4/6, tail and portrait
(`AlundraInventoryDirector.cs:24-41`): consistent, provided the block runs at the END of tick k. HEAD runs it at the START (loop 1), i.e.
RS(k+1)'s inventory before RS(k)'s box and Update(k)'s world: MenuOpen posed one tick early, cleared one tick early, the trigger blind to the
box release and to the map events of its own tick.
Candidate order of tick k: TickPad(k) -> Pass(k-1 words) -> text/choice presenters -> save screen(k-1 words) -> gate -> [map events, recycling]
-> effect gate -> pending gate := gate -> **portrait head point -> inventory Tick (delay, trigger | per-frame) -> sub Tick -> post-process ->
portrait Step -> presenters**. Residual order difference (H, guarded): inside RS(k+1) the binary runs 0, 3, 4, 6, 10, tail; the port runs
4, 6, tail at the end of tick k and 0, 3, 10 at the head of tick k+1. No cross read: the trigger refuses while flags != 0 or the save
screen is active, the head refuses while slot 0 is armed, slots 6 and 10 are never armed together (J9) (F, guards cited in §2).

## 4. Measurements

Binary, real code (`bin/box_trigger.py`, `bin/menuopen_window.py`, output `bin-out.txt`; emu.py copied, scratchpad/IO mapped, nested-call
stack fixed, stubs: inv_bin.STUBS + `0x800455B4`, `0x80084EFC`, `0x80081AA4`):
- freeze window (map events skipped in Update(P+a..P+b)): main 1..37; L1 then sub close 1..74; L1, back, close 1..112.
- box opened by the real `0x800450F0` (mode 0: ctrl 0x08; mode 1: 0x10), Square at sample S: sound 7 in RS(S+1), release in RS(S+19);
  Start at S+19 -> DisplayInventory in Update(S+19), MenuOpen from RS(S+20); Start at S+18 lost; S+20 opens in Update(S+20).
- box opened inside UpdateWorld(P) (map-events place) with Start at P: ctrl 0x08/0x10, slot 0 armed, inventory never armed. Control
  (no box): armed in Update(P), MenuOpen from RS(P+1).

Port, real proxy through `DialogueBoxMontage` (`O78ProbeTests.cs.txt`, outputs `probe-out/{base,order,pending}/`):

| Measure | HEAD | order only | candidate (order + pending gate) | binary |
|---|---|---|---|---|
| map events skipped, main | P+0..36 | P+1..37 | P+1..37 | 1..37 |
| sub-inventory / back to main | 0..73 / 0..111 | - | 1..74 / 1..112 | 1..74 / 1..112 |
| effect gate frozen | P..P+36 | P+1..P+37 | P+1..P+37 | = map events (F code) |
| pending-trigger gate frozen | P..P+36 | P..P+36 | P+1..P+37 | = UpdateEntities (F code) |
| Start on release tick R=S+19 (S=46, R=65) | lost | opens at R | opens at R | opens Update(R) |
| map-event box + Start on P | inventory at P, box never (map events frozen) | box, 0x08/0x10, no inventory | same | box wins |
| 2 ticks/frame, open at frame 0 | both ticks frozen | tick 0 runs, tick 1 frozen | same | per tick |
| O-E19-77 schedules (10, 1 and 2 ticks) | reference | identical | identical | - |

## 5. Tests and pins that move

Full `Alundra.Tests` in the copy: HEAD 3206/3214 without `Musics/`, the 8 failures all "Musics not found" and green once copied (24/24 rerun of
their two classes), `order` 3214/3214, candidate 3214/3214
(`results-*/`, `trx.py`). **No existing test or pin moves.** Six trace files (`docs/hero-trace-389-{spawn,highground}-{fixed,free}step.txt`,
`docs/intro-trace-389.txt`, `docs/intro-programs-389.txt`) rewritten by the candidate run: identical to HEAD (`traces/`). So nothing pins the
order today: the red-first tests below are the only guard.

## 6. Red-first tests (`AlundraInventoryTriggerFrameOrderTests.cs.txt`, needs two one-line seams)

Seams: `internal IReadOnlyList<bool> EffectFrozenOfTickForTests => _effectFrozenOfTick;` and `internal bool PendingTriggersGateForTests` set
right after the pending gate. HEAD 7/7 red, `order` 6/7 (the gates test red), candidate 7/7 green (`results-redfirst-*`).
1. map events skipped "1..37" (HEAD "0..36"). 2. "1..74" and "1..112" (HEAD "0..73", "0..111"). 3. effect gate (P, P+1, P+37, P+38) =
(false, true, true, false), pending gate same (HEAD (true, true, false, false)). 4. theory modes 0/1: Square at frame 46, Start at 65 ->
after frame 65 box closed, inventory active, MenuOpen (HEAD: inventory idle). 5. theory (0, 0x08), (1, 0x10): box opened by the map-event
script at P with Start at P -> after frame P box open, flags exactly 0x08/0x10, inventory idle, still idle 40 frames later (HEAD: inventory active).

## 7. Candidate change (`apply_o78.py pending`, diff `candidate-pending.diff.txt`; the production edit also deletes the empty loop and
moves its comments). Closed file list: `Alundra/Scripts/AlundraWorldProxy.cs` (behaviour, 2 seams, comments of the pad loop, loop 2 and the
pending re-read); comments only: `AlundraInventoryDirector.cs` (class doc 24-41 and the trigger site note), `AlundraInventoryPostProcess.cs:9`
("per-tick pad loop"), `AlundraSaveScreenDirector.cs:436` ("TickPad holds only the edges of the last tick" becomes false inside the loop);
new `Alundra.Tests/AlundraInventoryTriggerFrameOrderTests.cs`; docs: plan row O-E19-78 and slice section, new ADR (next free number, 0050
today) + index, annotate ADR-0040 line 22 (its multi-tick consequence disappears), annex with these scripts. No engine, MGUI or converter change.

## 8. Risks

- Entity scripts unchanged: they read the gate at frame start (F `AlundraEntityScriptProxy.cs:1193`), already Update(P+1..P+37) at HEAD (H for
  the production entity path, not measured).
- Multi-tick frames: the pending-trigger pass keeps ONE gate per frame (pre-existing); map events and effects are exact per tick (measured).
- The portrait head point and the warp-delay decrement now follow the map events of the tick (binary order); no map-event reader of the delay
  (F grep); a map event moving the hero on the trigger tick changes the portrait start (H, binary-faithful).
- Inventory sounds of a tick now come after the map events' sounds (binary order) (H, harmless).
- On the wiring frame the inventory presenters tick once after TryWire (H, idle).
- Save screen closing on tick k + Start on k now opens like the binary (H, same structure, unmeasured).
- ADR number collision with the D-E19-102 cycle.
