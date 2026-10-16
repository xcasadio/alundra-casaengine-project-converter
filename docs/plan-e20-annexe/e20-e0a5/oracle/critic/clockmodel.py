"""critic: model of the DLL clock for an entity born by the interface (reading of AlundraFrameSyncPasses.StepAnimationClock / SyncAnimation / HitPass.NoteSwitchPendingAtTick).
INFERENCE (model of the code read, not a DLL run). Compares OLD (HEAD) with NEW (counted creation switch) against the binary's call numbers.
Frames: every frame has m ticks (m may be 0 for some frames); the birth is the b-th tick (1-based) of frame F. F+1 integrates the entity at its head.
"""
import sys, itertools

def run(m_list, birth_frame, b, D, durations, mode):
    """m_list[i] = ticks of frame i. Returns dict tick->(cursor, sprite_tick_or_None), flag_raise=(tick, where)."""
    S = None
    t = 0
    reserved = 0
    pending = False         # creation switch pending (unvalidated)
    sprite = None           # None until integrated
    cursor = 0              # BoxTicks; creation: 0
    born = False
    integrated = False
    sprite_tick = 0
    flag = None
    cur_by_tick = {}
    sprite_by_tick = {}
    for fi, m in enumerate(m_list):
        # head of frame: integrate a newborn from an earlier frame
        if born and not integrated and fi == birth_frame + 1:
            integrated = True
            sprite_tick = 0
        for k in range(1, m + 1):
            t += 1
            if fi == birth_frame and k == b:
                born = True; S = t; pending = True; reserved = 0; cursor = 0
            if not born:
                continue
            # StepAnimationClock
            if pending:
                reserved += 1
                if mode == 'new':
                    cursor += 1          # counted creation tick
                else:
                    # old: NoteSwitchPendingAtTick: first tick loads (cursor 0), later ticks count
                    if reserved == 1:
                        cursor = 0
                    else:
                        cursor += 1
            else:
                # advance 1
                sprite_tick += 1
                cursor += 1
                if sprite_tick == D and flag is None:
                    flag = (t, 'clock')
            cur_by_tick[t] = cursor
            sprite_by_tick[t] = sprite_tick if integrated else None
        # end of frame: SyncAnimation for entities that took part (integrated at head of this frame)
        if born and integrated and pending and fi >= birth_frame + 1:
            pending = False
            sprite_tick = 0
            n = reserved if mode == 'new' else reserved - 1
            # a 0-tick frame validating with reserved == 0 would owe a tick (old); not reached here (reserved>=1)
            for _ in range(n):
                sprite_tick += 1
                if sprite_tick == D and flag is None:
                    flag = (t, 'replay@end of frame %d' % fi)
            reserved = 0
    return S, cur_by_tick, flag

def binary(S, D, tick):
    return tick - S + 1      # call number at this tick (call #0 = creation)

if __name__ == '__main__':
    D = 26
    for m in (1, 2, 3):
        for b in range(1, m + 1):
            frames = [m] * 60
            for mode in ('old', 'new'):
                S, cur, flag = run(frames, 2, b, D, None, mode)
                ok = all(cur[t] == binary(S, D, t) for t in cur if t <= S + 8) if mode == 'new' else None
                print('m=%d b=%d %s: S=%d flag=%s => seen by programs at S+%d (binary: S+%d) cursor@S..S+3=%s %s' % (
                    m, b, mode, S, flag, flag[0] - S + 1, D, [cur[S + i] for i in range(4)], ('cursor==binary' if ok else '') if mode == 'new' else ''))
    # irregular windows: 0-tick frames after the birth (render rate above the logic rate)
    print('--- irregular frames')
    frames = [1, 1, 1, 0, 0, 1, 0, 1, 1] + [1] * 60
    for mode in ('old', 'new'):
        S, cur, flag = run(frames, 2, 1, D, None, mode)
        print(mode, 'S=%d' % S, flag, 'cursor@S..S+3', [cur[S + i] for i in range(4)])
    # short animation, D < window (3-tick frames)
    print('--- short animation D=2, 3-tick frames')
    for D2 in (1, 2, 3, 4):
        for mode in ('new',):
            S, cur, flag = run([3] * 20, 2, 1, D2, None, mode)
            print('D=%d' % D2, mode, 'S=%d' % S, flag, 'binary flag raised in the anim pass of S+%d, seen S+%d' % (D2 - 1, D2), '| DLL seen at tick', flag[0] + 1)
