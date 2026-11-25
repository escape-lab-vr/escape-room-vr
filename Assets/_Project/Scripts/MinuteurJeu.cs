using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class MinuteurJeu : MonoBehaviour
{
    [SerializeField] private TMP_Text texteMinuteur;
    [SerializeField] private float dureeEnSecondes = 600f;

    [Tooltip("Coché : le minuteur part tout seul au lancement. Décoché : il attend Demarrer() (GameManager).")]
    [SerializeField] private bool demarrerAuLancement = true;

    [Tooltip("Appelé quand le compte à rebours arrive à 00:00.")]
    public UnityEvent OnTempsEcoule;

    public bool EstEnCours { get; private set; }
    public float TempsRestant => tempsRestant;
    public float DureeEnSecondes => dureeEnSecondes;

    private float tempsRestant;

    private void Start()
    {
        tempsRestant = Mathf.Max(0f, dureeEnSecondes);
        AfficherTemps();

        if (demarrerAuLancement)
        {
            EstEnCours = true;
        }
    }

    private void Update()
    {
        if (!EstEnCours)
            return;

        tempsRestant = Mathf.Max(0f, tempsRestant - Time.deltaTime);
        AfficherTemps();

        if (tempsRestant <= 0f)
        {
            EstEnCours = false;
            OnTempsEcoule?.Invoke();
        }
    }

    public void Demarrer() // repart de la durée complète et lance le compte à rebours
    {
        tempsRestant = Mathf.Max(0f, dureeEnSecondes);
        EstEnCours = true;
        AfficherTemps();
    }

    public void Arreter() // fige le temps affiché (ex. : victoire)
    {
        EstEnCours = false;
    }

    private void AfficherTemps()
    {
        if (texteMinuteur == null)
            return;

        int secondesTotales = Mathf.CeilToInt(tempsRestant);
        int minutes = secondesTotales / 60;
        int secondes = secondesTotales % 60;

        texteMinuteur.text = $"{minutes:00}:{secondes:00}";
    }
}
