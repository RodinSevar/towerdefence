using UnityEngine;
using System.Collections.Generic;

public class WaveManager : Singleton<WaveManager>
{
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private WaveSet waveSet;

    [Tooltip("Seconds creeps wait at their spawn before their first move order. The original map's script issues the order " +
             "immediately (0); a short hold gives the wave a moment to appear before it starts walking.")]
    [SerializeField] private float initialOrderDelay = 3f;

    [Tooltip("Creeps instantiated per frame. The map spawns a level at once; spreading it over a few frames avoids a hitch.")]
    [SerializeField] private int spawnsPerFrame = 200;

    private WaveSet.Wave[] waves => waveSet != null ? waveSet.waves : System.Array.Empty<WaveSet.Wave>();

    private int currentWave = 0;

    // Spawn progress of the current wave: every spawner gets enemyCount creeps
    private bool isSpawning;
    private WaveSet.Wave spawningWave;
    private int spawnerIndex, creepIndex;

    // Spawners register themselves in Spawner.Start
    public List<Spawner> activeSpawners = new List<Spawner>();

    /// <summary>True while the current wave still has creeps left to instantiate.</summary>
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

    /// <summary>
    /// Begins spawning the given wave (1-based): enemyCount creeps at every spawner, as in the original map. They appear
    /// over the next few frames. Returns false if the wave could not start.
    /// </summary>
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
        spawningWave = wave;
        spawnerIndex = 0;
        creepIndex = 0;
        isSpawning = true;
        return true;
    }

    private void Update()
    {
        if (!isSpawning) return;

        for (int budget = spawnsPerFrame; budget > 0 && isSpawning; budget--)
        {
            if (spawnerIndex >= activeSpawners.Count)
            {
                isSpawning = false;
                break;
            }
            if (creepIndex >= CreepsAt(activeSpawners[spawnerIndex]))
            {
                spawnerIndex++;
                creepIndex = 0;
                budget++; // moving to the next spawner costs nothing
                continue;
            }

            SpawnEnemy(spawningWave.enemy, activeSpawners[spawnerIndex], creepIndex);
            creepIndex++;
        }

        if (!isSpawning) GameManager.Instance.NotifyWaveSpawningComplete();
    }

    /// <summary>How many creeps of the current wave spawn at this spawner (the map fixes the count at one spawn).</summary>
    private int CreepsAt(Spawner spawner)
    {
        return spawner.amountOverride >= 0 ? spawner.amountOverride : spawningWave.enemyCount;
    }

    private void SpawnEnemy(EnemyData data, Spawner spawner, int index)
    {
        if (spawner.steps == null || spawner.steps.Length == 0) return;

        Enemy enemy = Instantiate(enemyPrefab, spawner.waypoints[0], Quaternion.identity);
        enemy.Init(data, spawner, index, initialOrderDelay);

        GameManager.Instance.RegisterEnemy(enemy);
    }

    public int GetTotalWaves() => waves.Length;
    public int GetCurrentWave() => currentWave;
}
