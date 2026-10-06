import sys, os
sys.path.insert(0, r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad\e19j-disc")
import lib
def dis(a, n):
    for i in lib.dis(a, n):
        print("%08x  %s %s" % (i.address, i.mnemonic, i.op_str))
def cstr(a):
    o = a - lib.BASE + lib.OFF
    e = lib.DATA.index(b'\0', o)
    return lib.DATA[o:e]
if __name__ == "__main__":
    a = int(sys.argv[1], 16); n = int(sys.argv[2])
    dis(a, n)
