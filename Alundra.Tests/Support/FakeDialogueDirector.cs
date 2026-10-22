#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Dialogue.Assets;
using Alundra.Dialogues;
using Alundra.Entities;

namespace Alundra.Tests.Support;

/// <summary>E20.e0c T2: a dialogue box whose only live state is the one the pickup asks about (<see cref="IsOpen"/>, <see cref="IsAwaitingChoice"/>); everything else does nothing.</summary>
internal sealed class FakeDialogueDirector : IDialogueDirector
{
    public bool HasPresenter => true;

    public bool IsOpen { get; set; }

    public bool IsAwaitingChoice { get; set; }

    public void OpenSpeaker(EntityScriptProxy? speaker, int? nameId = null)
    {
    }

    public void Open(DialogueAsset? asset, string? node, int controlMode)
    {
    }

    public void SetTextFlags(int flags)
    {
    }

    public void LatchTextStep()
    {
    }

    public void SetScrollMode(int mode)
    {
    }

    public void LatchScrollStart()
    {
    }

    /// <summary>E20.e0c T4: the close masks asked, in order.</summary>
    public List<int> CloseMasks { get; } = new();

    public void SetCloseMask(int mask) => CloseMasks.Add(mask);

    /// <summary>E20.e0c T4: the composed lines opened (text and control mode), in order; the box is then open.</summary>
    public List<(string Text, int Mode)> Opens { get; } = new();

    public void OpenComposed(string font3Text, int controlMode)
    {
        Opens.Add((font3Text, controlMode));
        IsOpen = true;
    }

    public bool RequestScriptClose() => false;

    public void Tick()
    {
    }

    public void OpenChoice(IReadOnlyList<string> labels)
    {
    }

    public int? TakeChoiceResult() => null;
}
