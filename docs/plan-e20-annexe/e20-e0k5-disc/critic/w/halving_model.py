"""Model of the integer-halving contact: accept the full step if free, else halve h (>>1) and accept acc+h whenever free. Compared with the oracle raw values (crit_sweep.txt)."""
LIM = 109 * 65536  # max free x (raw): box right edge reaches col 5 beyond


def contact(x0, step, lim=LIM):
    free = lambda x: x <= lim
    if free(x0 + step):
        return x0 + step, True
    acc = x0
    h = step
    accepted = False
    while True:
        h >>= 1
        if h == 0:
            break
        if free(acc + h):
            acc += h
            accepted = True
    return acc, accepted


def largest_free(x0, step, lim=LIM):
    return min(x0 + step, lim) if x0 <= lim else x0


ORACLE = {
    # step 196608 (3 px)
    (104, 196608): 7012352, (106, 196608): 7143424, (107, 196608): 7143423, (108, 196608): 7143424, (109, 196608): 7143424,
    # step 393216 (6 px)
    (104, 393216): 7143423, (106, 393216): 7143424, (107, 393216): 7143423, (108, 393216): 7143424, (109, 393216): 7143424,
}
for (x0, st), want in ORACLE.items():
    got, acc = contact(x0 * 65536, st)
    lf = largest_free(x0 * 65536, st)
    print(x0, st, 'oracle', want, 'halving', got, 'EQ' if got == want else 'NE', 'largest-free', lf, 'EQ' if lf == want else 'NE')
