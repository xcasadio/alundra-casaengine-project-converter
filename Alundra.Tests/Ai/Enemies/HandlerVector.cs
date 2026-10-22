#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>
/// One single-call vector of <c>vectors-beannoide.json</c> / <c>vectors-muruta.json</c> (E14.a1 oracle): the handler, the generator's word in and out, the 27 decoded fields of the entity and 7 of the hero before the call, the
/// attack of the toucher, what changed, the journal, and of the inputs only the break-effect byte (<see cref="InputFx"/>, null for the sprite's own), the platform flag and the active-effect flag.
/// </summary>
internal sealed record HandlerVector(
    string Handler, long RngIn, long RngOut, int Draws, long Latch,
    IReadOnlyDictionary<string, long> Entity, IReadOnlyDictionary<string, long> Hero, long[]? HeroAttack, long[]? AttackerAttack,
    IReadOnlyList<HandlerChange> Changed, IReadOnlyList<HandlerJournalEntry> Journal, long? InputFx, long InputPlat, long InputAe);
