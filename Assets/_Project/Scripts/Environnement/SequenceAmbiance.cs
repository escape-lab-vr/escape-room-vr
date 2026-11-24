// SequenceAmbiance.cs
// Rôle : environnement et systèmes (Brunel)
// Histoire « Alerte au labo A-111 » : l'ambiance monte en 3 temps.
//   1. Départ     : les portes vitrées se verrouillent derrière le joueur (« clac »). Pavillon calme.
//   2. Corridor   : près du labo, les néons du corridor grésillent et on entend un sifflement derrière la porte.
//   3. Labo       : la fuite éclate (gaz, gyrophares, cloches) + annonce de confinement. OnConfinement est appelé
//                   (le GameManager peut y brancher le départ du minuteur de 10 minutes).
//   Échec         : Decontamination() -> voile blanc + souffle (appelée par le GameManager quand le temps est écoulé).
//   Fin           : Evacuation() -> le joueur atteint le point de rassemblement : l'alarme s'arrête, annonce
//                   « Évacuation réussie », camion de pompiers avec gyrophares. OnEvacuationReussie est appelé
//                   (le GameManager peut y brancher l'écran de victoire).
// Emplacement : Assets/_Project/Scripts/Environnement/SequenceAmbiance.cs

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SequenceAmbiance : MonoBehaviour
{
    [Header("Systèmes")]
    [SerializeField] AlarmeController alarme;
    [SerializeField] FuiteGaz fuite;
    [SerializeField] DeclencheurZone[] zones;

    [Header("1. Départ")]
    [SerializeField] AudioSource sonVerrouillage;
    [SerializeField] float delaiVerrouillage = 1.5f;

    [Header("2. Corridor")]
    [SerializeField] AudioSource sifflementLointain;

    [Header("3. Labo")]
    [SerializeField] AudioSource annonce;

    [Header("Échec : décontamination")]
    [SerializeField] Renderer voile;
    [SerializeField] AudioSource sonDecontamination;
    [SerializeField] float dureeVoile = 4f;

    [Header("4. Fin : point de rassemblement")]
    [SerializeField] AudioSource annonceEvacuation;
    [SerializeField] AudioSource sirenePompiers;
    [SerializeField] Light[] lumieresPompiers;      // [0] rouge, [1] bleu
    [SerializeField] Renderer[] feuxPompiers;       // mêmes couleurs, dans le même ordre
    [SerializeField] float periodeFeux = 0.35f;

    [Tooltip("Appelé quand la fuite éclate dans le labo (ex. : démarrer le minuteur).")]
    public UnityEvent OnConfinement;

    [Tooltip("Appelé quand le joueur atteint le point de rassemblement (ex. : écran de victoire).")]
    public UnityEvent OnEvacuationReussie;

    public bool ConfinementActif { get; private set; }
    public bool EvacuationTerminee { get; private set; }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock bloc;

    void Start()
    {
        if (voile != null)
        {
            // Le voile suit la tête du joueur
            if (Camera.main != null) voile.transform.SetParent(Camera.main.transform, false);
            voile.transform.localPosition = Vector3.zero;
            voile.enabled = false;
        }
        StartCoroutine(Verrouiller());
    }

    IEnumerator Verrouiller()
    {
        yield return new WaitForSeconds(delaiVerrouillage);
        if (sonVerrouillage != null) sonVerrouillage.Play();
    }

    /// <summary>Le joueur approche du labo : premiers signes inquiétants.</summary>
    public void Inquietude()
    {
        if (ConfinementActif) return;
        if (alarme != null) alarme.Inquietude();
        if (sifflementLointain != null && !sifflementLointain.isPlaying) sifflementLointain.Play();
    }

    /// <summary>Le joueur entre dans le labo : la fuite éclate, confinement.</summary>
    public void EntreeLabo()
    {
        if (ConfinementActif) return;
        ConfinementActif = true;
        if (sifflementLointain != null) sifflementLointain.Stop();
        if (fuite != null) fuite.Demarrer();
        if (alarme != null) alarme.DemarrerAlerte();
        if (annonce != null) annonce.Play();
        OnConfinement?.Invoke();
    }

    /// <summary>Le joueur atteint le point de rassemblement dehors : fin de l'histoire.</summary>
    [ContextMenu("Évacuation réussie (test)")]
    public void Evacuation()
    {
        if (EvacuationTerminee) return;
        EvacuationTerminee = true;
        if (fuite != null) fuite.Arreter();
        if (alarme != null) alarme.Victoire();
        if (annonce != null) annonce.Stop();
        if (annonceEvacuation != null) annonceEvacuation.Play();
        if (sirenePompiers != null) sirenePompiers.Play();
        StartCoroutine(FeuxPompiers());
        OnEvacuationReussie?.Invoke();
    }

    IEnumerator FeuxPompiers()
    {
        int k = 0;
        while (true)
        {
            for (int i = 0; i < lumieresPompiers.Length; i++)
                if (lumieresPompiers[i] != null) lumieresPompiers[i].enabled = (i % 2) == (k % 2);
            for (int i = 0; i < feuxPompiers.Length; i++)
                if (feuxPompiers[i] != null) feuxPompiers[i].enabled = (i % 2) == (k % 2);
            k++;
            yield return new WaitForSeconds(periodeFeux);
        }
    }

    /// <summary>Temps écoulé : voile blanc et souffle de décontamination.</summary>
    [ContextMenu("Décontamination (test)")]
    public void Decontamination()
    {
        if (sonDecontamination != null) sonDecontamination.Play();
        if (voile != null) StartCoroutine(Voile());
    }

    IEnumerator Voile()
    {
        if (bloc == null) bloc = new MaterialPropertyBlock();
        voile.enabled = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, dureeVoile);
            voile.GetPropertyBlock(bloc);
            bloc.SetColor(BaseColorId, new Color(0.92f, 1f, 0.95f, Mathf.SmoothStep(0f, 1f, t)));
            voile.SetPropertyBlock(bloc);
            yield return null;
        }
    }

    /// <summary>Nouvelle partie : tout revient au calme.</summary>
    [ContextMenu("Réinitialiser (test)")]
    public void Reinitialiser()
    {
        StopAllCoroutines();
        ConfinementActif = false;
        if (fuite != null) fuite.Arreter();
        if (alarme != null) alarme.Normal();
        if (sifflementLointain != null) sifflementLointain.Stop();
        if (annonce != null) annonce.Stop();
        if (voile != null) voile.enabled = false;
        EvacuationTerminee = false;
        if (annonceEvacuation != null) annonceEvacuation.Stop();
        if (sirenePompiers != null) sirenePompiers.Stop();
        foreach (var l in lumieresPompiers) if (l != null) l.enabled = false;
        foreach (var r in feuxPompiers) if (r != null) r.enabled = false;
        foreach (var z in zones) if (z != null) z.Reinitialiser();
    }
}
