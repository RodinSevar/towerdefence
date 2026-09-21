using UnityEngine;

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
        
        if (FindAnyObjectByType<HUD>() == null)
            Debug.LogWarning("No UI in scene. Run Tools > Build UI in the editor.");
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
}
