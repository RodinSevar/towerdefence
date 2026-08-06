using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class W3RImporter
{
    [MenuItem("Tools/Dump WC3 Regions (.w3r)")]
    public static void DumpW3R()
    {
        string path = "mpq_files/war3map.w3r";
        if (!File.Exists(path))
        {
            Debug.LogError("Could not find war3map.w3r in the mpq_files folder.");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        int offset = 0;

        if (data.Length < 8) return;

        int version = System.BitConverter.ToInt32(data, offset); offset += 4;
        int numRegions = System.BitConverter.ToInt32(data, offset); offset += 4;

        Debug.Log($"Found {numRegions} regions in war3map.w3r (version {version})");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Warcraft 3 Regions Dump ({numRegions} total) ---");
        sb.AppendLine("This file contains every region placed in the Warcraft 3 map, translated to Unity coordinates.");
        sb.AppendLine();

        int parsed = 0;
        try
        {
            for (int i = 0; i < numRegions; i++)
            {
                if (offset >= data.Length) break;

                float left = System.BitConverter.ToSingle(data, offset); offset += 4;
                float bottom = System.BitConverter.ToSingle(data, offset); offset += 4;
                float right = System.BitConverter.ToSingle(data, offset); offset += 4;
                float top = System.BitConverter.ToSingle(data, offset); offset += 4;

                string name = ReadNullTerminatedString(data, ref offset);
                
                int index = System.BitConverter.ToInt32(data, offset); offset += 4;
                
                // Weather ID is 4 bytes
                offset += 4;
                
                string ambientSound = ReadNullTerminatedString(data, ref offset);
                
                // Color is 4 bytes (R, G, B, 0xFF)
                offset += 4;

                float centerX = (left + right) / 2f;
                float centerZ = (bottom + top) / 2f;
                
                Vector3 unityPos = new Vector3(centerX / 64f, 1f, centerZ / 64f);

                sb.AppendLine($"Region Name: {name}");
                sb.AppendLine($"Unity Pos:   X: {unityPos.x:F2}, Z: {unityPos.z:F2}");
                sb.AppendLine($"WC3 Bounds:  L:{left} R:{right} B:{bottom} T:{top}");
                sb.AppendLine("--------------------------------------------------");
                parsed++;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error parsing after {parsed} regions: {e.Message}");
        }

        File.WriteAllText("region_dump.txt", sb.ToString());
        AssetDatabase.Refresh();
        
        Debug.Log("Successfully dumped regions to region_dump.txt!");
    }

    private static string ReadNullTerminatedString(byte[] data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && data[offset] != 0)
        {
            offset++;
        }
        int length = offset - start;
        string result = System.Text.Encoding.UTF8.GetString(data, start, length);
        offset++; // Skip the null byte
        return result;
    }
}
