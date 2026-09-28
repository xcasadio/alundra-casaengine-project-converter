using Yarn;

namespace AlundraCasaEngineProjectConverter.Text;

/// <summary>
/// The seven Yarn functions of the E15.b function contract (docs/plan-e15-yarn.md, E15.b "Contrat des
/// fonctions", and ADR-0007). <see cref="YarnTextEmitter"/> emits calls to these functions (by name,
/// through the constants below) wherever the original's <c>\X</c> and <c>\V</c> codes read a value;
/// <see cref="CreateDeclarations"/> gives the Yarn compiler their name and delegate shape so those
/// calls type-check, with placeholder implementations that are never meant to run. The DLL registers
/// the real implementations at run time (E15.c), on a <see cref="Library"/> of its own built with a
/// <c>RegisterFunction</c> call per name below and the exact same delegate type.
/// </summary>
public static class AlundraYarnFunctions
{
    /// <summary><c>\X0</c> opening a page: the temporary falcon count <c>&lt;&lt;falcon_update&gt;&gt;</c> kept before updating it.</summary>
    public const string FalconTemp = "falcon_temp";

    /// <summary><c>\X1</c>: the falcon count after <c>&lt;&lt;falcon_update&gt;&gt;</c>.</summary>
    public const string Falcon = "falcon";

    /// <summary><c>\X2</c>/<c>\X4</c> opening a page: the item name of the category index kept before updating it.</summary>
    public const string CategoryItemNameBefore = "category_item_name_before";

    /// <summary><c>\X2</c>/<c>\X4</c> after another <c>\X</c> of the same page: the item name of the current category index.</summary>
    public const string CategoryItemName = "category_item_name";

    /// <summary><c>\X3</c>: the current category's threshold.</summary>
    public const string CategoryThreshold = "category_threshold";

    /// <summary><c>\X5</c>: the current category's threshold minus the falcon count.</summary>
    public const string CategoryRemaining = "category_remaining";

    /// <summary><c>\V&lt;n&gt;</c>: <c>INT_ARRAY_80191908[n]</c>.</summary>
    public const string GameVar = "game_var";

    /// <summary>
    /// A fresh <see cref="Library"/> declaring the seven functions above to the Yarn compiler
    /// (<c>CasaEngine.Compiler.Dialogue.YarnDialogueCompiler</c>), with the exact delegate shapes of
    /// the contract: <c>Func&lt;float&gt;</c> for <see cref="FalconTemp"/>, <see cref="Falcon"/>,
    /// <see cref="CategoryThreshold"/> and <see cref="CategoryRemaining"/>; <c>Func&lt;string&gt;</c>
    /// for <see cref="CategoryItemNameBefore"/> and <see cref="CategoryItemName"/>;
    /// <c>Func&lt;float, float&gt;</c> for <see cref="GameVar"/>. The implementations below are
    /// placeholders that let a script compile and, if ever run without the real registrations, return
    /// a harmless default; the DLL replaces them with its own registrations at run time (E15.c), never
    /// by reusing this library.
    /// </summary>
    public static Library CreateDeclarations()
    {
        var library = new Library();
        library.RegisterFunction<float>(FalconTemp, () => 0f);
        library.RegisterFunction<float>(Falcon, () => 0f);
        library.RegisterFunction<string>(CategoryItemNameBefore, () => string.Empty);
        library.RegisterFunction<string>(CategoryItemName, () => string.Empty);
        library.RegisterFunction<float>(CategoryThreshold, () => 0f);
        library.RegisterFunction<float>(CategoryRemaining, () => 0f);
        library.RegisterFunction<float, float>(GameVar, _ => 0f);
        return library;
    }
}
