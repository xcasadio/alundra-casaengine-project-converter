#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>One pass of the box, as the host recorded it.</summary>
internal sealed record OracleRow(int Frame, string Phase, string[] Notes, int Drawn, int[] Flags, int Y, int[] Sounds, int? Cursor, string[] Rows, int ScrollPixels,
    bool Held, bool Pressed);
