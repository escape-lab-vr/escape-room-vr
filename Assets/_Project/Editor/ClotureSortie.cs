// Escape Lab VR — Environnement (Brunel)
// Clôture autour de la zone extérieure de la sortie de secours (point de rassemblement).
// Empêche le joueur de marcher hors du terrain et de tomber dans le vide.
// Menu : Escape Lab > Clôturer la sortie extérieure
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClotureSortie
{
    // Zone praticable dehors : de la façade du corridor (z = -1,2) jusqu'au point de rassemblement.
    const float XOuest = 9.5f, XEst = 24.6f, ZNord = -1.2f, ZSud = -9.8f;
    const float HauteurCloture = 1.1f, HauteurMurInvisible = 3f;

    [MenuItem("Escape Lab/Clôturer la sortie extérieure")]
    static void Menu()
    {
        var sortie = GameObject.Find("Sortie_Secours");
        Transform parent = null;
        if (sortie != null)
        {
            // « Sortie_Secours » existe aussi comme panneau dans le corridor : on prend le groupe sous « Exterieur ».
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Sortie_Secours" && t.parent != null && t.parent.name == "Exterieur") { parent = t; break; }
        }
        var ancien = parent != null ? parent.Find("Cloture_Sortie") : null;
        if (ancien != null) Undo.DestroyObjectImmediate(ancien.gameObject);

        var go = Construire(parent);
        Undo.RegisterCreatedObjectUndo(go, "Clôturer la sortie");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Escape Lab",
            "Clôture ajoutée autour du point de rassemblement.\nSauvegarde la scène (Ctrl + S).", "OK");
    }

    public static GameObject Construire(Transform parent)
    {
        var groupe = new GameObject("Cloture_Sortie");
        if (parent != null) groupe.transform.SetParent(parent, false);
        var metal = Materiau();

        // Trois côtés ouverts : ouest (vers le hall), sud (derrière le panneau), est (bout du bâtiment).
        Cote(groupe.transform, "Ouest", new Vector3(XOuest, 0f, ZNord), new Vector3(XOuest, 0f, ZSud), metal);
        Cote(groupe.transform, "Sud",   new Vector3(XOuest, 0f, ZSud),  new Vector3(XEst,   0f, ZSud), metal);
        Cote(groupe.transform, "Est",   new Vector3(XEst,   0f, ZSud),  new Vector3(XEst,   0f, ZNord), metal);
        return groupe;
    }

    static void Cote(Transform parent, string nom, Vector3 a, Vector3 b, Material mat)
    {
        var cote = new GameObject("Cloture_" + nom);
        cote.transform.SetParent(parent, false);
        Vector3 dir = b - a;
        float longueur = dir.magnitude;
        Vector3 milieu = (a + b) / 2f;
        var rot = Quaternion.LookRotation(dir.normalized);

        // Poteaux tous les 2 m environ
        int n = Mathf.Max(1, Mathf.RoundToInt(longueur / 2f));
        for (int i = 0; i <= n; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, (float)i / n);
            Cube($"Poteau_{i}", cote.transform, p + Vector3.up * (HauteurCloture / 2f),
                 new Vector3(0.06f, HauteurCloture, 0.06f), rot, mat);
        }
        // Deux lisses horizontales
        Cube("Lisse_Haut", cote.transform, milieu + Vector3.up * (HauteurCloture - 0.05f),
             new Vector3(0.04f, 0.04f, longueur), rot, mat);
        Cube("Lisse_Bas", cote.transform, milieu + Vector3.up * 0.45f,
             new Vector3(0.04f, 0.04f, longueur), rot, mat);

        // Mur invisible plus haut que la clôture : on ne peut ni l'enjamber ni s'y téléporter de l'autre côté
        var mur = new GameObject("Mur_Invisible");
        mur.transform.SetParent(cote.transform, false);
        mur.transform.localPosition = milieu + Vector3.up * (HauteurMurInvisible / 2f);
        mur.transform.localRotation = rot;
        mur.AddComponent<BoxCollider>().size = new Vector3(0.2f, HauteurMurInvisible, longueur + 0.2f);
        mur.isStatic = true;
    }

    static void Cube(string nom, Transform parent, Vector3 pos, Vector3 taille, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nom;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // la collision est gérée par le mur invisible
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = taille;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    static Material Materiau()
    {
        // On réutilise le matériau du lampadaire s'il existe, sinon on en crée un.
        foreach (var guid in AssetDatabase.FindAssets("Poteau_Lampadaire t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) return m;
        }
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = "Cloture_Metal", color = new Color(0.18f, 0.18f, 0.20f) };
        const string dossier = "Assets/_Project/Materials";
        if (AssetDatabase.IsValidFolder(dossier))
            AssetDatabase.CreateAsset(mat, dossier + "/Cloture_Metal.mat");
        return mat;
    }
}
