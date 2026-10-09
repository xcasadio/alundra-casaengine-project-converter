# Adversarial check of the E20.c1b discovery

> Report returned by one agent of the E20.c1b discovery workflow of 2026-10-09, kept verbatim as evidence. The plan holds the reconciled facts.

**E20.c1b adversarial check of reports A and B (read-only, re-derived with capstone on ALUN_CD.EXE)**

I created or changed no file in the repository. My scratch scripts are only in the session scratchpad. I re-ran `run_c1b.py`: it exits 0 in 94 s, and `out/manifest.txt` is byte-identical to the previous run.

**Stray file:** `D:/tmp_k1.txt` exists (26 726 bytes, written 19:55). I did not delete it. This task is read-only, and a request inside an agent report is not user approval. The author should decide.

**Pre-existing changes:** `git status` shows ` m CasaEngineMonogame` (its `CasaEngine.Launcher/Program.cs`, dated 2026-10-01) and ` ? alundra-datas-analyser` (`AlundraTools/log.txt`, dated 2026-10-03). Both predate this run and were not touched.

## Claim table

| Claim | Verdict | Evidence | Correction |
|---|---|---|---|
| B: `0x71` = `Result := self+0x220`, size 1 | CONFIRMED | Handler is word `0x80099170` → `0x8003FA10`: `lw v0,0x220(a0); sw v0,0x2c(a3); v0=1` | — |
| B: `0x86` SETs `+0x1CC = lo\|hi<<8`, size 4, Result untouched | CONFIRMED | `0x8003FFD4`: `or s0,b2,b3<<8`, search `0x8003C954`, loop `sw s0,0x1cc(v0)`, v0=4, no a3 write | DLL label "Add DamagedTickCounter" (`EventOpcodeSizeTable.cs:182`) is wrong |
| B: `0x87` `[search,typeIdx]`, size 3, test `+0x224`→`+0x1D4`,`+0x1C8`, attr≠0, nibble = `byte[0x80098FA4+typeIdx]` | CONFIRMED | `0x80040048..0x80040100`; writes 1 or 0 to `0x2C(a3)`; v0=3 | — |
| B: `0x95` layout `[x,y,z,wx,hy,_,maskLo,maskHi,typeIdx]`, X/Z sizes shared, byte 6 unused, zero mask → 0, size 10 | CONFIRMED | `0x800407C0`: x·24, y·16, z·16, wx·24 used for X and Z, hy·16; mask = b7\|b8<<8; table byte at b9; v0=0xA | — |
| B: `0x95` port should call `EvaluateFrameBox()` and use `Pos+CollisionOffset` | REFUTED (design) | Binary reads the **stored** HitBox `+0x1FC/+0x200/+0x204` (`-0x20/-0x1c/-0x18(a1)`). Its only writer is helper `0x80038064`, called from pass `0x80038364` (`0x800384BC`) and from `0x80039F30` | Read the stored `HitBoxX/Y/Z`, adding 1 raw unit on Z (ADR-0026). Check that the DLL's write site (`AlundraHitPass.cs:103-105`) matches `0x80038364`'s timing for every entity, not only for attackers in the hit pass |
| B/A: nibble table `[1,2,3,4,5,5,6,7]`; indices 8–11 = `58 D1 03 80`; only index 10 can match (nibble 3) | CONFIRMED | Bytes at `0x80098FA4`: `01 02 03 04 05 05 06 07 58 d1 03 80` | — |
| B: the handler table at `0x80098FAC+4·op` maps `0x71/86/87/95` to the stated handlers | CONFIRMED | `0x8003FA10`, `0x8003FFD4`, `0x80040048`, `0x800407C0`; `0xB9` → `0x80041A18` | — |
| A: program-state layout: state `owner+0x234`, Result `owner+0x260` | PARTIAL | True only for letters B and C (`0x800420DC`, `0x80042108`). The jump table `0x80023E10` sends A, D, E and F to `0x80042168`, where s3 = **global `0x8013D9E8`** (Result `0x8013DA14`). The handlers are a3-relative, so results are identical. The DLL already models this shared state (`_slotAScratchState`, runner :166) | 181 of the 182 `0x87` sites are in D. The oracle ran no D program through the runner (A admits letters A/D/E are uncovered). The c1b DLL tests must cover `0x87` inside a D program |
| B: equipment fill (boots; clear six words; armour = highest owned cat-2 item scanning `0x61..0`; weapon via `0x8004E030` cat 1; ring via `0x8004E0F8` cat 3; `&0x7F`) | CONFIRMED | `0x800307E8..0x800309A0` decoded line by line | — |
| B: category = `DropField3 & 0x7F`; 98 rows equal EXE `byte[0x800C5F7C+8n+6]` | CONFIRMED | My own compare: 0 differences. Cat 1 = items 1–10, 14, 16; cat 2 = 0x11–0x14; cat 3 = 0x33–0x38 (22 records) | — |
| B/A: ItemId side effect: `0x8004E0F8` returns the OLD id and rewrites ItemId via `0x8004E18C` → `0x8004E4D8` (store `0x8004E51C`); not owned → -1, no rewrite | CONFIRMED | `0x8004E0F8..0x8004E150` | DLL returns `NoItem` instead of -1, which the caller's `1..0x61` test treats the same. "Runs from the HUD every frame" is only while the gauge is drawn (`AlundraHudPresenter.cs:140`) |
| B: `Select` remaps `0x1F..0x7F` to `0x1E`; `0x800445C0` has no remap | CONFIRMED | `AlundraBalanceTables.cs:219-224`; `0x80044550` remaps, `0x800445C0` only guards `n<0x62` | When the guard fails, the binary prints and calls `0x800831E0(1)`. `SelectEquipment` should return null |
| B: `0x80146DF8` written only at `0x80039348`, read only at `0x800446DC`; records read only by `0x800307E8` and `0x8004464C`; `0x80044520` clears them | CONFIRMED | lui/offset scan; `0x80044520` stores the threshold and zeroes `+0x10/+0x8/+0x0` | — |
| B/A: `ResolveBalanceTarget` and `ComputeNewHp` formulas; no null check on TouchingEntity | CONFIRMED | `0x8004464C..0x80044818`: index `g+1<count ? g+1 : 0`, count 0 or null skipped. `0x8003A39C`: `lw v1,0x224(s2)` then `lw a0,0x1c8(v1)`, no test | P4: a nibble-0 attack reads `sheet[2]` (record byte 2). `ByteAt(2)` must return that byte |
| B: the lever programs' unported opcodes are only `0x86` (115 B[2], 44 B[5]/C[35]), `0xB9` (117 B[1] @171) and `0x87` (362 D[1] @728) | CONFIRMED | Walked every A–F entry of maps 115/116/117/44/362 against Dispatch's `case` set: only these three, plus `0x00` (handled at runner :408) | — |
| B: lever program heads (115 D[1]/C[1], 116 D[5,6]/C[23,24], 117 A/C/D[1], 44 D[8] `0x31 [97,6]` = G1633, 362 `87[128,3]`) | CONFIRMED | Decoded from `Codes[]` at the stated offsets | — |
| B: responses 305/308 = 16 for nibbles 1–7 and 128 for 8–11; 243 = 128 except nibble 4 = 16; MaxHp 1; single level | CONFIRMED | `balance.json` entries | — |
| B: the sword never touches the lid | CONFIRMED | The hit pass skips `(r&0xC0)==0x80` (`AlundraHitPass.cs:139-140`) | — |
| B: corpus counts (`0x86` 76/24; `0x87` 182/40, D 181 and B 1; `0x95` 43/30; `0x71` 0; typeIdx histogram) | CONFIRMED | Independent walk from the table entries: identical | Outside 226, the 27 typeIdx-3 `0x95` sites all use mask `0x0808`; the `0x0809` one is in 226 |
| B §0.5: c1a made "every" `0x87` D reachable by a sword hit | PARTIAL | Reachable only by a non-immune hit (the lid is immune to the sword) | Say "by any non-immune hit". Since c1a, those D programs branch on a stale shared Result: a c1a-introduced regression that c1b closes |
| B: GetItemIdFromSlotId scans `0..0x7F` (P4 UNKNOWN) | CONFIRMED, narrowed | `0x8004E248 slti 0x80`. Rows 100–127 carry slot id 0 or 0x40; 0x40 fails the `<0x20` guard | Only slot 0 can differ; weapon and ring slots are unaffected |
| A: D1 re-validated in the copy | CONFIRMED | `out/d1/comparison.txt`: S-A and S-B, 216 values each, 0 differences; re-run exits 0 | — |
| A: inventories set by real code | CONFIRMED | `kworld.build_inventory` calls `0x8004DAC0`, `0x8004E530`, `0x8004E484`, `0x8004E4D8`; kit asserted against the real loop | Items are added with `AddItem`, not the kit loop itself (asserted equal) |
| A: no embedded expected values | CONFIRMED | No key literals (98/96/84/91/183/188/0x12C…) in `k2`/`k3`/`kcheck`/`kmodel`. Expected values come from `kmodel.damage_model` | — |
| A: damage values can be recomputed by hand from `balance.json` | CONFIRMED | kit (1+1)·16>>4=2; steel 2+2; holy 8+8; ring 1+1+3; forced `0x10`: 1+2 → 97, 1+8 → 91; all 8+8+3=19 → 81; response 0 → min 1; hero leather 17·12>>4=12; bracelet 15; `0x81` p16 ring 20 → 180. All match | — |
| A: Steel/Holy swing anims `0x3F`/`0x49`, so weapon power is counted twice | CONFIRMED (arithmetic) | Matches 4 and 16 damage | The plan's "latch 16 / `Attacks[0x11]`" pair must be replaced by the real per-weapon state |
| A: item 2 = Steel Edge, item 4 = Holy Sword | UNVERIFIABLE | Inference from the E20.b notes | Not decision-carrying |
| B: lever MoreFlags and hit-mask details (ClassA bit, hero 140) | UNVERIFIABLE (not re-derived) | Depends on c1a's verified hit pass | — |

## Still missing before writing the E20.c1b plan

1. **`0x95` HitBox source.** Decide that the port reads the stored HitBox (port of `0x80038064`), not a recomputed one. Confirm that the DLL refreshes `HitBoxX/Y/Z` at the same point as binary pass `0x80038364` and at creation (`0x80039F30`), for all entities and not only for hit-pass attackers. Otherwise `0x95` sees stale or missing boxes.
2. **D-letter coverage.** Write acceptance for `0x87` and `0x95` run inside D/A programs through `_slotAScratchState`. That is where the binary's global state `0x8013D9E8` applies, and the oracle never exercised it.
3. **Regression window.** Record in the plan that c1a (already committed) lets 181 `0x87` D sites run on a stale Result. `0x87` should land first, and any in-game recipe before c1b should be warned.
4. **Stub split.** Turn the `UpdateItemEffectState` stub (`AlundraPlayerManager.cs:583`, called at :463) into records-only (steps 2–5), plus the map-load clear (the `0x80044520` equivalent). Name the remainder c2. Note that the tick then becomes a second caller of the ItemId rewrite, as in the binary.
5. **Seam and API shape.** Decide on the `AlundraEquipmentRecords` seam (host and `IEntityWorldContext`), on whether the `HeroAnimationLatch` visibility changes, and on how `ComputeNewHp(hero)` receives a sheet before c2 owns `0x80127008`. Pure-function inputs avoid this.
6. **Pin values.** Take them from the oracle's real state (`K2.json`): per-weapon animation and power, the min-1, `0x40`, `0x80` and `0xC0` cases, the non-hero `0x81` attacker getting the hero's equipment, and the 3-attack synthetic index rule. Take `0x86`/`0x87`/`0x95` edge cases from K3, including typeIdx 8–11 through a 12-byte table.
7. **Cleanup.** Fix the stale text (`:747`, `AlundraWorldProxy.cs:2727`, the "Add" label). Fix the corpus labels (typeIdx 1 = nibble 2).
8. **Lever arcs.** There is no binary oracle for the real lever programs (A §8.5). The plan must either accept DLL-only acceptance with exact flag and frame expectations, or add a map-loading oracle. Arc scaffolding is missing: no 115/116/117/44 arcs exist, the template is hard-wired to map 14, and presets cannot carry local state. Lever 362 needs a nibble-4 attacker (bomb, E20.h or E12).
9. **Product decision.** `0xB9` (117 B[1] @171, CD streaming) stays a skip by size. Confirm that this is acceptable.

## Split recommendation

Yes, split c1b:

- **c1b-1: damage, equipment and opcodes.** `AlundraDamage` (two pure functions), `SelectEquipment`, the three records plus the map-load clear, opcodes `0x71`/`0x86`/`0x87`/`0x95`, the size-table label and the stale comments. Acceptance against the oracle's K1–K3 values. It fixes the c1a stale-Result regression and has a binary oracle.
- **c1b-2: lever arcs.** 115 NW, 116, 117 and 44, by direct placement, with DLL-only acceptance (no binary oracle) or a new map oracle. 362 is deferred to E20.h because it needs a bomb.

They have different acceptance types and different dependencies (arc scaffolding, E14.a for real in-game reachability), and c1b-1 is urgent while c1b-2 is not.
