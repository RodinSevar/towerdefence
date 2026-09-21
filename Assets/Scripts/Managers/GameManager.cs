using UnityEngine;
using System.Collections.Generic;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Settings")]
    public int initialLives = 20;
    public int initialGold = 60; // as in the original map
    public int initialLumber = 1; // as in the original map: one lumber to unlock a race
    [Tooltip("The original map gives +1 lumber when this level is about to start (a second race)")]
    public int bonusLumberLevel = 15;
    [Tooltip("Countdown before wave 1")]
    public float firstWaveDelay = 60f;
    [Tooltip("Countdown between a wave being cleared and the next one starting")]
    public float waveDelay = 30f;

    [Header("Level bonus (paid each time a wave is cleared)")]
    public int levelBonus = 10;
    public int levelBonusStep = 2;

    private int currentLives;
    private int currentGold;
    private int currentLumber;
    private int currentWave = 0;
    private bool isGameOver = false;
    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();

    public System.Action<int> OnGoldChanged;
    public System.Action<int> OnLivesChanged;
    public System.Action<int> OnLumberChanged;
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
        currentLumber = initialLumber;
        currentLives = initialLives;
        OnLumberChanged?.Invoke(currentLumber);
        OnGoldChanged?.Invoke(currentGold);
        OnLivesChanged?.Invoke(currentLives);

        // Start the first wave after a delay
        Invoke(nameof(StartNextWave), firstWaveDelay);
    }

    public void StartNextWave()
    {
        if (isGameOver) return;

        int nextWave = currentWave + 1;
        if (!WaveManager.Instance.StartWave(nextWave)) return;

        currentWave = nextWave;
        OnWaveStarted?.Invoke(currentWave);

        // A wave with no creeps is cleared immediately.
        CheckWaveCleared();
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);
    }

    public void AddLumber(int amount)
    {
        currentLumber += amount;
        OnLumberChanged?.Invoke(currentLumber);
    }

    public bool TrySpendLumber(int amount)
    {
        if (currentLumber < amount) return false;
        currentLumber -= amount;
        OnLumberChanged?.Invoke(currentLumber);
        return true;
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

    /// <summary>A wave is cleared when no creeps are alive (they all spawn at once, as in the original map).</summary>
    private void CheckWaveCleared()
    {
        if (isGameOver) return;
        if (activeEnemies.Count > 0) return;

        // Original map: bonus grows by 2 each level and is paid on clear.
        levelBonus += levelBonusStep;
        AddGold(levelBonus);
        Debug.Log($"Level {currentWave} cleared: +{levelBonus} gold");

        // Original map: +1 lumber when level 15 (bonusLumberLevel) is about to start, unlocking a second race
        if (currentWave + 1 == bonusLumberLevel) AddLumber(1);

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
    public int GetCurrentLumber() => currentLumber;
    public int GetCurrentLives() => currentLives;
}
