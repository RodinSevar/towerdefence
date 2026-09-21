using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Connects the game to the LAN: hosting or joining a lobby, starting the game everywhere at the same moment, and then feeding
/// the <see cref="LockstepSession"/>. Until the player chooses (offline, host or join) the simulation is held at tick 0.
/// The menu itself is <see cref="NetworkLobbyUI"/>.
/// </summary>
public class NetworkGame : Singleton<NetworkGame>
{
    public enum Phase { Menu, HostLobby, ClientLobby, Playing, Ended }

    public const int DefaultPort = 7777;

    public static LockstepSession Session { get; private set; }

    /// <summary>True while a network game is running (commands then go through the host).</summary>
    public static bool Active => Session != null && Instance != null && Instance.Current == Phase.Playing;

    public Phase Current { get; private set; } = Phase.Menu;
    public string Message { get; private set; } = "";
    public int DesyncTick { get; private set; } = -1;
    public IReadOnlyList<string> LobbyNames => lobbyNames;
    public bool IsHost { get; private set; }

    [Tooltip("Show the menu at start and hold the game until a mode is chosen. Turned off in batch mode (tests).")]
    [SerializeField] private bool showMenuAtStart = true;

    private NetListener listener;
    private NetPeer hostPeer;                                       // client: the connection to the host
    private readonly List<NetPeer> clients = new List<NetPeer>();   // host: everyone who has joined
    private readonly List<NetPeer> unnamed = new List<NetPeer>();   // host: connected, no Hello yet
    private readonly List<string> lobbyNames = new List<string>();
    private string playerName = "Player";

    private void Start()
    {
        if (showMenuAtStart && !Application.isBatchMode) Simulation.Running = false; // hold the game while the menu is up
        else Current = Phase.Playing; // offline; Active stays false because there is no session
    }

    // ------------------------------------------------------------------------------------------------ menu actions

    /// <summary>Starts a normal single-player game.</summary>
    public void PlayOffline()
    {
        Current = Phase.Playing;
        Simulation.Running = true;
    }

    public void HostGame(int port, string name)
    {
        try
        {
            playerName = name;
            listener = new NetListener(port);
            IsHost = true;
            Current = Phase.HostLobby;
            RefreshHostLobby();
            Message = "";
        }
        catch (Exception e) { Message = "Could not host: " + e.Message; }
    }

    public void JoinGame(string address, int port, string name)
    {
        try
        {
            playerName = name;
            hostPeer = NetPeer.Connect(address, port);
            hostPeer.Send(Msg.Hello, w => w.Write(name));
            IsHost = false;
            Current = Phase.ClientLobby;
            Message = "";
        }
        catch (Exception e) { Message = "Could not join: " + e.Message; }
    }

    /// <summary>Host: closes the lobby and starts the game for everyone.</summary>
    public void StartAsHost()
    {
        if (Current != Phase.HostLobby) return;
        int count = 1 + clients.Count;
        for (int i = 0; i < clients.Count; i++)
        {
            clients[i].PlayerId = i + 1;
            int id = i + 1;
            clients[i].Send(Msg.Start, w => { w.Write((byte)count); w.Write((byte)id); });
        }
        var names = new List<string> { playerName };
        foreach (var c in clients) names.Add(c.Name);

        listener.Stop();
        listener = null;
        BeginGame(count, 0, names, new List<NetPeer>(clients));
    }

    public void BackToMenu()
    {
        Shutdown();
        Current = Phase.Menu;
        Message = "";
    }

    // ------------------------------------------------------------------------------------------------ frame update

    private void Update()
    {
        switch (Current)
        {
            case Phase.HostLobby: UpdateHostLobby(); break;
            case Phase.ClientLobby: UpdateClientLobby(); break;
            case Phase.Playing: if (Session != null) Session.Poll(); break;
        }
    }

    private void UpdateHostLobby()
    {
        while (listener != null && listener.TryAccept(out var peer)) unnamed.Add(peer);

        foreach (var peer in unnamed.ToArray()) HandleLobbyMessages(peer);
        foreach (var peer in clients.ToArray()) HandleLobbyMessages(peer);
    }

    private void HandleLobbyMessages(NetPeer peer)
    {
        bool changed = false;
        while (peer.TryReceive(out var msg))
        {
            if (msg.type == Msg.Hello && unnamed.Remove(peer))
            {
                if (clients.Count >= PlayerManager.MaxPlayers - 1) { peer.Close(); continue; }
                peer.Name = msg.Reader().ReadString();
                clients.Add(peer);
                changed = true;
            }
            else if (msg.type == Msg.Disconnected)
            {
                if (clients.Remove(peer)) changed = true;
                unnamed.Remove(peer);
            }
        }
        if (changed) RefreshHostLobby();
    }

    private void RefreshHostLobby()
    {
        lobbyNames.Clear();
        lobbyNames.Add(playerName + " (host)");
        foreach (var c in clients) lobbyNames.Add(c.Name);
        foreach (var c in clients)
            c.Send(Msg.Lobby, w =>
            {
                w.Write((byte)lobbyNames.Count);
                foreach (var n in lobbyNames) w.Write(n);
            });
    }

    private void UpdateClientLobby()
    {
        if (hostPeer == null) return;
        while (hostPeer.TryReceive(out var msg))
        {
            switch (msg.type)
            {
                case Msg.Lobby:
                {
                    var r = msg.Reader();
                    int n = r.ReadByte();
                    lobbyNames.Clear();
                    for (int i = 0; i < n; i++) lobbyNames.Add(r.ReadString());
                    break;
                }
                case Msg.Start:
                {
                    var r = msg.Reader();
                    int count = r.ReadByte();
                    int id = r.ReadByte();
                    BeginGame(count, id, new List<string>(lobbyNames), new List<NetPeer> { hostPeer });
                    return;
                }
                case Msg.Disconnected:
                    EndGame("The host closed the lobby.");
                    return;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ the running game

    private void BeginGame(int count, int localId, List<string> names, List<NetPeer> peers)
    {
        PlayerManager.Instance.Configure(count, localId);
        for (int i = 0; i < names.Count && i < count; i++) PlayerManager.Instance.SetName(i, names[i].Replace(" (host)", ""));
        CommandQueue.Clear();

        Session = new LockstepSession(IsHost, localId, count, peers);
        Session.BundleReady += (turn, commands) =>
        {
            foreach (var c in commands) CommandQueue.Schedule(c, turn * LockstepSession.TurnTicks + 1);
        };
        Session.Desynced += tick =>
        {
            DesyncTick = tick;
            Message = $"Out of sync at tick {tick}. The game has been stopped.";
            Simulation.Running = false;
        };
        Session.Ended += EndGame;
        Simulation.OnChecksum += OnChecksum;

        Current = Phase.Playing;
        Simulation.Running = true;
        Session.Begin();
    }

    private void OnChecksum(int tick, uint hash)
    {
        if (Session != null) Session.ReportChecksum(tick, hash);
    }

    private void EndGame(string message)
    {
        Message = message;
        Simulation.Running = false;
        Current = Phase.Ended;
        Shutdown();
    }

    private void Shutdown()
    {
        Simulation.OnChecksum -= OnChecksum;
        if (Session != null) Session.Close();
        Session = null;
        if (hostPeer != null) hostPeer.Close();
        hostPeer = null;
        foreach (var c in clients) c.Close();
        clients.Clear();
        foreach (var c in unnamed) c.Close();
        unnamed.Clear();
        if (listener != null) listener.Stop();
        listener = null;
    }

    protected override void OnDestroy()
    {
        Shutdown();
        base.OnDestroy();
    }

    /// <summary>This machine's IPv4 addresses, for the host to tell friends.</summary>
    public static List<string> LocalAddresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork) list.Add(ip.ToString());
        }
        catch (Exception) { }
        return list;
    }
}
