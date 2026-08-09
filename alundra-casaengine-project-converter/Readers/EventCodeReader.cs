using System.Text.Json;

namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// The EventCodes pointer/size fields of SpriteInfo.Header, copied verbatim.
///
/// They are kept because <see cref="EventCodeDocument.Codes"/> is one flat blob and the six
/// per-program tables hold offsets into it: without EventCodeAddress (the address the blob was
/// loaded at in PSX RAM) and the per-program pointer/size pairs, an offset in a table cannot be
/// turned back into an index in Codes. Field names are the extractor's own - nothing here is
/// renamed or reinterpreted, because the meaning of the individual programs is not decoded yet.
/// </summary>
public sealed class EventCodeHeader
{
    public int EventCodeAddress { get; set; }
    public int EventCodesAPointer { get; set; }
    public int EventCodesASize { get; set; }
    public int EventCodesBPointer { get; set; }
    public int EventCodesBSize { get; set; }
    public int EventCodesCPointer { get; set; }
    public int EventCodesCSize { get; set; }
    public int EventCodesDPointer { get; set; }
    public int EventCodesDSize { get; set; }
    public int EventCodesEPointer { get; set; }
    public int EventCodesESize { get; set; }
    public int EventCodesFPointer { get; set; }
    public int EventCodesFSize { get; set; }
    public int EventCodesFAndRemainingSize { get; set; }
}

/// <summary>
/// One map's event bytecode, uninterpreted: the six program tables (A-F) and the raw code blob
/// they index into, plus the header fields needed to resolve those indices.
/// </summary>
public sealed class EventCodeDocument
{
    public int MapIndex { get; set; }
    public EventCodeHeader Header { get; set; } = new();
    public int[] EventCodesATable { get; set; } = Array.Empty<int>();
    public int[] EventCodesBTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesCTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesDTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesETable { get; set; } = Array.Empty<int>();
    public int[] EventCodesFTable { get; set; } = Array.Empty<int>();
    public int[] Codes { get; set; } = Array.Empty<int>();
}

/// <summary>
/// Reads SpriteInfo.EventCodes (and the EventCodes part of SpriteInfo.Header) out of the native
/// map dump data/map_N.json.
///
/// Deliberately a straight copy, not a decoder: the bytecode's opcodes are unknown, so any
/// structure imposed here would be a guess baked into the converted project. The tables and the
/// blob keep their source names and their source order; an absent field yields an empty array (or
/// zero) rather than an exception, since a map with no events is legitimate.
/// </summary>
public static class EventCodeReader
{
    public static EventCodeDocument Read(string nativeMapFilePath, int mapIndex)
    {
        using var stream = File.OpenRead(nativeMapFilePath);
        using var jsonDocument = JsonDocument.Parse(stream);
        return Read(jsonDocument.RootElement, mapIndex);
    }

    public static EventCodeDocument Read(JsonElement mapRootElement, int mapIndex)
    {
        var document = new EventCodeDocument { MapIndex = mapIndex };

        if (!mapRootElement.TryGetProperty("SpriteInfo", out var spriteInfoElement)
            || spriteInfoElement.ValueKind != JsonValueKind.Object)
        {
            return document;
        }

        if (spriteInfoElement.TryGetProperty("Header", out var headerElement)
            && headerElement.ValueKind == JsonValueKind.Object)
        {
            document.Header = ReadHeader(headerElement);
        }

        if (!spriteInfoElement.TryGetProperty("EventCodes", out var eventCodesElement)
            || eventCodesElement.ValueKind != JsonValueKind.Object)
        {
            return document;
        }

        document.EventCodesATable = ReadIntArray(eventCodesElement, "EventCodesATable");
        document.EventCodesBTable = ReadIntArray(eventCodesElement, "EventCodesBTable");
        document.EventCodesCTable = ReadIntArray(eventCodesElement, "EventCodesCTable");
        document.EventCodesDTable = ReadIntArray(eventCodesElement, "EventCodesDTable");
        document.EventCodesETable = ReadIntArray(eventCodesElement, "EventCodesETable");
        document.EventCodesFTable = ReadIntArray(eventCodesElement, "EventCodesFTable");
        document.Codes = ReadIntArray(eventCodesElement, "Codes");

        return document;
    }

    private static EventCodeHeader ReadHeader(JsonElement headerElement)
    {
        return new EventCodeHeader
        {
            EventCodeAddress = ReadInt32(headerElement, "EventCodeAddress"),
            EventCodesAPointer = ReadInt32(headerElement, "EventCodesAPointer"),
            EventCodesASize = ReadInt32(headerElement, "EventCodesASize"),
            EventCodesBPointer = ReadInt32(headerElement, "EventCodesBPointer"),
            EventCodesBSize = ReadInt32(headerElement, "EventCodesBSize"),
            EventCodesCPointer = ReadInt32(headerElement, "EventCodesCPointer"),
            EventCodesCSize = ReadInt32(headerElement, "EventCodesCSize"),
            EventCodesDPointer = ReadInt32(headerElement, "EventCodesDPointer"),
            EventCodesDSize = ReadInt32(headerElement, "EventCodesDSize"),
            EventCodesEPointer = ReadInt32(headerElement, "EventCodesEPointer"),
            EventCodesESize = ReadInt32(headerElement, "EventCodesESize"),
            EventCodesFPointer = ReadInt32(headerElement, "EventCodesFPointer"),
            EventCodesFSize = ReadInt32(headerElement, "EventCodesFSize"),
            EventCodesFAndRemainingSize = ReadInt32(headerElement, "EventCodesFAndRemainingSize"),
        };
    }

    private static int ReadInt32(JsonElement parentElement, string propertyName)
    {
        return parentElement.TryGetProperty(propertyName, out var element)
               && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : 0;
    }

    private static int[] ReadIntArray(JsonElement parentElement, string propertyName)
    {
        if (!parentElement.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var values = new int[element.GetArrayLength()];
        var index = 0;
        foreach (var valueElement in element.EnumerateArray())
        {
            values[index++] = valueElement.GetInt32();
        }

        return values;
    }
}
