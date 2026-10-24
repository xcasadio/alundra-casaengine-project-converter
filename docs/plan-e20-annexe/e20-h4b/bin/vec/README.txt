Replayable vector files of the h4b bricks (binary ALUN_CD.EXE, France, SHA-1 a705f8dab665645af5dfaf8fb348c7926b5caa58).

Format (one JSON file per function, "vectors": [...]):
  {"id": n, "fn": "0x800366fc", "args": [a0, a1, a2, a3(, a4, a5)],
   "reads":  [[address, size, value], ...],   every DATA access (1, 2 or 4 bytes) whose bytes were not yet written by the call; first occurrence, in execution order;
                                                 includes the words of the EXE's own tables that the code read (slide tables, ...) and the 5th argument at [0x801FFF10]
   "writes": [[address, size, value], ...],   final value of every (address, size) written, in execution order, EXCLUDING the callee's own stack frame (0x801FFE00..0x801FFEFF)
   "ret": v0, "ret_v1": v1,                   (absent when "stop_at" is present)
   "stop_at": "0x....",                       optional: the run ends when the PC reaches this address (the vector covers only the prefix of the function)
   "stub_spawn_ret"/"spawn_calls"/"journal_destroy": the stubbed callees of the fire-2 driver (0x80039F58) and of E13 (0x8003A59C)}
Values are unsigned 32/16/8-bit integers (two's complement for negatives). Addresses are PS1 virtual addresses (0x80xxxxxx).
Replay from an EMPTY memory: the real MIPS code runs, instruction fetch comes from the EXE image, and every data read must hit a byte listed in "reads"
(otherwise the run stops with 'INCOMPLETE'); then the writes and the return value must be reproduced bit for bit:
    python -B ../h/replay_all.py          (run from a COPY of the h/ folder: it only reads the EXE and these files)
A port in C# replays a vector by loading "reads" into a sparse memory, running the ported function on it, and comparing the memory it wrote with "writes".

Files (function; what the vector contains):
  vec_push_800366FC.json        0x800366FC  ApplyEntityForces: slide table + shift + push (+0xCC/+0xD0, zeroed) + map-bound clamp + ForceAdjusted (+0x13C) -> +0xDC/+0xE0
  vec_physpass_80036828.json    0x80036828  the force pass over the physics list (simple case: no UpdateEntityPhysics change; hero without ice/water scaling)
  vec_slotpass.json             0x80038634  slot pass: count = highest slot whose status is not 0 and not 4; status-4 slots overwritten by the 0x294-byte template at 0x80134368, +0 = slot index
  vec_listspass.json            0x800384F4  lists pass over slots 0..count INCLUSIVE (physics list 0x80127B28/0x80127D28, collideable list, draw list)
  vec_birthraise.json           0x80039D04  prefix up to 0x80039D40: the raise of [0x80134360] at birth
  vec_query_8003AF70.json       0x8003AF70  target query (+ sort 0x8003AE04 inside), output arrays 0x800CC058 (pointers) and 0x800CC158 (keys)
  vec_camera_8002CDA0.json      0x8002CDA0  camera follow up to 0x8002CF38 (after the scroll clamps): sway step, scroll += ((target-(scroll+half))>>4)+shake offset+kick, kicks reset
  vec_sway_8002C894.json        0x8002C894  the per-tick step of the 9-word shake block 0x800E4338
  vec_e13_8007F30C.json         0x8007F30C  E13 (writes the Y kick [0x800DD874] = 2)
  vec_fire2_80035C64.json       0x80035C64  fire-2 driver (guard 0x800351B8, cancel 0x800350C0/0x80034E08 inside); buffer 0x80127138
  tables.json                   the 256-entry sin/cos tables, the 32-direction tables, the slide tables (dumped from the EXE)
Known harness simplifications (see the report): the blank entity template at 0x80134368 is zero except word +0x48 = -1 (0x8003B21C) in the game, random in some vectors here.
