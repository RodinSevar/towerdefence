using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Readers for the extracted Warcraft III map files in mpq_files/, shared by the wave and tower importers.
/// </summary>
internal static class Wc3Data
{
    public const string MapDir = "mpq_files";

    // One Unity grid cell is 2 WC3 pathing cells of 32 units (see WPMImporter), i.e. 64 WC3 units.
    public const float WorldUnitsPerCell = 64f;

    public static string MapFile(string name) => Path.Combine(MapDir, name);

    // ---------- war3map.w3u ----------

    /// <summary>
    /// Reads the unit object table. Returns modified fields keyed by unit id (custom id if it has one, else the
    /// original id that was modified). File layout (version 2): two tables (original-modified, then custom), each a
    /// count followed by objects: oldId, newId, modCount, then per mod: fieldId, type (0 int, 1/2 real, 3 string),
    /// value, and a 4-byte end marker.
    /// </summary>
    public static Dictionary<string, Dictionary<string, object>> ParseUnits(string path)
    {
        var units = new Dictionary<string, Dictionary<string, object>>();
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            int version = r.ReadInt32();
            if (version != 2) throw new InvalidOperationException($"Unsupported w3u version {version} (expected 2).");

            for (int table = 0; table < 2; table++)
            {
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string oldId = ReadId(r);
                    string newId = ReadId(r);
                    int modCount = r.ReadInt32();
                    var fields = new Dictionary<string, object>();
                    for (int m = 0; m < modCount; m++)
                    {
                        string fieldId = ReadId(r);
                        int type = r.ReadInt32();
                        object value;
                        switch (type)
                        {
                            case 0: value = r.ReadInt32(); break;
                            case 1:
                            case 2: value = r.ReadSingle(); break;
                            case 3: value = ReadCString(r); break;
                            default: throw new InvalidOperationException($"Unknown w3u value type {type}");
                        }
                        r.ReadInt32(); // end marker
                        fields[fieldId] = value;
                    }
                    units[newId ?? oldId] = fields;
                }
            }
            if (r.BaseStream.Position != r.BaseStream.Length)
                throw new InvalidOperationException("w3u parse did not consume the whole file; format assumption is wrong.");
        }
        return units;
    }

    private static string ReadId(BinaryReader r)
    {
        byte[] b = r.ReadBytes(4);
        if (b[0] == 0 && b[1] == 0 && b[2] == 0 && b[3] == 0) return null;
        return Encoding.ASCII.GetString(b);
    }

    private static string ReadCString(BinaryReader r)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = r.ReadByte()) != 0) bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    // ---------- war3map.wts ----------

    public static Dictionary<int, string> ParseStrings(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8).Replace("\r", "").TrimStart('﻿');
        var strings = new Dictionary<int, string>();
        foreach (Match m in Regex.Matches(text, @"STRING (\d+)\n(?://[^\n]*\n)*\{\n(.*?)\n\}", RegexOptions.Singleline))
            strings[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
        return strings;
    }

    /// <summary>Resolves a "TRIGSTR_n" reference through the string table; other values pass through.</summary>
    public static string ResolveString(string value, Dictionary<int, string> strings)
    {
        if (value != null && value.StartsWith("TRIGSTR_") && int.TryParse(value.Substring(8), out int idx)
            && strings.TryGetValue(idx, out string s))
            return s;
        return value;
    }

    /// <summary>Converts WC3 text markup (|n, |cAARRGGBB ... |r) to TextMeshPro rich text.</summary>
    public static string ToRichText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("|n", "\n");
        s = Regex.Replace(s, @"\|c[0-9a-fA-F]{2}([0-9a-fA-F]{6})", "<color=#$1>");
        return s.Replace("|r", "</color>");
    }

    // ---------- field helpers ----------

    public static string GetString(Dictionary<string, object> f, string key)
        => f.TryGetValue(key, out var v) ? v as string : null;

    /// <summary>Reads a numeric field, or returns the fallback and records what was assumed.</summary>
    public static double GetNumber(Dictionary<string, object> f, string key, double fallback, List<string> notes, string assumption)
    {
        if (f.TryGetValue(key, out var v)) return Convert.ToDouble(v);
        if (assumption != null) notes.Add(assumption);
        return fallback;
    }
}
