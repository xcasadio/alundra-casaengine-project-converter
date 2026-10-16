"""Shared loaders for the enemies2 roster scripts. Everything is read-only on the repository.
Sources (all read by absolute path):
  data-extracted/data/map_N.json         (via cache/maps.pkl, written by extract_maps.py)
  alundra-project/Data/sprite-records.json, balance.json
  alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv      (sprite type names, jp/fr/en)
  alundra-casaengine-project-converter/maps.json                         (map zone and name per map id)
  Alundra/Scripts/*.cs                                                   (DLL registry, inline handlers, save book, opcode sizes)
"""
import csv
import json
import os
import pickle
import re

REPO = r"D:\development\repo\alundra-casaengine-project-converter"
HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.join(REPO, "Alundra", "Scripts")
SLOT_LETTER = "ABCDEF"
SLOT_INDEX = {c: i for i, c in enumerate(SLOT_LETTER)}


def load_cache():
    with open(os.path.join(HERE, "cache", "maps.pkl"), "rb") as fh:
        return pickle.load(fh)


def load_entity_names():
    """type id -> (fr, jp, en); rows 0..255 = global page, 256..511 = map page (EntityNameCatalogReader.cs)."""
    path = os.path.join(REPO, "alundra-datas-analyser", "AlundraTools", "AlundraTools", "EntityNames.csv")
    rows = []
    with open(path, encoding="utf-8-sig", newline="") as fh:
        lines = fh.read().split("\n")
    for ln in lines[1:]:
        if not ln.strip():
            continue
        cols = ln.rstrip("\r").split(";")
        rows.append((cols[1].strip() if len(cols) > 1 else "", cols[0].strip(), cols[2].strip() if len(cols) > 2 else ""))
    return rows


def load_maps_catalog():
    """map id -> (zone, name) from the hand-curated converter catalog maps.json."""
    path = os.path.join(REPO, "alundra-casaengine-project-converter", "maps.json")
    with open(path, encoding="utf-8") as fh:
        doc = json.load(fh)
    out = {}
    for zone_entry in doc:
        for zone, items in zone_entry.items():
            for it in items:
                out.setdefault(it["id"], (zone, it["name"]))
    return out


def load_balance():
    with open(os.path.join(REPO, "alundra-project", "Data", "balance.json"), encoding="utf-8") as fh:
        return json.load(fh)["EntriesBySpriteIndex"]


def balance_chain(balance, type_id):
    """The chain of levels of a sprite type as AlundraBalanceTables.Select walks it (index 0x1F..0x7F reads entry 0x1E)."""
    idx = 0x1E if 0x1F <= type_id <= 0x7F else type_id
    if idx >= len(balance) or balance[idx] is None:
        return []
    chain = []
    rec = balance[idx]
    while rec:
        pw = max([a["Power"] for a in (rec["Attacks"] or [])] or [0])
        chain.append({"level": rec["BalanceLevel"], "hp": rec["MaxHp"], "maxPower": pw, "nAttacks": rec["AttackCount"],
                      "resp": rec["DamageResponses"]})
        rec = rec["NextLevel"]
    return chain


def select_level(chain, threshold):
    """First record with level >= threshold (Select: the chain advances while record.Level < threshold)."""
    for r in chain:
        if r["level"] >= threshold:
            return r
    return chain[-1] if chain else None


def opcode_sizes():
    """opcode -> (size, name) parsed from the DLL's EventOpcodeSizeTable.cs."""
    path = os.path.join(SCRIPTS, "EventOpcodeSizeTable.cs")
    pat = re.compile(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"((?:[^"\\]|\\.)*)"\)\s*\}')
    out = {}
    with open(path, encoding="utf-8-sig") as fh:
        for ln in fh:
            m = pat.search(ln)
            if m:
                out[int(m.group(1), 16)] = (int(m.group(2)), m.group(3))
    return out


def read_text(path):
    with open(path, encoding="utf-8-sig") as fh:
        return fh.read()


def parse_dll_implementation():
    """What the DLL implements natively today (production), parsed from the sources.
    Returns dict: registry: {(slot, code): set(types)}, inline_e: set(codes), savebook: {(slot, code): type}, sources: dict."""
    slotmap = {"ProgramALoad": 0, "ProgramBMap": 1, "ProgramCTick": 2, "ProgramDTouch": 3, "ProgramEDeactivate": 4, "ProgramFInteract": 5}
    registry = {}
    sources = {}

    def parse_ai(fname):
        txt = read_text(os.path.join(SCRIPTS, fname))
        consts = {m.group(1): int(m.group(2)) for m in re.finditer(r"const int (\w+)\s*=\s*(\d+);", txt)}
        arrays = {}
        for m in re.finditer(r"int\[\]\s+(\w+)\s*=\s*\{([^}]*)\}", txt):
            arrays[m.group(1)] = [int(x) for x in m.group(2).replace(" ", "").split(",") if x]
        vt = re.search(r"var types = new\[\]\s*\{\s*(\w+)\s*\}", txt)
        arrays["types"] = [consts[vt.group(1)]] if vt else []
        for m in re.finditer(r"registry\.Register\(ScriptHelper\.(\w+),\s*(\w+),\s*(\w+),", txt):
            slot = slotmap[m.group(1)]
            code_arg = m.group(2)
            code = int(code_arg) if code_arg.isdigit() else consts[code_arg]
            types = arrays[m.group(3)]
            line = txt.count("\n", 0, m.start()) + 1
            registry.setdefault((slot, code), set()).update(types)
            sources[(slot, code)] = f"Alundra/Scripts/{fname}:{line}"
        # the wave-sword Register is an expression body on the next line
        for m in re.finditer(r"registry\.Register\(ScriptHelper\.(\w+),\s*(\w+),\s*(\w+),", txt):
            pass
        return consts

    for f in ("AlundraBeannoideAi.cs", "AlundraMurutaAi.cs", "AlundraWaveSwordAi.cs"):
        parse_ai(f)

    # inline E codes: AlundraEventProgramRunner.IsInlineDeactivateCode
    runner = read_text(os.path.join(SCRIPTS, "AlundraEventProgramRunner.cs"))
    m = re.search(r"IsInlineDeactivateCode\(int code\) => \(uint\)code <= 1u \|\| code == AlundraWaveSwordAi\.DeactivateCode;", runner)
    assert m, "IsInlineDeactivateCode changed: re-read it"
    wave = read_text(os.path.join(SCRIPTS, "AlundraWaveSwordAi.cs"))
    dcode = int(re.search(r"const int DeactivateCode = (\d+);", wave).group(1))
    inline_e = {0, 1, dcode}
    inline_line = runner.count("\n", 0, m.start()) + 1

    # save book
    book = read_text(os.path.join(SCRIPTS, "AlundraSaveBook.cs"))
    sb_type = int(re.search(r"const int SpriteType = (\d+);", book).group(1))
    sb_f = int(re.search(r"const int InteractCode = (\d+);", book).group(1))
    sb_c = int(re.search(r"const int TickCode = (\d+);", book).group(1))
    savebook = {(5, sb_f): sb_type, (2, sb_c): sb_type}
    return {"registry": registry, "inline_e": inline_e, "inline_line": inline_line, "savebook": savebook, "sources": sources}


def impl_status(impl, slot, code, type_id):
    """'registry' | 'inline' | 'savebook' | 'none' for one (slot, code) of a sprite type."""
    if type_id in impl["registry"].get((slot, code), ()):
        return "registry"
    if slot == 4 and code in impl["inline_e"]:
        return "inline"
    if impl["savebook"].get((slot, code)) == type_id:
        return "savebook"
    return "none"
