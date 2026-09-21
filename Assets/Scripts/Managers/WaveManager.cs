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

    public List<Spawner> activeSpawners = new List<Spawner>();

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
        if (currentWave == 0 || currentWave > waves.Length) return;

        WaveSet.Wave wave = waves[currentWave - 1];

        if (enemiesSpawnedInWave < wave.enemyCount)
        {
            if (activeSpawners.Count == 0)
            {
                activeSpawners.AddRange(FindObjectsByType<Spawner>(FindObjectsSortMode.None));
            }

            if (activeSpawners.Count == 0)
            {
                Debug.LogWarning("No active spawners found! Cannot spawn enemies.");
                enemiesSpawnedInWave = wave.enemyCount;
            }
            else
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= wave.spawnInterval)
                {
                    spawnTimer = 0f;
                    foreach (var spawner in activeSpawners)
                    {
                        SpawnEnemy(wave.enemy, spawner, enemiesSpawnedInWave);
                    }
                    enemiesSpawnedInWave++;
                }
            }
        }
    }

    public void StartWave(int waveNumber)
    {
        if (waveNumber > waves.Length)
        {
            Debug.LogWarning("Wave number exceeds configured waves!");
            return;
        }

        currentWave = waveNumber;
        enemiesSpawnedInWave = 0;
        spawnTimer = waves[waveNumber - 1].spawnInterval; // Spawn first enemy immediately
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
