# E19.sm4 value vectors (0x72 0x88 0x96 0x97 0xB3, and 0x15 0x66 0x7A with 0 site)

Provenance: read-only preparation of 2026-10-18 for the slice E19.sm4 (`docs/plan-e19-opcodes.md` section 1.4). Produced from COPIES of the
annex scripts (`../tools/`), never in place. Binary: `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` (checked), read
through `lib.py` at the absolute path it names (outside the repository). Nothing in the repository or the binary was changed.

## What a vector is

One execution of the REAL handler of the binary (table `0x80098FAC + 4*op`, the real search `0x8003C954`) in the MIPS interpreter of the annex
(`emu.py`, instrumented by `temu.py`). The expected values are what that handler did, never what a model says. A python model written from the
disassembly (`sm4model.py`) is run next to it as an agreement check: a disagreement stops the generator.

Files: `op72.json op88.json op96.json op97.json opB3.json` (the five opcodes of the slice), `zero-site/op15.json op66.json op7A.json`,
`mapping.json` (address -> field -> DLL member), `read-write-sets.json` (union of what each handler read and wrote), `counts.json`,
`MANIFEST.sha256` (sha256 of every file). Two runs with the same arguments (`gen_vectors.py OUT 20261018 60`) are byte-identical.

## Format of `opXX.json`

```
{ opcode, opcodeHex, label, size, handler, generator{seed, randomPerOpcode},
  vectors: [ {
    id            "op96/e07" (edge, hand written) or "op96/r012" (random, seeded)
    kind          "edge" | "random"
    note          what the case pins
    instruction   the bytes of the instruction (opcode first); the replay appends 0xFF
    size          the size the handler returns (= the table size of EventOpcodeSizeTable)
    logicSlot     the slot of the LOGIC entity (the handler's a0 = Dispatch's `entity`)
    ownerSlot     the slot of the OWNER (a1 = Dispatch's `owner`); no sm4 handler reads it
    world {
      slotCount   N + 1 (slots 0..N exist; slot 0 is the hero)
      slots[]     per slot: status hp hpMax refId flags animFlags hasPlatform parentSlot ridingSlot xCollSlot
                  targetAnimId targetDir lastTargetAnimId lastTargetDir
      stats       hp hpMax mp mpMax money          (PlayerStats, signed 16 bits)
      pad         hold pressed released interval   (TickPad words, unsigned 16 bits; only in opB3.json)
      stateBefore (only the zero-site files)
    }
    expect {
      returned    v0 = the number of bytes the pc advances
      result      the value written to Result, or null
      resultWritten  false when the handler never stores to Result (the replay keeps its own ResultBefore, 7)
      slotWrites  [ {slot, field, value} ]  FINAL value of every slot field the handler stored to (a store of the same value counts)
      statsWrites [ {field, value} ]        PlayerStats fields (signed 16)
      padWrites   [ {field, value} ]        TickPad words (unsigned 16)
      stateWrites [ {field, value} ]        program-state words other than Result
      matchedSlots  the slots the search returned (0x88, 0x96), in buffer order
      unchangedElsewhere  true: nothing else in the 0x80100000..0x80190000 window changed (checked on every vector)
    }
    reads         {mapped:[...], internal:[...], binary-constant:[...]}  every data byte the handler read, as named fields
    calls         hooked functions reached (CheckEntityRecord for the raw-id searches)
    today { returned, writes, redToday, changedFields }
                  what the DLL gives TODAY: the opcode is skipped by its table size (returns `size`, writes nothing, Result stale)
                  redToday = the real handler changes at least one observable value, so a test of this vector is RED today
    outOfTable, binaryStatusWord, policy   (0x88 only, index above 2) NOT a replayable value: see below
  } ] }
```

Signedness: `hp hpMax refId animFlags` and every `stats.*` are signed; `status flags targetAnimId targetDir lastTargetAnimId lastTargetDir`
and every `pad.*` unsigned. A pointer field (`parentSlot ridingSlot xCollSlot`) is a slot index or -1 (null). `hasPlatform` is
`PlatformEntity != null`.

## Mapping binary -> DLL (every byte read or written is mapped; an unmapped byte stops the generator, none was met)

See `mapping.json` for the full table. Summary of what the five handlers touch:

| binary | logical field | DLL member |
|---|---|---|
| slot i = `0x80127D30 + 0x294*i`, `N` = `[0x80134360]` | slot i, slotCount-1 | `IEntityWorldContext.SpawnedEntities[i]`, `.Count - 1`; slot 0 = `PlayerEntity` (`IsPlayer`, `EntityRefId = -1`) |
| `+0x0C` / `+0x10` / `+0x14` / `+0x18` | parentSlot / status / hp / hpMax | `ParentEntity` / `Status` / `Hp` / `HpMax` |
| `+0x28` / `+0x48` / `+0x6C` / `+0xB4` | hasPlatform / refId / flags / animFlags | `PlatformEntity` / `EntityRefId` / `Flags` / `AnimFlags` |
| `+0x88` / `+0x8C` / `+0x26C` / `+0x270` | targetAnimId / targetDir / lastTarget* | `TargetAnimationId` / `TargetDirection` / `LastTargetAnimationId` / `LastTargetDirection` |
| `+0x12C` / `+0x130` | ridingSlot / xCollSlot | `RidingEntity` (compared with the candidate's `LogicContextEntity`) / `XCollisionEntity` |
| `[0x80176318]` -> `+4` `+6` `+8` | stats.mp mpMax money (also hp `+0`, hpMax `+2`) | `GameState.PlayerStats.Mp/MpMax/Money/Hp/HpMax` (short) |
| `0x80126F28` `2A` `2C` `2E` | pad hold pressed released interval | `GameState.TickPad.ButtonsHold/ButtonsJustPressed/ButtonsReleased/ButtonsJustPressedByInterval` through the new seam `TickPad.Overwrite` |
| `0x80023D2C` (3 words) | status table | a new static table `{Normal, Deactivated, FlagToDestroy}` |
| `a2` -> instruction | instr[i] | `int[] v` of `Dispatch` |
| `0x8013D9E8 + 0x2C` | Result | `EventProgramState.Result` (never written by the five opcodes) |

What each handler reads (from `read-write-sets.json`): `0x72` only `targetAnimId`, `targetDir` of the logic slot; `0x88` the instruction
bytes 1 and 2, the status table word, and the search fields (`status refId flags animFlags hasPlatform parentSlot ridingSlot xCollSlot`, N);
`0x96` the instruction byte 1 (byte 2 is NEVER read), `hp`, `hpMax` of each match and the same search fields; `0x97` the instruction byte 1,
`stats.mp`, `stats.mpMax` (not `money`); `0xB3` the eight instruction bytes only.

## Replay recipe (C# test, DLL terms)

For each vector with no `outOfTable`:
1. Build `n = slotCount` proxies `new EntityScriptProxy { ... }` with `LogicContextEntity = new Entity { Name = ... }` (non null: the
   search compares pointers through it), set the scalar fields, then a second pass for the pointers: `ParentEntity` / `RidingEntity` =
   the designated slot's `LogicContextEntity`, `XCollisionEntity` = the designated slot's proxy, `PlatformEntity = new Entity()` when
   `hasPlatform`. Slot 0: `IsPlayer = true`, `EntityRefId = -1`.
2. Context: `PlayerEntity = slot 0`, `SpawnedEntities` = all slots in order. `GameState.PlayerStats` from `stats`. For `0xB3`: `TickPad.Overwrite`
   to `pad` (the seam) before the run.
3. Owner = slot `ownerSlot`; when `logicSlot != ownerSlot`, `owner.LogicEntity = slot logicSlot`. Run `instruction + [0xFF]` with
   `RunOneScriptCall`, `Result` before = 7.
4. Assert `CodeIndex == returned`, `Result == result ?? 7`, every `slotWrites/statsWrites/padWrites` field equals its value, and every other mapped
   field equals its value before. Vectors with `redToday == false` are green guards (they must also pass today).
Raw-id searches (instruction byte 1 below 0x80) stub the binary's id validator `0x8003C914` (fatal in the original on an invalid id): such
vectors pin the DLL, they are not states the original reaches with an invalid id. The 22 `outOfTable` vectors of `0x88` store the word the
binary reads past its 3-word table (`binaryStatusWord`); the slice's rule ignores them (nothing written, one warning), so they are not
replayed by value.

## Domain notes

`kind: random` and some edges use wide values (`Hp = 0x7FFFFFFF`, `Mp = 32767`, a negative `HpMax`) that the game cannot reach; they pin the
arithmetic of the binary (32-bit wrap for `0x96`, no 16-bit wrap for `0x97`). A slot above N is a trap in the binary run (active, matching
ids) to prove the loops stop at N inclusive; it does not exist in the DLL list.

## Reproduce

From a copy of `../tools/`: `python -B gen_vectors.py OUT 20261018 60`, `python -B gen_zero_site.py OUT/zero-site`, `python -B gen_mapping.py OUT`,
`python -B replay_vectors.py OUT` (replays every vector from the JSON alone on the real handlers: 348 vectors, 0 mismatches), then compare
`MANIFEST.sha256`. Agreement runs of the annex oracle and of the critic's oracle (copies): `../runs/`.
