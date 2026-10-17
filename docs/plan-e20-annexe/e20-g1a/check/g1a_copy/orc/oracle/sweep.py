"""sweep.py - E20.c1: the reach of the basic-sword ground swing against a Beannoid, measured by running the real code once per relative position.

One run = the H1 setup (hero faces east, Square at tick 1) with the hero moved: dx = target x - hero x (px), dy = hero y - target y (px).  22 ticks (the swing lasts 21).
The contact ticks are the ticks at which the real hit pass wrote +0x1D0 = 0x19 on the target.  For every run the independent model (scen.hit_model) is also evaluated on the rows
and compared with the real ticks.
"""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import c1world as C
import scen as S

DX_RANGE = range(-40, 81)
DY_RANGE = range(-40, 41)
DY_AT = (20, 40, 55)           # the dx values at which the Y window is measured


def run_point(dx, dy, mutate=None):
    sp = S.spec('sweep', '', dx, dy, n=22, holds={1: S.SQ}, experiments=False, mutate=mutate)
    res = C.run(sp)
    real = [r['tick'] for r in res['rows'] if r['targets'][0]['c1d0'] == 0x19]
    model = S.hit_model(res, 'hero', 0)
    real_h = [r['tick'] for r in res['rows'] if r['hero']['c1d0'] == 0x19]
    model_h = S.hit_model(res, 0, 'hero')
    return res, real, model, real_h, model_h


def sweep(outdir, mutate=None, quiet=False):
    """returns dict(reach, near, table_x, table_y, model_mismatches, lines)"""
    tx = {}
    th = {}
    mism = []
    for dx in DX_RANGE:
        res, real, model, real_h, model_h = run_point(dx, 0, mutate)
        tx[dx] = real
        th[dx] = real_h
        if real != model or real_h != model_h:
            mism.append(('x', dx, 0, (real, real_h), (model, model_h)))
    ty = {}
    for dx in DY_AT:
        for dy in DY_RANGE:
            res, real, model, real_h, model_h = run_point(dx, dy, mutate)
            ty[(dx, dy)] = real
            if real != model or real_h != model_h:
                mism.append(('y', dx, dy, (real, real_h), (model, model_h)))
    hit = [dx for dx in DX_RANGE if tx[dx]]
    far, near = max(hit), min(hit)
    lines = []
    lines.append('# E20.c1 reach of the basic-sword ground swing (anim 0x10, direction east) against a Beannoid, measured with the real code.')
    lines.append('# dx = target x - hero x (px); dy = hero y - target y (px); both integer px; the hero does not move during the swing; Square at tick 1; 22 ticks per run.')
    lines.append('# contact = the real hit pass wrote +0x1D0 = 0x19 on the target at that tick.  Model = scen.hit_model (independent rule of plan 0.2.5) on the same rows: %d mismatches over %d runs.'
                 % (len(mism), len(DX_RANGE) + len(DY_AT) * len(DY_RANGE)))
    lines.append('')
    lines.append('## X sweep at dy = 0')
    lines.append('reach: contact for dx in [%d, %d] (every dx in between has a contact: %s); no contact for dx < %d and dx > %d within the swept range [%d, %d]'
                 % (near, far, 'yes' if all(tx[d] for d in range(near, far + 1)) else 'NO', near, far, DX_RANGE[0], DX_RANGE[-1]))
    hh = [dx for dx in DX_RANGE if th[dx]]
    lines.append('the Beannoid body box touches the hero (hit written on the hero) for dx in [%s] at dy = 0 (every dx in between: %s); the hero then goes to the hurt state, which ends his swing'
                 % (('%d, %d' % (min(hh), max(hh))) if hh else 'none', 'yes' if hh and all(th[d] for d in range(min(hh), max(hh) + 1)) else 'NO'))
    lines.append('dx\tcontact ticks on the target\ttick at which the target touches the hero')
    for dx in DX_RANGE:
        lines.append('%d\t%s\t%s' % (dx, ','.join(str(t) for t in tx[dx]) or '-', ','.join(str(t) for t in th[dx]) or '-'))
    lines.append('')
    lines.append('## first contact tick by dx (runs of equal value)')
    runs = []
    for dx in DX_RANGE:
        v = tx[dx][0] if tx[dx] else None
        if runs and runs[-1][2] == v:
            runs[-1][1] = dx
        else:
            runs.append([dx, dx, v])
    for a_, b_, v in runs:
        lines.append('dx %d..%d: first contact tick %s' % (a_, b_, v if v is not None else '-'))
    lines.append('')
    lines.append('## Y window at fixed dx (dy = hero y - target y)')
    ywin = {}
    for dx in DY_AT:
        ys = [dy for dy in DY_RANGE if ty[(dx, dy)]]
        ywin[dx] = (min(ys), max(ys)) if ys else None
        lines.append('dx %d: contact for dy in [%s]; first contact tick by dy: %s' % (
            dx, ('%d, %d' % ywin[dx]) if ywin[dx] else 'none', ', '.join('%d:%d' % (dy, ty[(dx, dy)][0]) for dy in DY_RANGE if ty[(dx, dy)])))
    lines.append('')
    lines.append('## model mismatches (kind, dx, dy, real ticks, model ticks): %s' % (mism or 'none'))
    if not quiet and outdir:
        open(os.path.join(outdir, 'reach-sweep.txt'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
    return dict(reach=far, near=near, tx=tx, ty=ty, th=th, ywin=ywin, mismatches=mism, lines=lines)


if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', 'out')
    os.makedirs(out, exist_ok=True)
    r = sweep(out)
    print('\n'.join(r['lines'][:12]))
    sys.exit(1 if r['mismatches'] else 0)
