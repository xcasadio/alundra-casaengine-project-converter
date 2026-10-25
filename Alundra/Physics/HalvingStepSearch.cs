#nullable enable
using System;

namespace Alundra.Physics;

/// <summary>
/// E20.e0k5 T1 (docs/plan-e20-heros.md section 1.18.5, D-E0K5-1): the step search of the binary (<c>ComputeXYPosition</c> <c>0x80037730</c>) for an entity without an entity candidate and without a slide (a projectile: NoObstacleSlide), as a pure function.
/// The vector (16.16 raw units) is tried whole; when the wall test refuses it, X and Y are halved together and the half is tried from the same position; a sub-step that is accepted moves the position and the loop goes on with the half of the rest
/// from there (the accepted positions accumulate); the first whole step that is accepted ends the search. A half is <c>sra 1</c> (an arithmetic shift) except that -1 becomes 0 (the binary's rule at <c>0x80037950</c> and <c>0x80037D74</c>).
/// The loop ends when both halves are zero (cardinal direction: <c>TargetDirection &amp; 7 == 0</c>) or when either is zero (any other direction), after a refused attempt as after an accepted one. The 'largest free position' is NOT the rule:
/// with a wall just above 109 pixels, the start 107 and the step 3 pixels end at 109 minus one raw unit (7143423), not at 109.
/// The caller supplies the wall test <paramref name="isBlockedAt"/> (the offset from the start, in raw units: <c>true</c> = refused) and raises <c>ForceAdjusted</c> (<c>+0x13C</c>) itself when no sub-step was accepted
/// (<see cref="HalvingStepResult.Accepted"/> false) for a non-zero vector. A zero vector makes no query and accepts nothing (the binary returns before any attempt and raises nothing).
/// </summary>
public static class HalvingStepSearch
{
    public static HalvingStepResult Search(int forceX, int forceY, bool cardinal, Func<int, int, bool> isBlockedAt)
    {
        var stepX = forceX;
        var stepY = forceY;
        if (stepX == 0 && stepY == 0)
        {
            return new HalvingStepResult(0, 0, false);
        }

        var acceptedX = 0;
        var acceptedY = 0;
        var accepted = false;
        var attempt = 0;
        while (true)
        {
            if (!isBlockedAt(acceptedX + stepX, acceptedY + stepY))
            {
                accepted = true;
                if (attempt == 0)
                {
                    return new HalvingStepResult(stepX, stepY, true);
                }

                acceptedX += stepX;
                acceptedY += stepY;
            }

            stepX = Half(stepX);
            stepY = Half(stepY);
            var goOn = cardinal ? stepX != 0 || stepY != 0 : stepX != 0 && stepY != 0;
            if (!goOn)
            {
                return new HalvingStepResult(acceptedX, acceptedY, accepted);
            }

            attempt++;
        }
    }

    private static int Half(int value) => value == -1 ? 0 : value >> 1;
}
