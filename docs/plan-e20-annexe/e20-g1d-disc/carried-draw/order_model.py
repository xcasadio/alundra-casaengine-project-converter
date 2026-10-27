"""Read-only MODEL (no engine, no DLL run): the order of a carried object against its carrier, in the binary and in the port at HEAD.
Everything here is arithmetic on rules read in code; every rule carries its source in the comment. Labelled [MODELE] in the report.

BINARY (ALUN_CD.EXE, France), sort driver 0x800399B8 and recursion 0x800397AC (re-disassembled: dis_sort_driver.txt):
  s2(e)  = PosY(16.16) + (IDSV << 16)                              0x800397EC-0x80039804
  if collidable(e.Flags & 0x80) and not (e.AnimFlags & 0x80) and e.+0x28 (carrier) != 0:
        k = carrier.+0x1BC (after its own F) ; if s2 < k: key = k ; return                0x8003981C-0x80039858
  key(e) = (key & 0xFFFF0000) + (short)(PosZ >> 16)                                        L3 0x80039A94-0x80039AA4
  draw: list sorted DESCENDING by key then inserted at the head of OT slot 16*min(key>>20, 59)+6, so ascending key = drawn first
        (docs/plan-e19-g3c-annexe/discovery/notes.md 3.1, docs/plan-e20-annexe/e20-f1/check.md). Larger key = in front.
PORT at HEAD 539d4cf4:
  Elevation = 16*min((PosY + (IDSV<<16)) >> 20, 59) + 6, IDSV = frame 0 of (anim, dir) (WallPlacementOverlay.ComputeEntityElevation,
              FrameSyncPasses.RunWallInterleaveSortKeyPass / ResolveIdsv), carried+collidable: value raised to the carrier key (EntityDepthKey.RaiseToCarrier)
  SortCoordinate = round(-renderY*100), renderY = ceil(-(Y - Z))  (SnapToPixel true, TopDownElevation policy, DepthSortable2DComponent TopDownYUp, anchor 0,0)
                 = 100 * floor(Y_px - Z_px) with Z_px an integer
  RenderSortKey2D.CompareTo: (pass, layer, order, Elevation, SortCoordinate, LocalSortOffset(+part DrawOrder), StableId) ascending; ascending = drawn first.
Hero: PosZ = 0 on the flat ground, the carried object is drawn up at Z = hero Z + 32 px (CarryRules.HeadHeight) once the lift has risen (32 ticks).
"""
import json, math, sys
from collections import defaultdict

P = r"D:/development/repo/alundra-casaengine-project-converter/alundra-project/Data/sprite-records.json"
d = json.load(open(P, encoding="utf-8"))
rec = {r["BankKey"]: r for r in d.values()}


def flags(r):
    return r["MoreFlags"] | (r["CanPickup"] << 8) | (r["FlagsPortraitShadowType"] << 16)


def idsv_frames(bank, anim, direction):
    for e in rec[bank]["IdsvAnimDirs"]:
        if e["Anim"] == anim and e["Direction"] == direction:
            return e["Frames"]
    return None


def key_binary(y_px, idsv, z_px, carrier_l2=None, collidable=True):
    """The 32-bit key of the binary for an entity at integer-pixel y (fraction removed by the mask anyway), z integer. carrier_l2 = carrier s2 before L3."""
    s2 = (y_px << 16) + (idsv << 16)
    if collidable and carrier_l2 is not None and s2 < carrier_l2:
        s2 = carrier_l2
    return (s2 & 0xFFFF0000) + z_px  # (short)(PosZ>>16); z small here


def l2_value(y_px, idsv):
    return (y_px << 16) + (idsv << 16)


def elevation_port(y_px, idsv):
    row = (y_px + idsv) >> 4
    row = min(row, 0x3B)
    return row * 16 + 6


def coord_port(y_px, z_px):
    return 100 * math.floor(y_px - z_px)


def port_sort_tuple(y_px, idsv, z_px, carrier=None, collidable=True):
    """(elevation, coordinate) of the port. carrier = (y_px, idsv_carrier, z_carrier) -> RaiseToCarrier on the bucket."""
    value = (y_px << 16) + (idsv << 16)
    if collidable and carrier is not None:
        cy, cidsv, cz = carrier
        ckey = ((cy << 16) + (cidsv << 16)) & 0xFFFF0000
        ckey += cz
        if value < ckey:
            value = ckey
    row = min(value >> 20, 0x3B)
    return (row * 16 + 6, coord_port(y_px, z_px))


def study(bank_obj, hero_states, obj_anim=0, label=""):
    r = rec[bank_obj]
    collidable = bool(flags(r) & 0x80)
    out = []
    for (anim, direction) in hero_states:
        hero_frames = idsv_frames("alundra_0", anim, direction)
        obj_frames = idsv_frames(bank_obj, obj_anim, direction)
        if obj_frames is None:
            obj_frames = idsv_frames(bank_obj, obj_anim, 0)
        if hero_frames is None or obj_frames is None:
            continue
        h0 = hero_frames[0]
        o0 = obj_frames[0]
        # binary: every displayed frame of the hero pair with the object's current frame (frame 0 of its loop as the representative)
        in_front_binary = []
        for hf in hero_frames:
            for yfrac in range(16):  # whole-pixel row phase 0..15 (the mask removes the fraction; bucket depends on y_px mod 16 only through the carry)
                y = 200 + yfrac
                hk_l2 = l2_value(y, hf)
                hk = key_binary(y, hf, 0)
                ok = key_binary(y, o0, 32, carrier_l2=hk_l2, collidable=collidable)
                in_front_binary.append(ok > hk)
        # port: DLL frame 0 of the hero pair, object frame 0
        in_front_port = []
        for yfrac in range(16):
            y = 200 + yfrac
            he = (elevation_port(y, h0), coord_port(y, 0))
            oe = port_sort_tuple(y, o0, 32, carrier=(y, h0, 0), collidable=collidable)
            # ascending = drawn first; the object is in front iff its tuple is greater (LocalSortOffset/StableId only matter on a tie, never here: coordinate differs by 3200)
            in_front_port.append(oe > he)
        out.append((anim, direction, hero_frames, h0, o0, sum(in_front_binary) / len(in_front_binary), sum(in_front_port) / len(in_front_port)))
    return collidable, out


if __name__ == "__main__":
    states = []
    for anim in (5, 6, 7, 11, 12, 46, 47, 48):
        for direction in range(4):
            states.append((anim, direction))
    lines = []
    print("MODEL. share of whole-pixel Y phases (16) where the carried object (Z = hero + 32 px) is IN FRONT of the hero. binary = every displayed frame of the hero pair; port = HEAD (frame 0 IDSV, bucket + 100*floor(Y-Z)).")
    for bank in ("alundra_2", "alundra_1", "137", "34", "alundra_234", "41"):
        collidable, rows = study(bank, states)
        print(f"\n== object {bank}  collidable={collidable}  objectIDSV(anim0,frame0)={[idsv_frames(bank,0,dd)[0] if idsv_frames(bank,0,dd) else None for dd in range(4)]}")
        print("   anim dir heroFrames(IDSV)            heroIDSV0 objIDSV0  binary_in_front  port_in_front")
        for anim, direction, hf, h0, o0, b, p in rows:
            print(f"   {anim:4d} {direction:3d} {str(hf):28s} {h0:6d} {o0:8d}   {b:7.3f}          {p:7.3f}")
