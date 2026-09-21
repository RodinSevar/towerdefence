using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The in-game menu: opened by the Menu button (top left), F10, or Esc when nothing else is using Esc. Resume, or exit the game.
/// Offline it pauses the game; in a network game the game keeps running for everyone else.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private GameObject root;
    private bool pausedByMenu;
    private TextMeshProUGUI noteText;
    private RacePanelUI racePanel;

    private void Start()
    {
        var limiter = FindAnyObjectByType<UiWidthLimiter>();
        if (limiter == null) { enabled = false; return; }

        racePanel = FindAnyObjectByType<RacePanelUI>();
        root = BuildPanel(limiter.Frame);
        root.SetActive(false);

        var menuButton = GameObject.Find("MenuButton_Menu");
        if (menuButton != null && menuButton.TryGetComponent(out Button button)) button.onClick.AddListener(Toggle);
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f10Key.wasPressedThisFrame) { Toggle(); return; }
        if (!keyboard.escapeKey.wasPressedThisFrame) return;

        if (IsOpen) Toggle();
        else if (!GameInput.Blocked && !(TowerManager.Instance != null && TowerManager.Instance.IsPlacingTower())
                 && !(PlayerInteraction.Instance != null && PlayerInteraction.Instance.HasSelection)
                 && !(racePanel != null && racePanel.IsOpen))
            Toggle();
    }

    public void Toggle()
    {
        if (root == null) return;
        if (!IsOpen && NetworkGame.Instance != null && NetworkGame.Instance.MenuOpen) return; // the setup screen has its own buttons

        IsOpen = !IsOpen;
        root.SetActive(IsOpen);
        if (IsOpen)
        {
            root.transform.SetAsLastSibling();
            noteText.text = NetworkGame.Active ? "The game keeps running for the other players." : "The game is paused.";
        }

        // A single-player game pauses while the menu is open; a network game cannot
        if (IsOpen && !NetworkGame.Active && Simulation.Running) { Simulation.Running = false; pausedByMenu = true; }
        else if (!IsOpen && pausedByMenu) { Simulation.Running = true; pausedByMenu = false; }
    }

    private GameObject BuildPanel(RectTransform frame)
    {
        var overlay = UiKit.Panel("PauseMenu", frame, new Color(0, 0, 0, 0.6f));
        UiKit.Stretch(overlay.rectTransform);
        overlay.rectTransform.offsetMin = new Vector2(-6000f, 0f);
        overlay.rectTransform.offsetMax = new Vector2(6000f, 0f);

        var panel = UiKit.Panel("Panel", overlay.transform, UiKit.PanelColor);
        UiKit.Sized(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 230));

        var title = UiKit.Label("Title", panel.transform, "Menu", 26, TextAlignmentOptions.Center);
        UiKit.Sized(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(260, 36));

        var resume = UiKit.Button("ResumeButton", panel.transform, "Resume", 20, UiKit.GoodColor, out _);
        UiKit.Sized((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(240, 44));
        resume.onClick.AddListener(Toggle);

        var exit = UiKit.Button("ExitButton", panel.transform, "Exit game", 20, UiKit.BadColor, out _);
        UiKit.Sized((RectTransform)exit.transform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(240, 44));
        exit.onClick.AddListener(NetworkGame.QuitApplication);

        var note = UiKit.Label("Note", panel.transform, "", 13, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.6f));
        noteText = note;
        UiKit.Sized(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(260, 24));
        return overlay.gameObject;
    }
}
