#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Session;
using Alundra.Events;
using Alundra.Entities;

namespace Alundra.Tests.Support;

/// <summary>A program site: the map, the slot letter (A to F), the program index of that slot (<c>index &amp; 0x7F</c>, the key
/// of the program table) and the pc, an index into the map's <c>Codes</c>. Two programs can share a pc (they share code), so the
/// program is part of the key.</summary>
internal readonly record struct ProgramSite(int Map, char Slot, int Program, int Pc);
