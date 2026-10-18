Replayable vector files of E20.h1 (the instant items), binary ALUN_CD.EXE (France), SHA-1 a705f8dab665645af5dfaf8fb348c7926b5caa58.
Same format as docs/plan-e20-annexe/e20-h4b/bin/vec/README.txt, with four additions (stubs, calls, class, label).

One JSON file per function, {"function", "name", "vectors": [...]}.  A vector:
  {"id": n, "fn": "0x80034680", "args": [a0, a1, ...],
   "reads":  [[address, size, value], ...],   every DATA access (1, 2 or 4 bytes) whose bytes were not yet written by the call; first occurrence, in execution order;
                                                 includes the words of the EXE's own tables that the code read (the ring tables 0x80023654 / 0x80023694, the jump table 0x8002339C)
   "writes": [[address, size, value], ...],   final value of every (address, size) written, in execution order, EXCLUDING the callee's own stack frame (0x801FFE00..0x801FFEFF)
   "ret": v0, "ret_v1": v1,
   "stubs":  {"0x800490fc": [returns...], "0x8003bdd8": [pointers or 0...], ...}   the callees replaced by journalled hooks, with the values they return, in call order (everything else runs for real)
   "calls":  [["0x800490fc", [3]], ["0x8003bdd8", [0, 14, 0, x, y, z]], ...]       the calls the hooks saw: register arguments (and, for 0x8003bdd8, the two stack arguments y and z)
   "class":  "game" | "synthetic"      game = an input the game can produce; synthetic = robustness input (negative or huge life, an item argument outside the table, a count of 0, a full effect pool is NOT synthetic)
   "label":  what the vector is}
Values are unsigned 32/16/8-bit integers (two's complement for negatives).  Addresses are PS1 virtual addresses (0x80xxxxxx).
The memory of the harness (any RAM address works, the code reads through pointers): hero 0x80127D30 (and 0x801E4000 for the vectors where a0 is another entity), stats block 0x801E1000 behind
[0x80176318], inventory table 0x801E2000 behind [0x8017631C] (4 bytes per item, count = signed halfword at +2), [0x8017638C] = 0x63, effect objects 0x801E8000 + 0x100 k.
An effect object a stub hands out is initialised as InitEffect 0x8003BB14 leaves it (+0x3C/+0x40/+0x44 = x, y, z; forces +0x54/+0x58/+0x5C = 0): the first reads of the ring code (+0x3C, +0x40)
therefore carry the creation position, and the final writes carry position + offset.
Replay from an EMPTY memory: python -B ../replay_h1.py   (the real MIPS code runs, instruction fetch from the EXE image, every data read must hit a byte listed in "reads", the hooks of "stubs"
return the listed values; then the writes, the return value and the "calls" journal must be reproduced bit for bit).
A C# port replays a vector by mapping the addresses to its members (report.md section 3.d), running the ported function and comparing what it wrote; the model of the annex (../model_h1.py) does it
in Python (python -B ../check_model.py).

Files (function; what the vector contains; counts at the last generation are in ../out/gen_out.txt):
  vec_tryuseitem_8002ED64.json      0x8002ED64  TryUseItem: the Circle front, the control flags, the dispatcher answer (stub 0x8003499C)
  vec_dispatch_8003499C.json        0x8003499C  the start dispatcher: promoted item (stub 0x8004E0F8), refusals, jump table, the in-use words; the other starts and the instant starts are stubs
  vec_herb_start_80034680.json      0x80034680  I36 Herb start        (real heal, real UseItem; stubs: sound, effect creation)
  vec_lpotion_start_800346F0.json   0x800346F0  I37 L Potion start
  vec_elixir_start_80034760.json    0x80034760  I38 Magic Elixir start
  vec_cureale_start_800347D4.json   0x800347D4  I39 Cure Ale start
  vec_spotion_start_80034870.json   0x80034870  I41 S Potion start
  vec_herb_heal_80032E50.json       0x80032E50  Hp += max(5, HpMax/4), one effect
  vec_water1_heal_80032F84.json     0x80032F84  Hp += max(5, HpMax/2), four sparks (water 1, h4f1)
  vec_spotion_heal_800330FC.json    0x800330FC  Hp += max(10, HpMax/2), four sparks
  vec_lpotion_heal_80033274.json    0x80033274  Hp = HpMax, eight rings (L Potion; water 2, h4f2)
  vec_elixir_heal_800336F0.json     0x800336F0  Mp = MpMax, eight rings of animation 2
  vec_cureale_heal_80033A2C.json    0x80033A2C  Hp = HpMax, Mp = MpMax, eight rings, eight sparks
../vec_ss/vec_startselect_8002BAEC.json   0x8002BAEC  the per-frame function whole: the Start+Select counter (second guard site 0x8002BB94) and the Start-menu request (first guard site 0x8002BC90)
