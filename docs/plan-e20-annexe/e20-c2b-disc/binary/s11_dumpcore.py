"""S11: writes dis_core.txt = raw capstone listings (no commentary) of the ranges the report cites, each under a title."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bx, io, contextlib
R = [
 ("0x80031A68 damage entry (toucher / hazard)", 0x80031A68, 0x80031B4C),
 ("MovePlayer front: frozen exit, locked branch (+ exact 0x20 entry), free branch hurt block, death test, first dispatch", 0x80031B50, 0x80031D1C),
 ("MovePlayer dispatch + generic prologue calls + state table jump", 0x80031D68, 0x80031DB0),
 ("states 0x3A/0x3B (general), flip + ground/air", 0x80031DB4, 0x80031DF4),
 ("state 0x1C / 0x3E (general) and the shared tail 0x8003270C", 0x80032558, 0x800325A0),
 ("state 0x31 (TryUseItem only) / slope-4 handler / 0x3B water", 0x8003260C, 0x80032758),
 ("death branch 0x800327A4 + table 0x80022FBC handlers + END 0x80032914", 0x800327A4, 0x80032958),
 ("ComputeNewHp 0x8003A374 (head) and ResolveBalanceTarget 0x8004464C", 0x8003A374, 0x8003A3D4),
 ("ResolveBalanceTarget 0x8004464C", 0x8004464C, 0x8004481C),
 ("TryUseItem 0x8002ED64", 0x8002ED64, 0x8002EDB8),
 ("hit pass 0x80039300 (target selection, overlap, TouchingEntity write at 0x80039668, +0x1D0 = 25 at 0x80039670)", 0x80039300, 0x80039780),
 ("counters pass 0x80038998", 0x80038998, 0x80038A1C),
 ("lists pass 0x800384F4 (active / collidable / draw lists; blink test at 0x800385E8-0x800385F4)", 0x800384F4, 0x80038630),
 ("shadow pass 0x80038E84 (release on the same bit test)", 0x80038E84, 0x80038F20),
 ("tick dispatcher 0x8003B388", 0x8003B388, 0x8003B410),
 ("UpdateAnimation 0x80038AB4 (reload, Sfx, AnimFlags)", 0x80038AB4, 0x80038BD4),
 ("tile flags 0x80038064 (hazard word +0x180, gating on Flags & 0x100)", 0x80038064, 0x80038364),
 ("Start+Select 0x8002BB70 (Hp = 0, +0x1CC = 0x78)", 0x8002BB70, 0x8002BC50),
 ("C89 native handler: hero forced to 0x31 (0x80062900-0x80062988)", 0x80062900, 0x800629B0),
]
out = []
for title, a, b in R:
    out.append("=" * 100)
    out.append("%s   [%08x-%08x]" % (title, a, b))
    out.append("=" * 100)
    for ad, w, m, o in bx.dis(a, b):
        out.append("%08x  %08x  %-7s %s" % (ad, w, m, o))
    out.append("")
open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'dis_core.txt'), 'w', encoding='utf-8', newline='\n').write("\n".join(out))
print("ok", len(out))
