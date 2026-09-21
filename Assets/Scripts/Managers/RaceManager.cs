using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which tower races (elements) the player owns and which one is active. The active race decides which
/// towers appear on the build menu. Unlocking a race costs lumber (the original map: 1 lumber per race).
/// A constructor-unit mechanic can later replace "active race" with "selected constructor".
/// </summary>
public class RaceManager : Singleton<RaceManager>
{
    [SerializeField] private RaceData[] races;

    private readonly HashSet<RaceData> owned = new HashSet<RaceData>();
    private RaceData active;

    /// <summary>Raised when ownership or the active race changes.</summary>
    public event Action OnRacesChanged;

    public IReadOnlyList<RaceData> Races => races ?? Array.Empty<RaceData>();
    public RaceData ActiveRace => active;
    public TowerData[] ActiveTowers => active != null && active.towers != null ? active.towers : Array.Empty<TowerData>();

    private void Start()
    {
        // Races that cost nothing are owned from the start.
        foreach (var race in Races)
        {
            if (race != null && race.lumberCost <= 0) owned.Add(race);
        }
        foreach (var race in Races)
        {
            if (race != null && owned.Contains(race)) { active = race; break; }
        }
        OnRacesChanged?.Invoke();
    }

    public bool IsOwned(RaceData race) => owned.Contains(race);

    public bool CanUnlock(RaceData race)
    {
        return race != null && !owned.Contains(race) && GameManager.Instance.GetCurrentLumber() >= race.lumberCost;
    }

    /// <summary>Spends lumber to own the race and makes it the active one.</summary>
    public bool TryUnlock(RaceData race)
    {
        if (!CanUnlock(race) || !GameManager.Instance.TrySpendLumber(race.lumberCost)) return false;
        owned.Add(race);
        SetActive(race);
        return true;
    }

    public void SetActive(RaceData race)
    {
        if (race == null || !owned.Contains(race) || race == active) return;
        active = race;
        if (TowerManager.Instance != null) TowerManager.Instance.CancelPlacement();
        OnRacesChanged?.Invoke();
    }
}
