#nullable enable
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Alundra.Maps.Loop;
using Alundra.Combat;

namespace Alundra.Entities;

/// <summary>
/// E20.c1a (docs/plan-e20-heros.md section 1.5): the state the hit pass of the binary (<see cref="HitPass"/>, <c>UpdateBalanceRecords</c> <c>0x80039300</c>) reads on each entity, and what keeps it in step with
/// the animation clock of the tick: the balance record (<c>+0x1C4</c>), the current attack (<c>+0x1C8</c>), the animation byte of the clock (<c>+0xB4</c>) and the box of the frame shown at the tick (<c>+0x1D4</c>, whose
/// values are <see cref="CollisionOffsetX"/> to <see cref="CollisionHeight"/> and whose <c>HitBox</c> is <see cref="HitBoxX"/> to <see cref="HitBoxZ"/>).
/// <para>
/// <b>Why not <see cref="AnimFlags"/> and <see cref="CurrentAnimationId"/>.</b> The DLL writes them at the end of the frame only (<see cref="FrameSyncPasses.SyncAnimation"/>, D-E19-13, kept), while the binary changes them
/// in the tick that sees the switch (<c>0x80038B18-0x80038BAC</c>) and the hit pass of that very tick already reads them. So the clock (<see cref="FrameSyncPasses.StepAnimationClock"/>) tells this class every switch
/// it sees (<see cref="NoteSwitchPendingAtTick"/>, <see cref="NoteOwedSwitchTick"/>, <see cref="NoteChainEndSwitch"/>, and the spawn, <see cref="InitializeBalance"/>), and it reloads the attack and the byte then, and
/// keeps its own <b>cursor</b>: the animation and direction shown, and the number of ticks since the switch. The frame box is read from that cursor (<see cref="FrameBoxTimeline"/>), never from the sprite, which
/// still holds the old animation in the rest of a catch-up frame.
/// </para>
/// </summary>
public partial class EntityScriptProxy
{
    /// <summary>The balance record of the entity (<c>+0x1C4</c>): chosen at the creation by <see cref="InitializeBalance"/>; null in the degraded cases (no table, a spawn whose sprite header did not resolve).</summary>
    public BalanceRecord? BalanceRecord; //1c4

    /// <summary>The attack the entity deals while its current animation plays (<c>+0x1C8</c>): reloaded at every switch the clock sees; null when its record has none.</summary>
    public BalanceAttack? CurrentAttack; //1c8

    /// <summary>The binary's <c>+0xB4</c>: byte <c>0xD</c> of the animation set, loaded at every switch the clock sees (the bit <c>0x40</c> makes the entity untouchable). <see cref="AnimFlags"/> is the same byte, one frame late.</summary>
    public int HitAnimByte; //b4

    /// <summary>True when the frame shown now has a box (<c>+0x1D4 != 0</c>); refreshed by <see cref="EvaluateFrameBox"/> (the hit pass asks only for the entities that can attack).</summary>
    public bool HasFrameBox; //1d4

    /// <summary>The animation of the cursor (the target of the last switch the clock saw, or the spawn animation).</summary>
    internal uint BoxAnimationId;

    /// <summary>The direction row of the cursor's animation.</summary>
    internal int BoxDirection;

    /// <summary>The ticks since the switch of the cursor's animation: 0 at the tick of the switch (its frame 0 is shown, uncounted), then one more per tick the clock advances.</summary>
    internal int BoxTicks;

    /// <summary>True from the tick the clock loaded a switch until <see cref="FrameSyncPasses.SyncAnimation"/> validates it at the end of the frame: the sprite still holds the old animation, so the cursor counts the ticks itself.</summary>
    internal bool BoxSwitchLoaded;

    private FrameBoxTimeline? _boxTimeline;
    private bool _boxTimelineResolved;
    private bool _boxHostAsked;

    /// <summary>The entity this proxy stands for in the fields of the binary that hold an entity (<c>TouchingEntity</c>, <c>+0x224</c>): the entity of the world, or the one that carries the proxy.</summary>
    internal Entity? SelfEntity => LogicContextEntity ?? Owner;

    /// <summary>
    /// <c>InitializeEntity</c> (<c>0x80039D04</c>), balance part (<c>0x80039E30-0x80039E4C</c>, <c>0x80039EA8</c>): the record of the sprite (<see cref="SpriteType"/>, 0 for the hero) at the level <paramref name="threshold"/> of the
    /// map, <see cref="Hp"/> and <see cref="HpMax"/> from its maximum life, and the first switch the creation makes (the attack, the animation byte and the box of the animation 0). Does nothing without tables, or when the
    /// sprite header did not resolve at the spawn (<see cref="SpriteType"/> is -1): the binary has no such case.
    /// </summary>
    internal void InitializeBalance(BalanceTables? tables, int threshold)
    {
        if (tables == null || SpriteType < 0)
        {
            return;
        }

        var record = tables.Select(SpriteType, threshold);
        if (record == null)
        {
            return;
        }

        BalanceRecord = record;
        Hp = record.MaxHp;
        HpMax = record.MaxHp;

        var changed = FrameSyncPasses.TryResolveAnimationTarget(this, out _, out var direction);
        LoadAnimationSwitch(TargetAnimationId, changed ? direction : AnimationDirection, fromClock: false);
        StoreHitBox(); // E20.c1b K3: the creation (0x80039F30) stores the HitBox of the first frame too.
    }

    /// <summary>
    /// E20.c1b K3 (docs/plan-e20-heros.md section 1.6): the stored HitBox (<c>+0x1FC/+0x200/+0x204</c>) of the helper <c>0x80038064</c>: <c>Pos + CollisionOffset</c> (<c>0x80038094-0x800380B0</c>, not <c>ModdedPos</c>), written
    /// ONLY when the frame shown has a box (<c>+0x1D4 != 0</c>), after the movement of every tick (the end of <see cref="TickPhysics"/>, <c>0x800384BC</c>) and at the creation (<c>0x80039F30</c>), for every entity and
    /// not only for the attackers of the hit pass. The opcode <c>0x95</c> reads it (docs/plan-e20-annexe/e20-c1b/check.md: the STORED box, not a recomputed one). The hit pass of E20.c1a still computes the
    /// same value for its own attackers at its own point of the tick; nothing moves between the two, so the results are unchanged.
    /// </summary>
    internal void StoreHitBox()
    {
        if (!EvaluateFrameBox())
        {
            return;
        }

        unchecked
        {
            HitBoxX = PosX + CollisionOffsetX;
            HitBoxY = PosY + CollisionOffsetY;
            HitBoxZ = PosZ + CollisionOffsetZ;
        }
    }

    /// <summary>
    /// The switch block of <c>UpdateAnimation</c> (<c>0x80038B18-0x80038BAC</c>): the animation byte <c>+0xB4</c> from the animation set, the current attack from the record, and the box cursor at the frame 0 of the new
    /// animation. <paramref name="fromClock"/>: a tick of the clock saw it (see <see cref="BoxSwitchLoaded"/>).
    /// </summary>
    internal void LoadAnimationSwitch(uint animationId, int direction, bool fromClock)
    {
        HitAnimByte = AnimSetsByAnim != null && AnimSetsByAnim.TryGetValue((int)animationId, out var set) ? set.Acceleration : 0;
        CurrentAttack = BalanceRecord?.AttackForAnimation(animationId);
        BoxAnimationId = animationId;
        BoxDirection = direction;
        BoxTicks = 0;
        BoxSwitchLoaded = fromClock;
        _boxTimeline = null;
        _boxTimelineResolved = false;
        _boxHostAsked = false;
        HasFrameBox = false;
    }

    /// <summary>
    /// The clock found a switch pending at this tick (a new target, a new direction, or a chain restart). The first tick that sees it loads it (the binary switches at once); the later ticks of the same catch-up frame
    /// (<see cref="FrameSyncPasses.SyncAnimation"/> has not validated it yet, so it still looks pending) are ticks of the new animation: the cursor only counts them.
    /// </summary>
    internal void NoteSwitchPendingAtTick()
    {
        var changed = FrameSyncPasses.TryResolveAnimationTarget(this, out _, out var direction);
        if (!changed)
        {
            direction = AnimationDirection; // a restart onto the same animation (a self-chain): the row does not change.
        }

        if (BoxSwitchLoaded && BoxAnimationId == TargetAnimationId && BoxDirection == direction)
        {
            BoxTicks++;
            return;
        }

        LoadAnimationSwitch(TargetAnimationId, direction, fromClock: true);
    }

    /// <summary>
    /// E20.e0a5 (D-E0A5-2): the switch of the creation is still the current one. True while the entity is flagged <see cref="SwitchedAtCreation"/>, the appearance flag is still up (<see cref="SpawnAnimationActive"/>, so no end of frame validated anything and no
    /// <c>RepeatAnimation</c> relaunched it), the target is the animation of the creation, no restart of a chain is asked, the direction the target derives (a switch is pending, <see cref="FrameSyncPasses.TryResolveAnimationTarget"/>) is the one of the
    /// cursor, and the cursor was not reloaded by another switch (<see cref="BoxSwitchLoaded"/>). Once any other switch is seen it is false for good in the window: a target that comes back to the animation of the creation is an ordinary switch.
    /// </summary>
    internal bool IsCreationSwitchCurrent()
        => SwitchedAtCreation
        && SpawnAnimationActive
        && !BoxSwitchLoaded
        && PendingChainRestartFlag == 0
        && TargetAnimationId == SpawnAnimationId
        && FrameSyncPasses.TryResolveAnimationTarget(this, out _, out var direction)
        && direction == BoxDirection;

    /// <summary>E20.e0a5 (D-E0A5-1): the clock saw the entity while the switch of the creation is current: a COUNTED tick of the binary (the first call of the animation pass counts), the cursor follows (1 at the first tick, 2 at the next: <c>+0x1C</c>).</summary>
    internal void NoteCreationTick() => BoxTicks++;

    /// <summary>The clock consumed the tick owed to a switch the end of the frame validated without a tick to spend on it: this tick is the switch tick of the binary (the frame 0, uncounted).</summary>
    internal void NoteOwedSwitchTick() => LoadAnimationSwitch(TargetAnimationId, AnimationDirection, fromClock: false);

    /// <summary>The clock advanced the animation by one tick: the cursor follows.</summary>
    internal void NoteAnimationAdvanced() => BoxTicks++;

    /// <summary>The tick's advance ended a Chain animation: the binary jumps straight into the switch block (<c>0x80038D54-0x80038D68</c>) at this very tick, with the chained animation in <see cref="TargetAnimationId"/>.</summary>
    internal void NoteChainEndSwitch()
    {
        var changed = FrameSyncPasses.TryResolveAnimationTarget(this, out _, out var direction);
        LoadAnimationSwitch(TargetAnimationId, changed ? direction : AnimationDirection, fromClock: true);
    }

    /// <summary>
    /// The end of the frame validated nothing (the switch the clock loaded was cancelled by a script before the validation): the sprite never left its animation, so the cursor goes back to what the sprite shows.
    /// </summary>
    internal void NoteSwitchCancelled()
    {
        if (!BoxSwitchLoaded)
        {
            return;
        }

        LoadAnimationSwitch(CurrentAnimationId, AnimationDirection, fromClock: false);
        BoxTicks = LogicalClockSprite?.LogicalTick ?? 0;
    }

    /// <summary>Test-only: the logical tick of the sprite's clock that the box cursor stands for (<see cref="BoxTicks"/> wrapped or saturated by the duration of its animation); null without a timeline.</summary>
    internal int? CursorLogicalTickForTests()
    {
        EvaluateFrameBox();
        return _boxTimeline?.LogicalTick(BoxTicks);
    }

    /// <summary>
    /// The frame box shown at this tick (<c>0x80038C2C-0x80038CB8</c>): offsets <c>&lt;&lt; 16</c> into <see cref="CollisionOffsetX"/> to <see cref="CollisionOffsetZ"/>, sizes <c>(n &lt;&lt; 16) - 1</c> into
    /// <see cref="CollisionWidth"/>, <see cref="CollisionDepth"/> and <see cref="CollisionHeight"/> (an attacker's size 0 stays -1, <c>0x80038C80</c>). Sets and returns <see cref="HasFrameBox"/>.
    /// </summary>
    internal bool EvaluateFrameBox()
    {
        if (!_boxTimelineResolved)
        {
            var sprite = LogicalClockSprite ?? Owner?.GetComponent<AnimatedSpriteComponent>();
            if (SwitchedAtCreation && sprite != null && sprite.Animations.Count == 0)
            {
                // E20.e0a5 (D-E0A5-3): an entity born by the interface reads the box of its frame in the tick of its birth, before the engine built the animations of its sprite: the data of the animation come from the host, by the asset ids
                // of the sprite (the sprite is not touched). Asked once per switch: when the host has none, the timeline stays unresolved (never frozen at null) and the sprite answers once the engine integrated it.
                if (!_boxHostAsked)
                {
                    _boxHostAsked = true;
                    var data = ScriptHost?.ResolveAnimationData(sprite.AnimationAssetIds, BoxAnimationId, BoxDirection);
                    if (data != null)
                    {
                        _boxTimeline = FrameBoxTimeline.ForData(data);
                        _boxTimelineResolved = true;
                    }
                }
            }
            else
            {
                _boxTimelineResolved = true;
                _boxTimeline = sprite != null && FrameSyncPasses.TrySelectAnimationByNameSuffix(sprite, BoxAnimationId, BoxDirection, out var selected) && selected != null
                    ? FrameBoxTimeline.For(selected)
                    : null;
            }
        }

        if (_boxTimeline != null && _boxTimeline.TryGetBox(BoxTicks, out var box))
        {
            unchecked
            {
                CollisionOffsetX = box.OffsetX << 16;
                CollisionOffsetY = box.OffsetY << 16;
                CollisionOffsetZ = box.OffsetZ << 16;
                CollisionWidth = (box.Width << 16) - 1;
                CollisionDepth = (box.Depth << 16) - 1;
                CollisionHeight = (box.Height << 16) - 1;
            }

            HasFrameBox = true;
            return true;
        }

        HasFrameBox = false;
        return false;
    }
}
