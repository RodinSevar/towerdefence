using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared coloured materials. Writing <c>renderer.material.color</c> gives every object its own material instance, and
/// Unity does not destroy those when the object dies, so spawning waves of creeps leaks one material per creep. Objects
/// that only need "this shape in colour X" share one material per (template, colour) instead.
/// </summary>
public static class MaterialCache
{
    private static readonly Dictionary<(int template, Color32 color), Material> cache =
        new Dictionary<(int, Color32), Material>();

    public static Material Get(Material template, Color color)
    {
        var key = (template.GetInstanceID(), (Color32)color);
        if (cache.TryGetValue(key, out Material material) && material != null) return material;

        material = new Material(template) { name = $"{template.name} {(Color32)color}", color = color };
        cache[key] = material;
        return material;
    }
}
