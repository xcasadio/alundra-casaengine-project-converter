# Stick probe (E20.d2a)

The probe lets the author measure what the left stick of his pad really sends, so that E20.d2b can set the rule that
picks the direction of the run's start on a diagonal (D-E20-15, D-E20-32, `docs/plan-e20-heros.md`). It changes no
behaviour: the run keeps the OR of the keyboard, d-pad and stick bits.

## What it journals

Active only when the environment variable `ALUNDRA_DEBUG_STICK_PROBE` is `1` when the game process starts (read once). Without
it nothing is written.

Two kinds of line, written through the engine logs (`Logs.WriteInfo`), so they land in `log.txt` next to the other traces. In
`log.txt` the logger prefixes every line with the time and the level, so a real line reads
`HH:mm:ss [Info] StickProbe 0.7071 0.7071 0 3000`: the fields below start after `[Info] `.

```
StickProbe <x> <y> <D> <S>        once per rendered frame
StickProbe start <state> <nibble>  at each read of the run's start table
```

- `x`: horizontal deflection of the left stick, positive to the right; `y`: vertical deflection, positive upward. Both signed,
  4 decimals, invariant decimal point (`0.7071`, `-0.6000`). They are the `Value` of the engine actions `MoveRightStick` and
  `MoveUpStick` (never a difference of the two halves of an axis), so a full push reads `1.0000`, not 2.
- `D`: the direction bits held by the keyboard and the d-pad, in hex (`1000` Up, `2000` Right, `4000` Down, `8000` Left, OR-ed).
- `S`: the direction bits the stick sets, same bits; a bit is set only from the 0.5 dead zone of the mapping, so a half-way push
  (|axis| under 0.5) shows a non-zero `x` or `y` and an `S` without that bit.
- `start 29 N`: the start of the run in the preparation (state 0x29), read once the preparation is over (25 ticks); `start 2A N`: the
  resume in the slide (state 0x2A), read from its 11th tick on. `N` is the direction nibble the DLL uses today, `ButtonsHold >> 12`
  (the OR of the keyboard, d-pad and stick bits), in hex: `1` Up, `2` Right, `4` Down, `8` Left, `3` Up+Right, and so on.

## Procedure for the author

1. Deploy the Debug DLL of this branch (`Alundra/bin/Debug/net9.0-windows/Alundra.dll` copied to `alundra-project/Alundra.dll`; `cmp`
   must report no difference).
2. Set `ALUNDRA_DEBUG_STICK_PROBE=1` in the environment of the process that launches the game (the launch profile of your IDE, or
   `set ALUNDRA_DEBUG_STICK_PROBE=1` in the console before starting the launcher) and start the game as usual.
3. Load a save in a free field, with the pad plugged in. Hold the left stick, a few seconds each:
   - the eight directions at full deflection (right, up-right, up, up-left, left, down-left, down, down-right);
   - the same eight at half deflection;
   - the four diagonals at exactly 45 degrees, as exact as the hand allows (a stick gate or a finger on the rim helps);
   - nothing touched, for a few seconds (the rest values).
   While holding a diagonal, also hold Triangle for about a second (the preparation then the run: the `start 29` lines appear).
4. Quit the game. The journal is `log.txt` in the working directory of the launched process, rewritten at each launch: copy it
   before starting the game again. It also holds the other traces: `rg StickProbe log.txt` keeps the probe lines (plus one start-up
warning line, `StickProbe: ALUNDRA_DEBUG_STICK_PROBE=1 ...`, which says the probe is on).
5. Send the file (or its `StickProbe` lines).

The working directory of the process depends on how the author starts the game; the launcher opens `log.txt` relative to it
(`CasaEngine.Launcher/Program.cs`, read only).
