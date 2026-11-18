using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using TMPro;

public static class FixDrawUI
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before saving UI.");
        var previous = SceneManager.GetActiveScene();
        var draw = SceneManager.GetSceneByPath("Assets/Scenes/Draw.unity");
        bool opened = !draw.isLoaded;
        if (opened) draw = EditorSceneManager.OpenScene("Assets/Scenes/Draw.unity", OpenSceneMode.Additive);
        try
        {
            var allocator = UnityEngine.Object.FindObjectsByType<DrawTicketAllocator>().Single(x=>x.gameObject.scene==draw);
            var source = allocator.GetAllocationLabel(0) as TextMeshProUGUI;
            if (source == null || source.GetComponentInParent<Canvas>() == null) throw new Exception("Existing Draw UI missing.");
            var timer = allocator.GetComponent<DrawAllocationCountdown>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NanumGothic/SkillLabels SDF.asset");
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/NanumGothic/NanumGothic Dynamic SDF.asset");
            if (font == null || fallback == null) throw new Exception("Korean font assets missing.");
            const string characters = "0123456789남은김치테스트투표배분초종료추첨준비중클릭항아리넣기우회수할있어요가없효과아직미정입니다응모하세요스킬기다려주: ·";
            if (!fallback.TryAddCharacters(characters, out string missing)) throw new Exception("Missing Korean glyphs: " + missing);
            EditorUtility.SetDirty(fallback);
            const string presetPath = "Assets/UI/Fonts/NanumGothic/DrawHudOutline.mat";
            var preset = AssetDatabase.LoadAssetAtPath<Material>(presetPath);
            if (preset == null)
            {
                preset = new Material(font.material);
                preset.name = "DrawHudOutline";
                AssetDatabase.CreateAsset(preset, presetPath);
            }
            preset.EnableKeyword("OUTLINE_ON");
            preset.SetFloat("_OutlineWidth", .12f);
            preset.SetColor("_OutlineColor", new Color(.055f,.035f,.025f,1));
            EditorUtility.SetDirty(preset);
            EditorSceneManager.SaveScene(draw);
            File.Copy(draw.path, "AgentScripts/Backups/Draw-before-visible-clock-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity.bak");
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Repair draw counters, visible timer and four skill panels");
            var parent = source.transform.parent;
            TextMeshProUGUI Text(string name, string value, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, float pointSize)
            {
                var found = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include)
                    .Where(x=>x.gameObject.scene==draw && x.name==name).ToArray();
                if (found.Length > 1) throw new Exception("Duplicate UI: "+name);
                TextMeshProUGUI label;
                if (found.Length == 1) label=found[0];
                else
                {
                    var clone=UnityEngine.Object.Instantiate(source.gameObject,parent,false);
                    clone.name=name;
                    Undo.RegisterCreatedObjectUndo(clone,"Create "+name);
                    label=clone.GetComponent<TextMeshProUGUI>();
                }
                Undo.RecordObject(label,"Configure "+name);
                Undo.RecordObject(label.rectTransform,"Place "+name);
                label.font=font; label.fontSharedMaterial=preset;
                label.text=value; label.fontSize=pointSize;
                label.enableAutoSizing=false; label.alignment=TextAlignmentOptions.Center;
                label.color=Color.white; label.raycastTarget=false;
                label.rectTransform.anchorMin=label.rectTransform.anchorMax=anchor;
                label.rectTransform.pivot=pivot;
                label.rectTransform.anchoredPosition3D=new Vector3(position.x,position.y,0);
                label.rectTransform.sizeDelta=size;
                label.rectTransform.localScale=Vector3.one;
                label.gameObject.SetActive(true);
                return label;
            }
            var remaining=Text("RemainingKimchi","남은 김치 : 10",new Vector2(0,1),new Vector2(0,1),new Vector2(40,-38),new Vector2(640,80),36);
            remaining.alignment=TextAlignmentOptions.MidlineLeft;
            var clock=Text("DrawTimer","배분  10초",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-30),new Vector2(600,90),50);
            clock.color=new Color(1f,.9f,.7f);
            var hint=Text("DrawStatus","항아리를 클릭해 김치 넣기 · 넣은 김치는 회수할 수 없어요",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-130),new Vector2(1500,70),30);
            Undo.RecordObject(allocator,"Connect counters and status");
            var settings=new SerializedObject(allocator);
            settings.FindProperty("remainingLabel").objectReferenceValue=remaining;
            settings.FindProperty("statusLabel").objectReferenceValue=hint;
            var counts=settings.FindProperty("allocationLabels");
            for(int i=0;i<4;i++)
            {
                var old=allocator.GetAllocationLabel(i);
                Vector2 pos=old.rectTransform.anchoredPosition;
                var count=Text("Jar"+(i+1)+"Count","투표 : 0",new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,new Vector2(420,70),40);
                counts.GetArrayElementAtIndex(i).objectReferenceValue=count;
                Text("Jar"+(i+1)+"SkillName","스킬 "+(i+1),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(pos.x,340),new Vector2(440,60),36);
                Text("Jar"+(i+1)+"SkillDescription","효과는 아직 미정입니다.\n김치를 넣어 응모하세요.",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(pos.x,240),new Vector2(440,110),28);
            }
            settings.ApplyModifiedProperties();
            Undo.RecordObject(timer,"Connect visible timer");
            var timerData=new SerializedObject(timer);
            timerData.FindProperty("timerLabel").objectReferenceValue=clock;
            timerData.FindProperty("durationSeconds").floatValue=10f;
            timerData.ApplyModifiedProperties();
            Canvas.ForceUpdateCanvases();
            foreach(var text in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>().Where(x=>x.gameObject.scene==draw)) text.ForceMeshUpdate();
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(draw); EditorSceneManager.SaveScene(draw);
            return "Visible 10-second timer, expiry/no-tickets hints, four vote counters, four skill names and four placeholder descriptions saved; all UI is non-blocking.";
        }
        finally { if(opened) { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(draw,true); } }
    }

    public static object VerifyClicks()
    {
        if(!EditorApplication.isPlaying) throw new Exception("Play required for click verification.");
        var allocator=UnityEngine.Object.FindAnyObjectByType<DrawTicketAllocator>();
        var timer=UnityEngine.Object.FindAnyObjectByType<DrawAllocationCountdown>();
        if(allocator==null || !allocator.IsReady || timer==null) throw new Exception("Draw not initialized.");
        // Isolated runtime fixture: leave the serialized 10-second window and real results intact.
        MatchTicketWallet.ResetForNewMatch();
        MatchTicketWallet.OpenEditorTest(10);
        typeof(DrawTicketAllocator).GetField("<IsAllocationClosed>k__BackingField",Private).SetValue(allocator,false);
        typeof(DrawTicketAllocator).GetField("<RemainingTickets>k__BackingField",Private).SetValue(allocator,10);
        var allocations=(int[])typeof(DrawTicketAllocator).GetField("allocations",Private).GetValue(allocator);
        Array.Clear(allocations,0,allocations.Length);
        typeof(DrawAllocationCountdown).GetField("<RemainingSeconds>k__BackingField",Private).SetValue(timer,10f);
        typeof(DrawAllocationCountdown).GetField("<IsRunning>k__BackingField",Private).SetValue(timer,true);
        var camera=allocator.DrawCamera;
        for(int i=0;i<4;i++)
        {
            var collider=allocator.GetJarCollider(i);
            Vector2 point=camera.WorldToScreenPoint(collider.bounds.center);
            if(!allocator.TryClickJarAtScreenPoint(point)) throw new Exception("Screen ray did not deposit into jar "+i);
            if(allocator.GetAllocation(i)!=1 || allocator.GetAllocationLabel(i).text!="투표 : 1") throw new Exception("Vote counter mismatch on jar "+i);
        }
        if(allocator.RemainingTickets!=6) throw new Exception("Total balance mismatch.");
        var first=allocator.GetJarCollider(0);
        if(typeof(DrawTicketAllocator).GetMethod("TryRefund")!=null) throw new Exception("Refund API still exists.");
        timer.SendMessage("Update");
        var data=new SerializedObject(timer);
        var clock=data.FindProperty("timerLabel").objectReferenceValue as TMP_Text;
        if(clock==null || !clock.text.Contains("초")) throw new Exception("Countdown text missing.");
        typeof(DrawAllocationCountdown).GetField("<RemainingSeconds>k__BackingField",Private).SetValue(timer,.00001f);
        timer.SendMessage("Update");
        if(!allocator.IsAllocationClosed || clock.text!="배분 종료") throw new Exception("Visible expiry or closure failed.");
        if(allocator.TryClickJarAtScreenPoint(camera.WorldToScreenPoint(first.bounds.center))) throw new Exception("Expired window still accepts votes.");
        var scene=SceneManager.GetActiveScene();
        var texts=UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>().Where(x=>x.gameObject.scene==scene).ToArray();
        if(texts.Count(x=>x.name.EndsWith("SkillDescription"))!=4 || texts.Count(x=>x.name.EndsWith("SkillName"))!=4)
            throw new Exception("Four skill panels are incomplete.");
        Canvas.ForceUpdateCanvases();
        foreach(var text in texts) text.ForceMeshUpdate();
        EditorApplication.isPaused=true;
        return new { screenRayDeposits=4, refundRemoved=true, remaining=allocator.RemainingTickets, votes=allocator.GetAllocationsSnapshot(),
            timer=clock.text, closed=allocator.IsAllocationClosed, skillNames=4, descriptions=4 };
    }
}
