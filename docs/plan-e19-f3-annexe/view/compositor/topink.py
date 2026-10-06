import sys
sys.dont_write_bytecode = True
sys.path.insert(0, r'C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\f3b-disc\compositor')
import ref
A = ref.Assets()
rows = []
for ch in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ':
    g = A.fnt[ord(ch)]
    top = [sum(1 for x in range(g['width']) if A.page[g['y'] + y, g['x'] + x, 3] > 0) for y in range(3)]
    rows.append((ch, g['xadvance'], top))
for r in rows:
    print(r)
