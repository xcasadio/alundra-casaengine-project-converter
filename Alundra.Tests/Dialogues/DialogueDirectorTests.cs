#nullable enable
using System;
using Xunit;
using Alundra.Dialogues;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, (l5), D-E0C-4, D-E0C-5, T0 (h)): <see cref="DialogueDirector.OpenComposed"/>, the member that opens the box on a line the pickup composed (the name of the object and the
/// "gained!" of the ETC), on one page and with no Yarn reader, with the sound 6 of the opening and the control mode of the call - the way <see cref="DialogueDirector.Open"/> opens a node of an asset. Additive: it changes nothing
/// of what <see cref="DialogueDirector.Open"/> does.
/// </summary>
public sealed class DialogueDirectorTests : IDisposable
{
    public DialogueDirectorTests() => DialogueDirector.Instance.ResetForTests();

    public void Dispose() => DialogueDirector.Instance.ResetForTests();

    private static (DialogueDirector Director, GameState State, RecordingSoundPlayer Sound) Mount()
    {
        var director = DialogueDirector.Instance;
        var state = new GameState();
        var sound = new RecordingSoundPlayer();
        director.AttachToWorld(null, state, sound); // no presenter and so no Yarn runner: the composed line needs neither.
        return (director, state, sound);
    }

    [Fact]
    public void OpenComposed_OpensTheBox_PlaysTheSound6_AndCountsAnOpening()
    {
        var (director, _, sound) = Mount();
        var serial = director.OpenSerial;

        director.OpenComposed("Herbes gagné!", 0);

        Assert.True(director.IsOpen);
        Assert.Equal(serial + 1, director.OpenSerial);
        Assert.Contains(6, sound.Requests);
        Assert.False(director.IsAwaitingChoice);
    }

    [Fact]
    public void OpenComposed_PosesTheControlBitOfTheMode_MenuOpenForZero_MessageBoxForOne()
    {
        var both = GameState.PlayerControlBits.MenuOpen | GameState.PlayerControlBits.MessageBox;
        var (director, state, _) = Mount();
        director.OpenComposed("Herbes gagné!", 0);
        Assert.Equal(GameState.PlayerControlBits.MenuOpen, state.PlayerControlFlags & both);

        director.ResetForTests();
        var (director2, state2, _) = Mount();
        director2.OpenComposed("Épée sacrée gagné!", 1);
        Assert.Equal(GameState.PlayerControlBits.MessageBox, state2.PlayerControlFlags & both);
    }

    [Fact]
    public void OpenComposed_TypesTheComposedText_OnItsOnePage_UntilTheBoxWaitsForTheButton()
    {
        var (director, _, _) = Mount();
        director.OpenComposed("Herbes gagné!", 0);

        for (var pass = 0; pass < 300; pass++)
        {
            director.Pass(squareHeld: false, squarePressed: false);
        }

        Assert.Contains("Herbes gagné!", director.Box.Lines);
        Assert.True(director.Box.IsTypingDone);
        Assert.True(director.IsOpen); // the default close mask 3: the box waits for its timer or the button.
    }

    [Fact]
    public void OpenComposed_AfterAnOpenedBox_StartsAgainFromAnEmptyBox_AndAnEmptyTextOpensAnEmptyOne()
    {
        var (director, _, _) = Mount();
        director.OpenComposed("Un premier", 0);
        for (var pass = 0; pass < 300; pass++)
        {
            director.Pass(false, false);
        }

        director.OpenComposed(string.Empty, 0);
        Assert.True(director.IsOpen);
        Assert.All(director.Box.Lines, line => Assert.Equal(string.Empty, line));
    }
}
