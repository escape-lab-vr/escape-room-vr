# Guide Git — Escape Room VR (INFO 4900)

> À lire **avant** ton premier clone. Ce guide évite 95 % des catastrophes.
> En cas de doute : **on ne supprime rien, on ne force rien, on demande au gestionnaire de projet.**

---

## 1. Installation (une seule fois)

| Outil | Pourquoi |
|---|---|
| **Git** | Gestion de versions |
| **Git LFS** | Fichiers lourds (modèles 3D, textures, sons) |
| **Unity Hub** + la version de Unity de l'équipe : **`[À COMPLÉTER]`** | Même version pour tout le monde, sinon le projet est modifié à l'ouverture |
| Module **Android Build Support** (avec OpenJDK et Android SDK & NDK) | Indispensable pour builder sur le Quest 3 |
| *(Optionnel)* **GitHub Desktop** | Interface graphique si tu n'aimes pas le terminal |

Puis, dans un terminal :

```bash
git config --global user.name "Ton Nom"
git config --global user.email "ton-email-github@exemple.com"
git lfs install
```

> ⚠️ L'email doit être celui de **ton** compte GitHub, sinon tes commits ne te seront pas attribués.

---

## 2. Premier clonage

```bash
git clone https://github.com/[ORGANISATION]/[DEPOT].git
cd [DEPOT]
git lfs pull
```

Ensuite :
1. Ouvre **Unity Hub** → **Add** → **Add project from disk** → choisis le dossier cloné.
2. Vérifie que c'est **la bonne version de Unity**. Si Unity propose de changer de version : **refuse** et préviens l'équipe.
3. La première ouverture est longue (Unity reconstruit le dossier `Library`). C'est normal.

---

## 3. Les règles d'or

1. **On ne travaille jamais directement sur `main`.** Une branche par tâche.
2. **On ne modifie pas la scène principale** (`MainScene`). Chacun travaille dans **sa propre scène de test** et livre son travail sous forme de **prefabs**. Seul le gestionnaire de projet intègre dans la scène principale.
3. **On fait `git pull` avant de commencer à travailler.**
4. **On sauvegarde dans Unity avant de commit** (`Ctrl+S` + `File > Save Project`).
5. **Chaque fichier a son `.meta`** : on les commit toujours ensemble.
6. **Jamais de `git push --force`.**
7. **Jamais supprimer son dossier et re-cloner** sans avoir poussé son travail (tout ce qui n'est pas poussé est perdu).
8. **Petits commits, souvent.** Un commit = une chose identifiable.

---

## 4. La routine de travail

### Commencer une tâche

```bash
git switch main
git pull
git switch -c feature/nom-de-la-tache
```

Noms de branches :
- `feature/teleportation`, `feature/melange-fioles`
- `fix/porte-zone2-bloquee`
- `docs/readme`

### Pendant le travail

```bash
git status                 # voir ce qui a changé
git add .                  # préparer les fichiers
git commit -m "feat: ajoute la saisie de la carte d'accès"
git push -u origin feature/nom-de-la-tache   # le premier push
git push                                     # les suivants
```

> 💡 Avant `git add .`, regarde `git status` : si tu vois une scène que tu n'as pas à modifier, ne la commit pas (voir section 7).

### Terminer une tâche

1. Pousse ta branche.
2. Sur GitHub : **Pull request** vers `main`, lie l'issue (`Closes #12`).
3. Le gestionnaire de projet (ou un autre membre) relit et fusionne.
4. Après la fusion :

```bash
git switch main
git pull
git branch -d feature/nom-de-la-tache
```

---

## 5. Messages de commit

Format : `type: description courte au présent`

| Type | Usage | Exemple |
|---|---|---|
| `feat` | Nouvelle fonctionnalité | `feat: ajoute le minuteur au poignet` |
| `fix` | Correction de bogue | `fix: la porte de la zone 2 ne s'ouvrait pas` |
| `refactor` | Réorganisation sans changement de comportement | `refactor: sépare GameManager en deux scripts` |
| `asset` | Ajout de modèles, sons, textures | `asset: ajoute les modèles de fioles (CC0)` |
| `docs` | Documentation | `docs: trace de réunion du 22 oct.` |
| `perf` | Optimisation | `perf: réduit les draw calls de la zone 3` |

❌ À éviter : `update`, `modifs`, `ça marche enfin`, `asdf`.

---

## 6. Mettre sa branche à jour avec `main`

Si `main` a avancé pendant que tu travaillais :

```bash
git switch main
git pull
git switch feature/ma-tache
git merge main
```

---

## 7. En cas de problème — commandes **sans danger**

| Situation | Commande |
|---|---|
| Je ne sais pas où j'en suis | `git status` |
| Annuler mes modifications sur **un** fichier (pas encore commit) | `git restore chemin/du/fichier` |
| Mettre mes modifs de côté pour faire un `pull` | `git stash` puis, après, `git stash pop` |
| J'ai ajouté un fichier par erreur avec `git add` | `git restore --staged chemin/du/fichier` |
| Un merge tourne mal, je veux revenir en arrière | `git merge --abort` |
| Je veux voir l'historique | `git log --oneline --graph` |

### Conflit sur un fichier Unity (`.unity`, `.prefab`, `.asset`)

**Ne le résous pas à la main dans un éditeur de texte.** Garde une des deux versions :

```bash
git checkout --ours chemin/du/fichier     # garder MA version
git checkout --theirs chemin/du/fichier   # garder la version de main
git add chemin/du/fichier
git commit
```

Si tu ne sais pas laquelle garder : `git merge --abort` et **préviens le gestionnaire de projet**.

### Commandes **interdites** sans en parler à l'équipe

- `git push --force`
- `git reset --hard`
- `git clean -fd`
- Supprimer le dossier du projet pour re-cloner

---

## 8. Ce qui ne doit **jamais** être commit

Le `.gitignore` s'en charge, mais vérifie quand même :

- `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`
- Les builds (`.apk`) → ils vont dans les **Releases** GitHub, pas dans le dépôt
- Les fichiers `.csproj` et `.sln`

---

## 9. Sauvegardes

Le dépôt GitHub **est** la sauvegarde : chaque commit poussé est récupérable.

- À la fin de chaque jalon, le gestionnaire de projet crée un **tag** (`jalon-1`, `jalon-2`, …) : un point de retour garanti.
- Le build de chaque jalon est publié dans les **Releases**.
- **Pas de copies manuelles du projet dans d'autres dépôts** : ça casse l'historique et la traçabilité des contributions.

---

## 10. Aide-mémoire

```bash
# Début de journée
git switch main && git pull

# Nouvelle tâche
git switch -c feature/ma-tache

# Enregistrer
git status
git add .
git commit -m "feat: ..."
git push

# Se remettre à jour
git switch main && git pull
git switch feature/ma-tache && git merge main
```

**Questions ou problème → canal de l'équipe, avant de tenter quoi que ce soit de risqué.**
