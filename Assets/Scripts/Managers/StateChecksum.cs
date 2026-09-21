using System;

/// <summary>
/// A hash of the whole game state that matters to the simulation: players, lives, wave, every tower and every creep. In a
/// lockstep game each machine computes it on the same tick and they compare; a difference means the machines have drifted
/// apart (a desync). It also lets tests check that a game replays identically.
/// </summary>
public static class StateChecksum
{
    private const uint Basis = 2166136261u, Prime = 16777619u; // FNV-1a

    public static uint Compute()
    {
        uint h = Basis;
        Add(ref h, Simulation.CurrentTick);

        if (GameManager.Instance != null)
        {
            Add(ref h, GameManager.Instance.GetCurrentLives());
            Add(ref h, GameManager.Instance.CurrentWave);
            Add(ref h, GameManager.Instance.WaveCountdown);
            Add(ref h, GameManager.Instance.EnemiesAlive);
        }

        if (PlayerManager.Instance != null)
        {
            foreach (var p in PlayerManager.Instance.Players)
            {
                Add(ref h, p.id);
                Add(ref h, p.active ? 1 : 0);
                Add(ref h, p.gold);
                Add(ref h, p.lumber);
                Add(ref h, p.ownedRaces.Count);
                Add(ref h, p.activeRace != null ? RaceManager.Instance.IndexOf(p.activeRace) : -1);
            }
        }

        if (TowerManager.Instance != null)
        {
            foreach (var t in TowerManager.Instance.Towers)
            {
                Add(ref h, t.Id);
                Add(ref h, t.Owner);
                Add(ref h, t.Level);
                Add(ref h, t.transform.position.x);
                Add(ref h, t.transform.position.z);
                Add(ref h, t.FireTimer);
            }
        }

        if (EnemyManager.Instance != null)
        {
            var list = EnemyManager.Instance.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                Enemy e = list[i];
                if (e == null) continue;
                Add(ref h, e.Position.x);
                Add(ref h, e.Position.z);
                Add(ref h, e.GetHealth());
                Add(ref h, e.StepIndex);
            }
        }
        return h;
    }

    private static void Add(ref uint h, int v)
    {
        unchecked
        {
            h = (h ^ (uint)v) * Prime;
        }
    }

    private static void Add(ref uint h, float v) => Add(ref h, BitConverter.SingleToInt32Bits(v));
}
