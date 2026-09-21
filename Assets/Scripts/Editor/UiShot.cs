using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Screenshots of the real UI in play mode: the main menu, the setup lobby, the race selector over the lobby, the running game and
/// the pause menu, at a normal and an ultra-wide aspect ratio. Needs a graphics device (run without -nographics):
///   Unity -batchmode -projectPath . -executeMethod UiShot.Run -logFile ui.log
/// Images go to Screenshots/ui_*.png. The canvas is switched to camera space so it can be rendered into a texture.
/// </summary>
[InitializeOnLoad]
public static class UiShot
{
    private const string ActiveKey = "UiShot.Active";
    private static int step, waitFrames;
    private static double startTime;
    private static Canvas canvas;

    static UiShot()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
    }

    public static void Run()
    {
        SessionState.SetBool(ActiveKey, true);
        var args = System.Environment.GetCommandLineArgs();
        int r = System.Array.IndexOf(args, "-res");
        if (r >= 0) PlayModeWindow.SetCustomRenderingResolution(uint.Parse(args[r + 1]), uint.Parse(args[r + 2]), "UiShot");
        Directory.CreateDirectory("Screenshots");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        Debug.Log("UISHOT done");
        EditorApplication.Exit(0);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup - startTime > 180) { Debug.Log("UISHOT timeout"); Finish(); return; }
        if (!EditorApplication.isPlaying || NetworkGame.Instance == null || Camera.main == null || Time.frameCount < 20) return;
        if (waitFrames-- > 0) return;

        var net = NetworkGame.Instance;

        switch (step)
        {
            case 0: Next(net, () => { net.BackToMenu(); Simulation.Running = false; }); break;
            case 1: Shot("ui_1_menu", 0, 0); Next(net, () => net.PlayOffline("Tester")); break;
            case 2: Shot("ui_2_lobby", 0, 0); Next(net, () => net.SelectSlot(4)); break;
            case 3: Shot("ui_3_lobby_full", 0, 0); Next(net, () => Object.FindFirstObjectByType<RacePanelUI>().Toggle()); break;
            case 4: Shot("ui_4_lobby_races", 0, 0); Next(net, () => { Object.FindFirstObjectByType<RacePanelUI>().Toggle(); net.StartGame(); }); break;
            case 5: Shot("ui_5_game", 0, 0); Next(net, () => Object.FindFirstObjectByType<PauseMenu>().Toggle()); break;
            case 6: Shot("ui_6_pause", 0, 0); Next(net, () => { }); break;
            case 7: Finish(); break;
        }
    }

    private static void Next(NetworkGame net, System.Action action)
    {
        action();
        step++;
        waitFrames = 15; // let the UI update
    }

    private static void Shot(string name, int _w, int _h)
    {
        // Render the camera and the UI (switched to camera space) into a texture of the game view size
        int width = Screen.width, height = Screen.height;
        var cam = Camera.main;
        canvas = Object.FindFirstObjectByType<Canvas>();
        var rt = new RenderTexture(width, height, 24);
        var oldMode = canvas.renderMode;
        var oldCam = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        cam.targetTexture = rt;
        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        File.WriteAllBytes($"Screenshots/{name}_{width}x{height}.png", tex.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.active = null;
        canvas.renderMode = oldMode;
        canvas.worldCamera = oldCam;
        Object.Destroy(rt);
        Object.Destroy(tex);
        Debug.Log($"UISHOT {name} {width}x{height}");
    }
}
