# E20.e, T1 (phase A) : résultats mesurés (2026-10-23)

HEAD de départ `35695ef3` (le commit de T0), branche `chantier/theme1-porter-plateforme`. Phase A : aucun appelant de production, aucun comportement qui change.

## Code

- `Alundra/Cells/ICellMutator.cs` et `CellStore.cs` : l'opération `CutCell(column, row)` (efface le bit `0x02` de la marchabilité, mot de pile à -1, détruit la pile, un seul `CellsMutated`) et la constante `CuttableWalkabilityBit`.
  `SetCellBits` / `ClearCellBits` ne sont pas touchés (le test `SetThenClearCellBits_NeverTriggersOverlayReconstruction` tient).
- `Alundra/Cells/TileEffect.cs` : `Trigger(EntityScriptProxy)`, `ResolveCutEffectIndex(TileMapData?)`, `ColumnOf`, le cœur en deux temps `CutCorners` (au plus 4 sites), puis pour chaque site : l'effet par `EffectStage.Emit`, `LootDrop.SpawnRandomLoot(0xFF, ...)`, le son 31.
- `Alundra/Rendering/Effects/NativeEffects.cs` : `CreateTileCutEffect` (table globale, animation 0, aucun tirage, aucun son).
- `Alundra/Maps/IScriptHost.cs` (`TileCutEffectIndex => 2`) et `WorldProxy.cs` (champ, implémentation explicite, lecture de la propriété `TileCutEffectId` au chargement, juste après `BalanceThreshold`).

## Tests (38 neufs) et banc

- `Alundra.Tests/Support/CutBench.cs` (banc de S-E5 : le champ ET le store sur les mêmes enregistrements, un hôte qui est script host et contexte de monde, une entité à boîte), `CateVectors.cs` (lecteur des 4 000 vecteurs), `AnnexFiles.CutAnnex`.
- `Alundra.Tests/Cells/TileEffectTests.cs` (32 tests, (c1) à (c6) hors monde) et `TileEffectWorldTests.cs` (6 tests : (c1) sur la vraie carte 14 et (c6) la lecture au chargement, y compris la vraie propriété exportée).
- Rouges avant le code : `t1-red-names.txt`. Le squelette qui compile (`CutCell` vide, `Trigger` qui rend la main tout de suite, `ResolveCutEffectIndex` qui rend 2, la lecture au chargement retirée) : 29 rouges, 10 verts. Les 10 verts sont les cas « rien ne se passe »
  (sans boîte, sans mutateur, sans champ, hôte qui n'est pas un monde, bords du haut et du bas de la bande qui sautent, `ResolveCutEffectIndex` des cas de repli, `C2_TheOracleFile…`, `C1_ClearingTheBit…`), qui ne peuvent pas être rouges sur un squelette qui ne fait rien : chacun est accolé à son cas positif rouge.
  (Le total 39 de cette ligne de filtre compte un test existant dont le nom contient `TileEffect` ; les tests neufs sont 38.)
- Vert après le code : 39 sur 39 pour le filtre `TileEffect`.

## Suites

| suite | tests | échecs |
|---|---|---|
| Release, copie neutralisée (build puis test, `--blame-hang-timeout 60s`) | 5 311 | 0 |
| Debug, en dernier | 5 311 | 0 |

5 311 = 5 273 (fin d'e0c) + 38. `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identiques. `python -B tools/dll-structure/check_names.py --rules` : 0 trouvaille, 1 avertissement connu.
Les suites ont réécrit les fins de ligne de `docs/hero-trace-389-*.txt` (contenu inchangé avec `--ignore-cr-at-eol`) : restaurés par `git checkout --`, jamais indexés.
La mesure en ombre de (e) de T0 (câblage temporaire des deux bouchons, 5 311 tests, 1 échec : `HeroAttackTests`) a été faite sur ce code ; le câblage est retiré (`git diff` vide sur `Alundra/Ai` et `Alundra/Hero`).

## Écarts et notes

- Le plan nomme `Trigger` comme entrée unique ; `CutCorners` (interne) est le cœur testé avec `Trigger`, aucune interface neuve (S-E4).
- Hors table (colonne avant 0 ou à partir de 1248 pixels) : un pixel négatif donne la colonne 0, un pixel au-delà de la table garde la formule `p / 24` (le binaire lit une mémoire non définie, Q-E7) ; aucun vecteur n'y est (ils sont dans la table), un test épingle le cas négatif.
- Le mot du générateur de la DLL est sur 64 bits : les tests comparent le mot entier quand la semence est connue (C3) et les 32 bits bas aux vecteurs.
