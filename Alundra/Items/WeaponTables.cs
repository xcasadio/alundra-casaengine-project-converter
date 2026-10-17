#nullable enable
using Alundra.Entities;
using Alundra.Hero;

namespace Alundra.Items;

/// <summary>
/// E20.b B1 (docs/plan-e20-heros.md section 1.4, D-E20-7): the three weapon tables of <c>ALUN_CD.EXE</c> (France), copied byte for byte as data, each with its address, like
/// <see cref="AnimationTables"/> does for its own tables. The whole table of weapons is here, although E20.b only plays the animations of the sword: the charge, the flail, the wands and
/// the bow read the same records in the later slices (E20.f). Verified against the executable (text at <c>0x80020000</c>, file offset <c>0x800 + address - 0x80020000</c>).
/// <list type="bullet">
/// <item><description><see cref="RecordIndexOfItem"/>: <c>0x80028C14</c>, 100 bytes, one per item, the index of the weapon record (every item that is not in the list below, and the
/// "no item" answer <c>-1</c> read at <c>0x80028C13</c>, give record 0, the basic sword's).</description></item>
/// <item><description><see cref="Records"/>: <c>0x80028C78</c>, 12 records of 13 bytes: [chargeable] [ground animation, type, delay, z] [air ...] [charge ...]. A record pointer such as the
/// one <c>PlayerTryAttack</c> stores in <c>0x80127110</c> points at an animation byte (<c>0x80028C79 + 13 w</c>, plus 4 in the air), so <c>ptr[1]</c> is the type, <c>ptr[2]</c> the
/// delay of the spawn and <c>ptr[3]</c> its height.</description></item>
/// <item><description><see cref="AnimationFlags"/>: <c>0x80028D14</c>, two bytes per animation, <c>0x50</c> animations: [the animation counts the charge] [the animation spawns its
/// projectile after the delay of the record].</description></item>
/// </list>
/// </summary>
internal static class AlundraWeaponTables
{
    /// <summary>Address of <see cref="RecordIndexOfItem"/> in the executable.</summary>
    public const uint ItemToRecordAddress = 0x80028C14;

    /// <summary>Address of the first record; the pointers of <c>0x80127110</c> are <c>RecordsAddress + 1 + 13 w</c> (+ 4 in the air).</summary>
    public const uint RecordsAddress = 0x80028C78;

    /// <summary>Address of <see cref="AnimationFlags"/> in the executable.</summary>
    public const uint AnimationFlagsAddress = 0x80028D14;

    /// <summary>The size of a weapon record, in bytes.</summary>
    public const int RecordSize = 13;

    /// <summary>The number of weapon records.</summary>
    public const int RecordCount = 12;

    /// <summary>Offset, in a record, of the ground animation byte (the byte 0 is the chargeable flag); the air one is 4 further, the charge one 8.</summary>
    public const int GroundAnimationOffset = 1;

    /// <summary>Offset from the ground animation byte to the air one.</summary>
    public const int AirOffset = 4;

    /// <summary>The animations the table of flags covers (<c>0x50</c>, as the dispatch of <c>MovePlayer</c> does).</summary>
    public const int AnimationCount = 0x50;

    /// <summary><c>0x80028C14</c>: the weapon record of each item (100 bytes). Items 2 to 10, 14 and 16 have their own; the others read 0.</summary>
    private static readonly byte[] ItemToRecord = BuildItemToRecord();

    /// <summary>
    /// <c>0x80028C78</c>: the 12 records, 156 bytes. Hexadecimal, as the executable holds them:
    /// by the item map above: w0 basic sword (item 1), w1 Steel Edge (2), w2 Demon Buster (3), w3 Holy Sword (4), w4 Fireball Rod (16), w5 Drifting Ice Rod (14), w6 Chain Throw (9), w7 Crusher (10),
    /// w8 Hunter's Bow (5), w9 William's Bow (6), w10 Magic Staff (7: all zero, the "no animation" record, a press only plays the sound 3), w11 item 8 (the same animations as w0).
    /// </summary>
    private static readonly byte[] Records =
    {
        0x00, 0x10, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x3F, 0x00, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00, 0x18, 0x1B, 0x14, 0x00,
        0x01, 0x44, 0x00, 0x00, 0x00, 0x46, 0x00, 0x00, 0x00, 0x18, 0x1C, 0x14, 0x00,
        0x01, 0x49, 0x00, 0x00, 0x00, 0x4B, 0x00, 0x00, 0x00, 0x18, 0x1D, 0x14, 0x00,
        0x01, 0x11, 0x0B, 0x0A, 0x20, 0x15, 0x0B, 0x0A, 0x20, 0x11, 0x0C, 0x0A, 0x20,
        0x01, 0x11, 0x0E, 0x0A, 0x20, 0x15, 0x0E, 0x0A, 0x20, 0x11, 0x0F, 0x0A, 0x20,
        0x00, 0x12, 0x00, 0x00, 0x00, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x40, 0x00, 0x00, 0x00, 0x42, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x00,
        0x00, 0x13, 0x03, 0x08, 0x10, 0x17, 0x03, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x13, 0x04, 0x08, 0x10, 0x17, 0x04, 0x08, 0x10, 0x13, 0x07, 0x08, 0x08,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x10, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    /// <summary>
    /// <c>0x80028D14</c>: two bytes per animation. Byte 0 (the animation counts the charge): animations 0 to 4, 0x0E, 0x29 to 0x2D, 0x35, 0x3E. Byte 1 (the animation spawns its projectile once
    /// the counter reaches the delay of the record): animations 0x11, 0x13, 0x15, 0x17, 0x18.
    /// </summary>
    private static readonly byte[] Flags = BuildFlags();

    private static byte[] BuildItemToRecord()
    {
        var table = new byte[100];
        table[2] = 1;
        table[3] = 2;
        table[4] = 3;
        table[5] = 8;
        table[6] = 9;
        table[7] = 10;
        table[8] = 11;
        table[9] = 6;
        table[10] = 7;
        table[14] = 5;
        table[16] = 4;
        return table;
    }

    private static byte[] BuildFlags()
    {
        var table = new byte[AnimationCount * 2];
        foreach (var animation in new[] { 0, 1, 2, 3, 4, 0x0E, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x35, 0x3E })
        {
            table[animation * 2] = 1;
        }

        foreach (var animation in new[] { 0x11, 0x13, 0x15, 0x17, 0x18 })
        {
            table[animation * 2 + 1] = 1;
        }

        return table;
    }

    /// <summary>
    /// The weapon record of an item, as <c>MovePlayer</c> resolves it at its head (<c>0x80031B70-0x80031B8C</c>: <c>byte[0x80028C14 + itemId]</c>). <paramref name="itemId"/> is the answer of
    /// <see cref="PlayerManager.GetItemIdFromCurrentWeapon"/>: <see cref="PlayerManager.NoItem"/> (-1) reads the byte before the table (<c>0x80028C13</c>, zero) and gives record 0.
    /// </summary>
    public static int RecordIndexOfItem(uint itemId) => itemId < (uint)ItemToRecord.Length ? ItemToRecord[itemId] : 0;

    /// <summary>The address of the ground (<paramref name="air"/> false) or air animation byte of record <paramref name="record"/>, the value <c>PlayerTryAttack</c> stores in <c>0x80127110</c>.</summary>
    public static uint AnimationAddress(int record, bool air)
        => RecordsAddress + GroundAnimationOffset + (uint)(RecordSize * record) + (air ? (uint)AirOffset : 0u);

    /// <summary>Offset from the ground animation byte to the charged one (E20.f1a: the animation a released charge plays; <c>0x8002EC74-0x8002EC80</c>: <c>0x80028C79 + 13 w + 8</c>).</summary>
    public const int ChargeOffset = 8;

    /// <summary>The address of the charged animation byte of record <paramref name="record"/>, the value a released charge stores in <c>0x80127110</c> (<c>0x8002EC84</c>).</summary>
    public static uint ChargeAnimationAddress(int record)
        => RecordsAddress + GroundAnimationOffset + (uint)(RecordSize * record) + ChargeOffset;

    /// <summary>One byte of the records at <paramref name="address"/> (an address of the executable, such as the pointer of <c>0x80127110</c> plus 1, 2 or 3).</summary>
    public static byte RecordByte(uint address) => Records[address - RecordsAddress];

    /// <summary>The first flag of the animation (<c>0x80028D14 + 2 a</c>): the animation counts the charge (E20.f). False past the table.</summary>
    public static bool AnimationCountsCharge(uint animationId) => animationId < AnimationCount && Flags[animationId * 2] != 0;

    /// <summary>
    /// The second flag of the animation (<c>0x80028D14 + 2 a + 1</c>): the animation spawns its projectile after the delay of the record (<c>UpdateWeaponStepProgression</c>). False past the table
    /// (the binary would read the bytes that follow the table; only an animation of the table can follow an attack, since the pointer it reads is cleared at the tick after the press unless the
    /// record type is non-zero, and the records with a type are the bows and wands, whose animations are all in the table).
    /// </summary>
    public static bool AnimationSpawnsAfterDelay(uint animationId) => animationId < AnimationCount && Flags[animationId * 2 + 1] != 0;
}
