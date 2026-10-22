#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>
/// One call of a scenario (<c>calls[]</c>): the entity before and after (53 decoded fields each), the hero's seven fields, the generator, the latch, what changed and the journal of THIS call. <see cref="HeroAttackPointer"/> is the
/// hero's <c>curattack_1C8</c> of the per-tick column of the row BEFORE the call (the rows are valid call inputs, D-E14-6): <c>0x80144229</c> is the attack <c>(0x81, 1)</c>, <c>0x80144209</c> one the handler never reads.
/// </summary>
internal sealed record HandlerCall(
    int Tick, string Handler, int Slot, long Letter, long RngIn, long RngOut, long Latch,
    IReadOnlyDictionary<string, long> Before, IReadOnlyDictionary<string, long> After, IReadOnlyDictionary<string, long> Hero,
    IReadOnlyList<HandlerChange> Changed, IReadOnlyList<HandlerJournalEntry> Journal, long HeroAttackPointer = 0);
