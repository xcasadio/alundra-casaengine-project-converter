"""Critic: read the hero's 14-byte animation records straight from DATAS.BIN (common bank at file offset 0, sprite info
at +SpriteRecordsOffset), check the header bytes, list byte 0xD of the 94 records and compare with the extractor JSON.
Read-only. Usage: python -B -I heroanim_raw.py"""
import json, struct

R = r"D:/development/repo/alundra-casaengine-project-converter"
B = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN"
j = json.load(open(R + "/data-extracted/data/map_alundra.json", encoding="utf-8-sig"))
si = j["SpriteInfo"]
base_mem = si["Header"]["MemoryAddress"]
file_si = j["Offset"] + j["Header"]["SpriteRecordsOffset"]
rec = si["SpriteRecords"][0]
h = rec["Header"]
with open(B, "rb") as f:
    f.seek(file_si)
    blob = f.read(si["Header"]["SpriteTableSize"] + 200000)
hdr_off = h["MemoryAddress"] - base_mem
hb = blob[hdr_off:hdr_off + 32]
print("hero header raw:", hb.hex())
print("header bytes 16..: MoreFlags=%#x CanPickup=%#x Shadow=%#x" % (hb[16], hb[17], hb[18]),
      "expect", hex(h["MoreFlags"]), hex(h["CanPickup"]), hex(h["FlagsPortraitShadowType"]))
anim0 = rec["AnimSets"][0]["MemoryAddress"] - base_mem
n = len(rec["AnimSets"])
bad = 0
with80 = []
for k in range(n):
    r14 = blob[anim0 + 14 * k: anim0 + 14 * k + 14]
    a = rec["AnimSets"][k]
    if a["MemoryAddress"] - base_mem != anim0 + 14 * k:
        print("address mismatch", k)
    b13 = r14[13]
    if b13 != a["Acceleration"]:
        bad += 1
    if b13 & 0x80:
        with80.append((k, hex(b13), r14.hex()))
print("records", n, "byte13 != JSON Acceleration:", bad)
print("records with byte 0xD & 0x80:", with80)
