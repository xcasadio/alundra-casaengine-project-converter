namespace AlundraCasaEngineProjectConverter.Readers;

/// <summary>
/// How an animation's trailing control frame (SpriteFrame.TerminatorCode, see SpriteFrame's own
/// doc) ends playback, mirroring EntityManager.UpdateAnimation's three-way branch
/// (alundra-datas-analyser AlundraTools/AlundraEngine/Gameplay/EntityManager.cs:257-281):
///  - Loop: TerminatorCode == 1 -&gt; AnimationFrameIndex resets to 0, AnimCompleteCounter++
///    (:257-263).
///  - Hold: TerminatorCode != 1, (TerminatorCode &amp; 0x80) == 0 (always true in the shipped
///    data - every control frame's raw Delay is &lt; 0x80), and the control frame's
///    TransformIndexLow has bit 7 set -&gt; NextFrameDelay is pinned to int.MaxValue and
///    ForceResetAnimationFlag is raised, freezing the entity on its last displayed pose (:267-275).
///  - Chain: same TerminatorCode gate, TransformIndexLow's bit 7 clear -&gt; TargetAnimationId is
///    set to the control frame's TransformIndexLow AS A FULL, UNMASKED BYTE
///    (EntityManager.cs:277 assigns <c>lastFrame.TransformIndexLow</c> directly with no
///    <c>&amp; 0x7f</c>, and SiFrame.TransformIndexLow is itself an unmasked byte field - see
///    SiFrame.cs) - so ChainTo is the unmasked byte (a Chain only exists when bit 7 is clear, hence 0-127 in practice; 93 is the corpus maximum), and UpdateAnimation recurses immediately
///    on the new animation, same tick (:277-281).
/// </summary>
public enum AnimationEndKind
{
    Loop,
    Hold,
    Chain,
}

public readonly record struct AnimationEndResult(AnimationEndKind Kind, int ChainTargetAnimationId);

public static class AnimationEndClassifier
{
    /// <summary>
    /// Classifies one (AnimSet, direction) animation from its trailing control frame. Every
    /// animation in the corpus (92452 of them, per SpriteFrame's doc) ends with exactly one such
    /// frame (Quads.Count == 0, i.e. SpriteFrame.IsTerminator); when that invariant doesn't hold -
    /// never observed against real data, but not something this converter should trust blindly -
    /// this falls back to Loop, the converter's pre-existing behaviour for every animation before
    /// this classifier existed, and the caller can see it happened via
    /// Sprites.AnimationsMissingTerminator in <paramref name="report"/> so the assumption stays
    /// measurable instead of silent.
    /// </summary>
    public static AnimationEndResult Classify(SpriteAnimation animation, ConversionReport report)
    {
        var frames = animation.Frames;
        if (frames.Count == 0 || !frames[^1].IsTerminator)
        {
            report.Increment("Sprites.AnimationsMissingTerminator");
            return new AnimationEndResult(AnimationEndKind.Loop, 0);
        }

        var terminator = frames[^1];
        if (terminator.TerminatorCode == 1)
        {
            return new AnimationEndResult(AnimationEndKind.Loop, 0);
        }

        if ((terminator.TerminatorCode & 0x80) == 0)
        {
            if ((terminator.TransformIndexLow & 0x80) != 0)
            {
                return new AnimationEndResult(AnimationEndKind.Hold, 0);
            }

            return new AnimationEndResult(AnimationEndKind.Chain, terminator.TransformIndexLow);
        }

        // Never observed in the shipped data (every control frame's raw Delay is < 0x80): when
        // neither the Loop nor the (Delay & 0x80) == 0 gate fires, EntityManager.UpdateAnimation
        // falls through with AnimationFrameIndex still clamped at the terminator, which resolves to
        // the last displayed frame's pose (IsTransitionFrame redirects to frameIndex - 1,
        // EntityManager.cs:288-291) and no advance - the same visible effect as Hold, just without
        // raising ForceResetAnimationFlag (so DeactivateOnAnimationEnd would not fire). Treated as
        // Hold here as the closest faithful behaviour, and counted so it stays visible if the
        // corpus ever contains one.
        report.Increment("Sprites.AnimationsUnclassifiedTerminator");
        return new AnimationEndResult(AnimationEndKind.Hold, 0);
    }
}
