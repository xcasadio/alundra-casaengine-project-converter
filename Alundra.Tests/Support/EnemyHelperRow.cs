#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Alundra.Tests.Support;

/// <summary>One state of a helper run in the E14.a0b oracle (<c>docs/plan-e14-annexe/e14a/oracle/out/h1/&lt;helper&gt;.txt.gz</c>): the inputs and the outputs, signed 32-bit words, as the emulated <c>ALUN_CD.EXE</c> (France) left them.</summary>
internal sealed record EnemyHelperRow(int[] In, int[] Out);
