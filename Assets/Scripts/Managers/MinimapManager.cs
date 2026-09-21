using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws creeps and towers on the minimap. All dots are painted into one small texture shown by a single RawImage,
/// instead of one UI Image per unit: with ~1800 creeps, per-unit UI elements cost several milliseconds a frame in
/// canvas rebuilds alone.
/// </summary>
public class MinimapManager : Singleton<MinimapManager>
{
    private const int TextureSize = 192;
    private const float RefreshInterval = 1f / 30f;

    [SerializeField] private RectTransform minimapContainer;

    // World range shown by the minimap: -gridHalfSize..gridHalfSize on both axes
    private float gridHalfSize = 98f;

    // Dot sizes in minimap UI units (as before): enemies slightly smaller than towers
    private const float EnemyDotUi = 3f, TowerDotUi = 4f;
    private static readonly Color32 EnemyColor = new Color32(255, 0, 0, 255);
    private static readonly Color32 TowerColor = new Color32(40, 60, 255, 255);

    private readonly List<Transform> enemies = new List<Transform>();
    private readonly List<Transform> towers = new List<Transform>();

    private Texture2D texture;
    private Color32[] pixels;
    private float timer;

    private void Start()
    {
        if (minimapContainer == null) return;

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        pixels = new Color32[TextureSize * TextureSize];

        var go = new GameObject("MinimapDots", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(minimapContainer, false);
        go.transform.SetAsFirstSibling(); // under the camera-view lines
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false; // clicks go to the minimap itself
    }

    public void RegisterUnit(Transform unitTransform, bool isEnemy)
    {
        (isEnemy ? enemies : towers).Add(unitTransform);
    }

    public void UnregisterUnit(Transform unitTransform)
    {
        // Swap-remove; order does not matter for drawing
        if (Remove(enemies, unitTransform)) return;
        Remove(towers, unitTransform);
    }

    private static bool Remove(List<Transform> list, Transform t)
    {
        int i = list.LastIndexOf(t);
        if (i < 0) return false;
        int last = list.Count - 1;
        list[i] = list[last];
        list.RemoveAt(last);
        return true;
    }

    private void Update()
    {
        if (texture == null) return;

        timer += Time.unscaledDeltaTime;
        if (timer < RefreshInterval) return;
        timer = 0f;

        System.Array.Clear(pixels, 0, pixels.Length);
        float pixelsPerUi = TextureSize / Mathf.Max(1f, minimapContainer.rect.width);
        DrawDots(enemies, EnemyColor, Mathf.Max(2, Mathf.RoundToInt(EnemyDotUi * pixelsPerUi)));
        DrawDots(towers, TowerColor, Mathf.Max(2, Mathf.RoundToInt(TowerDotUi * pixelsPerUi)));

        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    private void DrawDots(List<Transform> units, Color32 color, int size)
    {
        float scale = TextureSize / (2f * gridHalfSize);
        int half = size / 2;
        for (int i = units.Count - 1; i >= 0; i--)
        {
            Transform t = units[i];
            if (t == null) // destroyed without unregistering
            {
                units[i] = units[units.Count - 1];
                units.RemoveAt(units.Count - 1);
                continue;
            }

            Vector3 p = t.position;
            int cx = Mathf.RoundToInt((p.x + gridHalfSize) * scale) - half;
            int cy = Mathf.RoundToInt((p.z + gridHalfSize) * scale) - half;
            for (int y = cy; y < cy + size; y++)
            {
                if (y < 0 || y >= TextureSize) continue;
                int row = y * TextureSize;
                for (int x = cx; x < cx + size; x++)
                {
                    if (x < 0 || x >= TextureSize) continue;
                    pixels[row + x] = color;
                }
            }
        }
    }
}
