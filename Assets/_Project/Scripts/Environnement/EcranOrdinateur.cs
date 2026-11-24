// EcranOrdinateur.cs
// Rôle : environnement et systèmes (Brunel)
// Poste informatique du labo : écran de verrouillage avec l'heure et la date réelles. On touche la souris, le clavier
// ou l'écran : écran de connexion, le mot de passe se tape tout seul (bruit des touches), puis le bureau s'affiche
// (logiciel du spectrophotomètre ouvert). Sans action pendant « veille » secondes, le poste se verrouille de nouveau.
// Emplacement : Assets/_Project/Scripts/Environnement/EcranOrdinateur.cs

using System.Collections;
using TMPro;
using UnityEngine;

public class EcranOrdinateur : MonoBehaviour
{
    [SerializeField] Renderer dalle;
    [SerializeField] Material matVerrouille, matConnexion, matBureau;
    [SerializeField] TMP_Text heure, date, motDePasse, heureBarre;
    [SerializeField] GameObject groupeVerrou, groupeConnexion, groupeBureau;
    [SerializeField] AudioSource sonTouche;
    [SerializeField] float veille = 90f;

    static readonly string[] Jours = { "dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi" };
    static readonly string[] Mois = { "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };

    public int Etat { get; private set; }     // 0 verrouillé, 1 connexion, 2 bureau
    float derniereAction;
    Coroutine saisie;

    void Awake() => Appliquer(0);

    /// <summary>Appelé quand on touche la souris, le clavier ou l'écran.</summary>
    public void Action()
    {
        derniereAction = Time.time;
        if (Etat == 0) Appliquer(1);
        else if (Etat == 1 && saisie == null) saisie = StartCoroutine(Saisir());
        else if (Etat == 2 && sonTouche != null) sonTouche.PlayOneShot(sonTouche.clip);
    }

    IEnumerator Saisir()
    {
        for (int i = 1; i <= 8; i++)
        {
            if (motDePasse != null) motDePasse.text = new string('•', i);
            if (sonTouche != null && sonTouche.clip != null) { sonTouche.pitch = Random.Range(0.92f, 1.08f); sonTouche.PlayOneShot(sonTouche.clip); }
            yield return new WaitForSeconds(Random.Range(0.08f, 0.16f));
        }
        yield return new WaitForSeconds(0.35f);
        if (motDePasse != null) motDePasse.text = "Bienvenue";
        yield return new WaitForSeconds(0.9f);
        Appliquer(2);
        saisie = null;
    }

    void Appliquer(int e)
    {
        Etat = e;
        if (dalle != null) dalle.sharedMaterial = e == 0 ? matVerrouille : e == 1 ? matConnexion : matBureau;
        if (groupeVerrou != null) groupeVerrou.SetActive(e == 0);
        if (groupeConnexion != null) groupeConnexion.SetActive(e == 1);
        if (groupeBureau != null) groupeBureau.SetActive(e == 2);
        if (motDePasse != null) motDePasse.text = "";
        derniereAction = Time.time;
    }

    void Update()
    {
        var t = System.DateTime.Now;
        string h = t.ToString("HH:mm");
        if (heure != null) heure.text = h;
        if (date != null) date.text = $"{Jours[(int)t.DayOfWeek]} {t.Day} {Mois[t.Month - 1]}";
        if (heureBarre != null) heureBarre.text = $"{h}\n<size=70%>{t:yyyy-MM-dd}</size>";
        if (Etat != 0 && saisie == null && Time.time - derniereAction > veille) Appliquer(0);
    }
}
