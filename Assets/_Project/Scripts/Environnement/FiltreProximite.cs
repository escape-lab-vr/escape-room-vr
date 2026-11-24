// FiltreProximite.cs
// Rôle : environnement et systèmes (Brunel)
// Comme dans la vraie vie : on ne peut ouvrir une armoire ou prendre un objet que si la main est tout près.
// Le rayon de la manette ne peut plus « attraper à distance » les objets du décor.
// Le script se met sur un objet interactif (XR Grab Interactable ou XR Simple Interactable).
// Emplacement : Assets/_Project/Scripts/Environnement/FiltreProximite.cs

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FiltreProximite : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Tooltip("Distance maximale entre la main (manette) et l'objet, en mètres.")]
    [SerializeField] float distanceMax = 0.35f;

    XRBaseInteractable interactable;

    public bool canProcess => isActiveAndEnabled;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null) return;
        interactable.selectFilters.Add(this);
        interactable.hoverFilters.Add(this);
    }

    void OnDestroy()
    {
        if (interactable == null) return;
        interactable.selectFilters.Remove(this);
        interactable.hoverFilters.Remove(this);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable cible) => Proche(interactor.transform.position);
    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable cible) => Proche(interactor.transform.position);

    bool Proche(Vector3 main)
    {
        if (interactable == null) return true;
        float min = float.MaxValue;
        foreach (var c in interactable.colliders)
        {
            if (c == null || !c.enabled) continue;
            min = Mathf.Min(min, Vector3.Distance(main, c.ClosestPoint(main)));
        }
        return min <= distanceMax;
    }
}
