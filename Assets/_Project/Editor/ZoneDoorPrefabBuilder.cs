// ZoneDoorPrefabBuilder.cs
// Rôle : environnement et systèmes (Brunel)
// Crée les prefabs de porte dans Assets/_Project/Prefabs (portes battantes en bois, comme les vraies portes coupe-feu
// du pavillon : cadre en acier, fenêtre étroite en verre armé, barre anti-panique, ferme-porte, plaques de protection) :
//   - ZoneDoor_Double.prefab (1,8 m) : porte A, sortie de secours
//   - ZoneDoor_Simple.prefab (0,9 m) : porte du labo A-111
// Menu : Escape Lab > Créer les prefabs ZoneDoor
// Ce script doit être dans un dossier "Editor".

using UnityEditor;
using UnityEngine;

public static class ZoneDoorPrefabBuilder
{
    const string PrefabFolder = "Assets/_Project/Prefabs";
    const string MatFolder = "Assets/_Project/Materials/Portes";

    [MenuItem("Escape Lab/Créer les prefabs ZoneDoor")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/_Project", "Prefabs");
        EnsureFolder("Assets/_Project", "Materials");
        EnsureFolder("Assets/_Project/Materials", "Portes");

        var bois   = Mat("Porte_Bois",   new Color(0.78f, 0.56f, 0.32f), 0.35f, false);
        var cadre  = Mat("Porte_Cadre",  new Color(0.22f, 0.23f, 0.24f), 0.5f, false);
        var metal  = Mat("Porte_Metal",  new Color(0.70f, 0.72f, 0.75f), 0.8f, false);
        var verre  = Mat("Porte_Hublot", new Color(0.65f, 0.80f, 0.90f), 0.9f, false);
        var voyant = Mat("Porte_Voyant", new Color(1f, 0.1f, 0.1f), 0.5f, true);

        string p1 = Build("ZoneDoor_Double", 1.8f, 2.2f, true,  bois, cadre, metal, verre, voyant);
        string p2 = Build("ZoneDoor_Simple", 0.9f, 2.1f, false, bois, cadre, metal, verre, voyant);

        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(p1);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log($"Prefabs créés : {p1} et {p2}");
    }

    // Repère du prefab : origine = au sol, au centre de l'ouverture.
    // x = largeur, y = hauteur, z = épaisseur (z négatif = côté où l'on arrive).
    static string Build(string name, float width, float height, bool isDouble,
                        Material bois, Material cadre, Material metal, Material verre, Material voyantMat)
    {
        var root = new GameObject(name);
        var door = root.AddComponent<ZoneDoor>();

        // Son spatialisé : on l'entend venir de la porte
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 1f;
        src.maxDistance = 15f;

        // Cadre en acier (décor, sans collider : les murs de la greybox bloquent déjà les côtés)
        Cube("Cadre_Haut",   root.transform, new Vector3(0f, height + 0.05f, 0f), new Vector3(width + 0.2f, 0.1f, 0.24f), cadre, false);
        Cube("Cadre_Gauche", root.transform, new Vector3(-width / 2f - 0.05f, height / 2f, 0f), new Vector3(0.1f, height, 0.24f), cadre, false);
        Cube("Cadre_Droit",  root.transform, new Vector3( width / 2f + 0.05f, height / 2f, 0f), new Vector3(0.1f, height, 0.24f), cadre, false);
        Cube("Seuil", root.transform, new Vector3(0f, 0.005f, 0f), new Vector3(width, 0.01f, 0.2f), metal, false);

        // Battants sur charnières : un pivot au bord de l'ouverture, le battant est son enfant.
        // Ils pivotent vers +z (le côté opposé à l'arrivée), comme une vraie porte qu'on pousse.
        int n = isDouble ? 2 : 1;
        float w = width / n;
        var battants = new Transform[n];
        var rotations = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            bool gauche = !isDouble || i == 0;
            float sx = gauche ? 1f : -1f;                 // direction du battant depuis sa charnière
            var pivot = new GameObject(gauche ? "Pivot_Gauche" : "Pivot_Droit").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(gauche ? -width / 2f : width / 2f, 0f, 0f);
            var b = new GameObject("Battant").transform;
            b.SetParent(pivot, false);
            b.localPosition = new Vector3(sx * w / 2f, 0f, 0f);
            float lw = w - 0.01f;
            Cube("Panneau", b, new Vector3(0f, height / 2f, 0f), new Vector3(lw, height - 0.01f, 0.045f), bois, true);
            // Fenêtre étroite en verre armé, côté charnière
            float fx = -sx * (lw / 2f - 0.2f);
            Cube("Fenetre_Cadre", b, new Vector3(fx, 1.55f, 0f), new Vector3(0.16f, 0.66f, 0.05f), metal, false);
            Cube("Fenetre", b, new Vector3(fx, 1.55f, 0f), new Vector3(0.12f, 0.6f, 0.052f), verre, false);
            // Barre anti-panique côté arrivée (on pousse), poignée côté opposé
            Cube("Barre", b, new Vector3(0f, 1.0f, -0.06f), new Vector3(lw * 0.75f, 0.05f, 0.04f), metal, false);
            Cube("Barre_Support_A", b, new Vector3(-lw * 0.36f, 1.0f, -0.035f), new Vector3(0.05f, 0.1f, 0.03f), metal, false);
            Cube("Barre_Support_B", b, new Vector3( lw * 0.36f, 1.0f, -0.035f), new Vector3(0.05f, 0.1f, 0.03f), metal, false);
            Cube("Poignee", b, new Vector3(sx * (lw / 2f - 0.1f), 1.0f, 0.05f), new Vector3(0.03f, 0.3f, 0.03f), metal, false);
            // Plaques de protection en bas, des deux côtés
            Cube("Plaque_Bas_A", b, new Vector3(0f, 0.13f, -0.025f), new Vector3(lw - 0.05f, 0.25f, 0.004f), metal, false);
            Cube("Plaque_Bas_B", b, new Vector3(0f, 0.13f,  0.025f), new Vector3(lw - 0.05f, 0.25f, 0.004f), metal, false);
            // Charnières et ferme-porte
            for (int h = 0; h < 3; h++)
                Cube("Charniere", b, new Vector3(-sx * (lw / 2f), 0.25f + h * (height - 0.5f) / 2f, 0f), new Vector3(0.02f, 0.1f, 0.06f), metal, false);
            Cube("Ferme_Porte", b, new Vector3(-sx * (lw / 2f - 0.2f), height - 0.12f, 0.06f), new Vector3(0.3f, 0.07f, 0.06f), cadre, false);
            battants[i] = pivot;
            rotations[i] = new Vector3(0f, gauche ? -88f : 88f, 0f);
        }
        if (isDouble) Cube("Couvre_Joint", battants[1].GetChild(0), new Vector3(-w / 2f + 0.005f, height / 2f, -0.03f), new Vector3(0.03f, height - 0.02f, 0.01f), metal, false);

        // Voyant au-dessus de la porte, côté arrivée
        var v = Cube("Voyant", root.transform, new Vector3(0f, height + 0.25f, -0.12f), new Vector3(0.2f, 0.12f, 0.05f), voyantMat, false);

        // Branche les références dans le composant ZoneDoor
        var so = new SerializedObject(door);
        var pb = so.FindProperty("battants");
        var pd = so.FindProperty("deplacements");
        var pr = so.FindProperty("rotations");
        pb.arraySize = n; pd.arraySize = 0; pr.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            pb.GetArrayElementAtIndex(i).objectReferenceValue = battants[i];
            pr.GetArrayElementAtIndex(i).vector3Value = rotations[i];
        }
        so.FindProperty("voyant").objectReferenceValue = v.GetComponent<Renderer>();
        so.FindProperty("source").objectReferenceValue = src;
        so.FindProperty("sonDeverrouillage").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Environnement/Porte_Deverrouillage.wav");
        so.FindProperty("sonGlissement").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Environnement/Porte_Glissement.wav");
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{PrefabFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return path;
    }

    // Les enfants héritent de l'échelle du parent : on compense pour garder la taille voulue
    static Vector3 Inverse(Vector3 size, Vector3 parentScale) =>
        new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);

    static Transform Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
    }

    static Material Mat(string name, Color color, float smoothness, bool emissive)
    {
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", color * 2f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
