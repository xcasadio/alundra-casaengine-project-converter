#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Events;
using Alundra.Ai.Enemies;
using Alundra.Ai.Objects;
using Alundra.Ai.Projectiles;
using Alundra.Scripts;

namespace Alundra.Ai;

/// <summary>
/// A native sprite handler (D-E14-1, docs/plan-e14-ia-native.md section 1.1a): the port of one entry of the
/// original's per-slot function tables (<c>g_entityEventFunctionsByType</c>, master table 0x80098F4C), shaped like
/// the save book's handlers: the entity whose picked event it is, the world context and the game state.
/// </summary>
internal delegate void AlundraNativeSpriteHandler(AlundraEntityScriptProxy entity, IEntityWorldContext context, GameState state);

/// <summary>
/// E14.a0 (D-E14-1, docs/plan-e14-ia-native.md section 1.1a): the registry of the native sprite handlers, keyed by
/// (program slot, sprite program code), each entry restricted to the sprite types it is registered for - the original's
/// table is per slot and code (the binary entry), and the same code is shared by different sprite types whose AI is not
/// ported yet, so a handler runs only for the types listed (the save book's <see cref="AlundraSaveBook.TryMatch"/> is
/// the one precedent). <see cref="EventProgramRunner.RunSpriteEvent"/> consults it after the save book and
/// E0/E1 (both stay outside the registry); an absent key or a type not listed is a counted no-op with one debug trace
/// per key. In production it holds the handlers of the Beannoid (E14.a1b) with E14.a1c, of the Muruta and, with E20.f3c, the C61 of the sword wave (<see cref="CreateForProduction"/>).
/// </summary>
internal sealed class AlundraNativeSpriteRegistry
{
    private readonly Dictionary<(int Slot, int Code), Entry> _entries = new();

    /// <summary>The registry of a production runner: the Beannoid's A1, C1 and D1 (<see cref="AlundraBeannoideAi"/>, E14.a1b) the Muruta's A0, C10 and D9 (<see cref="AlundraMurutaAi"/>, E14.a1c) and the C61 of the sword wave (<see cref="AlundraWaveSwordAi"/>, E20.f3c).</summary>
    internal static AlundraNativeSpriteRegistry CreateForProduction()
    {
        var registry = new AlundraNativeSpriteRegistry();
        AlundraBeannoideAi.Register(registry);
        AlundraMurutaAi.Register(registry);
        AlundraWaveSwordAi.Register(registry);
        return registry;
    }

    /// <summary>Number of registered (slot, code) keys.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Registers <paramref name="handler"/> for (<paramref name="slot"/>, <paramref name="code"/>), for the sprite types
    /// <paramref name="spriteTypes"/> only (<see cref="AlundraEntityScriptProxy.SpriteType"/>). A key can be registered
    /// once: a second registration of the same key throws (the original has one function per table entry). Returns a
    /// disposable that removes the entry, for tests.
    /// </summary>
    public IDisposable Register(int slot, int code, IEnumerable<int> spriteTypes, AlundraNativeSpriteHandler handler)
    {
        ArgumentNullException.ThrowIfNull(spriteTypes);
        ArgumentNullException.ThrowIfNull(handler);

        var key = (slot, code);
        if (_entries.ContainsKey(key))
        {
            throw new InvalidOperationException($"A native handler is already registered for slot {slot}, code {code}.");
        }

        _entries[key] = new Entry(spriteTypes.ToHashSet(), handler);
        return new Registration(this, key);
    }

    /// <summary>The handler registered for the key and the type, or false when the key is absent or the type not listed.</summary>
    public bool TryGet(int slot, int code, int spriteType, out AlundraNativeSpriteHandler? handler)
    {
        if (_entries.TryGetValue((slot, code), out var entry) && entry.SpriteTypes.Contains(spriteType))
        {
            handler = entry.Handler;
            return true;
        }

        handler = null;
        return false;
    }

    private sealed record Entry(HashSet<int> SpriteTypes, AlundraNativeSpriteHandler Handler);

    private sealed class Registration : IDisposable
    {
        private readonly AlundraNativeSpriteRegistry _owner;
        private readonly (int Slot, int Code) _key;

        public Registration(AlundraNativeSpriteRegistry owner, (int Slot, int Code) key)
        {
            _owner = owner;
            _key = key;
        }

        public void Dispose()
        {
            _owner._entries.Remove(_key);
        }
    }
}
