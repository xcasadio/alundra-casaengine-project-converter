## Critique de l'inventaire enemies2 (lecture seule)

**Verdict : fiable pour planifier les tranches, avec trois réserves.** La règle de répartition native/scriptée et les totaux de section 1 se vérifient. Plusieurs lignes et l'affirmation sur le Beannoïde partiel ne se vérifient pas avec les sources accessibles. Je n'ai rien écrit, ni dans le dépôt ni dans le scratchpad.

### Affirmations vérifiées

- **Règle de dispatch (CONFIRMÉ)** : `docs/plan-e14-annexe/e14a/check.md:75` (`0x800388F8-0x80038944`, statut CONFIRMED). La DLL suit la même logique à `Alundra/Scripts/AlundraEventProgramRunner.cs:347-349` (`ProgramIndexes[slot] & 0x7f == 0`, puis code natif).
- **E0, E1, E10 en ligne pour tout type (CONFIRMÉ)** : `AlundraEventProgramRunner.cs:378`, `IsInlineDeactivateCode` ne teste que le code, pas le type.
- **Registre de production (CONFIRMÉ)** : `AlundraNativeSpriteRegistry.cs:29-36`. Beannoïde A1/C1/D1 pour le type 335 (`AlundraBeannoideAi.cs:20-31`), Muruta A0/C10/D9 pour le type 347 (`AlundraMurutaAi.cs:22-45`), vague d'épée C61 pour les types 27, 28, 29 (`AlundraWaveSwordAi.cs:14-17`).
- **`Register` lève une exception sur clé déjà inscrite (CONFIRMÉ)** : `AlundraNativeSpriteRegistry.cs:53-55`. Les lignes 47-60 de l'inventaire sont justes.
- **Totaux de la section 5 (CONFIRMÉ)** : `roster.tsv` compte 52 lignes enemy ou boss, dont 13 boss, soit 39 enemy. La section 5 liste 37 ennemis et 13 boss, soit 50 non portés, ce qui réconcilie.
- **Ligne 27, épée d'onde (CONFIRMÉ)** : `roster.tsv:28` indique `A0:none C61:registry E10:inline`.
- **E11 n'est pas un alias (CONFIRMÉ)** : `docs/plan-e20-annexe/e20-armes/e0-check.md:75` (S14, E11 = `0x8007F25C`, appel récursif `0x8003A648`). Aucun butin : `e0-check.md:187`.
- **Compte des E11 de boss (CONFIRMÉ)** : huit lignes boss ont E11 dans la table (509, 418, 476, 506, 479, 488, 446, 463).
- **Ordre de l'histoire (CONFIRMÉ en partie)** : `docs/plan-e19-opcodes.md:382` (chaîne 389→…→163) et `:2564-2566` (10/14/15, Tarn's Manor, 44, 362). Le détail par carte n'est pas vérifié.
- **459 et 460 (CONFIRMÉ)** : `roster.tsv:345`, D46 appelle `ComputeNewHp` `0x8003A374`, classe enemy, 102 mots.

### Affirmations non vérifiées ou contradictoires

- **Beannoïde partiel, `0x41 [C,99]`, `[D,44]`, `[C,96]` sur les cartes 472 et 473 : INCONNU.** Aucune occurrence dans `docs/`. Seul le script de l'inventaire le contient (`report_gen.py`, `report.md`, `roster.tsv`). Il faut une relecture indépendante des programmes de ces cartes.
- **Zazan (506) et E11 : incohérence interne.** Section 6 dit « 7 par l'en-tête, Zazan par `0x41 [E,11]` », mais la table boss donne `E11` dans les colonnes de la ligne 506. Il faut dire si ce E11 vient de l'en-tête ou du programme.
- **Section 1 contre section 3.** La section 1 dit « portés aujourd'hui : seulement 335 et 347 ». La section 3 ajoute C61 pour les types 27 à 29. Le rapport doit dire si l'épée d'onde compte comme porté (elle n'est pas un ennemi).
- **« 6 722 enregistrements à A0 natif ne l'exécutent pas » : non vérifié.** Pas de décompte indépendant.
- **Chiffres bruts (1 566, 1 233, 171 cartes, 2 309 sur 5 651, 7 506 sur 24 007) : non recalculés**, faute de script. Ils sont internes au pipeline.
- **Gardes de nom de la décompilation (161, 132, 29) : non vérifiés.**
- **Carte 362 sans type non porté** (`report.md` section 5) : non vérifié, la carte 362 est dans la chaîne (`plan-e19-opcodes.md:2566`).
- **« Dix D en forme HitReaction » et partage D6/D11 : non vérifiés.** Le partage est plausible car `Register` interdit les doublons de clé (`AlundraNativeSpriteRegistry.cs:53-55`).
- **Adresse de l'E11 dans la décompilation : introuvable.** Une recherche de `8007F25C` et `8003A648` dans `alundra-datas-analyser/AlundraTools/AlundraEngine/` ne donne rien.

### Manques

- Le rapport n'explique pas la règle de naissance (`SpriteDirection & 0x40`, 826/740) ; je ne l'ai pas vérifiée.
- Le recensement du socle (`0x800808DC`, `CanMoveForward`, etc.) n'est pas recoupé avec les plans.
- Les gardes E4, E8, E15, E27 citant le lézard restent INCONNU, ce qui est honnête.

### Conclusion

Le rapport peut servir de base aux tranches pour la règle de répartition, les totaux de section 5 et la liste des ennemis non portés. Avant de planifier, il faut recalculer le Beannoïde partiel et les 1 233 enregistrements, et trancher les deux incohérences (Zazan/E11, épée d'onde comme « porté »).