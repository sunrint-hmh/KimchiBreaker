using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ApplyPSXRubberMaterial
{
    public static object Main()
    {
        const string folder="Assets/3d_models/RubberGloves/";
        var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"Glove_PSX_Source.png");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
        importer.maxTextureSize=128;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=0;
        importer.SaveAndReimport();
        string palettePath=folder+"Glove_PSX32_Palette.asset";
        var palette=AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath);
        if(palette==null)
        {
            palette=new Texture2D(8,4,TextureFormat.RGBA32,false,false);palette.name="Glove_PSX32_Palette";
            AssetDatabase.CreateAsset(palette,palettePath);
        }
        string[] hex={"16090D","2C1019","481825","651F31","872A41","AC3E52","CF5E6D","ED9194",
            "190909","31110F","501917","70221E","932F27","B74130","D85F46","F2906A",
            "120C09","24170F","3A2316","53321C","6F4426","8F5B33","B37B4B","D6A670",
            "1C1013","341E24","4F2F33","6E4245","905B5A","B07874","CF9D93","EDC9B7"};
        var colors=hex.Select(value=>
        {
            ColorUtility.TryParseHtmlString("#"+value,out Color color);
            return new Color(Mathf.Round(color.r*31)/31,Mathf.Round(color.g*31)/31,Mathf.Round(color.b*31)/31,1);
        }).ToArray();
        palette.SetPixels(colors);palette.Apply(false,false);palette.filterMode=FilterMode.Point;palette.wrapMode=TextureWrapMode.Clamp;palette.anisoLevel=0;
        AssetDatabase.SaveAssetIfDirty(palette);
        var shader=Shader.Find("KimchiBreaker/PSXRubber");
        if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("PSX rubber shader failed to import/compile.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Glove_WornPink.mat");
        material.shader=shader;material.shaderKeywords=Array.Empty<string>();
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Glove_PSX_Source.png"));
        material.SetTexture("_Palette",palette);material.SetFloat("_TexelGrid",128);material.SetFloat("_DitherStrength",.9f);material.SetFloat("_LightFloor",.65f);
        material.SetFloat("_AtlasSplit",.56f);
        material.renderQueue=2000;AssetDatabase.SaveAssetIfDirty(material);
        return new{textureResolution=128,paletteColors=palette.GetPixels32().Distinct().Count(),pointSampling=true,mipmaps=false,sceneSaved=false};
    }
}
