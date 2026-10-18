"""Sanity of my interpreter: patch single instructions in the in-memory image, the replay must then fail."""
import json, glob, os, struct, sys
import c_cpu
from c_cpu import Cpu, MemErr

from c_replay_core import replay_file
V = sys.argv[1]
tests = [
 ("herb min 5->6", 0x80032E7C, 0x24030006, "vec_herb_heal_80032E50.json"),
 ("herb slt->sltu cap", 0x80032E84, 0x00c5102b, "vec_herb_heal_80032E50.json"),
 ("herb sra 2->1", 0x80032E6C, 0x00021843, "vec_herb_heal_80032E50.json"),
 ("herb ForceZ 0x20000->0x30000", 0x80032ED4, 0x3c020003, "vec_herb_heal_80032E50.json"),
 ("spotion rng const mult hi 0x20001->0x20003", 0x800331D0, 0x34420003, "vec_spotion_heal_800330FC.json"),
 ("spotion min 10->11", 0x80033138, 0x2403000b, "vec_spotion_heal_800330FC.json"),
 ("spotion count 4->3", 0x80033244, 0x2a020003, "vec_spotion_heal_800330FC.json"),
 ("lpotion shl 11->10", 0x8003330C, 0x00031a80, "vec_lpotion_heal_80033274.json"),
 ("lpotion ForceZ 3->4", 0x80033340, 0x3c020004, "vec_lpotion_heal_80033274.json"),
 ("elixir anim 2->0", 0x80033770, 0x24060000, "vec_elixir_heal_800336F0.json"),
 ("cureale anim parity sltiu->", 0x80033A90, 0x2cc60000, "vec_cureale_heal_80033A2C.json"),
 ("herb start sound 0x31->0x32", 0x800346BC, 0x24040032, "vec_herb_start_80034680.json"),
 ("herb start slt->sltu hp<hpmax", 0x800346A4, 0x0043102b, "vec_herb_start_80034680.json"),
 ("elixir start slt swap", 0x80034780, 0x0050802a, "vec_elixir_start_80034760.json"),
 ("dispatch bound 0x62->0x61", 0x800349B0, 0x2e020061, "vec_dispatch_8003499C.json"),
 ("tryuse mask 0x20->0x10", 0x8002ED70, 0x30420010, "vec_tryuseitem_8002ED64.json"),
 ("noop patch (nop for nop)", 0x80032E78, 0x00000000, "vec_herb_heal_80032E50.json"),
]
for name, addr, w, f in tests:
    bad = replay_file(os.path.join(V, f), {addr: w}, stop_at_first=False)
    print("%-45s -> failing vectors: %d" % (name, bad))
