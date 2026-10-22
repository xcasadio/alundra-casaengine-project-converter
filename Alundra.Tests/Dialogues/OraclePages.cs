#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// F2-R3, the oracle's side: an exported Yarn page (its text and its markers, the edge spaces already cut by D-E15-8, except the trailing spaces of a centred last line, kept by D-E19-78) back to the bytes of
/// the original's box: <c>br</c> is <c>\N</c>, <c>glyph</c> a glyph <c>\W</c>, <c>voice</c>, <c>center</c> is <c>\H</c>, <c>slow</c> is <c>\T</c>,
/// <c>flag</c> is <c>\digits</c>, <c>yield</c> is <c>\Y</c>, <c>empty</c> nothing; a following page is an <c>\A</c> (the export's page
/// boundary, ADR-0006). Read from the raw <c>LineTexts</c> of the asset, never from the engine's markup parser nor from the director.
/// </summary>
internal static class OraclePages
{
    private static readonly Dictionary<char, byte> Windows1252 = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87,
        ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91,
        ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98,
        ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B, ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static readonly Dictionary<int, char> VoiceLetter = new() { [-1] = 'B', [0] = 'C', [1] = 'D', [2] = 'E', [3] = 'F', [4] = 'G' };
    private static readonly Regex Attribute = new(@"(\w+)=(-?\d+)", RegexOptions.Compiled);

    public static byte ByteOf(char ch) =>
        ch < 256 ? (byte)ch : Windows1252.TryGetValue(ch, out var b) ? b : throw new NotSupportedException($"U+{(int)ch:X4} has no byte of the box");

    public static byte[] PageBytes(string body)
    {
        var output = new List<byte>();
        var i = 0;
        while (i < body.Length)
        {
            var c = body[i];
            if (c == '\\' && i + 1 < body.Length)
            {
                output.Add(ByteOf(body[i + 1]));
                i += 2;
                continue;
            }

            if (c == '[')
            {
                var j = body.IndexOf(']', i);
                var tag = body.Substring(i + 1, j - i - 1);
                var name = tag.Split(' ')[0].TrimEnd('/');
                var attributes = Attribute.Matches(tag).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
                switch (name)
                {
                    case "br":
                        output.AddRange(new[] { (byte)'\\', (byte)'N' });
                        break;
                    case "voice":
                        output.AddRange(new[] { (byte)'\\', (byte)VoiceLetter[int.Parse(attributes["id"])] });
                        break;
                    case "center":
                        output.AddRange(new[] { (byte)'\\', (byte)'H' });
                        break;
                    case "slow":
                        output.AddRange(new[] { (byte)'\\', (byte)'T' });
                        break;
                    case "glyph":
                    {
                        var id = int.Parse(attributes["id"]);
                        output.AddRange(new[] { (byte)'\\', (byte)'W', (byte)(id < 26 ? id + 0x20 : id + 0x27) });
                        break;
                    }

                    case "flag":
                        output.Add((byte)'\\');
                        output.AddRange(attributes["id"].Select(d => (byte)d));
                        break;
                    case "yield":
                        output.AddRange(new[] { (byte)'\\', (byte)'Y' });
                        break;
                    case "empty":
                        break;
                    default:
                        throw new NotSupportedException("unknown marker " + tag);
                }

                i = j + 1;
                continue;
            }

            if (c == '{')
            {
                throw new NotSupportedException("function call in a page (its value is not modelled): " + body.Substring(i, body.IndexOf('}', i) - i + 1));
            }

            output.Add(ByteOf(c));
            i++;
        }

        return output.ToArray();
    }

    /// <summary>The pages of <paramref name="node"/> joined by <c>\A</c>; <paramref name="pageCount"/> is the number of pages.</summary>
    public static byte[] NodeBytes(DialogueAsset asset, string node, out int pageCount)
    {
        pageCount = 0;
        while (asset.LineTexts.ContainsKey($"line:{node}_p{pageCount}"))
        {
            pageCount++;
        }

        var output = new List<byte>();
        for (var k = 0; k < pageCount; k++)
        {
            if (k > 0)
            {
                output.AddRange(new[] { (byte)'\\', (byte)'A' });
            }

            output.AddRange(PageBytes(asset.LineTexts[$"line:{node}_p{k}"]));
        }

        return output.ToArray();
    }

    public static byte[] Bytes(string text) => text.Select(ByteOf).ToArray();
}
