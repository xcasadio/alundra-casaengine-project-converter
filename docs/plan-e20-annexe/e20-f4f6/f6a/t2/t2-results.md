# E20.f6a, T2 : résultats (2026-10-23)

HEAD de départ de T2 : `f6788006` (T1). Branche `chantier/theme1-porter-plateforme`. Les deux verifiers frais sont lancés par la session principale (hors de ce passage).

## 1. ADR

`docs/decisions/0087-the-ground-slope-of-the-marked-entities-with-gravity-is-fed-in-the-active-statuses.md` (index mis à jour). Numéro recompté sur toutes les branches (`git branch -a`, `ls-tree docs/decisions/`) : le plus haut pris est 0086, 0087 est libre.

## 2. Mutants du plan, rejoués sur les octets finaux de T1 (script `../t1/t1mut.py`, mutations réelles, construction Release en copie neutralisée, tests de la tranche et `GroundSlopeTests` du héros ; fichiers restaurés, `git diff` vide sur `Alundra/` après)

| Mutant du plan | Nom | Tué par |
|---|---|---|
| appel pour tous les gravitaires | M1 | S2, S4 |
| appel sans marqueur | M2 | S2 |
| minimum remplacé par maximum | M3 | 24 échecs : S1a x5, S1b x4, S1c, S2, S3a, S3b x3, S4 et 8 tests du héros |
| égalité de Z décalée d'une unité | M4 | 21 échecs : S1a, S1b, S1c, S2, S3a, S3b, S4 et 5 tests du héros |
| coin ignoré (les quatre coins un à un) | M5a, M5b, M5c, M5d | S1b, chacun par exactement une des quatre clés `fire11_dry_corner_*` |
| (ajout de la révision 3) appel limité au statut 2 | M6 | S4 |

**9 mutants sur 9 tués.**

## 3. Suites (une passe complète dépasse dix minutes : trois partitions disjointes dont la somme est le total)

Partitions par filtre `FullyQualifiedName` : P1 = `Arcs|Ai.|Audio|Cameras|Cells|Combat|Data|Dialogues` ; P2 = `Entities|Events|ExportGuards|Hero|Items` hors P1 ; P3 = tout le reste. (Les filtres du T0/T1 recouvraient par endroits ; ici les trois ensembles sont disjoints, d'où des comptes par partition différents de T0/T1 pour le même total.)

| Config | P1 | P2 | P3 | Total | Échecs |
|---|---|---|---|---|---|
| Release (copie neutralisée) | 1 314 | 2 749 | 1 564 | **5 627** | 0 |
| Debug (en dernier) | 1 314 | 2 749 | 1 564 | **5 627** | 0 |

5 606 au départ de f6a + 21 cas de T1 = 5 627.

## 4. Gardes

- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identique.
- `python -B tools/dll-structure/check_names.py --rules` : 0 finding, 1 avertissement connu (`TextBoxGpu.cs`).
- Moteur (`CasaEngineMonogame` `92d8e847`), analyseur (`d6879724`), convertisseur (`tools/`, hors scripts non suivis) et données : aucun diff depuis `e295875a` (`git diff e295875a HEAD --name-only` : seuls `Alundra/Entities/EntityScriptProxy.cs`, les 3 fichiers de tests, `CutBench.cs` et l'annexe/ADR).
- `docs/hero-trace-389-*.txt` réécrits par les suites : `git diff --ignore-cr-at-eol` vide, restaurés, jamais indexés.

## 5. Tests existants déplacés, contre la liste prédite

Prédite (T0) : **vide**. Mesurée (suites Release et Debug, 0 échec ; seul `CutBench` reçoit un champ optionnel) : **vide**. Aucun écart.

## 6. Écarts au plan

Aucun. L'ADR est écrit parce que T2 du plan l'annonce ; les verifiers frais restent à lancer par la session principale.
