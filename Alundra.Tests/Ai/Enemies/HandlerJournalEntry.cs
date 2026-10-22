#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>One entry of the oracle's call journal (<c>spawn_39F58</c>, <c>sound</c>, <c>effect_3BE74</c>): its kind and its arguments (a sound has its number as the only argument).</summary>
internal sealed record HandlerJournalEntry(string Kind, long[] Arguments);
