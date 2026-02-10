using UnityEngine;
using TMPro;

public class HUD : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI goldText;

    [SerializeField]
    private TextMeshProUGUI livesText;

    [SerializeField]
    private TextMeshProUGUI waveText;

    [SerializeField]
    private TextMeshProUGUI gameOverText;

    [SerializeField]
    private GameObject gameOverPanel;

    private void Start()
    {
        // Subscribe to game events
        GameManager.Instance.OnGoldChanged += UpdateGoldDisplay;
        GameManager.Instance.OnLivesChanged += UpdateLivesDisplay;
        GameManager.Instance.OnWaveStarted += UpdateWaveDisplay;
        GameManager.Instance.OnGameOver += ShowGameOverScreen;
        GameManager.Instance.OnGameWon += ShowGameWonScreen;

        // Initial updates
        UpdateGoldDisplay(GameManager.Instance.GetCurrentGold());
        UpdateLivesDisplay(GameManager.Instance.GetCurrentLives());
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

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= UpdateGoldDisplay;
            GameManager.Instance.OnLivesChanged -= UpdateLivesDisplay;
            GameManager.Instance.OnWaveStarted -= UpdateWaveDisplay;
            GameManager.Instance.OnGameOver -= ShowGameOverScreen;
            GameManager.Instance.OnGameWon -= ShowGameWonScreen;
        }
    }
}
