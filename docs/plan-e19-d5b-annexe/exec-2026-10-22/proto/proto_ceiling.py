"""proto_ceiling.py - PROTOTYPE patch (scratch copy only): the ceilings of risers with the binary's "move the target first" (0x80037630-0x80037650), for the controller path and the hero.
Usage: python -I proto_ceiling.py <probe root>"""
import sys

root = sys.argv[1]


def rw(path, fn):
    p = root + "/" + path
    s = open(p, encoding="utf-8-sig").read()
    s2 = fn(s)
    assert s2 != s, path
    open(p, "w", encoding="utf-8").write(s2)


def es(s):
    old = """    internal static bool TryFindCeiling(
        EntityScriptProxy entity, IReadOnlyList<EntityScriptProxy> collidables, int force, out int newPosZ)
    {
"""
    new = """    internal static bool TryFindCeiling(
        EntityScriptProxy entity, IReadOnlyList<EntityScriptProxy> collidables, int force, out int newPosZ)
        => TryFindCeiling(entity, collidables, force, out newPosZ, out _);

    /// <summary>PROTOTYPE (D5b): the same search, also giving the collidable that lowered the target (null for the absolute ceiling).</summary>
    internal static bool TryFindCeiling(
        EntityScriptProxy entity, IReadOnlyList<EntityScriptProxy> collidables, int force, out int newPosZ, out EntityScriptProxy? ceilingEntity)
    {
        ceilingEntity = null;
"""
    assert old in s
    s = s.replace(old, new, 1)
    old2 = """            targetTop = candidateModZ - 1;
            contact = true;
        }
"""
    new2 = """            targetTop = candidateModZ - 1;
            contact = true;
            ceilingEntity = candidate;
        }
"""
    assert old2 in s
    return s.replace(old2, new2, 1)


rw("Alundra/Physics/EntitySupport.cs", es)


def ep(s):
    old = """                        MoveVerticalAndPullPosition(FinalForceZ / 65536f, wasSupportedEnteringThisTick);
                        IsOnGround = 0;
"""
    new = """                        // PROTOTYPE (D5b, the ceilings of the risers): a rise stops under the collidable above it, after that collidable has moved first when it had not moved in this pass.
                        var rise = FinalForceZ;
                        if (rise > 0 && eligible)
                        {
                            while (EntitySupport.TryFindCeiling(this, collidables, rise, out var ceilingPosZ, out var ceilingEntity))
                            {
                                if (ceilingEntity != null && EnsureMovedThisPass(ceilingEntity))
                                {
                                    continue;
                                }

                                rise = ceilingPosZ - PosZ;
                                CollidedWithEntityZ = 1;
                                if ((Flags & EntityFlags.Gravity) != 0)
                                {
                                    ForceZ = 0;
                                    FinalForceZ = 0;
                                }

                                break;
                            }
                        }

                        MoveVerticalAndPullPosition(rise / 65536f, wasSupportedEnteringThisTick);
                        IsOnGround = 0;
"""
    assert old in s
    return s.replace(old, new, 1)


rw("Alundra/Entities/EntityScriptProxy.cs", ep)


def sm(s):
    old = """        else if (tickForce > 0 && EntitySupport.TryFindCeiling(hero, hero.ScriptHost?.Collidables ?? Array.Empty<EntityScriptProxy>(), tickForce, out var ceilingPosZ))
        {"""
    new = """        else if (tickForce > 0 && HeroCeilingMovingFirst(hero, tickForce, out var ceilingPosZ))
        {"""
    assert old in s
    s = s.replace(old, new, 1)
    old2 = "    private static bool RunHeroVerticalTick(EntityScriptProxy hero)\n"
    new2 = """    /// <summary>PROTOTYPE (D5b): the hero's ceiling search with "move the target first".</summary>
    private static bool HeroCeilingMovingFirst(EntityScriptProxy hero, int force, out int ceilingPosZ)
    {
        var collidables = hero.ScriptHost?.Collidables ?? Array.Empty<EntityScriptProxy>();
        while (EntitySupport.TryFindCeiling(hero, collidables, force, out ceilingPosZ, out var ceilingEntity))
        {
            if (ceilingEntity != null && hero.EnsureMovedThisPass(ceilingEntity))
            {
                continue;
            }

            return true;
        }

        ceilingPosZ = 0;
        return false;
    }

""" + old2
    assert old2 in s
    return s.replace(old2, new2, 1)


rw("Alundra/Physics/ScriptedMotion.cs", sm)
print("ceiling prototype applied")
