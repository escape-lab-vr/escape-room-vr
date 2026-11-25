// GameManager.cs
// Rôle : gestion de projet et intégration (Allan)
// Chef d'orchestre de la partie : il relie les énigmes (Jolin), les portes et l'ambiance (Brunel)
// et le minuteur (Sarata).
//   - Démarrage    : le minuteur part au début de la partie, ou à l'entrée du labo (case à cocher).
//   - Progression  : une énigme réussie appelle PuzzleSolved(numéro) -> la porte associée s'ouvre.
//   - Fin proche   : pendant la dernière minute, l'alarme passe en mode « fin proche ».
//   - Victoire     : le joueur atteint le point de rassemblement dehors (OnEvacuationReussie de Brunel)
//                    -> minuteur arrêté, temps affiché. Option : gagner dès que la porte de sortie s'ouvre.
//   - Échec        : le temps est écoulé -> décontamination.
//   - Recommencer  : RestartGame() recharge la scène.
// Brancher une énigme (sans code) : sur son événement de réussite, cliquer +, glisser le GameManager,
// choisir GameManager -> PuzzleSolved (int) et entrer le numéro de l'énigme (0 = la première).
// Tester sans énigme : en mode Play, clic droit sur le composant -> « Test : réussir l'énigme suivante ».
// Emplacement : Assets/_Project/Scripts/GameManager.cs

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Won, Lost }

    [System.Serializable]
    public class PuzzleStep
    {
        [Tooltip("Nom de l'énigme, pour s'y retrouver dans l'Inspector.")]
        public string name;

        [Tooltip("Porte ouverte quand l'énigme est réussie (peut rester vide).")]
        public ZoneDoor doorToOpen;

        [Tooltip("Actions en plus quand l'énigme est réussie (ex. : Fuite_Gaz -> FuiteGaz.Arreter).")]
        public UnityEvent onSolved;

        [HideInInspector]
        public bool isSolved;
    }

    [Header("Énigmes, dans l'ordre du jeu")]
    [SerializeField] private PuzzleStep[] puzzles;

    [Header("Minuteur (Sarata)")]
    [SerializeField] private MinuteurJeu timer;
    [Tooltip("Coché : le minuteur part à l'entrée du labo (OnConfinement). Décoché : dès le début de la partie.")]
    [SerializeField] private bool startTimerOnLabEntry = false;
    [Tooltip("Secondes restantes à partir desquelles l'alarme passe en « fin proche ».")]
    [SerializeField] private float finalMinuteThreshold = 60f;

    [Header("Décor et ambiance (Brunel)")]
    [SerializeField] private SequenceAmbiance ambiance;
    [SerializeField] private AlarmeController alarm;
    [Tooltip("Porte de sortie (PorteSortie). Utilisée pour la victoire seulement si la case ci-dessous est décochée.")]
    [SerializeField] private ZoneDoor exitDoor;
    [Tooltip("Coché : victoire au point de rassemblement dehors (OnEvacuationReussie). Décoché : dès que la porte de sortie s'ouvre.")]
    [SerializeField] private bool winOnEvacuation = true;

    [Header("Écrans de fin (optionnels)")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text victoryTimeText;
    [SerializeField] private GameObject defeatPanel;

    [Header("Événements")]
    public UnityEvent onGameWon;
    public UnityEvent onGameLost;

    public GameState State { get; private set; } = GameState.Playing;

    private bool finalMinuteTriggered;

    private void Start() // prépare la partie et branche les événements des autres scripts
    {
        HidePanel(victoryPanel);
        HidePanel(defeatPanel);

        if (timer != null)
        {
            timer.OnTempsEcoule.AddListener(LoseGame);
        }

        if (winOnEvacuation && ambiance != null)
        {
            ambiance.OnEvacuationReussie.AddListener(WinGame);
        }
        else if (exitDoor != null)
        {
            exitDoor.OnOpened.AddListener(WinGame);
        }

        if (startTimerOnLabEntry && ambiance != null)
        {
            ambiance.OnConfinement.AddListener(StartTimer);
        }
        else
        {
            StartTimer();
        }
    }

    private void Update() // surveille la dernière minute pour déclencher l'alarme
    {
        if (State != GameState.Playing)
        {
            return;
        }

        if (timer == null || !timer.EstEnCours || finalMinuteTriggered)
        {
            return;
        }

        if (timer.TempsRestant <= finalMinuteThreshold)
        {
            finalMinuteTriggered = true;

            if (alarm != null)
            {
                alarm.FinProche();
            }
        }
    }

    public void PuzzleSolved(int puzzleIndex) // appelée par une énigme quand elle est réussie
    {
        if (State != GameState.Playing)
        {
            return;
        }

        if (puzzles == null || puzzleIndex < 0 || puzzleIndex >= puzzles.Length)
        {
            Debug.LogWarning($"GameManager : énigme {puzzleIndex} inconnue.");
            return;
        }

        PuzzleStep step = puzzles[puzzleIndex];

        if (step.isSolved)
        {
            return;
        }

        step.isSolved = true;
        Debug.Log($"GameManager : énigme {puzzleIndex} réussie ({step.name}).");

        if (step.doorToOpen != null)
        {
            step.doorToOpen.Open();
        }

        step.onSolved?.Invoke();

        // Sans fin prévue (ni évacuation, ni porte de sortie), la victoire arrive quand toutes les énigmes sont réussies
        if (!HasEndTrigger() && AllPuzzlesSolved())
        {
            WinGame();
        }
    }

    public void RestartGame() // recharge la scène pour une nouvelle partie
    {
        Scene currentScene = SceneManager.GetActiveScene();

        Time.timeScale = 1f;
        SceneManager.LoadScene(currentScene.path);
    }

    private void StartTimer() // lance le compte à rebours de Sarata
    {
        if (timer == null || State != GameState.Playing)
        {
            return;
        }

        if (!timer.EstEnCours)
        {
            timer.Demarrer();
        }
    }

    private void WinGame() // la sortie est ouverte : victoire
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = GameState.Won;

        if (timer != null)
        {
            timer.Arreter();
        }

        // L'évacuation de Brunel coupe déjà l'alarme ; sinon, on le fait ici
        if (!winOnEvacuation && alarm != null)
        {
            alarm.Victoire();
        }

        ShowVictoryTime();
        ShowPanel(victoryPanel);
        onGameWon?.Invoke();
        Debug.Log("GameManager : victoire !");
    }

    private void LoseGame() // le temps est écoulé : échec
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = GameState.Lost;

        if (ambiance != null)
        {
            ambiance.Decontamination();
        }

        ShowPanel(defeatPanel);
        onGameLost?.Invoke();
        Debug.Log("GameManager : temps écoulé, partie perdue.");
    }

    private bool HasEndTrigger() // vrai si la victoire est déclenchée par l'évacuation ou la porte de sortie
    {
        if (winOnEvacuation && ambiance != null)
        {
            return true;
        }

        return exitDoor != null;
    }

    private bool AllPuzzlesSolved() // vrai si toutes les énigmes sont réussies
    {
        foreach (PuzzleStep step in puzzles)
        {
            if (!step.isSolved)
            {
                return false;
            }
        }

        return true;
    }

    private void ShowVictoryTime() // affiche le temps mis pour s'échapper
    {
        if (victoryTimeText == null || timer == null)
        {
            return;
        }

        float elapsedSeconds = timer.DureeEnSecondes - timer.TempsRestant;
        int totalSeconds = Mathf.CeilToInt(elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        victoryTimeText.text = $"Temps : {minutes:00}:{seconds:00}";
    }

    private void ShowPanel(GameObject panel) // affiche un écran de fin s'il existe
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    private void HidePanel(GameObject panel) // cache un écran de fin s'il existe
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    [ContextMenu("Test : réussir l'énigme suivante")]
    private void SolveNextPuzzleForTest() // simule la réussite de la prochaine énigme
    {
        if (puzzles == null)
        {
            return;
        }

        for (int i = 0; i < puzzles.Length; i++)
        {
            if (!puzzles[i].isSolved)
            {
                PuzzleSolved(i);
                return;
            }
        }
    }

    [ContextMenu("Test : temps écoulé")]
    private void LoseGameForTest() // simule la fin du temps
    {
        LoseGame();
    }
}
