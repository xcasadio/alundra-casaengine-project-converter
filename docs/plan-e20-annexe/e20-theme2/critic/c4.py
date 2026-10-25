import sys
sys.path.insert(0, r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/theme2-disc/critic')
import lib
t = int(sys.argv[1], 16)
for a in lib.callers(t):
    print(hex(a), 'in', hex(lib.func_of(a)), lib.cargs(a))
