# Contre-vérification du rapport h4 (E20.h4 magies des tomes, E20.h0 cadre d'usage)

**Verdict.** Le rapport est fiable pour écrire les tâches. J'ai relu tous les points qui portent une décision, sans trouver d'erreur sur le comportement du binaire. Il faut quand même corriger ou compléter quelques points avant de rédiger. Les trois plus importants : la ligne sur C65 dans la décompilation est fausse, le sprite 154 utilise aussi C65, et plusieurs manques côté DLL ne sont pas listés (§3). Les statistiques de mutants pour les pilotes ne se reproduisent pas (§2, point 4).

Mon dossier de travail est `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/check-h4/` (`o/` pour les scripts, `lin.py`, `xref.py`, `out_dmut.txt` et `out_hmut.txt` à la racine).

## 1. Ce que j'ai fait

- **Relecture du binaire.** J'ai redésassemblé au moins 40 adresses avec capstone, sans lire les listings de h4 : `0x8002ED64`, `0x8003499C`, `0x800348E0`, `0x8004E030`, `0x8004DEA4`, `0x8004DF3C`, `0x8004DEBC`, `0x8004E0F8`, `0x8003634C`, `0x800350C0`, `0x800351B8`, les 8 pilotes, `0x80032F84`, `0x80033274`, les 5 annulations, E1/E3/E13/E28, C63/C65/C75, `0x8003AF70`, `0x8003AE04`, `0x80038634`, `0x80039F58`/`0x80039D04`/`0x80039AD0`/`0x80039B28`, `0x800366FC`, `0x8002BC90`, `0x8002BB94`, `0x8004464C`, `0x8005A9E0`.
- **Tables lues.** Les sauts `0x8002339C`, `0x800233EC`, `0x800235F4` et `0x80022D3C`, et toutes les tables des pilotes.
- **Rejeu des scripts de h4 avec d'autres graines** (copiés dans mon dossier) :

| Script | Résultat |
|---|---|
| `h4_start` | 5000 vecteurs : 0 écart, 142/142 instructions. Autre graine, 1500 vecteurs : 0 écart |
| `h4_vec_drivers` | graine 4242 : 0 écart. Couvertures identiques au rapport (73/93, 106/108, 174/176...) |
| `h4_vec_handlers` | 0 écart, 100 % |
| `h4_damage` | 20480 cas, 0 écart |
| `trace_cast` (0x2B, 0x2D, 0x31) | naissances et fins identiques au tableau (22–97 / 129, 42–82 / 344, 42–102 / 129) |
| `interrupt_check` | identique |

- **Trois modèles écrits par moi**, depuis ma lecture du désassemblage, comparés au vrai code MIPS :

| Modèle | Fichier | Résultat |
|---|---|---|
| Requête `0x8003AF70` et tri `0x8003AE04` | `o/my_query.py` | 1500 vecteurs, 0 écart, couverture 94/94 et 91/91, dont 39 vecteurs à clés égales |
| Pilote du vent 1 | `o/my_wind1.py` | 1500 vecteurs, 0 écart, 216/218 |
| Gestionnaire C75 (orbe d'eau) | `o/my_c75.py` | 2000 vecteurs, 0 écart, 166/166 |

- **Sensibilité du modèle de requête.** Mutant « balayage jusqu'à `count` inclus » : 64 écarts sur 400. Mutant « tri dès 2 résultats » : 26 écarts. Les deux faits sont donc confirmés indépendamment.
- **Bornes testées directement sur le vrai pilote** (`o/my_bound.py`) :
  - terre 1 passe à 0x34 à partir du compteur 100 ;
  - terre 2 à partir de 0x118 ;
  - eau au compteur interne 60.
- **Côté DLL.** J'ai lu les fichiers et lignes cités, et fait un `grep` des consommateurs. Côté décompilation, j'ai lu `PlayerManager.cs`, `FunctionTypeC.cs`, `FunctionTypeE.cs`, `GameEngine.cs`, `PhysicsEngine.cs` et `StaticVariables.cs`. Corpus : les JSON de `data-extracted`, `map_alundra.json`, `BALANCE.BIN.json`, `effects-global.json`.

## 2. Verdict par affirmation

### Confirmé

- **Cadre d'usage (§1).**
  - `TryUseItem` : front `[0x80126F2A] & 0x20`, verrou `[0x800DC4B8] != 0` (n'importe quel bit), son 3.
  - `0x8003499C` : bornes 0x62 non signé, `0x14`, trous de la table `0x8002339C` (0x21, 0x22, 0x28, 0x2A), écriture de `[0x80127164]`, `[0x8012715C]`, `[0x80127160]`.
  - `0x800348E0` : ordre `+0x2C`, `+0x88 < 2`, `[0x80127164]`, bâton (7), PM, `0x8004DF3C(1)` borné `[0, MpMax]`.
  - Octets XA `0x80023365 + item` = 0, 1, 6, 6, 2, 3, 4, 5.
  - Pilote `0x8003634C` : table, compteur borné `0x7FFFFFFF`, libération si le résultat est non nul.
  - Appels du pilote en `0x80031CC4` et `0x80031C6C`.
  - Annulation `0x800350C0` : table `0x800233EC`, appelants `0x80031BFC`, `0x80032758`, `0x80032770`, `0x800327B4`, 3 petits pilotes, 13 appels du garde.
  - Garde `0x800351B8` : pente 4, ou animation hors {0x32, 0x33}.
  - États 0x32/0x33 (`0x800325A4`) et 0x34 (`0x80032680`) via la table `0x80022D3C`.
  - Menu refusé tant qu'un objet est en cours (`0x8002BC90`). `0x8002BB94` est bien le test `hold == 0x900`.
  - Remise à zéro à l'initialisation du héros (`0x8003295C`, appelée en `0x80031A48`) : un seul écrivain hors pilote, démarrage et annulation.

- **Les 8 pilotes (§3).** Toutes les tables, tous les décalages de champs, toutes les valeurs (3 apparitions en `+0x70=0xFE`, etc.) correspondent :
  - terre 1 : `0x8002343C`, 32 entrées de 6 octets ;
  - terre 2 : `0x80023504`, 15 entrées ; X et Y par le premier tirage, direction par le second ; ForceZ `0xFFF80000` ; destruction si `heroZ + 0x1000000 < +0x138` ;
  - eau : tables `0x80023544` et `0x8002357C`, entrées de 14 octets ; `+0x280` reçoit p3 seul ; `+0x28C` est le reste signé à la C ; `+0x290 = durée − compteur` ; soins à 30, anim 0x34 à 60 ; la phase 2 balaie les emplacements 1 à `count` inclus ;
  - feu 1 : enchaîne la phase 1 dans le même tick ; une boule tous les 8 ticks ;
  - feu 2 : sauvegarde des 9 mots, `c>>6` et `c>>4` ;
  - vent 1 : table `{30..100}`, arrêt de la recherche au premier candidat éloigné, sol testé seulement dans la branche aléatoire ;
  - vent 2 : pas d'enchaînement dans le même tick.
  - Les annulations (terre 2, eau, feu 1, feu 2, vent 2) sont exactes. Le calendrier (compteur 0 au tick T0+1, première frappe du vent 1 perdue) est cohérent avec la durée de l'animation 0x32 (32 ticks, relue dans `map_alundra.json`).

- **Gestionnaires natifs (§4).**
  - Registre : E (4, 0/1/3/13/28), C (2, 63/65/75), A (0, 254/0). Les cases du registre `0x80098F4C` sont celles du rapport.
  - Valeurs relues : E3, E13 avec `[0x800DD874]=2`, E28 avec `<0x1801` et `+0x320`.
  - C63 : délai, ForceZ `0xFFFD0000`, rotation de 4 sans plus court chemin.
  - C65 : fenêtre `0x1400000` × `0xF00000`, décalage `+0x14`.
  - C75 : orbite, ellipse 3:2, lâcher, vol rectiligne. Le Rond tenu gèle le compteur de vie.
  - Requête : balayage `1..count-1`, filtres, tri dès 3 résultats par un Hoare instable. Je l'ai reproduit avec mon propre modèle.

- **Le Rond tenu et le sprite 154.** Voir §3 pour ce qui manque.

- **Fait côté binaire sur le dernier emplacement.** `[0x80134360]` est l'indice du plus haut emplacement occupé (voir §3, point 1).
  - `0x80038634` le recalcule à chaque tick (`sw t2, 0x4360` en `0x800386CC`).
  - `0x80039D24-0x80039D3C` le relève à la naissance.
  - La requête exclut `count`, alors que la passe principale, les annulations et la phase 2 de l'eau l'incluent.

- **Divergences avec la décompilation.** D1 à D12 sont confirmées par lecture du C# :
  - D1 à D6 : `PlayerManager.cs` 2317–2345, 2911+, 2786+, `FunctionTypeC.cs` 7068+ ;
  - D7 et D8 : décompilation du feu et du vent 1 ;
  - D9 : le sol est testé aussi dans la branche cible ;
  - D10 : `Array.Sort`, `GameEngine.cs:1805` ;
  - D11 : `IsFadeActive` (qui appelle `AnimateWarpEffect`) est un mauvais nom ;
  - D12 : l'hypothèse « warp » de `h-g-deps.md:109` est fausse.
  - La remarque sur `ApplyEntityForces` (`PhysicsEngine.cs:1514`, `ForceX + (table >> shift)` contre `(ForceX + table) >> shift` en `0x80036758-0x80036764`) est exacte.

- **Sprites, soins et corpus.**
  - Enregistrements et fiches d'équilibrage des sprites 16 à 24 : attaques (type, puissance), animations (24, 12, 32, 46, 87, 120, 37, 180 ticks), sons 46, 55, 45, effets 8, 14, 18, 19 (3 variantes), 26.
  - Soins `0x80032F84` (`+max(5, PVmax/2)`, 4 étincelles) et `0x80033274` (`Hp = HpMax`, 8 anneaux).
  - E3 : 98 sprites de 75 cartes. E13 : carte 321, sprite 213.
  - Tomes en coffre : cartes 25, 76, 84, 95, 17, 439, 3, 448, 431.

- **Faits côté DLL.** Les lignes citées sont exactes :
  - `PreviousAdjustedForceX/Y` sans consommateur (`ApplyEntityForces` y est porté « flat-ground-only ») ;
  - garde du menu en bouchon (`AlundraInventoryDirector.cs:406-411`) ;
  - `TryUseItem` en bouchon rendant 1 (`:919`) ;
  - `UpdateItemUseDriver` vide, placé comme `0x80031CC4` (`:340`, `:1494`) ;
  - `Bytes` est un `byte[4]` (`AlundraEntityScriptProxy.cs:1214`) ;
  - `MpMax` plafonné à 4 (`AlundraPlayerManager.cs:1938`) ;
  - `SpawnEntityBySpriteIndex` (`AlundraWorldProxy.cs:3448`, Z+1), `DestroyEntity` (`:3267`, `:3295`) ;
  - registre : une clé = un enregistrement, restreint aux types listés ;
  - E0/E1 inline (`AlundraEventProgramRunner.cs:378`) ;
  - sauvegardes d'essai (`AlundraTestSaves.cs:67-72, 96`) ;
  - ordre des passes du binaire `0x8003B388` : slots, événements (MovePlayer d'abord), compteurs, listes, animation, physique, effets, coups ;
  - `MovePlayer` appelé une fois par tick logique.

### Réfuté ou à corriger

1. **§7, dernière phrase : « les gestionnaires E3, E13, E28, C63 et C65 de la décompilation sont corrects ».** Faux pour C65.
   - `FunctionTypeC.cs:16789-16880` pousse avec `g_cosinus[dir*4]*0x300` et `g_sinus[dir*4]<<9`.
   - Ces tables sont à `StaticVariables.cs:10998` (`0x80028734`) et `:10930` (`0x80028334`) : 512 entrées, amplitude 512.
   - Le binaire lit `0x800206F0` (cos) et `0x800204F0` (sin) : 256 entrées, amplitude 256, indice `8*idx` (`sll 4` en `0x8007AFE8-0x8007B010`).
   - Écarts numériques (+0xCC, +0xD0) :
     - direction 0 : binaire (196608, 0), décompilation (393216, 0) ;
     - direction 16 : binaire (−196608, 0), décompilation (278016, 278016).
   - C'est une divergence D13 à ajouter. E3, E13, E28 et C63 sont bien corrects.

2. **§4 : registre `(C,65) {24}`.** Incomplet. Le sprite global 154 a aussi C=65 (`MoreFlags 0x44`, E=1), sans fiche d'attaque : `BALANCE.BIN.json` donne `AttackCount 0`.
   - Le C65 lirait donc un `+0x1C8` nul (`0x8007AF48-0x8007AF50`).
   - Usage non trouvé : aucun des 102 sites de `0x80039F58` ne vise 154 par constante, et aucune des 484 cartes ne le place. Probablement dormant, mais la tranche doit décider.
   - Au passage, le sprite 16 est placé par 4 entités de la carte 388, hors du sort.

3. **§0.5 et Q2 : « un ennemi seul n'est jamais visé par C63, C65, C75, ni par le vent 1 ».** Le fait (le plus haut emplacement occupé est ignoré) est exact. La conséquence est trop générale.
   - Seule l'entité du plus haut emplacement est ignorée. Pour C63, C65 et C75, l'appelant occupe lui-même un emplacement, souvent plus haut que l'ennemi.
   - Pour le vent 1, l'appelant est le héros (emplacement 0) : l'ennemi du plus haut emplacement est ignoré.

4. **§9 : « 36 sur 36 » et « 53 sur 54 (le seul survivant est équivalent) ».** Non reproduit pour les pilotes.
   - Gestionnaires : 36/36 avec le script final, donc reproduit. Le fichier conservé `results/mut_handlers.txt` montre pourtant 35/36 (premier passage).
   - Pilotes : le script final donne **50/54**. Survivants : `earth1: end counter 0x64->0x65`, `heal water: minimum 5->4`, `wind1: last target not skipped`, `wrapper: in-use cleared only on 1`.
   - Le fichier conservé `out_vec_drivers_mut.txt` montre 49/54. Le passage « relancé aux bornes » n'est pas dans les fichiers livrés.
   - Seul `wrapper: in-use...` est équivalent. J'ai vérifié les trois autres indépendamment : désassemblage, appels directs du pilote, mon modèle du vent 1 avec une « dernière cible » posée. Les faits sont vrais, la statistique citée est fausse.

5. **§6 [M] : « annule au tick même ».** Le test impose l'animation 0x31 avant le tick.
   - Dans le jeu, la blessure pose 0x31 après le dernier appel du pilote (`0x80031C6C` avant `0x80031C84`) : l'annulation a lieu au tick suivant.
   - Le §1 le dit juste, le §6 est imprécis.

### Invérifiable

- **§0.2 : « le sprite 18 n'est jamais créé ».** Plusieurs sites sont pilotés par des données :
  - `0x8002F818` (naissance liée à une animation du héros, indice lu en `lbu a2, 1(a1)`) ;
  - `0x80032BEC`, `0x80032CD8`, `0x8007FD30` (contenu + 0x1E) ;
  - deux sites de page de carte.
  - Sans effet sur la conception.
- **Q5** (sons des effets) et le contenu des pistes XA : inconnus, comme le dit le rapport.
- **Corpus de Q7.** Le bâton de la carte 134 n'a pas été revu. Pour I47, une entité de la carte 411 porte aussi `Contents = 47`, non classée.

## 3. Ce qui manque au rapport

1. **Définition de `[0x80134360]` et équivalent DLL.** Le rapport dit seulement « 1 à count−1 ».
   - Il faut une indice de plus haut emplacement occupé, recalculé par la passe `0x80038634` (gardée par `& 0x48`) et relevé à la naissance.
   - La DLL n'a pas ce membre. `HighestLiveSlot()` est privé (`AlundraWorldProxy.cs:153`) et `RecycleDestroyedEntities` (`:3220`) ne le calcule pas.
   - Il faut aussi préciser, boucle par boucle, `<` pour la requête et `<=` pour les autres.

2. **Tables sin/cos à 256 entrées** (`0x800204F0`, `0x800206F0`). Utilisées par C75 et par C65 (à `8*idx`). Elles n'existent pas dans la DLL (`grep` vide) et ne figurent pas dans « À ajouter » du §8.

3. **Accès 32 bits sur `AIValues[0..9]`**, pas seulement sur `Bytes`. Le pilote de l'eau écrit des mots en `+0x280` (p3), `+0x284` (mode), `+0x288` (rayon), `+0x28C` (angle), `+0x290` (durée). La DLL a `short[10]` (`AlundraEntityScriptProxy.cs:1217`).

4. **Feu 2 : restauration d'un tampon jamais sauvegardé.** L'annulation `0x80034E08` et la branche d'annulation du pilote (`0x80035D7C-0x80035DC4`) recopient `0x80127138` sur le bloc de tremblement sans condition.
   - Annulé avant la naissance du sprite 21 (les 32 premiers ticks), ce tampon est périmé ou nul : il remet donc le bloc de tremblement à zéro.
   - La DLL doit garder un tampon de session initialisé à zéro et le restaurer sans garde.

5. **Pilote non appelé** quand `hero+0x20 != 0` (`0x80031BA4`, la DLL a déjà `BlockedByEntity`, `AlundraPlayerManager.cs:305`), ni quand `[0x800DC4B8] & 0x48` (`0x8003B398` : seule la passe des listes tourne). La branche verrouillée annule à chaque tick (`0x80031BFC`).

6. **Après la pose, le héros est libre** pendant environ 200 à 250 ticks (eau), avec l'objet toujours « en cours ». Le menu, le Rond et un nouveau tome sont refusés, mais il peut marcher et se battre.
   - Les orbes le suivent par `hero.ForceX/Y`.
   - Le Rond tenu prolonge l'orbite sans borne.
   - Le garde de pente n'est plus appelé en phase 2. Seuls les 4 sites de `MovePlayer` annulent encore.

7. **Découpage du §11 :**
   - les cinq annulations propres aux tomes ne sont rattachées à aucune tranche ;
   - les deux fonctions de soin écrivent les PV de l'entité (`+0x14`), pas ceux des statistiques ;
   - h4a « tables vides » est sous-spécifié : les fins de phase du feu et du vent dépendent de l'entité née, pas d'une table ;
   - le coup de caméra E13 s'ajoute à Y dans la formule de suivi (`0x8002CE6C`, remis à zéro en `0x8002CE98` et `0x8002CD7C`). Dans la DLL, l'axe Y est inversé en espace de rendu (`ComputeSmoothedCameraTarget` ajoute `-swayOffsetY`), donc la secousse y vaut −2. C'est une inférence, pas un fait lu.

8. **Pente 4 = eau profonde** (`DeepWaterSlope`, case de nage de `MovePlayer`) : entrer en eau profonde annule donc le sort. Le rapport ne dit que « pente 4 ».

## 4. Conclusion

- **Fiable :** adresses, tables, décalages de champs, ordre des gardes, tous les pilotes et gestionnaires, la requête et le tri, les cinq annulations, les soins, le calendrier, les divergences D1 à D12, les faits DLL cités.
- **À corriger avant de rédiger :** les points 1 à 3 et 5 de la section « Réfuté ou à corriger » du §2, c'est-à-dire D13/C65, le sprite 154 et la portée de Q2 et du §6. Ne pas citer les statistiques de mutants du rapport (point 4 du §2).
- **À compléter dans les tâches :** les huit manques du §3.
- **Pas de réfutation d'un fait de comportement du binaire** dans ce que j'ai relu.

Fichiers utiles : `check-h4/o/my_query.py`, `my_wind1.py`, `my_c75.py`, `my_bound.py` ; `check-h4/out_dmut.txt` et `out_hmut.txt` (mutants rejoués) ; `check-h4/lin.py` (désassembleur) et `xref.py` (références croisées).