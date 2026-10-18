import re, capstone, exelib
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
base = exelib.T_ADDR; data = exelib.DATA
n = (len(data) - 0x800) // 4
sites = {0xCC: [], 0xD0: []}
for i in range(n):
    addr = base + 4*i
    r = list(md.disasm(data[0x800+4*i:0x800+4*i+4], addr))
    if not r: continue
    x = r[0]
    if x.mnemonic != 'sw': continue
    m = re.match(r'(\$\w+), (-?0x[0-9a-fA-F]+|-?\d+)\((\$\w+)\)', x.op_str)
    if not m: continue
    off = int(m.group(2), 0)
    if off in sites and m.group(1) != '$zero':
        sites[off].append(addr)
    elif off in sites:
        sites[off].append(-addr)   # negative = store of $zero
for off, lst in sites.items():
    nz = [a for a in lst if a > 0]; z = [-a for a in lst if a < 0]
    print("sw rX, 0x%X(reg): %d non-zero stores, %d stores of $zero" % (off, len(nz), len(z)))
    print("   non-zero:", " ".join("%08X" % a for a in nz))
    print("   zero    :", " ".join("%08X" % a for a in z))
