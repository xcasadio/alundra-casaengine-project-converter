#nullable enable
namespace Alundra.Scripts;

/// <summary>
/// E20.f1a (docs/plan-e20-heros.md section 1.9): the named stub of the charge aura (global effect 3, attached to the hero, <c>0x8002F570-0x8002F5C0</c> and <c>0x8002F730-0x8002F744</c>), whose real port is E20.f1b.
/// It NEVER reads the effect reservoir nor <c>Destroying</c> (the <c>+0x7D</c> of the aura's slot): reading the reservoir from <c>MovePlayer</c> is the architecture change of E20.f1b (ADR-0046 says the scripts do not).
/// It holds one boolean, "the aura is alive in principle", and two counters:
/// <list type="bullet">
/// <item><description><see cref="Check"/>, called at every tick where the charge counter is 11 or more, at the site of the creation (<c>0x8002F570</c>): when the boolean is false it counts one creation and sets it (the model of the
/// null handle); it does not model the renewal by <c>+0x7D</c> (every 97 ticks of a held charge: E20.f1b);</description></item>
/// <item><description><see cref="Release"/>, called at the first tick where the counter is below 11 while the boolean is true (<c>0x8002F730</c>, non-null handle): it counts one release and clears the boolean.</description></item>
/// </list>
/// </summary>
internal sealed class AlundraAuraStub
{
    /// <summary>True from the creation to the release: the binary's handle <c>0x80126FF8</c> is non-null.</summary>
    public bool Alive { get; private set; }

    /// <summary>The number of creations counted (one per charge in the stub's model).</summary>
    public int Creations { get; private set; }

    /// <summary>The number of releases counted.</summary>
    public int Releases { get; private set; }

    /// <summary>Test-only: the number of calls of <see cref="Check"/> (the oracle's C2-steel has 59, from tick 33 to tick 91).</summary>
    public int CheckCallsForTests { get; private set; }

    /// <summary>The site of the creation: the charge counter is 11 or more.</summary>
    public void Check()
    {
        CheckCallsForTests++;
        if (!Alive)
        {
            Creations++;
            Alive = true;
        }
    }

    /// <summary>The site of the release: the charge counter is below 11.</summary>
    public void Release()
    {
        if (Alive)
        {
            Releases++;
            Alive = false;
        }
    }
}
