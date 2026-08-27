using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 6 (E2 of docs/plan-conversion-totale.md - "the pawn is spawned by the engine"): the two
/// assets that let World.LoadContent spawn and drive the hero without any converter-side scripting -
/// Entities/Alundra/Alundra.gameMode (a PlayerStartupSettings) and Data/Alundra.buttonsMapping (a
/// ButtonsMapping). Both are catalogued so the DLL and the editor can find them by name, and both get
/// deterministic ids (Ids.For), which the World assets need to reference stably across runs.
///
/// Why raw JObject documents rather than PlayerStartupSettings/ButtonsMapping instances run through
/// EditorAssetWriterService.SaveAsset - the path SpriteWriter and every Phase-3 asset use. ObjectBase.Id
/// has a private setter (see CasaEngine.Framework.Common.ObjectBase); SaveAsset always serializes
/// whatever Id the object already picked in its constructor (Guid.NewGuid()), so an object built that
/// way can never carry a deterministic id - the same fact AudioWriter's class doc and WorldWriter's
/// shared-camera/world/entity nodes already document and work around by writing the JSON by hand. Both
/// documents below are still built to the exact shape the engine's own Load() reads (verified against
/// PlayerStartupSettings.Load / ButtonsMapping.Load / InputMapping.Load / KeyButton.Load and the
/// shipped CasaEngineMonogame/Projects/RPGDemo examples), and both go through
/// EditorAssetWriterService.SaveDocument, the same engine-owned writer SaveAsset itself calls after
/// serializing - only the "build the JObject" step happens here instead of inside the engine.
///
/// PSX to Xbox-pad button table (Alundra/Alundra.Tests/Scripts/PlayerManager.cs line references are to
/// the gameplay DLL's own copy, which reproduces the original's control scheme):
///  - Cross  (Jump)   -&gt; A       (PlayerManager.cs:413)
///  - Square (Attack)  -&gt; X       (PlayerManager.cs:549/964)
///  - Circle (UseItem) -&gt; B       (PlayerManager.cs:1904)
///  - Triangle (Sprint)-&gt; Y       (PlayerManager.cs:430)
/// D-pad and the Menu/Start binding have no PSX equivalent worth naming (Alundra has no analogue to a
/// "pause with Select/Start" beyond the one worth carrying over).
///
/// Only one alternative binding slot exists per InputMapping (key_button / alternative_key_button -
/// see InputMapping.Load), so the movement actions get the D-pad as their alternative rather than the
/// left thumbstick RPGDemo's own file happens to use: the brief's D-pad request is the more literal
/// match for a digital, 4-direction PSX d-pad, and the schema has no room for both.
/// </summary>
public static class PlayerSetupWriter
{
    private static readonly string GameModeEntitiesFolder = Path.Combine("Entities", "Alundra");
    private const string GameModeFileName = "Alundra" + Constants.FileNameExtensions.PlayerStartupSettings;
    private const string GameModeCatalogName = "AlundraPlayer";
    private const string PlayerControllerClassName = "AlundraPlayerController";

    private const string ButtonsMappingDataFolder = "Data";
    private const string ButtonsMappingFileName = "Alundra" + Constants.FileNameExtensions.ButtonsMapping;
    private const string ButtonsMappingCatalogName = "AlundraButtons";

    /// <summary>
    /// Writes Entities/Alundra/Alundra.gameMode, pointing at the hero prefab's own asset id
    /// (resolved from Phase 3's bank-key -&gt; prefab id map, key "alundra_0" - see
    /// EntityNameCatalogReader's class doc: Sector5Id 0 of the map_alundra.json bank is the hero,
    /// "Alundra"). player_controller_class names the gameplay DLL's AlundraPlayerController by simple
    /// name, resolved at runtime by ElementFactory the same way WorldScriptClassName is; ai_controller
    /// and hud stay at PlayerStartupSettings' own defaults (AIController, no HUD).
    /// </summary>
    public static Guid WriteGameMode(
        string outputDirectory, IReadOnlyDictionary<string, Guid> prefabAssetIdsByBankKey, ConversionReport report)
    {
        if (!prefabAssetIdsByBankKey.TryGetValue("alundra_0", out var heroEntityAssetId))
        {
            report.Errors.Add(
                "PlayerSetup: hero prefab 'alundra_0' (Entities/Alundra/Alundra.entity) not found among "
                + "Phase 3's converted banks; Alundra.gameMode was not written.");
            return Guid.Empty;
        }

        var assetId = Ids.For("gameMode:alundra");
        var relativePath = Path.Combine(GameModeEntitiesFolder, GameModeFileName);

        Directory.CreateDirectory(Path.Combine(outputDirectory, GameModeEntitiesFolder));

        var gameModeNode = new JObject
        {
            ["id"] = assetId.ToString(),
            ["name"] = GameModeCatalogName,
            ["default_pawn_asset_id"] = heroEntityAssetId.ToString(),
            ["player_controller_class"] = PlayerControllerClassName,
            // PlayerStartupSettings.Load reads "hud_class" when present, otherwise falls back to
            // this exact (misspelled) key - see RpgGameMode.gameMode, the only other .gameMode this
            // engine ships, which carries the same "hud_classClass": null. ai_controller_class is
            // left out entirely: Load only reads it when the key exists, defaulting to AIController.
            ["hud_classClass"] = null,
        };

        EditorAssetWriterService.SaveDocument(relativePath, gameModeNode);
        EditorAssetCatalogService.Add(new AssetInfo(assetId)
        {
            Name = GameModeCatalogName,
            FileName = relativePath,
        });

        report.Increment("PlayerSetup.GameModes");
        return assetId;
    }

    /// <summary>
    /// Writes Data/Alundra.buttonsMapping: 9 digital actions, keyboard + one gamepad alternative
    /// each (see the class doc for the PSX-&gt;pad table and the single-alternative-slot choice).
    /// GamePadNumber "One", Invert false and DeadZone 0.75 are the engine's own InputMapping
    /// defaults, kept explicit here since KeyButton/InputMapping.Load read every field
    /// unconditionally (see RPGDemo's buttonsMapping.buttonsMapping).
    /// </summary>
    public static Guid WriteButtonsMapping(string outputDirectory, ConversionReport report)
    {
        var assetId = Ids.For("buttonsMapping:alundra");
        var relativePath = Path.Combine(ButtonsMappingDataFolder, ButtonsMappingFileName);

        Directory.CreateDirectory(Path.Combine(outputDirectory, ButtonsMappingDataFolder));

        var buttonsArray = new JArray
        {
            BuildDigitalBinding("MoveUp", "Up", "DPadUp"),
            BuildDigitalBinding("MoveDown", "Down", "DPadDown"),
            BuildDigitalBinding("MoveLeft", "Left", "DPadLeft"),
            BuildDigitalBinding("MoveRight", "Right", "DPadRight"),
            // Left stick, as four SEPARATE actions rather than extra slots on the four above: an
            // InputMapping's ButtonBehavior is either DigitalInput or AnalogInput, never both
            // (InputMapping.Update), so a digital D-pad binding cannot also carry an axis. The DLL ORs
            // these into the very same PSX pad bits as the D-pad (AlundraPlayerController.ActionBits),
            // so the ported game logic still sees one ButtonsHold and never learns the difference.
            BuildLeftStickBinding("MoveUpStick", "LeftStickY", invert: false),
            BuildLeftStickBinding("MoveDownStick", "LeftStickY", invert: true),
            BuildLeftStickBinding("MoveRightStick", "LeftStickX", invert: false),
            BuildLeftStickBinding("MoveLeftStick", "LeftStickX", invert: true),
            // PSX Cross (PlayerManager.cs:413).
            BuildDigitalBinding("Jump", "Space", "A"),
            // PSX Square (PlayerManager.cs:549/964).
            BuildDigitalBinding("Attack", "X", "X"),
            // PSX Circle (PlayerManager.cs:1904).
            BuildDigitalBinding("UseItem", "C", "B"),
            // PSX Triangle (PlayerManager.cs:430).
            BuildDigitalBinding("Sprint", "LeftShift", "Y"),
            BuildDigitalBinding("Menu", "Escape", "Start"),
        };

        var buttonsMappingNode = new JObject
        {
            ["id"] = assetId.ToString(),
            ["name"] = ButtonsMappingCatalogName,
            ["buttons"] = buttonsArray,
        };

        EditorAssetWriterService.SaveDocument(relativePath, buttonsMappingNode);
        EditorAssetCatalogService.Add(new AssetInfo(assetId)
        {
            Name = ButtonsMappingCatalogName,
            FileName = relativePath,
        });

        report.Increment("PlayerSetup.ButtonsMappings");
        report.Increment("PlayerSetup.ButtonsMappingActions", buttonsArray.Count);
        return assetId;
    }

    /// <summary>
    /// One InputMapping: a keyboard key as the primary binding, a gamepad button as the alternative -
    /// the only two binding slots InputMapping.Load reads (key_button / alternative_key_button).
    /// AnalogAxis/MouseButton are unused by DigitalInput bindings (see InputMapping.Update) but
    /// Load reads them unconditionally, so they carry the same placeholder values RPGDemo's file
    /// does (MouseX / LeftButton) rather than an invented "None" the engine has never actually shipped.
    /// </summary>
    private static JObject BuildDigitalBinding(string actionName, string key, string gamePadButton)
    {
        return new JObject
        {
            ["id"] = Ids.For($"buttonsMapping:alundra:{actionName}").ToString(),
            ["name"] = actionName,
            ["game_pad_number"] = "One",
            ["button_behavior"] = "DigitalInput",
            ["analog_axis"] = "MouseX",
            ["invert"] = false,
            ["dead_zone"] = 0.75f,
            ["key_button"] = new JObject
            {
                ["key"] = key,
                ["game_pad_button"] = "None",
                ["mouse_button"] = "LeftButton",
                ["input_device"] = "Keyboard",
            },
            ["alternative_key_button"] = new JObject
            {
                ["key"] = "None",
                ["game_pad_button"] = gamePadButton,
                ["mouse_button"] = "LeftButton",
                ["input_device"] = "GamePad",
            },
        };
    }

    /// <summary>
    /// One half-axis of the left stick, as an AnalogInput mapping: InputMapping.Update reads the axis,
    /// negates it when <paramref name="invert"/> is set, and reports Pressed once the result reaches
    /// DeadZone - so one axis needs two mappings, one per direction. MonoGame's LeftStickY is positive
    /// UP, hence up = not inverted and down = inverted.
    ///
    /// <para><see cref="LeftStickDeadZone"/> is deliberately NOT the 0.75 the digital bindings carry as a
    /// placeholder: a real stick's gate is circular, so at 45 degrees each axis only reaches about 0.707
    /// and a 0.75 threshold would make every DIAGONAL physically unreachable while the four cardinals
    /// still worked. 0.5 clears both axes on a diagonal while still requiring a deliberate push.</para>
    /// </summary>
    private static JObject BuildLeftStickBinding(string actionName, string analogAxis, bool invert)
    {
        return new JObject
        {
            ["id"] = Ids.For($"buttonsMapping:alundra:{actionName}").ToString(),
            ["name"] = actionName,
            ["game_pad_number"] = "One",
            ["button_behavior"] = "AnalogInput",
            ["analog_axis"] = analogAxis,
            ["invert"] = invert,
            ["dead_zone"] = LeftStickDeadZone,
            // Unused by an AnalogInput mapping (InputMapping.Update reads the axis, never these), but
            // InputMapping.Load reads both nodes unconditionally, so they carry the same inert
            // placeholders the digital bindings use rather than a shape the engine has never shipped.
            ["key_button"] = new JObject
            {
                ["key"] = "None",
                ["game_pad_button"] = "None",
                ["mouse_button"] = "LeftButton",
                ["input_device"] = "NoDevice",
            },
            ["alternative_key_button"] = new JObject
            {
                ["key"] = "None",
                ["game_pad_button"] = "None",
                ["mouse_button"] = "LeftButton",
                ["input_device"] = "NoDevice",
            },
        };
    }

    /// <summary>See <see cref="BuildLeftStickBinding"/> on why this is 0.5 and not the digital
    /// bindings' own 0.75 placeholder.</summary>
    private const float LeftStickDeadZone = 0.5f;
}
