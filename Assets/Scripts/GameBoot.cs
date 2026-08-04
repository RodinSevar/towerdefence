using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Boot script that sets up the entire game when the scene starts.
/// Simply attach this to any GameObject in the scene.
/// </summary>
public class GameBoot : MonoBehaviour
{
    private void Awake()
    {
        // Ensure we don't have duplicate boots
        GameBoot[] boots = FindObjectsByType<GameBoot>(FindObjectsSortMode.None);
        if (boots.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        SetupScene();
    }

    private void SetupScene()
    {
        // Scene environment (camera, ground, lighting, spawn point)
        SetupEnvironment();
        
        // Game managers
        SetupManagers();
        
        // UI
        SetupUI();
    }

    private void SetupEnvironment()
    {
        try
        {
            // Find or create camera
            Camera camera = FindAnyObjectByType<Camera>();
            GameObject cameraObj;
            
            if (camera == null)
            {
                Debug.Log("Creating new camera");
                cameraObj = new GameObject("Main Camera");
                camera = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
                cameraObj.tag = "MainCamera";
            }
            else
            {
                cameraObj = camera.gameObject;
            }

            cameraObj.transform.position = new Vector3(0, 100, -30);
            cameraObj.transform.rotation = Quaternion.Euler(70, 0, 0);
            
            // Set camera clipping planes
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            // Add camera controller
            if (cameraObj.GetComponent<CameraController>() == null)
            {
                cameraObj.AddComponent<CameraController>();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error setting up camera: " + e.Message);
        }

        // Ground creation has been moved to MapGenerator in SetupManagers()

        // Create spawn point if not exists
        if (GameObject.FindWithTag("SpawnPoint") == null)
        {
            GameObject spawnObj = new GameObject("SpawnPoint");
            spawnObj.tag = "SpawnPoint";
            spawnObj.transform.position = new Vector3(-18, 0.5f, 0);
        }

        // Create lighting if not exists
        if (FindAnyObjectByType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        }
    }

    private void SetupManagers()
    {
        
        // Create GridManager if not exists
        if (FindAnyObjectByType<GridManager>() == null)
        {
            GameObject gridObj = new GameObject("GridManager");
            gridObj.AddComponent<GridManager>();
        }

        // Generate Map
        if (FindAnyObjectByType<MapGenerator>() == null)
        {
            GameObject mapObj = new GameObject("MapGenerator");
            mapObj.tag = "Ground";
            MapGenerator gen = mapObj.AddComponent<MapGenerator>();
            
            // Try to load a custom image map from the Resources folder
            Texture2D customMap = Resources.Load<Texture2D>("MapLayout");
            if (customMap != null)
            {
                if (customMap.isReadable)
                {
                    var field = gen.GetType().GetField("mapTexture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null) field.SetValue(gen, customMap);
                }
                else
                {
                    Debug.LogWarning("The MapLayout.png image was found, but it is not readable! You must select the image in Unity and check 'Read/Write' in the Inspector. Falling back to default map generation.");
                }
            }
            
            gen.GenerateMap();
        }
        
        // Create GameManager if not exists
        if (FindAnyObjectByType<GameManager>() == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        // Create WaveManager if not exists
        if (FindAnyObjectByType<WaveManager>() == null)
        {
            GameObject wmObj = new GameObject("WaveManager");
            wmObj.AddComponent<WaveManager>();
        }

        // Create PathManager if not exists
        if (FindAnyObjectByType<PathManager>() == null)
        {
            GameObject pmObj = new GameObject("PathManager");
            pmObj.AddComponent<PathManager>();
        }

        // Create TowerManager if not exists
        if (FindAnyObjectByType<TowerManager>() == null)
        {
            GameObject tmObj = new GameObject("TowerManager");
            tmObj.AddComponent<TowerManager>();
        }

        // Create PlayerInteraction if not exists
        if (FindAnyObjectByType<PlayerInteraction>() == null)
        {
            GameObject piObj = new GameObject("PlayerInteraction");
            piObj.AddComponent<PlayerInteraction>();
        }

        // Create MinimapManager if not exists
        if (FindAnyObjectByType<MinimapManager>() == null)
        {
            GameObject mmObj = new GameObject("MinimapManager");
            mmObj.AddComponent<MinimapManager>();
        }
    }

    private void SetupUI()
    {
        // Create Canvas if not exists
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // Add EventSystem
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<InputSystemUIInputModule>();
            }
        }

        // Create HUD container
        GameObject hudObj = new GameObject("HUD");
        hudObj.transform.SetParent(canvas.transform, false);
        RectTransform hudRect = hudObj.AddComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        HUD hudComponent = hudObj.AddComponent<HUD>();

        // ---------------- TOP HUD (Resources & Wave) ----------------
        // Gold Text
        GameObject goldTextObj = new GameObject("GoldText");
        goldTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI goldText = goldTextObj.AddComponent<TextMeshProUGUI>();
        goldText.text = "Gold: 500";
        goldText.fontSize = 18; // Shrunk down
        RectTransform goldRect = goldTextObj.GetComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(0, 1);
        goldRect.anchorMax = new Vector2(0, 1);
        goldRect.offsetMin = new Vector2(10, -25); // Taking up a quarter of previous vertical space
        goldRect.offsetMax = new Vector2(200, 0);

        // Lives Text
        GameObject livesTextObj = new GameObject("LivesText");
        livesTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI livesText = livesTextObj.AddComponent<TextMeshProUGUI>();
        livesText.text = "Lives: 20";
        livesText.fontSize = 18;
        RectTransform livesRect = livesTextObj.GetComponent<RectTransform>();
        livesRect.anchorMin = new Vector2(0.5f, 1);
        livesRect.anchorMax = new Vector2(0.5f, 1);
        livesRect.offsetMin = new Vector2(-100, -25);
        livesRect.offsetMax = new Vector2(100, 0);

        // Wave Text
        GameObject waveTextObj = new GameObject("WaveText");
        waveTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI waveText = waveTextObj.AddComponent<TextMeshProUGUI>();
        waveText.text = "Wave: 0 / 5";
        waveText.fontSize = 18;
        RectTransform waveRect = waveTextObj.GetComponent<RectTransform>();
        waveRect.anchorMin = new Vector2(1, 1);
        waveRect.anchorMax = new Vector2(1, 1);
        waveRect.offsetMin = new Vector2(-250, -25);
        waveRect.offsetMax = new Vector2(-10, 0);

        var goldField = hudComponent.GetType().GetField("goldText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var livesField = hudComponent.GetType().GetField("livesText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var waveField = hudComponent.GetType().GetField("waveText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (goldField != null) goldField.SetValue(hudComponent, goldText);
        if (livesField != null) livesField.SetValue(hudComponent, livesText);
        if (waveField != null) waveField.SetValue(hudComponent, waveText);

        // ---------------- BOTTOM BAR (Warcraft 3 Style) ----------------
        GameObject bottomBarObj = new GameObject("BottomBar");
        bottomBarObj.transform.SetParent(canvas.transform, false);
        Image bottomBarBg = bottomBarObj.AddComponent<Image>();
        bottomBarBg.color = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark background
        RectTransform bottomBarRect = bottomBarObj.GetComponent<RectTransform>();
        bottomBarRect.anchorMin = new Vector2(0, 0);
        bottomBarRect.anchorMax = new Vector2(1, 0);
        bottomBarRect.offsetMin = new Vector2(0, 0);
        bottomBarRect.offsetMax = new Vector2(0, 100); // 100px tall

        // Left Section: Minimap
        GameObject minimapObj = new GameObject("MinimapBox");
        minimapObj.transform.SetParent(bottomBarObj.transform, false);
        Image minimapBg = minimapObj.AddComponent<Image>();
        minimapBg.color = new Color(0.05f, 0.05f, 0.05f, 1f);
        RectTransform minimapRect = minimapObj.GetComponent<RectTransform>();
        minimapRect.anchorMin = new Vector2(0, 0);
        minimapRect.anchorMax = new Vector2(0, 1);
        minimapRect.offsetMin = new Vector2(0, 0);
        minimapRect.offsetMax = new Vector2(100, 0); // 100x100 square
        
        minimapObj.AddComponent<MinimapInteraction>();
        minimapObj.AddComponent<MinimapCameraView>();
        minimapObj.AddComponent<RectMask2D>();
        
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.Initialize(minimapRect);
        }

        // Right Section: Action Grid (Tower Selection UI)
        GameObject actionGridObj = new GameObject("ActionGridBox");
        actionGridObj.transform.SetParent(bottomBarObj.transform, false);
        Image actionGridBg = actionGridObj.AddComponent<Image>();
        actionGridBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        RectTransform actionGridRect = actionGridObj.GetComponent<RectTransform>();
        actionGridRect.anchorMin = new Vector2(1, 0);
        actionGridRect.anchorMax = new Vector2(1, 1);
        actionGridRect.offsetMin = new Vector2(-250, 0); // 250px wide for grid
        actionGridRect.offsetMax = new Vector2(0, 0);

        GridLayoutGroup actionLayout = actionGridObj.AddComponent<GridLayoutGroup>();
        actionLayout.cellSize = new Vector2(40, 40); // 40x40 squares
        actionLayout.spacing = new Vector2(10, 10);
        actionLayout.padding = new RectOffset(10, 10, 10, 10);
        
        TowerSelectionUI towerSelectionUI = actionGridObj.AddComponent<TowerSelectionUI>();
        var containerField = towerSelectionUI.GetType().GetField("towersContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (containerField != null) containerField.SetValue(towerSelectionUI, actionGridObj.transform);

        // Center Section: Unit Info (Selected Tower UI)
        GameObject centerSectionObj = new GameObject("CenterSectionBox");
        centerSectionObj.transform.SetParent(bottomBarObj.transform, false);
        Image centerBg = centerSectionObj.AddComponent<Image>();
        centerBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        RectTransform centerRect = centerSectionObj.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0, 0);
        centerRect.anchorMax = new Vector2(1, 1);
        centerRect.offsetMin = new Vector2(100, 0);   // After Minimap
        centerRect.offsetMax = new Vector2(-250, 0);  // Before Action Grid

        // Selected Tower UI Content (Turns off when nothing selected)
        GameObject selectedTowerUIObj = new GameObject("SelectedTowerUI");
        selectedTowerUIObj.transform.SetParent(centerSectionObj.transform, false);
        RectTransform stRect = selectedTowerUIObj.AddComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.offsetMin = Vector2.zero;
        stRect.offsetMax = Vector2.zero;

        // Name text
        GameObject nameTextObj = new GameObject("NameText");
        nameTextObj.transform.SetParent(selectedTowerUIObj.transform, false);
        TextMeshProUGUI stName = nameTextObj.AddComponent<TextMeshProUGUI>();
        stName.text = "Tower Name";
        stName.fontSize = 18;
        stName.alignment = TextAlignmentOptions.Top;
        RectTransform nameRect = nameTextObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(20, -30);
        nameRect.offsetMax = new Vector2(-20, -5);

        // Stats text
        GameObject statsTextObj = new GameObject("StatsText");
        statsTextObj.transform.SetParent(selectedTowerUIObj.transform, false);
        TextMeshProUGUI stStats = statsTextObj.AddComponent<TextMeshProUGUI>();
        stStats.text = "Damage: 0\nRange: 0\nSpeed: 0";
        stStats.fontSize = 14;
        stStats.alignment = TextAlignmentOptions.TopLeft;
        RectTransform statsRect = statsTextObj.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 0);
        statsRect.anchorMax = new Vector2(1, 1);
        statsRect.offsetMin = new Vector2(20, 10);
        statsRect.offsetMax = new Vector2(-20, -30);

        // Sell Button
        GameObject sellBtnObj = new GameObject("SellButton");
        sellBtnObj.transform.SetParent(selectedTowerUIObj.transform, false);
        Button sellBtn = sellBtnObj.AddComponent<Button>();
        Image sellBtnImg = sellBtnObj.AddComponent<Image>();
        sellBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 1f);
        RectTransform sellBtnRect = sellBtnObj.GetComponent<RectTransform>();
        sellBtnRect.anchorMin = new Vector2(1, 0);
        sellBtnRect.anchorMax = new Vector2(1, 0);
        sellBtnRect.offsetMin = new Vector2(-150, 10);
        sellBtnRect.offsetMax = new Vector2(-20, 40);

        // Sell Button Text
        GameObject sellBtnTextObj = new GameObject("Text");
        sellBtnTextObj.transform.SetParent(sellBtnObj.transform, false);
        TextMeshProUGUI sellBtnText = sellBtnTextObj.AddComponent<TextMeshProUGUI>();
        sellBtnText.text = "Sell";
        sellBtnText.fontSize = 16;
        sellBtnText.color = Color.white;
        sellBtnText.alignment = TextAlignmentOptions.Center;
        RectTransform sellBtnTextRect = sellBtnTextObj.GetComponent<RectTransform>();
        sellBtnTextRect.anchorMin = Vector2.zero;
        sellBtnTextRect.anchorMax = Vector2.one;
        sellBtnTextRect.offsetMin = Vector2.zero;
        sellBtnTextRect.offsetMax = Vector2.zero;

        // Upgrade Button
        GameObject upgBtnObj = new GameObject("UpgradeButton");
        upgBtnObj.transform.SetParent(selectedTowerUIObj.transform, false);
        Button upgBtn = upgBtnObj.AddComponent<Button>();
        Image upgBtnImg = upgBtnObj.AddComponent<Image>();
        upgBtnImg.color = new Color(0.2f, 0.6f, 0.2f, 1f); // Green
        RectTransform upgBtnRect = upgBtnObj.GetComponent<RectTransform>();
        upgBtnRect.anchorMin = new Vector2(1, 0);
        upgBtnRect.anchorMax = new Vector2(1, 0);
        upgBtnRect.offsetMin = new Vector2(-300, 10);
        upgBtnRect.offsetMax = new Vector2(-160, 40);

        // Upgrade Button Text
        GameObject upgBtnTextObj = new GameObject("Text");
        upgBtnTextObj.transform.SetParent(upgBtnObj.transform, false);
        TextMeshProUGUI upgBtnText = upgBtnTextObj.AddComponent<TextMeshProUGUI>();
        upgBtnText.text = "Upgrade";
        upgBtnText.fontSize = 16;
        upgBtnText.color = Color.white;
        upgBtnText.alignment = TextAlignmentOptions.Center;
        RectTransform upgBtnTextRect = upgBtnTextObj.GetComponent<RectTransform>();
        upgBtnTextRect.anchorMin = Vector2.zero;
        upgBtnTextRect.anchorMax = Vector2.one;
        upgBtnTextRect.offsetMin = Vector2.zero;
        upgBtnTextRect.offsetMax = Vector2.zero;

        SelectionUI stComponent = selectedTowerUIObj.AddComponent<SelectionUI>();
        var nameField = stComponent.GetType().GetField("nameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var statsField = stComponent.GetType().GetField("statsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var btnField = stComponent.GetType().GetField("sellButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var btnTextField = stComponent.GetType().GetField("sellButtonText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgBtnField = stComponent.GetType().GetField("upgradeButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var upgBtnTextField = stComponent.GetType().GetField("upgradeButtonText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var panelField = stComponent.GetType().GetField("panelObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (nameField != null) nameField.SetValue(stComponent, stName);
        if (statsField != null) statsField.SetValue(stComponent, stStats);
        if (btnField != null) btnField.SetValue(stComponent, sellBtn);
        if (btnTextField != null) btnTextField.SetValue(stComponent, sellBtnText);
        if (upgBtnField != null) upgBtnField.SetValue(stComponent, upgBtn);
        if (upgBtnTextField != null) upgBtnTextField.SetValue(stComponent, upgBtnText);
        if (panelField != null) panelField.SetValue(stComponent, selectedTowerUIObj);

        // ---------------- GAME OVER PANEL ----------------
        GameObject gameOverPanelObj = new GameObject("GameOverPanel");
        gameOverPanelObj.transform.SetParent(hudObj.transform, false);
        Image panelImage = gameOverPanelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f);
        RectTransform panelRect = gameOverPanelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        gameOverPanelObj.SetActive(false);

        // Game Over Text
        GameObject gameOverTextObj = new GameObject("GameOverText");
        gameOverTextObj.transform.SetParent(gameOverPanelObj.transform, false);
        TextMeshProUGUI gameOverText = gameOverTextObj.AddComponent<TextMeshProUGUI>();
        gameOverText.text = "GAME OVER";
        gameOverText.fontSize = 72;
        gameOverText.alignment = TextAlignmentOptions.Center;
        RectTransform gameOverRect = gameOverTextObj.GetComponent<RectTransform>();
        gameOverRect.anchorMin = new Vector2(0, 0.5f);
        gameOverRect.anchorMax = new Vector2(1, 0.5f);
        gameOverRect.offsetMin = new Vector2(0, 50);
        gameOverRect.offsetMax = new Vector2(0, 150);

        // Restart Button
        GameObject restartBtnObj = new GameObject("RestartButton");
        restartBtnObj.transform.SetParent(gameOverPanelObj.transform, false);
        Button restartBtn = restartBtnObj.AddComponent<Button>();
        Image restartBtnImg = restartBtnObj.AddComponent<Image>();
        restartBtnImg.color = new Color(0.2f, 0.6f, 0.2f, 1f); // Green
        RectTransform restartBtnRect = restartBtnObj.GetComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        restartBtnRect.offsetMin = new Vector2(-100, -50);
        restartBtnRect.offsetMax = new Vector2(100, 0);

        // Restart Button Text
        GameObject restartBtnTextObj = new GameObject("Text");
        restartBtnTextObj.transform.SetParent(restartBtnObj.transform, false);
        TextMeshProUGUI restartBtnText = restartBtnTextObj.AddComponent<TextMeshProUGUI>();
        restartBtnText.text = "Restart Game";
        restartBtnText.fontSize = 24;
        restartBtnText.color = Color.white;
        restartBtnText.alignment = TextAlignmentOptions.Center;
        RectTransform restartBtnTextRect = restartBtnTextObj.GetComponent<RectTransform>();
        restartBtnTextRect.anchorMin = Vector2.zero;
        restartBtnTextRect.anchorMax = Vector2.one;
        restartBtnTextRect.offsetMin = Vector2.zero;
        restartBtnTextRect.offsetMax = Vector2.zero;

        var gameOverPanelField = hudComponent.GetType().GetField("gameOverPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gameOverTextField = hudComponent.GetType().GetField("gameOverText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var restartBtnField = hudComponent.GetType().GetField("restartButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (gameOverPanelField != null) gameOverPanelField.SetValue(hudComponent, gameOverPanelObj);
        if (gameOverTextField != null) gameOverTextField.SetValue(hudComponent, gameOverText);
        if (restartBtnField != null) restartBtnField.SetValue(hudComponent, restartBtn);
    }

    private void CreateTagIfNotExists(string tag)
    {
        // Tags can only be created in the editor, at runtime we just make sure objects have the tags
        // This is a no-op at runtime
    }
}
