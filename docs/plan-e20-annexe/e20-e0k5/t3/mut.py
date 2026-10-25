import subprocess, sys, os, re
REPO = "D:/development/repo/alundra-casaengine-project-converter"
SK = "C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets"
OUT = REPO + "/docs/plan-e20-annexe/e20-e0k5/t3"
SM = "Alundra/Physics/ScriptedMotion.cs"
HS = "Alundra/Physics/HalvingStepSearch.cs"
FILTER = "FullyQualifiedName~Alundra.Tests.Physics.HalvingStepSearchTests|FullyQualifiedName~Alundra.Tests.Physics.FirstStepBoundWorldTests"

BOUND_OLD = "entity.FinalForceX = bounded.X;\n            entity.FinalForceY = bounded.Y;\n            noStepAccepted = !bounded.Accepted;"

M = {
 "M1": (HS, None, "largest free position instead of the halving loop"),
 "M2": (HS, ("var stepY = forceY;", "var stepY = 0;"), "the loop on a single axis (Y ignored)"),
 "M3": (SM, ("noStepAccepted = !bounded.Accepted;", "noStepAccepted = false;"), "the flag not raised"),
 "M4": (SM, (BOUND_OLD, "var origX = entity.FinalForceX;\n            entity.FinalForceX = bounded.X;\n            entity.FinalForceY = bounded.Y;\n            noStepAccepted = !bounded.Accepted || bounded.X != origX;"), "the flag raised earlier, also on a partial bound"),
 "M5": (SM, (" && entity.SelfEntity?.World == null && (entity.FinalForceX", " && (entity.FinalForceX"), "the bound also after the integration"),
 "M6": (SM, ("var mask = CellsCollisionField.WalkabilityMaskFor(entity.Flags);", "var mask = CellsCollisionField.WalkabilityMaskFor(entity.Flags) & ~0x40u;"), "the wall test without 0x40"),
 "M7": (SM, ("sample.GroundHeight > reach", "sample.GroundHeight >= reach"), "the corner height test with >= instead of >"),
 "M8": (SM, ("if (entity.Controller != null && entity.HasLogicalZ && entity.SelfEntity?.World == null", "if (false && entity.Controller != null && entity.HasLogicalZ && entity.SelfEntity?.World == null"), "the step of the birth left free"),
 "M9": (SM, ("(entity.TargetDirection & 7) == 0, (dx, dy)", "(entity.TargetDirection & 7) != 0, (dx, dy)"), "the cardinal rule inverted"),
 "M10": (SM, ("var xs = new[] { left >> 16, (left + entity.Width) >> 16 };", "var xs = new[] { left >> 16, (left + entity.Width - 1) >> 16 };"), "the right edge of the box one raw unit short"),
 "M11": (HS, ("if (attempt == 0)\n                {\n                    return new HalvingStepResult(stepX, stepY, true);\n                }\n\n                acceptedX += stepX;", "acceptedX += stepX;"), "the first whole step accumulated instead of ending the search"),
 "M12": (SM, ("noStepAccepted = !bounded.Accepted;", "noStepAccepted = !bounded.Accepted; entity.FinalForceX = bounded.X + (bounded.Accepted ? 0 : 0); if (!bounded.Accepted) { noStepAccepted = false; }"), "the flag dropped when nothing is accepted (alias of M3 kept for a different shape)"),
}

def git(*a):
    return subprocess.run(["git", "-C", REPO] + list(a), capture_output=True, text=True)

def sh(cmd, log):
    with open(log, "w", encoding="utf-8") as f:
        p = subprocess.run(cmd, cwd=REPO, stdout=f, stderr=subprocess.STDOUT, shell=False)
    return p.returncode

M1_BODY = '''    public static HalvingStepResult Search(int forceX, int forceY, bool cardinal, Func<int, int, bool> isBlockedAt)
    {
        if (forceX == 0 && forceY == 0) { return new HalvingStepResult(0, 0, false); }
        long m = Math.Max(Math.Abs((long)forceX), Math.Abs((long)forceY));
        long lo = 0, hi = m;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (isBlockedAt((int)((long)forceX * mid / m), (int)((long)forceY * mid / m))) { hi = mid - 1; } else { lo = mid; }
        }
        return new HalvingStepResult((int)((long)forceX * lo / m), (int)((long)forceY * lo / m), lo > 0);
    }

    private static int Half(int value) => value == -1 ? 0 : value >> 1;
}
'''

def main():
    name = sys.argv[1]
    if git("diff", "--quiet", "--", "Alundra/", "Alundra.Tests/").returncode != 0:
        print("DIRTY before"); sys.exit(2)
    os.makedirs(OUT, exist_ok=True)
    path, rep, desc = M[name]
    full = REPO + "/" + path
    src = open(full, encoding="utf-8", newline="").read()
    crlf = "\r\n" in src
    s = src.replace("\r\n", "\n")
    if name == "M1":
        i = s.index("    public static HalvingStepResult Search(")
        new = s[:i] + M1_BODY
    else:
        old, nw = rep
        assert s.count(old) == 1, (name, s.count(old))
        new = s.replace(old, nw)
    if crlf: new = new.replace("\n", "\r\n")
    open(full, "w", encoding="utf-8", newline="").write(new)
    try:
        diff = git("diff", "--", path).stdout
        open(f"{OUT}/{name}.diff", "w", encoding="utf-8").write(diff)
        log = f"{OUT}/{name}.full"
        rc = sh(["dotnet", "test", "Alundra.Tests", "-c", "Release", f"-p:CustomAfterMicrosoftCommonTargets={SK}", "--filter", FILTER, "--blame-hang-timeout", "60s"], log)
    finally:
        git("checkout", "--", path)
    txt = open(log, encoding="utf-8", errors="replace").read()
    red = sorted(set(re.sub(r" \[[0-9 a-z<]*\]$", "", l.strip()) for l in txt.splitlines() if re.match(r"^\s+(Échoué|Failed) ", l)))
    open(f"{OUT}/{name}.red", "w", encoding="utf-8").write("\n".join(red) + "\n")
    tail = "\n".join(txt.splitlines()[-4:])
    open(f"{OUT}/{name}.summary", "w", encoding="utf-8").write(tail + "\n")
    clean = git("diff", "--quiet", "--", "Alundra/", "Alundra.Tests/").returncode == 0
    print(name, desc, "rc", rc, "red", len(red), "diff-empty-after", clean)
    print(tail)

main()
