using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Lockstep for a running network game, independent of Unity so it can be tested on its own.
///
/// The game advances in turns of <see cref="TurnTicks"/> ticks. Each machine sends the host its commands for turn N + 2 when it
/// enters turn N (an empty batch if it has none). The host waits until every active player's batch for a turn has arrived,
/// then broadcasts one bundle with all of them; every machine (host included) schedules the bundle's commands for the first
/// tick of that turn. A machine may run a tick only once the bundle for its turn is in, so all machines execute identical
/// commands on identical ticks. Turns 0 and 1 need no bundle (they are empty by definition), which gives the pipeline its start.
///
/// The host also compares state checksums reported by every machine, and turns a dropped connection into a
/// <see cref="PlayerLeftCommand"/> that goes out in the next bundle.
/// </summary>
public class LockstepSession
{
    public const int TurnTicks = 6; // 0.1 s at 60 ticks/s

    public readonly bool IsHost;
    public readonly int LocalId;
    public readonly int PlayerCount;

    private readonly List<NetPeer> peers; // host: every client; client: just the host
    private readonly bool[] active;
    private readonly List<GameCommand> outbox = new List<GameCommand>();

    private int highestBundle = 1;   // turns 0 and 1 are empty and need no bundle
    private int nextBatchTurn = 2;   // the next turn this machine still owes a batch for
    private int nextBundleTurn = 2;  // host: the next turn to bundle
    private bool desynced;

    // host only
    private readonly Dictionary<int, Dictionary<int, List<GameCommand>>> batches = new Dictionary<int, Dictionary<int, List<GameCommand>>>();
    private readonly List<GameCommand> hostCommands = new List<GameCommand>();
    private readonly Dictionary<int, Dictionary<int, uint>> checksums = new Dictionary<int, Dictionary<int, uint>>();

    /// <summary>A turn's commands are final: schedule them for the first tick of that turn.</summary>
    public event Action<int, List<GameCommand>> BundleReady;
    /// <summary>The machines' state checksums differ on this tick.</summary>
    public event Action<int> Desynced;
    /// <summary>The game cannot go on (for example the host went away).</summary>
    public event Action<string> Ended;

    public LockstepSession(bool isHost, int localId, int playerCount, List<NetPeer> peers, bool[] activeMask = null)
    {
        IsHost = isHost;
        LocalId = localId;
        PlayerCount = playerCount;
        this.peers = peers;
        active = new bool[playerCount];
        for (int i = 0; i < playerCount; i++) active[i] = activeMask == null || (i < activeMask.Length && activeMask[i]);
    }

    public bool IsPlayerActive(int id) => id >= 0 && id < active.Length && active[id];

    /// <summary>Sends the first batch. Call once when the game starts.</summary>
    public void Begin()
    {
        SendBatch(nextBatchTurn++);
    }

    /// <summary>True if the bundle for the turn containing <paramref name="tick"/> (1-based) has arrived.</summary>
    public bool CanRunTick(int tick) => (tick - 1) / TurnTicks <= highestBundle;

    /// <summary>Queues a command from this machine's player for the next batch.</summary>
    public void Submit(GameCommand command)
    {
        outbox.Add(command);
    }

    /// <summary>Call after each simulation tick has run.</summary>
    public void AfterTick(int tick)
    {
        if (tick % TurnTicks != 0) return;
        int enteringTurn = tick / TurnTicks;
        while (nextBatchTurn <= enteringTurn + 2) SendBatch(nextBatchTurn++);
    }

    /// <summary>Reports this machine's state checksum for a tick.</summary>
    public void ReportChecksum(int tick, uint hash)
    {
        if (IsHost) { RecordChecksum(tick, LocalId, hash); return; }
        foreach (var p in peers) p.Send(Msg.Checksum, w => { w.Write(tick); w.Write(hash); });
    }

    /// <summary>Processes incoming messages. Call every frame.</summary>
    public void Poll()
    {
        foreach (var peer in peers.ToArray())
        {
            while (peer.TryReceive(out var msg))
            {
                if (IsHost) HandleAsHost(peer, msg); else HandleAsClient(msg);
            }
        }
        if (IsHost) TryBuildBundles();
    }

    public void Close()
    {
        foreach (var p in peers) p.Close();
    }

    // ------------------------------------------------------------------------------------------------ sending

    private void SendBatch(int turn)
    {
        var commands = new List<GameCommand>(outbox);
        outbox.Clear();

        if (IsHost)
        {
            StoreBatch(turn, LocalId, commands);
            return;
        }
        foreach (var p in peers)
            p.Send(Msg.Batch, w =>
            {
                w.Write(turn);
                w.Write(commands.Count);
                foreach (var c in commands) c.Write(w);
            });
    }

    // ------------------------------------------------------------------------------------------------ host

    private void HandleAsHost(NetPeer peer, NetMessage msg)
    {
        switch (msg.type)
        {
            case Msg.Batch:
            {
                var r = msg.Reader();
                int turn = r.ReadInt32();
                int count = r.ReadInt32();
                var list = new List<GameCommand>(count);
                for (int i = 0; i < count; i++)
                {
                    var c = GameCommand.Read(r);
                    c.playerId = peer.PlayerId; // a client can only act as itself
                    list.Add(c);
                }
                if (IsPlayerActive(peer.PlayerId)) StoreBatch(turn, peer.PlayerId, list);
                break;
            }
            case Msg.Checksum:
            {
                var r = msg.Reader();
                int tick = r.ReadInt32();
                uint hash = r.ReadUInt32();
                RecordChecksum(tick, peer.PlayerId, hash);
                break;
            }
            case Msg.Disconnected:
                OnPeerLost(peer);
                break;
        }
    }

    private void StoreBatch(int turn, int playerId, List<GameCommand> commands)
    {
        if (turn < nextBundleTurn) return; // that turn was already bundled without it
        if (!batches.TryGetValue(turn, out var perPlayer)) batches[turn] = perPlayer = new Dictionary<int, List<GameCommand>>();
        perPlayer[playerId] = commands;
    }

    private void TryBuildBundles()
    {
        while (batches.TryGetValue(nextBundleTurn, out var perPlayer) && AllActiveHaveBatch(perPlayer))
        {
            var commands = new List<GameCommand>();
            for (int id = 0; id < PlayerCount; id++)
                if (perPlayer.TryGetValue(id, out var list)) commands.AddRange(list);
            commands.AddRange(hostCommands); // drop notices ride along with the next bundle
            hostCommands.Clear();

            int turn = nextBundleTurn++;
            batches.Remove(turn);
            highestBundle = turn;

            foreach (var p in peers)
                if (p.IsConnected)
                    p.Send(Msg.Bundle, w =>
                    {
                        w.Write(turn);
                        w.Write(commands.Count);
                        foreach (var c in commands) c.Write(w);
                    });
            BundleReady?.Invoke(turn, commands);
        }
    }

    private bool AllActiveHaveBatch(Dictionary<int, List<GameCommand>> perPlayer)
    {
        for (int id = 0; id < PlayerCount; id++)
            if (active[id] && !perPlayer.ContainsKey(id)) return false;
        return true;
    }

    private void OnPeerLost(NetPeer peer)
    {
        int id = peer.PlayerId;
        if (!IsPlayerActive(id)) return;
        active[id] = false;

        int heir = -1;
        for (int i = 0; i < PlayerCount && heir < 0; i++)
            if (active[i]) heir = i;
        if (heir >= 0) hostCommands.Add(new PlayerLeftCommand { playerId = id, heirId = heir, sequence = int.MaxValue });

        VerifyPendingChecksums();
    }

    private void RecordChecksum(int tick, int playerId, uint hash)
    {
        if (desynced) return;
        if (!checksums.TryGetValue(tick, out var perPlayer)) checksums[tick] = perPlayer = new Dictionary<int, uint>();
        perPlayer[playerId] = hash;
        Verify(tick);
    }

    private void VerifyPendingChecksums()
    {
        foreach (int tick in new List<int>(checksums.Keys)) Verify(tick);
    }

    private void Verify(int tick)
    {
        if (desynced || !checksums.TryGetValue(tick, out var perPlayer)) return;

        uint? reference = null;
        for (int id = 0; id < PlayerCount; id++)
        {
            if (!active[id]) continue;
            if (!perPlayer.TryGetValue(id, out uint h)) return; // still waiting for someone
            if (reference == null) reference = h;
            else if (reference.Value != h)
            {
                desynced = true;
                foreach (var p in peers) p.Send(Msg.Desync, w => w.Write(tick));
                Desynced?.Invoke(tick);
                return;
            }
        }
        checksums.Remove(tick);
    }

    // ------------------------------------------------------------------------------------------------ client

    private void HandleAsClient(NetMessage msg)
    {
        switch (msg.type)
        {
            case Msg.Bundle:
            {
                var r = msg.Reader();
                int turn = r.ReadInt32();
                int count = r.ReadInt32();
                var list = new List<GameCommand>(count);
                for (int i = 0; i < count; i++) list.Add(GameCommand.Read(r));
                highestBundle = Math.Max(highestBundle, turn);
                BundleReady?.Invoke(turn, list);
                break;
            }
            case Msg.Desync:
                desynced = true;
                Desynced?.Invoke(msg.Reader().ReadInt32());
                break;
            case Msg.Disconnected:
                Ended?.Invoke("Lost the connection to the host.");
                break;
        }
    }
}
