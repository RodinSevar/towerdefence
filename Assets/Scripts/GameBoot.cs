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

        // Create ground if not exists
        if (GameObject.FindWithTag("Ground") == null)
        {
            GameObject groundObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundObj.name = "Ground";
            groundObj.tag = "Ground";
            
            // A default Unity plane is 10x10 units. To make it 196x196, scale by 19.6.
            groundObj.transform.localScale = new Vector3(19.6f, 1f, 19.6f);
            groundObj.transform.position = Vector3.zero;

            MeshRenderer renderer = groundObj.GetComponent<MeshRenderer>();
            renderer.material.color = new Color(0.1f, 0.25f, 0.1f); // Set to dark green
        }

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

        // Create Gold Text
        GameObject goldTextObj = new GameObject("GoldText");
        goldTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI goldText = goldTextObj.AddComponent<TextMeshProUGUI>();
        goldText.text = "Gold: 500";
        goldText.fontSize = 36;
        RectTransform goldRect = goldTextObj.GetComponent<RectTransform>();
        goldRect.anchorMin = new Vector2(0, 1);
        goldRect.anchorMax = new Vector2(0, 1);
        goldRect.offsetMin = new Vector2(10, -50);
        goldRect.offsetMax = new Vector2(200, 0);

        // Create Lives Text
        GameObject livesTextObj = new GameObject("LivesText");
        livesTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI livesText = livesTextObj.AddComponent<TextMeshProUGUI>();
        livesText.text = "Lives: 20";
        livesText.fontSize = 36;
        RectTransform livesRect = livesTextObj.GetComponent<RectTransform>();
        livesRect.anchorMin = new Vector2(0.5f, 1);
        livesRect.anchorMax = new Vector2(0.5f, 1);
        livesRect.offsetMin = new Vector2(-100, -50);
        livesRect.offsetMax = new Vector2(100, 0);

        // Create Wave Text
        GameObject waveTextObj = new GameObject("WaveText");
        waveTextObj.transform.SetParent(hudObj.transform, false);
        TextMeshProUGUI waveText = waveTextObj.AddComponent<TextMeshProUGUI>();
        waveText.text = "Wave: 0 / 5";
        waveText.fontSize = 36;
        RectTransform waveRect = waveTextObj.GetComponent<RectTransform>();
        waveRect.anchorMin = new Vector2(1, 1);
        waveRect.anchorMax = new Vector2(1, 1);
        waveRect.offsetMin = new Vector2(-250, -50);
        waveRect.offsetMax = new Vector2(0, 0);

        // Use reflection to set private fields (alternative to serialized fields)
        var goldField = hudComponent.GetType().GetField("goldText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var livesField = hudComponent.GetType().GetField("livesText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var waveField = hudComponent.GetType().GetField("waveText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (goldField != null) goldField.SetValue(hudComponent, goldText);
        if (livesField != null) livesField.SetValue(hudComponent, livesText);
        if (waveField != null) waveField.SetValue(hudComponent, waveText);

        // Create Game Over Panel
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

        // Create Game Over Text
        GameObject gameOverTextObj = new GameObject("GameOverText");
        gameOverTextObj.transform.SetParent(gameOverPanelObj.transform, false);
        TextMeshProUGUI gameOverText = gameOverTextObj.AddComponent<TextMeshProUGUI>();
        gameOverText.text = "GAME OVER";
        gameOverText.fontSize = 72;
        gameOverText.alignment = TextAlignmentOptions.Center;
        RectTransform gameOverRect = gameOverTextObj.GetComponent<RectTransform>();
        gameOverRect.anchorMin = Vector2.zero;
        gameOverRect.anchorMax = Vector2.one;
        gameOverRect.offsetMin = Vector2.zero;
        gameOverRect.offsetMax = Vector2.zero;

        var gameOverPanelField = hudComponent.GetType().GetField("gameOverPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gameOverTextField = hudComponent.GetType().GetField("gameOverText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (gameOverPanelField != null) gameOverPanelField.SetValue(hudComponent, gameOverPanelObj);
        if (gameOverTextField != null) gameOverTextField.SetValue(hudComponent, gameOverText);

        // Create Tower Selection UI
        GameObject towerUIObj = new GameObject("TowerSelectionUI");
        towerUIObj.transform.SetParent(canvas.transform, false);
        RectTransform towerUIRect = towerUIObj.AddComponent<RectTransform>();
        towerUIRect.anchorMin = new Vector2(1, 0);
        towerUIRect.anchorMax = new Vector2(1, 1);
        towerUIRect.offsetMin = new Vector2(-160, 10);
        towerUIRect.offsetMax = new Vector2(-10, -10);

        VerticalLayoutGroup layoutGroup = towerUIObj.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.spacing = 5;
        layoutGroup.padding = new RectOffset(5, 5, 5, 5);

        TowerSelectionUI towerSelectionUI = towerUIObj.AddComponent<TowerSelectionUI>();

        var containerField = towerSelectionUI.GetType().GetField("towersContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (containerField != null) containerField.SetValue(towerSelectionUI, towerUIObj.transform);
    }

    private void CreateTagIfNotExists(string tag)
    {
        // Tags can only be created in the editor, at runtime we just make sure objects have the tags
        // This is a no-op at runtime
    }
}
