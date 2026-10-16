# E20 themes: coverage of the hero's attacks, carrying and platforms (2026-10-16)

Provenance: read-only discovery of 2026-10-16/17 (the author's fallback themes of the night of 16 to 17 October: "le joueur doit pouvoir
transporter un objet et monter sur une plateforme", "toutes les attaques du joueurs doivent etre implémentés", "implementer toutes les IA des
ennemis", "finir le moteur de script"), run while the DLL restructuring was paused (docs/plan-restructuration-dll.md, Q-R-2). Repository state
read: parent `7d776b3` (branch `chantier/restructuration-dll`), DLL under `Alundra/Scripts/`. Nothing in the repository was built, run or
changed by the discovery. The files are frozen evidence (D-R-6 style): line numbers and paths refer to that state; `SHA1SUMS.txt` lists the
copies. The plan holds the reconciled facts; where the critic refuted or could not verify a claim, the plan says so.

- `attacks.md`: every attack of the hero (weapons, charge, projectiles, items, magic) against the DLL, the tests and `docs/plan-e20-heros.md`,
  with the fresh critic's checks at the end. Correction found after the report: the Crusher's charge `0x19` is implemented and tested
  (`AlundraWeaponTables.cs:61`, `AlundraHeroChargeTests.cs:466-485`, case C10-crusher), not "unknown".
- `carry.md`: carrying, throwing, breaking, lifting, and the platform mechanisms (support by an entity, horizontal and vertical platforms,
  lifts, pushable blocks), with the critic's checks.
