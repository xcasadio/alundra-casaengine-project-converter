# h3bin : lecture au binaire de la bombe (objet 0x23) et des graines (objet 0x1F)

Lecture seule, 2026-10-18. Rien n'a ete ecrit dans le depot, dans `data-extracted/`, `alundra-project/`, ni dans le binaire. Tout est sous ce dossier.

Le rapport est le message final de l'agent lecteur (l'outil d'ecriture a refuse un fichier `report.md`, comme pour h4). Les faits y sont etiquetes FAIT / INFERENCE / INCONNU ; preuves [L] lu, [V] vecteurs, [M] mesure en jeu emule, [D] donnees, [DLL], [DEC].

- `run/` : copie de travail du harnais d'oracle de `docs/plan-e20-annexe/e20-h4/work/oracle` (interpreteur MIPS, monde, vraie `SpawnWarpEntity`, vraies passes d'entites, passe de coups) et scripts de cette lecture (`h3_*.py`, `sc_*.py`, `probe_*.py`, `classify_survivors.py`). Toujours lancer depuis `run/` : `python -B <script>`.
  - `run/lst/` : listings du binaire des fonctions lues (`*_annotated.txt` : desassemblage annote qui suit le flot).
- `tools/` : outils statiques (`lin.py` desassemblage lineaire, `dis2.py` annote, `scan_jal.py` appelants, `xref.py` references, `dscan.py` gestionnaires D/C qui lisent l'attaque, `scan68.py`), `exelib.py`, `mipsdis.py`, et `tools/corpus/` (recensements des cartes, `scan1.py` a `scan6.py`, a lancer depuis `tools/corpus/`).
- `results/` : sorties enregistrees (`scenarios/`, `goldens_h3.json`, `mut_models_*.txt`, `mut_words_*.txt`, `mut_words_summary.txt`, `bomb_damage_table.txt`, `bomb_damage_by_sheet.json`, `explosion_grid.txt`, `vec_final.txt`).

Les scripts lisent `ALUN_CD.EXE` (France, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`) au chemin absolu nomme dans `run/lib.py` et `tools/exelib.py`, et les cartes extraites au chemin absolu du depot ; ils n'ecrivent que sous ce dossier. Les originaux du harnais (`docs/plan-e20-annexe/e20-h4/` et `e20-g1a/`) sont inchanges.

## Commandes de rejeu (depuis `run/`)

| But | Commande |
|---|---|
| Vecteurs : vrai code contre modeles, 12 fonctions, memoire comparee octet par octet, journal des appels simules | `python -B h3_vec.py 20000 100` (une seule fonction : ajouter `start_beans start_bomb drv_beans drv_bomb cancel_beans cancel_bomb dispatch C62 D30 E12 C73 E21`) |
| Couverture des instructions | imprimee par la meme commande (`coverage ...`) |
| Mutants des modeles (sensibilite des vecteurs) | `python -B h3_mut.py --shard K/8 --n 2500` pour K = 0..7 (79 mutations) |
| Mutants a un mot du vrai code (en memoire) | `python -B h3_wmut.py <nom> --n 500 --seed 9` puis `python -B classify_survivors.py` |
| Vecteurs de reference pour un rejeu en C# | `python -B h3_goldens.py 40` -> `results/goldens_h3.json` (480 vecteurs : mots d'entree, mots changes, retour, journal) |
| Chronologies en jeu emule | `sc_throw.py 0x23` ou `0x1F` ; `sc_misc.py <fuse chain grass wall wall2 limit3 locked water air_start air_throw carry_actions death relift thrown_in_water>` ; `sc_beans.py <basic pot hurt water carry_long>` ; `sc_hurt.py 222` ; `probe_birth.py` |
| Zone de coup de l'explosion | `python -B sc_explosion.py` (grille) ou `python -B sc_explosion.py 36,0 24,20` |
| Degats de l'explosion sur les 512 fiches | `python -B sc_damage_table.py` |
| Regle de degats des sorts (h4), rejouee | `python -B h4_damage.py` |
