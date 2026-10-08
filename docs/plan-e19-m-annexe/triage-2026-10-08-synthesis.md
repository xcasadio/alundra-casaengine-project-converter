# Triage of the remaining E19 items, 2026-10-08 (synthesis, French as produced)

**Proposition E19 : restes (triage du 2026-10-08)**

**1. Fait ou périmé : à marquer dans le plan, en un commit de doc après la campagne de tests**
- O-E19-8 : résolu par E19.d2c1 (R1-R3). Le retard restant est l'écart accepté D-E19-13.
- O-E19-18 : résolu par E19.d2b (arc T-A10v).
- O-E19-19 : résolu par E19.d2c1 (C1-C4). La recette C7 reste en attente.
- O-E19-27 : chaque élément a un propriétaire (E19.h3, E19.t, S6, E19.h2, D5b, O-E19-40, O-E19-44).
- O-E19-30 : résolu (R2/R5, G0b/G2a, G2c-6, G3c/G4, E19.s).
- O-E19-54 : réglé, recettes validées sauf la carte 44.
- O-E19-38 : corrigé par E19.m1 (`d3e206f`). Réconcilier M-10, M-04 et M-23 dans le backlog.
- M-42 : fermé par f2a.
- §1.2u passe à ✅. Recettes d'O-E19-60 et O-E19-61 validées le 2026-10-06. O-E19-62 réglé par §1.2w, recettes LB-4 et démo Q en attente.
- O-E19-2 : tranché par la règle du 2026-10-03. Il ne reste que M-01 et O-E19-34.
- O-E19-4 : la partie E19 est faite. Le reste est O-E19-23 (E14).
- O-E19-28 : (a)-(c) renvoyés à D-E19-58, (d)-(f) acceptés, (g) renvoyé à E19.h2.
- G2a : l'en-tête passe de ⏳ à 🧪, la l.8068 est corrigée.
- M-37 et M-32 : clore sans changement, les recettes S6 et R5 sont validées.
- ADR-0050 : ajouter une section Amendment (« +19 mesuré à 1 tick par image »), après contrôle dans verify.md.
- D-E19-106 : rien à faire.

**2. Tranches que la session peut faire, par valeur puis risque**
Conditions communes : attendre la fin de la campagne de tests. Un seul propriétaire à la fois sur AlundraScriptedMotion.cs et EntitySupport.cs.

1. **T1 Pose de saut à l'arrivée (O-E19-32).** Taille S, risque faible. 0x36 en l'air donne 0x2D. Corriger les commentaires périmés, inverser le test, ajouter l'arrivée en l'air de la 476 et recenser les arrivées en l'air. C'est le seul gain visible possible sur la chaîne.
2. **T2 InitializeEventData (O-E19-34, M-09).** Taille M, risque moyen. D'abord la correspondance des décalages et le recensement des réentrées. Déclencheur : si une épingle de la chaîne bouge, on s'arrête. plan-verifier conseillé.
3. **T3 Découverte d'E19.h2.** Taille L, lecture seule : étendre emu.py à la boucle de scène pour TR-V, A10, A12 et A10J. L'exécution qui suivra est L à risque élevé (épingles de la chaîne, ordre du tick) : plan-verifier, approbation, puis verifier.
4. **T4 G4.** G4a regroupe M-11/O-E19-44 et la partie production de M-45 : taille M, risque moyen. Il faut lire le binaire d'abord et réécrire UH_APEX_REVERT. G4b regroupe M-15 et l'enquête sur M-33 : taille S. Aucun cas dans le corpus.
5. **T5 Clôture documentaire (le §1).** Taille S, aucun risque.
6. **T6 Gardes de tests Alundra.** Taille M, risque faible, tests seuls :
   - M-07/O-E19-14, avec la liste fermée {35 @1228, 38 @850, 39 @726} ;
   - M-20, M-25, M-44 ;
   - la partie test de M-45 (le cas « plafond avant entités » attend T4) ;
   - M-16, M-17, SJ-12.
7. **T7 G3, petits points.** Taille S, risque faible : taille 8 pour 0x5F, libellés de 0x35 et 0x36, M-26, M-13 en option, commentaire d'AlundraGameState.cs:127. Déclencheur : intro-trace-389.txt et intro-programs-389.txt sont régénérées. Vérifier d'abord qu'aucune tranche active ne possède EventOpcodeSizeTable.cs.
8. **T8 Moteur.** Taille S, risque faible : test de la planche absente (G2e), test du débordement de ReadInteger (L-a), M-34. CasaEngine.Tests se bâtit à part, puis on monte le pointeur du sous-module.

**3. Questions à l'auteur, par ce qu'elles débloquent**
1. **O-E19-29**, troncature au pixel quand un PNJ atterrit. (A) maintenant, (B) au chapitre 18, (C) jamais. Recommandation : A. Le cortège de la carte 10 `B[13]` cale sur la chaîne, et chaque nouvel arc épingle d'autres valeurs tronquées.
2. **Confirmations de recettes.**
   - Votre « test OK » couvre-t-il LB-4, la démo Q et U-4 ?
   - La recette G4 vaut-elle aussi pour G2b-4 ?
   - Pour G2a-4 : (A) inscrire le point 1 validé, G2a reste 🧪 ; (B) clore la section ; (C) des sauvegardes de test pour les cartes 100, 9 et 41/42. Recommandation : A.
3. **D5b.** (A) Porter l'ordre du binaire. (B) Renoncer pour l'instant. (C) Clore E19.d2c2 avec D6 et D7, et faire de D5b et M-18 une tranche à part. Recommandation : C, puis A quand la première carte à ascenseur entre dans le travail.
4. **O-E19-33**, préréglage `day3-start` et arc de la 179 `B[1]`. (A) les deux, (B) le préréglage seul, (C) rien avant E14. Recommandation : A, qui ferme aussi M-39.
5. **M-05**, 22 opcodes sans propriétaire (483 sites, aucun sur la chaîne). (A) un propriétaire par famille, (B) tout à E14, (C) un propriétaire « à placer » explicite. Recommandation : C maintenant, puis A famille par famille.
6. **O-E19-22**, eau profonde sans bottes triton. (A) maintenant dans la DLL, les blessures du sol avec E14, (B) tout avec E14, (C) laisser « à placer ». Recommandation : A (cartes 14 et 15 de la chaîne).
7. **O-E19-57**, gel de l'écran au départ d'un passage. (A) gel complet et remise à 0 de la couleur, (B) la couleur seule, (C) fermer. Recommandation : A en deux temps, la couleur d'abord. B si le coût côté moteur paraît trop haut.
8. **O-E19-31**, cartes sous-marines 159 et 160. (A) maintenant, (B) avec O-E19-22, (C) après E14. Recommandation : A, après lecture du binaire en 0x8003675C.
9. **O-E19-17**, rognage au bord de la carte. (A) une tranche, (B) un recensement en lecture seule d'abord, (C) attendre. Recommandation : B.
10. **O-E19-36**, pistes XA (0xB9). (A) les porter, (B) un sans-effet journalisé, (C) laisser l'opcode sauté. Recommandation : A, après les tranches de la chaîne.
11. **Fermetures.**
    - O-E19-25 : recommandation, fermer.
    - O-E19-49 : recommandation, consigner l'écart.
    - O-E19-59 : recommandation, garder la palette 0.
    - `bgm_044.wav` : recommandation, l'auteur le supprime, ou la session l'envoie à la Corbeille avec son accord explicite.

**4. Hors E19**
- **E14** : le reste d'O-E19-4/O-E19-23 (saisie, bris, butin, octet 0x1E), O-E19-35 et O-E19-37. La question Q-A2 se posera avec 0x95. Les blessures du sol d'O-E19-22 vont aussi à E14.
- **E18 et écran titre (O-E16-1)** : O-E19-56 (b) et 0xBB.
- **Moteur, plus tard** : O-E19-40, O-E19-43 (avec O-E19-56 (a) comme règle d'acceptation), et D-E19-106 si l'auteur la rouvre.
- **E17** : aucun point de ce triage.
