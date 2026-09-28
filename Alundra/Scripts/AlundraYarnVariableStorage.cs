#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Core.Logging;
using Yarn;

namespace Alundra.Scripts;

/// <summary>
/// E16.f T2 (docs/plan-e16-etat-partie.md, ADR-0010, ADR-0011): <c>Yarn.IVariableStorage</c> adossé aux
/// deux banques de drapeaux d'<see cref="AlundraGameState"/> - <c>$flag_n</c> lit/écrit le drapeau
/// <c>n</c> de <see cref="AlundraGameState.GameFlags"/>, <c>$tmp_flag_n</c> le drapeau <c>n</c> de
/// <see cref="AlundraGameState.TemporaryFlags"/> (D-E16-16), <c>n</c> décimal de 0 à 2047 sans zéro de
/// tête (D-E16-25). Ce stockage NE GARDE AUCUNE DONNÉE PROPRE (D-E16-15) : chaque lecture/écriture
/// retombe directement sur <see cref="AlundraGameState.GetFlag"/>/<see cref="AlundraGameState.AddFlag"/>/
/// <see cref="AlundraGameState.SetFlag"/>, exactement comme les opcodes <c>0x30</c>/<c>0x31</c>
/// (<c>AlundraEventProgramRunner.cs:1416</c>, test de bit) et <c>0x05</c>/<c>0x06</c>
/// (<c>AlundraEventProgramRunner.cs:449-457</c>, pose/efface le bit) le font déjà - un drapeau écrit
/// depuis Yarn est donc sauvegardé, ou non, exactement comme un drapeau écrit par un opcode.
///
/// Tout le reste (autre nom, variable interne de Yarn, <c>n</c> hors bornes ou mal écrit, une valeur
/// texte/nombre rangée sous un nom de drapeau, la lecture d'un nom refusé) est refusé sans exception et
/// sans changer d'état, journalisé une seule fois par nom (D-E16-18).
///
/// <see cref="Yarn.IVariableAccess.Program"/> et <see cref="Yarn.IVariableAccess.SmartVariableEvaluator"/>
/// sont de simples propriétés automatiques : ce stockage les garde telles que Yarn les pose, sans s'en
/// servir pour aucun drapeau (contrat item 8 ; E16.f T1 a mesuré que ni l'un ni l'autre n'intervient sur
/// un dialogue sans variable/fonction Yarn interne).
/// </summary>
public sealed class AlundraYarnVariableStorage : IVariableStorage
{
    private const string FlagPrefix = "$flag_";
    private const string TemporaryFlagPrefix = "$tmp_flag_";
    private const uint TemporaryFlagBit = 0x8000; // même bit que AlundraYarnBindings.HandleFlagCommand.
    private const int MaxFlagId = 2047; // D-E16-25 - les deux banques de 64 mots de l'original.

    private readonly AlundraGameState _gameState;
    private readonly HashSet<string> _loggedRefusedNames = new(StringComparer.Ordinal);

    public AlundraYarnVariableStorage(AlundraGameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        _gameState = gameState;
    }

    /// <summary>Gardée telle que Yarn la pose (contrat item 8) - ce stockage ne s'en sert pas.</summary>
    public Program? Program { get; set; }

    /// <summary>Gardée telle que Yarn la pose (contrat item 8) - ce stockage ne s'en sert pas.</summary>
    public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

    /// <summary>Contrat item 3 : un booléen, vrai si le bit du drapeau est posé. Pour tout <typeparamref
    /// name="T"/> auquel un booléen s'affecte (<c>bool</c>, <c>IConvertible</c> - le <c>T</c> demandé par
    /// la machine virtuelle pour lire une variable dans <c>&lt;&lt;if&gt;&gt;</c>, E16.f T1 - ou
    /// <c>object</c>) ; pour un autre <typeparamref name="T"/> ou un nom refusé, le refus est journalisé
    /// (contrat item 5, une valeur non booléenne ou la lecture d'un nom refusé) et rend faux, sans lever
    /// (comme le stockage refusant de T1 - E16.f T1, <c>RefusedRead_IsFalse_NoExceptionAndDialogueRunsToTheEnd</c>).</summary>
    public bool TryGetValue<T>(string variableName, out T? result)
    {
        if (!TryParseFlagName(variableName, out var flagId, out var bitIndex)
            || !CanAssignBoolTo<T>())
        {
            LogRefusedOnce(variableName);
            result = default;
            return false;
        }

        var value = (_gameState.GetFlag(flagId) & (1u << bitIndex)) != 0;
        result = (T)(object)value;
        return true;
    }

    /// <summary>Contrat item 6 : <c>Stored</c> pour un nom de drapeau valide, <c>Unknown</c> sinon.</summary>
    public VariableKind GetVariableKind(string variableName)
        => TryParseFlagName(variableName, out _, out _) ? VariableKind.Stored : VariableKind.Unknown;

    /// <summary>Une chaîne rangée sous n'importe quel nom est toujours refusée (contrat item 5, "une
    /// valeur texte ... rangée sous un nom de drapeau").</summary>
    public void SetValue(string variableName, string value) => LogRefusedOnce(variableName);

    /// <summary>Un nombre rangé sous n'importe quel nom est toujours refusé (contrat item 5, "une valeur
    /// ... nombre rangée sous un nom de drapeau").</summary>
    public void SetValue(string variableName, float value) => LogRefusedOnce(variableName);

    /// <summary>Contrat item 4 : <c>true</c> pose le bit comme l'opcode <c>0x05</c>
    /// (<c>AlundraEventProgramRunner.cs:449</c>, <c>AddFlag</c>) ; <c>false</c> ne l'efface que lui comme
    /// l'opcode <c>0x06</c> (<c>:457</c>, <c>SetFlag(flag, ~mask)</c>) - les autres bits du mot restent
    /// intacts. Refusé, sans écrire, si le nom n'est pas un nom de drapeau valide.</summary>
    public void SetValue(string variableName, bool value)
    {
        if (!TryParseFlagName(variableName, out var flagId, out var bitIndex))
        {
            LogRefusedOnce(variableName);
            return;
        }

        var mask = 1u << bitIndex;
        if (value)
        {
            _gameState.AddFlag(flagId, mask);
        }
        else
        {
            _gameState.SetFlag(flagId, ~mask);
        }
    }

    /// <summary>Contrat item 7 : le cycle de vie des deux banques reste à <see cref="AlundraGameState"/>
    /// (entrée de carte, nouvelle partie, chargement, D-E16-6) - cet appel ne les touche jamais, mais est
    /// journalisé (E16.f T1 a mesuré qu'un dialogue sans <c>visited()</c> ne l'appelle jamais).</summary>
    public void Clear()
        => Logs.WriteWarning("AlundraYarnVariableStorage: Clear() ignored - flag bank lifetime stays with AlundraGameState (D-E16-6).");

    /// <summary>Découpe <c>$flag_n</c>/<c>$tmp_flag_n</c> (D-E16-16) : <paramref name="n"/> doit être un
    /// décimal de 0 à <see cref="MaxFlagId"/> sans zéro de tête (D-E16-25), sinon le nom entier est
    /// refusé, y compris <c>$flag_</c>/<c>$tmp_flag_</c> tout court et toute variable interne de Yarn
    /// (<c>$Yarn.Internal.*</c>) - elles ne commencent par aucun des deux préfixes.</summary>
    private static bool TryParseFlagName(string variableName, out uint flagId, out int bitIndex)
    {
        flagId = 0;
        bitIndex = 0;

        if (string.IsNullOrEmpty(variableName))
        {
            return false;
        }

        string digits;
        bool isTemporary;
        if (variableName.StartsWith(TemporaryFlagPrefix, StringComparison.Ordinal))
        {
            digits = variableName[TemporaryFlagPrefix.Length..];
            isTemporary = true;
        }
        else if (variableName.StartsWith(FlagPrefix, StringComparison.Ordinal))
        {
            digits = variableName[FlagPrefix.Length..];
            isTemporary = false;
        }
        else
        {
            return false;
        }

        if (!IsDecimalWithoutLeadingZero(digits)
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            || n is < 0 or > MaxFlagId)
        {
            return false;
        }

        flagId = (uint)n | (isTemporary ? TemporaryFlagBit : 0u);
        bitIndex = n & 0x1f;
        return true;
    }

    private static bool IsDecimalWithoutLeadingZero(string digits)
    {
        if (digits.Length == 0 || (digits.Length > 1 && digits[0] == '0'))
        {
            return false;
        }

        foreach (var c in digits)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The <typeparamref name="T"/> a boolean flag value can be assigned to: <c>bool</c> itself,
    /// <c>IConvertible</c> (the actual <c>T</c> Yarn Spinner 3.2.1's virtual machine asks for reading a
    /// variable in <c>&lt;&lt;if&gt;&gt;</c>/<c>&lt;&lt;set&gt;&gt;</c>, E16.f T1), and <c>object</c>.</summary>
    private static bool CanAssignBoolTo<T>() => typeof(T) == typeof(bool) || typeof(T) == typeof(IConvertible) || typeof(T) == typeof(object);

    private void LogRefusedOnce(string variableName)
    {
        if (_loggedRefusedNames.Add(variableName))
        {
            Logs.WriteWarning($"AlundraYarnVariableStorage: refused Yarn variable '{variableName}' (D-E16-18).");
        }
    }
}
