import sys, exelib
def words(a, n):
    return [exelib.u32(a+4*i) for i in range(n)]
if __name__=='__main__':
    a=int(sys.argv[1],16); n=int(sys.argv[2],0)
    base=int(sys.argv[3],0) if len(sys.argv)>3 else 0
    for i,w in enumerate(words(a,n)):
        print(f"[{base+i:#04x}] {a+4*i:#010x}: {w:#010x}")
