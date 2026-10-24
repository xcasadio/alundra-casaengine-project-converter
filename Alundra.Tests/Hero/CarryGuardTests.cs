#nullable enable
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1): the two guards that are GREEN from the start of the slice and must stay so through the branchings of T3. (w6) The ladder is refused to a hero who carries an object (the fifth conjunct of the gate of the case 6,
/// <c>0x80031D54</c>), next to the hero who carries nothing and enters; (w12) the Square with an object in the hands changes nothing - the throw is a named stub at its place in the carrying group until E20.g2a replaces it - next to the hero who carries nothing and
/// swings his weapon on the same press.
/// </summary>
public sealed class CarryGuardTests
{
    private static EntityScriptProxy Hero()
    {
        var hero = CarryBench.Make("hero", 100 * CarryBench.Px, 100 * CarryBench.Px, 0);
        hero.IsPlayer = true;
        hero.TargetDirection = 0x10;
        return hero;
    }

    private static EntityScriptProxy Crate(EntityScriptProxy hero)
    {
        var crate = CarryBench.Make("crate", hero.PosX + 21 * CarryBench.Px, hero.PosY, hero.PosZ);
        crate.Flags = CarryRig.CrateFlags;
        crate.BalanceRecord = CarryBench.Sheet(1);
        PlayerManager.PutInHands(hero, crate);
        return crate;
    }

    private static void Move(EntityScriptProxy hero, uint hold, uint pressed = 0)
    {
        var pad = new PadState { ButtonsHold = hold, ButtonsJustPressed = pressed };
        PlayerManager.MovePlayer(hero, in pad, new GameState(), host: null);
    }

    [Fact]
    public void W6_TheLadderIsRefusedToAHeroWhoCarriesAnObject_AndEnteredByTheHeroWhoCarriesNothing()
    {
        var carrying = Hero();
        Crate(carrying);
        carrying.Slope_18c = 6; // on a ladder cell
        carrying.ForceAdjusted = 1; // walked into the wall this tick
        Assert.Equal(CarryRules.LiftGroundState, carrying.TargetAnimationId); // the state PutInHands gave on the ground: the lift gesture.
        Move(carrying, PadState.Up);
        Assert.NotEqual(PlayerManager.ClimbingAnimationId, carrying.TargetAnimationId);
        Assert.NotNull(carrying.CarriedEntity);

        var free = Hero();
        free.Slope_18c = 6;
        free.ForceAdjusted = 1;
        free.TargetAnimationId = 0;
        Move(free, PadState.Up);
        Assert.Equal(PlayerManager.ClimbingAnimationId, free.TargetAnimationId);
    }

    [Fact]
    public void W12_TheSquareWithAnObjectInTheHands_ChangesNothing_WhereTheHeroWithoutAnObjectSwingsHisWeapon()
    {
        var carrying = Hero();
        var crate = Crate(carrying);
        var link = carrying.CarriedEntity;
        var state = carrying.TargetAnimationId;
        var direction = carrying.TargetDirection;
        var flags = crate.Flags;
        Move(carrying, PadState.Square, PadState.Square);

        Assert.Equal(state, carrying.TargetAnimationId);
        Assert.Equal(direction, carrying.TargetDirection);
        Assert.Same(link, carrying.CarriedEntity);
        Assert.Same(carrying.LogicContextEntity, crate.PlatformEntity);
        Assert.Equal(0, crate.Flags2);
        Assert.Equal(flags, crate.Flags);
        Assert.Equal(carrying.PosX + 21 * CarryBench.Px, crate.PosX);

        var free = Hero();
        free.TargetAnimationId = 0;
        Move(free, PadState.Square, PadState.Square);
        Assert.NotEqual(0u, free.TargetAnimationId); // the swing of the weapon of the hero.
    }
}
