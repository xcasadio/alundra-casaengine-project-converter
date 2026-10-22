using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// Tests that create MGUI data bindings (loading a screen's XAML with <c>{dataBinding:MGBinding …}</c>, or setting a
/// window's data context). MGUI keeps every binding in a static, single-threaded registry
/// (<c>MGUI/MGUI.Core/UI/DataBinding/DataBindingManager.cs</c>): the UI runs on one thread, but xUnit runs test classes
/// in parallel, and two classes adding bindings at once corrupt its dictionary. Every test class that creates
/// bindings joins this collection, which runs alone - the same rule as the engine's own tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MguiDataBindingCollection
{
    public const string Name = "MguiDataBinding";
}
