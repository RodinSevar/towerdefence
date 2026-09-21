using UnityEngine;

/// <summary>
/// Runs the game logic at a fixed rate, independent of the frame rate: every tick has the same length, and systems run in one
/// fixed order. Two machines that start from the same state and apply the same inputs on the same ticks therefore end up in
/// the same state, which lockstep multiplayer relies on. Rendering-only work (camera, UI, minimap) stays in Update.
///
/// Anything that changes game state (movement, targeting, damage, timers, spawning, path refresh) must run from a tick, use
/// <see cref="TickDt"/> instead of Time.deltaTime, and avoid wall-clock time, Unity's Random and unordered iteration.
/// </summary>
public class Simulation : Singleton<Simulation>
{
    public const int TicksPerSecond = 60;
    public const float TickDt = 1f / TicksPerSecond;

    /// <summary>Ticks executed since the game started.</summary>
    public static int CurrentTick { get; private set; }

    /// <summary>Simulated seconds since the game started.</summary>
    public static double Time => CurrentTick * (double)TickDt;

    [Tooltip("Ticks run per rendered frame at most; if the game falls further behind it slows down instead of freezing")]
    [SerializeField] private int maxTicksPerFrame = 30;

    /// <summary>How often (in ticks) the state checksum is computed.</summary>
    public const int ChecksumInterval = 30;

    /// <summary>Raised after every <see cref="ChecksumInterval"/> ticks with the tick number and the state hash.</summary>
    public static event System.Action<int, uint> OnChecksum;

    /// <summary>False holds the game at its current tick (menu, lobby, desync).</summary>
    public static bool Running = true;

    /// <summary>True while a network game waits for other players' commands before it can go on.</summary>
    public static bool Stalled { get; private set; }

    private float accumulator;

    protected override void OnSingletonAwake()
    {
        CurrentTick = 0;
        Running = true;
        Stalled = false;
        CommandQueue.Clear();
    }

    private void Update()
    {
        if (!Running) return;

        // UnityEngine.Time.deltaTime is 0 while the game is paused (timeScale 0) and scaled by game speed.
        accumulator += UnityEngine.Time.deltaTime;
        int ran = 0;
        Stalled = false;
        while (accumulator >= TickDt && ran < maxTicksPerFrame)
        {
            // In a network game a tick may only run once every player's commands for its turn are known
            if (NetworkGame.Active && !NetworkGame.Session.CanRunTick(CurrentTick + 1)) { Stalled = true; break; }
            Step();
            if (NetworkGame.Active) NetworkGame.Session.AfterTick(CurrentTick);
            accumulator -= TickDt;
            ran++;
        }
        if (accumulator > TickDt) accumulator = TickDt; // the rest of a backlog is dropped
    }

    private static void Step()
    {
        CurrentTick++;
        CommandQueue.ExecuteDue(CurrentTick); // player commands first, then the systems

        // Fixed order. Later systems see the results of earlier ones within the same tick.
        if (PathManager.Instance != null) PathManager.Instance.SimTick();
        if (WaveManager.Instance != null) WaveManager.Instance.SimTick();
        if (EnemyManager.Instance != null) EnemyManager.Instance.SimTick(TickDt);
        if (TowerManager.Instance != null) TowerManager.Instance.SimTick(TickDt);
        Projectile.SimTickAll(TickDt);
        if (GameManager.Instance != null) GameManager.Instance.SimTick(TickDt);

        if (CurrentTick % ChecksumInterval == 0 && OnChecksum != null) OnChecksum(CurrentTick, StateChecksum.Compute());
    }
}
