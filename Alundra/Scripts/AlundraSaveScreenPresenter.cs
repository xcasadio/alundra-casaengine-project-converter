#nullable enable
using System;
using CasaEngine.Framework.UI;

namespace Alundra.Scripts;

/// <summary>
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5): reads <see cref="AlundraSaveScreenDirector"/>'s already-ticked state
/// and writes it into the screen's <see cref="AlundraSaveScreenViewModel"/>, which <c>SaveScreen.xaml</c> binds - one
/// <see cref="Tick"/> per LOGIC tick, from <see cref="AlundraWorldProxy.Update(float)"/>'s per-tick loop, right after
/// the director (the shape of <see cref="AlundraSubInventoryPresenter"/>).
///
/// <para><b>Push/remove</b> (the inventory's precedent): this presenter owns the screen and pushes it on the active
/// <see cref="IUIViewRuntime"/> the tick the director becomes active, and removes it the tick it is no longer
/// active (the end of <c>0x63</c>, or a world change, SE3). The OUI/NON question is the dialogue screen, on the
/// <see cref="UILayer.Modal"/> layer, pushed over this one (<see cref="UILayer.Menu"/>) without removing it.</para>
/// </summary>
public sealed class AlundraSaveScreenPresenter
{
    private readonly AlundraSaveScreenDirector _director;
    private readonly AlundraSaveScreenViewModel _viewModel;
    private readonly IUIScreen _screen;
    private readonly IUIViewRuntime? _uiView;
    private bool _pushed;

    public AlundraSaveScreenPresenter(
        AlundraSaveScreenDirector director,
        AlundraSaveScreenViewModel viewModel,
        IUIScreen screen,
        IUIViewRuntime? uiView)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(screen);

        _director = director;
        _viewModel = viewModel;
        _screen = screen;
        _uiView = uiView;
    }

    /// <summary>Test-only seam: whether this presenter believes its screen is pushed.</summary>
    internal bool IsPushedForTests => _pushed;

    public void Tick()
    {
        if (_director.IsActive)
        {
            if (!_pushed)
            {
                _uiView?.PushScreen(_screen);
                _pushed = true;
            }

            _viewModel.Apply(_director);
            return;
        }

        if (_pushed)
        {
            _viewModel.Apply(_director);
            _uiView?.RemoveScreen(_screen);
            _pushed = false;
        }
    }
}
