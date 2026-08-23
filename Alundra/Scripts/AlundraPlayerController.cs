#nullable enable
using System;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;

namespace Alundra.Scripts;

/// <summary>
/// One PSX pad snapshot subset (see <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PadState.cs</c>
/// for the full original bit layout), built once per frame by
/// <see cref="AlundraPlayerController.BuildPadState"/> from the converted "AlundraButtons"
/// <see cref="ButtonsMapping"/> (converter E2-A, <c>Data/Alundra.buttonsMapping</c>) and fed to
/// <see cref="AlundraPlayerManager.MovePlayer"/>. Only the bits the 9 mapped actions can ever reach
/// (Up/Right/Down/Left/Cross/Circle/Square/Triangle/Start) are ported here - L1/L2/R1/R2/Select have no
/// mapped action, so those original PadState bits are never produced by this struct.
/// </summary>
public readonly struct AlundraPadState
{
    public const uint Triangle = 0x0010;
    public const uint Circle = 0x0020;
    public const uint Cross = 0x0040;
    public const uint Square = 0x0080;
    public const uint Start = 0x0800;
    public const uint Up = 0x1000;
    public const uint Right = 0x2000;
    public const uint Down = 0x4000;
    public const uint Left = 0x8000;

    /// <summary>Buttons currently held, packed the same way as the original's own <c>ButtonsHold</c>.</summary>
    public uint ButtonsHold { get; init; }

    /// <summary>Buttons pressed THIS frame (not held last frame), packed like the original's own
    /// <c>ButtonsJustPressed</c>. Not yet consumed by <see cref="AlundraPlayerManager"/> (E2's own ported
    /// subset is direction-only), kept for the next opcodes/animations that will need it.</summary>
    public uint ButtonsJustPressed { get; init; }
}

/// <summary>
/// Local-player controller for the Alundra hero pawn (<c>Entities/Alundra/Alundra.entity</c>, catalog
/// name "AlundraPlayer" for the <c>.gameMode</c> asset that names this class - see
/// <c>docs/plan-conversion-totale.md</c> §4 E2). Resolved by simple type name via
/// <c>CasaEngine.Framework.Assets.ElementFactory.Create&lt;PlayerController&gt;</c> (needs a public
/// parameterless constructor - <see cref="PlayerController"/>/<see cref="Controller"/> already provide
/// one), constructed and made to <see cref="Controller.Possess"/> the engine-spawned pawn by
/// <c>World.InitializePlayerControllers</c>/<c>CreateLocalPlayerController</c>
/// (CasaEngineMonogame/CasaEngine/Framework/Scene/World/World.cs:282-367) BEFORE that pawn's own
/// <c>Entity.World</c> is set (<c>InternalAddEntities</c> only runs after the whole player-controller
/// loop) - so unlike a typical <c>OnPossess</c> override, this controller cannot reach the world/game from
/// there and does not try to. Instead <see cref="AlundraWorldProxy.InitializeWithWorld"/> (which DOES have
/// the world) drives input-mapping registration once via <see cref="EnsureInputMappingsRegistered"/>, and
/// each frame <see cref="AlundraEntityScriptProxy.Update"/>'s own player branch pulls a fresh
/// <see cref="AlundraPadState"/> from this controller via <see cref="BuildPadState"/> - see that class's
/// own doc for why nothing in the base engine ever calls <see cref="Controller"/>/<see cref="PlayerController"/>'s
/// own <c>Update</c> automatically (there is none to call).
/// </summary>
public sealed class AlundraPlayerController : PlayerController
{
    /// <summary>Catalog name of the converted <c>Data/Alundra.buttonsMapping</c> asset (converter E2-A).</summary>
    private const string ButtonsMappingCatalogName = "AlundraButtons";

    /// <summary>
    /// Action name (from <c>Data/Alundra.buttonsMapping</c>'s own 9 actions, converter E2-A) -&gt; the
    /// original PSX pad bit it feeds (see <see cref="AlundraPadState"/>'s own doc and
    /// <c>Alundra/Scripts/PlayerManager.cs:413/549/964/1904/430</c>, quoted by the converter's own E2-A
    /// note in <c>docs/plan-conversion-totale.md</c> §4 for the PSX-&gt;pad mapping this mirrors).
    /// </summary>
    private static readonly (string Action, uint Bit)[] ActionBits =
    {
        ("MoveUp", AlundraPadState.Up),
        ("MoveDown", AlundraPadState.Down),
        ("MoveLeft", AlundraPadState.Left),
        ("MoveRight", AlundraPadState.Right),
        ("Jump", AlundraPadState.Cross),
        ("Attack", AlundraPadState.Square),
        ("UseItem", AlundraPadState.Circle),
        ("Sprint", AlundraPadState.Triangle),
        ("Menu", AlundraPadState.Start),
    };

    /// <summary>
    /// Registers every "AlundraButtons" <see cref="InputMapping"/> onto <paramref name="game"/>'s
    /// <see cref="InputMappingManager"/>, once per game session - a fresh <see cref="AlundraPlayerController"/>
    /// is constructed on every world (re)load (<c>World.CreateLocalPlayerController</c>), but the
    /// underlying <see cref="InputComponent"/>/<see cref="InputMappingManager"/> lives on the game and
    /// survives across world reloads, so re-registering unconditionally would pile up duplicate mappings.
    /// Loads the "AlundraButtons" asset through <see cref="AssetContentManager"/> (the engine now
    /// registers an <c>AssetLoader&lt;ButtonsMapping&gt;</c> - CasaEngineMonogame commit fe19e1e6,
    /// previously a gap this method had to route around) and registers each of its 9 mappings
    /// individually, keyed by name, via <see cref="RegisterMappings"/> (which is what actually guards
    /// against duplicates, per-name - <see cref="InputMappingManager.Contains"/>, also added in fe19e1e6).
    /// No-op (logged once by <see cref="AlundraWorldProxy"/>'s own caller pattern - see that class'
    /// <c>InitializeWithWorld</c>) when <paramref name="game"/>/its <see cref="CasaEngineGame.InputComponent"/>
    /// is null (headless/editor-preview) or the "AlundraButtons" asset is missing from the catalog. If the
    /// asset load itself throws (e.g. an older, unmerged engine checkout without the fe19e1e6 loader
    /// registration), this logs a WARNING naming the asset and the exception TYPE and tells the user their
    /// standalone engine checkout needs rebuilding/merging - a silent swallow here would otherwise look
    /// exactly like "no mappings configured" instead of "the engine cannot load this asset kind yet".
    /// </summary>
    public static void EnsureInputMappingsRegistered(CasaEngineGame? game)
    {
        var inputMappingManager = game?.InputComponent?.InputMappingManager;
        if (inputMappingManager == null)
        {
            return;
        }

        var assetInfo = AssetCatalog.Get(ButtonsMappingCatalogName);
        if (assetInfo == null)
        {
            Logs.WriteWarning(
                $"AlundraPlayerController: no '{ButtonsMappingCatalogName}' asset in the catalog; "
                + "player input stays unmapped.");
            return;
        }

        ButtonsMapping? buttonsMapping;
        try
        {
            buttonsMapping = game!.AssetContentManager.Load<ButtonsMapping>(assetInfo.Id);
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"AlundraPlayerController: failed to load '{ButtonsMappingCatalogName}' ({assetInfo.Id}) - "
                + $"{ex.GetType().Name}: {ex.Message}. The engine (CasaEngineMonogame submodule) must be "
                + "rebuilt/merged to a version that registers AssetLoader<ButtonsMapping> (commit fe19e1e6 "
                + "or later) - run the launcher from the standalone engine checkout. Player input stays "
                + "unmapped.");
            return;
        }

        if (buttonsMapping == null)
        {
            Logs.WriteWarning(
                $"AlundraPlayerController: '{ButtonsMappingCatalogName}' loader returned null; "
                + "player input stays unmapped.");
            return;
        }

        RegisterMappings(buttonsMapping, inputMappingManager);
    }

    /// <summary>
    /// Registers each of <paramref name="buttonsMapping"/>'s own <see cref="InputMapping"/>s onto
    /// <paramref name="inputMappingManager"/>, one at a time, skipping any whose <c>Name</c> is already
    /// present per <see cref="InputMappingManager.Contains"/> - per-name
    /// idempotence (not a single first-action probe, which would have missed a manager left in a
    /// partially-registered state by e.g. a prior failed load). Internal and side-effecting on a real
    /// <see cref="InputMappingManager"/> only (no <see cref="CasaEngineGame"/> needed), so tests can call
    /// it directly against a fresh <see cref="InputMappingManager"/> instance.
    /// </summary>
    internal static void RegisterMappings(ButtonsMapping buttonsMapping, InputMappingManager inputMappingManager)
    {
        foreach (var inputMapping in buttonsMapping.Buttons)
        {
            if (!inputMappingManager.Contains(inputMapping.Name))
            {
                inputMappingManager.AddInputMapping(inputMapping);
            }
        }
    }

    /// <summary>
    /// Builds this frame's <see cref="AlundraPadState"/> by OR-ing in each of the 9 mapped actions' bit
    /// (<see cref="ActionBits"/>) whenever <see cref="PlayerController.Input"/>'s
    /// <see cref="PlayerInput.GetButtonState"/> reports it held/just-pressed. Returns the zero state
    /// (nothing held - a safe no-op input for <see cref="AlundraPlayerManager.MovePlayer"/>) when
    /// <see cref="PlayerController.Input"/> has not been wired yet (<c>World.CreateLocalPlayerController</c>
    /// sets it right after <see cref="Controller.Possess"/>, so this only matters for a controller
    /// constructed outside that path, e.g. a unit test), or when this controller's <see cref="Controller.Pawn"/>
    /// cannot reach an <see cref="InputMappingManager"/> yet (no <c>Entity.World</c>/<c>Game</c> - same
    /// shape). Each action whose name is not <see cref="InputMappingManager.Contains">registered</see> is
    /// skipped WITHOUT throwing (a plain <see cref="InputMappingManager.Contains"/> check, not a
    /// try/catch - this runs every frame) - see <see cref="ComputePadState"/> for the pure, testable core.
    /// </summary>
    public AlundraPadState BuildPadState()
    {
        if (Input == null)
        {
            return default;
        }

        var inputMappingManager = Pawn?.World?.Game?.InputComponent?.InputMappingManager;
        if (inputMappingManager == null)
        {
            return default;
        }

        return ComputePadState(inputMappingManager, Input.GetButtonState);
    }

    /// <summary>
    /// Pure core of <see cref="BuildPadState"/>, factored out for unit testing without a live
    /// <see cref="CasaEngineGame"/>/<see cref="PlayerInput"/>: OR-ing in each of the 9 mapped actions' bit
    /// (<see cref="ActionBits"/>) whenever <paramref name="inputMappingManager"/>
    /// <see cref="InputMappingManager.Contains">contains</see> that action's name AND
    /// <paramref name="getButtonState"/> reports it held/just-pressed. An action missing from
    /// <paramref name="inputMappingManager"/> is skipped entirely (never calls
    /// <paramref name="getButtonState"/> for it) - no exception is ever thrown by this method.
    /// </summary>
    internal static AlundraPadState ComputePadState(
        InputMappingManager inputMappingManager, Func<string, ButtonState> getButtonState)
    {
        uint hold = 0;
        uint justPressed = 0;

        foreach (var (action, bit) in ActionBits)
        {
            if (!inputMappingManager.Contains(action))
            {
                continue;
            }

            var state = getButtonState(action);

            if (state.IsKeyPressed)
            {
                hold |= bit;
            }

            if (state.IsKeyJustPressed)
            {
                justPressed |= bit;
            }
        }

        return new AlundraPadState { ButtonsHold = hold, ButtonsJustPressed = justPressed };
    }
}
