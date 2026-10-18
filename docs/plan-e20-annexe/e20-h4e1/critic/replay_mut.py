import json, sys, importlib
import mylib as L
# mutate the EXE text in memory then rerun the replay logic; count failures
muts = {
 'gate nop (0x80035F40)': (0x80035F40, 0),
 'z slt->sltu (0x80035FE4)': (0x80035FE4, 0x0062102B),
 'slti 0x6401 -> 0x6400': (0x80036000, 0x28426400),
 'last-target beq nop': (0x80035FF0, 0),
 'sltiu key unsigned': (0x80036000, 0x2C426401),
}
d = json.load(open('../h4e1/vec/vec_wind1_80035EB0.json'))
import mymips as X
for name,(addr,word) in muts.items():
    orig = bytes(L.TEXT[addr-L.TBASE:addr-L.TBASE+4])
    t = bytearray(L.TEXT); t[addr-L.TBASE:addr-L.TBASE+4] = word.to_bytes(4,'little'); L.TEXT = bytes(t)
    bad = 0
    for v in d['vectors']:
        mem = X.Mem(strict=True)
        for (a,n,val) in v['reads']: mem.poke(a,n,val)
        def h_spawn(c, v=v): c.r[2] = v['stub_spawn_ret'] & X.M32
        def h_destroy(c): c.r[2] = 0
        cpu = X.CPU(mem, hooks={0x80039F58:h_spawn, 0x8003A774:h_destroy})
        try:
            ret = cpu.run(0x80035EB0)
        except X.Incomplete:
            bad += 1; continue
        fin = {}
        for (a,n,val) in mem.logw:
            if 0x801FFE00 <= a < 0x801FFF00: continue
            fin[(a,n)] = val
        want = {(a,n):val for (a,n,val) in v['writes']}
        if fin != want or (ret & X.M32) != v['ret']: bad += 1
    t = bytearray(L.TEXT); t[addr-L.TBASE:addr-L.TBASE+4] = orig; L.TEXT = bytes(t)
    print('%-30s -> %d / 800 vectors fail' % (name, bad))
