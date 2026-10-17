"""Critic: list the hero (common bank, sprite type 0) animation sets and their byte 0xD (read-only, data-extracted).
Explores the JSON structure first. Usage: python -B -I heroanim.py"""
import json

R = r"D:/development/repo/alundra-casaengine-project-converter"
j = json.load(open(R + "/data-extracted/data/map_alundra.json", encoding="utf-8-sig"))
si = j["SpriteInfo"]
print("SpriteInfo type", type(si).__name__, (list(si.keys())[:30] if isinstance(si, dict) else len(si)))


def walk(o, path, depth):
    if depth > 4:
        return
    if isinstance(o, dict):
        for k, v in list(o.items())[:40]:
            if isinstance(v, (dict, list)):
                print("  " * depth + f"{path}.{k}: {type(v).__name__} len={len(v)}")
                walk(v, path + "." + k, depth + 1)
            else:
                print("  " * depth + f"{path}.{k} = {str(v)[:60]}")
    elif isinstance(o, list) and o:
        walk(o[0], path + "[0]", depth + 1)


walk(si, "SpriteInfo", 0)
