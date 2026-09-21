using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders the tower models of one race side by side at about the game camera angle. Needs graphics:
/// Unity -batchmode -projectPath . -executeMethod KitTowerShot.Run -quit -race 00_hbla
/// </summary>
public static class KitTowerShot
{
    public static void Run()
    {
        var args = System.Environment.GetCommandLineArgs();
        int ri = System.Array.IndexOf(args, "-race");
        string race = ri >= 0 ? args[ri + 1] : "00_hbla";
        var raceData = AssetDatabase.LoadAssetAtPath<RaceData>($"Assets/Data/Races/{race}.asset");

        Directory.CreateDirectory("Screenshots");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Object.FindFirstObjectByType<Light>().transform.rotation = Quaternion.Euler(50, -30, 0);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(2.4f, 1, 1f);
        var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        gm.SetColor("_BaseColor", new Color(0.78f, 0.86f, 0.95f));
        ground.GetComponent<Renderer>().sharedMaterial = gm;

        var list = new List<TowerData>(raceData.towers);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].modelPrefab == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(list[i].modelPrefab);
            go.transform.position = new Vector3((i - (list.Count - 1) / 2f) * 3.4f, 0, 0);
        }

        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.65f, 0.8f);
        cam.fieldOfView = 30;
        cam.transform.position = new Vector3(0, 12, -21);
        cam.transform.LookAt(new Vector3(0, 1.4f, 0));
        var rt = new RenderTexture(1800, 700, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1800, 700, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1800, 700), 0, 0);
        File.WriteAllBytes($"Screenshots/towers_{race}.png", tex.EncodeToPNG());
        Debug.Log("KitTowerShot: done.");
    }
}
