import subprocess, sys, re, os
# E20.f5 T4: real mutants of the DLL code, applied by script, reverted in a finally. Usage: t4mut.py [ID ...]
REPO = "D:/development/repo/alundra-casaengine-project-converter"
SKIP = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
AI = "Alundra/Ai/Projectiles/ArrowAi.cs"
SM = "Alundra/Physics/ScriptedMotion.cs"
PM = "Alundra/Hero/PlayerManager.cs"
NR = "Alundra/Ai/NativeSpriteRegistry.cs"
X_OV = "HitPass.AxisOverlaps(arrow.HitBoxX, arrow.CollisionWidth, bodyX, target.Width)"
Z_OV = "HitPass.AxisOverlaps(arrow.HitBoxZ, arrow.CollisionHeight, bodyZ, target.Depth)"
SET = "{ 3, 4, 7, 27, 28, 29 }"
M = [
 ("A01 DamagedTickCounter filter removed", AI, "                || target.DamagedTickCounter != 0\n", ""),
 ("A02 HitAnimByte 0x40 filter removed", AI, "                || (target.HitAnimByte & HitPass.InvulnerableAnimationBit) != 0\n", ""),
 ("A03 class mask filter removed", AI, "                || (target.Flags & mask) == 0\n", ""),
 ("A04 type 429 filter removed", AI, "                || target.SpriteType == ExcludedSpriteType)", ")"),
 ("A05 type 429 changed (0x1AC)", AI, "internal const int ExcludedSpriteType = 0x1AD;", "internal const int ExcludedSpriteType = 0x1AC;"),
 ("A06 self filter removed", AI, "            if (ReferenceEquals(target, arrow)\n                || (target.HitAnimByte", "            if ((target.HitAnimByte"),
 ("A07 hero excluded from the sweep", AI, "            if (ReferenceEquals(target, arrow)\n", "            if (ReferenceEquals(target, arrow) || target.IsPlayer\n"),
 ("A08 sweep stops one slot early", AI, "foreach (var target in context.SpawnedEntities)", "foreach (var target in System.Linq.Enumerable.SkipLast(context.SpawnedEntities, 1))"),
 ("A09 status filter added (FlagToDestroy skipped)", AI, "            int bodyX, bodyY, bodyZ;", "            if (target.Status == EntityStatus.FlagToDestroy) { continue; }\n\n            int bodyX, bodyY, bodyZ;"),
 ("A10 overlap exclusive on X", AI, X_OV, "HitPass.AxisOverlaps(arrow.HitBoxX, arrow.CollisionWidth - 1, bodyX, target.Width - 1)"),
 ("A11 overlap exclusive on Z (vertical reach)", AI, Z_OV, "HitPass.AxisOverlaps(arrow.HitBoxZ, arrow.CollisionHeight - 1, bodyZ, target.Depth - 1)"),
 ("A12 bounce direction +8", AI, "(entity.TargetDirection + 0x10) & 0x1F", "(entity.TargetDirection + 0x08) & 0x1F"),
 ("A13 gravity bit forgotten", AI, "private const uint BounceSetFlags = 0x140;", "private const uint BounceSetFlags = 0x40;"),
 ("A14 bounce phase not recognised", AI, "if (entity.TargetAnimationId == BounceAnimationId)", "if (entity.TargetAnimationId == 99)"),
 ("A15 end without the animation-end flag", AI, "if (entity.ForceResetAnimationFlag == 1)", "if (true)"),
 ("A16 status 3 at the fallback", AI, "entity.Status = EntityStatus.Normal;", "entity.Status = EntityStatus.Deactivated;"),
 ("A17 destruction with an effect (touch)", AI, "            entity.ScriptHost?.DestroyEntity(entity, -1);\n            return;\n        }\n\n        entity.Status", "            entity.ScriptHost?.DestroyEntity(entity, 8);\n            return;\n        }\n\n        entity.Status"),
 ("A18 destruction with an effect (end of the bounce)", AI, "                entity.ScriptHost?.DestroyEntity(entity, -1);\n            }\n\n            return;", "                entity.ScriptHost?.DestroyEntity(entity, 8);\n            }\n\n            return;"),
 ("A19 bounce on a hit (HitCounter test inverted)", AI, "if (entity.HitCounter == 0)", "if (entity.HitCounter != 0)"),
 ("B01 speed key on the current animation", SM, "(entity.JoinsBirthTick && entity.CurrentAnimationId == ~0u) || entity.HasLogicalZ ? entity.TargetAnimationId", "(entity.JoinsBirthTick && entity.CurrentAnimationId == ~0u) ? entity.TargetAnimationId"),
 ("B02 E17 registered for 7", AI, "SpriteTypes = { 3, 4, 5, 6 };", "SpriteTypes = { 3, 4, 5, 6, 7 };"),
 ("B03 E17 not registered", NR, "        ArrowAi.Register(registry);\n", ""),
 ("B04 set contains 5", PM, SET, "{ 3, 4, 5, 7, 27, 28, 29 }"),
 ("B05 set without 3 and 4 (removal proof)", PM, SET, "{ 7, 27, 28, 29 }"),
 ("B06 set without 4", PM, SET, "{ 3, 7, 27, 28, 29 }"),
 ("E01 Resync removed (equivalent expected)", AI, "        entity.ResyncControllerFromFlags();\n", ""),
 ("E02 Resync called twice (equivalent expected)", AI, "        entity.ResyncControllerFromFlags();\n", "        entity.ResyncControllerFromFlags();\n        entity.ResyncControllerFromFlags();\n"),
]
FILTER = ("FullyQualifiedName~ArrowAi|FullyQualifiedName~ArrowLife|FullyQualifiedName~ArrowProduction|FullyQualifiedName~ChargedArrow|FullyQualifiedName~WeaponSpawnSeam"
          "|FullyQualifiedName~NativeSpriteRegistry|FullyQualifiedName~WaveSword|FullyQualifiedName~BirthTick|FullyQualifiedName~LogicalZWorld|FullyQualifiedName~MapStateTests")
def git(*a):
    return subprocess.run(["git", "-C", REPO, *a], capture_output=True, text=True)
sel = sys.argv[1:]
for name, f, old, new in M:
    if sel and name.split()[0] not in sel: continue
    path = REPO + "/" + f
    data = open(path, "rb").read().decode("utf-8")
    crlf = "\r\n" in data
    d = data.replace("\r\n", "\n")
    assert d.count(old) == 1, (name, d.count(old))
    d2 = d.replace(old, new)
    if crlf: d2 = d2.replace("\n", "\r\n")
    open(path, "wb").write(d2.encode("utf-8"))
    try:
        r = subprocess.run(["dotnet", "test", REPO + "/Alundra.Tests/Alundra.Tests.csproj", "-c", "Release", f"-p:CustomAfterMicrosoftCommonTargets={SKIP}", "--filter", FILTER, "--blame-hang-timeout", "60s", "--nologo", "-v", "q"], capture_output=True, text=True, encoding="utf-8", errors="replace")
        out = r.stdout + r.stderr
        fails = re.findall(r"\[xUnit\.net [\d:.]+\]\s+(Alundra\.Tests\.\S.*?) \[FAIL\]", out)
        open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "last.out"), "w", encoding="utf-8").write(out)
        summ = re.findall(r"(?:échec|Failed)\s*:\s*(\d+),\s*(?:réussite|Passed)\s*:\s*(\d+)", out)
        err = ("error" in out.lower()) and not summ
        print(f"{name}: {'BUILD/RUN ERROR' if err else ('KILLED' if (summ and int(summ[0][0])>0) else 'SURVIVED')} summary={summ} nfail={len(set(fails))}", flush=True)
        for t in sorted(set(fails))[:12]: print("    ", t)
        if err: print(out[-1500:])
    finally:
        open(path, "wb").write(data.encode("utf-8"))
    st = git("diff", "--stat", "--", f).stdout.strip()
    print("   diff after revert:", repr(st), flush=True)
