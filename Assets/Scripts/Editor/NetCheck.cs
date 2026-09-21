using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

/// <summary>
/// Tests the lockstep protocol over real TCP on this machine: a host and two clients, each running a fake simulation, must
/// execute identical commands on identical ticks; a wrong checksum must be caught; a dropped player must be handed over the
/// same way everywhere; and a silent player must stall the others. Logs "NET ..." lines.
///   Unity -batchmode -nographics -projectPath . -executeMethod NetCheck.Run -quit -logFile net.log
/// </summary>
public static class NetCheck
{
    /// <summary>One machine: a session plus a stand-in simulation that records what it executes.</summary>
    private class Machine
    {
        public LockstepSession session;
        public int tick;
        public readonly Dictionary<int, List<GameCommand>> scheduled = new Dictionary<int, List<GameCommand>>();
        public readonly List<string> executed = new List<string>();
        public bool desynced;
        public uint checksumSalt;        // makes this machine's checksum differ when non-zero
        public bool paused;              // does not advance (a frozen or lagging machine)
        public Action<int> beforeTick;   // hook to submit commands at a tick

        public void Attach(LockstepSession s)
        {
            session = s;
            s.BundleReady += (turn, commands) =>
            {
                int t = turn * LockstepSession.TurnTicks + 1;
                if (!scheduled.TryGetValue(t, out var list)) scheduled[t] = list = new List<GameCommand>();
                list.AddRange(commands);
            };
            s.Desynced += _ => desynced = true;
        }

        public void Frame(int maxTick)
        {
            session.Poll();
            if (paused) return;
            while (tick < maxTick && session.CanRunTick(tick + 1))
            {
                tick++;
                beforeTick?.Invoke(tick);
                if (scheduled.TryGetValue(tick, out var list))
                {
                    scheduled.Remove(tick);
                    list.Sort((a, b) => a.playerId != b.playerId ? a.playerId.CompareTo(b.playerId) : a.sequence.CompareTo(b.sequence));
                    foreach (var c in list) executed.Add($"t{tick} p{c.playerId} s{c.sequence} {c.GetType().Name}");
                }
                session.AfterTick(tick);
                if (tick % 30 == 0) session.ReportChecksum(tick, (uint)(tick * 7919) ^ checksumSalt);
            }
        }
    }

    private class Game
    {
        public Machine host, c1, c2;
        public NetListener listener;
        public List<Machine> All => new List<Machine> { host, c1, c2 };

        public static Game Start()
        {
            var g = new Game { host = new Machine(), c1 = new Machine(), c2 = new Machine() };
            g.listener = new NetListener(0);
            var clientPeers = new List<NetPeer>();
            var hostSide = new List<NetPeer>();
            for (int i = 0; i < 2; i++)
            {
                clientPeers.Add(NetPeer.Connect("127.0.0.1", g.listener.Port));
                NetPeer accepted;
                var wait = Stopwatch.StartNew();
                while (!g.listener.TryAccept(out accepted))
                {
                    if (wait.ElapsedMilliseconds > 3000) throw new Exception("accept timed out");
                    Thread.Sleep(2);
                }
                accepted.PlayerId = i + 1;
                hostSide.Add(accepted);
            }
            g.host.Attach(new LockstepSession(true, 0, 3, hostSide));
            g.c1.Attach(new LockstepSession(false, 1, 3, new List<NetPeer> { clientPeers[0] }));
            g.c2.Attach(new LockstepSession(false, 2, 3, new List<NetPeer> { clientPeers[1] }));
            foreach (var m in g.All) m.session.Begin();
            return g;
        }

        public void Run(int maxTick, int maxMs, Func<bool> until = null)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < maxMs)
            {
                foreach (var m in All) m.Frame(maxTick);
                if (until != null && until()) return;
                if (until == null && host.tick >= maxTick && c1.tick >= maxTick && c2.tick >= maxTick) return;
                Thread.Sleep(1);
            }
        }

        public void Stop()
        {
            listener.Stop();
            foreach (var m in All) m.session.Close();
        }
    }

    public static void Run()
    {
        int failures = 0;
        failures += Check("commands run identically on every machine", TestIdentical);
        failures += Check("wrong checksum is detected", TestDesync);
        failures += Check("dropped player is handed over identically", TestDrop);
        failures += Check("a silent player stalls the others", TestStall);
        Debug.Log(failures == 0 ? "NET all checks passed" : $"NET {failures} check(s) FAILED");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int Check(string name, Func<string> test)
    {
        string problem;
        try { problem = test(); }
        catch (Exception e) { problem = e.ToString(); }
        Debug.Log(problem == null ? $"NET ok: {name}" : $"NET FAIL: {name}: {problem}");
        return problem == null ? 0 : 1;
    }

    private static string Compare(Machine a, Machine b, string label)
    {
        if (a.executed.Count != b.executed.Count) return $"{label}: {a.executed.Count} vs {b.executed.Count} commands";
        for (int i = 0; i < a.executed.Count; i++)
            if (a.executed[i] != b.executed[i]) return $"{label}: command {i} differs: '{a.executed[i]}' vs '{b.executed[i]}'";
        return null;
    }

    private static void Inject(Machine m, int playerId, int atTick, int seqBase)
    {
        int n = 0;
        var previous = m.beforeTick;
        m.beforeTick = t =>
        {
            previous?.Invoke(t);
            if (t >= atTick && t < atTick + 30 && t % 3 == 0)
                m.session.Submit(new SellTowerCommand { playerId = playerId, sequence = seqBase + n++, towerInstanceId = t });
        };
    }

    private static string TestIdentical()
    {
        var g = Game.Start();
        try
        {
            Inject(g.host, 0, 10, 0);
            Inject(g.c1, 1, 12, 0);
            Inject(g.c2, 2, 12, 0); // same tick as client 1: ordering must be by player
            g.Run(600, 8000);
            if (g.host.tick < 600 || g.c1.tick < 600 || g.c2.tick < 600) return $"did not reach tick 600 ({g.host.tick}/{g.c1.tick}/{g.c2.tick})";
            if (g.host.executed.Count < 25) return $"only {g.host.executed.Count} commands executed";
            return Compare(g.host, g.c1, "host vs client 1") ?? Compare(g.host, g.c2, "host vs client 2")
                ?? (g.host.desynced || g.c1.desynced || g.c2.desynced ? "false desync alarm" : null);
        }
        finally { g.Stop(); }
    }

    private static string TestDesync()
    {
        var g = Game.Start();
        try
        {
            g.c2.checksumSalt = 0xBEEF; // client 2 disagrees from the first checksum on
            g.Run(200, 8000, () => g.host.desynced && g.c1.desynced && g.c2.desynced);
            if (!g.host.desynced) return "host did not notice";
            if (!g.c1.desynced || !g.c2.desynced) return "clients were not told";
            return null;
        }
        finally { g.Stop(); }
    }

    private static string TestDrop()
    {
        var g = Game.Start();
        try
        {
            Inject(g.host, 0, 10, 0);
            Inject(g.c1, 1, 10, 0);
            g.Run(120, 8000);
            g.c2.session.Close(); // player 3 drops out
            g.c2.paused = true;
            int target = 420;
            g.Run(target, 8000, () => g.host.tick >= target && g.c1.tick >= target);
            if (g.host.tick < target || g.c1.tick < target) return $"the game did not go on after the drop ({g.host.tick}/{g.c1.tick})";
            string diff = Compare(g.host, g.c1, "host vs client 1");
            if (diff != null) return diff;
            int left = g.host.executed.FindAll(e => e.Contains("PlayerLeftCommand")).Count;
            return left == 1 ? null : $"expected one PlayerLeft command, saw {left}";
        }
        finally { g.Stop(); }
    }

    private static string TestStall()
    {
        var g = Game.Start();
        try
        {
            g.c2.paused = true; // never sends batches past the first
            g.Run(600, 800);
            // turns 0 and 1 are free, turn 2's batch was sent at the start: nobody may get further than turn 2's end
            int limit = 3 * LockstepSession.TurnTicks;
            if (g.host.tick > limit || g.c1.tick > limit) return $"ran past the point where a batch is missing ({g.host.tick}/{g.c1.tick} > {limit})";
            if (g.host.tick < 2 * LockstepSession.TurnTicks) return $"stalled too early ({g.host.tick})";
            return null;
        }
        finally { g.Stop(); }
    }
}
