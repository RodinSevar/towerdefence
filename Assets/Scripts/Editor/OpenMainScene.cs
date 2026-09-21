using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// When the editor starts with an empty, unsaved scene (Unity forgets the last scene after a headless run that opened none, such as
/// a compile check), open the main scene instead, so the game view is not blank and Play works.
/// </summary>
[InitializeOnLoad]
public static class OpenMainScene
{
    private const string MainScene = "Assets/Scenes/SampleScene.unity";

    static OpenMainScene()
    {
        if (UnityEngine.Application.isBatchMode) return;
        EditorApplication.delayCall += Check;
    }

    private static void Check()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene active = SceneManager.GetActiveScene();
        bool blank = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && active.rootCount <= 2; // camera + light of an untitled scene
        if (blank) EditorSceneManager.OpenScene(MainScene);
    }
}
