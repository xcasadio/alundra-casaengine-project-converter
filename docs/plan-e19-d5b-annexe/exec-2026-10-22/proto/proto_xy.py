"""proto_xy.py - PROTOTYPE patch (scratch copy only): M6 in XY, the transitive and once-only form: every passenger (hero or not) adds, to its own XY force of the tick, the planned adjusted force of each carrier of its chain, once each
(the binary adds the Adjusted force of the carrier through a recursion without memo: a pile counts the middle of the pile twice and leaves the top behind).
Usage: python -I proto_xy.py <probe root>"""
import sys

root = sys.argv[1]


def rw(path, fn):
    p = root + "/" + path
    s = open(p, encoding="utf-8-sig").read()
    s2 = fn(s)
    assert s2 != s, path
    open(p, "w", encoding="utf-8").write(s2)


def sm(s):
    old = """        if (entity.IsPlayer && entity.RidingEntity?.GameplayProxy is EntityScriptProxy carrier)
        {
            // PROTOTYPE M5: the planned adjusted force of the carrier for this very tick (a pure copy of the force block above, run on the carrier's own state).
            PeekAdjustedForce(carrier, out var plannedX, out var plannedY);
            entity.FinalForceX += plannedX;
            entity.FinalForceY += plannedY;
        }
"""
    new = """        if (entity.RidingEntity?.GameplayProxy is EntityScriptProxy carrier)
        {
            // PROTOTYPE M5/M6: the planned adjusted force of every carrier of the chain, once each (the pile is counted once).
            var depth = 0;
            for (var up = carrier; up != null && depth < 8; up = up.RidingEntity?.GameplayProxy as EntityScriptProxy, depth++)
            {
                PeekAdjustedForce(up, out var plannedX, out var plannedY);
                entity.FinalForceX += plannedX;
                entity.FinalForceY += plannedY;
            }
        }
"""
    assert old in s
    return s.replace(old, new, 1)


rw("Alundra/Physics/ScriptedMotion.cs", sm)
print("xy applied")
