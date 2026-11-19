// FuiteGaz.cs
// Rôle : environnement et systèmes (Brunel)
// Gaz vert qui s'échappe de la hotte du labo A-111.
//   - Demarrer() : le gaz sort et siffle (début de partie).
//   - Arreter()  : le gaz s'arrête en douceur. À appeler quand le bon mélange est réussi
//                  (énigme de Jolin) ou via EscapeGameManager.FuiteArretee().
// Emplacement : Assets/_Project/Scripts/Environnement/FuiteGaz.cs

using UnityEngine;
using UnityEngine.Events;

public class FuiteGaz : MonoBehaviour
{
    [SerializeField] ParticleSystem gaz;
    [SerializeField] AudioSource sifflement;
    [SerializeField] bool activeAuDepart = true;

    [Tooltip("Appelé quand la fuite est arrêtée.")]
    public UnityEvent OnArretee;

    public bool Active { get; private set; }

    void Start()
    {
        if (activeAuDepart) Demarrer();
        else ArreterInterne(false);
    }

    [ContextMenu("Démarrer (test)")]
    public void Demarrer()
    {
        Active = true;
        if (gaz != null) gaz.Play();
        if (sifflement != null && !sifflement.isPlaying) sifflement.Play();
    }

    [ContextMenu("Arrêter (test)")]
    public void Arreter() => ArreterInterne(true);

    void ArreterInterne(bool prevenir)
    {
        bool etaitActive = Active;
        Active = false;
        // StopEmitting : les particules déjà sorties finissent de se dissiper (plus naturel)
        if (gaz != null) gaz.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (sifflement != null) sifflement.Stop();
        if (prevenir && etaitActive) OnArretee?.Invoke();
    }
}
