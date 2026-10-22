#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>SplitMix64 of <c>model/gen_sequences.py</c>: the generator a test can port in six lines.</summary>
internal sealed class SplitMix64
{
    private ulong _s;

    public SplitMix64(ulong seed) => _s = seed;

    public ulong Next()
    {
        unchecked
        {
            _s += 0x9E3779B97F4A7C15UL;
            var z = _s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public int Below(int n) => (int)(Next() % (ulong)n);

    public int RandInt(int lo, int hi) => lo + Below(hi - lo + 1);

    public bool Chance(int permille) => Below(1000) < permille;

    public T Choice<T>(IReadOnlyList<T> sequence) => sequence[Below(sequence.Count)];
}
