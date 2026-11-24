// AppareilLabo.cs
// Rôle : environnement et systèmes (Brunel)
// Appareil de labo qu'on met en marche avec la main (bouton, interrupteur) : pièces qui tournent (barreau aimanté,
// ballon de l'évaporateur), voyants ou plaque qui s'allument, afficheur qui change (température qui monte, pH…), son.
// Emplacement : Assets/_Project/Scripts/Environnement/AppareilLabo.cs

using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class AppareilLabo : MonoBehaviour
{
    [Header("Commande")]
    [SerializeField] Transform bouton;
    [SerializeField] Vector3 angleBouton = new Vector3(0f, 0f, -90f);

    [Header("En marche")]
    [SerializeField] Transform[] rotors;
    [SerializeField] Vector3 axeRotation = Vector3.up;
    [SerializeField] float degresParSeconde = 540f;
    [SerializeField] GameObject[] actifs;           // objets visibles seulement en marche (lueur, voyant, vapeur)
    [SerializeField] AudioSource son;

    [Header("Afficheur")]
    [SerializeField] TMP_Text affichage;
    [SerializeField] string texteArret = "";
    [SerializeField] string texteMarche = "";        // texte fixe (ex. « pH 7,01 ») ; vide = valeur numérique
    [SerializeField] string format = "{0:0} °C";
    [SerializeField] float valeurDepart = 22f;
    [SerializeField] float valeurCible = 80f;
    [SerializeField] float vitesseValeur = 3f;        // unités par seconde

    public bool EnMarche { get; private set; }
    float valeur;
    Quaternion rotBouton;

    void Awake()
    {
        if (bouton != null) rotBouton = bouton.localRotation;
        valeur = valeurDepart;
        GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Basculer());
        Appliquer();
    }

    [ContextMenu("Marche / arrêt (test)")]
    public void Basculer()
    {
        EnMarche = !EnMarche;
        Appliquer();
    }

    void Appliquer()
    {
        if (bouton != null) bouton.localRotation = rotBouton * Quaternion.Euler(EnMarche ? angleBouton : Vector3.zero);
        if (actifs != null) foreach (var a in actifs) if (a != null) a.SetActive(EnMarche);
        if (son != null) { if (EnMarche) son.Play(); else son.Stop(); }
        Afficher();
    }

    void Update()
    {
        if (EnMarche && rotors != null)
            foreach (var r in rotors) if (r != null) r.Rotate(axeRotation, degresParSeconde * Time.deltaTime, Space.Self);
        float cible = EnMarche ? valeurCible : valeurDepart;
        if (!Mathf.Approximately(valeur, cible))
        {
            valeur = Mathf.MoveTowards(valeur, cible, vitesseValeur * Time.deltaTime);
            Afficher();
        }
    }

    void Afficher()
    {
        if (affichage == null) return;
        if (!EnMarche && Mathf.Abs(valeur - valeurDepart) < 0.5f) affichage.text = texteArret;
        else if (EnMarche && !string.IsNullOrEmpty(texteMarche)) affichage.text = texteMarche;
        else affichage.text = string.Format(format, valeur);
    }
}
