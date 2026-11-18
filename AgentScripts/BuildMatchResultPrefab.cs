using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class BuildMatchResultPrefab
{
    private const string Path = "Assets/Prefabs/MatchResultUI.prefab";
    public static object Main()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null)
            throw new Exception("Result prefab already exists; inspect before replacing it.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/UI/Fonts/NanumGothic/NanumGothic Dynamic SDF.asset");
        if (font == null) throw new Exception("Existing Korean font not found.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("MatchResultUI", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            // Author inactive objects so an existing user Play session gets no UI or input callbacks.
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = 5;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = 5; go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = position; rect.sizeDelta = size;
                return rect;
            }
            UnityEngine.UI.Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
            {
                var image = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = color; image.raycastTarget = false;
                return image;
            }
            TextMeshProUGUI Text(string name, Transform parent, string text, Vector2 position,
                Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
            {
                var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font; label.fontSize = fontSize; label.color = new Color(.94f,.94f,.86f);
                label.text = text; label.alignment = alignment; label.raycastTarget = false;
                label.enableAutoSizing = true; label.fontSizeMin = Mathf.Min(22f, fontSize); label.fontSizeMax = fontSize;
                return label;
            }
            var background = Image("Backdrop", root.transform, Vector2.zero, Vector2.zero, new Color(.025f,.035f,.025f,.98f));
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            var panel = Image("ScorePanel", root.transform, Vector2.zero, new Vector2(1260, 900), new Color(.065f,.075f,.06f));
            Image("RedTrim", panel.transform, new Vector2(0,446), new Vector2(1260,8), new Color(.68f,.09f,.035f));
            var title = Text("ResultTitle", panel.transform, "게임 결과", new Vector2(0,350), new Vector2(1080,90), 64);
            title.color = new Color(.7f,1f,.24f); title.fontStyle = FontStyles.Bold;
            var summary = Text("ResultSummary", panel.transform, "완료된 게임 결과가 없습니다.", new Vector2(0,265), new Vector2(1100,60), 32);
            Text("RankHeader", panel.transform, "순위", new Vector2(-480,180), new Vector2(140,50), 26);
            Text("NameHeader", panel.transform, "플레이어", new Vector2(-130,180), new Vector2(450,50), 26, TextAlignmentOptions.MidlineLeft);
            Text("CountHeader", panel.transform, "누적 김치", new Vector2(390,180), new Vector2(260,50), 26, TextAlignmentOptions.MidlineRight);
            var screen = root.AddComponent<MatchResultScreen>();
            var settings = new SerializedObject(screen);
            settings.FindProperty("title").objectReferenceValue = title;
            settings.FindProperty("summary").objectReferenceValue = summary;
            var rows = settings.FindProperty("rows"); rows.arraySize = 4;
            for (int i=0; i<4; i++)
            {
                var row = Image("PlayerRow" + (i+1), panel.transform, new Vector2(0,100-i*94),
                    new Vector2(1100,80), new Color(.105f,.115f,.105f));
                var rank = Text("Rank", row.transform, "", new Vector2(-480,0), new Vector2(140,70), 36);
                var playerName = Text("PlayerName", row.transform, "", new Vector2(-130,0), new Vector2(450,70), 36, TextAlignmentOptions.MidlineLeft);
                var count = Text("KimchiCount", row.transform, "", new Vector2(390,0), new Vector2(260,70), 36, TextAlignmentOptions.MidlineRight);
                var item = rows.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("root").objectReferenceValue = row.gameObject;
                item.FindPropertyRelative("rank").objectReferenceValue = rank;
                item.FindPropertyRelative("playerName").objectReferenceValue = playerName;
                item.FindPropertyRelative("kimchiCount").objectReferenceValue = count;
                item.FindPropertyRelative("background").objectReferenceValue = row;
                row.gameObject.SetActive(false);
            }
            var status = Text("ResultStatus", panel.transform, "인게임에서 시작해 모든 라운드를 완료하면 결과가 표시됩니다.",
                new Vector2(0,-290), new Vector2(1120,86), 26);
            settings.FindProperty("status").objectReferenceValue = status;
            var buttonImage = Image("PlayAgain", panel.transform, new Vector2(0,-385), new Vector2(400,78), new Color(.35f,.62f,.1f));
            buttonImage.raycastTarget = true;
            var button = buttonImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = buttonImage;
            var label = Text("Label", button.transform, "게임 시작", Vector2.zero, new Vector2(380,70), 32);
            label.color = new Color(.02f,.04f,.015f); label.fontStyle = FontStyles.Bold;
            settings.FindProperty("playAgainButton").objectReferenceValue = button;
            settings.FindProperty("playAgainLabel").objectReferenceValue = label;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var events = new GameObject("ResultEventSystem");
            events.SetActive(false);
            events.transform.SetParent(root.transform, false);
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            events.SetActive(true); // Parent remains inactive until instantiated by the user.
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Path);
            if (prefab == null) throw new Exception("Prefab save failed.");
            // Set the persistent asset's default active state without enabling the preview hierarchy.
            var assetSettings = new SerializedObject(prefab);
            assetSettings.FindProperty("m_IsActive").boolValue = true;
            assetSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(prefab);
            return new { prefab = Path, rows = 4, sceneChanged = false, font = font.name };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
