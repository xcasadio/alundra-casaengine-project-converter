import re,sys
src = open('head/EventProgramRunner.cs').read()
i = src.index('private int Dispatch(')
body = src[i:]
idx=[(m.start(), int(m.group(1),16)) for m in re.finditer(r'\n            case 0x([0-9A-Fa-f]{1,2}):', body)]
for want in [int(a,16) for a in sys.argv[1:]]:
    for k,(s,o) in enumerate(idx):
        if o==want:
            e = idx[k+1][0] if k+1<len(idx) else len(body)
            print('=====0x%02X'%o); print(body[s:e])
