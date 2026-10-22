"""proto_pathb.py - PROTOTYPE patch (scratch copy only): path B, the entities that keep their Z in the tick (HasLogicalZ: dropped items, projectiles, thrown objects) take the carrier's force,
move the target first and stop under the collidable above them. Also the test host (ContactHost.Rebuild) computes RidingEntity for every passenger like WorldProxy does.
Usage: python -I proto_pathb.py <probe root>"""
import sys

root = sys.argv[1]


def rw(path, fn):
    p = root + "/" + path
    s = open(p, encoding="utf-8-sig").read()
    s2 = fn(s)
    assert s2 != s, path
    open(p, "w", encoding="utf-8").write(s2)


def sm(s):
    old = """        entity.FinalForceZ = entity.ForceZ;
        entity.ApplyVerticalForceLaw();
        entity.TickForceZ = entity.FinalForceZ;
"""
    new = """        entity.FinalForceZ = entity.ForceZ;
        entity.ApplyVerticalForceLaw();
        // PROTOTYPE path B: a passenger takes the force of the tick of its carrier (0x80037364), unless it has an impulse.
        if (entity.IsZForceApplied == 0 && entity.RidingEntity?.GameplayProxy is EntityScriptProxy carrierB)
        {
            var carriedB = carrierB.CarrierTickForceZ();
            entity.ForceZ = carriedB;
            entity.FinalForceZ = carriedB;
        }

        entity.TickForceZ = entity.FinalForceZ;
"""
    assert old in s
    s = s.replace(old, new, 1)
    old = """            if (EntitySupport.TryFindSupport(entity, entity.ScriptHost.Collidables, seed, out _, out var supportTopZ))
            {
                landingHeight = supportTopZ;
            }
"""
    new = """            if (EntitySupport.TryFindSupport(entity, entity.ScriptHost.Collidables, seed, out var supportEntityB, out var supportTopZ))
            {
                var stillFoundB = true;
                if (supportEntityB != null && entity.EnsureMovedThisPass(supportEntityB))
                {
                    stillFoundB = EntitySupport.TryFindSupport(entity, entity.ScriptHost.Collidables, seed, out _, out supportTopZ);
                }

                if (stillFoundB)
                {
                    landingHeight = supportTopZ;
                }
            }
"""
    assert old in s
    s = s.replace(old, new, 1)
    old = "        else if (tickForce > 0 && EntitySupport.TryFindCeiling(entity, Array.Empty<EntityScriptProxy>(), tickForce, out var ceilingPosZ))"
    new = "        else if (tickForce > 0 && HeroCeilingMovingFirst(entity, tickForce, out var ceilingPosZ))"
    assert old in s
    return s.replace(old, new, 1)


rw("Alundra/Physics/ScriptedMotion.cs", sm)


def ch(s):
    old = "    public void Rebuild() => EntitySupport.BuildCollidables(All, Collidables);"
    new = """    public void Rebuild()
    {
        EntitySupport.BuildCollidables(All, Collidables);
        // PROTOTYPE: as WorldProxy.RefreshUpdateProxiesAndCollidables (the lists step): every passenger gets its RidingEntity.
        foreach (var proxy in All)
        {
            if (!proxy.IsPlayer)
            {
                proxy.RidingEntity = null;
            }
        }

        EntitySupport.UpdateRidingEntities(Collidables);
    }"""
    assert old in s
    return s.replace(old, new, 1)


rw("Alundra.Tests/Support/ContactHost.cs", ch)
print("path B + test host applied")
