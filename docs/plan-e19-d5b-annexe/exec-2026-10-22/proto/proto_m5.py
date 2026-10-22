"""proto_m5.py - PROTOTYPE patch (scratch copy only): M5, the hero takes the carrier's PLANNED adjusted XY force of the tick (binary 0x80037364 adds +0xDC/+0xE0 of the carrier,
computed in the forces pass before any move) instead of the displacement the carrier realized at its previous tick (LastTickDeltaX/Y, one tick of phase).
Usage: python -I proto_m5.py <probe root>"""
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
            entity.FinalForceX += carrier.LastTickDeltaX;
            entity.FinalForceY += carrier.LastTickDeltaY;
        }
"""
    new = """        if (entity.IsPlayer && entity.RidingEntity?.GameplayProxy is EntityScriptProxy carrier)
        {
            // PROTOTYPE M5: the planned adjusted force of the carrier for this very tick (a pure copy of the force block above, run on the carrier's own state).
            PeekAdjustedForce(carrier, out var plannedX, out var plannedY);
            entity.FinalForceX += plannedX;
            entity.FinalForceY += plannedY;
        }
"""
    assert old in s
    s = s.replace(old, new, 1)
    marker = "    /// <summary>The result of <see cref=\"ClampStepToMapEdge\"/>"
    add = '''    /// <summary>PROTOTYPE M5: AdjustedForceX/Y the entity will have at this tick (the target/step caches of RunOneKinematicTick, IncrementForce), without writing anything. Done: the entity already ran its forces in this pass.</summary>
    internal static void PeekAdjustedForce(EntityScriptProxy entity, out int adjustedX, out int adjustedY)
    {
        var pass = entity.ScriptHost?.PhysicsPassId ?? 0;
        if (pass != 0 && entity.PhysicsDoneInPass == pass)
        {
            adjustedX = entity.AdjustedForceX;
            adjustedY = entity.AdjustedForceY;
            return;
        }

        var animId = entity.JoinsBirthTick && entity.CurrentAnimationId == ~0u ? entity.TargetAnimationId : entity.CurrentAnimationId;
        AnimSetEntry animSet = default;
        var hasAnimSet = entity.AnimSetsByAnim != null && entity.AnimSetsByAnim.TryGetValue((int)animId, out animSet);
        var speed = hasAnimSet ? animSet.Speed : 0;
        var acceleration = (hasAnimSet ? animSet.Acceleration : 0) & 0xf;
        var targetX = entity.TargetForceX;
        var targetY = entity.TargetForceY;
        var stepX = entity.ForceStepX;
        var stepY = entity.ForceStepY;
        if (entity.Speed != speed || entity.TargetDirection != entity.CurrentDirection || entity.Acceleration != acceleration)
        {
            var dirIndex = (int)entity.TargetDirection;
            var offsetX = dirIndex >= 0 && dirIndex < AnimationTables.OffsetXList.Length ? AnimationTables.OffsetXList[dirIndex] : (short)0;
            var offsetY = dirIndex >= 0 && dirIndex < AnimationTables.OffsetYList.Length ? AnimationTables.OffsetYList[dirIndex] : (short)0;
            targetX = offsetX * speed;
            targetY = offsetY * speed;
            stepX = Math.Abs(targetX - entity.ForceX) >> acceleration;
            stepY = Math.Abs(targetY - entity.ForceY) >> acceleration;
        }

        adjustedX = IncrementForce(entity.ForceX, targetX, stepX);
        adjustedY = IncrementForce(entity.ForceY, targetY, stepY);
    }

'''
    assert marker in s
    return s.replace(marker, add + marker, 1)


rw("Alundra/Physics/ScriptedMotion.cs", sm)
print("m5 applied")
