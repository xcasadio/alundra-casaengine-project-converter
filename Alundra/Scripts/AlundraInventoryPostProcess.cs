#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// E13.d SI3 (docs/plan-e13d-sous-inventaire.md, D-E13D-21): port of <c>g_postProcessState</c>
/// (<c>StaticVariables.cs:13098</c>) and of the post-process block that reads/writes it
/// (<c>GraphicManager.cs:1691-1706</c>), run once per LOGIC TICK, in <see cref="AlundraWorldProxy"/>'s own
/// per-tick pad loop, AFTER both <see cref="AlundraInventoryDirector.Tick"/> and
/// <see cref="AlundraSubInventoryDirector.Tick"/> - the same "after all thirteen callbacks" position the
/// original's own post-process holds inside <c>GraphicManager.UpdateUserInterface</c>. It replaces the
/// director's own <c>_pendingSubInventoryTransition</c> hook (SI3): one state shared by BOTH inventories,
/// exactly like the original's single global.
///
/// <para><b>The two independent <c>if</c>s</b> (plan §1.1): <see cref="Run"/> re-reads <see cref="State"/>
/// between them, exactly like the binary (which re-reads <c>g_postProcessState</c> at <c>0x800481b4</c>
/// rather than caching it in a register) - so a caller that races both writes in the same tick (never
/// happens in practice: only one director's <c>RunInput</c> runs its L1/R1 branch per tick, since the two
/// inventories are never both active at once, D-E13D-26) still sees the SECOND branch test the value the
/// FIRST branch just wrote, not a stale copy.</para>
///
/// <para><b>"Slot 6 free" / "slot 4 free"</b> (plan §1.1 - the original tests
/// <c>g_callbackTable[6].Flags</c>/<c>[4].Flags == 0</c>, the callback-table rappel this port has no
/// literal table for): ported as <see cref="AlundraInventoryDirector.IsCallbackArmed"/> (raised by
/// <c>SetTransitionType(6)</c> in <c>DisplayInventory</c>, before the setup, and cleared at the
/// close-slide's own completion - see that property's own doc) and
/// <see cref="AlundraSubInventoryDirector.State"/> == 0 respectively.</para>
/// </summary>
public sealed class AlundraInventoryPostProcess
{
    public static readonly AlundraInventoryPostProcess Instance = new();

    private AlundraInventoryPostProcess()
    {
    }

    /// <summary>Port of <c>g_postProcessState</c> - 0 idle, 1 "open the sub-inventory next", 2 "open the
    /// main inventory's head next". Written by <see cref="AlundraInventoryDirector.RunInput"/>'s and
    /// <see cref="AlundraSubInventoryDirector"/>'s own L1/R1 branches (<c>1</c>/<c>2</c>
    /// respectively, <c>MainInventoryManager.cs:858</c>/<c>SubInventoryManager.cs:376</c>), read/reset by
    /// <see cref="Run"/> (<c>GraphicManager.cs:1696</c>/<c>:1703</c>) - the only three writers in the whole
    /// decompilation (plan §1.1).</summary>
    public int State { get; internal set; }

    /// <summary>Test-only: restores this session singleton to construction-equivalent state.</summary>
    internal void ResetForTests()
    {
        State = 0;
    }

    /// <summary>Port of the post-process block (<c>GraphicManager.cs:1691-1706</c>), called once per logic
    /// tick after both directors' own <see cref="AlundraInventoryDirector.Tick"/>/
    /// <see cref="AlundraSubInventoryDirector.Tick"/> (<see cref="AlundraWorldProxy"/>'s per-tick pad
    /// loop).</summary>
    public void Run()
    {
        // :1693-1697 - state 1, main inventory's own rappel free -> open the sub-inventory (StartFadeOut's
        // body, minus the portrait - AlundraSubInventoryDirector.OpenFromPostProcess).
        if (State == 1 && !AlundraInventoryDirector.Instance.IsCallbackArmed)
        {
            State = 0;
            AlundraSubInventoryDirector.Instance.OpenFromPostProcess();
        }

        // :1699-1704 - state 2, sub-inventory's own rappel free -> the main inventory's HEAD only
        // (DisplayInventory's guards/InitializeHudPosition/icon names/sound 4) - D-E13D-22, the setup
        // (FUN_80054f1c) runs from the NEXT tick, see AlundraInventoryDirector's own doc.
        if (State == 2 && AlundraSubInventoryDirector.Instance.State == 0)
        {
            State = 0;
            AlundraInventoryDirector.Instance.RunDisplayInventoryHeadFromPostProcess();
        }
    }
}
