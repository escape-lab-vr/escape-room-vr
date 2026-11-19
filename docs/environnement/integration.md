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
| `Declencheur_Corridor`, `Declencheur_Labo` | automatique | Déclenchent la tension puis la fuite (aucune action nécessaire) |

## 6. Performance (Oumar)

| Choix | Effet |
|---|---|
| Éclairage précalculé (une trentaine de lampes + soleil) | Aucun coût de lumière pendant le jeu |
| 3 lumières temps réel seulement (gyrophares) | Allumées uniquement pendant l'alerte |
| Objets du décor « Static » | Regroupés au rendu (moins d'appels de dessin) |
| Gaz limité à 150 particules | Transparence maîtrisée |
| Sondes de lumière | Mains et objets bien éclairés sans lumière temps réel |

Réglages du calcul : `Assets/_Project/Settings/Eclairage_Pavillon.lighting` (8 texels/m, cartes de 1024).

## 7. Intégration dans `MainScene` (Allan)

1. Ouvrir `MainScene`, lancer **Escape Lab → Construire la greybox du pavillon** et confirmer (une fenêtre rappelle que `MainScene` est réservée à l'intégration).
2. Désactiver l'objet `Lighting` du modèle VR.
3. **Escape Lab → Calculer la lumière (bake)**, puis sauvegarder.

## 8. Sons

Sons provisoires générés par code dans `Assets/_Project/Audio/Environnement` (cloche, sirène, portes, gaz, ventilation, annonce de confinement en voix de synthèse, décontamination). Ils peuvent être remplacés par de vrais sons libres de droits (CC0) en gardant **le même nom de fichier**.

## Utilisation de l'IA

Scripts, sons provisoires et documentation produits avec l'aide de Claude (Anthropic), relus et testés par Brunel.
