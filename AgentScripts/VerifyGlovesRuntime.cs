using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyGlovesRuntime
{
    private static double deadline;
    private static float clickedAt;
    private static LocalRubberGloveHands hands;
    private static IngredientClicker clicker;
    private static Vector3 leftRest,rightRest;
    private static readonly List<int> sides=new List<int>();
    private static bool clicked;
    private static int checks;
    public static string Arm()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter our temporary Play session first.");
        checks=0;clicked=false;sides.Clear();deadline=EditorApplication.timeSinceStartup+20;
        if(File.Exists("AgentScripts/RubberGloves-runtime-review.json"))File.Delete("AgentScripts/RubberGloves-runtime-review.json");
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        return "Temporary click/hand integration test armed; no scene saves.";
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    private static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play ended before verification.");
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Hand verification timed out.");
            if(!clicked)
            {
                var round=UnityEngine.Object.FindAnyObjectByType<RoundController>();
                if(round==null||!round.IsRunning)return;
                clicker=Camera.main.GetComponent<IngredientClicker>();Check(clicker!=null && clicker.enabled,"Ready local clicker");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LocalRubberGloveHands.prefab");
                hands=UnityEngine.Object.Instantiate(prefab,clicker.transform,false).GetComponent<LocalRubberGloveHands>();
                hands.SwingStarted+=(side,target,damage)=>sides.Add(side);
                leftRest=hands.LeftHand.localPosition;rightRest=hands.RightHand.localPosition;
                var oldRotation=clicker.transform.rotation;int oldCount=clicker.KimchiCount;
                var temporary=new List<GameObject>();
                try
                {
                    clicker.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.forward);
                    for(int i=0;i<3;i++)clicker.TryAttackAtAim();
                    Check(sides.SequenceEqual(new[]{0,1,0}),"Missed clicks alternate left-right-left");
                    Check(clicker.KimchiCount==oldCount,"Empty swings award no kimchi");
                    for(int i=0;i<2;i++)
                    {
                        var go=new GameObject("TemporaryHandTestIngredient",typeof(BoxCollider),typeof(Rigidbody));
                        temporary.Add(go);go.transform.position=clicker.transform.position+clicker.transform.forward*1.5f+clicker.transform.right*(i==0?-.012f:.012f);
                        go.GetComponent<BoxCollider>().size=new Vector3(.018f,.025f,.025f);
                        go.GetComponent<Rigidbody>().isKinematic=true;go.AddComponent<KimchiIngredient>();
                    }
                    clicker.TryAttackAtAim();
                    Check(sides.Count==4 && sides[3]==1,"One hand swing per multi-target click");
                    foreach(var go in temporary)Check(go.GetComponent<KimchiIngredient>().CurrentHp==9,"Each visible target damaged once");
                    Check(clicker.KimchiCount==oldCount,"Nonlethal hit awards no kimchi");
                    clicker.enabled=false;clicker.TryAttackAtAim();
                    Check(sides.Count==4,"Disabled attacks do not animate");
                }
                finally
                {
                    clicker.enabled=true;clicker.transform.rotation=oldRotation;
                    foreach(var go in temporary){go.SetActive(false);UnityEngine.Object.Destroy(go);}
                }
                clickedAt=Time.time;clicked=true;
            }
            else if(Time.time-clickedAt>.4f)
            {
                Check(Vector3.Distance(hands.LeftHand.localPosition,leftRest)<.004f,"Left hand recovered without drift");
                Check(Vector3.Distance(hands.RightHand.localPosition,rightRest)<.004f,"Right hand recovered without drift");
                Check(hands.GetComponentsInChildren<Collider>().Length==0,"Hands never block picking/collisions");
                Finish(true,"Misses, alternating sides, one swing per AOE click, damage, attack gate and recovery verified.");
            }
        }
        catch(Exception ex){Finish(false,ex.ToString());}
    }
    private static void Finish(bool passed,string detail)
    {
        EditorApplication.update-=Tick;
        File.WriteAllText("AgentScripts/RubberGloves-runtime-review.json",JsonUtility.ToJson(new Review{passed=passed,checksPassed=checks,detail=detail,sceneSaved=false},true));
    }
    [Serializable]private sealed class Review{public bool passed,sceneSaved;public int checksPassed;public string detail;}
}
