using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyPSXRubber
{
    public static object Main()
    {
        int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        const string folder="Assets/3d_models/RubberGloves/";
        var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Glove_WornPink.mat");
        Check(material.shader.name=="KimchiBreaker/PSXRubber" && !ShaderUtil.ShaderHasError(material.shader),"PSX shader compiles");
        var albedo=(Texture2D)material.GetTexture("_BaseMap");var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(albedo));
        Check(albedo.width==128 && albedo.height==128,"Actual imported 128px atlas");
        Check(importer.filterMode==FilterMode.Point && !importer.mipmapEnabled,"Sharp Point sampling without mip blur");
        Check(importer.sRGBTexture && importer.textureCompression==TextureImporterCompression.Uncompressed,"Color space and no block compression artifacts");
        var palette=(Texture2D)material.GetTexture("_Palette");
        Check(palette.GetPixels32().Distinct().Count()==32,"Exactly 32 palette entries");
        Check(palette.filterMode==FilterMode.Point && palette.mipmapCount==1,"No palette interpolation");
        Check(material.GetFloat("_TexelGrid")==128 && material.GetFloat("_DitherStrength")>0,"Dithering and texel grid configured");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LocalRubberGloveHands.prefab");
        foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            Check(renderer.sharedMaterials.Contains(material),"Both existing hands consume the updated material");
        var preview=new PreviewRenderUtility();
        try
        {
            var holder=new GameObject("PSXRubberPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var hands=UnityEngine.Object.Instantiate(prefab,holder.transform,false);hands.GetComponent<LocalRubberGloveHands>().enabled=false;
            holder.SetActive(true);
            preview.camera.cameraType=CameraType.Game;preview.camera.transform.position=Vector3.zero;preview.camera.transform.rotation=Quaternion.identity;
            preview.camera.fieldOfView=60;preview.camera.nearClipPlane=.3f;preview.camera.farClipPlane=30;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.12f,.085f);
            preview.lights[0].intensity=1.1f;preview.lights[0].color=new Color(1,.89f,.76f);preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);
            preview.lights[1].intensity=.35f;preview.lights[1].color=new Color(.7f,.8f,1);preview.lights[1].transform.rotation=Quaternion.Euler(20,150,0);
            preview.ambientColor=new Color(.24f,.26f,.22f);
            preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var old=RenderTexture.active;var temp=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);Texture2D output=null;
            try
            {
                Graphics.Blit(texture,temp);RenderTexture.active=temp;
                output=new Texture2D(1280,720,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();
                File.WriteAllBytes("AgentScripts/RubberGloves-PSX.png",output.EncodeToPNG());
                var pixels=output.GetPixels32();int nonBackground=pixels.Count(p=>p.r>p.g*1.3f && p.r>p.b*1.1f);
                Check(nonBackground>5000,"Gloves render visible red-pink texels, not a missing/black shader");
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(temp);if(output!=null)UnityEngine.Object.DestroyImmediate(output);}
            return new{checksPassed=checks,palette=32,textureGrid=128,sceneSaved=false,preview="AgentScripts/RubberGloves-PSX.png"};
        }
        finally{preview.Cleanup();}
    }
}
