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
    // Verre vraiment transparent (on voit dehors par les fenêtres et l'intérieur des vitrines)
    static Material VerreArme => GlassMat("Verre_Arme", new Color(0.55f, 0.60f, 0.62f), 0.45f);
    static Material Verre => GlassMat("Verre", new Color(0.80f, 0.90f, 0.95f), 0.18f);
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
        RailRound(Group("Rampe_Puits", hall, Vector3.zero), Y_RDC, PuitsR, true, -1, SegmentsPassage()); // ouverture vers l'escalier du sous-sol

        // Ruban de sécurité sur la rampe, face à l'entrée, et panneau d'avertissement
        var conf = Group("Confinement", hall, Vector3.zero);
        float LR = Chord(PuitsR);
        for (int i = 3; i <= 9; i++)
        {
            if (SegmentsPassage().Contains(i)) continue;
            Vector3 dir = Dir(i * 15f);
            Box($"Ruban_{i}", conf, dir * (PuitsR + 0.06f) + Vector3.up * 0.75f, new Vector3(LR, 0.07f, 0.01f), Ruban,
                collider: false, rot: Quaternion.LookRotation(-dir));
        }
        Vector3 pPanneau = Dir(128f) * (PuitsR + 0.45f);
        var rotPanneau = Quaternion.LookRotation(Vector3.back); // lisible depuis l'entrée (on regarde vers le sud)
        Box("Panneau_Pied", conf, pPanneau + Vector3.up * 0.5f, new Vector3(0.05f, 1f, 0.05f), Metal, collider: false);
        Sign("Panneau_Confinement", conf, "ACCÈS INTERDIT\n<size=60%>PAVILLON FERMÉ</size>", pPanneau + Vector3.up * 1.2f,
             rotPanneau, new Vector2(0.7f, 0.45f), Color.black, Jaune);

        // Comptoirs de chaque côté de la porte A (le tiroir contient la carte d'accès)
        // (Les comptoirs sont maintenant de chaque côté des 4 portes : voir ComptoirsPorte.)

        // Babillard (indice : code du tiroir) et plan d'évacuation (indice : A-111)
        Vector3 dB = Dir(135f);
        Babillard("Babillard", hall, dB * (HallR - 0.12f) + Vector3.up * 1.5f, Quaternion.LookRotation(dB), 1.6f, 1.0f);
        Vector3 dP = Dir(45f);
        // Plan d'évacuation (grand format, cadre en aluminium) : vrai plan du rez-de-chaussée du pavillon
        Box("Plan_Evacuation_Cadre", hall, dP * (HallR - 0.11f) + Vector3.up * 1.5f, new Vector3(1.04f, 0.8f, 0.02f),
            Mat("Aluminium", new Color(0.80f, 0.81f, 0.82f), 0.7f), collider: false, rot: Quaternion.LookRotation(dP));
        Box("Plan_Evacuation", hall, dP * (HallR - 0.125f) + Vector3.up * 1.5f, new Vector3(0.98f, 0.735f, 0.01f),
            TexMat("Plan_Evacuation", 0.98f, 0.735f), collider: false, rot: Quaternion.LookRotation(dP));

        // Mobilier : fauteuils rouges, bacs de recyclage, fontaine d'eau (comme dans le vrai hall)
        var mob = Group("Mobilier", hall, Vector3.zero);
        Fauteuil("Fauteuil_1", mob, 258f, 8.15f);
        Fauteuil("Fauteuil_2", mob, 282f, 8.15f);
        // Bacs de tri (comme sur les photos : deux bleus et un vert)
        var bacs = new (string nom, Color c, string txt, bool fente)[] {
            ("Bac_Recyclage", new Color(0.10f, 0.35f, 0.75f), "RECYCLAGE\n<size=65%>bouteilles · canettes</size>", false),
            ("Bac_Papier", new Color(0.10f, 0.35f, 0.75f), "PAPIER\n<size=65%>et carton</size>", true),
            ("Bac_Compost", new Color(0.12f, 0.55f, 0.25f), "COMPOST\n<size=65%>restes de table</size>", false) };
        for (int i = 0; i < bacs.Length; i++)
        {
            Vector3 d = Dir(147f + i * 3.2f);
            PoubelleTri(mob, bacs[i].nom, d * 8.66f, Quaternion.LookRotation(-d), bacs[i].c, bacs[i].txt, bacs[i].fente); // contre le mur
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
        Point("Point_Tiroir_Comptoir", pts, new Vector3(8.25f, 0.85f, -1.45f)); // comptoir à droite de la porte A
        Point("Point_Indice_Babillard", pts, dB * (HallR - 0.2f) + Vector3.up * 1.5f);
        Point("Point_LecteurCarte_PorteA", pts, Dir(350f) * (HallR - 0.14f) + Vector3.up * 1.2f);

        // Plafond du hall (plâtre lisse) avec l'ouverture vers le 2e étage
        Ring("Plafond_Hall", plafonds, HallH + 0.025f, OculusR, HallR + 0.05f, 0.05f, Platre, false);
        // Bandeau rond sous le balcon du 2e étage, avec l'enseigne en lettres détachées côté entrée (comme sur la photo)
        Ring("Bandeau_Oculus", hall, HallH - 0.3f, OculusR, OculusR + 0.2f, 0.6f, Platre, false);
        TexteArc(hall, "BIENVENUE À LA FACULTÉ DES SCIENCES", OculusR + 0.212f, 90f, HallH - 0.3f, 0.3f, new Color(0.08f, 0.08f, 0.08f));
        // Gicleurs et détecteurs de fumée du hall
        Gicleurs(plafonds, HallH, new[] { 5.6f, 7.9f }, 12, 15f);
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
        // Façade d'entrée en aluminium : 4 portes vitrées (verrouillées) avec imposte au-dessus, comme sur les photos
        var alu = Mat("Aluminium_Anodise", new Color(0.55f, 0.56f, 0.57f), 0.6f);
        for (int i = 0; i <= 4; i++)
            Box($"Meneau_{i}", vest, new Vector3(-glassW / 2f + 1.5f * i, 1.25f, VestEnd), new Vector3(0.1f, 2.5f, 0.16f), alu);
        Box("Traverse_Imposte", vest, new Vector3(0f, 2.2f, VestEnd), new Vector3(glassW, 0.1f, 0.16f), alu, collider: false);
        Box("Traverse_Tete", vest, new Vector3(0f, 2.47f, VestEnd), new Vector3(glassW + 0.1f, 0.06f, 0.2f), alu, collider: false);
        // Cadre périphérique : la façade s'emboîte exactement dans l'ouverture du mur (aucun jour)
        foreach (float sx in new[] { -1f, 1f })
            Box(sx < 0 ? "Cadre_Facade_G" : "Cadre_Facade_D", vest, new Vector3(sx * (glassW / 2f + 0.02f), 1.25f, VestEnd), new Vector3(0.06f, 2.5f, 0.22f), alu, collider: false);
        Box("Seuil_Entree", vest, new Vector3(0f, 0.008f, VestEnd), new Vector3(glassW, 0.016f, 0.3f), alu, collider: false);
        for (int i = 0; i < 4; i++)
        {
            float x = -glassW / 2f + 0.75f + 1.5f * i;
            var p = Group($"Porte_Entree_{i + 1}", vest, new Vector3(x, 0f, VestEnd));
            const float lw = 1.396f, lh = 2.15f;
            // Vitrage pleine hauteur (bloque la sortie) + imposte vitrée
            Box("Vitre", p, new Vector3(0f, lh / 2f, 0f), new Vector3(lw - 0.1f, lh - 0.1f, 0.012f), Verre);
            Box("Imposte", p, new Vector3(0f, 2.345f, 0f), new Vector3(1.4f, 0.19f, 0.012f), Verre, collider: false);
            Box("Joint_Bas", p, new Vector3(0f, 0.004f, 0f), new Vector3(lw, 0.008f, 0.05f), Noir, collider: false);
            // Cadre du battant : montants, traverse haute, traverse basse large, traverse du milieu
            Box("Montant_G", p, new Vector3(-lw / 2f + 0.05f, lh / 2f, 0f), new Vector3(0.1f, lh, 0.05f), alu, collider: false);
            Box("Montant_D", p, new Vector3( lw / 2f - 0.05f, lh / 2f, 0f), new Vector3(0.1f, lh, 0.05f), alu, collider: false);
            Box("Traverse_Haut", p, new Vector3(0f, lh - 0.05f, 0f), new Vector3(lw, 0.1f, 0.05f), alu, collider: false);
            Box("Traverse_Bas", p, new Vector3(0f, 0.14f, 0f), new Vector3(lw, 0.26f, 0.05f), alu, collider: false);
            // Côté intérieur : barre anti-panique ; côté extérieur : poignée de tirage verticale
            Box("Barre_Panique", p, new Vector3(0f, 1.0f, -0.07f), new Vector3(lw * 0.75f, 0.05f, 0.05f), Metal, collider: false);
            Box("Barre_Support_G", p, new Vector3(-lw * 0.36f, 1.0f, -0.045f), new Vector3(0.06f, 0.12f, 0.04f), Metal, collider: false);
            Box("Barre_Support_D", p, new Vector3( lw * 0.36f, 1.0f, -0.045f), new Vector3(0.06f, 0.12f, 0.04f), Metal, collider: false);
            float cote = i % 2 == 0 ? 1f : -1f; // les portes s'ouvrent par paires
            Box("Poignee_Exterieure", p, new Vector3(cote * (lw / 2f - 0.12f), 1.05f, 0.08f), new Vector3(0.03f, 0.6f, 0.03f), Metal, collider: false);
            Box("Ferme_Porte", p, new Vector3(-cote * (lw / 2f - 0.25f), lh - 0.12f, -0.06f), new Vector3(0.32f, 0.07f, 0.06f), alu, collider: false);
            Box("Serrure", p, new Vector3(cote * (lw / 2f - 0.05f), 1.0f, 0.03f), new Vector3(0.03f, 0.06f, 0.01f), Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), collider: false);
            Sign("Autocollant", p, "POUSSEZ", new Vector3(0f, 1.25f, -0.01f), Quaternion.LookRotation(Vector3.forward),
                 new Vector2(0.3f, 0.07f), new Color(0.9f, 0.9f, 0.9f), null);
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
        Gicleurs(etage, Y_Etage + HallH, new[] { 3.0f, 5.6f, 7.9f }, 12, 0f);
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

    // ------------------------------------------------------------------ ESCALIERS (comme dans le vrai hall, photos 2 et 3)
    // 1. Escalier principal : grande volée courbe en bois, dessous plein en acier noir, garde-corps noirs à barreaux serrés
    //    des deux côtés. Elle part du rez-de-chaussée À CÔTÉ du puits (4 premières marches un peu plus larges, au bout arrondi),
    //    passe au-dessus du bord du puits en montant, et arrive sur un palier en avancée du balcon vitré du 2e étage.
    //    Son centre de courbure est décalé du centre du hall (c'est ce qui donne la forme des photos).
    //    Fermé en bas (poteaux, ruban, panneau) et en haut (grille cadenassée sur le palier).
    // 2. Escalier du sous-sol : part juste à côté du pied de l'escalier principal et descend dans le puits en tournant
    //    dans le même sens : 2e étage -> RDC -> sous-sol, comme dans le vrai hall.
    const float EscRIn = 2.75f, EscROut = 3.95f, EscDepart = 65f, EscPas = -8.5f;
    const int EscN = 21, EscEvase = 4;
    static readonly Vector3 EscCentre = new Vector3(0.106f, 0f, 0.227f);   // 0,25 m vers le départ : le pied de l'escalier part du bord du puits

    static float Norm360(float a) => ((a % 360f) + 360f) % 360f;
    static Vector3 EscPoint(float a, float r) => EscCentre + Dir(a) * r;
    static float AngleHall(Vector3 p) => Norm360(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg);
    static float AngleHautEscalier() => AngleHall(EscPoint(EscDepart + EscPas * (EscN - 1), (EscRIn + EscROut) / 2f));
    // Escalier du sous-sol : dans le puits, il continue dans le même sens de rotation que l'escalier principal
    const float SsRIn = 1.75f, SsROut = 2.85f, SsPas = -7f, SsHaut = 101.6f;   // même sens de rotation que l'escalier principal
    const int SsN = 20;
    static float SsHautMarche() => (Y_RDC - Y_SousSol) / SsN;
    static float AngleArrivee() => 90f;                                    // marche d'arrivée = segment 6 de la rampe
    // Rampe du puits ouverte là où l'escalier principal part du bord du puits (segments 0 à 5) et vers le sous-sol (6)
    static HashSet<int> SegmentsPassage() => new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 };   // 0 et 1 : sous la volée qui passe au-dessus du bord

    static void BuildEscalier(Transform root)
    {
        var esc = Group("Escaliers", root, Vector3.zero);
        var bois = Mat("Marche_Bois", new Color(0.46f, 0.30f, 0.18f), 0.5f, "Bois", 1f);
        var acier = Mat("Acier_Noir", new Color(0.07f, 0.07f, 0.08f), 0.45f);
        var rampe = Mat("Rampe_Noire", new Color(0.12f, 0.09f, 0.08f), 0.55f);

        // 1. Escalier principal RDC -> 2e étage
        var p = Group("Escalier_Principal", esc, Vector3.zero);
        Volee(p, EscCentre, EscRIn, EscROut, EscDepart, EscPas, EscN, Y_RDC, Y_Etage - Y_RDC, bois, acier, rampe, EscEvase, true);

        // Palier d'arrivée en avancée du balcon (dalle, dessous noir, garde-corps vitrés + main courante)
        float aTop = EscDepart + EscPas * (EscN - 1);
        float aHall = AngleHautEscalier();
        Vector3 dH = Dir(aHall);
        var rotH = Quaternion.LookRotation(dH);
        float r0 = (EscPoint(aTop, (EscRIn + EscROut) / 2f)).magnitude - 0.5f, r1 = OculusR + 0.1f;
        var pal = Group("Palier_Balcon", p, Vector3.zero);
        Box("Dalle", pal, dH * ((r0 + r1) / 2f) + Vector3.up * (Y_Etage - 0.15f), new Vector3(1.7f, 0.3f, r1 - r0), Terrazzo, rot: rotH);
        Box("Dessous", pal, dH * ((r0 + r1) / 2f) + Vector3.up * (Y_Etage - 0.31f), new Vector3(1.72f, 0.02f, r1 - r0 + 0.02f), acier, collider: false, rot: rotH);
        Vector3 t = Vector3.Cross(Vector3.up, dH);
        foreach (float sx in new[] { -1f, 1f })
        {
            Vector3 bord = dH * ((r0 + r1) / 2f) + t * (sx * 0.83f) + Vector3.up * Y_Etage;
            Box("Vitre_Palier", pal, bord + Vector3.up * 0.5f, new Vector3(0.02f, 0.95f, r1 - r0), Verre, rot: rotH);
            Box("Main_Courante_Palier", pal, bord + Vector3.up * 1.02f, new Vector3(0.06f, 0.05f, r1 - r0), rampe, collider: false, rot: rotH);
        }
        Grille(p, "Grille_Haut", dH * (OculusR - 0.05f) + Vector3.up * Y_Etage, Quaternion.LookRotation(-dH), 1.6f);
        ColliderOnly("Blocage_Escalier_Haut", p, dH * (OculusR - 0.05f) + Vector3.up * (Y_Etage + 1.25f), new Vector3(1.8f, 2.5f, 0.15f), rotH);

        // Fermé en bas : deux poteaux à ruban posés sur la 2e marche, panneau, mur invisible (on peut monter 2 marches)
        float aBas = EscDepart + EscPas - 2f;
        float yP = Y_RDC + 2f * (Y_Etage - Y_RDC) / EscN;
        Vector3 pA = EscPoint(aBas, EscRIn + 0.1f) + Vector3.up * yP, pB = EscPoint(aBas, EscROut + (EscEvase - 1) * 0.08f - 0.1f) + Vector3.up * yP;
        var poteauMat = Mat("Poteau_Chrome", new Color(0.75f, 0.76f, 0.78f), 0.9f);
        Vector3[] poteaux = { pA, pB };
        for (int k = 0; k < 2; k++)
        {
            Disc($"Poteau_{k}_Base", p, poteaux[k] + Vector3.up * 0.015f, 0.3f, 0.03f, Noir, false);
            Disc($"Poteau_{k}", p, poteaux[k] + Vector3.up * 0.48f, 0.06f, 0.95f, poteauMat, false);
        }
        Vector3 ab = pB - pA;
        var rotRuban = Quaternion.LookRotation(Vector3.Cross(ab, Vector3.up));
        Box("Ruban_Escalier", p, (pA + pB) / 2f + Vector3.up * 0.88f, new Vector3(ab.magnitude, 0.05f, 0.005f),
            Mat("Ruban_Rouge", new Color(0.75f, 0.08f, 0.08f), 0.4f), collider: false, rot: rotRuban);
        Box("Panneau_Pied", p, (pA + pB) / 2f + Vector3.up * 0.55f, new Vector3(0.04f, 1.1f, 0.04f), Metal, collider: false);
        Vector3 versEsc = Vector3.Cross(Vector3.up, Dir(aBas));
        Sign("Panneau_Escalier_Ferme", p, "ESCALIER FERMÉ\n<size=60%>Accès interdit pendant le confinement</size>",
             (pA + pB) / 2f + Vector3.up * 1.25f, Quaternion.LookRotation(versEsc), new Vector2(0.75f, 0.3f), Color.black, Jaune);
        Vector3 qA = EscPoint(aBas, EscRIn - 0.6f), qB = EscPoint(aBas, EscROut + EscEvase * 0.08f + 0.4f);
        ColliderOnly("Blocage_Escalier_Bas", p, (qA + qB) / 2f + Vector3.up * 1.4f, new Vector3((qB - qA).magnitude, 2.8f, 0.15f), rotRuban);

        // 2. Suite vers le sous-sol : elle part du rez-de-chaussée JUSTE À CÔTÉ du pied de l'escalier principal,
        //    dans le puits, et continue de descendre en tournant dans le même sens.
        //    Pas de palier séparé : la marche d'arrivée est au niveau du plancher, par l'ouverture de la rampe.
        var s = Group("Escalier_Sous_Sol", esc, Vector3.zero);
        float aDepartSs = SsHaut - SsPas * (SsN - 1);
        Volee(s, Vector3.zero, SsRIn, SsROut, aDepartSs, SsPas, SsN, Y_SousSol, Y_RDC - Y_SousSol, bois, acier, rampe, 2, false);
        float aA = AngleArrivee();
        var arr = MeshGO("Marche_Arrivee", s, WedgeMesh(SsRIn, PuitsR + 0.05f, 7.5f, 0.06f), bois, Vector3.up * Y_RDC, Quaternion.LookRotation(Dir(aA)));
        arr.AddComponent<MeshCollider>().sharedMesh = arr.GetComponent<MeshFilter>().sharedMesh;
        MeshGO("Dessous_Arrivee", s, WedgeMesh(SsRIn, PuitsR + 0.05f, 7.5f, 0.02f), acier, Vector3.up * (Y_RDC - 0.3f), Quaternion.LookRotation(Dir(aA)));
        // Garde-corps reliés entre eux (même main courante, poteaux aux jonctions, mur invisible partout) :
        //  a) bord du vide intérieur : de la jonction (82,5°) jusqu'à la marche du haut du sous-sol, où il continue sur la volée ;
        //  b) côté de la marche d'arrivée, du vide intérieur jusqu'au bord du puits ;
        //  c) bord du puits jusqu'au pied de l'escalier principal, puis sur sa 1re marche jusqu'à son poteau de départ.
        var gc = Group("Garde_Corps_Arrivee", s, Vector3.zero);
        float rA = SsRIn + 0.04f;
        var pa = new List<Vector3>(); var ya = new List<float>();
        for (float a = aA - 7.5f; a < SsHaut - 0.01f; a += 3.2f) { pa.Add(Dir(a) * rA); ya.Add(0.95f); }
        pa.Add(Dir(SsHaut) * rA); ya.Add(0.95f);
        GardeCorpsSol(gc, "Bord_Vide", pa, ya, rampe, acier);
        GardeCorpsSol(gc, "Cote", new List<Vector3> { Dir(aA - 7.5f) * rA, Dir(aA - 7.5f) * PuitsR }, new List<float> { 0.95f, 1.13f }, rampe, acier);
        Poteau(gc, Dir(aA - 7.5f) * rA, 0.95f, acier);

        // Bord du puits jusqu'au pied de l'escalier principal : la main courante arrive pile sur son poteau de départ
        float y0 = Y_RDC + (Y_Etage - Y_RDC) / EscN;          // dessus de la 1re marche
        float rEsc = EscRIn + 0.04f;
        float aCoin = AngleHall(EscPoint(EscDepart + 7.9f, EscRIn));   // coin arrière de la 1re marche, sur le bord du puits
        var pp = new List<Vector3>(); var yp = new List<float>();
        for (float a = aA - 7.5f; a > aCoin + 0.5f; a -= 3.5f) { pp.Add(Dir(a) * PuitsR); yp.Add(1.13f); }
        pp.Add(Dir(aCoin) * PuitsR); yp.Add(1.13f);
        GardeCorpsSol(gc, "Bord_Puits", pp, yp, rampe, acier);
        Poteau(gc, Dir(aA - 7.5f) * PuitsR, 1.13f, acier);
        Poteau(gc, Dir(aCoin) * PuitsR, 1.13f, acier);
        var pm = new List<Vector3> { Dir(aCoin) * PuitsR + Vector3.up * y0, EscPoint(EscDepart + 4f, rEsc) + Vector3.up * y0, EscPoint(EscDepart, rEsc) + Vector3.up * y0 };
        GardeCorpsSol(gc, "Premiere_Marche", pm, new List<float> { 1.13f, y0 + 0.95f, y0 + 0.95f }, rampe, acier);

        // Rampe ronde du rez-de-chaussée : poteaux à ses deux bouts. Côté escalier principal, le poteau monte jusque
        // sous la volée, qui passe au-dessus du bord du puits (la rampe s'arrête là où l'escalier la recouvre).
        float aFin = 7f * 15f - 7.5f, aDeb = 360f - 7.5f;
        Poteau(gc, Dir(aFin) * PuitsR, 1.08f, Rampe);
        float iF = (EscDepart - aDeb + 360f) / -EscPas;                // position (en marches) de la volée au-dessus
        float ySous = (iF + 1f) * (Y_Etage - Y_RDC) / EscN - (Y_Etage - Y_RDC) / EscN * 0.5f - 0.34f;
        Box("Poteau_Sous_Volee", gc, Dir(aDeb) * PuitsR + Vector3.up * (ySous / 2f), new Vector3(0.07f, ySous, 0.07f), Rampe, collider: false);

        // Portillon cadenassé juste sous la marche du haut : on voit l'escalier descendre, on ne passe pas (confinement)
        float aG = SsHaut + (Mathf.Abs(SsPas) / 2f + 0.6f) + 1.5f;
        Vector3 dG = Dir(aG);
        Vector3 versDescente = -Vector3.Cross(Vector3.up, dG);        // vers les angles croissants (on descend)
        Grille(s, "Portillon_Sous_Sol", dG * ((SsRIn + SsROut) / 2f) + Vector3.up * (Y_RDC - SsHautMarche()), Quaternion.LookRotation(versDescente), SsROut - SsRIn);
        ColliderOnly("Blocage_Portillon", s, dG * ((SsRIn + SsROut) / 2f) + Vector3.up * 1.1f, new Vector3(SsROut - SsRIn + 0.1f, 2.5f, 0.15f), Quaternion.LookRotation(versDescente));

    }

    // Volée courbe autour de « centre ». pas = degrés par marche en montant (négatif = sens horaire vu de dessus).
    // evase = nombre de premières marches larges, pleines jusqu'au sol, au bout arrondi.
    static void Volee(Transform g, Vector3 centre, float rIn, float rOut, float aDepart, float pas, int n, float yBas, float hauteur,
                      Material bois, Material acier, Material rampe, int evase, bool bloquerDessous)
    {
        float h = hauteur / n;
        float demi = Mathf.Abs(pas) / 2f + 0.6f;
        var marches = Group("Marches", g, centre);
        var dessous = Group("Dessous", g, centre);
        var garde = Group("Garde_Corps", g, centre);
        var rails = new[] { new List<Vector3>(), new List<Vector3>() };     // 0 = côté intérieur, 1 = côté extérieur
        var bases = new[] { new List<Vector3>(), new List<Vector3>() };
        var hauts = new[] { new List<float>(), new List<float>() };
        for (int i = 0; i < n; i++)
        {
            float a = aDepart + pas * i;
            float y = yBas + h * (i + 1);
            Vector3 d = Dir(a);
            bool large = i < evase;
            float ro = rOut + (large ? (evase - i) * 0.08f : 0f);
            if (large)
            {
                // Marche pleine jusqu'au sol, prolongée vers l'arrière, avec un bout arrondi (comme sur la photo 3)
                float dmArr = demi + (evase - i) * 0.8f;
                float ac = a + Mathf.Sign(-pas) * (dmArr - demi) / 2f;
                var mg = MeshGO($"Marche_{i:00}", marches, WedgeMesh(rIn, ro, (dmArr + demi) / 2f, y - yBas), bois, Vector3.up * y, Quaternion.LookRotation(Dir(ac)));
                mg.AddComponent<MeshCollider>().sharedMesh = mg.GetComponent<MeshFilter>().sharedMesh;
                float aBout = ac + Mathf.Sign(-pas) * (dmArr + demi) / 2f;
                Disc($"Bout_Arrondi_{i:00}", marches, Dir(aBout) * (ro - 0.16f) + Vector3.up * (yBas + (y - yBas) / 2f),
                     0.32f, y - yBas, bois, false);
            }
            else
            {
                MeshGO($"Marche_{i:00}", marches, WedgeMesh(rIn, ro, demi, 0.06f), bois, Vector3.up * y, Quaternion.LookRotation(d));
                // Contremarche sombre au bord bas de la marche
                Vector3 dC = Dir(a - pas / 2f);
                Box($"Contremarche_{i:00}", marches, dC * ((rIn + ro) / 2f) + Vector3.up * (y - 0.06f - (h - 0.06f) / 2f),
                    new Vector3(0.015f, h - 0.06f, ro - rIn), acier, collider: false, rot: Quaternion.LookRotation(dC));
                // Dessous plein en acier noir : plaque inclinée + limons épais des deux côtés
                Vector3 m0 = Dir(a - pas / 2f) * ((rIn + rOut) / 2f) + Vector3.up * (y - h * 0.5f - 0.32f);
                Vector3 m1 = Dir(a + pas / 2f) * ((rIn + rOut) / 2f) + Vector3.up * (y + h * 0.5f - 0.32f);
                Box($"Sous_Face_{i:00}", dessous, (m0 + m1) / 2f, new Vector3(rOut - rIn + 0.04f, 0.04f, (m1 - m0).magnitude + 0.05f), acier,
                    collider: false, rot: Quaternion.LookRotation(m1 - m0, Vector3.up));
            }
            foreach (float r in new[] { rIn - 0.02f, rOut + 0.02f })
            {
                Vector3 q0 = Dir(a - pas / 2f) * r + Vector3.up * (y - h * 0.5f - 0.2f);
                Vector3 q1 = Dir(a + pas / 2f) * r + Vector3.up * (y + h * 0.5f - 0.2f);
                if (large && r > rIn) continue;
                Box($"Limon_{i:00}", dessous, (q0 + q1) / 2f, new Vector3(0.03f, 0.48f, (q1 - q0).magnitude + 0.03f), acier,
                    collider: false, rot: Quaternion.LookRotation(q1 - q0, Vector3.up));
            }
            // Garde-corps des deux côtés (côté intérieur dès la 1re marche) : 3 barreaux par marche qui montent
            // exactement jusqu'à la main courante, une main courante ronde continue, et un mur invisible pour ne pas tomber.
            for (int c = 0; c < 2; c++)
            {
                if (c == 1 && i < evase - 1) continue;
                int premiere = c == 0 ? 0 : evase - 1;
                float r = c == 0 ? rIn + 0.04f : rOut - 0.04f;
                rails[c].Add(d * r + Vector3.up * (y + 0.95f));
                foreach (float f in new[] { -0.33f, 0f, 0.33f })
                {
                    if ((i == premiere && f < 0f) || (i == n - 1 && f > 0f)) continue;
                    bases[c].Add(Dir(a + pas * f) * r + Vector3.up * y);
                    hauts[c].Add(0.95f + f * h);
                }
                if (i == premiere || i == n - 1) Poteau(garde, d * r + Vector3.up * y, 0.95f, acier);
            }
            if (bloquerDessous && y - yBas < 2.1f && !large)
                ColliderOnly($"Blocage_Dessous_{i:00}", g, centre + d * ((rIn + rOut) / 2f) + Vector3.up * (yBas + (y - yBas) / 2f),
                             new Vector3(2f * ((rIn + rOut) / 2f) * Mathf.Sin(Mathf.Abs(pas) * Mathf.Deg2Rad / 2f) + 0.05f, y - yBas, rOut - rIn),
                             Quaternion.LookRotation(d));
        }
        GardeCorps(garde, "Cote_Interieur", rails[0], bases[0], hauts[0], rampe, acier);
        GardeCorps(garde, "Cote_Exterieur", rails[1], bases[1], hauts[1], rampe, acier);
    }

    // Barre ronde entre deux points (main courante, lisse)
    static GameObject Tube(string name, Transform parent, Vector3 a, Vector3 b, float diam, Material mat)
    {
        Vector3 d = b - a;
        var g = Disc(name, parent, (a + b) / 2f, diam, d.magnitude + diam, mat, false);
        g.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(90f, 0f, 0f);
        return g;
    }

    // Poteau de départ / de jonction (là où deux garde-corps se rejoignent)
    static void Poteau(Transform parent, Vector3 pied, float h, Material m)
    {
        Box("Poteau", parent, pied + Vector3.up * (h / 2f), new Vector3(0.07f, h, 0.07f), m, collider: false);
        Box("Poteau_Chapeau", parent, pied + Vector3.up * (h + 0.02f), new Vector3(0.09f, 0.04f, 0.09f), m, collider: false);
    }

    // Garde-corps continu : main courante ronde qui passe par tous les points, barreaux (base + hauteur),
    // et un mur invisible qui suit la main courante : on ne peut ni passer entre les barreaux ni tomber par-dessus.
    static void GardeCorps(Transform parent, string name, List<Vector3> rail, List<Vector3> bases, List<float> hauts,
                           Material rampe, Material acier, bool blocage = true)
    {
        var g = Group(name, parent, Vector3.zero);
        for (int k = 1; k < rail.Count; k++)
        {
            Vector3 a = rail[k - 1], b = rail[k];
            Tube("Main_Courante", g, a, b, 0.05f, rampe);
            Vector3 hz = b - a; hz.y = 0f;
            if (!blocage || hz.magnitude < 0.001f) continue;
            float dy = Mathf.Abs(b.y - a.y);
            ColliderOnly("Blocage", g, (a + b) / 2f + Vector3.down * 0.5f, new Vector3(0.1f, 1.2f + dy, hz.magnitude + 0.06f), Quaternion.LookRotation(hz));
        }
        for (int k = 0; k < bases.Count; k++)
            Box("Barreau", g, bases[k] + Vector3.up * (hauts[k] / 2f), new Vector3(0.016f, hauts[k], 0.016f), acier, collider: false);
    }

    // Garde-corps posé sur des points de plancher (ou de marche) : la main courante passe à la hauteur absolue yRail[k],
    // barreaux tous les 11 cm, lisse basse juste au-dessus du plancher.
    static void GardeCorpsSol(Transform parent, string name, List<Vector3> pied, List<float> yRail, Material rampe, Material acier)
    {
        var g = Group(name, parent, Vector3.zero);
        var rail = new List<Vector3>();
        var bases = new List<Vector3>();
        var hauts = new List<float>();
        for (int k = 0; k < pied.Count; k++) rail.Add(new Vector3(pied[k].x, yRail[k], pied[k].z));
        for (int k = 1; k < pied.Count; k++)
        {
            Vector3 a = pied[k - 1], b = pied[k];
            int nb = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 0.11f));
            for (int j = 0; j < nb; j++)
            {
                float f = (j + 0.5f) / nb;
                Vector3 pb = Vector3.Lerp(a, b, f);
                bases.Add(pb);
                hauts.Add(Mathf.Lerp(yRail[k - 1], yRail[k], f) - pb.y);
            }
            if (Mathf.Abs(a.y - b.y) < 0.01f) Tube("Lisse_Basse", g, a + Vector3.up * 0.08f, b + Vector3.up * 0.08f, 0.03f, rampe);
        }
        GardeCorps(g, "Main_Courante_Barreaux", rail, bases, hauts, rampe, acier);
    }

    // Chevalet jaune « Attention plancher glissant »
    static void Chevalet(Transform parent, Vector3 pos, Quaternion rot)
    {
        var g = Group("Chevalet_Attention", parent, pos);
        g.localRotation = rot;
        var jaune = Mat("Plastique_Jaune", new Color(0.98f, 0.82f, 0.05f), 0.4f);
        foreach (float sz in new[] { -1f, 1f })
        {
            var panneau = Box(sz < 0 ? "Face_Avant" : "Face_Arriere", g, new Vector3(0f, 0.31f, sz * 0.1f), new Vector3(0.3f, 0.62f, 0.02f), jaune, collider: false);
            panneau.transform.localRotation = Quaternion.Euler(sz * 9f, 0f, 0f);
        }
        Sign("Texte_Attention", g, "ATTENTION\n<size=60%>PLANCHER GLISSANT</size>", new Vector3(0f, 0.42f, -0.125f),
             Quaternion.Euler(-9f, 0f, 0f), new Vector2(0.26f, 0.14f), Color.black, null);
    }

    // Grille / portillon métallique fermé avec cadenas et panneau
    static void Grille(Transform parent, string name, Vector3 pos, Quaternion rot, float largeur)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Cadre_Haut", g, new Vector3(0f, 2.1f, 0f), new Vector3(largeur, 0.05f, 0.05f), Metal, collider: false);
        Box("Cadre_Bas", g, new Vector3(0f, 0.08f, 0f), new Vector3(largeur, 0.05f, 0.05f), Metal, collider: false);
        Box("Cadre_Milieu", g, new Vector3(0f, 1.05f, 0f), new Vector3(largeur, 0.04f, 0.04f), Metal, collider: false);
        int nb = Mathf.Max(4, Mathf.RoundToInt(largeur / 0.12f));
        for (int b = 0; b <= nb; b++)
            Box($"Barreau_{b}", g, new Vector3(-largeur / 2f + b * largeur / nb, 1.09f, 0f), new Vector3(0.02f, 2.05f, 0.02f), Metal, collider: false);
        Box("Chaine", g, new Vector3(largeur * 0.3f, 1.0f, -0.03f), new Vector3(0.03f, 0.25f, 0.02f), GrisPanneau, collider: false);
        Box("Cadenas", g, new Vector3(largeur * 0.3f, 0.86f, -0.04f), new Vector3(0.06f, 0.07f, 0.03f), Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), collider: false);
        Box("Ruban", g, new Vector3(0f, 1.3f, -0.04f), new Vector3(largeur, 0.08f, 0.01f), Ruban, collider: false);
        Sign("Panneau", g, "FERMÉ\n<size=60%>ACCÈS INTERDIT</size>", new Vector3(-largeur * 0.15f, 1.6f, -0.05f),
             Quaternion.LookRotation(Vector3.forward), new Vector2(0.5f, 0.3f), Color.black, Jaune);
    }

    // Objet avec un maillage donné
    static GameObject MeshGO(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    // Marche en éventail : secteur d'anneau (rIn → rOut, ±demiAngle autour de +z local), épaisseur vers le bas
    static Mesh WedgeMesh(float rIn, float rOut, float demiAngle, float ep)
    {
        string key = $"W{rIn:0.00}_{rOut:0.00}_{demiAngle:0.0}_{ep:0.000}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        const int seg = 4;
        System.Func<float, float, float, Vector3> P = (r, deg, y) =>
            new Vector3(r * Mathf.Sin(deg * Mathf.Deg2Rad), y, r * Mathf.Cos(deg * Mathf.Deg2Rad));
        for (int k = 0; k < seg; k++)
        {
            float a0 = -demiAngle + 2f * demiAngle * k / seg, a1 = -demiAngle + 2f * demiAngle * (k + 1) / seg;
            Vector3 i0 = P(rIn, a0, 0f), i1 = P(rIn, a1, 0f), o0 = P(rOut, a0, 0f), o1 = P(rOut, a1, 0f);
            Vector3 dn = Vector3.down * ep;
            md.Quad(i0, o0, o1, i1, Vector3.up, UV(i0), UV(o0), UV(o1), UV(i1));
            md.Quad(i0 + dn, i1 + dn, o1 + dn, o0 + dn, Vector3.down, UV(i0), UV(i1), UV(o1), UV(o0));
            Vector3 no = P(1f, (a0 + a1) / 2f, 0f);
            float l = (o1 - o0).magnitude;
            md.Quad(o0 + dn, o0, o1, o1 + dn, no, new Vector2(0, 0), new Vector2(0, ep), new Vector2(l, ep), new Vector2(l, 0));
            md.Quad(i1 + dn, i1, i0, i0 + dn, -no, new Vector2(0, 0), new Vector2(0, ep), new Vector2(l, ep), new Vector2(l, 0));
        }
        foreach (float s in new[] { -1f, 1f })
        {
            float a = s * demiAngle;
            Vector3 i0 = P(rIn, a, 0f), o0 = P(rOut, a, 0f), dn = Vector3.down * ep;
            Vector3 nrm = P(1f, a + s * 90f, 0f);
            float l = rOut - rIn;
            md.Quad(i0 + dn, i0, o0, o0 + dn, nrm, new Vector2(0, 0), new Vector2(0, ep), new Vector2(l, ep), new Vector2(l, 0));
        }
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
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
        Vitrine(c, new Vector3(12f, 0f, -0.97f));
        Babillard("Babillard_Corridor", c, new Vector3(10.4f, 1.5f, 1.085f), Quaternion.LookRotation(Vector3.forward), 1.4f, 0.9f);
        Babillard("Babillard_Corridor_2", c, new Vector3(17.2f, 1.5f, 1.085f), Quaternion.LookRotation(Vector3.forward), 1.4f, 0.9f);

        // Affiches de recherche (inventées)
        Poster("Affiche_1", c, new Vector3(16.3f, 1.5f, -1.085f), Quaternion.LookRotation(Vector3.back), "Affiche_1");
        Poster("Affiche_2", c, new Vector3(19.7f, 1.5f, -1.085f), Quaternion.LookRotation(Vector3.back), "Affiche_3");
        Poster("Affiche_3", c, new Vector3(20.55f, 1.5f, -1.085f), Quaternion.LookRotation(Vector3.back), "Affiche_2");

        // Sécurité : armoire à extincteur, panneau électrique, prises, enseigne SORTIE
        // Armoire à extincteur : caisson ouvert à l'avant, porte vitrée, l'extincteur est visible dedans
        var ae = Group("Armoire_Extincteur", c, new Vector3(22.6f, 0f, 0.99f));
        Box("Fond", ae, new Vector3(0f, 1.2f, 0.1f), new Vector3(0.5f, 0.8f, 0.02f), Rouge);
        Box("Cote_G", ae, new Vector3(-0.24f, 1.2f, 0f), new Vector3(0.02f, 0.8f, 0.22f), Rouge);
        Box("Cote_D", ae, new Vector3( 0.24f, 1.2f, 0f), new Vector3(0.02f, 0.8f, 0.22f), Rouge);
        Box("Haut", ae, new Vector3(0f, 1.59f, 0f), new Vector3(0.5f, 0.02f, 0.22f), Rouge);
        Box("Bas", ae, new Vector3(0f, 0.81f, 0f), new Vector3(0.5f, 0.02f, 0.22f), Rouge);
        // Porte vitrée qui s'ouvre (charnière à gauche vue du corridor)
        var charn = Group("Charniere_Vitre", ae, new Vector3(0.23f, 0f, -0.115f));
        var vitre = Box("Vitre", charn, new Vector3(-0.22f, 1.2f, 0f), new Vector3(0.44f, 0.74f, 0.012f), Verre, isStatic: false);
        Vector3[] cpos = { new Vector3(0f, 0.37f, 0f), new Vector3(0f, -0.37f, 0f), new Vector3(-0.22f, 0f, 0f), new Vector3(0.22f, 0f, 0f) };
        Vector3[] ctai = { new Vector3(0.46f, 0.025f, 0.016f), new Vector3(0.46f, 0.025f, 0.016f), new Vector3(0.025f, 0.76f, 0.016f), new Vector3(0.025f, 0.76f, 0.016f) };
        for (int q = 0; q < 4; q++) Box("Cadre_Porte", vitre.transform, cpos[q], ctai[q], Rouge, collider: false, isStatic: false);
        Box("Poignee", vitre.transform, new Vector3(-0.17f, 0f, -0.012f), new Vector3(0.02f, 0.1f, 0.02f), Metal, collider: false, isStatic: false);
        var pbe = vitre.AddComponent<PorteBattante>();
        vitre.AddComponent<FiltreProximite>();
        var sobe = new SerializedObject(pbe);
        sobe.FindProperty("pivot").objectReferenceValue = charn;
        sobe.FindProperty("angleOuvert").vector3Value = new Vector3(0f, -100f, 0f);
        sobe.ApplyModifiedPropertiesWithoutUndo();
        Sign("Etiquette_Armoire", ae, "EXTINCTEUR", new Vector3(0f, 1.5f, -0.116f), Quaternion.LookRotation(Vector3.back),
             new Vector2(0.36f, 0.07f), Color.white, null);
        var ext = Group("Extincteur", ae, new Vector3(0f, 0.43f, 0f));
        ext.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Extincteur("Extincteur_Corridor", ext, Vector3.zero);
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
    // Positions LOCALES au labo (origine = coin sud-ouest). x : ouest → est (12 m), z : corridor → fenêtres (9 m).
    // Disposition d'un vrai labo de chimie : paillasses le long des murs, deux îlots au centre (un îlot de travail
    // et un îlot de service avec éviers, gaz et étagère à réactifs), rangée de hottes au mur est, armoires de sécurité
    // au mur ouest, équipements d'urgence près de la porte, fenêtres à cadres en aluminium avec stores.
    static void BuildLab(Transform root, Transform plafonds)
    {
        var lab = Group("Labo_A111", root, LabOrigin);
        var meuble = Mat("Meuble_Brun", new Color(0.42f, 0.26f, 0.17f), 0.45f, "Bois", 1f);
        var dessus = Mat("Dessus_Noir", new Color(0.05f, 0.05f, 0.05f), 0.8f);
        var jaune = Mat("Armoire_Jaune", new Color(0.95f, 0.80f, 0.10f), 0.4f);
        var bleuArm = Mat("Armoire_Bleue", new Color(0.15f, 0.30f, 0.62f), 0.4f);
        var alu = Mat("Aluminium", new Color(0.80f, 0.81f, 0.82f), 0.7f);

        Box("Sol_Labo", lab, new Vector3(6f, -0.05f, 4.5f), new Vector3(12f, 0.1f, 9f),
            Mat("Carrelage_Labo", Color.white, 0.6f, "Carrelage_Labo", 1.2f));
        Disc("Drain", lab, new Vector3(1.4f, 0.004f, 1.6f), 0.18f, 0.004f, Rampe, false);

        // Murs + plinthes
        Box("Mur_Ouest", lab, new Vector3(0f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), MurBeige);
        Box("Mur_Est",   lab, new Vector3(12f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), MurBeige);
        Box("Plinthe_Ouest", lab, new Vector3(0.11f, 0.05f, 4.5f), new Vector3(0.02f, 0.1f, 9f), Plinthe, collider: false);
        Box("Plinthe_Est",   lab, new Vector3(11.89f, 0.05f, 4.5f), new Vector3(0.02f, 0.1f, 9f), Plinthe, collider: false);

        BuildFenetresLabo(lab, alu);

        // ---- Îlots centraux
        Ilot("IlotA", lab, new Vector3(4.5f, 0f, 3f), 6f, meuble, dessus, false);   // îlot de travail (énigme du bécher)
        Ilot("IlotB", lab, new Vector3(6.2f, 0f, 6f), 7.4f, meuble, dessus, true);  // îlot de service (évier au bout est), raccourci : passage devant le réfrigérateur
        // Verrerie de travail sur les îlots (attrapable ; le centre de l'îlot A reste libre pour l'énigme du bécher)
        Verrerie("Erlenmeyer", "Erlenmeyer_IlotA", lab, new Vector3(6.8f, 0.92f, 2.6f), Liquide("Vert", new Color(0.3f, 0.8f, 0.35f)), true);
        Verrerie("Becher", "Becher_IlotA", lab, new Vector3(7.1f, 0.92f, 3.3f), null, true);
        Verrerie("FlaconBlanc", "Flacon_IlotB", lab, new Vector3(3.4f, 0.92f, 5.5f), null, true);
        Verrerie("Becher", "Becher_IlotB", lab, new Vector3(8.4f, 0.92f, 6.55f), Liquide("Incolore", new Color(0.85f, 0.92f, 0.95f)), true);
        Verrerie("Eprouvette", "Eprouvette_IlotB", lab, new Vector3(5.2f, 0.92f, 6.55f), null, true);
        for (int e = 0; e < 2; e++) Rotavap(lab, $"Evaporateur_{e}", new Vector3(2.1f + e * 0.8f, 0.92f, 3.2f));
        RackTubes(lab, "Portoir_Tubes_2", new Vector3(7.6f, 0.92f, 5.55f));
        // Îlot B : titrage, becs Bunsen sur les robinets de gaz, bain-marie ; îlot A : pipettes, bec Bunsen
        Titrage(lab, new Vector3(4.4f, 0.92f, 5.55f));
        Bunsen(lab, "Bec_Bunsen_1", new Vector3(6.3f, 0.92f, 5.45f));
        Bunsen(lab, "Bec_Bunsen_2", new Vector3(3.0f, 0.92f, 6.55f), true);
        BainMarie(lab, new Vector3(6.9f, 0.92f, 6.5f), true);
        PortePipettes(lab, new Vector3(3.6f, 0.92f, 2.5f));
        Bunsen(lab, "Bec_Bunsen_3", new Vector3(5.9f, 0.92f, 3.45f), true);
        // Réfrigérateur de labo contre le mur est, boîte à verre cassé et trousse de déversement près de la porte
        Refrigerateur(lab, new Vector3(11.55f, 0f, 6.2f), Vector3.left);
        Securite(lab, new Vector3(3.7f, 0f, 0.3f), new Vector3(2.3f, 0f, 0.35f));
        // Tabourets rangés aux bouts des îlots (pas devant les portes des armoires, qui doivent pouvoir s'ouvrir)
        var tab = Group("Tabourets", lab, Vector3.zero);
        Vector3[] tabs = { new Vector3(1.05f, 0f, 2.6f), new Vector3(1.05f, 0f, 3.4f), new Vector3(7.95f, 0f, 2.6f), new Vector3(7.95f, 0f, 3.4f),
                           new Vector3(2.05f, 0f, 5.6f), new Vector3(2.05f, 0f, 6.4f) };
        for (int ti = 0; ti < tabs.Length; ti++) Tabouret($"Tabouret_{ti}", tab, tabs[ti]);

        // ---- Mur nord : paillasse continue sous les fenêtres, avec appareils
        var pn = Group("Paillasse_Fenetres", lab, Vector3.zero);
        RangeeCaissons(pn, "Caisson_Nord", new Vector3(0.2f, 0f, 8.6f), Vector3.right, 11.6f, Vector3.back, 0.6f, meuble);
        Box("Dessus", pn, new Vector3(6f, 0.9f, 8.58f), new Vector3(11.65f, 0.04f, 0.68f), dessus);
        Balance("Balance_Analytique", pn, new Vector3(1.2f, 0.92f, 8.6f));
        PlaqueAgitatrice("Plaque_Agitatrice_1", pn, new Vector3(3.2f, 0.92f, 8.55f), Liquide("Bleu", new Color(0.2f, 0.45f, 0.9f)));
        PlaqueAgitatrice("Plaque_Agitatrice_2", pn, new Vector3(3.8f, 0.92f, 8.55f), Liquide("Incolore", new Color(0.85f, 0.92f, 0.95f)));
        PhMetre("PH_Metre", pn, new Vector3(5.4f, 0.92f, 8.6f));
        Dessiccateur(pn, new Vector3(7.2f, 0.92f, 8.6f));
        RackTubes(pn, "Portoir_Tubes_1", new Vector3(5.95f, 0.92f, 8.55f));
        var cahier = Group("Cahier_Labo", pn, new Vector3(9.0f, 0.92f, 8.5f)).gameObject;
        Box("Couverture", cahier.transform, new Vector3(0f, 0.006f, 0f), new Vector3(0.22f, 0.012f, 0.3f), Mat("Cahier", new Color(0.12f, 0.20f, 0.45f), 0.3f), collider: false);
        Box("Pages", cahier.transform, new Vector3(0.004f, 0.006f, 0f), new Vector3(0.214f, 0.008f, 0.29f), Blanc, collider: false);
        Attrapable(cahier, new Vector3(0f, 0.006f, 0f), new Vector3(0.22f, 0.012f, 0.3f), 0.3f);
        for (int f = 0; f < 3; f++)
            Verrerie("Pissette", $"Pissette_{f}", pn, new Vector3(10.0f + f * 0.14f, 0.92f, 8.7f), null, true);
        // Verrerie de travail sur la paillasse (attrapable)
        var bleu = Liquide("Bleu", new Color(0.2f, 0.45f, 0.9f));
        var rose = Liquide("Rose", new Color(0.95f, 0.35f, 0.6f));
        var jauneL = Liquide("Jaune", new Color(0.95f, 0.85f, 0.2f));
        Verrerie("Erlenmeyer", "Erlenmeyer_1", pn, new Vector3(2.2f, 0.92f, 8.55f), rose, true);
        Verrerie("Erlenmeyer", "Erlenmeyer_2", pn, new Vector3(2.45f, 0.92f, 8.6f), null, true);
        Verrerie("Becher", "Becher_1", pn, new Vector3(4.4f, 0.92f, 8.5f), bleu, true);
        Verrerie("Becher", "Becher_2", pn, new Vector3(4.6f, 0.92f, 8.65f), null, true);
        Verrerie("Eprouvette", "Eprouvette_1", pn, new Vector3(6.3f, 0.92f, 8.7f), jauneL, true);
        Verrerie("FlaconAmbre", "Flacon_Paillasse_1", pn, new Vector3(8.2f, 0.92f, 8.7f), null, true);
        Verrerie("FlaconBlanc", "Flacon_Paillasse_2", pn, new Vector3(8.35f, 0.92f, 8.7f), null, true);
        // Appareils de la paillasse sous les fenêtres
        PosteInformatique(pn, new Vector3(0.55f, 0.92f, 8.6f));
        Vortex(pn, new Vector3(6.7f, 0.92f, 8.55f));
        Spectro(pn, new Vector3(7.75f, 0.92f, 8.55f));
        Centrifugeuse(pn, new Vector3(9.6f, 0.92f, 8.6f));
        Microscope(pn, new Vector3(10.95f, 0.92f, 8.6f));

        // ---- Mur sud (côté corridor) : blouses et lunettes près de la porte, tableau blanc, paillasse avec évier
        var ps = Group("Mur_Sud_Equipement", lab, Vector3.zero);
        Box("Patere_Rail", ps, new Vector3(3.05f, 1.75f, 0.13f), new Vector3(1.6f, 0.04f, 0.04f), Metal, collider: false);
        var blouse = Mat("Blouse_Blanche", new Color(0.95f, 0.95f, 0.93f), 0.15f);
        for (int b = 0; b < 4; b++)
        {
            Box($"Blouse_{b}", ps, new Vector3(2.5f + b * 0.37f, 1.25f, 0.2f), new Vector3(0.34f, 0.95f, 0.1f), blouse, collider: false);
            Box($"Col_{b}", ps, new Vector3(2.5f + b * 0.37f, 1.7f, 0.2f), new Vector3(0.14f, 0.06f, 0.11f), blouse, collider: false);
        }
        Box("Armoire_Lunettes", ps, new Vector3(4.15f, 1.45f, 0.18f), new Vector3(0.45f, 0.45f, 0.16f), Mat("Armoire_Lunettes", new Color(0.55f, 0.75f, 0.85f), 0.5f), collider: false);
        Box("Armoire_Lunettes_Vitre", ps, new Vector3(4.15f, 1.45f, 0.265f), new Vector3(0.38f, 0.38f, 0.01f), Verre, collider: false);
        for (int l = 0; l < 3; l++)
            Box($"Lunettes_{l}", ps, new Vector3(4.03f + l * 0.12f, 1.42f, 0.22f), new Vector3(0.1f, 0.04f, 0.06f), Mat("Lunettes", new Color(0.85f, 0.92f, 0.98f), 0.9f), collider: false);
        Sign("Panneau_EPI", ps, "PORT DES LUNETTES ET DE LA BLOUSE\n<size=75%>OBLIGATOIRE EN TOUT TEMPS</size>", new Vector3(3.2f, 2.15f, 0.12f),
             Quaternion.LookRotation(Vector3.back), new Vector2(1.2f, 0.25f), Color.white, Mat("Bleu_Obligation", new Color(0.05f, 0.30f, 0.70f), 0.4f));
        Box("Chariot", ps, new Vector3(4.9f, 0.45f, 0.45f), new Vector3(0.8f, 0.9f, 0.5f), Metal);
        PoubellePedale(ps, new Vector3(9.15f, 0f, 0.3f)); // contre le mur, à côté de l'évier

        // Tableau blanc avec des notes de TP (le tiers gauche du tableau reste libre pour l'indice de l'équipe)
        var tb = Group("Tableau_Blanc", lab, new Vector3(7f, 1.6f, 0.12f));
        Box("Cadre", tb, new Vector3(0f, 0f, -0.005f), new Vector3(3.08f, 1.28f, 0.02f), alu, collider: false);
        Box("Surface", tb, new Vector3(0f, 0f, 0.008f), new Vector3(3f, 1.2f, 0.01f), Blanc, collider: false);
        Box("Rebord_Feutres", tb, new Vector3(0f, -0.64f, 0.05f), new Vector3(2.6f, 0.03f, 0.08f), alu, collider: false);
        string[] feutres = { "Feutre_Noir", "Feutre_Bleu", "Feutre_Rouge" };
        Color[] fc = { new Color(0.05f, 0.05f, 0.05f), new Color(0.1f, 0.2f, 0.7f), new Color(0.75f, 0.1f, 0.1f) };
        for (int k = 0; k < 3; k++)
            Box(feutres[k], tb, new Vector3(-0.6f + k * 0.12f, -0.61f, 0.05f), new Vector3(0.11f, 0.02f, 0.02f), Mat(feutres[k], fc[k], 0.4f), collider: false);
        Box("Brosse", tb, new Vector3(0.5f, -0.6f, 0.05f), new Vector3(0.14f, 0.04f, 0.05f), Noir, collider: false);
        var face = Quaternion.LookRotation(Vector3.back);
        Sign("Notes_TP", tb, NotesTableau, new Vector3(0.45f, 0.02f, 0.02f), face, new Vector2(1.95f, 1.1f),
             new Color(0.08f, 0.12f, 0.45f), null, TextAlignmentOptions.TopLeft, false, 0.6f);

        // Paillasse sud avec évier, égouttoir à verrerie au mur
        var pse = Group("Paillasse_Sud", lab, Vector3.zero);
        RangeeCaissons(pse, "Caisson_Sud", new Vector3(9.4f, 0f, 0.45f), Vector3.right, 2.4f, Vector3.forward, 0.65f, meuble);
        DessusAvecTrou(pse, "Dessus", new Vector3(10.6f, 0.9f, 0.47f), new Vector3(2.45f, 0.04f, 0.72f), new Vector2(10.9f, 0.5f), new Vector2(0.52f, 0.42f), dessus);
        Verrerie("Becher", "Becher_Evier", pse, new Vector3(10.35f, 0.92f, 0.5f), null, true);
        Etuve(pse, new Vector3(9.75f, 0.92f, 0.47f), true);
        Distributeurs(pse, new Vector3(10.45f, 1.5f, 0.17f), new Vector3(11.35f, 1.25f, 0.16f));
        Evier("Evier_Sud", pse, new Vector3(10.9f, 0.92f, 0.5f), Vector3.forward);
        var eg = Group("Egouttoir", pse, new Vector3(10.9f, 1.55f, 0.13f));
        Box("Panneau", eg, Vector3.zero, new Vector3(0.9f, 0.7f, 0.03f), Blanc, collider: false);
        for (int gx = 0; gx < 5; gx++)
            for (int gy = 0; gy < 3; gy++)
            {
                Box($"Tige_{gx}_{gy}", eg, new Vector3(-0.36f + gx * 0.18f, -0.25f + gy * 0.24f, 0.05f), new Vector3(0.012f, 0.012f, 0.09f), Blanc, collider: false);
                if ((gx + gy) % 2 == 0)
                    Ballon($"Verre_{gx}_{gy}", eg, new Vector3(-0.36f + gx * 0.18f, -0.25f + gy * 0.24f + 0.04f, 0.1f), 0.1f, null);
            }

        // ---- Mur ouest : douche d'urgence, extincteur, couverture anti-feu, premiers soins, réactifs, armoires de sécurité
        DoucheUrgence(lab, new Vector3(0.45f, 0f, 1.3f));
        var vertSecu = Mat("Vert_Securite", new Color(0.0f, 0.55f, 0.25f), 0.4f);
        Sign("Panneau_Douche", lab, "DOUCHE D'URGENCE\n<size=80%>LAVE-YEUX</size>", new Vector3(0.115f, 2.55f, 1.3f),
             Quaternion.LookRotation(Vector3.left), new Vector2(0.6f, 0.25f), Color.white, vertSecu);

        var urg = Group("Urgence", lab, Vector3.zero);
        Extincteur("Extincteur_Labo", urg, new Vector3(0.22f, 0f, 2.5f));
        Sign("Panneau_Extincteur", urg, "EXTINCTEUR", new Vector3(0.115f, 1.55f, 2.5f), Quaternion.LookRotation(Vector3.left),
             new Vector2(0.4f, 0.1f), Color.white, Rouge);
        Box("Couverture_Antifeu", urg, new Vector3(0.17f, 1.4f, 3.05f), new Vector3(0.08f, 0.35f, 0.3f), Rouge, collider: false);
        Sign("Etiquette_Couverture", urg, "COUVERTURE\nANTI-FEU", new Vector3(0.215f, 1.42f, 3.05f), Quaternion.LookRotation(Vector3.left),
             new Vector2(0.25f, 0.12f), Color.white, null);
        Box("Premiers_Soins", urg, new Vector3(0.17f, 1.45f, 3.6f), new Vector3(0.1f, 0.35f, 0.4f), Blanc, collider: false);
        Box("Croix_V", urg, new Vector3(0.225f, 1.45f, 3.6f), new Vector3(0.005f, 0.18f, 0.06f), vertSecu, collider: false);
        Box("Croix_H", urg, new Vector3(0.225f, 1.45f, 3.6f), new Vector3(0.005f, 0.06f, 0.18f), vertSecu, collider: false);

        // Étagère des fioles (énigme) : vraie étagère à réactifs avec rebords anti-chute
        var et = Group("Etagere_Fioles", lab, new Vector3(0.3f, 0f, 5f));
        Box("Cote_N", et, new Vector3(0f, 1.0f, 0.78f), new Vector3(0.4f, 2.0f, 0.04f), meuble);
        Box("Cote_S", et, new Vector3(0f, 1.0f, -0.78f), new Vector3(0.4f, 2.0f, 0.04f), meuble);
        Box("Fond", et, new Vector3(-0.18f, 1.0f, 0f), new Vector3(0.04f, 2.0f, 1.6f), meuble);
        for (int s = 0; s < 5; s++)
        {
            Box($"Tablette_{s}", et, new Vector3(0f, 0.1f + s * 0.45f, 0f), new Vector3(0.4f, 0.03f, 1.56f), meuble);
            if (s > 0) Box($"Rebord_{s}", et, new Vector3(0.19f, 0.15f + s * 0.45f, 0f), new Vector3(0.01f, 0.06f, 1.56f), alu, collider: false);
        }
        // Flacons bruns de réserve sur les tablettes du haut et du bas (celles du milieu restent libres pour les fioles de l'énigme)
        var ambre = GlassMat("Verre_Ambre", new Color(0.45f, 0.22f, 0.05f), 0.8f);
        foreach (int s in new[] { 0, 4 })
            for (int k = 0; k < 7; k++)
                Flacon($"Reactif_{s}_{k}", et, new Vector3(0.02f, 0.115f + s * 0.45f, -0.6f + k * 0.2f), 0.22f, 0.09f, ambre,
                       Liquide("Ambre", new Color(0.6f, 0.35f, 0.1f)), true);

        // Armoires de sécurité : inflammables (jaune) et acides/corrosifs (bleue), ventilées
        var arm = Group("Armoires_Securite", lab, Vector3.zero);
        var pj = Caisson(arm, "Armoire_Jaune", new Vector3(0.38f, 0f, 6.5f), Vector3.right, 1.0f, 0.55f, 1.8f, 2, jaune, 2,
            (g, yb, hn) => { for (int n = 0; n < 3; n++) for (int f = 0; f < 4; f++)
                Verrerie(f % 2 == 0 ? "FlaconAmbre" : "FlaconBlanc", $"Solvant_{n}_{f}", g, new Vector3(-0.35f + f * 0.2f, yb + n * hn + (n > 0 ? 0.01f : 0f), 0f), null, true); });
        foreach (var b in pj)
            Sign("Inflammable", b, "INFLAMMABLE\n<size=55%>TENIR LOIN DES FLAMMES</size>", new Vector3(0f, 0.45f, 0.011f),
                 Quaternion.LookRotation(Vector3.back), new Vector2(0.4f, 0.16f), new Color(0.75f, 0.05f, 0.05f), null);
        var pc = Caisson(arm, "Armoire_Corrosifs", new Vector3(0.38f, 0f, 7.55f), Vector3.right, 0.8f, 0.55f, 0.9f, 2, bleuArm, 1,
            (g, yb, hn) => { for (int n = 0; n < 2; n++) for (int f = 0; f < 3; f++)
                Verrerie("FlaconBlanc", $"Acide_{n}_{f}", g, new Vector3(-0.25f + f * 0.22f, yb + n * hn + (n > 0 ? 0.01f : 0f), 0f), null, true); });
        Sign("Corrosifs", pc[1], "ACIDES\n<size=70%>CORROSIFS</size>", new Vector3(0f, 0.2f, 0.011f),
             Quaternion.LookRotation(Vector3.back), new Vector2(0.3f, 0.14f), Color.white, null);

        // ---- Mur est : deux hottes chimiques (la 1re est la source de la fuite), bonbonne de gaz, déchets
        Hotte("Hotte", lab, new Vector3(11.45f, 0f, 3f), meuble, dessus, alu, true);
        Hotte("Hotte_2", lab, new Vector3(11.45f, 0f, 4.6f), meuble, dessus, alu, false);
        var bon = Group("Bonbonne_Azote", lab, new Vector3(11.7f, 0f, 1.25f));
        Disc("Corps", bon, new Vector3(0f, 0.7f, 0f), 0.23f, 1.4f, Mat("Bonbonne_Noire", new Color(0.08f, 0.08f, 0.08f), 0.5f), true);
        Disc("Ogive", bon, new Vector3(0f, 1.45f, 0f), 0.16f, 0.12f, Mat("Bonbonne_Noire", new Color(0.08f, 0.08f, 0.08f), 0.5f), false);
        Disc("Detendeur", bon, new Vector3(0f, 1.56f, 0f), 0.07f, 0.1f, Metal, false);
        Box("Chaine", bon, new Vector3(0f, 1.1f, 0f), new Vector3(0.26f, 0.03f, 0.26f), Metal, collider: false);
        Sign("Etiquette_Azote", bon, "N<sub>2</sub>", new Vector3(-0.118f, 0.9f, 0f), Quaternion.LookRotation(Vector3.right),
             new Vector2(0.12f, 0.1f), Color.white, null);
        // Déchets chimiques contre le mur est : bac de rétention jaune, deux bidons attrapables, affiche au mur
        var dechets = Group("Dechets_Chimiques", lab, new Vector3(11.55f, 0f, 7.55f));
        Box("Bac_Retention", dechets, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.08f, 0.95f), Mat("Plastique_Jaune", new Color(0.98f, 0.82f, 0.05f), 0.4f));
        string[] types = { "HALOGÉNÉS", "NON HALOGÉNÉS" };
        for (int d = 0; d < 2; d++)
        {
            var bidon = Verrerie("Bidon", $"Bidon_{d}", dechets, new Vector3(0f, 0.08f, -0.22f + d * 0.44f), null, true);
            bidon.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Sign("Etiquette", bidon.transform, $"SOLVANTS\n<size=70%>{types[d]}</size>", new Vector3(0f, 0.2f, 0.152f),
                 Quaternion.LookRotation(Vector3.back), new Vector2(0.2f, 0.12f), Color.black, Jaune);
        }
        Sign("Etiquette_Dechets", lab, "DÉCHETS CHIMIQUES\n<size=70%>SOLVANTS ORGANIQUES</size>", new Vector3(11.885f, 1.0f, 7.55f),
             Quaternion.LookRotation(Vector3.right), new Vector2(0.75f, 0.2f), Color.black, Jaune);

        // Tableau périodique et horloge (mur est, au-dessus des déchets)
        Box("Tableau_Periodique", lab, new Vector3(11.885f, 1.65f, 6.6f), new Vector3(0.01f, 0.9f, 1.6f),
            TexMat("Tableau_Periodique", 1.6f, 0.9f), collider: false);
        var horloge = Disc("Horloge", lab, new Vector3(11.88f, 2.5f, 6.6f), 0.35f, 0.03f, Blanc, false);
        horloge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Affiche SIMDUT au mur ouest (pictogrammes de danger)
        Box("Affiche_SIMDUT", lab, new Vector3(0.115f, 1.75f, 8.0f), new Vector3(0.01f, 0.6f, 0.45f),
            TexMat("Affiche_SIMDUT", 0.45f, 0.6f), collider: false);

        // Points pour l'équipe (mêmes positions qu'avant)
        var pts = Group("Points", lab, Vector3.zero);
        Point("Point_Fioles",  pts, new Vector3(0.7f, 1.2f, 5f));
        Point("Point_Becher",  pts, new Vector3(4.5f, 0.95f, 3f));
        Point("Point_Evier",   pts, new Vector3(10.1f, 0.95f, 6f));
        Point("Point_Hotte",   pts, new Vector3(11f, 1.2f, 3f));
        Point("Point_Horloge", pts, new Vector3(11.85f, 2.5f, 6.6f));
        Point("Ancre_Teleport_Ilot",    pts, new Vector3(4.5f, 0f, 1.8f), 0f);
        Point("Ancre_Teleport_Etagere", pts, new Vector3(1.3f, 0f, 5f), -90f);

        // Faux plafond en dalles, détails techniques
        Box("Plafond_Labo", plafonds, LabOrigin + new Vector3(6f, LabH + 0.025f, 4.5f), new Vector3(12f, 0.05f, 9f), Dalles, collider: false);
        CeilingDetails(lab, LabH, new[] { 1.5f, 4.5f, 7.5f, 10.5f }, new[] { 4.5f },
                       new[] { new Vector3(4.5f, 0f, 1.2f), new Vector3(8.5f, 0f, 7.8f) });
    }

    const string NotesTableau =
        "<b>TP 4 — Synthèse de l'aspirine</b>\n" +
        "C<sub>7</sub>H<sub>6</sub>O<sub>3</sub> + C<sub>4</sub>H<sub>6</sub>O<sub>3</sub>  →  C<sub>9</sub>H<sub>8</sub>O<sub>4</sub> + CH<sub>3</sub>COOH\n" +
        "acide salicylique   anhydride acétique      aspirine\n\n" +
        "• Acide salicylique : 2,00 g (14,5 mmol)\n" +
        "• Anhydride acétique : 5,0 mL   • H<sub>2</sub>SO<sub>4</sub> : 5 gouttes\n" +
        "• Chauffer 15 min à 80 °C, puis bain de glace\n" +
        "• Rendement théorique : 2,61 g   (M = 180,16 g/mol)\n" +
        "• T<sub>fusion</sub> attendue : 135 °C\n\n" +
        "<color=#B01818>ATTENTION : sous la hotte · gants nitrile · lunettes</color>";

    // Fenêtres du mur nord : allège, trumeaux peints, cadres en aluminium, imposte, appuis et stores vénitiens
    static void BuildFenetresLabo(Transform lab, Material alu)
    {
        var f = Group("Fenetres_Nord", lab, Vector3.zero);
        Box("Mur_Nord_Bas",  f, new Vector3(6f, 0.5f, 9f), new Vector3(12.2f, 1.0f, 0.2f), MurBeige);
        Box("Mur_Nord_Haut", f, new Vector3(6f, 2.8f, 9f), new Vector3(12.2f, 0.4f, 0.2f), MurBeige);
        Box("Vitrage", f, new Vector3(6f, 1.8f, 9.02f), new Vector3(12f, 1.6f, 0.02f), Verre);
        for (int i = 0; i <= 8; i++)
        {
            float px = Mathf.Clamp(1.5f * i, 0.15f, 11.85f);
            float w = (i == 0 || i == 8) ? 0.1f : 0.3f;
            Box($"Trumeau_{i}", f, new Vector3(px, 1.8f, 9f), new Vector3(w, 1.6f, 0.2f), MurBeige);
        }
        var stores = Mat("Store", new Color(0.88f, 0.87f, 0.82f), 0.3f);
        for (int b = 0; b < 8; b++)
        {
            float cx = 0.75f + 1.5f * b, w = 1.2f;
            var baie = Group($"Baie_{b}", f, new Vector3(cx, 0f, 8.95f));
            Box("Cadre_Haut", baie, new Vector3(0f, 2.575f, 0f), new Vector3(w, 0.05f, 0.08f), alu, collider: false);
            Box("Cadre_Bas",  baie, new Vector3(0f, 1.025f, 0f), new Vector3(w, 0.05f, 0.08f), alu, collider: false);
            Box("Cadre_G", baie, new Vector3(-w / 2f + 0.025f, 1.8f, 0f), new Vector3(0.05f, 1.6f, 0.08f), alu, collider: false);
            Box("Cadre_D", baie, new Vector3( w / 2f - 0.025f, 1.8f, 0f), new Vector3(0.05f, 1.6f, 0.08f), alu, collider: false);
            Box("Imposte", baie, new Vector3(0f, 2.15f, 0f), new Vector3(w, 0.05f, 0.08f), alu, collider: false);
            Box("Meneau", baie, new Vector3(0f, 1.6f, 0f), new Vector3(0.04f, 1.1f, 0.07f), alu, collider: false);
            Box("Poignee", baie, new Vector3(0.08f, 1.55f, -0.05f), new Vector3(0.02f, 0.12f, 0.02f), alu, collider: false);
            Box("Appui", baie, new Vector3(0f, 1.0f, -0.12f), new Vector3(w + 0.1f, 0.03f, 0.2f), Blanc, collider: false);
            // Stores : baissés à moitié sur 3 baies (côté soleil), remontés ailleurs
            bool baisse = b == 1 || b == 2 || b == 6;
            float bas = baisse ? 1.75f : 2.45f;
            Box("Store_Caisson", baie, new Vector3(0f, 2.53f, -0.08f), new Vector3(w - 0.1f, 0.04f, 0.05f), stores, collider: false);
            int lames = Mathf.RoundToInt((2.5f - bas) / 0.05f);
            for (int l = 0; l < lames; l++)
                Box($"Lame_{l}", baie, new Vector3(0f, 2.48f - l * 0.05f, -0.08f), new Vector3(w - 0.12f, 0.004f, 0.045f), stores,
                    collider: false, rot: Quaternion.Euler(baisse ? 35f : 0f, 0f, 0f));
        }
    }

    // ------------------------------------------------------------------ VERRERIE ET OBJETS MANIPULABLES
    // Maillage de révolution (verrerie, flacons) : profil (rayon, hauteur) du bas vers le haut
    static Mesh LatheMesh(string key, Vector2[] profil, int seg = 20)
    {
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        int n = profil.Length;
        for (int i = 0; i < n; i++)
            for (int k = 0; k <= seg; k++)
            {
                float a = 2f * Mathf.PI * k / seg;
                v.Add(new Vector3(profil[i].x * Mathf.Cos(a), profil[i].y, profil[i].x * Mathf.Sin(a)));
                uv.Add(new Vector2((float)k / seg, profil[i].y));
            }
        for (int i = 0; i < n - 1; i++)
            for (int k = 0; k < seg; k++)
            {
                int a0 = i * (seg + 1) + k, a1 = a0 + 1, b0 = a0 + seg + 1, b1 = b0 + 1;
                t.Add(a0); t.Add(b0); t.Add(a1);
                t.Add(a1); t.Add(b0); t.Add(b1);
                t.Add(a0); t.Add(a1); t.Add(b0);      // double face : visible de l'intérieur comme de l'extérieur
                t.Add(a1); t.Add(b1); t.Add(b0);
            }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }

    static GameObject Lathe(string name, Transform parent, Vector3 pos, string key, Vector2[] profil, Material mat)
    {
        var go = MeshGO(name, parent, LatheMesh(key, profil), mat, pos, Quaternion.identity);
        PetitObjet(go);
        return go;
    }

    static Vector2[] V(params float[] p) { var r = new Vector2[p.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = new Vector2(p[2 * i], p[2 * i + 1]); return r; }

    // Pièces de verrerie réalistes. type : Becher, Erlenmeyer, Eprouvette, Pissette, FlaconAmbre, FlaconBlanc, Bidon
    static GameObject Verrerie(string type, string name, Transform parent, Vector3 pos, Material liquide, bool attrapable)
    {
        var g = Group(name, parent, pos).gameObject;
        var tr = g.transform;
        var ambre = GlassMat("Verre_Ambre", new Color(0.45f, 0.22f, 0.05f), 0.8f);
        var plast = Mat("Plastique_Blanc", new Color(0.93f, 0.93f, 0.90f), 0.3f);
        Vector3 c = Vector3.zero, s = Vector3.one * 0.1f; float masse = 0.2f;
        switch (type)
        {
            case "Becher":
                Lathe("Verre", tr, Vector3.zero, "L_Becher", V(0,0, .034f,0, .035f,.004f, .035f,.095f, .038f,.098f), Verre);
                Lathe("Liquide", tr, Vector3.zero, "L_Becher_Liq", V(0,.004f, .033f,.004f, .033f,.07f, 0,.07f), liquide ?? EauClaire());
                c = new Vector3(0, .05f, 0); s = new Vector3(.075f, .1f, .075f); break;
            case "Erlenmeyer":
                Lathe("Verre", tr, Vector3.zero, "L_Erlen", V(0,0, .04f,0, .043f,.006f, .042f,.015f, .017f,.095f, .015f,.125f, .018f,.13f), Verre);
                Lathe("Liquide", tr, Vector3.zero, "L_Erlen_Liq", V(0,.004f, .039f,.004f, .04f,.012f, .03f,.04f, .02f,.07f, 0,.07f), liquide ?? EauClaire());
                c = new Vector3(0, .065f, 0); s = new Vector3(.085f, .13f, .085f); break;
            case "Eprouvette":
                Lathe("Pied", tr, Vector3.zero, "L_Epr_Pied", V(0,0, .035f,0, .035f,.012f, .014f,.016f, 0,.016f), Verre);
                Lathe("Tube", tr, Vector3.zero, "L_Epr_Tube", V(.0135f,.016f, .0135f,.26f, .017f,.265f), Verre);
                Lathe("Liquide", tr, Vector3.zero, "L_Epr_Liq", V(0,.017f, .012f,.017f, .012f,.2f, 0,.2f), liquide ?? EauClaire());
                c = new Vector3(0, .13f, 0); s = new Vector3(.07f, .27f, .07f); break;
            case "Pissette":
                Lathe("Corps", tr, Vector3.zero, "L_Pissette", V(0,0, .032f,0, .034f,.01f, .034f,.14f, .025f,.16f, .012f,.17f, .012f,.18f, 0,.18f), plast);
                var bec = Disc("Bec", tr, new Vector3(0.02f, 0.21f, 0f), 0.008f, 0.09f, plast, false);
                bec.transform.localRotation = Quaternion.Euler(0f, 0f, -55f);
                c = new Vector3(0, .1f, 0); s = new Vector3(.07f, .2f, .07f); break;
            case "FlaconAmbre":
            case "FlaconBlanc":
                var corps = type == "FlaconAmbre" ? ambre : plast;
                Lathe("Corps", tr, Vector3.zero, "L_Flacon", V(0,0, .032f,0, .034f,.006f, .034f,.11f, .03f,.13f, .016f,.145f, .014f,.15f, .014f,.165f, 0,.165f), corps);
                Lathe("Bouchon", tr, Vector3.zero, "L_Bouchon", V(0,.163f, .019f,.163f, .019f,.19f, 0,.19f), type == "FlaconAmbre" ? Noir : Mat("Bouchon_Bleu", new Color(0.1f, 0.3f, 0.8f), 0.4f));
                Box("Etiquette", tr, new Vector3(0f, .065f, 0f), new Vector3(.05f, .06f, .0695f), Blanc, collider: false);
                c = new Vector3(0, .095f, 0); s = new Vector3(.07f, .19f, .07f); masse = 0.5f; break;
            case "Bidon":
                var bl = Mat("Bidon_Blanc", new Color(0.93f, 0.93f, 0.88f), 0.3f);
                Box("Corps", tr, new Vector3(0f, 0.18f, 0f), new Vector3(0.24f, 0.36f, 0.3f), bl, collider: false);
                Box("Poignee", tr, new Vector3(0f, 0.4f, -0.06f), new Vector3(0.04f, 0.05f, 0.14f), bl, collider: false);
                Disc("Bouchon", tr, new Vector3(0f, 0.375f, 0.09f), 0.06f, 0.04f, Rouge, false);
                c = new Vector3(0, .2f, 0); s = new Vector3(.24f, .42f, .3f); masse = 2f; break;
        }
        if (attrapable)
        {
            Attrapable(g, c, s, masse);
            var liq = tr.Find("Liquide");
            float col = type == "Becher" ? 0.098f : type == "Erlenmeyer" ? 0.13f : type == "Eprouvette" ? 0.265f : -1f;
            if (liq != null && col > 0f) AjouterVersable(g, liq, col, liquide, liquide != null ? 0.6f : 0f);
        }
        return g;
    }

    static Material EauClaire() => Liquide("Eau_Claire", new Color(0.75f, 0.88f, 1f));

    // Liquide qu'on peut verser : filet visible (LineRenderer) qui tombe en arc, flaques, transvasement entre récipients
    static void AjouterVersable(GameObject g, Transform liquide, float hauteurCol, Material liqMat, float quantite)
    {
        Color c = liqMat != null && liqMat.HasProperty("_BaseColor") ? liqMat.GetColor("_BaseColor") : new Color(0.75f, 0.88f, 1f);
        c.a = 0.85f;
        liquide.gameObject.isStatic = false;
        var bec = new GameObject("Bec").transform;
        bec.SetParent(g.transform, false);
        bec.localPosition = new Vector3(0f, hauteurCol, 0f);
        var fg = new GameObject("Filet");
        fg.transform.SetParent(g.transform, false);
        var lr = fg.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.008f;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.sharedMaterial = GoutteMat();
        lr.startColor = c; lr.endColor = c;
        lr.enabled = false;
        var lv = g.AddComponent<LiquideVersable>();
        var so = new SerializedObject(lv);
        so.FindProperty("liquide").objectReferenceValue = liquide;
        so.FindProperty("bec").objectReferenceValue = bec;
        so.FindProperty("filet").objectReferenceValue = lr;
        so.FindProperty("matFlaque").objectReferenceValue = GlassMat("Flaque", new Color(0.75f, 0.88f, 1f), 0.75f);
        so.FindProperty("couleur").colorValue = c;
        so.FindProperty("quantiteDepart").floatValue = quantite;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material GoutteMat()
    {
        string path = $"{MatFolder}/GB_Gouttes.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var urp = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        m = urp != null ? new Material(urp) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Texte TextMeshPro qu'un script peut changer (afficheurs des appareils)
    static TextMeshPro TexteTMP(string name, Transform parent, string texte, Vector3 pos, Quaternion rot, Vector2 taille, Color couleur)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        var t = go.AddComponent<TextMeshPro>();
        t.text = texte;
        t.color = couleur;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = 2f;
        t.rectTransform.sizeDelta = taille;
        return t;
    }

    // Branche un AppareilLabo (marche/arrêt) sur un objet de commande ; renvoie le SerializedObject pour finir les réglages
    static SerializedObject Commande(GameObject cmd, Transform bouton, Vector3 angleBouton)
    {
        if (cmd.GetComponent<Collider>() == null) cmd.AddComponent<BoxCollider>();
        cmd.isStatic = false;
        var app = cmd.AddComponent<AppareilLabo>();
        cmd.AddComponent<FiltreProximite>();
        var so = new SerializedObject(app);
        so.FindProperty("bouton").objectReferenceValue = bouton;
        so.FindProperty("angleBouton").vector3Value = angleBouton;
        return so;
    }

    static System.Type typeGrab;
    // Rend un objet attrapable avec les manettes (XR Grab Interactable + Rigidbody + boîte de collision)
    static void Attrapable(GameObject go, Vector3 centre, Vector3 taille, float masse)
    {
        if (typeGrab == null)
            typeGrab = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit")
                    ?? System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false; // un objet qui bouge n'est pas statique
        var col = go.AddComponent<BoxCollider>();
        col.center = centre; col.size = taille;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = masse;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        if (typeGrab == null) return;
        var grab = go.AddComponent(typeGrab);
        var so = new SerializedObject(grab);
        var p = so.FindProperty("m_UseDynamicAttach");
        if (p != null) p.boolValue = true;
        // Suivi par la physique : l'objet tenu ne traverse plus les tables, les murs ni les armoires
        var mv = so.FindProperty("m_MovementType");
        if (mv != null) mv.intValue = 0; // VelocityTracking
        so.ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<FiltreProximite>();   // on ne prend l'objet que si la main est tout près
    }

    // Caisson de rangement creux avec portes qui s'ouvrent (PorteBattante). face = direction de la façade.
    // Renvoie les battants (pour y coller des étiquettes). Le contenu est ajouté par « remplir » (repère local du caisson).
    static List<Transform> Caisson(Transform parent, string name, Vector3 pos, Vector3 face, float larg, float prof, float haut,
                                   int nPortes, Material corps, int nEtageres, System.Action<Transform, float, float> remplir)
    {
        var g = Group(name, parent, pos);
        g.localRotation = Quaternion.LookRotation(face);
        float yb = 0.1f;
        Box("Fond", g, new Vector3(0f, (yb + haut) / 2f, -prof / 2f + 0.01f), new Vector3(larg, haut - yb, 0.02f), corps);
        Box("Cote_G", g, new Vector3(-larg / 2f + 0.01f, (yb + haut) / 2f, 0f), new Vector3(0.02f, haut - yb, prof), corps);
        Box("Cote_D", g, new Vector3( larg / 2f - 0.01f, (yb + haut) / 2f, 0f), new Vector3(0.02f, haut - yb, prof), corps);
        Box("Bas", g, new Vector3(0f, yb + 0.01f, 0f), new Vector3(larg, 0.02f, prof), corps);
        Box("Haut", g, new Vector3(0f, haut - 0.01f, 0f), new Vector3(larg, 0.02f, prof), corps);
        Box("Plinthe", g, new Vector3(0f, yb / 2f, prof / 2f - 0.07f), new Vector3(larg, yb, 0.02f), Noir, collider: false);
        var interieur = Mat("Interieur_Caisson", new Color(0.85f, 0.83f, 0.78f), 0.3f);
        for (int e = 1; e <= nEtageres; e++)
            Box($"Etagere_{e}", g, new Vector3(0f, yb + (haut - yb) * e / (nEtageres + 1f), 0.01f), new Vector3(larg - 0.04f, 0.018f, prof - 0.06f), interieur);
        remplir?.Invoke(g, yb + 0.02f, (haut - yb) / (nEtageres + 1f));

        var battants = new List<Transform>();
        float w = larg / nPortes;
        for (int i = 0; i < nPortes; i++)
        {
            bool gauche = nPortes == 1 || i < nPortes / 2;       // charnière à gauche ou à droite
            float xh = gauche ? -larg / 2f + i * w : -larg / 2f + (i + 1) * w;
            var pivot = Group($"Charniere_{i}", g, new Vector3(xh, 0f, prof / 2f + 0.011f));
            var b = Box($"Porte_{i}", pivot, new Vector3((gauche ? 1f : -1f) * w / 2f, (yb + haut) / 2f + 0.005f, 0f),
                        new Vector3(w - 0.006f, haut - yb - 0.02f, 0.018f), corps, isStatic: false);
            Box("Poignee", b.transform, new Vector3((gauche ? 1f : -1f) * (w / 2f - 0.06f), (haut - yb) * 0.3f, 0.02f),
                new Vector3(0.015f, 0.12f, 0.02f), Metal, collider: false, isStatic: false);
            var pb = b.AddComponent<PorteBattante>();
            b.AddComponent<FiltreProximite>();
            var so = new SerializedObject(pb);
            so.FindProperty("pivot").objectReferenceValue = pivot;
            so.FindProperty("angleOuvert").vector3Value = new Vector3(0f, gauche ? -105f : 105f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            battants.Add(b.transform);
        }
        return battants;
    }

    // Contenu varié pour un caisson : flacons, boîtes de gants, béchers empilés (quelques objets attrapables)
    static int compteurContenu;
    static void ContenuRangement(Transform g, float yBas, float hNiveau, float larg, float prof)
    {
        var boite = Mat("Boite_Gants", new Color(0.2f, 0.45f, 0.85f), 0.3f);
        var carton = Mat("Carton", new Color(0.62f, 0.48f, 0.32f), 0.2f);
        int k = compteurContenu++;
        for (int niveau = 0; niveau < 2; niveau++)
        {
            float y = yBas + niveau * hNiveau + (niveau > 0 ? 0.01f : 0f);
            float x = -larg / 2f + 0.15f;
            switch ((k + niveau) % 4)
            {
                case 0:
                    for (int f = 0; f < 3; f++) Verrerie(f % 2 == 0 ? "FlaconAmbre" : "FlaconBlanc", $"Flacon_{niveau}_{f}", g, new Vector3(x + f * 0.1f, y, 0f), null, true);
                    break;
                case 1:
                    for (int bg = 0; bg < 2; bg++)
                    {
                        var bgo = Box($"Boite_Gants_{bg}", g, new Vector3(x + 0.1f + bg * 0.27f, y + 0.065f, 0f), new Vector3(0.24f, 0.13f, 0.12f), boite, collider: false, isStatic: false);
                        Attrapable(bgo, Vector3.zero, new Vector3(0.24f, 0.13f, 0.12f), 0.15f);
                    }
                    break;
                case 2:
                    for (int f = 0; f < 3; f++) Verrerie("Becher", $"Becher_{niveau}_{f}", g, new Vector3(x + f * 0.09f, y, 0.05f), null, true);
                    break;
                default:
                    var ct = Box("Carton", g, new Vector3(x + 0.15f, y + 0.1f, 0f), new Vector3(0.3f, 0.2f, Mathf.Min(0.3f, prof - 0.1f)), carton, collider: false, isStatic: false);
                    Attrapable(ct, Vector3.zero, new Vector3(0.3f, 0.2f, Mathf.Min(0.3f, prof - 0.1f)), 0.4f);
                    break;
            }
        }
    }

    // Rangée de caissons sous une paillasse (le long de l'axe x local du parent), façade vers « face »
    static void RangeeCaissons(Transform parent, string name, Vector3 debut, Vector3 axe, float longueur, Vector3 face, float prof, Material corps)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(longueur / 1.2f));
        float w = longueur / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = debut + axe * (w * (i + 0.5f));
            Caisson(parent, $"{name}_{i}", p, face, w - 0.004f, prof, 0.88f, 2, corps, 1, (g, yb, hn) => ContenuRangement(g, yb, hn, w, prof));
        }
    }

    // Îlot de paillasse : caissons avec portes et tiroirs, dessus en résine noire.
    // Îlot de service : muret technique au centre (robinets de gaz, prises), étagère à réactifs, évier au bout est.
    static void Ilot(string name, Transform parent, Vector3 pos, float longueur, Material meuble, Material dessus, bool service)
    {
        var g = Group(name, parent, pos);
        // Deux rangées de caissons dos à dos (portes qui s'ouvrent des deux côtés), dessus continu en résine
        RangeeCaissons(g, "Caisson_Avant", new Vector3(-longueur / 2f, 0f, 0.375f), Vector3.right, longueur, Vector3.forward, 0.75f, meuble);
        RangeeCaissons(g, "Caisson_Arriere", new Vector3(-longueur / 2f, 0f, -0.375f), Vector3.right, longueur, Vector3.back, 0.75f, meuble);
        if (!service) { Box("Dessus", g, new Vector3(0f, 0.9f, 0f), new Vector3(longueur + 0.05f, 0.04f, 1.6f), dessus); return; }
        DessusAvecTrou(g, "Dessus", new Vector3(0f, 0.9f, 0f), new Vector3(longueur + 0.05f, 0.04f, 1.6f), new Vector2(longueur / 2f - 0.4f, 0f), new Vector2(0.52f, 0.42f), dessus);

        float xEvier = longueur / 2f - 0.4f;
        float xa = -longueur / 2f + 0.3f, xb = xEvier - 0.6f, lm = xb - xa;
        Box("Muret_Technique", g, new Vector3((xa + xb) / 2f, 1.12f, 0f), new Vector3(lm, 0.4f, 0.16f), GrisPanneau);
        // Étagère à réactifs sur montants, deux niveaux
        for (float x = xa; x <= xb + 0.01f; x += lm / 3f)
            Box("Montant", g, new Vector3(x, 1.55f, 0f), new Vector3(0.04f, 0.9f, 0.04f), Metal, collider: false);
        var ambre = GlassMat("Verre_Ambre", new Color(0.45f, 0.22f, 0.05f), 0.8f);
        var blanc = Mat("Plastique_Blanc", new Color(0.93f, 0.93f, 0.90f), 0.3f);
        for (int t = 0; t < 2; t++)
        {
            float y = 1.4f + t * 0.42f;
            Box($"Tablette_Reactifs_{t}", g, new Vector3((xa + xb) / 2f, y, 0f), new Vector3(lm, 0.025f, 0.3f), Blanc, collider: false);
            int n = Mathf.FloorToInt(lm / 0.32f);
            for (int k = 0; k < n; k++)
            {
                bool brun = (k + t) % 3 != 0;
                Flacon($"Flacon_{t}_{k}", g, new Vector3(xa + 0.15f + k * 0.32f, y + 0.013f, (k % 2 == 0) ? 0.06f : -0.06f),
                       0.2f + 0.04f * (k % 2), 0.08f, brun ? ambre : blanc, brun ? Liquide("Ambre", new Color(0.6f, 0.35f, 0.1f)) : null, true);
            }
        }
        // Robinets de gaz (bleus) et prises électriques des deux côtés du muret
        var gaz = Mat("Robinet_Gaz", new Color(0.1f, 0.3f, 0.8f), 0.5f);
        for (float x = xa + 0.5f; x < xb; x += 1.4f)
            foreach (float sz in new[] { 1f, -1f })
            {
                Box("Robinet_Gaz", g, new Vector3(x, 1.05f, sz * 0.1f), new Vector3(0.05f, 0.05f, 0.06f), gaz, collider: false);
                Box("Prise", g, new Vector3(x + 0.4f, 1.15f, sz * 0.082f), new Vector3(0.08f, 0.12f, 0.005f), Blanc, collider: false);
            }
        Evier("Evier", g, new Vector3(xEvier, 0.92f, 0f), Vector3.forward);
    }

    // Portes de caissons (lignes + poignées) le long d'une face de meuble
    static void PortesArmoire(Transform parent, Vector3 debut, float longueur, float largeurPorte, Vector3 normale)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(longueur / largeurPorte));
        float w = longueur / n;
        float s = normale.z;
        for (int i = 0; i < n; i++)
        {
            float x = debut.x + (i + 0.5f) * w;
            Box("Joint", parent, new Vector3(debut.x + i * w, 0.45f, debut.z), new Vector3(0.008f, 0.72f, 0.01f), Rampe, collider: false);
            Box("Tiroir_Joint", parent, new Vector3(x, 0.7f, debut.z), new Vector3(w - 0.02f, 0.008f, 0.01f), Rampe, collider: false);
            Box("Poignee_Tiroir", parent, new Vector3(x, 0.78f, debut.z + s * 0.015f), new Vector3(0.12f, 0.015f, 0.015f), Metal, collider: false);
            Box("Poignee_Porte", parent, new Vector3(x + w * 0.35f, 0.5f, debut.z + s * 0.015f), new Vector3(0.015f, 0.12f, 0.015f), Metal, collider: false);
        }
    }

    // Hotte chimique : meuble bas, plan de travail, parois, guillotine vitrée, bandeau avec moniteur de débit d'air
    static void Hotte(string name, Transform parent, Vector3 pos, Material meuble, Material dessus, Material alu, bool aVerrerie)
    {
        var h = Group(name, parent, pos);
        var hotteMat = Mat("Hotte", new Color(0.86f, 0.87f, 0.88f), 0.5f);
        const float L = 1.5f, P = 0.85f;   // largeur (z) et profondeur (x)
        Caisson(h, "Meuble_Bas", Vector3.zero, Vector3.left, L, P, 0.88f, 2, meuble, 1,
                (g, yb, hn) => { for (int f = 0; f < 3; f++) Verrerie("FlaconAmbre", $"Solvant_{f}", g, new Vector3(-0.45f + f * 0.25f, yb, 0f), null, true); });
        Box("Plan", h, new Vector3(0f, 0.9f, 0f), new Vector3(P, 0.04f, L), dessus);
        Box("Fond", h, new Vector3(P / 2f - 0.03f, 1.65f, 0f), new Vector3(0.06f, 1.5f, L), hotteMat);
        Box("Paroi_S", h, new Vector3(0f, 1.65f, -L / 2f + 0.04f), new Vector3(P, 1.5f, 0.08f), hotteMat);
        Box("Paroi_N", h, new Vector3(0f, 1.65f,  L / 2f - 0.04f), new Vector3(P, 1.5f, 0.08f), hotteMat);
        Box("Plafond", h, new Vector3(0f, 2.4f, 0f), new Vector3(P, 0.06f, L), hotteMat);
        Box("Bandeau", h, new Vector3(-P / 2f + 0.05f, 2.2f, 0f), new Vector3(0.1f, 0.35f, L), hotteMat);
        // Guillotine vitrée qui coulisse vers le haut quand on la saisit
        var guil = Box("Guillotine", h, new Vector3(-P / 2f + 0.06f, 1.55f, 0f), new Vector3(0.015f, 0.6f, L - 0.18f), Verre, isStatic: false);
        Box("Guillotine_Poignee", guil.transform, new Vector3(-0.03f, -0.3f, 0f), new Vector3(0.04f, 0.04f, L - 0.3f), alu, collider: false, isStatic: false);
        var pbg = guil.AddComponent<PorteBattante>();
        guil.AddComponent<FiltreProximite>();
        var sog = new SerializedObject(pbg);
        sog.FindProperty("angleOuvert").vector3Value = Vector3.zero;
        sog.FindProperty("glissementOuvert").vector3Value = new Vector3(0f, 0.45f, 0f);
        sog.ApplyModifiedPropertiesWithoutUndo();
        Box("Moniteur_Air", h, new Vector3(-P / 2f - 0.005f, 2.2f, 0.5f), new Vector3(0.01f, 0.1f, 0.16f), EmissiveMat("Voyant_Vert", new Color(0.2f, 1f, 0.3f), 1.5f), collider: false);
        Sign("Etiquette_Hotte", h, "HOTTE CHIMIQUE\n<size=65%>Guillotine à 45 cm max.</size>", new Vector3(-P / 2f - 0.005f, 2.2f, -0.2f),
             Quaternion.LookRotation(Vector3.right), new Vector2(0.75f, 0.22f), new Color(0.15f, 0.15f, 0.15f), null);
        if (!aVerrerie) return;
        // Montage dans la hotte : support, ballon, réfrigérant (la fuite de gaz part d'ici)
        Box("Support_Tige", h, new Vector3(0.15f, 1.3f, -0.2f), new Vector3(0.02f, 0.8f, 0.02f), Metal, collider: false);
        Box("Support_Pied", h, new Vector3(0.1f, 0.93f, -0.2f), new Vector3(0.2f, 0.02f, 0.15f), Metal, collider: false);
        Ballon("Ballon_Reaction", h, new Vector3(0.0f, 1.05f, -0.2f), 0.16f, Liquide("Jaune", new Color(0.95f, 0.85f, 0.2f)));
        Disc("Refrigerant", h, new Vector3(0.0f, 1.42f, -0.2f), 0.05f, 0.45f, Verre, false);
        Flacon("Flacon_Hotte", h, new Vector3(0.1f, 0.92f, 0.35f), 0.25f, 0.1f, GlassMat("Verre_Ambre", new Color(0.45f, 0.22f, 0.05f), 0.8f),
               Liquide("Ambre", new Color(0.6f, 0.35f, 0.1f)), true);
    }

    // Flacon de réactif (maillage de révolution) : corps à épaulement, col, bouchon, liquide, étiquette
    static void Flacon(string name, Transform parent, Vector3 basePos, float h, float d, Material corps, Material liquide, bool etiquette)
    {
        var g = Group(name, parent, basePos);
        string k = $"{h:0.000}_{d:0.000}";
        Lathe("Corps", g, Vector3.zero, "LF_" + k, V(0,0, .47f*d,0, .5f*d,.03f*h, .5f*d,.62f*h, .42f*d,.74f*h, .22f*d,.82f*h, .2f*d,.85f*h, .2f*d,.92f*h, 0,.92f*h), corps);
        Lathe("Bouchon", g, Vector3.zero, "LB_" + k, V(0,.9f*h, .26f*d,.9f*h, .26f*d,h, 0,h), Noir);
        if (liquide != null) Lathe("Liquide", g, Vector3.zero, "LL_" + k, V(0,.02f*h, .45f*d,.02f*h, .45f*d,.5f*h, 0,.5f*h), liquide);
        if (etiquette) Box("Etiquette", g, new Vector3(0f, h * 0.38f, 0f), new Vector3(d * 0.7f, h * 0.28f, d * 1.02f), Blanc, collider: false);
    }

    // Ballon à fond rond (maillage de révolution) avec col ; pos = centre de la panse
    static void Ballon(string name, Transform parent, Vector3 pos, float d, Material liquide)
    {
        var g = Group(name, parent, pos);
        float R = d / 2f;
        var prof = new List<Vector2>();
        foreach (float a in new[] { -90f, -70f, -45f, -20f, 0f, 20f, 40f, 58f })
            prof.Add(new Vector2(R * Mathf.Cos(a * Mathf.Deg2Rad), R * Mathf.Sin(a * Mathf.Deg2Rad)));
        prof.Add(new Vector2(0.24f * d, 0.5f * d)); prof.Add(new Vector2(0.24f * d, 0.95f * d)); prof.Add(new Vector2(0.3f * d, 0.98f * d));
        prof[0] = new Vector2(0f, -R);
        Lathe("Verre", g, Vector3.zero, $"LBal_{d:0.000}", prof.ToArray(), Verre);
        if (liquide != null)
            Lathe("Liquide", g, Vector3.zero, $"LBalL_{d:0.000}", V(0,-R*0.95f, R*0.6f,-R*0.78f, R*0.88f,-R*0.4f, R*0.92f,-R*0.2f, 0,-R*0.2f), liquide);
    }

    static void Tabouret(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent, pos);
        Disc("Base", g, new Vector3(0f, 0.02f, 0f), 0.45f, 0.04f, Noir, false);
        Disc("Pied", g, new Vector3(0f, 0.32f, 0f), 0.05f, 0.6f, Metal, false);
        Disc("Repose_Pieds", g, new Vector3(0f, 0.25f, 0f), 0.4f, 0.02f, Metal, false);
        Disc("Assise", g, new Vector3(0f, 0.65f, 0f), 0.36f, 0.07f, Mat("Assise_Noire", new Color(0.1f, 0.1f, 0.12f), 0.4f), true);
    }

    // Évier de laboratoire en inox encastré dans le plan de travail (le plan est découpé par DessusAvecTrou) :
    // cuve creuse, rebord, bonde, robinet col de cygne chromé, manettes « coude » ; la manette droite ouvre l'eau.
    static void Evier(string name, Transform parent, Vector3 pos, Vector3 dos)
    {
        var e = Group(name, parent, pos);
        var inox = Mat("Inox", new Color(0.70f, 0.71f, 0.72f), 0.85f);
        var chrome = Mat("Chrome", new Color(0.82f, 0.83f, 0.85f), 0.95f);
        float s = dos.z >= 0 ? 1f : -1f;          // côté où se trouve l'utilisateur (+z local)
        const float W = 0.5f, D = 0.4f, P = 0.22f;
        Box("Fond_Cuve", e, new Vector3(0f, -P, 0f), new Vector3(W + 0.02f, 0.01f, D + 0.02f), inox);
        Box("Paroi_G", e, new Vector3(-W / 2f - 0.005f, -P / 2f, 0f), new Vector3(0.01f, P, D + 0.02f), inox);
        Box("Paroi_D", e, new Vector3( W / 2f + 0.005f, -P / 2f, 0f), new Vector3(0.01f, P, D + 0.02f), inox);
        Box("Paroi_Av", e, new Vector3(0f, -P / 2f, D / 2f + 0.005f), new Vector3(W, P, 0.01f), inox);
        Box("Paroi_Ar", e, new Vector3(0f, -P / 2f, -D / 2f - 0.005f), new Vector3(W, P, 0.01f), inox);
        Box("Rebord_Av", e, new Vector3(0f, 0.002f, D / 2f + 0.03f), new Vector3(W + 0.1f, 0.004f, 0.05f), inox, collider: false);
        Box("Rebord_Ar", e, new Vector3(0f, 0.002f, -D / 2f - 0.03f), new Vector3(W + 0.1f, 0.004f, 0.05f), inox, collider: false);
        Box("Rebord_G", e, new Vector3(-W / 2f - 0.03f, 0.002f, 0f), new Vector3(0.05f, 0.004f, D), inox, collider: false);
        Box("Rebord_D", e, new Vector3( W / 2f + 0.03f, 0.002f, 0f), new Vector3(0.05f, 0.004f, D), inox, collider: false);
        Disc("Bonde", e, new Vector3(0f, -P + 0.007f, 0.05f * s), 0.07f, 0.004f, GrisPanneau, false);
        Disc("Grille_Bonde", e, new Vector3(0f, -P + 0.009f, 0.05f * s), 0.05f, 0.002f, Noir, false);

        // Robinet col de cygne, côté opposé à l'utilisateur
        // Les panneaux du caisson sous l'évier qui traverseraient la cuve sont retirés
        var cuve = new Bounds(e.position + Vector3.down * (P / 2f), new Vector3(W + 0.02f, P, D + 0.02f));
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || (t.name != "Haut" && t.name != "Fond" && t.name != "Cote_G" && t.name != "Cote_D")) continue;
            var r = t.GetComponent<Renderer>();
            if (r != null && r.bounds.Intersects(cuve)) Object.DestroyImmediate(t.gameObject);
        }

        float fz = -s * (D / 2f + 0.07f);
        Disc("Rosace", e, new Vector3(0f, 0.005f, fz), 0.07f, 0.01f, chrome, false);
        Disc("Colonne", e, new Vector3(0f, 0.17f, fz), 0.026f, 0.33f, chrome, false);
        const float R = 0.1f;
        Vector3 prec = new Vector3(0f, 0.33f, fz);
        for (int k = 1; k <= 10; k++)
        {
            float th = Mathf.PI * (1f - k / 10f);
            var q = new Vector3(0f, 0.33f + R * Mathf.Sin(th), fz + s * (R + R * Mathf.Cos(th)));
            var seg = Disc("Col_Cygne", e, (prec + q) / 2f, 0.022f, (q - prec).magnitude + 0.004f, chrome, false);
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, q - prec);
            prec = q;
        }
        Vector3 bec = prec + Vector3.down * 0.04f;
        Disc("Bec", e, prec + Vector3.down * 0.02f, 0.024f, 0.04f, chrome, false);
        Disc("Embout_Dentele", e, bec + Vector3.down * 0.012f, 0.018f, 0.025f, Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), false);

        var eauMat = GlassMat("Eau", new Color(0.72f, 0.86f, 1f), 0.5f);
        float yBas = -P + 0.01f;

        // Manettes « coude » : chaude à gauche, froide à droite ; chacune ouvre son eau
        foreach (float sx in new[] { -1f, 1f })
        {
            var pivot = Group(sx < 0 ? "Manette_Chaude" : "Manette_Froide", e, new Vector3(sx * 0.07f, 0.12f, fz));
            Disc("Moyeu", pivot, Vector3.zero, 0.03f, 0.03f, chrome, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lame = Box("Lame", pivot, new Vector3(0f, 0.055f, s * 0.02f), new Vector3(0.018f, 0.11f, 0.012f), chrome, isStatic: false);
            Box("Pastille", lame.transform, new Vector3(0f, 0.045f, s * 0.007f), new Vector3(0.016f, 0.016f, 0.003f),
                sx < 0 ? Rouge : Mat("Bleu_Robinet", new Color(0.1f, 0.3f, 0.85f), 0.5f), collider: false, isStatic: false);
            var filet = Disc(sx < 0 ? "Filet_Eau_Chaude" : "Filet_Eau_Froide", e, new Vector3(sx * 0.002f, (bec.y - 0.025f + yBas) / 2f, bec.z), 0.012f, bec.y - 0.025f - yBas, eauMat, false);
            var flaque = Disc("Eclaboussure", e, new Vector3(0f, yBas + 0.002f, bec.z), 0.09f, 0.003f, eauMat, false);
            filet.isStatic = false; flaque.isStatic = false;
            var son = Source(lame, Clip("Eau_Robinet"), true, false, 0.35f, 1f, 6f);
            var act = lame.AddComponent<EauActivable>();
            lame.AddComponent<FiltreProximite>();
            var so = new SerializedObject(act);
            so.FindProperty("poignee").objectReferenceValue = pivot;
            so.FindProperty("angleOuvert").vector3Value = new Vector3(0f, 0f, sx < 0 ? 90f : -90f);
            SetArray(so, "eau", new List<Object> { filet, flaque });
            so.FindProperty("son").objectReferenceValue = son;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Plan de travail avec une découpe rectangulaire (pour l'évier) : 4 morceaux autour du trou
    static void DessusAvecTrou(Transform parent, string name, Vector3 c, Vector3 taille, Vector2 trouC, Vector2 trouT, Material m)
    {
        float x0 = c.x - taille.x / 2f, x1 = c.x + taille.x / 2f, z0 = c.z - taille.z / 2f, z1 = c.z + taille.z / 2f;
        float hx0 = trouC.x - trouT.x / 2f, hx1 = trouC.x + trouT.x / 2f, hz0 = trouC.y - trouT.y / 2f, hz1 = trouC.y + trouT.y / 2f;
        Piece(parent, name + "_G", x0, hx0, z0, z1, c.y, taille.y, m);
        Piece(parent, name + "_D", hx1, x1, z0, z1, c.y, taille.y, m);
        Piece(parent, name + "_Av", hx0, hx1, hz1, z1, c.y, taille.y, m);
        Piece(parent, name + "_Ar", hx0, hx1, z0, hz0, c.y, taille.y, m);
    }

    static void Piece(Transform parent, string n, float a0, float a1, float b0, float b1, float y, float ep, Material m)
    {
        if (a1 - a0 < 0.001f || b1 - b0 < 0.001f) return;
        Box(n, parent, new Vector3((a0 + a1) / 2f, y, (b0 + b1) / 2f), new Vector3(a1 - a0, ep, b1 - b0), m);
    }

    // Douche d'urgence et lave-yeux combinés (modèle courant en labo) : conduite jaune, pomme de douche,
    // tirette à triangle, cuvette du lave-yeux avec deux têtes et palette à pousser. Les deux fonctionnent.
    static void DoucheUrgence(Transform lab, Vector3 pos)
    {
        var dou = Group("Douche_Securite", lab, pos);
        var jaune = Mat("Jaune_Securite", new Color(0.98f, 0.80f, 0.05f), 0.5f);
        var vert = Mat("Vert_LaveYeux", new Color(0.05f, 0.55f, 0.25f), 0.5f);
        var chrome = Mat("Chrome", new Color(0.82f, 0.83f, 0.85f), 0.95f);
        var eauMat = GlassMat("Eau", new Color(0.72f, 0.86f, 1f), 0.5f);
        var rideauMat = GlassMat("Eau_Douche", new Color(0.75f, 0.88f, 1f), 0.22f);
        float xp = -0.25f;                                   // conduite contre le mur
        Disc("Platine_Sol", dou, new Vector3(xp, 0.01f, 0f), 0.16f, 0.02f, jaune, false);
        Disc("Conduite", dou, new Vector3(xp, 1.15f, 0f), 0.048f, 2.3f, jaune, true);
        var bras = Disc("Bras_Douche", dou, new Vector3(xp + 0.225f, 2.28f, 0f), 0.04f, 0.45f, jaune, false);
        bras.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Disc("Coude", dou, new Vector3(xp, 2.28f, 0f), 0.06f, 0.06f, jaune, false);
        Disc("Raccord_Pomme", dou, new Vector3(0.2f, 2.22f, 0f), 0.05f, 0.1f, jaune, false);
        Lathe("Pomme_Douche", dou, new Vector3(0.2f, 2.07f, 0f), "L_Pomme_Douche", V(0,0.1f, 0.03f,0.1f, 0.13f,0.02f, 0.145f,0f, 0.145f,-0.012f, 0,-0.012f), jaune);
        // Tirette rigide et poignée triangulaire (on la tire vers le bas : la douche coule)
        Disc("Tige_Tirette", dou, new Vector3(0.02f, 1.95f, 0f), 0.012f, 0.58f, chrome, false);
        var tir = Group("Tirette", dou, new Vector3(0.02f, 1.62f, 0f));
        tir.gameObject.isStatic = false;
        var b1 = Box("Triangle_Bas", tir, new Vector3(0f, -0.1f, 0f), new Vector3(0.16f, 0.015f, 0.015f), jaune, collider: false, isStatic: false);
        var b2 = Box("Triangle_G", tir, new Vector3(-0.04f, -0.05f, 0f), new Vector3(0.015f, 0.12f, 0.015f), jaune, collider: false, isStatic: false, rot: Quaternion.Euler(0f, 0f, -35f));
        var b3 = Box("Triangle_D", tir, new Vector3(0.04f, -0.05f, 0f), new Vector3(0.015f, 0.12f, 0.015f), jaune, collider: false, isStatic: false, rot: Quaternion.Euler(0f, 0f, 35f));
        var colT = tir.gameObject.AddComponent<BoxCollider>();
        colT.center = new Vector3(0f, -0.06f, 0f); colT.size = new Vector3(0.18f, 0.14f, 0.05f);
        var rideau = Lathe("Eau_Douche", dou, new Vector3(0.2f, 0f, 0f), "L_Rideau_Douche", V(0.42f,0.02f, 0.12f,2.05f), rideauMat);
        rideau.isStatic = false;
        Disc("Grille_Sol", dou, new Vector3(0.2f, 0.004f, 0f), 0.25f, 0.006f, chrome, false);
        var sonDouche = Source(rideau, Clip("Douche"), true, false, 0.5f, 1f, 10f);
        var actD = tir.gameObject.AddComponent<EauActivable>();
        tir.gameObject.AddComponent<FiltreProximite>();
        var soD = new SerializedObject(actD);
        soD.FindProperty("poignee").objectReferenceValue = tir;
        soD.FindProperty("angleOuvert").vector3Value = Vector3.zero;
        soD.FindProperty("glissementOuvert").vector3Value = new Vector3(0f, -0.08f, 0f);
        SetArray(soD, "eau", new List<Object> { rideau });
        soD.FindProperty("son").objectReferenceValue = sonDouche;
        soD.ApplyModifiedPropertiesWithoutUndo();

        // Lave-yeux : bras, cuvette (double face pour voir l'intérieur), deux têtes avec capuchons, palette verte
        var brasLY = Disc("Bras_LaveYeux", dou, new Vector3(xp + 0.12f, 0.93f, 0f), 0.035f, 0.24f, jaune, false);
        brasLY.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Disc("Pied_Cuvette", dou, new Vector3(0.2f, 0.9f, 0f), 0.05f, 0.06f, jaune, false);
        var prof = V(0,0, 0.03f,0, 0.15f,0.07f, 0.165f,0.085f, 0.165f,0.095f);
        Lathe("Cuvette", dou, new Vector3(0.2f, 0.93f, 0f), "L_Cuvette_LY", prof, vert);
        var inv = new Vector2[prof.Length]; for (int k = 0; k < prof.Length; k++) inv[k] = prof[prof.Length - 1 - k];
        Lathe("Cuvette_Interieur", dou, new Vector3(0.2f, 0.93f, 0f), "L_Cuvette_LY_Int", inv, vert);
        var jets = new List<Object>();
        foreach (float sz in new[] { -1f, 1f })
        {
            Disc("Tete", dou, new Vector3(0.2f, 0.98f, sz * 0.05f), 0.035f, 0.04f, jaune, false);
            Disc("Capuchon", dou, new Vector3(0.2f, 1.005f, sz * 0.05f), 0.04f, 0.012f, Mat("Capuchon_Orange", new Color(0.95f, 0.45f, 0.05f), 0.4f), false);
            var jet = Disc("Jet", dou, new Vector3(0.2f, 1.07f, sz * 0.035f), 0.008f, 0.13f, eauMat, false);
            jet.transform.localRotation = Quaternion.Euler(sz * 15f, 0f, 0f);
            jet.isStatic = false;
            jets.Add(jet);
        }
        var piv = Group("Palette_Pivot", dou, new Vector3(0.39f, 0.95f, 0f));
        var palette = Box("Palette", piv, new Vector3(0f, 0.08f, 0f), new Vector3(0.015f, 0.16f, 0.12f), vert, isStatic: false);
        Sign("Palette_Texte", palette.transform, "POUSSER", new Vector3(0.009f, 0f, 0f), Quaternion.LookRotation(Vector3.left),
             new Vector2(0.1f, 0.04f), Color.white, null);
        var sonLY = Source(palette, Clip("Eau_Robinet"), true, false, 0.35f, 1f, 6f);
        var actL = palette.AddComponent<EauActivable>();
        palette.AddComponent<FiltreProximite>();
        var soL = new SerializedObject(actL);
        soL.FindProperty("poignee").objectReferenceValue = piv;
        soL.FindProperty("angleOuvert").vector3Value = new Vector3(0f, 0f, 40f);
        SetArray(soL, "eau", jets);
        soL.FindProperty("son").objectReferenceValue = sonLY;
        soL.ApplyModifiedPropertiesWithoutUndo();
    }

    // Support à tubes à essai (attrapable en entier) avec 6 tubes à fond rond et des liquides de couleur
    static void RackTubes(Transform parent, string name, Vector3 pos)
    {
        var g = Group(name, parent, pos).gameObject;
        var t = g.transform;
        var plast = Mat("Plastique_Blanc", new Color(0.93f, 0.93f, 0.90f), 0.3f);
        Box("Socle", t, new Vector3(0f, 0.005f, 0f), new Vector3(0.24f, 0.01f, 0.07f), plast, collider: false);
        Box("Plaque_Haute", t, new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.008f, 0.07f), plast, collider: false);
        Box("Plaque_Milieu", t, new Vector3(0f, 0.045f, 0f), new Vector3(0.24f, 0.006f, 0.07f), plast, collider: false);
        Box("Flanc_G", t, new Vector3(-0.115f, 0.05f, 0f), new Vector3(0.01f, 0.1f, 0.07f), plast, collider: false);
        Box("Flanc_D", t, new Vector3(0.115f, 0.05f, 0f), new Vector3(0.01f, 0.1f, 0.07f), plast, collider: false);
        Color[] cl = { new Color(0.9f, 0.2f, 0.2f), new Color(0.95f, 0.6f, 0.1f), new Color(0.95f, 0.9f, 0.2f), new Color(0.3f, 0.8f, 0.35f), new Color(0.2f, 0.45f, 0.9f), new Color(0.55f, 0.25f, 0.75f) };
        for (int k = 0; k < 6; k++)
        {
            float x = -0.09f + k * 0.036f;
            Disc($"Trou_{k}", t, new Vector3(x, 0.0945f, 0f), 0.02f, 0.002f, Noir, false);
            var tube = Group($"Tube_{k}", t, new Vector3(x, 0.012f, 0f)).gameObject;
            Lathe("Verre", tube.transform, Vector3.zero, "L_Tube", V(0,0, 0.004f,0.0006f, 0.007f,0.003f, 0.0085f,0.008f, 0.0085f,0.15f, 0.0095f,0.152f), Verre);
            var lm = Liquide($"Tube_{k}", cl[k]);
            var liq = Lathe("Liquide", tube.transform, Vector3.zero, "L_Tube_Liq", V(0,0.002f, 0.0065f,0.004f, 0.0075f,0.008f, 0.0075f,0.05f, 0,0.05f), lm);
            liq.isStatic = false;
            AjouterVersable(tube, liq.transform, 0.152f, lm, 0.7f);
        }
        Attrapable(g, new Vector3(0f, 0.05f, 0f), new Vector3(0.24f, 0.1f, 0.07f), 0.3f);
    }

    // Balance analytique : socle, afficheur incliné, plateau, cage vitrée avec portes coulissantes
    static void Balance(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent, pos);
        var gris = Mat("Gris_Clair", new Color(0.86f, 0.87f, 0.88f), 0.5f);
        Box("Socle", g, new Vector3(0f, 0.04f, 0.02f), new Vector3(0.22f, 0.08f, 0.34f), gris);
        Box("Pupitre", g, new Vector3(0f, 0.045f, -0.17f), new Vector3(0.2f, 0.05f, 0.08f), gris, collider: false, rot: Quaternion.Euler(-25f, 0f, 0f));
        Box("Ecran", g, new Vector3(0f, 0.071f, -0.18f), new Vector3(0.1f, 0.03f, 0.002f), Mat("Ecran_LCD", new Color(0.55f, 0.7f, 0.55f), 0.6f), collider: false, rot: Quaternion.Euler(-25f, 0f, 0f));
        var txt = TexteTMP("Affichage_Masse", g, "0.000 g", new Vector3(0f, 0.0715f, -0.1815f), Quaternion.Euler(65f, 0f, 0f), new Vector2(0.095f, 0.026f), new Color(0.05f, 0.1f, 0.05f));
        Disc("Pied_Plateau", g, new Vector3(0f, 0.09f, 0.04f), 0.02f, 0.02f, Metal, false);
        Disc("Plateau", g, new Vector3(0f, 0.102f, 0.04f), 0.09f, 0.004f, Metal, false);
        ColliderOnly("Collision_Plateau", g, new Vector3(0f, 0.102f, 0.04f), new Vector3(0.09f, 0.004f, 0.09f), Quaternion.identity);
        var bal = g.gameObject.AddComponent<BalanceLabo>();
        var sob = new SerializedObject(bal);
        sob.FindProperty("affichage").objectReferenceValue = txt;
        sob.ApplyModifiedPropertiesWithoutUndo();
        Box("Cage_Gauche", g, new Vector3(-0.105f, 0.23f, 0.04f), new Vector3(0.005f, 0.3f, 0.24f), Verre, collider: false);
        Box("Cage_Droite", g, new Vector3(0.105f, 0.23f, 0.04f), new Vector3(0.005f, 0.3f, 0.24f), Verre, collider: false);
        Box("Cage_Fond", g, new Vector3(0f, 0.23f, 0.16f), new Vector3(0.21f, 0.3f, 0.005f), Verre, collider: false);
        // Porte avant de la cage qui coulisse (on l'ouvre pour poser l'objet sur le plateau)
        var porteCage = Box("Cage_Avant", g, new Vector3(0f, 0.23f, -0.08f), new Vector3(0.21f, 0.3f, 0.008f), Verre, isStatic: false);
        Box("Poignee_Porte", porteCage.transform, new Vector3(0.08f, 0f, -0.008f), new Vector3(0.01f, 0.06f, 0.01f), Noir, collider: false, isStatic: false);
        var pbc = porteCage.AddComponent<PorteBattante>();
        porteCage.AddComponent<FiltreProximite>();
        var sop = new SerializedObject(pbc);
        sop.FindProperty("angleOuvert").vector3Value = Vector3.zero;
        sop.FindProperty("glissementOuvert").vector3Value = new Vector3(0f, 0.27f, 0f);
        sop.ApplyModifiedPropertiesWithoutUndo();
        Box("Cage_Toit", g, new Vector3(0f, 0.385f, 0.04f), new Vector3(0.22f, 0.012f, 0.25f), gris);
        Box("Poignee_G", g, new Vector3(-0.112f, 0.3f, 0.0f), new Vector3(0.008f, 0.06f, 0.015f), Noir, collider: false);
        Box("Poignee_D", g, new Vector3(0.112f, 0.3f, 0.0f), new Vector3(0.008f, 0.06f, 0.015f), Noir, collider: false);
    }

    // Plaque chauffante agitatrice : corps blanc, plaque céramique, 2 boutons, afficheur, bécher avec barreau aimanté
    static void PlaqueAgitatrice(string name, Transform parent, Vector3 pos, Material liquide)
    {
        var g = Group(name, parent, pos);
        var blanc = Mat("Blanc", new Color(0.96f, 0.96f, 0.96f), 0.6f);
        Box("Corps", g, new Vector3(0f, 0.045f, 0f), new Vector3(0.18f, 0.09f, 0.26f), blanc);
        Box("Facade", g, new Vector3(0f, 0.045f, -0.131f), new Vector3(0.17f, 0.07f, 0.004f), Mat("Gris_Fonce", new Color(0.25f, 0.27f, 0.3f), 0.5f), collider: false);
        Disc("Plaque_Ceramique", g, new Vector3(0f, 0.095f, 0.02f), 0.16f, 0.01f, Mat("Ceramique", new Color(0.92f, 0.92f, 0.88f), 0.7f), false);
        Box("Afficheur", g, new Vector3(0f, 0.065f, -0.134f), new Vector3(0.05f, 0.018f, 0.002f), Noir, collider: false);
        var temp = TexteTMP("Affichage_Temperature", g, "", new Vector3(0f, 0.065f, -0.1365f), Quaternion.identity, new Vector2(0.048f, 0.016f), new Color(1f, 0.25f, 0.1f));
        Verrerie("Becher", "Becher", g, new Vector3(0f, 0.1f, 0.02f), liquide, false);
        var barreau = Box("Barreau_Aimante", g, new Vector3(0f, 0.108f, 0.02f), new Vector3(0.025f, 0.006f, 0.006f), Blanc, collider: false, isStatic: false);
        var lueur = Disc("Lueur_Chauffe", g, new Vector3(0f, 0.1005f, 0.02f), 0.155f, 0.001f, EmissiveMat("Lueur_Rouge", new Color(1f, 0.25f, 0.05f), 1.2f), false);
        lueur.isStatic = false;
        var sonMoteur = Source(g.gameObject, Clip("Hotte_Aspiration"), true, false, 0.06f, 1f, 3f);
        // Bouton gauche : agitation (le barreau aimanté tourne) ; bouton droit : chauffage (la plaque rougit, la température monte)
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? -0.05f : 0.05f;
            var b = Disc(k == 0 ? "Bouton_Agitation" : "Bouton_Chauffage", g, new Vector3(sx, 0.04f, -0.14f), 0.035f, 0.02f, Noir, false);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Repere", b.transform, new Vector3(0f, 0.011f, 0.012f), new Vector3(0.004f, 0.002f, 0.012f), Blanc, collider: false, isStatic: false);
            var so = Commande(b, b.transform, new Vector3(0f, 135f, 0f));
            if (k == 0)
            {
                SetArray(so, "rotors", new List<Object> { barreau.transform });
                so.FindProperty("son").objectReferenceValue = sonMoteur;
            }
            else
            {
                SetArray(so, "actifs", new List<Object> { lueur });
                so.FindProperty("affichage").objectReferenceValue = temp;
                so.FindProperty("format").stringValue = "{0:0} °C";
                so.FindProperty("valeurDepart").floatValue = 22f;
                so.FindProperty("valeurCible").floatValue = 120f;
                so.FindProperty("vitesseValeur").floatValue = 4f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // pH-mètre de paillasse : boîtier, écran, bras porte-électrode, électrode en verre dans un bécher de tampon
    static void PhMetre(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent, pos);
        Box("Boitier", g, new Vector3(0f, 0.035f, 0f), new Vector3(0.18f, 0.07f, 0.2f), Mat("Gris_Fonce", new Color(0.25f, 0.27f, 0.3f), 0.5f));
        Box("Ecran", g, new Vector3(0f, 0.071f, -0.02f), new Vector3(0.11f, 0.002f, 0.07f), Mat("Ecran_LCD", new Color(0.55f, 0.7f, 0.55f), 0.6f), collider: false);
        var ph = TexteTMP("Affichage_pH", g, "", new Vector3(0f, 0.0725f, -0.02f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.1f, 0.05f), new Color(0.05f, 0.15f, 0.05f));
        var touche = Box("Touche_Mesure", g, new Vector3(0f, 0.074f, -0.08f), new Vector3(0.05f, 0.008f, 0.025f), Mat("Touche_Verte", new Color(0.1f, 0.6f, 0.25f), 0.4f), isStatic: false);
        Sign("Touche_Texte", touche.transform, "MESURE", new Vector3(0f, 0.0045f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(0.045f, 0.015f), Color.white, null);
        var soPh = Commande(touche, touche.transform, Vector3.zero);
        soPh.FindProperty("affichage").objectReferenceValue = ph;
        soPh.FindProperty("texteArret").stringValue = "";
        soPh.FindProperty("texteMarche").stringValue = "pH 7,01\n<size=50%>25,0 °C</size>";
        soPh.ApplyModifiedPropertiesWithoutUndo();
        Disc("Tige", g, new Vector3(0.14f, 0.2f, 0.05f), 0.012f, 0.4f, Metal, false);
        Box("Bras", g, new Vector3(0.1f, 0.3f, 0.05f), new Vector3(0.09f, 0.015f, 0.02f), Mat("Gris_Fonce", new Color(0.25f, 0.27f, 0.3f), 0.5f), collider: false);
        Lathe("Electrode", g, new Vector3(0.06f, 0.06f, 0.05f), "L_Electrode", V(0,0, 0.006f,0.002f, 0.007f,0.01f, 0.005f,0.02f, 0.006f,0.24f, 0,0.24f), Verre);
        Verrerie("Becher", "Becher_Tampon", g, new Vector3(0.06f, 0.0f, 0.05f), Liquide("Tampon", new Color(0.95f, 0.85f, 0.4f)), false);
    }

    // Dessiccateur en verre (cuve, plaque en porcelaine, couvercle à bouton)
    static void Dessiccateur(Transform parent, Vector3 pos)
    {
        var g = Group("Dessiccateur", parent, pos);
        Lathe("Cuve", g, Vector3.zero, "L_Dessic_Cuve", V(0,0, 0.1f,0, 0.13f,0.05f, 0.14f,0.14f, 0.165f,0.15f, 0.165f,0.16f), Verre);
        Disc("Plaque_Porcelaine", g, new Vector3(0f, 0.08f, 0f), 0.25f, 0.008f, Blanc, false);
        Lathe("Gel_Silice", g, Vector3.zero, "L_Dessic_Gel", V(0,0.005f, 0.1f,0.005f, 0.12f,0.04f, 0,0.04f), Liquide("Silice", new Color(0.35f, 0.5f, 0.95f)));
        Lathe("Couvercle", g, Vector3.zero, "L_Dessic_Couv", V(0.165f,0.16f, 0.16f,0.19f, 0.09f,0.25f, 0.025f,0.26f, 0.025f,0.29f, 0.02f,0.3f, 0,0.3f), Verre);
    }

    // ------------------------------------------------------------------ ÉQUIPEMENT COMPLET DU LABO (appareils qui fonctionnent)
    // Repère local de chaque appareil : façade vers -z (on le tourne de 180° pour le côté opposé d'un îlot).
    static Transform Appareil(Transform parent, string name, Vector3 pos, bool faceNord)
    {
        var g = Group(name, parent, pos);
        if (faceNord) g.localRotation = Quaternion.Euler(0f, 180f, 0f);
        return g;
    }

    static Material GrisClair => Mat("Gris_Clair", new Color(0.86f, 0.87f, 0.88f), 0.5f);
    static Material GrisFonce => Mat("Gris_Fonce", new Color(0.25f, 0.27f, 0.3f), 0.5f);
    static Material Ecran => Mat("Ecran_LCD", new Color(0.55f, 0.7f, 0.55f), 0.6f);

    // Microscope optique : socle, potence, platine, tourelle, binoculaire ; l'interrupteur allume la lampe
    static void Microscope(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Microscope", pos, faceNord);
        Box("Socle", g, new Vector3(0f, 0.025f, 0.02f), new Vector3(0.18f, 0.05f, 0.24f), GrisClair);
        Box("Potence", g, new Vector3(0f, 0.2f, 0.1f), new Vector3(0.06f, 0.32f, 0.06f), GrisClair, collider: false, rot: Quaternion.Euler(-12f, 0f, 0f));
        Box("Platine", g, new Vector3(0f, 0.14f, -0.01f), new Vector3(0.14f, 0.012f, 0.12f), Noir, collider: false);
        Box("Lame", g, new Vector3(0f, 0.148f, -0.01f), new Vector3(0.075f, 0.002f, 0.025f), Verre, collider: false);
        Disc("Tourelle", g, new Vector3(0f, 0.23f, 0.0f), 0.07f, 0.03f, GrisFonce, false);
        foreach (float sx in new[] { -0.015f, 0.015f })
            Disc("Objectif", g, new Vector3(sx, 0.195f, -0.005f), 0.014f, 0.05f, Metal, false);
        foreach (float sx in new[] { -0.022f, 0.022f })
        {
            var oc = Disc("Oculaire", g, new Vector3(sx, 0.33f, -0.03f), 0.025f, 0.1f, Noir, false);
            oc.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        }
        foreach (float sx in new[] { -0.045f, 0.045f })
            Disc("Molette", g, new Vector3(sx, 0.12f, 0.08f), 0.05f, 0.015f, Noir, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var lampe = Disc("Lampe", g, new Vector3(0f, 0.053f, -0.01f), 0.03f, 0.004f, EmissiveMat("Lampe_Microscope", new Color(1f, 0.95f, 0.8f), 3f), false);
        lampe.isStatic = false;
        var inter = Box("Interrupteur", g, new Vector3(0.08f, 0.025f, -0.08f), new Vector3(0.02f, 0.02f, 0.03f), Noir, isStatic: false);
        var so = Commande(inter, inter.transform, new Vector3(0f, 0f, 20f));
        SetArray(so, "actifs", new List<Object> { lampe });
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Centrifugeuse de paillasse : couvercle qui s'ouvre, rotor avec tubes, bouton START, affichage tr/min
    static void Centrifugeuse(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Centrifugeuse", pos, faceNord);
        Box("Corps", g, new Vector3(0f, 0.11f, 0f), new Vector3(0.38f, 0.22f, 0.42f), Blanc);
        Box("Cuve", g, new Vector3(0f, 0.2f, 0.02f), new Vector3(0.28f, 0.04f, 0.28f), GrisFonce, collider: false);
        var rotor = Group("Rotor", g, new Vector3(0f, 0.21f, 0.02f));
        rotor.gameObject.isStatic = false;
        Disc("Plateau_Rotor", rotor, Vector3.zero, 0.22f, 0.03f, Metal, false).isStatic = false;
        for (int k = 0; k < 6; k++)
        {
            var tb = Disc($"Tube_{k}", rotor, Dir(k * 60f) * 0.08f + Vector3.up * 0.02f, 0.02f, 0.05f, Mat("Plastique_Blanc", new Color(0.93f, 0.93f, 0.90f), 0.3f), false);
            tb.isStatic = false;
        }
        var charn = Group("Charniere", g, new Vector3(0f, 0.22f, 0.2f));
        var couv = Box("Couvercle", charn, new Vector3(0f, 0.015f, -0.2f), new Vector3(0.36f, 0.03f, 0.4f), Mat("Couvercle_Gris", new Color(0.7f, 0.72f, 0.75f), 0.6f), isStatic: false);
        Box("Hublot", couv.transform, new Vector3(0f, 0.016f, 0.02f), new Vector3(0.16f, 0.002f, 0.16f), Verre, collider: false, isStatic: false);
        var pb = couv.AddComponent<PorteBattante>();
        couv.AddComponent<FiltreProximite>();
        var sop = new SerializedObject(pb);
        sop.FindProperty("pivot").objectReferenceValue = charn;
        sop.FindProperty("angleOuvert").vector3Value = new Vector3(80f, 0f, 0f);
        sop.ApplyModifiedPropertiesWithoutUndo();
        Box("Panneau", g, new Vector3(0f, 0.1f, -0.211f), new Vector3(0.3f, 0.12f, 0.004f), GrisFonce, collider: false);
        var aff = TexteTMP("Affichage", g, "0 tr/min", new Vector3(-0.05f, 0.12f, -0.214f), Quaternion.identity, new Vector2(0.14f, 0.04f), new Color(0.3f, 1f, 0.4f));
        var start = Box("Bouton_Start", g, new Vector3(0.1f, 0.1f, -0.216f), new Vector3(0.05f, 0.03f, 0.01f), Mat("Touche_Verte", new Color(0.1f, 0.6f, 0.25f), 0.4f), isStatic: false);
        Sign("Start_Texte", start.transform, "START", new Vector3(0f, 0f, -0.006f), Quaternion.identity, new Vector2(0.045f, 0.02f), Color.white, null);
        var son = Source(g.gameObject, Clip("Hotte_Aspiration"), true, false, 0.12f, 1f, 4f);
        var so = Commande(start, start.transform, Vector3.zero);
        SetArray(so, "rotors", new List<Object> { rotor });
        so.FindProperty("degresParSeconde").floatValue = 1440f;
        so.FindProperty("son").objectReferenceValue = son;
        so.FindProperty("affichage").objectReferenceValue = aff;
        so.FindProperty("texteArret").stringValue = "0 tr/min";
        so.FindProperty("format").stringValue = "{0:0} tr/min";
        so.FindProperty("valeurDepart").floatValue = 0f;
        so.FindProperty("valeurCible").floatValue = 4000f;
        so.FindProperty("vitesseValeur").floatValue = 800f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Spectrophotomètre UV-Visible : trappe à cuvette, écran, bouton MESURE
    static void Spectro(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Spectrophotometre", pos, faceNord);
        Box("Corps", g, new Vector3(0f, 0.08f, 0f), new Vector3(0.42f, 0.16f, 0.34f), GrisClair);
        Box("Ecran", g, new Vector3(-0.08f, 0.13f, -0.171f), new Vector3(0.18f, 0.08f, 0.004f), Ecran, collider: false);
        var aff = TexteTMP("Affichage", g, "Prêt", new Vector3(-0.08f, 0.13f, -0.174f), Quaternion.identity, new Vector2(0.17f, 0.07f), new Color(0.05f, 0.15f, 0.05f));
        var charn = Group("Charniere_Trappe", g, new Vector3(0.11f, 0.16f, 0.06f));
        var trappe = Box("Trappe_Cuvette", charn, new Vector3(0f, 0.008f, -0.06f), new Vector3(0.12f, 0.016f, 0.12f), GrisFonce, isStatic: false);
        var pb = trappe.AddComponent<PorteBattante>();
        trappe.AddComponent<FiltreProximite>();
        var sop = new SerializedObject(pb);
        sop.FindProperty("pivot").objectReferenceValue = charn;
        sop.FindProperty("angleOuvert").vector3Value = new Vector3(100f, 0f, 0f);
        sop.ApplyModifiedPropertiesWithoutUndo();
        Box("Cuvette_Quartz", g, new Vector3(0.11f, 0.15f, 0f), new Vector3(0.012f, 0.045f, 0.012f), Verre, collider: false);
        var bouton = Box("Bouton_Mesure", g, new Vector3(0.1f, 0.06f, -0.172f), new Vector3(0.06f, 0.025f, 0.01f), Mat("Touche_Verte", new Color(0.1f, 0.6f, 0.25f), 0.4f), isStatic: false);
        Sign("Mesure_Texte", bouton.transform, "MESURE", new Vector3(0f, 0f, -0.006f), Quaternion.identity, new Vector2(0.055f, 0.02f), Color.white, null);
        var so = Commande(bouton, bouton.transform, Vector3.zero);
        so.FindProperty("affichage").objectReferenceValue = aff;
        so.FindProperty("texteArret").stringValue = "Prêt";
        so.FindProperty("texteMarche").stringValue = "A = 0,452\n<size=60%>λ = 540 nm</size>";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Agitateur vortex : on touche la coupelle, elle tourne (et le tube posé dessus)
    static void Vortex(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Agitateur_Vortex", pos, faceNord);
        Lathe("Socle", g, Vector3.zero, "L_Vortex", V(0,0, .07f,0, .075f,.01f, .06f,.08f, .03f,.09f, 0,.09f), Blanc);
        var coupelle = Disc("Coupelle", g, new Vector3(0f, 0.1f, 0f), 0.05f, 0.02f, Noir, false);
        var so = Commande(coupelle, null, Vector3.zero);
        SetArray(so, "rotors", new List<Object> { coupelle.transform });
        so.FindProperty("degresParSeconde").floatValue = 2000f;
        so.FindProperty("son").objectReferenceValue = Source(g.gameObject, Clip("Hotte_Aspiration"), true, false, 0.08f, 1f, 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Bain-marie thermostaté : cuve inox avec eau, bouton, affichage de la température
    static void BainMarie(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Bain_Marie", pos, faceNord);
        var inox = Mat("Inox", new Color(0.70f, 0.71f, 0.72f), 0.85f);
        Box("Cuve_Fond", g, new Vector3(0f, 0.01f, 0f), new Vector3(0.36f, 0.02f, 0.28f), inox);
        Box("Paroi_Av", g, new Vector3(0f, 0.08f, -0.14f), new Vector3(0.36f, 0.16f, 0.01f), inox);
        Box("Paroi_Ar", g, new Vector3(0f, 0.08f, 0.14f), new Vector3(0.36f, 0.16f, 0.01f), inox);
        Box("Paroi_G", g, new Vector3(-0.18f, 0.08f, 0f), new Vector3(0.01f, 0.16f, 0.28f), inox);
        Box("Paroi_D", g, new Vector3(0.18f, 0.08f, 0f), new Vector3(0.01f, 0.16f, 0.28f), inox);
        Box("Eau", g, new Vector3(0f, 0.07f, 0f), new Vector3(0.35f, 0.12f, 0.27f), Liquide("Eau_Bain", new Color(0.7f, 0.85f, 1f)), collider: false);
        Box("Boitier", g, new Vector3(0f, 0.08f, -0.17f), new Vector3(0.2f, 0.1f, 0.05f), GrisFonce, collider: false);
        var aff = TexteTMP("Affichage", g, "", new Vector3(-0.04f, 0.09f, -0.196f), Quaternion.identity, new Vector2(0.08f, 0.03f), new Color(1f, 0.3f, 0.1f));
        var bouton = Disc("Bouton", g, new Vector3(0.06f, 0.08f, -0.2f), 0.035f, 0.02f, Noir, false);
        bouton.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var so = Commande(bouton, bouton.transform, new Vector3(0f, 120f, 0f));
        so.FindProperty("affichage").objectReferenceValue = aff;
        so.FindProperty("valeurDepart").floatValue = 22f;
        so.FindProperty("valeurCible").floatValue = 37f;
        so.FindProperty("vitesseValeur").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Bec Bunsen relié au robinet de gaz : on tourne la molette, la flamme bleue s'allume (avec son)
    static void Bunsen(Transform parent, string name, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, name, pos, faceNord);
        Lathe("Pied", g, Vector3.zero, "L_Bunsen_Pied", V(0,0, .045f,0, .045f,.012f, .012f,.02f, 0,.02f), GrisFonce);
        Lathe("Tube", g, Vector3.zero, "L_Bunsen_Tube", V(.009f,.02f, .009f,.14f, .01f,.145f), Metal);
        Disc("Bague_Air", g, new Vector3(0f, 0.04f, 0f), 0.022f, 0.02f, Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), false);
        var tuyau = Disc("Tuyau_Gaz", g, new Vector3(0f, 0.03f, 0.08f), 0.012f, 0.16f, Mat("Tuyau_Orange", new Color(0.9f, 0.45f, 0.1f), 0.4f), false);
        tuyau.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var flamme = Lathe("Flamme", g, new Vector3(0f, 0.145f, 0f), "L_Flamme", V(0.009f,0, 0.012f,0.03f, 0.007f,0.07f, 0,0.1f), GlassMat("Flamme_Bleue", new Color(0.3f, 0.5f, 1f), 0.55f));
        var coeur = Lathe("Coeur_Flamme", g, new Vector3(0f, 0.145f, 0f), "L_Flamme_Coeur", V(0.006f,0, 0.006f,0.02f, 0,0.04f), EmissiveMat("Flamme_Coeur", new Color(0.4f, 0.7f, 1f), 3f));
        flamme.isStatic = false; coeur.isStatic = false;
        var molette = Disc("Molette_Gaz", g, new Vector3(0.03f, 0.01f, 0f), 0.025f, 0.012f, Noir, false);
        molette.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        var so = Commande(molette, molette.transform, new Vector3(0f, 90f, 0f));
        SetArray(so, "actifs", new List<Object> { flamme, coeur });
        so.FindProperty("son").objectReferenceValue = Source(g.gameObject, Clip("Gaz_Sifflement"), true, false, 0.06f, 1f, 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Montage de titrage : statif, pince, burette graduée avec robinet (on l'ouvre : ça goutte), erlenmeyer, carreau blanc
    static void Titrage(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Montage_Titrage", pos, faceNord);
        Box("Socle_Statif", g, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.2f, 0.02f, 0.3f), GrisFonce);
        Disc("Tige_Statif", g, new Vector3(0f, 0.38f, 0.15f), 0.014f, 0.74f, Metal, false);
        Box("Pince", g, new Vector3(0f, 0.55f, 0.08f), new Vector3(0.03f, 0.03f, 0.14f), Metal, collider: false);
        Lathe("Burette", g, new Vector3(0f, 0.3f, 0.02f), "L_Burette", V(0.004f,0, 0.006f,0.03f, 0.007f,0.05f, 0.007f,0.5f, 0.008f,0.51f), Verre);
        Lathe("Liquide_Burette", g, new Vector3(0f, 0.3f, 0.02f), "L_Burette_Liq", V(0,0.05f, 0.0062f,0.05f, 0.0062f,0.42f, 0,0.42f), Liquide("Soude", new Color(0.85f, 0.92f, 1f)));
        Box("Graduations", g, new Vector3(0f, 0.55f, 0.012f), new Vector3(0.002f, 0.4f, 0.001f), Noir, collider: false);
        Box("Carreau_Blanc", g, new Vector3(0f, 0.025f, 0.02f), new Vector3(0.15f, 0.01f, 0.15f), Blanc, collider: false);
        Verrerie("Erlenmeyer", "Erlenmeyer_Titrage", g, new Vector3(0f, 0.03f, 0.02f), Liquide("Rose", new Color(0.95f, 0.35f, 0.6f)), false);
        var goutte = Disc("Goutte", g, new Vector3(0f, 0.22f, 0.02f), 0.003f, 0.14f, GlassMat("Eau", new Color(0.72f, 0.86f, 1f), 0.5f), false);
        goutte.isStatic = false;
        var robinet = Box("Robinet_Burette", g, new Vector3(0f, 0.32f, 0.02f), new Vector3(0.04f, 0.008f, 0.008f), Mat("Robinet_Bleu", new Color(0.1f, 0.3f, 0.85f), 0.5f), isStatic: false);
        var so = Commande(robinet, robinet.transform, new Vector3(0f, 90f, 0f));
        SetArray(so, "actifs", new List<Object> { goutte });
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Étuve de séchage : porte vitrée qui s'ouvre, grilles, lampe intérieure, interrupteur et température qui monte à 105 °C
    static void Etuve(Transform parent, Vector3 pos, bool faceNord = false)
    {
        var g = Appareil(parent, "Etuve", pos, faceNord);
        const float L = 0.55f, H = 0.5f, P = 0.48f;
        Box("Fond", g, new Vector3(0f, H / 2f, P / 2f - 0.01f), new Vector3(L, H, 0.02f), GrisClair);
        Box("Cote_G", g, new Vector3(-L / 2f + 0.01f, H / 2f, 0f), new Vector3(0.02f, H, P), GrisClair);
        Box("Cote_D", g, new Vector3(L / 2f - 0.01f, H / 2f, 0f), new Vector3(0.02f, H, P), GrisClair);
        Box("Dessus", g, new Vector3(0f, H - 0.01f, 0f), new Vector3(L, 0.02f, P), GrisClair);
        Box("Bas", g, new Vector3(0f, 0.01f, 0f), new Vector3(L, 0.02f, P), GrisClair);
        Box("Panneau_Commande", g, new Vector3(0f, H + 0.05f, -0.02f), new Vector3(L, 0.1f, P - 0.04f), GrisFonce);
        foreach (float y in new[] { 0.17f, 0.33f })
            Box("Grille", g, new Vector3(0f, y, 0f), new Vector3(L - 0.05f, 0.006f, P - 0.06f), Metal, collider: true);
        Verrerie("Becher", "Becher_Sechage", g, new Vector3(-0.1f, 0.173f, 0f), null, true);
        var lampe = Disc("Lampe_Interieure", g, new Vector3(0f, H - 0.025f, 0f), 0.05f, 0.01f, EmissiveMat("Lampe_Etuve", new Color(1f, 0.85f, 0.6f), 2f), false);
        lampe.isStatic = false;
        var charn = Group("Charniere_Porte", g, new Vector3(-L / 2f, 0f, -P / 2f - 0.012f));
        var porte = Box("Porte", charn, new Vector3(L / 2f, H / 2f, 0f), new Vector3(L, H, 0.02f), GrisClair, isStatic: false);
        Box("Vitre", porte.transform, new Vector3(0f, 0f, -0.011f), new Vector3(L - 0.16f, H - 0.16f, 0.004f), VerreArme, collider: false, isStatic: false);
        Box("Poignee", porte.transform, new Vector3(L / 2f - 0.05f, 0f, -0.025f), new Vector3(0.02f, 0.15f, 0.03f), Metal, collider: false, isStatic: false);
        var pb = porte.AddComponent<PorteBattante>();
        porte.AddComponent<FiltreProximite>();
        var sop = new SerializedObject(pb);
        sop.FindProperty("pivot").objectReferenceValue = charn;
        sop.FindProperty("angleOuvert").vector3Value = new Vector3(0f, 100f, 0f);
        sop.ApplyModifiedPropertiesWithoutUndo();
        var aff = TexteTMP("Affichage", g, "", new Vector3(-0.1f, H + 0.05f, -P / 2f + 0.015f), Quaternion.identity, new Vector2(0.12f, 0.04f), new Color(1f, 0.3f, 0.1f));
        var inter = Box("Interrupteur", g, new Vector3(0.15f, H + 0.05f, -P / 2f + 0.012f), new Vector3(0.03f, 0.04f, 0.01f), Mat("Interrupteur_Rouge", new Color(0.85f, 0.1f, 0.1f), 0.4f), isStatic: false);
        var so = Commande(inter, inter.transform, new Vector3(25f, 0f, 0f));
        SetArray(so, "actifs", new List<Object> { lampe });
        so.FindProperty("affichage").objectReferenceValue = aff;
        so.FindProperty("valeurDepart").floatValue = 22f;
        so.FindProperty("valeurCible").floatValue = 105f;
        so.FindProperty("vitesseValeur").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }


    // Porte-pipettes avec 6 pipettes graduées et une poire à pipeter (propipette)
    static void PortePipettes(Transform parent, Vector3 pos)
    {
        var g = Group("Porte_Pipettes", parent, pos);
        Box("Socle", g, new Vector3(0f, 0.01f, 0f), new Vector3(0.18f, 0.02f, 0.1f), Blanc);
        Box("Support", g, new Vector3(0f, 0.12f, 0.03f), new Vector3(0.18f, 0.01f, 0.03f), Blanc, collider: false);
        Box("Montant", g, new Vector3(0f, 0.065f, 0.04f), new Vector3(0.012f, 0.11f, 0.012f), Blanc, collider: false);
        for (int k = 0; k < 6; k++)
        {
            var pip = Lathe($"Pipette_{k}", g, new Vector3(-0.07f + k * 0.028f, 0.02f, 0.03f), "L_Pipette", V(0.001f,0, 0.003f,0.02f, 0.003f,0.3f, 0.0035f,0.31f), Verre);
            pip.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        }
        var poire = Group("Poire_Pipeter", g, new Vector3(0.12f, 0.0f, -0.02f)).gameObject;
        Lathe("Poire", poire.transform, Vector3.zero, "L_Poire", V(0,0, 0.02f,0.01f, 0.03f,0.04f, 0.02f,0.07f, 0.006f,0.08f, 0.006f,0.11f, 0,0.11f), Mat("Caoutchouc_Rouge", new Color(0.75f, 0.1f, 0.08f), 0.3f));
        Attrapable(poire, new Vector3(0f, 0.055f, 0f), new Vector3(0.06f, 0.11f, 0.06f), 0.05f);
    }

    // Distributeurs au-dessus de l'évier : essuie-mains et savon
    static void Distributeurs(Transform parent, Vector3 posPapier, Vector3 posSavon)
    {
        var g = Group("Distributeurs", parent, Vector3.zero);
        Box("Essuie_Mains", g, posPapier, new Vector3(0.3f, 0.36f, 0.12f), Blanc, collider: false);
        Box("Fente_Papier", g, posPapier + new Vector3(0f, -0.17f, 0.062f), new Vector3(0.22f, 0.01f, 0.004f), Noir, collider: false);
        Box("Papier", g, posPapier + new Vector3(0f, -0.21f, 0.064f), new Vector3(0.2f, 0.07f, 0.002f), Mat("Papier_Brun", new Color(0.78f, 0.68f, 0.52f), 0.2f), collider: false);
        Box("Savon", g, posSavon, new Vector3(0.12f, 0.2f, 0.1f), Blanc, collider: false);
        Box("Savon_Fenetre", g, posSavon + new Vector3(0f, 0.02f, 0.052f), new Vector3(0.06f, 0.08f, 0.002f), Liquide("Savon", new Color(0.55f, 0.85f, 0.6f)), collider: false);
        Box("Savon_Poussoir", g, posSavon + new Vector3(0f, -0.08f, 0.055f), new Vector3(0.06f, 0.03f, 0.02f), Noir, collider: false);
    }



    // ------------------------------------------------------------------ POSTE INFORMATIQUE (vrai clavier, souris, écran qui s'allume)
    // Touches du clavier (x, y, largeur, hauteur en « unités » de 19 mm ; y = rangée depuis le haut). Même disposition que Clavier.png.
    static readonly float[] TouchesClavier = { 0f,0f,1f,1f, 2f,0f,1f,1f, 3f,0f,1f,1f, 4f,0f,1f,1f, 5f,0f,1f,1f, 6.5f,0f,1f,1f, 7.5f,0f,1f,1f, 8.5f,0f,1f,1f, 9.5f,0f,1f,1f, 11f,0f,1f,1f, 12f,0f,1f,1f, 13f,0f,1f,1f, 14f,0f,1f,1f, 15.5f,0f,1f,1f, 16.5f,0f,1f,1f, 17.5f,0f,1f,1f, 0f,1.5f,1f,1f, 1f,1.5f,1f,1f, 2f,1.5f,1f,1f, 3f,1.5f,1f,1f, 4f,1.5f,1f,1f, 5f,1.5f,1f,1f, 6f,1.5f,1f,1f, 7f,1.5f,1f,1f, 8f,1.5f,1f,1f, 9f,1.5f,1f,1f, 10f,1.5f,1f,1f, 11f,1.5f,1f,1f, 12f,1.5f,1f,1f, 13f,1.5f,2f,1f, 15.5f,1.5f,1f,1f, 16.5f,1.5f,1f,1f, 17.5f,1.5f,1f,1f, 19f,1.5f,1f,1f, 20f,1.5f,1f,1f, 21f,1.5f,1f,1f, 22f,1.5f,1f,1f, 0f,2.5f,1.5f,1f, 1.5f,2.5f,1f,1f, 2.5f,2.5f,1f,1f, 3.5f,2.5f,1f,1f, 4.5f,2.5f,1f,1f, 5.5f,2.5f,1f,1f, 6.5f,2.5f,1f,1f, 7.5f,2.5f,1f,1f, 8.5f,2.5f,1f,1f, 9.5f,2.5f,1f,1f, 10.5f,2.5f,1f,1f, 11.5f,2.5f,1f,1f, 12.5f,2.5f,1f,1f, 13.5f,2.5f,1.5f,1f, 15.5f,2.5f,1f,1f, 16.5f,2.5f,1f,1f, 17.5f,2.5f,1f,1f, 19f,2.5f,1f,1f, 20f,2.5f,1f,1f, 21f,2.5f,1f,1f, 22f,2.5f,1f,2f, 0f,3.5f,1.75f,1f, 1.75f,3.5f,1f,1f, 2.75f,3.5f,1f,1f, 3.75f,3.5f,1f,1f, 4.75f,3.5f,1f,1f, 5.75f,3.5f,1f,1f, 6.75f,3.5f,1f,1f, 7.75f,3.5f,1f,1f, 8.75f,3.5f,1f,1f, 9.75f,3.5f,1f,1f, 10.75f,3.5f,1f,1f, 11.75f,3.5f,1f,1f, 12.75f,3.5f,2.25f,1f, 19f,3.5f,1f,1f, 20f,3.5f,1f,1f, 21f,3.5f,1f,1f, 0f,4.5f,2.25f,1f, 2.25f,4.5f,1f,1f, 3.25f,4.5f,1f,1f, 4.25f,4.5f,1f,1f, 5.25f,4.5f,1f,1f, 6.25f,4.5f,1f,1f, 7.25f,4.5f,1f,1f, 8.25f,4.5f,1f,1f, 9.25f,4.5f,1f,1f, 10.25f,4.5f,1f,1f, 11.25f,4.5f,1f,1f, 12.25f,4.5f,2.75f,1f, 16.5f,4.5f,1f,1f, 19f,4.5f,1f,1f, 20f,4.5f,1f,1f, 21f,4.5f,1f,1f, 22f,4.5f,1f,2f, 0f,5.5f,1.25f,1f, 1.25f,5.5f,1.25f,1f, 2.5f,5.5f,1.25f,1f, 3.75f,5.5f,6.25f,1f, 10f,5.5f,1.25f,1f, 11.25f,5.5f,1.25f,1f, 12.5f,5.5f,1.25f,1f, 13.75f,5.5f,1.25f,1f, 15.5f,5.5f,1f,1f, 16.5f,5.5f,1f,1f, 17.5f,5.5f,1f,1f, 19f,5.5f,2f,1f, 21f,5.5f,1f,1f };

    // Un seul maillage pour les 104 touches : dessus avec la lettre (texture), côtés en pente comme de vraies touches
    static Mesh ClavierMesh()
    {
        const string key = "Clavier_Touches";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        const float u = 0.019f, gB = 0.0007f, gT = 0.0024f, yB = 0.0f, yT = 0.0085f;
        System.Func<float, float, Vector2> UVp = (x, z) => new Vector2((0.5f + x / u + 11.5f) / 24f, 1f - (0.5f + (3.25f - z / u)) / 7.5f);
        Vector2 uvCote = new Vector2(0.994f, 0.006f);
        for (int k = 0; k < TouchesClavier.Length; k += 4)
        {
            float x = TouchesClavier[k], y = TouchesClavier[k + 1], w = TouchesClavier[k + 2], h = TouchesClavier[k + 3];
            float x0 = (x - 11.5f) * u, x1 = (x + w - 11.5f) * u, z1 = (3.25f - y) * u, z0 = (3.25f - y - h) * u;
            Vector3 b00 = new Vector3(x0 + gB, yB, z0 + gB), b10 = new Vector3(x1 - gB, yB, z0 + gB), b11 = new Vector3(x1 - gB, yB, z1 - gB), b01 = new Vector3(x0 + gB, yB, z1 - gB);
            Vector3 t00 = new Vector3(x0 + gT, yT, z0 + gT), t10 = new Vector3(x1 - gT, yT, z0 + gT), t11 = new Vector3(x1 - gT, yT, z1 - gT), t01 = new Vector3(x0 + gT, yT, z1 - gT);
            md.Quad(t00, t01, t11, t10, Vector3.up, UVp(t00.x, t00.z), UVp(t01.x, t01.z), UVp(t11.x, t11.z), UVp(t10.x, t10.z));
            md.Quad(b00, t00, t10, b10, new Vector3(0f, 0.3f, -1f), uvCote, uvCote, uvCote, uvCote);
            md.Quad(b11, t11, t01, b01, new Vector3(0f, 0.3f, 1f), uvCote, uvCote, uvCote, uvCote);
            md.Quad(b01, t01, t00, b00, new Vector3(-1f, 0.3f, 0f), uvCote, uvCote, uvCote, uvCote);
            md.Quad(b10, t10, t11, b11, new Vector3(1f, 0.3f, 0f), uvCote, uvCote, uvCote, uvCote);
        }
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
    }

    // Image qui éclaire (écran allumé) : texture + émission de la même image
    static Material EcranMat(string tex, float w, float h, float intensite)
    {
        var m = TexMat(tex, w, h);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", m.mainTexture);
        m.SetColor("_EmissionColor", Color.white * intensite);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Ellipsoide(string name, Transform parent, Vector3 pos, Vector3 taille, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = taille;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // Poste informatique : écran 24 po sur pied, mini-PC derrière, vrai clavier (104 touches en relief), souris sans fil,
    // tapis. L'écran affiche l'écran de verrouillage avec l'heure réelle ; on touche la souris ou le clavier :
    // écran de connexion, le mot de passe se tape, puis le bureau (logiciel du spectro ouvert). Retour en veille après 90 s.
    static void PosteInformatique(Transform parent, Vector3 pos)
    {
        var g = Group("Poste_Informatique", parent, pos);
        var plast = Mat("Plastique_Noir_Mat", new Color(0.05f, 0.05f, 0.06f), 0.3f);
        var alu = Mat("Alu_Brosse", new Color(0.62f, 0.63f, 0.65f), 0.7f);
        const float EW = 0.527f, EH = 0.296f;

        // Écran
        Box("Pied", g, new Vector3(0f, 0.005f, 0.14f), new Vector3(0.25f, 0.01f, 0.18f), alu);
        Box("Colonne", g, new Vector3(0f, 0.15f, 0.2f), new Vector3(0.05f, 0.29f, 0.022f), alu, collider: false);
        var ecran = Group("Ecran", g, new Vector3(0f, 0.32f, 0.17f));
        ecran.localRotation = Quaternion.Euler(6f, 0f, 0f);
        Box("Coque", ecran, new Vector3(0f, 0f, 0.012f), new Vector3(0.542f, 0.322f, 0.02f), plast);
        Box("Bordure", ecran, new Vector3(0f, 0f, 0.0f), new Vector3(0.542f, 0.322f, 0.004f), Noir, collider: false);
        Box("Bandeau_Bas", ecran, new Vector3(0f, -0.152f, -0.002f), new Vector3(0.542f, 0.018f, 0.004f), plast, collider: false);
        var dalle = Box("Dalle", ecran, new Vector3(0f, 0.005f, -0.0025f), new Vector3(EW, EH, 0.002f), EcranMat("Ecran_Verrouille", EW, EH, 0.9f), isStatic: false);
        var led = Box("Voyant", ecran, new Vector3(0.24f, -0.152f, -0.0045f), new Vector3(0.006f, 0.003f, 0.001f), EmissiveMat("Voyant_Blanc", Color.white, 2f), collider: false);
        Box("Mini_PC", ecran, new Vector3(0f, 0f, 0.04f), new Vector3(0.18f, 0.18f, 0.035f), plast, collider: false);
        Box("Mini_PC_Voyant", ecran, new Vector3(0.08f, 0.08f, 0.058f), new Vector3(0.006f, 0.006f, 0.001f), EmissiveMat("Voyant_Bleu", new Color(0.2f, 0.5f, 1f), 3f), collider: false);
        Tube("Cable_Ecran", g, new Vector3(0.02f, 0.25f, 0.21f), new Vector3(0.02f, 0.005f, 0.23f), 0.006f, Noir);

        // Textes en direct (heure, date, mot de passe, heure de la barre des tâches)
        Color blanc = new Color(1f, 1f, 1f);
        var grpV = Group("Texte_Verrouille", ecran, new Vector3(0f, 0f, -0.004f));
        var heure = TexteTMP("Heure", grpV, "10:42", new Vector3(0f, 0.075f, 0f), Quaternion.identity, new Vector2(0.2f, 0.075f), blanc);
        var date = TexteTMP("Date", grpV, "mardi 24 novembre", new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector2(0.2f, 0.02f), blanc);
        var aide = TexteTMP("Aide", grpV, "Touchez la souris ou le clavier", new Vector3(0f, -0.12f, 0f), Quaternion.identity, new Vector2(0.18f, 0.012f), new Color(1f, 1f, 1f, 0.8f));
        aide.fontStyle = FontStyles.Normal; date.fontStyle = FontStyles.Normal;
        var grpC = Group("Texte_Connexion", ecran, new Vector3(0f, 0f, -0.004f));
        var mdp = TexteTMP("Mot_De_Passe", grpC, "", new Vector3(-0.012f, -0.04f, 0f), Quaternion.identity, new Vector2(0.11f, 0.014f), blanc);
        var grpB = Group("Texte_Bureau", ecran, new Vector3(0f, 0f, -0.004f));
        var hb = TexteTMP("Heure_Barre", grpB, "10:42", new Vector3(0.235f, -0.135f, 0f), Quaternion.identity, new Vector2(0.045f, 0.016f), blanc);
        hb.fontStyle = FontStyles.Normal;

        // Clavier : socle incliné (pieds), 104 touches en relief, câble vers le mini-PC
        var cl = Group("Clavier", g, new Vector3(-0.04f, 0f, -0.17f));
        cl.localRotation = Quaternion.Euler(-3f, 0f, 0f);
        Box("Socle", cl, new Vector3(0f, 0.008f, 0f), new Vector3(0.456f, 0.016f, 0.1425f), plast);
        Box("Cadre", cl, new Vector3(0f, 0.0165f, 0f), new Vector3(0.446f, 0.001f, 0.1325f), Mat("Clavier_Fond", new Color(0.09f, 0.09f, 0.1f), 0.2f), collider: false);
        var touches = MeshGO("Touches", cl, ClavierMesh(), Mat("Clavier", Color.white, 0.35f, "Clavier", 1f), new Vector3(0f, 0.0165f, 0f), Quaternion.identity);
        PetitObjet(touches);
        Tube("Cable_Clavier_1", g, new Vector3(-0.04f, 0.012f, -0.098f), new Vector3(-0.03f, 0.004f, 0.02f), 0.004f, Noir);
        Tube("Cable_Clavier_2", g, new Vector3(-0.03f, 0.004f, 0.02f), new Vector3(0.0f, 0.004f, 0.2f), 0.004f, Noir);

        // Souris sans fil et tapis
        Box("Tapis_Souris", g, new Vector3(0.31f, 0.0015f, -0.16f), new Vector3(0.24f, 0.003f, 0.2f), Mat("Tapis_Souris", new Color(0.07f, 0.08f, 0.1f), 0.1f), collider: false);
        var souris = Group("Souris", g, new Vector3(0.31f, 0.003f, -0.165f));
        souris.localRotation = Quaternion.Euler(0f, -8f, 0f);
        var corps = Ellipsoide("Corps", souris, new Vector3(0f, 0f, 0f), new Vector3(0.064f, 0.072f, 0.118f), plast);
        Box("Separation_Boutons", souris, new Vector3(0f, 0.031f, 0.03f), new Vector3(0.0015f, 0.006f, 0.05f), Noir, collider: false,
            rot: Quaternion.Euler(-14f, 0f, 0f));
        Box("Separation_G", souris, new Vector3(-0.02f, 0.025f, 0.012f), new Vector3(0.024f, 0.006f, 0.0012f), Noir, collider: false);
        Box("Separation_D", souris, new Vector3(0.02f, 0.025f, 0.012f), new Vector3(0.024f, 0.006f, 0.0012f), Noir, collider: false);
        var molette = Disc("Molette", souris, new Vector3(0f, 0.033f, 0.03f), 0.02f, 0.007f, Mat("Caoutchouc", new Color(0.15f, 0.15f, 0.16f), 0.1f), false);
        molette.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Interactions : souris, clavier et écran réveillent le poste
        var so = new SerializedObject(g.gameObject.AddComponent<EcranOrdinateur>());
        so.FindProperty("dalle").objectReferenceValue = dalle.GetComponent<Renderer>();
        so.FindProperty("matVerrouille").objectReferenceValue = EcranMat("Ecran_Verrouille", EW, EH, 0.9f);
        so.FindProperty("matConnexion").objectReferenceValue = EcranMat("Ecran_Connexion", EW, EH, 0.9f);
        so.FindProperty("matBureau").objectReferenceValue = EcranMat("Ecran_Bureau", EW, EH, 0.9f);
        so.FindProperty("heure").objectReferenceValue = heure;
        so.FindProperty("date").objectReferenceValue = date;
        so.FindProperty("motDePasse").objectReferenceValue = mdp;
        so.FindProperty("heureBarre").objectReferenceValue = hb;
        so.FindProperty("groupeVerrou").objectReferenceValue = grpV.gameObject;
        so.FindProperty("groupeConnexion").objectReferenceValue = grpC.gameObject;
        so.FindProperty("groupeBureau").objectReferenceValue = grpB.gameObject;
        so.FindProperty("sonTouche").objectReferenceValue = Source(g.gameObject, Clip("Clavier_Touche"), false, false, 0.5f, 1f, 3f);
        so.ApplyModifiedPropertiesWithoutUndo();
        var ecranScript = g.GetComponent<EcranOrdinateur>();
        foreach (var cmd in new[] { corps, cl.Find("Socle").gameObject, dalle })
        {
            cmd.isStatic = false;
            if (cmd.GetComponent<Collider>() == null) cmd.AddComponent<BoxCollider>();
            var t = cmd.AddComponent<ToucheOrdinateur>();
            var st = new SerializedObject(t);
            st.FindProperty("ecran").objectReferenceValue = ecranScript;
            st.ApplyModifiedPropertiesWithoutUndo();
            cmd.AddComponent<FiltreProximite>();
        }
    }

    static Material Gris(float v) => Mat($"Gris_{v:0.00}", new Color(v, v, v), 0.4f);

    // Étiquette courbe collée sur un seau (morceau de cône), image entière de gauche à droite vue de face (+z)
    static Mesh EtiquetteCourbeMesh(float r0, float r1, float y0, float y1, float angle)
    {
        string key = $"EtiqC{r0:0.000}_{r1:0.000}_{y0:0.00}_{y1:0.00}_{angle:0}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        const int seg = 12;
        for (int k = 0; k < seg; k++)
        {
            float a0 = 90f - angle / 2f + angle * k / seg, a1 = 90f - angle / 2f + angle * (k + 1) / seg;
            Vector3 p00 = Dir(a0) * r0 + Vector3.up * y0, p10 = Dir(a1) * r0 + Vector3.up * y0;
            Vector3 p01 = Dir(a0) * r1 + Vector3.up * y1, p11 = Dir(a1) * r1 + Vector3.up * y1;
            Vector3 n = Dir((a0 + a1) / 2f);
            float u0 = (float)k / seg, u1 = (float)(k + 1) / seg;
            md.Quad(p00, p01, p11, p10, n, new Vector2(u0, 0f), new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f));
        }
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
    }

    // Couvercle / rabat / tiroir qui s'ouvre quand on le touche (PorteBattante)
    static void Ouvrant(GameObject go, Transform pivot, Vector3 angle, Vector3 glissement)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
        if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        var pb = go.AddComponent<PorteBattante>();
        go.AddComponent<FiltreProximite>();
        var so = new SerializedObject(pb);
        so.FindProperty("pivot").objectReferenceValue = pivot;
        so.FindProperty("angleOuvert").vector3Value = angle;
        so.FindProperty("glissementOuvert").vector3Value = glissement;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Boîte à verre cassé (comme les vraies boîtes en carton des labos) et trousse de déversement (seau jaune à couvercle)
    static void Securite(Transform parent, Vector3 posVerre, Vector3 posTrousse)
    {
        // ---- Boîte « VERRE CASSÉ » : carton blanc, sac plastique replié sur les bords, deux rabats qui s'ouvrent,
        //      débris de verre au fond, étiquettes sur la face et le côté
        var bv = Group("Boite_Verre_Casse", parent, posVerre);
        var carton = Mat("Carton_Blanc", new Color(0.93f, 0.93f, 0.91f), 0.15f);
        var sac = GlassMat("Sac_Plastique", new Color(0.55f, 0.57f, 0.6f), 0.55f);
        const float L = 0.32f, Hb = 0.66f, e = 0.008f;
        Box("Fond", bv, new Vector3(0f, 0.005f, 0f), new Vector3(L, 0.01f, L), carton);
        Box("Face", bv, new Vector3(0f, Hb / 2f, L / 2f - e / 2f), new Vector3(L, Hb, e), carton);
        Box("Dos", bv, new Vector3(0f, Hb / 2f, -L / 2f + e / 2f), new Vector3(L, Hb, e), carton);
        Box("Cote_G", bv, new Vector3(-L / 2f + e / 2f, Hb / 2f, 0f), new Vector3(e, Hb, L), carton);
        Box("Cote_D", bv, new Vector3(L / 2f - e / 2f, Hb / 2f, 0f), new Vector3(e, Hb, L), carton);
        Box("Etiquette_Face", bv, new Vector3(0f, 0.36f, L / 2f + 0.001f), new Vector3(0.3f, 0.46f, 0.002f), TexMat("Etiquette_Verre_Casse", 0.3f, 0.46f), collider: false);
        Box("Etiquette_Cote", bv, new Vector3(L / 2f + 0.001f, 0.36f, 0f), new Vector3(0.3f, 0.46f, 0.002f), TexMat("Etiquette_Verre_Casse", 0.3f, 0.46f), collider: false,
            rot: Quaternion.Euler(0f, 90f, 0f));
        // sac : doublure intérieure + rebord replié par-dessus le carton
        Box("Sac_Interieur", bv, new Vector3(0f, Hb / 2f + 0.02f, 0f), new Vector3(L - 0.02f, Hb - 0.02f, L - 0.02f), sac, collider: false);
        Vector3[] rbP = { new Vector3(0f, Hb - 0.03f, L / 2f + 0.003f), new Vector3(0f, Hb - 0.03f, -L / 2f - 0.003f),
                          new Vector3(-L / 2f - 0.003f, Hb - 0.03f, 0f), new Vector3(L / 2f + 0.003f, Hb - 0.03f, 0f) };
        Vector3[] rbS = { new Vector3(L + 0.01f, 0.07f, 0.003f), new Vector3(L + 0.01f, 0.07f, 0.003f),
                          new Vector3(0.003f, 0.07f, L + 0.01f), new Vector3(0.003f, 0.07f, L + 0.01f) };
        for (int k = 0; k < 4; k++) Box("Sac_Rebord", bv, rbP[k], rbS[k], sac, collider: false);
        // débris de verre au fond
        var verre = GlassMat("Verre_Casse", new Color(0.85f, 0.92f, 0.95f), 0.45f);
        var rnd = new System.Random(12);
        for (int k = 0; k < 16; k++)
        {
            float fx = (float)rnd.NextDouble() - 0.5f, fz = (float)rnd.NextDouble() - 0.5f;
            Box($"Eclat_{k}", bv, new Vector3(fx * 0.24f, 0.015f + (float)rnd.NextDouble() * 0.04f, fz * 0.24f),
                new Vector3(0.02f + (float)rnd.NextDouble() * 0.05f, 0.002f, 0.01f + (float)rnd.NextDouble() * 0.03f), verre, collider: false,
                rot: Quaternion.Euler((float)rnd.NextDouble() * 60f - 30f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 60f - 30f));
        }
        Lathe("Tube_Casse", bv, new Vector3(0.05f, 0.03f, -0.04f), "L_Tube_Casse", V(0.007f, 0, 0.008f, 0.005f, 0.008f, 0.07f, 0.0075f, 0.075f), verre)
            .transform.localRotation = Quaternion.Euler(80f, 30f, 0f);
        // deux rabats sur le dessus (on les touche : ils s'ouvrent)
        foreach (float sx in new[] { -1f, 1f })
        {
            var piv = Group(sx < 0 ? "Charniere_Rabat_G" : "Charniere_Rabat_D", bv, new Vector3(sx * L / 2f, Hb, 0f));
            var rabat = Box("Rabat", piv, new Vector3(-sx * L / 4f, 0.004f, 0f), new Vector3(L / 2f - 0.004f, 0.006f, L), carton, isStatic: false);
            Ouvrant(rabat, piv, new Vector3(0f, 0f, sx < 0 ? 115f : -115f), Vector3.zero);
        }

        // ---- Trousse de déversement : seau jaune de 20 L avec couvercle à charnière, étiquette, et le matériel dedans
        var tr = Group("Trousse_Deversement", parent, posTrousse);
        var jaune = Mat("Plastique_Jaune", new Color(0.98f, 0.82f, 0.05f), 0.4f);
        const float Hs = 0.55f;
        Lathe("Seau", tr, Vector3.zero, "L_Seau_Deversement", V(0, 0, 0.16f, 0, 0.19f, 0.52f, 0.2f, 0.53f, 0.2f, Hs, 0.186f, Hs, 0.15f, 0.025f, 0, 0.025f), jaune);
        Box("Fond_Collider", tr, new Vector3(0f, 0.012f, 0f), new Vector3(0.3f, 0.025f, 0.3f), jaune, collider: true).GetComponent<Renderer>().enabled = false;
        for (int k = 0; k < 6; k++)
        {
            Vector3 d = Dir(k * 60f);
            ColliderOnly("Paroi", tr, d * 0.18f + Vector3.up * 0.28f, new Vector3(0.2f, 0.56f, 0.02f), Quaternion.LookRotation(d));
        }
        foreach (float y in new[] { 0.15f, 0.45f })
            Lathe("Cerclage", tr, Vector3.zero, $"L_Cerclage_{y:0.00}", V(0.163f + 0.03f * y / 0.52f + 0.003f, y, 0.163f + 0.03f * y / 0.52f + 0.008f, y + 0.008f, 0.163f + 0.03f * y / 0.52f + 0.003f, y + 0.016f), jaune);
        MeshGO("Etiquette", tr, EtiquetteCourbeMesh(0.16f + 0.03f * 0.17f / 0.52f + 0.003f, 0.16f + 0.03f * 0.4f / 0.52f + 0.003f, 0.17f, 0.4f, 100f),
               Mat("Etiquette_Trousse", Color.white, 0.3f, "Etiquette_Trousse", 1f), Vector3.zero, Quaternion.identity);
        // poignée (anse) en métal
        for (int k = 0; k < 8; k++)
        {
            float a0 = k * 180f / 8f, a1 = (k + 1) * 180f / 8f;
            Vector3 q0 = new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad) * 0.2f, 0.47f + Mathf.Sin(a0 * Mathf.Deg2Rad) * 0.03f, Mathf.Sin(a0 * Mathf.Deg2Rad) * 0.02f - 0.01f);
            Vector3 q1 = new Vector3(Mathf.Cos(a1 * Mathf.Deg2Rad) * 0.2f, 0.47f + Mathf.Sin(a1 * Mathf.Deg2Rad) * 0.03f, Mathf.Sin(a1 * Mathf.Deg2Rad) * 0.02f - 0.01f);
            Tube("Anse", tr, q0 + Vector3.right * 0.012f * Mathf.Sign(q0.x), q1 + Vector3.right * 0.012f * Mathf.Sign(q1.x), 0.006f, Metal);
        }
        // couvercle à charnière à l'arrière, avec languette pour l'ouvrir
        var charn = Group("Charniere_Couvercle", tr, new Vector3(0f, Hs, -0.2f));
        var couv = Lathe("Couvercle", charn, new Vector3(0f, 0f, 0.2f), "L_Couvercle_Seau", V(0, 0.035f, 0.19f, 0.035f, 0.206f, 0.02f, 0.206f, -0.02f, 0.2f, -0.02f, 0.2f, 0.01f, 0, 0.01f), jaune);
        Box("Languette", couv.transform, new Vector3(0f, 0.0f, 0.215f), new Vector3(0.06f, 0.03f, 0.02f), jaune, collider: false, isStatic: false);
        Box("Scelle", couv.transform, new Vector3(0f, -0.005f, 0.212f), new Vector3(0.01f, 0.03f, 0.004f), Mat("Scelle_Rouge", new Color(0.8f, 0.1f, 0.1f), 0.4f), collider: false, isStatic: false);
        var bc = couv.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.01f, 0f); bc.size = new Vector3(0.42f, 0.06f, 0.42f);
        Ouvrant(couv, charn, new Vector3(-95f, 0f, 0f), Vector3.zero);
        // matériel à l'intérieur (tout se prend en main)
        var tampons = Box("Tampons_Absorbants", tr, new Vector3(0f, 0.06f, 0f), new Vector3(0.22f, 0.07f, 0.22f), Mat("Tampon_Blanc", new Color(0.96f, 0.96f, 0.96f), 0.05f), collider: false, isStatic: false);
        for (int k = 1; k < 7; k++)
            Box("Couche", tampons.transform, new Vector3(0f, -0.035f + k * 0.01f, 0.1105f), new Vector3(0.22f, 0.001f, 0.001f), Mat("Gris_Clair", new Color(0.86f, 0.87f, 0.88f), 0.5f), collider: false, isStatic: false);
        Attrapable(tampons, Vector3.zero, new Vector3(0.22f, 0.07f, 0.22f), 0.4f);
        for (int k = 0; k < 2; k++)
        {
            var boudin = Group($"Boudin_Absorbant_{k}", tr, new Vector3(-0.05f + k * 0.1f, 0.13f, 0f)).gameObject;
            var cyl = Disc("Boudin", boudin.transform, Vector3.zero, 0.065f, 0.28f, Mat("Boudin_Gris", new Color(0.55f, 0.57f, 0.6f), 0.1f), false);
            cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Attrapable(boudin, Vector3.zero, new Vector3(0.065f, 0.065f, 0.28f), 0.3f);
        }
        var gants = Box("Boite_Gants_Nitrile", tr, new Vector3(0f, 0.23f, 0.02f), new Vector3(0.24f, 0.07f, 0.12f), Mat("Boite_Gants", new Color(0.2f, 0.45f, 0.85f), 0.3f), collider: false, isStatic: false);
        Attrapable(gants, Vector3.zero, new Vector3(0.24f, 0.07f, 0.12f), 0.15f);
        var lunettes = Group("Lunettes_Securite", tr, new Vector3(0f, 0.28f, -0.05f)).gameObject;
        foreach (float sx in new[] { -0.035f, 0.035f })
            Box("Verre_Lunette", lunettes.transform, new Vector3(sx, 0.02f, 0f), new Vector3(0.065f, 0.04f, 0.004f), GlassMat("Verre_Lunettes", new Color(0.85f, 0.9f, 0.95f), 0.4f), collider: false, isStatic: false);
        Box("Monture", lunettes.transform, new Vector3(0f, 0.041f, 0f), new Vector3(0.14f, 0.006f, 0.008f), Noir, collider: false, isStatic: false);
        foreach (float sx in new[] { -0.07f, 0.07f })
            Box("Branche", lunettes.transform, new Vector3(sx, 0.035f, -0.06f), new Vector3(0.005f, 0.008f, 0.12f), Noir, collider: false, isStatic: false);
        Attrapable(lunettes, new Vector3(0f, 0.025f, -0.03f), new Vector3(0.15f, 0.05f, 0.13f), 0.05f);
        var sacs = Box("Sacs_Elimination", tr, new Vector3(0.0f, 0.3f, 0.06f), new Vector3(0.18f, 0.02f, 0.12f), jaune, collider: false, isStatic: false);
        Attrapable(sacs, Vector3.zero, new Vector3(0.18f, 0.02f, 0.12f), 0.1f);
        // affiche murale verte au-dessus, comme dans les vrais labos
        Sign("Affiche_Trousse", parent, "TROUSSE DE\nDÉVERSEMENT", posTrousse + new Vector3(0f, 1.55f, -0.24f), Quaternion.LookRotation(Vector3.back),
             new Vector2(0.32f, 0.2f), Color.white, Mat("Vert_Securite", new Color(0.0f, 0.5f, 0.25f), 0.3f));
    }

    // Réfrigérateur de laboratoire sous comptoir : porte qui s'ouvre, lumière intérieure, deux tiroirs qu'on tire
    // pour prendre les flacons (ils sortent avec le tiroir), thermomètre à affichage sur la porte, affiche
    static void Refrigerateur(Transform parent, Vector3 pos, Vector3 face)
    {
        var blanc = Mat("Frigo_Blanc", new Color(0.95f, 0.95f, 0.94f), 0.6f);
        var bac = Mat("Bac_Frigo", new Color(0.88f, 0.9f, 0.92f), 0.5f);
        const float Lf = 0.6f, Pf = 0.62f, Hf = 0.85f;
        var lst = Caisson(parent, "Refrigerateur", pos, face, Lf, Pf, Hf, 1, blanc, 0, (g, yb, hn) =>
        {
            Box("Lampe_Interieure", g, new Vector3(0f, Hf - 0.03f, -0.1f), new Vector3(0.12f, 0.012f, 0.04f), EmissiveMat("Lampe_Frigo", new Color(0.9f, 0.95f, 1f), 1.5f), collider: false);
            Box("Grille_Arriere", g, new Vector3(0f, Hf / 2f, -Pf / 2f + 0.03f), new Vector3(Lf - 0.08f, Hf - 0.25f, 0.005f), Metal, collider: false);
            for (int n = 0; n < 2; n++)
            {
                float y = yb + 0.02f + n * 0.36f;
                var piv = Group($"Tiroir_{n}", g, new Vector3(0f, y, 0.02f));
                const float Lt = 0.52f, Pt = 0.5f, Ht = 0.11f;
                Box("Fond", piv, new Vector3(0f, 0.005f, 0f), new Vector3(Lt, 0.01f, Pt), bac, isStatic: false);
                Box("Dos", piv, new Vector3(0f, Ht / 2f, -Pt / 2f), new Vector3(Lt, Ht, 0.01f), bac, isStatic: false);
                Box("Cote_G", piv, new Vector3(-Lt / 2f, Ht / 2f, 0f), new Vector3(0.01f, Ht, Pt), bac, isStatic: false);
                Box("Cote_D", piv, new Vector3(Lt / 2f, Ht / 2f, 0f), new Vector3(0.01f, Ht, Pt), bac, isStatic: false);
                var avant = Box("Face", piv, new Vector3(0f, Ht / 2f + 0.01f, Pt / 2f), new Vector3(Lt + 0.02f, Ht + 0.03f, 0.014f), GlassMat("Bac_Transparent", new Color(0.8f, 0.88f, 0.95f), 0.6f), isStatic: false);
                Box("Poignee", avant.transform, new Vector3(0f, 0.03f, 0.012f), new Vector3(0.16f, 0.018f, 0.02f), Gris(0.3f), collider: false, isStatic: false);
                Ouvrant(avant, piv, Vector3.zero, new Vector3(0f, 0f, 0.36f));
                for (int f = 0; f < 4; f++)
                    Verrerie(f % 2 == 0 ? "FlaconAmbre" : "FlaconBlanc", $"Reactif_Froid_{n}_{f}", piv, new Vector3(-0.18f + f * 0.12f, 0.01f, -0.05f + (f % 2) * 0.12f), null, true);
            }
        });
        foreach (var b in lst)
        {
            Box("Poignee_Frigo", b, new Vector3(0.24f, 0.05f, 0.025f), new Vector3(0.025f, 0.35f, 0.03f), Metal, collider: false, isStatic: false);
            Box("Joint_Porte", b, new Vector3(0f, 0f, -0.011f), new Vector3(Lf - 0.03f, Hf - 0.14f, 0.004f), Gris(0.25f), collider: false, isStatic: false);
            Sign("Affiche_Frigo", b, "PRODUITS CHIMIQUES SEULEMENT\n<size=70%>AUCUNE NOURRITURE NI BOISSON</size>", new Vector3(0f, 0.2f, 0.011f),
                 Quaternion.LookRotation(Vector3.back), new Vector2(0.45f, 0.12f), new Color(0.75f, 0.05f, 0.05f), Blanc);
            Box("Thermometre", b, new Vector3(-0.15f, -0.05f, 0.012f), new Vector3(0.1f, 0.05f, 0.006f), Gris(0.25f), collider: false, isStatic: false);
            TexteTMP("Temperature", b, "4,2 °C", new Vector3(-0.15f, -0.05f, 0.016f), Quaternion.LookRotation(Vector3.back), new Vector2(0.08f, 0.03f), new Color(0.3f, 1f, 0.4f));
        }
    }

    // Évaporateur rotatif : socle, colonne, réfrigérant incliné, ballon dans le bain-marie
    static void Rotavap(Transform parent, string name, Vector3 pos)
    {
        var g = Group(name, parent, pos);
        var gris = Mat("Gris_Clair", new Color(0.86f, 0.87f, 0.88f), 0.5f);
        Box("Socle", g, new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.08f, 0.3f), gris);
        Disc("Colonne", g, new Vector3(-0.1f, 0.3f, 0f), 0.04f, 0.44f, Metal, false);
        Box("Moteur", g, new Vector3(-0.04f, 0.42f, 0f), new Vector3(0.12f, 0.1f, 0.1f), gris, collider: false, rot: Quaternion.Euler(0f, 0f, -30f));
        var refr = Lathe("Refrigerant", g, new Vector3(-0.02f, 0.45f, 0f), "L_Refrigerant", V(0.03f,0, 0.03f,0.28f, 0.012f,0.3f, 0,0.3f), Verre);
        refr.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
        Lathe("Bain_Marie", g, new Vector3(0.1f, 0.08f, 0f), "L_Bain", V(0,0, 0.11f,0, 0.11f,0.1f, 0.115f,0.105f), Mat("Inox", new Color(0.70f, 0.71f, 0.72f), 0.85f));
        Lathe("Eau_Bain", g, new Vector3(0.1f, 0.08f, 0f), "L_Bain_Eau", V(0,0.005f, 0.105f,0.005f, 0.105f,0.08f, 0,0.08f), Liquide("Eau_Bain", new Color(0.7f, 0.85f, 1f)));
        var rot = Group("Rotation_Ballon", g, new Vector3(0.1f, 0.19f, 0f));
        rot.gameObject.isStatic = false;
        Ballon("Ballon", rot, Vector3.zero, 0.12f, Liquide("Jaune", new Color(0.95f, 0.85f, 0.2f)));
        foreach (var t in rot.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
        Box("Ecran_Bain", g, new Vector3(0.1f, 0.05f, -0.152f), new Vector3(0.08f, 0.03f, 0.004f), Noir, collider: false);
        var tBain = TexteTMP("Affichage_Bain", g, "", new Vector3(0.1f, 0.05f, -0.155f), Quaternion.identity, new Vector2(0.075f, 0.025f), new Color(1f, 0.3f, 0.1f));
        var inter = Box("Interrupteur", g, new Vector3(-0.1f, 0.05f, -0.152f), new Vector3(0.03f, 0.04f, 0.01f), Mat("Interrupteur_Rouge", new Color(0.85f, 0.1f, 0.1f), 0.4f), isStatic: false);
        var son = Source(g.gameObject, Clip("Hotte_Aspiration"), true, false, 0.08f, 1f, 4f);
        var so = Commande(inter, inter.transform, new Vector3(25f, 0f, 0f));
        SetArray(so, "rotors", new List<Object> { rot });
        so.FindProperty("axeRotation").vector3Value = Vector3.right;
        so.FindProperty("degresParSeconde").floatValue = 200f;
        so.FindProperty("son").objectReferenceValue = son;
        so.FindProperty("affichage").objectReferenceValue = tBain;
        so.FindProperty("valeurDepart").floatValue = 22f;
        so.FindProperty("valeurCible").floatValue = 40f;
        so.FindProperty("vitesseValeur").floatValue = 1.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Extincteur posé sur son support mural ; le corps est attrapable
    static void Extincteur(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent, pos);
        Box("Support", g, new Vector3(-0.08f, 1.0f, 0f), new Vector3(0.02f, 0.12f, 0.1f), Metal, collider: false);
        Box("Support_Bas", g, new Vector3(0f, 0.49f, 0f), new Vector3(0.17f, 0.02f, 0.17f), Metal);
        var corps = Group("Extincteur", g, Vector3.zero).gameObject;
        var t = corps.transform;
        Lathe("Corps", t, new Vector3(0f, 0.5f, 0f), "L_Extincteur", V(0,0, .07f,0, .075f,.01f, .075f,.44f, .06f,.49f, .02f,.52f, 0,.52f), Rouge);
        Disc("Tete", t, new Vector3(0f, 1.04f, 0f), 0.05f, 0.05f, Noir, false);
        Box("Poignee", t, new Vector3(0.03f, 1.08f, 0f), new Vector3(0.1f, 0.015f, 0.03f), Noir, collider: false);
        Box("Tuyau", t, new Vector3(0.08f, 0.9f, 0f), new Vector3(0.015f, 0.3f, 0.015f), Noir, collider: false);
        Box("Etiquette", t, new Vector3(0f, 0.75f, 0f), new Vector3(0.1f, 0.15f, 0.152f), Blanc, collider: false);
        Attrapable(corps, new Vector3(0f, 0.78f, 0f), new Vector3(0.16f, 0.58f, 0.16f), 4f);
        // Il fonctionne : prends-le en main et appuie sur la gâchette (activer) -> nuage de poudre + bruit du jet
        var levier = t.Find("Poignee");
        var jg = new GameObject("Jet_Poudre");
        jg.transform.SetParent(t, false);
        jg.transform.localPosition = new Vector3(0.06f, 1.04f, 0f);
        jg.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.15f, 0f));
        var ps = jg.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(0.95f, 0.95f, 0.95f, 0.6f);
        main.gravityModifier = 0.15f;
        main.maxParticles = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 160f; em.enabled = false;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.01f;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 4f));
        var co = ps.collision; co.enabled = true; co.type = ParticleSystemCollisionType.World; co.mode = ParticleSystemCollisionMode.Collision3D;
        co.dampen = 0.8f; co.bounce = 0.05f; co.lifetimeLoss = 0.2f; co.quality = ParticleSystemCollisionQuality.Low;
        jg.GetComponent<ParticleSystemRenderer>().sharedMaterial = GoutteMat();
        var sonExt = Source(jg, Clip("Extincteur"), true, false, 0.6f, 1f, 10f);
        var ea = corps.AddComponent<ExtincteurActif>();
        var soE = new SerializedObject(ea);
        soE.FindProperty("jet").objectReferenceValue = ps;
        soE.FindProperty("son").objectReferenceValue = sonExt;
        soE.FindProperty("levier").objectReferenceValue = levier;
        soE.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ EXTÉRIEUR (vu par les portes vitrées et les fenêtres)
    static void BuildExterieur(Transform root)
    {
        var ext = Group("Exterieur", root, Vector3.zero);
        var herbe = Mat("Exterieur_Herbe", new Color(0.33f, 0.40f, 0.24f), 0.1f);
        var trottoir = Mat("Exterieur_Trottoir", new Color(0.55f, 0.55f, 0.53f), 0.2f);

        // Terrain tout autour du pavillon (en morceaux, pour ne pas couvrir le puits du hall). Tout est praticable.
        Box("Trottoir_Entree", ext, new Vector3(0f, -0.08f, 15f), new Vector3(12f, 0.1f, 6f), trottoir);
        Box("Herbe_Nord", ext, new Vector3(6f, -0.12f, 30f), new Vector3(60f, 0.1f, 24f), herbe);
        Box("Herbe_Labo", ext, new Vector3(20f, -0.12f, 14f), new Vector3(20f, 0.1f, 8f), herbe);
        Box("Herbe_Sud", ext, new Vector3(10f, -0.12f, -24.75f), new Vector3(80f, 0.1f, 30.5f), herbe);
        Box("Herbe_Est", ext, new Vector3(37.1f, -0.12f, 10.25f), new Vector3(25.8f, 0.1f, 39.5f), herbe);
        Box("Herbe_Ouest", ext, new Vector3(-24.75f, -0.12f, 4.25f), new Vector3(30.5f, 0.1f, 27.5f), herbe);
        // Herbe près du hall (cachée dans les planchers ; on laisse libre le puits central)
        Box("Herbe_Hall_Ouest", ext, new Vector3(-6.35f, -0.12f, 4.25f), new Vector3(6.3f, 0.1f, 27.5f), herbe);
        Box("Herbe_Hall_Est",   ext, new Vector3( 7.6f, -0.12f, 4.25f), new Vector3(8.8f, 0.1f, 27.5f), herbe);
        Box("Herbe_Hall_Sud",   ext, new Vector3( 0f,   -0.12f, -6.35f), new Vector3(6.4f, 0.1f, 6.3f), herbe);
        Box("Herbe_Hall_Nord",  ext, new Vector3( 0f,   -0.12f, 7.6f),  new Vector3(6.4f, 0.1f, 8.8f), herbe);
        Box("Herbe_Entree_Cotes", ext, new Vector3(0f, -0.12f, 15f), new Vector3(19f, 0.1f, 6f), herbe);

        // Rue au sud (là où arrivent les pompiers et l'ambulance) : asphalte, bordures, ligne jaune, trottoir
        var rue = Group("Rue", ext, Vector3.zero);
        var asphalte = Mat("Asphalte", new Color(0.13f, 0.13f, 0.14f), 0.35f);
        Box("Chaussee", rue, new Vector3(5f, -0.07f, -19f), new Vector3(100f, 0.06f, 7f), asphalte);
        Box("Bordure_Nord", rue, new Vector3(5f, -0.02f, -15.45f), new Vector3(100f, 0.15f, 0.15f), trottoir);
        Box("Bordure_Sud", rue, new Vector3(5f, -0.02f, -22.55f), new Vector3(100f, 0.15f, 0.15f), trottoir);
        Box("Trottoir_Rue", rue, new Vector3(5f, -0.06f, -14.6f), new Vector3(100f, 0.1f, 1.6f), trottoir);
        var ligne = Mat("Ligne_Jaune", new Color(0.95f, 0.78f, 0.1f), 0.4f);
        for (int l = 0; l < 25; l++)
            Box($"Ligne_{l}", rue, new Vector3(-44f + l * 4f, -0.038f, -19f), new Vector3(2f, 0.005f, 0.12f), ligne, collider: false);
        var poteauRue = Mat("Poteau_Lampadaire", new Color(0.18f, 0.18f, 0.20f), 0.5f);
        foreach (float lx in new[] { 8f, 26f })
        {
            Box($"Lampadaire_Rue_{lx}", rue, new Vector3(lx, 2.4f, -14.2f), new Vector3(0.14f, 4.8f, 0.14f), poteauRue);
            Box($"Lampadaire_Bras_{lx}", rue, new Vector3(lx, 4.75f, -14.7f), new Vector3(0.08f, 0.08f, 1.1f), poteauRue, collider: false);
            Box($"Lampadaire_Tete_{lx}", rue, new Vector3(lx, 4.68f, -15.2f), new Vector3(0.3f, 0.1f, 0.5f),
                EmissiveMat("Lampe", new Color(1f, 0.97f, 0.9f), 1.5f), collider: false);
        }
        // Allée du point de rassemblement jusqu'au trottoir de la rue
        Box("Allee_Rue", rue, new Vector3(23f, -0.06f, -12.2f), new Vector3(1.6f, 0.1f, 4.6f), trottoir);

        // Limites du terrain (invisibles), cachées derrière les rangées d'arbres
        var lim = Group("Limites", ext, Vector3.zero);
        ColliderOnly("Limite_Nord", lim, new Vector3(5f, 1.5f, 40f), new Vector3(100f, 3f, 0.5f), Quaternion.identity);
        ColliderOnly("Limite_Sud", lim, new Vector3(5f, 1.5f, -39f), new Vector3(100f, 3f, 0.5f), Quaternion.identity);
        ColliderOnly("Limite_Est", lim, new Vector3(49f, 1.5f, 0f), new Vector3(0.5f, 3f, 80f), Quaternion.identity);
        ColliderOnly("Limite_Ouest", lim, new Vector3(-39f, 1.5f, 0f), new Vector3(0.5f, 3f, 80f), Quaternion.identity);

        // Arbres réalistes : épinettes (conifères en étages) et érables (tronc, branches, feuillage en masses)
        var arbres = Group("Arbres", ext, Vector3.zero);
        var rnd = new System.Random(12);
        System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
        var places = new List<Vector3>();
        Vector3[] fixes = { new Vector3(14f, 0f, 16f), new Vector3(19.5f, 0f, 18.5f), new Vector3(25f, 0f, 15.5f), new Vector3(29f, 0f, 20f),
                            new Vector3(9f, 0f, 21f), new Vector3(-12f, 0f, 14f), new Vector3(30f, 0f, -12f), new Vector3(-14f, 0f, -11f),
                            new Vector3(-10f, 0f, 20f), new Vector3(34f, 0f, 4f), new Vector3(36f, 0f, -6f), new Vector3(-16f, 0f, 2f) };
        places.AddRange(fixes);
        for (float x = -34f; x <= 44f; x += 6.5f) { places.Add(new Vector3(x + R(-1.5f, 1.5f), 0f, R(-34f, -27f))); places.Add(new Vector3(x + R(-1.5f, 1.5f), 0f, R(33f, 37f))); }
        for (float z = -20f; z <= 28f; z += 7f) { places.Add(new Vector3(R(42f, 46f), 0f, z + R(-1.5f, 1.5f))); places.Add(new Vector3(R(-36f, -31f), 0f, z + R(-1.5f, 1.5f))); }
        for (int a = 0; a < places.Count; a++)
        {
            if (rnd.NextDouble() < 0.55) Epinette(arbres, $"Epinette_{a}", places[a], R(6f, 10f), rnd);
            else Erable(arbres, $"Erable_{a}", places[a], R(5f, 8f), rnd);
        }

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

    // Épinette (épinette noire / sapin baumier du Nouveau-Brunswick) : tronc effilé et 6 étages de branches en cônes
    static void Epinette(Transform parent, string name, Vector3 pos, float h, System.Random rnd)
    {
        var g = Group(name, parent, pos);
        g.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        var ecorce = Mat("Ecorce_Epinette", new Color(0.28f, 0.20f, 0.14f), 0.15f);
        var vert1 = Mat("Aiguilles_1", new Color(0.10f, 0.22f, 0.12f), 0.1f);
        var vert2 = Mat("Aiguilles_2", new Color(0.13f, 0.27f, 0.14f), 0.1f);
        Lathe("Tronc", g, Vector3.zero, "L_Tronc_Epinette", V(0,0, .2f,0, .16f,1.5f, .05f,8f, 0,8.2f), ecorce).transform.localScale = new Vector3(1f, h / 8f, 1f);
        int etages = 6;
        for (int e = 0; e < etages; e++)
        {
            float f = (float)e / etages;
            float rBase = Mathf.Lerp(2.0f, 0.45f, f) * h / 8f * (0.9f + 0.2f * (float)rnd.NextDouble());
            float y0 = Mathf.Lerp(1.2f, h - 1.6f, f) * 1f;
            float hc = Mathf.Lerp(2.4f, 1.6f, f) * h / 8f;
            var cone = Lathe($"Etage_{e}", g, new Vector3(0f, y0, 0f), "L_Cone", V(0,-0.05f, 1f,0, 0.65f,0.25f, 0.3f,0.65f, 0,1f), e % 2 == 0 ? vert1 : vert2);
            cone.transform.localScale = new Vector3(rBase, hc, rBase);
            cone.transform.localRotation = Quaternion.Euler(0f, e * 37f, 0f);
        }
        ColliderOnly("Collision_Tronc", g, new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.4f), Quaternion.identity);
    }

    // Érable : tronc effilé, deux grosses branches, feuillage en plusieurs masses irrégulières
    static void Erable(Transform parent, string name, Vector3 pos, float h, System.Random rnd)
    {
        var g = Group(name, parent, pos);
        g.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        var ecorce = Mat("Ecorce_Erable", new Color(0.33f, 0.27f, 0.21f), 0.15f);
        var vertA = Mat("Feuilles_1", new Color(0.18f, 0.32f, 0.12f), 0.1f);
        var vertB = Mat("Feuilles_2", new Color(0.24f, 0.38f, 0.14f), 0.1f);
        var vertC = Mat("Feuilles_3", new Color(0.14f, 0.27f, 0.10f), 0.1f);
        float ht = h * 0.45f;
        Lathe("Tronc", g, Vector3.zero, "L_Tronc_Erable", V(0,0, .26f,0, .2f,.4f, .16f,1f, 0,1f), ecorce).transform.localScale = new Vector3(1f, ht, 1f);
        foreach (float sx in new[] { -1f, 1f })
        {
            var br = Lathe("Branche", g, new Vector3(0f, ht * 0.85f, 0f), "L_Branche", V(0,0, .1f,0, .05f,1f, 0,1f), ecorce);
            br.transform.localScale = new Vector3(1f, h * 0.3f, 1f);
            br.transform.localRotation = Quaternion.Euler(0f, 0f, sx * 35f);
        }
        Material[] verts = { vertA, vertB, vertC };
        int n = 7;
        for (int k = 0; k < n; k++)
        {
            float a = k * 360f / n + (float)rnd.NextDouble() * 30f;
            float r = k == 0 ? 0f : h * 0.18f * (0.7f + 0.6f * (float)rnd.NextDouble());
            Vector3 c = Dir(a) * r + Vector3.up * (h * 0.68f + (k % 3) * h * 0.06f);
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            f.name = $"Feuillage_{k}";
            f.transform.SetParent(g, false);
            f.transform.localPosition = c;
            float sc = h * (0.32f + 0.12f * (float)rnd.NextDouble());
            f.transform.localScale = new Vector3(sc, sc * 0.8f, sc * (0.85f + 0.3f * (float)rnd.NextDouble()));
            f.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 30f, a, 0f);
            f.GetComponent<Renderer>().sharedMaterial = verts[k % 3];
            Object.DestroyImmediate(f.GetComponent<Collider>());
            PetitObjet(f);
        }
        ColliderOnly("Collision_Tronc", g, new Vector3(0f, 1.5f, 0f), new Vector3(0.5f, 3f, 0.5f), Quaternion.identity);
    }

    // ------------------------------------------------------------------ ÉLÉMENTS RÉUTILISÉS
    static int Seg(float angle) => Mathf.RoundToInt(angle / 15f) % N;
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
        if (Mathf.Abs(y0 - Y_RDC) < 0.01f) ComptoirsPorte(e);

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

    // Double porte coupe-feu fermée (décor), comme les portes du hall : battants en bois pleins qui remplissent toute
    // l'ouverture, couvre-joint central, cadre en acier, fenêtres étroites en verre armé, barres anti-panique,
    // ferme-portes, plaques de protection, seuil et étiquette « Porte coupe-feu — garder fermée ».
    static void ClosedDoubleDoor(Transform e, bool afficheConfinement)
    {
        var porte = Group("Porte_Coupe_Feu", e, Vector3.zero);
        var acier = Mat("Cadre_Acier", new Color(0.22f, 0.23f, 0.24f), 0.5f);
        var boisPorte = Mat("Porte_Coupe_Feu_Bois", new Color(0.78f, 0.56f, 0.32f), 0.35f, "Bois", 1f);
        float w = PorteL / 2f;
        // Cadre en acier sur toute l'épaisseur du mur (montants + traverse), un peu plus large côté hall
        foreach (float sx in new[] { -1f, 1f })
            Box(sx < 0 ? "Montant_G" : "Montant_D", porte, new Vector3(sx * (w + 0.05f), PorteH / 2f, 0f), new Vector3(0.1f, PorteH + 0.1f, 0.26f), acier, collider: false);
        Box("Traverse", porte, new Vector3(0f, PorteH + 0.05f, 0f), new Vector3(PorteL + 0.2f, 0.1f, 0.26f), acier, collider: false);
        Box("Seuil", porte, new Vector3(0f, 0.006f, 0f), new Vector3(PorteL, 0.012f, 0.22f), Metal, collider: false);
        // Les deux battants remplissent toute l'ouverture (aucun jour) ; le collider bloque le passage
        Box("Battants_Collision", porte, new Vector3(0f, PorteH / 2f, 0f), new Vector3(PorteL, PorteH, 0.05f), Bois, collider: true)
            .GetComponent<MeshRenderer>().enabled = false;
        foreach (float sx in new[] { -1f, 1f })
        {
            string c = sx < 0 ? "G" : "D";
            float cx = sx * w / 2f;
            Box($"Battant_{c}", porte, new Vector3(cx, PorteH / 2f, 0f), new Vector3(w - 0.004f, PorteH - 0.006f, 0.045f), boisPorte, collider: false);
            float fx = sx * (w - 0.22f);   // fenêtre étroite côté charnière
            Box($"Fenetre_Cadre_{c}", porte, new Vector3(fx, 1.55f, 0f), new Vector3(0.16f, 0.66f, 0.05f), Metal, collider: false);
            Box($"Fenetre_{c}", porte, new Vector3(fx, 1.55f, 0f), new Vector3(0.12f, 0.6f, 0.052f), VerreArme, collider: false);
            Box($"Barre_{c}", porte, new Vector3(cx, 1.0f, -0.06f), new Vector3(w * 0.75f, 0.05f, 0.04f), Metal, collider: false);
            Box($"Barre_Support_A_{c}", porte, new Vector3(cx - w * 0.36f, 1.0f, -0.035f), new Vector3(0.05f, 0.1f, 0.03f), Metal, collider: false);
            Box($"Barre_Support_B_{c}", porte, new Vector3(cx + w * 0.36f, 1.0f, -0.035f), new Vector3(0.05f, 0.1f, 0.03f), Metal, collider: false);
            Box($"Plaque_Bas_{c}", porte, new Vector3(cx, 0.13f, -0.025f), new Vector3(w - 0.06f, 0.25f, 0.004f), Metal, collider: false);
            Box($"Plaque_Bas_Arriere_{c}", porte, new Vector3(cx, 0.13f, 0.025f), new Vector3(w - 0.06f, 0.25f, 0.004f), Metal, collider: false);
            Box($"Ferme_Porte_{c}", porte, new Vector3(sx * (w - 0.25f), PorteH - 0.1f, -0.06f), new Vector3(0.3f, 0.07f, 0.06f), acier, collider: false);
            Box($"Bras_Ferme_Porte_{c}", porte, new Vector3(sx * (w - 0.05f), PorteH + 0.02f, -0.08f), new Vector3(0.25f, 0.02f, 0.02f), acier, collider: false);
            for (int h = 0; h < 3; h++)
                Box($"Charniere_{c}_{h}", porte, new Vector3(sx * (w - 0.005f), 0.25f + h * (PorteH - 0.5f) / 2f, -0.025f), new Vector3(0.02f, 0.1f, 0.012f), Metal, collider: false);
        }
        Box("Couvre_Joint", porte, new Vector3(0f, PorteH / 2f, -0.03f), new Vector3(0.035f, PorteH - 0.02f, 0.012f), Metal, collider: false);
        Sign("Etiquette_Coupe_Feu", porte, "PORTE COUPE-FEU\n<size=70%>GARDER FERMÉE</size>", new Vector3(w / 2f, 1.75f, -0.03f),
             Quaternion.identity, new Vector2(0.34f, 0.12f), Color.white, Rouge);
        if (afficheConfinement)
            Sign("Affiche_Confinement", porte, "ACCÈS FERMÉ\n<size=55%>PAVILLON FERMÉ</size>", new Vector3(-w / 2f, 1.3f, -0.03f),
                 Quaternion.identity, new Vector2(0.5f, 0.32f), Color.black, Jaune);
    }

    // Texte en lettres détachées posées sur un arc (face vers l'extérieur du cercle), centré sur l'angle donné
    static void TexteArc(Transform parent, string texte, float r, float angleCentre, float y, float hauteur, Color couleur)
    {
        var g = Group("Enseigne_Faculte", parent, Vector3.zero);
        float pasLettre = hauteur * 0.68f, pasEspace = hauteur * 0.4f;
        float total = 0f;
        foreach (char ch in texte) total += ch == ' ' ? pasEspace : pasLettre;
        float pos = -total / 2f;
        int k = 0;
        foreach (char ch in texte)
        {
            float w = ch == ' ' ? pasEspace : pasLettre;
            float a = angleCentre + (pos + w / 2f) / r * Mathf.Rad2Deg;
            pos += w;
            if (ch == ' ') continue;
            Vector3 d = Dir(a);
            Sign($"Lettre_{k++}_{ch}", g, ch.ToString(), d * r + Vector3.up * y, Quaternion.LookRotation(-d),
                 new Vector2(hauteur * 1.4f, hauteur), couleur, null);
        }
    }

    // Tête de gicleur pendante : rosace chromée, corps, ampoule rouge, déflecteur
    static void Gicleur(Transform parent, Vector3 plafond)
    {
        var g = Group("Gicleur", parent, plafond);
        var chrome = Mat("Chrome", new Color(0.82f, 0.83f, 0.85f), 0.95f);
        Disc("Rosace", g, new Vector3(0f, -0.004f, 0f), 0.075f, 0.008f, chrome, false);
        Box("Corps", g, new Vector3(0f, -0.028f, 0f), new Vector3(0.022f, 0.04f, 0.012f), Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), collider: false);
        Disc("Ampoule", g, new Vector3(0f, -0.035f, 0f), 0.007f, 0.022f, GlassMat("Ampoule_Rouge", new Color(0.95f, 0.1f, 0.05f), 0.8f), false);
        Disc("Deflecteur", g, new Vector3(0f, -0.05f, 0f), 0.04f, 0.003f, Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f), false);
    }

    // Gicleurs sur des cercles (plafonds ronds du hall), et un détecteur de fumée sur deux
    static void Gicleurs(Transform parent, float hPlafond, float[] rayons, int parCercle, float decalage)
    {
        var g = Group("Gicleurs", parent, Vector3.zero);
        int k = 0;
        foreach (float r in rayons)
        {
            for (int i = 0; i < parCercle; i++)
            {
                Vector3 d = Dir(decalage + i * 360f / parCercle + (k % 2) * 7f);
                Gicleur(g, d * r + Vector3.up * hPlafond);
                if (i % 3 == 0) Disc($"Detecteur_{k}_{i}", g, Dir(decalage + i * 360f / parCercle + 15f) * r + Vector3.up * (hPlafond - 0.02f), 0.12f, 0.04f, Blanc, false);
            }
            k++;
        }
    }

    // Poubelle de tri comme dans le hall : bac creux avec sac et déchets, couvercle sur charnière qu'on soulève à la main
    static void PoubelleTri(Transform parent, string nom, Vector3 pos, Quaternion rot, Color couleur, string etiquette, bool fente)
    {
        var g = Group(nom, parent, pos);
        g.localRotation = rot;
        var plast = Mat("Bac_" + nom, couleur, 0.45f);
        var fonce = Mat("Bac_Fonce_" + nom, couleur * 0.6f, 0.4f);
        const float L = 0.42f, H = 0.78f, e = 0.015f;
        Box("Fond", g, new Vector3(0f, 0.04f, 0f), new Vector3(L, 0.08f, L), fonce);
        Box("Paroi_Av", g, new Vector3(0f, H / 2f, L / 2f - e / 2f), new Vector3(L, H, e), plast);
        Box("Paroi_Ar", g, new Vector3(0f, H / 2f, -L / 2f + e / 2f), new Vector3(L, H, e), plast);
        Box("Paroi_G", g, new Vector3(-L / 2f + e / 2f, H / 2f, 0f), new Vector3(e, H, L), plast);
        Box("Paroi_D", g, new Vector3(L / 2f - e / 2f, H / 2f, 0f), new Vector3(e, H, L), plast);
        Box("Sac", g, new Vector3(0f, 0.3f, 0f), new Vector3(L - 0.04f, 0.45f, L - 0.04f), Mat("Sac_Noir", new Color(0.08f, 0.08f, 0.09f), 0.6f), collider: false);
        for (int k = 0; k < 4; k++)
        {
            var papier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            papier.name = "Dechet";
            papier.transform.SetParent(g, false);
            papier.transform.localPosition = new Vector3(-0.1f + k * 0.07f, 0.54f + (k % 2) * 0.03f, (k % 2) * 0.08f - 0.04f);
            papier.transform.localScale = new Vector3(0.07f, 0.06f, 0.08f);
            papier.GetComponent<Renderer>().sharedMaterial = k % 2 == 0 ? Blanc : Mat("Carton", new Color(0.62f, 0.48f, 0.32f), 0.2f);
            Object.DestroyImmediate(papier.GetComponent<Collider>());
            PetitObjet(papier);
        }
        // Couvercle sur charnière à l'arrière : on le soulève avec la main
        var charn = Group("Charniere_Couvercle", g, new Vector3(0f, H + 0.005f, -L / 2f));
        var couv = Box("Couvercle", charn, new Vector3(0f, 0.025f, L / 2f + 0.01f), new Vector3(L + 0.03f, 0.05f, L + 0.03f), plast, isStatic: false);
        if (fente) Box("Fente", couv.transform, new Vector3(0f, 0.026f, 0.05f), new Vector3(0.28f, 0.004f, 0.05f), Noir, collider: false, isStatic: false);
        else Disc("Ouverture", couv.transform, new Vector3(0f, 0.026f, 0.05f), 0.15f, 0.004f, Noir, false).isStatic = false;
        Box("Prise", couv.transform, new Vector3(0f, 0.0f, (L + 0.03f) / 2f + 0.01f), new Vector3(0.12f, 0.03f, 0.03f), fonce, collider: false, isStatic: false);
        var pb = couv.AddComponent<PorteBattante>();
        couv.AddComponent<FiltreProximite>();
        var so = new SerializedObject(pb);
        so.FindProperty("pivot").objectReferenceValue = charn;
        so.FindProperty("angleOuvert").vector3Value = new Vector3(-95f, 0f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();
        Box("Etiquette_Fond", g, new Vector3(0f, 0.55f, L / 2f + 0.002f), new Vector3(0.3f, 0.22f, 0.004f), Blanc, collider: false);
        Sign("Etiquette", g, etiquette, new Vector3(0f, 0.55f, L / 2f + 0.006f), Quaternion.LookRotation(Vector3.back), new Vector2(0.27f, 0.18f), couleur * 0.7f, null);
    }

    // Poubelle ronde à pédale (labo) : cuve en inox creuse, sac, couvercle qui s'ouvre (on le soulève ou on touche la pédale)
    static void PoubellePedale(Transform parent, Vector3 pos)
    {
        var g = Group("Poubelle_Pedale", parent, pos);
        var inox = Mat("Inox", new Color(0.70f, 0.71f, 0.72f), 0.85f);
        Lathe("Cuve", g, Vector3.zero, "L_Poubelle", V(0,0.03f, 0.16f,0.03f, 0.165f,0.1f, 0.165f,0.62f, 0.168f,0.63f), inox);
        Disc("Base", g, new Vector3(0f, 0.015f, 0f), 0.33f, 0.03f, Noir, false);
        ColliderOnly("Collision_Cuve", g, new Vector3(0f, 0.32f, 0f), new Vector3(0.3f, 0.6f, 0.3f), Quaternion.identity);
        Lathe("Sac", g, Vector3.zero, "L_Poubelle_Sac", V(0,0.04f, 0.155f,0.04f, 0.155f,0.55f, 0.17f,0.64f), Mat("Sac_Noir", new Color(0.08f, 0.08f, 0.09f), 0.6f));
        for (int k = 0; k < 3; k++)
        {
            var gant = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gant.name = "Gant_Jete";
            gant.transform.SetParent(g, false);
            gant.transform.localPosition = new Vector3(-0.05f + k * 0.05f, 0.5f, (k % 2) * 0.05f);
            gant.transform.localScale = new Vector3(0.08f, 0.04f, 0.06f);
            gant.GetComponent<Renderer>().sharedMaterial = Mat("Boite_Gants", new Color(0.2f, 0.45f, 0.85f), 0.3f);
            Object.DestroyImmediate(gant.GetComponent<Collider>());
            PetitObjet(gant);
        }
        var charn = Group("Charniere_Couvercle", g, new Vector3(0f, 0.635f, -0.165f));
        var couv = Disc("Couvercle", charn, new Vector3(0f, 0.012f, 0.165f), 0.34f, 0.025f, inox, false);
        couv.isStatic = false;
        couv.AddComponent<BoxCollider>();
        var pedale = Box("Pedale", g, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.12f, 0.02f, 0.08f), Noir, isStatic: false);
        foreach (var go in new[] { couv, pedale })
        {
            var pb = go.AddComponent<PorteBattante>();
            go.AddComponent<FiltreProximite>();
            var so = new SerializedObject(pb);
            so.FindProperty("pivot").objectReferenceValue = charn;
            so.FindProperty("angleOuvert").vector3Value = new Vector3(-75f, 0f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Comptoirs bas de chaque côté d'une porte du hall (comme sur les photos) : caisson beige, dessus gris, plinthe
    static void ComptoirsPorte(Transform e)
    {
        var beige = Mat("Comptoir_Beige", new Color(0.74f, 0.69f, 0.56f), 0.3f);
        var dessus = Mat("Comptoir_Dessus", new Color(0.50f, 0.47f, 0.42f), 0.5f);
        foreach (float sx in new[] { -1f, 1f })
        {
            var g = Group(sx < 0 ? "Comptoir_G" : "Comptoir_D", e, new Vector3(sx * 1.45f, 0f, -0.455f));
            Box("Caisson", g, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.8f, 1.0f, 0.5f), beige);
            Box("Plinthe", g, new Vector3(0f, 0.05f, -0.215f), new Vector3(0.8f, 0.1f, 0.02f), Mat("Plinthe_Grise", new Color(0.40f, 0.38f, 0.35f), 0.3f), collider: false);
            Box("Dessus", g, new Vector3(0f, 1.02f, -0.01f), new Vector3(0.86f, 0.04f, 0.6f), dessus, collider: false);
            Box("Retour", g, new Vector3(-sx * 0.38f, 0.5f, -0.2f), new Vector3(0.04f, 0.9f, 0.08f), beige, collider: false);
        }
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
    static void RailRound(Transform parent, float y0, float r, bool barreaux, int skipSeg, HashSet<int> skipSet = null)
    {
        float LR = Chord(r);
        for (int i = 0; i < N; i++)
        {
            if (i == skipSeg || (skipSet != null && skipSet.Contains(i))) continue;
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

    // Vitrine du département : caisson creux, vitres avant et côtés, tablettes en verre, éclairage intérieur,
    // et une vraie collection (verrerie ancienne, instruments, modèles moléculaires, plaque).
    static void Vitrine(Transform parent, Vector3 pos)
    {
        var v = Group("Vitrine_Molecules", parent, pos);
        const float W = 2.0f, H = 2.0f, P = 0.42f;
        var bois = Mat("Bois_Fonce", new Color(0.36f, 0.22f, 0.13f), 0.45f, "Bois", 1f);
        var fondMat = Mat("Feutrine_Bleue", new Color(0.10f, 0.16f, 0.30f), 0.05f);
        Box("Socle", v, new Vector3(0f, 0.17f, 0f), new Vector3(W, 0.34f, P), bois);
        Box("Fond", v, new Vector3(0f, 1.17f, -P / 2f + 0.01f), new Vector3(W, 1.66f, 0.02f), fondMat);
        Box("Cote_G", v, new Vector3(-W / 2f + 0.015f, 1.17f, 0f), new Vector3(0.03f, 1.66f, P), bois);
        Box("Cote_D", v, new Vector3( W / 2f - 0.015f, 1.17f, 0f), new Vector3(0.03f, 1.66f, P), bois);
        Box("Dessus", v, new Vector3(0f, H - 0.02f, 0f), new Vector3(W, 0.04f, P), bois);
        Box("Vitre", v, new Vector3(0f, 1.17f, P / 2f), new Vector3(W - 0.06f, 1.62f, 0.008f), Verre);
        Box("Montant_Centre", v, new Vector3(0f, 1.17f, P / 2f + 0.005f), new Vector3(0.025f, 1.62f, 0.012f), bois, collider: false);
        Box("Serrure", v, new Vector3(0.05f, 1.0f, P / 2f + 0.012f), new Vector3(0.02f, 0.03f, 0.01f), Metal, collider: false);
        Box("Eclairage", v, new Vector3(0f, H - 0.05f, 0.08f), new Vector3(W - 0.1f, 0.015f, 0.04f),
            EmissiveMat("Vitrine_Lumiere", new Color(1f, 0.96f, 0.88f), 2f), collider: false);
        float[] ys = { 0.36f, 0.82f, 1.28f, 1.72f };
        for (int s = 1; s < ys.Length; s++)
            Box($"Tablette_{s}", v, new Vector3(0f, ys[s], 0f), new Vector3(W - 0.06f, 0.012f, P - 0.04f), Verre, collider: false);
        var face = Quaternion.LookRotation(Vector3.back);
        Color encre = new Color(0.95f, 0.92f, 0.80f);

        // Tablette du bas : verrerie ancienne (colorée)
        Color[] cl = { new Color(0.2f, 0.5f, 0.9f), new Color(0.9f, 0.3f, 0.2f), new Color(0.2f, 0.75f, 0.35f), new Color(0.85f, 0.7f, 0.15f) };
        for (int k = 0; k < 4; k++)
            Ballon($"Ballon_Ancien_{k}", v, new Vector3(-0.7f + k * 0.45f, ys[0] + 0.1f, 0.02f), 0.14f + 0.02f * (k % 2), Liquide($"Vitrine_{k}", cl[k]));
        Disc("Burette", v, new Vector3(0.85f, ys[0] + 0.3f, -0.05f), 0.025f, 0.58f, Verre, false);
        Sign("Etiquette_Verrerie", v, "Verrerie de laboratoire, v. 1965", new Vector3(0f, ys[0] + 0.03f, P / 2f - 0.03f),
             face, new Vector2(0.6f, 0.05f), encre, null);

        // 2e tablette : instruments anciens (microscope, balance à fléau, mortier)
        var laiton = Mat("Laiton", new Color(0.72f, 0.55f, 0.25f), 0.8f);
        var mic = Group("Microscope", v, new Vector3(-0.6f, ys[1] + 0.006f, 0f));
        Box("Pied", mic, new Vector3(0f, 0.015f, 0f), new Vector3(0.14f, 0.03f, 0.18f), Noir, collider: false);
        Box("Bras", mic, new Vector3(0f, 0.12f, -0.06f), new Vector3(0.04f, 0.2f, 0.04f), Noir, collider: false);
        Box("Platine", mic, new Vector3(0f, 0.1f, 0f), new Vector3(0.1f, 0.01f, 0.1f), Noir, collider: false);
        var tube = Disc("Tube", mic, new Vector3(0f, 0.22f, -0.01f), 0.035f, 0.18f, laiton, false);
        tube.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        var bal = Group("Balance_Fleau", v, new Vector3(0.05f, ys[1] + 0.006f, 0f));
        Box("Socle", bal, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.04f, 0.12f), bois, collider: false);
        Disc("Colonne", bal, new Vector3(0f, 0.17f, 0f), 0.02f, 0.26f, laiton, false);
        Box("Fleau", bal, new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.01f, 0.01f), laiton, collider: false);
        foreach (float sx in new[] { -0.13f, 0.13f })
        {
            Box("Fil", bal, new Vector3(sx, 0.22f, 0f), new Vector3(0.003f, 0.15f, 0.003f), laiton, collider: false);
            Disc("Plateau", bal, new Vector3(sx, 0.145f, 0f), 0.09f, 0.008f, laiton, false);
        }
        Disc("Mortier", v, new Vector3(0.6f, ys[1] + 0.05f, 0.02f), 0.13f, 0.09f, Blanc, false);
        var pilon = Disc("Pilon", v, new Vector3(0.63f, ys[1] + 0.12f, 0.02f), 0.025f, 0.14f, Blanc, false);
        pilon.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
        Sign("Etiquette_Instruments", v, "Instruments du premier laboratoire de chimie", new Vector3(0f, ys[1] + 0.03f, P / 2f - 0.03f),
             face, new Vector2(0.8f, 0.05f), encre, null);

        // 3e tablette (hauteur des yeux) : modèles moléculaires boules-bâtonnets, couleurs CPK
        var molecules = new (string nom, string formule)[] {
            ("Eau", "H<sub>2</sub>O"), ("Dioxyde_Carbone", "CO<sub>2</sub>"), ("Methane", "CH<sub>4</sub>"),
            ("Ethanol", "C<sub>2</sub>H<sub>5</sub>OH"), ("Benzene", "C<sub>6</sub>H<sub>6</sub>") };
        for (int m = 0; m < molecules.Length; m++)
        {
            float mx = -0.76f + m * 0.38f;
            var g = Group($"Modele_{molecules[m].nom}", v, new Vector3(mx, ys[2] + 0.17f, 0f));
            Molecule(g, molecules[m].nom);
            Box("Socle", g, new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.02f, 0.08f), Noir, collider: false);
            Disc("Tige", g, new Vector3(0f, -0.09f, 0f), 0.008f, 0.13f, Metal, false);
            Sign("Formule", v, molecules[m].formule, new Vector3(mx, ys[2] + 0.03f, 0.12f), face, new Vector2(0.25f, 0.05f), encre, null);
        }

        // Tablette du haut : plaque du département et vieux livres
        Box("Plaque_Bois", v, new Vector3(-0.35f, ys[3] + 0.12f, -0.12f), new Vector3(0.8f, 0.22f, 0.02f), bois, collider: false);
        Sign("Plaque_Texte", v, "Collection historique\n<size=70%>Département de chimie et biochimie</size>", new Vector3(-0.35f, ys[3] + 0.12f, -0.105f),
             face, new Vector2(0.72f, 0.18f), laiton.color, null);
        Color[] livres = { new Color(0.45f, 0.1f, 0.1f), new Color(0.1f, 0.25f, 0.15f), new Color(0.15f, 0.15f, 0.35f), new Color(0.4f, 0.3f, 0.15f) };
        for (int b = 0; b < 4; b++)
            Box($"Livre_{b}", v, new Vector3(0.45f + b * 0.07f, ys[3] + 0.12f, -0.05f), new Vector3(0.06f, 0.22f + 0.02f * (b % 2), 0.16f),
                Mat($"Livre_{b}", livres[b], 0.3f), collider: false);
    }

    // Modèles boules-bâtonnets (C gris foncé, H blanc, O rouge) — environ 7 cm par liaison
    static void Molecule(Transform g, string nom)
    {
        var C = Mat("Atome_C", new Color(0.15f, 0.15f, 0.15f), 0.7f);
        var Hm = Mat("Atome_H", new Color(0.95f, 0.95f, 0.95f), 0.7f);
        var O = Mat("Atome_O", new Color(0.85f, 0.1f, 0.1f), 0.7f);
        var atomes = new List<(Vector3 p, Material m, float d)>();
        var liaisons = new List<(int a, int b)>();
        float L = 0.07f;
        switch (nom)
        {
            case "Eau":
                atomes.Add((Vector3.zero, O, 0.06f));
                atomes.Add((new Vector3(-0.055f, -0.045f, 0f), Hm, 0.04f));
                atomes.Add((new Vector3(0.055f, -0.045f, 0f), Hm, 0.04f));
                liaisons.Add((0, 1)); liaisons.Add((0, 2));
                break;
            case "Dioxyde_Carbone":
                atomes.Add((Vector3.zero, C, 0.055f));
                atomes.Add((new Vector3(-L, 0f, 0f), O, 0.06f));
                atomes.Add((new Vector3(L, 0f, 0f), O, 0.06f));
                liaisons.Add((0, 1)); liaisons.Add((0, 2));
                break;
            case "Methane":
                atomes.Add((Vector3.zero, C, 0.055f));
                foreach (var d in new[] { new Vector3(1, 1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(-1, -1, 1) })
                {
                    atomes.Add((d.normalized * L, Hm, 0.04f));
                    liaisons.Add((0, atomes.Count - 1));
                }
                break;
            case "Ethanol":
                atomes.Add((new Vector3(-0.05f, -0.02f, 0f), C, 0.055f));   // 0
                atomes.Add((new Vector3(0.02f, 0.02f, 0f), C, 0.055f));     // 1
                atomes.Add((new Vector3(0.09f, -0.02f, 0f), O, 0.06f));     // 2
                atomes.Add((new Vector3(0.14f, 0.02f, 0f), Hm, 0.04f));     // 3
                atomes.Add((new Vector3(-0.09f, 0.03f, 0.03f), Hm, 0.04f));
                atomes.Add((new Vector3(-0.07f, -0.06f, 0.04f), Hm, 0.04f));
                atomes.Add((new Vector3(-0.06f, -0.04f, -0.06f), Hm, 0.04f));
                atomes.Add((new Vector3(0.02f, 0.07f, 0.04f), Hm, 0.04f));
                atomes.Add((new Vector3(0.02f, 0.06f, -0.05f), Hm, 0.04f));
                liaisons.Add((0, 1)); liaisons.Add((1, 2)); liaisons.Add((2, 3));
                liaisons.Add((0, 4)); liaisons.Add((0, 5)); liaisons.Add((0, 6)); liaisons.Add((1, 7)); liaisons.Add((1, 8));
                break;
            case "Benzene":
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                    atomes.Add((new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.065f, C, 0.045f));
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                    atomes.Add((new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.115f, Hm, 0.03f));
                    liaisons.Add((i, (i + 1) % 6));
                    liaisons.Add((i, i + 6));
                }
                break;
        }
        for (int i = 0; i < atomes.Count; i++)
        {
            var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sph.name = $"Atome_{i}";
            sph.transform.SetParent(g, false);
            sph.transform.localPosition = atomes[i].p;
            sph.transform.localScale = Vector3.one * atomes[i].d;
            sph.GetComponent<Renderer>().sharedMaterial = atomes[i].m;
            Object.DestroyImmediate(sph.GetComponent<Collider>());
            PetitObjet(sph);
        }
        foreach (var li in liaisons)
        {
            Vector3 pa = atomes[li.a].p, pb = atomes[li.b].p;
            var t = Disc("Liaison", g, (pa + pb) / 2f, 0.01f, (pb - pa).magnitude, GrisPanneau, false);
            t.transform.localRotation = Quaternion.FromToRotation(Vector3.up, pb - pa);
        }
    }

    static void Babillard(string name, Transform parent, Vector3 pos, Quaternion rot, float w, float h)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Cadre", g, Vector3.zero, new Vector3(w + 0.06f, h + 0.06f, 0.03f), Metal, collider: false);
        Box("Liege", g, new Vector3(0f, 0f, -0.016f), new Vector3(w, h, 0.01f), Liege, collider: false);
        // Avis réalistes (format lettre) : séminaire, sécurité, horaire, offre d'emploi étudiant, club de chimie
        var feuilles = new[] { "Feuille_Seminaire", "Feuille_Securite", "Feuille_Horaire", "Feuille_Emploi", "Feuille_Club" };
        int nb = Mathf.Max(3, Mathf.FloorToInt((w - 0.1f) / 0.27f));
        int decal = 0; foreach (char ch in name) decal += ch; decal %= feuilles.Length;
        for (int i = 0; i < nb; i++)
        {
            float fx = -w / 2f + 0.16f + i * (w - 0.3f) / Mathf.Max(1, nb - 1);
            float fy = (i % 2 == 0 ? 0.14f : -0.12f) * h;
            string f = feuilles[(i + decal) % feuilles.Length];
            Box($"Feuille_{i}", g, new Vector3(fx, fy, -0.023f), new Vector3(0.216f, 0.279f, 0.003f),
                TexMat(f, 0.216f, 0.279f), collider: false, rot: Quaternion.Euler(0f, 0f, (i - nb / 2f) * 1.5f));
            Disc($"Punaise_{i}", g, new Vector3(fx, fy + 0.125f, -0.027f), 0.012f, 0.006f, Rouge, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    static void Poster(string name, Transform parent, Vector3 pos, Quaternion rot, string tex)
    {
        var g = Group(name, parent, pos);
        g.localRotation = rot;
        Box("Affiche", g, Vector3.zero, new Vector3(0.6f, 0.9f, 0.004f), TexMat(tex, 0.6f, 0.9f), collider: false);
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
                Gicleur(g, new Vector3(x, h, z + 0.3f));
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
            g.isStatic = false; // il tourne pendant l'alerte
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

        // Sons d'ambiance par pièce (portée courte : le son change quand on change de pièce)
        var amb = Group("Ambiances", sys, Vector3.zero);
        var neonClip = Clip("Neon_Bourdonnement");
        foreach (float nx in new[] { 12.5f, 19f })
        {
            var ng = new GameObject($"Neons_Corridor_{nx}");
            ng.transform.SetParent(amb, false);
            ng.transform.localPosition = new Vector3(nx, CorH - 0.2f, 0f);
            Source(ng, neonClip, true, true, 0.12f, 1f, 6f);
        }
        var ho = new GameObject("Hotte_Aspiration");
        ho.transform.SetParent(amb, false);
        ho.transform.localPosition = LabOrigin + new Vector3(11.4f, 2.2f, 3.8f);
        Source(ho, Clip("Hotte_Aspiration"), true, true, 0.3f, 1f, 7f);
        var hor = new GameObject("Horloge_Tic");
        hor.transform.SetParent(amb, false);
        hor.transform.localPosition = LabOrigin + new Vector3(11.8f, 2.5f, 6.6f);
        Source(hor, Clip("Horloge_Tic"), true, true, 0.25f, 1f, 4f);
        var nuitClip = Clip("Nuit_Exterieur");
        var nuitPos = new[] { new Vector3(21f, 1.5f, -6.5f), new Vector3(0f, 1.5f, 17f), new Vector3(20f, 1.5f, 14f) };
        for (int i = 0; i < nuitPos.Length; i++)
        {
            var ng = new GameObject($"Nuit_Dehors_{i}");
            ng.transform.SetParent(amb, false);
            ng.transform.localPosition = nuitPos[i];
            Source(ng, nuitClip, true, true, 0.4f, 1f, 11f);
        }

        // Fin : camion de pompiers derrière la clôture, gyrophares éteints jusqu'à l'évacuation
        var feux = new List<Object>();
        var feuxLumieres = new List<Object>();
        var camion = Camion(sys, new Vector3(17.5f, -0.04f, -19.6f), feux, feuxLumieres);
        Ambulance(sys, new Vector3(7.5f, -0.04f, -18.4f), feux, feuxLumieres);
        var sirenePomp = Source(camion.gameObject, Clip("Sirene_Pompiers"), true, false, 0.35f, 1f, 40f);
        var annEvac = new GameObject("Annonce_Evacuation");
        annEvac.transform.SetParent(sys, false);
        var sonAnnEvac = Source(annEvac, Clip("Annonce_Evacuation"), false, false, 0.9f, 0f, 50f);
        var zoneRass = Zone("Declencheur_Rassemblement", sys, new Vector3(21.5f, 1.5f, -7.4f), new Vector3(6f, 3f, 4.2f));
        UnityEventTools.AddPersistentListener(zoneRass.OnEntree, new UnityAction(seq.Evacuation));
        EditorUtility.SetDirty(zoneRass);

        var soS = new SerializedObject(seq);
        soS.FindProperty("alarme").objectReferenceValue = alarme;
        soS.FindProperty("fuite").objectReferenceValue = fuite;
        SetArray(soS, "zones", new List<Object> { zoneCor, zoneLab, zoneRass });
        soS.FindProperty("annonceEvacuation").objectReferenceValue = sonAnnEvac;
        soS.FindProperty("sirenePompiers").objectReferenceValue = sirenePomp;
        SetArray(soS, "lumieresPompiers", feuxLumieres);
        SetArray(soS, "feuxPompiers", feux);
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
        // SequenceAmbiance.OnEvacuationReussie (le joueur est au point de rassemblement = victoire),
        // PorteSortie.OnOpened, AlarmeController.FinProche() / Victoire().
    }

    // ------------------------------------------------------------------ VÉHICULES D'URGENCE (formes réelles)
    static Vector3 V3(Vector2 p, float z) => new Vector3(p.x, p.y, z);

    // Carrosserie : profil de côté (x vers l'avant, y vers le haut, points dans le sens inverse des aiguilles d'une montre)
    // extrudé sur la largeur, avec des arêtes chanfreinées pour un aspect arrondi comme une vraie tôle.
    static Vector2 NormaleExt(Vector2 a, Vector2 b) { Vector2 e = b - a; return new Vector2(e.y, -e.x).normalized; }

    static Mesh ProfilMesh(string key, Vector2[] p, float largeur, float ch, Vector2? centre = null)
    {
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var md = new MeshData();
        int n = p.Length;
        Vector2 c = Vector2.zero;
        for (int k = 0; k < n; k++) c += p[k];
        c /= n;
        if (centre.HasValue) c = centre.Value;          // point d'où l'on voit tout le contour (pour remplir les côtés)
        var pi = new Vector2[n];
        for (int k = 0; k < n; k++)                     // contour décalé vers l'intérieur (bissectrice des deux arêtes)
        {
            Vector2 nb = (NormaleExt(p[(k + n - 1) % n], p[k]) + NormaleExt(p[k], p[(k + 1) % n])).normalized;
            pi[k] = p[k] - nb * ch;
        }
        float zo = largeur / 2f, zi = zo - ch;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = p[k], b = p[(k + 1) % n], ia = pi[k], ib = pi[(k + 1) % n];
            Vector2 e = b - a;
            Vector3 nrm = new Vector3(e.y, -e.x, 0f).normalized;
            float l = e.magnitude;
            md.Quad(V3(a, -zi), V3(b, -zi), V3(b, zi), V3(a, zi), nrm, new Vector2(0f, -zi), new Vector2(l, -zi), new Vector2(l, zi), new Vector2(0f, zi));
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float s = sgn;
                Vector3 nc = (nrm + Vector3.forward * s).normalized;
                md.Quad(V3(a, s * zi), V3(b, s * zi), V3(ib, s * zo), V3(ia, s * zo), nc, a, b, ib, ia);
                md.Quad(V3(c, s * zo), V3(ia, s * zo), V3(ib, s * zo), V3(ib, s * zo), Vector3.forward * s, c, ia, ib, ib);
            }
        }
        var mesh = md.ToMesh(key);
        meshes[key] = mesh;
        return mesh;
    }

    static GameObject Carrosserie(string name, Transform parent, string key, Vector2[] profil, float largeur, float ch, Material mat, Vector2? centre = null)
    {
        var go = MeshGO(name, parent, ProfilMesh(key, profil, largeur, ch, centre), mat, Vector3.zero, Quaternion.identity);
        return go;
    }

    // Image collée sur un côté (z = ±zc) : lisible de l'extérieur des deux côtés
    static void Decal(string name, Transform parent, string tex, Vector3 centre, float w, float h)
        => Box(name, parent, centre, new Vector3(w, h, 0.002f), TexMat(tex, w, h), collider: false);
    // Image collée sur une face avant ou arrière (normale ±x)
    static void DecalX(string name, Transform parent, string tex, Vector3 centre, float w, float h)
        => Box(name, parent, centre, new Vector3(w, h, 0.002f), TexMat(tex, w, h), collider: false, rot: Quaternion.Euler(0f, 90f, 0f));

    // Roue de camion : pneu à flancs arrondis, jante en acier avec trous, moyeu et écrous ; jumelée à l'arrière.
    // s = côté (+1 droite / -1 gauche) ; zFace = position de la face extérieure.
    static void Roue(Transform parent, string name, float x, float zFace, float s, float diam, float larg, bool jumelee)
    {
        float R = diam / 2f;
        var pneu = Mat("Pneu", new Color(0.06f, 0.06f, 0.06f), 0.2f);
        var jante = Mat("Jante_Acier", new Color(0.78f, 0.79f, 0.8f), 0.75f);
        var passage = Mat("Passage_Roue", new Color(0.02f, 0.02f, 0.02f), 0.05f);
        int nb = jumelee ? 2 : 1;
        var g = Group(name, parent, new Vector3(x, R, 0f));
        // passage de roue sombre derrière le pneu
        var pr = Disc("Passage_Roue", g, new Vector3(0f, 0.03f, s * (zFace - 0.024f)), diam + 0.16f, 0.004f, passage, false);   // sur le flanc de la carrosserie
        pr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int j = 0; j < nb; j++)
        {
            float zc = zFace - larg / 2f - j * (larg + 0.02f);
            var r = Group(j == 0 ? "Pneu_Ext" : "Pneu_Int", g, new Vector3(0f, 0f, s * zc));
            r.localRotation = Quaternion.Euler(s > 0 ? 90f : -90f, 0f, 0f);   // axe y local = vers l'extérieur
            float h = larg / 2f;
            Lathe("Pneu", r, Vector3.zero, $"L_Pneu_{diam:0.00}_{larg:0.00}",
                  V(R * 0.64f, -h, R * 0.9f, -h, R * 0.98f, -h * 0.7f, R, -h * 0.3f, R, h * 0.3f, R * 0.98f, h * 0.7f, R * 0.9f, h, R * 0.64f, h), pneu);
            if (j > 0) continue;
            Lathe("Jante", r, Vector3.zero, $"L_Jante_{diam:0.00}_{larg:0.00}",
                  V(0, h * 0.2f, R * 0.18f, h * 0.2f, R * 0.22f, h * 0.45f, R * 0.5f, h * 0.45f, R * 0.6f, h * 0.7f, R * 0.66f, h * 0.95f, R * 0.66f, -h), jante);
            Disc("Moyeu", r, new Vector3(0f, h * 0.35f, 0f), R * 0.32f, h * 0.5f, jante, false);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                Disc("Ecrou", r, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * R * 0.26f, h * 0.55f, Mathf.Sin(a * Mathf.Deg2Rad) * R * 0.26f), 0.035f, 0.035f, Noir, false);
                Disc("Trou", r, new Vector3(Mathf.Cos((a + 22.5f) * Mathf.Deg2Rad) * R * 0.4f, h * 0.46f, Mathf.Sin((a + 22.5f) * Mathf.Deg2Rad) * R * 0.4f), R * 0.12f, 0.004f, passage, false);
            }
        }
    }

    // Rétroviseur de camion (grand miroir sur bras)
    static void Retroviseur(Transform parent, Vector3 attache, float s)
    {
        Tube("Bras_Retro", parent, attache, attache + new Vector3(0.05f, 0f, s * 0.22f), 0.025f, Metal);
        Tube("Bras_Retro_Bas", parent, attache + Vector3.down * 0.35f, attache + new Vector3(0.05f, -0.35f, s * 0.22f), 0.025f, Metal);
        Box("Miroir", parent, attache + new Vector3(0.05f, -0.15f, s * 0.24f), new Vector3(0.05f, 0.42f, 0.2f), Noir, collider: false);
        Box("Glace", parent, attache + new Vector3(0.022f, -0.15f, s * 0.24f), new Vector3(0.004f, 0.38f, 0.17f), Mat("Miroir", new Color(0.8f, 0.85f, 0.9f), 0.98f), collider: false);
    }

    // Rampe de gyrophares : socle noir, lentilles toujours visibles (éteintes) et lentilles allumées ajoutées à « feux »
    static void RampeGyrophares(Transform parent, Vector3 centre, float largeur, Material[] allume, Material[] eteint, List<Object> feux)
    {
        Box("Rampe_Socle", parent, centre, new Vector3(0.32f, 0.06f, largeur), Noir, collider: false);
        int n = 6;
        float w = largeur / n;
        for (int k = 0; k < n; k++)
        {
            int c = (k == 0 || k == n - 1 || k == 2) ? 0 : 1;      // couleurs alternées
            if (k == 3) c = 0;
            Vector3 p = centre + new Vector3(0f, 0.08f, -largeur / 2f + w * (k + 0.5f));
            Box("Lentille", parent, p, new Vector3(0.28f, 0.1f, w - 0.02f), eteint[c], collider: false);
            var f = Box("Lentille_Allumee", parent, p, new Vector3(0.29f, 0.105f, w - 0.015f), allume[c], collider: false, isStatic: false);
            var r = f.GetComponent<Renderer>(); r.enabled = false; feux.Add(r);
        }
    }

    static void LumiereGyro(Transform parent, string name, Vector3 pos, Color col, float portee, List<Object> lumieres)
    {
        var lg = new GameObject(name);
        lg.transform.SetParent(parent, false);
        lg.transform.localPosition = pos;
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.range = portee;
        l.intensity = 4f;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.enabled = false;
        lumieres.Add(l);
    }

    static void FeuFlash(Transform parent, string name, Vector3 pos, Vector3 taille, Material eteint, Material allume, List<Object> feux, Quaternion? rot = null)
    {
        Box(name, parent, pos, taille, eteint, collider: false, rot: rot);
        var f = Box(name + "_Allume", parent, pos, taille * 1.04f, allume, collider: false, isStatic: false, rot: rot);
        var r = f.GetComponent<Renderer>(); r.enabled = false; feux.Add(r);
    }

    // Camion de pompiers (autopompe) : cabine basculante à grand pare-brise, caisse à coffres à rideaux, échelle sur le toit,
    // lit de boyaux, pare-chocs chromé, roues jumelées, rampe de gyrophares rouge/blanc (allumée par SequenceAmbiance)
    static Transform Camion(Transform parent, Vector3 pos, List<Object> feux, List<Object> lumieres)
    {
        var c = Group("Camion_Pompiers", parent, pos);
        var rouge = Mat("Camion_Rouge", new Color(0.70f, 0.06f, 0.05f), 0.8f);
        var vitre = Mat("Vitre_Camion", new Color(0.05f, 0.07f, 0.09f), 0.95f);
        var chrome = Mat("Chrome", new Color(0.82f, 0.83f, 0.85f), 0.95f);
        var tole = Mat("Tole_Larmee", new Color(0.7f, 0.71f, 0.72f), 0.7f);
        const float LW = 2.45f, Z = LW / 2f;
        Carrosserie("Cabine", c, "P_Camion_Cabine", V(1.95f, 0.55f, 4.5f, 0.55f, 4.62f, 0.75f, 4.62f, 1.45f, 4.52f, 2.6f, 4.38f, 2.9f, 1.95f, 2.9f), LW, 0.07f, rouge);
        Carrosserie("Caisse", c, "P_Camion_Caisse", V(-4.4f, 0.6f, 1.9f, 0.6f, 1.9f, 2.65f, -4.4f, 2.65f), LW, 0.04f, rouge);
        ColliderOnly("Collision_Camion", c, new Vector3(0.1f, 1.6f, 0f), new Vector3(9.2f, 2.6f, LW + 0.1f), Quaternion.identity);
        foreach (float s in new[] { 1f, -1f })
        {
            Decal("Decal_Caisse", c, "Pompiers_Caisse", new Vector3(-1.275f, 1.625f, s * (Z + 0.002f)), 6.25f, 2.0f);
            Decal("Decal_Cabine", c, "Pompiers_Cabine", new Vector3(3.26f, 1.72f, s * (Z + 0.002f)), 2.55f, 2.25f);
            Box("Vitre_Equipe", c, new Vector3(2.62f, 2.12f, s * (Z + 0.006f)), new Vector3(0.95f, 0.8f, 0.006f), vitre, collider: false);
            Box("Vitre_Conducteur", c, new Vector3(3.78f, 2.12f, s * (Z + 0.006f)), new Vector3(0.85f, 0.8f, 0.006f), vitre, collider: false);
            Box("Marchepied_1", c, new Vector3(2.62f, 0.45f, s * (Z - 0.05f)), new Vector3(0.9f, 0.04f, 0.3f), tole, collider: false);
            Box("Marchepied_2", c, new Vector3(3.78f, 0.45f, s * (Z - 0.05f)), new Vector3(0.9f, 0.04f, 0.3f), tole, collider: false);
            Tube("Poignee_Montee", c, new Vector3(3.3f, 1.0f, s * (Z + 0.05f)), new Vector3(3.3f, 1.9f, s * (Z + 0.05f)), 0.03f, chrome);
            Tube("Main_Courante_Toit", c, new Vector3(-4.2f, 2.85f, s * (Z - 0.1f)), new Vector3(1.6f, 2.85f, s * (Z - 0.1f)), 0.035f, chrome);
            Retroviseur(c, new Vector3(4.45f, 2.45f, s * Z), s);
            Roue(c, "Roue_Avant", 3.55f, Z + 0.03f, s, 1.05f, 0.32f, false);
            Roue(c, "Roue_Arriere", -2.4f, Z + 0.03f, s, 1.05f, 0.32f, true);
            Box("Bavette", c, new Vector3(-2.95f, 0.45f, s * (Z - 0.18f)), new Vector3(0.01f, 0.5f, 0.6f), Noir, collider: false);
            Box("Phare", c, new Vector3(4.63f, 1.0f, s * 0.95f), new Vector3(0.02f, 0.2f, 0.32f), EmissiveMat("Phare", new Color(1f, 0.97f, 0.88f), 2f), collider: false);
            Box("Clignotant", c, new Vector3(4.63f, 1.22f, s * 0.95f), new Vector3(0.02f, 0.08f, 0.32f), Mat("Lentille_Ambre", new Color(1f, 0.6f, 0.1f), 0.8f), collider: false);
            FeuFlash(c, "Flash_Avant", new Vector3(4.635f, 1.45f, s * 0.95f), new Vector3(0.02f, 0.12f, 0.3f), Mat("Lentille_Rouge", new Color(0.5f, 0.05f, 0.05f), 0.9f), EmissiveMat("Feu_Pompiers_Rouge", new Color(1f, 0.05f, 0.05f), 4f), feux);
            FeuFlash(c, "Flash_Arriere", new Vector3(-4.405f, 2.4f, s * 1.05f), new Vector3(0.02f, 0.2f, 0.2f), Mat("Lentille_Rouge", new Color(0.5f, 0.05f, 0.05f), 0.9f), EmissiveMat("Feu_Pompiers_Rouge", new Color(1f, 0.05f, 0.05f), 4f), feux);
            Box("Feu_Arriere", c, new Vector3(-4.405f, 0.95f, s * 1.0f), new Vector3(0.02f, 0.22f, 0.22f), EmissiveMat("Feu_Arriere", new Color(0.9f, 0.05f, 0.05f), 1.2f), collider: false);
        }
        // Pare-brise (incliné), montant central, calandre, pare-chocs chromé, sirène
        Box("Pare_Brise", c, new Vector3(4.585f, 2.03f, 0f), new Vector3(0.01f, 1.1f, 2.2f), vitre, collider: false, rot: Quaternion.Euler(0f, 0f, 5f));
        Box("Montant_Central", c, new Vector3(4.59f, 2.03f, 0f), new Vector3(0.012f, 1.1f, 0.05f), Noir, collider: false, rot: Quaternion.Euler(0f, 0f, 5f));
        Box("Essuie_Glace", c, new Vector3(4.6f, 1.55f, -0.4f), new Vector3(0.015f, 0.015f, 0.7f), Noir, collider: false, rot: Quaternion.Euler(20f, 0f, 0f));
        DecalX("Calandre", c, "Pompiers_Calandre", new Vector3(4.624f, 1.2f, 0f), 2.3f, 0.95f);
        Box("Pare_Chocs", c, new Vector3(4.78f, 0.62f, 0f), new Vector3(0.3f, 0.3f, 2.5f), chrome, collider: false);
        Disc("Sirene", c, new Vector3(4.8f, 0.62f, 0.55f), 0.24f, 0.12f, Noir, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // Arrière : chevrons, plate-forme en tôle larmée, échelle et lit de boyaux sur le toit
        DecalX("Arriere", c, "Pompiers_Arriere", new Vector3(-4.402f, 1.625f, 0f), 2.35f, 2.0f);
        Box("Plateforme_Arriere", c, new Vector3(-4.6f, 0.55f, 0f), new Vector3(0.4f, 0.06f, 2.3f), tole, collider: false);
        Box("Chassis", c, new Vector3(0.1f, 0.42f, 0f), new Vector3(8.6f, 0.25f, 1.0f), Noir, collider: false);
        Box("Reservoir", c, new Vector3(0.6f, 0.45f, 0.8f), new Vector3(0.8f, 0.4f, 0.5f), Noir, collider: false);
        var couleurs = new[] { Mat("Boyau_Bleu", new Color(0.15f, 0.3f, 0.75f), 0.3f), Mat("Boyau_Jaune", new Color(0.95f, 0.8f, 0.15f), 0.3f), Mat("Boyau_Blanc", new Color(0.92f, 0.92f, 0.9f), 0.3f) };
        for (int k = 0; k < 9; k++)
            Box($"Boyau_{k}", c, new Vector3(-3.0f, 2.7f + (k / 3) * 0.08f, -0.7f + (k % 3) * 0.7f), new Vector3(2.4f, 0.08f, 0.62f), couleurs[(k + k / 3) % 3], collider: false);
        foreach (float sz in new[] { 0.35f, -0.35f })
        {
            Box("Echelle_Montant", c, new Vector3(-0.6f, 3.0f, sz), new Vector3(4.8f, 0.09f, 0.05f), Metal, collider: false);
            for (int k = 0; k < 3; k++)
                Box("Support_Echelle", c, new Vector3(-2.6f + k * 2f, 2.8f, sz), new Vector3(0.06f, 0.32f, 0.06f), Metal, collider: false);
        }
        for (int k = 0; k < 15; k++)
            Tube($"Echelon_{k}", c, new Vector3(-2.85f + k * 0.32f, 3.0f, -0.35f), new Vector3(-2.85f + k * 0.32f, 3.0f, 0.35f), 0.035f, Metal);
        // Rampe de gyrophares rouge / blanc sur le toit de la cabine
        var allume = new[] { EmissiveMat("Feu_Pompiers_Rouge", new Color(1f, 0.05f, 0.05f), 4f), EmissiveMat("Feu_Ambulance_Blanc", new Color(1f, 1f, 1f), 4f) };
        var eteint = new[] { GlassMat("Lentille_Rouge_Eteinte", new Color(0.6f, 0.05f, 0.05f), 0.85f), GlassMat("Lentille_Claire_Eteinte", new Color(0.85f, 0.85f, 0.9f), 0.7f) };
        RampeGyrophares(c, new Vector3(4.05f, 2.93f, 0f), 2.1f, allume, eteint, feux);
        LumiereGyro(c, "Lumiere_Rouge", new Vector3(4.05f, 3.5f, 0.6f), new Color(1f, 0.1f, 0.05f), 18f, lumieres);
        LumiereGyro(c, "Lumiere_Blanche", new Vector3(4.05f, 3.5f, -0.6f), new Color(1f, 0.95f, 0.9f), 18f, lumieres);
        return c;
    }

    // Ambulance (type III comme au Nouveau-Brunswick) : avant de fourgon (capot, pare-brise incliné), cellule de soins carrée,
    // damier réfléchissant rouge/jaune, étoile de vie, « AMBULANCE » à l'envers sur le capot, portes arrière, feux aux coins
    static void Ambulance(Transform parent, Vector3 pos, List<Object> feux, List<Object> lumieres)
    {
        var a = Group("Ambulance", parent, pos);
        var blanc = Mat("Ambulance_Blanc", new Color(0.95f, 0.95f, 0.94f), 0.8f);
        var vitre = Mat("Vitre_Camion", new Color(0.05f, 0.07f, 0.09f), 0.95f);
        var chrome = Mat("Chrome", new Color(0.82f, 0.83f, 0.85f), 0.95f);
        const float CW = 2.05f, MW = 2.35f;
        Carrosserie("Cabine", a, "P_Ambulance_Cabine", V(1.7f, 0.5f, 3.75f, 0.5f, 3.95f, 0.62f, 3.98f, 1.0f, 3.85f, 1.12f, 3.2f, 1.3f, 2.75f, 1.45f, 2.2f, 2.3f, 1.7f, 2.38f), CW, 0.1f, blanc, new Vector2(1.85f, 0.8f));
        Carrosserie("Cellule", a, "P_Ambulance_Cellule", V(-2.65f, 0.6f, 1.75f, 0.6f, 1.75f, 2.8f, -2.65f, 2.8f), MW, 0.06f, blanc);
        ColliderOnly("Collision_Ambulance", a, new Vector3(0.65f, 1.5f, 0f), new Vector3(6.7f, 2.6f, MW + 0.1f), Quaternion.identity);
        var rouge = Mat("Ambulance_Rouge", new Color(0.7f, 0.06f, 0.05f), 0.6f);
        foreach (float s in new[] { 1f, -1f })
        {
            Decal("Decal_Cellule", a, "Ambulance_Module", new Vector3(-0.45f, 1.7f, s * (MW / 2f + 0.002f)), 4.3f, 2.15f);
            Box("Bande_Cabine", a, new Vector3(2.7f, 0.98f, s * (CW / 2f + 0.002f)), new Vector3(1.9f, 0.1f, 0.002f), rouge, collider: false);
            Decal("Etoile_Porte", a, "Etoile_Vie", new Vector3(2.45f, 1.25f, s * (CW / 2f + 0.003f)), 0.32f, 0.32f);
            Box("Vitre_Porte", a, new Vector3(2.5f, 1.82f, s * (CW / 2f + 0.004f)), new Vector3(0.62f, 0.55f, 0.006f), vitre, collider: false);
            Box("Joint_Porte", a, new Vector3(2.88f, 1.2f, s * (CW / 2f + 0.004f)), new Vector3(0.012f, 1.2f, 0.004f), Noir, collider: false);
            Box("Poignee_Porte", a, new Vector3(2.15f, 1.45f, s * (CW / 2f + 0.01f)), new Vector3(0.14f, 0.03f, 0.02f), Noir, collider: false);
            Retroviseur(a, new Vector3(2.75f, 1.95f, s * CW / 2f), s);
            Roue(a, "Roue_Avant", 3.15f, CW / 2f + 0.02f, s, 0.8f, 0.24f, false);
            Roue(a, "Roue_Arriere", -1.2f, MW / 2f + 0.02f, s, 0.8f, 0.24f, true);
            Box("Phare", a, new Vector3(3.97f, 0.88f, s * 0.75f), new Vector3(0.03f, 0.16f, 0.32f), EmissiveMat("Phare", new Color(1f, 0.97f, 0.88f), 2f), collider: false);
            var feuR = EmissiveMat("Feu_Pompiers_Rouge", new Color(1f, 0.05f, 0.05f), 4f);
            var lentR = Mat("Lentille_Rouge", new Color(0.5f, 0.05f, 0.05f), 0.9f);
            FeuFlash(a, "Feu_Coin_Avant", new Vector3(1.755f, 2.62f, s * 1.0f), new Vector3(0.02f, 0.16f, 0.26f), lentR, feuR, feux);
            FeuFlash(a, "Feu_Coin_Arriere", new Vector3(-2.655f, 2.62f, s * 1.0f), new Vector3(0.02f, 0.16f, 0.26f), lentR, feuR, feux);
            FeuFlash(a, "Feu_Cote", new Vector3(-0.45f, 2.6f, s * (MW / 2f + 0.01f)), new Vector3(0.3f, 0.14f, 0.02f), lentR, feuR, feux);
            FeuFlash(a, "Feu_Calandre", new Vector3(3.99f, 0.98f, s * 0.35f), new Vector3(0.02f, 0.06f, 0.14f), lentR, feuR, feux);
            Box("Feux_Arriere", a, new Vector3(-2.655f, 1.0f, s * 1.05f), new Vector3(0.02f, 0.5f, 0.16f), EmissiveMat("Feu_Arriere", new Color(0.9f, 0.05f, 0.05f), 1.2f), collider: false);
            Box("Clignotant", a, new Vector3(-2.655f, 0.68f, s * 1.05f), new Vector3(0.02f, 0.12f, 0.16f), Mat("Lentille_Ambre", new Color(1f, 0.6f, 0.1f), 0.8f), collider: false);
        }
        // avant : pare-brise incliné, calandre, pare-chocs, capot avec « AMBULANCE » à l'envers
        Box("Pare_Brise", a, new Vector3(2.49f, 1.88f, 0f), new Vector3(0.01f, 1.0f, 1.86f), vitre, collider: false, rot: Quaternion.Euler(0f, 0f, 32.9f));
        Box("Calandre", a, new Vector3(3.985f, 0.82f, 0f), new Vector3(0.02f, 0.3f, 0.95f), Noir, collider: false);
        Box("Cadre_Calandre", a, new Vector3(3.982f, 0.82f, 0f), new Vector3(0.02f, 0.36f, 1.02f), chrome, collider: false);
        Box("Pare_Chocs", a, new Vector3(4.0f, 0.55f, 0f), new Vector3(0.14f, 0.18f, 2.05f), Noir, collider: false);
        Box("Capot_Inscription", a, new Vector3(3.3f, 1.297f, 0f), new Vector3(1.6f, 0.004f, 0.62f), TexMat("Ambulance_Capot", 1.6f, 0.62f), collider: false,
            rot: Quaternion.Euler(0f, 0f, -16.7f) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(0f, 0f, 0f));
        // arrière : portes (image), marchepied, poignées
        DecalX("Arriere", a, "Ambulance_Arriere", new Vector3(-2.652f, 1.7f, 0f), 2.3f, 2.15f);
        Box("Marchepied_Arriere", a, new Vector3(-2.8f, 0.52f, 0f), new Vector3(0.3f, 0.06f, 2.1f), Mat("Tole_Larmee", new Color(0.7f, 0.71f, 0.72f), 0.7f), collider: false);
        foreach (float sz in new[] { 0.2f, -0.2f })
            Box("Poignee_Arriere", a, new Vector3(-2.67f, 1.45f, sz), new Vector3(0.03f, 0.18f, 0.03f), chrome, collider: false);
        Box("Chassis", a, new Vector3(0.6f, 0.4f, 0f), new Vector3(5.8f, 0.2f, 0.9f), Noir, collider: false);
        // rampe de gyrophares rouge / blanc au-dessus de la cabine
        var allume = new[] { EmissiveMat("Feu_Pompiers_Rouge", new Color(1f, 0.05f, 0.05f), 4f), EmissiveMat("Feu_Ambulance_Blanc", new Color(1f, 1f, 1f), 4f) };
        var eteint = new[] { GlassMat("Lentille_Rouge_Eteinte", new Color(0.6f, 0.05f, 0.05f), 0.85f), GlassMat("Lentille_Claire_Eteinte", new Color(0.85f, 0.85f, 0.9f), 0.7f) };
        RampeGyrophares(a, new Vector3(1.6f, 2.84f, 0f), 2.0f, allume, eteint, feux);
        LumiereGyro(a, "Lumiere_Rouge", new Vector3(1.6f, 3.3f, 0.6f), new Color(1f, 0.1f, 0.05f), 14f, lumieres);
        LumiereGyro(a, "Lumiere_Blanche", new Vector3(1.6f, 3.3f, -0.6f), new Color(1f, 0.95f, 0.9f), 14f, lumieres);
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
        // Soirée : l'alerte arrive après les cours. Lune bleutée, ciel de nuit, lampadaires allumés dehors.
        var soleilGo = new GameObject("Lune");
        soleilGo.transform.SetParent(eclairage, false);
        soleilGo.transform.localRotation = Quaternion.Euler(40f, 200f, 0f);
        var soleil = soleilGo.AddComponent<Light>();
        soleil.type = LightType.Directional;
        soleil.color = new Color(0.55f, 0.65f, 0.90f);
        soleil.intensity = 0.18f;
        soleil.shadows = LightShadows.Soft;
        soleil.lightmapBakeType = LightmapBakeType.Baked;
        RenderSettings.sun = soleil;
        RenderSettings.skybox = CielNuit();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.09f);

        // Éclairage extérieur (précalculé) : marquise de l'entrée, porte de secours, lampadaire du point de rassemblement
        var ext = new[] {
            (nom: "Lumiere_Entree", pos: new Vector3(0f, 3.2f, 13.2f), portee: 9f, force: 1.6f),
            (nom: "Lumiere_Porte_Secours", pos: new Vector3(23f, 2.6f, -1.6f), portee: 7f, force: 1.4f),
            (nom: "Lumiere_Lampadaire", pos: new Vector3(24.8f, 3.3f, -4f), portee: 11f, force: 2.2f),
            (nom: "Lumiere_Rue_1", pos: new Vector3(8f, 4.6f, -15.2f), portee: 15f, force: 2.4f),
            (nom: "Lumiere_Rue_2", pos: new Vector3(26f, 4.6f, -15.2f), portee: 15f, force: 2.4f) };
        foreach (var e in ext)
        {
            var lg = new GameObject(e.nom);
            lg.transform.SetParent(eclairage, false);
            lg.transform.localPosition = e.pos;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.85f, 0.62f);
            l.range = e.portee;
            l.intensity = e.force;
            l.shadows = LightShadows.Soft;
            l.lightmapBakeType = LightmapBakeType.Baked;
        }

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

    // Ciel de nuit (Skybox procédural assombri) : le disque du « soleil » sert de lune
    static Material CielNuit()
    {
        string path = $"{MatFolder}/GB_Ciel_Nuit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_SunSize", 0.025f);
        m.SetFloat("_AtmosphereThickness", 0.45f);
        m.SetColor("_SkyTint", new Color(0.12f, 0.16f, 0.32f));
        m.SetColor("_GroundColor", new Color(0.04f, 0.04f, 0.05f));
        m.SetFloat("_Exposure", 0.32f);
        EditorUtility.SetDirty(m);
        return m;
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
        if (isStatic && Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.5f) PetitObjet(go);
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
        PetitObjet(go);
        return go;
    }

    // Petit accessoire : regroupé avec les autres (static batching) mais éclairé par les sondes de lumière.
    // Il ne prend pas de place dans les lightmaps : le calcul de la lumière reste rapide.
    static void PetitObjet(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.receiveGI = ReceiveGI.LightProbes;
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
    static void Sign(string name, Transform parent, string text, Vector3 pos, Quaternion rot, Vector2 size, Color color, Material fond,
                     TextAlignmentOptions align = TextAlignmentOptions.Center, bool gras = true, float tailleMax = 20f)
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
        t.alignment = align;
        t.fontStyle = gras ? FontStyles.Bold : FontStyles.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = tailleMax;
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
        m.enableInstancing = true; // moins d'appels de dessin sur le Quest
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

    // Matériau URP Lit transparent (alpha = opacité). Reflets conservés pour qu'on devine la vitre.
    static Material GlassMat(string name, Color color, float alpha)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;
        var m = Mat(name, new Color(color.r, color.g, color.b, alpha), 0.95f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Image (affiche, tableau) qui remplit exactement une surface de w × h mètres
    static Material TexMat(string tex, float w, float h)
    {
        string name = $"{tex}_{w:0.00}x{h:0.00}";
        bool nouveau = !mats.ContainsKey(name);
        var m = Mat(name, Color.white, 0.2f, tex, 1f);
        if (nouveau) { m.mainTextureScale = new Vector2(1f / w, 1f / h); EditorUtility.SetDirty(m); }
        return m;
    }

    // Liquide coloré (réactif dans un flacon) : légèrement transparent
    static Material Liquide(string name, Color c) => GlassMat("Liquide_" + name, c, 0.75f);

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
