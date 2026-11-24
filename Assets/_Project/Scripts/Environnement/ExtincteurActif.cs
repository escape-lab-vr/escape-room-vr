// ExtincteurActif.cs
// Rôle : environnement et systèmes (Brunel)
// Extincteur qui fonctionne : on le prend en main, on appuie sur la gâchette de la manette (« activer »)
// et un nuage de poudre sort de la lance avec le bruit du jet ; on relâche la gâchette pour arrêter.
// La charge baisse : après environ 12 secondes d'utilisation, l'extincteur est vide.
// Emplacement : Assets/_Project/Scripts/Environnement/ExtincteurActif.cs

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ExtincteurActif : MonoBehaviour
{
    [SerializeField] ParticleSystem jet;
    [SerializeField] AudioSource son;
    [SerializeField] Transform levier;
    [SerializeField] float autonomie = 12f;

    float charge;
    bool appuye;
    Quaternion rotLevier;

    void Awake()
    {
        charge = autonomie;
        if (levier != null) rotLevier = levier.localRotation;
        var grab = GetComponent<XRBaseInteractable>();
        if (grab != null)
        {
            grab.activated.AddListener(_ => appuye = true);
            grab.deactivated.AddListener(_ => appuye = false);
            grab.selectExited.AddListener(_ => appuye = false);
        }
        Appliquer(false);
    }

    void Update()
    {
        bool actif = appuye && charge > 0f;
        if (actif) charge -= Time.deltaTime;
        Appliquer(actif);
    }

    void Appliquer(bool actif)
    {
        if (jet != null)
        {
            var em = jet.emission;
            em.enabled = actif;
            if (actif && !jet.isPlaying) jet.Play();
        }
        if (son != null) { if (actif && !son.isPlaying) son.Play(); else if (!actif && son.isPlaying) son.Stop(); }
        if (levier != null) levier.localRotation = rotLevier * Quaternion.Euler(0f, 0f, actif ? -12f : 0f);
    }

    /// <summary>Recharge l'extincteur (nouvelle partie).</summary>
    public void Recharger() => charge = autonomie;
}
