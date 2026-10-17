import re, hashlib, sys
src = open(sys.argv[1], encoding='utf-8').read()
i = src.index('private static readonly byte[] TableBytes')
j = src.index('};', i)
body = src[i:j]
body = re.sub(r'//[^\n]*', '', body)
vals = [int(x, 16) for x in re.findall(r'0x([0-9A-Fa-f]{2})\b', body)]
b = bytes(vals)
print(len(b), hashlib.md5(b).hexdigest())
rows = [r for r in range(128) if any(b[r*16:(r+1)*16])]
print('nonzero rows codes:', [hex(0x80 + r) for r in rows])
