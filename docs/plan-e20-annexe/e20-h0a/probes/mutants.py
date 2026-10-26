import sys, subprocess, re, os
REPO = "D:/development/repo/alundra-casaengine-project-converter"
PM = "Alundra/Hero/PlayerManager.cs"
ID = "Alundra/UI/Inventory/InventoryDirector.cs"
SKIP = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
FILTER = "FullyQualifiedName~HeroHurtTests|FullyQualifiedName~HeroItemUseTests|FullyQualifiedName~InventoryDirectorTests|FullyQualifiedName~HeroSwimTests|FullyQualifiedName~HeroRunTests"

# id -> (description, file, [(old, new), ...])
M = {
 "M01": ("front ignored (the edge test always false)", PM,
   [("if ((pad.ButtonsJustPressed & PadState.Circle) == 0 || player.ScriptHost is not { } host)", "if (false || player.ScriptHost is not { } host)")]),
 "M02": ("lock limited to 0x34", PM,
   [("if (host.GameState.PlayerControlFlags != 0)\n        {\n            host.SoundPlayer?.PlaySfx(ItemRefusalSoundId);", "if ((host.GameState.PlayerControlFlags & 0x34) != 0)\n        {\n            host.SoundPlayer?.PlaySfx(ItemRefusalSoundId);")]),
 "M03": ("bound 0x62 lowered to 0x32", PM,
   [("ItemRefusalBound = 0x62;", "ItemRefusalBound = 0x32;")]),
 "M04": ("a hole forgotten (0x2A)", PM,
   [("item is 0x21 or 0x22 or 0x28 or 0x2A;", "item is 0x21 or 0x22 or 0x28;")]),
 "M05": ("a hole too many (0x25)", PM,
   [("item is 0x21 or 0x22 or 0x28 or 0x2A;", "item is 0x21 or 0x22 or 0x28 or 0x2A or 0x25;")]),
 "M06": ("item in use ignored", PM,
   [("|| (player.ItemInUse != 0 && player.ItemInUse != item)", "|| false")]),
 "M07": ("refusal as soon as an item is in use, the same included", PM,
   [("(player.ItemInUse != 0 && player.ItemInUse != item)", "(player.ItemInUse != 0)")]),
 "M08": ("counter not incremented", PM,
   [("            player.ItemUseCounter++;", "            ;")]),
 "M09": ("release on 0 instead of non-zero", PM,
   [("        if (answer != 0)\n        {\n            player.ItemInUse = 0;", "        if (answer == 0)\n        {\n            player.ItemInUse = 0;")]),
 "M10": ("release at every tick", PM,
   [("        if (answer != 0)\n        {\n            player.ItemInUse = 0;", "        if (true)\n        {\n            player.ItemInUse = 0;")]),
 "M11": ("release before the cancellation of the item", PM,
   [("        if (ItemCancelForTests is { } cancel)\n        {\n            cancel(player, item);", "        player.ItemInUse = 0;\n        if (ItemCancelForTests is { } cancel)\n        {\n            cancel(player, item);")]),
 "M12": ("increment before the driver of the item", PM,
   [("        var answer = ItemDriverForTests is { } driver ? driver(player, item) : ItemDriverStub(item);\n        if (player.ItemUseCounter < int.MaxValue)\n        {\n            player.ItemUseCounter++;\n        }\n",
     "        if (player.ItemUseCounter < int.MaxValue)\n        {\n            player.ItemUseCounter++;\n        }\n        var answer = ItemDriverForTests is { } driver ? driver(player, item) : ItemDriverStub(item);\n")]),
 "M13": ("driver called in the locked branch, before the cancellation (the item still in use)", PM,
   [("PlayerAnimationEffects.Create(player, PlayerAnimationEffects.LockedMode);\n", "PlayerAnimationEffects.Create(player, PlayerAnimationEffects.LockedMode);\n            UpdateItemUseDriver(player);\n")]),
 "M13b": ("driver called in the locked branch, after the cancellation (the item already freed)", PM,
   [("        CancelItemMode(player);\n\n            // E20.c2b D-C2B-5", "        CancelItemMode(player);\n            UpdateItemUseDriver(player);\n\n            // E20.c2b D-C2B-5")]),
 "M14": ("cancellation by a final else", PM,
   [("else if (SwimHandlerOf(player.TargetAnimationId) == SwimHandler.Skip)\n        {\n            CancelItemMode(player);", "else\n        {\n            CancelItemMode(player);")]),
 "M15": ("cancellation without release", PM,
   [("            ItemCancelStub(item);\n        }\n\n        player.ItemInUse = 0;", "            ItemCancelStub(item);\n        }\n")]),
 "M16": ("guard of the menu absent", ID,
   [("if (player != null && player.ItemInUse != 0)", "if (false)")]),
 "M17": ("cancellation call removed from the locked branch", PM,
   [("        CancelItemMode(player);\n\n            // E20.c2b D-C2B-5", "\n\n            // E20.c2b D-C2B-5")]),
 "M18": ("cancellation removed from the swim Skip group", PM,
   [("            case SwimHandler.Skip:\n                CancelItemMode(player);\n                break;", "            case SwimHandler.Skip:\n                break;")]),
 "M19": ("counter not reset at the start", PM,
   [("            player.ItemUseCounter = 0;\n            player.ItemUsePhase = 0;", "            player.ItemUsePhase = 0;")]),
 "M20": ("phase not reset at the start", PM,
   [("            player.ItemUseCounter = 0;\n            player.ItemUsePhase = 0;", "            player.ItemUseCounter = 0;")]),
 "M21": ("refusal sound 3 changed to 4", PM,
   [("ItemRefusalSoundId = 3;", "ItemRefusalSoundId = 4;")]),
 "M22": ("start answer inverted (in use on non-zero)", PM,
   [("        if (answer == 0)\n        {\n            player.ItemInUse = item;", "        if (answer != 0)\n        {\n            player.ItemInUse = item;")]),
 "M23": ("counter no longer saturated", PM,
   [("if (player.ItemUseCounter < int.MaxValue)", "if (true)")]),
 "M24": ("lower bound of the table raised (0x1F to 0x20)", PM,
   [("FirstUsableItem = 0x1F;", "FirstUsableItem = 0x20;")]),
 "M25": ("width of the table reduced (0x14 to 0x13)", PM,
   [("UsableItemCount = 0x14;", "UsableItemCount = 0x13;")]),
 "M26": ("driver also called at the tick of the injury removed", PM,
   [("        UpdatePlayerCarriedEntity(player, 2);\n        UpdateItemUseDriver(player);\n        player.TargetAnimationId = slopeAtHead", "        UpdatePlayerCarriedEntity(player, 2);\n        player.TargetAnimationId = slopeAtHead")]),
 "M27": ("free-tick driver call removed", PM,
   [("        UpdateItemUseDriver(player);\n\n        // PlayerManager.cs:61-353", "\n\n        // PlayerManager.cs:61-353")]),
 "M28": ("driver also called with BlockedByEntity", PM,
   [("        if (player.BlockedByEntity != null)\n        {\n", "        if (player.BlockedByEntity != null)\n        {\n            UpdateItemUseDriver(player);\n")]),
}

def run(cmd, timeout=590):
    return subprocess.run(cmd, cwd=REPO, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=timeout)

def main(ids, apply_only=False):
    for mid in ids:
        desc, f, reps = M[mid]
        path = os.path.join(REPO, f)
        raw = open(path, "rb").read()
        text = raw.decode("utf-8")
        eol = "\r\n" if "\r\n" in text else "\n"
        new = text
        for old, rep in reps:
            o = old.replace("\n", eol); r = rep.replace("\n", eol)
            if new.count(o) != 1:
                print(f"{mid}: PATTERN COUNT {new.count(o)} for {old[:60]!r}")
                new = None
                break
            new = new.replace(o, r)
        if new is None:
            continue
        open(path, "wb").write(new.encode("utf-8"))
        try:
            p = run(["dotnet", "test", "Alundra.Tests/Alundra.Tests.csproj", "-c", "Release",
                     f"-p:CustomAfterMicrosoftCommonTargets={SKIP}", "--filter", FILTER,
                     "--blame-hang-timeout", "60s", "--nologo", "-v", "q"])
            out = p.stdout + p.stderr
        finally:
            open(path, "wb").write(raw)
        failed = sorted(set(re.findall(r"\]\s+(?:Alundra\.Tests\.)?(\S.*?) \[FAIL\]", out)))
        summ = re.findall(r"(?:chou.|Pass|Fail|R.ussi)\S*!?\s*-\s*(?:Failed|.chec)\s*:\s*(\d+), (?:Passed|r.ussite)\s*:\s*(\d+),.*?(?:total|Total)\s*:\s*(\d+)", out)
        err = re.findall(r"error CS\d+.*", out)[:3]
        print(f"=== {mid} | {desc} | exit={p.returncode} | {summ} | red={len(failed)} {err}")
        for n in failed[:12]:
            print("   ", n)
        d = run(["git", "diff", "--quiet", "--", f])
        print("   diff-empty-after:", d.returncode == 0)

if __name__ == "__main__":
    main(sys.argv[1:])
