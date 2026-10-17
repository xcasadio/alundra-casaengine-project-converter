#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;

namespace Alundra.UI.Hud;

/// <summary>
/// One image of the HUD as its XAML binds it (parent ADR-0002): source (an asset id the engine's UI asset provider
/// resolves, a sprite or a UI animation), native canvas position, visibility, and - for an animation - where it
/// restarts and whether it plays. Every setter notifies only on an actual change.
/// </summary>
public sealed class HudImageViewModel : ViewModelBase
{
    private string? _sourceName;
    private Guid _sourceId;
    private int? _left;
    private int? _top;
    private Visibility _visibility = Visibility.Collapsed;
    private TimeSpan _animationStartOffset;
    private bool _isAnimationPlaying = true;
    private Vector2 _subPixelOffset;

    public string? SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName != value)
            {
                _sourceName = value;
                _sourceId = Guid.TryParse(value, out var id) ? id : Guid.Empty;
                NotifyPropertyChanged();
            }
        }
    }

    public int? Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                _left = value;
                NotifyPropertyChanged();
            }
        }
    }

    public int? Top
    {
        get => _top;
        set
        {
            if (_top != value)
            {
                _top = value;
                NotifyPropertyChanged();
            }
        }
    }

    public Visibility Visibility
    {
        get => _visibility;
        set
        {
            if (_visibility != value)
            {
                _visibility = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Where the image's animation restarts (MGImage restarts it when this changes).</summary>
    public TimeSpan AnimationStartOffset
    {
        get => _animationStartOffset;
        set
        {
            if (_animationStartOffset != value)
            {
                _animationStartOffset = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Whether the image's animation plays; MGImage restarts it at <see cref="AnimationStartOffset"/> on
    /// every change, and holds that frame while false.</summary>
    public bool IsAnimationPlaying
    {
        get => _isAnimationPlaying;
        set
        {
            if (_isAnimationPlaying != value)
            {
                _isAnimationPlaying = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>The part of the position finer than a native pixel, in native pixels (0 or a fraction): the screen
    /// applies it to the image's <c>RenderTransform</c>, which MGUI's XAML cannot bind (gap G9). Only the equipment
    /// icons use it: they are centred in screen pixels (<see cref="HudIcon.ScreenLeft"/>).</summary>
    public Vector2 SubPixelOffset
    {
        get => _subPixelOffset;
        set
        {
            if (_subPixelOffset != value)
            {
                _subPixelOffset = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>Sets <see cref="SourceName"/> to <paramref name="assetId"/>, formatting the id only when it changed.</summary>
    internal void SetSource(Guid assetId)
    {
        if (_sourceId != assetId || _sourceName == null)
        {
            SourceName = assetId.ToString("D");
        }
    }

    internal void Show(int left, int top)
    {
        Left = left;
        Top = top;
        Visibility = Visibility.Visible;
    }

    /// <summary>Restarts the animation at <paramref name="offset"/> and plays it, even when the offset did not change:
    /// the play flag's two changes each restart it (MGImage), so the image is in phase whatever it showed before.</summary>
    internal void RestartAt(TimeSpan offset)
    {
        AnimationStartOffset = offset;
        IsAnimationPlaying = false;
        IsAnimationPlaying = true;
    }
}
