using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the game UI into the open scene and wires every serialized field. Re-running replaces the previous UI,
/// so once you start hand-editing the Canvas in the scene, stop using the builder.
///
/// Layout follows the Warcraft III interface as measured on a 1024x768 screenshot. The canvas scales with
/// screen HEIGHT (reference 1024x768), and each cluster is anchored to the left, right or center of the screen,
/// so proportions hold at any resolution and buttons stay square on wide monitors. All numbers below are in
/// reference units: X is measured from the anchored screen edge, Y from the top (top bar) or bottom (console).
/// </summary>
public static class UIBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // Objects this builder owns; removed before rebuilding.
    private static readonly string[] OwnedRoots = { "Canvas", "EventSystem", "MinimapManager" };

    // ---- Layout (reference units, from the WC3 screenshot) ----
    private const float ConsoleHeight = 206f;   // bottom console
    private const float LeftWidth = 232f;       // minimap cluster
    private const float RightWidth = 246f;      // command card
    private const float MinimapSize = 180f;     // square, so minimap math stays simple
    private const float ButtonCell = 48f, ButtonGap = 7f;   // command card 4x3

    private static readonly Color ConsoleColor = new Color(0.14f, 0.12f, 0.11f);
    private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.08f);
    private static readonly Color SlotColor = new Color(0.05f, 0.06f, 0.10f, 0.9f);

    [MenuItem("Tools/Build UI")]
    public static void BuildInOpenScene()
    {
        Build();
    }

    /// <summary>Entry point for batch mode: opens the main scene, builds the UI, saves.</summary>
    public static void BuildAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("UIBuilder: UI built and scene saved.");
    }

    private static void Build()
    {
        foreach (string name in OwnedRoots)
        {
            GameObject old;
            while ((old = GameObject.Find(name)) != null) Object.DestroyImmediate(old);
        }

        // ---- Canvas + EventSystem ----
        var canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1024, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // scale with height
        Transform canvas = canvasObj.transform;

        var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObj.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // ---- HUD root ----
        RectTransform hud = Panel("HUD", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        var hudComp = hud.gameObject.AddComponent<HUD>();

        // Top-left menu buttons: x from left edge, y from top edge. Quests, Menu and Allies are placeholders;
        // the fourth (where the original has "Log") opens the race selector.
        string[] menuLabels = { "Quests (F9)", "Menu (F10)", "Allies", "Race (F12)" };
        Button raceButton = null;
        for (int i = 0; i < menuLabels.Length; i++)
        {
            float x = 3 + i * 109;
            Button menuButton = MakeButton("MenuButton_" + menuLabels[i].Split(' ')[0], hud, menuLabels[i], 12,
                new Color(0.10f, 0.12f, 0.22f), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -27), new Vector2(x + 105, -3), out _);
            if (i == 3) raceButton = menuButton;
        }

        // Top-right resource slots: gold / (wood slot) lives / (food slot) wave. x from right edge, y from top.
        var goldText = ResourceSlot("GoldSlot", hud, "Gold: 500", new Color(1f, 0.85f, 0.2f), -434, -329);
        var livesText = ResourceSlot("LivesSlot", hud, "Lives: 20", new Color(0.3f, 0.8f, 0.3f), -324, -219);
        var waveText = ResourceSlot("WaveSlot", hud, "Wave: 0 / 5", new Color(0.9f, 0.5f, 0.2f), -214, -109);
        var lumberText = ResourceSlot("LumberSlot", hud, "Lumber: 1", new Color(0.55f, 0.35f, 0.15f), -104, -4);

        // Game over overlay
        RectTransform gameOverPanel = Panel("GameOverPanel", hud, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.7f));
        var gameOverText = Text("GameOverText", gameOverPanel, "GAME OVER", 72, TextAlignmentOptions.Center,
            new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 50), new Vector2(0, 150));
        Button restartButton = MakeButton("RestartButton", gameOverPanel, "Restart Game", 24,
            new Color(0.2f, 0.6f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-100, -50), new Vector2(100, 0), out _);
        gameOverPanel.gameObject.SetActive(false);

        Wire(hudComp, "goldText", goldText);
        Wire(hudComp, "livesText", livesText);
        Wire(hudComp, "waveText", waveText);
        Wire(hudComp, "lumberText", lumberText);
        Wire(hudComp, "gameOverText", gameOverText);
        Wire(hudComp, "gameOverPanel", gameOverPanel.gameObject);
        Wire(hudComp, "restartButton", restartButton);

        // ---- Bottom console: [minimap] [portrait + unit info] [command card] ----
        RectTransform console = Panel("BottomBar", canvas, new Vector2(0, 0), new Vector2(1, 0),
            Vector2.zero, new Vector2(0, ConsoleHeight), ConsoleColor);

        // Left: minimap, anchored to the left edge
        RectTransform leftPanel = Panel("LeftPanel", console, new Vector2(0, 0), new Vector2(0, 1),
            Vector2.zero, new Vector2(LeftWidth, 0), PanelColor);
        RectTransform minimapBox = Panel("MinimapBox", leftPanel, new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(8, 10), new Vector2(8 + MinimapSize, 10 + MinimapSize), Color.black);
        minimapBox.gameObject.AddComponent<MinimapInteraction>();
        minimapBox.gameObject.AddComponent<MinimapCameraView>();
        minimapBox.gameObject.AddComponent<RectMask2D>();

        // Right: 4x3 command card (tower buy buttons), anchored to the right edge
        RectTransform actionGrid = Panel("ActionGridBox", console, new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-RightWidth, 0), Vector2.zero, PanelColor);
        var gridLayout = actionGrid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(ButtonCell, ButtonCell);
        gridLayout.spacing = new Vector2(ButtonGap, ButtonGap);
        gridLayout.padding = new RectOffset(16, 14, 35, 13);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        var towerSelection = actionGrid.gameObject.AddComponent<TowerSelectionUI>();
        Wire(towerSelection, "towersContainer", actionGrid);

        // Center: stretches between the two side clusters
        RectTransform centerBox = Panel("CenterSectionBox", console, Vector2.zero, Vector2.one,
            new Vector2(LeftWidth, 0), new Vector2(-RightWidth, 0), ConsoleColor);

        // Selected-unit panel: portrait + info (hidden until something is selected)
        RectTransform selected = Panel("SelectedTowerUI", centerBox, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        var selectionUI = selected.gameObject.AddComponent<SelectionUI>();

        Panel("Portrait", selected, new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(12, 20), new Vector2(124, 170), PanelColor);

        RectTransform info = Panel("InfoBox", selected, new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(132, 0), new Vector2(-12, 150), PanelColor);
        var nameText = Text("NameText", info, "Tower Name", 16, TextAlignmentOptions.Top,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -26), new Vector2(-8, -4));
        var statsText = Text("StatsText", info, "Damage: 0\nRange: 0\nSpeed: 0", 13, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 34), new Vector2(-8, -30));
        Button sellButton = MakeButton("SellButton", info, "Sell", 13, new Color(0.8f, 0.2f, 0.2f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-118, 6), new Vector2(-8, 30), out var sellLabel);
        Button upgradeButton = MakeButton("UpgradeButton", info, "Upgrade", 13, new Color(0.2f, 0.6f, 0.2f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-236, 6), new Vector2(-126, 30), out var upgradeLabel);

        Wire(selectionUI, "nameText", nameText);
        Wire(selectionUI, "statsText", statsText);
        Wire(selectionUI, "sellButton", sellButton);
        Wire(selectionUI, "sellButtonText", sellLabel);
        Wire(selectionUI, "upgradeButton", upgradeButton);
        Wire(selectionUI, "upgradeButtonText", upgradeLabel);
        Wire(selectionUI, "panelObject", selected.gameObject);

        BuildRacePanel(canvas, raceButton);

        // ---- Minimap manager (needs the minimap container) ----
        var minimapManagerObj = new GameObject("MinimapManager");
        var minimapManager = minimapManagerObj.AddComponent<MinimapManager>();
        Wire(minimapManager, "minimapContainer", minimapBox);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>Element (race) selector: race list on the left, details and unlock button on the right. Hidden until opened.</summary>
    private static void BuildRacePanel(Transform canvas, Button toggleButton)
    {
        RectTransform root = Panel("RacePanel", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        var panelUI = root.gameObject.AddComponent<RacePanelUI>();

        // 560x380 panel, centered horizontally and sitting above the bottom console
        RectTransform content = Panel("Content", root, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
            new Vector2(-280, -190), new Vector2(280, 190), new Color(0.08f, 0.08f, 0.10f, 0.96f));

        Text("Title", content, "Choose an Element", 18, TextAlignmentOptions.Top,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -34), new Vector2(-44, -6));
        Button close = MakeButton("CloseButton", content, "X", 14, new Color(0.6f, 0.2f, 0.2f),
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -32), new Vector2(-8, -8), out _);

        RectTransform list = Panel("RaceList", content, new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(12, 12), new Vector2(212, -40), null);
        var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 2;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        TextMeshProUGUI info = Text("InfoText", content, "", 12, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(224, 56), new Vector2(-12, -40));
        info.enableAutoSizing = true;
        info.fontSizeMin = 8;
        info.fontSizeMax = 13;

        Button action = MakeButton("ActionButton", content, "Unlock", 14, new Color(0.2f, 0.5f, 0.25f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-286, 12), new Vector2(-12, 44), out var actionLabel);

        Wire(panelUI, "content", content.gameObject);
        Wire(panelUI, "raceListContainer", list);
        Wire(panelUI, "infoText", info);
        Wire(panelUI, "actionButton", action);
        Wire(panelUI, "actionButtonText", actionLabel);
        Wire(panelUI, "closeButton", close);
        Wire(panelUI, "toggleButton", toggleButton);
    }

    // ---------- helpers ----------

    /// <summary>A 105x24 top-right resource slot (icon placeholder + text). xMin/xMax are offsets from the right edge.</summary>
    private static TextMeshProUGUI ResourceSlot(string name, Transform parent, string text, Color iconColor, float xMin, float xMax)
    {
        RectTransform slot = Panel(name, parent, new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(xMin, -27), new Vector2(xMax, -3), SlotColor);
        Panel("Icon", slot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, -9), new Vector2(21, 9), iconColor);
        return Text("Text", slot, text, 14, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(24, 0), new Vector2(-2, 0));
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax,
        Vector2 oMin, Vector2 oMax, Color? background)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        if (background.HasValue) obj.AddComponent<Image>().color = background.Value;
        return SetRect(obj.GetComponent<RectTransform>(), aMin, aMax, oMin, oMax);
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
        TextAlignmentOptions align, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        SetRect(obj.GetComponent<RectTransform>(), aMin, aMax, oMin, oMax);
        return tmp;
    }

    private static Button MakeButton(string name, Transform parent, string label, float fontSize, Color color,
        Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, out TextMeshProUGUI labelText)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var image = obj.AddComponent<Image>();
        image.color = color;
        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        SetRect(obj.GetComponent<RectTransform>(), aMin, aMax, oMin, oMax);

        labelText = Text("Text", obj.transform, label, fontSize, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        labelText.color = Color.white;
        return button;
    }

    private static RectTransform SetRect(RectTransform rect, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        rect.anchorMin = aMin;
        rect.anchorMax = aMax;
        rect.offsetMin = oMin;
        rect.offsetMax = oMax;
        return rect;
    }

    /// <summary>Assigns a serialized field. Throws if the field doesn't exist, so renames are caught immediately.</summary>
    private static void Wire(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
            throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
