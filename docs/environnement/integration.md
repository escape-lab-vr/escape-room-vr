# Guide d'intégration — environnement et systèmes

**Rôle :** environnement et systèmes
Ce guide explique comment chaque partie de l'équipe se branche sur le décor, les portes et les systèmes du décor.

---

## 1. Construire le pavillon dans sa scène de test

1. Ouvrir sa scène (`Assets/_Project/Scenes/Test/Test_<Nom>`).
2. Menu **Escape Lab → Construire la greybox du pavillon**.
3. Menu **Escape Lab → Calculer la lumière (bake)**, puis **Ctrl + S**.

Tout est créé sous l'objet `Greybox_Pavillon` : le décor, les portes, les systèmes (`Systemes`) et l'éclairage (`Eclairage`).

## 2. Les portes (`ZoneDoor`)

Prefabs : `Assets/_Project/Prefabs/ZoneDoor_Double.prefab` et `ZoneDoor_Simple.prefab`.

| Porte dans la scène | Ouverte par |
|---|---|
| `PorteA` (hall → corridor) | Énigme 1 : la carte d'accès dans le lecteur |
| `PorteLabo` (corridor → labo) | Énigme 2 : la carte d'accès dans le lecteur du labo |
| `PorteSortie` (sortie de secours) | Énigme 4 : code + levier. Son événement `OnOpened` sert à déclencher la victoire (GameManager) |

| Méthode / événement | Usage |
|---|---|
| `Open()` | Ouvre la porte (son, glissement, voyant vert) |
| `ResetDoor()` | Referme et reverrouille |
| `OnOpened` | Événement appelé quand la porte a fini de s'ouvrir |
| `Etat` | `Verrouillee`, `Ouverture` ou `Ouverte` |

**Brancher une énigme (sans code) :** dans l'Inspector de l'énigme, sur son événement de réussite (ex. `OnSolved`), cliquer **+**, glisser la porte, choisir **ZoneDoor → Open ()**.

**Tester sans énigme :** en mode Play, clic droit sur le composant `ZoneDoor` → **Ouvrir (test)**.

## 3. Points d'interaction (Jolin)

Objets vides placés dans la greybox, à utiliser comme repères pour les objets interactifs :

| Point | Zone | Pour |
|---|---|---|
| `Point_Tiroir_Comptoir` | Hall | Tiroir et carte d'accès |
| `Point_Indice_Babillard` | Hall | Affiche avec le code |
| `Point_LecteurCarte_PorteA` | Hall, sur le mur à côté de la porte A | Lecteur de carte |
| `Point_Indice_Molecule` | Corridor | Vitrine (ordre des fioles) |
| `Point_LecteurCarte_Labo` | Corridor | Lecteur du labo |
| `Point_Fioles`, `Point_Becher`, `Point_Evier` | Labo | Énigme du mélange |
| `Point_Clavier`, `Point_Levier` | Corridor | Énigme finale |

Quand le mélange est réussi, appeler **`Fuite_Gaz → FuiteGaz.Arreter()`** : le gaz s'arrête.

## 4. Téléportation et minuteur (Sarata)

- Les sols du hall, du corridor et du labo ont un collider : on peut y ajouter les **Teleportation Area**.
- Les ancres `Ancre_Teleport_Ilot` et `Ancre_Teleport_Etagere` sont déjà placées dans le labo.
- Les portes verrouillées bloquent le rayon de téléportation.
- Minuteur : emplacement prévu `Point_Horloge` (mur du labo). Pendant la dernière minute, appeler `Alarme → AlarmeController.FinProche()`.

## 5. Systèmes du décor (pour le GameManager d'Allan et le minuteur de Sarata)

Tout se trouve sous `Greybox_Pavillon/Systemes`. L'histoire est décrite dans `plan-laboratoire.md` (section 8).

| Objet | Méthode / événement | Usage |
|---|---|---|
| `Sequence_Ambiance` (`SequenceAmbiance`) | `OnConfinement` | Appelé quand le joueur entre dans le labo : **brancher ici le départ du minuteur** |
| | `Decontamination()` | À appeler quand le temps est écoulé (échec) : voile blanc + souffle |
| | `Reinitialiser()` | Nouvelle partie : ambiance calme, zones réarmées |
| `Alarme` (`AlarmeController`) | `FinProche()` | Dernière minute : gyrophares plus rapides + sirène |
| | `Victoire()` | Alarme coupée, néons stables |
| `Fuite_Gaz` (`FuiteGaz`) | `Arreter()` | Énigme du mélange réussie |
| `PorteSortie` (`ZoneDoor`) | `OnOpened` | Brancher la victoire |
| `Sequence_Ambiance` | `OnEvacuationReussie` | Appelé quand le joueur atteint le **point de rassemblement** dehors : **brancher ici l'écran de victoire** |
| | `Evacuation()` | Fin de l'histoire (appelée automatiquement) : alarme coupée, annonce « Évacuation réussie », camion de pompiers avec gyrophares |
| `Declencheur_Corridor`, `Declencheur_Labo`, `Declencheur_Rassemblement` | automatique | Tension, fuite, puis fin au point de rassemblement (aucune action nécessaire) |

## 6. Performance (Oumar)

| Choix | Effet |
|---|---|
| Éclairage précalculé (une trentaine de lampes + lune + 3 lumières extérieures) | Aucun coût de lumière pendant le jeu |
| 5 lumières temps réel seulement (3 gyrophares + 2 feux du camion) | Allumées uniquement pendant l'alerte ou la fin |
| Objets du décor « Static » + GPU Instancing sur les matériaux | Regroupés au rendu (moins d'appels de dessin) |
| Petits accessoires (flacons, poignées, molécules…) éclairés par les sondes | Pas de place dans les lightmaps : calcul de la lumière plus rapide |
| Gaz limité à 150 particules | Transparence maîtrisée |
| Sondes de lumière | Mains et objets bien éclairés sans lumière temps réel |

Réglages du calcul : `Assets/_Project/Settings/Eclairage_Pavillon.lighting` (8 texels/m, cartes de 1024).

## 7. Intégration dans `MainScene` (Allan)

1. Ouvrir `MainScene`, lancer **Escape Lab → Construire la greybox du pavillon** et confirmer (une fenêtre rappelle que `MainScene` est réservée à l'intégration).
2. Désactiver l'objet `Lighting` du modèle VR.
3. **Escape Lab → Calculer la lumière (bake)**, puis sauvegarder.

## 8. Ambiance de soirée et sons par pièce

- L'alerte arrive **le soir** : ciel de nuit, lune bleutée, lampadaires allumés dehors, néons allumés dedans.
- Chaque pièce a son propre son (portée courte) : bourdonnement des néons dans le corridor, aspiration des hottes et tic-tac de l'horloge dans le labo, grillons et vent dehors.

## 9. Armoires qui s'ouvrent et objets manipulables (à valider avec Jolin)

- Toutes les armoires du labo (paillasses, îlots, hottes, armoires jaune et bleue) et l'armoire à extincteur du corridor ont de vraies portes : script `PorteBattante` (on saisit la porte avec la manette, elle s'ouvre ou se ferme).
- On ne peut ouvrir une porte d'armoire ou prendre un objet que si la main est à moins de 35 cm (script `FiltreProximite`) : plus d'action à distance avec le rayon.
- Les objets tenus suivent la main par la physique (Velocity Tracking) : ils ne traversent plus les tables ni les armoires.
- Les robinets des éviers, le lave-yeux et la douche d'urgence fonctionnent (script `EauActivable`) : on actionne la manette, la palette ou la tirette, l'eau coule avec son bruit.
- Tout ce qui est rangé dans les armoires se prend en main. Les liquides coulent quand on penche un bécher, un erlenmeyer, une éprouvette ou un tube (`LiquideVersable`), et le niveau baisse. Le filet suit une vraie trajectoire (LineRenderer) et tombe là où on verse : dans un autre récipient (son niveau monte, les couleurs se mélangent) ou sur une surface (une flaque apparaît et grandit).
- Extincteur (`ExtincteurActif`) : on le prend, on appuie sur la gâchette (activate) → jet de poudre + son, 12 s d'autonomie. `Recharger()` le remet à plein.
- Équipement du labo (appareils `AppareilLabo` sauf indication) : microscope (lampe), centrifugeuse (couvercle `PorteBattante` + START, rotor, tr/min), spectrophotomètre (trappe + MESURE), vortex, bain-marie (37 °C), étuve (porte vitrée + 105 °C), 3 becs Bunsen (molette → flamme), montage de titrage (robinet de burette → goutte), réfrigérateur de labo (porte, flacons attrapables), porte-pipettes + poire attrapable, distributeurs de papier et de savon, boîte à verre cassé, trousse de déversement, poste informatique.
- Les appareils fonctionnent (`AppareilLabo`, `BalanceLabo`) : la balance pèse ce qu'on pose sur le plateau (porte de la cage coulissante), la plaque agitatrice chauffe et fait tourner le barreau aimanté, le pH-mètre mesure, l'évaporateur rotatif tourne et chauffe son bain, la guillotine des hottes coulisse.
- Les poubelles s'ouvrent (couvercle sur charnière, pédale).
- La verrerie de travail, les flacons, les pissettes, le cahier, les bidons de déchets et les extincteurs sont attrapables (`XRGrabInteractable` ajouté par le constructeur). Les objets d'énigme (fioles, bécher, carte, etc.) restent ceux de Jolin aux points prévus.

## 9 bis. Ancien menu de décor attrapable

Menu **Escape Lab → Rendre quelques objets du décor attrapables** : le cahier de labo, les pissettes et trois flacons de l'îlot B reçoivent un `XRGrabInteractable`. Ce ne sont pas des objets d'énigme, seulement du réalisme. À utiliser seulement si Jolin est d'accord.

## 10. Sons

Sons provisoires générés par code dans `Assets/_Project/Audio/Environnement` (cloche, sirène, portes, gaz, ventilation, néons, hotte, horloge, nuit dehors, sirène des pompiers, annonces de confinement et d'évacuation en voix de synthèse, décontamination). Ils peuvent être remplacés par de vrais sons libres de droits (CC0) en gardant **le même nom de fichier**.

## Utilisation de l'IA

Scripts, sons provisoires et documentation produits avec l'aide de Claude (Anthropic), relus et testés par Brunel.


## Version 10 (Brunel)
- Escaliers : garde-corps continus et reliés (même main courante ronde, poteaux à chaque jonction : bord du puits → 1re marche de l'escalier principal, marche d'arrivée → escalier du sous-sol). Un mur invisible suit chaque main courante (`GardeCorps`) : on ne peut plus tomber dans le puits depuis les marches. La rampe ronde du rez-de-chaussée s'arrête là où la volée passe au-dessus du bord (poteau qui monte jusque sous la volée).
- Poste informatique (`EcranOrdinateur` + `ToucheOrdinateur` sur la souris, le clavier et l'écran) : écran de verrouillage avec l'heure et la date réelles → on touche → connexion, mot de passe tapé (sons de touches) → bureau. Revient verrouillé après 90 s. `EcranOrdinateur.Etat` (0, 1, 2) et `Action()` sont publics si l'équipe veut s'en servir pour une énigme.
- Clavier de 104 touches en relief (un seul maillage, texture `Clavier.png`), souris sans fil, mini-PC derrière l'écran.
- Boîte à verre cassé (carton, sac, 2 rabats qui s'ouvrent) et trousse de déversement (seau jaune, couvercle à charnière, tampons, boudins, gants, lunettes, sacs : tout s'attrape).
- Réfrigérateur : 2 tiroirs qui sortent (glissement `PorteBattante`), les flacons sortent avec le tiroir. Îlot B raccourci de 0,6 m pour laisser le passage.
- Camion de pompiers et ambulance refaits : carrosseries profilées (cabine, pare-brise incliné), roues avec jantes et roues jumelées, livrées (coffres à rideaux, damier réfléchissant, étoile de vie, chevrons), rampes de gyrophares rouge/blanc qui clignotent à l'évacuation.
- Corrigé : couvercle de la centrifugeuse, trappe du spectrophotomètre et porte de l'étuve s'ouvrent maintenant vers l'extérieur.
