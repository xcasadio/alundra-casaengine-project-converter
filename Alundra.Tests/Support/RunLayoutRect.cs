#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>One rectangle of the cell layout of a run-oracle scenario (inclusive, later rectangles win per attribute; <c>null</c> leaves the attribute as it was).</summary>
internal sealed record RunLayoutRect(int C0, int R0, int C1, int R1, int? HeightByte, int? Walkability, int? GroundProperty);

/// <summary>One item of the journal of a tick of the run oracle: a sound (<paramref name="Id"/>) or an effect created by the binary's <c>0x8003BDD8</c> (<paramref name="Id"/>, position in pixels).</summary>
internal readonly record struct RunJournalItem(string Kind, int Id, double X, double Y, double Z);
