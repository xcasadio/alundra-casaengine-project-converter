using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// The inventory cursor as the screen plays it since the bound screens program (parent ADR-0002): the converter's
/// <c>UI/Animations/ui_inventory_cursor.anim2d</c>, sampled on the UI clock by the engine's 2D animation sampler, the
/// same one the UI asset provider drives. The original steps it every 10 ticks of 20 ms
/// (<c>AlundraInventoryDirector.cs:520-524</c>); the program allows 20 ms of drift against that, since the UI no longer
/// runs at 50 Hz. Played here at 60 frames per second, the way a display refreshes.
/// </summary>
public sealed class AlundraCursorAnimationTimingTests
{
    // wind_159/182/210/237, the cursor's four phases, and the pixel offset of each (AlundraInventoryComposer.cs:103-106).
    private static readonly Guid[] PhaseSprites =
    {
        Guid.Parse("6ed4380a-ba9c-5d0b-84db-22e1ddf61361"),
        Guid.Parse("c4a43c82-d394-5929-a1fa-61f29cc5dde4"),
        Guid.Parse("76368217-1aa5-5fee-a940-465e40601d66"),
        Guid.Parse("366c35dc-c165-5e6a-a4fc-897fe877391e"),
    };

    private static readonly Vector2[] PhaseOffsets = { new(0, 0), new(0, 0), new(-1, 1), new(-1, 0) };

    private const float FrameSeconds = 1f / 60f;
    private const float PhaseSeconds = 0.2f;
    private const float ToleranceSeconds = 0.020f;
    private const int Cycles = 25;

    private static Animation2dData LoadCursorAnimation()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "UI", "Animations", "ui_inventory_cursor.anim2d");
            if (File.Exists(candidate))
            {
                var animation = new Animation2dData();
                animation.Load(JObject.Parse(File.ReadAllText(candidate)));
                return animation;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraCursorAnimationTimingTests: no 'alundra-project/UI/Animations/ui_inventory_cursor.anim2d' above "
            + $"'{AppContext.BaseDirectory}' - run the converter's full export first.");
    }

    [Fact]
    public void OverTwentyFiveCycles_EveryPhaseChange_IsWithinTwentyMillisecondsOfTheOriginal()
    {
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(LoadCursorAnimation()));
        var part = sampler.RuntimeState.Parts[sampler.RuntimeState.DrawPartIndices[0]];

        Assert.Equal(PhaseSprites[0], part.SpriteId);

        var expectedPhase = 0;
        var changes = 0;
        var elapsed = 0f;
        // Two frames past the last expected change, which float accumulation can leave a hair beyond 20 s; the next
        // change is 200 ms further.
        var frames = (int)Math.Ceiling(Cycles * PhaseSprites.Length * PhaseSeconds / FrameSeconds) + 2;

        for (var frame = 0; frame < frames; frame++)
        {
            var previous = part.SpriteId;
            sampler.Update(FrameSeconds);
            elapsed += FrameSeconds;
            part = sampler.RuntimeState.Parts[sampler.RuntimeState.DrawPartIndices[0]];

            if (part.SpriteId == previous)
            {
                continue;
            }

            changes++;
            expectedPhase = (expectedPhase + 1) % PhaseSprites.Length;
            var originalChange = changes * PhaseSeconds;

            Assert.Equal(PhaseSprites[expectedPhase], part.SpriteId);
            Assert.Equal(PhaseOffsets[expectedPhase], part.Position);
            Assert.True(
                Math.Abs(elapsed - originalChange) < ToleranceSeconds,
                $"change {changes} at {elapsed * 1000:F1} ms, the original at {originalChange * 1000:F1} ms");
        }

        Assert.Equal(Cycles * PhaseSprites.Length, changes);
    }
}
