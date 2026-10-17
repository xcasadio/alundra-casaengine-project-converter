#nullable enable
using System;
using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Shared.Helpers;
using Alundra.Entities;
using Alundra.Dialogues;

namespace Alundra.UI.Inventory;

/// <summary>One text line of the inventory screen as its XAML binds it: its text and native canvas position.</summary>
public sealed class InventoryTextViewModel : ViewModelBase
{
    private string _text = string.Empty;
    private int? _left;
    private int? _top;

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
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
}
