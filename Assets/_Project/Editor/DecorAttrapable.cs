// Escape Lab VR — Environnement (Brunel)
// Rend quelques petits objets du décor du labo attrapables avec les manettes (XR Grab Interactable).
// Ce ne sont PAS des objets d'énigme : seulement du réalisme (cahier, pissettes, flacons de l'étagère de l'îlot B).
// À valider avec Jolin (interactions) avant de l'utiliser dans la scène finale.
// Menu : Escape Lab > Rendre quelques objets du décor attrapables
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DecorAttrapable
{
    static readonly string[] Noms = {
        "Cahier_Labo", "Pissette_0", "Pissette_1", "Pissette_2", "Pissette_3",
        "Flacon_1_0", "Flacon_1_2", "Flacon_1_4" };

    [MenuItem("Escape Lab/Rendre quelques objets du décor attrapables")]
    static void Menu()
    {
        var type = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit")
                ?? System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
        if (type == null)
        {
            EditorUtility.DisplayDialog("Escape Lab", "XR Interaction Toolkit introuvable dans le projet.", "OK");
            return;
        }
        var labo = GameObject.Find("Labo_A111");
        if (labo == null)
        {
            EditorUtility.DisplayDialog("Escape Lab", "Construis d'abord la greybox du pavillon.", "OK");
            return;
        }
        var trouves = new List<string>();
        foreach (var t in labo.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(Noms, t.name) < 0) continue;
            var go = t.gameObject;
            if (go.GetComponent(type) != null) continue;
            Undo.RegisterFullObjectHierarchyUndo(go, "Décor attrapable");
            foreach (var c in go.GetComponentsInChildren<Transform>(true)) c.gameObject.isStatic = false; // un objet qui bouge n'est pas statique

            // Collision : une boîte qui englobe tout l'objet
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = b.size;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.AddComponent(type);
            trouves.Add(t.name);
        }
        EditorSceneManager.MarkSceneDirty(labo.scene);
        EditorUtility.DisplayDialog("Escape Lab", trouves.Count == 0
            ? "Aucun nouvel objet (déjà attrapables ?)."
            : "Objets attrapables :\n" + string.Join("\n", trouves) + "\n\nSauvegarde la scène (Ctrl + S).", "OK");
    }
}
