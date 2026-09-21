using UnityEngine;
using System.Collections.Generic;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Settings")]
    public int initialLives = 20;
    public int initialGold = 500;
    public float waveDelay = 5f;

    private int currentLives;
    private int currentGold;
    private int currentWave = 0;
    private bool isGameOver = false;
    private List<Enemy> activeEnemies = new List<Enemy>();

    public System.Action<int> OnGoldChanged;
    public System.Action<int> OnLivesChanged;
    public System.Action<int> OnWaveStarted;
    public System.Action OnGameOver;
    public System.Action OnGameWon;

    private void Start()
    {
        currentGold = initialGold;
        currentLives = initialLives;
        OnGoldChanged?.Invoke(currentGold);
        OnLivesChanged?.Invoke(currentLives);
        
        // Start the first wave after a delay
        Invoke(nameof(StartNextWave), waveDelay);
    }

    public void StartNextWave()
    {
        if (isGameOver) return;

        currentWave++;
        OnWaveStarted?.Invoke(currentWave);
        WaveManager.Instance.StartWave(currentWave);
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);
    }
    

    public bool TrySpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            OnGoldChanged?.Invoke(currentGold);
            return true;
        }
        return false;
    }

    public void RegisterEnemy(Enemy enemy)
    {
        activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        activeEnemies.Remove(enemy);
        
        // Check if all enemies are dead and start next wave
        if (activeEnemies.Count == 0)
        {
            if (currentWave < WaveManager.Instance.GetTotalWaves())
            {
                // Cancel any pending invokes and schedule next wave
                CancelInvoke(nameof(StartNextWave));
                Invoke(nameof(StartNextWave), waveDelay);
            }
            else if (currentWave >= WaveManager.Instance.GetTotalWaves())
            {
                GameWon();
            }
        }
    }

    public void EnemyReachedEnd()
    {
        currentLives--;
        OnLivesChanged?.Invoke(currentLives);

        if (currentLives <= 0)
        {
            GameOver();
        }
    }

    public void GameOver()
    {
        isGameOver = true;
        OnGameOver?.Invoke();
        Time.timeScale = 0; // Pause the game
    }

    public void GameWon()
    {
        isGameOver = true;
        OnGameWon?.Invoke();
        Time.timeScale = 0;
    }

    public bool IsGameOver() => isGameOver;
    public int GetCurrentGold() => currentGold;
    public int GetCurrentLives() => currentLives;
}
