"""E20.g3 T3 mutation driver: apply one real mutant by exact text replacement, run the tests, revert, check git diff is empty.
usage: python -B mut.py <id> <filter-name> """
import subprocess, sys, re, os

REPO = r"D:/development/repo/alundra-casaengine-project-converter"
SKIP = r"C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"

EC = "Alundra/Physics/EntityCollision.cs"
ES = "Alundra/Physics/EntitySupport.cs"
MP = "Alundra/Physics/MovementObstacleProbe.cs"
SM = "Alundra/Physics/ScriptedMotion.cs"
EP = "Alundra/Entities/EntityScriptProxy.cs"
LA = "Alundra/Ai/Objects/LiftableAi.cs"

FIRST_LOOP_HEAD = """            if (ReferenceEquals(candidate, entity))
            {
                continue;
            }

            var candidateModX = candidate.PosX + candidate.ModX;"""

M = {}
M["M01_mask_genre1_only"] = (EC, FIRST_LOOP_HEAD, FIRST_LOOP_HEAD.replace("if (ReferenceEquals(candidate, entity))", "if (ReferenceEquals(candidate, entity) || EntityFlags.GetPickupKind(candidate.Flags) == EntityPickupKind.Liftable)"))
M["M02_genre3_skipped"] = (EC, FIRST_LOOP_HEAD, FIRST_LOOP_HEAD.replace("if (ReferenceEquals(candidate, entity))", "if (ReferenceEquals(candidate, entity) || EntityFlags.GetPickupKind(candidate.Flags) == EntityPickupKind.LiftableHeavy)"))
M["M03_carried_kept_in_list"] = (ES, "&& IsEligibleSubject(candidate))", "&& (candidate.Flags & EntityFlags.Collidable) != 0 && (candidate.AnimFlags & NoEntityCollisionAnimFlag) == 0)")
M["M04_thrown_keeps_0x80"] = (LA, "private const uint ReleaseClearBits = 0x80;", "private const uint ReleaseClearBits = 0;")
M["M05_flush_accepted_step_gives_contact"] = (EP, """            XCollisionEntity = blockingEntity;
            EntityBlockCount++;
        }
""", """            XCollisionEntity = blockingEntity;
            EntityBlockCount++;
        }
        else if (ScriptHost != null && (requested.X != 0f || requested.Y != 0f))
        {
            var rootNow = Owner.RootComponent.LocalTransform.Position;
            var ahead = Alundra.Physics.EntityCollision.FindEntityCollisionCandidate(
                this, (int)Math.Round((double)rootNow.X * 65536.0) + Math.Sign(requested.X), (int)Math.Round((double)rootNow.Y * 65536.0) + Math.Sign(requested.Y), PosZ, ScriptHost.Collidables);
            if (ahead != null)
            {
                XCollisionEntity = ahead;
            }
        }
""")
M["M06_nearest_instead_of_first"] = None  # built below (two edits)
M["M07_slide_after_entity_contact"] = (SM, "var forbidden = entity.XCollisionEntity != null || (entity.Flags & EntityFlags.NoObstacleSlide) != 0;", "var forbidden = (entity.Flags & EntityFlags.NoObstacleSlide) != 0;")
M["M08_terrain_before_entity"] = (MP, """        if (found?.OwnerEntity is not { } foundEntity)
        {
            return false;
        }
""", """        if (found?.OwnerEntity is not { } foundEntity)
        {
            return false;
        }

        if (proxy.ReadCollisionField() is CellsCollisionField mutField && proxy.Controller != null)
        {
            var mutLeft = candidateX + proxy.ModX;
            var mutTop = candidateY + proxy.ModY;
            var mutXs = new[] { mutLeft >> 16, (mutLeft + proxy.Width) >> 16 };
            var mutYs = new[] { mutTop >> 16, (mutTop + proxy.Height) >> 16 };
            var mutMask = CellsCollisionField.WalkabilityMaskFor(proxy.Flags);
            var mutReach = proxy.PosZ / 65536f + proxy.Controller.Settings.StepHeight;
            for (var i = 0; i < 4; i++)
            {
                if (!mutField.TrySampleGround(new Vector3(mutXs[i & 1], mutYs[i >> 1], 0f), float.MaxValue, mutMask, out var mutSample) || !mutSample.IsWalkable || mutSample.GroundHeight > mutReach)
                {
                    return false; // terrain blocks first: the entity is not the contact
                }
            }
        }
""")
M["M09_collidable_npc_traverses"] = (MP, "if (mover.GameplayProxy is not EntityScriptProxy proxy)", "if (mover.GameplayProxy is not EntityScriptProxy proxy || !proxy.IsPlayer)")
M["M10_beannoid_stopped"] = (ES, """=> (entity.Flags & EntityFlags.Collidable) != 0
            && (entity.AnimFlags""", """=> true
            && (entity.AnimFlags""")
M["M11_flush_counts_as_overlap"] = (EC, ": delta >= entity.Width + 1)", ": delta > entity.Width + 1)")
M["M12_z_axis_ignored"] = (EC, "if (delta < 0 ? moddedPosZ - candidateModZ >= candidate.Depth + 1 : delta >= entity.Depth + 1)", "if (delta == int.MinValue)")
M["M13_destroyed_kept_hidden"] = None


def read(p):
    with open(os.path.join(REPO, p), "r", encoding="utf-8-sig", newline="") as f:
        return f.read()


def raw(p):
    with open(os.path.join(REPO, p), "rb") as f:
        return f.read()


def write_bytes(p, b):
    with open(os.path.join(REPO, p), "wb") as f:
        f.write(b)


def apply_edit(src_bytes, old, new, count_expected_first=True):
    text = src_bytes.decode("utf-8")
    crlf = "\r\n" in text
    if crlf:
        old = old.replace("\n", "\r\n")
        new = new.replace("\n", "\r\n")
    n = text.count(old)
    if n < 1:
        raise SystemExit("PATTERN NOT FOUND: " + old[:80])
    text = text.replace(old, new, 1)
    return text.encode("utf-8"), n


def main():
    mid, flt = sys.argv[1], sys.argv[2]
    originals = {}
    edits = []
    if mid == "M06_nearest_instead_of_first":
        edits.append((EC, "        var moddedPosZ = subjectPosZ + entity.ModZ;\n", "        var moddedPosZ = subjectPosZ + entity.ModZ;\n        EntityScriptProxy? mutBest = null; long mutBestDist = long.MaxValue;\n"))
        edits.append((EC, "                continue;\n            }\n\n            return candidate;\n        }\n\n        return null;\n    }\n\n    /// <summary>\n    /// The asymmetric AABB",
                      "                continue;\n            }\n\n            var mutDist = System.Math.Abs((long)candidateModX - moddedPosX) + System.Math.Abs((long)candidateModY - moddedPosY) + System.Math.Abs((long)candidateModZ - moddedPosZ);\n            if (mutDist <= mutBestDist) { mutBest = candidate; mutBestDist = mutDist; }\n        }\n\n        return mutBest;\n    }\n\n    /// <summary>\n    /// The asymmetric AABB"))
    elif mid == "M13_destroyed_kept_hidden":
        raise SystemExit("not used")
    else:
        edits.append(M[mid])
    for path, old, new in edits:
        if path not in originals:
            originals[path] = raw(path)
        b, n = apply_edit(raw(path), old, new)
        print(f"edit {path}: pattern found {n}x, first replaced")
        write_bytes(path, b)
    try:
        d = subprocess.run(["git", "-C", REPO, "diff", "--stat"], capture_output=True, text=True).stdout
        print(d)
        cmd = ["dotnet", "test", REPO + "/Alundra.Tests", "-c", "Release", "-p:CustomAfterMicrosoftCommonTargets=" + SKIP,
               "--blame-hang-timeout", "60s", "--filter", flt, "--nologo", "-v", "q"]
        r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=590)
        out = r.stdout + r.stderr
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), mid + ".log"), "w", encoding="utf-8") as f:
            f.write(out)
        print("exit", r.returncode)
        errs = re.findall(r"error CS\d+.*", out)
        if errs:
            print("BUILD ERROR:", errs[:3])
        failed = sorted(set(re.findall(r"\]\s+(Alundra\.Tests\.\S.*?) \[FAIL\]", out)))
        print("FAILED:", len(failed))
        for x in failed[:40]:
            print("  ", x)
        for l in out.splitlines():
            if re.search(r"Passed!|Failed!|Total tests|Total:|Passed:|Failed:", l):
                print(l.strip())
    finally:
        for path, b in originals.items():
            write_bytes(path, b)
        subprocess.run(["git", "-C", REPO, "checkout", "--"] + list(originals.keys()))
        d = subprocess.run(["git", "-C", REPO, "diff", "--stat", "--", "Alundra"], capture_output=True, text=True).stdout
        print("DIFF AFTER REVERT (empty expected):", repr(d))


main()
