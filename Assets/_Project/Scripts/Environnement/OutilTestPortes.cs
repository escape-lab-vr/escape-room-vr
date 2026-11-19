// OutilTestPortes.cs
// Rôle : environnement et systèmes (Brunel)
// Outil de test : pendant le Play, la touche O ouvre toutes les portes ZoneDoor, la touche R les referme.
// Pratique pour visiter le pavillon avant que les énigmes soient branchées.
// Actif seulement dans l'éditeur et dans les builds de développement (jamais dans la version finale).
// Emplacement : Assets/_Project/Scripts/Environnement/OutilTestPortes.cs

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class OutilTestPortes : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void Update()
    {
        if (ToucheOuvrir())
        {
            foreach (var p in FindObjectsByType<ZoneDoor>(FindObjectsSortMode.None)) p.Open();
            Debug.Log("Test : toutes les portes sont ouvertes (touche O).");
        }
        if (ToucheFermer())
        {
            foreach (var p in FindObjectsByType<ZoneDoor>(FindObjectsSortMode.None)) p.ResetDoor();
            Debug.Log("Test : toutes les portes sont refermées (touche R).");
        }
    }

    static bool ToucheOuvrir()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.O);
#endif
    }

    static bool ToucheFermer()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
#endif
}
