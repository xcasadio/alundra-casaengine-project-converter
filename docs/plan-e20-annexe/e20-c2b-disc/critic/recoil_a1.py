import json
d=json.load(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e20-annexe/e20-c2/oracle/out/A1.json'))
for ri,run in enumerate(d['runs']):
    h=run['hero']; n=len(h['pos_x']); c=h['c1cc']
    t=[i for i in range(1,n) if c[i]==119][0]
    fx=h['force_x']
    print("run",ri,"first hurt tick",t)
    print(" force_x t..t+16", fx[t:t+17])
    print(" sum t..t+14 =",sum(fx[t:t+15])," sum t..t+15 =",sum(fx[t:t+16]))
    print(" pos_x before",h['pos_x'][t-1],"after t+16",h['pos_x'][t+16]," delta",h['pos_x'][t+16]-h['pos_x'][t-1])
    print(" anim_target", [hex(x) for x in h['anim_target'][t:t+17]])
    print(" dir_target t-1..t+16", h['dir_target'][t-1:t+17])
    print(" hp", h['hp'][t-1:t+2], " c1cc t..t+3", c[t:t+4], " visible t..t+8", h['visible'][t:t+9])
