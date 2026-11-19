// AlarmeController.cs
// Rôle : environnement et systèmes (Brunel)
// Ambiance d'urgence du pavillon :
//   - gyrophares rouges qui tournent et clignotent (plus vite quand il reste peu de temps) ;
//   - néons du plafond qui grésillent pendant l'alerte ;
//   - cloches d'alarme et sirène de fin (sons spatialisés).
// Modes : Normal, Inquietude (néons du corridor qui grésillent), Alerte (fuite), FinProche (dernière minute), Victoire.
// Emplacement : Assets/_Project/Scripts/Environnement/AlarmeController.cs

using UnityEngine;

public class AlarmeController : MonoBehaviour
{
    public enum Mode { Normal, Inquietude, Alerte, FinProche, Victoire }

    [Header("Gyrophares (rouges)")]
    [SerializeField] Renderer[] gyrophares;
    [Tooltip("Lumières rouges en temps réel. En garder très peu (2 ou 3) pour la performance du Quest.")]
    [SerializeField] Light[] lumieresGyrophares;
    [SerializeField] Color couleurAlarme = new Color(1f, 0.08f, 0.05f);
    [SerializeField] float vitesseRotation = 200f;

    [Header("Néons du plafond")]
    [SerializeField] Renderer[] neons;
    [SerializeField] Color couleurNeon = new Color(1f, 0.97f, 0.9f);
    [Tooltip("Chance, à chaque image, qu'un néon grésille pendant l'alerte.")]
    [SerializeField, Range(0f, 0.1f)] float probabiliteGresillement = 0.01f;
    [Tooltip("Néons qui grésillent en mode Inquietude (ceux du corridor, près du labo).")]
    [SerializeField] Renderer[] neonsInquietude;

    [Header("Sons")]
    [SerializeField] AudioSource[] cloches;
    [SerializeField] AudioSource sirene;

    [Header("Départ")]
    [SerializeField] bool alerteAuDepart = true;

    public Mode ModeActuel { get; private set; } = Mode.Normal;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock bloc;
    float[] intensitesDeBase;
    float[] neonsEteintsJusqua;

    bool AlarmeActive => ModeActuel == Mode.Alerte || ModeActuel == Mode.FinProche;

    void Awake()
    {
        bloc = new MaterialPropertyBlock();
        intensitesDeBase = new float[lumieresGyrophares.Length];
        for (int i = 0; i < lumieresGyrophares.Length; i++)
            if (lumieresGyrophares[i] != null) intensitesDeBase[i] = lumieresGyrophares[i].intensity;
        neonsEteintsJusqua = new float[Mathf.Max(neons.Length, neonsInquietude.Length)];
    }

    void Start()
    {
        Appliquer(alerteAuDepart ? Mode.Alerte : Mode.Normal);
    }

    public void DemarrerAlerte() => Appliquer(Mode.Alerte);
    public void Inquietude() { if (!AlarmeActive) Appliquer(Mode.Inquietude); }
    public void FinProche() => Appliquer(Mode.FinProche);
    public void Victoire() => Appliquer(Mode.Victoire);
    public void Normal() => Appliquer(Mode.Normal);

    void Appliquer(Mode mode)
    {
        ModeActuel = mode;
        bool alarme = AlarmeActive;

        foreach (var l in lumieresGyrophares) if (l != null) l.enabled = alarme;

        foreach (var c in cloches)
        {
            if (c == null) continue;
            if (alarme && !c.isPlaying) c.Play();
            if (!alarme) c.Stop();
        }

        if (sirene != null)
        {
            if (mode == Mode.FinProche && !sirene.isPlaying) sirene.Play();
            if (mode != Mode.FinProche) sirene.Stop();
        }

        if (!alarme)
        {
            foreach (var g in gyrophares) Couleur(g, Color.black, Color.black);
            for (int i = 0; i < neons.Length; i++) Couleur(neons[i], couleurNeon, couleurNeon);
        }
    }

    void Update()
    {
        if (ModeActuel == Mode.Inquietude) { Gresiller(neonsInquietude); return; }
        if (!AlarmeActive) return;

        // Gyrophares : rotation + pulsation (2 fois plus rapide en fin de partie)
        float vitesse = ModeActuel == Mode.FinProche ? 4f : 2f;
        float pulse = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(Time.time * vitesse * Mathf.PI));
        foreach (var g in gyrophares)
        {
            if (g == null) continue;
            g.transform.Rotate(0f, vitesseRotation * vitesse * 0.5f * Time.deltaTime, 0f, Space.World);
            Couleur(g, couleurAlarme, couleurAlarme * (3f * pulse));
        }
        for (int i = 0; i < lumieresGyrophares.Length; i++)
            if (lumieresGyrophares[i] != null) lumieresGyrophares[i].intensity = intensitesDeBase[i] * pulse;

        Gresiller(neons);
    }

    // Néons : grésillement aléatoire et court
    void Gresiller(Renderer[] liste)
    {
        for (int i = 0; i < liste.Length; i++)
        {
            if (liste[i] == null) continue;
            if (Time.time < neonsEteintsJusqua[i])
            {
                Couleur(liste[i], couleurNeon * 0.2f, Color.black);
            }
            else
            {
                if (Random.value < probabiliteGresillement)
                    neonsEteintsJusqua[i] = Time.time + Random.Range(0.05f, 0.25f);
                Couleur(liste[i], couleurNeon, couleurNeon);
            }
        }
    }

    void Couleur(Renderer r, Color baseColor, Color emission)
    {
        if (r == null) return;
        r.GetPropertyBlock(bloc);
        bloc.SetColor(BaseColorId, baseColor);
        bloc.SetColor(EmissionId, emission);
        r.SetPropertyBlock(bloc);
    }
}
