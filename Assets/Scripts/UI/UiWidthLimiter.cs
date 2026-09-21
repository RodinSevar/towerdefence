using UnityEngine;

/// <summary>
/// Keeps the UI in the middle of very wide screens. The game view stretches across the whole screen, but the HUD lives inside a
/// centred frame no wider than <see cref="maxAspect"/> times the screen height. Put this on the Canvas: at start it moves every
/// child into the frame, so anchors that used to mean "screen edge" now mean "edge of the frame".
/// </summary>
[RequireComponent(typeof(Canvas))]
public class UiWidthLimiter : MonoBehaviour
{
    [Tooltip("Widest the UI may get, as width / height (16:9 = 1.78)")]
    [SerializeField] private float maxAspect = 16f / 9f;

    private RectTransform canvasRect;
    private RectTransform frame;
    private Vector2 lastSize;

    /// <summary>The centred frame that holds the UI.</summary>
    public RectTransform Frame => frame;

    private void Awake()
    {
        canvasRect = (RectTransform)transform;
        frame = UiKit.Rect("UIFrame", transform);
        frame.SetAsFirstSibling();

        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            if (child != frame) children.Add(child);
        foreach (var child in children) child.SetParent(frame, false);

        Apply();
    }

    private void Update()
    {
        if (canvasRect.rect.size != lastSize) Apply();
    }

    private void Apply()
    {
        lastSize = canvasRect.rect.size;
        float width = Mathf.Min(lastSize.x, lastSize.y * maxAspect);
        frame.anchorMin = new Vector2(0.5f, 0f);
        frame.anchorMax = new Vector2(0.5f, 1f);
        frame.pivot = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = Vector2.zero;
        frame.sizeDelta = new Vector2(width, 0f);
    }
}
