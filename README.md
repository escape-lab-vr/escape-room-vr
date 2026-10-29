# Escape Room VR — Laboratoire scientifique

Projet d'équipe du cours **INFO 4900 — Sujets avancés en informatique** (Université de Moncton, automne 2026).
Application de réalité virtuelle développée sous **Unity** pour le **Meta Quest 3**.

> 📌 Avant de contribuer, lisez le [guide Git de l'équipe](CONTRIBUTING.md).

---

## Membres et responsabilités

| Rôle | Membre |
|---|---|
| Gestion de projet et intégration | Allan |
| Interactions VR | Jolin |
| Locomotion, ergonomie et interface | Sarata |
| Environnement et systèmes | Brunel |
| Tests, build et performance | Oumar |

## Objectif

Le joueur est enfermé dans un laboratoire scientifique. Il doit explorer, résoudre des énigmes de difficulté progressive et débloquer de nouvelles salles pour s'échapper. Un système de temps et de score évalue sa performance.

## Scénario d'utilisation

| Salle | Énigme | Interaction |
|---|---|---|
| 1. Sas d'entrée | Trouver une carte d'accès et la glisser dans un lecteur | Saisir + glisser |
| 2. Laboratoire de chimie | Mélanger des fioles pour obtenir une couleur qui révèle un code | Manipuler + verser |
| 3. Salle finale | Entrer le code final et abaisser un levier | Clavier + levier |

*Histoire narrative : à compléter.*

## Fonctionnalités

- [ ] Locomotion : téléportation entre les salles
- [ ] Interactions : saisie, insertion, manipulation, boutons et levier
- [ ] Retours utilisateur : vibrations, sons, signaux visuels
- [ ] Interface immersive : minuteur visible, menu / liste des tâches en jeu
- [ ] Gestion des états : menu → partie → salle débloquée → victoire ou temps écoulé
- [ ] Temps et score
- [ ] Fonctionnalité avancée : *à confirmer*

## Configuration technique

| Élément | Valeur |
|---|---|
| Version de Unity | *[À COMPLÉTER]* |
| Plateforme cible | Meta Quest 3 (Android) |
| Paquets XR | OpenXR, XR Interaction Toolkit *(versions à compléter)* |

## Ouvrir le projet

1. Installer **Unity Hub**, la version de Unity indiquée ci-dessus et le module **Android Build Support**.
2. Cloner le dépôt et récupérer les fichiers lourds :
   ```bash
   git clone https://github.com/[ORGANISATION]/[DEPOT].git
   cd [DEPOT]
   git lfs pull
   ```
3. Dans Unity Hub : **Add** → **Add project from disk** → choisir le dossier cloné.

## Build sur le Quest 3

*À compléter par le responsable tests, build et performance.*

## Architecture

*Schéma et description à ajouter dans [`docs/architecture/`](docs/architecture/).*

## Choix techniques importants

*À compléter au fil du projet.*

## Limitations connues

*À compléter avant la remise.*

## Traçabilité

- Traces de réunions : [`docs/reunions/`](docs/reunions/)
- Tâches et jalons : onglets **Issues** et **Projects** du dépôt

## Crédits et licences

| Ressource | Source | Licence |
|---|---|---|
| *—* | *—* | *—* |

## Utilisation de l'IA générative

| Date | Outil / version | Objectif | Vérification ou adaptation |
|---|---|---|---|
| *—* | *—* | *—* | *—* |
