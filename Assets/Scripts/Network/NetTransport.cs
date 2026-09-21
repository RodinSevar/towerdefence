using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/// <summary>Message types of the LAN protocol. Every message on the wire is [int length][byte type][payload].</summary>
public static class Msg
{
    public const byte Hello = 1;        // client -> host: [string name]
    public const byte Welcome = 2;      // host -> client: [byte playerId]
    public const byte Lobby = 3;        // host -> clients: [byte count][string name]*count
    public const byte Start = 4;        // host -> clients: [byte playerCount]
    public const byte Batch = 5;        // client -> host: [int turn][commands]
    public const byte Bundle = 6;       // host -> clients: [int turn][commands]
    public const byte Checksum = 7;     // client -> host: [int tick][uint hash]
    public const byte Desync = 8;       // host -> clients: [int tick]
    public const byte Disconnected = 255; // local only: the peer's connection ended
}

public struct NetMessage
{
    public byte type;
    public byte[] payload;
    public NetPeer from;

    public BinaryReader Reader() => new BinaryReader(new MemoryStream(payload));
}

/// <summary>
/// One TCP connection. A background thread reads framed messages into an inbox that the main thread drains; sending happens on
/// the caller's thread (messages are small and this is a LAN).
/// </summary>
public class NetPeer
{
    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly Thread reader;
    private readonly ConcurrentQueue<NetMessage> inbox = new ConcurrentQueue<NetMessage>();
    private readonly object sendLock = new object();
    private volatile bool connected = true;

    public int PlayerId = -1;
    public string Name = "Player";
    public bool IsConnected => connected;

    public NetPeer(TcpClient tcp)
    {
        client = tcp;
        client.NoDelay = true;
        stream = client.GetStream();
        reader = new Thread(ReadLoop) { IsBackground = true, Name = "NetPeer reader" };
        reader.Start();
    }

    public static NetPeer Connect(string host, int port, int timeoutMs = 4000)
    {
        var tcp = new TcpClient();
        var result = tcp.BeginConnect(host, port, null, null);
        if (!result.AsyncWaitHandle.WaitOne(timeoutMs) || !tcp.Connected)
        {
            tcp.Close();
            throw new IOException($"Could not connect to {host}:{port}");
        }
        tcp.EndConnect(result);
        return new NetPeer(tcp);
    }

    private void ReadLoop()
    {
        try
        {
            var header = new byte[5];
            while (connected)
            {
                ReadExactly(header, 5);
                int length = BitConverter.ToInt32(header, 0);
                if (length < 1 || length > 1 << 22) throw new InvalidDataException("bad frame length");
                var payload = new byte[length - 1];
                ReadExactly(payload, payload.Length);
                inbox.Enqueue(new NetMessage { type = header[4], payload = payload, from = this });
            }
        }
        catch (Exception) { /* connection ended */ }
        finally
        {
            connected = false;
            inbox.Enqueue(new NetMessage { type = Msg.Disconnected, payload = new byte[0], from = this });
        }
    }

    private void ReadExactly(byte[] buffer, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n <= 0) throw new EndOfStreamException();
            read += n;
        }
    }

    public bool TryReceive(out NetMessage message) => inbox.TryDequeue(out message);

    public void Send(byte type, Action<BinaryWriter> body = null)
    {
        if (!connected) return;
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(0);         // length placeholder
        w.Write(type);
        body?.Invoke(w);
        w.Flush();
        byte[] data = ms.ToArray();
        Array.Copy(BitConverter.GetBytes(data.Length - 4), data, 4);
        try
        {
            lock (sendLock) stream.Write(data, 0, data.Length);
        }
        catch (Exception) { connected = false; }
    }

    public void Close()
    {
        connected = false;
        try { client.Close(); } catch (Exception) { }
    }
}

/// <summary>Accepts incoming connections on a background thread; the main thread collects them.</summary>
public class NetListener
{
    private readonly TcpListener listener;
    private readonly ConcurrentQueue<TcpClient> accepted = new ConcurrentQueue<TcpClient>();
    private volatile bool running = true;

    public int Port { get; }

    public NetListener(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        new Thread(AcceptLoop) { IsBackground = true, Name = "NetListener accept" }.Start();
    }

    private void AcceptLoop()
    {
        try
        {
            while (running) accepted.Enqueue(listener.AcceptTcpClient());
        }
        catch (Exception) { /* stopped */ }
    }

    public bool TryAccept(out NetPeer peer)
    {
        if (accepted.TryDequeue(out var tcp)) { peer = new NetPeer(tcp); return true; }
        peer = null;
        return false;
    }

    public void Stop()
    {
        running = false;
        try { listener.Stop(); } catch (Exception) { }
    }
}
