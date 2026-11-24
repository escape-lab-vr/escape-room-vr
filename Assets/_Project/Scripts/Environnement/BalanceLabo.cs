// BalanceLabo.cs
// Rôle : environnement et systèmes (Brunel)
// Balance qui pèse vraiment : elle additionne la masse des objets posés sur le plateau et l'affiche en grammes.
// Emplacement : Assets/_Project/Scripts/Environnement/BalanceLabo.cs

using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BalanceLabo : MonoBehaviour
{
    [SerializeField] TMP_Text affichage;
    [Tooltip("Zone au-dessus du plateau (repère local de la balance).")]
    [SerializeField] Vector3 zoneCentre = new Vector3(0f, 0.17f, 0.04f);
    [SerializeField] Vector3 zoneTaille = new Vector3(0.19f, 0.14f, 0.22f);

    readonly Collider[] trouves = new Collider[16];
    readonly HashSet<Rigidbody> corps = new HashSet<Rigidbody>();
    float prochain;

    void Update()
    {
        if (affichage == null || Time.time < prochain) return;
        prochain = Time.time + 0.2f;
        int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(zoneCentre), zoneTaille / 2f, trouves, transform.rotation);
        corps.Clear();
        float masse = 0f;
        for (int i = 0; i < n; i++)
        {
            var rb = trouves[i].attachedRigidbody;
            if (rb != null && corps.Add(rb)) masse += rb.mass;
        }
        affichage.text = (masse * 1000f).ToString("0.000") + " g";
    }
}
