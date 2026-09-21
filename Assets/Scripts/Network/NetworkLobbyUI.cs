using UnityEngine;

/// <summary>
/// A plain immediate-mode menu for starting offline, hosting or joining a LAN game, plus a status line during a network game.
/// Scales with the screen height like the rest of the UI.
/// </summary>
public class NetworkLobbyUI : MonoBehaviour
{
    private string address = "192.168.0.10";
    private string port = NetworkGame.DefaultPort.ToString();
    private string playerName = "Player";
    private GUIStyle title, label, button, field, warning;
    private float styleScale;

    private void OnGUI()
    {
        var net = NetworkGame.Instance;
        if (net == null) return;

        float scale = Mathf.Max(0.6f, Screen.height / 900f);
        EnsureStyles(scale);

        switch (net.Current)
        {
            case NetworkGame.Phase.Playing: DrawStatus(net, scale); break;
            case NetworkGame.Phase.Menu: DrawMenu(net, scale); break;
            case NetworkGame.Phase.HostLobby: DrawHostLobby(net, scale); break;
            case NetworkGame.Phase.ClientLobby: DrawClientLobby(net, scale); break;
            case NetworkGame.Phase.Ended: DrawEnded(net, scale); break;
        }
    }

    private void EnsureStyles(float scale)
    {
        if (title != null && Mathf.Approximately(scale, styleScale)) return;
        styleScale = scale;
        title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * scale) };
        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * scale) };
        field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(18 * scale) };
        warning = new GUIStyle(label) { normal = { textColor = new Color(1f, 0.45f, 0.35f) } };
    }

    private Rect Panel(float scale, float width, float height)
    {
        var r = new Rect((Screen.width - width * scale) / 2, (Screen.height - height * scale) / 2, width * scale, height * scale);
        GUI.Box(r, GUIContent.none);
        return r;
    }

    private void DrawMenu(NetworkGame net, float scale)
    {
        var r = Panel(scale, 460, 400);
        GUILayout.BeginArea(new Rect(r.x + 20 * scale, r.y + 12 * scale, r.width - 40 * scale, r.height - 24 * scale));
        GUILayout.Label("Wintermaul TD", title);
        GUILayout.Space(8 * scale);

        if (GUILayout.Button("Play offline", button, GUILayout.Height(42 * scale))) net.PlayOffline();
        GUILayout.Space(14 * scale);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Your name", label, GUILayout.Width(110 * scale));
        playerName = GUILayout.TextField(playerName, 16, field);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port", label, GUILayout.Width(110 * scale));
        port = GUILayout.TextField(port, 5, field);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Host a LAN game", button, GUILayout.Height(38 * scale)) && int.TryParse(port, out int hostPort))
            net.HostGame(hostPort, playerName);

        GUILayout.Space(10 * scale);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Host IP", label, GUILayout.Width(110 * scale));
        address = GUILayout.TextField(address, 40, field);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Join", button, GUILayout.Height(38 * scale)) && int.TryParse(port, out int joinPort))
            net.JoinGame(address.Trim(), joinPort, playerName);

        if (!string.IsNullOrEmpty(net.Message)) GUILayout.Label(net.Message, warning);
        GUILayout.EndArea();
    }

    private void DrawHostLobby(NetworkGame net, float scale)
    {
        var r = Panel(scale, 460, 420);
        GUILayout.BeginArea(new Rect(r.x + 20 * scale, r.y + 12 * scale, r.width - 40 * scale, r.height - 24 * scale));
        GUILayout.Label("Hosting", title);
        GUILayout.Label("Tell the others to join: " + string.Join("  or  ", NetworkGame.LocalAddresses()) + "  port " + port, label);
        GUILayout.Space(8 * scale);
        GUILayout.Label("Players:", label);
        foreach (var n in net.LobbyNames) GUILayout.Label("  " + n, label);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Start game", button, GUILayout.Height(42 * scale))) net.StartAsHost();
        if (GUILayout.Button("Cancel", button, GUILayout.Height(32 * scale))) net.BackToMenu();
        GUILayout.EndArea();
    }

    private void DrawClientLobby(NetworkGame net, float scale)
    {
        var r = Panel(scale, 460, 340);
        GUILayout.BeginArea(new Rect(r.x + 20 * scale, r.y + 12 * scale, r.width - 40 * scale, r.height - 24 * scale));
        GUILayout.Label("Waiting for the host to start", title);
        GUILayout.Space(8 * scale);
        GUILayout.Label("Players:", label);
        foreach (var n in net.LobbyNames) GUILayout.Label("  " + n, label);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Leave", button, GUILayout.Height(32 * scale))) net.BackToMenu();
        GUILayout.EndArea();
    }

    private void DrawEnded(NetworkGame net, float scale)
    {
        var r = Panel(scale, 460, 200);
        GUILayout.BeginArea(new Rect(r.x + 20 * scale, r.y + 12 * scale, r.width - 40 * scale, r.height - 24 * scale));
        GUILayout.Label("Game over", title);
        GUILayout.Label(net.Message, warning);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Back to menu", button, GUILayout.Height(36 * scale))) net.BackToMenu();
        GUILayout.EndArea();
    }

    /// <summary>A thin status line while playing: who you are, waiting for others, or a desync warning.</summary>
    private void DrawStatus(NetworkGame net, float scale)
    {
        if (!NetworkGame.Active) return;
        string text = $"Network game - you are {PlayerManager.Instance.Local?.name}";
        GUIStyle style = label;
        if (net.DesyncTick >= 0) { text = net.Message; style = warning; }
        else if (Simulation.Stalled) { text += "  -  waiting for the other players..."; style = warning; }
        GUI.Label(new Rect(Screen.width / 2f - 260 * scale, 4 * scale, 520 * scale, 28 * scale), text, style);
    }
}
