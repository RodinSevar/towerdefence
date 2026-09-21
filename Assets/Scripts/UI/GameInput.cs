/// <summary>
/// Whether the game world should react to keyboard and mouse. It must not while a menu or the setup screen is up, otherwise
/// typing a name would move the camera or a click on a menu would place a tower.
/// </summary>
public static class GameInput
{
    public static bool Blocked =>
        (NetworkGame.Instance != null && NetworkGame.Instance.MenuOpen) || PauseMenu.IsOpen;
}
