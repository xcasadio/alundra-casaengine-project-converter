#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Hero;

/// <summary>One row of an E20.f2 flail probe (<c>docs/plan-e20-annexe/e20-f2/oracle/f-weapons/out/F-*.txt</c>): the state AFTER one tick of the real <c>MovePlayer</c> of <c>ALUN_CD.EXE</c> (France).</summary>
/// <param name="Tick">The 1-based tick of the scenario (the first tick after the warm-up).</param>
/// <param name="Hold">The pad word held on that tick.</param>
/// <param name="AnimTarget">The target animation (<c>+0x88</c>).</param>
/// <param name="AnimCurrent">The current animation (<c>+0x90</c>).</param>
/// <param name="PosZDll">PosZ in the DLL convention (the binary's minus its +1).</param>
/// <param name="OnGround"><c>+0x144</c>.</param>
/// <param name="RecPtr">The pending-spawn pointer <c>0x80127110</c>.</param>
/// <param name="RecCnt">The counter <c>0x80127114</c>.</param>
/// <param name="Sounds">The sounds asked on the tick with an id above 0 (a call with 0 is the call of the function with "no sound").</param>
/// <param name="ZeroSounds">The number of calls with the id 0 on the tick.</param>
/// <param name="HasLandingEffect">True when the tick journals the dust effect of the landing (<c>effect_3BDD8</c>, id 7).</param>
internal sealed record FlailProbeRow(
    int Tick, uint Hold, uint AnimTarget, uint AnimCurrent, int ForceX, int ForceY, int ForceZ, int PosZDll, int OnGround, long RecPtr, int RecCnt, int[] Sounds, int ZeroSounds, bool HasLandingEffect);

/// <summary>Who asked one sound or effect of a probe (<c>F-sound-callers.txt</c>): <c>CreatePlayerAnimationEffects</c> (the return address is inside the function), <c>UpdateAnimation</c> (the sound of the animation set) or other.</summary>
internal sealed record FlailCall(string Probe, int Tick, string Kind, int Id, string Origin);
