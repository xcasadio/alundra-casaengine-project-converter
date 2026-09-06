using System.Text.Json;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S2") - <see cref="BackdropDocument"/>'s
/// overlay-tint deserialization, moved here from the now-retired <c>BackdropRendererTests</c>
/// (that renderer's own drawing of the tint is gone; the JSON contract it read is unchanged and
/// still consumed by <see cref="AlundraBackdropStage.BuildDefinitions"/>).
/// </summary>
public class BackdropDocumentTests
{
    [Fact]
    public void BackdropDocument_DeserializesOverlayTintFields()
    {
        const string json = """
            {
              "MapIndex": 18,
              "Enabled": true,
              "OverlayEnabled": true,
              "OverlayColorR": 84,
              "OverlayColorG": 75,
              "OverlayColorB": 52,
              "Layers": []
            }
            """;

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.True(document!.OverlayEnabled);
        Assert.Equal(84, document.OverlayColorR);
        Assert.Equal(75, document.OverlayColorG);
        Assert.Equal(52, document.OverlayColorB);
    }

    [Fact]
    public void BackdropDocument_WithAbsentOverlayFields_DefaultsToTintDisabled()
    {
        // An old companion written before this feature existed - no Overlay* properties at all.
        const string json = """{ "MapIndex": 4, "Enabled": true, "Layers": [] }""";

        var document = JsonSerializer.Deserialize<BackdropDocument>(json);

        Assert.NotNull(document);
        Assert.False(document!.OverlayEnabled);
        Assert.Equal(0, document.OverlayColorR);
        Assert.Equal(0, document.OverlayColorG);
        Assert.Equal(0, document.OverlayColorB);
    }
}
