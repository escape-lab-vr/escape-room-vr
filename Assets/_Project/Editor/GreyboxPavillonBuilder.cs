// GreyboxPavillonBuilder.cs
// Rôle : environnement et systèmes (Brunel)
// Construit la greybox du pavillon Rémi-Rossignol : hall (rotonde), corridor A, labo A-111.
// Utilisation : ouvrir sa scène de test, puis menu  Escape Lab > Construire la greybox du pavillon
// Ce script doit être dans un dossier "Editor" (il n'est pas inclus dans le build du jeu).
// Convention : 1 unité = 1 mètre, x vers l'est, z vers le nord, y vers le haut.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GreyboxPavillonBuilder
{
    const string MatFolder = "Assets/_Project/Materials/Greybox";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // Dimensions principales
    const float HallR = 9f;     // rayon du hall (18 m de diamètre)
    const float HallH = 3.5f;   // hauteur du hall
    const float CorH = 2.7f;    // hauteur du corridor
    const float LabH = 3f;      // hauteur du labo
    static readonly Vector3 LabOrigin = new Vector3(12f, 0f, 1.2f); // coin sud-ouest du labo

    // Portes du hall, d'après le dessin de Brunel. Angle vu de dessus, depuis le centre du hall.
    // On entre par le hall d'entrée (au nord, 90) en regardant vers le sud. Donc :
    //   0 = est = à GAUCHE en entrant     180 = ouest = à DROITE     270 = sud = en face
    // L'angle doit être un multiple de 15.
    // Grande = entrée de corridor (A, B, C, D) ; Simple = porte de bureau (« autre porte »), verrouillée.
    struct HallDoor { public string Letter; public float Angle; public bool ToCorridor; public bool Simple;
        public HallDoor(string l, float a, bool c, bool simple = false) { Letter = l; Angle = a; ToCorridor = c; Simple = simple; } }

    static readonly HallDoor[] HallDoors =
    {
        new HallDoor("A",   0f, true),         // à gauche : corridor A -> labo A-111
        new HallDoor("B", 315f, false),        // en face, à gauche (en diagonale)
        new HallDoor("C", 225f, false),        // en face, à droite (en diagonale)
        new HallDoor("D", 180f, false),        // à droite
        new HallDoor("",  255f, false, true),  // autres portes, au fond du hall
        new HallDoor("",  270f, false, true),
        new HallDoor("",  285f, false, true),
    };

    // Hall d'entrée (rectangle au nord du rond) avec les 4 portes vitrées
    const float VestHalfW = 5.5f;  // demi-largeur
    const float VestEnd = 12f;     // z du mur des portes vitrées
    const int VestFirstSeg = 4, VestLastSeg = 8; // segments du mur rond retirés (52,5° à 127,5°)

    // ------------------------------------------------------------------ MENU
    [MenuItem("Escape Lab/Construire la greybox du pavillon")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name == "MainScene")
        {
            EditorUtility.DisplayDialog("Greybox",
                "Ouvre ta scène de test (Test_Brunel). On ne modifie pas MainScene.", "OK");
            return;
        }

        var old = GameObject.Find("Greybox_Pavillon");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Greybox",
                "Une greybox existe déjà dans la scène. La remplacer ?", "Remplacer", "Annuler"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        mats.Clear();
        EnsureFolders();

        var root = new GameObject("Greybox_Pavillon");
        var plafonds = Group("Plafonds", root.transform, Vector3.zero); // à masquer pour voir de haut

        BuildHall(root.transform, plafonds);
        BuildCorridor(root.transform, plafonds);
        BuildLab(root.transform, plafonds);

        Undo.RegisterCreatedObjectUndo(root, "Greybox pavillon");
        PlacePlayer(scene);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

        Debug.Log("Greybox du pavillon créée : Hall, Corridor, Labo_A111. Pense à Ctrl+S.");
    }

    // ------------------------------------------------------------------ HALL
    static void BuildHall(Transform root, Transform plafonds)
    {
        var hall = Group("Hall", root, Vector3.zero);

        var mur      = Mat("Mur_Hall",   new Color(0.78f, 0.75f, 0.62f));
        var granit   = Mat("Granit",     new Color(0.30f, 0.30f, 0.32f), 0.6f);
        var terrazzo = Mat("Terrazzo",   new Color(0.82f, 0.72f, 0.66f), 0.7f);
        var rampeMat = Mat("Rampe",      new Color(0.20f, 0.15f, 0.12f), 0.5f);
        var boisMat  = Mat("Bois_Marches", new Color(0.45f, 0.30f, 0.20f));
        var vide     = Mat("Vide",       Color.black, 0f);
        var verre    = Mat("Verre",      new Color(0.65f, 0.80f, 0.90f), 0.9f);
        var plafond  = Mat("Plafond",    new Color(0.92f, 0.90f, 0.85f));
        var comptoir = Mat("Comptoir",   new Color(0.62f, 0.60f, 0.50f));
        var liege    = Mat("Liege",      new Color(0.72f, 0.55f, 0.35f));
        var papier   = Mat("Papier",     new Color(0.95f, 0.95f, 0.95f));
        var rouge    = Mat("Porte_A_Remplacer", new Color(0.80f, 0.15f, 0.15f));

        // Sol rond + trou de l'escalier (visuel seulement)
        Disc("Sol_Hall", hall, new Vector3(0f, -0.05f, 0f), HallR * 2f, 0.1f, terrazzo, true);
        Disc("Vide_Escalier", hall, new Vector3(0f, 0.005f, 0f), 5.8f, 0.01f, vide, false);

        // Mur circulaire : 24 segments de 15°. On saute un segment par ouverture (entrée + portes).
        var murs = Group("Murs", hall, Vector3.zero);
        const int N = 24;
        float L = 2f * HallR * Mathf.Tan(Mathf.PI / N) + 0.05f;
        var openings = new HashSet<int>();
        for (int i = VestFirstSeg; i <= VestLastSeg; i++) openings.Add(i);
        foreach (var d in HallDoors) if (!d.Simple) openings.Add(Mathf.RoundToInt(d.Angle / 15f) % N);
        for (int i = 0; i < N; i++)
        {
            if (openings.Contains(i)) continue;
            Vector3 dir = Dir(i * 360f / N);
            Box($"Mur_{i:00}", murs, dir * HallR + Vector3.up * (HallH / 2f),
                new Vector3(L, HallH, 0.2f), mur, rot: Quaternion.LookRotation(dir));
        }

        // Hall d'entrée : rectangle qui part du rond, avec 4 portes vitrées au bout (verrouillées : confinement)
        var vest = Group("Hall_Entree", hall, Vector3.zero);
        float zStart = Mathf.Sqrt(HallR * HallR - VestHalfW * VestHalfW); // où le rectangle touche le rond
        float vDepth = VestEnd - zStart;
        Box("Sol_Entree", vest, new Vector3(0f, -0.05f, (zStart + VestEnd) / 2f - 0.5f),
            new Vector3(VestHalfW * 2f, 0.1f, vDepth + 1f), terrazzo);
        Box("Mur_Entree_Est",   vest, new Vector3( VestHalfW, HallH / 2f, zStart + vDepth / 2f),
            new Vector3(0.2f, HallH, vDepth), mur);
        Box("Mur_Entree_Ouest", vest, new Vector3(-VestHalfW, HallH / 2f, zStart + vDepth / 2f),
            new Vector3(0.2f, HallH, vDepth), mur);
        // Mur du fond : 4 portes vitrées de 1,5 m au centre (6 m), murs pleins de chaque côté
        const float glassW = 6f;
        float side = VestHalfW - glassW / 2f;
        Box("Mur_Fond_Est",   vest, new Vector3( glassW / 2f + side / 2f, HallH / 2f, VestEnd), new Vector3(side, HallH, 0.2f), mur);
        Box("Mur_Fond_Ouest", vest, new Vector3(-glassW / 2f - side / 2f, HallH / 2f, VestEnd), new Vector3(side, HallH, 0.2f), mur);
        Box("Linteau_Entree", vest, new Vector3(0f, (2.5f + HallH) / 2f, VestEnd), new Vector3(glassW, HallH - 2.5f, 0.2f), mur);
        for (int i = 0; i < 4; i++)
            Box($"Porte_Vitree_{i + 1}", vest, new Vector3(-glassW / 2f + 0.75f + 1.5f * i, 1.25f, VestEnd),
                new Vector3(1.45f, 2.5f, 0.08f), verre);
        Box("Plafond_Entree", plafonds, new Vector3(0f, HallH + 0.05f, (zStart + VestEnd) / 2f - 0.5f),
            new Vector3(VestHalfW * 2f, 0.1f, vDepth + 1f), plafond, collider: false);

        // Entrées A, B, C, D : toutes identiques, comme sur les photos.
        // Chaque entrée = ouverture + plaque avec la lettre + portes coupe-feu ouvertes contre les murs
        // + un bout de corridor (damier) fermé par une double porte (confinement).
        // Seule la double porte de A sera remplacée par le prefab ZoneDoor.
        var portes  = Group("Entrees", hall, Vector3.zero);
        var bois    = Mat("Porte_Bois", new Color(0.62f, 0.44f, 0.26f));
        var cadre   = Mat("Cadre_Porte", new Color(0.25f, 0.15f, 0.10f));
        var plaque  = Mat("Plaque_Lettre", new Color(0.25f, 0.15f, 0.10f));
        var murCor  = Mat("Mur_Corridor", new Color(0.93f, 0.92f, 0.88f));
        var plafond2 = Mat("Plafond", new Color(0.92f, 0.90f, 0.85f));
        var damier  = CheckerMat("Damier_Court", new Vector2(4f, 4f));
        const float Depth = 2.2f;   // profondeur du bout de corridor jusqu'à la double porte
        int autre = 0;
        foreach (var d in HallDoors)
        {
            Vector3 dir = Dir(d.Angle);
            var rot = Quaternion.LookRotation(dir);

            // « Autre porte » : simple porte de bureau dans le mur, verrouillée
            if (d.Simple)
            {
                autre++;
                var a = Group($"Autre_Porte_{autre}", portes, dir * (HallR - 0.11f));
                a.localRotation = rot;
                Box("Porte", a, new Vector3(0f, 1.05f, 0f), new Vector3(1.0f, 2.1f, 0.04f), bois);
                Box("Cadre_Haut",   a, new Vector3(0f, 2.14f, 0f), new Vector3(1.16f, 0.08f, 0.05f), cadre, collider: false);
                Box("Cadre_Gauche", a, new Vector3(-0.54f, 1.07f, 0f), new Vector3(0.08f, 2.14f, 0.05f), cadre, collider: false);
                Box("Cadre_Droit",  a, new Vector3( 0.54f, 1.07f, 0f), new Vector3(0.08f, 2.14f, 0.05f), cadre, collider: false);
                continue;
            }

            Box($"Linteau_Porte{d.Letter}", murs, dir * HallR + Vector3.up * 2.85f,
                new Vector3(2.4f, 1.3f, 0.2f), mur, rot: rot);

            // Repère local de l'entrée : z vers l'extérieur du hall, x sur le côté
            var e = Group($"Entree_{d.Letter}", portes, dir * HallR);
            e.localRotation = rot;

            // Plaque avec la lettre, au-dessus de l'ouverture, côté hall
            Box($"Plaque_{d.Letter}", e, new Vector3(0f, 2.5f, -0.12f), new Vector3(0.5f, 0.4f, 0.02f),
                plaque, collider: false);
            Label($"Lettre_{d.Letter}", e, d.Letter, new Vector3(0f, 2.5f, -0.14f), Quaternion.identity);

            // Cadre de l'ouverture
            Box("Cadre_Gauche", e, new Vector3(-1.17f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.25f), cadre, collider: false);
            Box("Cadre_Droit",  e, new Vector3( 1.17f, 1.1f, 0f), new Vector3(0.08f, 2.2f, 0.25f), cadre, collider: false);

            // Portes coupe-feu ouvertes, rabattues contre les murs du corridor
            Box("Battant_Ouvert_Gauche", e, new Vector3(-1.1f, 1.05f, 0.65f), new Vector3(0.05f, 2.1f, 1.1f), bois);
            Box("Battant_Ouvert_Droit",  e, new Vector3( 1.1f, 1.05f, 0.65f), new Vector3(0.05f, 2.1f, 1.1f), bois);

            // Bout de corridor (pour A, le vrai corridor existe déjà)
            if (!d.ToCorridor)
            {
                Box("Sol", e, new Vector3(0f, -0.05f, Depth / 2f), new Vector3(2.4f, 0.1f, Depth), damier);
                Box("Mur_Gauche", e, new Vector3(-1.2f, CorH / 2f, Depth / 2f), new Vector3(0.2f, CorH, Depth), murCor);
                Box("Mur_Droit",  e, new Vector3( 1.2f, CorH / 2f, Depth / 2f), new Vector3(0.2f, CorH, Depth), murCor);
                Box("Plafond", plafonds, dir * (HallR + Depth / 2f) + Vector3.up * (CorH + 0.05f),
                    new Vector3(2.4f, 0.1f, Depth), plafond2, collider: false, rot: rot);
            }

            // Double porte fermée au fond (verrouillée par le confinement)
            if (d.ToCorridor)
                Box($"Porte{d.Letter}_ARemplacer", e, new Vector3(0f, 1.1f, Depth), new Vector3(2.2f, 2.2f, 0.1f),
                    rouge, isStatic: false);
            else
                Box($"Porte{d.Letter}_Verrouillee", e, new Vector3(0f, 1.1f, Depth), new Vector3(2.2f, 2.2f, 0.1f), bois);
            Box("Imposte", e, new Vector3(0f, (2.2f + CorH) / 2f, Depth), new Vector3(2.4f, CorH - 2.2f, 0.1f), murCor);
            Box("Barre_AntiPanique", e, new Vector3(0f, 1.0f, Depth - 0.08f), new Vector3(1.8f, 0.05f, 0.05f),
                Mat("Metal_Barre", new Color(0.70f, 0.72f, 0.75f), 0.8f), collider: false);
        }

        // 8 piliers en granit
        var piliers = Group("Piliers", hall, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Dir(22.5f + 45f * i);
            Box($"Pilier_{i}", piliers, dir * 6.5f + Vector3.up * (HallH / 2f),
                new Vector3(0.6f, HallH, 0.6f), granit, rot: Quaternion.LookRotation(dir));
        }

        // Rampe autour du trou de l'escalier (bloque le joueur)
        var rampe = Group("Rampe_Escalier", hall, Vector3.zero);
        const int NR = 24;
        float LR = 2f * 3f * Mathf.Tan(Mathf.PI / NR) + 0.02f;
        for (int i = 0; i < NR; i++)
        {
            Vector3 dir = Dir(i * 360f / NR);
            Box($"Rampe_{i:00}", rampe, dir * 3f + Vector3.up * 0.525f,
                new Vector3(LR, 1.05f, 0.05f), rampeMat, rot: Quaternion.LookRotation(dir));
        }

        // Escalier en spirale (décor) : colonne + 20 marches
        var esc = Group("Escalier_Spirale", hall, Vector3.zero);
        Disc("Colonne", esc, new Vector3(0f, HallH / 2f, 0f), 0.3f, HallH, granit, false);
        for (int i = 0; i < 20; i++)
        {
            Vector3 dir = Dir(i * 18f);
            Box($"Marche_{i:00}", esc, dir * 1.4f + Vector3.up * (0.17f * (i + 1)),
                new Vector3(0.5f, 0.05f, 1.6f), boisMat, collider: false,
                rot: Quaternion.LookRotation(dir));
        }

        // Comptoirs de chaque côté de la porte A
        Box("Comptoir_Nord", hall, new Vector3(7.6f, 0.5f, 2.0f), new Vector3(1.4f, 1.0f, 0.5f), comptoir);
        Box("Comptoir_Sud",  hall, new Vector3(7.6f, 0.5f, -2.0f), new Vector3(1.4f, 1.0f, 0.5f), comptoir);

        // Babillard (indice : code du tiroir) et plan d'évacuation (indice : A-111)
        Vector3 dB = Dir(135f);
        Box("Babillard", hall, dB * (HallR - 0.12f) + Vector3.up * 1.6f,
            new Vector3(1.5f, 1.0f, 0.04f), liege, collider: false, rot: Quaternion.LookRotation(dB));
        Vector3 dP = Dir(45f);
        Box("Plan_Evacuation", hall, dP * (HallR - 0.12f) + Vector3.up * 1.6f,
            new Vector3(0.6f, 0.8f, 0.03f), papier, collider: false, rot: Quaternion.LookRotation(dP));

        // Points pour l'équipe (objets vides)
        var pts = Group("Points", hall, Vector3.zero);
        Point("Point_Depart_Joueur", pts, new Vector3(0f, 0f, 10.5f), 180f);
        Point("Point_Tiroir_Comptoir", pts, new Vector3(7.3f, 0.85f, -2.0f));
        Point("Point_Indice_Babillard", pts, dB * (HallR - 0.2f) + Vector3.up * 1.6f);
        Point("Point_LecteurCarte_PorteA", pts, new Vector3(11.05f, 1.2f, -1.08f));

        // Plafond
        Disc("Plafond_Hall", plafonds, new Vector3(0f, HallH + 0.05f, 0f), HallR * 2f, 0.1f, plafond, false);
    }

    // ------------------------------------------------------------------ CORRIDOR
    static void BuildCorridor(Transform root, Transform plafonds)
    {
        var c = Group("Corridor", root, Vector3.zero);
        const float x0 = 8.8f, x1 = 24f;

        var mur     = Mat("Mur_Corridor", new Color(0.93f, 0.92f, 0.88f));
        var bois    = Mat("Bois_Vitrine", new Color(0.70f, 0.52f, 0.30f));
        var verre   = Mat("Verre", new Color(0.65f, 0.80f, 0.90f), 0.9f);
        var console = Mat("Console", new Color(0.25f, 0.27f, 0.30f), 0.4f);
        var plafond = Mat("Plafond", new Color(0.92f, 0.90f, 0.85f));
        var rouge   = Mat("Porte_A_Remplacer", new Color(0.80f, 0.15f, 0.15f));

        // Sol en damier rouge et blanc
        Box("Sol_Corridor", c, new Vector3((x0 + x1) / 2f, -0.05f, 0f),
            new Vector3(x1 - x0, 0.1f, 2.4f), CheckerMat("Damier", new Vector2(25f, 4f)));

        // Mur nord (z = 1.2) : c'est aussi le mur sud du labo -> hauteur 3 m
        // Ouverture : porte du labo en x = 13.5 (largeur 1 m, hauteur 2.1 m)
        WallAlongX("Mur_Nord", c, 1.2f, x0, x1, LabH, mur, new Vector3(13.5f, 1.0f, 2.1f));

        // Mur sud (z = -1.2) : porte de secours en x = 23 (largeur 1.8 m, hauteur 2.2 m)
        WallAlongX("Mur_Sud", c, -1.2f, x0, x1, CorH, mur, new Vector3(23f, 1.8f, 2.2f));

        // Mur du fond avec la fenêtre
        Box("Mur_Fond", c, new Vector3(x1, CorH / 2f, 0f), new Vector3(0.2f, CorH, 2.6f), mur);
        Box("Fenetre_Fond", c, new Vector3(x1 - 0.12f, 1.6f, 0f), new Vector3(0.02f, 1.2f, 1.8f),
            verre, collider: false);

        // Portes à remplacer par le prefab ZoneDoor
        Box("PorteLabo_ARemplacer", c, new Vector3(13.5f, 1.05f, 1.2f),
            new Vector3(1.0f, 2.1f, 0.08f), rouge, isStatic: false);
        Box("PorteSortie_ARemplacer", c, new Vector3(23f, 1.1f, -1.2f),
            new Vector3(1.8f, 2.2f, 0.08f), rouge, isStatic: false);

        // Vitrine des molécules (indice : ordre des fioles)
        Box("Vitrine_Molecules", c, new Vector3(12f, 1.0f, -0.95f), new Vector3(2.0f, 2.0f, 0.45f), bois);

        // Console finale (clavier + levier) près de la sortie
        Box("Console_Code_Levier", c, new Vector3(21.6f, 0.55f, -0.95f),
            new Vector3(0.8f, 1.1f, 0.45f), console);

        var pts = Group("Points", c, Vector3.zero);
        Point("Point_Indice_Molecule", pts, new Vector3(12f, 1.3f, -0.7f));
        Point("Point_LecteurCarte_Labo", pts, new Vector3(14.2f, 1.2f, 1.08f));
        Point("Point_Clavier", pts, new Vector3(21.6f, 1.15f, -0.8f));
        Point("Point_Levier", pts, new Vector3(22.1f, 1.15f, -0.8f));
        Point("Point_Alarme_1", pts, new Vector3(11f, 2.3f, 1.08f));
        Point("Point_Alarme_2", pts, new Vector3(19f, 2.3f, -1.08f));

        Box("Plafond_Corridor", plafonds, new Vector3((x0 + x1) / 2f, CorH + 0.05f, 0f),
            new Vector3(x1 - x0, 0.1f, 2.4f), plafond, collider: false);
    }

    // ------------------------------------------------------------------ LABO A-111
    // Toutes les positions ci-dessous sont LOCALES au labo (origine = coin sud-ouest).
    static void BuildLab(Transform root, Transform plafonds)
    {
        var lab = Group("Labo_A111", root, LabOrigin);

        var mur      = Mat("Mur_Labo",     new Color(0.90f, 0.89f, 0.84f));
        var sol      = Mat("Sol_Labo",     new Color(0.92f, 0.90f, 0.84f), 0.5f);
        var meuble   = Mat("Meuble_Brun",  new Color(0.30f, 0.18f, 0.12f), 0.4f);
        var dessus   = Mat("Dessus_Noir",  new Color(0.06f, 0.06f, 0.06f), 0.7f);
        var verre    = Mat("Verre",        new Color(0.65f, 0.80f, 0.90f), 0.9f);
        var metal    = Mat("Metal",        new Color(0.70f, 0.72f, 0.75f), 0.8f);
        var jaune    = Mat("Armoire_Jaune", new Color(0.95f, 0.80f, 0.10f));
        var blanc    = Mat("Tableau_Blanc", Color.white, 0.8f);
        var hotteMat = Mat("Hotte",        new Color(0.80f, 0.82f, 0.85f), 0.5f);
        var plafond  = Mat("Plafond",      new Color(0.92f, 0.90f, 0.85f));

        Box("Sol_Labo", lab, new Vector3(6f, -0.05f, 4.5f), new Vector3(12f, 0.1f, 9f), sol);

        // Murs (le mur sud est le mur nord du corridor)
        Box("Mur_Ouest", lab, new Vector3(0f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), mur);
        Box("Mur_Est",   lab, new Vector3(12f, LabH / 2f, 4.5f), new Vector3(0.2f, LabH, 9f), mur);

        // Mur nord avec les 8 fenêtres
        Box("Mur_Nord_Bas",  lab, new Vector3(6f, 0.5f, 9f), new Vector3(12.2f, 1.0f, 0.2f), mur);
        Box("Mur_Nord_Haut", lab, new Vector3(6f, 2.8f, 9f), new Vector3(12.2f, 0.4f, 0.2f), mur);
        Box("Fenetres",      lab, new Vector3(6f, 1.8f, 9f), new Vector3(12f, 1.6f, 0.05f), verre);
        for (int i = 1; i < 8; i++)
            Box($"Montant_{i}", lab, new Vector3(1.5f * i, 1.8f, 9f), new Vector3(0.3f, 1.6f, 0.25f), mur);

        // Îlot A (énigme : bécher + évaporateurs)
        Box("IlotA_Meuble", lab, new Vector3(4.5f, 0.44f, 3f), new Vector3(6f, 0.88f, 1.6f), meuble);
        Box("IlotA_Dessus", lab, new Vector3(4.5f, 0.9f, 3f), new Vector3(6.05f, 0.04f, 1.65f), dessus);

        // Îlot B (décor)
        Box("IlotB_Meuble", lab, new Vector3(6.5f, 0.44f, 6f), new Vector3(8f, 0.88f, 1.6f), meuble);
        Box("IlotB_Dessus", lab, new Vector3(6.5f, 0.9f, 6f), new Vector3(8.05f, 0.04f, 1.65f), dessus);
        Box("Evier", lab, new Vector3(10.1f, 0.93f, 6f), new Vector3(0.6f, 0.03f, 0.5f), metal, collider: false);

        // Paillasse basse sous les fenêtres
        Box("Paillasse_Fenetres", lab, new Vector3(6f, 0.45f, 8.6f), new Vector3(11.6f, 0.9f, 0.6f), meuble);

        // Étagère des fioles (mur ouest), hotte (mur est), armoire jaune, chariot
        Box("Etagere_Fioles", lab, new Vector3(0.3f, 1.0f, 5f), new Vector3(0.4f, 2.0f, 1.6f), meuble);
        Box("Hotte", lab, new Vector3(11.5f, 1.2f, 3f), new Vector3(0.8f, 2.4f, 1.5f), hotteMat);
        Box("Armoire_Jaune", lab, new Vector3(0.6f, 0.9f, 0.4f), new Vector3(1.0f, 1.8f, 0.5f), jaune);
        Box("Chariot", lab, new Vector3(3f, 0.45f, 0.6f), new Vector3(0.8f, 0.9f, 0.5f), metal);

        // Douche de sécurité (décor simple)
        Disc("Douche_Tuyau", lab, new Vector3(0.6f, 1.15f, 1.2f), 0.05f, 2.3f, metal, false);

        // Tableau blanc (indice : couleur cible) et horloge (minuteur)
        Box("Tableau_Blanc", lab, new Vector3(7f, 1.6f, 0.13f), new Vector3(2f, 1f, 0.03f), blanc, collider: false);
        var horloge = Disc("Horloge", lab, new Vector3(11.88f, 2.3f, 6.5f), 0.4f, 0.02f, blanc, false);
        horloge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Points pour l'équipe
        var pts = Group("Points", lab, Vector3.zero);
        Point("Point_Fioles",  pts, new Vector3(0.7f, 1.2f, 5f));
        Point("Point_Becher",  pts, new Vector3(4.5f, 0.95f, 3f));
        Point("Point_Evier",   pts, new Vector3(10.1f, 0.95f, 6f));
        Point("Point_Hotte",   pts, new Vector3(11f, 1.2f, 3f));
        Point("Point_Horloge", pts, new Vector3(11.85f, 2.3f, 6.5f));
        Point("Ancre_Teleport_Ilot",    pts, new Vector3(4.5f, 0f, 1.8f), 0f);
        Point("Ancre_Teleport_Etagere", pts, new Vector3(1.3f, 0f, 5f), -90f);

        // Plafond du labo (le mur nord du corridor monte à 3 m pour fermer le labo)
        Box("Plafond_Labo", plafonds, LabOrigin + new Vector3(6f, LabH + 0.05f, 4.5f),
            new Vector3(12f, 0.1f, 9f), plafond, collider: false);
    }

    // ------------------------------------------------------------------ JOUEUR
    // Déplace le XR Origin de la scène sur le point de départ (devant l'entrée, face au sud).
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

    // Texte 3D simple (lettre de porte). Lisible quand on regarde dans la direction "rot".
    static void Label(string name, Transform parent, string text, Vector3 localPos, Quaternion rot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 100;
        tm.characterSize = 0.03f;
        tm.color = Color.white;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        go.isStatic = true;
    }

    static Transform Point(string name, Transform parent, Vector3 localPos, float yRotation = 0f)
    {
        var t = Group(name, parent, localPos);
        t.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        return t;
    }

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
                          bool collider = true, bool isStatic = true, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = isStatic;
        return go;
    }

    // Cylindre plat (sol rond, plafond rond, colonne). Le cylindre Unity fait 2 m de haut à l'échelle 1.
    static GameObject Disc(string name, Transform parent, Vector3 localPos, float diameter, float height,
                           Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // le collider par défaut est une capsule
        if (collider) go.AddComponent<MeshCollider>();
        go.isStatic = true;
        return go;
    }

    // Mur droit le long de l'axe X (à z fixe), avec des ouvertures de porte.
    // Chaque porte : x = centre, y = largeur, z = hauteur. Les portes doivent être triées par x.
    static void WallAlongX(string name, Transform parent, float z, float xFrom, float xTo, float h,
                           Material mat, params Vector3[] doors)
    {
        var g = Group(name, parent, Vector3.zero);
        float cur = xFrom;
        int k = 0;
        foreach (var d in doors)
        {
            float a = d.x - d.y / 2f, b = d.x + d.y / 2f;
            if (a > cur)
                Box($"{name}_{k++}", g, new Vector3((cur + a) / 2f, h / 2f, z), new Vector3(a - cur, h, 0.2f), mat);
            Box($"{name}_Linteau_{k++}", g, new Vector3(d.x, (d.z + h) / 2f, z), new Vector3(d.y, h - d.z, 0.2f), mat);
            cur = b;
        }
        if (xTo > cur)
            Box($"{name}_{k}", g, new Vector3((cur + xTo) / 2f, h / 2f, z), new Vector3(xTo - cur, h, 0.2f), mat);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials")) AssetDatabase.CreateFolder("Assets/_Project", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Greybox");
    }

    // Matériau URP enregistré dans Assets/_Project/Materials/Greybox (réutilisé s'il existe déjà)
    static Material Mat(string name, Color color, float smoothness = 0.2f)
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
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    // Damier rouge et blanc du corridor (carreaux de 30 cm)
    static Material CheckerMat(string name, Vector2 tiles)
    {
        if (mats.TryGetValue(name, out var cached)) return cached;

        string texPath = $"{MatFolder}/GB_Damier_Texture.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            var red = new Color(0.70f, 0.32f, 0.32f);
            var cream = new Color(0.93f, 0.90f, 0.84f);
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { red, cream, cream, red });
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            AssetDatabase.CreateAsset(tex, texPath);
        }

        var m = Mat(name, Color.white, 0.6f);
        m.mainTexture = tex;
        m.mainTextureScale = tiles; // un carreau de damier ≈ 0,6 m
        EditorUtility.SetDirty(m);
        return m;
    }
}
