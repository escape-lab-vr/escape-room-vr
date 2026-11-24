// PorteBattante.cs
// Rôle : environnement et systèmes (Brunel)
// Porte d'armoire (ou tiroir) qu'on ouvre et ferme avec la manette : on la saisit (gâchette de préhension
// ou rayon) et elle bascule entre ouverte et fermée, en douceur.
// Le script se met sur le battant (qui a un collider) ; « pivot » est l'objet qui tourne (la charnière).
// Emplacement : Assets/_Project/Scripts/Environnement/PorteBattante.cs

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class PorteBattante : MonoBehaviour
{
    [Tooltip("Objet qui tourne ou glisse (la charnière). Vide = cet objet.")]
    [SerializeField] Transform pivot;
    [Tooltip("Rotation locale quand la porte est ouverte (degrés).")]
    [SerializeField] Vector3 angleOuvert = new Vector3(0f, -100f, 0f);
    [Tooltip("Déplacement local quand c'est un tiroir (mètres).")]
    [SerializeField] Vector3 glissementOuvert = Vector3.zero;
    [SerializeField] float duree = 0.45f;
    [SerializeField] bool ouverteAuDepart = false;
    [SerializeField] AudioSource son;

    public bool Ouverte { get; private set; }

    float t;
    Quaternion rotFermee;
    Vector3 posFermee;

    void Awake()
    {
        if (pivot == null) pivot = transform;
        rotFermee = pivot.localRotation;
        posFermee = pivot.localPosition;
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Basculer());
        Ouverte = ouverteAuDepart;
        t = Ouverte ? 1f : 0f;
        Appliquer();
    }

    /// <summary>Ouvre si c'est fermé, ferme si c'est ouvert.</summary>
    [ContextMenu("Basculer (test)")]
    public void Basculer()
    {
        Ouverte = !Ouverte;
        if (son != null) son.Play();
    }

    void Update()
    {
        float cible = Ouverte ? 1f : 0f;
        if (Mathf.Approximately(t, cible)) return;
        t = Mathf.MoveTowards(t, cible, Time.deltaTime / Mathf.Max(0.05f, duree));
        Appliquer();
    }

    void Appliquer()
    {
        float s = Mathf.SmoothStep(0f, 1f, t);
        pivot.localRotation = rotFermee * Quaternion.Euler(angleOuvert * s);
        pivot.localPosition = posFermee + glissementOuvert * s;
    }
}
