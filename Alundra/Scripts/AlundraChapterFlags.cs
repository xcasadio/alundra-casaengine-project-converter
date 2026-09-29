#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// E16.c C1/C7 (docs/plan-e16-etat-partie.md, F8): port of <c>GetFirstEnabledFlagIndex</c> @ 0x800813B0 -
/// the story chapter the player is in, which <c>UpdateMenuStatusText</c> (0x80030FC8) stores in the save's
/// <c>CurrentFlagName</c>. The port writes it into the slot's <c>chapter</c> metadata instead
/// (<c>AlundraSaveGame.BuildMetadata</c>, C7), so the slot list reads it without decoding the save.
///
/// <para>The original walks <c>g_flagIdList</c>: records of 0x22 bytes (32 bytes of text, then a
/// <c>u16</c> flag id), whose first id sits at 0x8002962E (France disc). An id of 0 counts as set (it is
/// skipped), an id at or above <c>0x8000</c> ends the walk, and the walk stops on the first id whose flag
/// is clear. It returns 0 to 41. The 41 ids read in the binary are those of the analyser's
/// <c>ChapterFlags.cs:24-32</c> (alundra-datas-analyser AlundraTools/AlundraEngine), followed by the
/// <c>0xFFFF</c> terminator.</para>
///
/// <para>Only <c>g_saveData.GameFlags</c> is read: an id with the <c>0x8000</c> bit would select
/// <c>g_temporaryFlags</c>, but that same bit is the terminator test, which returns first. The highest id,
/// <c>0x6A2</c>, is in word 53, inside the 64 saved words; a word beyond the array given is read as clear,
/// so the walk never throws, whatever the array.</para>
///
/// <para>The record text starts with the chapter index as four ASCII digits ("0000", "0001", ...); the
/// original's loader uses only those digits. <see cref="FormatChapter"/> writes the same four digits. The
/// chapter's name and its display belong to E16.e.</para>
/// </summary>
public static class AlundraChapterFlags
{
    /// <summary>The walk's end: an id at or above this value stops it (0x800813B0).</summary>
    private const ushort Terminator = 0x8000;

    /// <summary>The flag closing each chapter, in story order, as read in <c>g_flagIdList</c> (0x8002962E,
    /// France disc); the <c>0xFFFF</c> terminator record that follows them is <see cref="Terminator"/>'s case
    /// and is not listed.</summary>
    private static readonly ushort[] ChapterFlagIds =
    [
        0x0003, 0x0008, 0x006C, 0x0676, 0x006D, 0x00EF, 0x0127, 0x00F7,
        0x00E9, 0x00E8, 0x015C, 0x012B, 0x028B, 0x014B, 0x0578, 0x0101,
        0x0579, 0x057A, 0x0372, 0x057B, 0x057C, 0x057D, 0x0385, 0x057E,
        0x0510, 0x0023, 0x001F, 0x0046, 0x0048, 0x004F, 0x037A, 0x01D3,
        0x0233, 0x06A2, 0x037B, 0x00E7, 0x044F, 0x03D0, 0x0664, 0x049F,
        0x04A0,
    ];

    /// <summary>The 41 chapter-closing flag ids, in story order.</summary>
    public static IReadOnlyList<ushort> FlagIds => ChapterFlagIds;

    /// <summary>The highest index <see cref="GetFirstEnabledFlagIndex"/> returns: every chapter flag set.</summary>
    public const int LastChapterIndex = 41;

    /// <summary>
    /// Port of <c>GetFirstEnabledFlagIndex</c> (0x800813B0): the index of the first chapter whose flag is
    /// clear in <paramref name="gameFlags"/>, or <see cref="LastChapterIndex"/> when every one is set.
    /// </summary>
    /// <param name="gameFlags">The persistent flag words (<c>GameFlags</c>): the 64 saved words or the
    /// session's 1024; <c>TemporaryFlags</c> is never passed.</param>
    public static int GetFirstEnabledFlagIndex(uint[] gameFlags)
    {
        ArgumentNullException.ThrowIfNull(gameFlags);

        for (var index = 0; index < ChapterFlagIds.Length; index++)
        {
            var flagId = ChapterFlagIds[index];
            if (flagId == 0)
            {
                continue; // 0x800813B0: an id of 0 counts as set.
            }

            if (flagId >= Terminator)
            {
                return index;
            }

            var word = flagId >> 5;
            if (word >= gameFlags.Length || (gameFlags[word] & (1u << (flagId & 0x1F))) == 0)
            {
                return index;
            }
        }

        return LastChapterIndex; // the terminator record.
    }

    /// <summary>The chapter index as the four ASCII digits that start the original's record text
    /// ("0000" to "0041"), the payload its loader reads back from <c>CurrentFlagName</c>.</summary>
    public static string FormatChapter(int chapterIndex)
    {
        return chapterIndex.ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
    }
}
