#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>The speaker the oracle portrait reads: the integer parts of its 16.16 position (<c>x</c>, <c>y</c>, <c>z</c>), its flags and its image height.</summary>
internal sealed class RawEntity
{
    public int Id;
    public uint Flags;
    public int X;
    public int Y;
    public int Z;
    public int H = 56;
}
