import sys, os, struct
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'e19j-disc'))
import lib
ins = lib.all_ins()
for t in [int(x,16) for x in sys.argv[1:]]:
    pat = struct.pack('<I', t)
    o = 0
    print('== word refs to %08x' % t)
    while True:
        o = lib.DATA.find(pat, o)
        if o < 0: break
        print('  data word at %08x' % (lib.BASE + o - lib.OFF)); o += 1
    hi = (t >> 16) + (1 if (t & 0x8000) else 0); lo = t & 0xFFFF
    print('== lui/addiu refs (hi=%04x lo=%04x)' % (hi, lo))
    for k, i in enumerate(ins):
        if i.mnemonic == 'lui' and i.op_str.endswith('0x%x' % hi):
            reg = i.op_str.split(',')[0].strip()
            for j in range(k+1, min(k+12, len(ins))):
                m = ins[j]
                if m.mnemonic in ('addiu','ori') and m.op_str.replace(' ','').startswith(reg + ',' + reg + ',') or (m.mnemonic == 'addiu' and ('0x%x' % lo) in m.op_str and reg in m.op_str):
                    imm = m.op_str.split(',')[-1].strip()
                    try:
                        v = int(imm, 16) if imm.startswith(('0x','-0x')) else int(imm)
                    except: continue
                    if (v & 0xFFFF) == lo:
                        print('  %08x lui + %08x %s' % (i.address, m.address, m.op_str))
