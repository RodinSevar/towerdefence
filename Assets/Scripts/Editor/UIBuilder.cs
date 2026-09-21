using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the game UI (HUD, bottom bar, minimap, selection panel, game-over panel) into the open scene and
/// wires every serialized field. Re-running replaces the previous UI, so edit this file (or tweak the result
/// in the scene and stop using the builder) rather than hand-editing and re-running.
/// </summary>
public static class UIBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // Objects this builder owns; removed before rebuilding.
    private static readonly string[] OwnedRoots = { "Canvas", "EventSystem", "MinimapManager" };

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
        canvasObj.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Transform canvas = canvasObj.transform;

        var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObj.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // ---- HUD (top text + game over panel) ----
        RectTransform hud = Panel("HUD", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        var hudComp = hud.gameObject.AddComponent<HUD>();

        var goldText = Text("GoldText", hud, "Gold: 500", 18, TextAlignmentOptions.TopLeft,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -25), new Vector2(200, 0));
        var livesText = Text("LivesText", hud, "Lives: 20", 18, TextAlignmentOptions.Top,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-100, -25), new Vector2(100, 0));
        var waveText = Text("WaveText", hud, "Wave: 0 / 5", 18, TextAlignmentOptions.TopRight,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -25), new Vector2(-10, 0));

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
        Wire(hudComp, "gameOverText", gameOverText);
        Wire(hudComp, "gameOverPanel", gameOverPanel.gameObject);
        Wire(hudComp, "restartButton", restartButton);

        // ---- Bottom bar (minimap | unit info | build grid) ----
        // Build grid is 4 columns x 3 rows like WC3 ability buttons (see TowerSelectionUI).
        const float barHeight = 160f, cell = 44f, gap = 4f, pad = 8f;
        const float gridWidth = 4 * cell + 3 * gap + 2 * pad;
        RectTransform bottomBar = Panel("BottomBar", canvas, new Vector2(0, 0), new Vector2(1, 0),
            Vector2.zero, new Vector2(0, barHeight), new Color(0.1f, 0.1f, 0.1f));

        RectTransform minimapBox = Panel("MinimapBox", bottomBar, new Vector2(0, 0), new Vector2(0, 1),
            Vector2.zero, new Vector2(barHeight, 0), new Color(0.05f, 0.05f, 0.05f));
        minimapBox.gameObject.AddComponent<MinimapInteraction>();
        minimapBox.gameObject.AddComponent<MinimapCameraView>();
        minimapBox.gameObject.AddComponent<RectMask2D>();

        RectTransform actionGrid = Panel("ActionGridBox", bottomBar, new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-gridWidth, 0), Vector2.zero, new Color(0.15f, 0.15f, 0.15f));
        var gridLayout = actionGrid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(cell, cell);
        gridLayout.spacing = new Vector2(gap, gap);
        gridLayout.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 4;
        var towerSelection = actionGrid.gameObject.AddComponent<TowerSelectionUI>();
        Wire(towerSelection, "towersContainer", actionGrid);

        RectTransform centerBox = Panel("CenterSectionBox", bottomBar, Vector2.zero, Vector2.one,
            new Vector2(barHeight, 0), new Vector2(-gridWidth, 0), new Color(0.12f, 0.12f, 0.12f));

        // Selected-unit panel (hidden until something is selected)
        RectTransform selected = Panel("SelectedTowerUI", centerBox, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        var selectionUI = selected.gameObject.AddComponent<SelectionUI>();
        var nameText = Text("NameText", selected, "Tower Name", 18, TextAlignmentOptions.Top,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -30), new Vector2(-20, -5));
        var statsText = Text("StatsText", selected, "Damage: 0\nRange: 0\nSpeed: 0", 14, TextAlignmentOptions.TopLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 10), new Vector2(-20, -30));
        Button sellButton = MakeButton("SellButton", selected, "Sell", 16, new Color(0.8f, 0.2f, 0.2f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 10), new Vector2(-20, 40), out var sellLabel);
        Button upgradeButton = MakeButton("UpgradeButton", selected, "Upgrade", 16, new Color(0.2f, 0.6f, 0.2f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(-300, 10), new Vector2(-160, 40), out var upgradeLabel);

        Wire(selectionUI, "nameText", nameText);
        Wire(selectionUI, "statsText", statsText);
        Wire(selectionUI, "sellButton", sellButton);
        Wire(selectionUI, "sellButtonText", sellLabel);
        Wire(selectionUI, "upgradeButton", upgradeButton);
        Wire(selectionUI, "upgradeButtonText", upgradeLabel);
        Wire(selectionUI, "panelObject", selected.gameObject);

        // ---- Minimap manager (needs the minimap container) ----
        var minimapManagerObj = new GameObject("MinimapManager");
        var minimapManager = minimapManagerObj.AddComponent<MinimapManager>();
        Wire(minimapManager, "minimapContainer", minimapBox);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // ---------- helpers ----------

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
