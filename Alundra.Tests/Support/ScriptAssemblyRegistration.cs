#nullable enable
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Assets;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>
/// R0 (engine ADR-0075): the engine creates a script object from a name written in the data only from the
/// registered gameplay assembly(ies), by full name. In the game the loader registers <c>Alundra.dll</c>; in this
/// test process the DLL is a plain project reference, so it is registered once for the whole run, before any test.
/// </summary>
internal static class ScriptAssemblyRegistration
{
    [ModuleInitializer]
    internal static void Register() => ElementFactory.RegisterScriptAssembly(typeof(WorldProxy).Assembly);
}
