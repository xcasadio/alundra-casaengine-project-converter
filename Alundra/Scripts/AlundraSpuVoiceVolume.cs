#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// Pure, stateless port of the original's key-on SPU volume computation
/// (<c>FUN_80090C58</c> @ <c>0x80090C58</c>, `alundra-datas-analyser/AlundraTools/AlundraEngine/Sound/SoundManager.cs`
/// around lines 4830-4900), restricted to a sound-effect voice's own path (docs/plan-audio-mix-exact-muet.md, T4.2):
/// sequence key <c>0x21</c> (a sound effect never triggers through the sequencer branch - <c>TriggerVoice</c> always
/// passes it), voice volume <c>0x7f</c> and voice pan <c>0x40</c> (both constants <c>TriggerVoice</c> passes for a
/// sound effect's own two call sites - T0.2's "Départ d'une voix de bruitage"), and the mono flag left out (T0.2's
/// "Drapeau mono" closed the fact that its write is dead code in this executable - always 0, i.e. always stereo).
/// </summary>
public static class AlundraSpuVoiceVolume
{
    /// <summary>
    /// Computes the (left, right) SPU volumes the original writes at key-on for one tone, straight port of
    /// <c>FUN_80090C58</c> with the same truncating integer arithmetic and the same operation order: base volume
    /// from the VAB master volume, then the program volume and tone volume, then tone pan, program pan and voice
    /// pan (each on its own channel), then the final square-and-scale on each channel.
    /// </summary>
    /// <param name="vabMasterVolume">VAB header's own <c>Mvol</c> (<c>VabHdr.Mvol</c>), 0..0x7f.</param>
    /// <param name="programVolume">Program's own <c>Mvol</c> (<c>ProgAtr.Mvol</c>), 0..0x7f.</param>
    /// <param name="programPan">Program's own <c>Mpan</c> (<c>ProgAtr.Mpan</c>), 0..0x7f.</param>
    /// <param name="toneVolume">Tone's own <c>Vol</c> (<c>VagAtr.Vol</c>), 0..0x7f.</param>
    /// <param name="tonePan">Tone's own <c>Pan</c> (<c>VagAtr.Pan</c>), 0..0x7f.</param>
    /// <returns>The left and right SPU volumes, 0..0x3fff.</returns>
    public static (int Left, int Right) ComputeKeyOn(
        int vabMasterVolume, int programVolume, int programPan, int toneVolume, int tonePan)
    {
        // FUN_80090C58 @ 0x80090C58: baseVolume = (voiceVolume(0x7f) * ((vabMvol << 14) - vabMvol)) / 0x3f01.
        var baseVolume = (0x7f * ((vabMasterVolume << 14) - vabMasterVolume)) / 0x3f01;

        // baseVolume = (baseVolume * programVolume * toneVolume) / 0x3f01.
        baseVolume = (baseVolume * programVolume * toneVolume) / 0x3f01;

        var left = baseVolume;
        var right = baseVolume;

        // Sequence key is always 0x21 for a sound effect voice (TriggerVoice's own two call sites) - the
        // sequencer-volume branch (g_sequenceStatePointers) never runs, so left/right stay at baseVolume here.

        // Tone pan.
        if (tonePan < 0x40)
        {
            right = (right * tonePan) / 0x3f;
        }
        else
        {
            left = (left * (0x7f - tonePan)) / 0x3f;
        }

        // Program pan.
        if (programPan < 0x40)
        {
            right = (right * programPan) / 0x3f;
        }
        else
        {
            left = (left * (0x7f - programPan)) / 0x3f;
        }

        // Voice pan: always 0x40 for a sound effect voice (TriggerVoice's own constant), so the
        // original's ">= 0x40" branch always runs here: left = (left * (0x7f - 0x40)) / 0x3f = left.
        const int voicePan = 0x40;
        left = (left * (0x7f - voicePan)) / 0x3f;

        // Mono flag left out (T0.2: DAT_sound_801f7658 is dead code in this executable, always 0).

        left = (left * left) / 0x3fff;
        right = (right * right) / 0x3fff;

        return (left, right);
    }

    /// <summary>Converts a 0..0x3fff SPU volume, as returned by <see cref="ComputeKeyOn"/>, into the 0..1 linear
    /// gain <see cref="CasaEngine.Framework.Audio.AudioService.PlayClipStereo"/>/<c>SetVoiceStereoGains</c> take.</summary>
    public static float ToGain(int spuVolume) => spuVolume / 16384f;

    /// <summary>
    /// Computes the (left, right) SPU volumes the original writes when it remixes an ALREADY PLAYING
    /// tone-voice (docs/plan-audio-mix-exact-muet.md, T4.3), straight port of
    /// <c>PlaySoundEffectWithToneVolumeMixCore</c> (<c>0x80049794</c>,
    /// `alundra-datas-analyser/AlundraTools/AlundraEngine/Sound/SoundManager.cs` around lines 5229-5300)
    /// for a single tone, including its MIPS signed-multiply-high magic-constant scaling (the original
    /// never divides here - it multiplies by a magic constant and shifts, which truncates differently
    /// from a plain division for negative intermediate products, though every input here stays
    /// non-negative in practice).
    /// </summary>
    /// <param name="leftMix">Opcode 0xAB/0xBF's own left mix operand, 0..0x7f.</param>
    /// <param name="rightMix">Opcode 0xAB/0xBF's own right mix operand, 0..0x7f.</param>
    /// <param name="programVolume">The resolved record's own program volume (<see cref="Alundra.Scripts.SfxResolution.ProgramVolume"/>), 0..0x7f.</param>
    /// <param name="toneVolume">The tone's own volume (<see cref="Alundra.Scripts.SfxToneRecord.Volume"/>), 0..0x7f.</param>
    /// <param name="tonePan">The tone's own pan (<see cref="Alundra.Scripts.SfxToneRecord.Pan"/>), 0..0x7f.</param>
    /// <returns>The left and right SPU volumes.</returns>
    public static (int Left, int Right) ComputeRemix(
        int leftMix, int rightMix, int programVolume, int toneVolume, int tonePan)
    {
        // PlaySoundEffectWithToneVolumeMixCore @ 0x80049794, SoundManager.cs:5229-5300.
        var programVolumeSquaredMinusOne = SquarePlusOne(programVolume) - 1;
        var leftMixSquaredMinusOne = SquarePlusOne(leftMix) - 1;
        var rightMixSquaredMinusOne = SquarePlusOne(rightMix) - 1;
        var toneVolumeSquaredMinusOne = SquarePlusOne(toneVolume) - 1;

        // FindVoiceBySfxIdAndToneIndex @ 0x8004974C picks the voice; these two weights read its own
        // g_voiceTonePan.
        var leftPanWeight = tonePan < 0x41 ? 0x3f : 0x7f - tonePan;
        var rightPanWeight = tonePan < 0x40 ? tonePan : 0x3f;

        var leftBase = ApplyMipsVolumeScale13(
            ApplyMipsVolumeScale13(leftMixSquaredMinusOne * toneVolumeSquaredMinusOne) * programVolumeSquaredMinusOne);
        var rightBase = ApplyMipsVolumeScale13(
            ApplyMipsVolumeScale13(rightMixSquaredMinusOne * toneVolumeSquaredMinusOne) * programVolumeSquaredMinusOne);

        var left = ApplyMipsVolumeScale11(leftBase * (leftPanWeight * leftPanWeight));
        var right = ApplyMipsVolumeScale11(rightBase * (rightPanWeight * rightPanWeight));

        return (left, right);
    }

    /// <summary>(x+1)^2 - the original's own tone/program/mix "squared volume" step (SoundManager.cs's
    /// own <c>SquarePlusOne</c>, despite the name only adding one BEFORE squaring).</summary>
    private static int SquarePlusOne(int value)
    {
        var adjustedValue = value + 1;
        return adjustedValue * adjustedValue;
    }

    /// <summary>Divides by 0x3f01 (16129) through the same MIPS signed multiply-high magic constant the
    /// original uses (<c>ApplyMipsVolumeScale13</c>, SoundManager.cs:5259-5262).</summary>
    private static int ApplyMipsVolumeScale13(int value)
    {
        return ApplySignedMagicScale(value, unchecked((int)0x80020009), 13);
    }

    /// <summary>Divides by 0x3fff (16383) through the same MIPS signed multiply-high magic constant the
    /// original uses (<c>ApplyMipsVolumeScale11</c>, SoundManager.cs:5264-5267).</summary>
    private static int ApplyMipsVolumeScale11(int value)
    {
        return ApplySignedMagicScale(value, unchecked((int)0x8418828d), 11);
    }

    /// <summary>Straight port of <c>ApplySignedMagicScale</c> (SoundManager.cs:5269-5274): MIPS uses
    /// signed multiply-high magic constants instead of a direct division in the tone-mix path.</summary>
    private static int ApplySignedMagicScale(int value, int magic, int shift)
    {
        var product = (long)value * magic;
        var high = (int)(product >> 32);
        return ((high + value) >> shift) - (value >> 31);
    }
}
