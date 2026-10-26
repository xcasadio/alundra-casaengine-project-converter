import sys
sys.path.insert(0, '.')
from bx import *
sites = [0x8002f818,0x80032bec,0x80032cd8,0x80036134,0x80062280,0x8006c060,0x8006cd38,0x8006d88c,0x8006e79c,0x8006efec,0x8006f0a8,0x8006f564,0x8006f704,0x80071dd8,0x800731cc,0x80077250,0x8007ca24,0x8007fd30]
for s in sites:
    print('=== site %08x' % s)
    for ad, w, m, o in dis(s-44, s+4):
        if m in ('move','addiu','ori','lw','lbu','lhu','li','addu','sll','lui','jal','or','lb','lh') and any(x in o for x in ('$a1','$a2','$a3','jal')) or ad==s or ad==s+4:
            print('%08x %s %s' % (ad, m, o))
