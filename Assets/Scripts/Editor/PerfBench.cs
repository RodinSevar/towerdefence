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
            case 11: PathEquivalence(); break;
            case 1: PathCosts(); break;
            case 2: SinglePlacement(); break;
            case 3: FillTowers(); break;
            case 4: SpawnWave(); break;
            case 5: MeasureLoad(); break;
            case 6: PlacementUnderLoad(); break;
            case 7: MeasureSpike(); break;
            case 8: Attribution(); break;
            case 9: Behavior(); break;
            case 12: WaitSpawn(); break;
            case 13: RouteCheck(); break;
        }
    }

    private static void Setup()
    {
        GameManager.Instance.CancelWaveTimer();          // no automatic wave
        GameManager.Instance.AddGold(10000000);
        // creeps leaking must not end the game (GameOver freezes game time); give the bench effectively unlimited lives
        typeof(GameManager).GetField("currentLives", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(GameManager.Instance, 1000000);
        var spawners = WaveManager.Instance.activeSpawners;
        Debug.Log($"BENCH env spawners={spawners.Count} towerData={Towers().Length} unityVersion={Application.unityVersion}");
        step = 10;
    }

    private static TowerData[] Towers() => RaceManager.Instance.Races[0].towers;

    /// <summary>Asks the path system for a direction on every spawner segment, as the creeps do each frame.</summary>
    private static void RegenAll()
    {
        PathManager.Instance.FlushDirtyFields(); // fields are normally refreshed a little per frame; force it to measure total work
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
        TerrainBuilder.Instance.EnsureBuilt();
        var mesh = TerrainBuilder.Instance.GroundMesh;
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
        step = 11;
    }

    // ---- routes: every spawner walks the map's route to the exit ----
    private static int routeFrame, routeCreeps, routeLivesStart;
    private static Dictionary<Spawner, int> routeMaxStep;
    private static Dictionary<Enemy, Vector3> routeSpawnPos;
    private static float routeStartTime;
    private static bool routeSpawned;
    private static Dictionary<Enemy, float> routeFirstMove; // game time (since the wave started) each creep first moved
    private static int routeMaxQueue;

    private static void RouteCheck()
    {
        if (!routeSpawned)
        {
            foreach (var s in WaveManager.Instance.activeSpawners)
                Debug.Log($"BENCH route spawner='{s.name}' player='{s.transform.parent.name}' group={s.routeName} amount={(s.amountOverride < 0 ? "wave" : s.amountOverride.ToString())} " +
                          $"steps={string.Join(" > ", System.Array.ConvertAll(s.steps, x => x.name))}");
            routeMaxStep = new Dictionary<Spawner, int>();
            routeSpawnPos = new Dictionary<Enemy, Vector3>();
            routeFirstMove = new Dictionary<Enemy, float>();
            routeMaxQueue = 0;
            routeLivesStart = GameManager.Instance.GetCurrentLives();
            Time.timeScale = 20f;
            WaveManager.Instance.StartWave(1);
            routeStartTime = (float)Simulation.Time;
            routeSpawned = true;
            routeFrame = 0;
            return;
        }
        if (WaveManager.Instance.IsSpawning) return;
        routeFrame++;

        var alive = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        if (routeFrame == 1) routeCreeps = alive.Length;
        foreach (var e in alive)
        {
            if (!routeSpawnPos.ContainsKey(e)) routeSpawnPos[e] = e.Position;
            routeMaxStep.TryGetValue(e.Origin, out int best);
            if (e.StepIndex > best) routeMaxStep[e.Origin] = e.StepIndex;
        }

        double gameTime = Simulation.Time - routeStartTime;
        routeMaxQueue = Mathf.Max(routeMaxQueue, EnemyManager.Instance.PathQueueLength);
        foreach (var e in alive)
            if (!routeFirstMove.ContainsKey(e) && (e.Position - routeSpawnPos[e]).sqrMagnitude > 0.0025f)
                routeFirstMove[e] = (float)gameTime;

        if (alive.Length > 0 && gameTime < 400.0) return;

        int leaked = routeLivesStart - GameManager.Instance.GetCurrentLives();
        if (routeFirstMove.Count > 0)
        {
            var starts = new List<float>(routeFirstMove.Values);
            starts.Sort();
            int movedBy1 = starts.FindAll(t => t <= 1f).Count, movedBy2 = starts.FindAll(t => t <= 2f).Count;
            Debug.Log($"BENCH routeStagger creeps={starts.Count} firstMove_s={starts[0]:F2} median_s={starts[starts.Count / 2]:F2} lastMove_s={starts[starts.Count - 1]:F2} " +
                      $"movedBy1s={movedBy1} movedBy2s={movedBy2} peakQueue={routeMaxQueue} rate={EnemyManager.Instance.pathRequestsPerSecond}/s");
        }
        Debug.Log($"BENCH routeResult gameSeconds={gameTime:F0} spawned={routeCreeps} leaked={leaked} stillAlive={alive.Length}");
        foreach (var s in WaveManager.Instance.activeSpawners)
        {
            routeMaxStep.TryGetValue(s, out int step);
            Debug.Log($"BENCH routeReached spawner='{s.name}' furthestStep={step + 1}/{s.steps.Length} ({s.steps[Mathf.Min(step, s.steps.Length - 1)].name})");
        }
        foreach (var e in alive) Debug.Log($"BENCH routeStuck spawner='{e.Origin.name}' step={e.StepIndex} at={e.Position}");

        Time.timeScale = 1f;
        GameManager.Instance.CancelWaveTimer(); // the cleared wave schedules the next one; the bench drives waves itself
        step = 1;
    }

    // ---- equivalence with the original reachability rules (kept here only as a test reference) ----

    private static bool LegacyValid(Vector2Int from, Vector2Int to)
    {
        var g = GridManager.Instance;
        if (g.IsCellOccupied(to)) return false;
        return Mathf.Abs(g.GetCellHeight(from) - g.GetCellHeight(to)) <= 1.2f;
    }

    private static HashSet<Vector2Int> LegacyReachedFrom(Vector2Int target)
    {
        var seen = new HashSet<Vector2Int> { target };
        var q = new Queue<Vector2Int>();
        q.Enqueue(target);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var nb = new Vector2Int(cur.x + dx, cur.y + dy);
                if (!LegacyValid(nb, cur)) continue;
                if (dx != 0 && dy != 0 && (!LegacyValid(new Vector2Int(nb.x, cur.y), cur) || !LegacyValid(new Vector2Int(cur.x, nb.y), cur))) continue;
                if (!GridManager.Instance.IsPlayable(nb)) continue;
                if (seen.Add(nb)) q.Enqueue(nb);
            }
        }
        return seen;
    }

    private static bool LegacyValidate()
    {
        var g = GridManager.Instance;
        var cache = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        foreach (var s in WaveManager.Instance.activeSpawners)
        {
            var wp = s.waypoints;
            for (int i = 0; wp != null && i < wp.Length - 1; i++)
            {
                var start = g.WorldToGridCell(wp[i]);
                var target = g.WorldToGridCell(wp[i + 1]);
                if (!cache.TryGetValue(target, out var reached)) cache[target] = reached = LegacyReachedFrom(target);
                if (!reached.Contains(start)) return false;
            }
        }
        return true;
    }

    private static void PathEquivalence()
    {
        var g = GridManager.Instance;
        var changed = new List<Vector2Int>();
        int agree = 0, newStricter = 0, newLooser = 0, trials = 0;

        void Compare(string label)
        {
            bool fresh = PathManager.Instance.ValidateFullMaze();
            bool legacy = LegacyValidate();
            trials++;
            if (fresh == legacy) agree++;
            else if (!fresh) { newStricter++; Debug.Log($"BENCH pathEquivalence stricter than legacy (expected only for corner squeezes): {label}"); }
            else { newLooser++; Debug.Log($"BENCH pathEquivalence MISMATCH (new accepts, legacy rejects): {label}"); }
        }

        // 1) random towers near the paths
        for (int i = 0; i < 40; i++)
        {
            var cell = g.FootprintOrigin(RandomNearPath());
            if (!g.CanBuildFootprint(cell)) continue;
            g.OccupyFootprint(cell);
            Compare($"random tower at {cell}");
            if (PathManager.Instance.ValidateFullMaze()) changed.Add(cell); else g.FreeFootprint(cell);
        }

        // 2) targeted layouts around a waypoint: full enclosure, one free side, one free diagonal (corner squeeze)
        var sp = WaveManager.Instance.activeSpawners[0];
        Vector2Int T = g.WorldToGridCell(sp.waypoints[sp.waypoints.Length - 1]);
        var ring = new List<Vector2Int>();
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (dx != 0 || dy != 0) ring.Add(new Vector2Int(T.x + dx, T.y + dy));

        void WithRing(System.Predicate<Vector2Int> occupy, string label)
        {
            var placed = new List<Vector2Int>();
            foreach (var c in ring)
                if (occupy(c) && !g.IsCellOccupied(c)) { g.OccupyCell(c); placed.Add(c); }
            Compare(label);
            foreach (var c in placed) g.FreeCell(c);
        }
        WithRing(c => true, "target fully enclosed");
        WithRing(c => c != new Vector2Int(T.x - 1, T.y), "target with one free orthogonal side");
        WithRing(c => c != new Vector2Int(T.x + 1, T.y + 1), "target with only a free diagonal (corner squeeze)");

        foreach (var c in changed) g.FreeFootprint(c); // restore the layout
        Debug.Log($"BENCH pathEquivalence trials={trials} agree={agree} newStricterThanLegacy={newStricter} newLooserThanLegacy={newLooser}");
        step = 13;
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

    private static double spawnStart, spawnMaxDt;
    private static int spawnFrames, materialsBefore;

    private static void SpawnWave()
    {
        materialsBefore = Resources.FindObjectsOfTypeAll<Material>().Length;
        spawnStart = Time.realtimeSinceStartupAsDouble;
        bool ok = WaveManager.Instance.StartWave(BenchWave);
        Debug.Log($"BENCH spawnWave wave={BenchWave} started={ok}");
        spawnFrames = 0; spawnMaxDt = 0;
        step = 12;
    }

    private static void WaitSpawn()
    {
        spawnFrames++;
        double dt = Time.unscaledDeltaTime;
        if (dt > spawnMaxDt) spawnMaxDt = dt;
        if (WaveManager.Instance.IsSpawning) return;

        int enemies = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length;
        int materialsAfter = Resources.FindObjectsOfTypeAll<Material>().Length;
        Debug.Log($"BENCH spawnDone enemies={enemies} frames={spawnFrames} maxFrameWhileSpawning_ms={spawnMaxDt * 1000:F1} materialsBefore={materialsBefore} materialsAfter={materialsAfter}");

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

    // ---- behavior: fixed game time, so results do not depend on how fast frames run ----
    private static double behaviorStart = -1;
    private static int aliveAtStart, livesAtStart, goldAtStart;
    private const double BehaviorSeconds = 5.0;

    private static void Behavior()
    {
        if (behaviorStart < 0)
        {
            behaviorStart = Simulation.Time;
            aliveAtStart = EnemyManager.Instance.Count;
            livesAtStart = GameManager.Instance.GetCurrentLives();
            goldAtStart = GameManager.Instance.GetCurrentGold();
            return;
        }
        if (Simulation.Time - behaviorStart < BehaviorSeconds) return;

        int alive = EnemyManager.Instance.Count;
        int leaked = livesAtStart - GameManager.Instance.GetCurrentLives();
        int killed = aliveAtStart - alive - leaked;
        int projectilesActive = 0;
        foreach (var p in UnityEngine.Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) if (p.gameObject.activeSelf) projectilesActive++;

        // ray picking: a ray straight down onto a creep must select it
        string pick = "no creep";
        var first = UnityEngine.Object.FindFirstObjectByType<Enemy>();
        if (first != null)
        {
            var picked = EnemyManager.Instance.PickAlongRay(new Ray(first.AimPoint + Vector3.up * 10f, Vector3.down), 0.6f, out float d);
            pick = picked == first ? $"ok (distance {d:F1})" : (picked == null ? "MISSED" : "picked a different creep (overlapping)");
        }

        // minimap: count painted pixels in the dots texture
        int enemyPixels = 0, towerPixels = 0;
        var dots = GameObject.Find("MinimapDots");
        if (dots != null && dots.GetComponent<UnityEngine.UI.RawImage>().texture is Texture2D tex)
        {
            foreach (var c in tex.GetPixels32()) { if (c.r > 200 && c.b < 50) enemyPixels++; else if (c.b > 200 && c.r < 100) towerPixels++; }
        }

        Debug.Log($"BENCH behavior gameSeconds={BehaviorSeconds:F0} aliveStart={aliveAtStart} aliveEnd={alive} killed={killed} leaked={leaked} " +
                  $"goldGained={GameManager.Instance.GetCurrentGold() - goldAtStart} activeProjectiles={projectilesActive} " +
                  $"pick={pick} minimapEnemyPixels={enemyPixels} minimapTowerPixels={towerPixels}");
        attrIndex = 0; attrFrames = 0; attrSum = 0;
        step = 8;
    }

    // ---- attribution: measure average frame time with each system switched off ----
    private static int attrIndex, attrFrames;
    private static double attrSum;
    private static readonly string[] AttrNames = { "everything on", "minimap off", "towers off", "enemies off", "minimap+towers+enemies off" };

    private static void SetSystems(bool minimap, bool towers, bool enemies)
    {
        if (MinimapManager.Instance != null) MinimapManager.Instance.enabled = minimap;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None)) t.enabled = towers;
        if (EnemyManager.Instance != null) EnemyManager.Instance.enabled = enemies;
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
            step =  9;
            behaviorStart = -1;
        }
    }
}
