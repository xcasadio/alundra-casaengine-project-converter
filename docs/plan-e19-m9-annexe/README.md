# E19.m9 annex (discovery, O-E19-34 / M-09) - what the InitializeEventData clear changes

Measured at `git rev-parse HEAD` = 3530eadf3097692ce830d450658dae9f85b37494 (branch chantier/e19-suite3). No production file was changed.

| file | content |
|---|---|
| `binary.md` | parts 1 and 2: every store of InitializeEventData 0x80041EE4 (only +4 and +0), the state layout mapped to the port, which state RunScript passes per slot, the stateful opcodes (port line and binary address) |
| `census.tsv` | part 3: one row per (map, slot, program) whose first instruction is a stateful opcode; columns map, chain/off, slot, program, entry_pc, opcode, operands, entities, class |
| `census-summary.txt` | part 3: totals by slot and opcode, chain vs off-chain, chain site list, first-opcode histogram per slot |
| `dynamic.md` | part 4: calls-to-pass as-is vs a "keep Parameters[1..9]" emulation (synthetic D/F, real B via RunMapEventsPass, real C after a clear, replica of an existing test) |
| `impact.md` | part 5: tests, arcs and traces that could move |
| `ZzE19m9InitDataCensusTests.cs.txt` | the throwaway test that produced census.* and dynamic.md (deleted from Alundra.Tests) |

Reproduce: copy the `.txt` to `Alundra.Tests/ZzE19m9InitDataCensusTests.cs`, then
`python <scratchpad>/lockcheck.py`, `dotnet build Alundra.Tests/Alundra.Tests.csproj -c Release -v q -nologo`,
`dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --blame-hang-timeout 300s --filter "FullyQualifiedName~ZzE19m9InitDataCensusTests"`.
Binary facts: `python docs/plan-e19-backlog-1006-annexe/mipsdis.py <addr> <n>`; the handler table (0x80098FAC) and the RunScript jump table (0x80023E10) were read
with a 10-line script over `ALUN_CD.EXE` (word at `0x800 + addr - 0x80020000`); the per-handler scan of `n($a3)` accesses (n in 8..0x2C) was linear up to the first
`jr ra` past the furthest forward branch, so a handler that moves `a3` (0x22 was found by hand) can be missed.

Approximations:
- Census: roots as `AlundraStoryChainOpcodeAudit.ReadRoots` (index & 0x7F, nonzero, in the table, table[index] != 0); bit 0x80 (global table) is ignored; a program counts
  once per (map, slot, index) with `entities` = number of records using it. Only the FIRST instruction is examined (a later wait is zeroed by the advancing instruction before it).
  Programs reachable only by a jump into another program's body are not listed.
- Dynamic: "keep" is a hand emulation in the test (InitializeEventData, then Parameters[1..9] restored, then RunOneScriptCall); the real RunScript does the A/D/E/F `g_` shared state
  with a private scratch object, so the emulated global state is one object per Harness. The C "cross-entity clear" is set by hand (Sp = 0, Codes = null), not produced by a program.
  B zone patterns (once / flicker) are hypotheses about the hero's movement, not recorded play. 0x1E has one site and the entity does not move in the test, so it never ends (not informative).
- Impact: not run; inferred from the census and from reading the tests.
