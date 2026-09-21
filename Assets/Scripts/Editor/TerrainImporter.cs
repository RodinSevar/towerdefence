using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the real terrain: war3map.w3e (heights, cliff levels, ramps, water, ground tiles) and war3map.wpm
/// (walkable / buildable) into a <see cref="TerrainMapData"/> asset. World units are the map's units divided by 64.
/// </summary>
public static class TerrainImporter
{
    public const string AssetPath = "Assets/Data/Terrain/WintermaulTerrain.asset";

    private const float WcUnitsPerTile = 128f;
    private const float WcLayerStep = 128f;        // one cliff level
    private const int BaseLayer = 2;               // the map's default level
    private const float WaterOffset = -89.6f;      // water height = (raw - 0x2000) / 4 - 89.6 (Warcraft III convention)

    [MenuItem("Tools/Import WC3 Terrain")]
    public static void ImportMenu()
    {
        Import();
    }

    /// <summary>Batch entry point.</summary>
    public static void ImportAndSave()
    {
        Import();
        AssetDatabase.SaveAssets();
        Debug.Log("TerrainImporter: done.");
    }

    public static TerrainMapData Import()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var data = AssetDatabase.LoadAssetAtPath<TerrainMapData>(AssetPath);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<TerrainMapData>();

        ReadTerrain(Wc3Data.MapFile("war3map.w3e"), data);
        ReadPathing(Wc3Data.MapFile("war3map.wpm"), data);

        if (isNew) AssetDatabase.CreateAsset(data, AssetPath);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        int ramps = 0, water = 0;
        foreach (byte f in data.cornerFlags)
        {
            if ((f & TerrainMapData.FlagRamp) != 0) ramps++;
            if ((f & TerrainMapData.FlagWater) != 0) water++;
        }
        Debug.Log($"TerrainImporter: {data.cornersX}x{data.cornersZ} corners, {data.cellsX}x{data.cellsZ} cells, " +
                  $"{ramps} ramp corners, {water} water corners, ground tiles [{string.Join(",", data.groundTileIds)}].");
        return data;
    }

    private static void ReadTerrain(string path, TerrainMapData data)
    {
        using (var r = new BinaryReader(File.OpenRead(path)))
        {
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "W3E!") throw new InvalidOperationException("Not a w3e file.");
            int version = r.ReadInt32();
            if (version != 11) throw new InvalidOperationException($"Unsupported w3e version {version} (expected 11).");
            r.ReadByte();          // tileset letter
            r.ReadInt32();         // custom tilesets flag

            data.groundTileIds = ReadIds(r);
            data.cliffTileIds = ReadIds(r);

            int cornersX = r.ReadInt32(), cornersZ = r.ReadInt32();
            float offsetX = r.ReadSingle(), offsetZ = r.ReadSingle();
            data.cornersX = cornersX;
            data.cornersZ = cornersZ;
            data.cellsX = (cornersX - 1) * 2;
            data.cellsZ = (cornersZ - 1) * 2;

            // The importer assumes the map is centred on the origin (offset = -half the map size).
            float expectedX = -(cornersX - 1) * WcUnitsPerTile / 2f, expectedZ = -(cornersZ - 1) * WcUnitsPerTile / 2f;
            if (Mathf.Abs(offsetX - expectedX) > 0.5f || Mathf.Abs(offsetZ - expectedZ) > 0.5f)
                throw new InvalidOperationException($"Map offset ({offsetX}, {offsetZ}) is not centred; expected ({expectedX}, {expectedZ}).");

            int n = cornersX * cornersZ;
            data.cornerHeight = new float[n];
            data.cornerLayer = new byte[n];
            data.cornerGround = new byte[n];
            data.cornerCliff = new byte[n];
            data.cornerFlags = new byte[n];
            data.cornerWater = new float[n];

            for (int i = 0; i < n; i++)
            {
                short groundRaw = r.ReadInt16();
                short waterRaw = r.ReadInt16();
                byte flagsAndGround = r.ReadByte();
                r.ReadByte();                          // ground texture variation
                byte cliffAndLayer = r.ReadByte();

                int layer = cliffAndLayer & 0x0F;
                float ground = (groundRaw - 0x2000) / 4f + (layer - BaseLayer) * WcLayerStep;
                data.cornerHeight[i] = ground / Wc3Data.WorldUnitsPerCell;
                data.cornerLayer[i] = (byte)layer;
                data.cornerGround[i] = (byte)(flagsAndGround & 0x0F);
                data.cornerFlags[i] = (byte)(flagsAndGround >> 4);
                data.cornerCliff[i] = (byte)(cliffAndLayer >> 4);
                data.cornerWater[i] = (((waterRaw & 0x3FFF) - 0x2000) / 4f + WaterOffset) / Wc3Data.WorldUnitsPerCell;
            }
            if (r.BaseStream.Position != r.BaseStream.Length)
                throw new InvalidOperationException("w3e parse did not consume the whole file; format assumption is wrong.");
        }
    }

    private static string[] ReadIds(BinaryReader r)
    {
        int count = r.ReadInt32();
        var ids = new string[count];
        for (int i = 0; i < count; i++) ids[i] = Encoding.ASCII.GetString(r.ReadBytes(4));
        return ids;
    }

    private static void ReadPathing(string path, TerrainMapData data)
    {
        byte[] file = File.ReadAllBytes(path);
        string magic = Encoding.ASCII.GetString(file, 0, 4);
        if (magic != "MP3W") throw new InvalidOperationException($"Not a wpm file (magic '{magic}').");
        int w = BitConverter.ToInt32(file, 8), h = BitConverter.ToInt32(file, 12);
        if (w != data.cellsX * 2 || h != data.cellsZ * 2)
            throw new InvalidOperationException($"Pathing map {w}x{h} does not match terrain cells {data.cellsX}x{data.cellsZ} (x2).");

        // Warcraft III pathing bits: 0x02 = not walkable, 0x08 = not buildable. Each 2x2 block of 32-unit pathing cells is
        // one 64-unit cell; the map is authored at that granularity, so the blocks are uniform and OR-ing loses nothing.
        data.fineX = w; data.fineZ = h;
        data.pathingFine = new byte[w * h];
        for (int i = 0; i < w * h; i++)
        {
            byte raw = file[16 + i], f = 0;
            if ((raw & 0x02) != 0) f |= TerrainMapData.PathBlocked;
            else if ((raw & 0x08) != 0) f |= TerrainMapData.PathNoBuild;
            data.pathingFine[i] = f;
        }

        data.cellPathing = new byte[data.cellsX * data.cellsZ];
        for (int cz = 0; cz < data.cellsZ; cz++)
        {
            for (int cx = 0; cx < data.cellsX; cx++)
            {
                byte combined = 0;
                for (int dz = 0; dz < 2; dz++)
                    for (int dx = 0; dx < 2; dx++)
                        combined |= file[16 + (cz * 2 + dz) * w + (cx * 2 + dx)];

                byte flags = 0;
                if ((combined & 0x02) != 0) flags |= TerrainMapData.PathBlocked;
                else if ((combined & 0x08) != 0) flags |= TerrainMapData.PathNoBuild;
                data.cellPathing[cz * data.cellsX + cx] = flags;
            }
        }
    }
}
