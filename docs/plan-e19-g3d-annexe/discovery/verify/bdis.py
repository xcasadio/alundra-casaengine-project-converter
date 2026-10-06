import sys, os
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'e19j-disc'))
import lib
start = int(sys.argv[1], 16)
n = int(sys.argv[2])
for i in lib.dis(start, n):
    print("%08x: %-7s %s" % (i.address, i.mnemonic, i.op_str))
