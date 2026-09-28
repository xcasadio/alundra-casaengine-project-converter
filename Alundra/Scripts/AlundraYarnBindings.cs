#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Dialogue.Yarn;
using Yarn;

namespace Alundra.Scripts;

/// <summary>
/// E15.c T4 (docs/plan-e15-yarn.md, E15.c contract item 3; ADR-0007): registers on a
/// <see cref="YarnDialogueRunner"/> the two commands and seven functions of the E15.b function
/// contract, so pages emitted with <c>&lt;&lt;flag n&gt;&gt;</c>, <c>&lt;&lt;falcon_update&gt;&gt;</c>
/// and the seven <c>{...}</c> reads compiled by the converter run against this DLL's own
/// <see cref="AlundraGameState"/>/<see cref="AlundraTextProgress"/> port instead of the placeholder
/// implementations the converter's own <c>AlundraYarnFunctions</c> class registers for its compile-time
/// checks (that class lives in the converter project, which this DLL cannot reference - the names and
/// delegate types below MUST stay identical to it; a mismatch would compile a Yarn script the DLL then
/// fails to run, or vice versa).
/// </summary>
public sealed class AlundraYarnBindings
{
    // -----------------------------------------------------------------------------------------
    // Names and delegate shapes of the E15.b function contract (docs/plan-e15-yarn.md, E15.b
    // "Contrat des fonctions"; ADR-0007 for the two category_item_name_* functions). MUST match
    // AlundraCasaEngineProjectConverter.Text.AlundraYarnFunctions in the converter project exactly -
    // same string values, same delegate arities and types on each RegisterFunction call below.
    // -----------------------------------------------------------------------------------------

    /// <summary><c>flag n</c>: same write as <c>AlundraDialogueDirector.ShowCurrentPage</c> does today
    /// for a page's numeric control codes (AlundraDialogueDirector.cs ~:290-300).</summary>
    public const string FlagCommandName = "flag";

    /// <summary><c>falcon_update</c>: keeps the state it is about to replace (ADR-0007), then runs
    /// <see cref="AlundraTextProgress.UpdateNumberOfFalcon"/> and
    /// <see cref="AlundraTextProgress.UpdatePlayerProgressState"/>.</summary>
    public const string FalconUpdateCommandName = "falcon_update";

    /// <summary><c>\X0</c> opening a page: the temporary falcon count kept before <c>falcon_update</c>.</summary>
    public const string FalconTempFunctionName = "falcon_temp";

    /// <summary><c>\X1</c>: the falcon count after <c>falcon_update</c>.</summary>
    public const string FalconFunctionName = "falcon";

    /// <summary><c>\X2</c>/<c>\X4</c> opening a page: the item name of the category index kept before
    /// <c>falcon_update</c>.</summary>
    public const string CategoryItemNameBeforeFunctionName = "category_item_name_before";

    /// <summary><c>\X2</c>/<c>\X4</c> after another <c>\X</c> of the same page: the item name of the
    /// current category index.</summary>
    public const string CategoryItemNameFunctionName = "category_item_name";

    /// <summary><c>\X3</c>: the current category's threshold.</summary>
    public const string CategoryThresholdFunctionName = "category_threshold";

    /// <summary><c>\X5</c>: the current category's threshold minus the falcon count.</summary>
    public const string CategoryRemainingFunctionName = "category_remaining";

    /// <summary><c>\V&lt;n&gt;</c>: <see cref="AlundraGameState.GameVariables"/>[n].</summary>
    public const string GameVarFunctionName = "game_var";

    private readonly AlundraGameState _gameState;
    private readonly string _projectPath;
    private readonly HashSet<string> _loggedUnhandledCommandNames = new(StringComparer.Ordinal);
    private bool _loggedInvalidFlagArgumentOnce;
    private bool _loggedMissingItemNameOnce;
    private bool _loggedGameVarOutOfRangeOnce;

    /// <summary>The state <c>falcon_update</c> is about to replace, kept for
    /// <see cref="FalconTempFunctionName"/> and <see cref="CategoryItemNameBeforeFunctionName"/>
    /// (ADR-0007). Both start at 0 - the same "never ran yet" value <c>falcon_temp()</c>/
    /// <c>category_item_name_before()</c> should report for a page that reads them before any
    /// <c>falcon_update</c> has ever run in this session - and are only ever taken from the live
    /// <see cref="AlundraGameState"/> by <see cref="HandleFalconUpdateCommand"/>'s own snapshot, at the
    /// moment each <c>falcon_update</c> actually runs (never here at construction time, since the game
    /// state can still change between binding construction and that first call).</summary>
    private int _keptFalconTemp;
    private int _keptCategoryIndex;

    public AlundraYarnBindings(AlundraGameState gameState)
        : this(gameState, EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Overload tests use to point item-name resolution at a fixture project directory instead
    /// of <see cref="EngineEnvironment.ProjectPath"/> (same pattern as <see cref="AlundraItemTables"/>).</summary>
    public AlundraYarnBindings(AlundraGameState gameState, string projectPath)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(projectPath);

        _gameState = gameState;
        _projectPath = projectPath;
    }

    /// <summary>Registers every command and function above on <paramref name="runner"/>, plus a handler
    /// that logs each unknown command name once and never throws.</summary>
    public void Register(YarnDialogueRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        runner.AddCommandHandler(FlagCommandName, HandleFlagCommand);
        runner.AddCommandHandler(FalconUpdateCommandName, HandleFalconUpdateCommand);

        runner.RegisterFunction(FalconTempFunctionName, (Func<float>)(() => _keptFalconTemp));
        runner.RegisterFunction(FalconFunctionName, (Func<float>)(() => _gameState.PlayerStats.Falcon));
        runner.RegisterFunction(CategoryItemNameBeforeFunctionName, (Func<string>)(() => ResolveCategoryItemName(_keptCategoryIndex)));
        runner.RegisterFunction(CategoryItemNameFunctionName, (Func<string>)(() => ResolveCategoryItemName(_gameState.TextCategoryIndex)));
        runner.RegisterFunction(CategoryThresholdFunctionName, (Func<float>)(() => AlundraTextProgress.CategoryThresholds[_gameState.TextCategoryIndex]));
        runner.RegisterFunction(CategoryRemainingFunctionName, (Func<float>)(() =>
            AlundraTextProgress.CategoryThresholds[_gameState.TextCategoryIndex] - _gameState.PlayerStats.Falcon));
        runner.RegisterFunction(GameVarFunctionName, (Func<float, float>)(n => ResolveGameVar((int)n)));

        runner.UnhandledCommand += OnUnhandledCommand;
    }

    /// <summary>
    /// A fresh <see cref="Library"/> declaring the two commands and seven functions above to
    /// <c>CasaEngine.Compiler.Dialogue.YarnDialogueCompiler</c>, for tests that compile Yarn fixtures
    /// without a real runner attached yet (same role as the converter's own
    /// <c>AlundraYarnFunctions.CreateDeclarations</c>, kept here so <c>Alundra.Tests</c> does not
    /// reference the converter project).
    /// </summary>
    public static Library CreateDeclarations()
    {
        var library = new Library();
        library.RegisterFunction<float>(FalconTempFunctionName, () => 0f);
        library.RegisterFunction<float>(FalconFunctionName, () => 0f);
        library.RegisterFunction<string>(CategoryItemNameBeforeFunctionName, () => string.Empty);
        library.RegisterFunction<string>(CategoryItemNameFunctionName, () => string.Empty);
        library.RegisterFunction<float>(CategoryThresholdFunctionName, () => 0f);
        library.RegisterFunction<float>(CategoryRemainingFunctionName, () => 0f);
        library.RegisterFunction<float, float>(GameVarFunctionName, _ => 0f);
        return library;
    }

    /// <summary>Port of the write <c>AlundraDialogueDirector.ShowCurrentPage</c> already does for a
    /// page's numeric control codes (AlundraDialogueDirector.cs ~:290-300):
    /// <c>AddFlag(n | 0x8000, 1 &lt;&lt; (n &amp; 0x1f))</c>.</summary>
    private void HandleFlagCommand(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 1
            || !double.TryParse(arguments[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rawValue))
        {
            LogInvalidFlagArgumentOnce(arguments.Count > 0 ? arguments[0] : string.Empty);
            return;
        }

        var n = (uint)rawValue;
        _gameState.AddFlag(n | 0x8000, 1u << (int)(n & 0x1f));
    }

    /// <summary>ADR-0007: keeps the state <c>falcon_update</c> is about to replace FIRST, then runs the
    /// two ported updates in the order the original calls them from its four <c>\X</c> sites.</summary>
    private void HandleFalconUpdateCommand(IReadOnlyList<string> arguments)
    {
        _keptFalconTemp = _gameState.PlayerStats.FalconTemp;
        _keptCategoryIndex = _gameState.TextCategoryIndex;

        AlundraTextProgress.UpdateNumberOfFalcon(_gameState);
        AlundraTextProgress.UpdatePlayerProgressState(_gameState);
    }

    /// <summary>ADR-0007/§5.7: the item name for a category index, through
    /// <see cref="AlundraTextProgress.RewardItemByCategory"/> and the DLL's existing item-name API
    /// (never a raw-file reader of its own) - reward item id -&gt; ETC entry <c>0x200 + id</c>.</summary>
    private string ResolveCategoryItemName(int categoryIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= AlundraTextProgress.RewardItemByCategory.Length)
        {
            LogMissingItemNameOnce($"category index {categoryIndex} out of range");
            return string.Empty;
        }

        var rewardItemId = AlundraTextProgress.RewardItemByCategory[categoryIndex];
        if (!AlundraEtcStringTable.TryResolveItemName(_projectPath, rewardItemId, out var name))
        {
            LogMissingItemNameOnce($"no name for reward item 0x{rewardItemId:X}");
            return string.Empty;
        }

        return name;
    }

    /// <summary>§5.7: <c>\V</c> reads <see cref="AlundraGameState.GameVariables"/> with NO bound check in
    /// the original (<c>c - '0'</c>) - this port stays in bounds and degrades instead of throwing.</summary>
    private float ResolveGameVar(int index)
    {
        if (index < 0 || index >= _gameState.GameVariables.Length)
        {
            LogGameVarOutOfRangeOnce(index);
            return 0f;
        }

        return _gameState.GameVariables[index];
    }

    private void OnUnhandledCommand(object? sender, UnhandledYarnCommandEventArgs e)
    {
        if (_loggedUnhandledCommandNames.Add(e.Name))
        {
            Logs.WriteWarning($"AlundraYarnBindings: no handler registered for Yarn command '{e.Name}'.");
        }
    }

    private void LogInvalidFlagArgumentOnce(string argument)
    {
        if (_loggedInvalidFlagArgumentOnce)
        {
            return;
        }

        _loggedInvalidFlagArgumentOnce = true;
        Logs.WriteWarning($"AlundraYarnBindings: 'flag' command received a non-numeric argument '{argument}'.");
    }

    private void LogMissingItemNameOnce(string reason)
    {
        if (_loggedMissingItemNameOnce)
        {
            return;
        }

        _loggedMissingItemNameOnce = true;
        Logs.WriteWarning($"AlundraYarnBindings: could not resolve a category reward item name ({reason}).");
    }

    private void LogGameVarOutOfRangeOnce(int index)
    {
        if (_loggedGameVarOutOfRangeOnce)
        {
            return;
        }

        _loggedGameVarOutOfRangeOnce = true;
        Logs.WriteWarning($"AlundraYarnBindings: 'game_var({index})' is out of range of GameVariables.");
    }
}
