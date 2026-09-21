using System.Collections.Generic;
using System.IO;
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

    // ---- wire format: [type][player][sequence][fields] ----
    protected abstract byte TypeId { get; }
    protected abstract void WriteFields(BinaryWriter w);
    protected abstract void ReadFields(BinaryReader r);

    public void Write(BinaryWriter w)
    {
        w.Write(TypeId);
        w.Write((byte)playerId);
        w.Write(sequence);
        WriteFields(w);
    }

    public static GameCommand Read(BinaryReader r)
    {
        byte type = r.ReadByte();
        GameCommand c;
        switch (type)
        {
            case 1: c = new PlaceTowerCommand(); break;
            case 2: c = new SellTowerCommand(); break;
            case 3: c = new UpgradeTowerCommand(); break;
            case 4: c = new UnlockRaceCommand(); break;
            case 5: c = new SetActiveRaceCommand(); break;
            case 6: c = new TransferGoldCommand(); break;
            case 7: c = new PlayerLeftCommand(); break;
            default: throw new InvalidDataException($"Unknown command type {type}");
        }
        c.playerId = r.ReadByte();
        c.sequence = r.ReadInt32();
        c.ReadFields(r);
        return c;
    }
}

public class PlaceTowerCommand : GameCommand
{
    public string towerId;        // TowerData.wc3Id
    public int originX, originY;  // bottom-left grid cell of the 4x4 footprint

    protected override byte TypeId => 1;
    protected override void WriteFields(BinaryWriter w) { w.Write(towerId); w.Write(originX); w.Write(originY); }
    protected override void ReadFields(BinaryReader r) { towerId = r.ReadString(); originX = r.ReadInt32(); originY = r.ReadInt32(); }

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

    protected override byte TypeId => 2;
    protected override void WriteFields(BinaryWriter w) => w.Write(towerInstanceId);
    protected override void ReadFields(BinaryReader r) => towerInstanceId = r.ReadInt32();

    public override void Execute() => TowerManager.Instance.ExecuteSell(playerId, towerInstanceId);
}

public class UpgradeTowerCommand : GameCommand
{
    public int towerInstanceId;

    protected override byte TypeId => 3;
    protected override void WriteFields(BinaryWriter w) => w.Write(towerInstanceId);
    protected override void ReadFields(BinaryReader r) => towerInstanceId = r.ReadInt32();

    public override void Execute() => TowerManager.Instance.ExecuteUpgrade(playerId, towerInstanceId);
}

public class UnlockRaceCommand : GameCommand
{
    public int raceIndex;

    protected override byte TypeId => 4;
    protected override void WriteFields(BinaryWriter w) => w.Write(raceIndex);
    protected override void ReadFields(BinaryReader r) => raceIndex = r.ReadInt32();

    public override void Execute() => RaceManager.Instance.ExecuteUnlock(playerId, raceIndex);
}

public class SetActiveRaceCommand : GameCommand
{
    public int raceIndex;

    protected override byte TypeId => 5;
    protected override void WriteFields(BinaryWriter w) => w.Write(raceIndex);
    protected override void ReadFields(BinaryReader r) => raceIndex = r.ReadInt32();

    public override void Execute() => RaceManager.Instance.ExecuteSetActive(playerId, raceIndex);
}

public class TransferGoldCommand : GameCommand
{
    public int toPlayerId, amount;

    protected override byte TypeId => 6;
    protected override void WriteFields(BinaryWriter w) { w.Write(toPlayerId); w.Write(amount); }
    protected override void ReadFields(BinaryReader r) { toPlayerId = r.ReadInt32(); amount = r.ReadInt32(); }

    public override void Execute() => PlayerManager.Instance.TransferGold(playerId, toPlayerId, amount);
}

/// <summary>
/// A player dropped out of a network game: everything they own (gold, lumber, towers) goes to one remaining player, chosen by
/// the host. Issued by the host and run by every machine on the same tick, like any other command (playerId is the leaver).
/// </summary>
public class PlayerLeftCommand : GameCommand
{
    public int heirId;

    protected override byte TypeId => 7;
    protected override void WriteFields(BinaryWriter w) => w.Write(heirId);
    protected override void ReadFields(BinaryReader r) => heirId = r.ReadInt32();

    public override void Execute() => PlayerManager.Instance.HandleLeave(playerId, heirId);
}

/// <summary>
/// Schedules commands for a simulation tick and runs the ones that are due. Offline, a command runs a couple of ticks after it
/// is issued. In a network game a command is sent to the host, comes back in a turn bundle, and every machine schedules the
/// bundle's commands for the start of that turn. Within a tick commands run ordered by (player, sequence), which is the same
/// on every machine.
/// </summary>
public static class CommandQueue
{
    /// <summary>Ticks between issuing a command and running it when playing offline.</summary>
    public static int InputDelayTicks = 2;

    private static readonly SortedDictionary<int, List<GameCommand>> pending = new SortedDictionary<int, List<GameCommand>>();
    private static int nextSequence;

    /// <summary>Issues a command from this machine's player.</summary>
    public static void Submit(GameCommand command)
    {
        if (NetworkGame.Active)
        {
            command.sequence = nextSequence++;
            NetworkGame.Session.Submit(command);
            return;
        }
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
        foreach (var c in list)
        {
            // a player who has left no longer acts (the notice that they left is the exception)
            if (!(c is PlayerLeftCommand) && PlayerManager.Instance != null && !PlayerManager.Instance.IsActive(c.playerId)) continue;
            c.Execute();
        }
    }

    public static void Clear()
    {
        pending.Clear();
        nextSequence = 0;
    }
}
