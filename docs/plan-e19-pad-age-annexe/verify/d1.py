import sys
sys.path.insert(0, r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\e19j-disc")
import lib

def show(a, n):
    for i in lib.dis(a, n):
        print("%08x  %-8s %s" % (i.address, i.mnemonic, i.op_str))

which = sys.argv[1:]
a = int(which[0], 16)
n = int(which[1])
show(a, n)
