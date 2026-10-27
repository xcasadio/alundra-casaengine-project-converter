"""independent model of the binary's camera Y step (0x8002CDA0, scroll space): scroll += ((target - scroll) >> 4) + offset + kick ; kick cleared at every step, not applied at the first step after a load."""
def run(scroll, target, kicks, snap=False):
    out = []
    for i, k in enumerate(kicks):
        if snap and i == 0:
            scroll = target
        else:
            scroll += ((target - scroll) >> 4) + k
        out.append(scroll)
    return out
print('d=0', run(100, 100, [2,0,0,0,0,0]))
print('d=1', run(100, 101, [2,0,0,0,0,0]))
print('d=2', run(100, 102, [2,0,0,0,0,0]))
print('d=15', run(100, 115, [2,0,0,0,0,0]))
print('d=16', run(100, 116, [2,0,0,0,0,0]))
print('d=-1', run(100, 99, [2,0,0,0,0,0]))
print('d=0 nokick', run(100, 100, [0]*4))
print('d=15 acc', run(100, 115, [2]*10))
print('d=2 acc', run(100, 102, [2,2,2,0,0,0]))
print('d=1 two', run(100, 101, [2,2,0,0,0]))
print('d=15 every other', run(100, 115, [2,0,2,0,2,0,2,0,2,0,2,0,2,0,0]))
print('--- 3-tick frames start 100 target 220')
for k in ([2,0,0],[0,2,0],[0,0,2],[2,3,4],[2,0,2],[0,0,0]):
    print(k, run(100,220,k)[-1], run(100,220,k))
print('snap frame [5,2,3]', run(100,220,[5,2,3],snap=True))
print('short [2] as 3 ticks', run(100,220,[2,0,0]))
print('two ticks d=0 kick 2', run(100,100,[2,0]))
