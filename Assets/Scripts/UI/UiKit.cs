using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small helpers for building uGUI at runtime (menus that are created by code, not by the scene builder). Positions use the
/// same reference units as the rest of the UI: the canvas scales with screen height.
/// </summary>
public static class UiKit
{
    public static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.97f);
    public static readonly Color ButtonColor = new Color(0.16f, 0.20f, 0.34f);
    public static readonly Color GoodColor = new Color(0.2f, 0.55f, 0.25f);
    public static readonly Color BadColor = new Color(0.6f, 0.2f, 0.2f);

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return r;
    }

    /// <summary>Places a rect by anchors and pixel offsets from those anchors.</summary>
    public static RectTransform Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        r.anchorMin = aMin;
        r.anchorMax = aMax;
        r.offsetMin = oMin;
        r.offsetMax = oMax;
        return r;
    }

    /// <summary>A rect of a fixed size whose anchor is a corner or the centre: pivot and anchor are both <paramref name="anchor"/>.</summary>
    public static RectTransform Sized(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
    {
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = position;
        r.sizeDelta = size;
        return r;
    }

    public static Image Panel(string name, Transform parent, Color color)
    {
        var r = Rect(name, parent);
        var image = r.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color? color = null)
    {
        var r = Rect(name, parent);
        var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button Button(string name, Transform parent, string label, float fontSize, Color color, out TextMeshProUGUI text)
    {
        var image = Panel(name, parent, color);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;
        text = Label("Text", image.transform, label, fontSize, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    public static TMP_InputField Input(string name, Transform parent, string initial, string placeholder, int maxChars, float fontSize)
    {
        var image = Panel(name, parent, new Color(0.02f, 0.02f, 0.04f, 1f));
        var input = image.gameObject.AddComponent<TMP_InputField>();

        var area = Rect("Text Area", image.transform);
        Place(area, Vector2.zero, Vector2.one, new Vector2(8, 3), new Vector2(-8, -3));
        area.gameObject.AddComponent<RectMask2D>();

        var text = Label("Text", area, "", fontSize, TextAlignmentOptions.Left);
        Stretch(text.rectTransform);
        var hint = Label("Placeholder", area, placeholder, fontSize, TextAlignmentOptions.Left, new Color(1, 1, 1, 0.35f));
        Stretch(hint.rectTransform);

        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = hint;
        input.characterLimit = maxChars;
        input.text = initial;
        return input;
    }
}
