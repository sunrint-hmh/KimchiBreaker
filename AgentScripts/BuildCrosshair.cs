using UnityEngine;
using UnityEditor;
using System.IO;

public static class BuildCrosshair
{
    public static string Main()
    {
        const string path = "Assets/UI/CrosshairRing.png";
        Directory.CreateDirectory("Assets/UI");
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
            float alpha = Mathf.Clamp01(30f - distance) * Mathf.Clamp01(distance - 25f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
            throw new System.InvalidOperationException("Circle sprite import failed.");
        return path;
    }
}
