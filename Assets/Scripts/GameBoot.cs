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

            cameraObj.transform.position = new Vector3(0, CameraController.DefaultHeight, -CameraController.DefaultHeight * 0.45f);
            cameraObj.transform.rotation = Quaternion.Euler(CameraController.DefaultPitch, 0, 0);
            
            // Set camera clipping planes
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f; // nothing worth drawing lies further than the map

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

        // The terrain lives in the scene (built by Tools > Build Terrain)
        if (FindAnyObjectByType<TerrainBuilder>() == null)
            Debug.LogError("No terrain in the scene. Run Tools > Build Terrain in the editor.");
        
        // Create GameManager if not exists
        if (FindAnyObjectByType<GameManager>() == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }

        if (FindAnyObjectByType<PlayerManager>() == null)
            new GameObject("PlayerManager").AddComponent<PlayerManager>();

        // The fixed-rate simulation clock drives all game logic
        if (FindAnyObjectByType<Simulation>() == null)
            new GameObject("Simulation").AddComponent<Simulation>();

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

        // Create EnemyManager if not exists (ticks all creeps and answers spatial queries)
        if (FindAnyObjectByType<EnemyManager>() == null)
        {
            GameObject emObj = new GameObject("EnemyManager");
            emObj.AddComponent<EnemyManager>();
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
