using UnityEngine;

public class SceneSetup : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod]
    public static void SetupScene()
    {
        // This will automatically run when the scene loads
    }

    private void OnEnable()
    {
        SetupSceneEnvironment();
    }

    private static void SetupSceneEnvironment()
    {
        // Create camera if not exists
        if (Camera.main == null)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0, 15, -10);
            cameraObj.transform.rotation = Quaternion.Euler(45, 0, 0);
        }

        // Create ground if not exists
        if (GameObject.FindWithTag("Ground") == null)
        {
            GameObject groundObj = new GameObject("Ground");
            groundObj.tag = "Ground";
            
            Mesh groundMesh = new Mesh();
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-20, 0, -20),
                new Vector3(20, 0, -20),
                new Vector3(-20, 0, 20),
                new Vector3(20, 0, 20)
            };
            
            int[] triangles = new int[6] { 0, 2, 1, 1, 2, 3 };
            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            groundMesh.vertices = vertices;
            groundMesh.triangles = triangles;
            groundMesh.uv = uv;
            groundMesh.RecalculateNormals();
            
            MeshFilter meshFilter = groundObj.AddComponent<MeshFilter>();
            meshFilter.mesh = groundMesh;
            
            MeshCollider collider = groundObj.AddComponent<MeshCollider>();
            collider.sharedMesh = groundMesh;
            
            MeshRenderer renderer = groundObj.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.8f, 0.8f, 0.8f);
        }

        // Create spawn point if not exists
        if (GameObject.FindWithTag("SpawnPoint") == null)
        {
            GameObject spawnObj = new GameObject("SpawnPoint");
            spawnObj.tag = "SpawnPoint";
            spawnObj.transform.position = new Vector3(-15, 0.5f, 0);
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
}
