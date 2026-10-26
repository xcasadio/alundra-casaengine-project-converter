#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Physics;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g1a (docs/plan-e20-heros.md section 1.24.1, D-G-9, D-G-12): the program C23 (<c>0x8006B848</c>, slot C, code 23 of the master table <c>0x80098F4C</c>) of the crates, the jars and the other liftable objects of the seven sprite types of the
/// export (<see cref="SpriteTypes"/>). It only decides WHEN the object leaves the hands; its flight is the generic motion of the entities:
/// <list type="bullet">
/// <item><description>nothing while the object is not carried (<c>+0x28</c>, <see cref="EntityScriptProxy.PlatformEntity"/>, null);</description></item>
/// <item><description>carried without a code of throw (<c>+0x24</c>, <see cref="EntityScriptProxy.Flags2"/>, 0): the animation 0, the links kept;</description></item>
/// <item><description>carried with a code of throw (1, 2, 3 or -1 through the table <c>0x80027400</c>, <see cref="ReleaseAnimation"/>): the animation of the flight, the code back to 0, the two links cleared (the carrier's <c>+0x2C</c> and its own
/// <c>+0x28</c>), the riding relation cleared (D-G-12), and the flags <see cref="ReleasedFlags"/>.</description></item>
/// </list>
/// The code -1 is written by mode 2 of <c>UpdatePlayerCarriedEntity</c> (a wound, deep water, a death), the codes 1 to 3 by the throw of the hero (g2a). E2, the destruction on arrival, is g2b's (<see cref="Break"/>, <see cref="RegisterBreak"/>). Both are registered by <see cref="NativeSpriteRegistry.CreateForProduction"/> (C23 since
/// phase C of g1a, inert while nothing is carried; E2 since phase B of g2b, inert for an object at rest, which never raises the slot E); the tests of the world register them themselves only when the key is absent.
/// </summary>
internal static class LiftableAi
{
    /// <summary>The program code of C23 in slot C.</summary>
    internal const int Code = 23;

    /// <summary>
    /// The sprite types whose header holds C23 in the export (D-G-8): the map-page sprites 35, 36, 38, 41, 48 and 182 (<c>0x100 +</c> the index: 291, 292, 294, 297, 304 and 438) and the sprite 234 of the global page (the very heavy stone). All seven also hold E2
    /// at the slot E. Guarded against the export by the tests.
    /// </summary>
    internal static readonly IReadOnlyList<int> SpriteTypes = new[] { 234, 291, 292, 294, 297, 304, 438 };

    /// <summary>The keys the class registers, for the pins of the production registry: C23, for the seven types.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, Code) };

    /// <summary>The bits the release raises (<c>| 0x30</c>) and clears (<c>&amp; ~0x80</c>) in the flags of the object.</summary>
    private const uint ReleaseSetBits = 0x30;

    private const uint ReleaseClearBits = 0x80;

    /// <summary>The bits E2 raises (<c>| 0x40</c>) and clears (<c>&amp; ~0x30</c>) in the flags of the object it does not destroy.</summary>
    private const uint BreakSetBits = 0x40;

    private const uint BreakClearBits = 0x30;

    /// <summary>The program code of E2 in slot E (<c>0x8007ED30</c>): the break of the crates, the jars and the other objects thrown, called once the object left the hands and met something.</summary>
    internal const int BreakCode = 2;

    /// <summary>
    /// The sprite types whose header holds E2 in the export (docs/plan-e20-annexe/e20-g1a/report.md, "Types a enregistrer"): the seven types of C23 (<see cref="SpriteTypes"/>) plus the barrel (index 34, type 290), the stone (index 37, type 293) and the trunk
    /// (index 117, type 373) of the family C4. Guarded against the export by the tests.
    /// </summary>
    internal static readonly IReadOnlyList<int> BreakSpriteTypes = new[] { 234, 290, 291, 292, 293, 294, 297, 304, 373, 438 };

    /// <summary>The keys <see cref="RegisterBreak"/> registers, for the pins of the production registry: E2, for the ten types (kept apart from <see cref="Keys"/>, the C23 of g1a).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> BreakKeys = new[] { (ScriptHelper.ProgramEDeactivate, BreakCode) };

    /// <summary>Registers E2 for the ten types of <see cref="BreakSpriteTypes"/> only; the disposable removes the key (for the tests that register it themselves when the registry of the runner does not hold it).</summary>
    internal static IDisposable RegisterBreak(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, BreakCode, BreakSpriteTypes, (entity, context, state) => Break(entity));

    /// <summary>
    /// One tick of E2 (<c>0x8007ED30</c>, E20.g2b) for <paramref name="entity"/>, run at every tick of the state Deactivated (slot E): the break of the object that met something. Once the animation 2 (the break) asked for is at its end
    /// (<c>+0xAC</c>, <see cref="EntityScriptProxy.ForceResetAnimationFlag"/>, is 1) or the four corners of the entity stand on the ground property 4 (<c>+0x184 &amp; 4</c>, <see cref="EntityScriptProxy.CombinedVramFlagsAND"/>), the object is destroyed with the break effect of its
    /// header: <c>DestroyEntity(e, -1)</c> (<c>0x8003A59C</c>: the loot, the status 4, the break effect, the carrier). Otherwise it asks for the animation 2 (<c>+0x88</c>, <see cref="EntityScriptProxy.TargetAnimationId"/>: the binary reads the animation ASKED, not the
    /// one validated) and the flags become <c>(flags | 0x40) &amp; ~0x30</c> (the crate <c>0x332374</c> becomes <c>0x332344</c>: no longer a subject of the deactivation on impact or on a hit, the animation end stays). The destruction is the one of the world the entity belongs to
    /// (<see cref="EntityScriptProxy.ScriptHost"/>, as the in-line E0/E1 of the runner reach it); none for an entity without a host.
    /// </summary>
    internal static void Break(EntityScriptProxy entity)
    {
        if ((entity.TargetAnimationId == 2 && entity.ForceResetAnimationFlag == 1) || (entity.CombinedVramFlagsAND & 4) != 0)
        {
            entity.ScriptHost?.DestroyEntity(entity, -1);
            return;
        }

        entity.TargetAnimationId = 2;
        entity.Flags = (entity.Flags | BreakSetBits) & ~BreakClearBits;
    }

    /// <summary>Registers C23 for the seven types of <see cref="SpriteTypes"/> only.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, Code, SpriteTypes, (entity, context, state) => Tick(entity, context));

    /// <summary>
    /// The table <c>0x80027400</c> read by the code of throw: 1 gives the animation 3, 2 the animation 1, 3 the animation 4, -1 the animation 3. Any other code (the binary reads the words around the table) is written by nothing: null, and
    /// <see cref="Tick"/> does not release.
    /// </summary>
    internal static uint? ReleaseAnimation(int throwKind) => throwKind switch
    {
        1 => 3u,
        2 => 1u,
        3 => 4u,
        -1 => 3u,
        _ => null,
    };

    /// <summary>The flags of the object once let go: <c>(flags | 0x30) &amp; ~0x80</c> (the crate <c>0x3323E4</c> becomes <c>0x332374</c>).</summary>
    internal static uint ReleasedFlags(uint flags) => (flags | ReleaseSetBits) & ~ReleaseClearBits;

    /// <summary>One tick of C23 for <paramref name="entity"/> (see the class).</summary>
    internal static void Tick(EntityScriptProxy entity, IEntityWorldContext context)
    {
        if (entity.PlatformEntity == null)
        {
            return;
        }

        TickCarried(entity, ReleaseFlagsPolicy.C23);
    }

    /// <summary>
    /// E20.g2c1 (docs/plan-e20-heros.md section 1.24.5, D-G2C1-8): the flags policy of the shared release. C23 always writes <c>(flags | 0x30) &amp; ~0x80</c>; C4 leaves the flags for the codes 1 to 3 and, for the code -1, writes <c>(flags | 0x10) &amp; ~0x80</c> when its native E code
    /// (<c>+0x80</c>, <see cref="EntityScriptProxy.SpriteProgramIndexes"/> at <see cref="ScriptHelper.ProgramEDeactivate"/>) is 2: a thrown C4 object stays collidable.
    /// </summary>
    internal enum ReleaseFlagsPolicy
    {
        C23,
        C4,

        /// <summary>E20.g2c2 (D-G2C2-1): the torch's C70, the twin of C23 with <c>(flags | 0x34) &amp; ~0x80</c>.</summary>
        C70,
    }

    /// <summary>The bits C70 raises (<c>| 0x34</c>: C23's <c>0x30</c> and the bit 4) in the flags of the torch it lets go; it clears <see cref="ReleaseClearBits"/> as C23 does.</summary>
    private const uint TorchReleaseSetBits = 0x34;

    /// <summary>The bit C4 raises (<c>| 0x10</c>) when it releases the code -1 of an object whose native E code is <see cref="C4DeathFlagsNativeCode"/>; it clears <see cref="ReleaseClearBits"/> as C23 does.</summary>
    private const uint C4DeathSetBits = 0x10;

    /// <summary>The native E code (<c>+0x80</c>) of the barrel, the stone and the trunk (E2): C4 writes the flags for the code -1 only for them, not for the objects of the plates (E0).</summary>
    private const int C4DeathFlagsNativeCode = 2;

    /// <summary>The type of the trunk (sprite 117, 0x175): the only C4 object that floats.</summary>
    private const int TrunkType = 373;

    /// <summary>The animation of the floating trunk, and the ground slope (<see cref="EntityScriptProxy.Slope_18c"/>) of the water that gives it.</summary>
    private const uint TrunkFloatAnimation = 5;

    private const int TrunkWaterSlope = 4;

    /// <summary>The sound of the landing or the touch (0x18), the effect of the bounce (9), its lift above the object (8 px in raw units) and the half turn of the direction (0x10 of 0x20).</summary>
    private const int LandingSfx = 0x18;

    private const int BounceEffect = 9;

    private const int BounceEffectLift = 8 << 16;

    private const uint BounceDirectionStep = 0x10;

    /// <summary>The type of the ice blocks (the sprite 48 of the map page): C23 in the header, and C4 once the script of the map (<c>0x41 [C, 4]</c>) changes it.</summary>
    private const int IceBlockType = 304;

    /// <summary>
    /// The eleven global-page types of the shop articles (docs/plan-e20-heros.md section 1.24.5, D-G2C1-9, Q-SHOP): the script of the shop maps (134, 163, 258, 298, 398) changes their native C to 4 (<c>0x41 [2, 4]</c>), so that the article lifted from the shelf and thrown on the
    /// counter is released. Their header has no C4: the key is registered for them, the entity reads its live code.
    /// </summary>
    internal static readonly IReadOnlyList<int> ShopArticleTypes = new[] { 48, 50, 66, 67, 68, 69, 71, 81, 83, 85, 113 };

    /// <summary>
    /// D-G2C1-1: the trunk is the only type whose ground slope (<see cref="EntityScriptProxy.Slope_18c"/>, the water under its four corners) is fed even when the map placed it (without the marker of the entities born during play): C4 reads it to float the trunk, and the eight trunks the
    /// maps 70 and 73 place on the water float from the first run of C4, as the original's.
    /// </summary>
    internal static bool FeedsGroundSlope(int spriteType) => spriteType == TrunkType;

    /// <summary>Registers C4 for the types of <see cref="C4ProductionTypes"/>, as <see cref="NativeSpriteRegistry.CreateForProduction"/> does.</summary>
    internal static IDisposable RegisterC4Production(NativeSpriteRegistry registry) => RegisterC4(registry, C4ProductionTypes);

    /// <summary>The program code of C4 in slot C (<c>0x80066984</c>): the barrel, the stone, the trunk and the four objects of the plates.</summary>
    internal const int C4Code = 4;

    /// <summary>The sprite types whose header holds C4 in the export (docs/plan-e20-annexe/e20-g2c/disc/report.md, section 1): 290 barrel, 293 stone, 363 to 369 the objects, 373 the trunk.</summary>
    internal static readonly IReadOnlyList<int> C4SpriteTypes = new[] { 290, 293, 363, 367, 368, 369, 373 };

    /// <summary>The 19 types the key <c>(C, 4)</c> is registered for in production: the seven of <see cref="C4SpriteTypes"/>, the ice block and the eleven shop articles (D-G2C1-9).</summary>
    internal static readonly IReadOnlyList<int> C4ProductionTypes = C4SpriteTypes.Concat(new[] { IceBlockType }).Concat(ShopArticleTypes).ToArray();

    /// <summary>The keys <see cref="RegisterC4"/> registers, for the pins of the production registry.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> C4Keys = new[] { (ScriptHelper.ProgramCTick, C4Code) };

    /// <summary>Registers C4 for the types given.</summary>
    internal static IDisposable RegisterC4(NativeSpriteRegistry registry, IEnumerable<int> types)
        => registry.Register(ScriptHelper.ProgramCTick, C4Code, types, (entity, context, state) => C4Tick(entity, context));

    /// <summary>The carried half of C23 and C4 (the same release table), for an entity that is carried: the animation, the links, the code of the throw, the flags by <paramref name="policy"/>.</summary>
    internal static void TickCarried(EntityScriptProxy entity, ReleaseFlagsPolicy policy)
    {
        if (entity.Flags2 == 0)
        {
            entity.TargetAnimationId = 0;
            return;
        }

        if (ReleaseAnimation(entity.Flags2) is not { } animation)
        {
            return;
        }

        var throwKind = entity.Flags2;
        entity.TargetAnimationId = animation;
        entity.Flags2 = 0;

        // the carrier's link (the binary writes 0 there without looking at it); a carrier without a proxy has none to clear.
        if (entity.PlatformEntity?.GameplayProxy is EntityScriptProxy carrier)
        {
            carrier.CarriedEntity = null;
        }

        entity.PlatformEntity = null;
        entity.RidingEntity = null;
        entity.Flags = policy switch
        {
            ReleaseFlagsPolicy.C23 => ReleasedFlags(entity.Flags),
            ReleaseFlagsPolicy.C70 => (entity.Flags | TorchReleaseSetBits) & ~ReleaseClearBits,
            ReleaseFlagsPolicy.C4 => throwKind == -1 && entity.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] == C4DeathFlagsNativeCode ? (entity.Flags | C4DeathSetBits) & ~ReleaseClearBits : entity.Flags,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "no case for this release policy"),
        };
    }

    /// <summary>One tick of C4 (see the class); <paramref name="readCorners"/> and <paramref name="createEffect"/> are the seams of the tests.</summary>
    internal static void C4Tick(EntityScriptProxy entity, IEntityWorldContext context, Func<EntityScriptProxy, uint[]>? readCorners = null, Action<int, int, int>? createEffect = null)
    {
        if (entity.PlatformEntity != null)
        {
            TickCarried(entity, ReleaseFlagsPolicy.C4);
            return;
        }

        readCorners ??= CornerTileWords.Read;
        var animation = entity.TargetAnimationId;
        if (animation != 0 && !(entity.SpriteType == TrunkType && animation == TrunkFloatAnimation))
        {
            if (entity.ForceAdjusted != 0)
            {
                if (entity.IsOnGround == 0)
                {
                    // blocked in the air: the first time it goes up, the object bounces (the lock, the direction half a turn, the effect 9 8 px above).
                    if (entity.ForceZ > 0 && entity.DelayOrAngleOrEntityId == 0)
                    {
                        entity.DelayOrAngleOrEntityId = 1;
                        entity.TargetDirection = (entity.TargetDirection + BounceDirectionStep) & 0x1F;
                        var z = entity.PosZ + BounceEffectLift;
                        if (createEffect != null)
                        {
                            createEffect(entity.PosX, entity.PosY, z);
                        }
                        else
                        {
                            context.EffectStage?.Reservoir.Create(false, BounceEffect, 0, entity.PosX, entity.PosY, z);
                        }
                    }
                }
                else
                {
                    // blocked on the ground: a corner on a band deactivates it (the status 3), then the animation 0.
                    if (CornerTileWords.AnyHasMask(readCorners(entity)))
                    {
                        entity.Status = EntityStatus.Deactivated;
                    }

                    entity.TargetAnimationId = 0;
                }
            }
            else if (entity.IsOnGround != 0 || entity.HitCounter != 0)
            {
                // on the ground or touching: the sound, the lock cleared, the animation 0 unless a corner is on a band (it glides).
                context.SoundPlayer?.PlaySfx(LandingSfx);
                entity.DelayOrAngleOrEntityId = 0;
                if (!CornerTileWords.AnyHasMask(readCorners(entity)))
                {
                    entity.TargetAnimationId = 0;
                }
            }
        }

        if (entity.SpriteType == TrunkType)
        {
            // the trunk floats (the animation 5) while the slope is 4 (the water), and stops floating when it leaves it.
            if (entity.TargetAnimationId == 0 && entity.Slope_18c == TrunkWaterSlope)
            {
                entity.TargetAnimationId = TrunkFloatAnimation;
            }
            else if (entity.TargetAnimationId == TrunkFloatAnimation && entity.Slope_18c != TrunkWaterSlope)
            {
                entity.TargetAnimationId = 0;
            }
        }
    }
}
