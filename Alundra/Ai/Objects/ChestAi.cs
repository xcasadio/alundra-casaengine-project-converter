#nullable enable
using Alundra.Entities;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.e0d (docs/plan-e20-heros.md section 1.26.2): the treasure chest (global sprite type 30, flags <c>0x8180</c>: Collidable, Gravity, <c>InteractRequiresButton</c>). The binary gives it the programs A255 (<c>0x80061BD4</c>, shared with the 87 pickup
/// objects, registered in <see cref="PickupAi.Register"/>) and F255 (<c>0x8007FC88</c>, the chest alone). Phase A: <see cref="Load"/>, the branch of A255 for the chest.
/// </summary>
internal static class ChestAi
{
    /// <summary>The global sprite type of the chest.</summary>
    internal const int SpriteType = 30;

    /// <summary>The animation of the chest open (<c>+0x88 == 1</c>, the end of the chain of the opening animation 2).</summary>
    internal const uint OpenAnimation = 1;

    /// <summary>
    /// A255 on the chest (<c>0x80061BD4</c>, the branch of the type <c>0x1E</c>): when the contents (<c>+0x3C</c>) are zero - the record holds none, or the flag of the chest is raised and the roll of the creation (<c>InitializeContents</c>,
    /// <c>0x80032A40</c>) gave it none - the chest is drawn open (<c>+0x88 = 1</c>); a chest that holds something is left as it is (closed). Nothing else is written.
    /// </summary>
    internal static void Load(EntityScriptProxy chest)
    {
        if (chest.ContentsItemId == 0)
        {
            chest.TargetAnimationId = OpenAnimation;
        }
    }
}
