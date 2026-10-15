"""k0_noriders.py - which entity does bit 0x4000 (DLL 'NoRiders') gate?  rider crate on a walking platform (as k0_carry.rider) with 0x4000 set on the RIDER or on the PLATFORM.
Run: python -B -I k0_noriders.py"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import k0lib as K
import k0_carry as KC
from k0_arrows import OUT

M = K.M32
lines = []
for who in ('none', 'rider', 'platform'):
    orig = KC.K.run
    def patched(m, n, slots, pre=None, post=None, ticker=None, hold=0, who=who):
        p, c = slots
        if who == 'rider':
            m.poke(K.ent(c) + 0x6C, m.peek(K.ent(c) + 0x6C) | 0x4000)
        elif who == 'platform':
            m.poke(K.ent(p) + 0x6C, m.peek(K.ent(p) + 0x6C) | 0x4000)
        return orig(m, n, slots, pre=pre, post=post, ticker=ticker, hold=hold)
    KC.K.run = patched
    try:
        m, (p, c), init, rows = KC.rider(n=20)
    finally:
        KC.K.run = orig
    sup = sorted(set(r['slots'][c]['support_12C'] == K.ent(p) for r in rows))
    dx = [round((r['slots'][c]['px'] - r['slots'][p]['px']) / 65536, 3) for r in rows[::5]]
    lines.append('0x4000 on %-8s: rider +0x12C == platform over the ticks: %s ; rider X - platform X every 5 ticks %s ; rider Z raw set %s' % (who, sup, dx, sorted(set(r['slots'][c]['pz'] for r in rows))[:3]))
txt = '\n'.join(lines)
open(os.path.join(OUT, 'noriders-summary.txt'), 'w').write(txt + '\n')
print(txt)
