using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuJeu : MonoBehaviour
{
    public void NouvellePartie()
    {
        Time.timeScale = 1f;

        Scene sceneActuelle = SceneManager.GetActiveScene();
        SceneManager.LoadScene(sceneActuelle.path);
    }
}