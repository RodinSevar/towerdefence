using UnityEngine;
using System.Collections.Generic;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Settings")]
    public int initialLives = 20;
    public int initialGold = 60; // per player, as in the original map
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
    private int currentWave = 0;
    private bool isGameOver = false;
    private float waveCountdown = -1f; // seconds until the next wave starts; negative = no wave scheduled
    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();

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
        currentLives = initialLives;
        OnLivesChanged?.Invoke(currentLives);

        // Start the first wave after a delay
        waveCountdown = firstWaveDelay;
    }

    /// <summary>One simulation tick (called by <see cref="Simulation"/>): counts down to the next wave.</summary>
    public void SimTick(float dt)
    {
        if (isGameOver || waveCountdown < 0f) return;
        waveCountdown -= dt;
        if (waveCountdown > 0f) return;
        waveCountdown = -1f;
        StartNextWave();
    }

    /// <summary>Seconds until the next wave starts, or a negative number if none is scheduled.</summary>
    public float WaveCountdown => waveCountdown;

    /// <summary>Cancels the scheduled wave (tests that start waves themselves).</summary>
    public void CancelWaveTimer() => waveCountdown = -1f;

    public void StartNextWave()
    {
        if (isGameOver) return;

        int nextWave = currentWave + 1;
        if (!WaveManager.Instance.StartWave(nextWave)) return;

        currentWave = nextWave;
        OnWaveStarted?.Invoke(currentWave);

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

    /// <summary>Called by WaveManager once the last creep of a wave has been instantiated.</summary>
    public void NotifyWaveSpawningComplete()
    {
        // Every creep may already be dead, or the wave may have had none.
        CheckWaveCleared();
    }

    /// <summary>A wave is cleared when every creep has spawned and none is alive.</summary>
    private void CheckWaveCleared()
    {
        if (isGameOver) return;
        if (activeEnemies.Count > 0 || WaveManager.Instance.IsSpawning) return;

        // Original map: bonus grows by 2 each level and is paid on clear.
        levelBonus += levelBonusStep;
        PlayerManager.Instance.AddGoldToAll(levelBonus); // every player gets the level bonus
        Debug.Log($"Level {currentWave} cleared: +{levelBonus} gold");

        // Original map: +1 lumber when level 15 (bonusLumberLevel) is about to start, unlocking a second race
        if (currentWave + 1 == bonusLumberLevel) PlayerManager.Instance.AddLumberToAll(1);

        if (currentWave < WaveManager.Instance.GetTotalWaves())
        {
            waveCountdown = waveDelay;
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
    public int GetCurrentLives() => currentLives;
}
