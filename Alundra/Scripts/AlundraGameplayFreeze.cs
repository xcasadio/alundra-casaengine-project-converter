#nullable enable
using CasaEngine.Framework.Scene.Entities.Components;

namespace Alundra.Scripts;

/// <summary>
/// E13.d D2 (docs/plan-e13d-inventaire.md, D-E13D-15): the part of the world freeze the T2 gate cannot
/// reach, because the ENGINE runs it, outside every gameplay proxy.
///
/// <para>While <see cref="AlundraGameState.PlayerControlBits.GameplayBlockedMask"/> is posed (an open
/// inventory, a dialogue box - every MenuOpen, D-E13D-15), the original skips <c>UpdateEntities</c> for
/// every entity, hero included: no event, no physics, no animation (EntityManager.cs:367-390). The port's
/// T2 gate already skips the DLL's own passes (AlundraEntityScriptProxy.Update), but two engine systems
/// keep going on their own (plan §1.2, measured by D0.3): the character controller integrates gravity on
/// every rendered frame (CharacterControllerComponent.Update), and sprite animations keep advancing
/// (AnimatedSpriteComponent.Update). This freezes both, and gives them back untouched when the mask is
/// lifted, so a fall resumes where it stopped - like the original, which simply resumes calling
/// <c>UpdateEntities</c>.</para>
///
/// <para><b>The controller</b>: <see cref="CharacterControllerComponent.ControlMode"/> can only be changed
/// through <see cref="CharacterControllerComponent.SetControlMode"/>, and for
/// <see cref="CharacterControlMode.Disabled"/> that calls <c>Stop()</c>, which wipes the velocity, the jump
/// and dash state and their timers. So the whole state is captured first
/// (<see cref="CharacterControllerComponent.CaptureStateSnapshot"/>) and restored at the thaw
/// (<see cref="CharacterControllerComponent.RestoreStateSnapshot"/>). The restore resets on its own the last
/// contact and collision/step hits and the ground's support reference, all recomputed by the next step,
/// and the position, which nothing moves during the freeze since everything that does sits behind the
/// gate. It also resets the external vertical displacement latch, which is NOT harmless: the DLL declares
/// it only once per logic tick, so on a thaw frame with zero ticks the controller would run with the
/// vertical owned externally and a latch of 0 - and UpdateGround would snap to the ground anything within
/// GroundSnapDistance, the very regression once measured for a hero clinging to a ladder
/// (AlundraScriptedMotion.cs:117-130). The thaw therefore declares it again right away, with the value its
/// owner declares every tick (<see cref="OwnerExternalVerticalDisplacement"/>).</para>
///
/// <para><b>The animations</b>: <see cref="AnimatedSpriteComponent.IsPlaybackPaused"/> is posed, and its
/// previous value given back at the thaw, so an animation something else had paused stays paused.</para>
///
/// <para>Applied from the END of <see cref="AlundraWorldProxy.Update"/>, to every spawned entity: the
/// engine updates the controllers and the sprites BEFORE the gameplay proxies each frame, and MenuOpen is
/// posed and lifted during this frame's proxies, so freezing or thawing here takes effect from the very next
/// engine update - a freeze applied from the entity's own proxy would let one more frame of fall through.
/// An entity spawned during a freeze is frozen on the frame it appears.</para>
/// </summary>
internal static class AlundraGameplayFreeze
{
    /// <summary>What one entity's freeze took away, to give it back.</summary>
    internal sealed class State
    {
        internal bool IsFrozen { get; set; }

        internal CharacterControllerStateSnapshot? ControllerSnapshot { get; set; }

        /// <summary>The sprite's own <see cref="AnimatedSpriteComponent.IsPlaybackPaused"/> before the
        /// freeze; null when the entity has no animated sprite.</summary>
        internal bool? AnimationWasPaused { get; set; }
    }

    /// <summary>Freezes or thaws <paramref name="proxy"/> when <paramref name="gameplayBlocked"/> changed
    /// since the last call; does nothing otherwise.</summary>
    internal static void Apply(AlundraEntityScriptProxy proxy, bool gameplayBlocked)
    {
        if (gameplayBlocked && !proxy.FreezeState.IsFrozen)
        {
            Freeze(proxy);
        }
        else if (!gameplayBlocked && proxy.FreezeState.IsFrozen)
        {
            Thaw(proxy);
        }
    }

    internal static void Freeze(AlundraEntityScriptProxy proxy)
    {
        var state = proxy.FreezeState;

        if (proxy.Controller is { } controller)
        {
            state.ControllerSnapshot = controller.CaptureStateSnapshot();
            controller.SetControlMode(CharacterControlMode.Disabled);
        }

        if (proxy.AnimatedSprite is { } sprite)
        {
            state.AnimationWasPaused = sprite.IsPlaybackPaused;
            sprite.IsPlaybackPaused = true;
        }

        state.IsFrozen = true;
    }

    internal static void Thaw(AlundraEntityScriptProxy proxy)
    {
        var state = proxy.FreezeState;

        if (proxy.Controller is { } controller && state.ControllerSnapshot is { } snapshot)
        {
            controller.RestoreStateSnapshot(snapshot);
            if (controller.IsVerticalOwnedExternally)
            {
                controller.SetExternalVerticalDisplacement(OwnerExternalVerticalDisplacement(proxy));
            }
        }

        if (state.AnimationWasPaused is { } wasPaused && proxy.AnimatedSprite is { } sprite)
        {
            sprite.IsPlaybackPaused = wasPaused;
        }

        state.IsFrozen = false;
        state.ControllerSnapshot = null;
        state.AnimationWasPaused = null;
    }

    /// <summary>The value the entity's owner declares every tick as its external vertical displacement:
    /// the climbing sentinel for a hero clinging to a ladder (AlundraScriptedMotion.cs:140-144), this tick's
    /// resolved vertical force for a controller-driven NPC (AlundraEntityScriptProxy.Update's own trailing
    /// declaration), and 0 otherwise - the value <c>Stop()</c> itself leaves.</summary>
    internal static float OwnerExternalVerticalDisplacement(AlundraEntityScriptProxy proxy)
    {
        if (proxy.IsPlayer)
        {
            return proxy.TargetAnimationId is AlundraPlayerManager.ClimbingAnimationId or AlundraPlayerManager.ClimbStillAnimationId
                ? AlundraScriptedMotion.ClimbingExternalDisplacementSentinel
                : 0f;
        }

        return proxy.FinalForceZ / 65536f;
    }
}
