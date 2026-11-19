using UnityEngine;
using TMPro;

public class MinuteurJeu : MonoBehaviour
{
    [SerializeField] private TMP_Text texteMinuteur;
    [SerializeField] private float dureeEnSecondes = 600f;

    private float tempsRestant;

    private void Start()
    {
        tempsRestant = Mathf.Max(0f, dureeEnSecondes);
        AfficherTemps();
    }

    private void Update()
    {
        if (tempsRestant <= 0f)
            return;

        tempsRestant = Mathf.Max(0f, tempsRestant - Time.deltaTime);
        AfficherTemps();
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