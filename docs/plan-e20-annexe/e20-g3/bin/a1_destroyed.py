"""a1_destroyed.py - disc-e20g3 A1: does a destroyed (status 4) crate stay in the collidable list? Real DestroyEntity 0x8003A59C (without effect: a1 = 0 means no break effect... the call is made with a1 = 0), then the lists pass 0x800384F4, then the slot pass 0x80038634 and the lists pass again."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lib392 as L
m, S = L.world392(hero_px=(720, 296), rec_ids=(5,))
e = L.K.ent(S[5])
def members():
    n = m.peek(0x80127D2C)
    return [(m.peek(0x80127C28 + 4 * k) - L.K.ent(0)) // 0x294 for k in range(n)]
m.call(0x800384F4)
print('before: status=%d flags=%X list=%s' % (m.peek(e + 0x10), m.peek(e + 0x6C), members()))
m.call(0x8003A59C, e, 0)           # DestroyEntity(entity, effect 0): status := 4, +0x228 := -1
print('after DestroyEntity: status=%d flags=%X' % (m.peek(e + 0x10), m.peek(e + 0x6C)))
m.call(0x800384F4)
print('lists pass right after (same tick, physics would run now): list=%s   <- the destroyed crate (slot %d) is still a member' % (members(), S[5]))
m.call(0x80038634)
m.call(0x800384F4)
print('after the next tick\'s slot pass 0x80038634 + lists pass: status=%d flags=%X list=%s' % (m.peek(e + 0x10), m.peek(e + 0x6C), members()))
