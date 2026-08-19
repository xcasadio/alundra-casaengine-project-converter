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
}
