// EauActivable.cs
// Rôle : environnement et systèmes (Brunel)
// Robinet, lave-yeux ou douche d'urgence qu'on actionne avec la main (manette tout près) :
// la manette/la poignée bouge, l'eau apparaît et on entend l'eau couler. On actionne de nouveau pour fermer.
// Emplacement : Assets/_Project/Scripts/Environnement/EauActivable.cs

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class EauActivable : MonoBehaviour
{
    [Tooltip("Pièce qui bouge (manette du robinet, palette du lave-yeux, tirette de la douche). Vide = cet objet.")]
    [SerializeField] Transform poignee;
    [SerializeField] Vector3 angleOuvert = new Vector3(0f, 90f, 0f);
    [SerializeField] Vector3 glissementOuvert = Vector3.zero;
    [Tooltip("Objets « eau » affichés quand c'est ouvert (filet d'eau, jets, rideau de la douche).")]
    [SerializeField] GameObject[] eau;
    [SerializeField] AudioSource son;
    [SerializeField] float duree = 0.3f;

    public bool Ouvert { get; private set; }

    float t;
    Quaternion rot0;
    Vector3 pos0;

    void Awake()
    {
        if (poignee == null) poignee = transform;
        rot0 = poignee.localRotation;
        pos0 = poignee.localPosition;
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Basculer());
        Appliquer(false);
    }

    [ContextMenu("Ouvrir / fermer (test)")]
    public void Basculer()
    {
        Ouvert = !Ouvert;
        Appliquer(Ouvert);
    }

    void Appliquer(bool ouvert)
    {
        if (eau != null) foreach (var e in eau) if (e != null) e.SetActive(ouvert);
        if (son != null) { if (ouvert) son.Play(); else son.Stop(); }
    }

    void Update()
    {
        float cible = Ouvert ? 1f : 0f;
        if (Mathf.Approximately(t, cible)) return;
        t = Mathf.MoveTowards(t, cible, Time.deltaTime / Mathf.Max(0.05f, duree));
        poignee.localRotation = rot0 * Quaternion.Euler(angleOuvert * t);
        poignee.localPosition = pos0 + glissementOuvert * t;
    }
}
