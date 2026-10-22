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

/// <summary>One line of the closed list (<c>Data/story-chain-skipped-opcodes.tsv</c>): the level is S (scene), A (ambient), O
/// (optional) or X (off the chain).</summary>
internal sealed record ListedSite(ProgramSite Site, int Opcode, string Class, string Slice, char Tier);
