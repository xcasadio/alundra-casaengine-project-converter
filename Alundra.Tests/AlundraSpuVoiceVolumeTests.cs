using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T4.2 (docs/plan-audio-mix-exact-muet.md): every row of the plan's own key-on volume table, straight
/// against <see cref="AlundraSpuVoiceVolume.ComputeKeyOn"/> - the plan's own numbers, hard-coded here so
/// a regression in the port (wrong operation order, wrong truncation, a missing pan branch) fails a
/// concrete assertion instead of a description.
/// </summary>
public class AlundraSpuVoiceVolumeTests
{
    // VAB 127, program 127, program pan 64 - every sound 300/301/302/162 row of the plan's table.
    [Theory]
    [InlineData(80, 64, 6500, 6500)] // 300 t0
    [InlineData(110, 64, 12290, 12290)] // 301 t0
    [InlineData(100, 34, 10157, 2957)] // 302 t0
    [InlineData(100, 94, 2786, 10157)] // 302 t1
    [InlineData(127, 64, 16383, 16383)] // 162 t0
    [InlineData(0, 0, 0, 0)] // 162 t1
    public void ComputeKeyOn_MatchesThePlanTable_WithProgramPan64(
        int toneVolume, int tonePan, int expectedLeft, int expectedRight)
    {
        var (left, right) = AlundraSpuVoiceVolume.ComputeKeyOn(
            vabMasterVolume: 127, programVolume: 127, programPan: 64, toneVolume, tonePan);

        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedRight, right);
    }

    // Synthetic program pans (still VAB 127, program 127, tone 127/64) - the plan's own program-pan
    // branch coverage, distinct from the sound-derived rows above.
    [Theory]
    [InlineData(32, 16383, 4226)]
    [InlineData(100, 3008, 16383)]
    public void ComputeKeyOn_MatchesThePlanTable_SyntheticProgramPans(
        int programPan, int expectedLeft, int expectedRight)
    {
        var (left, right) = AlundraSpuVoiceVolume.ComputeKeyOn(
            vabMasterVolume: 127, programVolume: 127, programPan, toneVolume: 127, tonePan: 64);

        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedRight, right);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(16384, 1f)]
    [InlineData(6500, 6500 / 16384f)]
    public void ToGain_DividesBy16384(int spuVolume, float expectedGain)
    {
        Assert.Equal(expectedGain, AlundraSpuVoiceVolume.ToGain(spuVolume), 6);
    }

    // T4.3 (docs/plan-audio-mix-exact-muet.md): every row of the plan's own remix table, hard-coded -
    // program volume 127 throughout (the only value the real data ever carries, T2.3).
    [Theory]
    [InlineData(0x40, 0x40, 100, 34, 2629, 765)] // 302 t0, Ship Klark (intérieur) card 390.
    [InlineData(0x40, 0x40, 100, 94, 721, 2629)] // 302 t1, card 390.
    [InlineData(0x50, 0x50, 127, 64, 6560, 6560)] // 200 t0, Inoa 162.
    [InlineData(0x14, 0x14, 127, 64, 440, 440)] // 200 t0.
    [InlineData(0, 0, 127, 64, 0, 0)] // 200 t0, silence.
    [InlineData(0x7f, 0x20, 100, 34, 10200, 197)] // 302 t0, asymmetric mix.
    [InlineData(0x7f, 0x20, 100, 94, 2798, 677)] // 302 t1, asymmetric mix.
    public void ComputeRemix_MatchesThePlanTable_WithProgramVolume127(
        int leftMix, int rightMix, int toneVolume, int tonePan, int expectedLeft, int expectedRight)
    {
        var (left, right) = AlundraSpuVoiceVolume.ComputeRemix(leftMix, rightMix, programVolume: 127, toneVolume, tonePan);

        Assert.Equal(expectedLeft, left);
        Assert.Equal(expectedRight, right);
    }

    // Control (plan's own T4.3 table): a full mix (0x7f/0x7f) on a 127/64 tone must reproduce exactly
    // the key-on volumes for that same tone (VAB 127, program 127, program pan 64 - the identity path).
    [Fact]
    public void ComputeRemix_FullMix_MatchesComputeKeyOn_ForTheSameTone()
    {
        var (remixLeft, remixRight) = AlundraSpuVoiceVolume.ComputeRemix(
            leftMix: 0x7f, rightMix: 0x7f, programVolume: 127, toneVolume: 127, tonePan: 64);
        var (keyOnLeft, keyOnRight) = AlundraSpuVoiceVolume.ComputeKeyOn(
            vabMasterVolume: 127, programVolume: 127, programPan: 64, toneVolume: 127, tonePan: 64);

        Assert.Equal((16383, 16383), (remixLeft, remixRight));
        Assert.Equal((keyOnLeft, keyOnRight), (remixLeft, remixRight));
    }
}
