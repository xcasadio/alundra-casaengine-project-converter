#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T1 acceptance (docs/plan-transitions-carte.md, slice "T1 - État de partie en session"): the SEVEN rows
/// of D-T-13's own field-by-field disposition table, each asserted across two CONSECUTIVE map entries
/// sharing the same session carrier (<see cref="AlundraGameState.Instance"/>) - the "two worlds share the
/// singleton" shape T7 already established (<see cref="AlundraScreenFadeDirectorTests"/>:370-412), driven
/// here through <see cref="AlundraGameState.InstallForMapEntry"/> directly rather than a full
/// <see cref="AlundraWorldProxy.InitializeWithWorld"/> montage - that method needs a live CasaEngineGame
/// asset catalog just to REACH this call (see <see cref="AlundraWorldProxyTests"/>' own class doc for why
/// the whole method is never exercised end-to-end in this test host), and <see cref="AlundraGameState"/>'s
/// own map-entry behaviour has no such dependency.
///
/// This class does NOT construct an <see cref="AlundraWorldProxy"/>, so it falls outside D-T-14's own
/// "ten classes" criterion (docs/plan-transitions-carte.md, D-T-14) - it still resets
/// <see cref="AlundraGameState.Instance"/>/<see cref="AlundraSoundBank"/>'s session cache in
/// constructor/Dispose, for the same isolation reason every other session-singleton test class does.
/// </summary>
public sealed class AlundraGameStateSessionTests : IDisposable
{
    public AlundraGameStateSessionTests()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    // ---- D-T-13's seven rows, each across two consecutive map entries (the last test covers the
    // interact-latch reference and its eight numeric fields together) -------------------------------

    [Fact]
    public void GameFlags_SurvivesTheNextMapEntry()
    {
        var state = AlundraGameState.Instance;

        // "World 1" sets a save-game flag during play (bit 0x8000 clear -> GameFlags bank).
        state.AddFlag(flag: 3, mask: 0x1);
        Assert.Equal(0x1u, state.GetFlag(3));

        state.InstallForMapEntry(); // "World 2"'s own map-entry install.

        Assert.Equal(0x1u, state.GetFlag(3)); // D-T-13: GameFlags conservé.
    }

    [Fact]
    public void TemporaryFlags_ClearedAtTheNextMapEntry()
    {
        var state = AlundraGameState.Instance;

        // "World 1" sets a session-only flag during play (bit 0x8000 set -> TemporaryFlags bank).
        state.AddFlag(flag: 0x8000 | 3, mask: 0x1);
        Assert.Equal(0x1u, state.GetFlag(0x8000 | 3));

        state.InstallForMapEntry(); // "World 2"'s own map-entry install.

        Assert.Equal(0u, state.GetFlag(0x8000 | 3)); // D-T-13: TemporaryFlags vidé (ClearTemporaryFlags, GameEngine.cs:429-438).
    }

    [Fact]
    public void MapIdToInternalMapIndexTable_SurvivesTheNextMapEntry_NonIdentityEntry()
    {
        var state = AlundraGameState.Instance;

        // "World 1" writes a NON-identity entry, as opcode 0x38 would (Script_SetSaveMapIdToInternalMapIndex_038,
        // EntityEventHandlers.cs:1202-1207).
        state.MapIdToInternalMapIndexTable[42] = 7;
        Assert.Equal((ushort)7, state.MapIdToInternalMapIndexTable[42]);

        state.InstallForMapEntry(); // "World 2"'s own map-entry install.

        Assert.Equal((ushort)7, state.MapIdToInternalMapIndexTable[42]); // D-T-13: conservé.
    }

    [Fact]
    public void PlayerControlFlags_ControlLocked_SurvivesTheNextMapEntry()
    {
        var state = AlundraGameState.Instance;

        // "World 1" locks player control (event opcode 0x10's own bridge).
        state.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
        Assert.NotEqual(0u, state.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked);

        state.InstallForMapEntry(); // "World 2"'s own map-entry install.

        Assert.NotEqual(0u, state.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked); // D-T-13: conservé.
    }

    [Fact]
    public void LastPadState_SurvivesTheNextMapEntry_ReadableBeforeThatWorldsFirstPlayerUpdate()
    {
        var state = AlundraGameState.Instance;

        // "World 1"'s last published pad snapshot (AlundraEntityScriptProxy.Update's own player branch).
        var pad = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
        state.LastPadState = pad;

        state.InstallForMapEntry(); // "World 2"'s own map-entry install, before its first player Update.

        // D-T-13: conservé - readable exactly as an opcode 0x2F fired before world 2's first player
        // Update would read the original's own global g_padState1.
        Assert.Equal(pad.ButtonsJustPressed, state.LastPadState.ButtonsJustPressed);
    }

    [Fact]
    public void InteractLatch_EntityClearedButEightNumericFieldsSurvive_NoWorldOneEntityReachable()
    {
        var state = AlundraGameState.Instance;

        // "World 1" leaves the interact latch armed on one of its own entities.
        var world1Entity = new AlundraEntityScriptProxy();
        state.InteractLatchEntity = world1Entity;
        state.InteractLatchFacing = 1;
        state.InteractLatchEntityX = 2;
        state.InteractLatchEntityY = 3;
        state.InteractLatchEntityZ = 4;
        state.InteractLatchPlayerX = 5;
        state.InteractLatchPlayerY = 6;
        state.InteractLatchPlayerZ = 7;
        state.InteractLatchDirection = 8;

        state.InstallForMapEntry(); // "World 2"'s own map-entry install - world 1 is torn down.

        // D-T-13: InteractLatchEntity vidé - no reference to world 1's dead entity is reachable from the
        // session state any more.
        Assert.Null(state.InteractLatchEntity);

        // D-T-13: the eight numeric fields stay conservés (they self-invalidate via their own eight
        // equality checks, exactly like the original).
        Assert.Equal(1, state.InteractLatchFacing);
        Assert.Equal(2, state.InteractLatchEntityX);
        Assert.Equal(3, state.InteractLatchEntityY);
        Assert.Equal(4, state.InteractLatchEntityZ);
        Assert.Equal(5, state.InteractLatchPlayerX);
        Assert.Equal(6, state.InteractLatchPlayerY);
        Assert.Equal(7, state.InteractLatchPlayerZ);
        Assert.Equal(8u, state.InteractLatchDirection);
    }

    // ---- "the sfx manifest is read only once for two worlds" (T1's own extra clause) ---------------

    [Fact]
    public void AlundraSoundBank_GetOrCreate_ReadsTheManifestOnlyOnceAcrossTwoWorlds_SameProjectPath()
    {
        var projectRoot = BuildTempProjectWithEmptyManifest();
        try
        {
            var world1Bank = AlundraSoundBank.GetOrCreate(projectRoot); // "world 1" install.
            var world2Bank = AlundraSoundBank.GetOrCreate(projectRoot); // "world 2" install, same project.

            // Same instance back both times: the manifest file behind it was parsed only once, at the
            // first request - a fresh AlundraSoundBank(projectRoot) would re-read it every time.
            Assert.Same(world1Bank, world2Bank);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void AlundraSoundBank_GetOrCreate_DoesNotShareACacheEntryAcrossDifferentProjectPaths()
    {
        var projectRootA = BuildTempProjectWithEmptyManifest();
        var projectRootB = BuildTempProjectWithEmptyManifest();
        try
        {
            var bankA = AlundraSoundBank.GetOrCreate(projectRootA);
            var bankB = AlundraSoundBank.GetOrCreate(projectRootB);

            Assert.NotSame(bankA, bankB); // D-T-14: two different projects never share a cached bank.
        }
        finally
        {
            Directory.Delete(projectRootA, recursive: true);
            Directory.Delete(projectRootB, recursive: true);
        }
    }

    private static string BuildTempProjectWithEmptyManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraGameStateSessionTests_" + Guid.NewGuid().ToString("N"));
        var soundsDir = Path.Combine(root, "Sounds");
        Directory.CreateDirectory(soundsDir);
        File.WriteAllText(Path.Combine(soundsDir, "sfx-manifest.json"), "[]");
        return root;
    }
}
