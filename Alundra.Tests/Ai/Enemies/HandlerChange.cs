#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>One word the handler changed in the oracle (<c>changed</c>): its name and its value before and after (raw words, 64-bit as read).</summary>
internal sealed record HandlerChange(string Name, long Before, long After);
