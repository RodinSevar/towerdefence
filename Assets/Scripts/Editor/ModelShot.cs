using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Model workflow: builds the low-poly models, saves the meshes to Assets/Models, and renders them from a few angles on a
/// plain ground to Screenshots/model_*.png. Needs a graphics device (run without -nographics):
/// Unity -batchmode -projectPath . -executeMethod ModelShot.Run -quit -logFile model.log
/// </summary>
public static class ModelShot
{
    private const int Width = 900, Height = 700;
    private const string ModelDir = "Assets/Models";
    private const string MaterialPath = "Assets/Materials/LowPoly.mat";

    public static void Run()
    {
        Directory.CreateDirectory(ModelDir);
        Directory.CreateDirectory("Screenshots");

        Mesh tower = Save(LowPolyModels.GuardTower());
        Material material = LowPolyMaterial();

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var light = Object.FindFirstObjectByType<Light>();
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(0.6f, 1, 0.6f);
        ground.GetComponent<Renderer>().sharedMaterial = MaterialWithColor(new Color(0.75f, 0.85f, 0.95f));

        var model = new GameObject("GuardTower", typeof(MeshFilter), typeof(MeshRenderer));
        model.GetComponent<MeshFilter>().sharedMesh = tower;
        model.GetComponent<MeshRenderer>().sharedMaterial = material;
        model.transform.localScale = Vector3.one * 2f; // the game scales a tower's model to its 2x2 footprint

        var camGo = new GameObject("ShotCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.65f, 0.8f);

        // the game camera looks down from about 55 degrees; also show a low side angle and the back
        Shot(cam, "model_guardtower_game", new Vector3(0, 6.5f, -6.5f), new Vector3(0, 1.4f, 0));
        Shot(cam, "model_guardtower_side", new Vector3(5.5f, 2.2f, -5.5f), new Vector3(0, 1.5f, 0));
        Shot(cam, "model_guardtower_back", new Vector3(-5.5f, 3.5f, 5.5f), new Vector3(0, 1.5f, 0));

        AssetDatabase.SaveAssets();
        Debug.Log("ModelShot: done.");
    }

    private static Mesh Save(Mesh mesh)
    {
        string path = $"{ModelDir}/{mesh.name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static Material LowPolyMaterial()
    {
        var shader = Shader.Find("Wintermaul/VertexColorLit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material MaterialWithColor(Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        return m;
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
