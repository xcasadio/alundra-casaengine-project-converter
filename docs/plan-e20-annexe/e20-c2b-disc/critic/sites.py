exec(open('bx.py').read().split("if __name__")[0])
sites=[0x8002bc2c,0x8004002c,0x80061ea0,0x80065df8,0x8006eb7c,0x80070554,0x80070bec,0x80071b30,0x80072504,0x80073570,0x80074824,0x80074d98,0x80076d60,0x80077a64,0x8007991c,0x8007c428,0x8007d964]
for s in sites:
    print("== %08x"%s)
    for ad,w,m,o in dis(s-24, s+4):
        print("  %08x %s %s"%(ad,m,o))
