using UnityEngine;

/// <summary>
/// The nine player slots of the map: colour, name and where each one starts (next to its creep spawn). Player ids in the game
/// are slot numbers, so a player's colour and start are the same in every game.
/// </summary>
public static class PlayerSlots
{
    public const int Count = 9;

    // Warcraft III player colours, in slot order
    public static readonly string[] ColorNames = { "Red", "Blue", "Teal", "Purple", "Yellow", "Orange", "Green", "Pink", "Gray" };

    public static readonly Color[] Colors =
    {
        new Color32(255, 3, 3, 255), new Color32(0, 66, 255, 255), new Color32(28, 230, 185, 255),
        new Color32(84, 0, 129, 255), new Color32(255, 252, 0, 255), new Color32(254, 138, 14, 255),
        new Color32(32, 192, 0, 255), new Color32(229, 91, 176, 255), new Color32(149, 150, 151, 255),
    };

    /// <summary>The spawners whose owner is "Something (Colour)" for this slot.</summary>
    private static bool Belongs(Spawner s, int slot)
    {
        return s.transform.parent != null && s.transform.parent.name.EndsWith("(" + ColorNames[slot] + ")");
    }

    /// <summary>Where the slot's player starts: the middle of that player's creep spawns.</summary>
    public static Vector3 StartPosition(int slot)
    {
        if (WaveManager.Instance == null) return Vector3.zero;
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var s in WaveManager.Instance.activeSpawners)
            if (Belongs(s, slot)) { sum += s.transform.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    /// <summary>The map region of the slot, for example "Top Left".</summary>
    public static string Region(int slot)
    {
        if (WaveManager.Instance != null)
            foreach (var s in WaveManager.Instance.activeSpawners)
                if (Belongs(s, slot))
                {
                    string name = s.transform.parent.name;
                    int paren = name.IndexOf('(');
                    return paren > 0 ? name.Substring(0, paren).Trim() : name;
                }
        return "";
    }

    public static string Label(int slot)
    {
        string region = Region(slot);
        return region.Length > 0 ? $"{ColorNames[slot]} - {region}" : ColorNames[slot];
    }
}
