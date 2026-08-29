# Plan — Corriger l'ordre caméra / map-events dans `AlundraWorldProxy.Update`

Date : 2026-08-29. Origine : **symptôme rapporté par l'utilisateur** — « au tout début du jeu la
caméra n'est pas à la bonne position ; c'est très vite corrigé car elle se repositionne avec le
suivi ». Hypothèse de l'utilisateur : le problème vient de l'initialisation au chargement de la map.

**Verdict de l'enquête : l'intuition vise le bon endroit, mais la cause est d'un cran à côté.**
L'initialisation est **fidèle** ; ce qui ne l'est pas, c'est **le moment où la caméra la consomme**.

## 1. Diagnostic (chaque point vérifié dans le code, pas déduit)

### 1.1 Ce que fait l'original

`UpdateWorld` (`GameEngine.cs:1638-1664`) : **`RunMapEvents()` → `UpdateEntities()`**, et le suivi de
caméra est **la dernière chose d'`UpdateEntities`** (`GameEngine.cs:1743-1753`) :

```csharp
private void UpdateEntities()
{
    EntityManager.UpdateEntities();
    if (g_entityFollowedByCamera != null && g_entityFollowedByCamera.IsLoadedNormalOrDeactivated)
    {
        g_cameraLookAtX = g_entityFollowedByCamera.PosX >> 16;   // ... Y, Z
    }
}
```

**La caméra de l'original voit donc toujours l'état d'APRÈS les map-events de la même frame.**

L'original sème aussi un look-at au chargement (`GameEngine.cs:1492-1496`, depuis
`g_saveData.CameraTileX/Y/Z`) et pose l'entité suivie au joueur (`:644`) — mais ce semis est
**écrasé avant d'être utilisé**, précisément parce que la première mise à jour du look-at arrive
après les premiers map-events.

### 1.2 Ce que fait notre portage

`AlundraWorldProxy.Update` exécute la caméra aux **étapes 2-4** et les map-events à l'**étape 7** :
l'ordre est **inversé**. Rien ne le justifie — les commentaires d'`Update` motivent l'ordre **interne**
au bloc caméra (suivi avant pan, pour que l'adoption de base voie l'écriture la même frame, E5.a),
mais **aucun** ne motive que le bloc entier précède les map-events. C'est un placement de commodité
hérité de la construction progressive d'`Update`.

L'initialisation, elle, est **fidèle** : `AdoptPlayerPawn` pose `EntityFollowedByCamera = joueur`
(port explicite de `:644`) et sème le joueur sur la tuile caméra `(33,59)` → pixel `(804, 952)` ;
`InitializeWithWorld` arme le snap de première frame (port de `g_isCameraScrolling = 1`).

### 1.3 Le mécanisme exact du symptôme

À la frame 1, le suivi s'exécute **avant** que le programme B 129 n'ait joué son `0x67`
(caméra → entité 6) et son `0x64` (joueur → `(804, 872, 0)`). Le snap accroche donc **la position de
spawn du joueur**, tandis que l'original accroche **l'entité 6**. Dès la frame 2, le lissage corrige.
D'où : « mauvaise position au tout début, corrigée très vite ».

**Portée générale, au-delà du démarrage** : tout **téléport scripté** (`0x64`/`0x65`) et tout
**reciblage de caméra** (`0x67`, `0x69`) est vu par la caméra avec **une frame de retard**. Au
démarrage cela se voit parce que c'est un snap ; ailleurs le lissage le masque.

## 2. Enveloppe

- **Résultat** : le bloc caméra (résolution, suivi, pan) et le bloc de rendu (couleur de fond,
  backdrop) s'exécutent **après** la passe de map-events, comme dans l'original. La caméra voit
  l'état de la frame courante.
- **Ce n'est pas un refactoring** : c'est un **changement de comportement** assumé et voulu, donc il
  a son propre plan et son propre commit.
- **Correction de relecture — les six items de caractérisation NE CHANGENT PAS.** Ils n'épinglent que
  l'ordre **interne** au bloc caméra (résolution → suivi → pan) ; aucun n'exerce la passe de
  map-events, `PlayerEntity` étant nul dans tous leurs montages. Le réordonnancement est donc un
  **no-op** pour eux. **Conséquence portée en condition d'arrêt** : si une assertion existante de ce
  fichier doit changer, c'est le signe que le correctif fait autre chose que ce qui est écrit — on
  s'arrête, on ne ré-baseline pas. Le seul ajout autorisé au fichier est le nouveau test du §4.
- **Non-objectifs** : `U-1` (verrou posé trop tôt — inatteignable, cf.
  `plan-update-caracterisation.md` §2 bis) ; toute autre modification d'`Update` ; le semis de
  look-at au chargement, qui est **déjà fidèle** et que la correction rend, comme dans l'original,
  sans effet observable.
- **Propriétaires** : `Alundra/Scripts/` et `Alundra.Tests/`.
- **Acceptation globale** : build 0 erreur ; `Alundra.Tests` verts ; convertisseur 138 ; preuve
  positive d'exécution des harnais puis `git status --short docs/` vide ; **validation en jeu par
  l'utilisateur** : plus de saut de caméra au démarrage.
- **Rollback** : un commit. **Budget** : un commit, ≤ 2 tours.

## 3. L'ordre cible, écrit intégralement

L'ordre actuel d'`Update` et l'ordre visé, pour qu'il n'y ait rien à deviner :

**Correction de relecture — la première rédaction ne plaçait pas la caméra assez loin.** Chez
l'original, le look-at est la dernière ligne d'`UpdateEntities`, et `UpdateEntities` **contient**
`UpdateEntitiesEvents()` (`EntityManager.cs:380`), dont la relance `do/while` est portée chez nous en
**`RunPendingEventTriggers`** (étape 11). Placer la caméra juste après les map-events aurait donc
laissé tout `0x67`/`0x64`/`0x69` **émis par la relance** avec une frame de retard — exactement la
classe de défaut que le §1.3 annonce corriger. La caméra va donc **à la fin**.

| | aujourd'hui | après |
|---|---|---|
| 1 | horloge (ticks) | horloge (ticks) |
| 2 | **caméra : résolution** | map-events (× ticks, gardé joueur) |
| 3 | **caméra : suivi** | flush de l'overlay (E7.b) |
| 4 | **caméra : pan** | *(si ≥ 1 entité)* listes, rattrapage, murs |
| 5 | couleur de fond | **caméra : résolution** |
| 6 | backdrop | **caméra : suivi** |
| 7 | map-events (× ticks) | **caméra : pan** |
| 8 | flush de l'overlay | couleur de fond |
| 9 | **retour anticipé si 0 entité** | backdrop |
| 10-12 | listes, rattrapage, murs | `CloseFrame` |
| 13 | `CloseFrame` | — |

**Le retour anticipé « 0 entité » disparaît**, remplacé par un bloc conditionnel `if (count != 0)`
autour des étapes 10-12. Deux bénéfices, tous deux vérifiables : la caméra et le rendu continuent de
tourner pour un monde sans entité (propriété actuelle, et ce qui rend le test de caractérisation
possible), et `CloseFrame` n'a plus **qu'un seul** site d'appel au lieu de deux — la reconnaissance
avait relevé qu'un `CloseFrame` manqué ou doublé est silencieux à l'intérieur d'une frame.

**Trois invariants à ne pas casser, chacun issu d'un fait établi :**

1. **Le flush d'overlay reste immédiatement après les map-events** (coalescence E7.b : une seule
   reconstruction pour les quatre `0x85` d'une entrée de map).
2. **`CloseFrame` est appelé exactement une fois par `Update`**, sur tous les chemins.
3. **L'ordre interne du bloc caméra est intact** : résolution → suivi → pan. Le suivi écrit `Target`
   inconditionnellement pour que le pan adopte sa base la même frame (E5.a) ; le déplacer casserait
   le relais que le test de caractérisation épingle.

**Absence de cycle, vérifiée** : les map-events **écrivent** de l'état caméra (`0x67` pose l'entité
suivie, `0x69` force le look-at) et n'en **lisent** aucun ; le bloc caméra lit cet état. Déplacer la
caméra après les map-events ne crée donc aucune dépendance circulaire.

## 4. Tranche unique C1 — réordonner, avec un test qui échoue d'abord

**Ordre imposé, et c'est le fond de la tranche** : écrire le test **avant** le correctif et **le voir
échouer**. Un test écrit après un correctif ne prouve pas qu'il corrige quelque chose.

1. **Test de non-régression du défaut** — et son montage, que la relecture a montré manquant.
   **Le montage de caractérisation existant ne peut PAS servir tel quel** : ses mondes n'ont pas
   d'entité « tileMap », donc `InitializeWithWorld` sort avant `AdoptPlayerPawn`, `PlayerEntity`
   reste nul, et le bloc `if (PlayerEntity != null) { … RunMapEventsPass … }` d'`Update` est
   **entièrement sauté** — un test écrit ainsi passerait avant **et** après, sans rien prouver. Et
   `AdoptPlayerPawn` n'est pas atteignable headless : il exige un `AlundraPlayerController` vivant
   (constat d'E3.d).
   **Mécanisme retenu, sur le précédent d'`InstallCellAndOverlaySystems`** (bloc carvé hors
   d'`InitializeWithWorld` précisément pour que les tests traversent le vrai chemin) : élargir
   `PlayerEntity`'s setter de `private` à `internal`, et `BuildMapEvents` de `private` à `internal`,
   pour que le test ensemence les deux. Ce sont les **seuls** élargissements autorisés par la tranche.
   Le test — **une seule et même entité est déplacée ET suivie** (correction de relecture : le type de
   recherche `0x80` désigne « le propriétaire », c'est-à-dire l'entité que `RunMapEventsPass` passe au
   runner, donc le **joueur** ; une entité suivie *distincte* ne pourrait jamais être déplacée par ce
   `0x64`, et le test échouerait avant **comme** après. Les autres types de recherche sont hors
   d'atteinte ici : ils parcourent `SpawnedEntities`, vide dans ce montage) :
   - ensemence `PlayerEntity` **et** pose `EntityFollowedByCamera` sur **ce même proxy** (statut
     `Normal`, pour que `IsLoadedNormalOrDeactivated` soit vrai), puis construit des map-events dont
     le programme exécute un `0x64` de type de recherche **0x80** déplaçant ce proxy de valeurs
     chiffrées ;
   - appelle **`proxy.Update(...)`** — jamais `RunMapEventsPass` ni le runner en direct, sans quoi il
     ne traverserait pas le chemin en cause ;
   - assère que `Target` reflète la position **d'après** le déplacement, **dans la même frame**.
   **Ce test doit ÉCHOUER sur le code actuel**, et l'exécution rapporte l'échec et ses valeurs
   chiffrées **avant** tout correctif. S'il passe du premier coup, il ne teste pas le défaut : arrêt.
2. **Le correctif** : appliquer l'ordre cible du §3 — bloc caméra et bloc de rendu **à la fin**,
   après le rattrapage d'évènements, et retour anticipé « 0 entité » remplacé par un bloc
   conditionnel. Respecter les trois invariants. Aucune autre modification.
3. **Les six items existants restent VERTS ET INCHANGÉS** (voir §2) : le rapport montre que le diff
   du fichier de caractérisation n'ajoute que le nouveau test, **sans une seule assertion modifiée**.
   Une assertion qui doit bouger est une condition d'arrêt.
4. **Mutation obligatoire** : remettre l'ordre d'origine → le test de l'item 1 échoue de nouveau.

**Acceptation** : le test échoue avant, passe après ; suites vertes ; **goldens inchangés** — le
harnais d'intro réimplémente sa propre boucle de frame et n'exerce pas la caméra, donc un
déplacement de golden ici serait un **signal d'arrêt**, pas un ré-baseline ; preuve positive
d'exécution ; **validation en jeu par l'utilisateur**.

**Arrêts** : si un golden bouge ; si le correctif exige de toucher un des trois invariants ; si le
test de l'étape 1 passe avant correctif ; **si une assertion existante du fichier de caractérisation
doit changer** ; si un élargissement de visibilité autre que les deux nommés s'avère nécessaire.
