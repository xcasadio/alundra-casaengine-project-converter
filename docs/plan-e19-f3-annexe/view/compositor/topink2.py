import sys
sys.dont_write_bytecode = True
sys.path.insert(0, '.')
import ref
A = ref.Assets()
for cid in sorted(A.fnt):
    g = A.fnt[cid]
    if g['width'] < 4: continue
    top = [sum(1 for x in range(g['width']) if A.page[g['y'] + y, g['x'] + x, 3] > 0) for y in range(3)]
    if top[0] or top[1]:
        print(cid, repr(chr(cid)), g['xadvance'], top)
