using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyGloveStretch
{
    public static object Main()
    {
        int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        var motion=new GloveStretchMotion();Vector3 far=new Vector3(0,0,8);
        motion.Begin(Vector3.zero,0,.055f,.025f,.16f);
        check(motion.Evaluate(0,far)==Vector3.zero,"Initial pose unchanged");
        check(Vector3.Distance(motion.Evaluate(.055f,far),far)<.00001f,"Exact long-distance contact");
        check(Vector3.Distance(motion.Evaluate(.07f,far),far)<.00001f,"Contact hold");
        check(motion.Evaluate(.12f,far).magnitude<far.magnitude,"Retraction");
        check(motion.Evaluate(1,far)==Vector3.zero && !motion.IsActive(1),"Full recovery");
        Vector3 current=new Vector3(.2f,.1f,3);
        motion.Begin(current,.1f,.055f,.025f,.16f);
        check(Vector3.Distance(motion.Evaluate(.1f,far),current)<.00001f,"Interrupted stretch continuous");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LocalRubberGloveHands.prefab");
        var preview=new PreviewRenderUtility();StretchableGloveMesh deformer=null;
        try
        {
            var holder=new GameObject("StretchPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var hands=UnityEngine.Object.Instantiate(prefab,holder.transform,false);
            hands.GetComponent<LocalRubberGloveHands>().enabled=false;
            var left=hands.transform.Find("LeftHand");var mesh=left.GetComponentInChildren<MeshFilter>();
            var original=mesh.sharedMesh;var originalVertices=original.vertices;var originalUVs=original.uv;
            Vector3[] baseline=originalVertices.Select(v=>mesh.transform.TransformPoint(v)).ToArray();
            int pinned=Enumerable.Range(0,originalVertices.Length).OrderBy(i=>left.InverseTransformPoint(baseline[i]).z).First();
            deformer=new StretchableGloveMesh(mesh,left,hands.transform);
            check(mesh.sharedMesh!=original,"Private mesh copy");
            holder.SetActive(true);
            foreach(float distance in new[]{2f,8f,20f})
            {
                Vector3 target=new Vector3(.3f,.1f,distance);
                deformer.Apply(hands.transform.InverseTransformPoint(target)-deformer.OriginalContactRig);
                check(Vector3.Distance(deformer.ContactWorld,target)<.0001f,"Contact anchor reaches "+distance+"m");
                var actual=mesh.sharedMesh.vertices.Select(v=>mesh.transform.TransformPoint(v)).ToArray();
                check(actual.Any(v=>Vector3.Distance(v,target)<.0001f),"Actual mesh vertex reaches surface at "+distance+"m");
                check(Vector3.Distance(actual[pinned],baseline[pinned])<.0001f,"Sleeve base stays pinned");
                check(mesh.sharedMesh.uv.SequenceEqual(originalUVs),"Original UVs stretch with mesh");
                check(original.vertices.SequenceEqual(originalVertices),"Shared asset untouched");
            }
            hands.transform.rotation=Quaternion.Euler(12,35,0);
            Vector3 stationaryTarget=new Vector3(0,0,5);
            deformer.Apply(hands.transform.InverseTransformPoint(stationaryTarget)-deformer.OriginalContactRig);
            check(Vector3.Distance(deformer.ContactWorld,stationaryTarget)<.0001f,"Contact stays in world space when view rotates");
            hands.transform.rotation=Quaternion.identity;
            for(int i=0;i<100;i++)deformer.Apply(new Vector3(.1f,.05f,i%12));
            deformer.Reset();
            check(mesh.sharedMesh.vertices.SequenceEqual(originalVertices),"Full mesh restoration after repeated stretches");
            check(mesh.sharedMesh.bounds==original.bounds,"Restored mesh bounds");
            check(hands.GetComponentsInChildren<Collider>().Length==0,"No collider stretch or extra hits");
            Vector3 previewTarget=new Vector3(0,0,3.5f);
            deformer.Apply(hands.transform.InverseTransformPoint(previewTarget)-deformer.OriginalContactRig);
            preview.camera.cameraType=CameraType.Game;preview.camera.transform.position=new Vector3(.7f,.7f,-1.2f);
            preview.camera.transform.LookAt(new Vector3(0,-.05f,1.5f));
            preview.camera.fieldOfView=60;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=40;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.12f,.085f);
            preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);
            preview.lights[1].intensity=.4f;preview.ambientColor=new Color(.24f,.26f,.22f);
            preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var prior=RenderTexture.active;var temporary=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);Texture2D png=null;
            try
            {
                Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                png=new Texture2D(1280,720,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();
                File.WriteAllBytes("AgentScripts/RubberGloves-stretch.png",png.EncodeToPNG());
            }
            finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(temporary);if(png!=null)UnityEngine.Object.DestroyImmediate(png);}
            deformer.Dispose();deformer=null;
            check(mesh.sharedMesh==original,"Original mesh reference restored on disposal");
            return new{checksPassed=checks,sceneSaved=false,meshContactsVerifiedThrough20Meters=true};
        }
        finally{deformer?.Dispose();preview.Cleanup();}
    }
}
