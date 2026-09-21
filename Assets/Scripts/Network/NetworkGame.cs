using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// The game's front door: the main menu, the setup lobby (choose your slot and colour, or host / join a LAN game), starting the
/// game, and then feeding the <see cref="LockstepSession"/> during a network game. Until the player starts a game the simulation
/// is held at tick 0 and the world is not interactable (see <see cref="GameInput"/>). The screens are drawn by <see cref="LobbyUI"/>.
/// </summary>
public class NetworkGame : Singleton<NetworkGame>
{
    public enum Phase { Menu, OfflineLobby, HostLobby, ClientLobby, Playing, Ended }

    public const int DefaultPort = 7777;

    public static LockstepSession Session { get; private set; }

    /// <summary>True while a network game is running (commands then go through the host).</summary>
    public static bool Active => Session != null && Instance != null && Instance.Current == Phase.Playing;

    public Phase Current { get; private set; } = Phase.Menu;
    public string Message { get; private set; } = "";
    public int DesyncTick { get; private set; } = -1;
    public bool IsHost { get; private set; }

    /// <summary>True while a menu or lobby is showing: the game world must not react to input.</summary>
    public bool MenuOpen => Current != Phase.Playing;

    public bool InLobby => Current == Phase.OfflineLobby || Current == Phase.HostLobby || Current == Phase.ClientLobby;

    /// <summary>The slot this player has picked (0-8).</summary>
    public int MySlot { get; private set; }

    /// <summary>Who holds a slot, or null if it is open.</summary>
    public string SlotHolder(int slot) => slotNames[slot];

    /// <summary>The lobby can be started by the host, or by the player in an offline game.</summary>
    public bool CanStart => Current == Phase.OfflineLobby || Current == Phase.HostLobby;

    [Tooltip("Show the menu at start and hold the game until a mode is chosen. Turned off in batch mode (tests).")]
    [SerializeField] private bool showMenuAtStart = true;

    private NetListener listener;
    private NetPeer hostPeer;                                       // client: the connection to the host
    private readonly NetPeer[] slotPeers = new NetPeer[PlayerSlots.Count];  // host: who sits in each slot (null = host / empty)
    private readonly List<NetPeer> unnamed = new List<NetPeer>();   // host: connected, no Hello yet
    private readonly string[] slotNames = new string[PlayerSlots.Count];
    private string playerName = "Player";

    private void Start()
    {
        if (showMenuAtStart && !Application.isBatchMode) Simulation.Running = false; // hold the game while the menu is up
        else Current = Phase.Playing; // no menu (tests): offline, Active stays false because there is no session
    }

    // ------------------------------------------------------------------------------------------------ menu actions

    /// <summary>Opens the setup screen for a single-player game.</summary>
    public void PlayOffline(string name)
    {
        playerName = name;
        ClearSlots();
        MySlot = 0;
        slotNames[MySlot] = playerName;
        IsHost = false;
        Current = Phase.OfflineLobby;
        Message = "";
    }

    public void HostGame(int port, string name)
    {
        try
        {
            playerName = name;
            listener = new NetListener(port);
            IsHost = true;
            ClearSlots();
            MySlot = 0;
            slotNames[MySlot] = playerName;
            Current = Phase.HostLobby;
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
            ClearSlots();
            Current = Phase.ClientLobby;
            Message = "";
        }
        catch (Exception e) { Message = "Could not join: " + e.Message; }
    }

    /// <summary>Takes a slot (and its colour and start position), if it is free.</summary>
    public void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= PlayerSlots.Count || slotNames[slot] != null) return;

        switch (Current)
        {
            case Phase.OfflineLobby:
            case Phase.HostLobby:
                slotNames[MySlot] = null;
                MySlot = slot;
                slotNames[MySlot] = playerName;
                if (Current == Phase.HostLobby) BroadcastLobby();
                break;
            case Phase.ClientLobby:
                hostPeer?.Send(Msg.SlotRequest, w => w.Write((byte)slot)); // the host answers with the new lobby
                break;
        }
    }

    /// <summary>Starts the game (host or offline player).</summary>
    public void StartGame()
    {
        if (!CanStart) return;

        var occupied = new bool[PlayerSlots.Count];
        var names = new string[PlayerSlots.Count];
        for (int i = 0; i < PlayerSlots.Count; i++) { occupied[i] = slotNames[i] != null; names[i] = slotNames[i] ?? ""; }

        if (Current == Phase.OfflineLobby)
        {
            BeginGame(MySlot, occupied, names, new List<NetPeer>());
            return;
        }

        var peers = new List<NetPeer>();
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            var peer = slotPeers[i];
            if (peer == null) continue;
            peer.PlayerId = i;
            int slot = i;
            peer.Send(Msg.Start, w => WriteLobby(w, slot));
            peers.Add(peer);
        }
        foreach (var p in unnamed) p.Close(); // anyone who never said hello
        unnamed.Clear();
        listener.Stop();
        listener = null;
        BeginGame(MySlot, occupied, names, peers);
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

        bool changed = false;
        foreach (var peer in unnamed.ToArray()) changed |= HandleLobbyMessages(peer);
        foreach (var peer in slotPeers)
            if (peer != null) changed |= HandleLobbyMessages(peer);
        if (changed) BroadcastLobby();
    }

    /// <summary>Host: reads what one lobby member sent. Returns true if the lobby changed.</summary>
    private bool HandleLobbyMessages(NetPeer peer)
    {
        bool changed = false;
        while (peer.TryReceive(out var msg))
        {
            if (msg.type == Msg.Hello && unnamed.Remove(peer))
            {
                int free = FirstFreeSlot();
                if (free < 0) { peer.Close(); continue; }
                peer.Name = msg.Reader().ReadString();
                slotPeers[free] = peer;
                slotNames[free] = peer.Name;
                changed = true;
            }
            else if (msg.type == Msg.SlotRequest)
            {
                int slot = msg.Reader().ReadByte();
                int current = Array.IndexOf(slotPeers, peer);
                if (current >= 0 && slot >= 0 && slot < PlayerSlots.Count && slotNames[slot] == null)
                {
                    slotPeers[current] = null;
                    slotNames[current] = null;
                    slotPeers[slot] = peer;
                    slotNames[slot] = peer.Name;
                }
                changed = true; // also tells a refused client the current state
            }
            else if (msg.type == Msg.Disconnected)
            {
                int slot = Array.IndexOf(slotPeers, peer);
                if (slot >= 0) { slotPeers[slot] = null; slotNames[slot] = null; changed = true; }
                unnamed.Remove(peer);
            }
        }
        return changed;
    }

    private int FirstFreeSlot()
    {
        for (int i = 0; i < PlayerSlots.Count; i++)
            if (slotNames[i] == null) return i;
        return -1;
    }

    private void BroadcastLobby()
    {
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            var peer = slotPeers[i];
            if (peer == null) continue;
            int slot = i;
            peer.Send(Msg.Lobby, w => WriteLobby(w, slot));
        }
    }

    private void WriteLobby(System.IO.BinaryWriter w, int yourSlot)
    {
        w.Write((byte)yourSlot);
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            w.Write(slotNames[i] != null);
            w.Write(slotNames[i] ?? "");
        }
    }

    private void ReadLobby(System.IO.BinaryReader r, out int yourSlot)
    {
        yourSlot = r.ReadByte();
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            bool taken = r.ReadBoolean();
            string name = r.ReadString();
            slotNames[i] = taken ? name : null;
        }
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
                    ReadLobby(msg.Reader(), out int slot);
                    MySlot = slot;
                    break;
                }
                case Msg.Start:
                {
                    ReadLobby(msg.Reader(), out int slot);
                    MySlot = slot;
                    var occupied = new bool[PlayerSlots.Count];
                    var names = new string[PlayerSlots.Count];
                    for (int i = 0; i < PlayerSlots.Count; i++) { occupied[i] = slotNames[i] != null; names[i] = slotNames[i] ?? ""; }
                    BeginGame(slot, occupied, names, new List<NetPeer> { hostPeer });
                    return;
                }
                case Msg.Disconnected:
                    EndGame("The host closed the lobby.");
                    return;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ the running game

    private void BeginGame(int localSlot, bool[] occupied, string[] names, List<NetPeer> peers)
    {
        MySlot = localSlot;
        PlayerManager.Instance.Configure(PlayerSlots.Count, localSlot, occupied);
        for (int i = 0; i < PlayerSlots.Count; i++)
            if (occupied[i]) PlayerManager.Instance.SetName(i, names[i]);
        CommandQueue.Clear();

        if (peers.Count > 0 || IsHost)
        {
            Session = new LockstepSession(IsHost, localSlot, PlayerSlots.Count, peers, occupied);
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
        }

        Current = Phase.Playing;
        Simulation.Running = true;
        Session?.Begin();

        var camera = FindAnyObjectByType<CameraController>();
        if (camera != null) camera.FocusOn(PlayerSlots.StartPosition(localSlot));
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

    private void ClearSlots()
    {
        for (int i = 0; i < PlayerSlots.Count; i++) { slotNames[i] = null; slotPeers[i] = null; }
    }

    private void Shutdown()
    {
        Simulation.OnChecksum -= OnChecksum;
        if (Session != null) Session.Close();
        Session = null;
        if (hostPeer != null) hostPeer.Close();
        hostPeer = null;
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            slotPeers[i]?.Close();
            slotPeers[i] = null;
        }
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

    /// <summary>Closes the game (or stops play mode in the editor).</summary>
    public static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
