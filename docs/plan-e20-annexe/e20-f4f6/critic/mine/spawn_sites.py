import re, sys
sys.path.insert(0,'.')
import mipsdis
md = mipsdis.md; data = mipsdis._data
start = 0x80020000; end = 0x800CA800
code = data[mipsdis.off(start):mipsdis.off(end)]
ins = list(md.disasm(code, start))
idx = {i.address:k for k,i in enumerate(ins)}
sites = [k for k,i in enumerate(ins) if i.mnemonic=='jal' and i.op_str.strip().lower() in ('0x80039f58',)]
print('call sites', len(sites))
res = {}
for k in sites:
    a2 = None; a1 = None
    # scan back (and the delay slot) for the last write to $a2 / $a1
    for j in range(k+1, k-14, -1):
        i = ins[j]
        if i.mnemonic in ('addiu','ori') and i.op_str.startswith('$a2, $zero'):
            a2 = int(i.op_str.split(',')[-1].strip(),16) if 'x' in i.op_str.split(',')[-1] else int(i.op_str.split(',')[-1])
            break
        if i.mnemonic in ('move','lw','addu','lbu','lb') and i.op_str.startswith('$a2'):
            a2 = 'dyn:' + i.mnemonic + ' ' + i.op_str
            break
    for j in range(k+1, k-14, -1):
        i = ins[j]
        if i.mnemonic in ('addiu','ori') and i.op_str.startswith('$a1, $zero'):
            a1 = int(i.op_str.split(',')[-1].strip(),16) if 'x' in i.op_str.split(',')[-1] else int(i.op_str.split(',')[-1])
            break
        if i.mnemonic in ('move','lw','addu','lbu','lb') and i.op_str.startswith('$a1'):
            a1 = 'dyn:' + i.mnemonic + ' ' + i.op_str
            break
        if i.mnemonic == 'move' and i.op_str.startswith('$a1, $zero'):
            a1 = 0; break
    res.setdefault((a1,a2),[]).append(ins[k].address)
for key in sorted(res, key=lambda x: (str(x[0]), str(x[1]))):
    print(key, ' '.join('%08X'%a for a in res[key]))
