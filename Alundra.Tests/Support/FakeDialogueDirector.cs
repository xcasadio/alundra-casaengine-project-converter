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

    public void SetCloseMask(int mask)
    {
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
