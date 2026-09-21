using UnityEngine;
using System.Collections.Generic;

public class WaveManager : Singleton<WaveManager>
{
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private WaveSet waveSet;

    private WaveSet.Wave[] waves => waveSet != null ? waveSet.waves : System.Array.Empty<WaveSet.Wave>();

    private int currentWave = 0;
    private int enemiesSpawnedInWave = 0;
    private float spawnTimer = 0f;
    private bool isSpawning = false;

    // Spawners register themselves in Spawner.Start
    public List<Spawner> activeSpawners = new List<Spawner>();

    /// <summary>True while the current wave still has creeps left to spawn.</summary>
    public bool IsSpawning => isSpawning;

    public void RegisterSpawner(Spawner spawner)
    {
        if (!activeSpawners.Contains(spawner))
            activeSpawners.Add(spawner);
    }

    public void UnregisterSpawner(Spawner spawner)
    {
        activeSpawners.Remove(spawner);
    }

    private void Update()
    {
        if (!isSpawning) return;

        WaveSet.Wave wave = waves[currentWave - 1];

        spawnTimer += Time.deltaTime;
        if (spawnTimer < wave.spawnInterval) return;
        spawnTimer = 0f;

        foreach (var spawner in activeSpawners)
        {
            SpawnEnemy(wave.enemy, spawner, enemiesSpawnedInWave);
        }
        enemiesSpawnedInWave++;

        if (enemiesSpawnedInWave >= wave.enemyCount)
        {
            isSpawning = false;
            GameManager.Instance.NotifyWaveSpawningComplete();
        }
    }

    /// <summary>Begins spawning the given wave (1-based). Returns false if it could not start.</summary>
    public bool StartWave(int waveNumber)
    {
        if (waveNumber < 1 || waveNumber > waves.Length)
        {
            Debug.LogError($"Wave {waveNumber} is not configured (WaveSet has {waves.Length} waves).");
            return false;
        }

        WaveSet.Wave wave = waves[waveNumber - 1];
        if (wave.enemy == null)
        {
            Debug.LogError($"Wave {waveNumber} has no enemy assigned in the WaveSet.");
            return false;
        }
        if (activeSpawners.Count == 0)
        {
            Debug.LogError("No spawners registered; cannot start the wave. Is a Spawner in the scene?");
            return false;
        }

        currentWave = waveNumber;
        enemiesSpawnedInWave = 0;
        spawnTimer = wave.spawnInterval; // Spawn first enemy immediately
        isSpawning = true;
        return true;
    }

    private void SpawnEnemy(EnemyData data, Spawner spawner, int index)
    {
        if (spawner.waypoints == null || spawner.waypoints.Length == 0) return;

        Enemy enemy = Instantiate(enemyPrefab, spawner.waypoints[0], Quaternion.identity);
        enemy.Init(data, spawner.waypoints, index + spawner.indexOffset, spawner.playerColor);

        GameManager.Instance.RegisterEnemy(enemy);
    }

    public int GetTotalWaves() => waves.Length;
    public int GetCurrentWave() => currentWave;
}
