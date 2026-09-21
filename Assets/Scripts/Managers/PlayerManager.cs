using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One player's own state: gold and lumber (gold can be traded), which tower races they own and which one is active.
/// Lives are shared by the whole team and stay in <see cref="GameManager"/>.
/// </summary>
public class PlayerState
{
    public readonly int id;
    public string name;
    public Color color;
    public int gold;
    public int lumber;
    public readonly HashSet<RaceData> ownedRaces = new HashSet<RaceData>();
    public RaceData activeRace;
    public bool active = true; // false once the player has left a network game

    /// <summary>Raised after any change to this player's gold, lumber or races.</summary>
    public event Action<PlayerState> Changed;

    public PlayerState(int id, string name, Color color)
    {
        this.id = id;
        this.name = name;
        this.color = color;
    }

    public void AddGold(int amount) { gold += amount; Changed?.Invoke(this); }
    public void AddLumber(int amount) { lumber += amount; Changed?.Invoke(this); }

    public bool TrySpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        Changed?.Invoke(this);
        return true;
    }

    public bool TrySpendLumber(int amount)
    {
        if (lumber < amount) return false;
        lumber -= amount;
        Changed?.Invoke(this);
        return true;
    }

    public void NotifyChanged() => Changed?.Invoke(this);
}

/// <summary>
/// The players in the game. Offline there is one (the local player); in a network game there is one per participant, and
/// each machine knows which of them it controls (<see cref="LocalPlayerId"/>). Everything that changes a player's state goes
/// through <see cref="GameCommand"/>s so every machine applies the same changes on the same tick.
/// </summary>
public class PlayerManager : Singleton<PlayerManager>
{
    public const int MaxPlayers = 9; // the original map has 9 player slots

    [Tooltip("Players in this game (1 offline)")]
    [SerializeField, Range(1, MaxPlayers)] private int playerCount = 1;

    private readonly List<PlayerState> players = new List<PlayerState>();
    private bool initialized;
    private bool[] slotOccupied; // null = every slot has a player

    public int LocalPlayerId { get; private set; }
    public IReadOnlyList<PlayerState> Players { get { EnsureInitialized(); return players; } }
    public PlayerState Local => Get(LocalPlayerId);

    /// <summary>Raised when the local player's gold, lumber or races change.</summary>
    public event Action<PlayerState> OnLocalPlayerChanged;

    /// <summary>Sets how many players there are and which one this machine controls (before the game starts).</summary>
    public void Configure(int count, int localId, bool[] occupied = null)
    {
        playerCount = Mathf.Clamp(count, 1, MaxPlayers);
        LocalPlayerId = Mathf.Clamp(localId, 0, playerCount - 1);
        slotOccupied = occupied;
        players.Clear();
        initialized = false;
    }

    public PlayerState Get(int id)
    {
        EnsureInitialized();
        return id >= 0 && id < players.Count ? players[id] : null;
    }

    /// <summary>Creates the players with the starting resources and free races (needs the GameManager and RaceManager settings).</summary>
    private void EnsureInitialized()
    {
        if (initialized) return;
        if (GameManager.Instance == null || RaceManager.Instance == null) return;
        initialized = true;

        for (int i = 0; i < playerCount; i++)
        {
            var p = new PlayerState(i, $"Player {i + 1}", PlayerSlots.Colors[i % PlayerSlots.Count])
            {
                gold = GameManager.Instance.initialGold,
                lumber = GameManager.Instance.initialLumber,
            };
            foreach (var race in RaceManager.Instance.Races)
                if (race != null && race.lumberCost <= 0) p.ownedRaces.Add(race);
            foreach (var race in RaceManager.Instance.Races)
                if (race != null && p.ownedRaces.Contains(race)) { p.activeRace = race; break; }

            if (slotOccupied != null && i < slotOccupied.Length && !slotOccupied[i]) p.active = false; // empty slot
            p.Changed += OnPlayerChanged;
            players.Add(p);
        }
    }

    private void OnPlayerChanged(PlayerState p)
    {
        if (p.id == LocalPlayerId) OnLocalPlayerChanged?.Invoke(p);
    }

    /// <summary>Gives every player the same amount (level bonus, lumber bonus).</summary>
    public void AddGoldToAll(int amount) { foreach (var p in Players) if (p.active) p.AddGold(amount); }
    public void AddLumberToAll(int amount) { foreach (var p in Players) if (p.active) p.AddLumber(amount); }

    public bool IsActive(int id) { var p = Get(id); return p != null && p.active; }

    /// <summary>
    /// A player dropped out of a network game: their gold, lumber and towers go to <paramref name="heirId"/>, and they take no
    /// further part (no bonuses, their commands are ignored).
    /// </summary>
    public void HandleLeave(int leaverId, int heirId)
    {
        var leaver = Get(leaverId);
        var heir = Get(heirId);
        if (leaver == null || heir == null || !leaver.active || leaverId == heirId) return;

        heir.AddGold(leaver.gold);
        heir.AddLumber(leaver.lumber);
        leaver.gold = 0;
        leaver.lumber = 0;
        leaver.active = false;
        leaver.NotifyChanged();
        if (TowerManager.Instance != null) TowerManager.Instance.TransferTowers(leaverId, heirId);
        Debug.Log($"{leaver.name} left; everything goes to {heir.name}.");
    }

    public void SetName(int id, string playerName) { var p = Get(id); if (p != null) p.name = playerName; }

    /// <summary>Moves gold between players. Returns false if the sender cannot afford it.</summary>
    public bool TransferGold(int fromId, int toId, int amount)
    {
        var from = Get(fromId);
        var to = Get(toId);
        if (from == null || to == null || !from.active || !to.active || fromId == toId || amount <= 0) return false;
        if (!from.TrySpendGold(amount)) return false;
        to.AddGold(amount);
        return true;
    }
}
