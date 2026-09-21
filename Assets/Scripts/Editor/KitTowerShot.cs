using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Renders the kit-assembled towers at game camera angle. Needs graphics: -executeMethod KitTowerShot.Run</summary>
public static class KitTowerShot
{
    public static void Run()
    {
        Directory.CreateDirectory("Screenshots");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Object.FindFirstObjectByType<Light>().transform.rotation = Quaternion.Euler(50, -30, 0);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(1.2f, 1, 1.2f);
        var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.SetColor("_BaseColor", new Color(0.75f, 0.85f, 0.95f));
        ground.GetComponent<Renderer>().sharedMaterial = gm;

        var towers = new[] { KitTowers.RoundKeep("RoundKeep"), KitTowers.Watchtower("Watchtower") };
        for (int i = 0; i < towers.Length; i++)
        {
            towers[i].transform.position = new Vector3((i - 0.5f) * 3.5f, 0, 0);
            towers[i].transform.localScale = Vector3.one * 1.8f;
        }

        Camera cam = Object.FindFirstObjectByType<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.65f, 0.8f);
        cam.fieldOfView = 30;
        cam.transform.position = new Vector3(0, 9, -11);
        cam.transform.LookAt(new Vector3(0, 2.6f, 0));
        var rt = new RenderTexture(1200, 800, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
        File.WriteAllBytes("Screenshots/kit_towers.png", tex.EncodeToPNG());
        Debug.Log("KitTowerShot: done.");
    }
}
