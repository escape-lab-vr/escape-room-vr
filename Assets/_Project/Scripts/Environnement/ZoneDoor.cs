// ZoneDoor.cs
// Rôle : environnement et systèmes (Brunel)
// Porte coulissante (double ou simple) qui sépare deux zones de l'escape room.
//   - Verrouillée au départ : voyant rouge, les battants bloquent le passage et la téléportation.
//   - Open() : appelée par l'énigme de la zone (événement OnSolved de Jolin). Son + glissement + voyant vert.
//   - ResetDoor() : revient à l'état verrouillé (nouvelle partie).
// Emplacement : Assets/_Project/Scripts/Environnement/ZoneDoor.cs

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ZoneDoor : MonoBehaviour
{
    public enum DoorState { Verrouillee, Ouverture, Ouverte }

    [Header("Battants qui glissent")]
    [Tooltip("Les battants de la porte (1 pour une porte simple, 2 pour une double porte).")]
    [SerializeField] Transform[] battants;
    [Tooltip("Déplacement local de chaque battant à l'ouverture (même ordre que la liste des battants).")]
    [SerializeField] Vector3[] deplacements;
    [Tooltip("Durée de l'ouverture, en secondes.")]
    [SerializeField] float dureeOuverture = 1.2f;

    [Header("Voyant")]
    [SerializeField] Renderer voyant;
    [SerializeField] Color couleurVerrouillee = new Color(1f, 0.1f, 0.1f);
    [SerializeField] Color couleurOuverte = new Color(0.1f, 1f, 0.2f);

    [Header("Sons (optionnels)")]
    [SerializeField] AudioSource source;
    [SerializeField] AudioClip sonDeverrouillage;
    [SerializeField] AudioClip sonGlissement;

    [Header("Départ")]
    [Tooltip("Coche pour une porte déjà ouverte au début de la partie.")]
    [SerializeField] bool ouverteAuDepart = false;

    [Header("Événements")]
    [Tooltip("Appelé quand la porte a fini de s'ouvrir (ex. : sauvegarder le temps, lancer un son).")]
    public UnityEvent OnOpened;

    public DoorState Etat { get; private set; } = DoorState.Verrouillee;

    Vector3[] positionsFermees;
    Coroutine animation;
    MaterialPropertyBlock bloc;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Awake()
    {
        // On retient la position fermée de chaque battant
        positionsFermees = new Vector3[battants.Length];
        for (int i = 0; i < battants.Length; i++)
            positionsFermees[i] = battants[i].localPosition;

        if (ouverteAuDepart) PlacerBattants(1f, DoorState.Ouverte);
        else PlacerBattants(0f, DoorState.Verrouillee);
    }

    /// <summary>Ouvre la porte. À brancher sur l'événement OnSolved de l'énigme.</summary>
    [ContextMenu("Ouvrir (test)")]
    public void Open()
    {
        if (Etat != DoorState.Verrouillee) return; // déjà ouverte ou en cours
        if (animation != null) StopCoroutine(animation);
        animation = StartCoroutine(Ouvrir());
    }

    /// <summary>Referme et reverrouille la porte (nouvelle partie).</summary>
    [ContextMenu("Réinitialiser (test)")]
    public void ResetDoor()
    {
        if (animation != null) StopCoroutine(animation);
        animation = null;
        PlacerBattants(0f, DoorState.Verrouillee);
    }

    IEnumerator Ouvrir()
    {
        Etat = DoorState.Ouverture;
        ChangerVoyant(couleurOuverte);
        Jouer(sonDeverrouillage);
        yield return new WaitForSeconds(0.3f); // petit délai : on entend le « clic » avant le glissement
        Jouer(sonGlissement);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dureeOuverture);
            Deplacer(Mathf.SmoothStep(0f, 1f, t)); // démarre et s'arrête en douceur
            yield return null;
        }

        PlacerBattants(1f, DoorState.Ouverte);
        animation = null;
        OnOpened?.Invoke();
    }

    void PlacerBattants(float ouverture, DoorState etat)
    {
        Deplacer(ouverture);
        Etat = etat;
        ChangerVoyant(etat == DoorState.Verrouillee ? couleurVerrouillee : couleurOuverte);
    }

    // ouverture = 0 (fermée) à 1 (ouverte)
    void Deplacer(float ouverture)
    {
        for (int i = 0; i < battants.Length; i++)
        {
            Vector3 d = i < deplacements.Length ? deplacements[i] : Vector3.zero;
            battants[i].localPosition = positionsFermees[i] + d * ouverture;
        }
    }

    void ChangerVoyant(Color c)
    {
        if (voyant == null) return;
        if (bloc == null) bloc = new MaterialPropertyBlock();
        voyant.GetPropertyBlock(bloc);
        bloc.SetColor(BaseColorId, c);
        bloc.SetColor(EmissionId, c * 2f); // le voyant brille
        voyant.SetPropertyBlock(bloc);
    }

    void Jouer(AudioClip clip)
    {
        if (source != null && clip != null) source.PlayOneShot(clip);
    }
}
