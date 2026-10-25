# E20.f6b, T5 (2026-10-25) : les mutants du code de la DLL et l'ADR

Plan : `docs/plan-e20-heros.md` section 1.22.2, T5. HEAD de depart `38cda53b`, branche `chantier/theme2-attaques-objets`. Le plan n'est pas touche ; les deux verifiers frais sont a la charge de la session principale.

## Mutants reels (`mutate_f6b_t5.py`, sortie complete dans `mutants-out.txt`)

Chaque mutant est applique par script (une modification textuelle d'un fichier, relu depuis `HEAD`), `Alundra.Tests` est construit en Release (copie neutralisee), la suite ciblee rapide est lancee (`BallAiTests`, `BallProductionTests`, `BallFlamesTests`, `NativeSpriteRegistryTests`, `WeaponSpawnSeamTests`, `MarkedEntityGroundSlopeTests`) ; si rien n'est rouge, la suite ciblee complete (la precedente + tout `Ai.Projectiles.` + `LiftableAiRegistrationTests`). Le fichier est ensuite restaure et `git diff -- <fichier>` est vide (verifie apres chacun : 24 fois sur 24).

**24 mutants, 24 tues, 0 survivant.** Les 16 du plan y sont tous (la colonne « plan ») ; 8 s'y ajoutent.

| # | Mutant | Plan | Etage | Rouges |
|---|---|---|---|---|
| 01 | cinq effets -> quatre | oui | rapide | 21 |
| 02 | periode de la bouffee 8 -> 4 | oui (periode 8) | rapide | 12 |
| 03 | pente 1 oubliee | oui | rapide | 2 |
| 04 | pente 4 oubliee dans la branche A (appels suivants) | oui | rapide | 2 |
| 05 | destruction avec l'effet 5 | oui | rapide | 3 |
| 06 | bouffee avant la destruction | oui | complet (`BallLifeTests` B5 branche A, 3 rouges) | 3 |
| 07 | decalage de Z du feu applique a la glace (traînee) | oui | rapide | 18 |
| 08 | idem (bouffee) | oui | rapide | 8 |
| 09 | vitesse non opposee (X) | oui | rapide | 16 |
| 10 | vitesse non opposee (Z) | oui | rapide | 16 |
| 11 | effet 17 pour la glace (traînee) | oui | rapide | 18 |
| 12 | effet 17 pour la glace (explosion) | oui | rapide | 8 |
| 13 | tirages faits a l'appel, hors de l'application de la file | oui | rapide | 1 |
| 14 | cle C60 oubliee | oui | rapide | 23 |
| 15 | cle E9 oubliee | oui | rapide | 14 |
| 16 | type 10 cable en production | oui | rapide | 2 |
| 17 | type 13 cable en production | oui | rapide | 2 |
| 18 | ensemble sans 3 (l'epee) | oui (« ensemble avec 3 », voir ecarts) | rapide | 2 |
| 19 | ensemble sans 15 (glace chargee) | | rapide | 5 |
| 20 | boule a la vitesse de l'animation courante (D-F5-5 : `KeyAnimationOf` sans `HasLogicalZ`) | oui | rapide | 8 |
| 21 | alimentation de la pente a toutes les entites (`if (true)`) | oui | rapide | 1 |
| 22 | periode de la traînee 4 -> 8 | | rapide | 36 |
| 23 | Z de la traînee sans le +1 du binaire | | rapide | 18 |
| 24 | branche A qui retourne apres la destruction sur l'eau | | rapide | 2 |

Tueurs principaux : 01-02, 05, 07-12, 22-23 `BallAiTests` (B1 a B3) ; 03-04, 24 `BallAiTests` B1/B2 ; 13 `B4` ; 14-15 `B7`, `M1`/`M3` (`BallFlamesTests`) et le compte de `NativeSpriteRegistryTests` ; 16-18 `BallProductionTests.P4` et `WeaponSpawnSeamTests.S5` ; 19 `P1`, `P3`, `P4` ; 20 `P1` (huit cas) ; 21 `MarkedEntityGroundSlopeTests.S2` ; 06 `BallLifeTests.B5`.

Aucun survivant : aucun test ajoute, aucune preuve d'equivalence a ecrire (les deux equivalents du jeu d'oracle, E9-5b et E9-9, sont deja prouves en T0 sur la machine).

## ADR

`docs/decisions/0098-...md` (numero recompte sur toutes les branches locales et distantes : dernier 0097, aucun 0098 ailleurs), index mis a jour. Ce n'est pas un amendement d'ADR-0084 (celui-ci porte la fleche chargee) : f6b prend des decisions neuves (D-F6-3 a D-F6-10 : C60, E9, cles, ordre des phases, ensemble).

## Ecarts au plan

1. « ensemble avec 3 » : le 3 (l'epee) est deja dans l'ensemble de production ; la lecture retenue est le mutant « ensemble sans 3 » (et sans 15), en plus des types 10 et 13 cables.
2. Un mutant (06) n'est tue que par l'etage complet (`BallLifeTests`), pas par le rapide.
3. Le script a produit lors de sa mise au point deux faux « SURVIVED » (T5-01, T5-02 : lecture du resume localise) ; ils ont ete rejoues apres correction et sont tues ; seule la sortie finale est versee.
4. La session principale lance les deux verifiers frais ; aucune suite complete n'a ete relancee ici (le code de la DLL est inchange depuis T4, `git diff` vide).
