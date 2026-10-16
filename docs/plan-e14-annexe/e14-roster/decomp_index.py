"""Index of the C# decompilation (read-only reference, alundra-datas-analyser submodule copy only).
Builds: (1) address -> (method name, file:line) from the '// 8003a1b8' style comments that precede each method;
        (2) the handler registrations of SpriteEventHandlers.cs: (slot, code) -> (class.method, line);
        (3) SpawnWarpEntity / SpawnChildEntity call sites with a literal sprite type.
Run: python -B decomp_index.py   (prints counts; the module is imported by the other scripts)."""
import os
import re
import sys

ROOT = r"D:\development\repo\alundra-casaengine-project-converter\alundra-datas-analyser\AlundraTools\AlundraEngine"
REL = "alundra-datas-analyser/AlundraTools/AlundraEngine/"

SLOTS = {"ProgramALoad": 0, "ProgramBMap": 1, "ProgramCTick": 2, "ProgramDTouch": 3, "ProgramEDeactivate": 4, "ProgramFInteract": 5}

ADDR_COMMENT = re.compile(r"//\s*(?:GHIDRA:\s*\w+\s*@\s*)?(?:0x)?([0-9a-fA-F]{8})\b")
METHOD_DECL = re.compile(r"^\s*(?:public|private|internal|protected)[^=;(]*?\b(\w+)\s*\(")


def cs_files():
    for dp, dn, fn in os.walk(ROOT):
        dn[:] = [d for d in dn if d not in ("obj", "bin")]
        for f in fn:
            if f.endswith(".cs"):
                yield os.path.join(dp, f)


def rel(path):
    return REL + os.path.relpath(path, ROOT).replace("\\", "/")


def build_addr_index():
    """address -> list of (name, 'relpath:line')."""
    idx = {}
    methods = {}  # (relpath, name) -> line (first)
    for path in cs_files():
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            lines = fh.read().split("\n")
        for i, line in enumerate(lines):
            m = ADDR_COMMENT.search(line)
            if m and line.strip().startswith("//"):
                for j in range(i + 1, min(i + 4, len(lines))):
                    d = METHOD_DECL.match(lines[j])
                    if d:
                        addr = int(m.group(1), 16)
                        idx.setdefault(addr, []).append((d.group(1), f"{rel(path)}:{j + 1}"))
                        break
            d = METHOD_DECL.match(line)
            if d:
                methods.setdefault((rel(path), d.group(1)), i + 1)
    return idx, methods


REG = re.compile(r"Register\(ScriptHelper\.(\w+),\s*(\d+),\s*([\w\.]+)\);(.*)")


def build_registrations():
    """(slot, code) -> (func, line, comment)."""
    path = os.path.join(ROOT, "Gameplay", "Scripts", "SpriteEventHandlers.cs")
    out = {}
    with open(path, encoding="utf-8-sig") as fh:
        for n, line in enumerate(fh, 1):
            m = REG.search(line)
            if m:
                out[(SLOTS[m.group(1)], int(m.group(2)))] = (m.group(3), n, m.group(4).strip())
    return out


SPAWN = re.compile(r"\b(SpawnWarpEntity|SpawnChildEntity)\s*\(")


def split_args(s):
    """Split a call's argument text at top-level commas."""
    args, depth, cur = [], 0, ""
    for ch in s:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            if depth == 0:
                args.append(cur.strip())
                return args
            depth -= 1
        if ch == "," and depth == 0:
            args.append(cur.strip())
            cur = ""
        else:
            cur += ch
    args.append(cur.strip())
    return args


def build_spawn_sites():
    """List of dict(file, line, func, kind, isMap, typeExpr, type) for every spawn-by-type call."""
    sites = []
    for path in cs_files():
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            text = fh.read()
        lines = text.split("\n")
        # enclosing method per line
        encl = [None] * len(lines)
        cur = None
        for i, line in enumerate(lines):
            d = METHOD_DECL.match(line)
            if d and not line.rstrip().endswith(";"):
                cur = d.group(1)
            encl[i] = cur
        for m in SPAWN.finditer(text):
            line_no = text.count("\n", 0, m.start())
            # skip the declarations
            if re.match(r"\s*public\s+Entity", lines[line_no]):
                continue
            tail = text[m.end():m.end() + 600]
            args = split_args(tail)
            kind = m.group(1)
            if kind == "SpawnWarpEntity":
                a_map, a_type = (args[1], args[2]) if len(args) > 2 else ("?", "?")
            else:
                a_map, a_type = (args[2], args[3]) if len(args) > 3 else ("?", "?")
            sites.append({"file": rel(path), "line": line_no + 1, "func": encl[line_no], "kind": kind, "mapArg": a_map, "typeArg": a_type})
    return sites


GUARD = re.compile(r'\.Name\s*(?:!=|==)\s*"([^"]+)"')


def build_guards():
    """method name -> (relpath:line of first guard, ordered list of sprite names found in `entity.Name != "X"` guards of that method).
    These guards are the decompilation author's own assertions of which sprite types reach a handler (they call Breakpoint otherwise)."""
    out = {}
    for path in cs_files():
        with open(path, encoding="utf-8-sig", errors="replace") as fh:
            lines = fh.read().split("\n")
        cur = None
        for i, line in enumerate(lines):
            d = METHOD_DECL.match(line)
            if d and not line.rstrip().endswith(";"):
                cur = d.group(1)
            for m in GUARD.finditer(line):
                ent = out.setdefault((rel(path), cur), {"line": i + 1, "names": []})
                if m.group(1) not in ent["names"]:
                    ent["names"].append(m.group(1))
    return out


def eval_const(expr):
    """Evaluate a literal int expression like 0xD1, 1, (uint)0xda; None if not a literal."""
    e = expr.strip()
    e = re.sub(r"\(\s*(?:uint|int)\s*\)", "", e).strip()
    if re.fullmatch(r"-?(?:0x[0-9a-fA-F]+|\d+)", e):
        return int(e, 0)
    return None


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    idx, methods = build_addr_index()
    print("addr comments:", len(idx))
    regs = build_registrations()
    print("registrations:", len(regs))
    sites = build_spawn_sites()
    print("spawn sites:", len(sites))
    lit = [s for s in sites if eval_const(s["typeArg"]) is not None]
    print("literal-type sites:", len(lit))
