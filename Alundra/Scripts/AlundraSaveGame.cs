#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// E16.c (docs/plan-e16-etat-partie.md, D-E16-4): Alundra's save-game object - the port of
/// <c>g_saveData</c> that the DLL hands to the engine's save-game service.
/// </summary>
public sealed class AlundraSaveGame
{
    /// <summary>The summary template <c>UpdateMenuStatusText</c> (0x80030FC8) starts from, read at 0x80022C38
    /// (F6): 30 characters.</summary>
    private const string SummaryTemplate = "  HP 00       TIME 00:00:00   ";

    /// <summary>
    /// E16.c C7 (F6, F7, D-E16-23): port of the <c>GameStateDescription</c> text <c>UpdateMenuStatusText</c>
    /// (0x80030FC8-0x8003132C) writes, <c>"  HP xx       TIME hh:mm:ss   "</c>. The original keeps the template
    /// of 0x80022C38 and overwrites:
    /// <list type="bullet">
    /// <item><description>characters 5-6 with the max HP, tens then ones, without any bound (F6);</description></item>
    /// <item><description>characters 19-20 with the hours, <c>t / 216000 % 100</c>;</description></item>
    /// <item><description>characters 22-23 with the minutes, <c>t / 3600 - 60 * (t / 216000)</c>;</description></item>
    /// <item><description>characters 25-26 with the seconds, <c>t / 60 - 60 * (t / 3600)</c>.</description></item>
    /// </list>
    /// <paramref name="gameTime"/> counts sixtieths of a second, as the binary divides (§2, Q3), not seconds as
    /// <c>GameEngine.cs:2724</c> reads it. The HP is the max HP: the original reads the hero's
    /// <c>g_entitySlots[0].HpMax</c>, which it keeps equal to <c>g_playerStats.HpMax</c>; this DLL has only the
    /// latter (F7). A max HP outside 0..99 (refused by the validation, and clamped to 0..50 by
    /// <see cref="AlundraPlayerManager.SetPlayerHpMax"/>) gives non-digit characters, as in the original, and
    /// never throws.
    /// </summary>
    public static string BuildSummary(short hpMax, uint gameTime)
    {
        var chars = SummaryTemplate.ToCharArray();

        WriteTwoDigits(chars, 5, hpMax);

        var hours = gameTime / 216000u % 100u;
        var minutes = gameTime / 3600u - 60u * (gameTime / 216000u);
        var seconds = gameTime / 60u - 60u * (gameTime / 3600u);
        WriteTwoDigits(chars, 19, (int)hours);
        WriteTwoDigits(chars, 22, (int)minutes);
        WriteTwoDigits(chars, 25, (int)seconds);

        return new string(chars);
    }

    private static void WriteTwoDigits(char[] chars, int index, int value)
    {
        chars[index] = unchecked((char)('0' + value / 10));
        chars[index + 1] = unchecked((char)('0' + value % 10));
    }
}
