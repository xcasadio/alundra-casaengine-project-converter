#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.SaveGames;

namespace Alundra.Scripts;

/// <summary>
/// E16.c (docs/plan-e16-etat-partie.md, D-E16-4): Alundra's save-game object - the port of <c>g_saveData</c>
/// (0x801EB2E8, 0x758 bytes, §2 Q3) that the DLL hands to the engine's save-game service
/// (<see cref="ISaveGameData"/>, engine ADR-0044).
///
/// <para><b>Content (C2).</b> The fields of <c>g_saveData</c>, in its order, without <c>SlotData</c>,
/// <c>LastMapId</c> and <c>Field_757</c> (no reader, §2 Q3), <c>CurrentFlagName</c> and
/// <c>GameStateDescription</c> (in the slot's metadata instead, <see cref="BuildMetadata"/>) and the port-only
/// <c>Offset</c>. <see cref="GameFlags"/> holds the original's 64 words (no persistent flag id reaches 2048,
/// §2 Q1). Not saved, as in the original: <c>TemporaryFlags</c>, <c>TextCategoryIndex</c>,
/// <c>GameVariables</c> (outside <c>g_saveData</c>, §0.2), and nothing Yarn-specific (the Yarn variables are
/// the flags, D-E16-15). The types are the DLL's; the nine stats are flat fields here, filed under a
/// <c>playerStats</c> object by <see cref="Serialize"/>.</para>
///
/// <para><b>Own arrays (C2, SC4).</b> The three arrays are <c>readonly</c> fields allocated at their length,
/// never shared with <see cref="AlundraGameState"/>: the service fills THIS object's arrays when loading, the
/// live state is only touched by <see cref="ApplyTo"/> (E16.d), and neither the validation nor
/// <see cref="ApplyTo"/> can meet a null or wrongly sized array.</para>
///
/// <para><b>Untrusted data.</b> A save file is untrusted in both formats (the binary's CRC-32 can be
/// recomputed, the JSON is hand-editable): the archive only guarantees each value's C# range, so a loaded
/// object is validated (C4) before <see cref="ApplyTo"/>. <see cref="Capture"/> and
/// <see cref="TryCaptureFromWorld"/> do not validate either; E16.d writes a capture only after it validates.</para>
/// </summary>
public sealed class AlundraSaveGame : ISaveGameData
{
    /// <summary>C2: the only data version so far.</summary>
    internal const int DataVersion1 = 1;

    /// <summary>The number of <c>GameFlags</c> words the original saves (<c>+0x05C</c>, 64 x 4 bytes).</summary>
    internal const int GameFlagWordCount = 64;

    /// <summary>The length of <c>MapIdToInternalMapIndexTable</c> (<c>+0x15C</c>, 500 x 2 bytes).</summary>
    internal const int MapIndexTableLength = 500;

    /// <summary>The length of <c>NumberOfItems</c> (<c>+0x556</c>, 256 x 2 bytes).</summary>
    internal const int NumberOfItemsLength = 256;

    /// <summary>The summary template <c>UpdateMenuStatusText</c> (0x80030FC8) starts from, read at 0x80022C38
    /// (F6): 30 characters.</summary>
    private const string SummaryTemplate = "  HP 00       TIME 00:00:00   ";

    // ---- Domains of the validation (C3, C4; the E16.c domain table) ------------------------------------

    /// <summary><c>SetPlayerHpMax</c> ceiling, 0x32 (<c>AlundraPlayerManager.cs:660-671</c>).</summary>
    internal const int MaxHpMax = 50;

    /// <summary><c>SetPlayerMpMax</c> ceiling (<c>AlundraPlayerManager.cs:704-714</c>); the jauge has 4 magic
    /// boxes (<c>AlundraHudDirector.cs:164</c>).</summary>
    internal const int MaxMpMax = 4;

    /// <summary><c>SetMoney</c> ceiling (<c>AlundraPlayerManager.cs:746-757</c>); a negative amount makes the
    /// jauge throw (<c>AlundraHudComposer.cs:320-331</c>).</summary>
    internal const int MaxMoney = 9999;

    /// <summary><c>SetPlayerWeaponId</c> accepts -1 or a slot 1..6 (<c>AlundraPlayerManager.cs:822-834</c>).</summary>
    internal const int MaxWeaponId = 6;

    /// <summary><c>IncreaseFalcon2</c> (0x8004E6EC) and <c>UpdateNumberOfFalcon</c> (0x8004E738) cap both
    /// counters at 0x32 (§2, Q6).</summary>
    internal const int MaxFalcon = 50;

    /// <summary>C3: a tile height is a byte, plus 1 on a slope (§2, Q6), well below 2048 where
    /// <c>Z &lt;&lt; 20</c> overflows.</summary>
    internal const int MaxCameraTileZ = 256;

    /// <summary>C3: <c>(X * 24 + 12) &lt;&lt; 16</c> fits an <see cref="int"/> up to X = 1364
    /// (<c>AlundraWorldProxy.cs:1563-1565</c>).</summary>
    internal const int MaxCameraTileX = 1364;

    /// <summary>C3: <c>(Y * 16 + 8) &lt;&lt; 16</c> fits an <see cref="int"/> up to Y = 2047.</summary>
    internal const int MaxCameraTileY = 2047;

    /// <summary>The "maximum count" column of <c>ItemsProperties</c> (<c>[id * 5 + 3]</c>, §2 Q6).</summary>
    private const int MaxCountColumn = 3;

    /// <summary><c>g_saveData.GameTime</c> (<c>+0x048</c>): sixtieths of a second (D-E16-23).</summary>
    public uint GameTime;

    /// <summary><c>g_saveData.InitialMapId</c> (<c>+0x04C</c>): the map the game restarts on. An <see cref="int"/>,
    /// like the id <see cref="BackdropLoader.TryParseMapIndex"/> returns and the keys of <c>world-index.json</c>.</summary>
    public int InitialMapId;

    /// <summary><c>g_saveData.CameraTileX</c> (<c>+0x050</c>): the hero's tile on <see cref="InitialMapId"/>.</summary>
    public int CameraTileX;

    /// <summary><c>g_saveData.CameraTileY</c> (<c>+0x054</c>).</summary>
    public int CameraTileY;

    /// <summary><c>g_saveData.CameraTileZ</c> (<c>+0x058</c>): <c>PosZ &gt;&gt; 20</c> (F5).</summary>
    public int CameraTileZ;

    /// <summary><c>g_saveData.GameFlags</c> (<c>+0x05C</c>): the first 64 words of the persistent flag bank.</summary>
    public readonly uint[] GameFlags = new uint[GameFlagWordCount];

    /// <summary><c>g_saveData.MapIdToInternalMapIndexTable</c> (<c>+0x15C</c>).</summary>
    public readonly ushort[] MapIdToInternalMapIndexTable = new ushort[MapIndexTableLength];

    /// <summary><c>PlayerStats.Hp</c> (<c>+0x544</c>).</summary>
    public short Hp;

    /// <summary><c>PlayerStats.HpMax</c>.</summary>
    public short HpMax;

    /// <summary><c>PlayerStats.Mp</c>.</summary>
    public short Mp;

    /// <summary><c>PlayerStats.MpMax</c>.</summary>
    public short MpMax;

    /// <summary><c>PlayerStats.MoneyAmount</c> (<see cref="AlundraPlayerStats.Money"/>).</summary>
    public short Money;

    /// <summary><c>PlayerStats.WeaponId</c>.</summary>
    public short WeaponId;

    /// <summary><c>PlayerStats.ItemId</c>.</summary>
    public short ItemId;

    /// <summary><c>PlayerStats.FalconTemp</c>.</summary>
    public short FalconTemp;

    /// <summary><c>PlayerStats.Falcon</c>.</summary>
    public short Falcon;

    /// <summary><c>g_saveData.NumberOfItems</c> (<c>+0x556</c>).</summary>
    public readonly short[] NumberOfItems = new short[NumberOfItemsLength];

    /// <summary><c>g_saveData.SaveSlotIndex</c> (<c>+0x756</c>), the death-retry counter (D-E16-22).</summary>
    public byte DeathRetryCount;

    /// <summary>
    /// C2/SC2: the data version this object was loaded with - not serialized. <see cref="DataVersion1"/> on a
    /// constructed or captured object; only <see cref="Serialize"/>, when loading, replaces it with
    /// <see cref="SaveGameArchive.DataVersion"/>, which the engine accepts from 0 up to
    /// <see cref="LatestDataVersion"/>. The validation refuses anything but <see cref="DataVersion1"/>. The
    /// setter is internal so tests can pose a version directly.
    /// </summary>
    public int LoadedDataVersion { get; internal set; } = DataVersion1;

    /// <inheritdoc />
    public int LatestDataVersion => DataVersion1;

    /// <summary>
    /// C2: writes or reads every field, in <c>g_saveData</c>'s order - the binary format is positional. It
    /// follows the version-1 layout whatever <see cref="SaveGameArchive.DataVersion"/> says, never branches on
    /// a value it read, and never throws on its own: an exception here would leave <c>TryLoad</c>
    /// (<c>SaveGameService.cs:165-176</c>). The only use of the archive's version is to remember it in
    /// <see cref="LoadedDataVersion"/> when loading, for the validation to check.
    /// </summary>
    public void Serialize(SaveGameArchive archive)
    {
        if (archive.IsLoading)
        {
            LoadedDataVersion = archive.DataVersion;
        }

        archive.Value("gameTime", ref GameTime);
        archive.Value("initialMapId", ref InitialMapId);
        archive.Value("cameraTileX", ref CameraTileX);
        archive.Value("cameraTileY", ref CameraTileY);
        archive.Value("cameraTileZ", ref CameraTileZ);
        archive.Value("gameFlags", GameFlags);
        archive.Value("mapIdToInternalMapIndexTable", MapIdToInternalMapIndexTable);

        archive.BeginObject("playerStats");
        archive.Value("hp", ref Hp);
        archive.Value("hpMax", ref HpMax);
        archive.Value("mp", ref Mp);
        archive.Value("mpMax", ref MpMax);
        archive.Value("money", ref Money);
        archive.Value("weaponId", ref WeaponId);
        archive.Value("itemId", ref ItemId);
        archive.Value("falconTemp", ref FalconTemp);
        archive.Value("falcon", ref Falcon);
        archive.EndObject();

        archive.Value("numberOfItems", NumberOfItems);
        archive.Value("deathRetryCount", ref DeathRetryCount);
    }

    /// <summary>
    /// C5: port of what <c>UpdateSavedData</c> (0x80031588, <c>GameEngine.cs:2648-2662</c>) gathers, from
    /// values already read. Copies words 0 to 63 of <see cref="AlundraGameState.GameFlags"/>, the map table,
    /// the item counters, the nine stats, <see cref="AlundraGameState.GameTime"/> and
    /// <see cref="AlundraGameState.DeathRetryCount"/> into this object's OWN arrays, then sets
    /// <see cref="InitialMapId"/> and <see cref="CameraTileX"/>/<see cref="CameraTileY"/>/<see cref="CameraTileZ"/>
    /// from the arguments. Does not validate. Production goes through <see cref="TryCaptureFromWorld"/>.
    /// </summary>
    public static AlundraSaveGame Capture(AlundraGameState state, int currentMapId, int tileX, int tileY, int tileZ)
    {
        ArgumentNullException.ThrowIfNull(state);

        var save = new AlundraSaveGame
        {
            GameTime = state.GameTime,
            InitialMapId = currentMapId,
            CameraTileX = tileX,
            CameraTileY = tileY,
            CameraTileZ = tileZ,
            DeathRetryCount = state.DeathRetryCount,
        };

        Array.Copy(state.GameFlags, save.GameFlags, GameFlagWordCount);
        Array.Copy(state.MapIdToInternalMapIndexTable, save.MapIdToInternalMapIndexTable, MapIndexTableLength);
        Array.Copy(state.NumberOfItems, save.NumberOfItems, NumberOfItemsLength);

        var stats = state.PlayerStats;
        save.Hp = stats.Hp;
        save.HpMax = stats.HpMax;
        save.Mp = stats.Mp;
        save.MpMax = stats.MpMax;
        save.Money = stats.Money;
        save.WeaponId = stats.WeaponId;
        save.ItemId = stats.ItemId;
        save.FalconTemp = stats.FalconTemp;
        save.Falcon = stats.Falcon;

        return save;
    }

    /// <summary>
    /// C5: captures from the live world, reading <c>UpdateSavedData</c>'s own sources - the current map from
    /// the "-{id}" suffix of <paramref name="worldName"/> (<see cref="BackdropLoader.TryParseMapIndex"/>, F4:
    /// the id a warp loads through <see cref="AlundraWorldIndexTable.Resolve"/>), and the hero's tile from
    /// <see cref="AlundraEntityScriptProxy.TileX"/>/<see cref="AlundraEntityScriptProxy.TileY"/>/
    /// <see cref="AlundraEntityScriptProxy.TileZ"/> (F5). Returns <c>false</c>, without throwing, when the
    /// name carries no id or the hero is missing. Does not validate.
    /// <para>Contract for E16.d: the save key captures through this method with the current world's name and
    /// <see cref="AlundraWorldProxy.PlayerEntity"/>, and writes the save only after the captured object
    /// validates.</para>
    /// </summary>
    public static bool TryCaptureFromWorld(
        AlundraGameState state,
        string worldName,
        AlundraEntityScriptProxy? player,
        out AlundraSaveGame? save)
    {
        ArgumentNullException.ThrowIfNull(state);

        save = null;
        if (player == null || worldName == null || !BackdropLoader.TryParseMapIndex(worldName, out var mapId))
        {
            return false;
        }

        save = Capture(state, mapId, player.TileX, player.TileY, player.TileZ);
        return true;
    }

    /// <summary>
    /// C4 (docs/plan-e16-etat-partie.md, D-E16-32): checks every field against its domain (the E16.c domain
    /// table, corrected by C3), stateless and without throwing whatever this object holds. A save is untrusted
    /// in both formats, so this runs after every load and before <see cref="ApplyTo"/>; E16.d also runs it on a
    /// capture before writing it. At the first value outside its domain it returns <c>false</c> with a message
    /// naming the field (with its serialized path), its value and the domain - for example
    /// <c>playerStats.money = -1, outside 0..9999</c>. In order:
    /// <list type="number">
    /// <item><description><see cref="LoadedDataVersion"/> must be 1: the engine loads any version up to
    /// <see cref="LatestDataVersion"/>, 0 included (SC2);</description></item>
    /// <item><description><c>gameTime</c>: 0..<see cref="AlundraGameState.GameTimeMax"/>;</description></item>
    /// <item><description><c>initialMapId</c>: a key of <c>world-index.json</c>, whose world is in the catalog and
    /// whose <c>.tileMap</c> gives a width and a height of at least 1. Only that one <c>.tileMap</c> is read, only
    /// after the key is found: its path always comes from <c>world-index.json</c>, never from the save. An
    /// exception of the catalog predicate, and any read failure, is a refusal (SC6);</description></item>
    /// <item><description><c>cameraTileX</c>: 0 to <c>min(w - 1, 1364)</c>; <c>cameraTileY</c>: 0 to
    /// <c>min(h - 1, 2047)</c>; <c>cameraTileZ</c>: 0..256 (C3);</description></item>
    /// <item><description><c>mapIdToInternalMapIndexTable[i]</c>: a key of <c>world-index.json</c>, or
    /// <c>i</c> itself (C3, F1: the New Game table is the identity over 500 entries, the index has 483
    /// keys);</description></item>
    /// <item><description><c>playerStats</c>: <c>hpMax</c> 0..50 then <c>hp</c> 0..<c>hpMax</c>, <c>mpMax</c>
    /// 0..4 then <c>mp</c> 0..<c>mpMax</c>, <c>money</c> 0..9999, <c>weaponId</c> -1 or 1..6, <c>itemId</c>
    /// 0..98 (<see cref="AlundraPlayerManager.ItemsCount"/>), <c>falconTemp</c> and <c>falcon</c>
    /// 0..50;</description></item>
    /// <item><description><c>numberOfItems</c>, the last field checked: at <c>id * 2 + 1</c> for an item id below
    /// <see cref="AlundraPlayerManager.ItemsCount"/>, 0..<c>ItemsProperties[id * 5 + 3]</c>; every even index and
    /// every index from 198 up, 0.</description></item>
    /// </list>
    /// <c>gameFlags</c> has no domain, and <c>deathRetryCount</c>'s domain, 0..255, is its type's (D-E16-22).
    /// </summary>
    public bool TryValidate(AlundraSaveGameRules rules, out string error)
    {
        ArgumentNullException.ThrowIfNull(rules);

        error = FindFirstInvalidField(rules) ?? string.Empty;
        return error.Length == 0;
    }

    private string? FindFirstInvalidField(AlundraSaveGameRules rules)
    {
        if (LoadedDataVersion != DataVersion1)
        {
            return Format($"data version {LoadedDataVersion} is not supported, only {DataVersion1}");
        }

        if (GameTime > AlundraGameState.GameTimeMax)
        {
            return OutsideRange("gameTime", GameTime, 0, AlundraGameState.GameTimeMax);
        }

        var mapError = CheckInitialMap(rules, out var mapWidth, out var mapHeight);
        if (mapError != null)
        {
            return mapError;
        }

        if (CameraTileX < 0 || CameraTileX > Math.Min(mapWidth - 1, MaxCameraTileX))
        {
            return OutsideRange("cameraTileX", CameraTileX, 0, Math.Min(mapWidth - 1, MaxCameraTileX));
        }

        if (CameraTileY < 0 || CameraTileY > Math.Min(mapHeight - 1, MaxCameraTileY))
        {
            return OutsideRange("cameraTileY", CameraTileY, 0, Math.Min(mapHeight - 1, MaxCameraTileY));
        }

        if (CameraTileZ < 0 || CameraTileZ > MaxCameraTileZ)
        {
            return OutsideRange("cameraTileZ", CameraTileZ, 0, MaxCameraTileZ);
        }

        for (var i = 0; i < MapIdToInternalMapIndexTable.Length; i++)
        {
            var value = MapIdToInternalMapIndexTable[i];
            if (value != i && rules.WorldIndex.Resolve(value) == null)
            {
                return Format($"mapIdToInternalMapIndexTable[{i}] = {value}, neither a key of world-index.json nor its own index");
            }
        }

        var statsError = CheckPlayerStats();
        if (statsError != null)
        {
            return statsError;
        }

        var itemsProperties = rules.ItemTables.ItemsProperties;
        for (var i = 0; i < NumberOfItems.Length; i++)
        {
            var itemId = (i - 1) / 2;
            var max = i % 2 == 1 && itemId < AlundraPlayerManager.ItemsCount
                ? itemsProperties[itemId * AlundraItemTables.ItemColumnCount + MaxCountColumn]
                : 0;

            if (NumberOfItems[i] < 0 || NumberOfItems[i] > max)
            {
                return OutsideRange($"numberOfItems[{i}]", NumberOfItems[i], 0, max);
            }
        }

        return null;
    }

    /// <summary>C3/C4/D-E16-32: <c>initialMapId</c> - a key, in the catalog, with a readable size of at least
    /// 1 x 1. Null when valid, with the map's size.</summary>
    private string? CheckInitialMap(AlundraSaveGameRules rules, out int width, out int height)
    {
        width = 0;
        height = 0;

        var worldPath = rules.WorldIndex.Resolve(InitialMapId);
        if (worldPath == null)
        {
            return Format($"initialMapId = {InitialMapId}, not a key of world-index.json");
        }

        bool inCatalog;
        try
        {
            inCatalog = rules.IsWorldInCatalog(worldPath);
        }
        catch (Exception ex)
        {
            return Format($"initialMapId = {InitialMapId}, the catalog lookup of '{worldPath}' failed ({ex.GetType().Name}: {ex.Message})");
        }

        if (!inCatalog)
        {
            return Format($"initialMapId = {InitialMapId}, world '{worldPath}' is not in the asset catalog");
        }

        if (!AlundraMapSizeReader.TryRead(rules.ProjectPath, worldPath, out width, out height, out var readError))
        {
            return Format($"initialMapId = {InitialMapId}, map size unreadable: {readError}");
        }

        if (width < 1 || height < 1)
        {
            return Format($"initialMapId = {InitialMapId}, map size {width} x {height}, width and height must be at least 1");
        }

        return null;
    }

    /// <summary>The nine stats, maxima before the values they bound (the cross rules of the domain table).</summary>
    private string? CheckPlayerStats()
    {
        if (HpMax < 0 || HpMax > MaxHpMax)
        {
            return OutsideRange("playerStats.hpMax", HpMax, 0, MaxHpMax);
        }

        if (Hp < 0 || Hp > HpMax)
        {
            return OutsideRange("playerStats.hp", Hp, 0, HpMax);
        }

        if (MpMax < 0 || MpMax > MaxMpMax)
        {
            return OutsideRange("playerStats.mpMax", MpMax, 0, MaxMpMax);
        }

        if (Mp < 0 || Mp > MpMax)
        {
            return OutsideRange("playerStats.mp", Mp, 0, MpMax);
        }

        if (Money < 0 || Money > MaxMoney)
        {
            return OutsideRange("playerStats.money", Money, 0, MaxMoney);
        }

        if (WeaponId != -1 && (WeaponId < 1 || WeaponId > MaxWeaponId))
        {
            return Format($"playerStats.weaponId = {WeaponId}, outside -1 or 1..{MaxWeaponId}");
        }

        if (ItemId < 0 || ItemId >= AlundraPlayerManager.ItemsCount)
        {
            return OutsideRange("playerStats.itemId", ItemId, 0, AlundraPlayerManager.ItemsCount - 1);
        }

        if (FalconTemp < 0 || FalconTemp > MaxFalcon)
        {
            return OutsideRange("playerStats.falconTemp", FalconTemp, 0, MaxFalcon);
        }

        if (Falcon < 0 || Falcon > MaxFalcon)
        {
            return OutsideRange("playerStats.falcon", Falcon, 0, MaxFalcon);
        }

        return null;
    }

    private static string OutsideRange(string field, long value, long min, long max)
    {
        return Format($"{field} = {value}, outside {min}..{max}");
    }

    private static string Format(FormattableString message)
    {
        return message.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// C6: copies an ALREADY VALIDATED object into <paramref name="state"/>. Clears the 1024 words of
    /// <see cref="AlundraGameState.GameFlags"/> before copying the 64 saved ones (otherwise flags of the
    /// session in progress would survive), then copies the map table, the item counters, the nine stats,
    /// <see cref="DeathRetryCount"/> and <see cref="GameTime"/>, and drops the unsaved fraction of the game
    /// time. It touches neither <c>TemporaryFlags</c>, <c>TextCategoryIndex</c>, <c>GameVariables</c>,
    /// <c>PlayerControlFlags</c> nor the rest of the session: that is E16.d's step 4. The array lengths are
    /// fixed at construction on both sides, so it does not throw.
    /// </summary>
    public void ApplyTo(AlundraGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Array.Clear(state.GameFlags);
        Array.Copy(GameFlags, state.GameFlags, GameFlagWordCount);
        Array.Copy(MapIdToInternalMapIndexTable, state.MapIdToInternalMapIndexTable, MapIndexTableLength);
        Array.Copy(NumberOfItems, state.NumberOfItems, NumberOfItemsLength);

        var stats = state.PlayerStats;
        stats.Hp = Hp;
        stats.HpMax = HpMax;
        stats.Mp = Mp;
        stats.MpMax = MpMax;
        stats.Money = Money;
        stats.WeaponId = WeaponId;
        stats.ItemId = ItemId;
        stats.FalconTemp = FalconTemp;
        stats.Falcon = Falcon;

        state.DeathRetryCount = DeathRetryCount;
        state.GameTime = GameTime;
        state.ResetGameTimeFraction();
    }

    /// <summary>
    /// C7: the slot's metadata, computed from this object - what <c>UpdateMenuStatusText</c> (0x80030FC8)
    /// writes into <c>CurrentFlagName</c> and <c>GameStateDescription</c>, which the slot list reads without
    /// decoding the save:
    /// <list type="bullet">
    /// <item><description><c>chapter</c>: <see cref="AlundraChapterFlags.GetFirstEnabledFlagIndex"/> over
    /// <see cref="GameFlags"/>, on four digits (<c>"0000"</c> to <c>"0041"</c>, F8);</description></item>
    /// <item><description><c>summary</c>: <see cref="BuildSummary"/> over <see cref="HpMax"/> and
    /// <see cref="GameTime"/> (F6).</description></item>
    /// </list>
    /// The chapter's name and the display belong to E16.e, which bounds <c>chapter</c> before using it (SC9).
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildMetadata()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["chapter"] = AlundraChapterFlags.FormatChapter(AlundraChapterFlags.GetFirstEnabledFlagIndex(GameFlags)),
            ["summary"] = BuildSummary(HpMax, GameTime),
        };
    }

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
