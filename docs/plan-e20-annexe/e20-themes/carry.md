# Porter un objet et monter sur une plateforme

Synthèse (lecture seule, aucune modification). Périmètre : porter/lancer/briser, et monter sur une plateforme.

(1) Porter. Le binaire (décrit dans plan-e20-heros.md §1.24, ligne 6302-6305) a trois écritures de +0x28 (TryLift 0x8002EE1C, départ des graines, départ de la bombe), des effacements dans C23, C4, C62, C70, C73 et DestroyEntity, et le héros ne change pas de physique en portant (ligne 6303). PlayerTryAction (0x8002EEAC) et TryLift (0x8002EDBC) sont décrits ligne 6304-6305. Le port aujourd'hui : PlayerTryAction et UpdatePlayerCarriedEntity sont des bouchons (AlundraPlayerManager.cs:926-927 et :1271-1272), et le CarriedEntity existe comme champ sans lecteur de portage (AlundraEntityScriptProxy.cs:89). Le plan §1.24 découpe : g1a (porter), g2a (lancer), g2b (briser), g1b (soulever, activation en dernier), g2c (autres familles C4/C70/E20). Statut : ⏳, détaillé le 2026-10-15. Prérequis : e0k2, e0a1 (fait), E14.a1b (fait) ; g2a exige f5 (D-F5-5) ; g2b exige e0b2 (en pause) et f6a. Réponses de l'auteur du 2026-10-16 : Q-G1 soulèvement seulement avec g3 (D-E20-39), Q-G2 marche, objet, plateforme horizontale (D-E20-40), Q-G3 pas de poser, Carré lance (D-E20-41). Non planifié : le détail des familles C4 (191 placements) et C70/E20 (57) est listé en g2c mais non décodé.

(2) Plateformes. Trois mécanismes distincts dans le binaire et la DLL : a) appui sur une entité, +0x12C (RidingEntity) ; b) porteur, +0x28/+0x2C (PlatformEntity/CarriedEntity), que le plan §1.24 dit être le vrai rôle de « PlatformEntity » ; c) plateformes qui bougent en Z, traitées par E19 F2 (D5b). Le code DLL : EntitySupport.cs:8-20 (support Z, relation RidingEntity, distinct de PlatformEntity), :72-75 (IsEligibleSubject), :108 (TryFindSupport), :276-310 (UpdateRidingEntities et UpdateRidingEntityOfHero) ; AlundraEntityScriptProxy.cs:726 (EvaluateEntitySupport) ; tests : une vingtaine de fichiers contiennent les termes support/platform/riding (dont AlundraHeightTargetSitesTests et AlundraArcSupport). Plateforme horizontale : couverte par D-E20-40 (le héros est emporté). Plateforme verticale et ascenseurs : D5b (E19, ligne 3878-3885) en pause, F2 : la DLL ne porte pas le héros sur une plateforme qui monte ou descend. Blocs poussables : rien d'explicite dans le plan hors C4 (« pierres, tonneaux, blocs ») non décodé.

Le décompilé (alundra-datas-analyser/.../PhysicsEngine.cs:30-58, GameEngine.cs:1767, Entity.cs:23-24, :70) porte les mêmes champs ; la décompilation perd une garde (plan ligne 6305 : genre 2 et 3 lus à l'inverse).

Données : les comptes (17 en-têtes soulevables, 30 cartes dans la chaîne dont 18 avec soulevables, 191 C4, 57 C70/E20) viennent du plan §1.24, non recomptés par moi.

## Items

- [planned] **Porter / poser / lancer (E20.g1a, g2a)** — orig: Binaire : TryLift 0x8002EDBC (genre (+0x6C&0x600)>>9, 2-3 seulement si objet 0x3B possédé), PlayerTryAction 0x8002EEAC, UpdatePlayerCarriedEntity 0x8002F884 et StepTowards 0x8002F854; +0x28 écrit en 3 sites (TryLift 0x8002EE1C, graines 0x8003429C, bombe 0x800345FC). Plan docs/plan-e20-heros.md:6302-6305 (étiquettes binaire/oracle). | DLL: absent : AlundraPlayerManager.cs:926-927 (PlayerTryAction stub renvoie 0), :1271-1272 (UpdatePlayerCarriedEntity vide), AlundraEntityScriptProxy.cs:89 (CarriedEntity champ seul) | tests: Aucun test de portage identifié ; A5 (AlundraWorld) affirme seulement qu'aucune entité ne porte le drapeau au chargement (plan ligne 4169) | plan: plan-e20-heros.md §1.24 E20.g1a/g2a ⏳ (lignes 6291-6294)
- [planned] **Briser caisses et jarres (E20.g2b)** — orig: E2 0x8007ED10 (destruction à l'arrivée, à la touche, au mur), E0/E2 ; plan ligne 6295 | DLL: partiel : AlundraDestroyEntity / BreakEffect présents (plan ligne 5780, D-F4-4 : type 7 s'appuie sur BreakEffect 8) ; pas de bris par lancer | tests: AlundraDestroyEntityBreakEffectTests (cité plan ligne 4564) | plan: plan-e20-heros.md §1.24 E20.g2b ⏳ ; prérequis e0b2 en pause
- [planned] **Soulever (E20.g1b)** — orig: TryLift, sonde de contact +0x130 du tick précédent (plan ligne 6306), Carré front 0x80126F2A&0x80 | DLL: absent (PlayerTryAction stub) | tests: aucun | plan: §1.24 E20.g1b ⏳ ; activation en dernier ; Q-G1 : seulement avec g3 (D-E20-39)
- [planned] **Autres familles : pierres, tonneaux, blocs (C4), torches (C70/E20) (E20.g2c)** — orig: C4 tête 0x80066984, effacement +0x28 0x80066A1C | DLL: absent | tests: aucun | plan: §1.24 E20.g2c ⏳ (191 C4 et 57 C70/E20 selon le plan)
- [done] **Support d'un héros par une entité (RidingEntity +0x12C)** — orig: PhysicsEngine.cs:30-58 (UpdateRidingEntity), :1358 (RidingEntity = other) ; binaire +0x12C = appui réel (plan ligne 6287) | DLL: EntitySupport.cs:276-310 (UpdateRidingEntities, UpdateRidingEntityOfHero), :108 TryFindSupport ; AlundraEntityScriptProxy.cs:726 EvaluateEntitySupport ; AlundraWorldProxy.cs:2960 (RidingEntity non effacé) | tests: fichiers de test contenant support/riding (AlundraHeightTargetSitesTests, AlundraArcSupport, AlundraLogicalZWorldTests...) | plan: E4.f (plan-e4 decision E4-4, EntitySupport.cs:8-20), E19.d2c2 D1-D5a faits (plan-e19 ~3738-3985)
- [partial] **Plateforme horizontale qui emporte le héros, objet porté suit** — orig: Oracle c2_platform (plan ligne 6280-6282) : identique avec et sans objet | DLL: héros emporté (E19.d2c2) ; objet suit : absent (dépend de g1a) | tests: oracle en dehors des tests unitaires | plan: D-E20-40 : couvert par E20.g1a ; ascenseurs renvoyés à E19 D5b
- [partial] **Plateforme verticale qui monte ou descend (F2, D5b)** — orig: PhysicsEngine.cs:1616 et ordre de passes, binaire 0x800364C8/0x80036828/0x80037364 (plan-e19 ligne 3878-3885) | DLL: AlundraEntityScriptProxy.cs (riding, ForceZ) ; le héros passe au travers ou clignote (plan-e19 ligne 3882) | tests: UH-10, UH-14 (plan-e19 ligne 3844) | plan: plan-e19-opcodes.md §1.2h.3.2 D5b en pause (conception à soumettre de nouveau)
- [unplanned] **Blocs poussables** — orig: non localisé dans le binaire par les plans ; C4 « pierres, tonneaux, blocs » (plan §1.24 g2c) | DLL: absent | tests: aucun | plan: non planifié en tant que tel ; couvert seulement par g2c (C4 non décodé)
- [planned] **Ascenseurs et trappes (0x21, 52, 83)** — orig: plan-e19 ligne 6940 : attentes portées, ascenseurs et trappes | DLL: absent (D5b en pause) | tests: aucun | plan: E19 D5b en pause (D-E20-40 : viendront avec D5b)

## Gaps

- Aucun décompte exact de cartes ou de prefabs qui utilisent plateformes, blocs poussables ou ascenseurs : je n'ai pas exécuté d'analyse sur alundra-project/ ; seuls les comptes du plan sont cités (17 en-têtes soulevables, 30 cartes dont 18 avec soulevables, 191 placements C4, 57 C70/E20).
- PlayerTryAction et TryLift côté décompilation : la recherche par nom n'a pas retourné leur définition dans PlayerManager.cs ; seuls les appels UpdatePlayerCarriedEntity sont visibles (PlayerManager.cs:43, :67, :86, :177). Les adresses binaires viennent du plan, non vérifiées par moi.
- C4 (pierres, tonneaux, blocs) n'est décodé que dans sa tête (plan §1.24 g2c) : impossible de dire quels blocs sont poussables sans décodage.
- Mécanisme de la plateforme qui monte (F2 / D5b) : en pause, sa conception doit être soumise à nouveau à l'auteur (plan-e19 ligne 2551 et 3738).
- Tests de portage : aucun test de portage dans Alundra.Tests (les fichiers trouvés ne concernent que support ou riding).

## Proposed slices

- S0 (fait): Support par entité (RidingEntity, EntitySupport, E4.f, E19.d2c2 D1-D5a) (dépend : aucun) [EntitySupport.cs:8-20, :276-310 ; plan-e19 ligne 3738-3985]
- S1 = E19.d2c2 D5b (F2): Plateformes qui bougent en Z : ordre des passes, ForceZ, égalité exacte dessus+1 (dépend : D5a fait ; levée de la pause (conception à soumettre de nouveau)) [plan-e19-opcodes.md:3878-3885, :7058 (ordre)]
- S2 = E20.e0k2 (marqueur Z logique): Marqueur du Z logique et loi du vol (dépend : E20.e0a1 (fait)) [plan-e20-heros.md:6293, :721]
- S3 = E20.g1a: Porter : UpdatePlayerCarriedEntity (3 modes), PutInHands, états 0x05/0x0B/0x0C/0x07/0x06/0x2E-0x30, suivi de l'objet, effacement du porteur, marche 16 px et dessus (dépend : S2 ; E14.a1b (fait)) [plan-e20-heros.md:6293 ; AlundraPlayerManager.cs:1271-1272 (stub)]
- S4 = E20.g2a: Lancer (front de Carré, table des directions, états 0x09/0x0A) (dépend : S3 ; E20.f5 (D-F5-5) ; S2) [plan-e20-heros.md:6294]
- S5 = E20.g2b: Briser caisses et jarres (E2, clés des dix en-têtes) (dépend : S4 ; E20.f6a ; e0b2 (en pause : bloque)) [plan-e20-heros.md:6295, D-E20-39 ; Q-G1]
- S6 = E20.g1b: Soulever (TryLift, PlayerTryAction, sonde +0x130), activation en dernier, avec g3 (dépend : S3, S4, S5 ; ordre à revoir (Q-G1 option B)) [plan-e20-heros.md:6296, :6299, :6264]
- S7 = E20.g2c: Pierres, tonneaux, blocs (C4) ; torches (C70/E20) (dépend : S5 ; T0 de lecture seule sur C4) [plan-e20-heros.md:6297]
- S8 (non planifié): Blocs poussables : à localiser (décodage de C4 et oracle) avant de planifier (dépend : S7 (décodage C4)) [plan-e20-heros.md:6297 ; aucune tranche existante]

## Questions

- Q-G1 est tranchée (D-E20-39, soulèvement seulement avec g3) : l'ordre de g1b (soulever avant ou après g2b) doit-il être revu, comme le demande le plan ligne 6264 ?
- Les ascenseurs et les plateformes verticales (D5b, F2) doivent-ils être repris maintenant, ou la recette « monter sur une plateforme » se limite-t-elle à l'horizontale (D-E20-40) ?
- Les blocs poussables font-ils partie de « monter sur une plateforme » ou de « transporter un objet » ? Le plan n'en parle pas explicitement hors C4 non décodé.

## Check

- confirmed: PlayerTryAction (AlundraPlayerManager.cs:926-927) est un bouchon qui renvoie 0 — AlundraPlayerManager.cs:927 : 'private static int PlayerTryAction(...) => 0;' ; doc ligne 926.
- confirmed: UpdatePlayerCarriedEntity (AlundraPlayerManager.cs:1271-1272) est vide — AlundraPlayerManager.cs:1272-1274 : corps vide ; appelée en 395, 646, 317 (commentaire).
- confirmed: CarriedEntity existe comme champ sans lecteur de portage (AlundraEntityScriptProxy.cs:89) — AlundraEntityScriptProxy.cs:89 'public Entity? CarriedEntity;' ; lu seulement pour copie (2597) et test de garde AlundraPlayerManager.cs:386.
- confirmed: EntitySupport.cs:8-20 distingue support Z (RidingEntity) de PlatformEntity — EntitySupport.cs:8-12 et 17-19 : 'NOT PlatformEntity ... the generic standing on relation ... RidingEntity'.
- confirmed: EntitySupport.cs:72-75 IsEligibleSubject ; :108 TryFindSupport — EntitySupport.cs:72-75 et 108-110 lus.
- confirmed: Plan §1.24 (plan-e20-heros.md:6262-6305) statut ⏳, découpe g1a/g2a/g2b/g1b/g2c, binaire +0x28 en trois écritures (6302), héros ne change pas de physique (6303) — plan-e20-heros.md:6262 (titre ⏳), 6291-6297 (tableau), 6302 (écritures +0x28 TryLift 0x8002EE1C, graines, bombe), 6303 (aucune instruction de la passe physique ne lit +0x2C).
- confirmed: Réponses auteur Q-G1/Q-G2/Q-G3 = D-E20-39/40/41 — plan-e20-heros.md:6264-6265 ; décisions plan-e20-heros.md:173-178.
- confirmed: Le vrai appui est +0x12C, PlatformEntity (+0x28) est un nom trompeur — plan-e20-heros.md:6287 ; AlundraWorldProxy.cs:2960 (RidingEntity +0x12C non effacé). Décompilation : Entity.cs:23 'PlatformEntity //28' et PhysicsEngine.cs:1616 utilisent PlatformEntity.
- confirmed: Plateforme verticale : D5b en pause, F2, égalité dessus+1 rompue — plan-e19-opcodes.md:3878-3885 (F2, contradicteur binaire, correctif S4b).
- refuted: Décompilation PlayerManager : définition de PlayerTryAction et UpdatePlayerCarriedEntity non trouvée (seuls les appels visibles, :43,:67,:86,:177) — alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PlayerManager.cs:1037 'private int PlayerTryAction()' et :3203-3204 '// GHIDRA: UpdatePlayerCarriedEntity @ 0x8002F884 / private void UpdatePlayerCarriedEntity(int mode)'. Les définitions existent (le gap de l'inventaire est faux).
- confirmed: Décompilation PhysicsEngine.cs:30-58 porte RidingEntity et UpdateRidingEntity — PhysicsEngine.cs:28-34 (boucle RidingEntity) et :53-54 (UpdateRidingEntity @80037364).
- confirmed: Décompilation GameEngine.cs:1767 et Entity.cs:23-24 portent PlatformEntity — GameEngine.cs:1767 'if (entity.PlatformEntity != null)' ; Entity.cs:23 'PlatformEntity; //28'.
- unverifiable: Décompilation perd une garde (genre 2 et 3 lus à l'inverse) — Le fait est attribué au plan (plan-e20-heros.md:6305) ; non re-vérifié dans PlayerManager.cs ni dans le binaire. Le genre (+0x6C&0x600)>>9 n'a pas été localisé dans la décompilation.
- unverifiable: Aucun test de portage dans Alundra.Tests — grep 'Carried|PutInHands|carried|CarryEntity|UpdateCarried' sur Alundra.Tests donne 16 fichiers (ex. AlundraHeroObjectTopsTests.cs, AlundraEntityCollisionTests.cs) ; non ouverts, donc non confirmé qu'ils ne testent pas le portage.
- refuted: Une vingtaine de fichiers de test contiennent support/platform/riding — Un grep 'carr|lift|platform' seul donnait 142 fichiers (surtout bruit) ; le décompte 'une vingtaine' n'est pas reproduit par une recherche ciblée (non recompté précisément).
- confirmed: Plan ligne 6280-6282 oracle c2_platform ; 6283 plateforme verticale : objet suit, héros tombe/clignote — plan-e20-heros.md:6280-6283 (table) ; l'oracle c2 cité en 6309-6310 est un texte de découverte non ré-exécuté.
- unverifiable: Comptes 17 en-têtes, 30 cartes dont 18, 191 C4, 57 C70/E20 — Repris du plan (plan-e20-heros.md:6269 et 6297), non recomptés dans alundra-project/.

### Missing

- Décompilation PlayerManager.cs:1037 (PlayerTryAction) et :3203-3204 (UpdatePlayerCarriedEntity) : définitions présentes, contrairement à la note de l'inventaire ; la lecture de leur corps (non faite) reste à faire pour le portage.
- Décompilation PlayerManager.cs:1117, :2027, :2224 : affectations de PlatformEntity = joueur (relation portage, hors DLL) ; non mentionnées dans l'inventaire comme sites de portage décompilés (le commentaire EntitySupport.cs:9-10 les cite).
- AlundraWorldProxy.cs:3290 : commentaire explicite disant que le nettoyage de PlatformEntity.CarriedEntity n'est pas porté au recyclage (écart connu, non listé dans les gaps de l'inventaire).
- Alundra/Scripts/AlundraPlayerManager.cs:375-395 : le garde 'CarriedEntity == null' (lignes 386) est lu par la DLL pour le chemin Idle/Moving ; l'inventaire ne le cite pas.
- Plan §1.24 (plan-e20-heros.md:6309-6308) décrit en détail les modes 0/1/2 d'UpdatePlayerCarriedEntity et le groupe d'états 0x8003232C : aucune tranche ne les porte au-delà de g1a ; l'inventaire ne cite pas ce contenu détaillé.

### Overall
Inventaire globalement fiable sur la partie DLL (stubs, champ CarriedEntity, EntitySupport, statuts planifiés) et sur les décisions D-E20-39/40/41, mais à corriger avant de planifier : (1) l'affirmation que la décompilation n'a pas de définition de PlayerTryAction/UpdatePlayerCarriedEntity est fausse (PlayerManager.cs:1037 et :3203-3204) ; (2) le décompte « une vingtaine de fichiers de test » et l'absence de test de portage ne sont pas vérifiés (16 fichiers hit sur un terme de portage, non ouverts) ; (3) les comptes de données (17, 30/18, 191, 57) et la garde « genre 2/3 inversée » ne sont pas re-vérifiés et reposent sur le plan. Pour planifier, re-vérifier avant tout : le corps de UpdatePlayerCarriedEntity côté décompilation, les 16 fichiers de test, et la chaîne du genre TryLift côté binaire. Le trou « blocs poussables » et l'écart de recyclage AlundraWorldProxy.cs:3290 sont à ajouter."}