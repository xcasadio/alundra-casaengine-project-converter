import cmips, replay_critic as R

m = cmips.Machine()
slots = [dict(status=2, hp=10, hpMax=10, refId=-1, flags=0, animFlags=0, hasPlatform=False, parentSlot=-1, ridingSlot=-1, xCollSlot=-1,
              targetAnimId=0, targetDir=0, lastTargetAnimId=0, lastTargetDir=0)]
for ins in ([0x2F, 0x00, 0x10, 0x00], [0x2F, 0x80, 0x00, 0x01], [0x2F, 0x00, 0x20, 0x00], [0x2F, 0x00, 0x01, 0x02], [0x2F, 0x00, 0x02, 0x03], [0x2F, 0x00, 0x02, 0x00]):
    v = dict(world=dict(slotCount=1, slots=slots, stats=dict(hp=10, hpMax=10, mp=0, mpMax=4, money=0),
                        pad=dict(hold=0x1000, pressed=0x0080, released=0x0100, interval=0x0200)),
             instruction=ins, logicSlot=0, ownerSlot=0)
    h, ret, pre, post, stores, sr = R.run(m, v)
    print('2F', ['%02X' % b for b in ins[1:]], 'returned', ret, 'Result(+0x2C) =', m.rw(R.STATE + 0x2C))
