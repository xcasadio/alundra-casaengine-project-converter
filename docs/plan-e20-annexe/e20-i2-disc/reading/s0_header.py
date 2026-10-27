import sys, struct
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2')
import bx
d = bx.data
print(len(d), d[:8])
print('pc0', hex(struct.unpack_from('<I', d, 0x10)[0]), 'text addr', hex(struct.unpack_from('<I', d, 0x18)[0]), 'text size', hex(struct.unpack_from('<I', d, 0x1c)[0]))
