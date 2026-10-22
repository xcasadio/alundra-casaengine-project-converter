#nullable enable
using System.Linq;
using Xunit;
using Alundra.Combat;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.f3c phase B, (p7a) [V] green by construction (docs/plan-e20-heros.md section 1.19.3): the damage of the sword wave on a Beannoid, by the pure function <see cref="Damage.ResolveBalanceTarget"/>. The wave attacks with the
/// attribute 129 (<c>0x81</c>) and the power 10, 20, 38 (steel, demon, holy sword: the balance record 27 to 29); the end-to-end damage stays for the recette. No equipment record is given here (the equipment term needs the hero's inventory).
/// </summary>
public sealed class AlundraWaveSwordDamageTests
{
    [Fact]
    public void P7a_TheWaveAttack129_OnTheBeannoidSheet_DealsThePowerTimesTheResponseOver16_ForThe3Swords()
    {
        var balance = BalanceTables.GetOrCreate(ProjectRootFinder.Find());
        var beannoid = balance.Select(335, 0)!;
        var response = beannoid.ByteAt(2 + (0x81 & 0xF)); // the byte of the sheet that answers the nibble 1: 16, a plain multiplier of 1 (kind 0: not a kill, not a block).
        var after = new[] { 10, 20, 38 }.Select(power => Damage.ResolveBalanceTarget(new BalanceAttack(0x81, power), beannoid, 100, null, 0)).ToArray();

        // (power * 16) >> 4 = the power: 100 - 10, 100 - 20, 100 - 38; and a life below the damage stops at 0.
        Assert.Equal((16, "90,80,62", 0), (response, string.Join(",", after), Damage.ResolveBalanceTarget(new BalanceAttack(0x81, 38), beannoid, 30, null, 0)));
    }
}
