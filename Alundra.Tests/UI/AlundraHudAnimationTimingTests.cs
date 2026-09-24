using Alundra.Scripts;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// The HUD's magic pips and coin as the screen plays them since the bound screens program (parent ADR-0002): the
/// converter's <c>UI/Animations/ui_hud_magic_pip.anim2d</c> and <c>ui_hud_coin.anim2d</c>, started where
/// <see cref="AlundraHudViewModel"/> says, then sampled on the UI clock at 60 frames per second. The original steps a
/// pip every 10 ticks of 20 ms, pip <c>i</c> one frame ahead of pip 0 (<c>AlundraHudDirector.UpdateMagicPipPhase</c>),
/// and the coin every 6 ticks while the money rolls (<c>AlundraHudDirector.UpdateMoneyRoll</c>). The program allows
/// 20 ms of drift against those.
/// </summary>
public sealed class AlundraHudAnimationTimingTests
{
    // wind_001/003/010/017 and wind_126/130/134/139: the frames of the pip and of the coin, in play order.
    private static readonly Guid[] PipFrames =
    {
        Guid.Parse("eb70224b-d6d5-558e-a1ef-438f072135f8"), Guid.Parse("f82389e9-f867-54e2-98c1-e3cdef9ef110"),
        Guid.Parse("82b86c2a-c312-53ac-843c-2395db413512"), Guid.Parse("6975db38-c10f-5210-92e8-ccdc5b426d40"),
    };

    private static readonly Guid[] CoinFrames =
    {
        Guid.Parse("e7a9df0e-b996-57fb-b12b-b4f99a0e8319"), Guid.Parse("ee099380-b4d7-55f5-8976-66368b94b07d"),
        Guid.Parse("e389abb9-1c65-59dd-8489-5605850cd425"), Guid.Parse("0b54121d-727f-5786-8d27-48b598a24192"),
    };

    private const double TickMs = 20.0;
    private const double FrameMs = 1000.0 / 60.0;
    private const double ToleranceMs = 20.0;

    private static Animation2dData LoadAnimation(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "UI", "Animations", name + ".anim2d");
            if (File.Exists(candidate))
            {
                var animation = new Animation2dData();
                animation.Load(JObject.Parse(File.ReadAllText(candidate)));
                return animation;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraHudAnimationTimingTests: no 'alundra-project/UI/Animations/{name}.anim2d' above '{AppContext.BaseDirectory}' "
            + "- run the converter's full export first.");
    }

    /// <summary>Plays <paramref name="animation"/> from <paramref name="startOffset"/> the way MGImage restarts an
    /// animated source (Reset, then Seek), for <paramref name="durationMs"/> of 60 Hz frames, and returns the time and
    /// the sprite of every frame change.</summary>
    private static List<(double TimeMs, Guid Sprite)> Play(Animation2dData animation, TimeSpan startOffset, double durationMs)
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
        sampler.Reset();
        sampler.Seek((float)startOffset.TotalSeconds);

        var changes = new List<(double, Guid)>();
        var current = sampler.RuntimeState.Parts[sampler.RuntimeState.DrawPartIndices[0]].SpriteId;
        changes.Add((0, current));
        for (var elapsed = FrameMs; elapsed <= durationMs; elapsed += FrameMs)
        {
            sampler.Update((float)(FrameMs / 1000.0));
            var sprite = sampler.RuntimeState.Parts[sampler.RuntimeState.DrawPartIndices[0]].SpriteId;
            if (sprite != current)
            {
                changes.Add((elapsed, sprite));
                current = sprite;
            }
        }

        return changes;
    }

    /// <summary>Every change of <paramref name="played"/> must be the original's next frame, within the tolerance of
    /// the original's own change.</summary>
    private static void AssertFollows(List<(double TimeMs, Guid Sprite)> played, List<(double TimeMs, Guid Sprite)> original, string what)
    {
        Assert.Equal(original[0].Sprite, played[0].Sprite);
        Assert.Equal(original.Count, played.Count);
        for (var i = 1; i < original.Count; i++)
        {
            Assert.Equal(original[i].Sprite, played[i].Sprite);
            Assert.True(
                Math.Abs(played[i].TimeMs - original[i].TimeMs) < ToleranceMs,
                $"{what}: change {i} at {played[i].TimeMs:F1} ms, the original at {original[i].TimeMs:F1} ms");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(129)]
    public void FourFullPips_OverTwentyFiveCycles_FollowTheOriginalWithinTwentyMilliseconds(int frameCounter)
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        var phase = (frameCounter / 10) % 4;
        view.SetVisible(true);
        view.SetAnimationClock(frameCounter, moneyRolling: false);
        view.SetTiles(AlundraHudComposer.Compose(
            true, 5, 5, 5, false, 4, 4, false, 0, 0, new[] { phase, (phase + 1) % 4, (phase + 2) % 4, (phase + 3) % 4 }));

        var animation = LoadAnimation("ui_hud_magic_pip");
        const double durationMs = 25 * 4 * 200.0;
        var pips = new[] { viewModel.MagicPip0, viewModel.MagicPip1, viewModel.MagicPip2, viewModel.MagicPip3 };
        for (var pip = 0; pip < 4; pip++)
        {
            // The original: from this tick on, one tick every 20 ms; pip i shows frame (FrameCounter / 10 + i) % 4.
            var original = new List<(double, Guid)> { (0, PipFrames[(phase + pip) % 4]) };
            for (var tick = 1; tick * TickMs <= durationMs; tick++)
            {
                var counter = frameCounter + tick;
                if (counter % 10 == 0)
                {
                    original.Add((tick * TickMs, PipFrames[((counter / 10) + pip) % 4]));
                }
            }

            AssertFollows(Play(animation, pips[pip].AnimationStartOffset, durationMs), original, $"pip {pip}");
        }
    }

    [Theory]
    [InlineData(36, 1)] // starts on a boundary: the original has just stepped to frame 1
    [InlineData(41, 0)]
    [InlineData(45, 0)]
    public void TheCoin_WhileTheMoneyRolls_FollowsTheOriginalWithinTwentyMilliseconds(int frameCounter, int coinFrame)
    {
        var viewModel = new AlundraHudViewModel();
        IAlundraHudView view = viewModel;
        view.SetVisible(true);
        view.SetAnimationClock(frameCounter, moneyRolling: true);
        view.SetTiles(AlundraHudComposer.Compose(true, 5, 5, 5, false, 0, 0, false, 100, coinFrame, new[] { 0, 1, 2, 3 }));
        Assert.True(viewModel.Coin.IsAnimationPlaying);

        const double durationMs = 25 * 4 * 120.0;
        var original = new List<(double, Guid)> { (0, CoinFrames[coinFrame]) };
        var frame = coinFrame;
        for (var tick = 1; tick * TickMs <= durationMs; tick++)
        {
            if ((frameCounter + tick) % 6 == 0)
            {
                frame = (frame + 1) % 4;
                original.Add((tick * TickMs, CoinFrames[frame]));
            }
        }

        AssertFollows(Play(LoadAnimation("ui_hud_coin"), viewModel.Coin.AnimationStartOffset, durationMs), original, "coin");
    }
}
