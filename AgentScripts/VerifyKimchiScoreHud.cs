using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VerifyKimchiScoreHud
{
    public static object Main()
    {
        int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        var match=new MatchProgress(6);
        check(LiveKimchiScore.Read(match,0,0).TotalCount==0,"Fresh match");
        check(LiveKimchiScore.Read(null,0,0).TotalCount==0,"No match safe");
        match.BeginRound();
        check(LiveKimchiScore.Read(match,0,7).TotalCount==7,"First round running total");
        match.CompleteRound(new Dictionary<int,int>{{0,7},{1,11}});
        var end=LiveKimchiScore.Read(match,0,7);
        check(end.RoundCount==7 && end.TotalCount==7,"Finished round not counted twice");
        check(LiveKimchiScore.Read(match,1,11).TotalCount==11,"Separate player scores");
        match.CompleteDraw();
        check(LiveKimchiScore.Read(match,0,7).TotalCount==7,"No double count while ready");
        match.BeginRound();
        check(LiveKimchiScore.Read(match,0,0).TotalCount==7,"Round two keeps prior total");
        check(LiveKimchiScore.Read(match,0,5).TotalCount==12,"Round two live sum");
        match.CompleteRound(new Dictionary<int,int>{{0,5}});
        check(LiveKimchiScore.Read(match,0,5).TotalCount==12,"Second round saved once");
        for(int round=3;round<=6;round++)
        {
            match.CompleteDraw();match.BeginRound();match.CompleteRound(new Dictionary<int,int>{{0,3}});
        }
        check(LiveKimchiScore.Read(match,0,3).TotalCount==24,"Final round not counted twice");
        bool negativeRejected=false;
        try{LiveKimchiScore.Read(match,0,-1);}catch(ArgumentOutOfRangeException){negativeRejected=true;}
        check(negativeRejected,"Negative live score rejected");
        var large=new MatchProgress(2);large.BeginRound();large.CompleteRound(new Dictionary<int,int>{{0,int.MaxValue}});
        large.CompleteDraw();large.BeginRound();
        check(LiveKimchiScore.Read(large,0,int.MaxValue).TotalCount==2L*int.MaxValue,"64-bit cumulative score");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/KimchiScoreHUD.prefab");
        check(prefab!=null && prefab.activeSelf,"Active prefab");
        var preview=new PreviewRenderUtility();
        try
        {
            var holder=new GameObject("ScoreHudPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var root=UnityEngine.Object.Instantiate(prefab,holder.transform,false);
            var hud=root.GetComponent<KimchiScoreHud>();hud.enabled=false;hud.Bind(null);
            var labels=root.GetComponentsInChildren<TMP_Text>();
            var roundLabel=labels.Single(x=>x.name=="RoundKimchi");var totalLabel=labels.Single(x=>x.name=="TotalKimchi");
            check(roundLabel.text.Contains("--") && totalLabel.text.Contains("--"),"Missing player placeholder");
            var playerObject=new GameObject("FakeScoreSource");playerObject.SetActive(false);playerObject.transform.SetParent(holder.transform,false);
            playerObject.AddComponent<Camera>();var clicker=playerObject.AddComponent<IngredientClicker>();
            var playerSettings=new SerializedObject(clicker);playerSettings.FindProperty("kimchiCount").intValue=7;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();hud.Bind(clicker);
            check(roundLabel.text=="이번 라운드  7개","Actual round count UI");
            var expected=LiveKimchiScore.Read(MatchSession.Progress,0,7);
            check(totalLabel.text=="누적 김치  "+expected.TotalCount.ToString("N0")+"개","Actual cumulative score UI");
            // Preview a fresh-round count rather than an impossible pre-match injected kill count.
            playerSettings.Update();playerSettings.FindProperty("kimchiCount").intValue=0;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();hud.RefreshNow();
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;
            var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(1920,1080);rect.localScale=Vector3.one*.01f;rect.localPosition=Vector3.zero;
            holder.SetActive(true);Canvas.ForceUpdateCanvases();
            foreach(var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>())check(!graphic.raycastTarget,"Click passes through HUD");
            foreach(var label in labels)
            {
                label.ForceMeshUpdate();check(!label.isTextOverflowing,"No overflow "+label.name);
                foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)).Distinct())check(label.font.HasCharacter(c,true,true),"Missing glyph "+c);
            }
            preview.camera.cameraType=CameraType.Game;preview.camera.orthographic=true;preview.camera.orthographicSize=5.4f;
            preview.camera.transform.position=new Vector3(0,0,-20);preview.camera.transform.rotation=Quaternion.identity;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.1f);
            canvas.worldCamera=preview.camera;
            preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);var texture=preview.EndPreview();
            var prior=RenderTexture.active;var temporary=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);
            Texture2D output=null;
            try
            {
                Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                output=new Texture2D(1280,720,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();
                File.WriteAllBytes("AgentScripts/KimchiScoreHUD-preview.png",output.EncodeToPNG());
            }
            finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(temporary);if(output!=null)UnityEngine.Object.DestroyImmediate(output);}
            return new {checksPassed=checks,sceneChanged=false,previewUsesIsolatedScoreSource=true};
        }
        finally{preview.Cleanup();}
    }
}
