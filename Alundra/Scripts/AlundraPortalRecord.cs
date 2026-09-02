#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// T3 (docs/plan-transitions-carte.md §1.1, §3 T3): one portal record parsed from the "Portals"
/// object-layer of a map's exported .tileMap (see <see cref="AlundraWorldProxy.BuildPortals"/>) - the 9
/// raw fields plus <see cref="Index"/> the original's own <c>Portal</c> struct carries
/// (alundra-datas-analyser/AlundraTools/AlundraEngine/DatasBin/Portal.cs:7-15), ALL exported as STRING
/// custom properties (§1.1.b, <c>TiledMapExporter.cs:411-420</c>) - never the object's own
/// x/y/width/height rectangle, which is a RENDER rectangle offset in elevation
/// (<c>y = (Y1 - cellHeight) * 16</c>) and would name the wrong tiles.
///
/// A plain mutable class (not a record/struct): built once per world load by
/// <see cref="AlundraWorldProxy.BuildPortals"/> and never mutated afterward - mutability here is just to
/// match the object-initializer style <see cref="AlundraMapEvent"/> already uses, not a design need.
/// </summary>
public sealed class AlundraPortalRecord
{
    public int Index;
    public int X1, Y1, X2, Y2;
    public int DestMapId;
    public int DestTileX, DestTileY;
    public int ZLevel;
    public int Flags;

    /// <summary>
    /// Bits 14-15 of <see cref="Flags"/> (<c>Portal.cs:32</c>) - direction (domain 0..3) the player must
    /// face, and hold on the pad, for the warp to trigger. Index into
    /// <see cref="AlundraPortalTrigger"/>'s own <c>RequiredInputByFacing</c> table (the port of
    /// <c>StaticVariables.SHORT_ARRAY_80022776</c>, address 0x80022776, <c>StaticVariables.cs:530</c>).
    /// </summary>
    // Masked to the two bits the original's own ushort Flags can carry there (Portal.cs:32 shifts a
    // ushort, so the result is 0..3 by construction). This port stores Flags as an int parsed from a
    // string custom property, so the mask is what keeps this an in-range index into the four-entry
    // required-input and cardinal-direction tables no matter what an export puts in the field.
    public uint RequiredFacingDirection => (uint)((Flags >> 14) & 0x3);

    /// <summary>
    /// Bits 12-13 of <see cref="Flags"/> (<c>Portal.cs:38</c>) - direction (domain 0..3) the player
    /// faces on arrival. Index into <see cref="AnimationTables.CardinalDirectionTable"/> (§1.2.c) -
    /// NEVER used as the direction value itself (index 1 gives 0x10, not 1).
    /// </summary>
    public int ArrivalDirectionIndex => (Flags & 0x3000) >> 12;

    /// <summary>
    /// Bits 4-6 of <see cref="Flags"/> (<c>Portal.cs:41</c>) - map transition effect id (fade style).
    /// NOT consumed by T3 (§1.2.c/T4-T5's own scope) - kept so this record already carries every field
    /// of the original's <c>Portal</c>, avoiding a second pass through the raw data later.
    /// </summary>
    public int TransitionEffectId => (Flags & 0x70) >> 4;

    /// <summary>
    /// Bits 0-3 of <see cref="Flags"/> (<c>Portal.cs:44</c>) - index into the original's own
    /// <c>g_warpBehaviorTable</c> (warp sound). NOT consumed by T3 - see
    /// <see cref="TransitionEffectId"/>'s own note.
    /// </summary>
    public int WarpBehaviorId => Flags & 0xF;
}
