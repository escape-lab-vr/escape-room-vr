// ZoneDoorPrefabBuilder.cs
// Rôle : environnement et systèmes (Brunel)
// Crée les prefabs de porte dans Assets/_Project/Prefabs :
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

        var bois   = Mat("Porte_Bois",   new Color(0.62f, 0.44f, 0.26f), 0.3f, false);
        var cadre  = Mat("Porte_Cadre",  new Color(0.25f, 0.15f, 0.10f), 0.3f, false);
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

        // Cadre (décor, sans collider : les murs de la greybox bloquent déjà les côtés)
        Cube("Cadre_Haut",   root.transform, new Vector3(0f, height + 0.05f, 0f), new Vector3(width + 0.2f, 0.1f, 0.2f), cadre, false);
        Cube("Cadre_Gauche", root.transform, new Vector3(-width / 2f - 0.05f, height / 2f, 0f), new Vector3(0.1f, height, 0.2f), cadre, false);
        Cube("Cadre_Droit",  root.transform, new Vector3( width / 2f + 0.05f, height / 2f, 0f), new Vector3(0.1f, height, 0.2f), cadre, false);

        // Battants (avec collider : ils bloquent le joueur et le rayon de téléportation)
        int n = isDouble ? 2 : 1;
        float w = width / n;
        var battants = new Transform[n];
        var deplacements = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float x = isDouble ? (i == 0 ? -w / 2f : w / 2f) : 0f;
            var b = Cube(isDouble ? (i == 0 ? "Battant_Gauche" : "Battant_Droit") : "Battant",
                         root.transform, new Vector3(x, height / 2f, 0f), new Vector3(w - 0.02f, height, 0.06f), bois, true);
            // Hublot et barre anti-panique, enfants du battant (ils glissent avec lui)
            Cube("Hublot", b, new Vector3(0f, 0.18f, 0f), Inverse(new Vector3(0.25f, 0.5f, 0.07f), b.localScale), verre, false);
            Cube("Barre",  b, new Vector3(0f, -0.05f, -0.6f), Inverse(new Vector3(w * 0.8f, 0.04f, 0.04f), b.localScale), metal, false);
            battants[i] = b;
            // Double : chaque battant glisse vers son côté. Simple : glisse vers la droite.
            float dir = isDouble ? (i == 0 ? -1f : 1f) : 1f;
            deplacements[i] = new Vector3(dir * (w + 0.05f), 0f, 0f);
        }

        // Voyant au-dessus de la porte, côté arrivée
        var v = Cube("Voyant", root.transform, new Vector3(0f, height + 0.25f, -0.12f), new Vector3(0.2f, 0.12f, 0.05f), voyantMat, false);

        // Branche les références dans le composant ZoneDoor
        var so = new SerializedObject(door);
        var pb = so.FindProperty("battants");
        var pd = so.FindProperty("deplacements");
        pb.arraySize = n; pd.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            pb.GetArrayElementAtIndex(i).objectReferenceValue = battants[i];
            pd.GetArrayElementAtIndex(i).vector3Value = deplacements[i];
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
