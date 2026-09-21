using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders screenshots of the terrain for visual checks. Needs a graphics device, so run without -nographics:
/// Unity -batchmode -projectPath . -executeMethod TerrainShot.Run -logFile shot.log
/// Images go to Screenshots/ (git-ignored).
/// </summary>
public static class TerrainShot
{
    private const int Width = 1600, Height = 900;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        TerrainBuilder builder = Object.FindFirstObjectByType<TerrainBuilder>();
        // Edit mode does not run Awake; the builder needs the grid singleton
        var gridGo = new GameObject("ShotGrid");
        var grid = gridGo.AddComponent<GridManager>();
        foreach (MonoBehaviour b in new MonoBehaviour[] { grid, builder })
            b.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(b, null);
        builder.EnsureBuilt();
        Directory.CreateDirectory("Screenshots");

        var camGo = new GameObject("ShotCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // (name, position, look-at)
        Shot(cam, "overview", new Vector3(0, 230, -150), new Vector3(0, 0, 0));
        Shot(cam, "top", new Vector3(0, 300, 0.1f), new Vector3(0, 0, 0));
        Shot(cam, "closeup_center", new Vector3(-20, 40, -50), new Vector3(0, 0, -20));
        Shot(cam, "closeup_cliff", new Vector3(-60, 30, 20), new Vector3(-40, 0, 40));
        Shot(cam, "exit_ship", new Vector3(0, 30, -80), new Vector3(0, 0, -96));

        Object.DestroyImmediate(camGo);
        Debug.Log("TerrainShot: done.");
    }

    private static void Shot(Camera cam, string name, Vector3 pos, Vector3 lookAt)
    {
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        File.WriteAllBytes($"Screenshots/{name}.png", tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
