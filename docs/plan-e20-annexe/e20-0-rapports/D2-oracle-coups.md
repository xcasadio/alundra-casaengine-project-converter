# D2: the hit-pass oracle (summary; full notes in ../d2-oracle-coups/notes.md)

> Report returned by one agent of the E20.0 workflow of 2026-10-09 (docs/plan-e20-heros.md section 1.2), kept verbatim as evidence.
> Where it disagrees with its check or with the critic, the plan section 0.2 holds the reconciled fact.

**D2 result: PASS on all six validation cases.** The three functions (`0x80039300`, `0x8003A374`, `0x8004464C`) run as real `ALUN_CD.EXE` code on synthetic entity sets. `python -B tools/run_all.py` rebuilds `out/` in about 25 s and exits 0.

## Validation cases (`out/cases.txt`: inputs, expected, got, verdict for every run)
| # | Case | Runs | Verdict |
|---|---|---|---|
| 1 | Reordered class mask: HitsClassA-only hits ClassA and not ClassB; HitsClassB-only hits ClassB and not ClassA | 2 | PASS |
| 2 | Overlap in raw 16.16, axes X, Y and Z, (n,m) = (1,1), (5,4), (16,12): `d=(n<<16)-1` hits, `d=n<<16` misses, `d=-((m<<16)-1)` hits, `d=-(m<<16)` misses | 36 | PASS |
| 3 | Response `0x80`: no TouchingEntity, `+0x1D0=0x19`, HitCounter +1. Response `0xC0`: TouchingEntity set, HP unchanged | 2 | PASS |
| 4 | Minimum damage: `power*r>>4 = 0` still costs 1 HP | 3 | PASS |
| 5 | First attacker: only the first of the list touches the target; the second one's HitCounter stays 0 | 3 | PASS |
| 6 | Several targets: both are touched, HitCounter 2 | 1 | PASS |

Eleven extra cases, all PASS: skip conditions, class C, effects 4/5, zero sizes, spark centre, equipment sum, hero sheet, `0x40` kill, latch (empty and non-empty list) and counters pass.

How the cases were built:
- The expected values are hand-written from the plan, not computed by the emulator.
- Sheets and attacks are encoded from `alundra-project/Data/balance.json`. The encoded blob equals the disc `DATA/BALANCE.BIN` byte for byte (613 records, 0 differences).
- The one exception is `0xC0`: no corpus record has it, so that case uses a copy of a real record with that byte changed.

Evidence beyond the six cases:
- **Mutations:** 13 one-word patches of the emulated binary are each caught by the case I named in advance.
- **Random sets:** 3000 sets (seed `20261009`) agree with an independent Python model on every checked value. The sets include 1363 hits, 525 touches, 24 touches with `0xC0` and 153 with the equipment bit.
- **Coverage:** every non-debug instruction of the hit pass, the spark routine, `ComputeNewHp` and `ResolveBalanceTarget` is executed. The only unexecuted code is the printf branches taken when `[0x800DC058] < 0`.
- **Second interpreter:** 300 random sets give byte-identical results on `emu.py` and `mips.py`.
- **Unset-byte tracker:** no read of an unwritten byte in any run, and a check shows the tracker reports one when it should.
- **Determinism:** two complete runs give identical outputs.

## Facts that differ from the plan or from D1
- **N1: the random-state address in D1 and plan 1.2 D1 is wrong.** The state word is `0x80098708`, not `0x800A8708` (`lui 0x800a; lw -0x78f8`, at `0x80032988` and `0x8003920C`). The EXE image holds `0xB017C93D` there. D1's seed write went to an unrelated data word (`0x0004FFFF`). S-A/S-B never draw random numbers and still pass, so D1's result is not affected. D2 seeds the right word.
- **N2: effects 4 and 5 are attached only if the response is not `0x80`.**
  - An immune hit attaches nothing, but still sets `+0x1D0=0x19`, increments HitCounter and creates the 3 sparks.
  - Attribute 7 gives effect 5 only. Attributes 6 and 10 give effect 4; attributes 7 and 9 give effect 5.
- **N3: the box size is stored as `(n<<16)-1` only for n ≥ 1.** For n = 0, a target size is stored 0 (`0x80039CC8`, `CE0`, `CF8`) and an attacker frame size is stored −1 (`0x80038C80-88`, no zero test).
- **N4: the hit pass uses the target's own `+0x1C4` record for the response, the hero included** (`0x800395D4`). `ComputeNewHp(hero)` reads `0x80127008` instead (`0x8003A3B0`). The two can differ, which E20.c1 must reproduce (case `x7`).
  - FACT: the only store with displacement `0x1C4` is `0x80039E38` (InitializeEntity).
  - INFERENCE: the hero's `+0x1C4` is never re-pointed. A displacement scan cannot exclude stores through a biased base register.
  - This answers D1 open issue 3 as far as the evidence goes.
- **N5: no record in `balance.json` has a `0xC0` response.** The branch exists in the code, but nothing in the file reaches it.
- **Attribute byte:** no attack has a non-zero attribute byte with a zero low nibble (such a byte would read `MaxHp` as its response).
- **Everything else in §0.2.5 on the hit pass is confirmed:** the damage formula, the animation latch, the first-attacker rule, multi-target hits, the counters pass and the reordered class mask. The latch happens even with an empty list, because it sits in the delay slot of `blez` at `0x80039344`.

## Files
All under `D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/d2-oracle-coups/`:
- `notes.md` — how to run, memory layout, balance-record encoding and source, decoded facts, limits
- `out/` — `cases.txt`, `cases.json`, `random.txt`, `mutations.txt`, `diff2_hits.txt`, `coverage.txt`, `balance-encoding.txt`, `tracker_check.txt`, `dis_hits.txt`, `scan_fields.txt`, `scan_sheet.txt`, `run.log`
- `tools/` — new: `hitworld.py`, `cases.py`, `model.py`, `random_sets.py`, `mutations.py`, `diff2_hits.py`, `balance_enc.py`, `dump_dis.py`, `scan_fields.py`, `scan_sheet.py`, `tracker_check.py`, `run_all.py`; copied unchanged from D1 (`cmp` equal): `emu.py`, `lib.py`, `machine.py`, `bank.py`, `world.py`, `mips.py`, `interp2.py`

## Open issues
1. The sets are synthetic; hit boxes are written directly, not derived from sprite frames (D6).
2. The D-handler dispatch is not run; `ComputeNewHp` is called for each touched entity in slot order, standing in for the D handlers.
3. The effect functions are stubs: only ids, targets, spark centres and the random-state advance are checked.
4. Whether any equipment can produce a `0xC0` response in the hero's merged sheet is UNKNOWN (D4).
5. The pad, physics and animation passes are not run here, so D2 says nothing about the tick at which a hit lands in the DLL (D7 and E20.c1).
6. Debug branches (`[0x800DC058] < 0`) are unexercised by design.

I did not use git, and `git status` shows only the `d1-oracle-moveplayer/` and `d2-oracle-coups/` annex folders as new.
