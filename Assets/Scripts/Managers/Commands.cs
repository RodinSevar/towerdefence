using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player action that changes the game: placing or selling a tower, choosing a race, trading gold. Commands contain only
/// data (ids and numbers, no object references), so a network game can send them to every machine; each machine runs the
/// command on the same simulation tick, in the same order. Input code never changes game state directly.
/// </summary>
public abstract class GameCommand
{
    public int playerId;
    public int sequence; // per-player counter, orders a player's commands within one tick
    public abstract void Execute();
}

public class PlaceTowerCommand : GameCommand
{
    public string towerId;        // TowerData.wc3Id
    public int originX, originY;  // bottom-left grid cell of the 4x4 footprint

    public override void Execute()
    {
        var tower = RaceManager.Instance.FindTower(towerId);
        if (tower == null) return;
        TowerManager.Instance.ExecutePlace(playerId, tower, new Vector2Int(originX, originY), out _);
    }
}

public class SellTowerCommand : GameCommand
{
    public int towerInstanceId;
    public override void Execute() => TowerManager.Instance.ExecuteSell(playerId, towerInstanceId);
}

public class UpgradeTowerCommand : GameCommand
{
    public int towerInstanceId;
    public override void Execute() => TowerManager.Instance.ExecuteUpgrade(playerId, towerInstanceId);
}

public class UnlockRaceCommand : GameCommand
{
    public int raceIndex;
    public override void Execute() => RaceManager.Instance.ExecuteUnlock(playerId, raceIndex);
}

public class SetActiveRaceCommand : GameCommand
{
    public int raceIndex;
    public override void Execute() => RaceManager.Instance.ExecuteSetActive(playerId, raceIndex);
}

public class TransferGoldCommand : GameCommand
{
    public int toPlayerId, amount;
    public override void Execute() => PlayerManager.Instance.TransferGold(playerId, toPlayerId, amount);
}

/// <summary>
/// Schedules commands for a simulation tick and runs the ones that are due. Offline, a command runs a couple of ticks after it
/// is issued; a network game will schedule commands further ahead so every machine has them in time. Within a tick commands
/// run ordered by (player, sequence), which is the same on every machine.
/// </summary>
public static class CommandQueue
{
    /// <summary>Ticks between issuing a command and running it. Must cover the network delay in a multiplayer game.</summary>
    public static int InputDelayTicks = 2;

    private static readonly SortedDictionary<int, List<GameCommand>> pending = new SortedDictionary<int, List<GameCommand>>();
    private static int nextSequence;

    /// <summary>Issues a command from this machine's player, to run <see cref="InputDelayTicks"/> from now.</summary>
    public static void Submit(GameCommand command)
    {
        command.sequence = nextSequence++;
        Schedule(command, Simulation.CurrentTick + InputDelayTicks);
    }

    /// <summary>Adds a command (local or received from another machine) to run on <paramref name="tick"/>.</summary>
    public static void Schedule(GameCommand command, int tick)
    {
        if (!pending.TryGetValue(tick, out var list)) pending[tick] = list = new List<GameCommand>();
        list.Add(command);
    }

    /// <summary>Runs every command scheduled for <paramref name="tick"/> (called by <see cref="Simulation"/> at the start of a tick).</summary>
    public static void ExecuteDue(int tick)
    {
        if (pending.Count == 0) return;
        if (!pending.TryGetValue(tick, out var list)) return;
        pending.Remove(tick);

        list.Sort((a, b) => a.playerId != b.playerId ? a.playerId.CompareTo(b.playerId) : a.sequence.CompareTo(b.sequence));
        foreach (var c in list) c.Execute();
    }

    public static void Clear()
    {
        pending.Clear();
        nextSequence = 0;
    }
}
