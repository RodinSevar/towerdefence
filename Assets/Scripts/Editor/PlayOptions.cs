using UnityEditor;

/// <summary>Editor convenience: start Play straight into an offline game instead of the main menu.</summary>
public static class PlayOptions
{
    private const string MenuPath = "Tools/Skip Menu On Play";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(NetworkGame.SkipMenuPref, !EditorPrefs.GetBool(NetworkGame.SkipMenuPref, false));
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(NetworkGame.SkipMenuPref, false));
        return true;
    }
}
