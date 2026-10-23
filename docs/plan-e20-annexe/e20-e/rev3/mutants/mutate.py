import subprocess, sys, json, os

REPO = "D:/development/repo/alundra-casaengine-project-converter"
SKIP = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
OUT = os.path.dirname(os.path.abspath(__file__))
TE = "Alundra/Cells/TileEffect.cs"
CS = "Alundra/Cells/CellStore.cs"
NA = "Alundra/Rendering/Effects/NativeEffects.cs"
WP = "Alundra/Maps/WorldProxy.cs"
PA = "Alundra/Hero/PlayerAnimationEffects.cs"
WS = "Alundra/Ai/Projectiles/WaveSwordAi.cs"

M = []
def m(mid, desc, f, edits):
    M.append((mid, desc, f, edits))

m("M01", "south row divided twice (D3)", TE, [("rowB = (entity.HitBoxY + entity.CollisionDepth) >> 20;", "rowB = ((entity.HitBoxY + entity.CollisionDepth) >> 20) >> 4;")])
m("M02", "+0x80000 added to the Z of the effect and of the loot (D4)", TE, [("row * 0x100000 + 0x80000, height << 20)", "row * 0x100000 + 0x80000, (height << 20) + 0x80000)")])
m("M03", "truncated division instead of the floor (D9)", TE, [("rowA = entity.HitBoxY >> 20;", "rowA = entity.HitBoxY / 0x100000;"), ("rowB = (entity.HitBoxY + entity.CollisionDepth) >> 20;", "rowB = (entity.HitBoxY + entity.CollisionDepth) / 0x100000;")])
m("M04", "column bounded for the position", TE, [("new CutSite(column * 0x180000 + 0xC0000,", "new CutSite(cellColumn * 0x180000 + 0xC0000,")])
m("M05", "row bounded for the position", TE, [("row * 0x100000 + 0x80000, height << 20)", "cellRow * 0x100000 + 0x80000, height << 20)")])
m("M06", "last corner forgotten", TE, [("for (var corner = 0; corner < 4; corner++)", "for (var corner = 0; corner < 3; corner++)")])
m("M07", "first corner forgotten", TE, [("for (var corner = 0; corner < 4; corner++)", "for (var corner = 1; corner < 4; corner++)")])
m("M08", "column order of the corners changed", TE, [("(corner & 1) == 0 ? columnA : columnB", "(corner & 1) == 0 ? columnB : columnA")])
m("M09", "row order of the corners changed", TE, [("corner < 2 ? rowA : rowB", "corner < 2 ? rowB : rowA")])
m("M10", "bit 0x01 read instead of 0x02", TE, [("(walkability & CellStore.CuttableWalkabilityBit) == 0", "(walkability & 0x01) == 0")])
m("M11", "bit 0x01 cleared instead of 0x02 by the cut", CS, [("_walkability[index] &= ~CuttableWalkabilityBit;", "_walkability[index] &= ~0x01;")])
m("M12", "strict band of Z", TE, [("cellTop < bandBottom || bandTop < cellTop", "cellTop <= bandBottom || bandTop <= cellTop")])
m("M13", "band without the +1 of the Z frame (Q-E6 naive rule)", TE, [("bandBottom = entity.HitBoxZ + 1;", "bandBottom = entity.HitBoxZ;")])
m("M14", "box of the current tick (StoreHitBox first)", TE, [("    public static void Trigger(EntityScriptProxy entity)\n    {\n", "    public static void Trigger(EntityScriptProxy entity)\n    {\n        entity.StoreHitBox();\n")])
m("M15", "stack word kept (-1 not written)", CS, [("        _wallTilesOffset[index] = -1;\n        _wallTiles[index] = null;\n\n        if (CellsMutated != null)", "        if (CellsMutated != null)")])
m("M16", "stack kept (not destroyed), word -1", CS, [("        _wallTiles[index] = null;\n\n        if (CellsMutated != null)", "        if (CellsMutated != null)")])
m("M17", "collision changed by the cut (walkability zeroed)", CS, [("_walkability[index] &= ~CuttableWalkabilityBit;", "_walkability[index] = 0;")])
m("M18", "effect created at once (CreateNow) instead of the queue", TE, [("world.EffectStage?.Emit(reservoir => NativeEffects.CreateTileCutEffect(reservoir, effect, x, y, z));", "world.EffectStage?.CreateNow(false, effect, 0, x, y, z);")])
m("M19", "sound 31 before the loot (and its sound 84)", TE, [("            LootDrop.SpawnRandomLoot(world, host.GameState, LootCode, x, y, z);\n            world.SoundPlayer?.PlaySfx(CutSfx);", "            world.SoundPlayer?.PlaySfx(CutSfx);\n            LootDrop.SpawnRandomLoot(world, host.GameState, LootCode, x, y, z);")])
m("M20", "sound 31 forgotten", TE, [("            world.SoundPlayer?.PlaySfx(CutSfx);\n", "")])
m("M21", "the four flags read before the first cut", TE, [
    ("        var count = 0;\n        for (var corner", "        var count = 0;\n        Span<(int, int)> pending = stackalloc (int, int)[4];\n        var pendingCount = 0;\n        for (var corner"),
    ("            mutator.CutCell(cellColumn, cellRow);\n", "            pending[pendingCount++] = (cellColumn, cellRow);\n"),
    ("        return count;\n    }\n}", "        for (var k = 0; k < pendingCount; k++)\n        {\n            mutator.CutCell(pending[k].Item1, pending[k].Item2);\n        }\n\n        return count;\n    }\n}")])
m("M22", "effect, loot and sound without the cut (bit not cleared)", TE, [("            mutator.CutCell(cellColumn, cellRow);\n", "")])
m("M23", "property read under the old name BalanceLevel", TE, [('CutEffectPropertyName = "TileCutEffectId"', 'CutEffectPropertyName = "BalanceLevel"')])
m("M24", "fallback forgotten (0 when the property is missing)", TE, [("        return DefaultCutEffectIndex;\n    }\n\n    /// <summary>The binary's table", "        return 0;\n    }\n\n    /// <summary>The binary's table")])
m("M25", "effect taken in the map table (mapTable = true)", NA, [("reservoir.Create(false, effect, 0, x, y, z);\n        if (slot != null)\n        {\n            CreatedForTests?.Invoke(slot);\n        }\n    }\n\n    /// <summary>The global effect of the charge", "reservoir.Create(true, effect, 0, x, y, z);\n        if (slot != null)\n        {\n            CreatedForTests?.Invoke(slot);\n        }\n    }\n\n    /// <summary>The global effect of the charge")])
m("M26", "effect id fixed to 2 instead of the host's value", TE, [("var effect = host.TileCutEffectIndex;", "var effect = DefaultCutEffectIndex;")])
m("M27", "property not read at the map load (index left at 2)", WP, [("TileCutEffectIndex = TileEffect.ResolveCutEffectIndex(tileMapData);", "TileCutEffectIndex = TileEffect.DefaultCutEffectIndex;")])
m("M28", "frame-box gate removed", TE, [("        if (!entity.HasFrameBox\n            || entity.ScriptHost is not IEntityWorldContext world", "        if (entity.ScriptHost is not IEntityWorldContext world")])
m("M29", "the hero does not call the port", PA, [("        Alundra.Cells.TileEffect.Trigger(player);\n", "")])
m("M30", "the wave does not call the port", WS, [("        Alundra.Cells.TileEffect.Trigger(entity);\n", "")])
m("M31", "loot rolled with another code", TE, [("public const int LootCode = 0xFF;", "public const int LootCode = 0x00;")])
m("M32", "the short cast of the west column removed", TE, [("ColumnOf((short)(entity.HitBoxX >> 16))", "ColumnOf(entity.HitBoxX >> 16)")])
m("M33", "row clamp off by one", TE, [("private const int MaxRow = 0x3B;", "private const int MaxRow = 0x3A;")])
m("M34", "column clamp off by one", TE, [("private const int MaxColumn = 0x33;", "private const int MaxColumn = 0x32;")])

FILTER = "FullyQualifiedName~TileEffect|FullyQualifiedName~TileCutEffectId|FullyQualifiedName~HeroAttackTests|FullyQualifiedName~TestSavesTests|FullyQualifiedName~CellStore|FullyQualifiedName~WaveSword|FullyQualifiedName~PlayerAnimationEffects|FullyQualifiedName~BalanceTables"

def git(*a):
    return subprocess.run(["git", "-C", REPO] + list(a), capture_output=True, text=True)

def run(cmd, timeout=500):
    return subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, cwd=REPO)

lo, hi = int(sys.argv[1]), int(sys.argv[2])
for idx, (mid, desc, f, edits) in enumerate(M, 1):
    if idx < lo or idx > hi:
        continue
    path = REPO + "/" + f
    with open(path, "rb") as fh:
        raw = fh.read()
    crlf = b"\r\n" in raw
    text = raw.decode("utf-8")
    if crlf:
        text = text.replace("\r\n", "\n")
    for old, new in edits:
        if text.count(old) != 1:
            print(mid, "EDIT-NOT-UNIQUE", text.count(old), old[:60]); sys.exit(2)
        text = text.replace(old, new)
    if crlf:
        text = text.replace("\n", "\r\n")
    with open(path, "wb") as fh:
        fh.write(text.encode("utf-8"))
    try:
        b = run(["dotnet", "build", "Alundra.Tests/Alundra.Tests.csproj", "-c", "Release", "-v", "q", "-nologo", "-p:CustomAfterMicrosoftCommonTargets=" + SKIP])
        if b.returncode != 0:
            res = "BUILD-ERROR"
            detail = "\n".join(l for l in b.stdout.splitlines() if "error" in l)[:600]
        else:
            t = run(["dotnet", "test", "Alundra.Tests/Alundra.Tests.csproj", "-c", "Release", "--no-build", "-nologo", "-p:CustomAfterMicrosoftCommonTargets=" + SKIP, "--blame-hang-timeout", "60s", "--filter", FILTER])
            out = t.stdout
            import re
            both = out + " " + t.stderr
            failed = sorted(set(m_.group(1).replace("Alundra.Tests.", "") for m_ in re.finditer(r"(Alundra\.Tests\.\S+) \[FAIL\]", both)))
            summ = [l for l in out.splitlines() if re.search(r"total\s*:", l)]
            res = "KILLED" if t.returncode != 0 else "SURVIVED"
            detail = (summ[-1] if summ else "") + " | " + " ; ".join(x[:140] for x in failed[:6]) + (" ... (+%d)" % (len(failed) - 6) if len(failed) > 6 else "")
    finally:
        git("checkout", "--", f)
    d = git("diff", "--stat", "--", f).stdout.strip()
    print(mid, res, desc, "|", detail, "| diff after revert:", repr(d), flush=True)
    with open(OUT + "/mutants.log", "a", encoding="utf-8") as lf:
        lf.write(f"{mid}\t{res}\t{desc}\t{detail}\tdiff-after-revert={d!r}\n")
