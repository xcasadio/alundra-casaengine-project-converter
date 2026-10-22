E19.sm1 oracle vectors: 0x60 0x61 0x3C 0x3D (prepared 2026-10-18, read-only preparation; nothing in the repository or in the binary was changed)

What this is
------------
1384 vectors (184 curated, one family per named test: 0x60 38, 0x61 27, 0x3C 66, 0x3D 53; 1200 seeded random, 300 per opcode) produced by running the REAL handlers of ALUN_CD.EXE (France)
in the minimal MIPS-I interpreter of the annex (docs/plan-e19-sm-annexe/sm1-sm4/work/oracle/emu.py + lib.py, copied unchanged next to gen_vectors.py):
  0x60 = 0x8003F3F8, 0x61 = 0x8003F488, 0x3C = 0x8003E558, 0x3D = 0x8003E64C, and the REAL search 0x8003C954 (table 0x80098FAC + 4 * opcode).
Only two functions are replaced by no-ops: the id validator 0x8003C914 (its result is discarded; it is fatal for an invalid record id, which no corpus site has) and the debug
printer 0x8003C840 (illegal search type >= 12, never used by a vector: the binary exits there, so no oracle value exists for it).
Binary: SHA-1 a705f8dab665645af5dfaf8fb348c7926b5caa58 (checked by gen_vectors.py, written in the head of the file).

Files
-----
sm1-vectors.json   head object + one vector per line. Frozen: the tests read it, they never recompute an expected value.
sm1-vectors.sha256 SHA-256 of sm1-vectors.json (a test pins it).
read-map.tsv       the mapping of every byte the handlers read to a DLL member. A read that is not in this table is a STOP (the generator raises).
equivalent-mutants.txt the exhaustive proof that comparing a tile as unsigned cannot change a result (the one mutant no vector can kill).
ctx-differences.txt vectors whose result differs when the hero is only PlayerEntity and not in SpawnedEntities (groups 0x82 and 0x84 of 0x60/0x61 only; never 0x3C/0x3D).
Tools (copies, run from the folder they sit in): work/vectors/{sm1lib.py, gen_vectors.py, analyse.py, tests_def.py, realbytes.py, equivalent_mutants.py, emu.py, lib.py}.
Regenerate: python -B gen_vectors.py <outdir>   (about 16 s; deterministic: no clock, no environment; two runs are byte-identical, SHA-256 a8c599c9...2069 at preparation).

Format
------
head: { format "sm1-vectors-1", binary {exe, sha1, handlers, search, stubs}, layout {slot_stride, slot0, n_word, fields}, counts }.
vector: {
  id         "v60-c01" (curated: c = case, d = 0x3C eligibility, a = anonymous montage) or "v3d-r042" (random), unique;
  title      one line of English;
  op         0x60 | 0x61 | 0x3C | 0x3D (decimal in the JSON);
  code       the instruction bytes (2 for 0x60/0x61, 7 for 0x3C/0x3D), unsigned 0..255; the test appends 0xFF;
  n          N: slots 0..n exist; slot 0 is the hero. In the binary run the slots n+1..9 hold ATTRACTIVE JUNK (active, matching raw ids 0..7, riding the logic entity, tile near 0):
             a handler that read beyond N would change a result; none does (classify() raises on such a read);
  logic      index of the logic entity (the handler's a0). The owner (a1) is the hero in the binary run; the handlers never read it;
  slots      n + 1 objects: { status 0..4, ref (EntityRefId; -1 for the hero), flags (uint32), anim (AnimFlags, uint32), tile [x, y, z] (signed int32),
             riding / xcoll / parent / platform: null, a slot index k, or "foreign" (a non-null reference to nothing in the world), blocked (bool) };
  anon       slot indices that are bare proxies WITHOUT LogicContextEntity (DLL-only vectors; nobody points at them; the binary is unaffected);
  tags       free labels; "random" for the seeded ones;
  junk_beyond_n  true when slots above N were junk-filled in the binary run;
  binary     { v0 (the size the handler returns: 2 or 7), result (Result, always written, 0 or 1), reads (sorted names of every datum the handler read: "N", "pc-pointer",
             "code[k]", "slot[i].<field>"), matches (the search group, slot indices in buffer order, 0x60/0x61 only) };
  dll_hero_outside_list_same  true when the DLL model gives the same Result when the hero is only PlayerEntity (not in SpawnedEntities).
}

Reading in C#: System.Text.Json, every number read with GetInt64(); flags and anim can exceed int.MaxValue (0xFFFFFF7F): cast unchecked to uint (flags) and to int (AnimFlags, EntityRefId, tiles),
as AlundraSpawnIndexOracle does. The file is found by walking up from AppContext.BaseDirectory to docs/plan-e19-sm-annexe/sm1/vectors/ (same convention as AlundraSpawnIndexOracle.OutputFolder).

How a C# test replays a vector (montage "prod")
-----------------------------------------------
  - one EntityScriptProxy per slot 0..n, in order; Status, EntityRefId, Flags, AnimFlags, TileX/TileY/TileZ from the slot; IsPlayer = true for slot 0 only;
  - every proxy has its OWN non-null LogicContextEntity (new Entity), except the "anon" slots (null). riding / parent / platform = slot k -> slot[k].LogicContextEntity;
    "foreign" -> a new Entity that belongs to no slot; xcoll = slot k -> the proxy itself;
  - blocked -> BlockedByEntity = new Entity;
  - the context: SpawnedEntities = all proxies in slot order (production shape: WorldProxy inserts the hero at slot 0), PlayerEntity = slot 0;
    montage "ctx" (0x3C/0x3D and the hero vectors only): SpawnedEntities without slot 0, PlayerEntity = slot 0 (the shape of AlundraClassAndBranchOpcodesTests.Context);
  - program [code..., 0xFF], Result = 7 (never 0 or 1: every expected value then differs from the value the skipped opcode leaves), CodeIndex 0;
    owner = the logic proxy (so entity.LogicEntity is null) except the "owner = hero" tests, where owner = slot 0 and owner.LogicEntity = the logic proxy;
  - assert Result == binary.result, CodeIndex == size, the trace record of the opcode has Kind Implemented, and nothing else changed (every field of every proxy,
    Parameters[0], Parameters[2..9], Sp): the opcodes write Result only.
The test NEVER calls EntitySupport.UpdateRidingEntities: RidingEntity is set by hand, so no stale value can enter.

What the curated vectors cover
------------------------------
bounds (both bounds of each axis, one step outside, inverted box), signs (tiles -1, INT_MIN, INT_MAX; operands 0x80..0xFF read unsigned), sizes (tile 256, 261, 65536),
empty groups, null references (rides nothing; bare proxies without identity), foreign pointers, statuses 0..4 for the reference and for the candidates,
the hero (as logic entity, as member of groups 0x81 0x82 0x83, as sole occupant of a box, as PlayerEntity only), the first and the last slot (N = 4 and N = 9),
logic entity != owner, EntityRefId wider than a byte, 0x3C eligibility (Collidable, NoEntityCollision, PlatformEntity; only bit 0x80 of each word), the real box of 226 B[3] @247.
32-bit wrap: none of the four opcodes does arithmetic; the only 32-bit-sensitive value is the tile (signed compare against 0..255 bounds), covered by INT_MIN / INT_MAX / -1 / 256.

Checks done by gen_vectors.py on every vector (a failure aborts the generation)
-------------------------------------------------------------------------------
1. the real handler returns the expected size and writes Result in {0, 1};
2. nothing outside Result and the results buffer is written (whole window 0x80100000..0x80190000 diffed);
3. every data read is classified (read-map.tsv); an unclassified read, a read of a slot above N or of a state word is a STOP;
4. an independent python model written for this slice from the listings (sm1lib.model_binary) gives the same Result;
5. the model of the DLL (EntitySearchService + the four proposed `case` blocks, montage "prod") gives the same Result.
