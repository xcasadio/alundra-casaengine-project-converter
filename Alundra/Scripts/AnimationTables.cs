namespace Alundra.Scripts;

/// <summary>
/// Static lookup tables ported verbatim from
/// alundra-datas-analyser/AlundraTools/AlundraEngine/StaticVariables.cs, used to resolve an entity's
/// spawn-time facing (<see cref="AlundraWorldProxy"/>'s spawn init, porting
/// <c>GameEngine.SpawnEntity</c>/<c>EntityManager.InitializeEntity</c>) and its per-frame animation
/// direction (<see cref="AlundraWorldProxy"/>'s animation sync pass, porting the target-resolution part
/// of <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4).
/// </summary>
internal static class AnimationTables
{
    /// <summary>
    /// StaticVariables.g_cardinalDirectionTable (StaticVariables.cs:527): maps a record's 2-bit
    /// <c>SpriteDirection</c> facing index (0..3) to the packed direction value
    /// <see cref="AlundraEntityScriptProxy.TargetDirection"/>/<see cref="AlundraEntityScriptProxy.CurrentDirection"/>
    /// actually store - <c>GameEngine.SpawnEntity</c> (GameEngine.cs:741,753) indexes it with
    /// <c>entityRecord.SpriteDirection &amp; 0x3</c>.
    /// </summary>
    public static readonly uint[] CardinalDirectionTable = { 0, 0x10, 0x08, 0x18 };

    /// <summary>
    /// StaticVariables.g_animationDirectionTable (StaticVariables.cs:145-151, address comment 0x800237F4):
    /// a 4x8 table indexed <c>[AnimationDirection * 8 + col]</c>
    /// (<c>col = ((TargetDirection + 2) &amp; 0x1c) &gt;&gt; 2</c>, 0..7) by
    /// <c>EntityManager.UpdateAnimation</c> @ 0x80038AB4 to resolve the next
    /// <see cref="AlundraEntityScriptProxy.AnimationDirection"/> (0=down, 1=up, 2=left, 3=right - see
    /// <c>LoaderSelectionScreen.cs:244</c>) from the entity's current facing and its target direction.
    /// </summary>
    public static readonly int[] AnimationDirectionTable =
    {
        0, 0, 2, 1, 1, 1, 3, 0,
        0, 0, 2, 1, 1, 1, 3, 0,
        0, 2, 2, 2, 1, 3, 3, 3,
        0, 2, 2, 2, 1, 3, 3, 3
    };

    /// <summary>
    /// Direction-name suffix order used by the converter for animation asset names
    /// (<c>AlundraCasaEngineProjectConverter.Writers.SpriteWriter.DirectionNames</c>: "…_anim{N}_{dir}"),
    /// indexed by <see cref="AlundraEntityScriptProxy.AnimationDirection"/> - the same 0=down/1=up/2=left/
    /// 3=right order as <see cref="AnimationDirectionTable"/>'s output.
    /// </summary>
    public static readonly string[] DirectionNames = { "down", "up", "left", "right" };

    /// <summary>
    /// StaticVariables.g_directionByButtons (StaticVariables.cs:99-103, address comment 0x80022c6c): maps
    /// the 4-bit directional pad mask (<c>ButtonsHold &gt;&gt; 0xc</c>, bit 0=Up, 1=Right, 2=Down, 3=Left -
    /// see <see cref="AlundraPadState"/>'s own doc) to the packed direction value
    /// <see cref="AlundraEntityScriptProxy.TargetDirection"/> stores. <c>0xFFFFFFFF</c> marks an invalid
    /// combination (e.g. Up+Down held together) - <see cref="AlundraPlayerManager.MovePlayer"/> falls back
    /// to the entity's current <c>TargetDirection</c> in that case, exactly like
    /// <c>PlayerManager.cs:199-205</c>.
    /// </summary>
    public static readonly uint[] DirectionByButtons =
    {
        0xFFFFFFFF, 0x10, 0x18, 0x14, 0x0, 0xFFFFFFFF, 0x1C, 0xFFFFFFFF, 0x8, 0xC, 0xFFFFFFFF, 0xFFFFFFFF, 0x4,
        0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF
    };

    /// <summary>
    /// StaticVariables.g_offsetXList (StaticVariables.cs:476-483, address comment 0x80023654): 32 signed
    /// 16-bit per-direction X speed multipliers (16.16-fixed-point-free, pre-multiplied by
    /// <c>AnimationSet.Speed</c> - see <see cref="AlundraPlayerManager"/>'s own kinematic tick), indexed by
    /// the packed direction value (<see cref="DirectionByButtons"/>'s own output domain).
    /// </summary>
    public static readonly short[] OffsetXList =
    {
        0x0, unchecked((short)0xff6a), unchecked((short)0xfeda), unchecked((short)0xfe5a), unchecked((short)0xfde1),
        unchecked((short)0xfd81), unchecked((short)0xfd3a), unchecked((short)0xfd0f), unchecked((short)0xfd00),
        unchecked((short)0xfd0f), unchecked((short)0xfd3a), unchecked((short)0xfd81), unchecked((short)0xfde1),
        unchecked((short)0xfe5a), unchecked((short)0xfeda), unchecked((short)0xff6a),
        0x0, 0x96, 0x126, 0x1a6, 0x21f, 0x27f, 0x2c6, 0x2f1, 0x300, 0x2f1, 0x2c6, 0x27f, 0x21f, 0x1a6, 0x126, 0x96
    };

    /// <summary>StaticVariables.g_offsetYList (StaticVariables.cs:486-494, address comment 0x80023694) -
    /// see <see cref="OffsetXList"/>'s own doc, same shape for Y.</summary>
    public static readonly short[] OffsetYList =
    {
        0x200, 0x1f6, 0x1d9, 0x1aa, 0x16a, 0x11c, 0xc4, 0x64, 0x0, unchecked((short)0xff9c), unchecked((short)0xff3c),
        unchecked((short)0xfee4), unchecked((short)0xfe96), unchecked((short)0xfe56), unchecked((short)0xfe27),
        unchecked((short)0xfe0a),
        unchecked((short)0xfe00), unchecked((short)0xfe0a), unchecked((short)0xfe27), unchecked((short)0xfe56),
        unchecked((short)0xfe96), unchecked((short)0xfee4), unchecked((short)0xff3c), unchecked((short)0xff9c), 0x0,
        0x64, 0xc4, 0x11c, 0x16a, 0x1aa, 0x1d9, 0x1f6
    };

    /// <summary>
    /// StaticVariables.g_heights_800236d4 (StaticVariables.cs:531-541, address comment 0x800236d4): 24
    /// per-pixel-column height offsets (in map-tile-height units, i.e. already the value added to
    /// <c>(tile.Height - 1) * StaticVariables.MapTileHeight</c>) used by the ladder-entering/exiting
    /// slope branches of <c>PhysicsEngine.ComputeEntityGroundHeight</c> @ 0x800370c4 - see
    /// <see cref="AlundraCellsCollisionField"/>.
    /// </summary>
    public static readonly byte[] HeightsTable_800236d4 =
    {
        0x1, 0x2, 0x2, 0x3,
        0x4, 0x4, 0x5, 0x6,
        0x6, 0x7, 0x8, 0x8,
        0x9, 0xA, 0xA, 0xB,
        0xC, 0xC, 0xD, 0xE,
        0xE, 0xF, 0x10, 0x10
    };
}
