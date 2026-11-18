using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VerifySkillHud
{
    public static object Main()
    {
        int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
        var players=(IDictionary)typeof(MatchSkillInventory).GetField("Players",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        int id=int.MinValue;while(players.Contains(id))id++;
        var preview=new PreviewRenderUtility();
        try
        {
            var state=new SkillCombatState();
            state.Configure(Array.Empty<MatchSkillInventory.OwnedSkill>(),0);
            foreach(string name in new[]{"SpicyHands","BigHands","ChargedGloves","AncestralTaste"})
                MatchSkillInventory.Grant(id,AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Skills/"+name+".asset"));
            state.Configure(MatchSkillInventory.GetSkills(id),0);
            check(SkillHudText.Detail(SkillKind.SpicyHands,state,0).Contains("+1"),"Damage bonus");
            check(SkillHudText.Detail(SkillKind.BigHands,state,0).Contains("20%"),"Aim bonus");
            check(SkillHudText.Detail(SkillKind.ChargedGloves,state,0).Contains("0%"),"Empty charge");
            check(SkillHudText.Detail(SkillKind.ChargedGloves,state,.5f).Contains("50%"),"Half charge");
            check(SkillHudText.Detail(SkillKind.ChargedGloves,state,1f).Contains("충전 완료"),"Ready charge");
            state.BeginClick(1,1f,out _);
            check(SkillHudText.Detail(SkillKind.ChargedGloves,state,1f).Contains("0%"),"Charge consumed");
            check(SkillHudText.Detail(SkillKind.AncestralTaste,state,0).Contains("5회"),"Initial ancestor count");
            for(int i=0;i<4;i++)state.RecordSuccessfulClick();
            check(SkillHudText.Detail(SkillKind.AncestralTaste,state,0).Contains("다음 명중"),"Next-click extra strike");
            state.RecordSuccessfulClick();
            check(SkillHudText.Detail(SkillKind.AncestralTaste,state,0).Contains("다른 보이는 재료 필요"),"Wait for extra target");
            state.ConsumeAncestorStrike();
            check(SkillHudText.Detail(SkillKind.AncestralTaste,state,0).Contains("5회"),"Counter reset");
            var holder=new GameObject("SkillHudPreview");holder.SetActive(false);preview.AddSingleGO(holder);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerSkillHUD.prefab");
            check(prefab!=null && prefab.activeSelf,"Active prefab");
            var root=UnityEngine.Object.Instantiate(prefab,holder.transform,false);
            var hud=root.GetComponent<PlayerSkillHud>();hud.enabled=false;
            hud.Bind(null);
            check(root.GetComponentsInChildren<TMP_Text>(true).Single(x=>x.name=="EmptySkills").text=="플레이어 연결 대기","Missing player safe");
            var source=new GameObject("FakeSkillSource");source.SetActive(false);source.transform.SetParent(holder.transform,false);
            source.AddComponent<Camera>();source.AddComponent<IngredientClicker>();
            var effects=source.AddComponent<PlayerSkillEffects>();
            var sourceSettings=new SerializedObject(effects);sourceSettings.FindProperty("playerId").intValue=id;
            sourceSettings.ApplyModifiedPropertiesWithoutUndo();
            effects.State.Configure(MatchSkillInventory.GetSkills(id),Time.time-2f);
            for(int i=0;i<4;i++)effects.State.RecordSuccessfulClick();
            hud.Bind(effects);
            check(root.GetComponentsInChildren<TMP_Text>(true).Count(x=>x.name=="Name" && x.gameObject.activeSelf)==4,"Four row definitions");
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;
            var rect=(RectTransform)root.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(1920,1080);rect.localScale=Vector3.one*.01f;rect.localPosition=Vector3.zero;
            holder.SetActive(true);hud.RefreshNow();
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform.Find("SkillPanel"));
            foreach(var label in root.GetComponentsInChildren<TMP_Text>().Where(x=>x.gameObject.activeInHierarchy))
            {
                label.ForceMeshUpdate();check(!label.raycastTarget,"HUD must not block clicks");
                check(!label.isTextOverflowing,"No overflow: "+label.text);
                foreach(char c in label.text.Where(c=>!char.IsWhiteSpace(c)).Distinct())
                    check(label.font.HasCharacter(c,true,true),"Missing glyph "+c);
            }
            preview.camera.cameraType=CameraType.Game;preview.camera.orthographic=true;preview.camera.orthographicSize=5.4f;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100f;
            preview.camera.transform.position=new Vector3(0,0,-20);preview.camera.transform.rotation=Quaternion.identity;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.1f);
            canvas.worldCamera=preview.camera;
            preview.BeginPreview(new Rect(0,0,1280,720),GUIStyle.none);preview.Render(true);Texture texture=preview.EndPreview();
            var prior=RenderTexture.active;var temporary=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);
            Texture2D output=null;
            try
            {
                Graphics.Blit(texture,temporary);RenderTexture.active=temporary;
                output=new Texture2D(1280,720,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,1280,720),0,0);output.Apply();
                File.WriteAllBytes("AgentScripts/PlayerSkillHUD-preview.png",output.EncodeToPNG());
            }
            finally{RenderTexture.active=prior;RenderTexture.ReleaseTemporary(temporary);if(output!=null)UnityEngine.Object.DestroyImmediate(output);}
            return new {checksPassed=checks,sceneChanged=false,previewUsesIsolatedTestSkills=true};
        }
        finally{players.Remove(id);preview.Cleanup();}
    }
}
