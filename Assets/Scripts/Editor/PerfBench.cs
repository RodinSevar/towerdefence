using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Headless performance benchmark. Runs the real scene in play mode and logs "BENCH ..." lines:
///   - path validation and flow-field regeneration cost (the tower-placement spike)
///   - cost of placing a tower, alone and under load
///   - frame time and GC allocation with a full wave (default: level 45, 100 creeps per spawner) and many towers
///
/// Run (Unity must be closed; the process exits when done):
///   Unity -batchmode -nographics -projectPath . -executeMethod PerfBench.Run -logFile bench.log
/// Numbers are CPU/logic cost only (no rendering in -nographics mode), so GPU cost is not measured here.
/// </summary>
[InitializeOnLoad]
public static class PerfBench
{
    private const string ActiveKey = "PerfBench.Active";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const int BenchWave = 45;
    private const int TowerCount = 80;
    private const int MeasureFrames = 300;
    private const double TimeoutSeconds = 300;

    private static int step;
    private static int stepFrames;
    private static double startTime;
    private static readonly System.Random rng = new System.Random(1234);

    // frame statistics
    private static double sumDt, maxDt;
    private static long sumAlloc;
    private static int gcCollections;
    private static ProfilerRecorder allocRecorder;
    private static int spikeFramesLeft;
    private static double spikeMax;

    static PerfBench()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    private static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        allocRecorder.Dispose();
        Debug.Log("BENCH done");
        EditorApplication.Exit(exitCode);
    }

    private static bool Ready()
    {
        return EditorApplication.isPlaying
            && GameManager.Instance != null && TowerManager.Instance != null && PathManager.Instance != null
            && RaceManager.Instance != null && WaveManager.Instance != null && GridManager.Instance != null
            && WaveManager.Instance.activeSpawners.Count > 0 && Time.frameCount > 10;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - startTime > TimeoutSeconds)
        {
            Debug.Log("BENCH timeout");
            Finish(2);
            return;
        }
        if (!Ready()) return;

        stepFrames++;
        switch (step)
        {
            case 0: Setup(); break;
            case 10: TerrainCheck(); break;
            case 1: PathCosts(); break;
            case 2: SinglePlacement(); break;
            case 3: FillTowers(); break;
            case 4: SpawnWave(); break;
            case 5: MeasureLoad(); break;
            case 6: PlacementUnderLoad(); break;
            case 7: MeasureSpike(); break;
            case 8: Attribution(); break;
        }
    }

    private static void Setup()
    {
        GameManager.Instance.CancelInvoke();          // no automatic wave
        GameManager.Instance.AddGold(10000000);
        var spawners = WaveManager.Instance.activeSpawners;
        Debug.Log($"BENCH env spawners={spawners.Count} towerData={Towers().Length} unityVersion={Application.unityVersion}");
        step = 10;
    }

    private static TowerData[] Towers() => RaceManager.Instance.Races[0].towers;

    /// <summary>Asks the path system for a direction on every spawner segment, as the creeps do each frame.</summary>
    private static void RegenAll()
    {
        foreach (var spawner in WaveManager.Instance.activeSpawners)
        {
            var wp = spawner.waypoints;
            if (wp == null) continue;
            for (int i = 0; i < wp.Length - 1; i++)
                PathManager.Instance.GetFlowDirection(wp[i], wp[i + 1]);
        }
    }

    /// <summary>
    /// Checks that terrain wall triangles face open air. For every near-vertical triangle, probes a point just outside
    /// along its normal and casts a ray down onto the terrain collider: if the ground there is at or below the triangle,
    /// the normal points outward (visible from the low side); if it is higher, the wall faces into solid ground (culled).
    /// </summary>
    private static void TerrainCheck()
    {
        var mapGen = UnityEngine.Object.FindFirstObjectByType<MapGenerator>();
        var mesh = mapGen.GetComponent<MeshFilter>().sharedMesh;
        var v = mesh.vertices; var tris = mesh.triangles;
        int walls = 0, outward = 0, inward = 0, undecided = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-8f) continue;
            n.Normalize();
            if (Mathf.Abs(n.y) > 0.5f) continue; // top face
            walls++;
            Vector3 centroid = (a + b + c) / 3f;
            Vector3 probe = centroid + new Vector3(n.x, 0, n.z).normalized * 0.05f;
            if (Physics.Raycast(probe + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f))
            {
                if (hit.point.y <= centroid.y + 0.01f) outward++; else inward++;
            }
            else undecided++;
        }
        Debug.Log($"BENCH terrainWalls triangles={walls} facingOutward={outward} facingIntoGround={inward} undecided={undecided}");
        step = 1;
    }

    private static void PathCosts()
    {
        var pm = PathManager.Instance;

        pm.ClearCache();
        var sw = Stopwatch.StartNew();
        bool ok = pm.ValidateFullMaze();
        double cold = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        pm.ValidateFullMaze();
        double warm = sw.Elapsed.TotalMilliseconds;

        pm.ClearCache();
        sw.Restart();
        RegenAll();
        double regen = sw.Elapsed.TotalMilliseconds;

        Debug.Log($"BENCH path valid={ok} validateCold_ms={cold:F2} validateWarm_ms={warm:F2} regenAllFields_ms={regen:F2}");
        step = 2;
    }

    private static Vector3 RandomNearPath()
    {
        var spawners = WaveManager.Instance.activeSpawners;
        var s = spawners[rng.Next(spawners.Count)];
        var wp = s.waypoints[rng.Next(s.waypoints.Length)];
        return new Vector3(wp.x + rng.Next(-8, 9), 0, wp.z + rng.Next(-8, 9));
    }

    private static bool PlaceOne(TowerData data)
    {
        return TowerManager.Instance.TryPlaceTowerAt(data, RandomNearPath());
    }

    private static void SinglePlacement()
    {
        TowerData data = Towers()[1];
        int attempts = 0;
        double placeMs = -1, regenMs = -1;
        while (attempts++ < 200)
        {
            var sw = Stopwatch.StartNew();
            if (PlaceOne(data))
            {
                placeMs = sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                RegenAll(); // what the creeps pay right after the maze changed
                regenMs = sw.Elapsed.TotalMilliseconds;
                break;
            }
        }
        Debug.Log($"BENCH placeOneTower_ms={placeMs:F2} regenAfterPlace_ms={regenMs:F2} attempts={attempts}");
        step = 3;
    }

    private static void FillTowers()
    {
        var towers = Towers();
        int placed = 0, attempts = 0;
        var sw = Stopwatch.StartNew();
        while (placed < TowerCount && attempts++ < 4000)
        {
            if (PlaceOne(towers[1 + placed % (towers.Length - 1)])) placed++;
        }
        RegenAll();
        Debug.Log($"BENCH towers placed={placed} attempts={attempts} total_ms={sw.Elapsed.TotalMilliseconds:F0}");
        step = 4;
    }

    private static void SpawnWave()
    {
        var sw = Stopwatch.StartNew();
        bool ok = WaveManager.Instance.StartWave(BenchWave);
        double ms = sw.Elapsed.TotalMilliseconds;
        int enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length;
        Debug.Log($"BENCH spawnWave wave={BenchWave} ok={ok} enemies={enemies} spawn_ms={ms:F1}");

        allocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        gcCollections = GC.CollectionCount(0);
        sumDt = maxDt = 0; sumAlloc = 0; stepFrames = 0;
        step = 5;
    }

    private static void MeasureLoad()
    {
        double dt = Time.unscaledDeltaTime;
        sumDt += dt;
        if (dt > maxDt) maxDt = dt;
        if (allocRecorder.Valid) sumAlloc += allocRecorder.LastValue;

        if (stepFrames >= MeasureFrames)
        {
            int enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length;
            Debug.Log($"BENCH load frames={stepFrames} avgFrame_ms={sumDt / stepFrames * 1000:F2} maxFrame_ms={maxDt * 1000:F1} " +
                      $"avgGCAlloc_KB={sumAlloc / (double)stepFrames / 1024:F1} gc0Collections={GC.CollectionCount(0) - gcCollections} enemiesAlive={enemies}");
            step = 6;
        }
    }

    private static void PlacementUnderLoad()
    {
        var data = Towers()[1];
        double ms = -1;
        for (int i = 0; i < 200; i++)
        {
            var sw = Stopwatch.StartNew();
            if (PlaceOne(data)) { ms = sw.Elapsed.TotalMilliseconds; break; }
        }
        Debug.Log($"BENCH placeUnderLoad_ms={ms:F2}");
        spikeFramesLeft = 10;
        spikeMax = 0;
        step = 7;
    }

    // ---- attribution: measure average frame time with each system switched off ----
    private static int attrIndex, attrFrames;
    private static double attrSum;
    private static readonly string[] AttrNames = { "everything on", "minimap off", "towers off", "enemies off", "minimap+towers+enemies off" };

    private static void SetSystems(bool minimap, bool towers, bool enemies)
    {
        if (MinimapManager.Instance != null) MinimapManager.Instance.enabled = minimap;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None)) t.enabled = towers;
        foreach (var e in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) e.enabled = enemies;
    }

    private static void Attribution()
    {
        const int warmup = 10, frames = 90;
        if (attrFrames == 0)
        {
            switch (attrIndex)
            {
                case 0: SetSystems(true, true, true); break;
                case 1: SetSystems(false, true, true); break;
                case 2: SetSystems(true, false, true); break;
                case 3: SetSystems(true, true, false); break;
                case 4: SetSystems(false, false, false); break;
            }
            attrSum = 0;
        }
        attrFrames++;
        if (attrFrames > warmup) attrSum += Time.unscaledDeltaTime;
        if (attrFrames >= warmup + frames)
        {
            Debug.Log($"BENCH attribution [{AttrNames[attrIndex]}] avgFrame_ms={attrSum / frames * 1000:F2}");
            attrIndex++; attrFrames = 0;
            if (attrIndex >= AttrNames.Length) Finish(0);
        }
    }

    private static void MeasureSpike()
    {
        double dt = Time.unscaledDeltaTime;
        if (dt > spikeMax) spikeMax = dt;
        if (--spikeFramesLeft <= 0)
        {
            Debug.Log($"BENCH spikeAfterPlacement maxFrame_ms={spikeMax * 1000:F1}");
            attrIndex = 0; attrFrames = 0; attrSum = 0;
            step = 8;
        }
    }
}
