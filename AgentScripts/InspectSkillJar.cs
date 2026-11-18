using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class InspectSkillJar
{
    public static object Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkillJar.prefab");
        var path = "Assets/3d_models/SkillJar/Jar_DirtyGlass_RGBA.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true; importer.SaveAndReimport();
        var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = image.GetPixels();
        var result = new {
            alphaMin = pixels.Min(x => x.a), alphaMax = pixels.Max(x => x.a), alphaMean = pixels.Average(x => x.a),
            renderers = prefab.GetComponentsInChildren<MeshRenderer>().Select(x => new {
                name=x.name, enabled=x.enabled, materials=x.sharedMaterials.Select(m=>new {
                    name=m.name, shader=m.shader.name, shaderSupported=m.shader.isSupported, queue=m.renderQueue,
                    alpha=m.GetColor("_BaseColor").a, texture=m.GetTexture("_BaseMap").name
                }).ToArray()
            }).ToArray()
        };
        importer.isReadable = false; importer.SaveAndReimport();
        return result;
    }
}
