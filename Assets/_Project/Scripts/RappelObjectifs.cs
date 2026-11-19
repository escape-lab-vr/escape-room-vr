using UnityEngine;

public class RappelObjectifs : MonoBehaviour
{
    [SerializeField] private GameObject panneauObjectifs;
    [SerializeField] private float intervalleEnSecondes = 180f;
    [SerializeField] private float dureeAffichage = 10f;

    private float tempsDepuisRappel;

    private void Start()
    {
        tempsDepuisRappel = 0f;

        if (panneauObjectifs != null)
            panneauObjectifs.SetActive(true);
    }

    private void Update()
    {
        if (panneauObjectifs == null)
            return;

        float intervalle = Mathf.Max(1f, intervalleEnSecondes);
        float duree = Mathf.Clamp(dureeAffichage, 0f, intervalle);

        tempsDepuisRappel += Time.deltaTime;

        if (tempsDepuisRappel >= intervalle)
            tempsDepuisRappel %= intervalle;

        panneauObjectifs.SetActive(tempsDepuisRappel < duree);
    }
}