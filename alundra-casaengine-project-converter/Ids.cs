using System.Security.Cryptography;
using System.Text;

namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// Deterministic asset GUID generation (RFC 4122 UUID v5, name-based/SHA-1), so that two runs of
/// the converter on the same input produce identical asset ids.
/// </summary>
public static class Ids
{
    private static readonly Guid ProjectNamespace = new("2b7f6b6a-2d63-4e33-9f0e-0f7f2b6a5c11");

    public static Guid For(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        Span<byte> namespaceBytes = stackalloc byte[16];
        ProjectNamespace.TryWriteBytes(namespaceBytes);
        SwapGuidByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(key);
        var data = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(data);
        nameBytes.CopyTo(data.AsSpan(namespaceBytes.Length));

        var hash = SHA1.HashData(data);
        var result = hash[..16];

        result[6] = (byte)((result[6] & 0x0F) | 0x50); // version 5
        result[8] = (byte)((result[8] & 0x3F) | 0x80); // RFC 4122 variant

        SwapGuidByteOrder(result);
        return new Guid(result);
    }

    // .NET's Guid byte layout stores Data1/Data2/Data3 little-endian, while RFC 4122 name-based
    // hashing operates on the standard big-endian byte order. This swap converts between the two.
    private static void SwapGuidByteOrder(Span<byte> guidBytes)
    {
        (guidBytes[0], guidBytes[3]) = (guidBytes[3], guidBytes[0]);
        (guidBytes[1], guidBytes[2]) = (guidBytes[2], guidBytes[1]);
        (guidBytes[4], guidBytes[5]) = (guidBytes[5], guidBytes[4]);
        (guidBytes[6], guidBytes[7]) = (guidBytes[7], guidBytes[6]);
    }
}
