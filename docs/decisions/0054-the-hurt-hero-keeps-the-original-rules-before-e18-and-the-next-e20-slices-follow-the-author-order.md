# ADR-0054: The hurt hero keeps the original's rules before E18, and the next E20 slices follow the author's order

- **Status**: Accepted; D-E20-24's order amended by ADR-0058 (D-E14-8: E14.a first; D-E20-27: collisions = E20.g)
- **Date**: 2026-10-09
- **Source**: this chantier: `docs/plan-e20-heros.md`, section 0.1 (D-E20-19 to D-E20-24, the author's answers of 2026-10-09, "fait toutes les recommandations", to the questions raised by the E20.c2 discovery, and his order of the next work), `docs/plan-e20-annexe/e20-c2/check.md` section (d)

## Context

- The E20.c2 discovery (`docs/plan-e20-annexe/e20-c2/`) found that the item-use driver `0x8003634C` and `UpdatePlayerCarriedEntity(2)` (`0x8002F884`) do nothing until an item is used (`[0x80127164]` is written non-zero only at `0x80034A90`, reached through `TryUseItem`) or something is carried (hero `+0x2C`). D-E20-14 (ADR-0053) had them ported in E20.c2.
- Before E18 the hero does not die (D-E20-5); the discovery asked what the hero shows and does at 0 HP.
- The binary hides any entity whose invulnerability counter `+0x1CC` has `& 3 == 3` (draw list, `0x800385D0`-`0x8003860C`); opcode `0x86` sets that counter on scripted entities (76 corpus sites).
- Opcodes `0xC3` (full restore, `0x80041D6C`) and `0xBC` (HpMax + n) were unported and unowned; once the hero's HP lives on the entity, they are the scripted heals.
- The hurt reaction picks `0x3A` on slope 4 (deep water); swimming is E20.i (D-E20-16), so the DLL has no slope-4 state handler yet.
- The author then gave the order of the next work: "les armes, l'utilisation des objets et les collisions".

## Decision

- **D-E20-19** (amends D-E20-14 of ADR-0053). `UpdateItemEffectState` is ported in E20.c2. The item-use driver `0x8003634C` and `UpdatePlayerCarriedEntity(2)` stay named stubs at their exact call sites until E20.h and E20.g, which port them for real.
- **D-E20-20.** Before E18, at 0 HP the hero shows 0 hearts and keeps control, and his regeneration stops, as in the original; only the death branch is missing.
- **D-E20-21.** The invulnerability blink applies to every entity, as in the original, including the entities made invulnerable by `0x86`.
- **D-E20-22.** E20.c2 ports `0xC3` and `0xBC` as writes to the hero entity.
- **D-E20-23.** A hurt in deep water keeps the original's rule (`0x3A` on slope 4) until E20.i; the short hurt it gives in the DLL meanwhile is a documented deviation.
- **D-E20-24.** The next E20 work follows the author's order: weapons (E20.f), then item use (E20.h), then collisions. The main session reads "collisions" as E20.g (carrying and throwing, with crates and jars becoming obstacles again, the end of the D-E19-28 and D-E19-46 deviations); this reading is to be confirmed by the author. Each slice's prerequisites come before it (for example the spawn of E20.e0 before the projectiles); E14.a and the hurt of E20.c2 follow.

## Consequences

- E20.c2 keeps two dead calls as named stubs; E20.h and E20.g own them and must replace the stubs.
- The hero can stand at 0 HP with no way to die before E18; the scripted heals and the regeneration accessories are the ways back.
- Entities under `0x86` start blinking once E20.c2 lands; pinned tests that look at entity visibility move with that slice.
- Weapons and item use arrive before the Muruta and Beannoide (E14.a); the hall of map 115 stays unplayable until E14.a.
