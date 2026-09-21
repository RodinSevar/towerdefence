using UnityEngine;
using TMPro;

public class HUD : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI goldText;

    [SerializeField]
    private TextMeshProUGUI livesText;

    [SerializeField]
    private TextMeshProUGUI lumberText;

    [SerializeField]
    private TextMeshProUGUI waveText;

    [SerializeField]
    private TextMeshProUGUI gameOverText;

    [SerializeField]
    private GameObject gameOverPanel;
    
    [SerializeField]
    private UnityEngine.UI.Button restartButton;

    private void Start()
    {
        // Subscribe to game events
        GameManager.Instance.OnLivesChanged += UpdateLivesDisplay;
        PlayerManager.Instance.OnLocalPlayerChanged += p => { UpdateGoldDisplay(p.gold); UpdateLumberDisplay(p.lumber); };
        GameManager.Instance.OnWaveStarted += UpdateWaveDisplay;
        GameManager.Instance.OnGameOver += ShowGameOverScreen;
        GameManager.Instance.OnGameWon += ShowGameWonScreen;

        // Initial updates
        UpdateGoldDisplay(PlayerManager.Instance.Local.gold);
        UpdateLivesDisplay(GameManager.Instance.GetCurrentLives());
        UpdateLumberDisplay(PlayerManager.Instance.Local.lumber);
        
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    private void UpdateGoldDisplay(int gold)
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold}";
    }

    private void UpdateLivesDisplay(int lives)
    {
        if (livesText != null)
            livesText.text = $"Lives: {lives}";
    }

    private void UpdateLumberDisplay(int lumber)
    {
        if (lumberText != null)
            lumberText.text = $"Lumber: {lumber}";
    }

    private void UpdateWaveDisplay(int wave)
    {
        if (waveText != null)
            waveText.text = $"Wave: {wave} / {WaveManager.Instance.GetTotalWaves()}";
    }

    private void ShowGameOverScreen()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = "GAME OVER!";
    }

    private void ShowGameWonScreen()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = "YOU WIN!";
    }
    
    private void RestartGame()
    {
        Time.timeScale = 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged -= UpdateLivesDisplay;
            GameManager.Instance.OnWaveStarted -= UpdateWaveDisplay;
            GameManager.Instance.OnGameOver -= ShowGameOverScreen;
            GameManager.Instance.OnGameWon -= ShowGameWonScreen;
        }
    }
}
