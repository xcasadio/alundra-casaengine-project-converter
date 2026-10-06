# op22-disc: D-E19-94 / O-E19-68, the target of 0x22 (read-only discovery, 2026-10-06)

Repo `chantier/e19-suite` (HEAD 79bbe5b at start; 0472402 at the end, docs only). Nothing built, run, exported, edited, staged or committed inside the
repository (checked at the end: `git status` unchanged, `alundra-project/Alundra.dll` and `Alundra/bin/.../Alundra.dll` still 07:02:11).
Everything lives under `scratchpad/op22-disc/`:

- `bv/` binary side (copy of the h1b2 verifier harness `vz.py`/`real.py`/`corp.py` + mine: `all17.py`, `cycles.py`, `rand_rules.py`, `gen_rows.py`, `table.py`);
  the MIPS interpreter is `f3-disc/binary/emu.py` (CPU only), data = the exported project read-only.
- `repo/` scratch COPY of the sources (no .git, no bin/obj; `alundra-project/{Data,Dialogues,Entities,Maps,Musics,Sounds,Sprites,UI}` are junctions to the real
  read-only export; its own `alundra-project/Alundra.dll` is the copy's build output). It carries three EXPERIMENTAL patches that are NOT the plan's code:
  `AlundraEventProgramRunner.WaitHeightTarget` reads `OP22_RULE` (lit|shift|hyb|dir); `AlundraEntitySpawnFactory.cs:627` sets `Controller.Settings.MinMoveDistance = 0f`;
  the engine copy has `CharacterControllerSettings.MinMoveDistance` (default 0.001f, key `min_move_distance`) replacing the const `MinMoveDistanceSquared`
  in `CharacterControllerComponent.cs`. Probe: `repo/Alundra.Tests/Op22ProbeTests.cs` (real DLL, `ArcRun` on the real maps). Outputs: `probe-out/`.
- `suite-*.summary`, `f-*.txt`: full `Alundra.Tests` runs (Debug, 2857 tests) per variant; the baseline of the copy has 79 environmental failures (dialogue/choice/pixel
  families, they need `docs/plan-e19-f*-annexe` that I did not copy); all comparisons are differences against that baseline.

Tags: [bin] read in the disassembly (`../f2b1-disc/oracle/bdis.py`); [emu] REAL binary code run in the MIPS interpreter; [dll] REAL DLL (scratch copy) through `ArcRun`;
[code] read in the repo; [hyp] hypothesis.

## 1. The binary's true rule

### 1.1 The handler (`0x8003DA70`, size 1, called through the table at `0x80098FAC + 4*op`) [bin]
- `0x8003DA84-0x8003DA90`: `Parameters[1]` (`state+8`) compared with the pc cursor (`*a2`, an address): different = first call at this pc.
- First call (`0x8003DA98-0x8003DAC4`): `state+8 = pc`; `s0 = logic+0x44` (the entity record); `byte(s0+9) << 19` (`sll 0x13`) -> `state+0xC`; returns 0. NO equality test at the first call.
- Later calls (`0x8003DAC8-0x8003DB0C`): `target = state+0xC`, `PosZ = logic+0x11C` (not ModdedPosZ `+0x1E0`). `bne target,PosZ` (exact 32-bit equality, `0x8003DAD4`) -> return 1.
  Else `gap = target - PosZ` (delay slot of the `bne`, `0x8003DAD8`); `blez gap` (signed): gap > 0: `slt (gap < ForceZ)` -> `ForceZ = gap` (ForceZ is `+0xB8`);
  gap <= 0: `slt (ForceZ < gap)` -> `ForceZ = gap`; return 0. It only CLAMPS (never creates or reverses a force).
- `0x23` `0x8003DB28`: `jal 0x8003DA70`; result != 0 -> 1; else `+0x140` (CollidedWithEntityZ) != 0 -> 1 (also at the first call). `0x20` `0x8003D9BC`, `0x21` `0x8003DA28` untouched here.
- Formats: PosZ and ForceZ are signed 16.16 (1 px = 0x10000); Height byte is in units of 8 px: `Height << 19` = Height * 8 * 65536 (48 px = Height 6 = 3145728).

### 1.2 The movement it relies on [bin] + [emu]
- Tick order `0x8003B388`: scripts (`0x800386D0`) -> lists -> animation -> physics `0x80038364`; a `1B` (`0x8003D7D0`: `ForceZ = sext16(v1|v2<<8) << 8`) moves the entity in the same tick.
  `FinalForceZ (+0xEC) = ForceZ` (`0x80036BB8`), then `ComputeZ 0x800375E0`.
- `F > 0` (`0x800375FC...`): cap test `0x80036D94` (`ModdedPosZ + Depth + F > 0x7800000`, strict), else `PosZ += F` (`0x80037708-0x80037714`: `addu v0,PosZ,s1`).
- `F <= 0` (`0x8003768C...`, F == 0 included): landing test `0x80036BFC`: `t1 = ModdedPosZ + F` (`0x80036C20`), `slt v0,T,t1` (`0x80036C24`): skip the landing when `T < t1`;
  so it lands iff `ModdedPosZ + F <= T`, then `PosZ = (T + 1) - ModZ` (`t1 = T + 1`, `0x80036C30`; store at `0x800376F0-0x800376FC`), contact `+0x140 = 1`, `ForceZ = 0` only with the gravity bit.
  Otherwise `PosZ += F` (`0x80037708`).
- Spawn (`0x80039EA0-0x80039F04`): `PosZ = z - ModZ + 1` with `z = Height << 19` (`sll $t0,$v0,0x13` at `0x8003A2E0`), then `if !(T + 1 < PosZ) PosZ = T + 1`. End pass `0x800380F0-0x80038108`: `IsOnGround = !(FloorHeight < PosZ)`.
- So EVERY binary position carries a +1: rest = T + 1, spawn = z + 1, while the 0x22 target `Height << 19` has no +1. This is the whole story:
  an ascent from T + 1 ends with a CLAMPED last step (F - 1), the comparison then holds exactly at `Height << 19`; a descent from an aligned position X + 1 arrives
  at `target + 1` after its full steps and needs ONE MORE call whose clamped step is -1 (115: calls 318 PosZ_b 3178497 / 319 3145729 / 320 3145728 ForceZ -1 / 321 3145728 return 1) [emu].
- ADR-0026 frame: `PosZ_dll = PosZ_b - 1` (rest T). The binary comparison `PosZ_b == H<<19` is, in that frame, `PosZ_dll == (H<<19) - 1`, and the clamp gap is the same number:
  the true rule is the SHIFTED target, in every direction and at every height. Proof [emu]: the DLL-frame handler with target `(H<<19)-1` (variant `shift` of `all17.py`) gives
  the binary's call count at all 17 sites; algebraically identical (`PosZ_b - 1 == (H<<19) - 1` iff `PosZ_b == H<<19`; `gap` unchanged).
- Literal target (today) = the same comparison with the +1 dropped. It is right on an ascent only because every ascent in the corpus starts aligned (rest on T, a multiple of 65536,
  force dividing the distance): ceil((n*F - 1)/F) = n. It is one call short on every aligned descent and one call late on an ascent whose distance is 1 mod F.
  Random model (`bv/rand_rules.py`, 38478 waits, constant force, no terrain): `shift` equal to the binary in 100 % (the binary's own stalls reproduced), `lit` different in 100 % of aligned
  descents (6803/6803) and in 2254 misaligned ascents, `dir` (literal on ascents, shifted on descents) different in 2378 misaligned ascents, `hyb` different only above 256 px.

### 1.3 Per direction, per height, or the rule?
- "Per direction" (literal ascent / shifted descent) is only a fit: it equals the rule on aligned ascents, it leaves every ascent ending one unit above the frame, and it is wrong on
  any misaligned ascent (none in the corpus).
- "Shifted below 256 px, literal above" is NOT per direction, it is per REPRESENTABILITY: the controller's root is a float32 in pixels (`PosZ = Round((double)root.Z * 65536)`,
  `AlundraEntityScriptProxy.cs:1265` head pull, `:2204` pull in `MoveVerticalAndPullPosition`, step `Controller.Move(0,0,F/65536f)` `:943`). The float32 spacing is 1 unit up to 256 px and 2 units from 256 px,
  so an odd position cannot exist there: `(H<<19)-1` is representable iff it is < 2^24 (Height <= 32). 127 rec17 and rec18 (Height 38, 304 px) are the only sites above.
  There the literal target is exact in CALLS (192) because the ascent is aligned (the binary's last clamped step becomes a full step); the entity ends one unit above the frame.
  [dll] with the shifted target at 304 px the ball rises for ever (`ForceZ 32767`, PosZ 94830592 after 2478 frames).
- Therefore the author's text (shifted below 256 px, literal above) = the right rule + its only physical exception, and "one rule per direction" is a misnomer of the session.

### 1.4 The second constraint nobody saw: the engine drops the descent's last step
- The shifted target makes the descent at 115 end with a clamped step of -1 raw unit (= 1.5e-5 px). `CharacterControllerComponent.Move` returns at once when
  `requestedDisplacement.LengthSquared() <= MinMoveDistanceSquared` (`CharacterControllerComponent.cs:16` const `0.000001f` = 0.001 px = 65 raw units; test at `:444`; the same const gates `:258`, `:277`, `:1055`, `:1072`, `:1384`).
- [dll] real DLL, real map 115, real B[2] program (hero in the zone, dialogue button pressed): rule `shift`, `hyb` or `dir` WITHOUT engine change: the wait never ends (frame 6000: `PosZ 3145728, ForceZ -1`) and the hero never regains control (`11` @379 is after the wait): a soft lock.
  With the engine minimum at 0 (global experiment, then the per-controller setting): 321 calls, `PosZ 3145727`.
- So a DLL-only rule cannot satisfy D-E19-94. The consistent fix is in the engine (rule: engine gaps are fixed in the engine): a per-controller minimum displacement.

## 2. Proof on the 17 sites (binary vs today vs proposed)

17 = 16 `0x22` reachable (36 C[4]@491; 89 C[21]@663 on rec20..25 = one program, six entities; 115 B[2]@367; 127 C[5,6,7,8,10..17] = 12; 363 C[4]@454) + 1 `0x23` (36 C[14]@1252 on rec42..53). Chain: none (chain maps list `AlundraStoryChainOpcodeAudit.ChainMaps`: no 36, 89, 115, 127, 363; 115 is one of the 7 exempt combat maps).

Binary [emu] (`all17.py`, real P/RunScript/handlers/cells; calls = ticks between the first call at the pc and the call that leaves it), variants on the same physics (`all17-5variants.txt`):

| site | binary | today (literal) | shift | hyb | dir |
|---|---|---|---|---|---|
| 127 C[5],C[6],C[10],C[11] (160 px target 160/160/96/96) | 160 | 160 | 160 | 160 | 160 |
| 127 C[7],C[8],C[12..15] (240 px) | 192 | 192 | 192 | 192 | 192 |
| 127 C[16],C[17] (304 px) | 192 | 192 | 192 (integer model; real DLL: runaway) | 192 | 192 |
| 89 C[21] x6 (112 px, rise 68 px) | 68 | 68 | 68 | 68 | 68 |
| 363 C[4] (80 px, rise 48) | 48 | 48 | 48 | 48 | 48 |
| 36 C[4] (176 px, rise 160) | 160 | 160 | 160 | 160 | 160 |
| 36 C[14] `0x23` x12 | 0 | 0 | 0 | 0 | 0 |
| 115 B[2] (descent 208 -> 48 px) | 321 | 320 | 321 | 321 | 321 |

Real DLL [dll] (`site-table.txt`; frames of `ArcRun`, one logic tick each): today = same calls as the binary at 16/17, 115 = 320. Proposed (`hyb` + engine setting 0 on factory-spawned controllers) = binary calls at 17/17
(115: 321, first frame 756, end frame 1077, `PosZ` at the end 3145727). Per-site first/end frames and end `PosZ` today -> proposed:

| site (record) | first/end frame | calls | end PosZ today -> proposed |
|---|---|---|---|
| 115 rec19 | 756 / 1076 -> 1077 | 320 -> 321 | 3145728 -> 3145727 |
| 127 rec6, rec7 (H 20) | 20 / 180 | 160 | 10485760 -> 10485759 |
| 127 rec11, rec12 (H 12) | 20 / 180 | 160 | 6291456 -> 6291455 |
| 127 rec8, rec9, rec13..16 (H 30) | 22 / 214 | 192 | 15728640 -> 15728639 |
| 127 rec17, rec18 (H 38) | 22 / 214 | 192 | 19922944 -> 19922944 (literal fallback) |
| 89 rec20..25 (H 14) | 48 / 116 | 68 | 7340032 -> 7340031 |
| 363 rec0 (H 10, flag 32769 set after the map load) | 14 / 62 | 48 | 5242880 -> 5242879 |
| 36 rec3 (H 22, flag 32774 on then off at frame 3, hero placed on the landed platform) | 166 / 326 | 160 | 11534336 -> 11534335 |
| 36 rec42..53 `0x23` | 910 / 910 | 0 (contact already posted: ends at its first call, the clamp never runs) | 1048576 unchanged (ForceZ -16384) |

The 12 hanging ceilings (36 C[14]) never rise: the contact is posted by the landing and `0x23` ends at its first call in the binary [emu] and in the DLL [dll]; the plan text calling the site "an ascent at +16384/tick" is wrong.
The cycle of the 127 and 89 loops (fall, rise, restart) has the same durations under `lit` and `hyb` in the DLL (`probe-out/cycles.*`), and under `bin`/`lit`/`hyb` in the interpreter (`cycles.py`).
Side observation, not caused by the rule: the DLL cycle of 127 is 1 tick shorter than the binary's (180 vs 181 for rec6, 214 vs 215 for rec17: the fall); the rise itself is equal.

## 3. The exact rule for the port

`WaitHeightTarget`, first call at the pc (everything else unchanged: equality, one-sided clamp on `target - PosZ`):

```
literal = RecordHeight << 19;                       // the binary's value (0x8003DAC0)
target  = literal - 1;                              // the binary's comparison, in the DLL frame (PosZ_dll = PosZ_binary - 1, ADR-0026)
if (entity.Controller != null && target >= 1 << 24) // the float32 root cannot hold an odd position from 256 px (Height >= 33)
    target = literal;
state.Parameters[2] = target;
```
Heights of the corpus: 6, 10, 12, 14, 20, 22, 30 -> shifted; 38 with a controller -> literal; Height 32 (256 px) -> shifted 16777215; Height 33 -> literal if a controller, 17301503 without.
Required with it (engine): a per-controller minimum displacement (new `CharacterControllerSettings.MinMoveDistance`, px, default 0.001 = today, key `min_move_distance`), set to 0 by the spawn factory
for the controllers it spawns (next to `IsVerticalOwnedExternally = true`, `AlundraEntitySpawnFactory.cs:626`). Without it the DLL part is a regression (115 soft-lock, section 1.4).
Not exact and not in the corpus: a descent of a controller entity above 256 px (the -1 step is half a float32 spacing).
End pose: an ascent ends at `PosZ_binary - 1` (frame-consistent) instead of the binary's exact `H<<19`: TileZ (`PosZ >> 20`) of the entity itself is one lower when `H<<19` is a multiple of 2^20 (all 17 sites, H even).
No consumer at the 17 sites: the only readers of an entity's TileZ are `0x3B` (player), `0x07` (`AlundraEventProgramRunner.cs:2976`) and `0xAD`, and the five maps contain no `0x07`/`0xAD`/`0x3C`/`0x3D`;
the render snap `floor(Y - Z)` (`SimulationSpacePolicy.cs` top-down) does not change for Z lowered by one raw unit. An entity resting on the platform keeps its pose (rest on top = `P + Depth + 1` in both frames).
Alternative that keeps the exact end pose without any engine change (not recommended, a fit): literal target + on an aligned descent one extra no-op call; it stays wrong on misaligned ascents.

## 4. Values writable in advance (generators)
- Handler rows in the DLL frame, from the REAL handlers with `PosZ_b = PosZ_dll + 1` (`bv/gen_rows.py` -> `handler-rows.txt`): Height 20 (target 10485759): (10484999,0)->0/0; (10484999,32768)->0/760; (10484999,700)->0/700; (10485759,0)->1; (10485760,-5)->0/-1;
  (10485760,0)->0/0; (9999999,-32768)->0/-32768; (9999999,600000)->0/485760; (10485759,32768,contact 1)->1/32768; descent shape (10485760,-32768)->0/-1, (10485765,-32768)->0/-6; Height 30 (15728639): (15000000,0)->0/0, (15000000,32768)->0/32768,
  contact ->1, (15728639,0)->1; Height 38 without controller (19922943): (19890175,32768)->0/32768, (19922943,0)->1, (19922944,-5)->0/-1; Height 38 with a controller (19922944): (19890176,32768)->0/32768, (19922944,32768)->1, (19922943,32768)->0/1.
  Validated: the 4 changed tests of `AlundraHeightWaitOpcodesTests` plus a 5-row `Theory` (height, controller, memo) are green in the copy.
- Memo table: Height 6/10/12/14/20/22/30/32 -> literal-1 = 3145727/5242879/6291455/7340031/10485759/11534335/15728639/16777215; 33 -> controller 17301504 / bare 17301503; 38 -> controller 19922944 / bare 19922943.
- The 17 sites: `bv/all17.py` (binary), `Op22ProbeTests.cs` + `run_rule.sh` (real DLL), `bv/table.py` -> `site-table.txt`.
- Engine rows: `Move(0,0,1/65536f)` on root.Z = 48f returns Vector3.Zero by default (length^2 2.3e-10 <= 1e-6) and moves root.Z to 47.99998474121094f with `MinMoveDistance = 0`.

## 5. Tests and traces
- Old -> new (full suite of the copy with the rule: exactly four tests move, all in `Alundra.Tests/AlundraHeightWaitOpcodesTests.cs`; nothing else, with or without the engine change):
  `TZ22_TheFirstCallMemorisesTheLiteralTargetOfTheRecordHeight_AndLeavesTheForce` (:160; :174 `10485760` -> `10485759`, rename: it is no longer the literal target),
  `TZ22_ClampsTheForceToTheGapWhenItWouldOvershoot_AndNeverPushes` (:179; PosZ -1 on every row: :182 10485000->10484999, :183 10485000->10484999, :184 10485760->10485759, :185 10485761->10485760, :186 10000000->9999999, :189 10485761->10485760, :190 10000000->9999999, :193 10485760->10485759; results unchanged; comment :181),
  `TZ23_IsTheHeightWaitOrAZContact_...` (:222; :226 comment, :233 15728640->15728639), `TZ23_TheTargetEndsItToo_...` (:249; :254 `owner.PosZ = 15728640`->15728639, :269 15728640->15728639).
- Under `dir` only three of the four move (the first-call test passes: ForceZ > 0 picks the literal), i.e. the unit tests alone do not discriminate; the real-site tests do.
- Unchanged, measured: the six traces (`docs/intro-trace-389.txt`, `intro-programs-389.txt`, the four `hero-trace-389-*`: regenerated by the suite in the copy, identical to the repo's ignoring CR, for the final hyb build); every arc and every other test (2774 pass = baseline 2778 - the 4); the engine minimum at 0 alone moves nothing (79 = baseline).
  Reasons: no `0x22`/`0x23` on the 30 chain maps; the harness has no controller and no world.
- New tests to write first (red first): unit (rows above); real sites (`ArcRun`, `RealController`+`Prefabs`, hero `Arrival` at the gate tile, `OnInstruction` on slot/pc): 115 (calls 321, PosZ 3145727; red: 320/3145728),
  127 rec6 (160 calls, 10485759; red: 10485760), 127 rec17 (192, 19922944: green before and after, the guard of the fallback), 89 (68, 7340031), 363, 36 C[4], 36 C[14] (0 calls, guard). Engine: default drop, setting 0 applies, Load/Clone/Validate (negative refused), editor serializer round trip.

## 6. Files (closed list)
Parent: `Alundra/Scripts/AlundraEventProgramRunner.cs` (`WaitHeightTarget` :2530-2565, docs :2519-2529, :732/:735 comments), `Alundra/Scripts/AlundraEntitySpawnFactory.cs` (:626), `Alundra/Scripts/AlundraEntityScriptProxy.cs` (:84-88 doc of `RecordHeight`), `Alundra/Scripts/EntityRecordMapper.cs` (:196-199 comment),
`Alundra.Tests/AlundraHeightWaitOpcodesTests.cs`, a new `Alundra.Tests/AlundraHeightTargetSitesTests.cs`, `docs/decisions/0040-*.md` + `docs/decisions/README.md` row, `docs/decisions/0026-*.md` (Status line only: partly superseded), `docs/plan-e19-opcodes.md` (§1.2n.1c, D-E19-94, O-E19-68, new task), a new annex `docs/plan-e19-op22-annexe/`.
Engine submodule (`CasaEngineMonogame`, branch + ADR first): `CasaEngine/Framework/Scene/Entities/Components/CharacterControllerSettings.cs`, `.../CharacterControllerComponent.cs` (7 uses of the const), `CasaEngine.EditorServices/EditorEntityJsonSerializer.cs` (:495), `CasaEngine.Tests/Physics/CharacterControllerSettingsTests.cs`, `CasaEngine.Tests/Physics/CharacterControllerComponentTests.cs`, `docs/decisions/0057-*.md` + index; then the submodule pointer in the parent.
Not touched: the converter, the export, the harness, `IntroTraceHarnessTests`, the TSV of skipped opcodes, `CasaEngine.Launcher/Program.cs`.

## 7. Risks
1. Order: DLL rule before the engine setting = soft lock at 115 (and a runaway if the 256 px exception is forgotten). One commit order: engine, pointer, DLL.
2. `MinMoveDistance = 0` on every factory-spawned controller: measured neutral on the 2774 passing tests and the arcs, not on the author's recipe (all NPC movement).
3. The default of the setting: `0.001f * 0.001f` = 1.0000001e-6 vs the const 9.99999997e-7: indistinguishable, but not bit-identical; keep the squared form if bit-identical defaults are wanted.
4. 127 rec17/18 end one unit above the frame (literal fallback); a controller descent above 256 px would be one call early (no site).
5. End TileZ of the platform/ball one lower (no consumer found), see section 3.
6. The `CasaEngine.Tests` suite was NOT built or run (the copy does not carry the editor projects): the engine tests are to be run by the plan (build explicitly, the .sln excludes the project).
7. `dotnet test` rewrites the four hero traces in the real repo: restore them as H1B2-2 did.

## 8. Question genuinely open (one)
D-E19-94 assumed a DLL-only rule. It is not possible: exactness at the one descent (115) needs an engine change (per-controller minimum displacement) or the extra-tick fit.
(A) engine setting (recommended: exact, small, default unchanged, consistent with "engine gaps are fixed in the engine"); (B) keep the literal target, 115 stays one call short (O-E19-68 stays a documented gap, no engine work); (C) the DLL fit (literal + one no-op call after an aligned descent, pose exact, no engine change, wrong on misaligned arrivals).
