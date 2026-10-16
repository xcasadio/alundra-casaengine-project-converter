"""critic: same model, with a script switch (animation 1, D1=16 ticks) injected at S+k. INFERENCE; compared with the binary ORACLE rows out/trace_wave_steel_east_SW{k}_1.txt (end flag first at S+16+k)."""
def run(m_list, birth_frame, b, D0, D1, k, mode):
    t = 0; S = None; reserved = 0; pending = False; creation = False; integrated = False; born = False
    sprite_tick = 0; flag = None; cursor = 0; D = D0
    for fi, m in enumerate(m_list):
        if born and not integrated and fi == birth_frame + 1:
            integrated = True; sprite_tick = 0
        for j in range(1, m + 1):
            t += 1
            if fi == birth_frame and j == b:
                born = True; S = t; pending = True; creation = True; reserved = 0; cursor = 0
            if not born:
                continue
            if t == S + k and k >= 0 and D != D1 and S is not None:
                # the script asks animation 1 before this tick's clock
                if creation:
                    creation = False
                    if mode == 'new_reset':
                        reserved = 0
                    cursor = 0  # reload at this tick's NoteSwitchPendingAtTick (BoxSwitchLoaded false)
                    switched_now = True
                pending = True; D = D1
                ordinary_new = True
            else:
                ordinary_new = False
            if pending:
                reserved += 1
                if creation and mode.startswith('new'):
                    cursor += 1
                else:
                    if ordinary_new: cursor = 0
                    elif reserved == 1: cursor = 0
                    else: cursor += 1
            else:
                sprite_tick += 1; cursor += 1
                if sprite_tick == D and flag is None: flag = (t, 'clock')
        if born and integrated and pending and fi >= birth_frame + 1:
            pending = False; creation = False; sprite_tick = 0
            n = reserved if (mode.startswith('new') and creation_was) else reserved - 1
            for _ in range(n):
                sprite_tick += 1
                if sprite_tick == D and flag is None: flag = (t, 'replay')
            reserved = 0
    return S, flag
# creation_was: whether the validated switch is still the creation's; recompute properly
def run2(m_list, birth_frame, b, D0, D1, k, mode):
    t = 0; S = None; reserved = 0; pending = False; creation = False; integrated = False; born = False
    sprite_tick = 0; flag = None; D = D0
    for fi, m in enumerate(m_list):
        if born and not integrated and fi == birth_frame + 1:
            integrated = True; sprite_tick = 0
        for j in range(1, m + 1):
            t += 1
            if fi == birth_frame and j == b:
                born = True; S = t; pending = True; creation = True; reserved = 0
            if not born:
                continue
            if k >= 0 and t == S + k:
                if creation:
                    creation = False
                    if mode == 'new_reset':
                        reserved = 0
                pending = True; D = D1
            if pending:
                reserved += 1
            else:
                sprite_tick += 1
                if sprite_tick == D and flag is None: flag = (t, 'clock')
        if born and integrated and pending:
            n = reserved if (mode.startswith('new') and creation) else reserved - 1
            pending = False; creation = False; sprite_tick = 0
            for _ in range(n):
                sprite_tick += 1
                if sprite_tick == D and flag is None: flag = (t, 'replay')
            reserved = 0
    return S, flag
if __name__ == '__main__':
    for k in (0, 1, 2):
        for m in (1, 2):
            for mode in ('old', 'new_noreset', 'new_reset'):
                S, flag = run2([m] * 80, 2, 1, 26, 16, k, mode)
                print('switch at S+%d, %d tick(s)/frame, %-12s: end flag raised at S+%d (binary S+%d)' % (k, m, mode, flag[0] - S, 16 + k))
