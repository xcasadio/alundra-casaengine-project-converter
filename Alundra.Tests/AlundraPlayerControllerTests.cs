using System;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Input;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="AlundraPlayerController"/>'s input-mapping registration
/// (<see cref="AlundraPlayerController.RegisterMappings"/>) and pad-state building
/// (<see cref="AlundraPlayerController.ComputePadState"/>) - the pure/testable cores
/// <see cref="AlundraPlayerController.EnsureInputMappingsRegistered"/>/<see cref="AlundraPlayerController.BuildPadState"/>
/// wrap around a live <c>CasaEngineGame</c>, which this project's headless test process cannot construct
/// (same constraint as <see cref="AlundraWorldProxy"/>'s own live-World tests).
/// </summary>
public class AlundraPlayerControllerTests
{
    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Data")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static readonly string[] ExpectedActionNames =
    {
        "MoveUp", "MoveDown", "MoveLeft", "MoveRight",
        // Left stick (user report, 2026-08-26: the stick moved nothing because only the D-pad was bound).
        // Four SEPARATE analog actions, because an InputMapping is either digital or analog and never
        // both - they alias onto the same four PSX bits in AlundraPlayerController.ActionBits.
        "MoveUpStick", "MoveDownStick", "MoveRightStick", "MoveLeftStick",
        "Jump", "Attack", "UseItem", "Sprint", "Menu",
    };

    /// <summary>
    /// Real-export anchor test: loads the actual converted <c>Data/Alundra.buttonsMapping</c> through the
    /// SAME <see cref="AssetLoader{T}"/> the engine now registers for <see cref="ButtonsMapping"/>
    /// (CasaEngineMonogame commit fe19e1e6) - <paramref name="assetContentManager"/> is unused by that
    /// loader (it only reads/parses the file), so passing null is safe here. Self-skips when
    /// <c>alundra-project/</c> is not present in this checkout (same pattern as
    /// <see cref="IntroTraceHarnessTests"/>).
    /// </summary>
    [Fact]
    public void AssetLoader_RealButtonsMappingFile_ParsesThirteenMappingsWithExpectedNames()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var filePath = Path.Combine(projectRoot, "Data", "Alundra.buttonsMapping");
        Assert.True(File.Exists(filePath));

        var loader = new AssetLoader<ButtonsMapping>();
        var asset = (ButtonsMapping?)loader.LoadAsset(filePath, null!);

        Assert.NotNull(asset);
        Assert.Equal(13, asset!.Buttons.Count);
        Assert.Equal(ExpectedActionNames, asset.Buttons.Select(b => b.Name).ToArray());
    }

    // -----------------------------------------------------------------------------------------
    // RegisterMappings
    // -----------------------------------------------------------------------------------------

    private static ButtonsMapping NewButtonsMapping()
    {
        var buttonsMapping = new ButtonsMapping();
        foreach (var name in ExpectedActionNames)
        {
            buttonsMapping.Buttons.Add(new InputMapping { Name = name });
        }

        return buttonsMapping;
    }

    [Fact]
    public void RegisterMappings_FreshManager_RegistersAllThirteen()
    {
        var manager = new InputMappingManager();

        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        foreach (var name in ExpectedActionNames)
        {
            Assert.True(manager.Contains(name));
        }
    }

    /// <summary>Reads the private backing list <see cref="InputMappingManager"/> keeps its registered
    /// mappings in - the class exposes no public count/enumeration, so this is the only way to assert the
    /// EXACT mapping count a duplicate-registration bug would otherwise silently inflate.</summary>
    private static int CountMappings(InputMappingManager manager)
    {
        var field = typeof(InputMappingManager).GetField(
            "_inputMappings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (System.Collections.IList)field!.GetValue(manager)!;
        return list.Count;
    }

    [Fact]
    public void RegisterMappings_AppliedTwice_LeavesExactlyThirteenMappings()
    {
        var manager = new InputMappingManager();

        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);
        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        Assert.Equal(13, CountMappings(manager));

        foreach (var name in ExpectedActionNames)
        {
            Assert.True(manager.Contains(name));
        }
    }

    [Fact]
    public void RegisterMappings_PartiallyRegisteredManager_OnlyAddsMissingNames()
    {
        var manager = new InputMappingManager();
        manager.AddInputMapping(new InputMapping { Name = "MoveUp" }); // pre-existing, e.g. a prior partial load

        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        foreach (var name in ExpectedActionNames)
        {
            Assert.True(manager.Contains(name));
        }
    }

    // -----------------------------------------------------------------------------------------
    // ComputePadState
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ComputePadState_AllMappingsRegisteredAndHeld_SetsEveryBit()
    {
        var manager = new InputMappingManager();
        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        var state = AlundraPlayerController.ComputePadState(manager, _ => new ButtonState { IsKeyPressed = true });

        Assert.Equal(
            AlundraPadState.Up | AlundraPadState.Down | AlundraPadState.Left | AlundraPadState.Right
            | AlundraPadState.Cross | AlundraPadState.Square | AlundraPadState.Circle | AlundraPadState.Triangle
            | AlundraPadState.Start,
            state.ButtonsHold);
    }

    /// <summary>
    /// User report (2026-08-26): "the left stick does not move Alundra, only the D-pad does". The fix
    /// gives the stick its own four AnalogInput actions, aliased onto the SAME four PSX bits as the
    /// D-pad. This test holds ONLY the stick actions - every D-pad action reads released - and requires
    /// the four direction bits to come out set anyway. It fails if the stick entries are dropped from
    /// <c>ActionBits</c>, or if they are wired onto different bits.
    /// </summary>
    [Fact]
    public void ComputePadState_OnlyTheLeftStickHeld_StillSetsTheFourDirectionBits()
    {
        var manager = new InputMappingManager();
        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        var state = AlundraPlayerController.ComputePadState(
            manager, name => new ButtonState { IsKeyPressed = name.EndsWith("Stick", StringComparison.Ordinal) });

        Assert.Equal(
            AlundraPadState.Up | AlundraPadState.Down | AlundraPadState.Left | AlundraPadState.Right,
            state.ButtonsHold);
    }

    /// <summary>Holding the stick AND the D-pad the same way must not produce anything new: both feed one
    /// ButtonsHold, so the ported game logic can never tell which device a direction came from.</summary>
    [Fact]
    public void ComputePadState_StickAndDPadAgreeing_ProducesTheSameBitsAsEitherAlone()
    {
        var manager = new InputMappingManager();
        AlundraPlayerController.RegisterMappings(NewButtonsMapping(), manager);

        var stickOnly = AlundraPlayerController.ComputePadState(
            manager, name => new ButtonState { IsKeyPressed = name == "MoveLeftStick" });
        var dPadOnly = AlundraPlayerController.ComputePadState(
            manager, name => new ButtonState { IsKeyPressed = name == "MoveLeft" });
        var both = AlundraPlayerController.ComputePadState(
            manager, name => new ButtonState { IsKeyPressed = name is "MoveLeft" or "MoveLeftStick" });

        Assert.Equal(AlundraPadState.Left, stickOnly.ButtonsHold);
        Assert.Equal(AlundraPadState.Left, dPadOnly.ButtonsHold);
        Assert.Equal(AlundraPadState.Left, both.ButtonsHold);
    }

    [Fact]
    public void ComputePadState_OnlySomeMappingsRegistered_SkipsMissingActions_NeverThrows()
    {
        var manager = new InputMappingManager();
        manager.AddInputMapping(new InputMapping { Name = "MoveRight" });
        manager.AddInputMapping(new InputMapping { Name = "Jump" });
        // Every other action name is deliberately NOT registered.

        var calledFor = new System.Collections.Generic.List<string>();
        var exception = Record.Exception(() =>
        {
            var state = AlundraPlayerController.ComputePadState(manager, name =>
            {
                calledFor.Add(name);
                return new ButtonState { IsKeyPressed = true };
            });

            Assert.Equal(AlundraPadState.Right | AlundraPadState.Cross, state.ButtonsHold);
        });

        Assert.Null(exception);
        Assert.Equal(new[] { "MoveRight", "Jump" }, calledFor);
    }

    [Fact]
    public void ComputePadState_JustPressedOnly_SetsOnlyJustPressedBit()
    {
        var manager = new InputMappingManager();
        manager.AddInputMapping(new InputMapping { Name = "Attack" });

        var state = AlundraPlayerController.ComputePadState(
            manager, _ => new ButtonState { IsKeyPressed = false, IsKeyJustPressed = true });

        Assert.Equal(0u, state.ButtonsHold);
        Assert.Equal(AlundraPadState.Square, state.ButtonsJustPressed);
    }

    [Fact]
    public void ComputePadState_NoMappingsRegistered_ReturnsZeroState_NeverThrows()
    {
        var manager = new InputMappingManager();

        var exception = Record.Exception(() =>
        {
            var state = AlundraPlayerController.ComputePadState(manager, _ => new ButtonState());
            Assert.Equal(0u, state.ButtonsHold);
            Assert.Equal(0u, state.ButtonsJustPressed);
        });

        Assert.Null(exception);
    }
}
