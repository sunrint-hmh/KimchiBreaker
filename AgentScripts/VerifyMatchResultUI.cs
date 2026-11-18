using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class VerifyMatchResultUI
{
    public static object Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MatchResultUI.prefab");
        if (prefab == null || !prefab.activeSelf) throw new Exception("Missing/inactive prefab.");
        var preview = new PreviewRenderUtility();
        int checks = 0;
        Action<bool,string> check = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
        try
        {
            var holder = new GameObject("ResultTestPreview"); holder.SetActive(false);
            preview.AddSingleGO(holder);
            var root = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
            root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true).gameObject.SetActive(false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(1920,1080); rect.localScale = Vector3.one * .01f;
            rect.anchoredPosition3D = Vector3.zero;
            holder.SetActive(true);
            var screen = root.GetComponent<MatchResultScreen>();
            screen.enabled = false; // Prevent Start/cursor changes in an existing user Play session.
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text Label(string name) => labels.Single(x => x.name == name);
            var properties = new SerializedObject(screen);
            check(properties.FindProperty("title").objectReferenceValue != null &&
                properties.FindProperty("summary").objectReferenceValue != null &&
                properties.FindProperty("status").objectReferenceValue != null &&
                properties.FindProperty("playAgainButton").objectReferenceValue != null, "Wired UI");
            check(root.GetComponentInChildren<UnityEngine.UI.Button>(true).GetComponent<UnityEngine.UI.Image>().raycastTarget,
                "Button target");
            check(labels.All(x => !x.raycastTarget), "Text does not intercept button");
            screen.RefreshFrom(new MatchProgress(6));
            check(Label("ResultSummary").text == "완료된 게임 결과가 없습니다.", "Standalone empty state");
            check(Label("Label").text == "게임 시작", "Standalone start label");
            check(!root.transform.Find("ScorePanel/PlayerRow1").gameObject.activeSelf, "No invented players");
            var local = new MatchProgress(1); local.BeginRound();
            local.CompleteRound(new Dictionary<int,int>{{0,18}});
            screen.RefreshFrom(local);
            check(Label("ResultTitle").text == "우승!" && Label("Label").text == "다시 하기", "Local win");
            check(root.transform.Find("ScorePanel/PlayerRow1/KimchiCount").GetComponent<TMP_Text>().text == "18개",
                "Actual local score");
            var tie = new MatchProgress(6);
            for (int round=0;round<6;round++)
            {
                tie.BeginRound(); tie.CompleteRound(new Dictionary<int,int>{{0,5},{1,3},{2,5},{3,2}});
                if (round<5) tie.CompleteDraw();
            }
            screen.RefreshFrom(tie);
            check(Label("ResultTitle").text == "공동우승", "Joint victory title");
            check(root.transform.Find("ScorePanel/PlayerRow3/Rank").GetComponent<TMP_Text>().text == "3위", "Tied rank UI");
            check(Label("ResultSummary").text.Contains("30개"), "Cumulative total UI");
            foreach (var label in labels.Where(x=>x.gameObject.activeInHierarchy))
            {
                foreach (char c in label.text.Where(c=>!char.IsWhiteSpace(c)).Distinct())
                    check(label.font.HasCharacter(c, true, true), "Missing glyph U+" + ((int)c).ToString("X4"));
            }
            Canvas.ForceUpdateCanvases();
            foreach (var label in labels.Where(x=>x.gameObject.activeInHierarchy))
            {
                label.ForceMeshUpdate();
                check(!label.isTextOverflowing, "Text overflow: " + label.name);
            }
            preview.camera.transform.position = new Vector3(0,0,-20);
            preview.camera.cameraType = CameraType.Game;
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.orthographic = true; preview.camera.orthographicSize = 5.4f;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.black;
            canvas.worldCamera = preview.camera;
            preview.BeginPreview(new Rect(0,0,1280,720), GUIStyle.none);
            preview.Render(true);
            Texture texture = preview.EndPreview();
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);
            Texture2D png = null;
            try
            {
                Graphics.Blit(texture, temporary); RenderTexture.active = temporary;
                png = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                png.ReadPixels(new Rect(0,0,1280,720),0,0); png.Apply();
                File.WriteAllBytes("AgentScripts/MatchResultUI-preview.png", png.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
                if (png != null) UnityEngine.Object.DestroyImmediate(png);
            }
            return new { checksPassed = checks, prefabWired = true, scenesModified = false,
                previewUsesIsolatedTestScores = true, preview = "AgentScripts/MatchResultUI-preview.png" };
        }
        finally { preview.Cleanup(); }
    }
}
