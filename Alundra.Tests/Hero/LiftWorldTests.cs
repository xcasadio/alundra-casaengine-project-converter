#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1b T2 (docs/plan-e20-heros.md section 1.24.4): the world tests of the lift (m1), (m4), (m5), (m6), (m7) and (m8), on the REAL hero controller (<see cref="CarryRig"/>: the real <c>MovePlayer</c>, the tick by tick physics pass, the
/// movement obstacle probe of g3, the idle of the real sheet at the acceleration byte 1) and a crate of the original, BEFORE the activation (T3): the production set <see cref="PlayerManager.ProductionWiredLiftableTypes"/> is still empty and the stub
/// <c>PlayerTryAction</c> still answers 0, so every test that expects a lift is RED until T3; the witnesses (the sword where the binary swings it) are green before and after.
/// <para>The expected values are the binary's, never the DLL's: the scenarios and the pad scripts of <c>docs/plan-e20-annexe/e20-g1a/critic-scenarios/c3_contact.py</c> (the lines A to G; the per-tick contact in <c>out/c3-contact-ticks.txt</c>), the scripts
/// of <c>docs/plan-e20-annexe/e20-g1a/work/orc/out/g15-misc.txt</c> (the lift in the air), <c>g9-lift-rules.txt</c> (the object on the head) and <c>docs/plan-e20-annexe/e20-g/oracle/c4_climb_lift.py</c> (the climb). The crate stands at x = 130, the hero pushes from
/// x = 100 towards the east; the animation after the tick of the Square is 5 for a lift on the ground, 11 in the air or in the climb, 16 for the sword on the ground, 20 for the sword in the air, 12 for the object that stood on the head.</para>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftWorldTests : IDisposable
{
    private const uint R = PadState.Right;
    private const uint SQ = PadState.Square;
    private const uint UP = PadState.Up;
    private const uint CROSS = PadState.Cross;

    /// <summary>A sprite type of the production set (the seven types C23: 234, 291, 292, 294, 297, 304, 438), the Pierre generique of the original's crate family (291).</summary>
    private const int WiredType = 291;

    /// <summary>
    /// A sprite type outside the production set (the torch, 393, whose C is (C, 70) and which g2c2 wires later): genre 1 like a crate, but outside the set: the sword, never a lift. E20.g2c1 phase C wired the barrel (290) with the other
    /// types of C4, so the witness was derived on the torch, a type that stays outside.
    /// </summary>
    private const int UnwiredType = 393;

    private const uint LiftOnTheGround = 5;
    private const uint LiftInTheAir = 11;
    private const uint SwordOnTheGround = 16;
    private const uint SwordInTheAir = 20;
    private const uint CarryStandOnTheGround = 12;

    public LiftWorldTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        PlayerManager.WiredLiftableTypesForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The pad word of each tick (1-based) from a list of (ticks, hold), like <c>gl.pad_script</c> of the oracle; 0 past the end.</summary>
    private static Func<int, uint> Script(params (int N, uint Hold)[] seq)
    {
        var d = new Dictionary<int, uint>();
        var t = 1;
        foreach (var (n, h) in seq)
        {
            for (var i = 0; i < n; i++)
            {
                d[t++] = h;
            }
        }

        return tick => d.TryGetValue(tick, out var v) ? v : 0u;
    }

    /// <summary>The kit of the new game (the basic sword equipped): the sword is what a press of the Square gives when nothing is lifted.</summary>
    private static void EquipTheKit(CarryHost host)
    {
        var tables = ItemTables.GetOrCreate(ProjectRootFinder.Find());
        host.ItemTables = tables;
        PlayerManager.InitializeNewGameInventory(host.GameState, tables);
    }

    /// <summary>The scene of the oracle: the hero at (100, 100) facing east, a crate of the original at (130, 100) (21 x 15 x 16, the Kinetic ghost of the prefab), the sword in the hand.</summary>
    private static CarryRig Scene(int ticksPerFrame, Func<int, uint> hold, int crateType = WiredType)
    {
        var rig = CarryRig.Build(ticksPerFrame, hold, probeFactory: host => new MovementObstacleProbe(host), heroX: 100f, heroY: 100f);
        EquipTheKit(rig.Host);
        var crate = rig.AddCrate(130, 100, 0, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, prefabBody: true);
        crate.SpriteType = crateType;
        return rig;
    }

    private static bool Carries(CarryRig rig) => rig.Hero.CarriedEntity != null && ReferenceEquals(rig.Hero.CarriedEntity, rig.Crate!.LogicContextEntity);

    // ---- (m1) the chronology of the ground: the lines A to G of c3 ----------------------------------------------------------------------------------------

    /// <summary>A line of the oracle: the pad script, the tick of the Square, whether it lifts (and the tick of the lift: the tick of the Square).</summary>
    public sealed record Line(string Name, (int N, uint Hold)[] Seq, int SquareTick, bool Lifts)
    {
        public override string ToString() => Name;
    }

    public static readonly Line[] Lines =
    {
        new("A release and Square at t10", new[] { (9, R), (1, SQ), (10, 0u) }, 10, true),
        new("B release at t10, Square at t11 (the half step of the release keeps the contact one tick)", new[] { (9, R), (1, 0u), (1, SQ), (10, 0u) }, 11, true),
        new("C Square at t10 still holding RIGHT", new[] { (9, R), (1, R | SQ), (10, R) }, 10, true),
        new("D pause t10-14, RIGHT and Square at t15 (the first tick of the new push)", new[] { (9, R), (5, 0u), (1, R | SQ), (10, R) }, 15, false),
        new("D2 the same with the Square at t16", new[] { (9, R), (5, 0u), (1, R), (1, R | SQ), (10, R) }, 16, true),
        new("E Square at t5 (the tick the hero reaches the crate)", new[] { (4, R), (1, R | SQ), (10, R) }, 5, false),
        new("E2 Square at t6", new[] { (5, R), (1, R | SQ), (10, R) }, 6, true),
        new("F Square held from t1 with RIGHT", new[] { (20, R | SQ) }, 0, false),
        new("G UP held (not pushing the crate), Square at t10", new[] { (9, UP), (1, UP | SQ), (10, 0u) }, 10, false),
    };

    public static TheoryData<int, int> LineAndCadence()
    {
        var data = new TheoryData<int, int>();
        for (var line = 0; line < Lines.Length; line++)
        {
            for (var tpf = 1; tpf <= 3; tpf++)
            {
                data.Add(line, tpf);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LineAndCadence))]
    public void M1_TheGroundChronology_TheSquareLiftsOnlyWithTheContactOfThePreviousTick(int lineIndex, int tpf)
    {
        var line = Lines[lineIndex];
        var rig = Scene(tpf, Script(line.Seq));
        var liftedAt = 0;
        uint animAtSquare = 0;
        rig.Run(20, tick =>
        {
            if (liftedAt == 0 && Carries(rig))
            {
                liftedAt = tick;
            }

            if (tick == line.SquareTick)
            {
                animAtSquare = rig.Hero.TargetAnimationId;
            }
        });

        if (line.Lifts)
        {
            Assert.Equal(line.SquareTick, liftedAt);
            Assert.Equal(LiftOnTheGround, animAtSquare);
            Assert.True(Carries(rig), "the crate stays in the hands");
            Assert.Same(rig.Hero.LogicContextEntity, rig.Crate!.PlatformEntity);
        }
        else
        {
            Assert.Equal(0, liftedAt);
            Assert.False(Carries(rig));
            if (line.SquareTick != 0)
            {
                Assert.Equal(SwordOnTheGround, animAtSquare);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void M1_TheHeroReachesTheCrateAtTheFifthTick_AndTheReleaseKeepsTheContactOneTickMore(int tpf)
    {
        // c3-contact-ticks.txt line B: x = 109.0 and the crate in contact from t5; at the release (t10) the half step of the walk (the force 2.4375 -> 1.2188) is refused again: the contact is still the crate at the end of t10, nil at t11.
        var rig = Scene(tpf, Script((9, R), (1, 0u), (1, 0u), (10, 0u)));
        var contact = new Dictionary<int, bool>();
        var x = new Dictionary<int, double>();
        rig.Run(12, tick =>
        {
            contact[tick] = rig.Hero.XCollisionEntity != null;
            x[tick] = rig.Hero.PosX / 65536.0;
        });

        for (var tick = 1; tick <= 4; tick++)
        {
            Assert.False(contact[tick], $"t{tick}: the crate is not touched yet");
        }

        for (var tick = 5; tick <= 10; tick++)
        {
            Assert.True(contact[tick], $"t{tick}: the crate is the contact");
        }

        Assert.False(contact[11]);
        Assert.Equal(109.0, x[5]);
        Assert.Equal(109.0, x[11]);
    }

    // ---- (m7) the lift in the air --------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(6, true, LiftInTheAir)]
    [InlineData(7, true, LiftInTheAir)]
    [InlineData(8, false, SwordInTheAir)]
    public void M7_TheLiftInTheAir_TheContactOfTheCrateAtT5AndT6GivesTwoLiftsThenTheSword(int squareTick, bool lifts, uint animAtSquare)
    {
        // g15 a): RIGHT x3, RIGHT+CROSS at t4 (the jump), then the Square at t6, t7 or t8 while RIGHT is held; the contact is the crate at t5 and t6, nil from t7.
        var rig = Scene(1, Script((3, R), (1, R | CROSS), (squareTick - 5, R), (1, R | SQ), (14, R)));
        var liftedAt = 0;
        uint anim = 0;
        var contact = new Dictionary<int, bool>();
        rig.Run(12, tick =>
        {
            contact[tick] = rig.Hero.XCollisionEntity != null;
            if (liftedAt == 0 && Carries(rig))
            {
                liftedAt = tick;
            }

            if (tick == squareTick)
            {
                anim = rig.Hero.TargetAnimationId;
            }
        });

        Assert.True(contact[5]);
        if (!lifts)
        {
            Assert.True(contact[6]);
            Assert.False(contact[7]);
        }

        Assert.Equal(lifts ? squareTick : 0, liftedAt);
        Assert.Equal(animAtSquare, anim);
    }

    // ---- (m4) the object on the head -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void M4_AnObjectOnTheHead_IsLiftedWithoutAnyButton_AtTheFirstMovePlayerThatSeesIt()
    {
        // g9 G): a crate dropped on the head of the hero at z = 32; the physics sets its support (+0x12C = the hero), and the first MovePlayer that finds it lifts it with no button (anim 0 -> 12, the plain carrying stand: the object already stood on
        // the head). In the oracle the support is set by the physics of t1 and the lift is the MovePlayer of t2; here the state is read BEFORE each tick, so the rule is checked whatever the tick the montage settles the support at.
        CarryRig? holder = null;
        var supportedBefore = new Dictionary<int, bool>();
        var rig = CarryRig.Build(1, tick =>
        {
            supportedBefore[tick] = holder?.Crate is { } c && ReferenceEquals(c.RidingEntity, holder.Hero.LogicContextEntity);
            return 0u;
        }, probeFactory: host => new MovementObstacleProbe(host), heroX: 100f, heroY: 100f);
        holder = rig;
        EquipTheKit(rig.Host);
        var crate = rig.AddCrate(100, 100, 32, sizeX: 21, sizeY: 15, sizeZ: 16, offsetX: -10, offsetY: -7, prefabBody: true);
        crate.SpriteType = WiredType;
        var carriedAt = 0;
        rig.Run(6, tick =>
        {
            if (carriedAt == 0 && Carries(rig))
            {
                carriedAt = tick;
            }
        });

        var firstSeen = 0;
        for (var tick = 1; tick <= 6; tick++)
        {
            if (supportedBefore.GetValueOrDefault(tick))
            {
                firstSeen = tick;
                break;
            }
        }

        Assert.InRange(firstSeen, 1, 3);
        Assert.Equal(firstSeen, carriedAt);
        Assert.Equal(CarryStandOnTheGround, rig.Hero.TargetAnimationId);
        Assert.Same(rig.Hero.LogicContextEntity, crate.PlatformEntity);
    }

    // ---- (m5) a liftable outside the set gives the sword ------------------------------------------------------------------------------------------------------

    [Fact]
    public void M5_AnObjectOutsideTheSet_GivesTheSword_NextToTheCrateOfTheSameSceneThatLifts()
    {
        // The same pad (RIGHT x5, then RIGHT+SQUARE at t6: the line E2): the object outside the set (the torch, genre 1 like a crate, not wired) gets the sword, the crate of the set lifts.
        var pad = Script((5, R), (1, R | SQ), (10, R));

        var outside = Scene(1, pad, UnwiredType);
        outside.Run(12);
        Assert.False(Carries(outside));
        Assert.Null(outside.Hero.CarriedEntity);
        Assert.Equal(SwordOnTheGround, outside.Hero.TargetAnimationId);

        var inside = Scene(1, pad, WiredType);
        inside.Run(12);
        Assert.True(Carries(inside));
        Assert.Equal(LiftOnTheGround, inside.Hero.TargetAnimationId);
    }

    // ---- (m6) the priority: the interaction of a person, the lift of an object ----------------------------------------------------------------------------------

    [Fact]
    public void M6_TheContactOfAPerson_GivesTheTalk_TheContactOfACrate_GivesTheLift()
    {
        // The binary: PlayerTryAction first (TryLift of a genre 0 gives 0, the tick goes on), then CheckEntityInteraction. The contact word is the one poke of the scene (as c4_climb_lift), at the tick of the Square.
        var squareTick = 3;
        var person = Scene(1, Script((2, 0u), (1, SQ), (6, 0u)));
        var sailor = person.AddObject("sailor", 140, 100, 20, 14, 32, offsetX: -10, offsetY: -7);
        sailor.Flags |= EntityFlags.InteractRequiresButton;
        sailor.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8D;
        person.Run(squareTick - 1);
        person.Hero.XCollisionEntity = sailor;
        person.Run(1);
        Assert.Same(sailor, person.Host.GameState.InteractLatchEntity); // the button branch of CheckEntityInteraction took the contact (the F program is run and the entity released by the programs pass of the same tick).
        Assert.Null(person.Hero.CarriedEntity);
        Assert.Equal(0u, person.Hero.TargetAnimationId);

        var crate = Scene(1, Script((2, 0u), (1, SQ), (6, 0u)));
        crate.Run(squareTick - 1);
        crate.Hero.XCollisionEntity = crate.Crate;
        crate.Run(1);
        Assert.True(Carries(crate));
        Assert.Equal(LiftOnTheGround, crate.Hero.TargetAnimationId);
        Assert.Null(crate.Host.GameState.InteractLatchEntity);
    }

    [Fact]
    public void M6_AnObjectThatIsBothLiftableAndInteractive_IsLiftedFirst_TheLiftComesBeforeTheInteraction()
    {
        // The order of the binary at 0x80031E38 is TryUseItem, PlayerTryAction, then CheckEntityInteraction: a wired liftable that also carries a button F program is lifted, and the interaction
        // (which would latch the entity) never runs. Pins the priority against an inverted order (T4 mutant 11).
        var rig = Scene(1, Script((2, 0u), (1, SQ), (6, 0u)));
        rig.Crate.Flags |= EntityFlags.InteractRequiresButton;
        rig.Crate.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x8D;
        rig.Run(2);
        rig.Hero.XCollisionEntity = rig.Crate;
        rig.Run(1);
        Assert.True(Carries(rig));
        Assert.Equal(LiftOnTheGround, rig.Hero.TargetAnimationId);
        Assert.Null(rig.Host.GameState.InteractLatchEntity);
    }

    // ---- (m8) the climb ---------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(6)]
    [InlineData(14)]
    public void M8_TheClimbState_TheSquareWithTheContactLifts_AnimationElevenInTheAir(int heightPx)
    {
        // c4_climb_lift rows A and C: the hero in the climb state (0x0E) with his feet off the ground, UP held, the contact poked at the tick of the Square: lifted, anim_t 14 -> 11. TryUseItem then PlayerTryAction at 0x800321D4-0x800321E4.
        var rig = Scene(1, Script((2, UP), (1, UP | SQ), (6, UP)));
        var hero = rig.Hero;
        rig.Run(2);
        // The bench has no ladder: the two UP ticks would leave the climb state (the hero falls to the air state), so the state is put AFTER them, right before the tick of the Square, and the test pins that
        // the climb branch of MovePlayer is the one that calls TryUseItem then PlayerTryAction (retiring those calls leaves the hero in the climb state and lifts nothing).
        hero.PosZ = heightPx << 16;
        hero.PushLogicalPositionToRoot(); // the controller too is off the ground
        hero.TargetAnimationId = PlayerManager.ClimbingAnimationId;
        hero.CurrentAnimationId = PlayerManager.ClimbingAnimationId;
        hero.TargetDirection = 0x10;
        hero.Flags &= ~EntityFlags.Gravity;
        hero.XCollisionEntity = rig.Crate;
        hero.IsOnGround = 0; // the feet are off the ground in the oracle (z 6 and 14 px over a floor at 0)
        rig.Run(1);

        Assert.True(Carries(rig));
        Assert.Equal(LiftInTheAir, hero.TargetAnimationId);
    }

    [Fact]
    public void M8_TheClimbState_TheSquareWithoutTheContact_LiftsNothing()
    {
        // c4_climb_lift row B: the same Square without the contact word: nothing is lifted.
        var rig = Scene(1, Script((2, UP), (1, UP | SQ), (6, UP)));
        var hero = rig.Hero;
        rig.Run(2);
        hero.PosZ = 6 << 16;
        hero.PushLogicalPositionToRoot();
        hero.TargetAnimationId = PlayerManager.ClimbingAnimationId;
        hero.CurrentAnimationId = PlayerManager.ClimbingAnimationId;
        hero.TargetDirection = 0x10;
        hero.Flags &= ~EntityFlags.Gravity;
        hero.IsOnGround = 0;
        rig.Run(1);

        Assert.False(Carries(rig));
        Assert.Null(hero.CarriedEntity);
    }
}
