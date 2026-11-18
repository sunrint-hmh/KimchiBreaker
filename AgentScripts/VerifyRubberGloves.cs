using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class VerifyRubberGloves
{
    public static object Main()
    {
        int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        var rest=new Pose(new Vector3(-.22f,-.25f,.62f),Quaternion.Euler(18,8,6));
        var motion=new GloveSwingMotion();
        motion.Begin(rest,rest,new Vector3(0,0,3),1,0,.055f,.16f);
        check(Vector3.Distance(motion.Evaluate(0).position,rest.position)<.00001f,"No initial teleport");
        var peak=motion.Evaluate(.055f);
        check(peak.position.z>rest.position.z && peak.position.y>rest.position.y,"Thrust toward aim");
        check(Vector3.Distance(peak.position,rest.position)<=.38001f,"Bounded travel");
        check(motion.IsActive(.1f) && !motion.IsActive(.22f),"Finite swing duration");
        var recovered=motion.Evaluate(1);
        check(Vector3.Distance(recovered.position,rest.position)<.00001f && Quaternion.Angle(recovered.rotation,rest.rotation)<.001f,"Returns exactly to idle");
        var interrupted=motion.Evaluate(.1f);
        motion.Begin(interrupted,rest,new Vector3(.5f,.2f,3),9,.1f,.055f,.16f);
        check(Vector3.Distance(motion.Evaluate(.1f).position,interrupted.position)<.00001f,"Fast clicks start from current pose");
        for(int i=0;i<100;i++)motion.Begin(motion.Evaluate(i*.02f),rest,new Vector3(0,0,3),i%8+1,i*.02f,.055f,.16f);
        check(Vector3.Distance(motion.Evaluate(10).position,rest.position)<.00001f,"No rapid-click position drift");
        motion.Begin(rest,rest,Vector3.zero,1,0,.055f,.16f);
        check(!float.IsNaN(motion.Evaluate(.055f).position.x),"Zero target safe");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LocalRubberGloveHands.prefab");
        check(prefab!=null && prefab.activeSelf,"Active prefab");
        check(prefab.GetComponentsInChildren<Collider>(true).Length==0,"No hand colliders");
        check(prefab.GetComponentsInChildren<Transform>(true).All(t=>t.gameObject.layer==2),"Excluded from click rays");
        check(prefab.GetComponent<LocalRubberGloveHands>().LeftHand!=null && prefab.GetComponent<LocalRubberGloveHands>().RightHand!=null,"Both hand references wired");
        foreach(string map in new[]{"Albedo","Normal","Roughness","MetallicSmoothness"})
        {
            string path="Assets/3d_models/RubberGloves/Glove_WornPink_"+map+".png";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            check(tex.width==256 && tex.height==256,"Low-resolution atlas "+map);
            check(importer.filterMode==FilterMode.Point && importer.textureCompression==TextureImporterCompression.Uncompressed,"Point/uncompressed "+map);
            check(importer.sRGBTexture==(map=="Albedo"),"Correct color space "+map);
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/3d_models/RubberGloves/Glove_WornPink.mat");
        check(material.GetFloat("_Metallic")==0 && material.GetFloat("_Surface")==0,"Opaque non-metallic rubber");
        var preview=new PreviewRenderUtility();
        try
        {
            var holder=new GameObject("HandAssetPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var hands=UnityEngine.Object.Instantiate(prefab,holder.transform,false);hands.GetComponent<LocalRubberGloveHands>().enabled=false;
            holder.SetActive(true);
            preview.camera.cameraType=CameraType.Game;preview.camera.transform.position=Vector3.zero;preview.camera.transform.rotation=Quaternion.identity;
            preview.camera.fieldOfView=60;preview.camera.nearClipPlane=.3f;preview.camera.farClipPlane=30;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.12f,.085f);
            preview.lights[0].intensity=1.1f;preview.lights[0].color=new Color(1,.89f,.76f);preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);
            preview.lights[1].intensity=.35f;preview.lights[1].color=new Color(.7f,.8f,1);preview.lights[1].transform.rotation=Quaternion.Euler(20,150,0);
            preview.ambientColor=new Color(.24f,.26f,.22f);
            void Capture(string path)
            {
                preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
                var prior=RenderTexture.active;var temporary=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);Texture2D output=null;
                try
                {
                    Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                    output=new Texture2D(1280,720,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();
                    File.WriteAllBytes(path,output.EncodeToPNG());
                }
                finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(temporary);if(output!=null)UnityEngine.Object.DestroyImmediate(output);}
            }
            Capture("AgentScripts/RubberGloves-idle.png");
            var left=hands.transform.Find("LeftHand");var right=hands.transform.Find("RightHand");
            var leftPose=new Pose(left.localPosition,left.localRotation);var swing=new GloveSwingMotion();
            swing.Begin(leftPose,leftPose,Vector3.forward*3,1,0,.055f,.16f);var leftPeak=swing.Evaluate(.055f);
            left.SetLocalPositionAndRotation(leftPeak.position,leftPeak.rotation);
            check(right.localPosition.x>0 && left.localPosition.x>leftPose.position.x,"One hand advances while the other stays ready");
            Capture("AgentScripts/RubberGloves-swing.png");
            int triangles=prefab.GetComponentsInChildren<MeshFilter>(true).Sum(x=>x.sharedMesh.triangles.Length/3);
            return new{checksPassed=checks,triangles=triangles,sceneChanged=false,textureRulesVerified=true};
        }
        finally{preview.Cleanup();}
    }
}
