using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the generated terrain textures in Assets/Textures/Terrain (see tools/generate_terrain_textures.py):
/// seamless, so they repeat; mipmapped with anisotropic filtering so they hold up at the game's oblique camera angle;
/// files ending in "_n" are normal maps.
/// </summary>
public class TerrainTextureImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Textures/Terrain/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        var importer = (TextureImporter)assetImporter;
        bool isNormal = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_n");

        importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 512;
        if (!isNormal) importer.sRGBTexture = true;
    }
}
