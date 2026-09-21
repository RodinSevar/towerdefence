using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screens shown before a game: main menu (name, play offline, host, join, exit), the setup lobby (nine slots with their
/// colours, a minimap marking where each slot starts, Start), and the end notice. While any of them is up the game world is held
/// and cannot be clicked (<see cref="GameInput"/>); the race selector can still be opened from the lobby to browse ahead.
/// During a network game it also shows a thin status line.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    private const float MapSize = 340f;
    private const float RowHeight = 36f;

    private GameObject root;
    private GameObject menuScreen, lobbyScreen, endedScreen;
    private TMP_InputField nameInput, portInput, addressInput;
    private TextMeshProUGUI menuMessage, lobbyTitle, lobbyInfo, endedMessage, statusLine;
    private Button startButton;
    private TextMeshProUGUI startLabel;

    private readonly Image[] rowSwatch = new Image[PlayerSlots.Count];
    private readonly TextMeshProUGUI[] rowLabel = new TextMeshProUGUI[PlayerSlots.Count];
    private readonly TextMeshProUGUI[] rowHolder = new TextMeshProUGUI[PlayerSlots.Count];
    private readonly Image[] rowBackground = new Image[PlayerSlots.Count];
    private readonly RectTransform[] marker = new RectTransform[PlayerSlots.Count];
    private readonly Image[][] markerBars = new Image[PlayerSlots.Count][];
    private RawImage mapImage;
    private bool markersPlaced;

    private RacePanelUI racePanel;

    private void Start()
    {
        var limiter = FindAnyObjectByType<UiWidthLimiter>();
        if (limiter == null) { enabled = false; return; }
        RectTransform frame = limiter.Frame;
        racePanel = FindAnyObjectByType<RacePanelUI>();

        // Full-frame dimmer that also swallows clicks meant for the HUD behind it
        var dim = UiKit.Panel("SetupScreens", frame, new Color(0, 0, 0, 0.78f));
        UiKit.Stretch(dim.rectTransform);
        dim.rectTransform.offsetMin = new Vector2(-6000f, 0f); // also cover the sides outside the width-limited frame
        dim.rectTransform.offsetMax = new Vector2(6000f, 0f);
        root = dim.gameObject;

        // Sits just under the race selector, so the selector can be opened over the lobby
        var raceRoot = frame.Find("RacePanel");
        if (raceRoot != null) root.transform.SetSiblingIndex(raceRoot.GetSiblingIndex());

        BuildMenuScreen(root.transform);
        BuildLobbyScreen(root.transform);
        BuildEndedScreen(root.transform);

        statusLine = UiKit.Label("NetworkStatus", frame, "", 15, TextAlignmentOptions.Center);
        UiKit.Sized(statusLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(560, 24));
        statusLine.gameObject.SetActive(false);
    }

    private void Update()
    {
        var net = NetworkGame.Instance;
        if (net == null || root == null) return;

        bool showRoot = net.MenuOpen;
        if (root.activeSelf != showRoot) root.SetActive(showRoot);

        menuScreen.SetActive(net.Current == NetworkGame.Phase.Menu);
        bool browsing = racePanel != null && racePanel.IsOpen; // the element browser takes over the screen while it is open
        lobbyScreen.SetActive(net.InLobby && !browsing);
        endedScreen.SetActive(net.Current == NetworkGame.Phase.Ended);

        if (net.Current == NetworkGame.Phase.Menu) menuMessage.text = net.Message;
        if (net.InLobby) RefreshLobby(net);
        if (net.Current == NetworkGame.Phase.Ended) endedMessage.text = net.Message;

        // status line during a network game
        bool showStatus = NetworkGame.Active;
        if (statusLine.gameObject.activeSelf != showStatus) statusLine.gameObject.SetActive(showStatus);
        if (showStatus)
        {
            string me = PlayerManager.Instance.Local != null ? PlayerManager.Instance.Local.name : "";
            if (net.DesyncTick >= 0) { statusLine.text = net.Message; statusLine.color = new Color(1f, 0.45f, 0.35f); }
            else if (Simulation.Stalled) { statusLine.text = "Waiting for the other players..."; statusLine.color = new Color(1f, 0.85f, 0.4f); }
            else { statusLine.text = $"Network game - you are {me}"; statusLine.color = new Color(1, 1, 1, 0.7f); }
        }
    }

    // ------------------------------------------------------------------------------------------------ main menu

    private void BuildMenuScreen(Transform parent)
    {
        var panel = UiKit.Panel("MainMenu", parent, UiKit.PanelColor);
        UiKit.Sized(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 470));
        menuScreen = panel.gameObject;
        Transform t = panel.transform;

        var title = UiKit.Label("Title", t, "Wintermaul TD", 34, TextAlignmentOptions.Center);
        UiKit.Sized(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(400, 46));

        Row(t, "Your name", -76, out var nameLabel);
        nameInput = UiKit.Input("NameInput", t, "Player", "name", 16, 18);
        UiKit.Sized((RectTransform)nameInput.transform, new Vector2(1f, 1f), new Vector2(-24, -76), new Vector2(250, 34));

        var offline = UiKit.Button("PlayOffline", t, "Play offline", 22, UiKit.GoodColor, out _);
        UiKit.Sized((RectTransform)offline.transform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(392, 46));
        offline.onClick.AddListener(() => NetworkGame.Instance.PlayOffline(CleanName()));

        Row(t, "Port", -204, out var portLabel);
        portInput = UiKit.Input("PortInput", t, NetworkGame.DefaultPort.ToString(), "port", 5, 18);
        UiKit.Sized((RectTransform)portInput.transform, new Vector2(1f, 1f), new Vector2(-24, -204), new Vector2(250, 34));
        portInput.contentType = TMP_InputField.ContentType.IntegerNumber;

        var host = UiKit.Button("Host", t, "Host a LAN game", 20, UiKit.ButtonColor, out _);
        UiKit.Sized((RectTransform)host.transform, new Vector2(0.5f, 1f), new Vector2(0, -248), new Vector2(392, 42));
        host.onClick.AddListener(() => { if (int.TryParse(portInput.text, out int p)) NetworkGame.Instance.HostGame(p, CleanName()); });

        Row(t, "Host address", -304, out var addressLabel);
        addressInput = UiKit.Input("AddressInput", t, "192.168.0.10", "host IP", 40, 18);
        UiKit.Sized((RectTransform)addressInput.transform, new Vector2(1f, 1f), new Vector2(-24, -304), new Vector2(250, 34));

        var join = UiKit.Button("Join", t, "Join", 20, UiKit.ButtonColor, out _);
        UiKit.Sized((RectTransform)join.transform, new Vector2(0.5f, 1f), new Vector2(0, -348), new Vector2(392, 42));
        join.onClick.AddListener(() => { if (int.TryParse(portInput.text, out int p)) NetworkGame.Instance.JoinGame(addressInput.text.Trim(), p, CleanName()); });

        menuMessage = UiKit.Label("Message", t, "", 15, TextAlignmentOptions.Center, new Color(1f, 0.5f, 0.4f));
        UiKit.Sized(menuMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 58), new Vector2(400, 24));

        var exit = UiKit.Button("Exit", t, "Exit game", 18, UiKit.BadColor, out _);
        UiKit.Sized((RectTransform)exit.transform, new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(200, 36));
        exit.onClick.AddListener(NetworkGame.QuitApplication);
    }

    private static void Row(Transform parent, string text, float y, out TextMeshProUGUI label)
    {
        label = UiKit.Label(text, parent, text, 18, TextAlignmentOptions.Left);
        UiKit.Sized(label.rectTransform, new Vector2(0f, 1f), new Vector2(24, y), new Vector2(130, 34));
    }

    private string CleanName()
    {
        string n = nameInput.text.Trim();
        return n.Length > 0 ? n : "Player";
    }

    // ------------------------------------------------------------------------------------------------ setup lobby

    private void BuildLobbyScreen(Transform parent)
    {
        var panel = UiKit.Panel("Lobby", parent, UiKit.PanelColor);
        UiKit.Sized(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 560));
        lobbyScreen = panel.gameObject;
        Transform t = panel.transform;

        lobbyTitle = UiKit.Label("Title", t, "Game setup", 28, TextAlignmentOptions.Center);
        UiKit.Sized(lobbyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(760, 40));

        var hint = UiKit.Label("Hint", t, "Pick a slot: click its row or its X on the map", 15, TextAlignmentOptions.Left, new Color(1, 1, 1, 0.65f));
        UiKit.Sized(hint.rectTransform, new Vector2(0f, 1f), new Vector2(20, -58), new Vector2(380, 22));

        // slot list (left)
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            int slot = i;
            var row = UiKit.Button("Slot" + i, t, "", 16, new Color(0.13f, 0.14f, 0.2f), out var unusedText);
            Object.Destroy(unusedText.gameObject);
            UiKit.Sized((RectTransform)row.transform, new Vector2(0f, 1f), new Vector2(20, -84 - i * (RowHeight + 3)), new Vector2(392, RowHeight));
            row.onClick.AddListener(() => NetworkGame.Instance.SelectSlot(slot));
            rowBackground[i] = row.GetComponent<Image>();

            var swatch = UiKit.Panel("Swatch", row.transform, PlayerSlots.Colors[i]);
            UiKit.Sized(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(24, 24));
            swatch.raycastTarget = false;
            rowSwatch[i] = swatch;

            rowLabel[i] = UiKit.Label("Label", row.transform, PlayerSlots.ColorNames[i], 17, TextAlignmentOptions.Left);
            UiKit.Place(rowLabel[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 0), new Vector2(-150, 0));
            rowHolder[i] = UiKit.Label("Holder", row.transform, "Open", 17, TextAlignmentOptions.Right);
            UiKit.Place(rowHolder[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(150, 0), new Vector2(-10, 0));
        }

        // minimap with start markers (right)
        var mapFrame = UiKit.Panel("MapFrame", t, new Color(0, 0, 0, 1));
        UiKit.Sized(mapFrame.rectTransform, new Vector2(1f, 1f), new Vector2(-20, -60), new Vector2(MapSize + 8, MapSize + 8));
        var map = UiKit.Rect("Map", mapFrame.transform);
        UiKit.Place(map, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        mapImage = map.gameObject.AddComponent<RawImage>();
        mapImage.color = Color.white;
        mapImage.raycastTarget = true;
        for (int i = 0; i < PlayerSlots.Count; i++) BuildMarker(map, i);

        lobbyInfo = UiKit.Label("Info", t, "", 15, TextAlignmentOptions.TopRight, new Color(1, 1, 1, 0.75f));
        UiKit.Sized(lobbyInfo.rectTransform, new Vector2(1f, 1f), new Vector2(-20, -60 - MapSize - 18), new Vector2(MapSize + 8, 90));

        // buttons (bottom)
        var back = UiKit.Button("Back", t, "Back", 20, UiKit.BadColor, out _);
        UiKit.Sized((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(20, 16), new Vector2(150, 44));
        back.onClick.AddListener(() => NetworkGame.Instance.BackToMenu());

        var browse = UiKit.Button("Browse", t, "Browse elements", 18, UiKit.ButtonColor, out _);
        UiKit.Sized((RectTransform)browse.transform, new Vector2(0.5f, 0f), new Vector2(-40, 16), new Vector2(210, 44));
        browse.onClick.AddListener(() => { if (racePanel != null) racePanel.Toggle(); });

        startButton = UiKit.Button("Start", t, "Start game", 22, UiKit.GoodColor, out startLabel);
        UiKit.Sized((RectTransform)startButton.transform, new Vector2(1f, 0f), new Vector2(-20, 16), new Vector2(190, 44));
        startButton.onClick.AddListener(() => NetworkGame.Instance.StartGame());
    }

    /// <summary>A coloured X on the minimap at a slot's start; clicking it takes that slot.</summary>
    private void BuildMarker(RectTransform map, int slot)
    {
        var holder = UiKit.Panel("Marker" + slot, map, new Color(0, 0, 0, 0.001f)); // near-invisible but clickable
        var rect = holder.rectTransform;
        UiKit.Sized(rect, Vector2.zero, Vector2.zero, new Vector2(34, 34));
        rect.pivot = new Vector2(0.5f, 0.5f);
        var button = holder.gameObject.AddComponent<Button>();
        button.targetGraphic = holder;
        button.onClick.AddListener(() => NetworkGame.Instance.SelectSlot(slot));

        var bars = new Image[4]; // two dark bars (outline) under two coloured bars
        for (int i = 0; i < 4; i++)
        {
            var bar = UiKit.Panel(i < 2 ? "Outline" : "Bar", rect, i < 2 ? new Color(0, 0, 0, 0.9f) : PlayerSlots.Colors[slot]);
            bar.raycastTarget = false;
            UiKit.Sized(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, i < 2 ? new Vector2(34, 9) : new Vector2(30, 5));
            bar.rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0) ? 45 : -45);
            bars[i] = bar;
        }
        marker[slot] = rect;
        markerBars[slot] = bars;
    }

    private void RefreshLobby(NetworkGame net)
    {
        bool offline = net.Current == NetworkGame.Phase.OfflineLobby;
        lobbyTitle.text = offline ? "Single player" : net.IsHost ? "Hosting a LAN game" : "Waiting for the host to start";

        var addresses = NetworkGame.LocalAddresses();
        lobbyInfo.text = net.Current == NetworkGame.Phase.HostLobby
            ? "Others join with:\n" + string.Join("\n", addresses) + $"\nport {portInput.text}"
            : offline ? "" : "The host starts the game.";

        startButton.gameObject.SetActive(net.CanStart);
        startLabel.text = "Start game";

        if (mapImage.texture == null && TerrainBuilder.Instance != null) mapImage.texture = TerrainBuilder.Instance.MinimapTexture;
        if (!markersPlaced && WaveManager.Instance != null && WaveManager.Instance.activeSpawners.Count > 0) PlaceMarkers();

        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            string holder = net.SlotHolder(i);
            bool mine = i == net.MySlot;
            rowLabel[i].text = PlayerSlots.Label(i);
            rowHolder[i].text = holder == null ? "Open" : mine ? holder + " (you)" : holder;
            rowHolder[i].color = holder == null ? new Color(1, 1, 1, 0.45f) : Color.white;
            rowBackground[i].color = mine ? new Color(0.25f, 0.32f, 0.55f) : holder != null ? new Color(0.18f, 0.18f, 0.24f) : new Color(0.13f, 0.14f, 0.2f);

            if (marker[i] == null) continue;
            float alpha = holder == null ? 0.55f : 1f;
            float size = mine ? 1.35f : 1f;
            marker[i].localScale = Vector3.one * size;
            for (int b = 0; b < 4; b++)
            {
                var c = b < 2 ? new Color(0, 0, 0, 0.9f * alpha) : PlayerSlots.Colors[i];
                if (b >= 2) c.a = alpha;
                markerBars[i][b].color = c;
            }
            if (mine) marker[i].SetAsLastSibling();
        }
    }

    private void PlaceMarkers()
    {
        markersPlaced = true;
        var map = (RectTransform)mapImage.transform;
        float w = map.rect.width, h = map.rect.height;
        for (int i = 0; i < PlayerSlots.Count; i++)
        {
            Vector3 p = PlayerSlots.StartPosition(i);
            marker[i].anchorMin = marker[i].anchorMax = Vector2.zero;
            marker[i].anchoredPosition = new Vector2((p.x + 96f) / 192f * w, (p.z + 96f) / 192f * h);
        }
    }

    // ------------------------------------------------------------------------------------------------ ended

    private void BuildEndedScreen(Transform parent)
    {
        var panel = UiKit.Panel("Ended", parent, UiKit.PanelColor);
        UiKit.Sized(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 220));
        endedScreen = panel.gameObject;

        var title = UiKit.Label("Title", panel.transform, "Game ended", 28, TextAlignmentOptions.Center);
        UiKit.Sized(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(440, 40));
        endedMessage = UiKit.Label("Message", panel.transform, "", 17, TextAlignmentOptions.Center, new Color(1f, 0.6f, 0.5f));
        UiKit.Sized(endedMessage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(440, 60));

        var back = UiKit.Button("Back", panel.transform, "Back to menu", 20, UiKit.ButtonColor, out _);
        UiKit.Sized((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(-105, 16), new Vector2(200, 42));
        back.onClick.AddListener(() => NetworkGame.Instance.BackToMenu());
        var exit = UiKit.Button("Exit", panel.transform, "Exit game", 20, UiKit.BadColor, out _);
        UiKit.Sized((RectTransform)exit.transform, new Vector2(0.5f, 0f), new Vector2(105, 16), new Vector2(200, 42));
        exit.onClick.AddListener(NetworkGame.QuitApplication);
    }
}
