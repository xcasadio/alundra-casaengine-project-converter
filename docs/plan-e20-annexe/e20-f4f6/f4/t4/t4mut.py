import subprocess, sys, re, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
SKIP = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
AI = "Alundra/Ai/Projectiles/ChargedArrowAi.cs"
NE = "Alundra/Rendering/Effects/NativeEffects.cs"
PM = "Alundra/Hero/PlayerManager.cs"
NR = "Alundra/Ai/NativeSpriteRegistry.cs"
RUN = "Alundra/Events/EventProgramRunner.cs"
TICK = "        CheckAndTriggerTileEffect(entity);\n        CreateTrail(entity);\n"
M = [
 ("M01 C94 absent (Tick does nothing)", AI, TICK, "        return;\n"),
 ("M02 trail period 8 (& 3 -> & 7)", AI, "(entity.FrameCounter & 3) != 0", "(entity.FrameCounter & 7) != 0"),
 ("M03 trail also at S (counter 1)", AI, "(entity.FrameCounter & 3) != 0 ||", "((entity.FrameCounter & 3) != 0 && entity.FrameCounter != 1) ||"),
 ("M04 trail also in status 3", AI, "entity.Status != EntityStatus.Normal ||", "(entity.Status != EntityStatus.Normal && entity.Status != EntityStatus.Deactivated) ||"),
 ("M05 wrong animation (0)", AI, "var direction = DirectionIndex(entity.TargetDirection);", "var direction = 0;"),
 ("M06a force X not shifted", NE, "slot.ForceX = -forceX >> 1;", "slot.ForceX = -forceX;"),
 ("M06b force Y not shifted", NE, "slot.ForceY = -forceY >> 1;", "slot.ForceY = -forceY;"),
 ("M07 Z offset 4 px (0x40000)", NE, "ChargedArrowTrailEffect, direction, x, y, z + 0x80000)", "ChargedArrowTrailEffect, direction, x, y, z + 0x40000)"),
 ("M08 plug called only every 4 ticks", AI, TICK, "        if ((entity.FrameCounter & 3) == 0) { CheckAndTriggerTileEffect(entity); }\n        CreateTrail(entity);\n"),
 ("M09 plug never called", AI, TICK, "        CreateTrail(entity);\n"),
 ("M09b port call removed (journal kept)", AI, "        Alundra.Cells.TileEffect.Trigger(entity);\n", ""),
 ("M10 7 forgotten in the registry", NR, "        ChargedArrowAi.Register(registry);\n", ""),
 ("M11 7 absent from the production set", PM, "HashSet<int> { 7, 27, 28, 29 }", "HashSet<int> { 27, 28, 29 }"),
 ("M12 4 present in the production set", PM, "HashSet<int> { 7, 27, 28, 29 }", "HashSet<int> { 4, 7, 27, 28, 29 }"),
 ("M13 E1 without break effect", RUN, "entity.ScriptHost?.DestroyEntity(entity, -1);\n            return;\n        }\n\n        // E14.a0 (D-E14-1", "entity.ScriptHost?.DestroyEntity(entity, 0);\n            return;\n        }\n\n        // E14.a0 (D-E14-1"),
]
FILTER = "FullyQualifiedName~ChargedArrow|FullyQualifiedName~WeaponSpawnSeam|FullyQualifiedName~NativeSpriteRegistry|FullyQualifiedName~NativeSpriteTypeFilter|FullyQualifiedName~WaveSword|FullyQualifiedName~BirthTick"
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
        fails = re.findall(r"^\s+\S+\s+(Alundra\.Tests\.\S+)(?: \[|$)", out, re.M)
        summ = re.findall(r"(?:échec|Failed)\s*:\s*(\d+),\s*(?:réussite|Passed)\s*:\s*(\d+)", out)
        open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'last.out'),'w',encoding='utf-8').write(out)
        err = ("error" in out.lower()) and not summ
        print(f"{name}: {'BUILD/RUN ERROR' if err else ('KILLED' if (summ and int(summ[0][0])>0) else 'SURVIVED')} summary={summ} nfail={len(set(fails))}", flush=True)
        for t in sorted(set(fails))[:10]: print("    ", t)
        if err: print(out[-1500:])
    finally:
        open(path, "wb").write(data.encode("utf-8"))
    st = git("diff", "--stat", "--", f).stdout.strip()
    print("   diff after revert:", repr(st), flush=True)
