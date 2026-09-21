using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replays a fixed scripted game (tower placements, a race unlock, a sale, a big wave) and logs the state checksum every
/// 30 ticks as "DET tick=N hash=H". Run it twice with different game speeds; frame pacing must not change a single hash:
///   Unity -batchmode -nographics -projectPath . -executeMethod DeterminismCheck.Run -logFile det_a.log -timescale 20
///   Unity -batchmode -nographics -projectPath . -executeMethod DeterminismCheck.Run -logFile det_b.log -timescale 6
/// then compare the DET lines of the two logs (Unity must be closed).
/// </summary>
[InitializeOnLoad]
public static class DeterminismCheck
{
    private const string ActiveKey = "DeterminismCheck.Active";
    private const string ScaleKey = "DeterminismCheck.Scale";
    private const int EndTick = 2400;

    private static bool started;
    private static double startTime;

    static DeterminismCheck()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
    }

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-timescale");
        SessionState.SetFloat(ScaleKey, i >= 0 ? float.Parse(args[i + 1]) : 10f);
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        Debug.Log("DET done");
        EditorApplication.Exit(code);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - startTime > 240) { Debug.Log("DET timeout"); Finish(2); return; }
        if (!EditorApplication.isPlaying || GameManager.Instance == null || TowerManager.Instance == null || PathManager.Instance == null
            || RaceManager.Instance == null || WaveManager.Instance == null || GridManager.Instance == null || PlayerManager.Instance == null
            || Simulation.Instance == null || WaveManager.Instance.activeSpawners.Count == 0 || Time.frameCount < 10) return;

        if (!started)
        {
            started = true;
            Script();
            Time.timeScale = SessionState.GetFloat(ScaleKey, 10f);
            Simulation.OnChecksum += (tick, hash) => Debug.Log($"DET tick={tick} hash={hash:X8}");
        }
        if (Simulation.CurrentTick >= EndTick) 
        {
            Debug.Log($"DET summary towers={TowerManager.Instance.TowerCount} enemies={EnemyManager.Instance.Enemies.Count} " +
                      $"gold=[{string.Join(",", System.Linq.Enumerable.Select(PlayerManager.Instance.Players, p => p.gold))}] wave={GameManager.Instance.CurrentWave}");
            Finish(0);
        }
    }

    /// <summary>Schedules every action of the scripted game at fixed ticks.</summary>
    private static void Script()
    {
        // three players; the game is scripted, so no input is read
        PlayerManager.Instance.Configure(3, 0);
        foreach (var p in PlayerManager.Instance.Players) p.gold = 100000;
        GameManager.Instance.CancelWaveTimer();
        typeof(GameManager).GetField("currentLives", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(GameManager.Instance, 1000000);

        var rng = new System.Random(4242);
        var g = GridManager.Instance;
        var spawners = WaveManager.Instance.activeSpawners;
        var tower = RaceManager.Instance.Races[0].towers[1];
        int start = 60; // absolute ticks: how many ticks ran before this script started depends on startup timing
        Debug.Log($"DET scriptStart tick={Simulation.CurrentTick}");
        for (int i = 0; i < 70; i++)
        {
            var s = spawners[rng.Next(spawners.Count)];
            var wp = s.waypoints[rng.Next(s.waypoints.Length)];
            var origin = g.FootprintOrigin(new Vector3(wp.x + rng.Next(-8, 9), 0, wp.z + rng.Next(-8, 9)));
            CommandQueue.Schedule(new PlaceTowerCommand
            {
                playerId = i % 3, sequence = i, towerId = tower.wc3Id, originX = origin.x, originY = origin.y,
            }, start + i * 2);
        }
        CommandQueue.Schedule(new UnlockRaceCommand { playerId = 1, raceIndex = 1 }, start + 100);
        CommandQueue.Schedule(new TransferGoldCommand { playerId = 0, toPlayerId = 2, amount = 5000 }, start + 120);
        CommandQueue.Schedule(new SellTowerCommand { playerId = 0, towerInstanceId = 1 }, start + 300);
        CommandQueue.Schedule(new SellTowerCommand { playerId = 1, towerInstanceId = 5 }, start + 300); // not player 1's: refused

        // a large wave (level 45), starting once the towers are up
        typeof(GameManager).GetField("currentWave", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(GameManager.Instance, 44);
        GameManager.Instance.ScheduleNextWave((300 - Simulation.CurrentTick) * Simulation.TickDt); // starts at tick 300
    }
}
