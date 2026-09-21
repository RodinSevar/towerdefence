using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The catalogue of tower races, plus the local player's view of it. Each player owns their own races and has their own active
/// one (see <see cref="PlayerState"/>); the active race decides which towers appear on that player's build menu. Unlocking a race
/// costs lumber (the original map: 1 lumber per race). A constructor-unit mechanic can later replace "active race" with
/// "selected constructor".
///
/// Choosing a race is a <see cref="GameCommand"/>: <see cref="TryUnlock"/> and <see cref="SetActive"/> issue it for the local
/// player, and <see cref="ExecuteUnlock"/> / <see cref="ExecuteSetActive"/> apply it on the simulation tick.
/// </summary>
public class RaceManager : Singleton<RaceManager>
{
    [SerializeField] private RaceData[] races;

    /// <summary>Raised when the local player's ownership or active race changes.</summary>
    public event Action OnRacesChanged;

    public IReadOnlyList<RaceData> Races => races ?? Array.Empty<RaceData>();

    private PlayerState Local => PlayerManager.Instance != null ? PlayerManager.Instance.Local : null;

    public RaceData ActiveRace => Local?.activeRace;
    public TowerData[] ActiveTowers => ActiveRace != null && ActiveRace.towers != null ? ActiveRace.towers : Array.Empty<TowerData>();

    private void Start()
    {
        if (PlayerManager.Instance != null) PlayerManager.Instance.OnLocalPlayerChanged += _ => OnRacesChanged?.Invoke();
        OnRacesChanged?.Invoke();
    }

    public bool IsOwned(RaceData race) => Local != null && Local.ownedRaces.Contains(race);

    public bool CanUnlock(RaceData race)
    {
        return race != null && Local != null && !Local.ownedRaces.Contains(race) && Local.lumber >= race.lumberCost;
    }

    public int IndexOf(RaceData race) => Array.IndexOf(races, race);

    /// <summary>The tower with this WC3 id in any race, or null.</summary>
    public TowerData FindTower(string wc3Id)
    {
        foreach (var race in Races)
            if (race != null && race.towers != null)
                foreach (var t in race.towers)
                    if (t != null && t.wc3Id == wc3Id) return t;
        return null;
    }

    // ------------------------------------------------------------------------------------------------ local player's requests

    /// <summary>Asks to spend lumber to own the race and make it active.</summary>
    public bool TryUnlock(RaceData race)
    {
        if (!CanUnlock(race)) return false;
        CommandQueue.Submit(new UnlockRaceCommand { playerId = PlayerManager.Instance.LocalPlayerId, raceIndex = IndexOf(race) });
        return true;
    }

    /// <summary>Asks to make an owned race the active one.</summary>
    public void SetActive(RaceData race)
    {
        if (race == null || Local == null || !Local.ownedRaces.Contains(race) || race == Local.activeRace) return;
        CommandQueue.Submit(new SetActiveRaceCommand { playerId = PlayerManager.Instance.LocalPlayerId, raceIndex = IndexOf(race) });
    }

    // ------------------------------------------------------------------------------------------------ applied on the tick

    public void ExecuteUnlock(int playerId, int raceIndex)
    {
        var player = PlayerManager.Instance.Get(playerId);
        if (player == null || raceIndex < 0 || raceIndex >= races.Length) return;
        var race = races[raceIndex];
        if (race == null || player.ownedRaces.Contains(race) || !player.TrySpendLumber(race.lumberCost)) return;

        player.ownedRaces.Add(race);
        ExecuteSetActive(playerId, raceIndex);
    }

    public void ExecuteSetActive(int playerId, int raceIndex)
    {
        var player = PlayerManager.Instance.Get(playerId);
        if (player == null || raceIndex < 0 || raceIndex >= races.Length) return;
        var race = races[raceIndex];
        if (race == null || !player.ownedRaces.Contains(race)) return;

        player.activeRace = race;
        if (playerId == PlayerManager.Instance.LocalPlayerId && TowerManager.Instance != null) TowerManager.Instance.CancelPlacement();
        player.NotifyChanged();
    }
}
