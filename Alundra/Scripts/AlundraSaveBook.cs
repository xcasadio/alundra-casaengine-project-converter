#nullable enable
namespace Alundra.Scripts;

/// <summary>
/// E16.e L1 (docs/plan-e16-etat-partie.md): the save book, « SaveBook (Ne pas toucher !) » - sprite type 237,
/// one per map on 65 maps (§2, Q4) - the ONLY entity whose native sprite AI this DLL runs. Its two native
/// handlers are reached through <see cref="AlundraEventProgramRunner.RunSpriteEvent"/>, which dispatches here only
/// when <see cref="TryMatch"/> holds: slot F code 1 (<c>Script_FInteract_FUN_8007fc64</c>, 0x8007FC64) and slot C
/// code 72 (<c>AI_ProcessWarpTransitionState</c>, 0x8007B998, entry 72 of the <c>ProgramCTick</c> table at
/// 0x800C4F34, word 0x800C5054). Porting any other native handler is E14's, never this class's.
/// </summary>
public sealed class AlundraSaveBook
{
    /// <summary>The one session instance.</summary>
    public static readonly AlundraSaveBook Instance = new();

    private AlundraSaveBook()
    {
    }

    /// <summary>§2, Q4: the save book's sprite type (<see cref="AlundraEntityScriptProxy.SpriteType"/>), line 239 of
    /// the analyser's <c>EntityNames.csv</c> (index 237 once its header line is skipped).</summary>
    public const int SpriteType = 237;

    /// <summary>Slot F code of <c>Script_FInteract_FUN_8007fc64</c> (<c>SpriteEventHandlers.cs:233-234</c>).</summary>
    internal const int InteractCode = 1;

    /// <summary>Slot C code of <c>AI_ProcessWarpTransitionState</c> (§2, Q4: entry 72 of the <c>ProgramCTick</c>
    /// table).</summary>
    internal const int TickCode = 72;

    /// <summary>
    /// L1: true when <paramref name="entity"/>'s picked event (<see cref="AlundraEntityScriptProxy.EventTrigger"/>) is
    /// one of the save book's two native handlers - ALL of: sprite type <see cref="SpriteType"/>, a native slot
    /// (<c>ProgramIndexes[slot] &amp; 0x7f == 0</c>, the test <see cref="AlundraEntityScriptProxy.RunPickedEvent"/>
    /// already made), and (slot, <c>SpriteProgramIndexes[slot]</c>) either (F, <see cref="InteractCode"/>) or (C,
    /// <see cref="TickCode"/>). <paramref name="slot"/> is the matched slot.
    /// </summary>
    internal static bool TryMatch(AlundraEntityScriptProxy entity, out int slot)
    {
        slot = entity.EventTrigger;
        if (entity.SpriteType != SpriteType || slot < 0 || slot >= entity.ProgramIndexes.Length)
        {
            return false;
        }

        if ((entity.ProgramIndexes[slot] & 0x7f) != 0)
        {
            return false;
        }

        var code = entity.SpriteProgramIndexes[slot];
        return (slot == ScriptHelper.ProgramFInteract && code == InteractCode)
            || (slot == ScriptHelper.ProgramCTick && code == TickCode);
    }

    /// <summary>Slot F code 1 of the save book - ported by E16.e T2 (L4).</summary>
    internal void RunInteract(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state)
    {
    }

    /// <summary>Slot C code 72 of the save book - ported by E16.e T2 (L4).</summary>
    internal void RunTick(AlundraEntityScriptProxy book, IEntityWorldContext context, AlundraGameState state)
    {
    }
}
