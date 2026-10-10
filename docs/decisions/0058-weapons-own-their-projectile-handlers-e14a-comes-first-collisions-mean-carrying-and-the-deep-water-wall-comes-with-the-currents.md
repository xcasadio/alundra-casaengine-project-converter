# ADR-0058: The weapons own their projectile handlers, E14.a comes before the weapons, collisions mean carrying and solid crates, and the deep-water wall comes with the currents

- **Status**: Accepted
- **Date**: 2026-10-10
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 0.1 (D-E20-25 to D-E20-27, D-E20-31, D-E20-32), `docs/plan-conversion-totale.md` section E14 (D-E14-8), the author's answers of 2026-10-10 (« 1 a, 2 b, 3 a, 4 a, 5 a, 6 a, 7 a, sonde, 9 a ») to the questions of the paused envelope revision (section 1.1) and of slices E20.d and E20.i; discovery `docs/plan-e20-annexe/e20-armes/`, `e20-d/`, `e20-i/`

## Context

- D-E20-18 (ADR-0053) gave the arrow handler E17 and the ball handlers C60 (and E9) to E14 on the premise that enemies share them; the census shows they are used only by the hero's projectile sprites.
- D-E14-2 (ADR-0055) exports the break-effect byte inside E14.a; the charged arrow (E20.f4) needs it.
- D-E20-24 (ADR-0054) ordered weapons, item use and collisions first, with "collisions" read as E20.g; D-E14-4 placed E14.a after them.
- Without boots of level 2 or more the original makes deep water a wall (`0x80037504`, O-E19-22); the currents (`+0x188`, O-E20-16) had no owner.
- The analog-stick rule of D-E20-15 needs a measure of the author's controller.

## Decision

- **D-E20-25** (amends D-E20-18). E17, C60 and E9 are E20.f's (slices E20.f5 and E20.f6); C54 stays with E14.
- **D-E20-26** (confirms D-E14-2). The break-effect export stays inside E14.a; the charged arrow uses it once E14.a is done.
- **D-E20-27** (settles D-E20-24's reading). "Collisions" means E20.g: carrying and throwing, with crates and jars solid again.
- **D-E14-8** (amends the order of D-E20-24 and D-E14-4). E14.a (E14.a0, E14.a0b, E14.a1) comes first, so the hall of map 115 becomes playable; its plan (`docs/plan-e14-ia-native.md`) is approved.
- **D-E20-31.** The deep-water wall (O-E19-22) and the currents (O-E20-16) are ported together in a slice E20.i2.
- **D-E20-32.** For the stick rule (E20.d2), a probe is built first; the author measures his controller with it, then the rule is closed.

## Consequences

- E20.f5 and E20.f6 are no longer blocked; their envelope edges stand.
- E20.e0x exports only the global sprite table; E14.a1 adds the break effect.
- The next work order: E14.a, then the weapons, item use and E20.g; E20.f1b (aura) and E20.d2's probe can run alongside.
