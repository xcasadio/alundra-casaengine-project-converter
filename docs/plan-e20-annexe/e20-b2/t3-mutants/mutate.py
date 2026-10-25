import subprocess, sys, re, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
SK = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
OUT = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/b2t3/out"
os.makedirs(OUT, exist_ok=True)
EPR = "Alundra/Events/EventProgramRunner.cs"
M = {
 "M1_threshold_gt": (EPR, "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;", "state.Result = _gameState.DeathRetryCount > v[1] ? 1 : 0;"),
 "M2_two_param_bytes": (EPR, "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;\n                return 2;", "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;\n                return 3;"),
 "M3_result_not_written": (EPR, "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;", "_ = _gameState.DeathRetryCount >= v[1] ? 1 : 0;"),
 "M4_rank_item8_is_3": ("Alundra/Hero/PlayerManager.cs", "var currentPriority = properties[(int)(currentIndex * 5 + 2)];", "var currentPriority = currentIndex == 8 ? 3u : properties[(int)(currentIndex * 5 + 2)];"),
 "M5_sheet38_ignored": ("Alundra/Combat/BalanceTables.cs", "return WalkChain(_entries[itemId + 0x1E], threshold);", "return WalkChain(_entries[1 + 0x1E], threshold);"),
 "M6_equipment_bit_ignored": ("Alundra/Combat/Damage.cs", "if ((attack.Attribute & EquipmentBit) != 0 && equipment != null)", "if (false && equipment != null)"),
 "M7_record11_chargeable": ("Alundra/Hero/PlayerManager.cs", "&& WeaponTables.RecordByte(WeaponTables.RecordsAddress + (uint)(WeaponTables.RecordSize * player.CurrentWeaponRecord)) != 0\n            && WeaponTables.AnimationCountsCharge", "&& (WeaponTables.RecordByte(WeaponTables.RecordsAddress + (uint)(WeaponTables.RecordSize * player.CurrentWeaponRecord)) != 0 || player.CurrentWeaponRecord == 11)\n            && WeaponTables.AnimationCountsCharge"),
 "M8_flag1787_at_refusal": (EPR, "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;", "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0; if (state.Result == 0) { _gameState.AddFlag(1787u, 1u << (1787 & 0x1f)); }"),
 "M9_item8_never_selected": ("Alundra/Hero/PlayerManager.cs", "if (entrySectionId == slotId)\n            {\n                var usageCount = state.NumberOfItems[currentIndex * 2 + 1];", "if (entrySectionId == slotId && currentIndex != 8)\n            {\n                var usageCount = state.NumberOfItems[currentIndex * 2 + 1];"),
 "M10_air_swing_uses_ground_record": ("Alundra/Hero/PlayerManager.cs", "var address = WeaponTables.AnimationAddress(player.CurrentWeaponRecord, IsHeroInTheAir(player));", "var address = WeaponTables.AnimationAddress(player.CurrentWeaponRecord, false);"),
 "M11_c2_signed_compare": (EPR, "state.Result = _gameState.DeathRetryCount >= v[1] ? 1 : 0;", "state.Result = (sbyte)_gameState.DeathRetryCount >= (sbyte)v[1] ? 1 : 0;"),
 "M12_record31_runs_sprite_handler": ("Alundra/Entities/EntityScriptProxy.cs", "var programIndex = ProgramIndexes[EventTrigger] & 0x7f;\n\n        if (programIndex == 0)\n        {\n            // g_entityEventFunctionsByType", "var programIndex = ProgramIndexes[EventTrigger] & 0x00;\n\n        if (programIndex == 0)\n        {\n            // g_entityEventFunctionsByType"),
}
FILTER = "|".join("FullyQualifiedName~" + c for c in ["DeathRetryCountOpcodeTests", "LegendarySwordSnowKingArcTests", "ItemInventoryTests", "DamageTests", "LegendarySwordArenaTests", "TestSavesTests"])

def run(cmd):
    return subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace")

for name in sys.argv[1:]:
    f, old, new = M[name]
    path = os.path.join(REPO, f)
    raw = open(path, "rb").read().decode("utf-8")
    crlf = "\r\n" in raw
    text = raw.replace("\r\n", "\n")
    assert text.count(old) == 1, (name, text.count(old))
    open(path, "wb").write(text.replace(old, new).replace("\n", "\r\n" if crlf else "\n").encode("utf-8"))
    try:
        r = subprocess.run(["dotnet", "test", "Alundra.Tests", "-c", "Release", "-p:CustomAfterMicrosoftCommonTargets=" + SK, "--filter", FILTER, "--blame-hang-timeout", "60s"], cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace")
        full = r.stdout + r.stderr
        open(os.path.join(OUT, name + ".full"), "w", encoding="utf-8").write(full)
        red = sorted(set(re.sub(r" \[[^\]]*\]$", "", l.strip()) for l in full.splitlines() if l.strip().startswith(("Échoué", "Failed"))))
        tail = [l for l in full.splitlines() if "Échec!" in l or "Réussi!" in l or "error CS" in l]
        open(os.path.join(OUT, name + ".red"), "w", encoding="utf-8").write("\n".join(red) + "\n")
        print(name, "KILLED" if red else "SURVIVED", len(red), "red;", tail[:2])
    finally:
        run(["git", "checkout", "--", f])
    d = run(["git", "diff", "--quiet", "--", f])
    assert d.returncode == 0, "diff not empty after " + name
