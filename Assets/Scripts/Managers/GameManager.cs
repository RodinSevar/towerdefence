using UnityEngine;
using System.Collections.Generic;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Settings")]
    public int initialLives = 20;
    public int initialGold = 500;
    [Tooltip("Seconds between a wave being cleared and the next one starting (also before wave 1)")]
    public float waveDelay = 5f;

    private int currentLives;
    private int currentGold;
    private int currentWave = 0;
    private bool isGameOver = false;
    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();

    public System.Action<int> OnGoldChanged;
    public System.Action<int> OnLivesChanged;
    public System.Action<int> OnWaveStarted;
    public System.Action OnGameOver;
    public System.Action OnGameWon;

    protected override void OnSingletonAwake()
    {
        // A previous game over / win pauses the game; make sure a fresh scene always starts running.
        Time.timeScale = 1f;
    }

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

        int nextWave = currentWave + 1;
        if (!WaveManager.Instance.StartWave(nextWave)) return;

        currentWave = nextWave;
        OnWaveStarted?.Invoke(currentWave);
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
        CheckWaveCleared();
    }

    /// <summary>Called by WaveManager once the last creep of a wave has spawned.</summary>
    public void NotifyWaveSpawningComplete()
    {
        // Every creep may already be dead (e.g. killed between spawn ticks).
        CheckWaveCleared();
    }

    /// <summary>A wave is cleared only when nothing is left to spawn AND nothing is alive.</summary>
    private void CheckWaveCleared()
    {
        if (isGameOver) return;
        if (activeEnemies.Count > 0 || WaveManager.Instance.IsSpawning) return;

        if (currentWave < WaveManager.Instance.GetTotalWaves())
        {
            // Cancel any pending invokes and schedule next wave
            CancelInvoke(nameof(StartNextWave));
            Invoke(nameof(StartNextWave), waveDelay);
        }
        else
        {
            GameWon();
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
        if (isGameOver) return;
        isGameOver = true;
        OnGameOver?.Invoke();
        Time.timeScale = 0; // Pause the game
    }

    public void GameWon()
    {
        if (isGameOver) return;
        isGameOver = true;
        OnGameWon?.Invoke();
        Time.timeScale = 0;
    }

    public bool IsGameOver() => isGameOver;
    public int GetCurrentGold() => currentGold;
    public int GetCurrentLives() => currentLives;
}
