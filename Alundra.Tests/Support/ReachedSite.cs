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

/// <summary>One instruction the walk reached.</summary>
internal readonly record struct ReachedSite(ProgramSite Site, int Opcode);
