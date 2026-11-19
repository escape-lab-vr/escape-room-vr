// DeclencheurZone.cs
// Rôle : environnement et systèmes (Brunel)
// Zone invisible : quand la tête du joueur (caméra) y entre, l'événement OnEntree est appelé.
// Exemple : entrée dans le labo A-111 -> l'alarme se déclenche et le gaz sort.
// On vérifie la position de la caméra à chaque image : ça marche aussi avec la téléportation.
// Emplacement : Assets/_Project/Scripts/Environnement/DeclencheurZone.cs

using UnityEngine;
using UnityEngine.Events;

public class DeclencheurZone : MonoBehaviour
{
    [Tooltip("Taille de la zone en mètres (centrée sur cet objet).")]
    [SerializeField] Vector3 taille = new Vector3(4f, 3f, 4f);
    [Tooltip("Ce qui déclenche la zone. Vide = la caméra du joueur.")]
    [SerializeField] Transform cible;
    [Tooltip("Coché : ne se déclenche qu'une fois par partie.")]
    [SerializeField] bool uneSeuleFois = true;

    public UnityEvent OnEntree;

    public bool DejaDeclenchee { get; private set; }
    bool dedans;

    void Update()
    {
        if (cible == null)
        {
            if (Camera.main == null) return;
            cible = Camera.main.transform;
        }
        if (uneSeuleFois && DejaDeclenchee) return;

        Vector3 p = transform.InverseTransformPoint(cible.position);
        bool estDedans = Mathf.Abs(p.x) <= taille.x / 2f && Mathf.Abs(p.y) <= taille.y / 2f && Mathf.Abs(p.z) <= taille.z / 2f;
        if (estDedans && !dedans)
        {
            DejaDeclenchee = true;
            OnEntree?.Invoke();
        }
        dedans = estDedans;
    }

    /// <summary>Nouvelle partie : la zone peut de nouveau se déclencher.</summary>
    public void Reinitialiser()
    {
        DejaDeclenchee = false;
        dedans = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, taille);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, taille);
    }
}
