using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Contact sheets of the imported Kenney kit pieces (names + sizes logged in grid order, row by row, left to right).
/// Needs graphics: Unity -batchmode -projectPath . -executeMethod KitShot.Run -quit -logFile kit.log
/// </summary>
public static class KitShot
{
    private const string KitDir = "Assets/ThirdParty/Kenney/RetroFantasy";
    private const int PerSheet = 20, Columns = 5, Width = 1500, Height = 1000;

    public static void Run()
    {
        Directory.CreateDirectory("Screenshots");
        string[] names = Directory.GetFiles(KitDir, "*.fbx").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Object.FindFirstObjectByType<Light>().transform.rotation = Quaternion.Euler(50, -30, 0);
        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.5f, 0.58f);
        cam.fieldOfView = 25;

        const float spacing = 3f;
        for (int s = 0; s * PerSheet < names.Length; s++)
        {
            var spawned = new List<GameObject>();
            var log = new List<string>();
            int count = Mathf.Min(PerSheet, names.Length - s * PerSheet);
            for (int i = 0; i < count; i++)
            {
                string name = names[s * PerSheet + i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitDir}/{name}.fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3((i % Columns) * spacing, 0, -(i / Columns) * spacing);
                spawned.Add(go);
                var b = go.GetComponentInChildren<Renderer>().bounds;
                var mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                log.Add($"{i}:{name} size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) tris={mesh.triangles.Length / 3}");
            }
            Debug.Log($"KitShot sheet {s}: " + string.Join(" | ", log));

            float cx = (Columns - 1) * spacing / 2f, cz = -((count - 1) / Columns) * spacing / 2f;
            cam.transform.position = new Vector3(cx, 22, cz - 20);
            cam.transform.LookAt(new Vector3(cx, 0, cz));
            var rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            File.WriteAllBytes($"Screenshots/kit_{s}.png", tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.active = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            foreach (var g in spawned) Object.DestroyImmediate(g);
        }
        Debug.Log("KitShot: done.");
    }
}
