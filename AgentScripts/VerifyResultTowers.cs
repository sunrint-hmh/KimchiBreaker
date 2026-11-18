using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VerifyResultTowers
{
    public static object Main()
    {
        int checks=0;
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        check(KimchiTowerRules.JarCount(0)==0,"Zero");
        check(KimchiTowerRules.JarCount(1)==0,"One kimchi is discarded visually");
        check(KimchiTowerRules.JarCount(2)==1,"Two kimchi per jar");
        check(KimchiTowerRules.JarCount(5)==2,"Odd score uses floor");
        check(KimchiTowerRules.JarCount(long.MaxValue)==long.MaxValue/2,"No rounding overflow");
        bool negativeRejected=false;
        try{KimchiTowerRules.JarCount(-1);}catch(ArgumentOutOfRangeException){negativeRejected=true;}
        check(negativeRejected,"Negative score rejected");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MatchResultTowers.prefab");
        check(prefab!=null && prefab.activeSelf,"Tower prefab active");
        var preview=new PreviewRenderUtility();
        try
        {
            var holder=new GameObject("TowerTestPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var root=UnityEngine.Object.Instantiate(prefab,holder.transform,false);
            root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true).gameObject.SetActive(false);
            var screen=root.GetComponentInChildren<MatchResultScreen>(true);screen.enabled=false;
            var display=root.GetComponent<ResultTowerDisplay>();
            var camera=root.GetComponentInChildren<Camera>(true);camera.enabled=false;camera.aspect=16f/9f;
            var hud=root.GetComponentInChildren<Canvas>(true);
            hud.renderMode=RenderMode.WorldSpace;
            hud.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;
            holder.SetActive(true);
            var match=new MatchProgress(1);match.BeginRound();
            match.CompleteRound(new Dictionary<int,int>{{0,1},{1,5},{2,9},{3,15}});
            screen.RefreshFrom(match);
            check(display.DisplayedJarCounts.SequenceEqual(new long[]{0,2,4,7}),"Exact floor counts in player seat order");
            var jarAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SkillJar.prefab");
            float height=ResultTowerDisplay.VisualBounds(jarAsset).size.y;
            for(int i=0;i<4;i++)
            {
                var column=root.transform.Find("TowerStage/GeneratedResultTowers/PlayerTower_"+i);
                var jars=column.Cast<Transform>().Where(x=>x.name.StartsWith("Jar_")).ToArray();
                check(jars.LongLength==display.DisplayedJarCounts[i],"Actual jar instances "+i);
                var character=column.Find("PlayerCharacter");
                check(character!=null,"Character for every player, including zero jars");
                var bounds=character.GetComponentsInChildren<Renderer>().Select(x=>x.bounds).ToArray();
                float feet=bounds.Min(x=>x.min.y);
                check(Mathf.Abs(feet-display.DisplayedJarCounts[i]*height)<.001f,"Feet on top of tower "+i);
                if(jars.Length>1)
                {
                    float step=jars[1].localPosition.y-jars[0].localPosition.y;
                    check(Mathf.Abs(step-height)<.001f,"Exact jar-height spacing");
                }
                var characterTop=bounds.Max(x=>x.max.y);
                var top=camera.WorldToViewportPoint(new Vector3(column.position.x,characterTop,0));
                check(top.y<.801f && top.x>.1f && top.x<.9f,"Camera frames full tower "+i);
            }
            check(root.GetComponentsInChildren<Collider>().All(x=>!x.enabled),"Result geometry has no collisions");
            check(root.GetComponentsInChildren<TMP_Text>(true).Single(x=>x.name=="countLabels_1").text=="김치 5개",
                "Exact score shown despite discarded odd kimchi");
            var rect=(RectTransform)hud.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(1920,1080);
            rect.localScale=Vector3.one*(camera.orthographicSize*2f/1080f);
            rect.localPosition=new Vector3(0,camera.transform.localPosition.y,-2);
            Canvas.ForceUpdateCanvases();
            foreach(var label in root.GetComponentsInChildren<TMP_Text>().Where(x=>x.gameObject.activeInHierarchy))
            {
                label.ForceMeshUpdate();check(!label.isTextOverflowing,"No overflow: "+label.name);
            }
            preview.camera.cameraType=CameraType.Game;
            preview.camera.transform.SetPositionAndRotation(camera.transform.position,Quaternion.identity);
            preview.camera.orthographic=true;preview.camera.orthographicSize=camera.orthographicSize;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=camera.farClipPlane;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=camera.backgroundColor;
            hud.worldCamera=preview.camera;
            preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);
            Texture texture=preview.EndPreview();
            var prior=RenderTexture.active;var temporary=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);
            Texture2D png=null;
            try
            {
                Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                png=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                png.ReadPixels(new Rect(0,0,1280,720),0,0);png.Apply();
                File.WriteAllBytes("AgentScripts/ResultTowers-preview.png",png.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=prior;RenderTexture.ReleaseTemporary(temporary);
                if(png!=null)UnityEngine.Object.DestroyImmediate(png);
            }
            screen.RefreshFrom(new MatchProgress(6));
            check(display.DisplayedJarCounts.Length==0,"Empty scene invents no scores/towers");
            return new {checksPassed=checks,sceneChanged=false,reusedJar=true,previewUsesTestScores=true,
                preview="AgentScripts/ResultTowers-preview.png"};
        }
        finally{preview.Cleanup();}
    }
}
