// GreyboxPavillonBuilder.cs — version 3 (réaliste)
// Rôle : environnement et systèmes (Brunel)
// Construit le pavillon Rémi-Rossignol : hall en rotonde sur 3 niveaux (sous-sol, rez-de-chaussée, 2e étage),
// escalier fermé, corridor A, labo A-111, portes ZoneDoor, détails réalistes,
// systèmes du décor (alarme, gaz, sons) et éclairage précalculé.
// Utilisation : ouvrir sa scène de test, puis menu  Escape Lab > Construire la greybox du pavillon
//               puis  Escape Lab > Calculer la lumière (bake)
// Ce script doit être dans un dossier "Editor" (il n'est pas inclus dans le build du jeu).
// Convention : 1 unité = 1 mètre, x vers l'est, z vers le nord, y vers le haut.

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

public static class GreyboxPavillonBuilder
{
    const string MatFolder = "Assets/_Project/Materials/Greybox";
    const string TexFolder = "Assets/_Project/Art/Textures/Environnement";
    const string AudioFolder = "Assets/_Project/Audio/Environnement";

    // ------------------------------------------------------------------ DIMENSIONS
    const float HallR = 9f;       // rayon du hall (18 m de diamètre)
    const float HallH = 3.5f;     // hauteur sous plafond d'un niveau du hall
    const float Dalle = 0.3f;     // épaisseur d'une dalle de plancher
    const float PuitsR = 3f;      // rayon du puits de l'escalier (rampe)
    const float OculusR = 4.5f;   // rayon de l'ouverture vers le 2e étage
    const float CorH = 2.7f;      // hauteur du corridor
    const float LabH = 3f;        // hauteur du labo
    const float PorteL = 1.8f;    // largeur des doubles portes coupe-feu
    const float PorteH = 2.2f;    // hauteur des portes
    static readonly Vector3 LabOrigin = new Vector3(12f, 0f, 1.2f); // coin sud-ouest du labo

    // Niveaux (y du sol fini)
    const float Y_RDC = 0f;
    const float Y_Etage = HallH + Dalle;          // 3,8 m
    const float Y_SousSol = -Dalle - (HallH - Dalle); // -3,5 m

    // Portes du hall, d'après le dessin de Brunel. On entre au nord (90°) en regardant vers le sud :
    //   0° = est = à GAUCHE en entrant (porte A, vers le labo)   180° = ouest = à DROITE (porte D)
    //   315° = en face à gauche (B)   225° = en face à droite (C)
    // Les 3 niveaux ont les mêmes portes. Seule la porte A du rez-de-chaussée s'ouvre.
    static readonly (string lettre, float angle)[] Portes = { ("A", 0f), ("B", 315f), ("C", 225f), ("D", 180f) };

    // Hall d'entrée (rectangle au nord du rond) avec les 4 portes vitrées
    const float VestHalfW = 5.5f;
    const float VestEnd = 12f;
    const int VestFirstSeg = 4, VestLastSeg = 8; // segments du mur rond retirés au rez-de-chaussée

    const int N = 24; // segments du mur rond (15° chacun)

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly List<Object> lampesCreees = new List<Object>();
    static readonly List<Object> lampesCorridor = new List<Object>();
    static ZoneDoor porteSortie;

    // ------------------------------------------------------------------ MENU
    [MenuItem("Escape Lab/Construire la greybox du pavillon")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name == "MainScene" && !EditorUtility.DisplayDialog("Greybox",
                "MainScene est réservée à l'intégration (gestionnaire de projet). Continuer ?", "Continuer", "Annuler"))
            return;

        var old = GameObject.Find("Greybox_Pavillon");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Greybox",
                "Une greybox existe déjà dans la scène. La remplacer ?", "Remplacer", "Annuler"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        mats.Clear();
        meshes.Clear();
        lampesCreees.Clear();
        lampesCorridor.Clear();
        porteSortie = null;
        EnsureFolders();
        ZoneDoorPrefabBuilder.BuildAll(); // portes aux dimensions réelles (1,8 m et 0,9 m)

        var root = new GameObject("Greybox_Pavillon");
        var plafonds = Group("Plafonds", root.transform, Vector3.zero); // touche H dans la vue Scene pour les masquer

        BuildHall(root.transform, plafonds);
        BuildEtages(root.transform);
        BuildEscalier(root.transform);
        BuildCorridor(root.transform, plafonds);
        BuildLab(root.transform, plafonds);
        BuildExterieur(root.transform);

        PlacePlayer(scene);
        BuildSystems(root.transform);
        BuildLighting(root.transform, scene);
        Undo.RegisterCreatedObjectUndo(root, "Greybox pavillon");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

        Debug.Log("Pavillon v3 créé : hall sur 3 niveaux, corridor A, labo A-111, portes, systèmes et lumières. " +
                  "Pense à Ctrl+S, puis Escape Lab > Calculer la lumière.");
    }

    // ------------------------------------------------------------------ MATÉRIAUX
    static Material MurHall => Mat("Mur_Hall", new Color(0.86f, 0.82f, 0.70f), 0.15f, "Peinture", 1f);
    static Material MurBeige => Mat("Mur_Beige", new Color(0.95f, 0.93f, 0.87f), 0.15f, "Peinture", 1f);
    static Material Terrazzo => Mat("Terrazzo", Color.white, 0.75f, "Terrazzo", 1.5f);
    static Material Platre => Mat("Plafond_Platre", new Color(0.96f, 0.95f, 0.92f), 0.1f, "Peinture", 2f);
    static Material Dalles => Mat("Plafond_Dalles", Color.white, 0.1f, "Plafond_Dalles", 0.6f);
    static Material Plinthe => Mat("Plinthe", new Color(0.32f, 0.22f, 0.16f), 0.3f);
    static Material Cadre => Mat("Cadre_Porte", new Color(0.24f, 0.15f, 0.10f), 0.3f);
    static Material Bois => Mat("Porte_Bois", new Color(0.86f, 0.62f, 0.36f), 0.35f, "Bois", 1f);
    static Material Metal => Mat("Metal", new Color(0.72f, 0.74f, 0.77f), 0.8f);
    static Material VerreArme => Mat("Verre_Arme", new Color(0.30f, 0.34f, 0.36f), 0.85f);
    static Material Verre => Mat("Verre", new Color(0.62f, 0.76f, 0.84f), 0.95f);
    static Material Granit => Mat("Granit", new Color(0.28f, 0.28f, 0.30f), 0.7f);
    static Material Rampe => Mat("Rampe", new Color(0.16f, 0.12f, 0.10f), 0.5f);
    static Material Ruban => Mat("Ruban_Confinement", Color.white, 0.4f, "Ruban", 0.4f);
    static Material Jaune => Mat("Panneau_Jaune", new Color(1f, 0.82f, 0.10f), 0.4f);
    static Material Rouge => Mat("Rouge_Securite", new Color(0.78f, 0.10f, 0.08f), 0.5f);
    static Material Liege => Mat("Liege", Color.white, 0.1f, "Liege", 0.5f);
    static Material Noir => Mat("Noir", new Color(0.05f, 0.05f, 0.05f), 0.6f);
    static Material Blanc => Mat("Blanc", new Color(0.96f, 0.96f, 0.96f), 0.6f);
    static Material GrisPanneau => Mat("Gris_Panneau", new Color(0.62f, 0.64f, 0.66f), 0.5f);

    // ------------------------------------------------------------------ HALL (rez-de-chaussée)
    static void BuildHall(Transform root, Transform plafonds)
    {
        var hall = Group("Hall", root, Vector3.zero);

        // Sol en terrazzo avec le puits de l'escalier au centre
        Ring("Sol_Hall", hall, Y_RDC - Dalle / 2f, PuitsR, HallR + 0.05f, Dalle, Terrazzo, true);

        // Mur rond : on retire le hall d'entrée et les 4 portes
        var skip = new HashSet<int>();
        for (int i = VestFirstSeg; i <= VestLastSeg; i++) skip.Add(i);
        foreach (var p in Portes) skip.Add(Seg(p.angle));
        var murs = Group("Murs", hall, Vector3.zero);
        WallsRound(murs, Y_RDC, HallH, skip, MurHall);

        // Portes : A (ZoneDoor, s'ouvre) ; B, C, D fermées (confinement)
        var portes = Group("Portes", hall, Vector3.zero);
        foreach (var p in Portes)
            DoorOpening(portes, Y_RDC, HallH, p.angle, p.lettre, p.lettre == "A", true);
        var ailes = Group("Ailes", hall, Vector3.zero);
        foreach (var p in Portes)
            if (p.lettre != "A") Aile(ailes, Y_RDC, HallH, p.angle, p.lettre); // l'aile A est le vrai corridor

        BuildVestibule(hall, plafonds);

        // Piliers en granit, près des murs (comme sur les photos)
        var piliers = Group("Piliers", hall, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Dir(22.5f + 45f * i);
            Box($"Pilier_{i}", piliers, dir * 7.7f + Vector3.up * (HallH / 2f),
                new Vector3(0.6f, HallH, 0.6f), Granit, rot: Quaternion.LookRotation(dir));
        }

        // Rampe à barreaux autour du puits ; un mur invisible bloque le joueur
        RailRound(Group("Rampe_Puits", hall, Vector3.zero), Y_RDC, PuitsR, true, -1);

        // Ruban de sécurité sur la rampe, face à l'entrée, et panneau d'avertissement
        var conf = Group("Confinement", hall, Vector3.zero);
        float LR = Chord(PuitsR);
        for (int i = 3; i <= 9; i++)
        {
            Vector3 dir = Dir(i * 15f);
            Box($"Ruban_{i}", conf, dir * (PuitsR + 0.06f) + Vector3.up * 0.75f, new Vector3(LR, 0.07f, 0.01f), Ruban,
                collider: false, rot: Quaternion.LookRotation(-dir));
        }
        Vector3 pPanneau = Dir(90f) * (PuitsR + 0.45f);
        var rotPanneau = Quaternion.LookRotation(Vector3.back); // lisible depuis l'entrée (on regarde vers le sud)
        Box("Panneau_Pied", conf, pPanneau + Vector3.up * 0.5f, new Vector3(0.05f, 1f, 0.05f), Metal, collider: false);
        Sign("Panneau_Confinement", conf, "ACCÈS INTERDIT\n<size=60%>PAVILLON FERMÉ</size>", pPanneau + Vector3.up * 1.2f,
             rotPanneau, new Vector2(0.7f, 0.45f), Color.black, Jaune);

        // Comptoirs de chaque côté de la porte A (le tiroir contient la carte d'accès)
        var comptoir = Mat("Comptoir", new Color(0.55f, 0.50f, 0.42f), 0.5f);
        Box("Comptoir_Nord", hall, new Vector3(7.6f, 0.5f, 2.0f), new Vector3(1.4f, 1.0f, 0.5f), comptoir);
        Box("Comptoir_Sud",  hall, new Vector3(7.6f, 0.5f, -2.0f), new Vector3(1.4f, 1.0f, 0.5f), comptoir);

        // Babillard (indice : code du tiroir) et plan d'évacuation (indice : A-111)
        Vector3 dB = Dir(135f);
        Babillard("Babillard", hall, dB * (HallR - 0.12f) + Vector3.up * 1.5f, Quaternion.LookRotation(dB), 1.6f, 1.0f);
        Vector3 dP = Dir(45f);
        Box("Plan_Evacuation", hall, dP * (HallR - 0.12f) + Vector3.up * 1.55f, new Vector3(0.5f, 0.7f, 0.02f),
            Mat("Plan_Evac", Color.white, 0.4f, "Affiche_2", 1f), collider: false, rot: Quaternion.LookRotation(dP));

        // Mobilier : fauteuils rouges, bacs de recyclage, fontaine d'eau (comme dans le vrai hall)
        var mob = Group("Mobilier", hall, Vector3.zero);
        Fauteuil("Fauteuil_1", mob, 258f, 8.15f);
        Fauteuil("Fauteuil_2", mob, 282f, 8.15f);
        var bleu = Mat("Bac_Bleu", new Color(0.10f, 0.35f, 0.75f), 0.4f);
        for (int i = 0; i < 2; i++)
        {
            Vector3 d = Dir(150f + i * 4f);
            Box($"Bac_Recyclage_{i}", mob, d * 8.55f + Vector3.up * 0.4f, new Vector3(0.45f, 0.8f, 0.45f), bleu,
                rot: Quaternion.LookRotation(d));
        }
        Vector3 dF = Dir(203f);
        // Fontaine d'eau murale : plaque contre le mur, bac en inox à 0,9 m, bouton et bec
        var fo = Group("Fontaine_Eau", mob, dF * (HallR - 0.1f));
        fo.localRotation = Quaternion.LookRotation(-dF); // z local = vers le centre du hall
        Box("Plaque_Murale", fo, new Vector3(0f, 0.75f, 0.02f), new Vector3(0.45f, 0.7f, 0.03f), Metal, collider: false);
        Box("Bac", fo, new Vector3(0f, 0.86f, 0.2f), new Vector3(0.42f, 0.1f, 0.34f), Metal);
        Box("Cuvette", fo, new Vector3(0f, 0.912f, 0.2f), new Vector3(0.34f, 0.005f, 0.26f), GrisPanneau, collider: false);
        Box("Bec", fo, new Vector3(0.1f, 0.95f, 0.12f), new Vector3(0.03f, 0.05f, 0.03f), Metal, collider: false);
        Box("Bouton", fo, new Vector3(0f, 0.86f, 0.375f), new Vector3(0.08f, 0.05f, 0.01f), Noir, collider: false);
        Box("Habillage_Bas", fo, new Vector3(0f, 0.6f, 0.12f), new Vector3(0.3f, 0.4f, 0.2f), Metal, collider: false);

        // Points pour l'équipe (objets vides)
        var pts = Group("Points", hall, Vector3.zero);
        Point("Point_Depart_Joueur", pts, new Vector3(0f, 0f, 10.5f), 180f);
        Point("Point_Tiroir_Comptoir", pts, new Vector3(7.3f, 0.85f, -2.0f));
        Point("Point_Indice_Babillard", pts, dB * (HallR - 0.2f) + Vector3.up * 1.5f);
        Point("Point_LecteurCarte_PorteA", pts, Dir(350f) * (HallR - 0.14f) + Vector3.up * 1.2f);

        // Plafond du hall (plâtre lisse) avec l'ouverture vers le 2e étage
        Ring("Plafond_Hall", plafonds, HallH + 0.025f, OculusR, HallR + 0.05f, 0.05f, Platre, false);
    }

    static void BuildVestibule(Transform hall, Transform plafonds)
    {
        var vest = Group("Hall_Entree", hall, Vector3.zero);
        float zStart = Mathf.Sqrt(HallR * HallR - VestHalfW * VestHalfW);
        float vDepth = VestEnd - zStart;
        Box("Sol_Entree", vest, new Vector3(0f, -Dalle / 2f, (zStart + VestEnd) / 2f - 0.5f),
            new Vector3(VestHalfW * 2f, Dalle, vDepth + 1f), Terrazzo);
        foreach (float sx in new[] { 1f, -1f })
        {
            Box(sx > 0 ? "Mur_Est" : "Mur_Ouest", vest, new Vector3(sx * VestHalfW, HallH / 2f, zStart + vDepth / 2f),
                new Vector3(0.2f, HallH, vDepth), MurHall);
            Box(sx > 0 ? "Plinthe_Est" : "Plinthe_Ouest", vest, new Vector3(sx * (VestHalfW - 0.11f), 0.05f, zStart + vDepth / 2f),
                new Vector3(0.02f, 0.1f, vDepth), Plinthe, collider: false);
        }
        // Mur du fond : 4 portes vitrées de 1,5 m (verrouillées), murs pleins de chaque côté
        const float glassW = 6f;
        float side = VestHalfW - glassW / 2f;
        foreach (float sx in new[] { 1f, -1f })
            Box(sx > 0 ? "Mur_Fond_Est" : "Mur_Fond_Ouest", vest, new Vector3(sx * (glassW / 2f + side / 2f), HallH / 2f, VestEnd),
                new Vector3(side, HallH, 0.2f), MurHall);
        Box("Linteau_Entree", vest, new Vector3(0f, (2.5f + HallH) / 2f, VestEnd), new Vector3(glassW, HallH - 2.5f, 0.2f), MurHall);
        for (int i = 0; i < 4; i++)
        {
            float x = -glassW / 2f + 0.75f + 1.5f * i;
            Box($"Porte_Vitree_{i + 1}", vest, new Vector3(x, 1.25f, VestEnd), new Vector3(1.42f, 2.5f, 0.06f), Verre);
            Box($"Cadre_Vitree_{i + 1}", vest, new Vector3(x - 0.73f, 1.25f, VestEnd), new Vector3(0.06f, 2.5f, 0.1f), Metal, collider: false);
            Box($"Poignee_{i + 1}", vest, new Vector3(x + 0.55f, 1.05f, VestEnd - 0.06f), new Vector3(0.03f, 0.5f, 0.03f), Metal, collider: false);
        }
        Box("Plafond_Entree", plafonds, new Vector3(0f, HallH + 0.025f, (zStart + VestEnd) / 2f - 0.5f),
            new Vector3(VestHalfW * 2f, 0.05f, vDepth + 1f), Platre, collider: false);
    }

    // ------------------------------------------------------------------ 2E ÉTAGE ET SOUS-SOL (même hall rond)
    static void BuildEtages(Transform root)
    {
        // 2e étage : plancher en anneau autour de l'ouverture, balcon vitré, mêmes portes (fermées)
        var etage = Group("Etage_2", root, Vector3.zero);
        Ring("Plancher_Etage", etage, Y_Etage - (Dalle - 0.05f) / 2f, OculusR, HallR + 0.05f, Dalle - 0.05f, Terrazzo, true);
        WallsRound(Group("Murs", etage, Vector3.zero), Y_Etage, HallH, PortesSegments(), MurHall);
        var pE = Group("Portes", etage, Vector3.zero);
        foreach (var p in Portes) DoorOpening(pE, Y_Etage, HallH, p.angle, p.lettre, false, false);
        var ailesE = Group("Ailes", etage, Vector3.zero);
        foreach (var p in Portes) Aile(ailesE, Y_Etage, HallH, p.angle, p.lettre);
        RailRound(Group("Balcon_Vitre", etage, Vector3.zero), Y_Etage, OculusR, false, Seg(AngleHautEscalier()));
        Ring("Plafond_Etage", etage, Y_Etage + HallH + 0.15f, 0f, HallR + 0.05f, 0.3f, Platre, false);
        // Enseigne visible depuis l'entrée, sur le bord du balcon (côté sud)
        Sign("Enseigne_Faculte", etage, "Bienvenue à la Faculté des sciences",
             Dir(270f) * (OculusR + 0.02f) + Vector3.up * (HallH - 0.35f), Quaternion.LookRotation(Vector3.back),
             new Vector2(3.4f, 0.4f), new Color(0.15f, 0.12f, 0.10f), Mat("Enseigne_Fond", new Color(0.93f, 0.90f, 0.82f), 0.3f));
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = Dir(45f + 90f * i);
            Box($"Pilier_{i}", etage, d * 7.7f + Vector3.up * (Y_Etage + HallH / 2f), new Vector3(0.6f, HallH, 0.6f), Granit,
                rot: Quaternion.LookRotation(d));
        }

        // Sous-sol : même hall rond, visible par le puits (lueur rouge pendant l'alerte)
        var ss = Group("Sous_Sol", root, Vector3.zero);
        Ring("Sol_Sous_Sol", ss, Y_SousSol - Dalle / 2f, 0f, HallR + 0.05f, Dalle, Terrazzo, true);
        float hSS = (Y_RDC - Dalle) - Y_SousSol;
        WallsRound(Group("Murs", ss, Vector3.zero), Y_SousSol, hSS, PortesSegments(), MurHall);
        var pS = Group("Portes", ss, Vector3.zero);
        foreach (var p in Portes) DoorOpening(pS, Y_SousSol, hSS, p.angle, p.lettre, false, false);
        Fauteuil("Fauteuil_SS", ss, 90f, 8.15f, Y_SousSol);
    }

    // ------------------------------------------------------------------ ESCALIER (relie les 3 niveaux, bloqué)
    static void BuildEscalier(Transform root)
    {
        var esc = Group("Escalier_Spirale", root, Vector3.zero);
        var marche = Mat("Marche_Bois", new Color(0.42f, 0.28f, 0.18f), 0.4f, "Bois", 1f);
        float hautTotal = Y_Etage - Y_SousSol;
        int n = NbMarches();
        float contre = hautTotal / n;
        Disc("Colonne", esc, new Vector3(0f, (Y_SousSol + Y_Etage) / 2f, 0f), 0.3f, hautTotal, Granit, false);
        float angle = 0f;
        for (int i = 0; i < n; i++)
        {
            angle = i * 18f;
            Vector3 dir = Dir(angle);
            float y = Y_SousSol + contre * (i + 1);
            Box($"Marche_{i:00}", esc, dir * 1.4f + Vector3.up * (y - 0.025f), new Vector3(0.5f, 0.05f, 1.6f), marche,
                collider: false, rot: Quaternion.LookRotation(dir));
            Box($"Main_Courante_{i:00}", esc, dir * 2.2f + Vector3.up * (y + 0.9f), new Vector3(0.48f, 0.04f, 0.04f), Rampe,
                collider: false, rot: Quaternion.LookRotation(dir));
            Box($"Barreau_{i:00}", esc, dir * 2.2f + Vector3.up * (y + 0.45f), new Vector3(0.025f, 0.9f, 0.025f), Rampe,
                collider: false, rot: Quaternion.LookRotation(dir));
        }
        // Palier du haut vers le balcon, fermé par une grille de sécurité (confinement)
        Vector3 dHaut = Dir(angle);
        var rotH = Quaternion.LookRotation(dHaut);
        Box("Palier_Haut", esc, dHaut * 3.35f + Vector3.up * (Y_Etage - 0.025f), new Vector3(1.6f, 0.05f, 2.3f), marche,
            collider: false, rot: rotH);
        var grille = Group("Grille_Securite", esc, dHaut * 3.1f + Vector3.up * Y_Etage);
        grille.localRotation = rotH;
        Box("Cadre_Haut", grille, new Vector3(0f, 2.1f, 0f), new Vector3(1.7f, 0.06f, 0.06f), Metal, collider: false);
        for (int b = 0; b < 14; b++)
            Box($"Barreau_{b}", grille, new Vector3(-0.78f + b * 0.12f, 1.05f, 0f), new Vector3(0.025f, 2.1f, 0.025f), Metal, collider: false);
        Box("Ruban_Grille", grille, new Vector3(0f, 1.1f, -0.04f), new Vector3(1.7f, 0.08f, 0.01f), Ruban, collider: false);
        Sign("Panneau_Grille", grille, "FERMÉ\n<size=60%>ACCÈS INTERDIT</size>", new Vector3(0f, 1.5f, -0.05f),
             Quaternion.LookRotation(Vector3.forward), new Vector2(0.55f, 0.35f), Color.black, Jaune);
    }

    // ------------------------------------------------------------------ CORRIDOR A
    static void BuildCorridor(Transform root, Transform plafonds)
    {
        var c = Group("Corridor", root, Vector3.zero);
        const float x0 = 9.05f, x1 = 24f, xm = (x0 + x1) / 2f, len = x1 - x0;

        // Sol : bande centrale en damier et carreaux crème sur les côtés (comme sur les photos)
        Box("Sol_Corridor", c, new Vector3(xm, -0.05f, 0f), new Vector3(len, 0.1f, 2.4f),
            Mat("Carreaux_Creme", Color.white, 0.8f, "Carreaux_Creme", 0.6f));
        Box("Sol_Damier", c, new Vector3(xm, 0.001f, 0f), new Vector3(len, 0.002f, 1.2f),
            Mat("Damier", Color.white, 0.85f, "Damier", 0.6f), collider: false);

        // Murs : nord (porte du labo 0,9 m), sud (porte de secours 1,8 m et portes de bureaux décoratives)
        WallAlongX("Mur_Nord", c, 1.2f, x0, x1, LabH, MurBeige, new Vector3(13.5f, 0.9f, 2.1f));
        WallAlongX("Mur_Sud", c, -1.2f, x0, x1, CorH, MurBeige, new Vector3(23f, PorteL, PorteH));

        // Mur du fond : fenêtre et radiateur (comme au bout du vrai corridor)
        Box("Mur_Fond", c, new Vector3(x1, CorH / 2f, 0f), new Vector3(0.2f, CorH, 2.6f), MurBeige);
        Box("Fenetre_Fond", c, new Vector3(x1 - 0.11f, 1.65f, 0f), new Vector3(0.02f, 1.1f, 1.8f), Verre, collider: false);
        Box("Appui_Fenetre", c, new Vector3(x1 - 0.17f, 1.08f, 0f), new Vector3(0.14f, 0.04f, 1.9f), Mat("Appui_Bois", new Color(0.45f, 0.30f, 0.20f), 0.4f), collider: false);
        Box("Radiateur", c, new Vector3(x1 - 0.18f, 0.45f, 0f), new Vector3(0.12f, 0.6f, 1.2f), Blanc, collider: false);

        // Portes ZoneDoor : labo (0,9 m) et sortie de secours (1,8 m, on y arrive depuis le nord)
        PlaceDoor("PorteLabo", "ZoneDoor_Simple", c, new Vector3(13.5f, 0f, 1.2f), Quaternion.identity);
        porteSortie = PlaceDoor("PorteSortie", "ZoneDoor_Double", c, new Vector3(23f, 0f, -1.2f), Quaternion.Euler(0f, 180f, 0f));

        // Portes de bureaux et de labos (décor, fermées), avec leurs numéros
        DecorDoor("Porte_A110", c, new Vector3(14.6f, 0f, -1.09f), Quaternion.LookRotation(Vector3.back), "A-110", false);
        DecorDoor("Porte_A108", c, new Vector3(18.0f, 0f, -1.09f), Quaternion.LookRotation(Vector3.back), "A-108", true);
        Sign("Plaque_A111", c, "A-111", new Vector3(14.25f, 2.05f, 1.085f), Quaternion.LookRotation(Vector3.forward),
             new Vector2(0.32f, 0.1f), Color.white, Mat("Plaque_Brune", new Color(0.30f, 0.20f, 0.14f), 0.4f));
        Sign("Plaque_Labo", c, "LABORATOIRE DE CHIMIE ORGANIQUE", new Vector3(15.3f, 2.05f, 1.085f), Quaternion.LookRotation(Vector3.forward),
             new Vector2(1.1f, 0.08f), Color.white, Mat("Plaque_Brune", new Color(0.30f, 0.20f, 0.14f), 0.4f));
        Box("Danger_Labo", c, new Vector3(12.75f, 1.4f, 1.085f), new Vector3(0.22f, 0.22f, 0.01f),
            Mat("Danger", Color.white, 0.4f, "Danger", 0.22f), collider: false, rot: Quaternion.Euler(0f, 0f, 0f));

        // Vitrine des molécules (indice : ordre des fioles)
        var vit = Group("Vitrine_Molecules", c, new Vector3(12f, 0f, -0.97f));
        Box("Meuble", vit, new Vector3(0f, 1.0f, 0f), new Vector3(2.0f, 2.0f, 0.42f), Bois);
        Box("Vitre", vit, new Vector3(0f, 1.05f, 0.215f), new Vector3(1.85f, 1.8f, 0.01f), Verre, collider: false);
        for (int s = 0; s < 4; s++)
            Box($"Tablette_{s}", vit, new Vector3(0f, 0.4f + s * 0.45f, 0.02f), new Vector3(1.85f, 0.02f, 0.34f), Bois, collider: false);
        Babillard("Babillard_Corridor", c, new Vector3(10.4f, 1.5f, 1.085f), Quaternion.LookRotation(Vector3.forward), 1.4f, 0.9f);
        Babillard("Babillard_Corridor_2", c, new Vector3(17.2f, 1.5f, 1.085f), Quaternion.LookRotation(Vector3.forward), 1.4f, 0.9f);

        // Affiches de recherche (inventées)
        Poster("Affiche_1", c, new Vector3(16.3f, 1.5f, -1.085f), Quaternion.LookRotation(Vector3.back), "Affiche_1");
        Poster("Affiche_2", c, new Vector3(19.8f, 1.5f, -1.085f), Quaternion.LookRotation(Vector3.back), "Affiche_3");

        // Sécurité : armoire à extincteur, panneau électrique, prises, enseigne SORTIE
        Box("Armoire_Extincteur", c, new Vector3(22.6f, 1.2f, 1.04f), new Vector3(0.5f, 0.8f, 0.12f), Rouge);
        Box("Extincteur_Vitre", c, new Vector3(22.6f, 1.2f, 0.975f), new Vector3(0.4f, 0.65f, 0.01f), Verre, collider: false);
        Box("Panneau_Electrique", c, new Vector3(19.4f, 1.3f, 1.08f), new Vector3(0.5f, 1.3f, 0.04f), GrisPanneau, collider: false);
        foreach (float px in new[] { 10.3f, 16.9f, 20.8f })
            Box($"Prise_{px}", c, new Vector3(px, 0.35f, -1.095f), new Vector3(0.08f, 0.12f, 0.01f), Blanc, collider: false);
        Sign("Sortie_Secours", c, "SORTIE", new Vector3(23f, 2.45f, -1.08f), Quaternion.LookRotation(Vector3.back),
             new Vector2(0.45f, 0.16f), new Color(0.9f, 0.1f, 0.08f), Mat("Enseigne_Sortie", new Color(0.92f, 0.92f, 0.90f), 0.4f));

        // Console finale (clavier + levier) près de la sortie
        Box("Console_Code_Levier", c, new Vector3(21.6f, 0.55f, -0.95f), new Vector3(0.8f, 1.1f, 0.45f),
            Mat("Console", new Color(0.25f, 0.27f, 0.30f), 0.4f));

        var pts = Group("Points", c, Vector3.zero);
        Point("Point_Indice_Molecule", pts, new Vector3(12f, 1.3f, -0.7f));
        Point("Point_LecteurCarte_Labo", pts, new Vector3(14.1f, 1.2f, 1.08f));
        Point("Point_Clavier", pts, new Vector3(21.6f, 1.15f, -0.8f));
        Point("Point_Levier", pts, new Vector3(22.1f, 1.15f, -0.8f));

        // Faux plafond en dalles de 60 cm, gicleurs, détecteurs de fumée, grilles de ventilation
        Box("Plafond_Corridor", plafonds, new Vector3(xm, CorH + 0.025f, 0f), new Vector3(len, 0.05f, 2.4f), Dalles, collider: false);
        CeilingDetails(c, CorH, new[] { 11f, 14f, 17f, 20f, 23f }, new[] { 0f }, new[] { new Vector3(12.5f, 0f, 0.3f), new Vector3(18.5f, 0f, -0.3f) });
    }

    // ------------------------------------------------------------------ LABO A-111
    // Positions LOCALES au labo (origine = coin sud-ouest).
    static void BuildLab(Transform root, Transform plafonds)
    {
        var lab = Group("Labo_A111", root, LabOrigin);
        var meuble = Mat("Meuble_Brun", new Color(0.42f, 0.26f, 0.17f), 0.45f, "Bois", 1f);
        var dessus = Mat("Dessus_Noir", new Color(0.05f, 0.05f, 0.05f), 0.8f);
        var jaune = Mat("Armoire_Jaune", new Color(0.95f, 0.80f, 0.10f), 0.4f);
        var hotteMat = Mat("Hotte", new Color(0.82f, 0.84f, 0.86f), 0.5f);

        Box("Sol_Labo", lab, new Vector3(6f, -0.05f, 4.5f), new Vector3(12f, 0.1f, 9f),
            Mat("Carrelage_Labo", Color.white, 0.6f, "Carrelage_Labo", 1.2f));
        Disc("Drain", lab, new Vector3(1.4f, 0.004f, 1.6f), 0.18f, 0.004f, Rampe, false);

        // Murs + plinthes
        Box("Mur_Ouest", lab, new Vector3(0f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), MurBeige);
        Box("Mur_Est",   lab, new Vector3(12f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), MurBeige);
        Box("Plinthe_Ouest", lab, new Vector3(0.11f, 0.05f, 4.5f), new Vector3(0.02f, 0.1f, 9f), Plinthe, collider: false);
        Box("Plinthe_Est",   lab, new Vector3(11.89f, 0.05f, 4.5f), new Vector3(0.02f, 0.1f, 9f), Plinthe, collider: false);

        // Mur nord avec 8 grandes fenêtres (lumière du jour), appuis
        Box("Mur_Nord_Bas",  lab, new Vector3(6f, 0.5f, 9f), new Vector3(12.2f, 1.0f, 0.2f), MurBeige);
        Box("Mur_Nord_Haut", lab, new Vector3(6f, 2.8f, 9f), new Vector3(12.2f, 0.4f, 0.2f), MurBeige);
        Box("Fenetres",      lab, new Vector3(6f, 1.8f, 9f), new Vector3(12f, 1.6f, 0.04f), Verre);
        for (int i = 1; i < 8; i++)
            Box($"Montant_{i}", lab, new Vector3(1.5f * i, 1.8f, 9f), new Vector3(0.3f, 1.6f, 0.26f), Granit);
        Box("Traverse", lab, new Vector3(6f, 1.45f, 8.97f), new Vector3(12f, 0.05f, 0.08f), Metal, collider: false);

        // Îlot A (énigme : bécher + évaporateurs) et îlot B (décor : robinets, supports)
        Ilot("IlotA", lab, new Vector3(4.5f, 0f, 3f), 6f, meuble, dessus, false);
        Ilot("IlotB", lab, new Vector3(6.5f, 0f, 6f), 8f, meuble, dessus, true);
        Box("Evier", lab, new Vector3(10.1f, 0.925f, 6f), new Vector3(0.6f, 0.01f, 0.5f), Metal, collider: false);
        for (int e = 0; e < 2; e++)
        {
            var ev = Group($"Evaporateur_{e}", lab, new Vector3(2.1f + e * 0.8f, 0.92f, 3.2f));
            Box("Socle", ev, new Vector3(0f, 0.2f, 0f), new Vector3(0.35f, 0.4f, 0.35f), Blanc);
            Box("Bras", ev, new Vector3(0.05f, 0.5f, 0f), new Vector3(0.08f, 0.35f, 0.08f), Blanc, collider: false);
            var ballon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballon.name = "Ballon";
            ballon.transform.SetParent(ev, false);
            ballon.transform.localPosition = new Vector3(0.12f, 0.35f, -0.1f);
            ballon.transform.localScale = Vector3.one * 0.18f;
            ballon.GetComponent<Renderer>().sharedMaterial = Verre;
            Object.DestroyImmediate(ballon.GetComponent<Collider>());
        }

        // Paillasse sous les fenêtres, étagère des fioles, hotte (source de la fuite), armoire jaune, chariot
        Box("Paillasse_Fenetres", lab, new Vector3(6f, 0.44f, 8.6f), new Vector3(11.6f, 0.88f, 0.6f), meuble);
        Box("Paillasse_Dessus", lab, new Vector3(6f, 0.9f, 8.6f), new Vector3(11.65f, 0.04f, 0.65f), dessus);
        var et = Group("Etagere_Fioles", lab, new Vector3(0.3f, 0f, 5f));
        Box("Cote_N", et, new Vector3(0f, 1.0f, 0.78f), new Vector3(0.4f, 2.0f, 0.04f), meuble);
        Box("Cote_S", et, new Vector3(0f, 1.0f, -0.78f), new Vector3(0.4f, 2.0f, 0.04f), meuble);
        Box("Fond", et, new Vector3(-0.18f, 1.0f, 0f), new Vector3(0.04f, 2.0f, 1.6f), meuble);
        for (int s = 0; s < 5; s++)
            Box($"Tablette_{s}", et, new Vector3(0f, 0.1f + s * 0.45f, 0f), new Vector3(0.4f, 0.03f, 1.56f), meuble);
        var hotte = Group("Hotte", lab, new Vector3(11.5f, 0f, 3f));
        Box("Caisson", hotte, new Vector3(0.1f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 1.5f), hotteMat);
        Box("Plan", hotte, new Vector3(-0.2f, 0.9f, 0f), new Vector3(0.4f, 0.04f, 1.4f), dessus);
        Box("Vitre_Guillotine", hotte, new Vector3(-0.21f, 1.4f, 0f), new Vector3(0.01f, 0.7f, 1.3f), Verre, collider: false);
        Box("Bas_Hotte", hotte, new Vector3(-0.1f, 0.44f, 0f), new Vector3(0.6f, 0.88f, 1.5f), meuble);
        Box("Armoire_Jaune", lab, new Vector3(0.6f, 0.9f, 0.4f), new Vector3(1.0f, 1.8f, 0.5f), jaune);
        Sign("Inflammable", lab, "INFLAMMABLE", new Vector3(0.6f, 1.35f, 0.655f), Quaternion.LookRotation(Vector3.back),
             new Vector2(0.7f, 0.12f), new Color(0.75f, 0.05f, 0.05f), null);
        Box("Chariot", lab, new Vector3(3f, 0.45f, 0.6f), new Vector3(0.8f, 0.9f, 0.5f), Metal);
        Disc("Poubelle", lab, new Vector3(2.2f, 0.35f, 0.5f), 0.4f, 0.7f, GrisPanneau, true);

        // Douche de sécurité et lavage des yeux (près de la porte, comme sur les photos)
        var dou = Group("Douche_Securite", lab, new Vector3(0.45f, 0f, 1.3f));
        Disc("Tuyau", dou, new Vector3(0f, 1.15f, 0f), 0.05f, 2.3f, Metal, false);
        Disc("Pomme", dou, new Vector3(0.25f, 2.25f, 0f), 0.3f, 0.06f, Metal, false);
        Disc("Lave_Yeux", dou, new Vector3(0.2f, 1.0f, 0f), 0.28f, 0.06f, Metal, false);

        // Tableau blanc (indice : couleur cible), tableau périodique (inventé), horloge (minuteur)
        Box("Tableau_Blanc", lab, new Vector3(7f, 1.6f, 0.125f), new Vector3(2f, 1f, 0.02f), Blanc, collider: false);
        Box("Cadre_Tableau", lab, new Vector3(7f, 1.6f, 0.115f), new Vector3(2.06f, 1.06f, 0.01f), Metal, collider: false);
        Box("Tableau_Periodique", lab, new Vector3(11.885f, 1.45f, 6.6f), new Vector3(0.01f, 0.9f, 1.6f),
            Mat("Tableau_Periodique", Color.white, 0.3f, "Tableau_Periodique", 1.6f), collider: false);
        var horloge = Disc("Horloge", lab, new Vector3(11.88f, 2.4f, 6.6f), 0.4f, 0.03f, Blanc, false);
        horloge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Points pour l'équipe
        var pts = Group("Points", lab, Vector3.zero);
        Point("Point_Fioles",  pts, new Vector3(0.7f, 1.2f, 5f));
        Point("Point_Becher",  pts, new Vector3(4.5f, 0.95f, 3f));
        Point("Point_Evier",   pts, new Vector3(10.1f, 0.95f, 6f));
        Point("Point_Hotte",   pts, new Vector3(11f, 1.2f, 3f));
        Point("Point_Horloge", pts, new Vector3(11.85f, 2.4f, 6.6f));
        Point("Ancre_Teleport_Ilot",    pts, new Vector3(4.5f, 0f, 1.8f), 0f);
        Point("Ancre_Teleport_Etagere", pts, new Vector3(1.3f, 0f, 5f), -90f);

        // Faux plafond en dalles, détails techniques
        Box("Plafond_Labo", plafonds, LabOrigin + new Vector3(6f, LabH + 0.025f, 4.5f), new Vector3(12f, 0.05f, 9f), Dalles, collider: false);
        CeilingDetails(lab, LabH, new[] { 1.5f, 4.5f, 7.5f, 10.5f }, new[] { 4.5f },
                       new[] { new Vector3(4.5f, 0f, 1.2f), new Vector3(8.5f, 0f, 7.8f) });
    }

    static void Ilot(string name, Transform parent, Vector3 pos, float longueur, Material meuble, Material dessus, bool robinets)
    {
        var g = Group(name, parent, pos);
        Box("Meuble", g, new Vector3(0f, 0.44f, 0f), new Vector3(longueur, 0.88f, 1.6f), meuble);
        Box("Dessus", g, new Vector3(0f, 0.9f, 0f), new Vector3(longueur + 0.05f, 0.04f, 1.65f), dessus);
        // Portes des tiroirs (lignes plus foncées sur les côtés longs)
        for (float x = -longueur / 2f + 0.5f; x < longueur / 2f; x += 0.6f)
            foreach (float sz in new[] { 1f, -1f })
                Box("Joint", g, new Vector3(x, 0.45f, sz * 0.805f), new Vector3(0.01f, 0.75f, 0.01f), Rampe, collider: false);
        if (!robinets) return;
        for (float x = -longueur / 2f + 1f; x < longueur / 2f - 0.5f; x += 2f)
        {
            Box("Robinet_Pied", g, new Vector3(x, 1.12f, 0f), new Vector3(0.04f, 0.4f, 0.04f), Metal, collider: false);
            Box("Robinet_Bec", g, new Vector3(x, 1.3f, 0.1f), new Vector3(0.03f, 0.03f, 0.22f), Metal, collider: false);
            Box("Support_Tige", g, new Vector3(x + 0.6f, 1.25f, -0.4f), new Vector3(0.015f, 0.7f, 0.015f), Metal, collider: false);
            Box("Support_Pince", g, new Vector3(x + 0.6f, 1.35f, -0.33f), new Vector3(0.02f, 0.02f, 0.14f), Metal, collider: false);
        }
    }

    // ------------------------------------------------------------------ EXTÉRIEUR (vu par les portes vitrées et les fenêtres)
    static void BuildExterieur(Transform root)
    {
        var ext = Group("Exterieur", root, Vector3.zero);
        var herbe = Mat("Exterieur_Herbe", new Color(0.33f, 0.40f, 0.24f), 0.1f);
        var trottoir = Mat("Exterieur_Trottoir", new Color(0.55f, 0.55f, 0.53f), 0.2f);

        // Terrain tout autour du pavillon (en morceaux, pour ne pas couvrir le puits du hall)
        Box("Trottoir_Entree", ext, new Vector3(0f, -0.08f, 15f), new Vector3(12f, 0.1f, 6f), trottoir);
        Box("Herbe_Nord", ext, new Vector3(6f, -0.12f, 30f), new Vector3(60f, 0.1f, 24f), herbe, collider: false);
        Box("Herbe_Labo", ext, new Vector3(20f, -0.12f, 14f), new Vector3(20f, 0.1f, 8f), herbe, collider: false);
        Box("Herbe_Sud", ext, new Vector3(10f, -0.12f, -24.75f), new Vector3(80f, 0.1f, 30.5f), herbe, collider: false);
        Box("Herbe_Est", ext, new Vector3(37.1f, -0.12f, 10.25f), new Vector3(25.8f, 0.1f, 39.5f), herbe, collider: false);
        Box("Herbe_Ouest", ext, new Vector3(-24.75f, -0.12f, 4.25f), new Vector3(30.5f, 0.1f, 27.5f), herbe, collider: false);
        // Herbe près du hall (cachée dans les planchers ; on laisse libre le puits central)
        Box("Herbe_Hall_Ouest", ext, new Vector3(-6.35f, -0.12f, 4.25f), new Vector3(6.3f, 0.1f, 27.5f), herbe, collider: false);
        Box("Herbe_Hall_Est",   ext, new Vector3( 7.6f, -0.12f, 4.25f), new Vector3(8.8f, 0.1f, 27.5f), herbe, collider: false);
        Box("Herbe_Hall_Sud",   ext, new Vector3( 0f,   -0.12f, -6.35f), new Vector3(6.4f, 0.1f, 6.3f), herbe, collider: false);
        Box("Herbe_Hall_Nord",  ext, new Vector3( 0f,   -0.12f, 7.6f),  new Vector3(6.4f, 0.1f, 8.8f), herbe, collider: false);
        Box("Herbe_Entree_Cotes", ext, new Vector3(0f, -0.12f, 15f), new Vector3(19f, 0.1f, 6f), herbe, collider: false);

        // Derrière la porte de secours : palier en béton, allée, herbe praticable et point de rassemblement
        var sortie = Group("Sortie_Secours", ext, Vector3.zero);
        Box("Herbe_Sortie", sortie, new Vector3(16.85f, -0.12f, -5.4f), new Vector3(14.7f, 0.1f, 8.2f), herbe);
        Box("Palier_Beton", sortie, new Vector3(23f, -0.05f, -2.9f), new Vector3(3f, 0.1f, 3.2f), trottoir);
        Box("Allee", sortie, new Vector3(23f, -0.06f, -7f), new Vector3(1.6f, 0.1f, 5f), trottoir);
        var poteau = Mat("Poteau_Lampadaire", new Color(0.18f, 0.18f, 0.20f), 0.5f);
        Box("Lampadaire_Poteau", sortie, new Vector3(25f, 1.75f, -4f), new Vector3(0.1f, 3.5f, 0.1f), poteau);
        Box("Lampadaire_Tete", sortie, new Vector3(24.8f, 3.5f, -4f), new Vector3(0.5f, 0.12f, 0.25f),
            EmissiveMat("Lampe", new Color(1f, 0.97f, 0.9f), 1.5f), collider: false);
        // Panneau vert « Point de rassemblement » : le but du joueur une fois dehors
        var vert = Mat("Panneau_Vert", new Color(0.05f, 0.45f, 0.20f), 0.4f);
        Box("Panneau_Poteau", sortie, new Vector3(23f, 0.9f, -9.4f), new Vector3(0.06f, 1.8f, 0.06f), Metal);
        Sign("Panneau_Rassemblement", sortie, "POINT DE\nRASSEMBLEMENT", new Vector3(23f, 1.95f, -9.36f),
             Quaternion.LookRotation(Vector3.back), new Vector2(0.8f, 0.45f), Color.white, vert);
    }

    // ------------------------------------------------------------------ ÉLÉMENTS RÉUTILISÉS
    static int Seg(float angle) => Mathf.RoundToInt(angle / 15f) % N;
    static int NbMarches() => Mathf.RoundToInt((Y_Etage - Y_SousSol) / 0.175f);
    static float AngleHautEscalier() => ((NbMarches() - 1) * 18f) % 360f;
    static float Chord(float r) => 2f * r * Mathf.Tan(Mathf.PI / N) + 0.05f;

    static HashSet<int> PortesSegments()
    {
        var s = new HashSet<int>();
        foreach (var p in Portes) s.Add(Seg(p.angle));
        return s;
    }

    // Mur rond de 24 segments + plinthes, en sautant les segments demandés
    static void WallsRound(Transform parent, float y0, float h, HashSet<int> skip, Material mur)
    {
        float L = Chord(HallR);
        for (int i = 0; i < N; i++)
        {
            if (skip.Contains(i)) continue;
            Vector3 dir = Dir(i * 15f);
            var rot = Quaternion.LookRotation(dir);
            Box($"Mur_{i:00}", parent, dir * HallR + Vector3.up * (y0 + h / 2f), new Vector3(L, h, 0.2f), mur, rot: rot);
            Box($"Plinthe_{i:00}", parent, dir * (HallR - 0.11f) + Vector3.up * (y0 + 0.05f), new Vector3(L - 0.04f, 0.1f, 0.02f),
                Plinthe, collider: false, rot: rot);
        }
    }

    // Ouverture de porte dans le mur rond : linteau, jambages, cadre, porte, plaque avec la lettre
    static void DoorOpening(Transform parent, float y0, float h, float angle, string lettre, bool zoneDoor, bool affiche)
    {
        Vector3 dir = Dir(angle);
        var e = Group($"Entree_{lettre}", parent, dir * HallR + Vector3.up * y0);
        e.localRotation = Quaternion.LookRotation(dir); // z local = vers l'extérieur du hall
        float L = Chord(HallR);
        Box("Linteau", e, new Vector3(0f, (PorteH + h) / 2f, 0f), new Vector3(L, h - PorteH, 0.2f), MurHall);
        float jamb = (L - PorteL - 0.16f) / 2f;
        foreach (float sx in new[] { 1f, -1f })
        {
            Box(sx > 0 ? "Jambage_D" : "Jambage_G", e, new Vector3(sx * (L / 2f - jamb / 2f), PorteH / 2f, 0f),
                new Vector3(jamb, PorteH, 0.2f), MurHall);
            if (!zoneDoor) // le prefab ZoneDoor a déjà son cadre
                Box(sx > 0 ? "Cadre_D" : "Cadre_G", e, new Vector3(sx * (PorteL / 2f + 0.04f), PorteH / 2f, 0f),
                    new Vector3(0.08f, PorteH + 0.08f, 0.26f), Cadre, collider: false);
        }
        if (!zoneDoor)
            Box("Cadre_Haut", e, new Vector3(0f, PorteH + 0.04f, 0f), new Vector3(PorteL + 0.16f, 0.08f, 0.26f), Cadre, collider: false);

        if (zoneDoor) PlaceDoor($"Porte{lettre}", "ZoneDoor_Double", e, Vector3.zero, Quaternion.identity);
        else ClosedDoubleDoor(e, affiche);

        Box("Plaque", e, new Vector3(0f, PorteH + 0.55f, -0.115f), new Vector3(0.45f, 0.38f, 0.02f), Cadre, collider: false);
        Sign("Lettre", e, lettre, new Vector3(0f, PorteH + 0.55f, -0.13f), Quaternion.identity, new Vector2(0.36f, 0.32f), Color.white, null);
    }

    // Aile du bâtiment derrière une porte du hall : un corridor fermé de 7 m (sol, murs, toit, mur du bout).
    // On ne peut pas y entrer (porte fermée), mais de l'extérieur le pavillon a ses vraies ailes.
    static void Aile(Transform parent, float y0, float h, float angle, string lettre)
    {
        const float L = 7f, W = 2.8f;
        Vector3 dir = Dir(angle);
        var a = Group($"Aile_{lettre}", parent, dir * HallR + Vector3.up * y0);
        a.localRotation = Quaternion.LookRotation(dir); // z local = vers l'extérieur
        Box("Sol", a, new Vector3(0f, -Dalle / 2f, L / 2f + 0.1f), new Vector3(W, Dalle, L), Terrazzo, collider: false);
        Box("Mur_G", a, new Vector3(-W / 2f + 0.1f, h / 2f, L / 2f + 0.1f), new Vector3(0.2f, h, L), MurHall, collider: false);
        Box("Mur_D", a, new Vector3( W / 2f - 0.1f, h / 2f, L / 2f + 0.1f), new Vector3(0.2f, h, L), MurHall, collider: false);
        Box("Mur_Bout", a, new Vector3(0f, h / 2f, L + 0.2f), new Vector3(W, h, 0.2f), MurHall, collider: false);
        Box("Toit", a, new Vector3(0f, h + 0.15f, L / 2f + 0.1f), new Vector3(W, 0.3f, L + 0.2f), Platre, collider: false);
        // Une fenêtre au bout de l'aile, vue de l'extérieur
        Box("Fenetre_Bout", a, new Vector3(0f, h * 0.55f, L + 0.31f), new Vector3(1.2f, 1.0f, 0.02f), Verre, collider: false);
    }

    // Double porte coupe-feu fermée (décor) : battants, fenêtres grillagées, barres anti-panique
    static void ClosedDoubleDoor(Transform e, bool afficheConfinement)
    {
        var porte = Group("Porte_Fermee", e, Vector3.zero);
        float w = PorteL / 2f;
        foreach (float sx in new[] { -1f, 1f })
        {
            string cote = sx < 0 ? "G" : "D";
            Box($"Battant_{cote}", porte, new Vector3(sx * w / 2f, PorteH / 2f, 0f), new Vector3(w - 0.01f, PorteH - 0.01f, 0.05f), Bois);
            Box($"Fenetre_{cote}", porte, new Vector3(sx * (w / 2f + 0.15f), 1.55f, 0f), new Vector3(0.16f, 0.55f, 0.06f), VerreArme, collider: false);
            Box($"Barre_{cote}", porte, new Vector3(sx * w / 2f, 1.0f, -0.06f), new Vector3(w * 0.75f, 0.05f, 0.04f), Metal, collider: false);
            Box($"Plaque_Bas_{cote}", porte, new Vector3(sx * w / 2f, 0.12f, -0.03f), new Vector3(w - 0.06f, 0.22f, 0.005f), Metal, collider: false);
        }
        Box("Ferme_Porte", porte, new Vector3(0.5f, PorteH - 0.12f, -0.07f), new Vector3(0.35f, 0.06f, 0.06f), GrisPanneau, collider: false);
        if (afficheConfinement)
            Sign("Affiche_Confinement", porte, "ACCÈS FERMÉ\n<size=55%>PAVILLON FERMÉ</size>", new Vector3(-w / 2f, 1.3f, -0.03f),
                 Quaternion.identity, new Vector2(0.5f, 0.32f), Color.black, Jaune);
    }

    // Porte de bureau ou de labo (décor) posée contre un mur du corridor
    static void DecorDoor(string name, Transform parent, Vector3 pos, Quaternion rot, string numero, bool danger)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Cadre", g, new Vector3(0f, 1.1f, 0.005f), new Vector3(1.06f, 2.2f, 0.03f), Cadre, collider: false);
        Box("Porte", g, new Vector3(0f, 1.05f, -0.015f), new Vector3(0.9f, 2.08f, 0.03f), Bois, collider: false);
        Box("Poignee", g, new Vector3(0.36f, 1.0f, -0.04f), new Vector3(0.05f, 0.05f, 0.05f), Metal, collider: false);
        Sign("Numero", g, numero, new Vector3(0f, 2.32f, -0.01f), Quaternion.identity, new Vector2(0.32f, 0.1f), Color.white,
             Mat("Plaque_Brune", new Color(0.30f, 0.20f, 0.14f), 0.4f));
        if (danger)
        {
            Box("Danger", g, new Vector3(0f, 1.45f, -0.035f), new Vector3(0.22f, 0.22f, 0.005f), Mat("Danger", Color.white, 0.4f, "Danger", 0.22f), collider: false);
            Sign("Haute_Tension", g, "DANGER\n<size=70%>HAUTE TENSION</size>", new Vector3(0f, 1.15f, -0.035f), Quaternion.identity,
                 new Vector2(0.32f, 0.18f), new Color(0.75f, 0.05f, 0.05f), Blanc);
        }
    }

    // Rampe circulaire : barreaux (rez-de-chaussée) ou panneaux vitrés (balcon du 2e étage)
    static void RailRound(Transform parent, float y0, float r, bool barreaux, int skipSeg)
    {
        float LR = Chord(r);
        for (int i = 0; i < N; i++)
        {
            if (i == skipSeg) continue;
            Vector3 dir = Dir(i * 15f);
            var rot = Quaternion.LookRotation(dir);
            Vector3 c = dir * r + Vector3.up * y0;
            Box($"Main_Courante_{i:00}", parent, c + Vector3.up * 1.05f, new Vector3(LR, 0.06f, 0.08f), Rampe, collider: false, rot: rot);
            if (barreaux)
            {
                ColliderOnly($"Blocage_{i:00}", parent, c + Vector3.up * 0.55f, new Vector3(LR, 1.1f, 0.1f), rot);
                Box($"Lisse_{i:00}", parent, c + Vector3.up * 0.1f, new Vector3(LR, 0.04f, 0.04f), Rampe, collider: false, rot: rot);
                Vector3 t = Vector3.Cross(Vector3.up, dir);
                for (int j = -1; j <= 1; j++)
                    Box($"Barreau_{i:00}_{j + 1}", parent, c + t * (j * LR / 3f) + Vector3.up * 0.575f,
                        new Vector3(0.025f, 0.95f, 0.025f), Rampe, collider: false, rot: rot);
            }
            else
            {
                Box($"Vitre_{i:00}", parent, c + Vector3.up * 0.52f, new Vector3(LR - 0.04f, 0.95f, 0.02f), Verre, collider: false, rot: rot);
            }
        }
    }

    static void Babillard(string name, Transform parent, Vector3 pos, Quaternion rot, float w, float h)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Cadre", g, Vector3.zero, new Vector3(w + 0.06f, h + 0.06f, 0.03f), Metal, collider: false);
        Box("Liege", g, new Vector3(0f, 0f, -0.016f), new Vector3(w, h, 0.01f), Liege, collider: false);
        var feuilles = new[] { "Affiche_1", "Affiche_2", "Affiche_3" };
        for (int i = 0; i < 4; i++)
        {
            float fx = -w / 2f + 0.2f + i * (w - 0.3f) / 4f;
            float fy = (i % 2 == 0 ? 0.12f : -0.1f) * h;
            Box($"Feuille_{i}", g, new Vector3(fx, fy, -0.023f), new Vector3(0.21f, 0.29f, 0.003f),
                Mat(feuilles[i % 3], Color.white, 0.2f, feuilles[i % 3], 1f), collider: false, rot: Quaternion.Euler(0f, 0f, (i - 1.5f) * 2f));
        }
    }

    static void Poster(string name, Transform parent, Vector3 pos, Quaternion rot, string tex)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Affiche", g, Vector3.zero, new Vector3(0.6f, 0.9f, 0.004f), Mat(tex, Color.white, 0.2f, tex, 1f), collider: false);
        Box("Baguette", g, new Vector3(0f, 0.47f, 0f), new Vector3(0.68f, 0.02f, 0.02f), Mat("Appui_Bois", new Color(0.45f, 0.30f, 0.20f), 0.4f), collider: false);
    }

    static void Fauteuil(string name, Transform parent, float angle, float r, float y0 = 0f)
    {
        Vector3 dir = Dir(angle);
        var g = Group(name, parent, dir * r + Vector3.up * y0);
        g.localRotation = Quaternion.LookRotation(-dir); // z local = vers le centre du hall (là où l'on regarde assis)
        var tissu = Mat("Fauteuil_Rouge", new Color(0.62f, 0.10f, 0.10f), 0.2f);
        // Banquette 2 places : pieds, assise, dossier contre le mur (côté -z), accoudoirs
        foreach (float px in new[] { -0.62f, 0.62f })
            foreach (float pz in new[] { -0.28f, 0.28f })
                Box("Pied", g, new Vector3(px, 0.06f, pz), new Vector3(0.05f, 0.12f, 0.05f), Metal, collider: false);
        Box("Assise", g, new Vector3(0f, 0.27f, 0.05f), new Vector3(1.4f, 0.3f, 0.65f), tissu);
        Box("Dossier", g, new Vector3(0f, 0.62f, -0.3f), new Vector3(1.4f, 0.55f, 0.15f), tissu, collider: false);
        Box("Accoudoir_G", g, new Vector3(-0.66f, 0.5f, 0f), new Vector3(0.1f, 0.2f, 0.7f), tissu, collider: false);
        Box("Accoudoir_D", g, new Vector3( 0.66f, 0.5f, 0f), new Vector3(0.1f, 0.2f, 0.7f), tissu, collider: false);
    }

    // Gicleurs et détecteurs de fumée en ligne, grilles de ventilation
    static void CeilingDetails(Transform parent, float h, float[] xs, float[] zs, Vector3[] grilles)
    {
        var g = Group("Details_Plafond", parent, Vector3.zero);
        int k = 0;
        foreach (float x in xs)
            foreach (float z in zs)
            {
                Disc($"Gicleur_{k}", g, new Vector3(x, h - 0.03f, z + 0.3f), 0.06f, 0.06f, Metal, false);
                if (k % 2 == 0) Disc($"Detecteur_{k}", g, new Vector3(x + 0.8f, h - 0.02f, z - 0.2f), 0.12f, 0.04f, Blanc, false);
                k++;
            }
        var grilleMat = Mat("Grille_Ventilation", Color.white, 0.4f, "Grille_Ventilation", 0.6f);
        for (int i = 0; i < grilles.Length; i++)
            Box($"Grille_{i}", g, grilles[i] + Vector3.up * (h - 0.005f), new Vector3(0.6f, 0.01f, 0.6f), grilleMat, collider: false);
    }

    // ------------------------------------------------------------------ JOUEUR
    static void PlacePlayer(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (!go.name.Contains("XR Origin")) continue;
            Undo.RecordObject(go.transform, "Placer le joueur");
            go.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10.5f), Quaternion.Euler(0f, 180f, 0f));
            Debug.Log($"{go.name} placé au point de départ (0, 0, 10.5), face au hall.");
            return;
        }
        Debug.LogWarning("Aucun XR Origin trouvé : place ton joueur à la main en (0, 0, 10.5), rotation Y = 180.");
    }

    // ------------------------------------------------------------------ SYSTÈMES DU DÉCOR
    static void BuildSystems(Transform root)
    {
        var sys = Group("Systemes", root, Vector3.zero);
        var lampeMat = EmissiveMat("Lampe", new Color(1f, 0.97f, 0.9f), 1.5f);
        var gyroMat = EmissiveMat("Gyrophare", new Color(1f, 0.08f, 0.05f), 0f);
        var chaud = new Color(1f, 0.93f, 0.82f);

        // Éclairage précalculé (aucun coût pendant le jeu)
        var lampes = Group("Lampes", sys, Vector3.zero);
        for (int i = 0; i < 8; i++) // hall : plafonniers ronds encastrés
            Lampe($"Hall_{i}", lampes, Dir(45f * i) * 6.4f + Vector3.up * (HallH - 0.01f), true, Vector3.zero, lampeMat, chaud, 1.1f);
        Lampe("Entree_1", lampes, new Vector3(-2.5f, HallH - 0.01f, 10f), true, Vector3.zero, lampeMat, chaud, 1.0f);
        Lampe("Entree_2", lampes, new Vector3( 2.5f, HallH - 0.01f, 10f), true, Vector3.zero, lampeMat, chaud, 1.0f);
        for (int i = 0; i < 4; i++) // 2e étage
            Lampe($"Etage_{i}", lampes, Dir(45f + 90f * i) * 6.6f + Vector3.up * (Y_Etage + HallH - 0.01f), true, Vector3.zero, lampeMat, chaud, 0.9f);
        for (int i = 0; i < 2; i++) // sous-sol (faible)
            Lampe($"SousSol_{i}", lampes, Dir(90f + 180f * i) * 6f + Vector3.up * (Y_RDC - Dalle - 0.01f), true, Vector3.zero, lampeMat, chaud, 0.5f);
        for (int i = 0; i < 6; i++) // corridor : plafonniers fixés en haut des murs, en alternance (comme sur les photos)
        {
            float z = i % 2 == 0 ? 1.04f : -1.04f;
            Lampe($"Corridor_{i}", lampes, new Vector3(10.5f + 2.5f * i, CorH - 0.14f, z), false, new Vector3(1.2f, 0.08f, 0.12f), lampeMat, chaud, 1.1f);
            lampesCorridor.Add(lampesCreees[lampesCreees.Count - 1]);
        }
        foreach (float lx in new[] { 2.5f, 6f, 9.5f }) // labo : luminaires encastrés dans les dalles
            foreach (float lz in new[] { 2.5f, 6.5f })
                Lampe($"Labo_{lx}_{lz}", lampes, LabOrigin + new Vector3(lx, LabH - 0.01f, lz), false, new Vector3(1.2f, 0.02f, 0.6f), lampeMat, chaud, 1.1f);

        // Gyrophares : sous-sol (la lueur rouge monte par le puits), corridor, labo
        var gyroGroup = Group("Gyrophares", sys, Vector3.zero);
        var gyros = new List<Object>();
        var gyroLights = new List<Object>();
        var gyroPos = new[] {
            new Vector3(0f, Y_RDC - Dalle - 0.08f, 5.5f),
            new Vector3(17f, CorH - 0.08f, 0f),
            LabOrigin + new Vector3(6f, LabH - 0.08f, 4.5f) };
        var gyroRange = new[] { 10f, 7f, 7f };
        for (int i = 0; i < gyroPos.Length; i++)
        {
            var g = Disc($"Gyrophare_{i}", gyroGroup, gyroPos[i], 0.18f, 0.12f, gyroMat, false);
            gyros.Add(g.GetComponent<Renderer>());
            var lgo = new GameObject($"Lumiere_Gyrophare_{i}");
            lgo.transform.SetParent(gyroGroup, false);
            lgo.transform.localPosition = gyroPos[i] + Vector3.down * 0.4f;
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.1f, 0.05f);
            l.range = gyroRange[i];
            l.intensity = 2.5f;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            l.enabled = false;
            gyroLights.Add(l);
        }

        // Cloches d'alarme (sons spatialisés)
        var clocheMat = Mat("Cloche", new Color(0.75f, 0.1f, 0.08f), 0.6f);
        var cloches = new List<Object>();
        var clocheClip = Clip("Alarme_Cloche");
        var clochePos = new[] { new Vector3(11f, 2.3f, 1.08f), new Vector3(19f, 2.3f, -1.08f), Dir(30f) * (HallR - 0.15f) + Vector3.up * 2.6f };
        var clocheAngle = new[] { 270f, 90f, 210f };
        for (int i = 0; i < clochePos.Length; i++)
        {
            var cl = Disc($"Cloche_{i}", sys, clochePos[i], 0.25f, 0.08f, clocheMat, false);
            cl.transform.localRotation = Quaternion.LookRotation(Dir(clocheAngle[i])) * Quaternion.Euler(90f, 0f, 0f);
            cloches.Add(Source(cl, clocheClip, true, false, 0.45f, 1f, 18f));
        }

        // Sirène de fin (dernière minute) : entendue partout
        var sireneGo = new GameObject("Sirene_FinProche");
        sireneGo.transform.SetParent(sys, false);
        var sirene = Source(sireneGo, Clip("Sirene_FinProche"), true, false, 0.35f, 0f, 50f);

        // Ventilation : un bruit de fond dans chaque zone
        var ventClip = Clip("Ambiance_Ventilation");
        var ventNoms = new[] { "Ventilation_Hall", "Ventilation_Corridor", "Ventilation_Labo" };
        var ventPos = new[] { new Vector3(0f, 3f, 0f), new Vector3(16f, 2.4f, 0f), LabOrigin + new Vector3(6f, 2.7f, 4.5f) };
        for (int i = 0; i < ventNoms.Length; i++)
        {
            var v = new GameObject(ventNoms[i]);
            v.transform.SetParent(sys, false);
            v.transform.localPosition = ventPos[i];
            Source(v, ventClip, true, true, 0.25f, 1f, 14f);
        }

        // Fuite de gaz à la hotte du labo
        var gazGo = new GameObject("Fuite_Gaz");
        gazGo.transform.SetParent(sys, false);
        gazGo.transform.localPosition = LabOrigin + new Vector3(11.2f, 1.25f, 3f);
        gazGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // le gaz sort vers l'ouest, dans le labo
        var ps = GasParticles(gazGo);
        var siff = Source(gazGo, Clip("Gaz_Sifflement"), true, false, 0.5f, 1f, 10f);
        var fuite = gazGo.AddComponent<FuiteGaz>();
        var soF = new SerializedObject(fuite);
        soF.FindProperty("gaz").objectReferenceValue = ps;
        soF.FindProperty("sifflement").objectReferenceValue = siff;
        soF.FindProperty("activeAuDepart").boolValue = false; // la fuite démarre à l'entrée du labo
        soF.ApplyModifiedPropertiesWithoutUndo();

        // Contrôleur d'alarme
        var alarmeGo = new GameObject("Alarme");
        alarmeGo.transform.SetParent(sys, false);
        var alarme = alarmeGo.AddComponent<AlarmeController>();
        var soA = new SerializedObject(alarme);
        SetArray(soA, "gyrophares", gyros);
        SetArray(soA, "lumieresGyrophares", gyroLights);
        SetArray(soA, "neons", lampesCreees);
        SetArray(soA, "cloches", cloches);
        soA.FindProperty("sirene").objectReferenceValue = sirene;
        soA.FindProperty("alerteAuDepart").boolValue = false; // pavillon calme au départ
        SetArray(soA, "neonsInquietude", lampesCorridor);
        soA.ApplyModifiedPropertiesWithoutUndo();

        // Histoire « Alerte au labo A-111 » : départ calme, tension dans le corridor, fuite à l'entrée du labo
        var seqGo = new GameObject("Sequence_Ambiance");
        seqGo.transform.SetParent(sys, false);
        var seq = seqGo.AddComponent<SequenceAmbiance>();

        var verrou = new GameObject("Son_Verrouillage_Entree");
        verrou.transform.SetParent(sys, false);
        verrou.transform.localPosition = new Vector3(0f, 1.2f, VestEnd - 0.3f);
        var sonVerrou = Source(verrou, Clip("Porte_Verrouillage"), false, false, 0.9f, 1f, 15f);

        var siffLoin = new GameObject("Sifflement_Derriere_Porte");
        siffLoin.transform.SetParent(sys, false);
        siffLoin.transform.localPosition = new Vector3(13.5f, 1.2f, 1.6f);
        var sonSiffLoin = Source(siffLoin, Clip("Gaz_Sifflement"), true, false, 0.25f, 1f, 7f);

        var annGo = new GameObject("Annonce_Confinement");
        annGo.transform.SetParent(sys, false);
        var sonAnnonce = Source(annGo, Clip("Annonce_Confinement"), false, false, 0.9f, 0f, 50f);

        var decoGo = new GameObject("Son_Decontamination");
        decoGo.transform.SetParent(sys, false);
        var sonDeco = Source(decoGo, Clip("Decontamination"), false, false, 0.8f, 0f, 50f);

        // Voile blanc de décontamination : sphère autour de la tête, cachée jusqu'à l'échec
        var voile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        voile.name = "Voile_Decontamination";
        voile.transform.SetParent(sys, false);
        voile.transform.localScale = Vector3.one * 0.6f;
        Object.DestroyImmediate(voile.GetComponent<Collider>());
        var voileR = voile.GetComponent<Renderer>();
        voileR.sharedMaterial = VoileMat();
        voileR.enabled = false;

        // Zones invisibles (en orange dans la vue Scene)
        var zoneCor = Zone("Declencheur_Corridor", sys, new Vector3(12.3f, 1.5f, 0f), new Vector3(2.6f, 3f, 2.4f));
        var zoneLab = Zone("Declencheur_Labo", sys, LabOrigin + new Vector3(6f, 1.5f, 4.6f), new Vector3(11.6f, 3f, 8.4f));
        UnityEventTools.AddPersistentListener(zoneCor.OnEntree, new UnityAction(seq.Inquietude));
        UnityEventTools.AddPersistentListener(zoneLab.OnEntree, new UnityAction(seq.EntreeLabo));
        EditorUtility.SetDirty(zoneCor);
        EditorUtility.SetDirty(zoneLab);

        var soS = new SerializedObject(seq);
        soS.FindProperty("alarme").objectReferenceValue = alarme;
        soS.FindProperty("fuite").objectReferenceValue = fuite;
        SetArray(soS, "zones", new List<Object> { zoneCor, zoneLab });
        soS.FindProperty("sonVerrouillage").objectReferenceValue = sonVerrou;
        soS.FindProperty("sifflementLointain").objectReferenceValue = sonSiffLoin;
        soS.FindProperty("annonce").objectReferenceValue = sonAnnonce;
        soS.FindProperty("voile").objectReferenceValue = voileR;
        soS.FindProperty("sonDecontamination").objectReferenceValue = sonDeco;
        soS.ApplyModifiedPropertiesWithoutUndo();

        // Outil de test (éditeur et builds de développement seulement) : touche O = ouvrir toutes les portes
        var outil = new GameObject("Outil_Test_Portes");
        outil.transform.SetParent(sys, false);
        outil.AddComponent<OutilTestPortes>();

        // Pour l'équipe : SequenceAmbiance.OnConfinement (départ du minuteur), SequenceAmbiance.Decontamination() (échec),
        // PorteSortie.OnOpened (victoire), AlarmeController.FinProche() / Victoire().
    }

    static DeclencheurZone Zone(string name, Transform parent, Vector3 pos, Vector3 taille)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var z = go.AddComponent<DeclencheurZone>();
        var so = new SerializedObject(z);
        so.FindProperty("taille").vector3Value = taille;
        so.ApplyModifiedPropertiesWithoutUndo();
        return z;
    }

    // Matériau URP Unlit transparent, visible des deux côtés (on est à l'intérieur de la sphère)
    static Material VoileMat()
    {
        string path = $"{MatFolder}/GB_Voile_Decontamination.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 0f);     // alpha
        m.SetFloat("_Cull", 0f);      // deux faces
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 4000;         // dessiné après tout le reste
        m.SetColor("_BaseColor", new Color(0.92f, 1f, 0.95f, 0f));
        EditorUtility.SetDirty(m);
        return m;
    }

    // Luminaire : rond (plafonnier encastré) ou rectangulaire, avec sa lumière précalculée
    static void Lampe(string name, Transform parent, Vector3 pos, bool rond, Vector3 taille, Material mat, Color couleur, float intensite)
    {
        GameObject visuel = rond ? Disc("Lampe_" + name, parent, pos, 0.45f, 0.02f, mat, false)
                                 : Box("Lampe_" + name, parent, pos, taille, mat, collider: false);
        lampesCreees.Add(visuel.GetComponent<Renderer>());
        var lgo = new GameObject("Lumiere_" + name);
        lgo.transform.SetParent(parent, false);
        lgo.transform.localPosition = pos + Vector3.down * 0.3f;
        lgo.isStatic = true;
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = couleur;
        l.range = 6.5f;
        l.intensity = intensite;
        l.shadows = LightShadows.Soft;          // ombres précalculées : gratuites pendant le jeu
        l.lightmapBakeType = LightmapBakeType.Baked;
    }

    static ParticleSystem GasParticles(GameObject go)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startColor = new Color(0.45f, 1f, 0.35f, 0.3f);
        main.gravityModifier = -0.02f;   // le gaz monte doucement
        main.maxParticles = 150;         // limité pour le Quest
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.rateOverTime = 30f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 25f;
        sh.radius = 0.3f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.5f, 1f, 0.4f), 0f), new GradientColorKey(new Color(0.3f, 0.8f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat();
        return ps;
    }

    static Material ParticleMat()
    {
        string path = $"{MatFolder}/GB_Gaz.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var urp = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        m = urp != null ? new Material(urp) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static AudioSource Source(GameObject go, AudioClip clip, bool loop, bool playOnAwake, float volume, float spatial, float maxDist)
    {
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = loop;
        a.playOnAwake = playOnAwake;
        a.volume = volume;
        a.spatialBlend = spatial;
        a.minDistance = 1f;
        a.maxDistance = maxDist;
        a.rolloffMode = AudioRolloffMode.Linear;
        return a;
    }

    static AudioClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{name}.wav");
        if (c == null) Debug.LogWarning($"Son introuvable : {AudioFolder}/{name}.wav");
        return c;
    }

    static void SetArray(SerializedObject so, string prop, List<Object> items)
    {
        var p = so.FindProperty(prop);
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static ZoneDoor PlaceDoor(string name, string prefabName, Transform parent, Vector3 localPos, Quaternion localRot)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab introuvable : {prefabName}. Lance Escape Lab > Créer les prefabs ZoneDoor.");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        return go.GetComponent<ZoneDoor>();
    }

    // ------------------------------------------------------------------ ÉCLAIRAGE
    static void BuildLighting(Transform root, UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "Lighting" && go.activeSelf)
            {
                Undo.RecordObject(go, "Désactiver l'éclairage du modèle");
                go.SetActive(false);
                Debug.Log("L'objet « Lighting » du modèle VR a été désactivé : le pavillon a son propre éclairage.");
            }

        var eclairage = Group("Eclairage", root, Vector3.zero);
        var soleilGo = new GameObject("Soleil");
        soleilGo.transform.SetParent(eclairage, false);
        soleilGo.transform.localRotation = Quaternion.Euler(35f, 200f, 0f); // vient du nord : fenêtres du labo, portes vitrées
        var soleil = soleilGo.AddComponent<Light>();
        soleil.type = LightType.Directional;
        soleil.color = new Color(1f, 0.95f, 0.85f);
        soleil.intensity = 1.2f;
        soleil.shadows = LightShadows.Soft;
        soleil.lightmapBakeType = LightmapBakeType.Baked;
        RenderSettings.sun = soleil;

        // Sondes de lumière : éclairent les objets qui bougent (mains, fioles, carte)
        var sondesGo = new GameObject("Sondes_Lumiere");
        sondesGo.transform.SetParent(eclairage, false);
        var pts = new List<Vector3>();
        foreach (float h in new[] { 0.5f, 1.7f })
        {
            foreach (float r in new[] { 4.5f, 7f })
                for (int i = 0; i < 8; i++) pts.Add(Dir(45f * i + 22.5f) * r + Vector3.up * h);
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { 9f, 11f }) pts.Add(new Vector3(x, h, z));
            for (float x = 10f; x <= 23.5f; x += 2.5f) pts.Add(new Vector3(x, h, 0f));
            for (float x = 1.5f; x <= 10.5f; x += 3f)
                for (float z = 1.5f; z <= 7.5f; z += 3f) pts.Add(LabOrigin + new Vector3(x, h, z));
        }
        sondesGo.AddComponent<LightProbeGroup>().probePositions = pts.ToArray();

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings")) AssetDatabase.CreateFolder("Assets/_Project", "Settings");
        const string lsPath = "Assets/_Project/Settings/Eclairage_Pavillon.lighting";
        var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
        if (ls == null)
        {
            ls = new LightingSettings();
            AssetDatabase.CreateAsset(ls, lsPath);
        }
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        ls.lightmapResolution = 8f;      // texels par mètre : léger pour le Quest
        ls.lightmapMaxSize = 1024;
        ls.directionalityMode = LightmapsMode.NonDirectional;
        ls.lightmapCompression = LightmapCompression.NormalQuality;
        ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
        ls.ao = true;
        EditorUtility.SetDirty(ls);
        Lightmapping.lightingSettings = ls;
    }

    [MenuItem("Escape Lab/Calculer la lumière (bake)")]
    public static void Bake()
    {
        if (GameObject.Find("Greybox_Pavillon") == null)
        {
            EditorUtility.DisplayDialog("Lumière", "Construis d'abord la greybox du pavillon.", "OK");
            return;
        }
        Lightmapping.BakeAsync();
        Debug.Log("Calcul de la lumière lancé (barre de progression en bas à droite). Pense à Ctrl+S à la fin.");
    }

    // ------------------------------------------------------------------ OUTILS
    static Vector3 Dir(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    static Transform Group(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static Transform Point(string name, Transform parent, Vector3 localPos, float yRotation = 0f)
    {
        var t = Group(name, parent, localPos);
        t.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        return t;
    }

    // Boîte avec des coordonnées de texture en mètres : les textures gardent leur taille réelle partout
    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat,
                          bool collider = true, bool isStatic = true, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        go.isStatic = isStatic;
        return go;
    }

    // Petit cylindre (lampe ronde, gicleur, poubelle…). Éclairé par les sondes de lumière.
    static GameObject Disc(string name, Transform parent, Vector3 localPos, float diameter, float height, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = false;
        return go;
    }

    // Anneau (ou disque si rIn = 0) : sols et plafonds ronds, avec le trou du puits ou de l'ouverture
    static GameObject Ring(string name, Transform parent, float yCenter, float rIn, float rOut, float h, Material mat, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, yCenter, 0f);
        var mesh = RingMesh(rIn, rOut, h, 64);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true;
        return go;
    }

    // Mur droit le long de l'axe X (à z fixe), avec ouvertures (x = centre, y = largeur, z = hauteur) et plinthes des 2 côtés
    static void WallAlongX(string name, Transform parent, float z, float xFrom, float xTo, float h,
                           Material mat, params Vector3[] doors)
    {
        var g = Group(name, parent, Vector3.zero);
        float cur = xFrom;
        int k = 0;
        foreach (var d in doors)
        {
            float a = d.x - d.y / 2f, b = d.x + d.y / 2f;
            k = MurPlein(g, name, k, cur, a, z, h, mat);
            Box($"{name}_Linteau_{k++}", g, new Vector3(d.x, (d.z + h) / 2f, z), new Vector3(d.y, h - d.z, 0.2f), mat);
            cur = b;
        }
        MurPlein(g, name, k, cur, xTo, z, h, mat);
    }

    // Morceau de mur plein entre xa et xb, avec une plinthe de chaque côté
    static int MurPlein(Transform g, string name, int k, float xa, float xb, float z, float h, Material mat)
    {
        if (xb - xa < 0.01f) return k;
        Box($"{name}_{k++}", g, new Vector3((xa + xb) / 2f, h / 2f, z), new Vector3(xb - xa, h, 0.2f), mat);
        foreach (float s in new[] { 1f, -1f })
            Box($"Plinthe_{k++}", g, new Vector3((xa + xb) / 2f, 0.05f, z + s * 0.11f), new Vector3(xb - xa, 0.1f, 0.02f), Plinthe, collider: false);
        return k;
    }

    static void ColliderOnly(string name, Transform parent, Vector3 localPos, Vector3 size, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        go.AddComponent<BoxCollider>().size = size;
        go.isStatic = true;
    }

    // Texte 3D net (TextMeshPro), avec un fond facultatif. Lisible quand on regarde dans la direction « rot ».
    static void Sign(string name, Transform parent, string text, Vector3 pos, Quaternion rot, Vector2 size, Color color, Material fond)
    {
        if (fond != null)
            Box(name + "_Fond", parent, pos, new Vector3(size.x + 0.05f, size.y + 0.05f, 0.012f), fond, collider: false, rot: rot);
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos + rot * new Vector3(0f, 0f, -0.011f);
        go.transform.localRotation = rot;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = 20f;
        t.rectTransform.sizeDelta = size;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Greybox");
    }

    // Matériau URP (réutilisé s'il existe déjà). tex = nom d'une texture du dossier Environnement, tile = sa taille réelle en mètres.
    static Material Mat(string name, Color color, float smoothness = 0.2f, string tex = null, float tile = 1f)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/GB_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (tex != null)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{tex}.png");
            if (t == null) Debug.LogWarning($"Texture introuvable : {TexFolder}/{tex}.png");
            m.mainTexture = t;
            m.mainTextureScale = new Vector2(1f / tile, 1f / tile);
        }
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material EmissiveMat(string name, Color color, float intensity)
    {
        var m = Mat(name, color, 0.5f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; // la lumière vient des lampes précalculées
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ MAILLAGES (coordonnées de texture en mètres)
    class MeshData
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        // Ajoute un quadrilatère ; l'ordre des sommets est corrigé pour que la face regarde vers « normal »
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.sqrMagnitude < 1e-10f) cr = Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(cr, normal) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            Unwrapping.GenerateSecondaryUVSet(m); // coordonnées pour la lumière précalculée
            return m;
        }
    }

    static Mesh BoxMesh(Vector3 s)
    {
        string key = $"B{s.x:0.000}_{s.y:0.000}_{s.z:0.000}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        float x = s.x / 2f, y = s.y / 2f, z = s.z / 2f;
        // +X / -X : texture (z, y)
        md.Quad(new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z), Vector3.right,
                new Vector2(0, 0), new Vector2(0, s.y), new Vector2(s.z, s.y), new Vector2(s.z, 0));
        md.Quad(new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z), new Vector3(-x, -y, -z), Vector3.left,
                new Vector2(0, 0), new Vector2(0, s.y), new Vector2(s.z, s.y), new Vector2(s.z, 0));
        // +Y / -Y : texture (x, z)
        md.Quad(new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z), Vector3.up,
                new Vector2(0, 0), new Vector2(0, s.z), new Vector2(s.x, s.z), new Vector2(s.x, 0));
        md.Quad(new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), Vector3.down,
                new Vector2(0, s.z), new Vector2(0, 0), new Vector2(s.x, 0), new Vector2(s.x, s.z));
        // +Z / -Z : texture (x, y)
        md.Quad(new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z), new Vector3(-x, -y, z), Vector3.forward,
                new Vector2(0, 0), new Vector2(0, s.y), new Vector2(s.x, s.y), new Vector2(s.x, 0));
        md.Quad(new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, -y, -z), Vector3.back,
                new Vector2(0, 0), new Vector2(0, s.y), new Vector2(s.x, s.y), new Vector2(s.x, 0));
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
    }

    static Vector2 UV(Vector3 p) => new Vector2(p.x, p.z);

    static Mesh RingMesh(float rIn, float rOut, float h, int seg)
    {
        string key = $"R{rIn:0.00}_{rOut:0.00}_{h:0.000}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        float y = h / 2f;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * 2f * Mathf.PI / seg, a1 = (i + 1) * 2f * Mathf.PI / seg;
            var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 o0 = d0 * rOut, o1 = d1 * rOut, i0 = d0 * rIn, i1 = d1 * rIn;
            // Dessus et dessous
            md.Quad(o0 + Vector3.up * y, i0 + Vector3.up * y, i1 + Vector3.up * y, o1 + Vector3.up * y, Vector3.up, UV(o0), UV(i0), UV(i1), UV(o1));
            md.Quad(o0 - Vector3.up * y, o1 - Vector3.up * y, i1 - Vector3.up * y, i0 - Vector3.up * y, Vector3.down, UV(o0), UV(o1), UV(i1), UV(i0));
            // Bord extérieur
            float s0 = a0 * rOut, s1 = a1 * rOut;
            md.Quad(o0 - Vector3.up * y, o0 + Vector3.up * y, o1 + Vector3.up * y, o1 - Vector3.up * y, (d0 + d1).normalized,
                    new Vector2(s0, 0), new Vector2(s0, h), new Vector2(s1, h), new Vector2(s1, 0));
            // Bord intérieur (puits / ouverture)
            if (rIn > 0.01f)
            {
                float t0 = a0 * rIn, t1 = a1 * rIn;
                md.Quad(i0 - Vector3.up * y, i1 - Vector3.up * y, i1 + Vector3.up * y, i0 + Vector3.up * y, -(d0 + d1).normalized,
                        new Vector2(t0, 0), new Vector2(t1, 0), new Vector2(t1, h), new Vector2(t0, h));
            }
        }
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
    }
}
