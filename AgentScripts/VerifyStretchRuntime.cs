using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyStretchRuntime
{
    private static double deadline;
    public static string Arm()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter our temporary Play session first.");
        deadline=EditorApplication.timeSinceStartup+20;
        if(File.Exists("AgentScripts/GloveStretch-runtime-review.json"))File.Delete("AgentScripts/GloveStretch-runtime-review.json");
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        return "Stretch-to-collider and moving face-proxy test armed; no scene saves.";
    }
    private static void Tick()
    {
        if(EditorApplication.isPlaying && EditorApplication.timeSinceStartup<deadline)
        {
            var round=UnityEngine.Object.FindAnyObjectByType<RoundController>();
            if(round!=null && !round.IsRunning)return;
        }
        EditorApplication.update-=Tick;
        int checks=0;GameObject proxy=null;Camera camera=null;Quaternion originalRotation=Quaternion.identity;
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        try
        {
            check(EditorApplication.isPlaying,"Temporary play active");
            camera=Camera.main;originalRotation=camera.transform.rotation;
            var clicker=camera.GetComponent<IngredientClicker>();
            var hands=camera.GetComponentInChildren<LocalRubberGloveHands>();
            check(hands!=null,"Existing user-applied hand prefab");
            camera.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
            proxy=new GameObject("TemporaryFaceCollider",typeof(SphereCollider));
            proxy.transform.position=camera.transform.position+camera.transform.forward*6f;
            var collider=proxy.GetComponent<SphereCollider>();collider.radius=.25f;
            Vector3 target=default;int side=-1;int clicks=0;
            hands.SwingStarted+=(index,position,damage)=>{side=index;target=position;clicks++;};
            int score=clicker.KimchiCount;
            clicker.TryAttackAtAim();
            check(clicker.LastAimCollider==collider,"Actual click ray hits face proxy");
            check(clicks==1 && side>=0,"One stretch per click");
            var motions=(GloveStretchMotion[])typeof(LocalRubberGloveHands).GetField("motions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hands);
            var late=typeof(LocalRubberGloveHands).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            // Deterministically sample the real runtime mesh at its contact hold.
            motions[side].Begin(Vector3.zero,Time.time-.06f,.055f,.025f,.16f);late.Invoke(hands,null);
            check(Vector3.Distance(hands.GetContactWorld(side),target)<.001f,"Hand reaches collider surface six meters away");
            Vector3 localPoint=proxy.transform.InverseTransformPoint(target);
            proxy.transform.position+=camera.transform.right*.3f;proxy.transform.Rotate(0,25,0);
            Vector3 moved=proxy.transform.TransformPoint(localPoint);late.Invoke(hands,null);
            check(Vector3.Distance(hands.GetContactWorld(side),moved)<.001f,"Hand follows moving/rotating face surface");
            camera.transform.rotation=Quaternion.Euler(20,35,0);late.Invoke(hands,null);
            check(Vector3.Distance(hands.GetContactWorld(side),moved)<.001f,"Contact stays on target when player looks away");
            motions[side].Begin(Vector3.zero,Time.time-2,.055f,.025f,.16f);late.Invoke(hands,null);
            foreach(var mesh in hands.GetComponentsInChildren<MeshFilter>())
            {
                var deformerMeshes=(StretchableGloveMesh[])typeof(LocalRubberGloveHands).GetField("meshes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(hands);
                check(mesh.sharedMesh.name.EndsWith("_LocalStretch"),"Private runtime mesh in use");
                check(deformerMeshes[0].Offset==Vector3.zero && deformerMeshes[1].Offset==Vector3.zero,"Deformation fully reset");
            }
            check(clicker.KimchiCount==score,"Face animation does not invent scores/damage rules");
            check(hands.GetComponentsInChildren<Collider>().Length==0,"Stretch adds no physical hitbox");
            Write(true,checks,"6m surface contact, moving target, view rotation, reset and unchanged scoring verified.");
        }
        catch(Exception ex){Write(false,checks,ex.ToString());}
        finally
        {
            if(camera!=null)camera.transform.rotation=originalRotation;
            if(proxy!=null){proxy.SetActive(false);UnityEngine.Object.Destroy(proxy);}
        }
    }
    private static void Write(bool passed,int checks,string detail)
    {
        File.WriteAllText("AgentScripts/GloveStretch-runtime-review.json",JsonUtility.ToJson(
            new Review{passed=passed,checksPassed=checks,detail=detail,sceneSaved=false},true));
    }
    [Serializable]private sealed class Review{public bool passed,sceneSaved;public int checksPassed;public string detail;}
}
