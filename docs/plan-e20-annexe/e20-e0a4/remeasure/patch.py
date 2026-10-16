#!/usr/bin/env python3
"""Throwaway measure patches of E20.e0a4 T0 (never committed), at the EXACT placement of the slice.
Usage: patch.py PA | PAB | PANOAF | PABEARLY
 PA       phase A only: SwitchedAtCreation + AnimFlags in SpawnEntityByRecordId (after ScriptHost, before InitializeBalance)
 PAB      PA + the marker JoinsBirthTick posed LAST by the three opcodes (D-E0A4-3)
 PANOAF   PA without the AnimFlags load
 PABEARLY PA + the marker posed BEFORE the push of 0x8A / 0x8B (0x2D: right after the spawn)
Exact byte replacements, each anchor present exactly once, EOL preserved."""
import sys
from pathlib import Path

REPO = Path("D:/development/repo/alundra-casaengine-project-converter")
WORLD = REPO / "Alundra/Scripts/AlundraWorldProxy.cs"
RUNNER = REPO / "Alundra/Scripts/AlundraEventProgramRunner.cs"


def edit(path, old, new):
    raw = path.read_bytes()
    eol = "\r\n" if b"\r\n" in raw else "\n"
    o = old.replace("\n", eol).encode("utf-8")
    n = new.replace("\n", eol).encode("utf-8")
    c = raw.count(o)
    if c != 1:
        sys.exit(f"anchor found {c} times in {path.name} (expected 1):\n{old}")
    path.write_bytes(raw.replace(o, n))


A_OLD = "                parentEntity: logicEntity.LogicContextEntity, tileMapData: _tileMapData, collisionField: CollisionField);\n            var spawnedProxy = entity.GameplayProxy as AlundraEntityScriptProxy;\n            if (spawnedProxy != null)\n            {\n                spawnedProxy.ScriptHost = this;\n                spawnedProxy.InitializeBalance(BalanceTables, BalanceThreshold); // E20.c1a C1.\n"
SW = "                spawnedProxy.SwitchedAtCreation = true; // MEASURE (throwaway)\n"
AF = ("                spawnedProxy.AnimFlags = spawnedProxy.AnimSetsByAnim != null && spawnedProxy.AnimSetsByAnim.TryGetValue((int)spawnedProxy.TargetAnimationId, out var firstAnimationSet)"
      " ? firstAnimationSet.Acceleration : 0; // MEASURE (throwaway)\n")


def phase_a(with_af):
    edit(WORLD, A_OLD, A_OLD.replace("                spawnedProxy.InitializeBalance", SW + (AF if with_af else "") + "                spawnedProxy.InitializeBalance"))


def late_marker():
    edit(RUNNER,
         "        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[1]);\n\n        if (spawned == null && _loggedFailedActivations.Add(v[1]))\n        {\n            Logs.WriteDebug(\n                $\"AlundraEventProgramRunner: opcode 0x2D ActivateEntity({v[1]}) - spawn failed (record \"\n                + \"disabled/missing, or the spawn path threw) - the original breakpoints here instead.\");\n        }\n",
         "        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[1]);\n\n        if (spawned == null && _loggedFailedActivations.Add(v[1]))\n        {\n            Logs.WriteDebug(\n                $\"AlundraEventProgramRunner: opcode 0x2D ActivateEntity({v[1]}) - spawn failed (record \"\n                + \"disabled/missing, or the spawn path threw) - the original breakpoints here instead.\");\n        }\n\n        if (spawned != null) { spawned.JoinsBirthTick = true; } // MEASURE (throwaway)\n")
    edit(RUNNER,
         "        spawned.PosZ = (v[6] + v[7] * 0x100) << 16;\n        spawned.PushLogicalPositionToRoot();\n    }\n",
         "        spawned.PosZ = (v[6] + v[7] * 0x100) << 16;\n        spawned.PushLogicalPositionToRoot();\n        spawned.JoinsBirthTick = true; // MEASURE (throwaway)\n    }\n")
    edit(RUNNER,
         "            spawned.PushLogicalPositionToRoot();\n        }\n    }\n\n    /// <summary>Script_46_02E",
         "            spawned.PushLogicalPositionToRoot();\n        }\n\n        if (spawned != null) { spawned.JoinsBirthTick = true; } // MEASURE (throwaway)\n    }\n\n    /// <summary>Script_46_02E")


def early_marker():
    edit(RUNNER,
         "        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[1]);\n\n        if (spawned == null && _loggedFailedActivations.Add(v[1]))\n        {\n            Logs.WriteDebug(\n                $\"AlundraEventProgramRunner: opcode 0x2D ActivateEntity({v[1]}) - spawn failed (record \"\n                + \"disabled/missing, or the spawn path threw) - the original breakpoints here instead.\");\n        }\n",
         "        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[1]);\n\n        if (spawned == null && _loggedFailedActivations.Add(v[1]))\n        {\n            Logs.WriteDebug(\n                $\"AlundraEventProgramRunner: opcode 0x2D ActivateEntity({v[1]}) - spawn failed (record \"\n                + \"disabled/missing, or the spawn path threw) - the original breakpoints here instead.\");\n        }\n\n        if (spawned != null) { spawned.JoinsBirthTick = true; } // MEASURE (throwaway)\n")
    edit(RUNNER,
         "        spawned.PosX = (v[2] + v[3] * 0x100) << 16;\n        spawned.PosY = (v[4] + v[5] * 0x100) << 16;\n",
         "        spawned.JoinsBirthTick = true; // MEASURE (throwaway, before the push)\n        spawned.PosX = (v[2] + v[3] * 0x100) << 16;\n        spawned.PosY = (v[4] + v[5] * 0x100) << 16;\n")
    edit(RUNNER,
         "        if (spawned != null && matches.Count != 0)\n        {\n            spawned.PosX = matches[0].PosX",
         "        if (spawned != null) { spawned.JoinsBirthTick = true; } // MEASURE (throwaway, before the push)\n\n        if (spawned != null && matches.Count != 0)\n        {\n            spawned.PosX = matches[0].PosX")


v = sys.argv[1]
if v == "PA":
    phase_a(True)
elif v == "PAB":
    phase_a(True); late_marker()
elif v == "PANOAF":
    phase_a(False)
elif v == "PABEARLY":
    phase_a(True); early_marker()
else:
    sys.exit("unknown variant")
print("applied", v)
