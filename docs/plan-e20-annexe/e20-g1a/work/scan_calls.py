import sys
sys.path.insert(0,'emu')
import lib
ins = lib.all_ins()
targets = {0x8002F884:'UpdatePlayerCarriedEntity',0x8002EEAC:'PlayerTryAction',0x8002EDBC:'TryLift',0x8002F854:'StepTowards',0x8003A774:'DestroyEntity1',0x8003A59C:'DestroyEntity2',0x80037E34:'MoveEntity'}
for i,x in enumerate(ins):
    if x.mnemonic=='jal':
        t=int(x.op_str,16)
        if t in targets:
            print('%08x jal %s' % (x.address, targets[t]))
