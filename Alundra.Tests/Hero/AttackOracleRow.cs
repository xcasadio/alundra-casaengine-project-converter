#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Hero;

/// <summary>One row of an E20.b attack-oracle trace: the state AFTER the passes of one logic tick of the real <c>MovePlayer</c> of <c>ALUN_CD.EXE</c> (France) in the D1 oracle.</summary>
/// <param name="Tick">The 1-based logic tick (0 for the start state).</param>
/// <param name="Hold">The pad word held on that tick (the tick pad derives its edges from it).</param>
/// <param name="PosZDll">PosZ in the DLL convention (the binary's value minus the +1 of its rest).</param>
/// <param name="RecPtr">The pending-spawn pointer <c>0x80127110</c> (an address of the executable), 0 when none.</param>
/// <param name="RecCnt">The counter <c>0x80127114</c>.</param>
/// <param name="Sounds">The sounds asked on the tick (<c>sound(N)</c> with N &gt; 0: a call with 0 is a call of the function, not a sound, and the DLL asks none).</param>
/// <param name="Spawns">The number of spawn calls (<c>0x80039F58</c>) journalled on the tick.</param>
internal sealed record AttackOracleRow(
    int Tick, uint Hold, uint AnimTarget, uint AnimCur, int DirTarget, int ForceX, int ForceY, int ForceZ, int PosX, int PosY, int PosZDll, int OnGround,
    long RecPtr, int RecCnt, int[] Sounds, int Spawns);
