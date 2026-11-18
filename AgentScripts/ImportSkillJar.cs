using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class ImportSkillJar
{
    private const string Folder = "Assets/3d_models/SkillJar/";
    private const string PrefabPath = "Assets/Prefabs/SkillJar.prefab";

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before importing assets.");
        AssetDatabase.Refresh();
        var modelImporter = AssetImporter.GetAtPath(Folder + "SkillJar.fbx") as ModelImporter;
        if (modelImporter == null) throw new Exception("Jar model import missing.");
        modelImporter.bakeAxisConversion = true;
        modelImporter.importCameras = false;
        modelImporter.importLights = false;
        modelImporter.importAnimation = false;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        modelImporter.SaveAndReimport();

        Texture2D albedo = Texture("Jar_DirtyGlass_RGBA.png", true);
        Texture2D packed = Texture("Jar_MetallicSmoothness.png", false);
        var materials = new[] {
            Material("Jar_DirtyGlass", Color.white, albedo, packed),
            Material("Jar_InnerGlass", new Color(1, 1, 1, 0.42f), albedo, packed),
            Material("Jar_ThickRim", new Color(.72f, .76f, .68f, 3f), albedo, packed),
            Material("Jar_ThickBase", new Color(.75f, .79f, .72f, 2.7f), albedo, packed)
        };

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SkillJar.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            go.name = "SkillJar";
            go.transform.position = Vector3.zero;
            int triangles = 0;
            Bounds bounds = default;
            bool first = true;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                if (filter.sharedMesh.subMeshCount != materials.Length)
                    throw new Exception("Unexpected jar material slot count: " + filter.sharedMesh.subMeshCount);
                // FBX submeshes are ordered by first face use, not Blender's slot indices.
                var imported = renderer.sharedMaterials;
                var assigned = new UnityEngine.Material[imported.Length];
                for (int slot = 0; slot < imported.Length; slot++)
                {
                    if (imported[slot] == null) throw new Exception("Missing imported material identity.");
                    foreach (var material in materials)
                        if (imported[slot].name == material.name) assigned[slot] = material;
                    if (assigned[slot] == null) throw new Exception("Unknown FBX material: " + imported[slot].name);
                }
                renderer.sharedMaterials = assigned;
                // Transparent objects should not cast solid opaque silhouettes.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    triangles += (int)filter.sharedMesh.GetIndexCount(i) / 3;
            }
            if (first || bounds.size.y < .75f || bounds.size.y > .95f)
                throw new Exception("Unexpected imported Y-up dimensions: " + bounds.size);
            // Static mesh picking follows the actual surface and leaves the mouth hollow.
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var selection = filter.gameObject.AddComponent<MeshCollider>();
                selection.sharedMesh = filter.sharedMesh;
                selection.convex = false;
                selection.isTrigger = false;
            }
            go.layer = 0;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return $"Saved {PrefabPath}: {triangles} triangles, bounds {bounds.size}, 4 named URP transparent materials, 256px Point textures.";
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Texture2D Texture(string name, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(Folder + name) as TextureImporter;
        if (importer == null) throw new Exception("Missing texture " + name);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name);
    }

    private static UnityEngine.Material Material(string name, Color color, Texture2D albedo, Texture2D packed)
    {
        string path = Folder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader missing.");
        if (mat == null) { mat = new UnityEngine.Material(shader); AssetDatabase.CreateAsset(mat, path); }
        else mat.shader = shader;
        mat.SetTexture("_BaseMap", albedo);
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_MetallicGlossMap", packed);
        mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_BlendModePreserveSpecular", 1);
        mat.SetFloat("_SmoothnessTextureChannel", 0);
        bool opaque = false;
        mat.SetFloat("_Surface", opaque ? 0 : 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetFloat("_ZWrite", opaque ? 1 : 0);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetFloat("_SrcBlend", (float)(opaque ? BlendMode.One : BlendMode.SrcAlpha));
        mat.SetFloat("_DstBlend", (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetOverrideTag("RenderType", opaque ? "Opaque" : "Transparent");
        if (opaque) mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        else mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHAMODULATE_ON");
        mat.SetShaderPassEnabled("ShadowCaster", false);
        mat.SetShaderPassEnabled("DepthOnly", opaque);
        mat.renderQueue = (int)(opaque ? RenderQueue.Geometry : RenderQueue.Transparent);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
