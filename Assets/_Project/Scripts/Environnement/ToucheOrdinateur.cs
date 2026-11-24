// ToucheOrdinateur.cs
// Rôle : environnement et systèmes (Brunel)
// Se met sur la souris, le clavier et l'écran du poste : quand la main les touche, le poste réagit (EcranOrdinateur).
// Emplacement : Assets/_Project/Scripts/Environnement/ToucheOrdinateur.cs

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class ToucheOrdinateur : MonoBehaviour
{
    [SerializeField] EcranOrdinateur ecran;

    void Awake()
    {
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => { if (ecran != null) ecran.Action(); });
    }
}
