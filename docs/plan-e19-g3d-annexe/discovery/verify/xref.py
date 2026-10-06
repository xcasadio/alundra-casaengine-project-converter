import sys, os, pickle, re
SP = r"C:\Users\casad\AppData\Local\Temp\claude\D--development-repo-alundra-casaengine-project-converter\b00d1a72-420d-4acc-ab34-0c25fbdad3a1\scratchpad"
sys.path.insert(0, os.path.join(SP, 'e19j-disc'))
import lib
PK = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'ins.pkl')
if os.path.exists(PK):
    ins = pickle.load(open(PK,'rb'))
else:
    ins = [(i.address, i.mnemonic, i.op_str) for i in lib.all_ins()]
    pickle.dump(ins, open(PK,'wb'))
def jal_callers(target):
    t = hex(target)
    out = []
    for a,m,o in ins:
        if m == 'jal' and o.strip() == t: out.append(a)
    return out
if __name__ == '__main__':
    for t in sys.argv[1:]:
        c = jal_callers(int(t,16))
        print(t, len(c), [hex(x) for x in c])
