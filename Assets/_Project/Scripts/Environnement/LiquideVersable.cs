// LiquideVersable.cs
// Rôle : environnement et systèmes (Brunel)
// Un récipient (bécher, erlenmeyer, tube, éprouvette) qu'on penche : au-delà d'un certain angle, un filet de liquide
// bien visible sort par le col et tombe en arc. Là où il arrive :
//   - dans un autre récipient : ce récipient se remplit (et prend la couleur du liquide versé) ;
//   - sur une table ou le sol : une flaque de la couleur du liquide apparaît et grandit.
// Le niveau du récipient versé baisse jusqu'à ce qu'il soit vide.
// Emplacement : Assets/_Project/Scripts/Environnement/LiquideVersable.cs

using System.Collections.Generic;
using UnityEngine;

public class LiquideVersable : MonoBehaviour
{
    [SerializeField] Transform liquide;            // maillage du liquide (son échelle Y = niveau)
    [SerializeField] Transform bec;                // point de sortie (bord du col)
    [SerializeField] LineRenderer filet;           // le filet qui coule
    [SerializeField] Material matFlaque;           // matériau des flaques (transparent)
    [SerializeField] Color couleur = new Color(0.7f, 0.85f, 1f, 0.85f);
    [SerializeField, Range(0f, 1f)] float quantiteDepart = 1f;
    [Tooltip("Inclinaison (degrés) à partir de laquelle le liquide coule quand le récipient est plein.")]
    [SerializeField] float angleSeuil = 60f;
    [Tooltip("Temps pour vider complètement le récipient (secondes).")]
    [SerializeField] float dureeVidage = 5f;
    [SerializeField] AudioSource son;

    public float Quantite { get; private set; }
    public Color Couleur => couleur;

    Vector3 echelle0;
    Renderer rendLiquide;
    MaterialPropertyBlock bloc;
    Rigidbody monCorps;
    readonly Vector3[] points = new Vector3[40];
    static readonly List<Transform> flaques = new List<Transform>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        monCorps = GetComponentInParent<Rigidbody>();
        if (liquide != null)
        {
            echelle0 = liquide.localScale;
            rendLiquide = liquide.GetComponent<Renderer>();
        }
        Quantite = quantiteDepart;
        AppliquerNiveau();
        AppliquerCouleur();
        if (filet != null) filet.enabled = false;
    }

    void Update()
    {
        if (liquide == null || bec == null || filet == null) return;
        float inclinaison = Vector3.Angle(transform.up, Vector3.up);
        // Plus le récipient est plein, plus il coule tôt (plein : seuil ; presque vide : il faut le retourner)
        float seuil = Mathf.Lerp(angleSeuil + 60f, angleSeuil, Quantite);
        bool verse = inclinaison > seuil && Quantite > 0.005f;
        filet.enabled = verse;
        if (son != null) { if (verse && !son.isPlaying) son.Play(); else if (!verse && son.isPlaying) son.Stop(); }
        if (!verse) return;

        float debit = Time.deltaTime / Mathf.Max(0.5f, dureeVidage);
        Quantite = Mathf.Max(0f, Quantite - debit);
        AppliquerNiveau();
        Couler(debit);
    }

    // Trajectoire du filet (petite vitesse de sortie + gravité) jusqu'au premier obstacle
    void Couler(float debit)
    {
        Vector3 sortie = transform.up; sortie.y = Mathf.Min(sortie.y, 0f);
        Vector3 v = sortie.normalized * 0.35f;
        Vector3 p = bec.position;
        int n = 0;
        points[n++] = p;
        RaycastHit touche = default;
        bool aTouche = false;
        const float dt = 0.025f;
        for (int i = 0; i < points.Length - 1 && !aTouche; i++)
        {
            Vector3 suivant = p + v * dt;
            v += Physics.gravity * dt;
            Vector3 d = suivant - p;
            var hits = Physics.RaycastAll(p, d.normalized, d.magnitude + 0.001f, ~0, QueryTriggerInteraction.Ignore);
            float meilleur = float.MaxValue;
            foreach (var h in hits)
            {
                if (monCorps != null && h.rigidbody == monCorps) continue;   // pas son propre récipient
                if (h.distance < meilleur) { meilleur = h.distance; touche = h; aTouche = true; }
            }
            p = aTouche ? touche.point : suivant;
            points[n++] = p;
        }
        filet.positionCount = n;
        filet.SetPositions(points);
        if (!aTouche) return;

        // Dans un autre récipient ? Il se remplit. Sinon : flaque.
        var cible = touche.collider.GetComponentInParent<LiquideVersable>();
        if (cible != null && cible != this) { cible.Recevoir(debit, couleur); return; }
        Flaque(touche.point, touche.normal, debit);
    }

    /// <summary>Reçoit du liquide versé par un autre récipient (le niveau monte, les couleurs se mélangent).</summary>
    public void Recevoir(float quantite, Color c)
    {
        if (liquide == null) return;
        float avant = Quantite;
        Quantite = Mathf.Min(1f, Quantite + quantite);
        couleur = avant < 0.01f ? c : Color.Lerp(couleur, c, quantite / Mathf.Max(0.01f, Quantite));
        AppliquerNiveau();
        AppliquerCouleur();
    }

    void Flaque(Vector3 point, Vector3 normale, float debit)
    {
        Transform proche = null;
        foreach (var f in flaques)
            if (f != null && Vector3.Distance(f.position, point) < f.localScale.x * 0.5f + 0.03f) { proche = f; break; }
        if (proche == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Flaque";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normale * 0.002f;
            go.transform.up = normale;
            go.transform.localScale = new Vector3(0.03f, 0.0008f, 0.03f);
            var r = go.GetComponent<Renderer>();
            if (matFlaque != null) r.sharedMaterial = matFlaque;
            var b = new MaterialPropertyBlock();
            var c = couleur; c.a = 0.75f;
            b.SetColor(BaseColorId, c);
            r.SetPropertyBlock(b);
            flaques.Add(go.transform);
            proche = go.transform;
        }
        float d = Mathf.Min(0.6f, proche.localScale.x + debit * 0.5f);
        proche.localScale = new Vector3(d, 0.0008f, d);
    }

    void AppliquerNiveau()
    {
        if (liquide == null) return;
        liquide.gameObject.SetActive(Quantite > 0.005f);
        liquide.localScale = new Vector3(echelle0.x, echelle0.y * Mathf.Max(0.03f, Quantite), echelle0.z);
    }

    void AppliquerCouleur()
    {
        var c = couleur; c.a = 0.85f;
        if (rendLiquide != null)
        {
            if (bloc == null) bloc = new MaterialPropertyBlock();
            rendLiquide.GetPropertyBlock(bloc);
            bloc.SetColor(BaseColorId, c);
            rendLiquide.SetPropertyBlock(bloc);
        }
        if (filet != null) { filet.startColor = c; filet.endColor = c; }
    }

    /// <summary>Remplit de nouveau le récipient (nouvelle partie).</summary>
    public void Remplir()
    {
        Quantite = quantiteDepart;
        AppliquerNiveau();
    }
}
